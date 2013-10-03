using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
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
  internal static class Parser
  {
    private const string csReturnLabel = "#return";
    private const string csBreakLabel = "#break";
    private const string csContinueLabel = "#continue";
    private const string csEnv = "_ENV";

    #region -- class Scope ------------------------------------------------------------

    ///////////////////////////////////////////////////////////////////////////////
    /// <summary>Scope, der einen ExpressionBlock bildet.</summary>
    private class Scope
    {
      private Scope parent;   // Parent-Scope auf den zugegriffen werden kann
      private Dictionary<string, ParameterExpression> scopeVariables = null; // Lokal deklarierte Variablen
      private List<Expression> block = new List<Expression>();  // Instructions in diesem Block
      private bool lBlockGenerated = false; // Wurde der Block generiert

      #region -- Ctor -----------------------------------------------------------------

      /// <summary>Erzeugt einen Scope</summary>
      /// <param name="parent"></param>
      public Scope(Scope parent)
      {
        this.parent = parent;
      } // ctor

      #endregion

      #region -- LookupParameter ------------------------------------------------------

      /// <summary>Erzeugt eine neue lokale Variable innerhalb des Scops.</summary>
      /// <param name="type">Type der Variable</param>
      /// <param name="sName">Name der Variable</param>
      /// <returns>Die Expression der Variable, mit der auf Sie zugegriffen werden kann.</returns>
      public ParameterExpression RegisterVariable(Type type, string sName)
      {
        if (scopeVariables == null)
          scopeVariables = new Dictionary<string, ParameterExpression>();

        return scopeVariables[sName] = Expression.Variable(type, sName);
      } // proc RegisterParameter

      /// <summary>Sucht nach Parameter (die Variablen) innerhalb des Scope und seinen Eltern.</summary>
      /// <param name="sName">Name der Variable</param>
      /// <returns>Die Expression der Variable/Parameter oder <c>null</c></returns>
      public virtual ParameterExpression LookupParameter(string sName)
      {
        // Suche den Parameter im aktuellen Scope
        ParameterExpression p;
        if (scopeVariables != null && scopeVariables.TryGetValue(sName, out p))
          return p;

        if (parent != null)
          return parent.LookupParameter(sName);
        else
          return null;
      } // func LookupParameter

      #endregion

      #region -- LookupLabel ----------------------------------------------------------

      /// <summary>Erzeugt ein benanntes Label</summary>
      /// <param name="type">Rückgabetyp an diesem Label</param>
      /// <param name="sName">Name des Labels</param>
      /// <returns>Sprungziel zum Label</returns>
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

      public void InsertExpression(Expression expr)
      {
        CheckBlockGenerated();
        block.Insert(0, expr);
      } // proc AddExpression

      public void AddExpression(Expression expr)
      {
        CheckBlockGenerated();
        block.Add(expr);
      } // proc AddExpression

      public void AddExpression(params Expression[] expr)
      {
        CheckBlockGenerated();
        block.AddRange(expr);
      } // proc AddExpression

      /// <summary>Schließt den ExpressionBlock ab und gibt ihn zurück.</summary>
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

      public virtual Lua Runtime { get { return parent.Runtime; } }
    } // class Scope

    #endregion

    #region -- class LoopScope --------------------------------------------------------

    ///////////////////////////////////////////////////////////////////////////////
    /// <summary>Spezieller Scope für die Verwaltung von Schleifen.</summary>
    private class LoopScope : Scope
    {
      private bool lAutoEmitLabels;
      private LabelTarget continueLabel = Expression.Label(csContinueLabel);
      private LabelTarget breakLabel = Expression.Label(csBreakLabel);

      #region -- Ctor -----------------------------------------------------------------

      /// <summary>Erzeugt einen Scope für die Schleifen.</summary>
      /// <param name="parent"></param>
      /// <param name="lAutoEmitLabels">Sollen die Labels automatisch eingefügt werden</param>
      public LoopScope(Scope parent, bool lAutoEmitLabels = true)
        : base(parent)
      {
        this.lAutoEmitLabels = lAutoEmitLabels;
        if (lAutoEmitLabels)
          AddExpression(Expression.Label(continueLabel));
      } // ctor

      #endregion

      #region -- LookupLabel ----------------------------------------------------------

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

      public override Expression ExpressionBlock
      {
        get
        {
          if (lAutoEmitLabels)
            AddExpression(Expression.Label(breakLabel));
          return base.ExpressionBlock;
        }
      } // prop ExpressionBlock

      public LabelTarget BreakLabel { get { return breakLabel; } }
      public LabelTarget ContinueLabel { get { return continueLabel; } }
    } // class LambdaScope

    #endregion

    #region -- class LambdaScope ------------------------------------------------------

    ///////////////////////////////////////////////////////////////////////////////
    /// <summary>Lambda-Scope für die Verwaltung von Labels und Parametern.</summary>
    private class LambdaScope : Scope
    {
      private LabelTarget returnLabel = Expression.Label(typeof(object[]), csReturnLabel);
      private Dictionary<string, LabelTarget> labels = null;
      private Dictionary<string, ParameterExpression> parameters = new Dictionary<string, ParameterExpression>();

      #region -- Ctor -----------------------------------------------------------------

      /// <summary>Erzeugt einen Lambda-Scope für die Verwaltung von Labels und Parametern.</summary>
      /// <param name="parent"></param>
      public LambdaScope(Scope parent)
        : base(parent)
      {
      } // ctor

      #endregion

      #region -- RegisterParameter, LookupParameter -----------------------------------

      /// <summary>Ermöglicht es einen Parameter für die Lambda-Funktion zu definieren.</summary>
      /// <param name="type"></param>
      /// <param name="sName"></param>
      /// <returns></returns>
      public ParameterExpression RegisterParameter(Type type, string sName)
      {
        return parameters[sName] = Expression.Parameter(type, sName);
      } // proc RegisterParameter

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

        // Suche das erste passende Label, in falscher Reihen
        LabelTarget l;
        if (labels.TryGetValue(sName, out l))
          return l;

        // Lege das Label erstmal an
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
    /// <summary></summary>
    private class GlobalScope : LambdaScope
    {
      private Lua runtime;

      public GlobalScope(Lua runtime)
        : base(null)
      {
        this.runtime = runtime;
      } // ctor

      public override Lua Runtime { get { return runtime; } }
    } // class GlobalScope

    #endregion

    #region -- class PrefixMemberInfo -------------------------------------------------

    ///////////////////////////////////////////////////////////////////////////////
    /// <summary>Mini-Parse-Tree für die Prefixauflösung</summary>
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
        if (Instance != null && Member == null && Indices != null && Arguments == null)
        {
          // Setzen eines Indexes
          Expression[] r = new Expression[Indices.Length + 2];
          r[0] = Instance;
          Array.Copy(Indices, 0, r, 1, Indices.Length);
          r[r.Length - 1] = exprToSet;
          return Expression.Dynamic(scope.Runtime.GetSetIndexMember(new CallInfo(Indices.Length)), typeof(object), r);
        }
        else if (Instance != null && Member != null && Indices == null && Arguments == null)
        {
          // Setzen eines Members
          return Expression.Dynamic(scope.Runtime.GetSetMemberBinder(Member), typeof(object), Instance, exprToSet);
        }
        else if (Instance != null && Member == null && Indices == null && Arguments == null && Instance is ParameterExpression)
        {
          // Setzen einer Variable
          return Expression.Assign(Instance, Expression.Convert(exprToSet, typeof(object)));
        }
        else
          throw ParseError(Position, "Expression is not assignable");
      } // func GenerateSet

      public Expression GenerateGet(Scope scope)
      {
        if (Instance != null && Member == null && Indices != null && Arguments == null)
        {
          // Hole einen Index ab
          Expression[] r = new Expression[Indices.Length + 1];
          r[0] = Lua.ToObjectExpression(Instance); // Setze das Objekt
          Array.Copy(Indices, 0, r, 1, Indices.Length); // Setze die Indices

          Instance = Expression.Dynamic(scope.Runtime.GetGetIndexMember(new CallInfo(Indices.Length)), typeof(object), r);
          Indices = null;
        }
        else if (Instance != null && Member != null && Indices == null && Arguments == null)
        {
          // Hole einen Member ab
          Instance = Expression.Dynamic(scope.Runtime.GetGetMemberBinder(Member), typeof(object), Lua.ToObjectExpression(Instance));
          Member = null;
        }
        else if (Instance != null && Member == null && Indices == null && Arguments == null)
        {
          // Es handelt schon um eine Instanz
        }
        else if (Instance != null && Indices == null && Arguments != null)
        {
          Expression[] r = new Expression[Arguments.Length + 1];
          r[0] = Lua.ToObjectExpression(Instance); // Delegate
          // es werden alle Parameter auf Object umgewandelt, außer der letzte
          for (int i = 0; i < Arguments.Length - 1; i++)
            r[i + 1] = Lua.ToObjectExpression(Arguments[i], false); // Parameter nur das erste Objekt
          if (Arguments.Length > 0)
            r[r.Length - 1] = Arguments[Arguments.Length - 1]; // Behalte den Typ bei

          // Funktionen geben immer ein Array zurück
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

        return Instance;
      } // func GenerateInstance

      public Token Position { get; set; }
      public Expression Instance { get; private set; }
      public string Member { get; set; }
      public Expression[] Indices { get; set; }
      public Expression[] Arguments { get; set; }
    } // class PrefixMemberInfo

    #endregion

    #region -- Parse Chunk, Block -----------------------------------------------------

    public static LambdaExpression ParseChunk(Lua runtime, LuaLexer code, IEnumerable<KeyValuePair<string, Type>> args)
    {
      List<ParameterExpression> parameters = new List<ParameterExpression>();
      var globalScope = new GlobalScope(runtime);

      // Registriere die Parameter
      parameters.Add(globalScope.RegisterParameter(typeof(Lua), csEnv)); // Environment, also der Verweis auf die Lua-Instanz
      if (args != null)
        foreach (var c in args)
          parameters.Add(globalScope.RegisterParameter(c.Value, c.Key)); // Füge weitere Parameter an

      // Hole den ersten Token
      if (code.Current == null)
        code.Next();

      // Hole den Chunknamen vom Parser
      string sChunkName = Path.GetFileNameWithoutExtension(code.Current.Start.FileName);

      // Erzeuge den Block
      ParseBlock(globalScope, code);

      if (code.Current.Typ != LuaToken.Eof)
        throw ParseError(code.Current, "Unexpected eof.");

      // Lese den Chunk ein
      return Expression.Lambda(globalScope.ExpressionBlock, sChunkName, parameters);
    } // func ParseChunk

    private static void ParseBlock(Scope scope, LuaLexer code)
    {
      // Lese die Statement
      bool lLoop = true;
      while (lLoop)
        switch (code.Current.Typ)
        {
          case LuaToken.Eof: // Ende der Datei erreicht
            lLoop = false;
            break;

          case LuaToken.KwReturn: //  Return steht nur am Ende eines Block
            ParseReturn(scope, code);
            break;

          case LuaToken.KwBreak: // Break darf nur am Ende eines Blocks stehen
            ParseBreak(scope, code);
            lLoop = false;
            break;

          case LuaToken.Semicolon: // Anweisungsende, einfach überlesen
            code.Next();
            break;

          default:
            if (!ParseStatement(scope, code))
              lLoop = false;
            break;
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
        exprReturnValue = Lua.RuntimeHelperExpression(Lua.RuntimeHelper.ReturnResult,
          Expression.NewArrayInit(typeof(object),
            from c in ParseExpressionList(scope, code) select Expression.Convert(c, typeof(object))
            )
          );
      }
      else
        exprReturnValue = Expression.NewArrayInit(typeof(object));

      if (code.Current.Typ == LuaToken.Semicolon)
        code.Next();

      scope.AddExpression(Expression.Goto(scope.LookupLabel(typeof(object[]), csReturnLabel), exprReturnValue));
    } // func ParseReturn

    private static bool IsExpressionStart(LuaLexer code)
    {
      return code.Current.Typ == LuaToken.BracketOpen ||
        code.Current.Typ == LuaToken.Identifier ||
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
        case LuaToken.Identifier: // Irgendwas Expressionartiges
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

        case LuaToken.ColonColon: // Beginn eines Labels
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
          prefixes.Add(ParsePrefix(scope, code));
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
              Lua.ToObjectExpression(expr.Current) :
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
                Lua.ToObjectExpression(l) :
                Expression.Constant(null, typeof(object))));
            }
            else
            {
              // Generiere Zuweisung via t Variable
              if (l.Type == typeof(object[]) || typeof(object[]).IsAssignableFrom(l.Type))
              {
                v = scope.RegisterVariable(typeof(object[]), "#tmp");
                scope.AddExpression(Expression.Assign(v, l));
                scope.AddExpression(prefixes[iPrefix++].GenerateSet(scope, Lua.RuntimeHelperExpression(Lua.RuntimeHelper.GetObject, Expression.Convert(v, typeof(object[])), Expression.Constant(0, typeof(int)))));
              }
              else
                scope.AddExpression(prefixes[iPrefix++].GenerateSet(scope, Lua.ToObjectExpression(l)));
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
              scope.AddExpression(prefixes[iPrefix].GenerateSet(scope, Lua.RuntimeHelperExpression(Lua.RuntimeHelper.GetObject, Expression.Convert(v, typeof(object[])), Expression.Constant(iLastIndex, typeof(int)))));
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
      var expr = Lua.ToBooleanExpression(ParseExpression(scope, code));
      FetchToken(LuaToken.KwThen, code);

      scope.AddExpression(Expression.IfThenElse(expr, ParseIfElseBlock(scope, code), ParseElseStatement(scope, code)));
    } // proc ParseIfStatement

    private static Expression ParseElseStatement(Scope scope, LuaLexer code)
    {
      if (code.Current.Typ == LuaToken.KwElseif)
      {
        code.Next();
        var expr = Lua.ToBooleanExpression(ParseExpression(scope, code));
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
        throw new ArgumentException(); // Todo LuaException
    } // func ParseElseStatement

    private static Expression ParseIfElseBlock(Scope parent, LuaLexer code)
    {
      Scope scope = new Scope(parent);
      ParseBlock(scope, code);
      return scope.ExpressionBlock;
    } // func ParseIfElseBlock

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
          var expr = ParseExpression(scope, code);
          FetchToken(LuaToken.BracketClose, code);

          info = new PrefixMemberInfo(tStart, Lua.ToObjectExpression(expr), null, null, null);
          break;

        case LuaToken.Identifier:
          var t = FetchToken(LuaToken.Identifier, code);
          var p = scope.LookupParameter(t.Value);
          if (p == null) // Als globale Variable verwalten, da es keine locale Variable gibt
            info = new PrefixMemberInfo(tStart,scope.LookupParameter(csEnv), t.Value, null, null);
          else
            info = new PrefixMemberInfo(tStart, p, null, null, null);
          break;

        case LuaToken.String: // Literal String
          info = new PrefixMemberInfo(tStart, Expression.Constant(FetchToken(LuaToken.String, code).Value, typeof(string)), null, null, null);
          break;

        case LuaToken.Number: // Literal Zahl
          info = new PrefixMemberInfo(tStart,Lua.ParseNumber(FetchToken(LuaToken.Number, code)), null, null, null);
          break;

        case LuaToken.HexNumber: // Literal HexZahl
          info = new PrefixMemberInfo(tStart,Lua.ParseHexNumber(FetchToken(LuaToken.HexNumber, code)), null, null, null);
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
          case LuaToken.BracketSquareOpen: // Index als Suffix
            code.Next();
            info.GenerateGet(scope);
            info.Indices = ParseExpressionList(scope, code).ToArray();
            FetchToken(LuaToken.BracketSquareClose, code);
            break;

          case LuaToken.Dot: // Eigenschaft als Suffix abfrage
            code.Next();
            info.GenerateGet(scope);
            info.Member = FetchToken(LuaToken.Identifier, code).Value;
            break;

          case LuaToken.BracketOpen: // Argumentenliste
            info.GenerateGet(scope);
            info.Arguments = ParseArgumentList(scope, code);
            break;

          case LuaToken.BracketCurlyOpen: // LuaTable als Argument
            // Es handelt sich um ein Delegate
            info.GenerateGet(scope);
            info.Arguments = new Expression[] { ParseTableConstructor(scope, code) };
            break;

          case LuaToken.String: // String als Argument
            // Es handelt sich um ein Delegate
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

    #region -- Parse Expressions ------------------------------------------------------

    private static IEnumerable<Expression> ParseExpressionList(Scope scope, LuaLexer code)
    {
      while (true)
      {
        yield return ParseExpression(scope, code);

        // Noch eine Expression
        if (code.Current.Typ == LuaToken.Comma)
          code.Next();
        else
          break;
      }
    } // func ParseExpressionList

    private static Expression ParseExpression(Scope scope, LuaLexer code)
    {
      // exp ::= expOr
      return ParseExpressionOr(scope, code);
    } // func ParseExpression

    private static Expression ParseExpressionOr(Scope scope, LuaLexer code)
    {
      // expOr ::= expAnd { or expAnd}

      var expr = ParseExpressionAnd(scope, code);

      while (code.Current.Typ == LuaToken.KwOr)
      {
        code.Next();

        // and gibt das erste argument zurück, wenn es true ist
        expr = Expression.Condition(
          Lua.ToBooleanExpression(expr),
          Lua.ToObjectExpression(expr),
          Lua.ToObjectExpression(ParseExpressionAnd(scope, code)),
          typeof(object));
        // Bitweises Or
        //expr = Expression.Dynamic(scope.Runtime.GetBinaryOperationBinder(ExpressionType.OrElse), typeof(object), expr, ParseExpressionAnd(scope, code));
      }

      return expr;
    } // func ParseExpressionOr

    private static Expression ParseExpressionAnd(Scope scope, LuaLexer code)
    {
      // expAnd ::= expCmp { or expCmp}

      var expr = ParseExpressionCmd(scope, code);

      while (code.Current.Typ == LuaToken.KwAnd)
      {
        code.Next();

        // and gibt das erste argument zurück, wenn es false ist
        expr = Expression.Condition(
          Lua.ToBooleanExpression(expr),
          Lua.ToObjectExpression(ParseExpressionCmd(scope, code)),
          Lua.ToObjectExpression(expr),
          typeof(object));
        // Bitweises And
        //expr = Expression.Dynamic(scope.Runtime.GetBinaryOperationBinder(ExpressionType.And), typeof(object), expr, ParseExpressionCmd(scope, code));
      }

      return expr;
    } // func ParseExpressionAnd

    private static Expression ParseExpressionCmd(Scope scope, LuaLexer code)
    {
      // expCmd ::= expCon { ( < | > | <= | >= | ~= | == ) expCon}

      var expr = ParseExpressionCon(scope, code);

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

        expr = Expression.Dynamic(scope.Runtime.GetBinaryOperationBinder(typ), typeof(object), Lua.ToObjectExpression(expr), Lua.ToObjectExpression(ParseExpressionCon(scope, code)));
      }
    } // func ParseExpressionCmd

    private static Expression ParseExpressionCon(Scope scope, LuaLexer code)
    {
      // expCon::= expPlus { '..' expPlus}
      List<Expression> exprs = new List<Expression>();
      exprs.Add(ParseExpressionPlus(scope, code));

      while (code.Current.Typ == LuaToken.DotDot)
      {
        code.Next();
        exprs.Add(ParseExpressionPlus(scope, code));
      }

      // Erzeuge Concat
      if (exprs.Count > 1)
      {
        for (int i = 0; i < exprs.Count; i++)
        {
          exprs[i] = exprs[i].Type == typeof(string) ?
            exprs[i] :
            Expression.Convert(Lua.RuntimeHelperExpression(Lua.RuntimeHelper.Convert, Lua.ToObjectExpression(exprs[i]), Expression.Constant(typeof(string), typeof(Type))), typeof(string));
        }
        return Lua.RuntimeHelperExpression(Lua.RuntimeHelper.StringConcat, Expression.NewArrayInit(typeof(string), exprs));
      }
      else
        return exprs[0];
    } // func ParseExpressionCon

    private static Expression ParseExpressionPlus(Scope scope, LuaLexer code)
    {
      // expPlus ::= expMul { ( + | - ) expMul}

      var expr = ParseExpressionMultiply(scope, code);

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

        expr = Expression.Dynamic(scope.Runtime.GetBinaryOperationBinder(typ), typeof(object), Lua.ToObjectExpression(expr), Lua.ToObjectExpression(ParseExpressionMultiply(scope, code)));
      }
    } // func ParseExpressionPlus

    private static Expression ParseExpressionMultiply(Scope scope, LuaLexer code)
    {
      // expMul ::= expUn { ( * | / | % ) expUn}

      var expr = ParseExpressionUnary(scope, code);

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

        expr = Expression.Dynamic(scope.Runtime.GetBinaryOperationBinder(typ), typeof(object), Lua.ToObjectExpression(expr), Lua.ToObjectExpression(ParseExpressionUnary(scope, code)));
      }
    } // func ParseExpressionUnary

    private static Expression ParseExpressionUnary(Scope scope, LuaLexer code)
    {
      // expUn ::= { 'not' | - | # } expPow
      if (code.Current.Typ == LuaToken.KwNot)
      {
        code.Next();
        return Expression.Dynamic(scope.Runtime.GetUnaryOperationBinary(ExpressionType.Not), typeof(object), Lua.ToObjectExpression(ParseExpressionUnary(scope, code)));
      }
      else if (code.Current.Typ == LuaToken.Minus)
      {
        code.Next();
        return Expression.Dynamic(scope.Runtime.GetUnaryOperationBinary(ExpressionType.Negate), typeof(object), Lua.ToObjectExpression(ParseExpressionUnary(scope, code)));
      }
      else if (code.Current.Typ == LuaToken.Cross)
      {
        code.Next();
        return Expression.Dynamic(scope.Runtime.GetGetMemberBinder("Length"), typeof(object), Lua.ToObjectExpression(ParseExpressionUnary(scope, code)));
      }
      else
        return ParseExpressionPower(scope, code);
    } // func ParseExpressionUnary

    private static Expression ParseExpressionPower(Scope scope, LuaLexer code)
    {
      // expPow ::= cast [ ^ expPow ]
      Expression expr = ParseExpressionCast(scope, code);
      
      if (code.Current.Typ == LuaToken.Caret)
      {
        code.Next();
        return Expression.Dynamic(scope.Runtime.GetBinaryOperationBinder(ExpressionType.Power), typeof(object),
          Lua.ToObjectExpression(expr), ParseExpressionPower(scope, code));
      }
      else
        return expr;
    } // func ParseExpressionPower

    private static Expression ParseExpressionCast(Scope scope, LuaLexer code)
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

        Expression expr = ParseExpression(scope, code);

        FetchToken(LuaToken.BracketClose, code);

        return Expression.Convert(expr, GetType(t, sbTypeName.ToString()));
      }
      else
        return ParsePrefix(scope, code).GenerateGet(scope);
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
      // Erzeuge einen einfachen Block ohne Schleife
      FetchToken(LuaToken.KwDo, code);

      // Erzeuge die Schleife
      LoopScope loopScope = new LoopScope(scope);
      ParseBlock(loopScope, code);
      scope.AddExpression(loopScope.ExpressionBlock);
      
      // Ende
      FetchToken(LuaToken.KwEnd, code);
    } // ParseDoLoop

    private static void ParseWhileLoop(Scope scope, LuaLexer code)
    {
      LoopScope loopScope = new LoopScope(scope);

      // Lies die Bedingung
      FetchToken(LuaToken.KwWhile, code);

      loopScope.AddExpression(
        Expression.IfThenElse(
          Lua.ToBooleanExpression(ParseExpression(scope, code)), 
          Expression.Empty(), 
          Expression.Goto(loopScope.BreakLabel)
        )
      );

      // Erzeuge den CodeBlock
      FetchToken(LuaToken.KwDo, code);
      ParseBlock(loopScope, code);
      FetchToken(LuaToken.KwEnd, code);

      // Schleife für Goto
      Expression.Goto(loopScope.ContinueLabel);

      scope.AddExpression(loopScope.ExpressionBlock);
    } // func ParseWhileLoop

    private static void ParseRepeatLoop(Scope scope, LuaLexer code)
    {
      LoopScope loopScope = new LoopScope(scope);

      // Inhalt der Schleife
      FetchToken(LuaToken.KwRepeat, code);
      ParseBlock(loopScope, code);

      // Lies die Bedingung
      FetchToken(LuaToken.KwUntil, code);
      loopScope.AddExpression(
        Expression.IfThenElse(
          Lua.ToBooleanExpression(ParseExpression(scope, code)),
          Expression.Empty(),
          Expression.Goto(loopScope.ContinueLabel)
        )
      );

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
        Expression loopStart = ParseExpression(scope, code);
        FetchToken(LuaToken.Comma, code);
        Expression loopEnd = ParseExpression(scope, code);
        Expression loopStep;
        if (code.Current.Typ == LuaToken.Comma)
        {
          code.Next();
          loopStep = ParseExpression(scope, code);
        }
        else
          loopStep = Expression.Constant(1, typeof(int));

        LoopScope loopScope = new LoopScope(scope, false);
        ParameterExpression loopVarParameter = loopScope.RegisterVariable(typeof(object), loopVar.Value);

        FetchToken(LuaToken.KwDo, code);
        ParseBlock(loopScope, code);
        FetchToken(LuaToken.KwEnd, code);
        scope.AddExpression(GenerateForLoop(loopScope, loopVarParameter, loopStart, loopEnd, loopStep));
      }
      else
      {
        throw new NotImplementedException();
        /*
 The generic for statement works over functions, called iterators. On each iteration, the iterator function is called to produce a new value, stopping when this new value is nil. The generic for loop has the following syntax:

	stat ::= for namelist in explist do block end
	namelist ::= Name {‘,’ Name}

A for statement like

     for var_1, ···, var_n in explist do block end

is equivalent to the code:

     do
       local f, s, var = explist
       while true do
         local var_1, ···, var_n = f(s, var)
         if var_1 == nil then break end
         var = var_1
         block
       end
     end

Note the following:

    explist is evaluated only once. Its results are an iterator function, a state, and an initial value for the first iterator variable.
    f, s, and var are invisible variables. The names are here for explanatory purposes only.
    You can use break to exit a for loop.
    The loop variables var_i are local to the loop; you cannot use their values after the for ends. If you need these values, then assign them to other variables before breaking or exiting the loop.

*/
      }
    } // func ParseForLoop

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
      loopScope.InsertExpression(Expression.Assign(loopVar, internLoopVar));
      
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
            Lua.ToObjectExpression(ParseLamdaDefinition(scope, code, funcVar.Name, false))
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

      if (code.Current.Typ == LuaToken.Identifier)
      {
        parameters.Add(scope.RegisterParameter(typeof(object), FetchToken(LuaToken.Identifier, code).Value));
        while (code.Current.Typ == LuaToken.Comma)
        {
          code.Next();
          parameters.Add(scope.RegisterParameter(typeof(object), FetchToken(LuaToken.Identifier, code).Value));
        }
      }
      FetchToken(LuaToken.BracketClose, code);

      // Lese den Code-Block
      ParseBlock(scope, code);

      FetchToken(LuaToken.KwEnd, code);
      return Expression.Lambda(scope.ExpressionBlock, sName, parameters);
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
        scopeTable.AddExpression(Expression.Assign(tableVar, Lua.CreateEmptyTable()));

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
        return Lua.CreateEmptyTable();
      }
    } // func ParseTableConstructor

    private static void ParseTableField(ParameterExpression tableVar, Scope scope, LuaLexer code, ref int iIndex)
    {
      // field ::= '[' exp ']' '=' exp | Name '=' exp | exp
      if (code.Current.Typ == LuaToken.BracketSquareOpen)
      {
        // Lies den Index
        code.Next();
        var index = ParseExpression(scope, code);
        FetchToken(LuaToken.BracketSquareClose, code);
        FetchToken(LuaToken.Assign, code);

        // Erzeuge den Befehl
        scope.AddExpression(
          Expression.Dynamic(scope.Runtime.GetSetIndexMember(new CallInfo(1)), typeof(object),
            tableVar,
            Lua.ToObjectExpression(index),
            Lua.ToObjectExpression(ParseExpression(scope, code))
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
            Lua.ToObjectExpression(ParseExpression(scope, code))
          )
        );
      }
      else
      {
        Expression expr = ParseExpression(scope, code);

        if (code.Current.Typ == LuaToken.BracketCurlyClose) // Letzte Zuweisung, verteile ein eventuelles Array auf die Indices
        {
          scope.AddExpression(
            Lua.RuntimeHelperExpression(Lua.RuntimeHelper.TableSetObjects,
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
              Lua.ToObjectExpression(expr)
            )
          );
        }
      }
    } // proc ParseTableField

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
