using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.IronLua;

namespace LuaDLR.Test
{
  ///////////////////////////////////////////////////////////////////////////////
  /// <summary></summary>
  [TestClass]
  public class LuaTypeTests : TestHelper
  {
		public class Graph
		{
			public struct PointF
			{
				public float x, y;
			}
			public class Point
			{
				public int x, y;
			}
			public void DrawLine(string pen, float x1, float y1, float x2, float y2)
			{
				Console.WriteLine(string.Format(@"DrawLine '{4}' with float: {0:f1}, {1:f1}, {2:f1}, {3:f1}", x1, y1, x2, y2, pen));
			}
			public void DrawLine(string pen, PointF pt1, PointF pt2)
			{
				Console.WriteLine(string.Format(@"DrawLine '{4}' with PointF: {0:f1},{1:f1}, {2:f1},{3:f1}", pt1.x, pt1.y, pt2.x, pt2.y, pen));
			}
			public void DrawLine(string pen, int x1, int y1, int x2, int y2)
			{
				Console.WriteLine(string.Format(@"DrawLine '{4}' with int: {0}, {1}, {2}, {3}", x1, y1, x2, y2, pen));
			}
			public void DrawLine(string pen, Point pt1, Point pt2)
			{
				Console.WriteLine(string.Format(@"DrawLine '{4}' with Point: {0},{1}, {2},{3}", pt1.x, pt1.y, pt2.x, pt2.y, pen));
			}
		}

    public class SubClass
    {
      public void Test()
      {
        Console.WriteLine("Empty");
      }

      public void Test(string a)
      {
        Console.WriteLine(a);
      }

      public void Test(string a, string b)
      {
        Console.WriteLine(a + b);
      }

      public void Fire()
      {
        if (EventTest != null)
          EventTest();
        if (EventTest2 != null)
          EventTest2(this, EventArgs.Empty);
      }

      public static int Value { get; set; }

      public event Action EventTest;
      public event EventHandler EventTest2;
    }

    public struct SubStruct
    {
      private int iValue;

      public SubStruct(int i)
      {
        iValue = i;
      }

      public int Value { get { return iValue; } set { iValue = value; } }
    }

    public static void Test()
    {
      Console.WriteLine("Hallo");
    }

		public static float OverloadMethod(float i)
		{
			Console.WriteLine("FLOAT");
			return i;
		}

		public static int OverloadMethod(int i)
		{
			Console.WriteLine("INT");
			return i;
		}

		public static bool GreenColor(Color color)
		{
			Console.WriteLine("Color: {0}", color);
			return color == Color.Green;
		}

    [TestMethod]
    public void TypeTest01()
    {
      LuaType t = LuaType.GetType(typeof(Stream));
      Assert.IsTrue(t.Type != null);
      t = LuaType.GetType("System.Test.Test", false, true);
      Assert.IsTrue(t.Type == null);
      t = LuaType.GetType("LuaDLR.Test.LuaTypeTests.SubClass", false, true);
      Assert.IsTrue(t.Type != null);
      t = LuaType.GetType(typeof(List<string>));
      Assert.IsTrue(t.Type != null);
      t = LuaType.GetType(typeof(string[]));
      Assert.IsTrue(t.Type != null);
    }

    [TestMethod]
    public void TypeTest02()
    {

      using (Lua l = new Lua())
      {
        dynamic g = l.CreateEnvironment();

        Type t = typeof(SubClass);
        LuaType tl = LuaType.GetType(t);
        TestResult(g.dochunk("return clr.LuaDLR.Test.LuaTypeTests.SubClass"), tl);
        TestResult(g.dochunk("return clr.LuaDLR.Test.LuaTypeTests.SubClass:GetType()"), t);

        TestResult(g.dochunk("return clr.System.IO.Stream"), LuaType.GetType(typeof(Stream)));

        LuaType tNull = g.dochunk("return clr.System.Test.Test");
        Assert.IsTrue(tNull.Type == null);

        tl = g.dochunk("return clr.System.Collections.Generic.List[clr.System.String]", "test");
        TestResult(new LuaResult(tl.Type), typeof(List<string>));
        tl = g.dochunk("return clr.System.String[]", "test");
        TestResult(new LuaResult(tl.Type), typeof(string[]));
      }
    }

    [TestMethod]
    public void TypeTest03()
    {
      using (Lua l = new Lua())
      {
        dynamic g = l.CreateEnvironment();

        g.dochunk("return cast(System.IO.Stream, null);");
        g.dochunk("return cast(LuaDLR.Test.LuaTypeTests.SubClass, null);");
        g.dochunk("return cast(System.Collections.Generic.List[string[]], null);");
        g.dochunk("return cast(System.String[], null);");
        g.dochunk("return cast(string[], null);");
      }
    }

    [TestMethod]
    public void TypeTest04()
    {
      dynamic t = LuaType.GetType(typeof(int));
      Type t1 = t;
      Type t2 = (Type)t;
      Assert.IsTrue(t1 == typeof(int));
      Assert.IsTrue(t2 == typeof(int));
    }

    [TestMethod]
    public void TypeTest05()
    {
      TestCode(Lines(
        new string[]
        {
          "const StringBuilder typeof System.Text.StringBuilder;",
          "local sb : StringBuilder = StringBuilder();",
          "sb:Append('hallo');",
          "return sb:ToString();"
        }),
        "hallo");
    }

    [TestMethod]
    public void TypeTest06()
    {
      TestCode("clr.LuaDLR.Test.LuaTypeTests.SubClass.Value = 3;");
      Assert.IsTrue(SubClass.Value == 3);
    }

		[TestMethod]
		public void TypeTest07()
		{
			TestCode("return clr.LuaDLR.Test.LuaTypeTests:GreenColor(clr.System.Drawing.Color.Green);", true);
		}

		[TestMethod]
		public void PropertyTest01()
		{
			TestCode(Lines(
				"clr.LuaDLR.Test.LuaTypeTests.SubClass.Value = 1;",
				"clr.LuaDLR.Test.LuaTypeTests.SubClass.Value = 2;",
				"clr.LuaDLR.Test.LuaTypeTests.SubClass.Value = 1.0;",
				"return clr.LuaDLR.Test.LuaTypeTests.SubClass.Value;"), 1);
		}

    [TestMethod]
    public void MethodTest01()
    {
      using (Lua l = new Lua())
      {
        dynamic g = l.CreateEnvironment();
        g.console = LuaType.GetType(typeof(Console));
        g.dochunk("console.WriteLine('Hallo!');", "test");
        dynamic wl = g.console.WriteLine;
        Assert.IsTrue(wl.GetType() == typeof(LuaOverloadedMethod));
        int iCount = wl.Count;
        Assert.IsTrue(iCount == 19);
        for (int i = 0; i < wl.Count; i++)
        {
          if (i == 17)
            Console.WriteLine("VarArgs NotImplemented.");
          else
            Console.WriteLine("{0}: {1}", i, wl[i].GetType().Name);
        }
      }
    }

    [TestMethod]
    public void MethodTest02()
    {
      using (Lua l = new Lua())
      {
        dynamic g = l.CreateEnvironment();
        g.console = LuaType.GetType(typeof(Console));
        g.dochunk("console.WriteLine('Hallo!');", "test");
        dynamic wl = g.console.WriteLine;
        Delegate dlg1 = wl[LuaType.GetType(typeof(string))];
        Delegate dlg2 = g.dochunk("return console.WriteLine[clr.System.String]");
        Delegate dlg3 = g.dochunk("return console.WriteLine[]");
        Assert.IsTrue(dlg1 == dlg2);
        Assert.IsTrue(dlg3 != null);
      }
    }

    [TestMethod]
    public void MethodTest03()
    {
      using (Lua l = new Lua())
      {
        l.PrintExpressionTree = true;
        dynamic g = l.CreateEnvironment();
        g.console = LuaType.GetType(typeof(Console));
        g.dochunk("console.WriteLine('Hallo!');", "test");
        g.dochunk("c = console.WriteLine[clr.System.String];");
        //g.c = g.console.WriteLine[typeof(string)];
        g.c("Hallo");
        g.dochunk("c('Hallo!')");
      }
    }

    [TestMethod]
    public void MethodTest04()
    {
      using (Lua l = new Lua())
      {
        l.PrintExpressionTree = true;
        var g = l.CreateEnvironment();
        dynamic m = g.DoChunk("return clr.LuaDLR.Test.LuaTypeTests.Test", "dummy");
        MethodInfo mi = m;
        Action action = m;
        Delegate dlg = m;
        m();
        Assert.IsTrue(mi != null);
        Assert.IsTrue(action != null);
        Assert.IsTrue(dlg != null);
      }
    }

    [TestMethod]
    public void MethodTest05()
    {
      using (Lua l = new Lua())
      {
        dynamic g = l.CreateEnvironment();
        dynamic r = g.dochunk(Lines(
          new string[] {
            "local c = clr.LuaDLR.Test.LuaTypeTests.SubClass()",
            "c.Test('Test');",
            "return c.Test;"
          }));

        foreach (var c in r[0])
          Console.WriteLine(c.GetType().Name);
      }
    }

    [TestMethod]
    public void MethodTest06()
    {
      TestCode("return 'Hallo'.Substring(2);", "llo");
    } 

    [TestMethod]
    public void EventTest01()
    {
      SubClass c = new SubClass();
      c.EventTest += () => Console.WriteLine("Fired.");
      c.Fire();
    }

    [TestMethod]
    public void EventTest02()
    {
      TestCode(Lines(
        "const SubClass typeof LuaDLR.Test.LuaTypeTests.SubClass;",
        "local c : SubClass = SubClass();",
        "c.EventTest:add(function():void print('Fired.'); end);",
        "c:Fire();"));
    }

    [TestMethod]
    public void EventTest03()
    {
      TestCode(Lines(
        "const SubClass typeof LuaDLR.Test.LuaTypeTests.SubClass;",
        "local c : SubClass = SubClass();",
        "c.EventTest2:add(function(sender : object, e : System.EventArgs):void print('Fired.'); end);",
        "c:Fire();"));
    }

    [TestMethod]
    public void EventTest04()
    {
      using (Lua l = new Lua())
      {
        var g = l.CreateEnvironment();
        var c = l.CompileChunk(Lines(
          "local a : System.EventHandler = function(a, b) : void",
          "  print('Hallo');",
          "end;",
					"a()"), "dummy", Lua.DefaultDebugEngine);
        g.DoChunk(c);
      }
    }

    [TestMethod]
    public void EventTest05()
    {
      using (Lua l = new Lua())
      {
        l.PrintExpressionTree = true;
        var g = l.CreateEnvironment();
        var c = l.CompileChunk(Lines(
          "local a : System.EventHandler = function(a, b) : void",
          "  print('Hallo');",
          "end;",
          "a();"), "dummy", null);
        g.DoChunk(c);
      }
    }

    [TestMethod]
    public void CtorTest01()
    {
      TestCode("return clr.LuaDLR.Test.LuaTypeTests.SubStruct().Value", 0);
    }

    [TestMethod]
    public void CtorTest02()
    {
      TestCode("return clr.LuaDLR.Test.LuaTypeTests.SubStruct(2).Value", 2);
    }

    [TestMethod]
    public void CtorTest03()
    {
      TestCode("return cast(LuaDLR.Test.LuaTypeTests.SubStruct, { Value = 2 }).Value", 2);
    }

		[TestMethod]
		public void ArrayTest01()
		{
			TestCode(Lines(
				"local a : int[] = clr.System.Int32[2];",
				"a[0] = 1;",
				"a[1] = 2;",
				"return a[0], a[1];"), 1, 2);
		}

		[TestMethod]
		public void ArrayTest02()
		{
			TestCode(Lines(
				"local a = clr.System.Int32[2,2];",
				"a[0, 0] = 1;",
				"a[1, 1] = 2;",
				"return a[0, 0], a[1, 1];"), 1, 2);
		}

		[TestMethod]
		public void ArrayTest03()
		{
			TestCode(Lines(
				"local a = clr.System.Int32[](1, 2);",
				"return a[0], a[1];"), 1, 2);
		}

		[TestMethod]
		public void ArrayTest04()
		{
			TestCode(Lines(
				"local a = clr.System.Int32[]{1, 2};",
				"return a[0], a[1];"), 1, 2);
		}

		[TestMethod]
		public void ArrayTest05()
		{
			TestCode(Lines(
				"function t() return 1, 2; end;",
				"local a = clr.System.Int32[](t());",
				"return a[0], a[1];"), 1, 2);
		}

		[TestMethod]
		public void OverloadTest01()
		{
			TestCode("return clr.LuaDLR.Test.LuaTypeTests:OverloadMethod(1), clr.LuaDLR.Test.LuaTypeTests:OverloadMethod(1.2);", 1, 1.2f);
			TestCode("return clr.LuaDLR.Test.LuaTypeTests.OverloadMethod(1), clr.LuaDLR.Test.LuaTypeTests.OverloadMethod(1.2);", 1, 1.2f);
		}

		[TestMethod]
		public void OverloadTest02()
		{
			TestCode(Lines(
				"graph = clr.LuaDLR.Test.LuaTypeTests.Graph();",
				"graph.DrawLine('a', 1, 1, 1, 1);",
				"graph.DrawLine('b', 1, 1, 1, 1.0);",
				"graph.DrawLine('c', 1, 1, 1.0, 1);",
				"graph.DrawLine('d', 1, 1, 1.0, 1.0);",
				"graph.DrawLine('e', 1, 1.0, 1, 1);",
				"graph.DrawLine('f', 1, 1.0, 1, 1.0);",
				"graph.DrawLine('g', 1, 1.0, 1.0, 1);",
				"graph.DrawLine('h', 1, 1.0, 1.0, 1.0);",
				"graph.DrawLine('i', 1.0, 1, 1, 1);",
				"graph.DrawLine('j', 1.0, 1, 1, 1.0);",
				"graph.DrawLine('k', 1.0, 1, 1.0, 1);",
				"graph.DrawLine('l', 1.0, 1, 1.0, 1.0);",
				"graph.DrawLine('m', 1.0, 1.0, 1, 1);",
				"graph.DrawLine('n', 1.0, 1.0, 1, 1.0);",
				"graph.DrawLine('o', 1.0, 1.0, 1.0, 1);",
				"graph.DrawLine('p', 1.0, 1.0, 1.0, 1.0);",
				"local i1 = 1;",
				"local i2 = 2;",
				"local i3 = 3;",
				"local i4 = 4;",
				"graph.DrawLine('aa', i1, i1, i2, i1);",
				"graph.DrawLine('ab', i1, i2, i3, i4);",
				"graph.DrawLine('ac', i1, i1, i1, i1);"));
		}

		[TestMethod]
		public void ExtensionTest01()
		{
			LuaType.RegisterTypeExtension(typeof(TypeExt));

			TestCode("return 'Hallo0':LetterCount();", 5);
			TestCode("t = 'Hallo0'; return t:LetterCount();", 5);
		}
  } // class LuaTypeTests 

	public static class TypeExt
	{
		public static int LetterCount(this string s)
		{
			int i = 0;
			foreach (char c in s)
				if (char.IsLetter(c))
					i++;

			return i;
		}
	}
}
