using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.IronLua;

namespace LuaDLR.Test
{
  [TestClass]
  public class LuaTableTests : TestHelper
  {
    [TestMethod]
    public void TestMemberSet01()
    {
      Assert.IsTrue(TestReturn("hallo = 42; hallo1 = 43; return hallo, hallo1;", 42, 43));
      Assert.IsTrue(TestReturn("hallo = 42; hallo = 43; return hallo;", 43));
    } // proc TestMemberSet01

    [TestMethod]
    public void TestMemberSet02()
    {
      TestCode("hallo = 42; _G['hallo'] = 43; return hallo;", 43);
    } // proc TestMemberSet02

    //[TestMethod]
    //public void TestMemberSet03()
    //{
    //  TestCode(
    //    Lines(
    //    "test.year = 2001;",
    //    "return test.year;"
    //     ),2001);
    //} // proc TestMemberSet02

    [TestMethod]
    public void EnvDynamicCall01()
    {
      using (Lua l = new Lua())
      {
        l.PrintExpressionTree = true;
        dynamic g = l.CreateEnvironment();
        g.dochunk(GetCode("Lua.EnvDynamicCall01.lua"), "test.lua");

        // test of c# binders
        Debug.Print("C# Binders:");
        Assert.IsTrue(TestReturn(g.b.a(5), 20));
        Assert.IsTrue(TestReturn(g.b.b(5), 115));
        Assert.IsTrue(TestReturn(g.b.c(g.b, 5), 110));
        Assert.IsTrue(TestReturn(g.test(5), 10));

        // test of lua binders
        Debug.Print("Lua Binders:");
        Assert.IsTrue(TestReturn(g.dochunk("return b.a(5)", "test.lua"), 20));
        Assert.IsTrue(TestReturn(g.dochunk("return b:b(5)", "test.lua"), 115));
        Assert.IsTrue(TestReturn(g.dochunk("return b.b(b, 5)","test.lua"),  115));
        Assert.IsTrue(TestReturn(g.dochunk("return b:c(5)","test.lua"),  110));
        Assert.IsTrue(TestReturn(g.dochunk("return b.c(b, 5)","test.lua"),  110));
        Assert.IsTrue(TestReturn(g.dochunk("return test(5)", "test.lua"), 10));
      }
    } // prop EnvDynamicCall01
  } // class LuaTableTests
}
