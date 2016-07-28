using System;
using System.Reflection;

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

		/// <summary>Executes the Chunk on the given Environment</summary>
		/// <param name="env"></param>
		/// <param name="callArgs"></param>
		/// <returns></returns>
		public LuaResult Run(LuaTable env, params object[] callArgs)
		{
			if (!IsCompiled)
				throw new ArgumentException(Properties.Resources.rsChunkNotCompiled, "chunk");

			object[] args = new object[callArgs == null ? 1 : callArgs.Length + 1];
			args[0] = env;
			if (callArgs != null)
				Array.Copy(callArgs, 0, args, 1, callArgs.Length);

			try
			{
				object r = chunk.DynamicInvoke(args);
				return r is LuaResult ? (LuaResult)r : new LuaResult(r);
			}
			catch (TargetInvocationException e)
			{
				throw e.InnerException; // rethrow with new stackstrace
			}
		} // proc Run

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
    public MethodInfo Method { get { return chunk == null ? null : chunk.GetMethodInfo(); } }

    /// <summary>Get the IL-Size</summary>
    public virtual int Size
    {
			get
			{
				if (chunk == null)
					return 0;

				// Gib den Type zurück
				MethodInfo miChunk = chunk.GetMethodInfo();
				Type typeMethod = miChunk.GetType();
				if (typeMethod == RuntimeMethodInfoType)
				{
					dynamic methodBody = ((dynamic)miChunk).GetMethodBody();
					return methodBody.GetILAsByteArray().Length;
				}
				else if (typeMethod == RtDynamicMethodType)
				{
					dynamic dynamicMethod = RtDynamicMethodOwnerFieldInfo.GetValue(miChunk);
					if (dynamicMethod == null)
						return -1;
					return dynamicMethod.GetILGenerator().ILOffset;
				}
				else
					return -1;
			}
    } // prop Size

		// -- Static --------------------------------------------------------------

		private static readonly Type RuntimeMethodInfoType = Type.GetType("System.Reflection.RuntimeMethodInfo");
		private static readonly Type DynamicMethodType = Type.GetType("System.Reflection.Emit.DynamicMethod");
		private static readonly Type RtDynamicMethodType = Type.GetType("System.Reflection.Emit.DynamicMethod+RTDynamicMethod");
		private static readonly FieldInfo RtDynamicMethodOwnerFieldInfo;

		static LuaChunk()
		{
			if (RtDynamicMethodType != null)
			{
				RtDynamicMethodOwnerFieldInfo = RtDynamicMethodType.GetTypeInfo().FindDeclaredField("m_owner", ReflectionFlag.NonPublic);
				if (RtDynamicMethodOwnerFieldInfo == null)
					throw new InvalidOperationException("RTDynamicMethod:m_owner not found");
			}
			else
			{
				RtDynamicMethodOwnerFieldInfo = null;
			}
		} // sctor
  } // class LuaChunk

  #endregion
}
