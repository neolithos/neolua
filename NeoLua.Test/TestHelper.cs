using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.IronLua;

namespace LuaDLR.Test
{
  public class TestHelper
  {
    public string Lines(params string[] lines)
    {
      return String.Join(Environment.NewLine, lines);
    } // func Lines

    public void TestCode(string sCode, params object[] expectedResult)
    {
      using (Lua l = new Lua())
      {
        l.PrintExpressionTree = true;
        var g = l.CreateEnvironment();
        Console.WriteLine("Test: {0}", sCode);
        Console.WriteLine(new string('=', 66));
        Stopwatch sw = new Stopwatch();
        sw.Start();
        TestResult(g.DoChunk(sCode, "test.lua"), expectedResult);
        Console.WriteLine("  Dauer: {0}ms", sw.ElapsedMilliseconds);
        Console.WriteLine();
        Console.WriteLine();
      }
    } // proc TestCode

    public void TestExpr(string sExpression, params object[] expectedResult)
    {
      TestCode("return " + sExpression + ";", expectedResult);
    } // proc TestStmt

    private static string FormatValue(object v)
    {
      return String.Format("({0}){1}", v == null ? "object" : v.GetType().Name, v);
    } // func FormatValue

    public static void TestResult(LuaResult result, params object[] expectedResult)
    {
      if(result == null)
        throw new ArgumentNullException("no result");

      if (expectedResult == null || expectedResult.Length == 0) // no results expected
      {
        if (result.Values.Length == 0)
        {
          Console.WriteLine("OK: no result == no result");
        }
        else
        {
          Console.WriteLine("FAIL: no result != {0}", FormatValue(result[0]));
          Assert.Fail();
        }
      }
      else
      {
        for (int i = 0; i < expectedResult.Length; i++)
        {
          object valueResult = result[i];
          object valueExpected = expectedResult[i];

          if (valueResult is double)
            valueResult = Math.Round((double)valueResult, 3);
          if (valueExpected is double)
            valueExpected = Math.Round((double)valueExpected, 3);

          bool lOk = Object.Equals(valueResult, valueExpected);
          Console.WriteLine("{0}: {1} {2} {3}", lOk ? "OK" : "FAIL", FormatValue(valueResult), lOk ? "==" : "!=", FormatValue(valueExpected));
          if (!lOk)
            Assert.Fail();
        }
        if (result.Values.Length != expectedResult.Length)
        {
          Console.WriteLine("FAIL: Result Count {0} != {1}", result.Values.Length, expectedResult.Length);
          Assert.Fail();
        }
      }
    } // proc TestResult

    public object[] NullResult { get { return new object[] { null }; } }





    protected static bool TestExpression(bool lFullCode, string sExpr, object result)
    {
      Debug.Print("Test: " + sExpr);
      using (Lua l = new Lua())
      {
        l.PrintExpressionTree = true;
        LuaResult r = l.CreateEnvironment().DoChunk(lFullCode ? sExpr : "local a = " + sExpr + "; return a;", "test.lua");
        if (result == null && r[0] == result || (Object.Equals(r[0].ToString(), result.ToString()) && r[0].GetType() == result.GetType()))
          return true;
        else
        {
          Debug.Print("{0} != {1}", r[0], result);
          return false;
        }
      }
    } // func TestConstant

    protected static bool TestExpressionTable(bool lFullCode, string sExpr, params KeyValuePair<object, object>[] tests)
    {
      Debug.Print("Test: " + sExpr);
      Lua l = new Lua();
      l.PrintExpressionTree = true;
      object[] r = l.CreateEnvironment().DoChunk(lFullCode ? sExpr : "local a = " + sExpr + "; return a;", "test.lua");
      LuaTable t = r[0] as LuaTable;
      if (t == null)
        return false;
      else
      {
        List<KeyValuePair<object, object>> testList = new List<KeyValuePair<object, object>>(tests);
        foreach (var cur in t)
        {
          int iIdx = testList.FindIndex(c => Object.Equals(c.Key, cur.Key));
          if (iIdx == -1)
            return false;
          else
          {
            if (!Object.Equals(cur.Value, testList[iIdx].Value))
              return false;
            testList.RemoveAt(iIdx);
          }
        }
        return testList.Count == 0;
      }
    } // func TestConstant

    protected static bool TestCompare(string sExpr)
    {
      Debug.Print("Test: " + sExpr);
      Lua l = new Lua();
      l.PrintExpressionTree = true;
      object[] r = l.CreateEnvironment().DoChunk("local a; if " + sExpr + " then a = true; else a = false; end; return a;", "test.lua");
      return (bool)r[0];
    } // func TestCompare

    protected static KeyValuePair<object, object> TV(object item, object value)
    {
      return new KeyValuePair<object, object>(item, value);
    } // func TV

    protected bool TestReturn(string sCode, params object[] result)
    {
      Lua l = new Lua();
      l.PrintExpressionTree = true;
      object[] r = l.CreateEnvironment().DoChunk(sCode, "test.lua");

      return TestReturn(result, r);
    } // func TestReturn

    protected static bool TestReturn(LuaResult result, params object[] r)
    {
      if (r == null && result.Values.Length == 0)
        return true;
      if (r.Length != result.Values.Length)
        return false;

      for (int i = 0; i < r.Length; i++)
        if (r[i] == null && result[i] == null ||
          (Object.Equals(r[i].ToString(), result[i].ToString()) && r[i].GetType() == result[i].GetType()))
        {
          Debug.Print("[{0}] {1} == {2}", i, r[i], result[i]);
        }
        else
        {
          Debug.Print("[{0}] {1} != {2}", i, r[i], result[i]);
          return false;
        }
      return true;
    } // func TestReturn

    protected static string GetCode(string sName)
    {
      using (Stream src = typeof(TestHelper).Assembly.GetManifestResourceStream(typeof(TestHelper), sName))
      using (TextReader tr = new StreamReader(src))
        return tr.ReadToEnd();
    } // func GetCode
  } // class TestHelper
}
