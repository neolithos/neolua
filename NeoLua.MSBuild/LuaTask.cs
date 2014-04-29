using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Neo.IronLua
{
  ///////////////////////////////////////////////////////////////////////////////
  /// <summary></summary>
  public sealed class LuaTask : Task, IGeneratedTask, IDisposable
  {
    private LuaChunk chunk;
    private LuaGlobal global;

    public LuaTask(LuaChunk chunk)
    {
      this.chunk = chunk;
      this.global = new LuaGlobal(LuaTaskFactory.Lua);
    } // ctor
    
    public void Dispose()
    {
      chunk = null;
      global = null;
    } // proc Dispose

    public object GetPropertyValue(TaskPropertyInfo property)
    {
      return Lua.RtConvertValue(global[property.Name], property.PropertyType);
    } // func GetPropertyValue

    public void SetPropertyValue(TaskPropertyInfo property, object value)
    {
      global[property.Name] = value;
    } // proc SetPropertyValue

    public override bool Execute()
    {
      try
      {
        global.DoChunk(chunk, this.BuildEngine, this.Log);
        return true;
      }
      catch (LuaRuntimeException e)
      {
        Log.LogError("{0} (at line {1},{2})", e.Message, this.chunk.ChunkName, e.Line);
        return false;
      }
    } // func Execute
  } // class LuaTask
}
