using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

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
	}
}
