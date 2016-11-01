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
			LuaTable t = new LuaTable();
			t["test"] = "t";
			Assert.AreEqual(t["test"], "t");
			t.SetMemberValue("Test", "a", true);
			Assert.AreEqual(t.GetMemberValue("Test", true, true), "a");
			t.SetMemberValue("Test", "b");
			Assert.AreEqual(t["Test"], "b");
			t.SetMemberValue("Test", "n", true);
			Assert.AreEqual(t["test"], "n");

			IDictionary<string, object> a = t.Members;
			string[] r = new string[2];
			a.Keys.CopyTo(r, 0);
			Assert.IsTrue(r[0] == "test" && r[1] == "Test");

			t.SetMemberValue("Test", null, true, true);
			t.SetMemberValue("Test", null, true, true);

			Assert.IsTrue(t["Test"] == null);
			Assert.IsTrue(t["test"] == null);
		}

    [TestMethod]
    public void TestMember02()
    {
      TestCode(Lines(
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
      TestCode(Lines(
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
      TestCode(Lines(
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
      }
      catch(LuaRuntimeException)
      {
				return;
      }
			Assert.Fail();
    }

	  [TestMethod]
		public void TestMember10()
		{
			LuaGlobalPortable g = new LuaGlobalPortable(new Lua());
			g["a"] = 1;
			g.Members.Clear();

			TestResult(new LuaResult(g["a"]), new object[] { null });
		}

    #endregion

    #region -- TestIndex --------------------------------------------------------------

    [TestMethod]
    public void TestIndex01()
    {
			LuaTable t = new LuaTable();
			t[1] = 1;
			t[(object)2] = 2;
			for (int i = 4; i <= 16; i++)
				t[i] = i;
			Assert.AreEqual(t.Length, 2);
			Assert.AreEqual(t.Values.Count, 15);
			Assert.AreEqual(t[1], 1);
			Assert.AreEqual(t[4], 4);
			t[3] = 3;
			Assert.AreEqual(t.Length, 16);
			Assert.AreEqual(t.Values.Count, 16);
			Assert.AreEqual(t[4], 4);

			t[1] = null;
			Assert.AreEqual(t.Length, 0);
			Assert.AreEqual(t.Values.Count, 15);
			Assert.AreEqual(t[4], 4);
			t[18] = 18;
			Assert.AreEqual(t[18], 18);
			Assert.AreEqual(t.Values.Count, 16);
			t[20] = 20;
			Assert.AreEqual(t[20], 20);
			Assert.AreEqual(t.Values.Count, 17);
			t[17] = 17;
			Assert.AreEqual(t.Values.Count, 18);
			t[1] = 1;
			Assert.AreEqual(t.Values.Count, 19);
			Assert.AreEqual(t.Length, 18);
			for (int i = 1; i <= 18; i++)
				Assert.AreEqual(t[i], i);
			Assert.AreEqual(t[20], 20);
		}

		[TestMethod]
    public void TestIndex02()
    {
      TestCode(Lines(
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
      TestCode(Lines(
        "local t : table = {};",
        "t['test'] = 3;",
        "return t.test;"), 3);
    }

    [TestMethod]
    public void TestIndex05()
    {
      TestCode(Lines(
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

		[TestMethod]
		public void TestIndex07()
		{
			TestCode(Lines(
				"local days = {'Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'};",
				"return days[1], days[2], days[3], days[4];"),
				"Sunday", "Monday", "Tuesday", "Wednesday");
		}

		[TestMethod]
		public void TestIndex08()
		{
			LuaTable t = new LuaTable();
			t[-1] = 50;
			Assert.AreEqual(50, t[-1]);
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
      TestCode(Lines(
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
      TestCode(Lines(
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
      TestCode(Lines(
        "tm = {}",
        "tm.__unm = function (t) return -t.n; end;",
        "tm.__bnot = function (t) return ~t.n; end;",
        "t = { __metatable = tm, n = 4 };",
        "return -t, ~t;"), -4, ~4);
    }

    [TestMethod]
    public void TestMetaTable05()
    {
      TestCode(Lines(
        "tm = {}",
        "tm.__concat = function (t, a) return t.s .. a; end;",
        "t = { __metatable = tm, s = 'a' };",
        "return t .. 'b', 'a' .. t .. 'b';"), "ab", "aab");
    }

    [TestMethod]
    public void TestMetaTable06()
    {
      TestCode(Lines(
        "tm = {}",
        "tm.__len = function (t) return 4; end;",
        "t = { __metatable = tm };",
        "return #t, #tm, #'aa';"), 4, 0 , 2);
    }

    [TestMethod]
    public void TestMetaTable07()
    {
      TestCode(Lines(
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
      TestCode(Lines(
        "tm = {}",
        "tm.__index = function (t, key) return key; end;",
        "t = { __metatable = tm, n = 4 };",
        "return t.n, t.test, t[1] ;"), 4, "test", 1);
    }

    [TestMethod]
    public void TestMetaTable09()
    {
      TestCode(Lines(
        "tm = {}",
        "tm.__index = { test = 1, [1] = 2 };",
        "t = { __metatable = tm, n = 4 };",
        "return t.n, t.test, t[1] ;"), 4, 1, 2);
    }

    [TestMethod]
    public void TestMetaTable10()
    {
      TestCode(Lines(
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
      TestCode(Lines(
        "tm = {}",
        "tm.__call = function (t, ...) return #{...}; end;",
        "t = { __metatable = tm };",
        "return t(1,2,3);"), 3);
    }

    #endregion

    #region -- TestConvert ------------------------------------------------------------

    [TestMethod]
    public void TestConvert01()
    {
			using (Lua l = new Lua())
			{
				l.PrintExpressionTree = Console.Out;
				var g = l.CreateEnvironment();
				var r = g.DoChunk("return cast(System.Diagnostics.ProcessStartInfo, { FileName = 'Test.exe', Arguments = 'aaa' });", "dummy");
				ProcessStartInfo psi = (ProcessStartInfo)r[0];
				Assert.IsTrue(psi.FileName == "Test.exe");
				Assert.IsTrue(psi.Arguments == "aaa");
			}
    } // func TestConvert01

    #endregion

		#region -- Length -----------------------------------------------------------------

		[TestMethod]
		public void TestLength01()
		{
			dynamic t = new LuaTable();
			t[1] = 1;
			t[2] = 1;
			t[4] = 1;
			Assert.IsTrue(((LuaTable)t).Length == 2);
			t[8] = 1;
			t[3] = 1;
			Assert.IsTrue(((LuaTable)t).Length == 4);
		}

		#endregion

		#region -- Insert, Sort, ... ------------------------------------------------------

		[TestMethod]
		public void TestInsert01()
		{
			LuaTable t = new LuaTable();
			LuaTable.insert(t, "a");
			LuaTable.insert(t, "c");
			LuaTable.insert(t, 2, "b");
			Assert.AreEqual(t.ArrayList.Count, 3);
			Assert.AreEqual(t.ArrayList[0], "a");
			Assert.AreEqual(t.ArrayList[1], "b");
			Assert.AreEqual(t.ArrayList[2], "c");
		}


		[TestMethod]
		public void TestInsert02()
		{
			TestCode(Lines(
				"local t = {};",
				"table.insert(t, 'a');",
				"table.insert(t, 'c');",
				"table.insert(t, 2, 'b');",
				"return t[1], t[2], t[3];"),
				"a", "b", "c");
		}

		[TestMethod]
		public void TestCollect01()
		{
			LuaTable t = new LuaTable();
			t[1] = 1;
			t[2] = 2;
			t[3] = 3;
			t[18] = 18;
			TestResult(LuaTable.collect(t), 1, 2, 3);
		}

		[TestMethod]
		public void TestCollect02()
		{
			LuaTable t = new LuaTable();
			t[1] = 1;
			t[2] = 2;
			t[3] = 3;
			t[18] = 18;
			TestResult(LuaTable.collect(t, 1, 20), 1, 2, 3, 18);
		}

		[TestMethod]
		public void TestUnpack01()
		{
			LuaTable t = new LuaTable();
			t[1] = 1;
			t[2] = 2;
			t[3] = 3;
			t[18] = 18;
			TestResult(LuaTable.unpack(t), 1, 2, 3);
		}

		[TestMethod]
		public void TestUnpack02()
		{
			TestCode(Lines(
				"local t = {1, 2, 3, [18] = 18};",
				"return table.unpack(t);"),
				1, 2, 3);
		}

		[TestMethod]
		public void TestUnpack03()
		{
			LuaTable t = new LuaTable();
			t[1] = 1;
			t[2] = 2;
			t[3] = 3;
			t[18] = 18;
			TestResult(LuaTable.unpack(t, 1, 20), 1, 2, 3, null, null, null, null, null, null, null, null, null, null, null, null, null, null, 18, null, null);
		}

		[TestMethod]
		public void TestUnpack04()
		{
			TestCode(Lines(
				"local t = {1, 2, 3, [5] = 5};",
				"return table.unpack(t, 1, 6);"),
				1, 2, 3, null, 5, null);
		}

		[TestMethod]
		public void TestPack01()
		{
			object[] a = new object[20];
			a[0] = 1;
			a[1] = 2;
			a[2] = 3;
			a[17] = 18;
			LuaTable t = LuaTable.pack(a);

			Assert.AreEqual(t[1], 1);
			Assert.AreEqual(t[2], 2);
			Assert.AreEqual(t[3], 3);
			Assert.AreEqual(t[18], 18);
			Assert.AreEqual(t.Length, 3);
			Assert.AreEqual(t["n"], 20);
		}

		[TestMethod]
		public void TestPack02()
		{
			TestCode(Lines(
				"local t = {1, 2, 3, [8] = 8};",
				"local r = table.pack(table.unpack(t));",
				"return r.n, #r, r[1], r[3];"),
				3, 3, 1, 3);
		}

		[TestMethod]
		public void TestPack03()
		{
			TestCode(Lines(
				"local f = function () return 1, 2, 3, nil, 8; end;",
				"local r = table.pack(f());",
				"return r.n, #r, r[1], r[3];"),
				5, 3, 1, 3);
		}

		[TestMethod]
		public void TestPack04()
		{
			TestCode(Lines(
				"local f = function (...) return table.pack(...); end;",
				"local r = f(1, 2, 3, nil, 8);",
				"return r.n, #r, r[1], r[3];"),
				5, 3, 1, 3);
		}

		[TestMethod]
		public void TestRemove01()
		{
			TestCode(Lines(
				"local t = {1, 2, 3, [8] = 8};",
				"return table.remove(t, 1), #t;"),
				1, 2);
			TestCode(Lines(
				"local t = {1, 2, 3, 4, 5, 6, 7, 8};",
				"return table.remove(t, 1), #t;"),
				1, 7);
		}

		[TestMethod]
		public void TestRemove02()
		{
			TestCode(Lines(
				"local t = {1, 2, 3, [8] = 8};",
				"return table.remove(t, 8), #t;"),
				8, 3);
		}

		[TestMethod]
		public void TestRemove03()
		{
			TestCode(Lines(
				"local t = {1, 2, 3, [8] = 8};",
				"return table.remove(t, 7), #t;"),
				null, 3);
		}

		[TestMethod]
		public void TestRemove04()
		{
			TestCode(Lines(
				"local t = {1, 2, 3, [8] = 8};",
				"return table.remove(t), #t;"),
				3, 2);
		}

		[TestMethod]
		public void TestSort01()
		{
			TestCode(Lines(
				"local t = {3, 2, 1};",
				"table.sort(t);",
				"return t[1], t[2], t[3];"),
				1, 2, 3);
		}

		[TestMethod]
		public void TestSort02()
		{
			TestCode(Lines(
				"local t = {2, 3, 1};",
				"table.sort(t, function (x,y) return y - x; end);",
				"return t[1], t[2], t[3];"),
				3, 2, 1);
		}

		[TestMethod]
		public void TestSort03()
		{
			TestCode(Lines(
				"local t = {2, 1 , 1, 3};",
				"table.sort(t, function (x,y) return x < y; end);",
				"return t[1], t[2], t[3], t[4];"),
				1, 1, 2, 3);
		}

		[TestMethod]
		public void TestSort04()
		{
			TestCode(Lines(
				"local t = {2, 1 , 1, 3};",
				"table.sort(t, function (x,y) return x > y; end);",
				"return t[1], t[2], t[3], t[4];"),
				3, 2, 1, 1);
		}

		[TestMethod]
		public void TestConcat01()
		{
			TestCode(Lines(
				"local t = {1, 2, 3, [8] = 8};",
				"return table.concat(t, ' + ');"),
				"1 + 2 + 3");
		}

		[TestMethod]
		public void TestConcat02()
		{
			TestCode(Lines(
				"local t = {1, 2, 3, [8] = 8};",
				"return table.concat(t, ' + ', 2, 8);"),
				"2 + 3 + 8");
		}

		#endregion

		#region -- Get Member Test --------------------------------------------------------

		public class LuaTableFirst : LuaTable
		{
			[LuaMember("Foo")]
			private void LuaFoo()
			{
				Console.WriteLine("{0}:Foo() called", GetType().Name);
			}

			[LuaMember()]
			private void TestDef()
			{
				Console.WriteLine("{0}:TestDef() called", GetType().Name);
			}

			[LuaMember(true)]
			private string Bar(LuaTable self)
			{
				return self?.GetType().Name;
			}
		}

		public class LuaTableSecond : LuaTableFirst
		{
		}

		public class LuaTableParent : LuaTable
		{
			private readonly LuaTable f = new LuaTableFirst();

			protected override object OnIndex(object key)
				=> base.OnIndex(key) ?? f.GetValue(key);
		}

		[TestMethod]
		public void TestGetMemberBind()
		{
			var a = new LuaTableFirst();
			var b = new LuaTableSecond();

			using (var l = new Lua())
			{
				var g = l.CreateEnvironment();
				g["a"] = a;
				g["b"] = b;

				g.DoChunk("b.Foo(); a.Foo(); a.TestDef()", "test.lua");
			}
		}

		[TestMethod]
		public void TestGetMemberBind02()
		{
			var a = new LuaTableFirst();
			var b = new LuaTableParent();

			using (var l = new Lua())
			{
				var g = l.CreateEnvironment();
				g["a"] = a;
				g["b"] = b;

				l.PrintExpressionTree = Console.Out;
				TestResult(
					g.DoChunk("return a:Bar(), b:Bar(), b.Bar(b), b.Bar(a)", "test.lua"),
					"LuaTableFirst", "LuaTableParent", "LuaTableParent", "LuaTableFirst"
				);
			}
		}

		#endregion


		[TestMethod]
    public void EnvDynamicCall01()
    {
			using (Lua l = new Lua())
			{
				l.PrintExpressionTree = Console.Out;
				dynamic g = l.CreateEnvironment();
				g.dochunk(GetLines("Lua.EnvDynamicCall01.lua"), "test.lua");

				TestResult(new LuaResult(g.test(2)), 4);
				TestResult(((LuaTable)g).CallMember("test", 2), 4);

				// test of c# binders
				Debug.Print("C# Binders:");
				TestResult(g.b.a(5), 20);
				TestResult(g.b.b(5), 115);
				TestResult(g.b.c(g.b, 5), 110);
				TestResult(g.test(5), 10);

				// test of lua binders
				Debug.Print("Lua Binders:");
				TestResult(g.dochunk("return b.a(5)", "test.lua"), 20);
				TestResult(g.dochunk("return b:b(5)", "test.lua"), 115);
				TestResult(g.dochunk("return b.b(b, 5)", "test.lua"), 115);
				TestResult(g.dochunk("return b:c(5)", "test.lua"), 110);
				TestResult(g.dochunk("return b.c(b, 5)", "test.lua"), 110);
				TestResult(g.dochunk("return test(5)", "test.lua"), 10);
			}
    } // prop EnvDynamicCall01

		[TestMethod]
		public void TestTableInterface01()
		{
			LuaTable t = new LuaTable();
			t[1] = 1;
			t[2] = 2;
			t[100] = 100;

			t["Test"] = "test";
			t["o"] = "o";

			Assert.AreEqual(t.Length, 2);
			Assert.AreEqual(t.ArrayList.Count, 2);
			Assert.AreEqual(t.Members.Count, 2);
			Assert.AreEqual(t.Values.Count, 5);

			t[2] = null;

			Assert.AreEqual(t.Length, 1);
			Assert.AreEqual(t.ArrayList.Count, 1);
			Assert.AreEqual(t.Members.Count, 2);
			Assert.AreEqual(t.Values.Count, 4);

			t["o"] = null;

			Assert.AreEqual(t.Length, 1);
			Assert.AreEqual(t.ArrayList.Count, 1);
			Assert.AreEqual(t.Members.Count, 1);
			Assert.AreEqual(t.Values.Count, 3);

			int i = 0;
			foreach (var c in t.ArrayList)
				Console.WriteLine("Index [{0}] = {1}", i++, c);
			foreach (var c in t.Members)
				Console.WriteLine("Member [{0}] = {1}", c.Key, c.Value);
			foreach (var c in t.Values)
				Console.WriteLine("Key [{0}] = {1}", c.Key, c.Value);
		}
  } // class LuaTableTests
}
