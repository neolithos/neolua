using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.IronLua;

namespace LuaDLR.Test
{
	[TestClass]
	public class Exceptions : TestHelper
	{
		[TestMethod]
		public void Exception01()
		{
			using (Lua l = new Lua())
			{
				var g = l.CreateEnvironment();
				try
				{
					l.PrintExpressionTree = PrintExpressionTree ? Console.Out : null;
					g.DoChunk(l.CompileChunk("\nNull(a, a);", "test.lua", Lua.StackTraceCompileOptions, new KeyValuePair<string, Type>("a", typeof(int))), 1);
				}
				catch (Exception e)
				{
					var d = LuaExceptionData.GetData(e);
					Assert.IsTrue(d[2].LineNumber == 2);
				}
			}
		} // proc Exception01

		[TestMethod]
		public void Exception02()
		{
			using (Lua l = new Lua())
			{
				var g = l.CreateEnvironment();
				try
				{
					l.PrintExpressionTree = PrintExpressionTree ? Console.Out : null;
					g.DoChunk(l.CompileChunk("math.abs(-1 / a).A();", "test.lua", Lua.StackTraceCompileOptions, new KeyValuePair<string, Type>("a", typeof(int))), 1);
				}
				catch (Exception e)
				{
					var d = LuaExceptionData.GetData(e);
					Debug.Print("Error: {0}", e.Message);
					Debug.Print("Error at:\n{0}", d.StackTrace);
					Assert.IsTrue(d[2].LineNumber == 1); //  && d[2].ColumnNumber == 18 in release this is one
				}
			}
		} // proc Exception01
	} // class Runtime
}
