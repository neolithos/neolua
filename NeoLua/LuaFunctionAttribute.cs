using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Neo.IronLua
{
  ///////////////////////////////////////////////////////////////////////////////
  /// <summary>Marks a function or a GET property for the global namespace.</summary>
  [AttributeUsage(AttributeTargets.Property | AttributeTargets.Method, AllowMultiple=true, Inherited=false)]
  public class LuaMemberAttribute : Attribute
  {
    private string sName;
    
    /// <summary>Marks global Members, they act normally as library</summary>
    /// <param name="sName"></param>
    public LuaMemberAttribute(string sName)
    {
      this.sName = sName;
    } // ctor

    /// <summary>Global name of the function.</summary>
    public string Name { get { return sName; } }
  } // class LuaLibraryAttribute

  ///////////////////////////////////////////////////////////////////////////////
  /// <summary>Marks global functions in the lua environment</summary>
  [AttributeUsage(AttributeTargets.Method)]
  public class LuaFunctionAttribute : LuaMemberAttribute
  {
    /// <summary>Marks global functions in the lua environment</summary>
    /// <param name="sName">Global name of the function in lua</param>
    [Obsolete("Use LuaMember")]
    public LuaFunctionAttribute(string sName)
      : base(sName)
    {
    } // ctor
  } // class LuaFunctionAttribute
}
