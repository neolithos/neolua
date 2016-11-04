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
			//	return a.i + b.i;
			//}

			//public static implicit operator CompareMe(int a)
			//{
			//	return new CompareMe(a);
			//}

			//public static implicit operator int(CompareMe a)
			//{
			//	return a.i;
			//}

			//public static CompareMe operator -(CompareMe a, CompareMe b)
			//{
			//	return new CompareMe(a.i - b.i);
			//}

			//public static CompareMe operator *(CompareMe a, CompareMe b)
			//{
			//	return new CompareMe(a.i * b.i);
			//}

			//public static CompareMe operator /(CompareMe a, CompareMe b)
			//{
			//	return new CompareMe(a.i / b.i);
			//}

			//public static CompareMe operator %(CompareMe a, CompareMe b)
			//{
			//	return new CompareMe(a.i % b.i);
			//}

			//public static bool operator ==(CompareMe a, CompareMe b)
			//{
			//	return a.i == b.i;
			//}

			//public static bool operator !=(CompareMe a, CompareMe b)
			//{
			//	return a.i != b.i;
			//}

			//public static bool operator <=(CompareMe a, CompareMe b)
			//{
			//	return a.i <= b.i;
			//}

			//public static bool operator >=(CompareMe a, CompareMe b)
			//{
			//	return a.i >= b.i;
			//}

			//public static bool operator <(CompareMe a, CompareMe b)
			//{
			//	return a.i < b.i;
			//}

			//public static bool operator >(CompareMe a, CompareMe b)
			//{
			//	return a.i > b.i;
			//}

			//public static CompareMe operator &(CompareMe a, CompareMe b)
			//{
			//	return a.i & b.i;
			//}

			//public static CompareMe operator |(CompareMe a, CompareMe b)
			//{
			//	return a.i | b.i;
			//}

			////+, -, !, ~, ++, --, true, false
			////+, -, *, /, %, &, | , ^, <<, >>
			////==, !=, <, >, <=, >=

			//public static CompareMe operator ++(CompareMe a)
			//{
			//	a.i++;
			//	return a;
			//}

			//public static CompareMe operator --(CompareMe a)
			//{
			//	a.i--;
			//	return a;
			//}
		}

    [TestMethod]
    public void Control01()
    {
      TestCode(GetLines("Lua.Control01.lua"), 3);
    }

    [TestMethod]
    public void Control02()
    {
      TestCode(GetLines("Lua.Control02.lua"), 10);
    }

    [TestMethod]
    public void Control03()
    {
      TestCode(GetLines("Lua.Control03.lua"), 10);
    }

    [TestMethod]
    public void Control04()
    {
      TestCode(GetLines("Lua.Control04.lua"), 4);
    }

    [TestMethod]
    public void Control05()
    {
      TestCode(GetLines("Lua.Control05.lua"), 10);
    }

    [TestMethod]
    public void Control06()
    {
      TestCode(GetLines("Lua.Control06.lua"), 55);
    }

    [TestMethod]
    public void Control07()
    {
      TestCode("return;");
    }

    [TestMethod]
    public void Control08()
    {
      TestCode(GetLines("Lua.Control08.lua"), 4321);
    }

    [TestMethod]
    public void Control09()
    {
      TestCode(GetLines("Lua.Control09.lua"), 4321);
    }

    [TestMethod]
    public void Control10()
    {
      TestCode(GetLines("Lua.Control10.lua"), 4321);
    }

    [TestMethod]
    public void Control11()
    {
      TestCode(GetLines("Lua.Control11.lua"), 6);
    }

		[TestMethod]
		public void Control12()
		{
			TestCode(Lines(
				"local s = 0;",
				"for i = 1, 100, 0.1 do",
				"  s = s + i;",
				"end;",
				"return s;"), 50045.5);
		}
		
		[TestMethod]
		public void TestVariableAssign01()
		{
			TestCode(Lines(
				"local type = type;",
				"return type(2);"), "number");
		}
  } // class ControlStructures
}
