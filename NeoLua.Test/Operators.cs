using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.IronLua;

namespace LuaDLR.Test
{
  public struct TestOperator
  {
    private int i;

    public TestOperator(int i)
    {
      this.i = i;
    }

    public static TestOperator operator +(int a, TestOperator b)
    {
      Console.WriteLine("  operator+ int,TestOperator");
      return new TestOperator(a + b.i);
    } // 

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
 
    public static implicit operator int(TestOperator a)
    {
      Console.WriteLine("  implicit TestOperator");
      return a.i;
    }

    public static explicit operator string(TestOperator a)
    {
      Console.WriteLine("  implicit TestOperator");
      return a.i.ToString();
    }

    public static implicit operator TestOperator(int a)
    {
      Console.WriteLine("  implicit int");
      return new TestOperator(a);
    }
  }

  [TestClass]
  public class Operators
  {
    [TestMethod]
    public void Operator01()
    {
      int a = 1 + 2;
      Assert.IsTrue(a == 3);
      Console.WriteLine("Test 1 (a = new TestOperator(2) + 1):");
      a = new TestOperator(2) + 1;
      Assert.IsTrue(a == 3);

      Console.WriteLine("Test 2 (TestOperator b = 1 + 2):");
      TestOperator b = 1 + 2;
      Assert.IsTrue(a == 3);

      Console.WriteLine("Test 3 (TestOperator c = 2 + new TestOperator(1)):");
      TestOperator c = 2 + new TestOperator(1);
      Assert.IsTrue(c == 3);

      Console.WriteLine("Methods:");
      Type t = typeof(TestOperator);
      foreach (MethodInfo mi in t.GetMethods(BindingFlags.Public | BindingFlags.InvokeMethod | BindingFlags.Static | BindingFlags.DeclaredOnly))
        Console.WriteLine(mi.Name);
    } // proc Operator01

    [TestMethod]
    public void Operator02()
    {
      using (Lua l = new Lua())
      {
        l.PrintExpressionTree = true;
        dynamic g = l.CreateEnvironment();

        int a = g.dochunk(String.Join(Environment.NewLine,
          new string[]
          {
            "local a : int = clr.LuaDLR.Test.TestOperator(2) + 1;",
            "return a;"
          }));
        Assert.IsTrue(a == 3);
      }
    } // proc Operator02
  } // class Operators
}
