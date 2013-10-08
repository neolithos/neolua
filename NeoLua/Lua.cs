using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Text;

namespace Neo.IronLua
{
  ///////////////////////////////////////////////////////////////////////////////
  /// <summary>Verwaltet eine Lua-Script Umgebung</summary>
  public partial class Lua : LuaTable
  {
    private bool lPrintExpressionTree = false;

    public Lua()
    {
      this["_VERSION"] = "NeoLua 5.2";
    } // ctor

    #region -- RegisterFunction, UnregisterFunction -----------------------------------

    public void RegisterFunction(string sName, Delegate function)
    {
      if (String.IsNullOrEmpty(sName))
        throw new ArgumentNullException("name");
      if (function == null)
        throw new ArgumentNullException("function");

      this[sName] = function;
    } // proc RegisterFunction

    /// <summary>Registers a type as an library.</summary>
    /// <param name="sName"></param>
    /// <param name="type"></param>
    public void RegisterPackage(string sName, Type type)
    {
      if (String.IsNullOrEmpty(sName))
        throw new ArgumentNullException("name");
      if (type == null)
        throw new ArgumentNullException("type");

      this[sName] = new LuaPackageProxy(type);
    } // func RegisterPackage

    #endregion

    #region -- DoChunk ----------------------------------------------------------------

    /// <summary>Führt die angegebene Datei aus.</summary>
    /// <param name="sFileName">Dateiname die gelesen werden soll.</param>
    /// <param name="args">Parameter für den Codeblock</param>
    /// <returns>Ergebnis der Ausführung.</returns>
    public object[] DoChunk(string sFileName, params KeyValuePair<string, object>[] args)
    {
      return DoChunk(sFileName, new StreamReader(sFileName), args);
    } // proc DoFile

    /// <summary>Führt den angegebene Stream aus.</summary>
    /// <param name="sr">Inhalt</param>
    /// <param name="sName">Name der Datei</param>
    /// <param name="args">Parameter für den Codeblock</param>
    /// <returns>Ergebnis der Ausführung.</returns>
    public object[] DoChunk(TextReader sr, string sName, params KeyValuePair<string, object>[] args)
    {
      return DoChunk(sName, sr, args);
    } // proc DoChunk

    /// <summary>Führt die angegebene Zeichenfolge aus.</summary>
    /// <param name="sCode">Code</param>
    /// <param name="sName">Name des Codes</param>
    /// <param name="args"Parameter für den Codeblock></param>
    /// <returns>Ergebnis der Ausführung.</returns>
    public object[] DoChunk(string sCode, string sName, params KeyValuePair<string, object>[] args)
    {
      return DoChunk(sName, new StringReader(sCode), args);
    } // func DoChunk

    private object[] DoChunk(string sChunkName, TextReader tr, KeyValuePair<string, object>[] args)
    {
      // Erzeuge die Parameter
      object[] callArgs;
      KeyValuePair<string, Type>[] callTypes;
      if (args != null)
      {
        callArgs = new object[args.Length];
        callTypes = new KeyValuePair<string, Type>[args.Length];
        for (int i = 0; i < args.Length; i++)
        {
          callArgs[i] = args[i].Value;
          callTypes[i] = new KeyValuePair<string, Type>(args[i].Key, args[i].Value == null ? typeof(object) : args[i].Value.GetType());
        }
      }
      else
      {
        callArgs = new object[0];
        callTypes = new KeyValuePair<string, Type>[0];
      }

      // Führe den Block aus
      Delegate dlg = CompileChunk(sChunkName, tr, callTypes);
      return ExecuteCompiledChunk(dlg, callArgs);
    } // func DoChunk

    #endregion

    #region -- Compile ----------------------------------------------------------------

    /// <summary>Erzeugt ein Delegate aus dem Code, ohne ihn auszuführen.</summary>
    /// <param name="sFileName">Dateiname die gelesen werden soll.</param>
    /// <param name="args">Parameter für den Codeblock</param>
    /// <returns>Delegate, welches erzeugt wurde.</returns>
    public Delegate CompileChunk(string sFileName, params KeyValuePair<string, Type>[] args)
    {
      return CompileChunk(sFileName, new StreamReader(sFileName), args);
    } // func CompileChunk

    /// <summary>Erzeugt ein Delegate aus dem Code, ohne ihn auszuführen.</summary>
    /// <param name="sr">Inhalt</param>
    /// <param name="sName">Name der Datei</param>
    /// <param name="args">Parameter für den Codeblock</param>
    /// <returns>Delegate, welches erzeugt wurde.</returns>
    public Delegate CompileChunk(TextReader tr, string sName, params KeyValuePair<string, Type>[] args)
    {
      return CompileChunk(sName, tr, args);
    } // func CompileChunk

    /// <summary>Erzeugt ein Delegate aus dem Code, ohne ihn auszuführen.</summary>
    /// <param name="sCode">Code, der das Delegate darstellt.</param>
    /// <param name="sName">Name des Delegates</param>
    /// <param name="args">Argumente</param>
    /// <returns>Delegate, welches erzeugt wurde.</returns>
    public Delegate CompileChunk(string sCode, string sName, params KeyValuePair<string, Type>[] args)
    {
      return CompileChunk(sName, new StringReader(sCode), args);
    } // func CompileChunk

    private Delegate CompileChunk(string sChunkName, TextReader tr, IEnumerable<KeyValuePair<string, Type>> args)
    { 
      using (LuaLexer l = new LuaLexer(sChunkName, tr))
      {
        LambdaExpression expr = Parser.ParseChunk(this, l, args);

        if (lPrintExpressionTree)
        {
          Console.WriteLine(Parser.ExpressionToString(expr));
          Console.WriteLine(new string('=', 79));
        }
        return expr.Compile();
      }
    } // func CompileChunk

    public object[] ExecuteCompiledChunk(Delegate chunk, params object[] callArgs)
    {
      object[] args = new object[callArgs == null ? 0 : callArgs.Length + 1];
      args[0] = this;
      if (callArgs != null)
        Array.Copy(callArgs, 0, args, 1, callArgs.Length);

      return (object[])chunk.DynamicInvoke(args);
    } // func ExecuteCompiledChunk

    #endregion

    internal bool PrintExpressionTree { get { return lPrintExpressionTree; } set { lPrintExpressionTree = value; } }
  } // class Lua
}
