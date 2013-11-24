using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LuaDLR.Test
{
  public class ComplexTestClass
  {
    public static T GenericSimple<T>(T i)
    {
      return i;
    } // proc Test

    public static string GenericSimple(string i)
    {
      return i;
    } // proc Test
  } // class ComplexTestClass

  [TestClass]
  public class ComplexStructures : TestHelper
  {
    [TestMethod]
    public void Generics01()
    {
      Assert.IsTrue(TestExpression(true, GetCode("Lua.Generics01.lua"), 3));
    } // proc Generics01

    [TestMethod]
    public void Generics02()
    {
      Assert.IsTrue(TestExpression(true, "return clr.LuaDLR.Test.ComplexTestClass:GenericSimple(3)", 3));
      Assert.IsTrue(TestExpression(true, "return clr.LuaDLR.Test.ComplexTestClass:GenericSimple('3')", "3"));
    } // proc Generics02
  } // class ComplexStructures
}
