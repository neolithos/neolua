using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace Neo.IronLua
{
  #region -- class Parser -------------------------------------------------------------

  ///////////////////////////////////////////////////////////////////////////////
  /// <summary></summary>
  internal static partial class Parser
  {
    private const string csReturnLabel = "#return";
    private const string csBreakLabel = "#break";
    private const string csContinueLabel = "#continue";
    private const string csEnv = "_G";
    private const string csArgListP = "#arglistP";
    private const string csArgList = "#arglist";
    private const string csClr = "clr";

    #region -- class Scope ------------------------------------------------------------

    ///////////////////////////////////////////////////////////////////////////////
    /// <summary>Scope, that builds a block.</summary>
    private class Scope
    {
      private Scope parent;   // Parent-Scope, that is accessable
      private Dictionary<string, Expression> scopeVariables = null; // local declared variables or const definitions
      private List<Expression> block = new List<Expression>();  // Instructions in the current block
      private bool lBlockGenerated = false; // Is the block generated

      #region -- Ctor -----------------------------------------------------------------

      /// <summary>Creates the scope</summary>
      /// <param name="parent">parent-scope</param>
      public Scope(Scope parent)
      {
        this.parent = parent;
      } // ctor

      #endregion

      #region -- LookupParameter ------------------------------------------------------

      /// <summary>Creates a new variable in the current scope.</summary>
      /// <param name="type">Type of the variable</param>
      /// <param name="sName">Name of the variable</param>
      /// <returns>The expression that represents the access to the variable.</returns>
      public ParameterExpression RegisterVariable(Type type, string sName)
      {
        ParameterExpression expr = Expression.Variable(type, sName);
        RegisterVariableOrConst(sName, expr);
        return expr;
      } // proc RegisterParameter

      public void RegisterConst(string sName, ConstantExpression expr)
      {
        RegisterVariableOrConst(sName, expr);
      } // proc RegisterConst

      private void RegisterVariableOrConst(string sName, Expression expr)
      {
        if (scopeVariables == null)
          scopeVariables = new Dictionary<string, Expression>();
        scopeVariables[sName] = expr;
      } // proc EnsureVariables

      /// <summary>Looks up the variable/parameter/const through the scopes.</summary>
      /// <param name="sName">Name of the variable</param>
      /// <param name="lLocalOnly"></param>
      /// <returns>The access-expression for the variable, parameter or <c>null</c>, if it is not registered.</returns>
      public virtual Expression LookupExpression(string sName, bool lLocalOnly = false)
      {
        // Lookup the current scope
        Expression p;
        if (scopeVariables != null && scopeVariables.TryGetValue(sName, out p))
          return p;

        if (parent != null && !lLocalOnly) // lookup the parent scope
          return parent.LookupExpression(sName);
        else
          return null;
      } // func LookupParameter

      #endregion

      #region -- LookupLabel ----------------------------------------------------------

      /// <summary>Create a named label or look for an existing</summary>
      /// <param name="type">Returntype for the label</param>
      /// <param name="sName">Name for the label</param>
      /// <returns>Labeltarget</returns>
      public virtual LabelTarget LookupLabel(Type type, string sName)
      {
        return parent.LookupLabel(type, sName);
      } // func LookupLabel

      #endregion

      #region -- Expression Block -----------------------------------------------------

      [Conditional("DEBUG")]
      private void CheckBlockGenerated()
      {
        if (lBlockGenerated)
          throw new InvalidOperationException();
      } // proc CheckBlockGenerated

      public void InsertExpression(int iIndex, Expression expr)
      {
        CheckBlockGenerated();
        block.Insert(iIndex, expr);
      } // proc AddExpression

      public void AddExpression(Expression expr)
      {
        CheckBlockGenerated();
        block.Add(expr);
      } // proc AddExpression

      /// <summary>Close the expression block and return it.</summary>
      public virtual Expression ExpressionBlock
      {
        get
        {
          CheckBlockGenerated();
          lBlockGenerated = true;

          if (block.Count == 0)
            return Expression.Empty();
          else 
          {
            ParameterExpression[] variables = Variables;
            if (variables.Length == 0)
              return Expression.Block(block);
            else if (ExpressionBlockType == null)
              return Expression.Block(variables, block);
            else
              return Expression.Block(ExpressionBlockType, variables, block);
          }
        }
      } // func ExpressionBlock

      public Type ExpressionBlockType { get; set; }
      public bool ExistExpressions { get { return block.Count > 0; } }

      #endregion

      /// <summary>Access to the Lua-Binders</summary>
      public virtual Lua Runtime { get { return parent.Runtime; } }
      /// <summary>Emit-Debug-Information</summary>
      public virtual bool EmitDebug { get { return parent.EmitDebug; } }
      /// <summary>Return type of the current Lambda-Scope</summary>
      public virtual Type ReturnType { get { return parent.ReturnType; } }
      /// <summary></summary>
      public ParameterExpression[] Variables { get { return scopeVariables == null ? new ParameterExpression[0] : (from v in scopeVariables.Values where v is ParameterExpression select (ParameterExpression)v).ToArray(); } }
    } // class Scope

    #endregion

    #region -- class LoopScope --------------------------------------------------------

    ///////////////////////////////////////////////////////////////////////////////
    /// <summary>Scope that represents the loop content.</summary>
    private class LoopScope : Scope
    {
      private LabelTarget continueLabel = Expression.Label(csContinueLabel);
      private LabelTarget breakLabel = Expression.Label(csBreakLabel);

      #region -- Ctor -----------------------------------------------------------------

      /// <summary>Scope that represents the loop content.</summary>
      /// <param name="parent"></param>
      public LoopScope(Scope parent)
        : base(parent)
      {
      } // ctor

      #endregion

      #region -- LookupLabel ----------------------------------------------------------

      /// <summary>Creates or lookup the label</summary>
      /// <param name="type">Type of the label. Is ignored on std. labels.</param>
      /// <param name="sName">Name of the label.</param>
      /// <returns>LabelTarget</returns>
      public override LabelTarget LookupLabel(Type type, string sName)
      {
        if (sName == csBreakLabel)
          return breakLabel;
        else if (sName == csContinueLabel)
          return continueLabel;
        else
          return base.LookupLabel(type, sName);
      } // func LookupLabel

      #endregion

      /// <summary>Default break position.</summary>
      public LabelTarget BreakLabel { get { return breakLabel; } }
      /// <summary>Default continue position.</summary>
      public LabelTarget ContinueLabel { get { return continueLabel; } }
    } // class LambdaScope

    #endregion

    #region -- class LambdaScope ------------------------------------------------------

    ///////////////////////////////////////////////////////////////////////////////
    /// <summary>Lambda-Scope with labels and parameters.</summary>
    private class LambdaScope : Scope
    {
#if DEBUG
      private bool lReturnLabelUsed = false;
#endif
      private LabelTarget returnLabel;
      private Expression returnDefaultValue;
      private Dictionary<string, LabelTarget> labels = null;
      private Dictionary<string, ParameterExpression> parameters = new Dictionary<string, ParameterExpression>();

      #region -- Ctor -----------------------------------------------------------------

      /// <summary>Creates the lambda-scope, that manages labels and arguments.</summary>
      /// <param name="parent"></param>
      /// <param name="returnType"></param>
      /// <param name="returnDefaultValue"></param>
      public LambdaScope(Scope parent, Type returnType = null, Expression returnDefaultValue = null)
        : base(parent)
      {
        if (returnType != null)
          ResetReturnLabel(returnType, returnDefaultValue);
      } // ctor

      #endregion

      #region -- RegisterParameter, LookupParameter -----------------------------------

      /// <summary>Registers arguments for the function.</summary>
      /// <param name="type">Type of the argument</param>
      /// <param name="sName">Name of the argument</param>
      /// <returns>Access to the argument</returns>
      public ParameterExpression RegisterParameter(Type type, string sName)
      {
        return parameters[sName] = Expression.Parameter(type, sName);
      } // proc RegisterParameter

      /// <summary>Lookup the variables and arguments.</summary>
      /// <param name="sName">Name of the parameter/variable.</param>
      /// <param name="lLocalOnly"></param>
      /// <returns></returns>
      public override Expression LookupExpression(string sName, bool lLocalOnly = false)
      {
        ParameterExpression p;
        if (parameters.TryGetValue(sName, out p))
          return p;
        return base.LookupExpression(sName, lLocalOnly);
      } // func LookupParameter

      #endregion

      #region -- LookupLabel ----------------------------------------------------------

      public override LabelTarget LookupLabel(Type type, string sName)
      {
        if (sName == csReturnLabel)
          return returnLabel;
        if (labels == null)
          labels = new Dictionary<string, LabelTarget>();
        if (type == null)
          type = typeof(void);

        // Lookup the label
        LabelTarget l;
        if (labels.TryGetValue(sName, out l))
          return l;

        // Create the label, if it is not internal
        if (sName[0] == '#')
          throw new ArgumentException("Internal label does not exist.");

        return labels[sName] = Expression.Label(type, sName);
      } // func LookupLabel

      #endregion

      public void ResetReturnLabel(Type returnType, Expression returnDefaultValue)
      {
#if DEBUG
        if (lReturnLabelUsed)
          throw new InvalidOperationException("Reset is not allowed after expressions added.");
#endif
        this.returnLabel = Expression.Label(returnType, csReturnLabel);
        this.returnDefaultValue = returnDefaultValue;
      } // proc ResetReturnLabel

      public override Expression ExpressionBlock
      {
        get
        {
          AddExpression(Expression.Label(returnLabel, returnDefaultValue == null ? Expression.Default(returnLabel.Type) : returnDefaultValue));
          return base.ExpressionBlock;
        }
      } // prop ExpressionBlock

      public LabelTarget ReturnLabel
      {
        get
        {
#if DEBUG
          lReturnLabelUsed = true;
#endif
          return returnLabel;
        }
      } // prop ReturnLabel
      
      public override Type ReturnType { get { return returnLabel.Type; } }
    } // class LambdaScope

    #endregion

    #region -- class GlobalScope ------------------------------------------------------

    ///////////////////////////////////////////////////////////////////////////////
    /// <summary>Global parse-scope.</summary>
    private class GlobalScope : LambdaScope
    {
      private Lua runtime;
      private bool lDebug;

      /// <summary>Global parse-scope</summary>
      /// <param name="runtime">Runtime and binder of the global scope.</param>
      /// <param name="lDebug"></param>
      /// <param name="returnType"></param>
      /// <param name="returnDefaultValue"></param>
      public GlobalScope(Lua runtime, bool lDebug, Type returnType, Expression returnDefaultValue)
        : base(null, returnType, returnDefaultValue)
      {
        this.runtime = runtime;
        this.lDebug = lDebug;
      } // ctor

      /// <summary>Access to the binders</summary>
      public override Lua Runtime { get { return runtime; } }
      /// <summary>Emit-Debug-Information</summary>
      public override bool EmitDebug { get { return lDebug; } }
    } // class GlobalScope

    #endregion

    #region -- class PrefixMemberInfo -------------------------------------------------

    ///////////////////////////////////////////////////////////////////////////////
    /// <summary>Mini-Parse-Tree for resolve of prefix expressions</summary>
    private class PrefixMemberInfo
    {
      public PrefixMemberInfo(Token position, Expression instance, string sMember, Expression[] indices, Expression[] arguments)
      {
        this.Position = position;
        this.Instance = instance;
        this.Member = sMember;
        this.Indices = indices;
        this.Arguments = arguments;
      } // ctor

      public Expression GenerateSet(Scope scope, Expression exprToSet)
      {
        Expression expr;
        if (Instance != null && Member == null && Indices != null && Arguments == null)
        {
          // Assign to an index
          Expression[] r = new Expression[Indices.Length + 2];
          r[0] = ToTypeExpression(Instance, typeof(object));
          for (int i = 0; i < Indices.Length; i++)
            r[i + 1] = ToTypeExpression(Indices[i], typeof(object));
          r[r.Length - 1] = ToTypeExpression(exprToSet, typeof(object));
          expr = Expression.Dynamic(scope.Runtime.GetSetIndexMember(new CallInfo(Indices.Length)), typeof(object), r);
        }
        else if (Instance != null && Member != null && Indices == null && Arguments == null)
        {
          // Assign the value to a member
          if (MethodMember)
            expr = Expression.Call(
              ToTypeExpression(Instance, typeof(LuaTable)),
              Lua.TableSetMethodInfo,
              Expression.Constant(Member, typeof(string)),
              ToTypeExpression(exprToSet, typeof(Delegate)));
          else
            expr = Expression.Dynamic(scope.Runtime.GetSetMemberBinder(Member), typeof(object), Instance, ToTypeExpression(exprToSet, typeof(object)));
        }
        else if (Instance != null && Member == null && Indices == null && Arguments == null && Instance is ParameterExpression)
        {
          // Assign the value to a variable
          expr = Expression.Assign(Instance, ToTypeExpression(exprToSet, Instance.Type));
        }
        else
          throw ParseError(Position, Properties.Resources.rsParseExpressionNotAssignable);

        return expr;
      } // func GenerateSet

      public Expression GenerateGet(Scope scope, bool lNeedResult)
      {
        if (Instance != null && Member == null && Indices != null && Arguments == null)
        {
          // Create the arguments for the index assign
          Expression[] r = new Expression[Indices.Length + 1];
          r[0] = ToTypeExpression(Instance, typeof(object)); // Array instance
          for (int i = 0; i < Indices.Length - 1; i++)
            r[i + 1] = ToTypeExpression(Indices[i], typeof(object)); // Copy the index values
          if (Indices.Length > 0)
          {
            // First the arguments are pushed on the stack, and later comes the call, so we wrap the last parameter
            r[r.Length - 1] = WrapDebugInfo(scope.EmitDebug, true, Position, Position, Indices[Indices.Length - 1]); // Let the type as it is
          }

          Instance = Expression.Dynamic(scope.Runtime.GetGetIndexMember(new CallInfo(Indices.Length)), typeof(object), r);
          Indices = null;
        }
        else if (Instance != null && Member != null && Indices == null && Arguments == null)
        {
          // Convert the member to an instance
          Instance = WrapDebugInfo(scope.EmitDebug, true, Position, Position, Instance);
          Instance = Expression.Dynamic(scope.Runtime.GetGetMemberBinder(Member), typeof(object), ToTypeExpression(Instance, typeof(object)));
          Member = null;
          MethodMember = false;
        }
        else if (Instance != null && Member == null && Indices == null && Arguments == null)
        {
          // Nothing to todo, we have already an instance
        }
        else if (Instance != null && Indices == null && Arguments != null)
        {
          Expression[] r = new Expression[Arguments.Length + 1];
          r[0] = ToTypeExpression(Instance, typeof(object)); // Delegate

          // All arguments are converted to an object, except of the last one (rollup)
          for (int i = 0; i < Arguments.Length - 1; i++)
            r[i + 1] = ToTypeExpression(Arguments[i], typeof(object)); // Convert the arguments 
          if (Arguments.Length > 0)
          {
            // First the arguments are pushed on the stack, and later comes the call, so we wrap the last parameter
            r[r.Length - 1] = WrapDebugInfo(scope.EmitDebug, true, Position, Position, Arguments[Arguments.Length - 1]); // Let the type as it is
          }
          else
            r[0] = WrapDebugInfo(scope.EmitDebug, true, Position, Position, r[0]);

          // Functions always return an array objects
          Instance =
              Expression.Dynamic(Member == null ?
                scope.Runtime.GetInvokeBinder(new CallInfo(Arguments.Length)) :
                scope.Runtime.GetInvokeMemberBinder(Member, new CallInfo(Arguments.Length)), typeof(object), r
              );
          if (lNeedResult)
            Instance = Expression.Dynamic(Lua.FunctionResultBinder, typeof(LuaResult), Instance);

          Member = null;
          MethodMember = false;
          Arguments = null;
        }
        else
          throw ParseError(Position, Properties.Resources.rsParseExpressionNoResult);

        return Instance;
      } // func GenerateGet

      public void SetMember(Token tMember, bool lMethod)
      {
        Position = tMember;
        MethodMember = lMethod;
        Member = tMember.Value;
      } // proc SetMember

      public Token Position { get; set; }
      public Expression Instance { get; private set; }
      public string Member { get; private set; }
      public bool MethodMember { get; private set; }
      public Expression[] Indices { get; set; }
      public Expression[] Arguments { get; set; }
    } // class PrefixMemberInfo

    #endregion

    #region -- Parse Chunk, Block -----------------------------------------------------

    /// <summary>Parses the chunk to an function.</summary>
    /// <param name="runtime">Binder</param>
    /// <param name="lDebug">Compile the script with debug information.</param>
    /// <param name="lHasEnvironment">Creates the _G parameter.</param>
    /// <param name="code">Lexer for the code.</param>
    /// <param name="typeDelegate">Type for the delegate. <c>null</c>, for an automatic type</param>
    /// <param name="returnType">Defines the return type of the chunk.</param>
    /// <param name="args">Arguments of the function.</param>
    /// <returns>Expression-Tree for the code.</returns>
    public static LambdaExpression ParseChunk(Lua runtime, bool lDebug, bool lHasEnvironment, LuaLexer code, Type typeDelegate, Type returnType, IEnumerable<KeyValuePair<string, Type>> args)
    {
      List<ParameterExpression> parameters = new List<ParameterExpression>();
      if (returnType == null)
        returnType = typeof(LuaResult);
      var globalScope = new GlobalScope(runtime, lDebug, returnType, returnType == typeof(LuaResult) ? Expression.Property(null, Lua.ResultEmptyPropertyInfo) : null);

      // Registers the global LuaTable
      if (lHasEnvironment)
        parameters.Add(globalScope.RegisterParameter(typeof(LuaGlobal), csEnv));

      if (args != null)
      {
        foreach (var c in args)
          parameters.Add(globalScope.RegisterParameter(c.Value, c.Key)); // Add alle arguments
      }

      // Get the first token
      if (code.Current == null)
        code.Next();

      // Get the name for the chunk and clean it from all unwanted chars
      string sChunkName = runtime.CreateEmptyChunk(Path.GetFileNameWithoutExtension(code.Current.Start.FileName)).Name;

      // Create the block
      ParseBlock(globalScope, code);

      if (code.Current.Typ != LuaToken.Eof)
        throw ParseError(code.Current, Properties.Resources.rsParseEof);

      // Create the function
      return typeDelegate == null ?
        Expression.Lambda(globalScope.ExpressionBlock, sChunkName, parameters) :
        Expression.Lambda(typeDelegate, globalScope.ExpressionBlock, sChunkName, parameters);
    } // func ParseChunk

    private static void ParseBlock(Scope scope, LuaLexer code)
    {
      // Lese die Statement
      bool lLoop = true;
      while (lLoop)
      {
        switch (code.Current.Typ)
        {
          case LuaToken.Eof: // End of file
            lLoop = false;
            break;

          case LuaToken.KwReturn: //  The return-statement is only allowed on the end of a scope
            ParseReturn(scope, code);
            break;

          case LuaToken.KwBreak: // The break-statement is only allowed on the end of a scope
            ParseBreak(scope, code);
            lLoop = false;
            break;

          case LuaToken.Semicolon: // End of statement => ignore
            code.Next();
            break;

          default:
            if (scope.EmitDebug) // Start every statement with a debug point
              scope.AddExpression(GetDebugInfo(code.Current, code.Current));

            if (!ParseStatement(scope, code)) // Parse normal statements
              lLoop = false;
            break;
        }
      }
      if (scope.EmitDebug)
        scope.AddExpression(Expression.ClearDebugInfo(code.Current.Start.Document)); // Clear debug info
    } // func ParseBlock

    private static void ParseReturn(Scope scope, LuaLexer code)
    {
      // eat return
      code.Next();

      // Build the return expression for all parameters
      Expression exprReturnValue;

      if (IsExpressionStart(code)) // there is a return value
      {
        if (scope.ReturnType == typeof(LuaResult))
        {
          Expression[] exprs = ParseExpressionList(scope, code).ToArray();
          if (exprs.Length == 1 && exprs[0].Type == typeof(LuaResult))
            exprReturnValue = exprs[0];
          else
            exprReturnValue = Expression.New(Lua.ResultConstructorInfo,
              Expression.NewArrayInit(typeof(object),
                from c in exprs select Expression.Convert(c, typeof(object))
                )
              );
        }
        else if (scope.ReturnType.IsArray)
        {
          Type typeArray = scope.ReturnType.GetElementType();
          exprReturnValue = Expression.NewArrayInit(
            typeArray,
            from c in ParseExpressionList(scope, code) select ToTypeExpression(c, typeArray));
        }
        else
        {
          List<Expression> exprList = new List<Expression>(ParseExpressionList(scope, code));

          if (exprList.Count == 1)
            exprReturnValue = ToTypeExpression(exprList[0], scope.ReturnType);
          else
          {
            ParameterExpression tmpVar = Expression.Variable(scope.ReturnType);
            exprList[0] = Expression.Assign(tmpVar, ToTypeExpression(exprList[0], scope.ReturnType));
            exprList.Add(tmpVar);
            exprReturnValue = Expression.Block(scope.ReturnType, new ParameterExpression[] { tmpVar }, exprList);
          }
        }
      }
      else // use the default-value
      {
        if (scope.ReturnType == typeof(LuaResult))
          exprReturnValue = Expression.Property(null, Lua.ResultEmptyPropertyInfo);
        else if (scope.ReturnType.IsArray)
          exprReturnValue = Expression.NewArrayInit(scope.ReturnType.GetElementType());
        else
          exprReturnValue = Expression.Default(scope.ReturnType);
      }

      if (code.Current.Typ == LuaToken.Semicolon)
        code.Next();
      
      scope.AddExpression(Expression.Goto(scope.LookupLabel(scope.ReturnType, csReturnLabel), exprReturnValue));
    } // func ParseReturn

    private static bool IsExpressionStart(LuaLexer code)
    {
      return code.Current.Typ == LuaToken.BracketOpen ||
        code.Current.Typ == LuaToken.Identifier ||
        code.Current.Typ == LuaToken.DotDotDot ||
        code.Current.Typ == LuaToken.String ||
        code.Current.Typ == LuaToken.Number ||
        code.Current.Typ == LuaToken.HexNumber ||
        code.Current.Typ == LuaToken.KwTrue ||
        code.Current.Typ == LuaToken.KwFalse ||
        code.Current.Typ == LuaToken.KwNil ||
        code.Current.Typ == LuaToken.BracketCurlyOpen ||
        code.Current.Typ == LuaToken.Minus ||
        code.Current.Typ == LuaToken.KwFunction ||
        code.Current.Typ == LuaToken.KwCast;
    } // func IsExpressionStart

    #endregion

    #region -- Parse Statement --------------------------------------------------------

    private static bool ParseStatement(Scope scope, LuaLexer code)
    {
      switch (code.Current.Typ)
      {
        case LuaToken.Identifier: // Expression
        case LuaToken.DotDotDot:
        case LuaToken.BracketOpen:
        case LuaToken.String:
        case LuaToken.Number:
        case LuaToken.HexNumber:
        case LuaToken.KwFalse:
        case LuaToken.KwTrue:
        case LuaToken.KwNil:
        case LuaToken.BracketCurlyOpen:
        case LuaToken.Minus:
        case LuaToken.KwCast:
          ParseExpressionStatement(scope, code, false);
          return true;

        case LuaToken.ColonColon: // Start of a label
          ParseLabel(scope, code);
          return true;

        case LuaToken.KwGoto:
          ParseGoto(scope, code);
          return true;

        case LuaToken.KwDo:
          ParseDoLoop(scope, code);
          return true;

        case LuaToken.KwWhile:
          ParseWhileLoop(scope, code);
          return true;

        case LuaToken.KwRepeat:
          ParseRepeatLoop(scope, code);
          return true;

        case LuaToken.KwIf:
          ParseIfStatement(scope, code);
          return true;

        case LuaToken.KwFor:
          ParseForLoop(scope, code);
          return true;

        case LuaToken.KwForEach:
          ParseForEachLoop(scope, code);
          return true;

        case LuaToken.KwFunction:
          ParseFunction(scope, code, false);
          return true;

        case LuaToken.KwLocal:
          code.Next();
          if (code.Current.Typ == LuaToken.KwFunction)
            ParseFunction(scope, code, true);
          else
            ParseExpressionStatement(scope, code, true);
          return true;
        case LuaToken.KwConst:
          code.Next();
          ParseConst(scope, code);
          return true;

        case LuaToken.InvalidString:
          throw ParseError(code.Current, Properties.Resources.rsParseInvalidString);
        case LuaToken.InvalidComment:
          throw ParseError(code.Current, Properties.Resources.rsParseInvalidComment);
        case LuaToken.InvalidChar:
          throw ParseError(code.Current, Properties.Resources.rsParseInvalidChar);

        default:
          return false;
      }
    }  // func ParseStatement

    private static void ParseExpressionStatement(Scope scope, LuaLexer code, bool lLocal)
    {
      List<PrefixMemberInfo> prefixes = new List<PrefixMemberInfo>();

      // parse the assgiee list (var0, var1, var2, ...)
      while (true)
      {
        if (lLocal) // parse local variables
        {
          Token tVar;
          Type typeVar;
          ParseIdentifierAndType(code, out tVar, out typeVar);
          ParameterExpression exprVar = scope.LookupExpression(tVar.Value, true) as ParameterExpression;
          if (exprVar == null)
            exprVar = scope.RegisterVariable(typeVar, tVar.Value);
          else if (exprVar.Type != typeVar)
            throw ParseError(tVar, Properties.Resources.rsParseTypeRedef);

          prefixes.Add(new PrefixMemberInfo(tVar, exprVar , null, null, null));
        }
        else // parse a assignee
        {
          // parse as a prefix
          prefixes.Add(ParsePrefix(scope, code));
        }

        // is there another prefix
        if (code.Current.Typ == LuaToken.Comma)
          code.Next();
        else
          break;
      }

      // Optional assign
      if (code.Current.Typ == LuaToken.Assign)
      {
        code.Next();

        // parse all expressions
        IEnumerator<Expression> expr = ParseExpressionList(scope, code).GetEnumerator();
        expr.MoveNext();

        if (prefixes.Count == 1) // one expression, one variable?
        {
          scope.AddExpression(
            prefixes[0].GenerateSet(scope,
              expr.Current != null ?
              ToTypeExpression(expr.Current) :
              Expression.Constant(null, typeof(object))
            )
          );
        }
        else if (expr.Current == null) // No expression, assign null
        {
          for (int i = 0; i < prefixes.Count; i++)
            scope.AddExpression(prefixes[i].GenerateSet(scope, Expression.Constant(null, typeof(object))));
        }
        else // assign on an unknown number of expressions
        {
          int iPrefix = 0;
          Expression l = expr.Current;
          Expression c;
          ParameterExpression v = null;

          while (true)
          {
            // parse the next expression
            if (expr.MoveNext()) // there should be more, a normal assign
            {
              c = expr.Current;

              scope.AddExpression(prefixes[iPrefix++].GenerateSet(scope,
                 l != null ?
                 ToTypeExpression(l) :
                 Expression.Constant(null, typeof(object))));
            }
            else // it was the last expression
            {
              // is the last expression a function
              if (l.Type == typeof(LuaResult))
              {
                v = scope.RegisterVariable(typeof(LuaResult), "#tmp");
                scope.AddExpression(Expression.Assign(v, l));
                scope.AddExpression(prefixes[iPrefix++].GenerateSet(scope, GetResultExpression(v, 0)));
              }
              else
                scope.AddExpression(prefixes[iPrefix++].GenerateSet(scope, ToTypeExpression(l)));
              break;
            }

            l = c;

            if (iPrefix >= prefixes.Count)
            {
              scope.AddExpression(c);
              break;
            }
          }

          // assign the rest of the result-array
          if (v != null)
          {
            int iLastIndex = 1;

            while (iPrefix < prefixes.Count)
            {
              scope.AddExpression(prefixes[iPrefix].GenerateSet(scope, GetResultExpression(v, iLastIndex)));
              iPrefix++;
              iLastIndex++;
            }
          }
        }

        // Führe die restlichen Expressions aus
        while (expr.MoveNext())
          scope.AddExpression(expr.Current);
      }
      else
      {
        for (int i = 0; i < prefixes.Count; i++)
          scope.AddExpression(prefixes[i].GenerateGet(scope, false));
      }
    } // proc ParseExpressionStatement

    private static void ParseIfStatement(Scope scope, LuaLexer code)
    {
      // if expr then block { elseif expr then block } [ else block ] end
      FetchToken(LuaToken.KwIf, code);
      var expr = ToBooleanExpression(ParseExpression(scope, code, scope.EmitDebug));
      FetchToken(LuaToken.KwThen, code);

      scope.AddExpression(Expression.IfThenElse(expr, ParseIfElseBlock(scope, code), ParseElseStatement(scope, code)));
    } // proc ParseIfStatement

    private static Expression ParseElseStatement(Scope scope, LuaLexer code)
    {
      if (code.Current.Typ == LuaToken.KwElseif)
      {
        code.Next();
        var expr = ToBooleanExpression(ParseExpression(scope, code, scope.EmitDebug));
        FetchToken(LuaToken.KwThen, code);

        return Expression.IfThenElse(expr, ParseIfElseBlock(scope, code), ParseElseStatement(scope, code));
      }
      else if (code.Current.Typ == LuaToken.KwElse)
      {
        code.Next();
        var block = ParseIfElseBlock(scope, code);
        FetchToken(LuaToken.KwEnd, code);
        return block;
      }
      else if (code.Current.Typ == LuaToken.KwEnd)
      {
        code.Next();
        return Expression.Empty();
      }
      else
        throw ParseError(code.Current, Properties.Resources.rsParseUnexpectedTokenElse);
    } // func ParseElseStatement

    private static Expression ParseIfElseBlock(Scope parent, LuaLexer code)
    {
      Scope scope = new Scope(parent);
      ParseBlock(scope, code);
      return scope.ExpressionBlock;
    } // func ParseIfElseBlock

    private static void ParseConst(Scope scope, LuaLexer code)
    {
      // const ::= variable '=' expr

      Token tVarName;
      Type typeVar;
      ParseIdentifierAndType(code, out tVarName, out typeVar);
      FetchToken(LuaToken.Assign, code);

      Expression exprConst = ParseExpression(scope, code, false); // No Debug-Emits
      if (typeVar != typeof(object))
        exprConst = ToTypeExpression(exprConst, typeVar);

      // Try to eval the statement
      if (exprConst.Type == typeof(object) || exprConst.Type == typeof(LuaResult)) // dynamic calls, no constant possible
        throw ParseError(tVarName, Properties.Resources.rsConstExpressionNeeded);
      else
        try
        {
          object r = EvaluateExpression(exprConst);
          if (r == null) // Eval via compile
          {
            Type typeFunc = Expression.GetFuncType(exprConst.Type);
            LambdaExpression exprEval = Expression.Lambda(typeFunc, exprConst);
            Delegate dlg = exprEval.Compile();
            r = dlg.DynamicInvoke();
          }
          scope.RegisterConst(tVarName.Value, Expression.Constant(r, exprConst.Type));
        }
        catch (Exception e)
        {
          throw ParseError(tVarName, String.Format(Properties.Resources.rsConstExpressionEvalError, e.Message));
        }
    } // func ParseConst

    #endregion

    #region -- Evaluate Expression ----------------------------------------------------

    private static object EvaluateExpression(Expression expr)
    {
      if (expr is ConstantExpression)
        return EvaluateConstantExpression((ConstantExpression)expr);
      else if (expr is UnaryExpression)
        return EvaluateUnaryExpression((UnaryExpression)expr);
      else if (expr is BinaryExpression)
        return EvaluateBinaryExpression((BinaryExpression)expr);
      else
        return null;
    } // func EvaluateExpresion

    private static object EvaluateConstantExpression(ConstantExpression expr)
    {
      if (expr.Type == typeof(object))
        return null;
      return Lua.RtConvert(expr.Value, expr.Type);
    } // func EvaluateConstantExpression

    private static object EvaluateUnaryExpression(UnaryExpression expr)
    {
      object r = EvaluateExpression(expr.Operand);
      if (r == null)
        return null;

      switch (expr.NodeType)
      {
        case ExpressionType.Convert:
        case ExpressionType.ConvertChecked:
          return Lua.RtConvert(r, expr.Type);
        default:
          return null;
      }
    } // func EvaluateConstantExpression

    private static object EvaluateBinaryExpression(BinaryExpression expr)
    {
      return null;
    } // func EvaluateConstantExpression

    #endregion

    #region -- Parse Prefix, Suffix ---------------------------------------------------

    private static PrefixMemberInfo ParsePrefix(Scope scope, LuaLexer code)
    {
      // prefix ::= Identifier suffix_opt |  '(' exp ')' suffix | literal | tablector

      Token tStart = code.Current;
      PrefixMemberInfo info;
      switch (tStart.Typ)
      {
        case LuaToken.BracketOpen: // Parse eine Expression
          code.Next();
          var expr = ParseExpression(scope, code, scope.EmitDebug);
          FetchToken(LuaToken.BracketClose, code);

          info = new PrefixMemberInfo(tStart, ToTypeExpression(expr), null, null, null);
          break;

        case LuaToken.DotDotDot:
        case LuaToken.Identifier:
          var t = code.Current;
          if (t.Value == csClr) // clr is a special package, that always exists
          {
            code.Next();
            info = new PrefixMemberInfo(tStart, Expression.Constant(LuaGlobal.Clr, typeof(IDynamicMetaObjectProvider)), null, null, null);
          }
          else
          {
            var p = scope.LookupExpression(t.Typ == LuaToken.DotDotDot ? csArgList : t.Value);
            if (t.Typ == LuaToken.DotDotDot && p == null)
              throw ParseError(t, Properties.Resources.rsParseNoArgList);
            code.Next();
            if (p == null) // No local variable found
              info = new PrefixMemberInfo(tStart, scope.LookupExpression(csEnv), t.Value, null, null);
            else
              info = new PrefixMemberInfo(tStart, p, null, null, null);
          }
          break;

        case LuaToken.String: // Literal String
          info = new PrefixMemberInfo(tStart, Expression.Constant(FetchToken(LuaToken.String, code).Value, typeof(string)), null, null, null);
          break;

        case LuaToken.Number: // Literal Zahl
          info = new PrefixMemberInfo(tStart, ParseNumber(FetchToken(LuaToken.Number, code)), null, null, null);
          break;

        case LuaToken.HexNumber: // Literal HexZahl
          info = new PrefixMemberInfo(tStart, ParseHexNumber(FetchToken(LuaToken.HexNumber, code)), null, null, null);
          break;

        case LuaToken.KwTrue: // Literal TRUE
          code.Next();
          info = new PrefixMemberInfo(tStart, Expression.Constant(true, typeof(bool)), null, null, null);
          break;

        case LuaToken.KwFalse: // Literal FALSE
          code.Next();
          info = new PrefixMemberInfo(tStart, Expression.Constant(false, typeof(bool)), null, null, null);
          break;

        case LuaToken.KwNil: // Literal NIL
          code.Next();
          info = new PrefixMemberInfo(tStart, Expression.Constant(null, typeof(object)), null, null, null);
          break;

        case LuaToken.BracketCurlyOpen: // tablector
          info = new PrefixMemberInfo(tStart, ParseTableConstructor(scope, code), null, null, null);
          break;

        case LuaToken.KwFunction: // Function definition
          code.Next();
          info = new PrefixMemberInfo(tStart, ParseLamdaDefinition(scope, code, null, false), null, null, null);
          break;

        default:
          throw ParseError(code.Current, Properties.Resources.rsParseUnexpectedTokenPrefix);
      }

      return ParseSuffix(scope, code, info);
    } // func ParsePrefix

    private static PrefixMemberInfo ParseSuffix(Scope scope, LuaLexer code, PrefixMemberInfo info)
    {
      // suffix_opt ::= [ suffix ]
      // suffix ::= { '[' exp ']'  | '.' Identifier | args | ':' Identifier args }
      // args ::= tablector | string | '(' explist ')'

      while (true)
      {
        switch (code.Current.Typ)
        {
          case LuaToken.BracketSquareOpen: // Index
            code.Next();
            info.GenerateGet(scope, true);
            info.Indices = ParseExpressionList(scope, code).ToArray();
            FetchToken(LuaToken.BracketSquareClose, code);
            break;

          case LuaToken.Dot: // Property of an class
            code.Next();
            info.GenerateGet(scope, true);
            info.SetMember(FetchToken(LuaToken.Identifier, code), false);
            break;

          case LuaToken.BracketOpen: // List of arguments
            info.GenerateGet(scope, true);
            info.Arguments = ParseArgumentList(scope, code);
            break;

          case LuaToken.BracketCurlyOpen: // LuaTable as an argument
            info.GenerateGet(scope, true);
            info.Arguments = new Expression[] { ParseTableConstructor(scope, code) };
            break;

          case LuaToken.String: // String as an argument
            info.GenerateGet(scope, true);
            info.Arguments = new Expression[] { Expression.Constant(FetchToken(LuaToken.String, code).Value, typeof(object)) };
            break;

          case LuaToken.Colon: // Methodenaufruf
            code.Next();

            // Lese den Namen um den Member zu belegen
            info.GenerateGet(scope, true);
            info.SetMember(FetchToken(LuaToken.Identifier, code), true);

            // Parse die Parameter
            switch (code.Current.Typ)
            {
              case LuaToken.BracketOpen: // Argumentenliste
                info.Arguments = ParseArgumentList(scope, code);
                break;

              case LuaToken.BracketCurlyOpen: // LuaTable als Argument
                info.Arguments = new Expression[] { ParseTableConstructor(scope, code) }; ;
                break;

              case LuaToken.String: // String als Argument
                info.Arguments = new Expression[] { Expression.Constant(FetchToken(LuaToken.String, code).Value, typeof(object)) }; ;
                break;
            }
            break;

          default:
            return info;
        }
      }
    } // func ParsePrefix

    private static Expression[] ParseArgumentList(Scope scope, LuaLexer code)
    {
      FetchToken(LuaToken.BracketOpen, code);

      // Es handelt sich um ein Delegate
      if (code.Current.Typ == LuaToken.BracketClose)
      {
        code.Next();
        return new Expression[0];
      }
      else
      {
        var args = ParseExpressionList(scope, code).ToArray();
        FetchToken(LuaToken.BracketClose, code);
        return args;
      }
    } // func ParseArgumentList

    #endregion

    #region -- Parse Numer, HexNumber -------------------------------------------------

    internal static Expression ParseNumber(Token t)
    {
      int i;
      double d;
      string sNumber = t.Value;
      if (String.IsNullOrEmpty(sNumber))
        return Expression.Constant(0, typeof(int));
      else if (Int32.TryParse(sNumber, NumberStyles.Integer, CultureInfo.InvariantCulture, out i))
        return Expression.Constant(i, typeof(int));
      else if (Double.TryParse(sNumber, NumberStyles.Float, CultureInfo.InvariantCulture, out d))
        return Expression.Constant(d, typeof(double));
      else
        throw ParseError(t, String.Format(Properties.Resources.rsParseConvertNumberError, sNumber));
    } // func ParseNumber

    internal static Expression ParseHexNumber(Token t)
    {
      int i;
      //double d;
      string sNumber = t.Value;

      if (String.IsNullOrEmpty(sNumber))
        return Expression.Constant(0, typeof(int));
      else
      {
        // remove the '0x'
        if (sNumber.Length > 2 && sNumber[0] == '0' && (sNumber[1] == 'x' || sNumber[1] == 'X'))
          sNumber = sNumber.Substring(2);

        // Convert the number as an integer
        if (Int32.TryParse(sNumber, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out i))
          return Expression.Constant(i, typeof(int));
        // Todo: Binary Exponents?
        //else if (Double.TryParse(sNumber, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out d))
        //  return Expression.Constant(d, typeof(Double));
        else
          throw ParseError(t, String.Format(Properties.Resources.rsParseConvertNumberError, sNumber));
      }
    } // func ParseHexNumber

    #endregion

    #region -- Parse Expressions ------------------------------------------------------

    private static IEnumerable<Expression> ParseExpressionList(Scope scope, LuaLexer code)
    {
      while (true)
      {
        yield return ParseExpression(scope, code, scope.EmitDebug);

        // Noch eine Expression
        if (code.Current.Typ == LuaToken.Comma)
          code.Next();
        else
          break;
      }
    } // func ParseExpressionList

    private static Expression ParseExpression(Scope scope, LuaLexer code, bool lDebug)
    {
      Token tStart = code.Current;
      bool lWrap = false;
      Expression expr = ParseExpression(scope, code, ref lWrap);
      if (lWrap && lDebug)
        return WrapDebugInfo(true, false, tStart, code.Current, expr);
      else
        return expr;
    } // func ParseExpression

    private static Expression ParseExpression(Scope scope, LuaLexer code, ref bool lWrap)
    {
      // exp ::= expOr
      return ParseExpressionOr(scope, code, ref lWrap);
    } // func ParseExpression

    private static Expression ParseExpressionOr(Scope scope, LuaLexer code, ref bool lWrap)
    {
      // expOr ::= expAnd { or expAnd}
      var expr = ParseExpressionAnd(scope, code, ref lWrap);

      while (code.Current.Typ == LuaToken.KwOr)
      {
        code.Next();

        Expression exprAnd = ParseExpressionAnd(scope, code, ref lWrap);
        Type typeAndResult = GetAndOrResultType(expr.Type, exprAnd.Type);

        // and gibt das erste argument zurück, wenn es true ist
        expr = Expression.Condition(
          ToBooleanExpression(expr),
          ToTypeExpression(expr, typeAndResult),
          ToTypeExpression(exprAnd, typeAndResult),
          typeAndResult);

        // Bitweises Or
        //expr = Expression.Dynamic(scope.Runtime.GetBinaryOperationBinder(ExpressionType.OrElse), typeof(object), expr, ParseExpressionAnd(scope, code));
      }

      return expr;
    } // func ParseExpressionOr


    private static Expression ParseExpressionAnd(Scope scope, LuaLexer code, ref bool lWrap)
    {
      // expAnd ::= expCmp { or expCmp}
      var expr = ParseExpressionCmd(scope, code, ref lWrap);

      while (code.Current.Typ == LuaToken.KwAnd)
      {
        code.Next();

        Expression exprOr = ParseExpressionCmd(scope, code, ref lWrap);
        Type typeOrResult = GetAndOrResultType(expr.Type, exprOr.Type);

        // and gibt das erste argument zurück, wenn es false ist
        expr = Expression.Condition(
          ToBooleanExpression(expr),
          ToTypeExpression(exprOr, typeOrResult),
          ToTypeExpression(expr, typeOrResult),
          typeOrResult);

        // Bitweises And
        //expr = Expression.Dynamic(scope.Runtime.GetBinaryOperationBinder(ExpressionType.And), typeof(object), expr, ParseExpressionCmd(scope, code));
      }

      return expr;
    } // func ParseExpressionAnd

    private static Expression ParseExpressionCmd(Scope scope, LuaLexer code, ref bool lWrap)
    {
      // expCmd ::= expCon { ( < | > | <= | >= | ~= | == ) expCon}
      Token tStart = code.Current;
      var expr = ParseExpressionCon(scope, code, ref lWrap);

      while (true)
      {
        ExpressionType typ;
        switch (code.Current.Typ)
        {
          case LuaToken.Lower:
            typ = ExpressionType.LessThan;
            break;
          case LuaToken.Greater:
            typ = ExpressionType.GreaterThan;
            break;
          case LuaToken.LowerEqual:
            typ = ExpressionType.LessThanOrEqual;
            break;
          case LuaToken.GreaterEqual:
            typ = ExpressionType.GreaterThanOrEqual;
            break;
          case LuaToken.NotEqual:
            typ = ExpressionType.NotEqual;
            break;
          case LuaToken.Equal:
            typ = ExpressionType.Equal;
            break;
          default:
            return expr;
        }
        code.Next();

        expr = BinaryOperationExpression(scope.Runtime, typ, ToTypeExpression(expr), ToTypeExpression(ParseExpressionCon(scope, code, ref lWrap)));
        lWrap |= true;
      }
    } // func ParseExpressionCmd

    private static Expression ParseExpressionCon(Scope scope, LuaLexer code, ref bool lWrap)
    {
      // expCon::= expPlus { '..' expPlus}
      List<Expression> exprs = new List<Expression>();
      exprs.Add(ParseExpressionPlus(scope, code, ref lWrap));

      while (code.Current.Typ == LuaToken.DotDot)
      {
        code.Next();
        exprs.Add(ParseExpressionPlus(scope, code, ref lWrap));
      }

      // Erzeuge Concat
      if (exprs.Count > 1)
      {
        for (int i = 0; i < exprs.Count; i++)
        {
          Expression expr = ToTypeExpression(exprs[i]);
          exprs[i] = expr.Type == typeof(string) ?
            expr :
            RuntimeHelperConvertExpression(expr, expr.Type, typeof(string));
        }

        lWrap |= true;
        return RuntimeHelperExpression(LuaRuntimeHelper.StringConcat, Expression.NewArrayInit(typeof(string), exprs));
      }
      else
        return exprs[0];
    } // func ParseExpressionCon

    private static Expression ParseExpressionPlus(Scope scope, LuaLexer code, ref bool lWrap)
    {
      // expPlus ::= expMul { ( + | - ) expMul}
      var expr = ParseExpressionMultiply(scope, code, ref lWrap);

      while (true)
      {
        ExpressionType typ;
        switch (code.Current.Typ)
        {
          case LuaToken.Plus:
            typ = ExpressionType.Add;
            break;
          case LuaToken.Minus:
            typ = ExpressionType.Subtract;
            break;
          default:
            return expr;
        }
        code.Next();

        expr = BinaryOperationExpression(scope.Runtime, typ, ToTypeExpression(expr), ToTypeExpression(ParseExpressionMultiply(scope, code, ref lWrap)));
        lWrap |= true;
      }
    } // func ParseExpressionPlus

    private static Expression ParseExpressionMultiply(Scope scope, LuaLexer code, ref bool lWrap)
    {
      // expMul ::= expUn { ( * | / | % ) expUn}
      var expr = ParseExpressionUnary(scope, code, ref lWrap);

      while (true)
      {
        ExpressionType typ;
        switch (code.Current.Typ)
        {
          case LuaToken.Star:
            typ = ExpressionType.Multiply;
            break;
          case LuaToken.Slash:
            typ = ExpressionType.Divide;
            break;
          case LuaToken.Percent:
            typ = ExpressionType.Modulo;
            break;
          default:
            return expr;
        }
        code.Next();

        expr = BinaryOperationExpression(scope.Runtime, typ, ToTypeExpression(expr), ToTypeExpression(ParseExpressionUnary(scope, code, ref lWrap)));
        lWrap |= true;
      }
    } // func ParseExpressionUnary

    private static Expression ParseExpressionUnary(Scope scope, LuaLexer code, ref bool lWrap)
    {
      // expUn ::= { 'not' | - | # } expPow
      if (code.Current.Typ == LuaToken.KwNot)
      {
        code.Next();
        Expression expr = ParseExpressionUnary(scope, code, ref lWrap);
        lWrap |= true;
        return UnaryOperationExpression(scope.Runtime, ExpressionType.Not, expr, expr.Type);
      }
      else if (code.Current.Typ == LuaToken.Minus)
      {
        code.Next();
        Expression expr = ParseExpressionUnary(scope, code, ref lWrap);
        lWrap |= true;
        return UnaryOperationExpression(scope.Runtime, ExpressionType.Negate, expr, expr.Type);
      }
      else if (code.Current.Typ == LuaToken.Cross)
      {
        code.Next();
        lWrap |= true;
        return Expression.Dynamic(scope.Runtime.GetGetMemberBinder("Length"), typeof(object), ToTypeExpression(ParseExpressionUnary(scope, code, ref lWrap), typeof(object)));
      }
      else
        return ParseExpressionPower(scope, code, ref lWrap);
    } // func ParseExpressionUnary

    private static Expression ParseExpressionPower(Scope scope, LuaLexer code, ref bool lWrap)
    {
      // expPow ::= cast [ ^ expPow ]
      Expression expr = ParseExpressionCast(scope, code, ref lWrap);

      if (code.Current.Typ == LuaToken.Caret)
      {
        code.Next();
        lWrap |= true;
        return BinaryOperationExpression(scope.Runtime, ExpressionType.Power, ToTypeExpression(expr), ParseExpressionPower(scope, code, ref lWrap));
      }
      else
        return expr;
    } // func ParseExpressionPower

    private static Expression ParseExpressionCast(Scope scope, LuaLexer code, ref bool lWrap)
    {
      // cast ::= cast(type, expr)
      if (code.Current.Typ == LuaToken.KwCast)
      {
        var t = code.Current;
        code.Next();

        FetchToken(LuaToken.BracketOpen, code);

        // Lies den Typ aus
        string sTypeName = ParseType(code);
        FetchToken(LuaToken.Comma, code);

        Expression expr = ParseExpression(scope, code, ref lWrap);

        FetchToken(LuaToken.BracketClose, code);

        lWrap |= true;
        return RuntimeHelperConvertExpression(expr, expr.Type, GetType(t, sTypeName));
      }
      else
        return ParsePrefix(scope, code).GenerateGet(scope, true);
    } // func ParseExpressionCast

    private static void ParseIdentifierAndType(LuaLexer code, out Token tName, out Type type)
    {
      // var ::= name ':' type
      tName = FetchToken(LuaToken.Identifier, code);
      if (code.Current.Typ == LuaToken.Colon)
      {
        code.Next();
        type = GetType(tName, ParseType(code));
      }
      else
        type = typeof(object);
    } // func ParseIdentifierAndType

    private static string ParseType(LuaLexer code)
    {
      StringBuilder sbTypeName = new StringBuilder();
      sbTypeName.Append(FetchToken(LuaToken.Identifier, code).Value);
      while (code.Current.Typ == LuaToken.Dot || code.Current.Typ == LuaToken.Plus)
      {
        if (code.Current.Typ == LuaToken.Plus)
          sbTypeName.Append('+');
        else
          sbTypeName.Append('.');
        code.Next();
        sbTypeName.Append(FetchToken(LuaToken.Identifier, code).Value);
      }
      return sbTypeName.ToString();
    } // func ParseType

    private static Type GetType(Token t, string sTypeName)
    {
      switch (sTypeName)
      {
        case "byte":
          return typeof(byte);
        case "sbyte":
          return typeof(sbyte);
        case "short":
          return typeof(short);
        case "ushort":
          return typeof(ushort);
        case "int":
          return typeof(int);
        case "uint":
          return typeof(uint);
        case "long":
          return typeof(long);
        case "ulong":
          return typeof(ulong);
        case "float":
          return typeof(float);
        case "double":
          return typeof(double);
        case "decimal":
          return typeof(decimal);
        case "datetime":
          return typeof(DateTime);
        case "string":
          return typeof(string);
        case "object":
          return typeof(object);
        case "type":
          return typeof(Type);
        case "table":
          return typeof(LuaTable);
        default:
          Type type = Lua.GetType(sTypeName);
          if (type == null)
            throw ParseError(t, String.Format(Properties.Resources.rsParseUnknownType, sTypeName));
          else
            return type;
      }
    } // func GetType

    #endregion

    #region -- Parse Goto, Label ------------------------------------------------------

    private static void ParseGoto(Scope scope, LuaLexer code)
    {
      // goto Identifier
      FetchToken(LuaToken.KwGoto, code);

      var t = FetchToken(LuaToken.Identifier, code);
      scope.AddExpression(Expression.Goto(scope.LookupLabel(null, t.Value)));
    } // proc ParseGoto

    private static void ParseLabel(Scope scope, LuaLexer code)
    {
      // ::identifier::
      FetchToken(LuaToken.ColonColon, code);

      // Erzeuge das Label
      scope.AddExpression(Expression.Label(scope.LookupLabel(null, FetchToken(LuaToken.Identifier, code).Value)));

      FetchToken(LuaToken.ColonColon, code);
    } // proc ParseLabel

    #endregion

    #region -- Parse Loops ------------------------------------------------------------

    private static void ParseDoLoop(Scope scope, LuaLexer code)
    {
      // doloop ::= do '(' name { ',' name } = expr { ',' expr }  ')' block end

      // create empty block, that can used as an loop
      Scope outerScope = new Scope(scope);
      Expression[] exprFinally = null;

      // fetch do
      FetchToken(LuaToken.KwDo, code);
      if (code.Current.Typ == LuaToken.BracketOpen) // look for disposable variables
      {
        code.Next();
        ParseExpressionStatement(outerScope, code, true);

        // Build finally-Block for the declared variables
        exprFinally = (
          from c in outerScope.Variables
          select Expression.IfThen(
            Expression.TypeIs(c, typeof(IDisposable)),
            Expression.Call(Expression.Convert(c, typeof(IDisposable)), typeof(IDisposable).GetMethod("Dispose"))
          )).ToArray();

        FetchToken(LuaToken.BracketClose, code);
      }

      LoopScope loopScope = new LoopScope(outerScope);

      // Add the Contine label after the declaration
      loopScope.AddExpression(Expression.Label(loopScope.ContinueLabel));
      // parse the block
      ParseBlock(loopScope, code);
      // create the break label
      loopScope.AddExpression(Expression.Label(loopScope.BreakLabel));

      FetchToken(LuaToken.KwEnd, code);

      if (exprFinally != null || exprFinally.Length > 0)
      {
        outerScope.AddExpression(
          Expression.TryFinally(
            loopScope.ExpressionBlock,
            Expression.Block(exprFinally)
          )
        );
        scope.AddExpression(outerScope.ExpressionBlock);
      }
      else
        scope.AddExpression(loopScope.ExpressionBlock);
    } // ParseDoLoop

    private static void ParseWhileLoop(Scope scope, LuaLexer code)
    {
      // while expr do block end;
      LoopScope loopScope = new LoopScope(scope);

      // get the expression
      FetchToken(LuaToken.KwWhile, code);

      loopScope.AddExpression(Expression.Label(loopScope.ContinueLabel));
      loopScope.AddExpression(
        Expression.IfThenElse(
          ToBooleanExpression(ParseExpression(scope, code, scope.EmitDebug)),
          Expression.Empty(),
          Expression.Goto(loopScope.BreakLabel)
        )
      );

      // append the block
      FetchToken(LuaToken.KwDo, code);
      ParseBlock(loopScope, code);
      FetchToken(LuaToken.KwEnd, code);

      // goto continue
      loopScope.AddExpression(Expression.Goto(loopScope.ContinueLabel));
      loopScope.AddExpression(Expression.Label(loopScope.BreakLabel));

      scope.AddExpression(loopScope.ExpressionBlock);
    } // func ParseWhileLoop

    private static void ParseRepeatLoop(Scope scope, LuaLexer code)
    {
      LoopScope loopScope = new LoopScope(scope);

      // continue label
      loopScope.AddExpression(Expression.Label(loopScope.ContinueLabel));

      // loop content
      FetchToken(LuaToken.KwRepeat, code);
      ParseBlock(loopScope, code);

      // get the loop expression
      FetchToken(LuaToken.KwUntil, code);
      loopScope.AddExpression(
        Expression.IfThenElse(
          ToBooleanExpression(ParseExpression(scope, code, scope.EmitDebug)),
          Expression.Empty(),
          Expression.Goto(loopScope.ContinueLabel)
        )
      );

      loopScope.AddExpression(Expression.Label(loopScope.BreakLabel));

      scope.AddExpression(loopScope.ExpressionBlock);
    } // func ParseRepeatLoop

    private static void ParseForLoop(Scope scope, LuaLexer code)
    {
      // for name
      FetchToken(LuaToken.KwFor, code);
      Token tLoopVar;
      Type typeLoopVar;
      ParseIdentifierAndType(code, out tLoopVar, out typeLoopVar);
      if (code.Current.Typ == LuaToken.Assign)
      {
        // = exp, exp [, exp] do block end
        FetchToken(LuaToken.Assign, code);
        Expression loopStart = ParseExpression(scope, code, scope.EmitDebug);
        FetchToken(LuaToken.Comma, code);
        Expression loopEnd = ParseExpression(scope, code, scope.EmitDebug);
        Expression loopStep;
        if (code.Current.Typ == LuaToken.Comma)
        {
          code.Next();
          loopStep = ParseExpression(scope, code, scope.EmitDebug);
        }
        else
          loopStep = Expression.Constant(1, typeof(int));

        LoopScope loopScope = new LoopScope(scope);
        ParameterExpression loopVarParameter = loopScope.RegisterVariable(typeLoopVar == typeof(object) ? loopStart.Type : typeLoopVar, tLoopVar.Value);

        FetchToken(LuaToken.KwDo, code);
        ParseBlock(loopScope, code);
        FetchToken(LuaToken.KwEnd, code);
        scope.AddExpression(GenerateForLoop(loopScope, loopVarParameter, loopStart, loopEnd, loopStep));
      }
      else
      {
        // {, name} in explist do block end

        // fetch all loop variables
        LoopScope loopScope = new LoopScope(scope);
        List<ParameterExpression> loopVars = new List<ParameterExpression>();
        loopVars.Add(loopScope.RegisterVariable(typeLoopVar, tLoopVar.Value));
        while (code.Current.Typ == LuaToken.Comma)
        {
          code.Next();
          ParseIdentifierAndType(code, out tLoopVar, out typeLoopVar);
          loopVars.Add(loopScope.RegisterVariable(typeLoopVar, tLoopVar.Value));
        }

        // get the loop expressions
        FetchToken(LuaToken.KwIn, code);
        Expression[] explist = ParseExpressionList(scope, code).ToArray();

        // parse the loop body
        FetchToken(LuaToken.KwDo, code);
        ParseBlock(loopScope, code);
        FetchToken(LuaToken.KwEnd, code);

        scope.AddExpression(GenerateForLoop(loopScope, loopVars, explist));
      }
    } // func ParseForLoop

    private static void ParseForEachLoop(Scope scope, LuaLexer code)
    {
      ParameterExpression varEnumerable = Expression.Variable(typeof(System.Collections.IEnumerable), "#enumerable");
      ParameterExpression varEnumerator = Expression.Variable(typeof(System.Collections.IEnumerator), "#enumerator");

      // foreach name in exp do block end;
      code.Next(); // foreach

      // fetch the loop variable
      LoopScope loopScope = new LoopScope(scope);
      Token tLoopVar;
      Type typeLoopVar;
      ParseIdentifierAndType(code, out tLoopVar, out typeLoopVar);
      ParameterExpression loopVar = loopScope.RegisterVariable(typeLoopVar, tLoopVar.Value);

      // get the enumerable expression
      FetchToken(LuaToken.KwIn, code);
      Expression exprEnum = ParseExpression(scope, code, scope.EmitDebug);

      // parse the loop body
      FetchToken(LuaToken.KwDo, code);
      ParseBlock(loopScope, code);
      FetchToken(LuaToken.KwEnd, code);

      Type typeEnumerator = typeof(System.Collections.IEnumerator);
      var miGetEnumerator = typeof(System.Collections.IEnumerable).GetMethod("GetEnumerator");
      var miMoveNext = typeEnumerator.GetMethod("MoveNext");
      var piCurrent = typeEnumerator.GetProperty("Current", BindingFlags.GetProperty | BindingFlags.Public | BindingFlags.Instance);

      loopScope.InsertExpression(0, Expression.Assign(loopVar, ToTypeExpression(Expression.Property(varEnumerator, piCurrent), loopVar.Type)));
      scope.AddExpression(
        Expression.Block(new ParameterExpression[] { varEnumerable, varEnumerator, loopVar },
        // local enumerable = exprEnum as IEnumerator
        Expression.Assign(varEnumerable, Expression.TypeAs(exprEnum, typeof(System.Collections.IEnumerable))),

        // if enumerable == nil then error
        Expression.IfThen(Expression.Equal(varEnumerable, Expression.Constant(null, typeof(object))), Lua.ThrowExpression("Expression is not enumerable.")),

        // local enum = exprEnum.GetEnumerator()
        Expression.Assign(varEnumerator, Expression.Call(varEnumerable, miGetEnumerator)),

        // while enum.MoveNext() do
        Expression.Label(loopScope.ContinueLabel),
        Expression.IfThenElse(Expression.Call(varEnumerator, miMoveNext), Expression.Empty(), Expression.Goto(loopScope.BreakLabel)),

        //   loopVar = enum.Current
        loopScope.ExpressionBlock,

        // end;
        Expression.Goto(loopScope.ContinueLabel),
        Expression.Label(loopScope.BreakLabel)
        ));
    } // proc ParseForEachLoop

    private static Expression GenerateForLoop(LoopScope loopScope, ParameterExpression loopVar, Expression loopStart, Expression loopEnd, Expression loopStep)
    {
      const string csVar = "#var";
      const string csEnd = "#end";
      const string csStep = "#step";
      ParameterExpression internLoopVar = Expression.Variable(loopVar.Type, csVar);
      ParameterExpression endVar = Expression.Variable(loopEnd.Type, csEnd);
      ParameterExpression stepVar = Expression.Variable(loopStep.Type, csStep);
      LabelTarget labelLoop = Expression.Label("#loop");

      // Erzeuge CodeBlock
      loopScope.InsertExpression(0, Expression.Assign(loopVar, internLoopVar));

      // Erzeuge den Schleifenblock
      return Expression.Block(new ParameterExpression[] { internLoopVar, endVar, stepVar },
        Expression.Assign(internLoopVar, ToTypeExpression(loopStart, internLoopVar.Type)),
        Expression.Assign(endVar, loopEnd),
        Expression.Assign(stepVar, loopStep),

        Expression.Label(labelLoop),

        Expression.IfThenElse(
          ToTypeExpression(
            BinaryOperationExpression(null, ExpressionType.OrElse,
              BinaryOperationExpression(null, ExpressionType.AndAlso,
                BinaryOperationExpression(loopScope.Runtime, ExpressionType.GreaterThan, stepVar, stepVar.Type, Expression.Constant(0, typeof(int)), typeof(int)), typeof(bool),
                BinaryOperationExpression(loopScope.Runtime, ExpressionType.LessThanOrEqual, internLoopVar, internLoopVar.Type, endVar, endVar.Type), typeof(bool)
              ), typeof(bool),
              BinaryOperationExpression(null, ExpressionType.AndAlso,
                BinaryOperationExpression(loopScope.Runtime, ExpressionType.LessThanOrEqual, stepVar, stepVar.Type, Expression.Constant(0, typeof(int)), typeof(int)), typeof(bool),
                BinaryOperationExpression(loopScope.Runtime, ExpressionType.GreaterThanOrEqual, internLoopVar, internLoopVar.Type, endVar, endVar.Type), typeof(bool)
              ), typeof(bool)
            ),
            typeof(bool)
          ),
          loopScope.ExpressionBlock,
          Expression.Goto(loopScope.BreakLabel)
        ),
        Expression.Label(loopScope.ContinueLabel),

        Expression.Assign(internLoopVar, ToTypeExpression(BinaryOperationExpression(loopScope.Runtime, ExpressionType.Add, internLoopVar, internLoopVar.Type, stepVar, stepVar.Type), internLoopVar.Type)),

        Expression.Goto(labelLoop),
        Expression.Label(loopScope.BreakLabel)
      );
    } // func GenerateForLoop

    private static Expression GenerateForLoop(LoopScope loopScope, List<ParameterExpression> loopVars, Expression[] explist)
    {
      const string csFunc = "#f";
      const string csState = "#s";
      const string csVar = "#var";

      ParameterExpression varTmp = Expression.Variable(typeof(LuaResult), "#tmp");
      ParameterExpression varFunc = Expression.Variable(typeof(Delegate), csFunc);
      ParameterExpression varState = Expression.Variable(typeof(object), csState);
      ParameterExpression varVar = Expression.Variable(typeof(object), csVar);

      // Convert the parameters
      if (explist.Length > 1)
        for (int i = 0; i < explist.Length; i++)
          explist[i] = ToTypeExpression(explist[i], typeof(object));

      // local var1, ..., varn = tmp;
      for (int i = 0; i < loopVars.Count; i++)
        loopScope.InsertExpression(i, Expression.Assign(loopVars[i], ToTypeExpression(GetResultExpression(varTmp, i), loopVars[i].Type)));
      return Expression.Block(new ParameterExpression[] { varTmp, varFunc, varState, varVar },
        // fill the local loop variables initial
        // local #f, #s, #var = explist
        Expression.Assign(varTmp,
          explist.Length == 1 && explist[0].Type == typeof(LuaResult) ? explist[0] : Expression.New(Lua.ResultConstructorInfo, Expression.NewArrayInit(typeof(object), explist))
        ),
        Expression.Assign(varFunc, ToTypeExpression(GetResultExpression(varTmp, 0), typeof(Delegate))),
        Expression.Assign(varState, GetResultExpression(varTmp, 1)),
        Expression.Assign(varVar, GetResultExpression(varTmp, 2)),

        Expression.Label(loopScope.ContinueLabel),

        // local tmp = f(s, var)
        Expression.Assign(varTmp, Expression.Dynamic(Lua.FunctionResultBinder, typeof(LuaResult), Expression.Dynamic(loopScope.Runtime.GetInvokeBinder(new CallInfo(2)), typeof(object), varFunc, varState, varVar))),

        // var = tmp[0]
        Expression.Assign(varVar, GetResultExpression(varTmp, 0)),

        // if var == nil then goto break;
        Expression.IfThenElse(Expression.Equal(varVar, Expression.Constant(null, typeof(object))),
          Expression.Goto(loopScope.BreakLabel),
          loopScope.ExpressionBlock), // LoopBody

        Expression.Goto(loopScope.ContinueLabel),
        Expression.Label(loopScope.BreakLabel)
      );
    } // func GenerateForLoop

    private static void ParseBreak(Scope scope, LuaLexer code)
    {
      FetchToken(LuaToken.KwBreak, code);

      // Erzeuge die Expression
      scope.AddExpression(Expression.Goto(scope.LookupLabel(null, csBreakLabel)));

      // Optionales Semicolon
      FetchToken(LuaToken.Semicolon, code, true);
    } // func ParseBreak

    #endregion

    #region -- Parse Function, Lambda -------------------------------------------------

    private static void ParseFunction(Scope scope, LuaLexer code, bool lLocal)
    {
      FetchToken(LuaToken.KwFunction, code);

      if (lLocal) // Local function, only one identifier is allowed
      {
        var t = FetchToken(LuaToken.Identifier, code);
        ParameterExpression funcVar = scope.RegisterVariable(typeof(Delegate), t.Value);
        scope.AddExpression(
          Expression.Assign(
            funcVar,
            ToTypeExpression(ParseLamdaDefinition(scope, code, funcVar.Name, false), funcVar.Type)
          )
        );
      }
      else // Function that is assigned to a table. A chain of identifiers is allowed.
      {
        Expression assignee = null;
        string sMember = FetchToken(LuaToken.Identifier, code).Value;

        // Collect the chain of members
        while (code.Current.Typ == LuaToken.Dot)
        {
          code.Next();

          // Create the get-member for the current assignee
          assignee = ParseFunctionAddChain(scope, assignee, sMember);
          sMember = FetchToken(LuaToken.Identifier, code).Value;
        }
        // add a method to the table. methods get a hidden parameter and will bo marked
        if (code.Current.Typ == LuaToken.Colon)
        {
          code.Next();

          // add the last member to the assignee chain
          assignee = ParseFunctionAddChain(scope, assignee, sMember);
          // fetch the method name
          sMember = FetchToken(LuaToken.Identifier, code).Value;

          // generate the lambda
          scope.AddExpression(
            Expression.Call(
              ToTypeExpression(assignee, typeof(LuaTable)),
              Lua.TableSetMethodInfo,
              Expression.Constant(sMember, typeof(string)),
              ToTypeExpression(ParseLamdaDefinition(scope, code, sMember, true), typeof(Delegate)))
            );
        }
        else
        {
          if (assignee == null)
            assignee = scope.LookupExpression(csEnv); // create a global function
          
          scope.AddExpression(
            Expression.Dynamic(
              scope.Runtime.GetSetMemberBinder(sMember), typeof(object), 
              assignee, ToTypeExpression(ParseLamdaDefinition(scope, code, sMember, false), 
              typeof(object))));
        }
      }
    } // proc ParseLamdaDefinition

    private static Expression ParseFunctionAddChain(Scope scope, Expression assignee, string sMember)
    {
      if (assignee == null)
      {
         Expression expr = scope.LookupExpression(sMember);
         if (expr == null)
           assignee = ParseFunctionAddChain(scope, scope.LookupExpression(csEnv), sMember);
         else
           assignee = expr;
      }
      else
        assignee = Expression.Dynamic(scope.Runtime.GetGetMemberBinder(sMember), typeof(object), assignee);
      return assignee;
    } // proc ParseFunctionAddChain

    private static Expression ParseLamdaDefinition(Scope parent, LuaLexer code, string sName, bool lSelfParameter)
    {
      List<ParameterExpression> parameters = new List<ParameterExpression>();
      LambdaScope scope = new LambdaScope(parent);

      // Lese die Parameterliste ein
      FetchToken(LuaToken.BracketOpen, code);
      if (lSelfParameter)
        parameters.Add(scope.RegisterParameter(typeof(object), "self"));

      if (code.Current.Typ == LuaToken.Identifier || code.Current.Typ == LuaToken.DotDotDot)
      {
        if (code.Current.Typ == LuaToken.DotDotDot)
        {
          code.Next();
          ParseLamdaDefinitionArgList(scope, parameters);
        }
        else
        {
          Token tName;
          Type typeArgument;
          ParseIdentifierAndType(code, out tName, out typeArgument);
          parameters.Add(scope.RegisterParameter(typeArgument, tName.Value));

          while (code.Current.Typ == LuaToken.Comma)
          {
            code.Next();
            if (code.Current.Typ == LuaToken.DotDotDot)
            {
              code.Next();
              ParseLamdaDefinitionArgList(scope, parameters); // last argument
              break;
            }
            else
            {
              ParseIdentifierAndType(code, out tName, out typeArgument);
              parameters.Add(scope.RegisterParameter(typeArgument, tName.Value));
            }
          }
        }
      }
      FetchToken(LuaToken.BracketClose, code);

      // Is there a specific result 
      if (code.Current.Typ == LuaToken.Colon)
      {
        var t = code.Current;
        code.Next();
        Type typeResult = GetType(t, ParseType(code));
        scope.ResetReturnLabel(typeResult, null);
      }
      else
        scope.ResetReturnLabel(typeof(LuaResult), Expression.Property(null, Lua.ResultEmptyPropertyInfo));

      // Lese den Code-Block
      ParseBlock(scope, code);

      FetchToken(LuaToken.KwEnd, code);
      return Expression.Lambda(scope.ExpressionBlock, scope.Runtime.CreateEmptyChunk(sName).Name, parameters);
    } // proc ParseLamdaDefinition

    private static void ParseLamdaDefinitionArgList(LambdaScope scope, List<ParameterExpression> parameters)
    {
      ParameterExpression paramArgList = scope.RegisterParameter(typeof(object[]), csArgListP);
      ParameterExpression varArgList = scope.RegisterVariable(typeof(LuaResult), csArgList);
      parameters.Add(paramArgList);
      scope.AddExpression(Expression.Assign(varArgList, Expression.New(Lua.ResultConstructorInfo, paramArgList)));
    } // proc ParseLamdaDefinitionArgList

    #endregion

    #region -- Parse TableConstructor -------------------------------------------------

    private static Expression ParseTableConstructor(Scope scope, LuaLexer code)
    {
      // table ::= '{' [field] { fieldsep field } [fieldsep] '}'
      // fieldsep ::= ',' | ';'
      FetchToken(LuaToken.BracketCurlyOpen, code);

      if (code.Current.Typ != LuaToken.BracketCurlyClose)
      {
        int iIndex = 1;
        Scope scopeTable = new Scope(scope);

        // Erzeuge die Table
        ParameterExpression tableVar = scopeTable.RegisterVariable(typeof(LuaTable), "#table");
        scopeTable.AddExpression(Expression.Assign(tableVar, CreateEmptyTableExpression()));

        ParseTableField(tableVar, scopeTable, code, ref iIndex);

        // Sammle weitere Felder ein
        while (code.Current.Typ == LuaToken.Comma || code.Current.Typ == LuaToken.Semicolon)
        {
          code.Next();

          ParseTableField(tableVar, scopeTable, code, ref iIndex);
        }

        // Optionaler Endtrenner
        if (code.Current.Typ == LuaToken.Comma || code.Current.Typ == LuaToken.Semicolon)
          code.Next();

        scopeTable.AddExpression(ToTypeExpression(tableVar, typeof(object)));
        scopeTable.ExpressionBlockType = typeof(object);

        // Schließende Klammer
        FetchToken(LuaToken.BracketCurlyClose, code);

        return scopeTable.ExpressionBlock;
      }
      else
      {
        FetchToken(LuaToken.BracketCurlyClose, code);
        return CreateEmptyTableExpression();
      }
    } // func ParseTableConstructor

    private static void ParseTableField(ParameterExpression tableVar, Scope scope, LuaLexer code, ref int iIndex)
    {
      // field ::= '[' exp ']' '=' exp | Name '=' exp | exp
      if (code.Current.Typ == LuaToken.BracketSquareOpen)
      {
        // Lies den Index
        code.Next();
        var index = ParseExpression(scope, code, scope.EmitDebug);
        FetchToken(LuaToken.BracketSquareClose, code);
        FetchToken(LuaToken.Assign, code);

        // Erzeuge den Befehl
        scope.AddExpression(
          LuaTable.SetValueExpression(tableVar, 
            ToTypeExpression(index, typeof(object)),
            ToTypeExpression(ParseExpression(scope, code, scope.EmitDebug), typeof(object))
          )
        );
      }
      else if (code.Current.Typ == LuaToken.Identifier && code.LookAhead.Typ == LuaToken.Assign)
      {
        // Lies den Identifier
        string sMember = code.Current.Value;
        code.Next();
        FetchToken(LuaToken.Assign, code);

        // Erzeuge den Code
        scope.AddExpression(
          Expression.Dynamic(scope.Runtime.GetSetMemberBinder(sMember), typeof(object),
            tableVar,
            ToTypeExpression(ParseExpression(scope, code, scope.EmitDebug), typeof(object))
          )
        );
      }
      else
      {
        Expression expr = ParseExpression(scope, code, scope.EmitDebug);

        if (code.Current.Typ == LuaToken.BracketCurlyClose) // Letzte Zuweisung, verteile ein eventuelles Array auf die Indices
        {
          scope.AddExpression(
            RuntimeHelperExpression(LuaRuntimeHelper.TableSetObjects,
              tableVar,
              ToTypeExpression(expr, typeof(object)),
              Expression.Constant(iIndex, typeof(int))
            )
          );
        }
        else // Erster Wert wird zugewiesen
        {
          scope.AddExpression(
            Expression.Dynamic(scope.Runtime.GetSetIndexMember(new CallInfo(1)), typeof(object),
              tableVar,
              Expression.Convert(Expression.Constant(iIndex++, typeof(int)), typeof(object)),
              ToTypeExpression(expr, typeof(object))
            )
          );
        }
      }
    } // proc ParseTableField

    internal static Expression CreateEmptyTableExpression()
    {
      return Expression.New(typeof(LuaTable));
    } // func CreateEmptyTableExpression

    #endregion

    #region -- FetchToken, ParseError -------------------------------------------------

    private static Token FetchToken(LuaToken typ, LuaLexer code, bool lOptional = false)
    {
      if (code.Current.Typ == typ)
      {
        var t = code.Current;
        code.Next();
        return t;
      }
      else if (lOptional)
        return null;
      else
        throw ParseError(code.Current, String.Format(Properties.Resources.rsParseUnexpectedToken, LuaLexer.GetTokenName(code.Current.Typ), LuaLexer.GetTokenName(typ)));
    } // proc FetchToken

    private static LuaParseException ParseError(Token start, string sMessage)
    {
      return new LuaParseException(start.Start, sMessage, null);
    } // func ParseError

    #endregion

    #region -- ExpressionToString -----------------------------------------------------

    private static PropertyInfo propertyDebugView = null;

    public static string ExpressionToString(Expression expr)
    {
      if (propertyDebugView == null)
        propertyDebugView = typeof(Expression).GetProperty("DebugView", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.GetProperty, null, typeof(string), new Type[0], null);

      return (string)propertyDebugView.GetValue(expr, null);
    } // func ExpressionToString

    #endregion
  } // class Parser

  #endregion
}
