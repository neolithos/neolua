using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.IronLua;

namespace LuaDLR.Test
{
	[TestClass]
	public class LuaLibs : TestHelper
	{
		[TestMethod]
		public void TestExecute01()
		{
			TestCode("return os.execute('dir c:\')", true, "exit", 0);
		}

		[TestMethod]
		public void TestExecute02()
		{
			TestCode("return os.execute('\"cmd.exe\" /c dir c:\')", true, "exit", 0);
		}
		[TestMethod]
		public void TestExecute03()
		{
			string sBatch = Path.Combine(Path.GetDirectoryName(typeof(LuaLibs).Assembly.Location), "Lua", "Echo.bat");
			ProcessStartInfo psi = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "cmd.exe"), "/c " + sBatch + " 2>&1");
			psi.RedirectStandardOutput = true;
			psi.UseShellExecute = false;
			psi.CreateNoWindow = true;
			using (Process p = Process.Start(psi))
			{
				string sLine;
				while ((sLine = p.StandardOutput.ReadLine()) != null)
					Debug.Print(sLine);

				Debug.Print("ExitCode: {0}", p.ExitCode);
			}
		}

		private static string GetEchoBatch()
		{
			var f = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(typeof(LuaLibs).Assembly.Location), "..\\..\\..\\Lua", "Echo.bat"));
			return File.Exists(f)
				? f
				: throw new FileNotFoundException("Batch file missing.", f);
		} // func GetEchoBatch

		private const string echoOutput = "\nThis text goes to Standard Output\n\nLine";

		[TestMethod]
		public void TestExecute04()
		{
			TestCode(Lines(
				String.Format("do (f = io.popen([[{0}]], 'r+'))", GetEchoBatch()),
				"  local s = '';",
				"  while true do",
				"    local l = f:read();",
				"    if l ~= nil then",
				"      s = s .. '\\n' .. l;",
				"    else",
				"      break;",
				"    end;",
				"  end;",
				"  return f:close(), s",
				"end;"
			), 0, echoOutput);
		}

		[TestMethod]
		public void TestExecute05()
		{
			TestCode(Lines(
				String.Format("do (f = io.popen([[{0}]], 'r+'))", GetEchoBatch()),
				"  local s = '';",
				"  for l in f:lines() do",
				"    s = s .. '\\n' .. l;",
				"  end;",
				"  return f:close(), s",
				"end;"
			), 0, echoOutput);
		}

		[TestMethod]
		public void TestRegEx01()
		{
			TestCode(
				Lines(
					"s = [[",
					"(Standard)",
					"(() Double Brackets ())",
					"( Across Lines",
					")",
					"(()  Double Brackets Across Lines",
					"())",
					"(2 in) (A Line)",
					"Before(Inside)After",
					"MissMatch (found))",
					")Not Matched(",
					"MissMatch2 ((found2)",
					"]]",
					"local t = '';",
					"for k in string.gmatch(s,'%b()') do",
					"  print(k)",
					"  t = t .. k .. '\\n'",
					"end",
					"return t"
				),
				String.Join("\n",
					"(Standard)",
					"(() Double Brackets ())",
					"( Across Lines",
					")",
					"(()  Double Brackets Across Lines",
					"())",
					"(2 in)",
					"(A Line)",
					"(Inside)",
					"(found)",
					"(found2)",
					""
				)
			);
		}

		[TestMethod]
		public void TestFind01()
		{
			TestCode("return string.find('fsdfsdF/R/1/EP', '.*/.*/.*/.*')", 1, 14);
		}

		[TestMethod]
		public void TestFind02()
		{
			TestCode("return string.find('fsdfsdF/R/1/EP', '.*/.*/.*/(.*)')", 1, 14, "EP");
		}

		[TestMethod]
		public void TestFind03()
		{
			TestCode("return string.find('fsdfsdF/R/1/EP', '.*/(.*)/.*/(.*)')", 1, 14, "R", "EP");
		}

		[TestMethod]
		public void TestFind04()
		{
			TestCode("return string.find('   abc', '%a+');", 4, 6);
		} // proc TestRuntimeLua13

		[TestMethod]
		public void TestMatch01()
		{
			TestCode("return string.match('(abc=123)', '%(([%s%a%d%-,=%_]*)');", "abc=123");
		} // proc TestRuntimeLua13

		[TestMethod]
		public void TestLoad01()
		{
			TestCode(Lines(
				//"local fn = function(...) local a, b = ...; return a, b; end;",
				"local fn = load('local a, b = ...; return a, b;');",
				"return fn(23,42);"), 
				23, 42
			);
		}

		[TestMethod]
		public void TestRequire01()
		{
			// Lua should find a module by replacing '?' with the module name in the package path.
			Directory.CreateDirectory("lua_module");
			File.WriteAllText(Path.Combine("lua_module", "ReqModule.lua"), "function foo() return 'bar' end");

			TestCode(Lines(
				"package.path = package.path..';./lua_module/?.lua'",
				"require('ReqModule');",
				"return foo();"),
				"bar"
			);
		}

		[TestMethod]
		public void TestRequire02()
		{
			// Lua should find a module by searching for the module in package path directories
			Directory.CreateDirectory("lua_module");
			File.WriteAllText(Path.Combine("lua_module", "ReqModule.lua"), "function foo() return 'bar' end");

			TestCode(Lines(
					"package.path = package.path..';./lua_module'",
					"require('ReqModule');",
					"return foo();"),
				"bar"
			);
		}

		[TestMethod]
		public void TestRequire03()
		{
			// Lua should find a module by replacing '?' with the module name in the environment LUA_PATH.
			Directory.CreateDirectory("lua_module");
			File.WriteAllText(Path.Combine("lua_module", "ReqModule.lua"), "function foo() return 'bar' end");
			Environment.SetEnvironmentVariable("LUA_PATH", "./lua_module/?.lua");
			TestCode(Lines(
			
					"require('ReqModule');",
					"return foo();"),
				"bar"
			);
		}

		[DataTestMethod]
		[DataRow("%a", "\\p{L}")]
		[DataRow("%%", "%")]
		[DataRow("%_", "_")]
		[DataRow("%(([%s%a%d%-,=%_]*)", @"\(([\s\p{L}\d\-,=_]*)")]
		public void CanTranslatesRegularExpression(string from, string to)
		{
			var (translatedPattern, _) = LuaLibraryString.TranslateRegularExpression(from);
			Assert.AreEqual(to, translatedPattern);
			// ensure it parses
			var re = new Regex(translatedPattern);
		}

		
		[DataTestMethod]
		[DataRow("kt foobar win64 ", "(kt)%s+(foobar[a-zA-Z]*)%s+([%a%d_]+)%s+", 2, "win64")]
		[DataRow("kt foobar win64 LKG_AutoPlayer_Tests:Autoplayer_", "(kt)%s+(foobar[a-zA-Z]*)%s+([%a%d_]+)%s+", 2, "win64")]
		// 
		public void CanMatchRegularExpression(string line, string pattern, int resultIndex, string expected)
		{
			var res = LuaLibraryString.match(line, pattern);
			Assert.AreEqual(expected, res[resultIndex]);
		}
	}
}