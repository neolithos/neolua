using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

namespace Neo.IronLua
{
	#region -- class LuaGlobal ----------------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public class LuaGlobal : LuaGlobalPortable
	{
		private LuaFilePackage io = null;
		private LuaLibraryPackage package = null;
		internal Dictionary<object, object> loaded = new Dictionary<object, object>();

		#region -- Ctor/Dtor --------------------------------------------------------------

		/// <summary>Create a new environment for the lua script manager.</summary>
		/// <param name="lua"></param>
		public LuaGlobal(Lua lua)
			: base(lua)
		{
		} // ctor

		#endregion

		#region -- DoChunk ----------------------------------------------------------------

		/// <summary>Compiles and execute the filename.</summary>
		/// <param name="sFileName">Name of the lua file.</param>
		/// <param name="args">Parameter definition for the file.</param>
		/// <returns>Return values of the file.</returns>
		public LuaResult DoChunk(string sFileName, params KeyValuePair<string, object>[] args)
		{
			using (StreamReader sr = new StreamReader(sFileName))
				return DoChunk(sr, sFileName, args);
		} // proc DoFile

		#endregion

		#region -- Basic Functions --------------------------------------------------------

		/// <summary></summary>
		/// <param name="sText"></param>
		protected override void OnPrint(string sText)
		{
			if (Environment.UserInteractive)
				Console.WriteLine(sText);
			else
				base.OnPrint(sText);
		} // proc OnPrint

		/// <summary></summary>
		/// <param name="args"></param>
		/// <returns></returns>
		[LuaMember("dofile")]
		private LuaResult LuaDoFile(object[] args)
		{
			if (args == null || args.Length == 0)
				throw new ArgumentException(); // no support for stdin
			else if (args.Length == 1)
				return DoChunk((string)args[0]);
			else
				return DoChunk((string)args[0], CreateArguments(1, args));
		} // func LuaDoFile

		#region -- class LuaLoadReturnClosure ---------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private class LuaLoadReturnClosure
		{
			public LuaTable env; // force the environment on the first index
			public LuaChunk chunk;
			public LuaGlobal @this;

			public LuaResult Run(LuaTable localEnv)
			{
				return chunk.Run(localEnv ?? env ?? @this, new object[0]);
			} // func Run
		} // class LuaLoadReturnClosure

		#endregion

		private object LuaLoadReturn(LuaChunk c, LuaTable defaultEnv)
		{
			var run = new  LuaLoadReturnClosure();
			run.env = defaultEnv;
			run.chunk = c;
			run.@this = this;
			return new Func<LuaTable, LuaResult>(run.Run);
		} // func LuaLoadReturn

		/// <summary></summary>
		/// <param name="ld"></param>
		/// <param name="source"></param>
		/// <param name="mode"></param>
		/// <param name="env"></param>
		/// <returns></returns>
		[LuaMember("load")]
		private object LuaLoad(object ld, string source, string mode, LuaTable env)
		{
			if (String.IsNullOrEmpty(source))
				source = "=(load)";

			if (mode == "b" || !(ld is string || ld is LuaMethod || ld is Delegate)) // binary chunks are not implementeted
				throw new NotImplementedException();

			try
			{
				// collect the chunks
				if (Lua.RtInvokeable(ld))
				{
					StringBuilder sbCode = new StringBuilder();
					string sPart;
					while (!String.IsNullOrEmpty(sPart = (string)new LuaResult(RtInvokeSite(ld))[0]))
						sbCode.Append(sPart);
					ld = sbCode.ToString();
				}
				// create the chunk
				return LuaLoadReturn(Lua.CompileChunk((string)ld, source, null), env);
			}
			catch (Exception e)
			{
				return new LuaResult(null, e.Message);
			}
		} // func LuaLoad

		/// <summary></summary>
		/// <param name="filename"></param>
		/// <param name="mode"></param>
		/// <param name="env"></param>
		/// <returns></returns>
		[LuaMember("loadfile")]
		private object LuaLoadFile(string filename, string mode, LuaTable env)
		{
			if (mode == "b") // binary chunks are not implementeted
				throw new NotImplementedException();

			// create the chunk
			return LuaLoadReturn(Lua.CompileChunk(filename, null), env);
		} // func LuaLoadFile

		[LuaMember("require")]
		private LuaResult LuaRequire(object modname)
		{
			if (modname == null)
				throw new ArgumentNullException();

			// check if the modul is loaded in this global
			if (loaded.ContainsKey(modname))
				return new LuaResult(loaded[modname]);

			// check if the modul is loaded in a different global
			var chunk = ((LuaLibraryPackage)LuaPackage).LuaRequire(this, modname as string);
			if (chunk != null)
				return new LuaResult(loaded[modname] = DoChunk(chunk)[0]);
			else
				return LuaResult.Empty;
		} // func LuaRequire

		#endregion

		#region -- Basic Libraries --------------------------------------------------------

		/// <summary></summary>
		[LuaMember("io")]
		public dynamic LuaLibraryIO
		{
			get
			{
				if (io == null)
					io = new LuaFilePackage();
				return io;
			}
		} // prop LuaLibraryIO

		/// <summary></summary>
		[LuaMember("package")]
		public dynamic LuaPackage
		{
			get
			{
				if (package == null)
					package = new LuaLibraryPackage(this);
				return package;
			}
		} // prop LuaPackage

		[LuaMember("os")]
		private static LuaType LuaLibraryOS
		{
			get { return LuaType.GetType(typeof(LuaLibraryOS)); }
		} // prop LuaLibraryTable

		#endregion
	} // class LuaGlobal

	#endregion
}
