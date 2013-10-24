using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Text;

namespace Neo.IronLua
{
  #region -- class LuaDebugInfo -------------------------------------------------------

  ///////////////////////////////////////////////////////////////////////////////
  /// <summary></summary>
  [Serializable]
  internal class LuaDebugInfo : IComparable<LuaDebugInfo>
  {
    private string sChunkName;
    private string sFileName;
    private int ilOffset;
    private int iLine;
    private int iColumn;

    public LuaDebugInfo(LuaChunk chunk, SymbolDocumentInfo document, int ilOffset, int iLine, int iColumn)
    {
      this.sChunkName = chunk.ChunkName;
      this.sFileName = document.FileName;
      this.ilOffset = ilOffset;
      this.iLine = iLine;
      this.iColumn = iColumn;
    } // ctor

    public int CompareTo(LuaDebugInfo other)
    {
      return ilOffset - other.ilOffset;
    } // func CompareTo

    public string ChunkName { get { return sChunkName; } }
    public string FileName { get { return sFileName; } }
    public int ILOffset { get { return ilOffset; } }
    public int Line { get { return iLine; } }
    public int Column { get { return iColumn; } }
  } // class LuaDebugInfo

  #endregion

  #region -- class LuaChunk -----------------------------------------------------------

  ///////////////////////////////////////////////////////////////////////////////
  /// <summary>Represents the compiled chunk.</summary>
  public class LuaChunk : IDisposable
  {
    private Lua lua;
    private Delegate chunk = null;
    private string sName;
    private string sChunkName = null;

    private List<LuaDebugInfo> debugInfos = null;
    private List<LuaChunk> assigned = null;

    internal LuaChunk(Lua lua, string sName, string sChunkName)
    {
      this.lua = lua;
      this.sName = sName;
      this.sChunkName = sChunkName;
    } // ctor

    /// <summary>Removes the chunk from the lua-engine.</summary>
    public void Dispose()
    {
      if (assigned != null)
      {
        for (int i = 0; i < assigned.Count; i++)
          assigned[i].Dispose();
        assigned.Clear();
        assigned = null;
      }
      if (debugInfos != null)
      {
        debugInfos.Clear();
        debugInfos = null;
      }
      chunk = null;
      lua.RemoveChunk(sName);
    } // proc Dispose

    internal void AssignChunk(LuaChunk assignChunk)
    {
      if (assigned == null)
        assigned = new List<LuaChunk>();
      if (assigned.IndexOf(assignChunk) == -1)
        assigned.Add(assignChunk);
    } // proc AssignChunk

    internal void AddDebugInfo(SymbolDocumentInfo document, int ilOffset, int iLine, int iColumn)
    {
      if (debugInfos == null)
        debugInfos = new List<LuaDebugInfo>();

      LuaDebugInfo di = new LuaDebugInfo(this, document, ilOffset, iLine, iColumn);
      int iPos = debugInfos.BinarySearch(di);
      if (iPos < 0)
        debugInfos.Insert(~iPos, di);
      else
        debugInfos[iPos] = di;
    } // proc AddDebugInfo

    internal LuaDebugInfo GetDebugInfo(int ilOffset)
    {
      LuaDebugInfo info = null;

      // find debug info
      if (debugInfos != null)
        for (int i = 0; i < debugInfos.Count; i++)
          if (debugInfos[i].ILOffset == ilOffset)
          {
            info = debugInfos[i];
            break;
          }
          else if (debugInfos[i].ILOffset > ilOffset)
          {
            info = i == 0 ? null : debugInfos[i - 1];
            break;
          }

      // clear debug
      if (info != null && info.Line == 16707566)
        info = null;

      return info;
    } // func GetDebugInfo

    /// <summary>Set or get the compiled script.</summary>
    internal Delegate Chunk { get { return chunk; } set { chunk = value; } }
    /// <summary>Internal Unique Name for the script</summary>
    internal string Name { get { return sName; } }

    /// <summary>Name of the compiled chunk.</summary>
    public string ChunkName { get { return sChunkName ?? sName; } internal set { sChunkName = value; } }

    /// <summary>Is the chunk compiled and is executable.</summary>
    public bool IsCompiled { get { return chunk != null; } }
    /// <summary>Is the chunk compiled with debug infos</summary>
    public bool HasDebugInfo { get { return debugInfos != null; } }
    /// <summary>Is this an empty chunk</summary>
    public bool IsEmpty { get { return !IsCompiled && debugInfos == null; } }
  } // class LuaChunk

  #endregion
}
