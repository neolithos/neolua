using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text;

namespace Neo.IronLua
{
  #region -- class LuaChunk -----------------------------------------------------------

  ///////////////////////////////////////////////////////////////////////////////
  /// <summary>Represents the compiled chunk.</summary>
  public class LuaChunk
  {
    private Lua lua;
    private string sName;
		private Delegate chunk = null;

		/// <summary>Create the chunk</summary>
		/// <param name="lua">Attached runtime</param>
		/// <param name="sName">Name of the chunk</param>
		/// <param name="chunk"></param>
		protected internal LuaChunk(Lua lua, string sName, Delegate chunk)
		{
			this.lua = lua;
			this.sName = sName;
			this.chunk = chunk;
		} // ctor

		/// <summary>Assign a methodname with the current chunk.</summary>
		/// <param name="sName">unique method name</param>
		protected void RegisterMethod(string sName)
		{
			Lua.RegisterMethod(sName, this);
		} // proc RegisterMethod

		/// <summary>Gets for the StackFrame the position in the source file.</summary>
		/// <param name="method"></param>
		/// <param name="ilOffset"></param>
		/// <returns></returns>
		protected internal virtual ILuaDebugInfo GetDebugInfo(MethodBase method, int ilOffset)
		{
			return null;
		} // func GetDebugInfo

    /// <summary>Returns the associated LuaEngine</summary>
    public Lua Lua { get { return lua; } }
    /// <summary>Set or get the compiled script.</summary>
    protected internal Delegate Chunk { get { return chunk; } set { chunk = value; } }

    /// <summary>Name of the compiled chunk.</summary>
    public string ChunkName { get { return sName; } }

		/// <summary>Is the chunk compiled and executable.</summary>
		public bool IsCompiled { get { return chunk != null; } }
		/// <summary>Is the chunk compiled with debug infos</summary>
		public virtual bool HasDebugInfo { get { return false; } }

    /// <summary>Returns the declaration of the compiled chunk.</summary>
    public MethodInfo Method { get { return chunk == null ? null : chunk.Method; } }

    /// <summary>Get the IL-Size</summary>
    public virtual int Size
    {
			get
			{
				if (chunk == null)
					return 0;

				// Gib den Type zurück
				Type typeMethod = chunk.Method.GetType();
				if (typeMethod.FullName == "System.Reflection.RuntimeMethodInfo")
					return chunk.Method.GetMethodBody().GetILAsByteArray().Length;
				else if (typeMethod.FullName == "System.Reflection.Emit.DynamicMethod+RTDynamicMethod")
				{
					if (fiOwner == null)
					{
						fiOwner = typeMethod.GetField("m_owner", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.GetField);
						if (fiOwner == null)
							throw new InvalidOperationException("RTDynamicMethod:m_owner not found");
					}
					DynamicMethod dyn = (DynamicMethod)fiOwner.GetValue(chunk.Method);
					return dyn.GetILGenerator().ILOffset;
				}
				else
					return -1;
			}
    } // prop Size

		// -- Static --------------------------------------------------------------

		private static FieldInfo fiOwner = null; // m_owner
  } // class LuaChunk

  #endregion
}
