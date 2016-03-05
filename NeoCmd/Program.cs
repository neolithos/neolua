using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.Globalization;
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
		#region -- enum Commands ----------------------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
    private enum Commands
    {
			None,
      Run,
      Exit,
      List,
      Load,
      Debug,
      Environment,
			Cache,
      Help
    } // enum Commands

		#endregion

		#region -- class LuaCommandGlobal -------------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private sealed class LuaCommandGlobal : LuaGlobal
		{
			public LuaCommandGlobal(Lua lua)
				: base(lua)
			{
				this.LuaPackage.path += ";" + LuaLibraryPackage.ExecutingDirectoryPathVariable;
			} // ctor

			[LuaMember("read")]
			private string ReadLine()
			{
				return ReadLine(String.Empty);
			} // func ReadLine

			[LuaMember("read")]
			private string ReadLine(string sLabel)
			{
				Console.Write(sLabel);
				Console.Write("> ");
				return Console.ReadLine();
			} // func ReadLine

			/// <summary></summary>
			[
			LuaMember("console"),
			LuaMember("out")
			]
			private static LuaType LuaConsole { get { return LuaType.GetType(typeof(Console)); } }
		} // class LuaCommandGlobal

		#endregion

    #region -- class LuaTraceLineConsoleDebugger --------------------------------------

    private class LuaTraceLineConsoleDebugger : LuaTraceLineDebugger
    {
      private bool lInException = false;
      private LuaTraceLineEventArgs lastTracePoint = null;
      private Stack<LuaTraceLineEventArgs> frames = new Stack<LuaTraceLineEventArgs>();

      private void UpdateStackLine(int iTop, LuaTraceLineEventArgs e)
      {
        // 12345678.123 123
        Console.CursorLeft = Console.WindowWidth - 16;
        Console.CursorTop = iTop;
        if (e == null)
          Console.Write(new string(' ', 16));
        else
        {
          string sFileName = Path.GetFileName(e.SourceName);
          if (sFileName.Length > 12)
            sFileName = sFileName.Substring(0, 12);
          else if (sFileName.Length < 12)
            sFileName = sFileName.PadRight(12);

          string sLine = e.SourceLine.ToString().PadLeft(4);
          if (sLine.Length > 4)
            sLine = sLine.Substring(0, 4);

          WriteText(ConsoleColor.DarkGray, sFileName);
          WriteText(ConsoleColor.Gray, sLine);
        }
      } // proc UpdateStackLine

      private void UpdateStack(LuaTraceLineEventArgs e)
      {
        var stack = frames.ToArray();

        int iCurrentLeft = Console.CursorLeft;
        int iCurrentTop = Console.CursorTop;
        int iStart;
        int iTop = 0;
        try
        {
          if (stack.Length > 9)
            iStart = stack.Length - 8;
          else
            iStart = 0;

          for (int i = iStart; i < stack.Length; i++)
            UpdateStackLine(iTop++, stack[i]);
          UpdateStackLine(iTop++, e);

          while (iTop < 9)
            UpdateStackLine(iTop++, null);
        }
        finally
        {
          Console.CursorLeft = iCurrentLeft;
          Console.CursorTop = iCurrentTop;
        }
        Thread.Sleep(100);
      } // proc UpdateStack

      protected override void OnExceptionUnwind(LuaTraceLineExceptionEventArgs e)
      {
        if (!lInException)
        {
          lInException = true;
          Console.WriteLine();
          int iTop = Console.CursorTop;
          WriteText(ConsoleColor.DarkRed, "Exception: ");
          WriteText(ConsoleColor.Red, e.Exception.Message); Console.WriteLine();
          WriteText(ConsoleColor.Gray, "press any key to continue");
          Console.WriteLine();
          Console.ReadKey();
          int iClearTo = Console.CursorTop;

          Console.CursorLeft = 0;
 
          string sClear = new string(' ', Console.WindowWidth);
          for (int i = iTop; i <= iClearTo; i++)
          {
            Console.CursorTop = i;
            Console.WriteLine(sClear);
          }

          Console.CursorTop = iTop;
        }
        base.OnExceptionUnwind(e);
      } // proc OnExceptionUnwind

      protected override void OnFrameEnter(LuaTraceLineEventArgs e)
      {
        base.OnFrameEnter(e);
        if (lastTracePoint != null)
          frames.Push(lastTracePoint);
      } // proc OnFrameEnter

      protected override void OnTracePoint(LuaTraceLineEventArgs e)
      {
        UpdateStack(lastTracePoint = e);
        base.OnTracePoint(e);
      } // proc OnTracePoint

      protected override void OnFrameExit()
      {
        if (frames.Count > 0)
          frames.Pop();
        else if (frames.Count == 0)
          lInException = false;
        UpdateStack(null);
        base.OnFrameExit();
      } // proc OnFrameExit
    } // class LuaTraceLineConsoleDebugger

    #endregion

    private static Lua lua = new Lua(); // create lua script compiler
    private static LuaGlobal global;
    private static ILuaDebug debugEngine = LuaStackTraceDebugger.Default;
    private static ILuaDebug debugConsole = new LuaTraceLineConsoleDebugger();

    private static void WriteText(ConsoleColor textColor, string sText)
    {
      ConsoleColor clOld = Console.ForegroundColor;
      Console.ForegroundColor = textColor;
      Console.Write(sText);
      Console.ForegroundColor = clOld;
    } // proc WriteText

		private static string GetTypeName(Type type)
		{
			LuaType t = LuaType.GetType(type);
			return t.AliasName ?? t.Name;
		} // func GetTypeName

    private static void WriteVariable(object key, object value)
    {
      Console.Write("  ");
      if (key is int)
        WriteText(ConsoleColor.White, String.Format("[{0}]", key));
      else if (key is string)
        WriteText(ConsoleColor.White, key.ToString());
      else
      {
				WriteText(ConsoleColor.DarkGray, String.Format("({0})", key == null ? "object" : GetTypeName(key.GetType())));
        WriteText(ConsoleColor.White, key == null ? String.Empty : key.ToString());
      }
      WriteText(ConsoleColor.Gray, " = ");

			WriteVariableValue(value);
    } // proc WriteVariable

		private static void WriteVariableValue(object value)
		{
			// Convert value to string
			string sValue;
			string sType;
			if (value == null)
			{
				sValue = "nil";
				sType = GetTypeName(typeof(object));
			}
			else
			{
				sValue = (string)Lua.RtConvertValue(value, typeof(string)) ?? "nil";
				sType = GetTypeName(value.GetType());
			}

			// Print the type
			WriteText(ConsoleColor.DarkGray, String.Format("({0})", sType));

			// Print value
			int iLength = Console.WindowWidth - Console.CursorLeft;
			sValue = sValue.Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
			if (iLength < sValue.Length)
			{
				sValue = sValue.Substring(0, iLength - 3) + "...";
				WriteText(ConsoleColor.DarkCyan, sValue);
			}
			else
			{
				WriteText(ConsoleColor.DarkCyan, sValue);
				Console.WriteLine();
			}
		} // proc WriteVariableValue

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
      WriteText(ConsoleColor.DarkGray, eData.FormatStackTrace(0, true));
      Console.WriteLine();
      if (e.InnerException != null)
      {
        WriteText(ConsoleColor.Gray, ">>> INNER EXCEPTION <<<");
        WriteException(e.InnerException);
      }
    } // proc WriteException

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
					else if (sCommand.StartsWith(":cache", StringComparison.OrdinalIgnoreCase))
					{
						sLine = sCommand.Substring(6).Trim();
						return Commands.Cache;
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
          else if (sCommand.StartsWith(":debugtrace", StringComparison.OrdinalIgnoreCase))
          {
            sLine = "trace";
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
				else if (sbLine.Length == 0 && sInput.Length > 1 && sInput[0] == '=')
				{
					sLine = "return " + sInput.Substring(1);
					return Commands.Run;
				}
				else
        {
          sbLine.AppendLine(sInput);
          Console.Write("> ");
        }
      }
    } // func InputCommand

		private static Commands ParseArgument(string sArg, out string sLine)
		{
			if (!String.IsNullOrEmpty(sArg))
			{
				if (sArg[0] == '-')
				{
					if (sArg == "-debugon")
					{
						sLine = Boolean.TrueString;
						return Commands.Debug;
					}
					else if (sArg == "-debugoff")
					{
						sLine = Boolean.FalseString;
						return Commands.Debug;
					}
          else if (sArg == "-debugtrace")
          {
            sLine = "trace";
            return Commands.Debug;
          }
				}
				else
				{
					sLine = sArg;
					return Commands.Load;
				}
			}

			sLine = null;
			return Commands.None;
		} // func ParseArgument

    private static void RunScript(Func<string> code, string sName)
    {
			try
			{
				Stopwatch sw = new Stopwatch();
				sw.Start();

				// compile chunk
				LuaChunk c = lua.CompileChunk(code(), sName, new LuaCompileOptions() { DebugEngine = debugEngine });

				string sCompileTime = String.Format("{0:N0} ms", sw.ElapsedMilliseconds);
				sw.Reset();
				sw.Start();

				// run chunk
				LuaResult r = global.DoChunk(c);
				string sRunTime = String.Format("{0:N0} ms", sw.ElapsedMilliseconds);

				string sSize;
				if (c.Size < 0)
					sSize = "unknown";
				else if (c.Size == 0)
					sSize = String.Empty;
				else
					sSize = c.Size.ToString("N0") + " byte";

				// start with a new line
				if (Console.CursorLeft > 0)
					Console.WriteLine();

				// print result
				if (r.Count > 0)
				{
					for (int i = 0; i < r.Count; i++)
						WriteVariable(i, r[i]);
				}

				// print summary
				const string csCompile = "==> compile: ";
				const string csRuntime = " run: ";

				Console.CursorLeft = Console.WindowWidth - csCompile.Length - (sSize.Length > 0 ? sSize.Length + 3 : 0) - sCompileTime.Length - csRuntime.Length - sRunTime.Length - 1;
				WriteText(ConsoleColor.DarkGreen, csCompile);
				WriteText(ConsoleColor.Green, sCompileTime);
				if (sSize.Length > 0)
				{
					WriteText(ConsoleColor.DarkGreen, " [");
					WriteText(ConsoleColor.Green, sSize);
					WriteText(ConsoleColor.DarkGreen, "]");
				}
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
			int iCurrentArg = 0;

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

      global = lua.CreateEnvironment<LuaCommandGlobal>();

      // change to the samples directory
#if DEBUG
      string sSamples = Path.GetFullPath(@"..\..\Samples");
			Debug.Listeners.Add(new ConsoleTraceListener());
#else
      string sSamples = Path.GetFullPath("Samples");
#endif
      if (Directory.Exists(sSamples))
        Environment.CurrentDirectory = sSamples;

      while (true)
      {
        string sLine;
				Commands cmd;

				if (iCurrentArg < args.Length)
					cmd = ParseArgument(args[iCurrentArg++], out sLine);
				else
					cmd = InputCommand(out sLine);

        switch (cmd)
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
           if(sLine =="trace")
           {
             WriteText(ConsoleColor.DarkYellow, "Compile emits traceline code, now."); Console.WriteLine();
             debugEngine = debugConsole;
           }
           else if (sLine == Boolean.TrueString)
            {
              WriteText(ConsoleColor.DarkYellow, "Compile emits stack trace information and runtime functions, now."); Console.WriteLine();
							debugEngine = LuaStackTraceDebugger.Default;
            }
            else
            {
              WriteText(ConsoleColor.DarkYellow, "Compile creates dynamic functions, now."); Console.WriteLine();
              debugEngine = null;
            }
            Console.WriteLine();
            break;
          case Commands.Environment:
            WriteText(ConsoleColor.DarkYellow, "New environment created."); Console.WriteLine();
            Console.WriteLine();
            global = lua.CreateEnvironment<LuaCommandGlobal>();
            break;
					case Commands.Cache:
						lua.DumpRuleCaches(Console.Out);
				    Console.WriteLine();
						break;
          case Commands.Help:
            WriteText(ConsoleColor.DarkYellow, "Commands:"); Console.WriteLine();
            WriteCommand(":q", "Exit the application.");
            WriteCommand(":list", "Lists all global variables.");
            WriteCommand(":load", "Loads the lua-script from a file.");
            WriteCommand(":debugoff", "Tell the compiler to emit no debug informations.");
            WriteCommand(":debugon", "Let the compiler emit debug informations.");
            WriteCommand(":debugtrace", "Let the compiler emit trace line functionality.");
            WriteCommand(":c", "Clears the current script buffer.");
            WriteCommand(":env", "Create a fresh environment.");
            WriteCommand(":cache", "Shows the content of the binder cache.");
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
