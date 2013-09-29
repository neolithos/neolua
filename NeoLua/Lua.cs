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
    } // ctor

    #region -- RegisterFunction, UnregisterFunction -----------------------------------

    public void RegisterFunction(string sName, Delegate function)
    {
      if (String.IsNullOrEmpty(sName))
        throw new ArgumentNullException("name");
      if (function == null)
        throw new ArgumentNullException("function");

      SetValue(sName, function);
    } // proc RegisterFunction

    public void UnregisterFunction(string sName)
    {
      SetValue(sName, null);
    } // proc UnregisterFunction

    #endregion

    #region -- DoChunk ----------------------------------------------------------------

    /// <summary>Führt die angegebene Datei aus.</summary>
    /// <param name="sFileName">Dateiname die gelesen werden soll.</param>
    /// <param name="args">Parameter für den Codeblock</param>
    /// <returns>Ergebnis der Ausführung.</returns>
    public object[] DoChunk(string sFileName, params KeyValuePair<string, object>[] args)
    {
      return DoChunk(ScannerBuffer.Create(sFileName), args);
    } // proc DoFile

    /// <summary>Führt den angegebene Stream aus.</summary>
    /// <param name="sr">Inhalt</param>
    /// <param name="sName">Name der Datei</param>
    /// <param name="args">Parameter für den Codeblock</param>
    /// <returns>Ergebnis der Ausführung.</returns>
    public object[] DoChunk(TextReader sr, string sName, params KeyValuePair<string, object>[] args)
    {
      return DoChunk(ScannerBuffer.Create(null, sr, 0, sName), args);
    } // proc DoChunk

    /// <summary>Führt die angegebene Zeichenfolge aus.</summary>
    /// <param name="sCode">Code</param>
    /// <param name="sName">Name des Codes</param>
    /// <param name="args"Parameter für den Codeblock></param>
    /// <returns>Ergebnis der Ausführung.</returns>
    public object[] DoChunk(string sCode, string sName, params KeyValuePair<string, object>[] args)
    {
      return DoChunk(ScannerBuffer.CreateFromString(sCode, sName), args);
    } // func DoChunk

    private object[] DoChunk(ScannerBuffer code, KeyValuePair<string, object>[] args)
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
      Delegate dlg = CompileChunk(code, callTypes);
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
      return CompileChunk(ScannerBuffer.Create(sFileName), args);
    } // func CompileChunk

    /// <summary>Erzeugt ein Delegate aus dem Code, ohne ihn auszuführen.</summary>
    /// <param name="sr">Inhalt</param>
    /// <param name="sName">Name der Datei</param>
    /// <param name="args">Parameter für den Codeblock</param>
    /// <returns>Delegate, welches erzeugt wurde.</returns>
    public Delegate CompileChunk(TextReader tr, string sName, params KeyValuePair<string, Type>[] args)
    {
      return CompileChunk(ScannerBuffer.Create(null, tr, 0, sName), args);
    } // func CompileChunk

    /// <summary>Erzeugt ein Delegate aus dem Code, ohne ihn auszuführen.</summary>
    /// <param name="sCode">Code, der das Delegate darstellt.</param>
    /// <param name="sName">Name des Delegates</param>
    /// <param name="args">Argumente</param>
    /// <returns>Delegate, welches erzeugt wurde.</returns>
    public Delegate CompileChunk(string sCode, string sName, params KeyValuePair<string, Type>[] args)
    {
      return CompileChunk(ScannerBuffer.CreateFromString(sCode, sName), args);
    } // func CompileChunk

    private Delegate CompileChunk(ScannerBuffer code, IEnumerable<KeyValuePair<string, Type>> args)
    { 
      using (LuaLexer l = new LuaLexer(code))
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
