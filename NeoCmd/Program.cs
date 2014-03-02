using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;

namespace Neo.IronLua
{
  ///////////////////////////////////////////////////////////////////////////////
  /// <summary></summary>
  public static class Program
  {
    public static void Main(string[] args)
    {
      dynamic ra = new LuaResult(1, 2, 3);
      int aaa = ra[1];
      Console.WriteLine(aaa);
      
      //CodePlexExample6();
      CodePlexExample7();
      //TestMemory(@"..\..\Samples\Test.lua");
      return;

      // create lua script compiler
      using (Lua l = new Lua())
        try
        {
          // create an environment that is associated  to the lua scripts
          LuaGlobal g = l.CreateEnvironment();

          // register new functions
          g.RegisterFunction("print", new Action<object[]>(Print));
          g.RegisterFunction("read", new Func<string, string>(Read));

          foreach (string c in args)
          {
            using (LuaChunk chunk = l.CompileChunk(c, true)) // compile the script with debug informations, that is needed for a complete stacktrace
              try
              {
                object[] r = g.DoChunk(chunk); // execute the chunk
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
                LuaExceptionData d = LuaExceptionData.GetData(e.InnerException); // get stack trace
                Console.WriteLine("StackTrace: {0}", d.GetStackTrace(0, false));
                Console.ForegroundColor = ConsoleColor.Gray;
              }
          }
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
    } // func Read

    private static void CodePlexExample1()
    {
      using (Lua l = new Lua())
      {
        var g = l.CreateEnvironment();

        object[] r = g.DoChunk("return a + b", "test.lua",
          new KeyValuePair<string, object>("a", 2),
          new KeyValuePair<string, object>("b", 4));

        Console.WriteLine(r[0]);
      }
    }

    private static void CodePlexExample2()
    {
      using (Lua l = new Lua())
      {
        var g = l.CreateEnvironment();
        dynamic dg = g;
        
        dg.a = 2; // dynamic way to set a variable
        g["b"] = 4; // second way to access variable
        g.DoChunk("c = a + b", "test.lua");

        Console.WriteLine(dg.c);
      }
    }

    private static void CodePlexExample3()
    {
      using (Lua l = new Lua())
      {
        var g = l.CreateEnvironment();

        g["myadd"] = new Func<int, int, int>((a, b) => a + b);

        g.DoChunk("function Add(a, b) return myadd(a, b) end;", "test.lua");

        dynamic dg = g;
        Console.WriteLine(dg.Add(2, 4)[0]);

        var f = (Func<object, object, object[]>)g["Add"];
        object[] r = f(2, 4);
        Console.WriteLine(r[0]);
      }
    }

    private static void CodePlexExample4()
    {
      using (Lua l = new Lua())
      {
        var g = l.CreateEnvironment();

        LuaTable t = new LuaTable();
        g["t"] = t;

        t["a"] = 2;
        t["b"] = 4;
        t["add"] = new Func<dynamic, int>(self => 
          {
            return self.a + self.b;
          });
        
        object[] r = g.DoChunk("return t:add()", "test.lua");
        Console.WriteLine(r[0]);
      }
    }

    private static void CodePlexExample5()
    {
      using (Lua l = new Lua())
      {
        LuaChunk c = l.CompileChunk("return a;", "test.lua", false);

        var g1 = l.CreateEnvironment();
        var g2 = l.CreateEnvironment();

        g1["a"] = 2;
        g2["a"] = 4;

        Console.WriteLine((int)(g1.DoChunk(c)[0]) + (int)(g2.DoChunk(c)[0]));
      }
    }

    private static void CodePlexExample6()
    {
      using (Lua l = new Lua())
      {
        var g = l.CreateEnvironment();
        object[] r = g.DoChunk (
          String.Join(Environment.NewLine,
          "local sys = clr.System;",
          "local sb = sys.Text.StringBuilder();",
          "sb:Append('Hallo '):Append('Welt!');",
          "return sb:ToString();"
          ),
          "test.lua");
        Console.WriteLine(r[0]);
      }
    }

    private static void CodePlexExample7()
    {
      using (Lua l = new Lua())
      {
        var f = l.CreateLambda<Func<double, double>>("f", "return clr.System.Math:Abs(x) * 2", "x");
        Console.WriteLine("f({0}) = {1}", 2, f(2));
        Console.WriteLine("f({0}) = {1}", 2, f(-2));

        var f2 = l.CreateLambda("f2", "local Math = clr.System.Math; return Math:Abs(x) * 2;", null, typeof(double), new KeyValuePair<string, Type>("x", typeof(double)));
        Console.WriteLine("f2({0}) = {1}", 2, f2.DynamicInvoke(2));
        Console.WriteLine("f2({0}) = {1}", 2, f2.DynamicInvoke(-2));
      }
    }

  } // class Program

  ///////////////////////////////////////////////////////////////////////////////
  /// <summary></summary>
  public class DiposeTest : IDisposable
  {
    public void Dispose()
    {
      Console.WriteLine("DisposeTest:Dispose");
    }
  } // class DiposeTest
}
