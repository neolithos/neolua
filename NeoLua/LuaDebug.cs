using System;
using System.Linq;
using System.Linq.Expressions;
using System.Text;

namespace Neo.IronLua
{
	#region -- enum LuaDebugLevel -------------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
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

	#region -- interface ILuaDebug ------------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
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

	#region -- interface ILuaDebugInfo --------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
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
