#region -- copyright --
//
// Licensed to the Apache Software Foundation (ASF) under one
// or more contributor license agreements.  See the NOTICE file
// distributed with this work for additional information
// regarding copyright ownership.  The ASF licenses this file
// to you under the Apache License, Version 2.0 (the
// "License"); you may not use this file except in compliance
// with the License.  You may obtain a copy of the License at
// 
//   http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing,
// software distributed under the License is distributed on an
// "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY
// KIND, either express or implied.  See the License for the
// specific language governing permissions and limitations
// under the License.
//
#endregion
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;

namespace Neo.IronLua
{
	public static class Program
	{
		#region -- enum Commands ------------------------------------------------------

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
			Help,
			ClrOn,
			ClrOff
		} // enum Commands

		#endregion

		#region -- class LuaCommandGlobal ---------------------------------------------

		private sealed class LuaCommandGlobal : LuaGlobal
		{
			public LuaCommandGlobal(Lua lua)
				: base(lua)
			{
				this.LuaPackage.path += ";" + LuaLibraryPackage.ExecutingDirectoryPathVariable;
			} // ctor

			[LuaMember("read")]
			private string ReadLine()
				=> ReadLine(String.Empty);

			[LuaMember("read")]
			private string ReadLine(string label)
			{
				Console.Write(label);
				Console.Write("> ");
				return Console.ReadLine();
			} // func ReadLine

			/// <summary></summary>
			[
			LuaMember("console"),
			LuaMember("out")
			]
			private static LuaType LuaConsole => LuaType.GetType(typeof(Console));
		} // class LuaCommandGlobal

		#endregion

		#region -- class LuaTraceLineConsoleDebugger ----------------------------------

		private class LuaTraceLineConsoleDebugger : LuaTraceLineDebugger
		{
			private bool inException = false;
			private LuaTraceLineEventArgs lastTracePoint = null;
			private Stack<LuaTraceLineEventArgs> frames = new Stack<LuaTraceLineEventArgs>();

			private void UpdateStackLine(int top, LuaTraceLineEventArgs e)
			{
				// 12345678.123 123
				Console.CursorLeft = Console.WindowWidth - 16;
				Console.CursorTop = top;
				if (e == null)
					Console.Write(new string(' ', 16));
				else
				{
					var fileName = Path.GetFileName(e.SourceName);
					if (fileName.Length > 12)
						fileName = fileName.Substring(0, 12);
					else if (fileName.Length < 12)
						fileName = fileName.PadRight(12);

					var line = e.SourceLine.ToString().PadLeft(4);
					if (line.Length > 4)
						line = line.Substring(0, 4);

					WriteText(ConsoleColor.DarkGray, fileName);
					WriteText(ConsoleColor.Gray, line);
				}
			} // proc UpdateStackLine

			private void UpdateStack(LuaTraceLineEventArgs e)
			{
				var stack = frames.ToArray();

				var currentLeft = Console.CursorLeft;
				var currentTop = Console.CursorTop;
				var top = 0;
				try
				{
					var start = stack.Length > 9
						? stack.Length - 8
						: 0;

					for (var i = start; i < stack.Length; i++)
						UpdateStackLine(top++, stack[i]);
					UpdateStackLine(top++, e);

					while (top < 9)
						UpdateStackLine(top++, null);
				}
				finally
				{
					Console.CursorLeft = currentLeft;
					Console.CursorTop = currentTop;
				}
				Thread.Sleep(100);
			} // proc UpdateStack

			protected override void OnExceptionUnwind(LuaTraceLineExceptionEventArgs e)
			{
				if (!inException)
				{
					inException = true;
					Console.WriteLine();
					var top = Console.CursorTop;
					WriteText(ConsoleColor.DarkRed, "Exception: ");
					WriteText(ConsoleColor.Red, e.Exception.Message); Console.WriteLine();
					WriteText(ConsoleColor.Gray, "press any key to continue");
					Console.WriteLine();
					Console.ReadKey();
					var clearTo = Console.CursorTop;

					Console.CursorLeft = 0;

					var clearText = new string(' ', Console.WindowWidth);
					for (var i = top; i <= clearTo; i++)
					{
						Console.CursorTop = i;
						Console.WriteLine(clearText);
					}

					Console.CursorTop = top;
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
					inException = false;
				UpdateStack(null);
				base.OnFrameExit();
			} // proc OnFrameExit
		} // class LuaTraceLineConsoleDebugger

		#endregion

		private static Lua lua = new Lua(); // create lua script compiler
		private static LuaGlobal global;
		private static ILuaDebug debugEngine = LuaStackTraceDebugger.Default;
		private static bool ClrEnabled = true;
		private static readonly ILuaDebug debugConsole = new LuaTraceLineConsoleDebugger();

		private static void WriteText(ConsoleColor textColor, string text)
		{
			var oldColor = Console.ForegroundColor;
			Console.ForegroundColor = textColor;
			Console.Write(text);
			Console.ForegroundColor = oldColor;
		} // proc WriteText

		private static string GetTypeName(Type type)
		{
			var t = LuaType.GetType(type);
			return t.AliasName ?? t.Name;
		} // func GetTypeName

		private static void WriteVariable(object key, object value)
		{
			Console.Write("  ");
			if (key is int)
				WriteText(ConsoleColor.White, String.Format("[{0}]", key));
			else if (key is string s)
				WriteText(ConsoleColor.White, s);
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
			string stringValue;
			string typeName;
			if (value == null)
			{
				stringValue = "nil";
				typeName = GetTypeName(typeof(object));
			}
			else
			{
				stringValue = (string)Lua.RtConvertValue(value, typeof(string)) ?? "nil";
				typeName = GetTypeName(value.GetType());
			}

			// Print the type
			WriteText(ConsoleColor.DarkGray, String.Format("({0})", typeName));

			// Print value
			var length = Console.WindowWidth - Console.CursorLeft;
			stringValue = stringValue.Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
			if (length < stringValue.Length)
			{
				stringValue = stringValue.Substring(0, length - 3) + "...";
				WriteText(ConsoleColor.DarkCyan, stringValue);
			}
			else
			{
				WriteText(ConsoleColor.DarkCyan, stringValue);
				Console.WriteLine();
			}
		} // proc WriteVariableValue

		public static void WriteCommand(string command, string description)
		{
			Console.CursorLeft = 2;
			WriteText(ConsoleColor.Gray, command);
			Console.CursorLeft = 12;
			WriteText(ConsoleColor.DarkGray, description);
			Console.WriteLine();
		} // proc WriteCommand

		private static void WriteError(string text)
		{
			WriteText(ConsoleColor.DarkRed, text);
			Console.WriteLine();
		} // proc WriteError

		private static void WriteException(Exception e)
		{
			WriteText(ConsoleColor.DarkRed, e.GetType().Name + ": ");
			WriteText(ConsoleColor.Red, e.Message);
			Console.WriteLine();
			var eData = LuaExceptionData.GetData(e);
			WriteText(ConsoleColor.DarkGray, eData.FormatStackTrace(0, true));
			Console.WriteLine();
			if (e.InnerException != null)
			{
				WriteText(ConsoleColor.Gray, ">>> INNER EXCEPTION <<<");
				WriteException(e.InnerException);
			}
		} // proc WriteException

		private static bool IsCommand(string input)
		{
			for (var i = 0; i < input.Length; i++)
			{
				if (!Char.IsWhiteSpace(input[i]))
					return input[i] == ':';
			}
			return false;
		} // func HasInputData

		private static Commands InputCommand(out string line)
		{
			var sbLine = new StringBuilder();

			// i need a clear line
			if (Console.CursorLeft > 0)
				Console.WriteLine();

			// remind the start point
			Console.ForegroundColor = ConsoleColor.Gray;
			Console.Write("> ");

			while (true)
			{
				var input = Console.ReadLine();
				if (input.Length == 0)
				{
					line = sbLine.ToString();
					return Commands.Run;
				}
				else if (IsCommand(input))
				{
					var command = input.Trim();
					if (command.StartsWith(":q", StringComparison.OrdinalIgnoreCase))
					{
						line = String.Empty;
						return Commands.Exit;
					}
					if (command.StartsWith(":h", StringComparison.OrdinalIgnoreCase))
					{
						line = String.Empty;
						return Commands.Help;
					}
					else if (command.StartsWith(":load", StringComparison.OrdinalIgnoreCase))
					{
						line = command.Substring(5).Trim();
						return Commands.Load;
					}
					else if (command.StartsWith(":list", StringComparison.OrdinalIgnoreCase))
					{
						line = command.Substring(5).Trim();
						return Commands.List;
					}
					else if (command.StartsWith(":cache", StringComparison.OrdinalIgnoreCase))
					{
						line = command.Substring(6).Trim();
						return Commands.Cache;
					}
					else if (command.StartsWith(":clron", StringComparison.OrdinalIgnoreCase))
					{
						line = command.Substring(6).Trim();
						return Commands.ClrOn;
					}
					else if (command.StartsWith(":clroff", StringComparison.OrdinalIgnoreCase))
					{
						line = command.Substring(7).Trim();
						return Commands.ClrOff;
					}
					else if (command.StartsWith(":c", StringComparison.OrdinalIgnoreCase))
					{
						sbLine.Clear();
						Console.WriteLine("> ");
					}
					else if (command.StartsWith(":debugoff", StringComparison.OrdinalIgnoreCase))
					{
						line = Boolean.FalseString;
						return Commands.Debug;
					}
					else if (command.StartsWith(":debugon", StringComparison.OrdinalIgnoreCase))
					{
						line = Boolean.TrueString;
						return Commands.Debug;
					}
					else if (command.StartsWith(":debugtrace", StringComparison.OrdinalIgnoreCase))
					{
						line = "trace";
						return Commands.Debug;
					}
					else if (command.StartsWith(":debugtexcept", StringComparison.OrdinalIgnoreCase))
					{
						line = "except";
						return Commands.Debug;
					}
					else if (command.StartsWith(":env", StringComparison.OrdinalIgnoreCase))
					{
						line = "true";
						return Commands.Environment;
					}
					else
						WriteError("Unkown command.");
				}
				else if (sbLine.Length == 0 && input.Length > 1 && input[0] == '=')
				{
					line = "return " + input.Substring(1);
					return Commands.Run;
				}
				else
				{
					sbLine.AppendLine(input);
					Console.Write("> ");
				}
			}
		} // func InputCommand

		private static Commands ParseArgument(string arg, out string line)
		{
			if (!String.IsNullOrEmpty(arg))
			{
				if (arg[0] == '-')
				{
					if (arg == "-debugon")
					{
						line = Boolean.TrueString;
						return Commands.Debug;
					}
					else if (arg == "-debugoff")
					{
						line = Boolean.FalseString;
						return Commands.Debug;
					}
					else if (arg == "-debugtrace")
					{
						line = "trace";
						return Commands.Debug;
					}
					else if (arg == "-debugexcept")
					{
						line = "except";
						return Commands.Debug;
					}
				}
				else
				{
					line = arg;
					return Commands.Load;
				}
			}

			line = null;
			return Commands.None;
		} // func ParseArgument

		private static void RunScript(Func<string> code, string name)
		{
			try
			{
				var sw = new Stopwatch();
				sw.Start();

				// compile chunk
				var c = lua.CompileChunk(code(), name, new LuaCompileOptions() { DebugEngine = debugEngine, ClrEnabled = ClrEnabled });

				var compileTime = String.Format("{0:N0} ms", sw.ElapsedMilliseconds);
				sw.Reset();
				sw.Start();

				// run chunk
				var r = global.DoChunk(c);
				string sRunTime = String.Format("{0:N0} ms", sw.ElapsedMilliseconds);

				string chunkSize;
				if (c.Size < 0)
					chunkSize = "unknown";
				else if (c.Size == 0)
					chunkSize = String.Empty;
				else
					chunkSize = c.Size.ToString("N0") + " byte";

				// start with a new line
				if (Console.CursorLeft > 0)
					Console.WriteLine();

				// print result
				if (r.Count > 0)
				{
					for (var i = 0; i < r.Count; i++)
						WriteVariable(i, r[i]);
				}

				// print summary
				const string csCompile = "==> compile: ";
				const string csRuntime = " run: ";

				Console.CursorLeft = Console.WindowWidth - csCompile.Length - (chunkSize.Length > 0 ? chunkSize.Length + 3 : 0) - compileTime.Length - csRuntime.Length - sRunTime.Length - 1;
				WriteText(ConsoleColor.DarkGreen, csCompile);
				WriteText(ConsoleColor.Green, compileTime);
				if (chunkSize.Length > 0)
				{
					WriteText(ConsoleColor.DarkGreen, " [");
					WriteText(ConsoleColor.Green, chunkSize);
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
				var ex = e is TargetInvocationException ? e.InnerException : e;
				WriteException(ex);
			}
		} // proc RunScript

		[STAThread]
		public static void Main(string[] args)
		{
			var currentArg = 0;

			WriteText(ConsoleColor.Gray, "NeoLua Interactive Command"); Console.WriteLine();
			WriteText(ConsoleColor.DarkGray, "Version ");
			WriteText(ConsoleColor.White, String.Format("{0} ({1})", LuaGlobal.VersionString, Lua.Version));
			WriteText(ConsoleColor.DarkGray, " by neolithos");
			Console.WriteLine();
			Console.WriteLine();
			WriteText(ConsoleColor.DarkGray, "  source code at ");
			WriteText(ConsoleColor.Gray, "https://github.com/neolithos/neolua");
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
			var samples = Path.GetFullPath(@"..\..\Samples");
			Debug.Listeners.Add(new ConsoleTraceListener());
#else
			var samples = Path.GetFullPath("Samples");
#endif
			if (Directory.Exists(samples))
				Environment.CurrentDirectory = samples;

			while (true)
			{
				string line;
				Commands cmd;

				if (currentArg < args.Length)
					cmd = ParseArgument(args[currentArg++], out line);
				else
					cmd = InputCommand(out line);

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
						RunScript(() => File.ReadAllText(Path.GetFullPath(line)), Path.GetFileName(line));
						break;
					case Commands.Debug:
						if (line == "trace")
						{
							WriteText(ConsoleColor.DarkYellow, "Compile emits traceline code, now."); Console.WriteLine();
							debugEngine = debugConsole;
						}
						else if(line == "except")
						{
							WriteText(ConsoleColor.DarkYellow, "Compile emits exception code, now."); Console.WriteLine();
							debugEngine = LuaExceptionDebugger.Default;
						}
						else if (line == Boolean.TrueString)
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
					case Commands.ClrOn:
						ClrEnabled = true;
						WriteText(ConsoleColor.DarkYellow, "Clr access enabled."); Console.WriteLine();
						Console.WriteLine();
						break;
					case Commands.ClrOff:
						WriteText(ConsoleColor.DarkYellow, "Clr access disabled."); Console.WriteLine();
						ClrEnabled = false;
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
						WriteCommand(":debugexcept", "Let the compiler emit exception line functionality.");
						WriteCommand(":c", "Clears the current script buffer.");
						WriteCommand(":env", "Create a fresh environment.");
						WriteCommand(":cache", "Shows the content of the binder cache.");
						WriteCommand(":clron", "Enables access to the clr.");
						WriteCommand(":clroff", "Disables access to the clr.");
						Console.WriteLine();
						break;
					case Commands.Run:
						if (line.Length > 0)
							RunScript(() => line, "line");
						break;
					case Commands.Exit:
						return;
				}
			}
		} // Main
	}
}
