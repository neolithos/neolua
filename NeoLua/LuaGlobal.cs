using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
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

		/// <summary></summary>
		/// <param name="callInfo"></param>
		/// <returns></returns>
		protected override CallSiteBinder GetInvokeBinder(CallInfo callInfo)
		{
			return lua.GetInvokeBinder(callInfo);
		} // func GetInvokeBinder

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

		private static bool IsTrue(object value)
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
		private static object LuaAssert(object value, string sMessage)
		{
			Debug.Assert(IsTrue(value), sMessage);
			return value;
		} // func LuaAssert

		/// <summary></summary>
		/// <param name="opt"></param>
		/// <param name="arg"></param>
		/// <returns></returns>
		[LuaMember("collectgarbage")]
		private static LuaResult LuaCollectgarbage(string opt, object arg = null)
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
		private static void LuaError(string sMessage, int level)
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
		private static LuaTable LuaGetMetaTable(object obj)
		{
			LuaTable t = obj as LuaTable;
			return t == null ? null : t.MetaTable;
		} // func LuaGetMetaTable

		private static LuaResult pairsEnum(object s, object current)
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
		private static LuaResult LuaIPairs(LuaTable t)
		{
			var e = new LuaIndexPairEnumerator(t);
			return new LuaResult(new Func<object, object, LuaResult>(pairsEnum), e, e);
		} // func ipairs

		/// <summary></summary>
		/// <param name="t"></param>
		/// <returns></returns>
		[LuaMember("pairs")]
		private static LuaResult LuaPairs(LuaTable t)
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
			if (String.IsNullOrEmpty(source))
				source = "=(load)";

			if (mode == "b" || !(ld is string || ld is LuaMethod || ld is Delegate)) // binary chunks are not implementeted
				throw new NotImplementedException();

			try
			{
				// collect the chunks
				if (Lua.IsCallable(ld))
				{
					StringBuilder sbCode = new StringBuilder();
					string sPart;
					while (!String.IsNullOrEmpty(sPart = (string)new LuaResult(RtInvokeSite(ld))[0]))
						sbCode.Append(sPart);
					ld = sbCode.ToString();
				}
				// create the chunk
				return LuaLoadReturn(lua.CompileChunk((string)ld, source, null), env); // is only disposed, when Lua-Script-Engine disposed.
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
		private static object LuaNext(LuaTable t, object next)
		{
			if (t == null)
				return null;
			else
				return t.NextKey(next);
		} // func LuaNext

		/// <summary></summary>
		/// <param name="target"></param>
		/// <param name="args"></param>
		/// <returns></returns>
		[LuaMember("pcall")]
		private LuaResult LuaPCall(object target, params object[] args)
		{
			return LuaXPCall(target, null, args);
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
		private static bool LuaRawEqual(object a, object b)
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
		private static object LuaRawGet(LuaTable t, object index)
		{
			return t.GetValue(index, true);
		} // func LuaRawGet

		/// <summary></summary>
		/// <param name="v"></param>
		/// <returns></returns>
		[LuaMember("rawlen")]
		private static int LuaRawLen(object v)
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
		private static LuaResult LuaSelect(string index, params object[] values)
		{
			if (index == "#")
				return new LuaResult(values.Length);
			else
			{
				int iIndex = Convert.ToInt32(Lua.RtParseNumber(index, true) ?? 0);

				if (iIndex < 0)
				{
					iIndex = values.Length + iIndex;
					if (iIndex < 0)
						iIndex = 0;
				}
				if (iIndex < values.Length)
				{
					object[] r = new object[values.Length - iIndex];
					Array.Copy(values, iIndex, r, 0, r.Length);
					return r;
				}
				else
					return LuaResult.Empty;
			}
		} // func LuaSelect

		/// <summary></summary>
		/// <param name="t"></param>
		/// <param name="metaTable"></param>
		/// <returns></returns>
		[LuaMember("setmetatable")]
		private static LuaTable LuaSetMetaTable(LuaTable t, LuaTable metaTable)
		{
			t.MetaTable = metaTable;
			return t;
		} // proc LuaSetMetaTable

		/// <summary></summary>
		/// <param name="v"></param>
		/// <param name="iBase"></param>
		/// <returns></returns>
		[LuaMember("tonumber")]
		private object LuaToNumber(object v, Nullable<int> iBase = null)
		{
			if (v == null)
				return null;
			else
			{
				switch (Type.GetTypeCode(v.GetType()))
				{
					case TypeCode.String:
						if (iBase.HasValue)
							return Lua.RtParseNumber(null, (string)v, 0, iBase.Value, lua.FloatType == LuaFloatType.Double, false);
						else
							return Lua.RtParseNumber((string)v, lua.FloatType == LuaFloatType.Double, false);
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
		private static string LuaToString(object v)
		{
			if (v == null)
				return "nil";
			else
				return (string)Lua.RtConvertValue(v, typeof(string));
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
			else if (v is Delegate || v is ILuaMethod)
				return "function";
			else if (v is LuaThread)
				return "thread";
			else if (v is LuaFile)
				return ((LuaFile)v).IsClosed ? "closed file" : "file";
			else
				return lClr ? v.GetType().FullName : "userdata";
		} // func LuaType

		/// <summary></summary>
		/// <param name="target"></param>
		/// <param name="msgh"></param>
		/// <param name="args"></param>
		/// <returns></returns>
		[LuaMember("xpcall")]
		private LuaResult LuaXPCall(object target, object msgh, params object[] args)
		{
			// call the function save
			try
			{
				return new LuaResult(true, RtInvokeSite(target, args));
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
		private static LuaType LuaLibraryTable
		{
			get { return LuaType.GetType(typeof(LuaTable)); }
		} // prop LuaLibraryTable

		[LuaMember("coroutine")]
		private static LuaType LuaLibraryCoroutine
		{
			get { return LuaType.GetType(typeof(LuaThread)); }
		} // prop LuaLibraryTable

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

		[LuaMember("bit32")]
		private static LuaType LuaLibraryBit32
		{
			get { return LuaType.GetType(typeof(LuaLibraryBit32)); }
		} // prop LuaLibraryTable

		[LuaMember("math")]
		private static LuaType LuaLibraryMath
		{
			get { return LuaType.GetType(typeof(LuaLibraryMath)); }
		} // prop LuaLibraryTable

		[LuaMember("os")]
		private static LuaType LuaLibraryOS
		{
			get { return LuaType.GetType(typeof(LuaLibraryOS)); }
		} // prop LuaLibraryTable

		[LuaMember("string")]
		private static LuaType LuaLibraryString
		{
			get { return LuaType.GetType(typeof(LuaLibraryString)); }
		} // prop LuaLibraryTable

		#endregion

		/// <summary>Access to the assigned Lua script manager</summary>
		public Lua Lua { get { return lua; } }
	} // class LuaGlobal

	#endregion

	#region -- class LuaMemberAttribute -------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Marks a function or a GET property for the global namespace.</summary>
	[AttributeUsage(AttributeTargets.Property | AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
	public sealed class LuaMemberAttribute : Attribute
	{
		private string sName;

		/// <summary>Marks global Members, they act normally as library</summary>
		/// <param name="sName"></param>
		public LuaMemberAttribute(string sName)
		{
			this.sName = sName;
		} // ctor

		/// <summary>Global name of the function.</summary>
		public string Name { get { return sName; } }
	} // class LuaLibraryAttribute

	#endregion
}
