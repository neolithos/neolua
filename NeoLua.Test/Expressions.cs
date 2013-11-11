using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.IronLua;

namespace LuaDLR.Test
{
  [TestClass]
  public class ExpressionsArithmentic : TestHelper
  {
    [TestMethod]
    public void TestExpr01() { Assert.IsTrue(TestExpression(false, "1 + 2", 3)); }

    [TestMethod]
    public void TestExpr02() { Assert.IsTrue(TestExpression(false, "1 + 2 * 3", 7)); }

    [TestMethod]
    public void TestExpr03() { Assert.IsTrue(TestExpression(false, "1 + 2.2 * 3", 7.6)); }

    [TestMethod]
    public void TestExpr04() { Assert.IsTrue(TestExpression(false, "1 + -2.2 * 3", -5.6)); }

    [TestMethod]
    public void TestExpr05() { Assert.IsTrue(TestExpression(false, "-2 * '2'", -4.0)); }

    [TestMethod]
    public void TestExpr06() { Assert.IsTrue(TestExpression(false, "nil", null)); }

    [TestMethod]
    public void TestExpr07() { Assert.IsTrue(TestExpression(false, "true", true)); }

    [TestMethod]
    public void TestExpr08() { Assert.IsTrue(TestExpression(false, "false", false)); }

    [TestMethod]
    public void TestExpr09a() { Assert.IsTrue(TestExpression(false, "2 ^ 8", 256)); }

    [TestMethod]
    public void TestExpr09b() { Assert.IsTrue(TestExpression(false, "2.0 ^ 8", 256.0)); }

    [TestMethod]
    public void TestExpr10() { Assert.IsTrue(TestExpression(false, "1 / 2", 0)); }

    [TestMethod]
    public void TestExpr11() { Assert.IsTrue(TestExpression(false, "1.0 / 2", 0.5)); }

    [TestMethod]
    public void TestExpr12() { Assert.IsTrue(TestExpression(false, "1 % 3", 1)); }

    [TestMethod]
    public void TestExpr13() { Assert.IsTrue(TestExpression(false, "1.0 % 3", 1.0)); }

    [TestMethod]
    public void TestExpr14() { Assert.IsTrue(TestExpression(false, "1.3 % 4.3", 1.3)); }

    [TestMethod]
    public void TestExpr15() { Assert.IsTrue(TestExpression(false, "'a' .. 'b' .. 'c'", "abc")); }

    [TestMethod]
    public void TestExpr16() { try { TestExpression(false, "nil + 1", 1); Assert.IsTrue(false); } catch { } }

    [TestMethod]
    public void TestExpr17() { Assert.IsTrue(TestExpression(false, "1 + (1 + 2) * 3", 10)); }

    [TestMethod]
    public void TestExpr18() { try { TestExpression(false, "-nil", 1); Assert.IsTrue(false); } catch { } }

    [TestMethod]
    public void TestExpr19() { Assert.IsTrue(TestExpression(true, "const a = 20; return a;", 20)); }

    [TestMethod]
    public void TestExpr20() { Assert.IsTrue(TestExpression(true, "const a = cast(ushort, 20); return a;", (ushort)20)); }

    [TestMethod]
    public void TestExpr21() { Assert.IsTrue(TestExpression(true, "const a = cast(int, '20'); return a;", 20)); }
  } // class ExpressionsArithmentic
  
  [TestClass]
  public class ExpressionsCompare : TestHelper
  {
    [TestMethod]
    public void TestExpr01() { Assert.IsTrue(TestExpression(false, "1 < 2", true)); }

    [TestMethod]
    public void TestExpr02() { Assert.IsTrue(TestExpression(false, "1 > 2", false)); }

    [TestMethod]
    public void TestExpr03() { Assert.IsFalse(TestCompare("1 == '1'")); }

    [TestMethod]
    public void TestExpr04() { Assert.IsTrue(TestCompare("1")); }

    [TestMethod]
    public void TestExpr05() { Assert.IsFalse(TestCompare("0")); }

    [TestMethod]
    public void TestExpr06() { Assert.IsFalse(TestCompare("nil")); }

    [TestMethod]
    public void TestExpr07() { Assert.IsTrue(TestExpression(false, "10 or 20", 10)); }

    [TestMethod]
    public void TestExpr08() { Assert.IsTrue(TestExpression(false, "10 or false", 10)); }

    [TestMethod]
    public void TestExpr09() { Assert.IsTrue(TestExpression(false, "nil or 'a'", "a")); }

    [TestMethod]
    public void TestExpr10() { Assert.IsTrue(TestExpression(false, "nil and 10", null)); }

    [TestMethod]
    public void TestExpr11() { Assert.IsTrue(TestExpression(false, "false and false", false)); }

    [TestMethod]
    public void TestExpr12() { Assert.IsTrue(TestExpression(false, "false and nil", false)); }

    [TestMethod]
    public void TestExpr13() { Assert.IsTrue(TestExpression(false, "false or nil", null)); }

    [TestMethod]
    public void TestExpr14() { Assert.IsTrue(TestExpression(false, "10 and 20", 20)); }
  } // class ExpressionsCompare

  [TestClass]
  public class ExpressionsArrayLength : TestHelper
  {
  } // class ExpressionsArrayLength
  
  [TestClass]
  public class ExpressionsTables : TestHelper
  {
    [TestMethod]
    public void TestExpr01() { Assert.IsTrue(TestExpressionTable(false, "{}")); }

    [TestMethod]
    public void TestExpr02()
    {
      Assert.IsTrue(TestExpressionTable(false, "{1, 2, 3; 4}",
        TV(1, 1),
        TV(2, 2),
        TV(3, 3),
        TV(4, 4)));
    } // proc TestExpr

    [TestMethod]
    public void TestExpr03()
    {
      Assert.IsTrue(TestExpressionTable(false,"{a = 1, b = 2, c = 3;  d = 4}",
        TV("a", 1),
        TV("b", 2),
        TV("c", 3),
        TV("d", 4)));
    } // proc TestExpr

    [TestMethod]
    public void TestExpr04()
    {
      Assert.IsTrue(TestExpressionTable(false, "{['a'] = 1}",
        TV("a", 1)));
    } // proc TestExpr

    [TestMethod]
    public void TestExpr06()
    {
      Assert.IsTrue(TestExpressionTable(true, "function f(a) return a; end;" + Environment.NewLine +
        "local g = 32; local x = 24;" + Environment.NewLine+
        "a = { [f('z')] = g; 'x', 'y'; x = 1, f(x), [30] = 23; 45 }" + Environment.NewLine +
        "return a;",
        TV("z", 32),
        TV(1, "x"),
        TV(2, "y"),
        TV("x", 1),
        TV(3, 24),
        TV(30, 23),
        TV(4, 45)));
    } // proc TestExpr06

    //[TestMethod]
    //public void TestExpr05()
    //{
    //  Assert.IsTrue(TestExpressionTable("{a = {}}"));
    //} // proc TestExpr
  } // class ExpressionsTables
}
