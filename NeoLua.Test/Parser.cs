using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.IronLua;

namespace LuaDLR.Test
{
  ///////////////////////////////////////////////////////////////////////////////
  /// <summary></summary>
  [TestClass]
  public class Parser
  {
    #region -- TokenTest --------------------------------------------------------------

    private KeyValuePair<LuaToken, string> T(LuaToken t, string v)
    {
      return new KeyValuePair<LuaToken, string>(t, v);
    } // func T

    private bool TokenTest(string sToken, params KeyValuePair<LuaToken, string>[] token)
    {
      using (LuaLexer l = new LuaLexer("test.lua", new StringReader(sToken)))
      {
        l.Next();

        for (int i = 0; i < token.Length; i++)
        {
          Debug.Write(String.Format("Test: {0} = {1} ==>", l.Current.Typ, token[i].Key));
          if (l.Current.Typ != token[i].Key)
          {
            Debug.WriteLine("tokens FAILED");
            return false;
          }
          else if (l.Current.Value != token[i].Value)
          {
            Debug.WriteLine("values '{0}' != '{1}'   FAILED", l.Current.Value, token[i].Value);
            return false;
          }
          Debug.WriteLine("OK");
          l.Next();
        }
        if (l.Current.Typ != LuaToken.Eof)
          return false;
        return true;
      }
    } // func TokenTest

    [TestMethod]
    public void TokenTest()
    {
      Assert.IsTrue(TokenTest("°",  T(LuaToken.InvalidChar, "°")));
      Assert.IsTrue(TokenTest("'h", T( LuaToken.InvalidString, "h")));
      Assert.IsTrue(TokenTest("--[[ a", T(LuaToken.InvalidComment, String.Empty)));

      Assert.IsTrue(TokenTest("Hallo", T(LuaToken.Identifier, "Hallo")));
      Assert.IsTrue(TokenTest("'Hallo'", T(LuaToken.String,"Hallo")));
      Assert.IsTrue(TokenTest("\"Hallo\"", T(LuaToken.String, "Hallo")));
      Assert.IsTrue(TokenTest("[[Hallo]]", T(LuaToken.String, "Hallo")));
      Assert.IsTrue(TokenTest("2",T( LuaToken.Number, "2")));
      Assert.IsTrue(TokenTest("0xA3", T(LuaToken.HexNumber, "0xA3")));

      Assert.IsTrue(TokenTest("and break cast const do else elseif end false for foreach function goto if in local nil not or repeat return then true until while",
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
        T(LuaToken.KwLocal,"local"),
        T(LuaToken.KwNil,"nil"),
        T(LuaToken.KwNot,"not"),
        T(LuaToken.KwOr,"or"),
        T(LuaToken.KwRepeat,"repeat"),
        T(LuaToken.KwReturn,"return"),
        T(LuaToken.KwThen,"then"),
        T(LuaToken.KwTrue,"true"),
        T(LuaToken.KwUntil,"until"),
        T(LuaToken.KwWhile,"while")
      ));
      Assert.IsTrue(TokenTest("::label::", 
        T(LuaToken.ColonColon, String.Empty), 
        T(LuaToken.Identifier, "label"), 
        T(LuaToken.ColonColon, String.Empty)));

      Assert.IsTrue(TokenTest("+     -     *     /     %     ^     #",
        T(LuaToken.Plus, String.Empty),
        T(LuaToken.Minus, String.Empty),
        T(LuaToken.Star, String.Empty),
        T(LuaToken.Slash, String.Empty),
        T(LuaToken.Percent, String.Empty),
        T(LuaToken.Caret, String.Empty),
        T(LuaToken.Cross, String.Empty)
        ));
      Assert.IsTrue(TokenTest("==    ~=    <=    >=    <     >     =",
        T(LuaToken.Equal, String.Empty),
        T(LuaToken.NotEqual, String.Empty),
        T(LuaToken.LowerEqual, String.Empty),
        T(LuaToken.GreaterEqual, String.Empty),
        T(LuaToken.Lower, String.Empty),
        T(LuaToken.Greater, String.Empty),
        T(LuaToken.Assign, String.Empty)
        ));
      Assert.IsTrue(TokenTest("(     )     {     }     [     ]",
        T(LuaToken.BracketOpen, String.Empty),
        T(LuaToken.BracketClose, String.Empty),
        T(LuaToken.BracketCurlyOpen, String.Empty),
        T(LuaToken.BracketCurlyClose, String.Empty),
        T(LuaToken.BracketSquareOpen, String.Empty),
        T(LuaToken.BracketSquareClose, String.Empty)
       ));
      Assert.IsTrue(TokenTest(";     :     ,     .     ..    ...",
        T(LuaToken.Semicolon, String.Empty),
        T(LuaToken.Colon, String.Empty),
        T(LuaToken.Comma, String.Empty),
        T(LuaToken.Dot, String.Empty),
        T(LuaToken.DotDot, String.Empty),
        T(LuaToken.DotDotDot, String.Empty)
        ));

      Assert.IsTrue(TokenTest("a = 'alo\\n123\"'",
        T(LuaToken.Identifier, "a"),
        T(LuaToken.Assign, String.Empty),
        T(LuaToken.String, "alo\r\n123\"")
        ));
      Assert.IsTrue(TokenTest("a = \"alo\\n123\\\"\"",
        T(LuaToken.Identifier, "a"),
        T(LuaToken.Assign, String.Empty),
        T(LuaToken.String, "alo\r\n123\"")
        ));
      Assert.IsTrue(TokenTest("a = '\\97lo\\10\\04923\\\"'",
        T(LuaToken.Identifier, "a"),
        T(LuaToken.Assign, String.Empty),
        T(LuaToken.String, "alo\r\n123\"")
        ));
      Assert.IsTrue(TokenTest("a = [[alo\n123\"]]",
        T(LuaToken.Identifier, "a"),
        T(LuaToken.Assign, String.Empty),
        T(LuaToken.String, "alo\r\n123\"")
        ));
      Assert.IsTrue(TokenTest("a = [==[\nalo\n123\"]==]",
        T(LuaToken.Identifier, "a"),
        T(LuaToken.Assign, String.Empty),
        T(LuaToken.String, "alo\r\n123\"")
        ));
      Assert.IsTrue(TokenTest("a = [===[]==]]===]",
        T(LuaToken.Identifier, "a"),
        T(LuaToken.Assign, String.Empty),
        T(LuaToken.String, "]==]")
        ));

      Assert.IsTrue(TokenTest("--[===[]==]]===]", T(LuaToken.Eof, String.Empty)));

      Assert.IsTrue(TokenTest("3", T(LuaToken.Number, "3")));
      Assert.IsTrue(TokenTest("3.0", T(LuaToken.Number, "3.0")));
      Assert.IsTrue(TokenTest("3.1416", T(LuaToken.Number, "3.1416")));
      Assert.IsTrue(TokenTest("314.16e-2", T(LuaToken.Number, "314.16e-2")));
      Assert.IsTrue(TokenTest("0.31416E1", T(LuaToken.Number, "0.31416E1")));

      Assert.IsTrue(TokenTest("0xff", T(LuaToken.HexNumber, "0xff")));
      Assert.IsTrue(TokenTest("0x0.1E", T(LuaToken.HexNumber, "0x0.1E")));
      Assert.IsTrue(TokenTest("0xA23p-4", T(LuaToken.HexNumber, "0xA23p-4")));
      Assert.IsTrue(TokenTest("0X1.921FB54442D18P+1", T(LuaToken.HexNumber, "0X1.921FB54442D18P+1")));
    } // proc TokenTest

    #endregion

    #region -- TestConstants ----------------------------------------------------------

    private bool TestConstant(Lua l, string sVarValue, object result)
    {
      Debug.Print("Test: " + sVarValue);
      object[] r = l.CreateEnvironment().DoChunk("local a = " + sVarValue + "; return a;", "test.lua");
      return Object.Equals(r[0], result);
    } // func TestConstant

    [TestMethod]
    public void TestConstants()
    {
      Lua l = new Lua();

      Assert.IsTrue(TestConstant(l, "3", 3));
      Assert.IsTrue(TestConstant(l, "3.0", 3.0));
      Assert.IsTrue(TestConstant(l, "3.1416", 3.1416));
      Assert.IsTrue(TestConstant(l, "314.16e-2", 314.16e-2));
      Assert.IsTrue(TestConstant(l, "0.31416E1", 0.31416E1));

      Assert.IsTrue(TestConstant(l, "0xff", 0xff));
      //Assert.IsTrue(TestVariable(l, "0x0.1E", ));
      //Assert.IsTrue(TestVariable(l, "0xA23p-4", ));
      //Assert.IsTrue(TestVariable(l, "0X1.921FB54442D18P+1", ));
    } // proc TestConstants

    #endregion
  } // class Lexer
}
