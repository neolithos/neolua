using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.IO;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading;

namespace Neo.IronLua
{
  ///////////////////////////////////////////////////////////////////////////////
  /// <summary></summary>
  public static class Program
  {
    private enum Commands
    {
      Run,
      Exit,
      List,
      Load,
      Debug,
      Environment,
      Help
    } // enum Commands

    private static Lua lua = new Lua(); // create lua script compiler
    private static LuaGlobal global;
    private static bool lDebugMode = true;

    private static void WriteText(ConsoleColor textColor, string sText)
    {
      ConsoleColor clOld = Console.ForegroundColor;
      Console.ForegroundColor = textColor;
      Console.Write(sText);
      Console.ForegroundColor = clOld;
    } // proc WriteText

    private static void WriteVariable(object key, object value)
    {
      Console.Write("  ");
      if (key is int)
        WriteText(ConsoleColor.White, String.Format("[{0}]", key));
      else if (key is string)
        WriteText(ConsoleColor.White, key.ToString());
      else
      {
        WriteText(ConsoleColor.DarkGray, String.Format("({0})", key == null ? "object" : key.GetType().Name));
        WriteText(ConsoleColor.White, key == null ? String.Empty : key.ToString());
      }
      WriteText(ConsoleColor.Gray, " = ");
      WriteText(ConsoleColor.DarkGray, String.Format("({0})", value == null ? "object" : value.GetType().Name));

      string sValue = value == null ? String.Empty : value.ToString();
      int iLength = Console.WindowWidth - Console.CursorLeft;
      if (iLength < sValue.Length)
      {
        sValue = sValue.Substring(0, iLength - 3) + "...";
        WriteText(ConsoleColor.Gray, sValue);
      }
      else
      {
        WriteText(ConsoleColor.Gray, sValue);
        Console.WriteLine();
      }
    } // proc WriteVariable

    public static void WriteCommand(string sCommand, string sDescription)
    {
      Console.CursorLeft = 2;
      WriteText(ConsoleColor.Gray, sCommand);
      Console.CursorLeft = 12;
      WriteText(ConsoleColor.DarkGray, sDescription);
      Console.WriteLine();
    } // proc WriteCommand

    private static void WriteError(string sText)
    {
      WriteText(ConsoleColor.DarkRed, sText);
      Console.WriteLine();
    } // proc WriteError

    private static void WriteException(Exception e)
    {
      WriteText(ConsoleColor.DarkRed, e.GetType().Name + ": ");
      WriteText(ConsoleColor.Red, e.Message);
      Console.WriteLine();
      LuaExceptionData eData = LuaExceptionData.GetData(e);
      WriteText(ConsoleColor.DarkGray, eData.GetStackTrace(0, true));
      Console.WriteLine();
      if (e.InnerException != null)
      {
        WriteText(ConsoleColor.Gray, ">>> INNER EXCEPTION <<<");
        WriteException(e.InnerException);
      }
    } // proc WriteException

    private static void CreateFreshEnvironment()
    {
      // create the enviroment
      global = lua.CreateEnvironment();

      global["console"] = LuaType.GetType(typeof(Console));
      global["read"] = new Func<string, string>(ReadLine);
    } // proc CreateFreshEnvironment

    private static string ReadLine(string sLabel)
    {
      Console.Write(sLabel);
      Console.Write(": ");
      return Console.ReadLine();
    } // func ReadLine

    private static bool IsCommand(string sInput)
    {
      for (int i = 0; i < sInput.Length; i++)
        if (!Char.IsWhiteSpace(sInput[i]))
          return sInput[i] == ':';
      return false;
    } // func HasInputData

    private static Commands InputCommand(out string sLine)
    {
      StringBuilder sbLine = new StringBuilder();

      // i need a clear line
      if (Console.CursorLeft > 0)
        Console.WriteLine();

      // remind the start point
      Console.ForegroundColor = ConsoleColor.Gray;
      Console.Write("> ");

      while (true)
      {
        string sInput = Console.ReadLine();
        if (sInput.Length == 0)
        {
          sLine = sbLine.ToString();
          return Commands.Run;
        }
        else if (IsCommand(sInput))
        {
          string sCommand = sInput.Trim();
          if (sCommand.StartsWith(":q", StringComparison.OrdinalIgnoreCase))
          {
            sLine = String.Empty;
            return Commands.Exit;
          }
          if (sCommand.StartsWith(":h", StringComparison.OrdinalIgnoreCase))
          {
            sLine = String.Empty;
            return Commands.Help;
          }
          else if (sCommand.StartsWith(":load", StringComparison.OrdinalIgnoreCase))
          {
            sLine = sCommand.Substring(5).Trim();
            return Commands.Load;
          }
          else if (sCommand.StartsWith(":list", StringComparison.OrdinalIgnoreCase))
          {
            sLine = sCommand.Substring(5).Trim();
            return Commands.List;
          }
          else if (sCommand.StartsWith(":c", StringComparison.OrdinalIgnoreCase))
          {
            sbLine.Clear();
            Console.WriteLine("> ");
          }
          else if (sCommand.StartsWith(":debugoff", StringComparison.OrdinalIgnoreCase))
          {
            sLine = Boolean.FalseString;
            return Commands.Debug;
          }
          else if (sCommand.StartsWith(":debugon", StringComparison.OrdinalIgnoreCase))
          {
            sLine = Boolean.TrueString;
            return Commands.Debug;
          }
          else if (sCommand.StartsWith(":env", StringComparison.OrdinalIgnoreCase))
          {
            sLine = "true";
            return Commands.Environment;
          }
          else
            WriteError("Unkown command.");
        }
        else
        {
          sbLine.AppendLine(sInput);
          Console.Write("> ");
        }
      }
    } // func InputCommand

    private static void RunScript(Func<string> code, string sName)
    {
      try
      {
        Stopwatch sw = new Stopwatch();
        sw.Start();
        
        // compile chunk
        LuaChunk c = lua.CompileChunk(code(), sName, lDebugMode);
        string sCompileTime = String.Format("{0:N0} ms", sw.ElapsedMilliseconds);
        sw.Reset();
        sw.Start();

        // run chunk
        global.DoChunk(c);
        string sRunTime = String.Format("{0:N0} ms", sw.ElapsedMilliseconds);

        // print summary
        if (Console.CursorLeft > 0)
          Console.WriteLine();

        const string csCompile = "==> compile: ";
        const string csRuntime = " run: ";
        Console.CursorLeft = Console.WindowWidth - csCompile.Length - sCompileTime.Length - csRuntime.Length - sRunTime.Length - 1;
        WriteText(ConsoleColor.DarkGreen, csCompile);
        WriteText(ConsoleColor.Green, sCompileTime);
        WriteText(ConsoleColor.DarkGreen, csRuntime);
        WriteText(ConsoleColor.Green, sRunTime);
        Console.WriteLine();
      }
      catch (LuaParseException e)
      {
        WriteText(ConsoleColor.DarkRed, String.Format("Parse error at line {0:N0} (column: {1:N0}):", e.Line, e.Column));
        Console.WriteLine();
        WriteText(ConsoleColor.DarkRed, "  " + e.Message);
        Console.WriteLine();
      }
      catch (Exception e)
      {
        Exception ex = e is TargetInvocationException ? e.InnerException : e;
        WriteException(ex);
      }
    } // proc RunScript

    [STAThread]
    public static void Main(string[] args)
    {
      WriteText(ConsoleColor.Gray, "NeoLua Interactive Command"); Console.WriteLine();
      WriteText(ConsoleColor.DarkGray, "Version ");
      WriteText(ConsoleColor.White, String.Format("{0} ({1})", LuaGlobal.VersionString, Lua.Version));
      WriteText(ConsoleColor.DarkGray, " by neolithos");
      Console.WriteLine();
      Console.WriteLine();
      WriteText(ConsoleColor.DarkGray, "  source code at ");
      WriteText(ConsoleColor.Gray, "http://neolua.codeplex.com");
      Console.WriteLine();
      WriteText(ConsoleColor.DarkGray, "  supported from ");
      WriteText(ConsoleColor.Gray, "http://tecware-gmbh.de");
      Console.WriteLine();
      Console.WriteLine();
      WriteText(ConsoleColor.DarkGray, "  Write ':h' for help.");
      Console.WriteLine();
      Console.WriteLine();

      CreateFreshEnvironment();

      // change to the samples directory
#if DEBUG
      string sSamples = Path.GetFullPath(@"..\..\Samples");
#else
      string sSamples = Path.GetFullPath("Samples");
#endif
      if (Directory.Exists(sSamples))
        Environment.CurrentDirectory = sSamples;

      while (true)
      {
        string sLine;
        switch (InputCommand(out sLine))
        {
          case Commands.List:
            // list all variables in global
            WriteText(ConsoleColor.DarkYellow, "List global:");
            Console.WriteLine();
            foreach (var c in global)
              WriteVariable(c.Key, c.Value);
            Console.WriteLine();
            break;
          case Commands.Load:
            RunScript(() => File.ReadAllText(Path.GetFullPath(sLine)), Path.GetFileName(sLine));
            break;
          case Commands.Debug:
            if (sLine == Boolean.TrueString)
            {
              WriteText(ConsoleColor.DarkYellow, "Compile emits stack trace information and runtime functions, now."); Console.WriteLine();
              lDebugMode = true;
            }
            else
            {
              WriteText(ConsoleColor.DarkYellow, "Compile creates dynamic functions, now."); Console.WriteLine();
              lDebugMode = false;
            }
            Console.WriteLine();
            break;
          case Commands.Environment:
            WriteText(ConsoleColor.DarkYellow, "New environment created."); Console.WriteLine();
            Console.WriteLine();
            CreateFreshEnvironment();
            break;
          case Commands.Help:
            WriteText(ConsoleColor.DarkYellow, "Commands:"); Console.WriteLine();
            WriteCommand(":q", "Exit the application.");
            WriteCommand(":list", "Lists all global variables.");
            WriteCommand(":load", "Loads the lua-script from a file.");
            WriteCommand(":debugoff", "Tell the compiler to emit no debug informations.");
            WriteCommand(":debugon", "Let the compiler emit debug informations.");
            WriteCommand(":c", "Clears the current script buffer.");
            WriteCommand(":env", "Create a fresh environment.");
            Console.WriteLine();
            break;
          case Commands.Run:
            if (sLine.Length > 0)
              RunScript(() => sLine, "line");
            break;
          case Commands.Exit:
            return;
        }
      }
    } // Main
  }
}
