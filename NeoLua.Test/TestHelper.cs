using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Neo.IronLua;

namespace LuaDLR.Test
{
  public class TestHelper
  {
    protected static bool TestExpression(bool lFullCode, string sExpr, object result)
    {
      Debug.Print("Test: " + sExpr);
      Lua l = new Lua();
      l.PrintExpressionTree = true;
      object[] r = l.DoChunk(lFullCode ? sExpr : "local a = " + sExpr + "; return a;", "test.lua");
      if (result == null && r.Length == 0 || r[0] == result || (Object.Equals(r[0].ToString(), result.ToString()) && r[0].GetType() == result.GetType()))
        return true;
      else
      {
        Debug.Print("{0} != {1}", r[0], result);
        return false;
      }
    } // func TestConstant

    protected static bool TestExpressionTable(bool lFullCode, string sExpr, params KeyValuePair<object, object>[] tests)
    {
      Debug.Print("Test: " + sExpr);
      Lua l = new Lua();
      l.PrintExpressionTree = true;
      object[] r = l.DoChunk(lFullCode ? sExpr : "local a = " + sExpr + "; return a;", "test.lua");
      LuaTable t = r[0] as LuaTable;
      if (t == null)
        return false;
      else
      {
        List<KeyValuePair<object, object>> testList = new List<KeyValuePair<object, object>>(tests);
        foreach (var cur in t)
        {
          int iIdx = testList.FindIndex(c => Object.Equals(c.Key, cur.Key));
          if (iIdx == -1)
            return false;
          else
          {
            if (!Object.Equals(cur.Value, testList[iIdx].Value))
              return false;
            testList.RemoveAt(iIdx);
          }
        }
        return testList.Count == 0;
      }
    } // func TestConstant

    protected static bool TestCompare(string sExpr)
    {
      Debug.Print("Test: " + sExpr);
      Lua l = new Lua();
      l.PrintExpressionTree = true;
      object[] r = l.DoChunk("local a; if " + sExpr + " then a = true; else a = false; end; return a;", "test.lua");
      return (bool)r[0];
    } // func TestCompare

    protected static KeyValuePair<object, object> TV(object item, object value)
    {
      return new KeyValuePair<object, object>(item, value);
    } // func TV

    protected bool TestReturn(string sCode, params object[] result)
    {
      Lua l = new Lua();
      l.PrintExpressionTree = true;
      object[] r = l.DoChunk(sCode, "test.lua");

      return TestReturn(result, r);
    } // func TestReturn

    protected static bool TestReturn(object[] result, params object[] r)
    {
      if (r == null && result.Length == 0)
        return true;
      if (r.Length != result.Length)
        return false;

      for (int i = 0; i < r.Length; i++)
        if (r[i] == null && result[i] == null ||
          (Object.Equals(r[i].ToString(), result[i].ToString()) && r[i].GetType() == result[i].GetType()))
        { }
        else
        {
          Debug.Print("{0} != {1}", r[i], result[i]);
          return false;
        }
      return true;
    } // func TestReturn

    protected static string GetCode(string sName)
    {
      using (Stream src = typeof(TestHelper).Assembly.GetManifestResourceStream(typeof(TestHelper), sName))
      using (TextReader tr = new StreamReader(src))
        return tr.ReadToEnd();
    } // func GetCode
  } // class TestHelper
}
