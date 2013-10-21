using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Text;

namespace Neo.IronLua
{
  #region -- class LuaChunk -----------------------------------------------------------

  ///////////////////////////////////////////////////////////////////////////////
  /// <summary>Represents the compiled chunk.</summary>
  public class LuaChunk : IDisposable
  {
    private Lua lua;
    private Delegate chunk = null;
    private string sName;
    private string sChunkName = null;

    internal LuaChunk(Lua lua, string sName, string sChunkName)
    {
      this.lua = lua;
      this.sName = sName;
      this.sChunkName = sChunkName;
    } // ctor

    /// <summary>Removes the chunk from the lua-engine.</summary>
    public void Dispose()
    {
      lua.RemoveChunk(sName);
    } // proc Dispose

    /// <summary>Set or get the compiled script.</summary>
    internal Delegate Chunk { get { return chunk; } set { chunk = value; } }
    /// <summary>Internal Unique Name for the script</summary>
    internal string Name { get { return sName; } }

    /// <summary>Name of the compiled chunk.</summary>
    public string ChunkName { get { return sChunkName ?? sName; } internal set { sChunkName = value; } }
    /// <summary>Is the chunk compiled and is executable.</summary>
    public bool IsCompiled { get { return chunk != null; } }
  } // class LuaChunk

  #endregion
}
