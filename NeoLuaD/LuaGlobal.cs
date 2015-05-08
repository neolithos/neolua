using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Neo.IronLua
{
	#region -- class LuaGlobal ----------------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public class LuaGlobal : LuaGlobalPortable
	{
		#region -- class LuaLoadedTable ---------------------------------------------------

		private class LuaLoadedTable : LuaTable
		{
			private LuaGlobal global;

			public LuaLoadedTable(LuaGlobal global)
			{
				this.global = global;
			} // ctor

			protected override object OnIndex(object key)
			{
				object value;
				if (global.loaded != null && global.loaded.TryGetValue(key, out value))
					return value;
				return base.OnIndex(key);
			} // func OnIndex
		} // class LuaLoadedTable

		#endregion

		#region -- class LuaLibraryPackage ------------------------------------------------

		internal sealed class LuaLibraryPackage
		{
			private string[] paths;

			public LuaLibraryPackage(LuaGlobal global)
			{
				this.loaded = new LuaLoadedTable(global);
				this.path = ";;";
			} // ctor

			public LuaTable loaded { get; private set; }
			public string path
			{
				get
				{
					return String.Join(";", paths);
				}
				set
				{
					if (String.IsNullOrEmpty(value))
						paths = null;
					else
						paths = value.Split(';');
				}
			} // prop Path

			public string[] Path { get { return paths; } }
		} // class LuaLibraryPackage

		#endregion

		private LuaFilePackage io = null;
		private LuaLibraryPackage package = null;
		private Dictionary<object, object> loaded = new Dictionary<object, object>();

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

		// todo:
		//[LuaMember("require")]
		//private LuaResult LuaRequire(object modname)
		//{
		//	if (modname == null)
		//		throw new ArgumentNullException();

		//	// check if the modul is loaded in this global
		//	if (loaded.ContainsKey(modname))
		//		return new LuaResult(loaded[modname]);

		//	// check if the modul is loaded in a different global
		//	LuaChunk chunk = Lua.LuaRequire(this, modname);
		//	if (chunk != null)
		//		return new LuaResult(loaded[modname] = DoChunk(chunk)[0]);
		//	else
		//		return LuaResult.Empty;
		//} // func LuaRequire

		//private bool LuaRequireCheckFile(ref string sFileName, ref DateTime dtStamp)
		//{
		//	try
		//	{
		//		if (!File.Exists(sFileName))
		//			return false;

		//		dtStamp = File.GetLastWriteTime(sFileName);
		//		return true;
		//	}
		//	catch (IOException)
		//	{
		//		return false;
		//	}
		//} // func LuaRequireCheckFile

		//internal bool LuaRequireFindFile(string sPath, string sModName, ref string sFileName, ref DateTime dtStamp)
		//{
		//	if (sPath == "%currentdirectory%")
		//		sPath = Environment.CurrentDirectory;

		//	sFileName = Path.Combine(sPath, sModName + ".lua");
		//	return LuaRequireCheckFile(ref sFileName, ref dtStamp);
		//} // func LuaRequireFindFile

		//internal bool LuaRequireFindFile(string sModName, out string sFileName, out DateTime dtStamp)
		//{
		//	dtStamp = DateTime.MinValue;
		//	sFileName = null;

		//	bool lStdIncluded = false;
		//	string[] paths;
		//	if (package == null || package.Path == null)
		//	{
		//		paths = Lua.StandardPackagesPaths;
		//		lStdIncluded = true;
		//	}
		//	else
		//		paths = package.Path;

		//	foreach (string c in paths)
		//	{
		//		if (String.IsNullOrEmpty(c))
		//		{
		//			if (lStdIncluded)
		//				continue;

		//			foreach (string c1 in Lua.StandardPackagesPaths)
		//			{
		//				if (LuaRequireFindFile(c1, sModName, ref sFileName, ref dtStamp))
		//					return true;
		//			}
		//			lStdIncluded = true;
		//		}
		//		else
		//		{
		//			if (LuaRequireFindFile(c, sModName, ref sFileName, ref dtStamp))
		//				return true;
		//		}
		//	}

		//	return false;
		//} // func LuaRequireFindFile

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

		[LuaMember("debug")]
		private static LuaType LuaLibraryDebug
		{
			get { return LuaType.GetType(typeof(LuaLibraryDebug)); }
		} // prop LuaLibraryDebug

		#endregion
	} // class LuaGlobal

	#endregion
}
