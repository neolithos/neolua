using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Neo.IronLua
{
  ///////////////////////////////////////////////////////////////////////////////
  /// <summary></summary>
  public static class Program
  {
    public static void Main(string[] args)
    {
      //TestMemory(@"..\..\Samples\Test.lua");
      //return;
      // executes the script
      try
      {
        Lua l = new Lua();
        LuaGlobal g = l.CreateEnvironment();

        g.RegisterFunction("print", new Action<object[]>(Print));
        g.RegisterFunction("read", new Func<string, string>(Read));

        foreach (string c in args)
        {
          using (LuaChunk chunk = l.CompileChunk(c, true))
            try
            {
              object[] r = g.DoChunk(chunk);
              if (r != null && r.Length > 0)
              {
                Console.WriteLine(new string('=', 79));
                for (int i = 0; i < r.Length; i++)
                  Console.WriteLine("[{0}] = {1}", i, r[i]);
              }
            }
            catch (TargetInvocationException e)
            {
              Console.ForegroundColor = ConsoleColor.DarkRed;
              Console.WriteLine("Expception: {0}", e.InnerException.Message);
              LuaExceptionData d = LuaExceptionData.GetData(e.InnerException);
              Console.WriteLine("StackTrace: {0}", d.GetStackTrace(0, false));
              Console.ForegroundColor = ConsoleColor.Gray;
            }
        }
        l.Dispose();
      }
      catch (Exception e)
      {
        Exception re = e is TargetInvocationException ? e.InnerException : e;
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.DarkRed;
        Console.WriteLine("Expception: {0}", re.Message);
        Console.ForegroundColor = ConsoleColor.Gray;
      }
#if DEBUG
      Console.WriteLine();
      Console.Write("<return>");
      Console.ReadLine();
#endif
    } // Main

    private static void TestMemory(string sFileName)
    {
      for (int i = 0; i < 5; i++)
      {
        using (Lua l = new Lua())
        {
          for (int j = 0; j < 10; j++)
          {
            Console.Write("i={0};j={1}  ", i, j);

            Stopwatch sw = new Stopwatch();
            sw.Start();
            using (l.CompileChunk(sFileName, true))
            { }
            Console.WriteLine("{0:N0} ms", sw.ElapsedMilliseconds);

            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
            Thread.Sleep(100);
          }          
        }
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
        Thread.Sleep(100);
      }
      Console.WriteLine("done");
    } // proc TestMemory

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
    }
  } // class Program
}
