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
      //dynamic ra = new LuaResult(1, 2, 3);
      //int aaa = ra[1];
      //Console.WriteLine(aaa);

      //CodePlexExample4b();
      //TestMemory(@"..\..\Samples\Test.lua");
      //return;

      // create lua script compiler
      using (Lua l = new Lua())
      {
        // create an environment that is associated  to the lua scripts
        dynamic g = l.CreateEnvironment();

        // register new functions
        g.print = new Action<object[]>(Print);
        g.read = new Func<string, string>(Read);

        foreach (string c in args)
        {
          using (LuaChunk chunk = l.CompileChunk(c, true)) // compile the script with debug informations, that is needed for a complete stacktrace
            try
            {
              object[] r = g.dochunk(chunk); // execute the chunk
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
        dynamic dg = g;

        LuaResult r = g.DoChunk("return a + b", "test.lua",
          new KeyValuePair<string, object>("a", 2),
          new KeyValuePair<string, object>("b", 4));

        Console.WriteLine(r[0]);

        dynamic dr = dg.dochunk("return a + b", "test.lua", "a", 2, "b", 4);
        Console.WriteLine((int)dr);
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
        dg.dochunk("c = a + b", "test.lua");

        Console.WriteLine(dg.c);
        Console.WriteLine(g["c"]);
      }
    }

    private static void CodePlexExample2a()
    {
      using (Lua l = new Lua())
      {
        dynamic dg = l.CreateEnvironment();
        dg.t = new LuaTable();
        dg.t.a = 2;
        dg.t.b = 4;
        dg.dochunk("t.c = t.a + t.b", "test.lua");
        Console.WriteLine(dg.t.c);
      }
    }

    private static void CodePlexExample2b()
    {
      using (Lua l = new Lua())
      {
        dynamic dg = l.CreateEnvironment();
        dg.t = new LuaTable();
        dg.t[0] = 2;
        dg.t[1] = 4;
        dg.dochunk("t[2] = t[0] + t[1]", "test.lua");
        Console.WriteLine(dg.t[2]);
      }
    }

    private static void CodePlexExample3()
    {
      using (Lua l = new Lua())
      {
        dynamic dg = l.CreateEnvironment();

        dg.myadd = new Func<int, int, int>((a, b) => a + b);

        dg.dochunk("function Add(a, b) return myadd(a, b) end;", "test.lua");

        Console.WriteLine((int)dg.Add(2, 4));

        var f = (Func<object, object, LuaResult>)dg.Add;
        Console.WriteLine(f(2, 4).ToInt32());
      }
    }

    private static void CodePlexExample3a()
    {
      using (Lua l = new Lua())
      {
        dynamic dg = l.CreateEnvironment();

        dg.myadd = new Func<int, int, int>((a, b) => a + b);

        dg.dochunk("function Add(a : int, b : int) : int return myadd(a, b) end;", "test.lua");

        Console.WriteLine((int)dg.Add(2, 4));

        var f = (Func<int, int, int>)dg.Add;
        Console.WriteLine(f(2, 4));
      }
    }

    private static void CodePlexExample4()
    {
      using (Lua l = new Lua())
      {
        dynamic dg = l.CreateEnvironment();

        dg.t = new LuaTable();
        dg.t.a = 2;
        dg.t.b = 4;
        
        dg.t.add = new Func<dynamic, int>(self => 
          {
            return self.a + self.b;
          });

        ((LuaTable)dg.t).DefineMethod("add2", (Delegate)dg.t.add);

        Console.WriteLine(dg.dochunk("return t:add()", "test.lua")[0]);
        Console.WriteLine(dg.dochunk("return t:add2()", "test.lua")[0]);
        Console.WriteLine(dg.t.add(dg.t));
        Console.WriteLine(dg.t.add2());
      }
    }

    private static void CodePlexExample4a()
    {
      using (Lua l = new Lua())
      {
        dynamic dg = l.CreateEnvironment();
        LuaResult r = dg.dochunk("t = { a = 2, b = 4 };" +
          "t.add = function(self)" +
          "  return self.a + self.b;" +
          "end;" +
          "function t.add1(self)" +
          "  return self.a + self.b;" +
          "end;" +
          "t:add2 = function (self)" +
          "  return self.a + self.b;" +
          "end;" +
          "function t:add3()" +
          "  return self.a + self.b;" +
          "end;" +
          "return t:add(), t:add2(), t:add3(), t.add(t), t.add2(t), t.add3(t);", 
          "test.lua");
        Console.WriteLine(r[0]);
        Console.WriteLine(r[1]);
        Console.WriteLine(r[2]);
        Console.WriteLine(r[3]);
        Console.WriteLine(r[4]);
        Console.WriteLine(r[5]);
        Console.WriteLine(dg.t.add(dg.t)[0]);
        Console.WriteLine(dg.t.add2()[0]);
        Console.WriteLine(dg.t.add3()[0]);
      }
    }

    private static void CodePlexExample4b()
    {
      using (Lua l = new Lua())
      {
        dynamic dg = l.CreateEnvironment();

        dg.dochunk("function classA()" +
          "  local c = { sum = 0 };" +
          "  function c:add(a)" +
          "    self.sum = self.sum + a;" +
          "  end;" +
          "  return c;" +
          "end;", "classA.lua");

        dynamic o = dg.classA()[0];
        o.add(1);
        o.add(2);
        o.add(3);
        Console.WriteLine(o.sum);
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
