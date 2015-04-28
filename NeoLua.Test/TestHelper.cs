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
		public static bool PrintExpressionTree = true;

    public string Lines(params string[] lines)
    {
      return String.Join(Environment.NewLine, lines);
    } // func Lines

    public string GetLines(string sName)
    {
      using (Stream src = typeof(TestHelper).Assembly.GetManifestResourceStream(typeof(TestHelper), sName))
      using (TextReader tr = new StreamReader(src))
        return tr.ReadToEnd();
    } // func GetLines

    public void TestCode(string sCode, params object[] expectedResult)
    {
			using (Lua l = new Lua())
			{
				l.PrintExpressionTree = PrintExpressionTree ? Console.Out : null;
				//var g = l.CreateEnvironment();
				var t = new LuaTable();
				Console.WriteLine("Test: {0}", sCode);
				Console.WriteLine(new string('=', 66));
				Stopwatch sw = new Stopwatch();
				sw.Start();
				//TestResult(g.DoChunk(sCode, "test.lua"), expectedResult);
				TestResult(l.CompileChunk(sCode, "test.lua", null).Run(t), expectedResult);
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

          if (valueResult is LuaTable && valueExpected is KeyValuePair<object, object>[])
            TestTable((LuaTable)valueResult, (KeyValuePair<object, object>[])valueExpected);
          else
          {
            bool lOk = Object.Equals(valueResult, valueExpected);
            Console.WriteLine("{0}: {1} {2} {3}", lOk ? "OK" : "FAIL", FormatValue(valueResult), lOk ? "==" : "!=", FormatValue(valueExpected));
            if (!lOk)
              Assert.Fail();
          }
        }
        if (result.Values.Length != expectedResult.Length)
        {
          Console.WriteLine("FAIL: Result Count {0} != {1}", result.Values.Length, expectedResult.Length);
          Assert.Fail();
        }
      }
    } // proc TestResult

    public static void TestTable(LuaTable table, KeyValuePair<object, object>[] tvs)
    {
      bool lFailed = false;
      Console.WriteLine("Table {");
      TestTable("  ", table, tvs, ref lFailed);
      Console.WriteLine("}");
      Assert.IsFalse(lFailed);
    } // proc TestTable

    private static void TestTable(string sIndent, LuaTable table, KeyValuePair<object, object>[] tvs, ref bool lFailed)
    {
      bool[] lTested = new bool[tvs.Length];
      Array.Clear(lTested, 0, lTested.Length);

      foreach (var tv in table)
      {
				if (String.Compare(tv.Key as string, LuaTable.csMetaTable) == 0)
					continue;

        int iIndex = Array.FindIndex(tvs, c => Object.Equals(tv.Key, c.Key));
        if (iIndex == -1)
        {
          Console.WriteLine(sIndent + "Key not found: {0}", FormatValue(tv.Key));
          lFailed |= true;
        }
        else
        {
          if (tv.Value is LuaTable && tvs[iIndex].Value is KeyValuePair<object, object>[])
          {
            Console.WriteLine(sIndent + "Table[{0}] {{", FormatValue(tv.Key));
            TestTable(sIndent + "  ", (LuaTable)tv.Value, (KeyValuePair<object, object>[])(tvs[iIndex].Value), ref lFailed);
            Console.WriteLine(sIndent + "}");
          }
          else
          {
            bool lOk = Object.Equals(tvs[iIndex].Value, tv.Value);
            Console.WriteLine(sIndent + "[{0}]: {1} {2} {3}", FormatValue(tv.Key), FormatValue(tv.Value), lOk ? "==" : "!=", FormatValue(tvs[iIndex].Value));
            lFailed |= !lOk;
          }
          lTested[iIndex] = true;
        }
      }

      for (int i = 0; i < lTested.Length; i++)
        if (!lTested[i])
        {
          Console.WriteLine(sIndent + "  Key not tested: {0}", FormatValue(tvs[i].Key));
          lFailed |= true;
        }
    } // proc TestTable

    public static KeyValuePair<object, object>[] Table(params KeyValuePair<object, object>[] tv)
    {
      return tv;
    } // func Table

    public static KeyValuePair<object, object> TV(object item, object value)
    {
      return new KeyValuePair<object, object>(item, value);
    } // func TV

    public object[] NullResult { get { return new object[] { null }; } }
  } // class TestHelper
}
