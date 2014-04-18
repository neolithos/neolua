using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.IronLua;

namespace LuaDLR.Test
{
  [TestClass]
  public class LuaTableTests : TestHelper
  {
    #region -- TestMember -------------------------------------------------------------

    [TestMethod]
    public void TestMember01()
    {
      TestCode(String.Join(Environment.NewLine,
        "local t : table = {};",
        "t.test = 3;",
        "return t.test;"), 3);
    }

    [TestMethod]
    public void TestMember02()
    {
      TestCode(String.Join(Environment.NewLine,
        "t = {};",
        "t.test = 3;",
        "return t.test;"), 3);
    }

    [TestMethod]
    public void TestMember03()
    {
      dynamic t = new LuaTable();
      t.test = 3;
      TestResult(new LuaResult(t.test), 3);
    }

    [TestMethod]
    public void TestMember04()
    {
      TestCode(String.Join(Environment.NewLine,
        "t = {};",
        "t.hallo = 42;",
        "t.hallo1 = 43;",
        "return t.hallo, t.hallo1;"), 42, 43);
    }

    [TestMethod]
    public void TestMember05()
    {
      dynamic t = new LuaTable();
      t.hallo = 42;
      t.hallo1 = 43;
      TestResult(new LuaResult(t.hallo, t.hallo1), 42, 43);
    }

    [TestMethod]
    public void TestMember06()
    {
      TestCode(String.Join(Environment.NewLine,
        "t = {};",
        "t.hallo = 42;",
        "t.hallo = 43;",
        "return t.hallo;"), 43);
    }

    [TestMethod]
    public void TestMember07()
    {
      dynamic t = new LuaTable();
      t.hallo = 42;
      t.hallo = 43;
      TestResult(new LuaResult(t.hallo), 43);
    }

    [TestMethod]
    public void TestMember08()
    {
      TestCode("hallo = 42; _G['hallo'] = 43; return hallo;", 43);
    }

    [TestMethod]
    public void TestMember09()
    {
      try
      {
        TestCode(
          Lines(
          "test.year = 2001;",
          "return test.year;"
           ), 2001);
        Assert.Fail();
      }
      catch(TargetInvocationException e)
      {
        Assert.IsTrue(e.InnerException is LuaRuntimeException);
      }
    }

    #endregion

    #region -- TestIndex --------------------------------------------------------------

    [TestMethod]
    public void TestIndex01()
    {
      TestCode(String.Join(Environment.NewLine,
        "local t : table = {};",
        "t[2] = 3;",
        "return t[2];"), 3);
    }

    [TestMethod]
    public void TestIndex02()
    {
      TestCode(String.Join(Environment.NewLine,
        "t = {};",
        "t[2] = 3;",
        "return t[2];"), 3);
    }

    [TestMethod]
    public void TestIndex03()
    {
      LuaTable t = new LuaTable();
      t[2] = 3;
      TestResult(new LuaResult(t[2]), 3);
    }

    [TestMethod]
    public void TestIndex04()
    {
      TestCode(String.Join(Environment.NewLine,
        "local t : table = {};",
        "t['test'] = 3;",
        "return t.test;"), 3);
    }

    [TestMethod]
    public void TestIndex05()
    {
      TestCode(String.Join(Environment.NewLine,
        "t = {};",
        "t['test'] = 3;",
        "return t.test;"), 3);
    }

    [TestMethod]
    public void TestIndex06()
    {
      dynamic t = new LuaTable();
      t["test"] = 3;
      TestResult(new LuaResult(t.test), 3);
    }

    #endregion

    #region -- MetaTable --------------------------------------------------------------

    [TestMethod]
    public void TestMetaTable01()
    {
      dynamic tm = new LuaTable();
      tm.__add = new Func<dynamic, int, int>((a, b) => a.n + b);

      dynamic t = new LuaTable();
      t.__metatable = tm;
      t.n = 4;
      TestResult(new LuaResult(t + 2), 6);
    }

    [TestMethod]
    public void TestMetaTable02()
    {
      TestCode(String.Join(Environment.NewLine,
        "tm = {}",
        "tm.__add = function (t, a) return t.n + a; end;",
        "tm.__sub = function (t, a) return t.n - a; end;",
        "tm.__mul = function (t, a) return t.n * a; end;",
        "tm.__div = function (t, a) return t.n / a; end;",
        "tm.__idiv = function (t, a) return t.n // a; end;",
        "tm.__pow = function (t, a) return t.n ^ a; end;",
        "t = { __metatable = tm, n = 4 };",
        "return t + 2, t - 2, t * 2, t / 2, t // 3, t ^ 2;"), 6, 2, 8, 2.0, 1, 16.0);
    }

    [TestMethod]
    public void TestMetaTable03()
    {
      TestCode(String.Join(Environment.NewLine,
        "tm = {}",
        "tm.__band = function (t, a) return t.n & a; end;",
        "tm.__bor = function (t, a) return t.n | a; end;",
        "tm.__bxor = function (t, a) return t.n ~ a; end;",
        "tm.__shr = function (t, a) return t.n >> a; end;",
        "tm.__shl = function (t, a) return t.n << a; end;",
        "t = { __metatable = tm, n = 4 };",
        "return t & 2, t | 2, t ~ 2, t >> 2, t << 2;"), 4 & 2, 4 | 2, 4 ^ 2, 4 >> 2, 4 << 2);
    }

    [TestMethod]
    public void TestMetaTable04()
    {
      TestCode(String.Join(Environment.NewLine,
        "tm = {}",
        "tm.__unm = function (t) return -t.n; end;",
        "tm.__bnot = function (t) return ~t.n; end;",
        "t = { __metatable = tm, n = 4 };",
        "return -t, ~t;"), -4, ~4);
    }

    [TestMethod]
    public void TestMetaTable05()
    {
      TestCode(String.Join(Environment.NewLine,
        "tm = {}",
        "tm.__concat = function (t, a) return t.s .. a; end;",
        "t = { __metatable = tm, s = 'a' };",
        "return t .. 'b', 'a' .. t .. 'b';"), "ab", "aab");
    }

    [TestMethod]
    public void TestMetaTable06()
    {
      TestCode(String.Join(Environment.NewLine,
        "tm = {}",
        "tm.__len = function (t) return 4; end;",
        "t = { __metatable = tm };",
        "return #t, #tm, #'aa';"), 4, -1 , 2);
    }

    [TestMethod]
    public void TestMetaTable07()
    {
      TestCode(String.Join(Environment.NewLine,
        "tm = {}",
        "tm.__eq = function (t, a) return t.n == a; end;",
        "tm.__lt = function (t, a) return t.n < a; end;",
        "tm.__le = function (t, a) return t.n <= a; end;",
        "t = { __metatable = tm, n = 4 };",
        "return t == 4, t ~= 4, t < 5, t > 3, t <= 4, t >= 4 ;"), true, false, true, true, true, true);
    }

    [TestMethod]
    public void TestMetaTable08()
    {
      TestCode(String.Join(Environment.NewLine,
        "tm = {}",
        "tm.__index = function (t, key) return key; end;",
        "t = { __metatable = tm, n = 4 };",
        "return t.n, t.test, t[1] ;"), 4, "test", 1);
    }

    [TestMethod]
    public void TestMetaTable09()
    {
      TestCode(String.Join(Environment.NewLine,
        "tm = {}",
        "tm.__index = { test = 1, [1] = 2 };",
        "t = { __metatable = tm, n = 4 };",
        "return t.n, t.test, t[1] ;"), 4, 1, 2);
    }

    [TestMethod]
    public void TestMetaTable10()
    {
      TestCode(String.Join(Environment.NewLine,
        "tm = {}",
        "tm.__newindex = function (t, key, value) rawset(t, '_' .. key .. '_', value); end;",
        "t = { n = 4, __metatable = tm };",
        "t.test = 1;",
        "t[1] = 2;",
        "rawset(t, 3, 3);",
        "return t;"), Table(TV("n", 4), TV("_test_", 1), TV("_1_", 2), TV(3, 3)));
    }

    [TestMethod]
    public void TestMetaTable11()
    {
      TestCode(String.Join(Environment.NewLine,
        "tm = {}",
        "tm.__call = function (t, ...) return #{...}; end;",
        "t = { __metatable = tm };",
        "return t(1,2,3);"), 3);
    }

    #endregion

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
