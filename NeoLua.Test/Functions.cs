using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.IronLua;

namespace LuaDLR.Test
{
	public delegate int ParamOutDelegate(int a, ref int b, out int c);
	public delegate int ParamIntDelegate(int a, int b, int c);

	public class TestParam
	{
		public int ParamOut(int a, ref int b, out int c)
		{
			c = b;
			b = a;
			return a;
		} // func ParamOut

		public int GetValue()
		{
			return Value;
		}

		public int Value { get { return 4; } }

		public int Test { get; set; }

		public int this[int i] { get { return i; } }
	} // class TestParam

	internal class TestDelegateConvert : LuaTable
	{
		[LuaMember]
		public ParamIntDelegate Dlg { get; set; }
	}

	[TestClass]
	public class Functions : TestHelper
	{
		public delegate void GetByrefDelegate(out int a, ref int b, out int c);

		public static int PropertyTest { get; set; }

		public static int SumInts(int a, int b, int c, int[] r)
		{
			int s = a + b + c;
			for (int i = 0; i < r.Length; i++)
				s += r[i];
			return s;
		}

		public static int SumInts2(int a, int b)
		{
			return a + b;
		}

		public static int[] GetArray(int l)
		{
			int[] a = new int[l];
			for (int i = 0; i < l; i++)
				a[i] = i + 1;
			return a;
		}

		public static LuaResult GetResult(int l)
		{
			object[] a = new object[l];
			for (int i = 0; i < l; i++)
				a[i] = i + 1;
			return new LuaResult(a);
		}

		public static void GetByref(out int a, ref int b, out int c)
		{
			int d = b;
			a = d + 1;
			b = d + 2;
			c = d + 3;
		}

		[TestMethod]
		public void TestFunctions01()
		{
			TestCode(Lines(
			  "function test(a)",
			  "  return 1 + a;",
			  "end;",
			  "return test(2);"),
			  3);
		}

		private void PrintDelegateArguments(MethodInfo mi)
		{
			int i = 0;
			foreach (ParameterInfo p in mi.GetParameters())
				Console.WriteLine("[{0}] {1} : {2}", i++, p.Name, p.ParameterType.Name);
		}

		private void PrintDelegate(Delegate dlg)
		{
			Type t = dlg.GetType();
			Console.WriteLine("{0} : {1}", dlg.Method.Name, t.Name);
			PrintDelegateArguments(dlg.Method);
			PrintDelegateArguments(t.GetMethod("Invoke"));

			Console.WriteLine();
			Console.WriteLine(new string('=', 66));
		}

		[TestMethod]
		public void TestFunctions02()
		{
			TestParam p = new TestParam();
			int i = 0;
			Func<TestParam, int> f1 = (p_) => { i++; return p.Value + p_.Value; };
			Func<int> f2 = p.GetValue;
			Func<int> f3 = () => 1;
			PrintDelegate(f1);
			PrintDelegate(f2);
			PrintDelegate(f3);
			PrintDelegate(new ParamOutDelegate(p.ParamOut));
			Console.WriteLine(i);
		}

		[TestMethod]
		public void TestFunctions03()
		{
			TestCode(Lines(
			  "local function test(a)",
			  "  return 1 + a;",
			  "end;",
			  "return test(2);"),
			  3);
			TestCode(Lines(
			  "local function test(a : int) : int",
			  "  return 1 + a;",
			  "end;",
			  "local a : int = test(2);",
			  "return a;"),
			  3);
		}

		[TestMethod]
		public void TestFunctions04()
		{
			TestCode(Lines(
			  "local f : System.Func[int, int, int, int[], int] = clr.LuaDLR.Test.Functions.SumInts;",
			  "return f(), f(1, 2), f(1, 2, 3), f(1, 2, 3, 4), f(1, 2, 3, 4, 5);"),
			  0, 3, 6, 10, 15);
		}

		[TestMethod]
		public void TestFunctions05()
		{
			TestCode(Lines(
			  "local f : System.Func[int, int, int, int[], int] = clr.LuaDLR.Test.Functions.SumInts;",
			  "local r : result = clr.LuaDLR.Test.Functions.GetResult(0);",
			  "return f(r);"),
			  0);
		}

		[TestMethod]
		public void TestFunctions06()
		{
			TestCode(Lines(
			  "local f : System.Func[int, int, int, int[], int] = clr.LuaDLR.Test.Functions.SumInts;",
			  "local r : result = clr.LuaDLR.Test.Functions.GetResult(5);",
			  "return f(r), f(1, r),",
			  "   f(1, 1, 1, r), ",
			  "   f(1, 1, 1, 1, r);"),
			  15, 16, 18, 19);
		}

		[TestMethod]
		public void TestFunctions07()
		{
			TestCode(Lines(
			  "local f : System.Func[int, int, int, int[], int] = clr.LuaDLR.Test.Functions.SumInts;",
			  "local r : int[] = clr.LuaDLR.Test.Functions.GetArray(5);",
			  "return f(1, 1, 1, r);"),
			  18);
		}

		[TestMethod]
		public void TestFunctions08()
		{
			TestCode(Lines(
			  "local f : LuaDLR.Test.Functions.GetByrefDelegate = clr.LuaDLR.Test.Functions.GetByref;",
			  "return f(1)"
			  ), 2, 3, 4);
			TestCode(Lines(
			  "local f : LuaDLR.Test.Functions.GetByrefDelegate = clr.LuaDLR.Test.Functions.GetByref;",
			  "return f(0, 1)"
			  ), 1, 2, 3);
		}

		[TestMethod]
		public void TestFunctions09()
		{
			TestCode(Lines(
			  "local f : System.Func[int, int, int] = clr.LuaDLR.Test.Functions.SumInts2;",
			  "return f(), f(1), f(1, 2), f(1, 2, 3);"), 0, 1, 3, 3);
		}

		[TestMethod]
		public void TestFunctions10()
		{
			using (Lua l = new Lua())
			{
				l.PrintExpressionTree = Console.Out;
				var g = l.CreateEnvironment();
				var p = new TestParam();
				g.DoChunk("p.Test = 3;", "dummy", new KeyValuePair<string, object>("p", p));
				Assert.AreEqual(3, p.Test);
			}
		}

		[TestMethod]
		public void TestFunctions11()
		{
			TestCode(Lines(
				"local function add(a : int, b : int, c : int) : int",
				"  return a + b + c;",
				"end;",
				"return add(1, 2, 3), add(1, 2, arg3=3), add(1, arg3=3, arg2=2), add(1, arg3=3);"),
				6, 6, 6, 4);
			// the name of the argument is not "a" this is the name of the parameter, the argument name is "arg1"
		}

		[TestMethod]
		public void TestFunctions12()
		{
			TestCode(Lines(
				"local add : LuaDLR.Test.ParamIntDelegate = function (a : int, b : int, c : int) : int",
				"  return a + b + c;",
				"end;",
				"return add(1, 2, 3), add(1, 2, c=3), add(1, c=3, b=2), add(1, c=3);"),
				6, 6, 6, 4);
		}

		[TestMethod]
		public void TestFunctions50()
		{
			using (Lua l = new Lua())
			{
				l.PrintExpressionTree = Console.Out;
				var f = l.CreateLambda<Func<int>>("test", "local function test(a:int):int return 1 + a; end; return test(2);");
				TestResult(new LuaResult(f()), 3);
			}
		}

		[TestMethod]
		public void TestFunctions51()
		{
			TestCode("local function test(a) return 1 + a; end; return test(2);", 3);
		}

		[TestMethod]
		public void TestFunctions52()
		{
			TestCode("local test = function (a) return 1 + a; end; return test(2);", 3);
		}

		[TestMethod]
		public void TestFunctions53()
		{
			TestCode("local test = function () return 1, 2, 3; end; return (test());", 1);
		}

		[TestMethod]
		public void TestFunctions54()
		{
			TestCode("local test = function () return 1, 2, 3; end; return test();", 1, 2, 3);
		}

		[TestMethod]
		public void TestFunctions55()
		{
			TestCode("local test = function () return 1, 2, 3; end; return 'a', test();", "a", 1, 2, 3);
		}

		[TestMethod]
		public void TestFunctions56()
		{
			TestCode("local test = function () return 3, 2, 1; end; return 2 * test();", 6);
		}

		[TestMethod]
		public void TestFunctions57()
		{
			TestCode(GetLines("Lua.Function08.lua"), 1, 4);
			TestCode(GetLines("Lua.Function08a.lua"), 1, 4);
		}

		[TestMethod]
		public void TestFunctions58()
		{
			using (Lua l = new Lua())
			{
				l.PrintExpressionTree = Console.Out;
				dynamic g = l.CreateEnvironment();
				TestResult(g.dochunk("return p:ParamOut(1, 2);", "dummy", "p", new TestParam()), 1, 1, 2);
			}
		}

		[TestMethod]
		public void TestFunctions59()
		{
			using (Lua l = new Lua())
			{
				l.PrintExpressionTree = Console.Out;
				dynamic g = l.CreateEnvironment();
				TestResult(g.dochunk("local b = p:GetValue(); return b;", "dummy", "p", new TestParam()), 4);
			}
		}

		[TestMethod]
		public void TestFunctions60()
		{
			using (Lua l = new Lua())
			{
				l.PrintExpressionTree = Console.Out;
				dynamic g = l.CreateEnvironment();
				g.dochunk(Lines(
				  "function add(a : int, b : int) : int",
				  "  return a + b;",
				  "end;"), "add.lua");
				Func<int, int, int> a = g.add;
				Assert.IsTrue(a(1, 3) == 4);
				Assert.IsTrue(g.add(1, 2) == 3);
			}
		}

		[TestMethod]
		public void TestFunctions61()
		{
			using (Lua l = new Lua())
			{
				l.PrintExpressionTree = Console.Out;
				dynamic g = l.CreateEnvironment();
				TestResult(g.dochunk("function test(a) if a ~= nil then return a; end; end;", "test.lua"));
				TestResult(g.test());
				TestResult(g.test(1), 1);
			}
		}

		[TestMethod]
		public void TestFunctions62()
		{
			using (var l = new Lua())
			{
				l.PrintExpressionTree = Console.Out;
				dynamic g = l.CreateEnvironment();
				var c = l.CompileChunk("print(a)", "dummy", null, new KeyValuePair<string, Type>("a", typeof(object)));

				g.dochunk(c, 1);
				g.dochunk(c, "a");
			}
		}

		[TestMethod]
		public void TestFunctionConvert01()
		{
			var f = new TestDelegateConvert();
			using (var l = new Lua())
			{
				// todo: signatur must be compatible (by .net rules), there is not convert of delegate parameters, that is provided by RtConvertValue
				var g = l.CreateEnvironment();
				TestResult(g.DoChunk(
					Lines(
						"d.Dlg = function (a : int, b : int, c : int) : int return  a + b + c end;",
						//"d.Dlg = function (a, b, c) return  a + b + c end;",
						"return d.Dlg(1, 2, 20);"
					),
					"test.lua", new KeyValuePair<string, object>("d", f)
				), 23);
			}
		}

	} // class Functions
}
