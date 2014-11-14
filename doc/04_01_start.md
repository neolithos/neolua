# Getting started

In this section I will show a more complex example, how NeoLua is used.

###### Host program (C#):
```C#
public static class Program
{
  public static void Main(string[] args)
  {
    // create lua script engine
    using (Lua l = new Lua())
    {
      // create an environment, that is associated to the lua scripts
      dynamic g = l.CreateEnvironment();

      // register new functions
      g.print = new Action<object[]>(Print);
      g.read = new Func<string, string>(Read);

      foreach (string c in args)
      {
        using (LuaChunk chunk = l.CompileChunk(c, Lua.DefaultDebugEngine)) // compile the script with debug informations, that is needed for a complete stack trace
          try
          {
            g.dochunk(chunk); // execute the chunk
          }
          catch (TargetInvocationException e)
          {
            Console.WriteLine("Expception: {0}", e.InnerException.Message);
            LuaExceptionData d = LuaExceptionData.GetData(e.InnerException); // get stack trace
            Console.WriteLine("StackTrace: {0}", d.GetStackTrace(0, false));
          }
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

###### Lua-Script:
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