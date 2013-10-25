using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Neo.IronLua
{
  ///////////////////////////////////////////////////////////////////////////////
  /// <summary></summary>
  [AttributeUsage(AttributeTargets.Method)]
  public class LuaFunctionAttribute : Attribute
  {
    private string sName;

    public LuaFunctionAttribute(string sName)
    {
      this.sName = sName;
    } // ctor

    public string Name { get { return sName; } }
  } // class LuaFunctionAttribute
}
