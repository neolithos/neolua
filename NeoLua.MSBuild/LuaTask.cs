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
			=> Lua.RtConvertValue(global[property.Name], property.PropertyType);
		
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
