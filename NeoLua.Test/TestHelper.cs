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
			=> String.Join(Environment.NewLine, lines);
	
		public string GetLines(string sName)
		{
			using (var src = typeof(TestHelper).Assembly.GetManifestResourceStream(typeof(TestHelper), sName))
			using (var tr = new StreamReader(src))
				return tr.ReadToEnd();
		} // func GetLines

		public void TestCode(string code, params object[] expectedResult)
		{
			using (var l = new Lua())
			{
				l.PrintExpressionTree = PrintExpressionTree ? Console.Out : null;
				var g = l.CreateEnvironment<LuaGlobal>();
				Console.WriteLine("Test: {0}", code);
				Console.WriteLine(new string('=', 66));
				var sw = new Stopwatch();
				sw.Start();
				TestResult(g.DoChunk(code, "test.lua"), expectedResult);
				Console.WriteLine("  Dauer: {0}ms", sw.ElapsedMilliseconds);
				Console.WriteLine();
				Console.WriteLine();
			}
		} // proc TestCode

		public void TestExpr(string expression, params object[] expectedResult)
			=> TestCode("return " + expression + ";", expectedResult);
	
		private static string FormatValue(object v)
			=> String.Format("({0}){1}", v == null ? "object" : v.GetType().Name, v);

		public static void TestResult(LuaResult result, params object[] expectedResult)
		{
			if (result == null)
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
				for (var i = 0; i < expectedResult.Length; i++)
				{
					var valueResult = result[i];
					var valueExpected = expectedResult[i];

					if (valueResult is double)
						valueResult = Math.Round((double)valueResult, 3);
					if (valueExpected is double)
						valueExpected = Math.Round((double)valueExpected, 3);

					if (valueResult is LuaTable && valueExpected is KeyValuePair<object, object>[])
						TestTable((LuaTable)valueResult, (KeyValuePair<object, object>[])valueExpected);
					else
					{
						var lOk = Object.Equals(valueResult, valueExpected);
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
			var failed = false;
			Console.WriteLine("Table {");
			TestTable("  ", table, tvs, ref failed);
			Console.WriteLine("}");
			Assert.IsFalse(failed);
		} // proc TestTable

		private static void TestTable(string sIndent, LuaTable table, KeyValuePair<object, object>[] tvs, ref bool failed)
		{
			var isTested = new bool[tvs.Length];
			Array.Clear(isTested, 0, isTested.Length);

			foreach (var tv in table)
			{
				if (String.Compare(tv.Key as string, LuaTable.csMetaTable) == 0)
					continue;

				var idx = Array.FindIndex(tvs, c => Object.Equals(tv.Key, c.Key));
				if (idx == -1)
				{
					Console.WriteLine(sIndent + "Key not found: {0}", FormatValue(tv.Key));
					failed |= true;
				}
				else
				{
					if (tv.Value is LuaTable && tvs[idx].Value is KeyValuePair<object, object>[])
					{
						Console.WriteLine(sIndent + "Table[{0}] {{", FormatValue(tv.Key));
						TestTable(sIndent + "  ", (LuaTable)tv.Value, (KeyValuePair<object, object>[])(tvs[idx].Value), ref failed);
						Console.WriteLine(sIndent + "}");
					}
					else
					{
						var lOk = Object.Equals(tvs[idx].Value, tv.Value);
						Console.WriteLine(sIndent + "[{0}]: {1} {2} {3}", FormatValue(tv.Key), FormatValue(tv.Value), lOk ? "==" : "!=", FormatValue(tvs[idx].Value));
						failed |= !lOk;
					}
					isTested[idx] = true;
				}
			}

			for (var i = 0; i < isTested.Length; i++)
			{
				if (!isTested[i])
				{
					Console.WriteLine(sIndent + "  Key not tested: {0}", FormatValue(tvs[i].Key));
					failed |= true;
				}
			}
		} // proc TestTable

		public static KeyValuePair<object, object>[] Table(params KeyValuePair<object, object>[] tv)
			=> tv;

		public static KeyValuePair<object, object> TV(object item, object value)
			=> new KeyValuePair<object, object>(item, value);

		public object[] NullResult => new object[] { null };
	} // class TestHelper
}
