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
	[TestClass]
	public class RunLuaTests : TestHelper
	{
		private object TestAssert(object test, string sMessage)
		{
			if (!(bool)Lua.RtConvertValue(test, typeof(bool)))
			{
				LuaStackFrame frame = LuaExceptionData.GetStackTrace(new StackTrace(0, true)).FirstOrDefault(c => c.Type == LuaStackFrameType.Lua);
				if (frame == null)
					Assert.IsTrue(false, "Test failed (unknown position) " + sMessage);
				else
					Assert.IsTrue(false, "Test failed at line {0}, column {1}, file {2} {3}", frame.LineNumber, frame.ColumnNumber, frame.FileName, sMessage);
			}
			return test;
		}

		private void DoScript(Lua l, LuaGlobal g, string sScript)
		{
			Type type = typeof(RunLuaTests);
			using (Stream src = type.Assembly.GetManifestResourceStream(type, "CompTests." + sScript))
			using (StreamReader sr = new StreamReader(src))
			{
				Console.WriteLine();
				Console.WriteLine(new string('=', 60));
				Console.WriteLine("= " + sScript);
				Console.WriteLine();
				try
				{
					g.DoChunk(l.CompileChunk(sr, sScript, Lua.DefaultDebugEngine));
				}
				catch (Exception e)
				{
					Console.WriteLine("StackTrace:");
					Console.WriteLine(LuaExceptionData.GetData(e).StackTrace);
					throw;
				}
			}
		}

		[TestMethod]
		public void RunTest()
		{
			using (Lua l = new Lua())
			{
				var g = l.CreateEnvironment();
				dynamic dg = g;

				dg.math.randomseed(0);
				dg.assert = new Func<object, string, object>(TestAssert);

				//DoScript(l, g, "calls.lua");
				//DoScript(l, g, "strings.lua");
				//DoScript(l, g, "literals.lua");
				
				//DoScript(l, g, "bitwise.lua");
			}
		}

		[TestMethod]
		public void TestSingle01()
		{
			TestCode("return 0xfffffffffffff800", 0xfffffffffffff800);
		}

		[TestMethod]
		public void TestSingle02()
		{
			TestCode(Lines(
				"function deep(n)",
				"  if n > 0 then return deep(n-1); else return 101; end;",
 				"end;",
				"return deep(3000);"), 101);
		}

		[TestMethod]
		public void TestSingle03()
		{
			TestCode("a = nil; (function (x) a = x end)(23) return a", 23);
		}
	}
}
