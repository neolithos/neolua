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

		[TestMethod]
		public void TestExecute04()
		{
			string sBatch = Path.Combine(Path.GetDirectoryName(typeof(LuaLibs).Assembly.Location), "Lua", "Echo.bat");
			string sCode = String.Join(Environment.NewLine,
				String.Format("do (f = io.popen([[{0}]], 'r+'))", sBatch),
				"while true do",
				"  local l = f:read();",
				"  if l ~= nil then",
				"    print(l);",
				"  else",
				"    break;",
				"  end;",
				"end;",
				"end;"
			);
			TestCode(sCode);
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
	}
}
