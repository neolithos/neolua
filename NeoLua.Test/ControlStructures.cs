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
    public class CompareMe : IComparable<int>, IComparable<CompareMe>, IComparable
    {
      private int i;

      public CompareMe(int i)
      {
        this.i = i;
      }

      public int CompareTo(int other)
      {
        return i - other;
      }

      public int CompareTo(CompareMe other)
      {
        return i - other.i;
      }

      public int CompareTo(object obj)
      {
        if (obj is CompareMe)
          return i - ((CompareMe)obj).i;
        else if (obj is int)
          return i - (int)obj;
        else
          return -1;
      }

      public override bool Equals(object obj)
      {
        return CompareTo(obj) == 0;
      }

      public override int GetHashCode()
      {
        return i.GetHashCode();
      }

      //public static int operator +(CompareMe a, CompareMe b)
      //{
      //  return a.i + b.i;
      //}

      //public static implicit operator CompareMe(int a)
      //{
      //  return new CompareMe(a);
      //}

      //public static implicit operator int(CompareMe a)
      //{
      //  return a.i;
      //}

      //public static CompareMe operator -(CompareMe a, CompareMe b)
      //{
      //  return new CompareMe(a.i - b.i);
      //}

      //public static CompareMe operator *(CompareMe a, CompareMe b)
      //{
      //  return new CompareMe(a.i * b.i);
      //}

      //public static CompareMe operator /(CompareMe a, CompareMe b)
      //{
      //  return new CompareMe(a.i / b.i);
      //}

      //public static CompareMe operator %(CompareMe a, CompareMe b)
      //{
      //  return new CompareMe(a.i % b.i);
      //}

      //public static bool operator ==(CompareMe a, CompareMe b)
      //{
      //  return a.i == b.i;
      //}

      //public static bool operator !=(CompareMe a, CompareMe b)
      //{
      //  return a.i != b.i;
      //}

      //public static bool operator <=(CompareMe a, CompareMe b)
      //{
      //  return a.i <= b.i;
      //}

      //public static bool operator >=(CompareMe a, CompareMe b)
      //{
      //  return a.i >= b.i;
      //}

      //public static bool operator <(CompareMe a, CompareMe b)
      //{
      //  return a.i < b.i;
      //}

      //public static bool operator >(CompareMe a, CompareMe b)
      //{
      //  return a.i > b.i;
      //}

      //public static CompareMe operator &(CompareMe a, CompareMe b)
      //{
      //  return a.i & b.i;
      //}

      //public static CompareMe operator |(CompareMe a, CompareMe b)
      //{
      //  return a.i | b.i;
      //}

      ////+, -, !, ~, ++, --, true, false
      ////+, -, *, /, %, &, | , ^, <<, >>
      ////==, !=, <, >, <=, >=

      //public static CompareMe operator ++(CompareMe a)
      //{
      //  a.i++;
      //  return a;
      //}

      //public static CompareMe operator --(CompareMe a)
      //{
      //  a.i--;
      //  return a;
      //}
    }

    [TestMethod]
    public void Control01()
    {
      Assert.IsTrue(TestExpression(true, GetCode("Lua.Control01.lua"), 3));
    }

    [TestMethod]
    public void Control02()
    {
      Assert.IsTrue(TestExpression(true, GetCode("Lua.Control02.lua"), 10));
    }

    [TestMethod]
    public void Control03()
    {
      Assert.IsTrue(TestExpression(true, GetCode("Lua.Control03.lua"), 10));
    }

    [TestMethod]
    public void Control04()
    {
      Assert.IsTrue(TestExpression(true, GetCode("Lua.Control04.lua"), 4));
    }

    [TestMethod]
    public void Control05()
    {
      Assert.IsTrue(TestExpression(true, GetCode("Lua.Control05.lua"), 10));
    }

    [TestMethod]
    public void Control06()
    {
      Assert.IsTrue(TestExpression(true, GetCode("Lua.Control06.lua"), 55));
    }

    [TestMethod]
    public void Control07()
    {
      Assert.IsTrue(TestExpression(true, "return;", null));
    }

    [TestMethod]
    public void Control08()
    {
      Assert.IsTrue(TestExpression(true, GetCode("Lua.Control08.lua"), 4321));
    }

    [TestMethod]
    public void Control09()
    {
      Assert.IsTrue(TestExpression(true, GetCode("Lua.Control09.lua"), 4321));
    }

    [TestMethod]
    public void Control10()
    {
      Assert.IsTrue(TestExpression(true, GetCode("Lua.Control10.lua"), 4321));
    }

    [TestMethod]
    public void Control11()
    {
      Assert.IsTrue(TestExpression(true, GetCode("Lua.Control11.lua"), 6));
    }

    //[TestMethod]
    //public void Control12()
    //{
    //  CompareMe a = 1;
    //  System.Diagnostics.Debug.Print("{0}", a + 3);
    //  //Assert.IsTrue(TestExpression(true, GetCode("Lua.Control12.lua"), 4));
    //}
  } // class ControlStructures
}
