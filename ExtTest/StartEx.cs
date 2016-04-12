using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Neo.IronLua;

namespace ExtTest
{
	class StartEx
	{
		public const string ProgramSource = "local a, b = tonumber(read(\"a\")), tonumber(read(\"b\"));\n" +
			"local PrintResult = function(o, op)\n" +
			"		print(o.. ' = ' .. a..op..b);\n" +
			"end;\n" +
			"PrintResult(a + b, \" + \");\n" +
			"PrintResult(a - b, \" - \");\n" +
			"PrintResult(a * b, \" * \");\n" +
			"PrintResult(a / b, \" / \");\n" +
			"PrintResult(a // b, \" // \");\n";

		public static void Main1(string[] args)
		{
			// create lua script engine
			using (var l = new Lua())
			{
				// create an environment, that is associated to the lua scripts
				dynamic g = l.CreateEnvironment<LuaGlobal>();

				// register new functions
				g.print = new Action<object[]>(Print);
				g.read = new Func<string, string>(Read);


				var chunk = l.CompileChunk(ProgramSource, "test.lua", new LuaCompileOptions() { DebugEngine = LuaStackTraceDebugger.Default }); // compile the script with debug informations, that is needed for a complete stack trace

				try
				{
					g.dochunk(chunk); // execute the chunk
				}
				catch (Exception e)
				{
					Console.WriteLine("Expception: {0}", e.Message);
					var d = LuaExceptionData.GetData(e); // get stack trace
					Console.WriteLine("StackTrace: {0}", d.FormatStackTrace(0, false));
				}
			}
		} // Main

		private static void Print(object[] texts)
		{
			foreach (object o in texts)
				Console.Write(o);
			Console.WriteLine();
		} // proc Print

		private static string Read(string sLabel)
		{
			Console.Write(sLabel);
			Console.Write(": ");
			return Console.ReadLine();
		} // func Read	
	}
}
