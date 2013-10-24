using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LuaDLR.Test
{
  [TestClass]
  public class ControlStructures : TestHelper
  {
    [TestMethod]
    public void Control01()
    {
      Assert.IsTrue(TestExpression(true, GetCode("Lua.Control01.lua"), 3));
    }

    [TestMethod]
    public void Control02()
    {
      Assert.IsTrue(TestExpression(true, GetCode("Lua.Control02.lua"), 10));
    }

    [TestMethod]
    public void Control03()
    {
      Assert.IsTrue(TestExpression(true, GetCode("Lua.Control03.lua"), 10));
    }

    [TestMethod]
    public void Control04()
    {
      Assert.IsTrue(TestExpression(true, GetCode("Lua.Control04.lua"), 4));
    }

    [TestMethod]
    public void Control05()
    {
      Assert.IsTrue(TestExpression(true, GetCode("Lua.Control05.lua"), 10));
    }

    [TestMethod]
    public void Control06()
    {
      Assert.IsTrue(TestExpression(true, GetCode("Lua.Control06.lua"), 55));
    }

    [TestMethod]
    public void Control07()
    {
      Assert.IsTrue(TestExpression(true, "return;", null));
    }

    [TestMethod]
    public void Control08()
    {
      Assert.IsTrue(TestExpression(true, GetCode("Lua.Control08.lua"), 4321));
    }

    [TestMethod]
    public void Control09()
    {
      Assert.IsTrue(TestExpression(true, GetCode("Lua.Control09.lua"), 4321));
    }

    [TestMethod]
    public void Control10()
    {
      Assert.IsTrue(TestExpression(true, GetCode("Lua.Control10.lua"), 4321));
    }
  } // class ControlStructures
}
