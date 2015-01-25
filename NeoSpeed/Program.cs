using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NeoSpeed
{
  class Program
  {
    private static Neo.IronLua.ILuaDebug debugNeoLua = Neo.IronLua.Lua.DefaultDebugEngine.DebugEngine;

    static void Main(string[] args)
    {
      string[] scripts = new string[]
      {
				Path.GetFullPath(@"..\..\Scripts\Empty.lua"),
				Path.GetFullPath(@"..\..\Scripts\Sum.lua"),
				Path.GetFullPath(@"..\..\Scripts\Sum_strict{0}.lua"),
				Path.GetFullPath(@"..\..\Scripts\Sum_echo.lua"),
				Path.GetFullPath(@"..\..\Scripts\String.lua"),
				Path.GetFullPath(@"..\..\Scripts\String_echo.lua"),
				Path.GetFullPath(@"..\..\Scripts\Delegate.lua"),
				Path.GetFullPath(@"..\..\Scripts\StringBuilder{0}.lua"),
				Path.GetFullPath(@"..\..\Scripts\CallStd.lua"),
				Path.GetFullPath(@"..\..\Scripts\TableIntSet.lua"),
				Path.GetFullPath(@"..\..\Scripts\TableIntGet.lua"),
				Path.GetFullPath(@"..\..\Scripts\TableVsList{0}.lua"),
				Path.GetFullPath(@"..\..\Scripts\TableString.lua"),
      };

      Console.WriteLine();
      Console.WriteLine("Rebuild cache:");
      for (int i = 0; i < scripts.Length; i++)
      {
        string sScript1 = File.ReadAllText(String.Format(scripts[i], 1));
        string sScript2 = File.ReadAllText(String.Format(scripts[i], 2));
        double t1 = ExecuteScriptLoop(ExecuteLuaIntf, sScript1, 100);
        double t2 = ExecuteScriptLoop(ExecuteNeoLua, sScript2, 100);
        Console.WriteLine("  {0,-20}: LuaInterface {1,6:N1} ms    NeoLua {2,6:N1} ms  {3,6:N3}",
          Path.GetFileNameWithoutExtension(scripts[i].Replace("{0}", "")),
          t1,
          t2,
          t2 == 0.0 ? Double.NaN : t1 / t2
        );
      }
      Console.WriteLine();
      Console.WriteLine("Precompiled (no cache clear):");
      debugNeoLua = null;
      for (int i = 0; i < scripts.Length; i++)
      {
        string sScript1 = File.ReadAllText(String.Format(scripts[i], 1));
        string sScript2 = File.ReadAllText(String.Format(scripts[i], 2));
        double t1 = ExecuteLuaIntfCompiled(sScript1, 100);
        double t2 = ExecuteNeoLuaCompiled(sScript2, 100);
        Console.WriteLine("  {0,-20}: LuaInterface {1,6:N1} ms    NeoLua {2,6:N1} ms  {3,6:N3}",
          Path.GetFileNameWithoutExtension(scripts[i].Replace("{0}", "")),
          t1,
          t2,
          t2 == 0.0 ? Double.NaN : t1 / t2
        );
      }
      Console.WriteLine();
      Console.WriteLine("Precompiled (no cache clear, debug):");
			debugNeoLua = Neo.IronLua.Lua.DefaultDebugEngine.DebugEngine;
      for (int i = 0; i < scripts.Length; i++)
      {
        string sScript1 = File.ReadAllText(String.Format(scripts[i], 1));
        string sScript2 = File.ReadAllText(String.Format(scripts[i], 2));
        double t1 = ExecuteLuaIntfCompiled(sScript1, 100);
        double t2 = ExecuteNeoLuaCompiled(sScript2, 100);
        Console.WriteLine("  {0,-20}: LuaInterface {1,6:N1} ms    NeoLua {2,6:N1} ms  {3,6:N3}",
          Path.GetFileNameWithoutExtension(scripts[i].Replace("{0}", "")),
          t1,
          t2,
          t2 == 0.0 ? Double.NaN : t1 / t2
        );
      }
      Console.WriteLine();
      Console.WriteLine(v == 1801800 ? "All called." : String.Format("{0} != {1}", v, 1201200));

			Console.ReadLine();
    }

    private static double ExecuteLuaIntfCompiled(string sScript, int iLoops)
    {
      using (LuaInterface.Lua lua = new LuaInterface.Lua())
      {
        lua.RegisterFunction("test", null, typeof(Program).GetMethod("LuaTest"));
        lua.RegisterFunction("echo", null, typeof(Program).GetMethod("LuaEcho"));
        LuaInterface.LuaFunction f = lua.LoadString(sScript, "test");
        
        Stopwatch sw = new Stopwatch();
        sw.Start();

        for (int i = 0; i < iLoops; i++)
          DebugOut("LuaIntf-C", i, f.Call());

        return sw.ElapsedMilliseconds / (double)iLoops;
      }
    }

    private static double ExecuteNeoLuaCompiled(string sScript, int iLoops)
    {
      using (Neo.IronLua.Lua lua = new Neo.IronLua.Lua())
      {
				Neo.IronLua.LuaChunk chunk = lua.CompileChunk(sScript, "test", new Neo.IronLua.LuaCompileOptions() { DebugEngine = debugNeoLua });
        Neo.IronLua.LuaGlobal g = lua.CreateEnvironment();
        g["test"] = new Action<int>(LuaTest);
        g["echo"] = new Func<object, object>(LuaEcho);

        Stopwatch sw = new Stopwatch();
        sw.Start();
        for (int i = 0; i < iLoops; i++)
          DebugOut("NeoLua-C", i, g.DoChunk(chunk));
        return sw.ElapsedMilliseconds / (double)iLoops;
      }
    }

    private static double ExecuteScriptLoop(Func<int, string, long> f, string sScript, int iLoops)
    {
      long sum = 0;
      for (int i = 0; i < iLoops; i++)
        sum += f(i, sScript);
      return sum / (double)iLoops;
    } // func ExecuteScriptLoop

    private static long ExecuteLuaIntf(int i, string sScript)
    {
      Stopwatch sw = new Stopwatch();
      sw.Start();
      using (LuaInterface.Lua lua = new LuaInterface.Lua())
      {
        lua.RegisterFunction("test", null, typeof(Program).GetMethod("LuaTest"));
        lua.RegisterFunction("echo", null, typeof(Program).GetMethod("LuaEcho"));
        DebugOut("LuaIntf", i, lua.DoString(sScript, "test"));
      }
      return sw.ElapsedMilliseconds;
    } // proc ExecuteLuaIntf

    //private static Neo.IronLua.Lua lua = new Neo.IronLua.Lua();
    private static long ExecuteNeoLua(int i, string sScript)
    {
      Stopwatch sw = new Stopwatch();
      sw.Start();
      using (Neo.IronLua.Lua lua = new Neo.IronLua.Lua())
      {
        Neo.IronLua.LuaGlobal g = lua.CreateEnvironment();
        g["test"] = new Action<int>(LuaTest);
        g["echo"] = new Func<object, object>(LuaEcho);
        DebugOut("NeoLua", i, g.DoChunk(sScript, "test"));
      }
      return sw.ElapsedMilliseconds;
    }

    private static int v = 0;
    
    public static void LuaTest(int i)
    {
      v++;
    }

    public static object LuaEcho(object e)
    {
      v++;
      return e;
    }

    private static void DebugOut(string sLua, int i, object[] r)
    {
      //Debug.Print("Call {0}:{1}: {2}", sLua, i, r != null && r.Length == 1 ? r[0] : "<none>");
    } // proc DebugOut

    private static int ConvDummy(object value)
    {
      return (int)value;
    }

    private static long SumLuaCS()
    {
      Stopwatch sw = new Stopwatch();
      sw.Start();

      object sum = 0;
      for (object i = 0; ConvDummy(i) < 1000; i = ConvDummy(i) + 1)
        sum = ConvDummy(sum) + ConvDummy(LuaEcho(i));
    
      return sw.ElapsedMilliseconds;
    }
  }
}
