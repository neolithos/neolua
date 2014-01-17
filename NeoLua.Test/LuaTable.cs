using System;
using System.Collections.Generic;
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
      Assert.IsTrue(TestReturn("hallo = 42; _G['hallo'] = 43; return hallo;", 43));
    } // proc TestMemberSet02

    [TestMethod]
    public void EnvDynamicCall01()
    {
      using (Lua l = new Lua())
      {
        dynamic g = l.CreateEnvironment();
        dynamic dc = g.dochunk;
        dc(GetCode("Lua.EnvDynamicCall01.lua"), "test.lua");
        Assert.IsTrue(TestReturn(g.b.b(5), 15));
      }
    } // prop EnvDynamicCall01
  } // class LuaTableTests
}
