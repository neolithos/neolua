# Getting started

In this section I will show a more complex example, how NeoLua is used.

This example only works with the desktop NeoLua.

###### Host program (C#):
```C#
public static class Program
{
	public const string ProgramSource = "local a, b = tonumber(read(\"a\")), tonumber(read(\"b\"));\n\n" +
		"local PrintResult = function(o, op)\n" +
		"		print(o.. ' = ' .. a..op..b);\n" +
		"end;\n\n" +
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
} // class Program
```

###### Lua-Script (without escapes)
```Lua
local a, b = tonumber(read("a")), tonumber(read("b"));

function PrintResult(o, op)
	print(o .. ' = ' .. a .. op .. b);
end;

PrintResult(a + b, " + ");
PrintResult(a - b, " - ");
PrintResult(a * b, " * ");
PrintResult(a / b, " / ");
```