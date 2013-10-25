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
  // todo: ...
  #region -- class Parser -------------------------------------------------------------

  ///////////////////////////////////////////////////////////////////////////////
  /// <summary></summary>
  internal static partial class Parser
  {
    private const string csReturnLabel = "#return";
    private const string csBreakLabel = "#break";
    private const string csContinueLabel = "#continue";
    private const string csEnv = "_G";
    private const string csArgList = "#arglist";

    #region -- class Scope ------------------------------------------------------------

    ///////////////////////////////////////////////////////////////////////////////
    /// <summary>Scope, that builds a block.</summary>
    private class Scope
    {
      private Scope parent;   // Parent-Scope, that is accessable
      private Dictionary<string, ParameterExpression> scopeVariables = null; // local declared variables
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
        if (scopeVariables == null)
          scopeVariables = new Dictionary<string, ParameterExpression>();

        return scopeVariables[sName] = Expression.Variable(type, sName);
      } // proc RegisterParameter

      /// <summary>Looks up the variable/parameter through the scopes.</summary>
      /// <param name="sName">Name of the variable</param>
      /// <returns>The access-expression for the variable, parameter or <c>null</c>, if it is not registered.</returns>
      public virtual ParameterExpression LookupParameter(string sName)
      {
        // Lookup the current scope
        ParameterExpression p;
        if (scopeVariables != null && scopeVariables.TryGetValue(sName, out p))
          return p;

        if (parent != null) // lookup the parent scope
          return parent.LookupParameter(sName);
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

          if (scopeVariables != null && scopeVariables.Count > 0)
            if (block.Count == 0)
              return Expression.Block(scopeVariables.Values, Expression.Empty());
            else if (ExpressionBlockType == null)
              return Expression.Block(scopeVariables.Values, block);
            else
              return Expression.Block(ExpressionBlockType, scopeVariables.Values, block);
          else if (block.Count == 0)
            return Expression.Empty();
          else
            return Expression.Block(block);
        }
      } // func ExpressionBlock

      public Type ExpressionBlockType { get; set; }

      #endregion

      /// <summary>Access to the Lua-Binders</summary>
      public virtual Lua Runtime { get { return parent.Runtime; } }
      /// <summary>Emit-Debug-Information</summary>
      public virtual bool EmitDebug { get { return parent.EmitDebug; } }
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
      private LabelTarget returnLabel = Expression.Label(typeof(object[]), csReturnLabel);
      private Dictionary<string, LabelTarget> labels = null;
      private Dictionary<string, ParameterExpression> parameters = new Dictionary<string, ParameterExpression>();

      #region -- Ctor -----------------------------------------------------------------

      /// <summary>Creates the lambda-scope, that manages labels and arguments.</summary>
      /// <param name="parent"></param>
      public LambdaScope(Scope parent)
        : base(parent)
      {
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
      /// <returns></returns>
      public override ParameterExpression LookupParameter(string sName)
      {
        ParameterExpression p;
        if (parameters.TryGetValue(sName, out p))
          return p;
        return base.LookupParameter(sName);
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
          throw new ArgumentException("Internes Label übergeben, welches nicht existiert.");

        return labels[sName] = Expression.Label(type, sName);
      } // func LookupLabel

      #endregion

      public override Expression ExpressionBlock
      {
        get
        {
          AddExpression(Expression.Label(returnLabel, Expression.Constant(null, returnLabel.Type)));
          return base.ExpressionBlock;
        }
      } // prop ExpressionBlock

      public LabelTarget ReturnLabel { get { return returnLabel; } }
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
      public GlobalScope(Lua runtime, bool lDebug)
        : base(null)
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
      private Token tStart = null;
      private Token tEnd = null;

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
          r[0] = Instance;
          Array.Copy(Indices, 0, r, 1, Indices.Length);
          r[r.Length - 1] = exprToSet;
          expr = Expression.Dynamic(scope.Runtime.GetSetIndexMember(new CallInfo(Indices.Length)), typeof(object), r);
        }
        else if (Instance != null && Member != null && Indices == null && Arguments == null)
        {
          // Assign the value to a member
          expr = Expression.Dynamic(scope.Runtime.GetSetMemberBinder(Member), typeof(object), Instance, exprToSet);
        }
        else if (Instance != null && Member == null && Indices == null && Arguments == null && Instance is ParameterExpression)
        {
          // Assign the value to a variable
          expr = Expression.Assign(Instance, Expression.Convert(exprToSet, typeof(object)));
        }
        else
          throw ParseError(Position, "Expression is not assignable");

        if (tStart != null)
        {
          expr = WrapDebugInfo(scope, tStart, tEnd, expr);
          tStart = null;
        }

        return expr;
      } // func GenerateSet

      public Expression GenerateGet(Scope scope)
      {
        if (Instance != null && Member == null && Indices != null && Arguments == null)
        {
          // Create the arguments for the index assign
          Expression[] r = new Expression[Indices.Length + 1];
          r[0] = ToObjectExpression(Instance); // Array instance
          Array.Copy(Indices, 0, r, 1, Indices.Length); // Copy the index values

          Instance = Expression.Dynamic(scope.Runtime.GetGetIndexMember(new CallInfo(Indices.Length)), typeof(object), r);
          Indices = null;
        }
        else if (Instance != null && Member != null && Indices == null && Arguments == null)
        {
          // Convert the member to an instance
          Instance = Expression.Dynamic(scope.Runtime.GetGetMemberBinder(Member), typeof(object), ToObjectExpression(Instance));
          Member = null;
        }
        else if (Instance != null && Member == null && Indices == null && Arguments == null)
        {
          // Nothing to todo, we have already an instance
        }
        else if (Instance != null && Indices == null && Arguments != null)
        {
          Expression[] r = new Expression[Arguments.Length + 1];
          r[0] = ToObjectExpression(Instance); // Delegate
          // All arguments are converted to an object, except of the last one (rollup)
          for (int i = 0; i < Arguments.Length - 1; i++)
            r[i + 1] = ToObjectExpression(Arguments[i], false); // Convert the arguments 
          if (Arguments.Length > 0)
            r[r.Length - 1] = Arguments[Arguments.Length - 1]; // Let the type as it is

          // Functions always return an array objects
          Instance = Expression.Dynamic(Lua.FunctionResultBinder, typeof(object[]), 
            Expression.Dynamic(Member == null ? 
              scope.Runtime.GetInvokeBinder(new CallInfo(Arguments.Length)) : 
              scope.Runtime.GetInvokeMemberBinder(Member, new CallInfo(Arguments.Length)), typeof(object), r)
          );
          Member = null;
          Arguments = null;
        }
        else
          throw ParseError(Position, "Expression as no result.");

        if (tStart != null)
        {
          Instance = WrapDebugInfo(scope, tStart, tEnd, Instance);
          tStart = null;
        }

        return Instance;
      } // func GenerateGet

      public void SetDebugInfo(Token tStart, Token tEnd)
      {
        this.tStart = tStart;
        this.tEnd = tEnd;
      } // proc SetDebugInfo

      public Token Position { get; set; }
      public Expression Instance { get; private set; }
      public string Member { get; set; }
      public Expression[] Indices { get; set; }
      public Expression[] Arguments { get; set; }
    } // class PrefixMemberInfo

    #endregion

    #region -- Parse Chunk, Block -----------------------------------------------------

    /// <summary>Parses the chunk to an function.</summary>
    /// <param name="runtime">Binder</param>
    /// <param name="code">Lexer for the code.</param>
    /// <param name="args">Arguments of the function.</param>
    /// <returns>Expression-Tree for the code.</returns>
    public static LambdaExpression ParseChunk(Lua runtime, bool lDebug, LuaLexer code, IEnumerable<KeyValuePair<string, Type>> args)
    {
      List<ParameterExpression> parameters = new List<ParameterExpression>();
      var globalScope = new GlobalScope(runtime, lDebug);

      // Registers the global LuaTable
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
        throw ParseError(code.Current, "Unexpected eof.");

      // Create the function
      return Expression.Lambda(globalScope.ExpressionBlock, sChunkName, parameters);
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
            if (!ParseStatement(scope, code)) // Parse normal statements
              lLoop = false;
            break;
        }
      }
    } // func ParseBlock

    private static void ParseReturn(Scope scope, LuaLexer code)
    {
      // Parse das Return
      code.Next();

      // Wird als letzten Parameter ein Array übergeben so wird, das Return-Objekt um dessen Element erweitert.
      Expression exprReturnValue;

      if (IsExpressionStart(code))
      {
        exprReturnValue = RuntimeHelperExpression(LuaRuntimeHelper.ReturnResult,
          Expression.NewArrayInit(typeof(object),
            from c in ParseExpressionList(scope, code) select Expression.Convert(c, typeof(object))
            )
          );
      }
      else
        exprReturnValue = Expression.Constant(Lua.EmptyResult, typeof(object[]));

      if (code.Current.Typ == LuaToken.Semicolon)
        code.Next();

      scope.AddExpression(Expression.Goto(scope.LookupLabel(typeof(object[]), csReturnLabel), exprReturnValue));
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

        case LuaToken.InvalidString:
          throw ParseError(code.Current, "Newline in string constant.");
        case LuaToken.InvalidComment:
          throw ParseError(code.Current, "Comment not closed.");
        case LuaToken.InvalidChar:
          throw ParseError(code.Current, "Unexpected char.");

        default:
          return false;
      }
    }  // func ParseStatement

    private static void ParseExpressionStatement(Scope scope, LuaLexer code, bool lLocal)
    {
      List<PrefixMemberInfo> prefixes = new List<PrefixMemberInfo>();

      // Parse die Zuweisungen
      while (true)
      {
        if (lLocal)
        {
          var t = FetchToken(LuaToken.Identifier, code);
          prefixes.Add(new PrefixMemberInfo(t, scope.RegisterVariable(typeof(object), t.Value), null, null, null));
        }
        else
        {
          // Parse Prefix
          bool lWrap = false;
          Token tStart = code.Current;
          PrefixMemberInfo prefix = ParsePrefix(scope, code, ref lWrap);
          if (lWrap && scope.EmitDebug)
            prefix.SetDebugInfo(tStart, code.Current);
          prefixes.Add(prefix);
        }

        // Noch ein Prefix
        if (code.Current.Typ == LuaToken.Comma)
          code.Next();
        else
          break;
      }

      // Kommt eine Zuweisung
      if (code.Current.Typ == LuaToken.Assign)
      {
        code.Next();

        // Zuweise parsen
        IEnumerator<Expression> expr = ParseExpressionList(scope, code).GetEnumerator();
        expr.MoveNext();

        if (prefixes.Count == 1) // Es handelt sich nur um ein Prefix 1 zu 1 zuweisung
        {
          scope.AddExpression(
            prefixes[0].GenerateSet(scope,
              expr.Current != null ?
              ToObjectExpression(expr.Current) :
              Expression.Constant(null, typeof(object))
            )
          );
        }
        else if (expr.Current == null) // Keine Zuweisungen, null initialisieren
        {
          for (int i = 0; i < prefixes.Count; i++)
            scope.AddExpression(prefixes[i].GenerateSet(scope, Expression.Constant(null, typeof(object))));
        }
        else // Zuweisung mit unbekannter Anzahl von Parametern, 
        {
          int iPrefix = 0;
          Expression l = expr.Current;
          Expression c;
          ParameterExpression v = null;

          while (true)
          {
            // Hole die nächste Expression ab
            if (expr.MoveNext())
            {
              c = expr.Current;

             scope.AddExpression(prefixes[iPrefix++].GenerateSet(scope,
                l != null ?
                ToObjectExpression(l) :
                Expression.Constant(null, typeof(object))));
            }
            else
            {
              // Generiere Zuweisung via t Variable
              if (l.Type == typeof(object[]) || typeof(object[]).IsAssignableFrom(l.Type))
              {
                v = scope.RegisterVariable(typeof(object[]), "#tmp");
                scope.AddExpression(Expression.Assign(v, l));
                scope.AddExpression(prefixes[iPrefix++].GenerateSet(scope, RuntimeHelperExpression(LuaRuntimeHelper.GetObject, Expression.Convert(v, typeof(object[])), Expression.Constant(0, typeof(int)))));
              }
              else
                scope.AddExpression(prefixes[iPrefix++].GenerateSet(scope, ToObjectExpression(l)));
              break;
            }

            l = c;

            if (iPrefix >= prefixes.Count)
            {
              scope.AddExpression(c);
              break;
            }
          }

          // Weise den Rest aus dem eventuellen Array zu
          if (v != null)
          {
            int iLastIndex = 1;

            while (iPrefix < prefixes.Count)
            {
              scope.AddExpression(prefixes[iPrefix].GenerateSet(scope, RuntimeHelperExpression(LuaRuntimeHelper.GetObject, Expression.Convert(v, typeof(object[])), Expression.Constant(iLastIndex, typeof(int)))));
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
          scope.AddExpression(prefixes[i].GenerateGet(scope));
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
        throw ParseError(code.Current, "Unexpected token in IfElse-Statement");
    } // func ParseElseStatement

    private static Expression ParseIfElseBlock(Scope parent, LuaLexer code)
    {
      Scope scope = new Scope(parent);
      ParseBlock(scope, code);
      return scope.ExpressionBlock;
    } // func ParseIfElseBlock

    #endregion

    #region -- Parse Prefix, Suffix ---------------------------------------------------

    private static PrefixMemberInfo ParsePrefix(Scope scope, LuaLexer code, ref bool lWrap)
    {
      // prefix ::= Identifier suffix_opt |  '(' exp ')' suffix | literal | tablector

      Token tStart = code.Current;
      PrefixMemberInfo info;
      switch (tStart.Typ)
      {
        case LuaToken.BracketOpen: // Parse eine Expression
          code.Next();
          var expr = ParseExpression(scope, code, ref lWrap);
          FetchToken(LuaToken.BracketClose, code);

          info = new PrefixMemberInfo(tStart, ToObjectExpression(expr), null, null, null);
          break;

        case LuaToken.Identifier:
        case LuaToken.DotDotDot:
            var t = code.Current;
            var p = scope.LookupParameter(t.Typ == LuaToken.DotDotDot ? csArgList : t.Value);
            if (t.Typ == LuaToken.DotDotDot && p == null)
              throw ParseError(t, "No arglist defined.");
            code.Next();
            if (p == null) // Als globale Variable verwalten, da es keine locale Variable gibt
              info = new PrefixMemberInfo(tStart, scope.LookupParameter(csEnv), t.Value, null, null);
            else
              info = new PrefixMemberInfo(tStart, p, null, null, null);
            lWrap |= true;
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
          throw ParseError(code.Current, "Literal, function or tablector expected.");
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
            info.GenerateGet(scope);
            info.Indices = ParseExpressionList(scope, code).ToArray();
            FetchToken(LuaToken.BracketSquareClose, code);
            break;

          case LuaToken.Dot: // Property of an class
            code.Next();
            info.GenerateGet(scope);
            info.Member = FetchToken(LuaToken.Identifier, code).Value;
            break;

          case LuaToken.BracketOpen: // List of arguments
            info.GenerateGet(scope);
            info.Arguments = ParseArgumentList(scope, code);
            break;

          case LuaToken.BracketCurlyOpen: // LuaTable as an argument
            info.GenerateGet(scope);
            info.Arguments = new Expression[] { ParseTableConstructor(scope, code) };
            break;

          case LuaToken.String: // String as an argument
            info.GenerateGet(scope);
            info.Arguments = new Expression[] { Expression.Constant(FetchToken(LuaToken.String, code).Value, typeof(object)) };
            break;

          case LuaToken.Colon: // Methodenaufruf
            code.Next();

            // Lese den Namen um den Member zu belegen
            info.GenerateGet(scope);
            info.Member = FetchToken(LuaToken.Identifier, code).Value;

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
        throw ParseError(t, String.Format("Number expected ('{0}' not converted).", sNumber));
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
        // remote the '0x'
        if (sNumber.Length > 2 && sNumber[0] == '0' && (sNumber[1] == 'x' || sNumber[1] == 'X'))
          sNumber = sNumber.Substring(2);

        // Konvertiere die Zahl
        if (Int32.TryParse(sNumber, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out i))
          return Expression.Constant(i, typeof(int));
        // Todo: Binäre Exponente???
        //else if (Double.TryParse(sNumber, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out d))
        //  return Expression.Constant(d, typeof(Double));
        else
          throw ParseError(t, String.Format("Number expected ('{0}' not converted).", sNumber));
      }
    } // func ParseHexNumber

    #endregion

    #region -- Parse Expressions ------------------------------------------------------

    private static Expression WrapDebugInfo(Scope scope, Token tStart, Token tEnd, Expression expr)
    {
      if (scope.EmitDebug)
      {
        // create the debug expression
        Expression startDebug = Expression.DebugInfo(tStart.Start.Document,
          tStart.Start.Line,
          tStart.Start.Col,
          tEnd.Start.Line,
          tEnd.Start.Col);
        Expression clearDebug = Expression.ClearDebugInfo(tStart.Start.Document);

        if (expr.Type == typeof(void))
        {
          return Expression.Block(
            startDebug,
            expr,
            clearDebug);
        }
        else
        {
          ParameterExpression r = Expression.Variable(expr.Type);
          return Expression.Block(expr.Type, new ParameterExpression[] { r },
            startDebug,
            Expression.Assign(r, expr),
            clearDebug,
            r);
        }
      }
      else
        return expr;
    } // func WrapDebugInfo

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

    private static Expression ParseExpression(Scope scope, LuaLexer code, bool lDebugInfo)
    {
      // exp ::= expOr
      bool lWrap = false;
      if (lDebugInfo)
      {
        Token tStart = code.Current;
        Expression expr = ParseExpressionOr(scope, code, ref lWrap);
        if (lWrap)
          return WrapDebugInfo(scope, tStart, code.Current, expr);
        else
          return expr;
      }
      else
        return ParseExpressionOr(scope, code, ref lWrap);
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

        // and gibt das erste argument zurück, wenn es true ist
        expr = Expression.Condition(
          ToBooleanExpression(expr),
          ToObjectExpression(expr),
          ToObjectExpression(ParseExpressionAnd(scope, code, ref lWrap)),
          typeof(object));
        lWrap |= true;

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

        // and gibt das erste argument zurück, wenn es false ist
        expr = Expression.Condition(
          ToBooleanExpression(expr),
          ToObjectExpression(ParseExpressionCmd(scope, code, ref lWrap)),
          ToObjectExpression(expr),
          typeof(object));
        lWrap |= true;
        
        // Bitweises And
        //expr = Expression.Dynamic(scope.Runtime.GetBinaryOperationBinder(ExpressionType.And), typeof(object), expr, ParseExpressionCmd(scope, code));
      }

      return expr;
    } // func ParseExpressionAnd

    private static Expression ParseExpressionCmd(Scope scope, LuaLexer code, ref bool lWrap)
    {
      // expCmd ::= expCon { ( < | > | <= | >= | ~= | == ) expCon}
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

        expr = Expression.Dynamic(scope.Runtime.GetBinaryOperationBinder(typ), typeof(object), ToObjectExpression(expr), ToObjectExpression(ParseExpressionCon(scope, code, ref lWrap)));
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
          exprs[i] = exprs[i].Type == typeof(string) ?
            exprs[i] :
            RuntimeHelperConvertExpression(ToObjectExpression(exprs[i]), typeof(string));
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

        expr = Expression.Dynamic(scope.Runtime.GetBinaryOperationBinder(typ), typeof(object), ToObjectExpression(expr), ToObjectExpression(ParseExpressionMultiply(scope, code, ref lWrap)));
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

        expr = Expression.Dynamic(scope.Runtime.GetBinaryOperationBinder(typ), typeof(object), ToObjectExpression(expr), ToObjectExpression(ParseExpressionUnary(scope, code, ref lWrap)));
        lWrap |= true;
      }
    } // func ParseExpressionUnary

    private static Expression ParseExpressionUnary(Scope scope, LuaLexer code, ref bool lWrap)
    {
      // expUn ::= { 'not' | - | # } expPow
      if (code.Current.Typ == LuaToken.KwNot)
      {
        code.Next();
        lWrap |= true;
        return Expression.Dynamic(scope.Runtime.GetUnaryOperationBinary(ExpressionType.Not), typeof(object), ToObjectExpression(ParseExpressionUnary(scope, code, ref lWrap)));
      }
      else if (code.Current.Typ == LuaToken.Minus)
      {
        code.Next();
        lWrap |= true;
        return Expression.Dynamic(scope.Runtime.GetUnaryOperationBinary(ExpressionType.Negate), typeof(object), ToObjectExpression(ParseExpressionUnary(scope, code, ref lWrap)));
      }
      else if (code.Current.Typ == LuaToken.Cross)
      {
        code.Next();
        lWrap |= true;
        return Expression.Dynamic(scope.Runtime.GetGetMemberBinder("Length"), typeof(object), ToObjectExpression(ParseExpressionUnary(scope, code, ref lWrap)));
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
        return Expression.Dynamic(scope.Runtime.GetBinaryOperationBinder(ExpressionType.Power), typeof(object),
          ToObjectExpression(expr), ParseExpressionPower(scope, code, ref lWrap));
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

        StringBuilder sbTypeName = new StringBuilder();

        FetchToken(LuaToken.BracketOpen, code);

        // Lies den Typ aus
        sbTypeName.Append(FetchToken(LuaToken.Identifier, code).Value);
        while (code.Current.Typ == LuaToken.Dot)
        {
          code.Next();
          sbTypeName.Append('.');
          sbTypeName.Append(FetchToken(LuaToken.Identifier, code).Value);
        }
        FetchToken(LuaToken.Comma, code);

        Expression expr = ParseExpression(scope, code, ref lWrap);

        FetchToken(LuaToken.BracketClose, code);

        lWrap |= true;
        return RuntimeHelperConvertExpression(expr, GetType(t, sbTypeName.ToString()));
      }
      else
        return ParsePrefix(scope, code, ref lWrap).GenerateGet(scope);
    } // func ParseExpressionCast

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
        default:
          Type type = Lua.GetType(sTypeName);
          if (type == null)
            throw ParseError(t, String.Format("Type '{0}' not found.", sTypeName));
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
      // create empty block, that can used as an loop
      LoopScope loopScope = new LoopScope(scope);
      loopScope.AddExpression(Expression.Label(loopScope.ContinueLabel));

      // do block end;
      FetchToken(LuaToken.KwDo, code);
      ParseBlock(loopScope, code);
      FetchToken(LuaToken.KwEnd, code);

      loopScope.AddExpression(Expression.Label(loopScope.BreakLabel));
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
      var loopVar = FetchToken(LuaToken.Identifier, code);
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
        ParameterExpression loopVarParameter = loopScope.RegisterVariable(typeof(object), loopVar.Value);

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
        loopVars.Add(loopScope.RegisterVariable(typeof(object), loopVar.Value));
        while (code.Current.Typ == LuaToken.Comma)
        {
          code.Next();
          loopVars.Add(loopScope.RegisterVariable(typeof(object), FetchToken(LuaToken.Identifier, code).Value));
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
      ParameterExpression loopVar = loopScope.RegisterVariable(typeof(object), FetchToken(LuaToken.Identifier, code).Value);
      
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

      loopScope.InsertExpression(0, Expression.Assign(loopVar, Expression.Property(varEnumerator, piCurrent)));
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
      ParameterExpression internLoopVar = Expression.Variable(typeof(object), csVar);
      ParameterExpression endVar = Expression.Variable(typeof(object), csEnd);
      ParameterExpression stepVar = Expression.Variable(typeof(object), csStep);
      LabelTarget labelLoop = Expression.Label("#loop");

      // Erzeuge CodeBlock
      loopScope.InsertExpression(0, Expression.Assign(loopVar, internLoopVar));
      
      // Erzeuge den Schleifenblock
      return Expression.Block(new ParameterExpression[] { internLoopVar, endVar, stepVar },
        Expression.Assign(internLoopVar, Expression.Convert(loopStart, typeof(object))),
        Expression.Assign(endVar, Expression.Convert(loopEnd, typeof(object))),
        Expression.Assign(stepVar, Expression.Convert(loopStep, typeof(object))),

        Expression.Label(labelLoop),

        Expression.IfThenElse(
          Expression.Convert(
            Expression.Dynamic(loopScope.Runtime.GetBinaryOperationBinder(ExpressionType.Or), typeof(object),
              Expression.Dynamic(loopScope.Runtime.GetBinaryOperationBinder(ExpressionType.And), typeof(object),
                Expression.Dynamic(loopScope.Runtime.GetBinaryOperationBinder(ExpressionType.GreaterThan), typeof(object), stepVar, Expression.Constant(0, typeof(int))),
                Expression.Dynamic(loopScope.Runtime.GetBinaryOperationBinder(ExpressionType.LessThanOrEqual), typeof(object), internLoopVar, endVar)
              ),
              Expression.Dynamic(loopScope.Runtime.GetBinaryOperationBinder(ExpressionType.And), typeof(object),
                Expression.Dynamic(loopScope.Runtime.GetBinaryOperationBinder(ExpressionType.LessThanOrEqual), typeof(object), stepVar, Expression.Constant(0, typeof(int))),
                Expression.Dynamic(loopScope.Runtime.GetBinaryOperationBinder(ExpressionType.GreaterThanOrEqual), typeof(object), internLoopVar, endVar)
              )
            ),
            typeof(bool)
          ),
          loopScope.ExpressionBlock,
          Expression.Goto(loopScope.BreakLabel)
        ),
        Expression.Label(loopScope.ContinueLabel),

        Expression.Assign(internLoopVar, Expression.Dynamic(loopScope.Runtime.GetBinaryOperationBinder(ExpressionType.Add), typeof(object), internLoopVar, stepVar)),

        Expression.Goto(labelLoop),
        Expression.Label(loopScope.BreakLabel)
      );
    } // func GenerateForLoop

    private static Expression GenerateForLoop(LoopScope loopScope, List<ParameterExpression> loopVars, Expression[] explist)
    {
      const string csFunc = "#f";
      const string csState = "#s";
      const string csVar = "#var";

      ParameterExpression varTmp = Expression.Variable(typeof(object[]), "#tmp");
      ParameterExpression varFunc = Expression.Variable(typeof(Delegate), csFunc);
      ParameterExpression varState = Expression.Variable(typeof(object), csState);
      ParameterExpression varVar = Expression.Variable(typeof(object), csVar);
      
      // Convert the parameters
      if (explist.Length > 1)
        for (int i = 0; i < explist.Length; i++)
          explist[i] = Expression.Convert(explist[i], typeof(object));

      // local var1, ..., varn = tmp;
      for (int i = 0; i < loopVars.Count; i++)
        loopScope.InsertExpression(i, Expression.Assign(loopVars[i], Parser.RuntimeHelperExpression(LuaRuntimeHelper.GetObject, varTmp, Expression.Constant(i))));
      return Expression.Block(new ParameterExpression[] { varTmp, varFunc, varState, varVar },
        // fill the local loop variables initial
        // local #f, #s, #var = explist
        Expression.Assign(varTmp,
          explist.Length == 1 && explist[0].Type == typeof(object[]) ? explist[0] : Parser.RuntimeHelperExpression(LuaRuntimeHelper.ReturnResult, Expression.NewArrayInit(typeof(object), explist))
        ),
        Expression.Assign(varFunc, Expression.Convert(Parser.RuntimeHelperExpression(LuaRuntimeHelper.GetObject, varTmp, Expression.Constant(0, typeof(int))), typeof(Delegate))),
        Expression.Assign(varState, Parser.RuntimeHelperExpression(LuaRuntimeHelper.GetObject, varTmp, Expression.Constant(1, typeof(int)))),
        Expression.Assign(varVar, Parser.RuntimeHelperExpression(LuaRuntimeHelper.GetObject, varTmp, Expression.Constant(2, typeof(int)))),

        Expression.Label(loopScope.ContinueLabel),

        // local tmp = f(s, var)
        Expression.Assign(varTmp, Expression.Dynamic(Lua.FunctionResultBinder, typeof(object[]), Expression.Dynamic(loopScope.Runtime.GetInvokeBinder(new CallInfo(2)), typeof(object), varFunc, varState, varVar))),

        // var = tmp[0]
        Expression.Assign(varVar, Parser.RuntimeHelperExpression(LuaRuntimeHelper.GetObject, varTmp, Expression.Constant(0, typeof(int)))),

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
      
      if (lLocal) // Nur ein Identifier ist erlaubt
      {
        var t = FetchToken(LuaToken.Identifier, code);
        ParameterExpression funcVar = scope.RegisterVariable(typeof(object), t.Value);
        scope.AddExpression(
          Expression.Assign(
            funcVar,
            ToObjectExpression(ParseLamdaDefinition(scope, code, funcVar.Name, false))
          )
        );
      }
      else // Liste mit Identifiern ist möglich
      {
        StringBuilder sbFullMemberName = new StringBuilder();
        bool lGenerateSelf = false;
        // Member mit der Startet
        Expression assignee = scope.LookupParameter(csEnv);
        string sMember = FetchToken(LuaToken.Identifier, code).Value;
        sbFullMemberName.Append(sMember);

        // Es gibt die Möglichkeit mehrere Member anzugegeben
        while (code.Current.Typ == LuaToken.Dot)
        {
          code.Next();

          // Erzeuge eine GetMember für den aktuelen Assignee
          assignee = Expression.Dynamic(scope.Runtime.GetGetMemberBinder(sMember), typeof(object), assignee);
          sMember = FetchToken(LuaToken.Identifier, code).Value;
          sbFullMemberName.Append('.').Append(sMember);
        }
        if (code.Current.Typ == LuaToken.Colon)
        {
          code.Next();

          // Erzeuge eine GetMember für den aktuelen Assignee
          lGenerateSelf = true;
          assignee = Expression.Dynamic(scope.Runtime.GetGetMemberBinder(sMember), typeof(object), assignee);
          sMember = FetchToken(LuaToken.Identifier, code).Value;
          sbFullMemberName.Append('.').Append(sMember);
        }

        scope.AddExpression(Expression.Dynamic(scope.Runtime.GetSetMemberBinder(sMember), typeof(object), assignee, ParseLamdaDefinition(scope, code, sMember, lGenerateSelf)));
      }
    } // proc ParseLamdaDefinition

    private static Expression ParseLamdaDefinition(Scope parent, LuaLexer code, string sName, bool lSelfParameter)
    {
      List<ParameterExpression > parameters = new List<ParameterExpression>();
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
          parameters.Add(scope.RegisterParameter(typeof(object[]), csArgList));
        }
        else
        {
          parameters.Add(scope.RegisterParameter(typeof(object), code.Current.Value));
          code.Next();

          while (code.Current.Typ == LuaToken.Comma)
          {
            code.Next();
            if (code.Current.Typ == LuaToken.DotDotDot)
            {
              code.Next();
              parameters.Add(scope.RegisterParameter(typeof(object[]), csArgList)); // last argument
              break;
            }
            else
              parameters.Add(scope.RegisterParameter(typeof(object), FetchToken(LuaToken.Identifier, code).Value));
          }
        }
      }
      FetchToken(LuaToken.BracketClose, code);

      // Lese den Code-Block
      ParseBlock(scope, code);

      FetchToken(LuaToken.KwEnd, code);
      return Expression.Lambda(scope.ExpressionBlock, scope.Runtime.CreateEmptyChunk(sName).Name, parameters);
    } // proc ParseLamdaDefinition

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
        
        //scope.AddExpression(tableVar);
        scopeTable.AddExpression(Expression.Convert(tableVar, typeof(object)));
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
          Expression.Dynamic(scope.Runtime.GetSetIndexMember(new CallInfo(1)), typeof(object),
            tableVar,
            ToObjectExpression(index),
            ToObjectExpression(ParseExpression(scope, code, scope.EmitDebug))
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
            ToObjectExpression(ParseExpression(scope, code, scope.EmitDebug))
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
              Expression.Convert(expr, typeof(object)), 
              Expression.Constant(iIndex, typeof(int))
            )
          );
        }
        else // Erster Wert wird zugewiesen
        {
          scope.AddExpression(
            Expression.Dynamic(scope.Runtime.GetSetIndexMember(new CallInfo(1)), typeof(object),
              tableVar,
              Expression.Constant(iIndex++, typeof(int)),
              ToObjectExpression(expr)
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
        throw ParseError(code.Current, String.Format("Unexpected token '{0}'. '{1}' expected.", code.Current.Typ, typ));
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
