using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Globalization;
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
    public void TestRuntimeClrClass04()
    {
      Assert.IsTrue(TestReturn("return clr.System.Text.StringBuilder:GetType();", typeof(StringBuilder)));
    } // proc TestRuntimeClrClass04

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

    [TestMethod]
    public void TestRuntimeLua04()
    {
      Assert.IsTrue(TestReturn("return string.byte('hallo', 2, 4);", 97, 108, 108));
    } // proc TestRuntimeLua04

    [TestMethod]
    public void TestRuntimeLua05()
    {
      Assert.IsTrue(TestReturn("return string.byte('hallo', 2);", 97));
    } // proc TestRuntimeLua05

    [TestMethod]
    public void TestRuntimeLua06()
    {
      Assert.IsTrue(TestReturn("return 'hallo':Substring(1, 3);", "all"));
    } // proc TestRuntimeLua06

    [TestMethod]
    public void TestRuntimeLua07()
    {
      Assert.IsTrue(TestReturn("return 'hallo'[1];", 'a'));
    } // proc TestRuntimeLua07

    [TestMethod]
    public void TestRuntimeLua08()
    {
      Assert.IsTrue(TestReturn("return string.sub('hallo', 3);", "llo"));
      Assert.IsTrue(TestReturn("return string.sub('hallo', 10);", ""));
      Assert.IsTrue(TestReturn("return string.sub('hallo', 3, 4);", "ll"));
      Assert.IsTrue(TestReturn("return string.sub('hallo', -3);", "llo"));
      Assert.IsTrue(TestReturn("return string.sub('hallo', -3, -2);", "ll"));
    } // proc TestRuntimeLua08

    [TestMethod]
    public void TestRuntimeLua09()
    {
      Assert.IsTrue(TestReturn("return bit32.extract(0xFF00, 8, 8);", -1));
      Assert.IsTrue(TestReturn("return bit32.replace(0x0FFF, -1, 8, 8);", (uint)0xFFFF));
    } // proc TestRuntimeLua09

    [TestMethod]
    public void TestRuntimeLua10()
    {
      Assert.IsTrue(TestReturn("return string.format('%d', 8);", "8"));
    } // proc TestRuntimeLua10

    [TestMethod]
    public void TestRuntimeLua11()
    {
      Assert.IsTrue(TestReturn(GetCode("Lua.Runtime11.lua"), 4));
    } // proc TestRuntimeLua11

    [TestMethod]
    public void TestRuntimeLua12()
    {
      Assert.IsTrue(TestReturn(GetCode("Lua.Runtime12.lua"), 2));
    } // proc TestRuntimeLua12

    [TestMethod]
    public void TestRuntimeLua13()
    {
      Assert.IsTrue(TestReturn("return string.find('   abc', '%a+');", 4, 6, "abc"));
    } // proc TestRuntimeLua13

    [TestMethod]
    public void TestRuntimeLua14()
    {
      using (Lua l = new Lua())
      {
        var g = l.CreateEnvironment();
        l.PrintExpressionTree = true;
        g.RegisterPackage("debug", typeof(System.Diagnostics.Debug));
        g.DoChunk("debug:Print('Hallo World!');", "test.lua");
      }
    } // proc TestRuntimeLua13

    [TestMethod]
    public void TestDateTime01()
    {
      using (Lua l = new Lua())
      {
        dynamic g = l.CreateEnvironment();

        g.dochunk("print(os.date('Today is %A, in %B'))");

        TestResult(g.dochunk("return os.date('%x', 906000490)"), new DateTime(1998, 09, 17).ToString("d"));
        TestResult(g.dochunk("return os.date('%d.%m.%Y')"), DateTime.Today.ToString("d"));
        g.dochunk("t = os.date('*t');");
        DateTime dt = DateTime.Now;
        TestResult(g.dochunk("return t.year"), dt.Year);
        TestResult(g.dochunk("return t.month"), dt.Month);
        TestResult(g.dochunk("return t.day"), dt.Day);
        TestResult(g.dochunk("return t.hour"), dt.Hour);
        TestResult(g.dochunk("return t.min"), dt.Minute);
        TestResult(g.dochunk("return t.sec"), dt.Second);
        TestResult(g.dochunk("return t.wday"), (int)dt.DayOfWeek);
        TestResult(g.dochunk("return t.yday"), dt.DayOfYear);
        TestResult(g.dochunk("return t.isdst"), true);
        g.dochunk("t = os.date('!*t');");
        dt = DateTime.UtcNow;
        TestResult(g.dochunk("return t.year"), dt.Year);
        TestResult(g.dochunk("return t.month"), dt.Month);
        TestResult(g.dochunk("return t.day"), dt.Day);
        TestResult(g.dochunk("return t.hour"), dt.Hour);
        TestResult(g.dochunk("return t.min"), dt.Minute);
        TestResult(g.dochunk("return t.sec"), dt.Second);
        TestResult(g.dochunk("return t.wday"), (int)dt.DayOfWeek);
        TestResult(g.dochunk("return t.yday"), dt.DayOfYear);
        TestResult(g.dochunk("return t.isdst"), false);

        TestResult(g.dochunk("return os.date()"), DateTime.Now.ToString("G"));

        g.dochunk("t={};t.year = 2001; print(os.time(t))");
      }
    }
  } // class Runtime
}
