using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.IronLua;

namespace LuaDLR.Test
{
  [TestClass]
  public class Runtime : TestHelper
  {
    public class SubClass
    {
      public SubClass(byte a)
      {
        this.Value = a;
      }

      public byte Value { get; set; }
    } // class SubClass

    [TestMethod]
    public void TestRuntimeClrProperty01()
    {
      Assert.IsTrue(TestReturn("return clr.System.Environment.NewLine;", Environment.NewLine));
    } // proc TestRuntimeClrProperty

    [TestMethod]
    public void TestRuntimeClrMethod01()
    {
      Assert.IsTrue(TestReturn("return clr.System.Convert:ToInt32(cast(short, 2));", 2));
    } // proc TestRuntimeClrMethod

    [TestMethod]
    public void TestRuntimeClrMethod02()
    {
      Assert.IsTrue(TestReturn("function a() return 'Hallo ', 'Welt', '!'; end; return clr.System.String:Concat(a());", "Hallo Welt!"));
    } // proc TestRuntimeClrMethod

    [TestMethod]
    public void TestRuntimeClrMethod03()
    {
      Assert.IsTrue(TestReturn("function a() return 'Hallo ', 'Welt', '!'; end; return clr.System.String:Concat('Text', ': ', a());", "Text: Hallo Welt!"));
    } // proc TestRuntimeClrMethod

    [TestMethod]
    public void TestRuntimeClrClass01()
    {
      Assert.IsTrue(TestReturn("local a = clr.LuaDLR.Test.TestParam:ctor(); return a:GetValue();", 4));
    } // proc TestRuntimeClrClass01

    [TestMethod]
    public void TestRuntimeClrClass02()
    {
      Assert.IsTrue(TestReturn("local a = clr.LuaDLR.Test.Runtime.SubClass:ctor(4); return a.Value;", (byte)4));
    } // proc TestRuntimeClrClass02

    [TestMethod]
    public void TestRuntimeLua01()
    {
      Assert.IsTrue(TestReturn("print('Hallo Welt');"));
    } // proc TestRuntimeLua01

    [TestMethod]
    public void TestRuntimeLua02()
    {
      Assert.IsTrue(TestReturn("local p = print; print = function() p('Hallo Welt'); end; print();"));
    } // proc TestRuntimeLua02

    [TestMethod]
    public void TestRuntimeLua03()
    {
      Assert.IsTrue(TestReturn("return cast(int, math.abs(-1));", 1));
    } // proc TestRuntimeLua03
  } // class Runtime
}
