using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.IronLua;

namespace LuaDLR.Test
{
	public enum TestEnum
	{
		Value1 = 1,
		Value2,
		Value3
	}

  [TestClass]
  public class Expressions : TestHelper
	{
		#region -- class DynTest ----------------------------------------------------------

		private class DynTest : DynamicObject
		{
			public override bool TryGetMember(GetMemberBinder binder, out object result)
			{
				if (binder.Name == "Value1")
				{
					result = 42;
					return true;
				}
				return base.TryGetMember(binder, out result); // => false
			}

			public override IEnumerable<string> GetDynamicMemberNames()
			{
				yield return "Value1";
			}

			public int Value3 => 43;
		}

		#endregion


		#region -- TestHelper --

		public struct TestOperator2
    {
      private int i;

      public TestOperator2(int i)
      {
        this.i = i;
      }

      public static implicit operator int(TestOperator2 a)
      {
        Console.WriteLine("  implicit int (TestOperator2)");
        return a.i;
      }

      public int Count { get { return i; } }
    }

    public class ComparableObject : IComparable
    {
      private int i;

      public ComparableObject(int i)
      {
        this.i = i;
      }

      public int CompareTo(object obj)
      {
        Console.WriteLine("CompareTo object");
        return ((IComparable)obj).CompareTo(obj);
      }
    }

    public class CompareTyped : IComparable<int>, IComparable<string>
    {
      private int i;

      public CompareTyped(int i)
      {
        this.i = i;
      }

      public int CompareTo(int other)
      {
        Console.WriteLine("CompareTo other");
        return i - other;
      }

      public int CompareTo(string other)
      {
        Console.WriteLine("CompareTo string");
        return i - int.Parse(other);
      }
    }

    public struct TestOperator
    {
      private int i;

      public TestOperator(int i)
      {
        this.i = i;
      }

      public override string ToString()
      {
        return i.ToString();
      }

      public int Length { get { return i; } }

      public static TestOperator operator -(TestOperator a)
      {
        Console.WriteLine("  operator- TestOperator");
        return new TestOperator(-a.i);
      }

      public static TestOperator operator ~(TestOperator a)
      {
        Console.WriteLine("  operator~ TestOperator");
        return new TestOperator(~a.i);
      }

      public static TestOperator operator +(TestOperator a, int b)
      {
        Console.WriteLine("  operator+ TestOperator,int");
        return new TestOperator(a.i + b);
      } // 

      public static TestOperator operator +(TestOperator a, TestOperator b)
      {
        Console.WriteLine("  operator+ TestOperator,TestOperator");
        return new TestOperator(a.i + b.i);
      } //

      public static TestOperator operator -(TestOperator a, int b)
      {
        Console.WriteLine("  operator- TestOperator,int");
        return new TestOperator(a.i - b);
      } //

      public static TestOperator operator *(TestOperator a, int b)
      {
        Console.WriteLine("  operator* TestOperator,int");
        return new TestOperator(a.i * b);
      } //

      public static TestOperator operator /(TestOperator a, int b)
      {
        Console.WriteLine("  operator/ TestOperator,int");
        return new TestOperator(a.i / b);
      } //

      public static TestOperator operator &(TestOperator a, int b)
      {
        Console.WriteLine("  operator& TestOperator,int");
        return new TestOperator(a.i & b);
      } //

      public static TestOperator operator |(TestOperator a, int b)
      {
        Console.WriteLine("  operator| TestOperator,int");
        return new TestOperator(a.i | b);
      } //

      public static TestOperator operator ^(TestOperator a, int b)
      {
        Console.WriteLine("  operator^ TestOperator,int");
        return new TestOperator(a.i ^  b);
      } //

      public static TestOperator operator >>(TestOperator a, int b)
      {
        Console.WriteLine("  operator>> TestOperator,int");
        return new TestOperator(a.i >> b);
      } //

      public static TestOperator operator <<(TestOperator a, int b)
      {
        Console.WriteLine("  operator<< TestOperator,int");
        return new TestOperator(a.i << b);
      } //

      public static bool operator >(TestOperator a, int b)
      {
        Console.WriteLine("  operator<< TestOperator,int");
        return a.i > b;
      } //

      public static bool operator >=(TestOperator a, int b)
      {
        Console.WriteLine("  operator>= TestOperator,int");
        return a.i >= b;
      } //

      public static bool operator <(TestOperator a, int b)
      {
        Console.WriteLine("  operator< TestOperator,int");
        return a.i < b;
      } //

      public static bool operator <=(TestOperator a, int b)
      {
        Console.WriteLine("  operator<= TestOperator,int");
        return a.i <= b;
      } //

      public static implicit operator int(TestOperator a)
      {
        Console.WriteLine("  implicit int (TestOperator)");
        return a.i;
      }

      public static explicit operator string(TestOperator a)
      {
        Console.WriteLine("  implicit string");
        return a.i.ToString();
      }

      public static implicit operator TestOperator(int a)
      {
        Console.WriteLine("  implicit TestOperator (int)");
        return new TestOperator(a);
      }
    }

    private enum IntEnum : int
    {
      Null= 0,Eins = 1, Zwei, Drei
    }
    private enum ShortEnum : short
    {
      Eins = 1, Zwei, Drei
    }

    public static int Return1()
    {
      return 1;
    }

    public static int Return2()
    {
      return 2;
    }

    public static LuaResult ReturnLua1()
    {
      return new LuaResult(1);
    }

    public static LuaResult ReturnLua2()
    {
      return new LuaResult(2);
    }

    public static LuaResult ReturnLua3()
    {
      return new LuaResult(3, 2, 1);
    }

    public static void ReturnVoid()
    {
      Console.WriteLine("ReturnVoid Called");
    }

    public class IndexAccess
    {
      public int this[int i] { get { return i; } }
    }

    #endregion

    #region -- Conversion -------------------------------------------------------------

    [TestMethod]
    public void TestConvert01()
    {
      TestExpr("cast(bool, 1)", true);
      TestExpr("cast(bool, 0)", true);
      TestExpr("clr.LuaDLR.Test.Expressions.ReturnVoid()");
      TestExpr("(clr.LuaDLR.Test.Expressions.ReturnVoid())", NullResult);
      TestExpr("clr.LuaDLR.Test.Expressions.ReturnLua3()", 3, 2, 1);
      TestExpr("(clr.LuaDLR.Test.Expressions.ReturnLua3())", 3);
    }

		[TestMethod]
		public void TestConvertStatic01()
		{
			TestResult(new LuaResult(Lua.RtConvertValue(1, typeof(bool))), true);
			TestResult(new LuaResult(Lua.RtConvertValue(0, typeof(bool))), true);
		}

    [TestMethod]
    public void TestConvert02()
    {
      using (Lua l = new Lua())
      {
				l.PrintExpressionTree = Console.Out;
        dynamic g = l.CreateEnvironment();
        var c = l.CompileChunk("return cast(bool, a)", "dummy", null, new KeyValuePair<string, Type>("a", typeof(object)));

        TestResult(g.dochunk(c, 1), true);
        TestResult(g.dochunk(c, ShortEnum.Eins), true);
        TestResult(g.dochunk(c, 0), true); // lua definition
        TestResult(g.dochunk(c, null), false);
        TestResult(g.dochunk(c, new object()), true);
      }
    }

		[TestMethod]
		public void TestConvertStatic02()
		{
			TestResult(new LuaResult(Lua.RtConvertValue(1, typeof(bool))), true);
			TestResult(new LuaResult(Lua.RtConvertValue(ShortEnum.Eins, typeof(bool))), true);
			TestResult(new LuaResult(Lua.RtConvertValue(0, typeof(bool))), true);
			TestResult(new LuaResult(Lua.RtConvertValue(null, typeof(bool))), false);
			TestResult(new LuaResult(Lua.RtConvertValue(new object(), typeof(bool))), true);
		}

		[TestMethod]
    public void TestConvert03()
    {
      TestExpr("cast(string, 'a')", "a");
      TestExpr("cast(string, 1)", "1");
      TestExpr("cast(string, 0)", "0");
      TestExpr("cast(string, cast(short, 0))", "0");
    }

		[TestMethod]
		public void TestConvertStatic03()
		{
			TestResult(new LuaResult(Lua.RtConvertValue("a", typeof(string))), "a");
			TestResult(new LuaResult(Lua.RtConvertValue('a', typeof(string))), "a");
			TestResult(new LuaResult(Lua.RtConvertValue(1, typeof(string))), "1");
			TestResult(new LuaResult(Lua.RtConvertValue(0, typeof(string))), "0");
			TestResult(new LuaResult(Lua.RtConvertValue((short)0, typeof(string))), "0");

			TestResult(new LuaResult(Lua.RtConvertValue(ShortEnum.Eins, typeof(string))), "Eins");
			TestResult(new LuaResult(Lua.RtConvertValue(null, typeof(string))), "");
			TestResult(new LuaResult(Lua.RtConvertValue(new object(), typeof(string))), "System.Object");

			TestResult(new LuaResult(Lua.RtConvertValue(1, typeof(Decimal))), 1m);
			TestResult(new LuaResult(Lua.RtConvertValue("1.2", typeof(Decimal))), 1.2m);
			TestResult(new LuaResult(Lua.RtConvertValue(1.2m, typeof(string))), "1.2");
			TestResult(new LuaResult(Lua.RtConvertValue(1.2m, typeof(int))), 1);

			TestResult(new LuaResult(Lua.RtConvertValue("90238fad-cb41-4efa-bb2f-4d56a0088a01", typeof(Guid))), new Guid("90238fad-cb41-4efa-bb2f-4d56a0088a01"));
		}

		[TestMethod]
    public void TestConvert04()
    {
      using (Lua l = new Lua())
      {
				l.PrintExpressionTree = Console.Out;
        dynamic g = l.CreateEnvironment();
        var c = l.CompileChunk("return cast(string, a)", "dummy", null, new KeyValuePair<string, Type>("a", typeof(object)));

        TestResult(g.dochunk(c, 'a'), "a");
        TestResult(g.dochunk(c, 1), "1");
        TestResult(g.dochunk(c, ShortEnum.Eins), "Eins");
        TestResult(g.dochunk(c, null), "");
        TestResult(g.dochunk(c, new object()), "System.Object");
				TestResult(g.dochunk(c, 1.2m), "1.2");
			}
		}

		[TestMethod]
    public void TestConvert05()
    {
      TestExpr("cast(int, '1')", 1);
      TestExpr("cast(short, '0')", (short)0);
      TestExpr("cast(int, '1.2')", 1);
      TestExpr("cast(int, nil)", 0);
      TestExpr("cast(System.String, nil)", String.Empty);
			TestExpr("cast(System.Environment, nil)", NullResult);
			TestExpr("cast(System.Decimal, '1.2')", 1.2m);

		}

		[TestMethod]
		public void TestConvertStatic05()
		{
			TestResult(new LuaResult(Lua.RtConvertValue("1", typeof(int))), 1);
			TestResult(new LuaResult(Lua.RtConvertValue(0, typeof(short))), (short)0);
			TestResult(new LuaResult(Lua.RtConvertValue("1.2", typeof(int))), 1);
			TestResult(new LuaResult(Lua.RtConvertValue(null, typeof(int))), 0);
			TestResult(new LuaResult(Lua.RtConvertValue(null, typeof(string))), String.Empty);
			TestResult(new LuaResult(Lua.RtConvertValue(null, typeof(Environment))), NullResult);
		}

		[TestMethod]
    public void TestConvert06()
    {
      using (Lua l = new Lua())
      {
				l.PrintExpressionTree = Console.Out;
        dynamic g = l.CreateEnvironment();
        var c = l.CompileChunk("return cast(int, a)", "dummy", null, new KeyValuePair<string, Type>("a", typeof(object)));

        TestResult(g.dochunk(c, "1"), 1);
        TestResult(g.dochunk(c, ShortEnum.Eins), 1);
        TestResult(g.dochunk(c, '1'), 49);
        TestResult(g.dochunk(c, "1.2"), 1);
        TestResult(g.dochunk(c, null), 0);
				TestResult(g.dochunk(c, 1.2m), 1);
			}
    }

		[TestMethod]
		public void TestConvertStatic06()
		{
			TestResult(new LuaResult(Lua.RtConvertValue("1", typeof(int))), 1);
			TestResult(new LuaResult(Lua.RtConvertValue(ShortEnum.Eins, typeof(int))), 1);
			TestResult(new LuaResult(Lua.RtConvertValue('1', typeof(int))), 49);
			TestResult(new LuaResult(Lua.RtConvertValue("1.2", typeof(int))), 1);
			TestResult(new LuaResult(Lua.RtConvertValue(null, typeof(int))), 0);

			TestResult(new LuaResult(Lua.RtConvertValue("Eins", typeof(ShortEnum))), ShortEnum.Eins);
			TestResult(new LuaResult(Lua.RtConvertValue(1, typeof(ShortEnum))), ShortEnum.Eins);
			TestResult(new LuaResult(Lua.RtConvertValue(ShortEnum.Zwei, typeof(int))), 2);
			TestResult(new LuaResult(Lua.RtConvertValue(ShortEnum.Zwei, typeof(string))), "Zwei");
		}

		[TestMethod]
    public void TestConvert07()
    {
      TestExpr("cast(int, clr.LuaDLR.Test.Expressions.ReturnLua1())", 1);
      TestExpr("cast(short, clr.LuaDLR.Test.Expressions.ReturnLua2())", (short)2);
			TestExpr("cast(LuaDLR.Test.Expressions.ShortEnum, 'Eins')", ShortEnum.Eins);
			TestExpr("cast(LuaDLR.Test.Expressions.ShortEnum, 1)", ShortEnum.Eins);
		}

    [TestMethod]
    public void TestConvert08()
    {
      using (Lua l = new Lua())
      {
				l.PrintExpressionTree = Console.Out;
        dynamic g = l.CreateEnvironment();
        var c = l.CompileChunk("return cast(int, a)", "dummy", null, new KeyValuePair<string, Type>("a", typeof(object)));

        TestResult(g.dochunk(c, new LuaResult(2)), 2);
        TestResult(g.dochunk(c, new LuaResult((short)2)), 2);
        TestResult(g.dochunk(c, new LuaResult(ShortEnum.Zwei)), 2);
      }
    }

		#endregion

		#region -- Arithmetic -------------------------------------------------------------

		[TestMethod]
    public void TestOperator01()
    {
      int a = 1 + 2;
      Assert.IsTrue(a == 3);
      Console.WriteLine("Test 1 (int a = new TestOperator(2) + 1):");
      a = new TestOperator(2) + 1;
      Assert.IsTrue(a == 3);

      Console.WriteLine("Test 2 (TestOperator b = 1 + 2):");
      TestOperator b = 1 + 2;
      Assert.IsTrue(a == 3);

      Console.WriteLine("Test 3 (TestOperator c = 2 + new TestOperator(1)):");
      TestOperator c = 2 + new TestOperator(1);
      Assert.IsTrue(c == 3);

      Console.WriteLine("Test 4 (int c = 2 + new TestOperator(1)):");
      int d = 2 + new TestOperator(1);
      Assert.IsTrue(d == 3);

      Console.WriteLine("Test 5 (int c = new TestOperator(1) + 2):");
      int e = new TestOperator(1) + 2;
      Assert.IsTrue(e == 3);

      Console.WriteLine("Test 6 (int c = new TestOperator(1) + 2):");
      int f = (byte)1 + new TestOperator(2);
      Assert.IsTrue(f == 3);

      Console.WriteLine("Methods:");
      Type t = typeof(TestOperator);
      foreach (MethodInfo mi in t.GetMethods(BindingFlags.Public | BindingFlags.InvokeMethod | BindingFlags.Static | BindingFlags.DeclaredOnly))
        Console.WriteLine(mi.Name);
    } // proc TestOperator01

    [TestMethod]
    public void TestArithmetic01() { TestExpr("1 + 2", 3); }

    [TestMethod]
    public void TestArithmetic02() { TestExpr("1 + 2.0", 3.0); }

    [TestMethod]
    public void TestArithmetic03() { TestExpr("1 + '2'", 3); }

    [TestMethod]
    public void TestArithmetic04() { TestExpr("1 + '2.0'", 3.0); }

    [TestMethod]
    public void TestArithmetic05()
    {
      using (Lua l = new Lua())
      {
				l.PrintExpressionTree = Console.Out;
        var g = l.CreateEnvironment();
        var c = l.CompileChunk("return 1 + a;", "test.lua", null, new KeyValuePair<string, Type>("a", typeof(string)));

        TestResult(g.DoChunk(c, "2"), 3);
        TestResult(g.DoChunk(c, "2.0"), 3.0);
      }
    } 

    [TestMethod]
    public void TestArithmetic06()
    {
      using (Lua l = new Lua())
      {
				l.PrintExpressionTree = Console.Out;
        var g = l.CreateEnvironment();
        var c = l.CompileChunk("return 1 + a;", "test.lua", null, new KeyValuePair<string, Type>("a", typeof(object)));

        TestResult(g.DoChunk(c, 2), 3);
        TestResult(g.DoChunk(c, 2.0), 3.0);
        TestResult(g.DoChunk(c, "2"), 3);
        TestResult(g.DoChunk(c, "2.0"), 3.0);
        TestResult(g.DoChunk(c, 2.0f), 3.0f);
      }
    }

    [TestMethod]
    public void TestArithmetic07()
    {
      try
      {
        TestExpr("1 + nil", null);
      }
			catch (LuaRuntimeException e)
      {
        Assert.IsTrue(e.Message.IndexOf("Object") >= 0);
				return;
      }
			Assert.Fail();
    }

    [TestMethod]
    public void TestArithmetic08()
    {
      try
      {
        TestExpr("1 + clr.LuaDLR.Test.Expressions.ReturnVoid()", NullResult);
      }
			catch (LuaRuntimeException e)
      {
        Assert.IsTrue(e.Message.IndexOf("Object") >= 0);
				return;
      }
			Assert.Fail();
    }

    [TestMethod]
    public void TestArithmetic09()
    {
      using (Lua l = new Lua())
      {
				l.PrintExpressionTree = Console.Out;
        var g = l.CreateEnvironment();
        var c1 = l.CompileChunk("return a + b;", "test.lua", null, new KeyValuePair<string, Type>("a", typeof(int)), new KeyValuePair<string, Type>("b", typeof(TestOperator)));
        var c2 = l.CompileChunk("return a + b;", "test.lua", null, new KeyValuePair<string, Type>("a", typeof(object)), new KeyValuePair<string, Type>("b", typeof(object)));

				TestResult(g.DoChunk(c1, 1, new TestOperator(2)), new TestOperator(3));

				TestResult(g.DoChunk(c2, 1, new TestOperator(2)), new TestOperator(3));
				TestResult(g.DoChunk(c2, new TestOperator(2), 1), new TestOperator(3));
				TestResult(g.DoChunk(c2, new TestOperator(2), new TestOperator(1)), new TestOperator(3));
				TestResult(g.DoChunk(c2, new TestOperator(2), (short)1), new TestOperator(3));
				TestResult(g.DoChunk(c2, new TestOperator2(2), 1L), 3L);
        TestResult(g.DoChunk(c2, 2, new TestOperator2(1)), 3);
      }
    }

    [TestMethod]
    public void TestArithmetic10()
    {
      using (Lua l = new Lua())
      {
				l.PrintExpressionTree = Console.Out;
        var g = l.CreateEnvironment();
        var c = l.CompileChunk("return a + b;", "test.lua", null, new KeyValuePair<string, Type>("a", typeof(object)), new KeyValuePair<string, Type>("b", typeof(object)));

        TestResult(g.DoChunk(c, ShortEnum.Eins, ShortEnum.Zwei), ShortEnum.Drei);
        TestResult(g.DoChunk(c, IntEnum.Eins, IntEnum.Zwei), IntEnum.Drei);
        TestResult(g.DoChunk(c, (short)1, IntEnum.Zwei), 3);
        TestResult(g.DoChunk(c, ShortEnum.Eins, 2), 3);
      }
    }

    [TestMethod]
    public void TestArithmetic11()
    {
      using (Lua l = new Lua())
      {
				l.PrintExpressionTree = Console.Out;
        var g = l.CreateEnvironment();
        var c1 = l.CompileChunk("return a + b;", "test.lua", null, new KeyValuePair<string, Type>("a", typeof(Nullable<int>)), new KeyValuePair<string, Type>("b", typeof(Nullable<int>)));
        var c2 = l.CompileChunk("return a + b;", "test.lua", null, new KeyValuePair<string, Type>("a", typeof(object)), new KeyValuePair<string, Type>("b", typeof(object)));

        TestResult(g.DoChunk(c1, 1, 2), 3);
        TestResult(g.DoChunk(c1, null, 2), NullResult);

        TestResult(g.DoChunk(c2, new Nullable<short>(1), new Nullable<short>(2)), (short)3);
        TestResult(g.DoChunk(c2, new Nullable<int>(1), new Nullable<int>(2)), 3);
        TestResult(g.DoChunk(c2, new Nullable<int>(1), new Nullable<short>(2)), 3);
        TestResult(g.DoChunk(c2, new Nullable<short>(1), (short)2), (short)3);
        TestResult(g.DoChunk(c2, new Nullable<int>(1), 2), 3);
        TestResult(g.DoChunk(c2, new Nullable<int>(1), (short)2), 3);
      }
    }

    [TestMethod]
    public void TestArithmetic12()
    {
      using (Lua l = new Lua())
      {
				l.PrintExpressionTree = Console.Out;
        var g = l.CreateEnvironment();
        TestResult(g.DoChunk("return clr.LuaDLR.Test.Expressions.Return1() + clr.LuaDLR.Test.Expressions.Return2();", "test.lua"), 3);
        Console.WriteLine();
        TestResult(g.DoChunk("return clr.LuaDLR.Test.Expressions.ReturnLua1() + clr.LuaDLR.Test.Expressions.ReturnLua2();", "test.lua"), 3);
        Console.WriteLine();
        TestResult(g.DoChunk("return clr.LuaDLR.Test.Expressions.Return1() + clr.LuaDLR.Test.Expressions.ReturnLua2();", "test.lua"), 3);
        Console.WriteLine();
        var c2 = l.CompileChunk("return a() + b();", "test.lua", null, new KeyValuePair<string, Type>("a", typeof(object)), new KeyValuePair<string, Type>("b", typeof(object)));
        TestResult(g.DoChunk(c2, new Func<int>(Return1), new Func<int>(Return2)), 3);
        TestResult(g.DoChunk(c2, new Func<LuaResult>(ReturnLua1), new Func<LuaResult>(ReturnLua2)), 3);
        TestResult(g.DoChunk(c2, new Func<int>(Return1), new Func<LuaResult>(ReturnLua2)), 3);
      }
    }

    [TestMethod]
    public void TestArithmetic13() { TestExpr("2 ^ 3", 8.0); }

    [TestMethod]
    public void TestArithmetic14()
    {
      TestExpr("2 - 3", -1);
      TestExpr("2 * 3", 6);
      TestExpr("15 / 3", 5.0);
      TestExpr("15 // 3", 5);
      TestExpr("5 / 2", 2.5);
      TestExpr("5 // 2", 2);
      TestExpr("5.2 // 2", (long)2);
      TestExpr("5 % 2", 1);
      TestExpr("5.2 % 2", 1.2);
      TestExpr("2 ^ 0.5", 1.414);

      TestExpr("3 & 2", 2);
      TestExpr("2 | 1", 3);
      TestExpr("3 ~ 2", 1);
      TestExpr("1 << 8", 256);
      TestExpr("256 >> 8", 1);
      TestExpr("3.2 ~ 2", (long)1);

      TestExpr("clr.LuaDLR.Test.Expressions.TestOperator(2) + 3", new TestOperator(5));
      TestExpr("clr.LuaDLR.Test.Expressions.TestOperator(2) - 3", new TestOperator(-1));
      TestExpr("clr.LuaDLR.Test.Expressions.TestOperator(2) * 3", new TestOperator(6));
      TestExpr("clr.LuaDLR.Test.Expressions.TestOperator(6) / 3", new TestOperator(2));
      TestExpr("clr.LuaDLR.Test.Expressions.TestOperator(6) // 3", new TestOperator(2));
      TestExpr("clr.LuaDLR.Test.Expressions.TestOperator(3) & 2", new TestOperator(2));
      TestExpr("clr.LuaDLR.Test.Expressions.TestOperator(2) | 1", new TestOperator(3));
      TestExpr("clr.LuaDLR.Test.Expressions.TestOperator(3) ~ 2", new TestOperator(1));
      TestExpr("clr.LuaDLR.Test.Expressions.TestOperator(1) << 8", new TestOperator(256));
      TestExpr("clr.LuaDLR.Test.Expressions.TestOperator(256) >> 8", new TestOperator(1));
    }

    [TestMethod]
    public void TestArithmetic15()
    {
      using (Lua l = new Lua())
      {
				l.PrintExpressionTree = Console.Out;

        dynamic g = l.CreateEnvironment();
        TestResult(g.dochunk("return a // b", "dummy", "a", 15, "b", 3), 5);
        TestResult(g.dochunk("return a // b", "dummy", "a", 5.2, "b", 2), (long)2);

        TestResult(g.dochunk("return a & b", "dummy", "a", 3.0, "b", 2), (long)2);
        TestResult(g.dochunk("return a | b", "dummy", "a", 2.0, "b", 3), (long)3);
        TestResult(g.dochunk("return a ~ b", "dummy", "a", 3.0, "b", 2), (long)1);
				TestResult(g.dochunk("return a << b", "dummy", "a", 1.0, "b", 8), (long)256);
        TestResult(g.dochunk("return a >> b", "dummy", "a", 256.0, "b", 8), (long)1);

        LuaChunk c1 = l.CompileChunk("return a ~ b", "dummy", null, new KeyValuePair<string, Type>("a", typeof(object)), new KeyValuePair<string, Type>("b", typeof(int)));
				TestResult(g.dochunk(c1, 3.2, 2), (long)1);
        TestResult(g.dochunk(c1, "3.2", 2), (long)1);
        TestResult(g.dochunk(c1, ShortEnum.Drei, 2), 1);
        TestResult(g.dochunk(c1, new Nullable<short>(3), 2), 1);
        LuaChunk c2 = l.CompileChunk("return a() ~ b", "dummy", null, new KeyValuePair<string, Type>("a", typeof(object)), new KeyValuePair<string, Type>("b", typeof(int)));
				TestResult(g.dochunk(c2, new Func<LuaResult>(() => new LuaResult(3.2)), 2), (long)1);
        TestResult(g.dochunk(c2, new Func<LuaResult>(() => new LuaResult(new TestOperator(3))), 2), new TestOperator(1));
        TestResult(g.dochunk(c2, new Func<LuaResult>(() => new LuaResult(new Nullable<float>(3.2f))), 2), 1);
      }
    }

    [TestMethod]
    public void TestArithmetic16()
    {
      TestExpr("1 + 2 * 3", 7);
      TestExpr("1 + 2.2 * 3", 7.6);
      TestExpr("1 + (3 & 2) * 3", 7);
      TestExpr("1 + (2 | 1) * 3", 10);
      TestExpr("1 + 2 * 3 & 3", 3);
    }

    [TestMethod]
    public void TestArithmetic17()
    {
      TestExpr("-2", -2);
			TestExpr("-2.1", -2.1);
      TestExpr("-'2.1'", -2.1);
      TestExpr("~2", ~2);
			TestExpr("~2.1", (long)~2);
      TestExpr("~'2'", ~2);
			TestExpr("~'2.1'", (long)~2);
    }

    [TestMethod]
    public void TestArithmetic18()
    {
      using (Lua l = new Lua())
      {
				l.PrintExpressionTree = Console.Out;

        dynamic g = l.CreateEnvironment();
        TestResult(g.dochunk("return -a", "dummy", "a", 2), -2);
        TestResult(g.dochunk("return -a", "dummy", "a", 2.1), -2.1);
        TestResult(g.dochunk("return -a", "dummy", "a", new TestOperator(2)), new TestOperator(-2));
        TestResult(g.dochunk("return -a", "dummy", "a", new TestOperator2(2)), -2);
        TestResult(g.dochunk("return -a", "dummy", "a", ShortEnum.Zwei), (ShortEnum)(-2));

        TestResult(g.dochunk("return ~a", "dummy", "a", 2), ~2);
        TestResult(g.dochunk("return ~a", "dummy", "a", 2.1), (long)~2);
        TestResult(g.dochunk("return ~a", "dummy", "a", new TestOperator(2)), new TestOperator(~2));
        TestResult(g.dochunk("return ~a", "dummy", "a", new TestOperator2(2)), ~2);
				TestResult(g.dochunk("return ~a", "dummy", "a", ShortEnum.Zwei), (ShortEnum)~2);
      }
    }

    [TestMethod]
    public void TestArithmetic19()
    {
      TestExpr("1 + -2.2 * 3", -5.6);
      TestExpr("-2 * '2'", -4);
      TestExpr("2 * -'2'", -4);
      TestExpr("-2 * '2.0'", -4.0);
      TestExpr("1 + (1 + 2) * 3", 10);
    }

    [TestMethod]
    public void TestArithmetic20()
    {
      try
      {
        TestExpr("-clr.LuaDLR.Test.Expressions.ReturnVoid()", null);
      }
      catch (LuaRuntimeException e)
      {
        Assert.IsTrue(e.Message.IndexOf("nil") >= 0);
				return;
      }
			Assert.Fail();
    }

    [TestMethod]
    public void TestArithmetic21()
    {
      TestExpr("true", true);
      TestExpr("false", false);
      TestExpr("nil", NullResult);
    }

    [TestMethod]
    public void TestArithmetic22()
    {
      TestExpr("clr.LuaDLR.Test.Expressions.ReturnLua2() + 1", 3);
    }

    [TestMethod]
    public void TestArithmetic23()
    {
      TestCode("return -0x80", (int)-0x80);
      TestCode("return -0x8000", (int)-0x8000);
      TestCode("return -0x80000000", (long)-0x80000000);
    }

    #endregion

    #region -- Const ------------------------------------------------------------------

    [TestMethod]
    public void TestConst01()
    {
      using (Lua l = new Lua())
      {
				l.PrintExpressionTree = Console.Out;
        var g = l.CreateEnvironment();

        TestResult(g.DoChunk("const a = 20; return a;", "dummy"), 20);
        TestResult(g.DoChunk("const a = cast(ushort, 20); return a;", "dummy"), (ushort)20);
        TestResult(g.DoChunk("const a = cast(int, '20'); return a;", "dummy"), 20);
      }
    }

    #endregion

    #region -- Logic ------------------------------------------------------------------

    [TestMethod]
    public void TestLogic01() { TestExpr("10 or 20", 10); }

    [TestMethod]
    public void TestLogic02() { TestExpr("10 or false", 10); }

    [TestMethod]
    public void TestLogic03() { TestExpr("nil or 'a'", "a"); }

    [TestMethod]
    public void TestLogic04() { TestExpr("false or nil", NullResult); }

    [TestMethod]
    public void TestLogic05() { TestExpr("nil and 10", NullResult); }

    [TestMethod]
    public void TestLogic06() { TestExpr("false and false", false); }

    [TestMethod]
    public void TestLogic07() { TestExpr("false and nil", false); }

    [TestMethod]
    public void TestLogic08() { TestExpr("10 and 20", 20); }

    [TestMethod]
    public void TestLogic09()
    {
      TestExpr("clr.LuaDLR.Test.Expressions.TestOperator(10) and 20", 20);
      TestExpr("clr.LuaDLR.Test.Expressions.TestOperator(10) or 20", new LuaResult(new TestOperator(10)));
      TestExpr("clr.LuaDLR.Test.Expressions.ReturnLua1() or 20", new LuaResult(1));
    }

    [TestMethod]
    public void TestLogic10() 
    {
      using (Lua l = new Lua())
      {
				l.PrintExpressionTree = Console.Out;
        var g = l.CreateEnvironment();
        var c = l.CompileChunk("if a then return true else return false end", "dummy", null, new KeyValuePair<string, Type>("a", typeof(object)));

        TestResult(g.DoChunk(c, 1), true);
        TestResult(g.DoChunk(c, 0), true);
        TestResult(g.DoChunk(c, ShortEnum.Drei), true);
        TestResult(g.DoChunk(c, IntEnum.Null), true);
        TestResult(g.DoChunk(c, new object[] { null }), false);
      }
    }

    [TestMethod]
    public void TestLogic11()
    {
      using (Lua l = new Lua())
      {
				l.PrintExpressionTree = Console.Out;
        var g = l.CreateEnvironment();
        var c = l.CompileChunk("return a or b", "dummy", null, new KeyValuePair<string, Type>("a", typeof(object)), new KeyValuePair<string, Type>("b", typeof(object)));

        TestResult(g.DoChunk(c, 10, 20), 10);
        TestResult(g.DoChunk(c, (short)10, (short)20), (short)10);
        TestResult(g.DoChunk(c, 10, (short)20), 10);
        TestResult(g.DoChunk(c, 10, false), 10);
        TestResult(g.DoChunk(c, null, "a"), "a");
        TestResult(g.DoChunk(c, false, null), NullResult);
        TestResult(g.DoChunk(c, new TestOperator(10), 20), new TestOperator(10));
      }
    }

    [TestMethod]
    public void TestLogic12()
    {
      using (Lua l = new Lua())
      {
				l.PrintExpressionTree = Console.Out;
        var g = l.CreateEnvironment();
        var c = l.CompileChunk("return a and b", "dummy", null, new KeyValuePair<string, Type>("a", typeof(object)), new KeyValuePair<string, Type>("b", typeof(object)));

        TestResult(g.DoChunk(c, null, 10), NullResult);
        TestResult(g.DoChunk(c, false, false), false);
        TestResult(g.DoChunk(c, false, null), false);
        TestResult(g.DoChunk(c, 10, 20), 20);
        TestResult(g.DoChunk(c, new TestOperator(10), 20), 20);
      }
    }

    [TestMethod]
    public void TestLogic13()
    {
      TestExpr("not true", false);
      TestExpr("not false", true);
      TestExpr("not 1", false);
      TestExpr("not 0", false);
      TestExpr("not nil", true);
    }

    [TestMethod]
    public void TestLogic14()
    {
      using (Lua l = new Lua())
      {
				l.PrintExpressionTree = Console.Out;
        var g = l.CreateEnvironment();
        var c = l.CompileChunk("return not a", "dummy", null, new KeyValuePair<string, Type>("a", typeof(object)));

        TestResult(g.DoChunk(c, 1), false);
        TestResult(g.DoChunk(c, 0), false);
        TestResult(g.DoChunk(c, ShortEnum.Drei), false);
        TestResult(g.DoChunk(c, IntEnum.Null), false);
        TestResult(g.DoChunk(c, new LuaResult(0)), false);
        TestResult(g.DoChunk(c, new object[] { null }), true);
      }
    }

		[TestMethod]
		public void TestLogic15()
		{
			TestCode(Lines(
				"local t : table = {}",
				"if t then return 1 else return 0 end"),
				1);
		}
		
		[TestMethod]
		public void TestLogic16() { TestCode("return not 0", false); }

		[TestMethod]
		public void TestLogic17() { TestCode("return not 1", false); }

		[TestMethod]
		public void TestLogic18() { TestCode("return not nil", true); }

		[TestMethod]
		public void TestLogic19() { TestExpr("true and 10 or 20", 10); }

		[TestMethod]
		public void TestLogic20()
		{
			TestExpr("false and 10 or 20", 20);
			TestExpr("false and '10' or '20'", "20");
		}

		[TestMethod]
		public void TestLogic21()
		{
			TestExpr("511 == '511'", false);
			try
			{
				TestExpr("511 < '512'", true); // should: input:1: attempt to compare number with string
				Assert.Fail();
			}
			catch
			{
			}
		}

		#endregion

		#region -- Compare ----------------------------------------------------------------

		[TestMethod]
    public void TestCompare01() { TestExpr("1 < 2", true); }

    [TestMethod]
    public void TestCompare02() { TestExpr("1 > 2", false); }

    [TestMethod]
    public void TestCompare03()
    {
      using (Lua l = new Lua())
      {
				l.PrintExpressionTree = Console.Out;
        dynamic g = l.CreateEnvironment();
        var c = l.CompileChunk("return a < b", "dummy", null,
          new KeyValuePair<string, Type>("a", typeof(object)),
          new KeyValuePair<string, Type>("b", typeof(object))
        );

        TestResult(g.dochunk(c, 1, 2), true);
        TestResult(g.dochunk(c, 2, 1), false);
        TestResult(g.dochunk(c, 2, 2), false);
        TestResult(g.dochunk(c, new TestOperator(1), 2), true);
        TestResult(g.dochunk(c, 1, new TestOperator(2)), true);
      }
    }

    [TestMethod]
    public void TestCompare04() { TestExpr("1 <= 2", true); }

    [TestMethod]
    public void TestCompare05() { TestExpr("1 >= 2", false); }

    [TestMethod]
    public void TestCompare06() { TestExpr("1 == 2", false); }

    [TestMethod]
    public void TestCompare07() { TestExpr("1 ~= 2", true); }

    [TestMethod]
    public void TestCompare08()
    {
      using (Lua l = new Lua())
      {
				l.PrintExpressionTree = Console.Out;
        dynamic g = l.CreateEnvironment();
        var c = l.CompileChunk("return a == b", "dummy", null,
          new KeyValuePair<string, Type>("a", typeof(object)),
          new KeyValuePair<string, Type>("b", typeof(object))
        );

				TestResult(g.dochunk(c, 1, 2), false);
				TestResult(g.dochunk(c, 2, 1), false);
				TestResult(g.dochunk(c, 2, 2), true);
				TestResult(g.dochunk(c, 2, (short)2), true);
				TestResult(g.dochunk(c, new TestOperator(1), 2), false);
				TestResult(g.dochunk(c, 2, new TestOperator(2)), false);
				object a = new object();
				TestResult(g.dochunk(c, a, a), true);
				TestResult(g.dochunk(c, "a", "a"), true);
				TestResult(g.dochunk(c, 3.0m, null), false);
				TestResult(g.dochunk(c, null, 3.0m), false);
				TestResult(g.dochunk(c, null, null), true);
			}
    }

    [TestMethod]
    public void TestCompare09()
    {
      using (Lua l = new Lua())
      {
				l.PrintExpressionTree = Console.Out;
        dynamic g = l.CreateEnvironment();
        var c = l.CompileChunk("return a ~= b", "dummy", null,
          new KeyValuePair<string, Type>("a", typeof(object)),
          new KeyValuePair<string, Type>("b", typeof(object))
        );

        TestResult(g.dochunk(c, 1, 2), true);
        TestResult(g.dochunk(c, 2, 1), true);
        TestResult(g.dochunk(c, 2, 2), false);
        TestResult(g.dochunk(c, 2, (short)2), false);
        TestResult(g.dochunk(c, new TestOperator(1), 2), true);
        TestResult(g.dochunk(c, 2, new TestOperator(2)), true);
        object a = new object();
        TestResult(g.dochunk(c, "a", "a"), false);
      }
    }

    [TestMethod]
    public void TestCompare10()
    {
      TestCode(String.Join(Environment.NewLine,
        "local a : string = '2';",
        "return a == 2;"
        ), true);
    }

		[TestMethod]
		public void TestCompare11()
		{
			TestCode(Lines(
				"local a = clr.LuaDLR.Test.TestEnum.Value1;",
				"if a == clr.LuaDLR.Test.TestEnum.Value1 then",
				"  return 42;",
				"else",
				"  return 0;",
				"end;"), 42);
		}

		[TestMethod]
		public void TestCompare12()
		{
			TestCode(Lines(
				"local a = clr.LuaDLR.Test.TestEnum.Value1;",
				"if a == 1 then",
				"  return 42;",
				"else",
				"  return 0;",
				"end;"), 42);
		}

		[TestMethod]
		public void TestCompare13()
		{
			TestCode(Lines(
				"local a : LuaDLR.Test.TestEnum = 1;",
				"if a == clr.LuaDLR.Test.TestEnum.Value1 then",
				"  return 42;",
				"else",
				"  return 0;",
				"end;"), 42);
		}

		[TestMethod]
		public void TestCompare14()
		{
			TestCode(Lines(
				"local a = clr.LuaDLR.Test.TestEnum.Value1;",
				"if a ~= clr.LuaDLR.Test.TestEnum.Value1 then",
				"  return 0;",
				"else",
				"  return 42;",
				"end;"), 42);
		}

		[TestMethod]
		public void TestCompare15()
		{
			TestCode(Lines(
				"local a = clr.LuaDLR.Test.TestEnum.Value1;",
				"if a ~= 1 then",
				"  return 0;",
				"else",
				"  return 42;",
				"end;"), 42);
		}

		[TestMethod]
		public void TestCompare16()
		{
			TestCode(Lines(
				"local a : LuaDLR.Test.TestEnum = 1;",
				"if a ~= clr.LuaDLR.Test.TestEnum.Value1 then",
				"  return 0;",
				"else",
				"  return 42;",
				"end;"), 42);
		}

		[TestMethod]
		public void TestCompare17()
		{
			TestCode(Lines(
				"local a = '_test';",
				"if a[0] == '_' then return 42 else return -1 end;"
			), 42);
		}

		#endregion

		#region -- Concat -----------------------------------------------------------------

		[TestMethod]
    public void TestConcat01() { TestExpr("'a' .. 'b' .. 'c'", "abc"); }

    [TestMethod]
    public void TestConcat02() { TestExpr("'a' .. 1 .. 'c'", "a1c"); }

    [TestMethod]
    public void TestConcat03() { TestExpr("'a' .. clr.LuaDLR.Test.Expressions.TestOperator(1) .. 'c'", "a1c"); }

    [TestMethod]
    public void TestConcat04() { TestExpr("'a' .. clr.LuaDLR.Test.Expressions.ReturnVoid() .. 'c'", "ac"); }

    [TestMethod]
    public void TestConcat05() { TestExpr("'a' .. nil .. 'c'", "ac"); }

    #endregion

    #region -- ArrayLength ------------------------------------------------------------

    [TestMethod]
    public void TestArrayLength01()
    {
      TestExpr("#'abc'", 3);
    }

    [TestMethod]
    public void TestArrayLength02()
    {
      using (Lua l = new Lua())
      {
				l.PrintExpressionTree = Console.Out;
        dynamic g = l.CreateEnvironment();
        var c = l.CompileChunk("return #a;", "dummy", null, new KeyValuePair<string, Type>("a", typeof(object)));

        TestResult(g.dochunk(c, "abc"), 3);
        TestResult(g.dochunk(c, new TestOperator(3)), 3);
        TestResult(g.dochunk(c, new TestOperator2(3)), 3);
      }
    }

    #endregion

    #region -- Table ------------------------------------------------------------------

    [TestMethod]
    public void TestTable01() 
    {
      TestCode("return {}", Table());
    }

    [TestMethod]
    public void TestTable02()
    {
      TestCode("return {1, 2, 3; 4}",
        Table(
          TV(1, 1),
          TV(2, 2),
          TV(3, 3),
          TV(4, 4)
        )
      );
    }

    [TestMethod]
    public void TestTable03()
    {
      TestCode("return {a = 1, b = 2, c = 3;  d = 4}",
        Table(
          TV("a", 1),
          TV("b", 2),
          TV("c", 3),
          TV("d", 4)
        )
      );
    }

    [TestMethod]
    public void TestTable04()
    {
      TestCode("return {['a'] = 1}", Table(TV("a", 1)));
    }

    [TestMethod]
    public void TestTable05()
    {
      string sCode = Lines(
        "function f(a)",
        "  return a;",
        "end;",
        "local g = 32;",
        "local x = 24;",
        "a = { [f('z')] = g; 'x', 'y'; x = 1, f(x), [30] = 23; 45 }",
        "return a"
      );

      TestCode(sCode,
        Table(
          TV("z", 32),
          TV(1, "x"),
          TV(2, "y"),
          TV("x", 1),
          TV(3, 24),
          TV(30, 23),
          TV(4, 45)
        )
      );
    }

    [TestMethod]
    public void TestTable06()
    {
      TestCode("return {a = {}}", Table(TV("a", Table())));
    }

    [TestMethod]
    public void TestTable07()
    {
      TestCode(Lines(
        "function f()",
        "  return 1, 2, 3, 4;",
        "end;",
        "return {f()};"
        ),
        Table(
          TV(1, 1),
          TV(2, 2),
          TV(3, 3),
          TV(4, 4)
        )
      );
    }

    [TestMethod]
    public void TestTable08()
    {
      TestCode(Lines(
        "function f()",
        "  return 1, 2, 3, 4;",
        "end;",
        "return {0,f()};"
        ),
        Table(
          TV(1, 0),
          TV(2, 1),
          TV(3, 2),
          TV(4, 3),
          TV(5, 4)
        )
      );
    }

    [TestMethod]
    public void TestTable09()
    {
      TestCode(Lines(
        "function f()",
        "  return 1, 2, 3, 4;",
        "end;",
        "return {f(),2};"
        ),
        Table(
          TV(1, 1),
          TV(2, 2)
        )
      );
    }

    #endregion

    #region -- Index Tests ------------------------------------------------------------

    [TestMethod]
    public void TestIndex01()
    {
      using (Lua l = new Lua())
      {
				l.PrintExpressionTree = Console.Out;
        var g = l.CreateEnvironment();
        TestResult(
          g.DoChunk("return test[0], test[1], test[2];", "dummy",
            new KeyValuePair<string, object>("test", new int[] { 1, 2, 3 })
          ), 1, 2, 3);
      }
    }

    [TestMethod]
    public void TestIndex02()
    {
      using (Lua l = new Lua())
      {
				l.PrintExpressionTree = Console.Out;
        var g = l.CreateEnvironment();
        TestResult(
          g.DoChunk("return test[0, 0], test[0, 1], test[1,0], test[1,1];", "dummy",
            new KeyValuePair<string, object>("test", new int[,] { { 1, 2 }, { 3, 4 } })
          ), 1, 2, 3, 4);
      }
    }

    [TestMethod]
    public void TestIndex03()
    {
      using (Lua l = new Lua())
      {
				l.PrintExpressionTree = Console.Out;
        var g = l.CreateEnvironment();
        TestResult(
          g.DoChunk("return test[0], test[1];", "dummy",
            new KeyValuePair<string, object>("test", new IndexAccess())
          ), 0, 1);
      }
    }

    [TestMethod]
    public void TestIndex04()
    {
      using (Lua l = new Lua())
      {
				l.PrintExpressionTree = Console.Out;
        var g = l.CreateEnvironment();
        TestResult(
          g.DoChunk("test[1] = 42; return test[0], test[1], test[2];", "dummy",
            new KeyValuePair<string, object>("test", new int[] { 1, 2, 3 })
          ), 1, 42, 3);
      }
    }

    #endregion

    #region -- TestVarArg01 -----------------------------------------------------------

    [TestMethod]
    public void TestVarArg01()
    {
      TestCode(Lines(
        "function sum(...)",
        "  local a, b, c = ...;",
        "  return a + b + c;",
        "end;",
        "return sum(3, 20, 100);"),
        123);
    }

    [TestMethod]
    public void TestVarArg02()
    {
      TestCode(Lines(
        "function test(...)",
        "  local a : table = {...};",
        "  return a[2];",
        "end;",
        "return test(1,2,3);"),
        2);
    }

    [TestMethod]
    public void TestVarArg03()
    {
      TestCode(Lines(
        "function test(...)",
        "  return ...[1];",
        "end;",
        "return test(1,2,3);"),
        2);
    }

		#endregion

		#region -- DynamicObjectCompatibility ---------------------------------------------

		[TestMethod]
		public void DynamicObjectCompatibility01()
		{
			using (var l = new Lua())
			{
				var g = l.CreateEnvironment();
				TestResult(g.DoChunk(
					Lines(
						"foreach c in dyn.GetDynamicMemberNames() do print(c); end;",
						"return dyn.Value1, dyn.Value2, dyn.Value3"), "test.lua",
					new KeyValuePair<string, object>("dyn", new DynTest())), 42, null, 43);
			}
			// test get.member
			// test get.members
		} // proc DynamicObjectCompatibility01

		//[TestMethod]
		//public void DynamicComCompatibility01()
		//{
		//	TestCode(Lines(
		//		"local t = clr.System.Type:GetTypeFromProgID('WScript.Shell')",
		//		"local w = clr.System.Activator:CreateInstance(t);",
		//		"return w.CurrentDirectory;"),
		//		Environment.CurrentDirectory);
		//}

		#endregion

		#region -- Assign -----------------------------------------------------------------

		[TestMethod]
		public void TestAssign01()
		{
			TestCode(Lines(
				"local a = {};",
				"local i = 3;",
				"i, a[i] = i + 1, 20;",
				"return i, a[3];"),
				4, 20);
		}

		[TestMethod]
		public void TestAssign02()
		{
			TestCode(Lines(
				"local x = 1;",
				"local y = 2;",
				"x, y = y, x",
				"return y, x;"),
				1, 2);
		}

		#endregion

		/*
print("testing assignments, logical operators, and constructors")

local res, res2 = 27

a, b = 1, 2+3
assert(a==1 and b==5)
a={}
function f() return 10, 11, 12 end
a.x, b, a[1] = 1, 2, f()
assert(a.x==1 and b==2 and a[1]==10)
a[f()], b, a[f()+3] = f(), a, 'x'
assert(a[10] == 10 and b == a and a[13] == 'x')

do
  local f = function (n) local x = {}; for i=1,n do x[i]=i end;
                         return table.unpack(x) end;
  local a,b,c
  a,b = 0, f(1)
  assert(a == 0 and b == 1)
  A,b = 0, f(1)
  assert(A == 0 and b == 1)
  a,b,c = 0,5,f(4)
  assert(a==0 and b==5 and c==1)
  a,b,c = 0,5,f(0)
  assert(a==0 and b==5 and c==nil)
end

a, b, c, d = 1 and nil, 1 or nil, (1 and (nil or 1)), 6
assert(not a and b and c and d==6)

d = 20
a, b, c, d = f()
assert(a==10 and b==11 and c==12 and d==nil)
a,b = f(), 1, 2, 3, f()
assert(a==10 and b==1)

assert(a<b == false and a>b == true)
assert((10 and 2) == 2)
assert((10 or 2) == 10)
assert((10 or assert(nil)) == 10)
assert(not (nil and assert(nil)))
assert((nil or "alo") == "alo")
assert((nil and 10) == nil)
assert((false and 10) == false)
assert((true or 10) == true)
assert((false or 10) == 10)
assert(false ~= nil)
assert(nil ~= false)
assert(not nil == true)
assert(not not nil == false)
assert(not not 1 == true)
assert(not not a == true)
assert(not not (6 or nil) == true)
assert(not not (nil and 56) == false)
assert(not not (nil and true) == false)

assert({} ~= {})
print('+')

a = {}
a[true] = 20
a[false] = 10
assert(a[1<2] == 20 and a[1>2] == 10)

function f(a) return a end

local a = {}
for i=3000,-3000,-1 do a[i] = i; end
a[10e30] = "alo"; a[true] = 10; a[false] = 20
assert(a[10e30] == 'alo' and a[not 1] == 20 and a[10<20] == 10)
for i=3000,-3000,-1 do assert(a[i] == i); end
a[print] = assert
a[f] = print
a[a] = a
assert(a[a][a][a][a][print] == assert)
a[print](a[a[f]] == a[print])
assert(not pcall(function () local a = {}; a[nil] = 10 end))
assert(not pcall(function () local a = {[nil] = 10} end))
assert(a[nil] == nil)
a = nil

a = {10,9,8,7,6,5,4,3,2; [-3]='a', [f]=print, a='a', b='ab'}
a, a.x, a.y = a, a[-3]
assert(a[1]==10 and a[-3]==a.a and a[f]==print and a.x=='a' and not a.y)
a[1], f(a)[2], b, c = {['alo']=assert}, 10, a[1], a[f], 6, 10, 23, f(a), 2
a[1].alo(a[2]==10 and b==10 and c==print)

a[2^31] = 10; a[2^31+1] = 11; a[-2^31] = 12;
a[2^32] = 13; a[-2^32] = 14; a[2^32+1] = 15; a[10^33] = 16;

assert(a[2^31] == 10 and a[2^31+1] == 11 and a[-2^31] == 12 and
       a[2^32] == 13 and a[-2^32] == 14 and a[2^32+1] == 15 and
       a[10^33] == 16)

a = nil


-- test conflicts in multiple assignment
do
  local a,i,j,b
  a = {'a', 'b'}; i=1; j=2; b=a
  i, a[i], a, j, a[j], a[i+j] = j, i, i, b, j, i
  assert(i == 2 and b[1] == 1 and a == 1 and j == b and b[2] == 2 and
         b[3] == 1)
end

-- repeat test with upvalues
do
  local a,i,j,b
  a = {'a', 'b'}; i=1; j=2; b=a
  local function foo ()
    i, a[i], a, j, a[j], a[i+j] = j, i, i, b, j, i
  end
  foo()
  assert(i == 2 and b[1] == 1 and a == 1 and j == b and b[2] == 2 and
         b[3] == 1)
  local t = {}
  (function (a) t[a], a = 10, 20  end)(1);
  assert(t[1] == 10)
end

-- bug in 5.2 beta
local function foo ()
  local a
  return function ()
    local b
    a, b = 3, 14    -- local and upvalue have same index
    return a, b
  end
end

local a, b = foo()()
assert(a == 3 and b == 14)
*/
	} // class Expressions
}
