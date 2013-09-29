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
      TestExpression(true, GetCode("Lua.Control01.lua"), 3);
    }

    [TestMethod]
    public void Control02()
    {
      TestExpression(true, GetCode("Lua.Control02.lua"), 10);
    }

    [TestMethod]
    public void Control03()
    {
      TestExpression(true, GetCode("Lua.Control03.lua"), 10);
    }

    [TestMethod]
    public void Control04()
    {
      TestExpression(true, GetCode("Lua.Control04.lua"), 4);
    }

    [TestMethod]
    public void Control05()
    {
      TestExpression(true, GetCode("Lua.Control05.lua"), 10);
    }

    [TestMethod]
    public void Control06()
    {
      TestExpression(true, GetCode("Lua.Control06.lua"), 55);
    }
    [TestMethod]
    public void Control07()
    {
      TestExpression(true, "return;", null);
    }

  } // class ControlStructures
}
