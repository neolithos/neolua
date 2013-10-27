using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Neo.IronLua
{
  ///////////////////////////////////////////////////////////////////////////////
  /// <summary>Marks global functions in the lua environment</summary>
  [AttributeUsage(AttributeTargets.Method)]
  public class LuaFunctionAttribute : Attribute
  {
    private string sName;

    /// <summary>Marks global functions in the lua environment</summary>
    /// <param name="sName">Global name of the function in lua</param>
    public LuaFunctionAttribute(string sName)
    {
      this.sName = sName;
    } // ctor

    /// <summary>Global name of the function.</summary>
    public string Name { get { return sName; } }
  } // class LuaFunctionAttribute
}
