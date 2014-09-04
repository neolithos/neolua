using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace Neo.IronLua
{
	#region -- class LuaGlobal ----------------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public class LuaGlobal : LuaTable
	{
		/// <summary></summary>
		public const string VersionString = "NeoLua 5.3";

		#region -- class LuaIndexPairEnumerator -------------------------------------------

		private class LuaIndexPairEnumerator : System.Collections.IEnumerator
		{
			private LuaTable t;
			private int[] indexes;
			private int iCurrent = -1;

			public LuaIndexPairEnumerator(LuaTable t)
			{
				this.t = t;

				List<int> lst = new List<int>();
				foreach (var c in t)
				{
					if (c.Key is int)
						lst.Add((int)c.Key);
				}
				lst.Sort();
				indexes = lst.ToArray();
			} // ctor

			public object Current
			{
				get
				{
					if (iCurrent >= 0 && iCurrent < indexes.Length)
					{
						int i = indexes[iCurrent];
						return new KeyValuePair<object, object>(i, t[i]);
					}
					else
						return null;
				}
			} // prop Current

			public bool MoveNext()
			{
				iCurrent++;
				return iCurrent < indexes.Length;
			} // func MoveNext

			public void Reset()
			{
				iCurrent = -1;
			} // proc Reset
		} // class LuaIndexPairEnumerator

		#endregion

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

		private Lua lua;
		private LuaFilePackage io = null;
		private LuaLibraryPackage package = null;
		private Dictionary<object, object> loaded = new Dictionary<object, object>();

		#region -- Ctor/Dtor --------------------------------------------------------------

		/// <summary>Create a new environment for the lua script manager.</summary>
		/// <param name="lua"></param>
		public LuaGlobal(Lua lua)
		{
			if (lua == null)
				throw new ArgumentNullException("lua");

			this.lua = lua;

			// Initialize LuaMember
			InitLuaMemberMap(this);
		} // ctor

		#endregion

		#region -- void RegisterPackage ---------------------------------------------------

		/// <summary>Registers a type as an library.</summary>
		/// <param name="sName"></param>
		/// <param name="type"></param>
		public void RegisterPackage(string sName, Type type)
		{
			if (String.IsNullOrEmpty(sName))
				throw new ArgumentNullException("name");
			if (type == null)
				throw new ArgumentNullException("type");

			this[sName] = LuaType.GetType(type);
		} // func RegisterPackage

		#endregion

		#region -- DoChunk ----------------------------------------------------------------

		/// <summary>Compiles and execute the filename.</summary>
		/// <param name="sFileName">Name of the lua file.</param>
		/// <param name="args">Parameter definition for the file.</param>
		/// <returns>Return values of the file.</returns>
		public LuaResult DoChunk(string sFileName, params KeyValuePair<string, object>[] args)
		{
			return DoChunk(sFileName, new StreamReader(sFileName), args);
		} // proc DoFile

		/// <summary>Compiles and execute the stream.</summary>
		/// <param name="sr">Stream</param>
		/// <param name="sName">Name of the stream</param>
		/// <param name="args">Parameter definition for the stream.</param>
		/// <returns>Return values of the stream.</returns>
		public LuaResult DoChunk(TextReader sr, string sName, params KeyValuePair<string, object>[] args)
		{
			return DoChunk(sName, sr, args);
		} // proc DoChunk

		/// <summary>Compiles and executes code.</summary>
		/// <param name="sCode">Lua-Code</param>
		/// <param name="sName">Name of the lua-code</param>
		/// <param name="args">Parameter definition for the lua-code.</param>
		/// <returns>Return values of the lua-code.</returns>
		public LuaResult DoChunk(string sCode, string sName, params KeyValuePair<string, object>[] args)
		{
			return DoChunk(sName, new StringReader(sCode), args);
		} // func DoChunk

		private LuaResult DoChunk(string sChunkName, TextReader tr, KeyValuePair<string, object>[] args)
		{
			// Erzeuge die Parameter
			object[] callArgs;
			KeyValuePair<string, Type>[] callTypes;
			if (args != null)
			{
				callArgs = new object[args.Length];
				callTypes = new KeyValuePair<string, Type>[args.Length];
				for (int i = 0; i < args.Length; i++)
				{
					callArgs[i] = args[i].Value;
					callTypes[i] = new KeyValuePair<string, Type>(args[i].Key, args[i].Value == null ? typeof(object) : args[i].Value.GetType());
				}
			}
			else
			{
				callArgs = new object[0];
				callTypes = new KeyValuePair<string, Type>[0];
			}

			// Führe den Block aus
			return DoChunk(lua.CompileChunk(sChunkName, null, tr, callTypes), callArgs);
		} // func DoChunk

		/// <summary>Executes a precompiled chunk on the lua environment.</summary>
		/// <param name="chunk">Compiled chunk.</param>
		/// <param name="callArgs">Arguments for the chunk.</param>
		/// <returns>Return values of the chunk.</returns>
		public LuaResult DoChunk(LuaChunk chunk, params object[] callArgs)
		{
			if (chunk == null || !chunk.IsCompiled)
				throw new ArgumentException(Properties.Resources.rsChunkNotCompiled, "chunk");
			if (lua != chunk.Lua)
				throw new ArgumentException(Properties.Resources.rsChunkWrongScriptManager, "chunk");

			object[] args = new object[callArgs == null ? 0 : callArgs.Length + 1];
			args[0] = this;
			if (callArgs != null)
				Array.Copy(callArgs, 0, args, 1, callArgs.Length);

			try
			{
				object r = chunk.Chunk.DynamicInvoke(args);
				return r is LuaResult ? (LuaResult)r : new LuaResult(r);
			}
			catch (TargetInvocationException e)
			{
				LuaExceptionData.GetData(e.InnerException); // secure the stacktrace
				throw e.InnerException; // rethrow with new stackstrace
			}
		} // func DoChunk

		#endregion

		#region -- Basic Functions --------------------------------------------------------

		private bool IsTrue(object value)
		{
			if (value == null)
				return false;
			else if (value is bool)
				return (bool)value;
			else
				try
				{
					return Convert.ToBoolean(value);
				}
				catch
				{
					return true;
				}
		} // func IsTrue

		/// <summary></summary>
		/// <param name="value"></param>
		/// <param name="sMessage"></param>
		/// <returns></returns>
		[LuaMember("assert")]
		private object LuaAssert(object value, string sMessage)
		{
			Debug.Assert(IsTrue(value), sMessage);
			return value;
		} // func LuaAssert

		/// <summary></summary>
		/// <param name="opt"></param>
		/// <param name="arg"></param>
		/// <returns></returns>
		[LuaMember("collectgarbage")]
		private LuaResult LuaCollectgarbage(string opt, object arg = null)
		{
			switch (opt)
			{
				case "collect":
					GC.Collect();
					return LuaCollectgarbage("count");
				case "count":
					long iMem = GC.GetTotalMemory(false);
					return new LuaResult(iMem / 1024.0, iMem % 1024);
				case "isrunning":
				case "step":
					return new LuaResult(true);
				case "setpause":
					return new LuaResult(false);
				default:
					return LuaResult.Empty;
			}
		} // func LuaCollectgarbage

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

		/// <summary></summary>
		/// <param name="args"></param>
		/// <returns></returns>
		[LuaMember("dochunk")]
		private LuaResult LuaDoChunk(object[] args)
		{
			if (args == null || args.Length == 0)
				throw new ArgumentException();
			if (args[0] is LuaChunk)
			{
				if (args.Length == 1)
					return DoChunk((LuaChunk)args[0]);
				else
				{
					object[] p = new object[args.Length - 1];
					Array.Copy(args, 1, p, 0, p.Length);
					return DoChunk((LuaChunk)args[0], p);
				}
			}
			else if (args[0] is string)
			{
				if (args.Length == 1)
					return DoChunk((string)args[0], "dummy.lua");
				else if (args.Length == 2)
					return DoChunk((string)args[0], (string)args[1]);
				else
					return DoChunk((string)args[0], (string)args[1], CreateArguments(2, args));
			}
			else if (args[0] is TextReader)
			{
				if (args.Length == 1)
					throw new ArgumentOutOfRangeException();
				else if (args.Length == 2)
					return DoChunk((TextReader)args[0], (string)args[1]);
				else
					return DoChunk((TextReader)args[0], (string)args[1], CreateArguments(2, args));
			}
			else
				throw new ArgumentException();
		} // func LuaDoChunk

		private static KeyValuePair<string, object>[] CreateArguments(int iOffset, object[] args)
		{
			KeyValuePair<string, object>[] p = new KeyValuePair<string, object>[(args.Length - iOffset + 1) / 2]; // on 3 arguments we have 1 parameter

			// create parameter
			for (int i = 0; i < p.Length; i++)
			{
				int j = 2 + i * 2;
				string sName = (string)args[j++];
				object value = j < args.Length ? args[j] : null;
				p[i] = new KeyValuePair<string, object>(sName, value);
			}
			return p;
		} // func CreateArguments

		/// <summary></summary>
		/// <param name="sMessage"></param>
		/// <param name="level"></param>
		[LuaMember("error")]
		private void LuaError(string sMessage, int level)
		{
			if (level == 0)
				level = 1;

			// level ist der StackTrace
			throw new LuaRuntimeException(sMessage, level, true);
		} // proc LuaError

		/// <summary></summary>
		/// <param name="obj"></param>
		/// <returns></returns>
		[LuaMember("getmetatable")]
		private LuaTable LuaGetMetaTable(object obj)
		{
			LuaTable t = obj as LuaTable;
			return t == null ? null : t.MetaTable;
		} // func LuaGetMetaTable

		private LuaResult pairsEnum(object s, object current)
		{
			System.Collections.IEnumerator e = (System.Collections.IEnumerator)s;

			// return value
			if (e.MoveNext())
			{
				KeyValuePair<object, object> k = (KeyValuePair<object, object>)e.Current;
				return new LuaResult(k.Key, k.Value);
			}
			else
				return LuaResult.Empty;
		} // func pairsEnum

		/// <summary></summary>
		/// <param name="t"></param>
		/// <returns></returns>
		[LuaMember("ipairs")]
		private LuaResult LuaIPairs(LuaTable t)
		{
			var e = new LuaIndexPairEnumerator(t);
			return new LuaResult(new Func<object, object, LuaResult>(pairsEnum), e, e);
		} // func ipairs

		/// <summary></summary>
		/// <param name="t"></param>
		/// <returns></returns>
		[LuaMember("pairs")]
		private LuaResult LuaPairs(LuaTable t)
		{
			var e = ((System.Collections.IEnumerable)t).GetEnumerator();
			return new LuaResult(new Func<object, object, LuaResult>(pairsEnum), e, e);
		} // func LuaPairs

		private object LuaLoadReturn(LuaChunk c, LuaGlobal env)
		{
			if (env == null)
				return new Func<LuaResult>(() => this.DoChunk(c));
			else
				return new Func<LuaResult>(() => env.DoChunk(c));
		} // func LuaLoadReturn

		/// <summary></summary>
		/// <param name="ld"></param>
		/// <param name="source"></param>
		/// <param name="mode"></param>
		/// <param name="env"></param>
		/// <returns></returns>
		[LuaMember("load")]
		private object LuaLoad(object ld, string source, string mode, LuaGlobal env)
		{
			if (source == null)
				source = "=(load)";

			if (mode == "b" || !(ld is string)) // binary chunks are not implementeted
				throw new NotImplementedException();

			// create the chunk
			return LuaLoadReturn(lua.CompileChunk((string)ld, source, null), env); // is only disposed, when Lua-Script-Engine disposed.
		} // func LuaLoad

		/// <summary></summary>
		/// <param name="filename"></param>
		/// <param name="mode"></param>
		/// <param name="env"></param>
		/// <returns></returns>
		[LuaMember("loadfile")]
		private object LuaLoadFile(string filename, string mode, LuaGlobal env)
		{
			if (mode == "b") // binary chunks are not implementeted
				throw new NotImplementedException();

			// create the chunk
			return LuaLoadReturn(lua.CompileChunk(filename, null), env);
		} // func LuaLoadFile

		/// <summary></summary>
		/// <param name="t"></param>
		/// <param name="next"></param>
		/// <returns></returns>
		[LuaMember("next")]
		private object LuaNext(LuaTable t, object next = null)
		{
			throw new NotImplementedException();
		} // func LuaNext

		/// <summary></summary>
		/// <param name="dlg"></param>
		/// <param name="args"></param>
		/// <returns></returns>
		[LuaMember("pcall")]
		private LuaResult LuaPCall(Delegate dlg, params object[] args)
		{
			return LuaXPCall(dlg, null, args);
		} // func LuaPCall

		/// <summary></summary>
		/// <param name="sText"></param>
		protected virtual void OnPrint(string sText)
		{
			if (Environment.UserInteractive)
				Console.WriteLine(sText);
			else
				Debug.WriteLine(sText);
		} // proc OnPrint

		/// <summary></summary>
		/// <param name="args"></param>
		[LuaMember("print")]
		private void LuaPrint(params object[] args)
		{
			if (args == null)
				return;

			OnPrint(String.Join(" ", (from a in args select a == null ? String.Empty : a.ToString())));
		} // proc LuaPrint

		/// <summary></summary>
		/// <param name="a"></param>
		/// <param name="b"></param>
		/// <returns></returns>
		[LuaMember("rawequal")]
		private bool LuaRawEqual(object a, object b)
		{
			if (a == null && b == null)
				return true;
			else if (a != null && b != null)
			{
				if (a.GetType() == b.GetType())
				{
					if (a.GetType().IsValueType)
						return Object.Equals(a, b);
					else
						return Object.ReferenceEquals(a, b);
				}
				else
					return false;
			}
			else
				return false;
		} // func LuaRawEqual

		/// <summary></summary>
		/// <param name="t"></param>
		/// <param name="index"></param>
		/// <returns></returns>
		[LuaMember("rawget")]
		private object LuaRawGet(LuaTable t, object index)
		{
			return t.GetValue(index, true);
		} // func LuaRawGet

		/// <summary></summary>
		/// <param name="v"></param>
		/// <returns></returns>
		[LuaMember("rawlen")]
		private int LuaRawLen(object v)
		{
			if (v == null)
				return 0;
			else if (v is LuaTable)
				return ((LuaTable)v).Length;
			else
				return Lua.RtLength(v);
		} // func LuaRawLen

		/// <summary></summary>
		/// <param name="t"></param>
		/// <param name="index"></param>
		/// <param name="value"></param>
		/// <returns></returns>
		[LuaMember("rawset")]
		private LuaTable LuaRawSet(LuaTable t, object index, object value)
		{
			t.SetValue(index, value, true);
			return t;
		} // func LuaRawSet

		[LuaMember("require")]
		private LuaResult LuaRequire(object modname)
		{
			if (modname == null)
				throw new ArgumentNullException();

			// check if the modul is loaded in this global
			if (loaded.ContainsKey(modname))
				return new LuaResult(loaded[modname]);

			// check if the modul is loaded in a different global
			LuaChunk chunk = lua.LuaRequire(this, modname);
			if (chunk != null)
				return new LuaResult(loaded[modname] = DoChunk(chunk)[0]);
			else
				return LuaResult.Empty;
		} // func LuaRequire

		private bool LuaRequireCheckFile(ref string sFileName, ref DateTime dtStamp)
		{
			try
			{
				if (!File.Exists(sFileName))
					return false;

				dtStamp = File.GetLastWriteTime(sFileName);
				return true;
			}
			catch (IOException)
			{
				return false;
			}
		} // func LuaRequireCheckFile

		internal bool LuaRequireFindFile(string sPath, string sModName, ref string sFileName, ref DateTime dtStamp)
		{
			if (sPath == "%currentdirectory%")
				sPath = Environment.CurrentDirectory;

			sFileName = Path.Combine(sPath, sModName + ".lua");
			return LuaRequireCheckFile(ref sFileName, ref dtStamp);
		} // func LuaRequireFindFile

		internal bool LuaRequireFindFile(string sModName, out string sFileName, out DateTime dtStamp)
		{
			dtStamp = DateTime.MinValue;
			sFileName = null;

			bool lStdIncluded = false;
			string[] paths;
			if (package == null || package.Path == null)
			{
				paths = lua.StandardPackagesPaths;
				lStdIncluded = true;
			}
			else
				paths = package.Path;

			foreach (string c in paths)
			{
				if (String.IsNullOrEmpty(c))
				{
					if (lStdIncluded)
						continue;

					foreach (string c1 in lua.StandardPackagesPaths)
					{
						if (LuaRequireFindFile(c1, sModName, ref sFileName, ref dtStamp))
							return true;
					}
					lStdIncluded = true;
				}
				else
				{
					if (LuaRequireFindFile(c, sModName, ref sFileName, ref dtStamp))
						return true;
				}
			}

			return false;
		} // func LuaRequireFindFile

		/// <summary></summary>
		/// <param name="index"></param>
		/// <param name="values"></param>
		/// <returns></returns>
		[LuaMember("select")]
		private LuaResult LuaSelect(int index, params object[] values)
		{
			if (index < 0)
			{
				index = values.Length + index;
				if (index < 0)
					index = 0;
			}

			if (index < values.Length)
			{
				object[] r = new object[values.Length - index];
				Array.Copy(values, index, r, 0, r.Length);
				return r;
			}
			else
				return LuaResult.Empty;
		} // func LuaSelect

		/// <summary></summary>
		/// <param name="t"></param>
		/// <param name="metaTable"></param>
		/// <returns></returns>
		[LuaMember("setmetatable")]
		private LuaTable LuaSetMetaTable(LuaTable t, LuaTable metaTable)
		{
			t.MetaTable = metaTable;
			return t;
		} // proc LuaSetMetaTable

		/// <summary></summary>
		/// <param name="v"></param>
		/// <param name="iBase"></param>
		/// <returns></returns>
		[LuaMember("tonumber")]
		private object LuaToNumber(object v, int iBase)
		{
			if (v == null)
				return null;
			else
			{
				switch (Type.GetTypeCode(v.GetType()))
				{
					case TypeCode.String:
						return lua.ParseNumber((string)v, iBase == 16);
					case TypeCode.SByte:
					case TypeCode.Byte:
					case TypeCode.Int16:
					case TypeCode.UInt16:
					case TypeCode.Int32:
					case TypeCode.UInt32:
					case TypeCode.Int64:
					case TypeCode.UInt64:
					case TypeCode.Single:
					case TypeCode.Double:
					case TypeCode.Decimal:
						return v;
					case TypeCode.Boolean:
						return (bool)v ? 1 : 0;
					default:
						return null;
				}
			}
		} // func LuaToNumber

		/// <summary></summary>
		/// <param name="v"></param>
		/// <returns></returns>
		[LuaMember("tostring")]
		private string LuaToString(object v)
		{
			if (v == null)
				return null;
			else
				return v.ToString();
		} // func LuaToString

		/// <summary></summary>
		/// <param name="v"></param>
		/// <param name="lClr"></param>
		/// <returns></returns>
		[LuaMember("type")]
		private string LuaTypeTest(object v, bool lClr = false)
		{
			if (v == null)
				return "nil";
			else if (v is int || v is double)
				return "number";
			else if (v is string)
				return "string";
			else if (v is bool)
				return "boolean";
			else if (v is LuaTable)
				return "table";
			else if (v is Delegate)
				return "function";
			else if (v is LuaThread)
				return "thread";
			else if (v is LuaFile)
				return ((LuaFile)v).IsClosed ? "closed file" : "file";
			else
				return lClr ? "userdata" : v.GetType().FullName;
		} // func LuaType

		/// <summary></summary>
		/// <param name="dlg"></param>
		/// <param name="msgh"></param>
		/// <param name="args"></param>
		/// <returns></returns>
		[LuaMember("xpcall")]
		private LuaResult LuaXPCall(Delegate dlg, Delegate msgh, params object[] args)
		{
			// call the function save
			try
			{
				return new LuaResult(true, dlg.DynamicInvoke(args));
			}
			catch (Exception e)
			{
				return new LuaResult(false, e.Message, e);
			}
		} // func LuaPCall

		/// <summary></summary>
		[LuaMember("_VERSION")]
		public virtual string Version { get { return VersionString; } }

		#endregion

		#region -- Basic Libraries --------------------------------------------------------

		[LuaMember("table")]
		private LuaType LuaLibraryTable
		{
			get { return LuaType.GetType(typeof(LuaTable)); }
		} // prop LuaLibraryTable

		[LuaMember("coroutine")]
		private LuaType LuaLibraryCoroutine
		{
			get { return LuaType.GetType(typeof(LuaThread)); }
		} // prop LuaLibraryTable

		[LuaMember("io", PropertyLateBind = true)]
		private LuaFilePackage LuaLibraryIO
		{
			get
			{
				if (io == null)
					io = new LuaFilePackage();
				return io;
			}
		} // prop LuaLibraryIO

		[LuaMember("package", PropertyLateBind = true)]
		private LuaLibraryPackage LuaPackage
		{
			get
			{
				if (package == null)
					package = new LuaLibraryPackage(this);
				return package;
			}
		} // prop LuaPackage

		[LuaMember("bit32")]
		private LuaType LuaLibraryBit32
		{
			get { return LuaType.GetType(typeof(LuaLibraryBit32)); }
		} // prop LuaLibraryTable

		[LuaMember("math")]
		private LuaType LuaLibraryMath
		{
			get { return LuaType.GetType(typeof(LuaLibraryMath)); }
		} // prop LuaLibraryTable

		[LuaMember("os")]
		private LuaType LuaLibraryOS
		{
			get { return LuaType.GetType(typeof(LuaLibraryOS)); }
		} // prop LuaLibraryTable

		[LuaMember("string")]
		private LuaType LuaLibraryString
		{
			get { return LuaType.GetType(typeof(LuaLibraryString)); }
		} // prop LuaLibraryTable

		#endregion

		// -- Static --------------------------------------------------------------

		#region -- struct LuaMember -------------------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private struct LuaMember
		{
			public override string ToString()
			{
				return Info.Name + " => " + Member.ToString();
			}
			public LuaMemberAttribute Info;
			public MemberInfo Member;
		} // struct LuaMember

		#endregion

		#region -- class LuaGlobalType ----------------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private sealed class LuaGlobalType
		{
			private readonly Type type;
			private readonly LuaMember[] members;

			#region -- Ctor/Dtor ------------------------------------------------------------

			public LuaGlobalType(Type type)
			{
				this.type = type;

				// collect the type information
				List<LuaMember> collected = new List<LuaMember>();
				Collect(type, collected);
				this.members = collected.ToArray();
			} // ctor

			#endregion

			#region -- Collect --------------------------------------------------------------

			private void Collect(Type type, List<LuaMember> collected)
			{
				// is the type collected
				LuaGlobalType current = Array.Find(memberMap, c => c != null && c.Type == type);

				if (current != null) // dump map
					collected.AddRange(current.members);
				else if (type != typeof(LuaGlobal)) // collect recursive
					Collect(type.BaseType, collected);

				// collect current level
				foreach (MemberInfo mi in type.GetMembers(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.InvokeMethod | BindingFlags.GetProperty | BindingFlags.DeclaredOnly))
				{
					LuaMemberAttribute[] info = (LuaMemberAttribute[])Attribute.GetCustomAttributes(mi, typeof(LuaMemberAttribute));

					for (int i = 0; i < info.Length; i++)
					{
						if (info[i].Name == null) // remove all member
						{
							for (int j = 0; j < collected.Count - 1; j++)
								if (IsOverrideOf(mi, collected[j].Member))
								{
									collected.RemoveAt(j);
									break;
								}
						}
						else
						{
							int iStartIndex = FindMember(collected, info[i].Name);
							if (iStartIndex == -1)
							{
								collected.Add(new LuaMember { Info = info[i], Member = mi });
							}
							else
							{
								// count the overloaded elements
								int iNextIndex = iStartIndex;
								while (iNextIndex < collected.Count && collected[iNextIndex].Info.Name == info[i].Name)
									iNextIndex++;

								// properties it can only exists one property
								if (mi.MemberType == MemberTypes.Property)
								{
									collected.RemoveRange(iStartIndex, iNextIndex - iStartIndex);
									collected.Add(new LuaMember { Info = info[i], Member = mi });
								}
								else // generate overload list
								{
									RemoveUseLessOverloads(collected, (MethodInfo)mi, iStartIndex, ref iNextIndex);
									collected.Insert(iNextIndex, new LuaMember { Info = info[i], Member = mi });
								}
							}
						}
					} // for info
				} // for member
			} // proc Collect

			private void RemoveUseLessOverloads(List<LuaMember> collected, MethodInfo mi, int iStartIndex, ref int iNextIndex)
			{
				while (iStartIndex < iNextIndex)
				{
					MethodInfo miTest = collected[iStartIndex].Member as MethodInfo;

					if (miTest == null || IsOverrideOf(mi, miTest) || SameArguments(mi, miTest))
					{
						collected.RemoveAt(iStartIndex);
						iNextIndex--;
						continue;
					}

					iStartIndex++;
				}
			} // proc RemoveUseLessOverloads

			private bool IsOverrideOf(MemberInfo mi, MemberInfo miTest)
			{
				if (mi.GetType() == miTest.GetType() && mi.Name == miTest.Name)
				{
					if (mi.MemberType == MemberTypes.Property)
						return IsOverridePropertyOf((PropertyInfo)mi, (PropertyInfo)miTest);
					else if (mi.MemberType == MemberTypes.Method)
						return IsOverrideMethodOf((MethodInfo)mi, (MethodInfo)miTest);
					else
						return false;
				}
				else
					return false;
			} // func IsOverrideOf

			private bool IsOverridePropertyOf(PropertyInfo pi, PropertyInfo piTest)
			{
				return IsOverrideMethodOf(pi.GetGetMethod(true), piTest.GetGetMethod(true));
			} // func IsOverridePropertyOf

			private bool IsOverrideMethodOf(MethodInfo mi, MethodInfo miTest)
			{
				MethodInfo miCur = mi;
				while (true)
				{
					if (miCur == miTest)
						return true;
					else if (miCur == miCur.GetBaseDefinition())
						return false;
					miCur = miCur.GetBaseDefinition();
				}
			} // func IsOverrideMethodOf

			private bool SameArguments(MethodInfo mi1, MethodInfo mi2)
			{
				ParameterInfo[] parameterInfo1 = mi1.GetParameters();
				ParameterInfo[] parameterInfo2 = mi2.GetParameters();
				if (parameterInfo1.Length == parameterInfo2.Length)
				{
					for (int i = 0; i < parameterInfo1.Length; i++)
						if (parameterInfo1[i].ParameterType != parameterInfo2[i].ParameterType ||
								parameterInfo1[i].Attributes != parameterInfo2[i].Attributes)
							return false;

					return true;
				}
				else
					return false;
			} // func SameArguments

			private int FindMember(List<LuaMember> collected, string sName)
			{
				for (int i = 0; i < collected.Count; i++)
					if (collected[i].Info.Name == sName)
						return i;
				return -1;
			} // func FindMember

			#endregion

			#region -- Init -----------------------------------------------------------------

			public void Init(LuaGlobal g)
			{
				int i = 0;
				while (i < members.Length)
				{
					int iStart = i;
					int iCount = 1;
					string sCurrentName = members[i].Info.Name;

					// count same elements
					while (++i < members.Length && sCurrentName == members[i].Info.Name)
						iCount++;

					if (iCount == 1) // create single member
					{
						MemberInfo mi = members[iStart].Member;
						if (mi.MemberType == MemberTypes.Property)
							g.SetMemberValue(sCurrentName,
								members[iStart].Info.PropertyLateBind ?
									(object)new LuaMemberDynamicProperty(g, (PropertyInfo)mi) :
									((PropertyInfo)mi).GetValue(g, null),
								false, true);
						else
							g.SetMemberValue(sCurrentName, new LuaMethod(g, (MethodInfo)mi), false, true);
					}
					else //create overloaded member
					{
						MethodInfo[] methods = new MethodInfo[iCount];
						for (int j = 0; j < iCount; j++)
							methods[j] = (MethodInfo)members[iStart + j].Member;
						g.SetMemberValue(sCurrentName, new LuaOverloadedMethod(g, methods), false, true);
					}
				}
			} // proc Init

			#endregion

			public Type Type { get { return type; } }
		} // struct LuaGlobalType

		#endregion

		private static int iMemberMapCount = 0;
		private static LuaGlobalType[] memberMap = new LuaGlobalType[0];
		private static object lockMember = new object();

		private static void InitLuaMemberMap(LuaGlobal g)
		{
			LuaGlobalType map;
			lock (lockMember)
			{
				// is the type collected
				Type typeGlobal = g.GetType();
				map = Array.Find(memberMap, c => c != null && c.Type == typeGlobal);
				if (map == null) // collect the infomration
				{
					map = new LuaGlobalType(typeGlobal);

					if (iMemberMapCount == memberMap.Length)
					{
						LuaGlobalType[] newMemberMap = new LuaGlobalType[memberMap.Length + 4];
						Array.Copy(memberMap, 0, newMemberMap, 0, memberMap.Length);
						memberMap = newMemberMap;
					}

					memberMap[iMemberMapCount++] = map;
				}
			}

			// initialize global
			map.Init(g);
		} // func GetLuaMemberMap
	} // class LuaGlobal

	#endregion

	#region -- class LuaMemberAttribute -------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Marks a function or a GET property for the global namespace.</summary>
	[AttributeUsage(AttributeTargets.Property | AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
	public sealed class LuaMemberAttribute : Attribute
	{
		private string sName;
		private bool lDynamicBind;

		/// <summary>Marks global Members, they act normally as library</summary>
		/// <param name="sName"></param>
		public LuaMemberAttribute(string sName)
		{
			this.sName = sName;
		} // ctor

		/// <summary>Global name of the function.</summary>
		public string Name { get { return sName; } }
		/// <summary>Do not initialize property direct. Do create a dynamic proxy.</summary>
		public bool PropertyLateBind { get { return lDynamicBind; } set { lDynamicBind = value; } }
	} // class LuaLibraryAttribute

	#endregion

	#region -- class LuaMemberDynamicProperty -------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public sealed class LuaMemberDynamicProperty : IDynamicMetaObjectProvider
	{
		#region -- class LuaMemberPropertyMetaObject --------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private class LuaMemberPropertyMetaObject : DynamicMetaObject
		{
			#region -- Ctor/Dtor ------------------------------------------------------------

			public LuaMemberPropertyMetaObject(LuaMemberDynamicProperty value, Expression parameter)
				: base(parameter, BindingRestrictions.Empty, value)
			{
			} // ctor

			#endregion

			#region -- GetTargetMetaObject --------------------------------------------------

			private DynamicMetaObject GetTargetMetaObject()
			{
				LuaMemberDynamicProperty p = (LuaMemberDynamicProperty)Value;
				return new DynamicMetaObject(

					Expression.Property(
						Expression.Convert(
							Expression.Property(Expression.Convert(Expression, typeof(LuaMemberDynamicProperty)), Lua.LuaMemberPropertyInstancePropertyInfo),
							p.property.DeclaringType
						), p.property),
					BindingRestrictions.GetTypeRestriction(Expression, typeof(LuaMemberDynamicProperty))
					.Merge(BindingRestrictions.GetExpressionRestriction(Expression.Equal(Expression.Property(Expression.Convert(Expression, typeof(LuaMemberDynamicProperty)), Lua.LuaMemberPropertyPropertyPropertyInfo), Expression.Constant(p.property))))
				);
			} // func GetTargetMetaObject

			#endregion

			#region -- Binder ---------------------------------------------------------------

			public override DynamicMetaObject BindUnaryOperation(UnaryOperationBinder binder)
			{
				return binder.FallbackUnaryOperation(GetTargetMetaObject());
			} // func BindUnaryOperation

			public override DynamicMetaObject BindBinaryOperation(BinaryOperationBinder binder, DynamicMetaObject arg)
			{
				return binder.FallbackBinaryOperation(GetTargetMetaObject(), arg);
			} // func BindBinaryOperation

			public override DynamicMetaObject BindCreateInstance(CreateInstanceBinder binder, DynamicMetaObject[] args)
			{
				return binder.FallbackCreateInstance(GetTargetMetaObject(), args);
			} // func BindCreateInstance

			public override DynamicMetaObject BindGetMember(GetMemberBinder binder)
			{
				return binder.FallbackGetMember(GetTargetMetaObject());
			} // func BindGetMember

			public override DynamicMetaObject BindSetMember(SetMemberBinder binder, DynamicMetaObject value)
			{
				return binder.FallbackSetMember(GetTargetMetaObject(), value);
			} // func BindSetMember

			public override DynamicMetaObject BindDeleteMember(DeleteMemberBinder binder)
			{
				return binder.FallbackDeleteMember(GetTargetMetaObject());
			} // func BindDeleteMember

			public override DynamicMetaObject BindGetIndex(GetIndexBinder binder, DynamicMetaObject[] indexes)
			{
				return binder.FallbackGetIndex(GetTargetMetaObject(), indexes);
			} // func BindGetIndex

			public override DynamicMetaObject BindSetIndex(SetIndexBinder binder, DynamicMetaObject[] indexes, DynamicMetaObject value)
			{
				return binder.FallbackSetIndex(GetTargetMetaObject(), indexes, value);
			} // func BindSetIndex

			public override DynamicMetaObject BindDeleteIndex(DeleteIndexBinder binder, DynamicMetaObject[] indexes)
			{
				return binder.FallbackDeleteIndex(GetTargetMetaObject(), indexes);
			} // func BindDeleteIndex

			public override DynamicMetaObject BindInvoke(InvokeBinder binder, DynamicMetaObject[] args)
			{
				return binder.FallbackInvoke(GetTargetMetaObject(), args);
			} // func BindInvoke

			public override DynamicMetaObject BindInvokeMember(InvokeMemberBinder binder, DynamicMetaObject[] args)
			{
				return binder.FallbackInvokeMember(GetTargetMetaObject(), args);
			} // func BindInvokeMember

			public override DynamicMetaObject BindConvert(ConvertBinder binder)
			{
				return binder.FallbackConvert(GetTargetMetaObject());
			} // func BindConvert

			#endregion
		} // class LuaMemberPropertyMetaObject

		#endregion

		private object instance;
		private PropertyInfo property;

		#region -- Ctor/Dtor --------------------------------------------------------------

		internal LuaMemberDynamicProperty(object instance, PropertyInfo property)
		{
			this.instance = instance;
			this.property = property;
		} // ctor

		/// <summary></summary>
		/// <param name="parameter"></param>
		/// <returns></returns>
		public DynamicMetaObject GetMetaObject(Expression parameter)
		{
			return new LuaMemberPropertyMetaObject(this, parameter);
		} // func GetMetaObject
	
		/// <summary></summary>
		/// <returns></returns>
		public override string ToString()
		{
			return "dynamic property [ " + property.ToString() + "]";
		} // func ToString

		#endregion

		/// <summary></summary>
		public object Instance { get { return instance; } }
		/// <summary></summary>
		public PropertyInfo Property { get { return property; } }
	} // class LuaMemberProperty

	#endregion
}
