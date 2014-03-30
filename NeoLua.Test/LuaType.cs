using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.IronLua;

namespace LuaDLR.Test
{
  ///////////////////////////////////////////////////////////////////////////////
  /// <summary></summary>
  [TestClass]
  public class LuaTypeTests : TestHelper
  {
    public class SubClass
    {
    }

    [TestMethod]
    public void TypeTest01()
    {
      LuaType t = LuaType.GetType(typeof(Stream));
      Assert.IsTrue(t.Type != null);
      t = LuaType.GetType("System.Test.Test", false, true);
      Assert.IsTrue(t.Type == null);
      t = LuaType.GetType("LuaDLR.Test.LuaTypeTests.SubClass", false, true);
      Assert.IsTrue(t.Type != null);
      t = LuaType.GetType(typeof(List<string>));
      Assert.IsTrue(t.Type != null);
      t = LuaType.GetType(typeof(string[]));
      Assert.IsTrue(t.Type != null);
    } // proc TypeTest01

    [TestMethod]
    public void TypeTest02()
    {
      using (Lua l = new Lua())
      {
        dynamic g = l.CreateEnvironment();
        LuaType t = g.dochunk("return clr.LuaDLR.Test.LuaTypeTests.SubClass", "test");
        Type t2 = g.dochunk("return clr.LuaDLR.Test.LuaTypeTests.SubClass:GetType()", "test");
        Assert.IsTrue(t.Type == t2);

        t = g.dochunk("return clr.System.IO.Stream", "test");
        Assert.IsTrue(t.Type == typeof(Stream));
        t = g.dochunk("return clr.System.Test.Test", "test");
        Assert.IsTrue(t.Type == null);
        t = g.dochunk("return clr.LuaDLR.Test.LuaTypeTests.SubClass", "test");
        Assert.IsTrue(t.Type == typeof(SubClass));
        t = g.dochunk("return clr.System.Collections.Generic.List[clr.System.String]", "test");
        Assert.IsTrue(t.Type == typeof(List<string>));
        t = g.dochunk("return clr.System.String[]", "test");
        Assert.IsTrue(t.Type == typeof(string[]));
      }
    } // propc TypeTest02

    [TestMethod]
    public void TypeTest03()
    {
      using (Lua l = new Lua())
      {
        dynamic g = l.CreateEnvironment();

        g.dochunk("return cast(System.IO.Stream, null);");
        g.dochunk("return cast(LuaDLR.Test.LuaTypeTests.SubClass, null);");
        g.dochunk("return cast(System.Collections.Generic.List[string[]], null);");
        g.dochunk("return cast(System.String[], null);");
        g.dochunk("return cast(string[], null);");
      }
    } // proc TypeTest03

    [TestMethod]
    public void TypeTest04()
    {
      using (Lua l = new Lua())
      {
        dynamic g = l.CreateEnvironment();
        g.console = LuaType.GetType(typeof(Console));
        g.dochunk("console.WriteLine('Hallo!');", "test");
        dynamic wl = g.console.WriteLine;
        Assert.IsTrue(wl.GetType() == typeof(LuaOverloadedMethod));
        int iCount = wl.Count;
        Assert.IsTrue(iCount == 19);
        for (int i = 0; i < wl.Count; i++)
        {
          if (i == 17)
            Console.WriteLine("VarArgs NotImplemented.");
          else
            Console.WriteLine("{0}: {1}", i, wl[i].GetType().Name);
        }
      }
    } // propc TypeTest04

    [TestMethod]
    public void TypeTest05()
    {
      dynamic t = LuaType.GetType(typeof(int));
      Type t1 = t;
      Type t2 = (Type)t;
      Assert.IsTrue(t1 == typeof(int));
      Assert.IsTrue(t2 == typeof(int));
    } // propc TypeTest05

    [TestMethod]
    public void TypeTest06()
    {
      using (Lua l = new Lua())
      {
        dynamic g = l.CreateEnvironment();
        g.console = LuaType.GetType(typeof(Console));
        g.dochunk("console.WriteLine('Hallo!');", "test");
        dynamic wl = g.console.WriteLine;
        Delegate dlg1 = wl[LuaType.GetType(typeof(string))];
        Delegate dlg2 = g.dochunk("return console.WriteLine[clr.System.String]");
        Delegate dlg3 = g.dochunk("return console.WriteLine[]");
        Assert.IsTrue(dlg1 == dlg2);
        Assert.IsTrue(dlg3 != null);
      }
    } // propc TypeTest06

    [TestMethod]
    public void TypeTest07()
    {
      using (Lua l = new Lua())
      {
        l.PrintExpressionTree = true;
        dynamic g = l.CreateEnvironment();
        g.console = LuaType.GetType(typeof(Console));
        g.dochunk("console.WriteLine('Hallo!');", "test");
        g.dochunk("c = console.WriteLine[clr.System.String];");
        //g.c = g.console.WriteLine[typeof(string)];
        //g.c("Hallo");
        //g.dochunk("c('Hallo!')");
      }
    } // proc TypeTest07
  } // class LuaTypeTests 
}
