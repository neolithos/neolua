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
  public class Functions : TestHelper
  {
    [TestMethod]
    public void TestFunctions01()
    {
      Assert.IsTrue(TestReturn("function test(a) return 1 + a; end; return test(2);", 3));
    } // proc TestFunctions01

    [TestMethod]
    public void TestFunctions02()
    {
      Assert.IsTrue(TestReturn("local function test(a) return 1 + a; end; return test(2);", 3));
    } // proc TestFunctions01

    [TestMethod]
    public void TestFunctions03()
    {
      Assert.IsTrue(TestReturn("local test = function (a) return 1 + a; end; return test(2);", 3));
    } // proc TestFunctions01

    [TestMethod]
    public void TestFunctions04()
    {
      Assert.IsTrue(TestReturn("local test = function () return 1, 2, 3; end; return (test());", 1));
    } // proc TestFunctions01

    [TestMethod]
    public void TestFunctions05()
    {
      Assert.IsTrue(TestReturn("local test = function () return 1, 2, 3; end; return test();", 1, 2, 3));
    } // proc TestFunctions01

    [TestMethod]
    public void TestFunctions06()
    {
      Assert.IsTrue(TestReturn("local test = function () return 1, 2, 3; end; return 'a', test();", "a", 1, 2, 3));
    } // proc TestFunctions01

    [TestMethod]
    public void TestFunctions07()
    {
      Assert.IsTrue(TestReturn("local test = function () return 3, 2, 1; end; return 2 * test();", 6));
    } // proc TestFunctions01

    [TestMethod]
    public void TestFunctions08()
    {
      Assert.IsTrue(TestReturn(GetCode("Lua.Function08.lua"), 1, 4));
    } // proc TestFunctions01

    // Todo: Test für Parameter
  } // class Functions
}
