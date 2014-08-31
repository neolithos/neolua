using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Neo.IronLua
{
  public sealed class LuaTaskFactory : ITaskFactory
  {
    private readonly static Lua lua = new Lua();

    private LuaChunk task = null;
    private TaskPropertyInfo[] taskProperties = null;

    public bool Initialize(string taskName, IDictionary<string, TaskPropertyInfo> parameterGroup, string taskBody, IBuildEngine taskFactoryLoggingHost)
    {
      TaskLoggingHelper log = new TaskLoggingHelper(taskFactoryLoggingHost, taskName);
        
      // We use the property group for the declaration
      taskProperties = (from c in parameterGroup select c.Value).ToArray();

      // Compile chunk
      try
      {
        log.LogMessage("Compile script.");
        task = lua.CompileChunk(taskBody, taskName, Lua.DefaultDebugEngine, 
          new KeyValuePair<string, Type>("engine", typeof(IBuildEngine)), 
          new KeyValuePair<string, Type>("log", typeof(TaskLoggingHelper))
        );
        
        return true;
      }
      catch (LuaParseException e)
      {
        log.LogError("{0} (at line {1},{2})", e.Message, taskName, e.Line);
        return false;
      }
    } // func Initialize

    public ITask CreateTask(IBuildEngine taskFactoryLoggingHost)
    {
      return new LuaTask(task);
    } // func CreateTask

    public void CleanupTask(ITask task)
    {
      ((LuaTask)task).Dispose();
    } // proc CleanupTask

    public TaskPropertyInfo[] GetTaskParameters()
    {
      return taskProperties;
    } // func GetTaskParameters

    public string FactoryName { get { return typeof(LuaTaskFactory).Name; } }
    public Type TaskType { get { return typeof(LuaTask); } }

    public static Lua Lua { get { return lua; } }
  } // class LuaTaskFactory
}
