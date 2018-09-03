#region -- copyright --
//
// Licensed to the Apache Software Foundation (ASF) under one
// or more contributor license agreements.  See the NOTICE file
// distributed with this work for additional information
// regarding copyright ownership.  The ASF licenses this file
// to you under the Apache License, Version 2.0 (the
// "License"); you may not use this file except in compliance
// with the License.  You may obtain a copy of the License at
// 
//   http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing,
// software distributed under the License is distributed on an
// "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY
// KIND, either express or implied.  See the License for the
// specific language governing permissions and limitations
// under the License.
//
#endregion
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Neo.IronLua
{
	public sealed class LuaTaskFactory : ITaskFactory
	{
		private LuaChunk task = null;
		private TaskPropertyInfo[] taskProperties = null;

		public bool Initialize(string taskName, IDictionary<string, TaskPropertyInfo> parameterGroup, string taskBody, IBuildEngine taskFactoryLoggingHost)
		{
			var log = new TaskLoggingHelper(taskFactoryLoggingHost, taskName);

			// We use the property group for the declaration
			taskProperties = (from c in parameterGroup select c.Value).ToArray();

			// Compile chunk
			try
			{
				log.LogMessage("Compile script.");
				task = Lua.CompileChunk(taskBody, taskName, Lua.StackTraceCompileOptions,
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
			=> new LuaTask(task);

		public void CleanupTask(ITask task)
			=> ((LuaTask)task).Dispose();
		
		public TaskPropertyInfo[] GetTaskParameters()
		  => taskProperties;

		public string FactoryName => typeof(LuaTaskFactory).Name;
		public Type TaskType => typeof(LuaTask);

		public static Lua Lua { get; } = new Lua();
	} // class LuaTaskFactory
}
