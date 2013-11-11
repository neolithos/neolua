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
        using (LuaChunk c = l.CompileChunk("\nNull(a, a);", "test.lua", true, new KeyValuePair<string, Type>("a", typeof(int))))
          try
          {
            g.DoChunk(c, 1);
          }
          catch (Exception e)
          {
            LuaExceptionData d = LuaExceptionData.GetData(e.InnerException);
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
        using (LuaChunk c = l.CompileChunk("math.abs(-1 / a).A();", "test.lua", true, new KeyValuePair<string, Type>("a", typeof(int))))
          try
          {
            g.DoChunk(c, 1);
          }
          catch (TargetInvocationException e)
          {
            LuaExceptionData d = LuaExceptionData.GetData(e.InnerException);
            Debug.Print("Error: {0}", e.InnerException.Message);
            Debug.Print("Error at:\n{0}", d.StackTrace);
            Assert.IsTrue(d[2].LineNumber == 1); //  && d[2].ColumnNumber == 18 in release this is one
          }
      }
    } // proc Exception01
  } // class Runtime
}
