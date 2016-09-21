using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.IronLua;

namespace LuaDLR.Test
{
	[TestClass]
	public class Runtime : TestHelper
	{
		private class LuaGlobalNew : LuaGlobal
		{
			public LuaGlobalNew(Lua l)
			  : base(l)
			{
			}

			[LuaMember("LogInfo")]
			public int LogInfo(string sText)
			{
				Console.WriteLine(sText);
				return 3;
			}

			[LuaMember("BoolProperty")]
			public bool BoolProperty { get; set; }
		}

		public class SubClass
		{
			public SubClass(byte a)
			{
				this.Value = a;
			}

			public byte Value { get; set; }
		} // class SubClass

		[TestMethod]
		public void TestRuntimeClrProperty01()
		{
			TestCode("return clr.System.Environment.NewLine;", Environment.NewLine);
		} // proc TestRuntimeClrProperty

		[TestMethod]
		public void TestRuntimeClrMethod01()
		{
			TestCode("return clr.System.Convert:ToInt32(cast(short, 2));", 2);
		} // proc TestRuntimeClrMethod

		[TestMethod]
		public void TestRuntimeClrMethod02()
		{
			TestCode(Lines(
			  "function a()",
			  "  return 'Hallo ', 'Welt', '!';",
			  "end;",
			  "return clr.System.String:Concat(a());"),
			  "Hallo Welt!");
		} // proc TestRuntimeClrMethod

		[TestMethod]
		public void TestRuntimeClrMethod03()
		{
			TestCode(Lines(
			  "function a()",
			  "  return 'Hallo ', 'Welt', '!';",
			  "end;",
			  "return clr.System.String:Concat('Text', ': ', a());"),
			  "Text: Hallo Welt!");
		} // proc TestRuntimeClrMethod

		[TestMethod]
		public void TestRuntimeMethod01()
		{
			TestCode(Lines(
				"function test()",
				"    local t = {};",
				"    table.insert(t, 'a');",
				"    table.insert(t, 'b');",
				"    table.insert(t, 'c');",
				"    table.insert(t, 'd');",
				"    return t;",
				"end;",
				"local t = test();",
				"return #t, #test();"), 4, 4);
		}

		[TestMethod]
		public void TestRuntimeClrClass01()
		{
			TestCode("local a = clr.LuaDLR.Test.TestParam:ctor(); return a:GetValue();", 4);
		} // proc TestRuntimeClrClass01

		[TestMethod]
		public void TestRuntimeClrClass02()
		{
			TestCode("local a = clr.LuaDLR.Test.Runtime.SubClass:ctor(4); return a.Value;", (byte)4);
		} // proc TestRuntimeClrClass02

		[TestMethod]
		public void TestRuntimeClrClass04()
		{
			TestCode("return clr.System.Text.StringBuilder:GetType();", typeof(StringBuilder));
		} // proc TestRuntimeClrClass04

		[TestMethod]
		public void TestRuntimeLua01()
		{
			TestCode("print('Hallo Welt');");
		} // proc TestRuntimeLua01

		[TestMethod]
		public void TestRuntimeLua02()
		{
			TestCode("local p = print; print = function() p('Hallo Welt'); end; print();");
		} // proc TestRuntimeLua02

		[TestMethod]
		public void TestRuntimeLua03()
		{
			TestCode("return cast(int, math.abs(-1));", 1);
		} // proc TestRuntimeLua03

		[TestMethod]
		public void TestRuntimeLua04()
		{
			TestCode("return string.byte('hallo', 2, 4);", 97, 108, 108);
		} // proc TestRuntimeLua04

		[TestMethod]
		public void TestRuntimeLua05()
		{
			TestCode("return string.byte('hallo', 2);", 97);
		} // proc TestRuntimeLua05

		[TestMethod]
		public void TestRuntimeLua06()
		{
			TestCode("return 'hallo':Substring(1, 3);", "all");
		} // proc TestRuntimeLua06

		[TestMethod]
		public void TestRuntimeLua07()
		{
			TestCode("return 'hallo'[1];", 'a');
		} // proc TestRuntimeLua07

		[TestMethod]
		public void TestRuntimeLua08()
		{
			TestCode("return string.sub('hallo', 3);", "llo");
			TestCode("return string.sub('hallo', 10);", "");
			TestCode("return string.sub('hallo', 3, 4);", "ll");
			TestCode("return string.sub('hallo', -3);", "llo");
			TestCode("return string.sub('hallo', -3, -2);", "ll");
		} // proc TestRuntimeLua08

		[TestMethod]
		public void TestRuntimeLua09()
		{
			TestCode("return bit32.extract(0xFF00, 8, 8);", (uint)0xFF);
			TestCode("return bit32.replace(0x0FFF, -1, 8, 8);", (uint)0xFFFF);
		} // proc TestRuntimeLua09

		[TestMethod]
		public void TestRuntimeLua10()
		{
			TestCode("return string.format('%d', 8);", "8");
		} // proc TestRuntimeLua10

		[TestMethod]
		public void TestRuntimeLua11()
		{
			TestCode(GetLines("Lua.Runtime11.lua"), 4, "helloworldfromLua");
		} // proc TestRuntimeLua11

		[TestMethod]
		public void TestRuntimeLua12()
		{
			TestCode(GetLines("Lua.Runtime12.lua"), 2, "fromworldtoLua");
		} // proc TestRuntimeLua12

		[TestMethod]
		public void TestRuntimeLua14()
		{
			using (Lua l = new Lua())
			{
				var g = l.CreateEnvironment();
				l.PrintExpressionTree = Console.Out;
				g.RegisterPackage("debug", typeof(System.Diagnostics.Debug));
				g.DoChunk("debug:Print('Hallo World!');", "test.lua");
			}
		} // proc TestRuntimeLua13

		[TestMethod]
		public void TestRuntimeLua15()
		{
			TestCode(Lines("return string.gsub('192.168.33.15', '[0-9]+', 'x');"),
				"x.x.x.x", 4);
		}

		[TestMethod]
		public void TestGlobalMember01()
		{
			using (Lua l = new Lua())
			{
				dynamic g = new LuaGlobalNew(l);
				TestResult(g.dochunk("return LogInfo('Hello');"), 3);
			}
		}

		[TestMethod]
		public void TestToNumber01()
		{
			TestCode(Lines(
				"function t() return '8'; end;",
				"return tonumber(t());"
				), 8);
		}

		[TestMethod]
		public void TestDateTime01()
		{
			using (Lua l = new Lua())
			{
				dynamic g = l.CreateEnvironment<LuaGlobal>();

				g.dochunk("print(os.date('Today is %A, in %B'))");

				TestResult(g.dochunk("return os.date('%x', 906000490)"), new DateTime(1998, 09, 17).ToString("d"));
				TestResult(g.dochunk("return os.date('%d.%m.%Y')"), DateTime.Today.ToString("d"));
				g.dochunk("t = os.date('*t');");
				DateTime dt = DateTime.Now;
				TestResult(g.dochunk("return t.year"), dt.Year);
				TestResult(g.dochunk("return t.month"), dt.Month);
				TestResult(g.dochunk("return t.day"), dt.Day);
				TestResult(g.dochunk("return t.hour"), dt.Hour);
				TestResult(g.dochunk("return t.min"), dt.Minute);
				TestResult(g.dochunk("return t.sec"), dt.Second);
				TestResult(g.dochunk("return t.wday"), (int)dt.DayOfWeek);
				TestResult(g.dochunk("return t.yday"), dt.DayOfYear);
				TestResult(g.dochunk("return t.isdst"), true);
				g.dochunk("t = os.date('!*t');");
				dt = DateTime.UtcNow;
				TestResult(g.dochunk("return t.year"), dt.Year);
				TestResult(g.dochunk("return t.month"), dt.Month);
				TestResult(g.dochunk("return t.day"), dt.Day);
				TestResult(g.dochunk("return t.hour"), dt.Hour);
				TestResult(g.dochunk("return t.min"), dt.Minute);
				TestResult(g.dochunk("return t.sec"), dt.Second);
				TestResult(g.dochunk("return t.wday"), (int)dt.DayOfWeek);
				TestResult(g.dochunk("return t.yday"), dt.DayOfYear);
				TestResult(g.dochunk("return t.isdst"), false);

				TestResult(g.dochunk("return os.date()"), DateTime.Now.ToString("G"));

				g.dochunk("t={};t.year = 2001; print(os.time(t))");
			}
		}

		[TestMethod]
		public void TestGlobalProperty01()
		{
			using (Lua l = new Lua())
			{
				var g = l.CreateEnvironment<LuaGlobalNew>();

				g.DoChunk("BoolProperty = true", "test1.lua");
				TestResult(g.DoChunk("return BoolProperty", "test2.lua"), true);
			}
		}

		[TestMethod]
		public void TestConvert01()
		{
			var t = new LuaTable();
			t["DataType"] = typeof(StringBuilder);
			var r = Lua.RtConvertValue(t, typeof(LuaDLR.Test.LuaTypeTests.DataTypeTest));
			Assert.AreEqual(typeof(LuaTypeTests.DataTypeTest), r.GetType());
			Assert.AreEqual(typeof(StringBuilder), ((LuaTypeTests.DataTypeTest)r).DataType);
		}

		[TestMethod]
		public void RegexTest01()
		{
			TestCode(Lines("return string.match('1.2.0', '^(%d+)%.?(%d)%.?(%d)(.-)$')"), "1", "2", "0", "");
			TestCode(Lines("return string.match('1.2.0-beta', '^(%d+)%.?(%d)%.?(%d)(.-)$')"), "1", "2", "0", "-beta");
			TestCode(Lines("return string.match('1.2-alpha', '^(%d+)%.?(%d)(.-)$')"), "1", "2", "-alpha");
		}

		[TestMethod]
		public void RegexTesta()
		{
			TestCode(Lines("return string.match('hello . world', '^(%a+) (%p?) (%a+)$')"), "hello", ".", "world");//a, p
			TestCode(Lines("return string.match('hello   world', '^(%a+) (%P?) (%a+)$')"), "hello", " ", "world");// P
		}

		[TestMethod]
		public void RegexTestA()
		{
			TestCode(Lines("return string.match('!.?.#', '^(%A{1})%.?(%A{1})%.?(%A{1})$')"), "!", "?", "#");//A
		}

		[TestMethod]
		public void RegexTestu()
		{
			TestCode(Lines("return string.match('HELLO WORLD', '^(%u+) (%u+)$')"), "HELLO", "WORLD");//u
		}

		[TestMethod]
		public void RegexTestU()
		{
			TestCode(Lines("return string.match('hello world', '^(%U+) (%U+)$')"), "hello", "world");//U
		}

		[TestMethod]
		public void RegexTestl()
		{
			TestCode(Lines("return string.match('hello world', '^(%l+)(%s?)(%l+)$')"), "hello", " ", "world");//l
		}

		[TestMethod]
		public void RegexTestL()
		{
			TestCode(Lines("return string.match('HELLO . WORLD', '^(%L+) (%S?) (%L+)$')"), "HELLO", ".", "WORLD");//L
		}


		[TestMethod]
		public void RegexTestx()
		{
			TestCode(Lines("return string.match('1A.2B.3C', '^(%x{2})%.?(%x{2})%.?(%x{2})$')"), "1A", "2B", "3C");//x
		}

		[TestMethod]
		public void RegexTestX()
		{
			TestCode(Lines("return string.match('zz.zz.zz', '^(%X{2})%.?(%X{2})%.?(%X{2})$')"), "zz", "zz", "zz");//X
		}

		[TestMethod]
		public void RegexTestg()
		{
			TestCode(Lines("return string.match('Hello World!?', '^(%g+) (%g+)$')"), "Hello", "World!?");//g
		}

		[TestMethod]
		public void RegexTestG()
		{
			TestCode(Lines("return string.match('\x1B', '^(%G+)$')"), "\x1B");//G
		}

		[TestMethod]
		public void RegexTestc()
		{
			TestCode(Lines("return string.match('\x1B', '^(%c+)$')"), "\x1B");//c
		}

		[TestMethod]
		public void RegexTestC()
		{
			TestCode(Lines("return string.match('Hello World!?', '^(%C+) (%C+)$')"), "Hello", "World!?");//C
		}

		[TestMethod]
		public void RegexTestw()
		{
			TestCode(Lines("return string.match('11.AA.2B', '^(%w{2})%.?(%w{2})%.?(%w{2})$')"), "11", "AA", "2B");//w
		}

		[TestMethod]
		public void RegexTestW()
		{
			TestCode(Lines("return string.match('!.?.#', '^(%W{1})%.?(%W{1})%.?(%W{1})$')"), "!", "?", "#");//W
		}

		[TestMethod]
		public void RegexTests()
		{
			TestCode(Lines("return string.match(' ', '^(%s+)$')"), " ");//s
		}

		[TestMethod]
		public void RegexTestS()
		{
			TestCode(Lines("return string.match('a', '^(%S+)$')"), "a");//S
		}

		[TestMethod]
		public void RegexTestb()
		{
			TestCode(Lines("return string.match('(123)', '^(%b())$')"), "(123)", "(123)", "");//b
		}

		[TestMethod]
		public void RegexTest02()
		{
			TestCode(Lines("return string.match('HELLO world!? 2C.\x1B.#.1', '^(%u+) (%g+) (%x+)%.?(%c+)%.?(%A{1})%.?(%d{1})$')"), "HELLO", "world!?", "2C", "\x1B", "#", "1");//u, g, x, c, A, d
		}
	}
 } //class Runtime 


