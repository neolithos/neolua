using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.IronLua;

namespace LuaDLR.Test
{
	[TestClass]
	public class Parser : TestHelper
	{
		#region -- TokenTest ----------------------------------------------------------

		private KeyValuePair<LuaToken, string> T(LuaToken t, string v)
		{
			return new KeyValuePair<LuaToken, string>(t, v);
		} // func T

		private void TokenTest(ILuaLexer lex, params KeyValuePair<LuaToken, string>[] expectedTokens)
		{
			using (lex)
			{
				lex.Next();

				for (var i = 0; i < expectedTokens.Length; i++)
				{
					Debug.Write(String.Format("Test: {0} = {1} ==>", lex.Current.Typ, expectedTokens[i].Key));
					if (lex.Current.Typ != expectedTokens[i].Key)
					{
						Debug.WriteLine("tokens FAILED");
						Assert.Fail();
					}
					else if (lex.Current.Value != expectedTokens[i].Value)
					{
						Debug.WriteLine("values '{0}' != '{1}'   FAILED", lex.Current.Value, expectedTokens[i].Value);
						Assert.Fail();
					}
					Debug.WriteLine("OK");
					lex.Next();
				}
				if (lex.Current.Typ != LuaToken.Eof)
					Assert.Fail($"Invalid token {lex.Current.Typ} (Expected: Eof)");
			}
		} // func TokenTest

		private ILuaLexer CreateLuaLexer(string lines)
			=> LuaLexer.Create("test.lua", new StringReader(lines));

		private ILuaLexer CreateHtmlLexer(string lines)
			=> LuaLexer.CreateHtml(new LuaCharLexer("test.lua", new StringReader(lines), LuaLexer.HtmlCharStreamLookAHead));

		private void LuaTokenTest(string lines, params KeyValuePair<LuaToken, string>[] expectedTokens)
			=> TokenTest(CreateLuaLexer(lines), expectedTokens);

		private void HtmlTokenTest(string lines, params KeyValuePair<LuaToken, string>[] expectedTokens)
			=> TokenTest(CreateHtmlLexer(lines), expectedTokens);

		#endregion

		#region -- Basic Token Test ---------------------------------------------------

		[TestMethod]
		public void TokenTest()
		{
			LuaTokenTest("°", T(LuaToken.InvalidChar, "°"));
			LuaTokenTest("'h", T(LuaToken.InvalidString, "h"));
			LuaTokenTest("--[[ a", T(LuaToken.InvalidComment, String.Empty));

			LuaTokenTest("Hallo", T(LuaToken.Identifier, "Hallo"));
			LuaTokenTest("'Hallo'", T(LuaToken.String, "Hallo"));
			LuaTokenTest("\"Hallo\"", T(LuaToken.String, "Hallo"));
			LuaTokenTest("[[Hallo]]", T(LuaToken.String, "Hallo"));
			LuaTokenTest("2", T(LuaToken.Number, "2"));
			LuaTokenTest("0xA3", T(LuaToken.Number, "0xA3"));

			LuaTokenTest("and break cast const do else elseif end false for foreach function goto if in local nil not or repeat return then true until while",
				T(LuaToken.KwAnd, "and"),
				T(LuaToken.KwBreak, "break"),
				T(LuaToken.KwCast, "cast"),
				T(LuaToken.KwConst, "const"),
				T(LuaToken.KwDo, "do"),
				T(LuaToken.KwElse, "else"),
				T(LuaToken.KwElseif, "elseif"),
				T(LuaToken.KwEnd, "end"),
				T(LuaToken.KwFalse, "false"),
				T(LuaToken.KwFor, "for"),
				T(LuaToken.KwForEach, "foreach"),
				T(LuaToken.KwFunction, "function"),
				T(LuaToken.KwGoto, "goto"),
				T(LuaToken.KwIf, "if"),
				T(LuaToken.KwIn, "in"),
				T(LuaToken.KwLocal, "local"),
				T(LuaToken.KwNil, "nil"),
				T(LuaToken.KwNot, "not"),
				T(LuaToken.KwOr, "or"),
				T(LuaToken.KwRepeat, "repeat"),
				T(LuaToken.KwReturn, "return"),
				T(LuaToken.KwThen, "then"),
				T(LuaToken.KwTrue, "true"),
				T(LuaToken.KwUntil, "until"),
				T(LuaToken.KwWhile, "while")
			);
			LuaTokenTest("::label::",
				T(LuaToken.ColonColon, String.Empty),
				T(LuaToken.Identifier, "label"),
				T(LuaToken.ColonColon, String.Empty)
			);

			LuaTokenTest("+     -     *     /     %     ^     #    //    &    |    ~    >>    <<",
				T(LuaToken.Plus, String.Empty),
				T(LuaToken.Minus, String.Empty),
				T(LuaToken.Star, String.Empty),
				T(LuaToken.Slash, String.Empty),
				T(LuaToken.Percent, String.Empty),
				T(LuaToken.Caret, String.Empty),
				T(LuaToken.Cross, String.Empty),
				T(LuaToken.SlashShlash, String.Empty),
				T(LuaToken.BitAnd, String.Empty),
				T(LuaToken.BitOr, String.Empty),
				T(LuaToken.Dilde, String.Empty),
				T(LuaToken.ShiftRight, String.Empty),
				T(LuaToken.ShiftLeft, String.Empty)
			 );
			LuaTokenTest("==    ~=    <=    >=    <     >     =",
				T(LuaToken.Equal, String.Empty),
				T(LuaToken.NotEqual, String.Empty),
				T(LuaToken.LowerEqual, String.Empty),
				T(LuaToken.GreaterEqual, String.Empty),
				T(LuaToken.Lower, String.Empty),
				T(LuaToken.Greater, String.Empty),
				T(LuaToken.Assign, String.Empty)
			);
			LuaTokenTest("(     )     {     }     [     ]",
				T(LuaToken.BracketOpen, String.Empty),
				T(LuaToken.BracketClose, String.Empty),
				T(LuaToken.BracketCurlyOpen, String.Empty),
				T(LuaToken.BracketCurlyClose, String.Empty),
				T(LuaToken.BracketSquareOpen, String.Empty),
				T(LuaToken.BracketSquareClose, String.Empty)
			);
			LuaTokenTest(";     :     ,     .     ..    ...",
				T(LuaToken.Semicolon, String.Empty),
				T(LuaToken.Colon, String.Empty),
				T(LuaToken.Comma, String.Empty),
				T(LuaToken.Dot, String.Empty),
				T(LuaToken.DotDot, String.Empty),
				T(LuaToken.DotDotDot, String.Empty)
			);

			LuaTokenTest("a = 'alo\\n123\"'",
				T(LuaToken.Identifier, "a"),
				T(LuaToken.Assign, String.Empty),
				T(LuaToken.String, "alo\n123\"")
			);
			LuaTokenTest("a = \"alo\\n123\\\"\"",
				T(LuaToken.Identifier, "a"),
				T(LuaToken.Assign, String.Empty),
				T(LuaToken.String, "alo\n123\"")
			);
			LuaTokenTest("a = '\\97lo\\10\\04923\\\"'",
				T(LuaToken.Identifier, "a"),
				T(LuaToken.Assign, String.Empty),
				T(LuaToken.String, "alo\n123\"")
			);
			LuaTokenTest("a = [[alo\n123\"]]",
				T(LuaToken.Identifier, "a"),
				T(LuaToken.Assign, String.Empty),
				T(LuaToken.String, "alo\n123\"")
			);
			LuaTokenTest("a = [==[\nalo\n123\"]==]",
				T(LuaToken.Identifier, "a"),
				T(LuaToken.Assign, String.Empty),
				T(LuaToken.String, "alo\n123\"")
			);
			LuaTokenTest("a = [===[]==]]===]",
				T(LuaToken.Identifier, "a"),
				T(LuaToken.Assign, String.Empty),
				T(LuaToken.String, "]==]")
			);
			LuaTokenTest("and_break_cast = { const_do_else = true }",
				T(LuaToken.Identifier, "and_break_cast"),
				T(LuaToken.Assign, String.Empty),
				T(LuaToken.BracketCurlyOpen, String.Empty),
				T(LuaToken.Identifier, "const_do_else"),
				T(LuaToken.Assign, String.Empty),
				T(LuaToken.KwTrue, "true"),
				T(LuaToken.BracketCurlyClose, String.Empty)
			);

			LuaTokenTest("--[===[]==]]===]", T(LuaToken.Eof, String.Empty));
			LuaTokenTest("--[0] = ", T(LuaToken.Eof, String.Empty));
			LuaTokenTest("[== ", T(LuaToken.InvalidStringOpening, String.Empty));

			LuaTokenTest("'a\n", T(LuaToken.InvalidString, "a"));
			LuaTokenTest("'a\\\na'", T(LuaToken.String, "a\na"));
			LuaTokenTest(@"'a\g", T(LuaToken.InvalidString, "a"));
			LuaTokenTest(@"'a\x", T(LuaToken.InvalidString, "a"));
			LuaTokenTest(@"'a\xar", T(LuaToken.InvalidString, "a"));
			LuaTokenTest(@"'a\xa'", T(LuaToken.InvalidString, "a"));
			LuaTokenTest(@"'a\xaar'", T(LuaToken.String, "a\xaar"));

			LuaTokenTest("3", T(LuaToken.Number, "3"));
			LuaTokenTest("3.0", T(LuaToken.Number, "3.0"));
			LuaTokenTest("3.1416", T(LuaToken.Number, "3.1416"));
			LuaTokenTest("314.16e-2", T(LuaToken.Number, "314.16e-2"));
			LuaTokenTest("0.31416E1", T(LuaToken.Number, "0.31416E1"));

			LuaTokenTest("0xff", T(LuaToken.Number, "0xff"));
			LuaTokenTest("0x0.1E", T(LuaToken.Number, "0x0.1E"));
			LuaTokenTest("0xA23p-4", T(LuaToken.Number, "0xA23p-4"));
			LuaTokenTest("0X1.921FB54442D18P+1", T(LuaToken.Number, "0X1.921FB54442D18P+1"));
		} // proc TokenTest

		#endregion

		#region -- TestConstants ------------------------------------------------------

		private void TestConstant(Lua l, string sVarValue, object result)
		{
			Debug.Print("Test: " + sVarValue);
			var r = l.CreateEnvironment().DoChunk("return " + sVarValue + ";", "test.lua");
			Assert.AreEqual(r[0], result);
		} // func TestConstant

		[TestMethod]
		public void TestConstants()
		{
			var l = new Lua();

			TestConstant(l, "3", 3);
			TestConstant(l, "3.0", 3.0);
			TestConstant(l, "3.1416", 3.1416);
			TestConstant(l, "314.16e-2", 314.16e-2);
			TestConstant(l, "0.31416E1", 0.31416E1);
			TestConstant(l, "0e12", 0.0);
			TestConstant(l, ".0", .0);
			TestConstant(l, "0.", 0.0);
			TestConstant(l, ".2e2", 20.0);
			TestConstant(l, "2.E-1", 0.2);

			TestConstant(l, "0xff", 0xff);
			//TestVariable(l, "0x0.1E", );
			//TestVariable(l, "0xA23p-4", );
			//TestVariable(l, "0X1.921FB54442D18P+1", );
		} // proc TestConstants

		#endregion

		#region -- Test Parser --------------------------------------------------------

		[TestMethod]
		public void TestParser01()
		{
			Assert.AreEqual(Lua.RtParseNumber(null, "fffffffffffff800", 0, 16, true, false), 0xfffffffffffff800);
			Assert.AreEqual(Lua.RtParseNumber(null, "314.16e-2", 0, 10, true, false), 314.16e-2);
			Assert.AreEqual(Lua.RtParseNumber(null, "-0.1246e+4", 0, 10, true, false), -1246.0);
			Assert.AreEqual(Lua.RtParseNumber(null, "2.5", 0, 16, true, false), 2.3125);
			Assert.AreEqual(Lua.RtParseNumber(null, "  -1246", 0, 10, true, false), -1246);
			Assert.AreEqual(Lua.RtParseNumber(null, "  -123456789123456789", 0, 10, true, false), -123456789123456789);
			Assert.AreEqual(Lua.RtParseNumber(null, "  123456789123456789", 0, 10, true, false), 123456789123456789);
			Assert.AreEqual(Lua.RtParseNumber(null, "110", 0, 2, true, false), 6);
			Assert.AreEqual(Lua.RtParseNumber(null, "-1111", 0, 2, true, false), -15);
			Assert.AreEqual(Lua.RtParseNumber(null, "FF", 0, 16, true, false), 255);
		}

		[TestMethod]
		public void TestParserIssue55()
		{
			using (var l = new Lua())
			{
				var g = l.CreateEnvironment();
				var r = g.DoChunk("repeat break if true then end until true return 23", "TestParserIssue55");
				Assert.AreEqual(23, r[0]);
			}
		}

		[TestMethod]
		public void TestParserIssue92()
		{
			TestCode(Lines(
				"styles = {",
				"  ['SubStyle'] = {",
				"    [0] = 'zoop',",
				"    --[1] = 'removed',",
				"  }",
				"};",
				"return styles.SubStyle[0];"),
				"zoop"
			);
		}

		[TestMethod]
		public void TestClrDisabled()
		{
			string code = "return type(clr) == type(nil);";
			using (var l = new Lua())
			{
				l.PrintExpressionTree = PrintExpressionTree ? Console.Out : null;
				var g = l.CreateEnvironment<LuaGlobal>();
				g.DefaultCompileOptions = new LuaCompileOptions()
				{
					ClrEnabled = false
				};
				Console.WriteLine("Test: {0}", code);
				Console.WriteLine(new string('=', 66));
				var sw = new Stopwatch();
				sw.Start();
				TestResult(g.DoChunk(code, "test.lua"), true);
				Console.WriteLine("  Dauer: {0}ms", sw.ElapsedMilliseconds);
				Console.WriteLine();
				Console.WriteLine();
			}
		}

		[TestMethod]
		public void TestClrEnabled()
		{
			// As the type of the clr object could change, let's air on the side of safety
			// and only check if it exists as it could be anything
			string code = "return type(clr) == type(nil);";
			using (var l = new Lua())
			{
				l.PrintExpressionTree = PrintExpressionTree ? Console.Out : null;
				var g = l.CreateEnvironment<LuaGlobal>();
				g.DefaultCompileOptions = new LuaCompileOptions()
				{
					ClrEnabled = true
				};
				Console.WriteLine("Test: {0}", code);
				Console.WriteLine(new string('=', 66));
				var sw = new Stopwatch();
				sw.Start();
				TestResult(g.DoChunk(code, "test.lua"), false);
				Console.WriteLine("  Dauer: {0}ms", sw.ElapsedMilliseconds);
				Console.WriteLine();
				Console.WriteLine();
			}
		}

		#endregion

		#region -- Test Position ------------------------------------------------------

		[TestMethod]
		public void TestPosition01()
		{
			var t = Lines("return", "  break", "  'a'");
			using (var lex = CreateLuaLexer(t))
			{
				lex.Next();

				Assert.AreEqual(1, lex.Current.Start.Line);
				Assert.AreEqual(1, lex.Current.Start.Col);
				Assert.AreEqual(0, lex.Current.Start.Index);

				Assert.AreEqual(1, lex.Current.End.Line);
				Assert.AreEqual(7, lex.Current.End.Col);
				Assert.AreEqual(6, lex.Current.End.Index);
				Assert.AreEqual("return", t.Substring((int)lex.Current.Start.Index, (int)lex.Current.End.Index - (int)lex.Current.Start.Index));

				lex.Next();

				Assert.AreEqual(2, lex.Current.Start.Line);
				Assert.AreEqual(3, lex.Current.Start.Col);
				Assert.AreEqual(10, lex.Current.Start.Index);

				Assert.AreEqual(2, lex.Current.End.Line);
				Assert.AreEqual(8, lex.Current.End.Col);
				Assert.AreEqual(15, lex.Current.End.Index);
				Assert.AreEqual("break", t.Substring((int)lex.Current.Start.Index, (int)lex.Current.End.Index - (int)lex.Current.Start.Index));

				lex.Next();

				Assert.AreEqual(3, lex.Current.Start.Line);
				Assert.AreEqual(3, lex.Current.Start.Col);
				Assert.AreEqual(19, lex.Current.Start.Index);

				Assert.AreEqual(3, lex.Current.End.Line);
				Assert.AreEqual(6, lex.Current.End.Col);
				Assert.AreEqual(22, lex.Current.End.Index);
				Assert.AreEqual("'a'", t.Substring((int)lex.Current.Start.Index, (int)lex.Current.End.Index - (int)lex.Current.Start.Index));
			}
		}

		[TestMethod]
		public void TestPosition02()
		{
			var t = "\nNull(";
			using (var lex = CreateLuaLexer(t))
			{
				lex.Next();

				Assert.AreEqual(2, lex.Current.Start.Line);
				Assert.AreEqual(1, lex.Current.Start.Col);
				Assert.AreEqual(1, lex.Current.Start.Index);

				Assert.AreEqual(2, lex.Current.End.Line);
				Assert.AreEqual(5, lex.Current.End.Col);
				Assert.AreEqual(5, lex.Current.End.Index);
				Assert.AreEqual("Null", t.Substring((int)lex.Current.Start.Index, (int)lex.Current.End.Index - (int)lex.Current.Start.Index));

				lex.Next();

				Assert.AreEqual(2, lex.Current.Start.Line);
				Assert.AreEqual(5, lex.Current.Start.Col);
				Assert.AreEqual(5, lex.Current.Start.Index);

				Assert.AreEqual(2, lex.Current.End.Line);
				Assert.AreEqual(6, lex.Current.End.Col);
				Assert.AreEqual(6, lex.Current.End.Index);
				Assert.AreEqual("(", t.Substring((int)lex.Current.Start.Index, (int)lex.Current.End.Index - (int)lex.Current.Start.Index));
			}
		}

		[TestMethod]
		public void TestPosition03()
		{
			var t = "\r\nNull(";
			using (var lex = CreateLuaLexer(t))
			{
				lex.Next();

				Assert.AreEqual(2, lex.Current.Start.Line);
				Assert.AreEqual(1, lex.Current.Start.Col);
				Assert.AreEqual(2, lex.Current.Start.Index);

				Assert.AreEqual(2, lex.Current.End.Line);
				Assert.AreEqual(5, lex.Current.End.Col);
				Assert.AreEqual(6, lex.Current.End.Index);
				Assert.AreEqual("Null", t.Substring((int)lex.Current.Start.Index, (int)lex.Current.End.Index - (int)lex.Current.Start.Index));

				lex.Next();

				Assert.AreEqual(2, lex.Current.Start.Line);
				Assert.AreEqual(5, lex.Current.Start.Col);
				Assert.AreEqual(6, lex.Current.Start.Index);

				Assert.AreEqual(2, lex.Current.End.Line);
				Assert.AreEqual(6, lex.Current.End.Col);
				Assert.AreEqual(7, lex.Current.End.Index);
				Assert.AreEqual("(", t.Substring((int)lex.Current.Start.Index, (int)lex.Current.End.Index - (int)lex.Current.Start.Index));
			}
		}

		#endregion

		#region -- Html Lexer ---------------------------------------------------------

		[TestMethod]
		public void ParsePlainTest()
		{
			HtmlTokenTest("<html> < a% >",
				T(LuaToken.Identifier, "print"),
				T(LuaToken.String, "<html> < a% >")
			);
		}

		[TestMethod]
		public void ParseLuaTest()
		{
			HtmlTokenTest("<html><%test(); %></html>",
				T(LuaToken.Identifier, "print"),
				T(LuaToken.String, "<html>"),
				T(LuaToken.Semicolon, String.Empty),
			
				T(LuaToken.Identifier, "test"),
				T(LuaToken.BracketOpen, String.Empty),
				T(LuaToken.BracketClose, String.Empty),
				T(LuaToken.Semicolon, String.Empty),

				T(LuaToken.Identifier, "print"),
				T(LuaToken.String, "</html>"),
				T(LuaToken.Semicolon, String.Empty)
			);
		} // proc ParseLuaTest

		[TestMethod]
		public void ParseOutputTest()
		{
			HtmlTokenTest("  <%otext();%> <%test();%> <html><%test(); %></html>",
				T(LuaToken.Identifier, "otext"),
				T(LuaToken.BracketOpen, String.Empty),
				T(LuaToken.BracketClose, String.Empty),
				T(LuaToken.Semicolon, String.Empty),

				T(LuaToken.Identifier, "test"),
				T(LuaToken.BracketOpen, String.Empty),
				T(LuaToken.BracketClose, String.Empty),
				T(LuaToken.Semicolon, String.Empty),

				T(LuaToken.Identifier, "print"),
				T(LuaToken.String, "<html>"),
				T(LuaToken.Semicolon, String.Empty),

				T(LuaToken.Identifier, "test"),
				T(LuaToken.BracketOpen, String.Empty),
				T(LuaToken.BracketClose, String.Empty),
				T(LuaToken.Semicolon, String.Empty),

				T(LuaToken.Identifier, "print"),
				T(LuaToken.String, "</html>"),
				T(LuaToken.Semicolon, String.Empty)
			);
		}

		[TestMethod]
		public void ParseVarTest()
		{
			HtmlTokenTest("<html><%=test::N0%></html>",
				T(LuaToken.Identifier, "print"),
				T(LuaToken.String, "<html>"),
				T(LuaToken.Semicolon, String.Empty),

				T(LuaToken.Identifier, "printValue"),
				T(LuaToken.BracketOpen, String.Empty),
				T(LuaToken.Identifier, "test"),
				T(LuaToken.Comma, String.Empty),
				T(LuaToken.String, "N0"),
				T(LuaToken.BracketClose, String.Empty),
				T(LuaToken.Semicolon, String.Empty),

				T(LuaToken.Identifier, "print"),
				T(LuaToken.String, "</html>"),
				T(LuaToken.Semicolon, String.Empty)
			);
		}

		[TestMethod]
		public void TestEmptyEnd()
		{
			HtmlTokenTest(
				Lines("<% if true then %>",
					"<html></html>",
					"<% end; %>",
					""
				),
				T(LuaToken.KwIf, "if"),
				T(LuaToken.KwTrue, "true"),
				T(LuaToken.KwThen, "then"),
				T(LuaToken.Identifier, "print"),
				T(LuaToken.String, "<html></html>\n"),
				T(LuaToken.Semicolon, String.Empty),
				T(LuaToken.KwEnd, "end"),
				T(LuaToken.Semicolon, String.Empty)
			);
		}


		#endregion
	} // class Lexer
}
