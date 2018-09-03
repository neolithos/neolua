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
using System.Linq.Expressions;

namespace Neo.IronLua
{
	#region -- enum LuaDebugLevel -----------------------------------------------------

	/// <summary>Descripes the debug-level.</summary>
	[Flags]
	public enum LuaDebugLevel
	{
		/// <summary>No debug info will be emitted.</summary>
		None,
		/// <summary>Before every new line is a DebugInfo emitted (Line exact).</summary>
		Line = 1,
		/// <summary>Every expression is wrap by a DebugInfo (Column exact).</summary>
		Expression = 2,
		/// <summary>Registriert die Methoden</summary>
		RegisterMethods = 4
	} // enum LuaDebugLevel

	#endregion

	#region -- interface ILuaDebug ----------------------------------------------------

	/// <summary></summary>
	public interface ILuaDebug
	{
		/// <summary>Create the chunk</summary>
		/// <param name="expr">Content of the chunk.</param>
		/// <param name="lua"></param>
		/// <returns></returns>
		LuaChunk CreateChunk(Lua lua, LambdaExpression expr);
		/// <summary>How should the parser emit the DebugInfo's</summary>
		LuaDebugLevel Level { get; }
	} // interface ILuaDebug

	#endregion

	#region -- interface ILuaDebugInfo ------------------------------------------------

	/// <summary>Information about a position.</summary>
	public interface ILuaDebugInfo
	{
		/// <summary>Name of the chunk.</summary>
		string ChunkName { get; }
		/// <summary>Source of the position.</summary>
		string FileName { get; }
		/// <summary>Line</summary>
		int Line { get; }
		/// <summary>Column</summary>
		int Column { get; }
	} // interface ILuaDebugInfo

	#endregion
}
