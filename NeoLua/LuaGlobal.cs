using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace Neo.IronLua
{
	#region -- class LuaGlobalPortable ------------------------------------------------

	/// <summary>Basic implementation of all lua packages and functions, that 
	/// are compatible with the portable library.</summary>
	public class LuaGlobal : LuaTable
	{
		/// <summary></summary>
		public const string VersionString = "NeoLua 5.3";

		private readonly Lua lua;

		#region -- Ctor/Dtor ----------------------------------------------------------

		/// <summary>Create a new environment for the lua script manager/compiler.</summary>
		/// <param name="lua">The lua script compiler.</param>
		public LuaGlobal(Lua lua)
		{
			this.lua = lua ?? throw new ArgumentNullException(nameof(lua));
		} // ctor

		/// <summary>Redirects the invoke binder to the script manager/compiler.</summary>
		/// <param name="callInfo"></param>
		/// <returns></returns>
		protected override CallSiteBinder GetInvokeBinder(CallInfo callInfo)
			=> lua.GetInvokeBinder(callInfo);

		#endregion

		#region -- void RegisterPackage -----------------------------------------------

		/// <summary>Registers a type as an library.</summary>
		/// <param name="name"></param>
		/// <param name="type"></param>
		public void RegisterPackage(string name, Type type)
		{
			if (String.IsNullOrEmpty(name))
				throw new ArgumentNullException(nameof(name));
			if (type == null)
				throw new ArgumentNullException(nameof(type));

			this[name] = LuaType.GetType(type);
		} // func RegisterPackage

		#endregion

		#region -- DoChunk ------------------------------------------------------------

		/// <summary>Compiles and execute the filename.</summary>
		/// <param name="fileName">Name of the lua file.</param>
		/// <param name="args">Parameter definition for the file.</param>
		/// <returns>Return values of the file.</returns>
		public LuaResult DoChunk(string fileName, params KeyValuePair<string, object>[] args)
		{
			using (var sr = new StreamReader(fileName))
				return DoChunk(sr, fileName, args);
		} // proc DoFile

		/// <summary>Compiles and executes code.</summary>
		/// <param name="code">Lua-Code</param>
		/// <param name="name">Name of the lua-code</param>
		/// <param name="args">Parameter definition for the lua-code.</param>
		/// <returns>Return values of the lua-code.</returns>
		public LuaResult DoChunk(string code, string name, params KeyValuePair<string, object>[] args)
		{
			using (var tr = new StringReader(code))
				return DoChunk(tr, name, args);
		} // func DoChunk

		/// <summary>Compiles and execute the stream.</summary>
		/// <param name="tr">Stream</param>
		/// <param name="name">Name of the stream</param>
		/// <param name="args">Parameter definition for the stream.</param>
		/// <returns>Return values of the stream.</returns>
		public LuaResult DoChunk(TextReader tr, string name, params KeyValuePair<string, object>[] args)
		{
			// Erzeuge die Parameter
			object[] callArgs;
			KeyValuePair<string, Type>[] callTypes;
			if (args != null)
			{
				callArgs = new object[args.Length];
				callTypes = new KeyValuePair<string, Type>[args.Length];
				for (var i = 0; i < args.Length; i++)
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

			// execute block, set compile options
			return DoChunk(lua.CompileChunk(tr, name, DefaultCompileOptions, callTypes), callArgs);
		} // proc DoChunk

		/// <summary>Executes a precompiled chunk on the lua environment.</summary>
		/// <param name="chunk">Compiled chunk.</param>
		/// <param name="callArgs">Arguments for the chunk.</param>
		/// <returns>Return values of the chunk.</returns>
		public LuaResult DoChunk(LuaChunk chunk, params object[] callArgs)
		{
			if (chunk == null)
				throw new ArgumentException(Properties.Resources.rsChunkNotCompiled, nameof(chunk));
			
			return chunk.Run(this, callArgs);
		} // func DoChunk

		/// <summary>Change the default compile options of dochunk or dofile</summary>
		/// <example>DefaultCompileOptions = Lua.StackTraceCompileOptions;</example>
		public LuaCompileOptions DefaultCompileOptions { get; set; } = null;

		#endregion

		#region -- Basic Functions ----------------------------------------------------

		#region -- class ArrayIndexEnumerator -----------------------------------------

		private sealed class ArrayIndexEnumerator : IEnumerable<KeyValuePair<int, object>>
		{
			private readonly IEnumerable<object> array;

			public ArrayIndexEnumerator(IEnumerable<object> array)
			{
				this.array = array;
			} // ctor

			public IEnumerator<KeyValuePair<int, object>> GetEnumerator()
			{
				var i = 1;
				foreach (var c in array)
					yield return new KeyValuePair<int, object>(i++, c);
			} // func GetEnumerator

			IEnumerator IEnumerable.GetEnumerator()
				=> GetEnumerator();
		} // class ArrayIndexEnumerator

		#endregion

		private static bool IsTrue(object value)
		{
			if (value == null)
				return false;
			else if (value is bool l)
				return l;
			else
			{
				try
				{
					return Convert.ToBoolean(value);
				}
				catch
				{
					return true;
				}
			}
		} // func IsTrue

		internal static KeyValuePair<string, object>[] CreateArguments(int offset, object[] args)
		{
			var p = new KeyValuePair<string, object>[(args.Length - offset + 1) / 2]; // on 3 arguments we have 1 parameter

			// create parameter
			for (var i = 0; i < p.Length; i++)
			{
				var j = 2 + i * 2;
				var name = (string)args[j++];
				var value = j < args.Length ? args[j] : null;
				p[i] = new KeyValuePair<string, object>(name, value);
			}

			return p;
		} // func CreateArguments

		/// <summary></summary>
		/// <param name="value"></param>
		/// <param name="message"></param>
		/// <returns></returns>
		[LuaMember("assert")]
		public static object LuaAssert(object value, string message)
		{
			if (!IsTrue(value))
				LuaError(message ?? "assertion failed!", 1);
			return value;
		} // func LuaAssert

		/// <summary></summary>
		/// <param name="opt"></param>
		/// <param name="arg"></param>
		/// <returns></returns>
		[LuaMember("collectgarbage")]
		public static LuaResult LuaCollectgarbage(string opt, object arg = null)
		{
			switch (opt)
			{
				case "collect":
					GC.Collect();
					return LuaCollectgarbage("count");
				case "count":
					var mem = GC.GetTotalMemory(false);
					return new LuaResult(mem / 1024.0, mem % 1024);
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

			if (args[0] is LuaChunk chunk)
			{
				if (args.Length == 1)
					return DoChunk(chunk);
				else
				{
					var p = new object[args.Length - 1];
					Array.Copy(args, 1, p, 0, p.Length);
					return DoChunk(chunk, p);
				}
			}
			else if (args[0] is string code)
			{
				if (args.Length == 1)
					return DoChunk(code, "dummy.lua");
				else if (args.Length == 2)
					return DoChunk(code, (string)args[1]);
				else
					return DoChunk(code, (string)args[1], CreateArguments(2, args));
			}
			else if (args[0] is TextReader reader)
			{
				if (args.Length == 1)
					throw new ArgumentOutOfRangeException();
				else if (args.Length == 2)
					return DoChunk(reader, (string)args[1]);
				else
					return DoChunk(reader, (string)args[1], CreateArguments(2, args));
			}
			else
				throw new ArgumentException();
		} // func LuaDoChunk

		#region -- loadfile -----------------------------------------------------------

		#region -- class LuaLoadReturnClosure -----------------------------------------

		private class LuaLoadReturnClosure
		{
			public LuaTable env; // force the environment on the first index
			public LuaChunk chunk;
			public LuaGlobal @this;

			public LuaResult Run(object[] callArgs)
				=> chunk.Run(env ?? @this, new object[] { callArgs ?? LuaResult.Empty.Values });
		} // class LuaLoadReturnClosure

		#endregion

		private object LuaLoadReturn(LuaChunk c, LuaTable defaultEnv)
		{
			var run = new LuaLoadReturnClosure
			{
				env = defaultEnv,
				chunk = c,
				@this = this
			};
			return new Func<object[], LuaResult>(run.Run);
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
					var sbCode = new StringBuilder();
					string part;
					while (!String.IsNullOrEmpty(part = (string)new LuaResult(RtInvokeSite(ld))[0]))
						sbCode.Append(part);
					ld = sbCode.ToString();
				}

				// create the chunk
				return LuaLoadReturn(Lua.CompileChunk((string)ld, source, DefaultCompileOptions, new KeyValuePair<string, Type>("...", typeof(object[]))), env);
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
			return LuaLoadReturn(Lua.CompileChunk(filename, DefaultCompileOptions, new KeyValuePair<string, Type>("...", typeof(object[]))), env);
		} // func LuaLoadFile

		#endregion

		#region -- LuaRequire ---------------------------------------------------------

		internal readonly Dictionary<object, object> loaded = new Dictionary<object, object>();

		[LuaMember("require")]
		private LuaResult LuaRequire(object modname)
		{
			if (modname == null)
				throw new ArgumentNullException();

			// check if the modul is loaded in this global
			if (loaded.TryGetValue(modname, out var currentlyLoaded))
				return new LuaResult(currentlyLoaded);

			// check if the modul is loaded in a different global
			var chunk = ((LuaLibraryPackage)LuaPackage).LuaRequire(this, modname as string);
			if (chunk != null)
				return new LuaResult(loaded[modname] = DoChunk(chunk)[0]);
			else
				return LuaResult.Empty;
		} // func LuaRequire

		#endregion

		/// <summary></summary>
		/// <param name="message"></param>
		/// <param name="level"></param>
		[LuaMember("error")]
		public static void LuaError(object message, int level)
		{
			if (level == 0)
				level = 1;

			// level ist der StackTrace
			if (message is Exception)
				throw (Exception)message;
			else
				throw new LuaRuntimeException(message?.ToString() ?? String.Empty, level, true);
		} // proc LuaError

		/// <summary></summary>
		/// <param name="obj"></param>
		/// <returns></returns>
		[LuaMember("getmetatable")]
		public static LuaTable LuaGetMetaTable(object obj)
			=> obj is LuaTable t ? t.MetaTable : null;

		/// <summary></summary>
		/// <param name="t"></param>
		/// <returns></returns>
		[LuaMember("rawmembers")]
		public static IEnumerable<KeyValuePair<string, object>> LuaRawMembers(LuaTable t)
		{
			if (t == null)
				throw new ArgumentNullException("#1");

			return t.Members;
		} // func LuaRawMembers

		/// <summary></summary>
		/// <param name="t"></param>
		/// <returns></returns>
		[LuaMember("rawarray")]
		public static IList<object> LuaRawArray(LuaTable t)
		{
			if (t == null)
				throw new ArgumentNullException("#1");

			return t.ArrayList;
		} // func LuaRawArray

		private static LuaResult PairsEnum<TKey>(object s, object current)
		{
			var e = (System.Collections.IEnumerator)s;

			// return value
			if (e.MoveNext())
			{
				var k = (KeyValuePair<TKey, object>)e.Current;
				return new LuaResult(k.Key, k.Value);
			}
			else
			{
				var d = e as IDisposable;
				d?.Dispose();
				return LuaResult.Empty;
			}
		} // func pairsEnum

		/// <summary></summary>
		/// <param name="t"></param>
		/// <returns></returns>
		[LuaMember("ipairs")]
		public static LuaResult LuaIPairs(LuaTable t)
		{
			if (t == null)
				throw new ArgumentNullException("#1");

			var e = new ArrayIndexEnumerator(t.ArrayList).GetEnumerator(); // todo: possible memory leak if the enumeration does not reach the end
			return new LuaResult(new Func<object, object, LuaResult>(PairsEnum<int>), e, e);
		} // func ipairs

		/// <summary></summary>
		/// <param name="t"></param>
		/// <returns></returns>
		[LuaMember("mpairs")]
		public static LuaResult LuaMPairs(LuaTable t)
		{
			if (t == null)
				throw new ArgumentNullException("#1");

			var e = t.Members.GetEnumerator(); // todo: possible memory leak if the enumeration does not reach the end
			return new LuaResult(new Func<object, object, LuaResult>(PairsEnum<string>), e, e);
		} // func LuaPairs

		/// <summary></summary>
		/// <param name="t"></param>
		/// <returns></returns>
		[LuaMember("pairs")]
		public static LuaResult LuaPairs(LuaTable t)
		{
			if (t == null)
				throw new ArgumentNullException("#1");

			var e = ((System.Collections.IEnumerable)t).GetEnumerator(); // todo: possible memory leak if the enumeration does not reach the end
			return new LuaResult(new Func<object, object, LuaResult>(PairsEnum<object>), e, e);
		} // func LuaPairs

		/// <summary></summary>
		/// <param name="t"></param>
		/// <param name="next"></param>
		/// <returns></returns>
		[LuaMember("next")]
		private static LuaResult LuaNext(LuaTable t, object next)
		{
			if (t == null)
				return null;
			var n = t.NextKey(next);
			return new LuaResult(n, t[n]);
		} // func LuaNext

		/// <summary></summary>
		/// <param name="t"></param>
		/// <param name="next"></param>
		/// <returns></returns>
		[LuaMember("nextKey")]
		private static object LuaNextKey(LuaTable t, object next)
			=> t?.NextKey(next);

		/// <summary></summary>
		/// <param name="target"></param>
		/// <param name="args"></param>
		/// <returns></returns>
		[LuaMember("pcall")]
		private LuaResult LuaPCall(object target, params object[] args)
			=> LuaXPCall(target, null, args);

		/// <summary></summary>
		/// <param name="text"></param>
		protected virtual void OnPrint(string text)
		{
			if (Environment.UserInteractive)
				Console.WriteLine(text);
			else
				Debug.WriteLine(text);
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
		public static bool LuaRawEqual(object a, object b)
		{
			var aIsNull = a is null;
			var bIsNull = b is null;
			if (aIsNull && bIsNull)
				return true;
			else if (!(aIsNull || bIsNull))
			{
				if (a.GetType() == b.GetType())
				{
					return a.GetType().GetTypeInfo().IsValueType
						? Equals(a, b)
						: ReferenceEquals(a, b);
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
		public static object LuaRawGet(LuaTable t, object index)
			=> t.GetValue(index, true);

		/// <summary></summary>
		/// <param name="v"></param>
		/// <returns></returns>
		[LuaMember("rawlen")]
		public static int LuaRawLen(object v)
		{
			if (v == null)
				return 0;
			else if (v is LuaTable t)
				return t.Length;
			else
				return Lua.RtLength(v);
		} // func LuaRawLen

		/// <summary></summary>
		/// <param name="t"></param>
		/// <param name="index"></param>
		/// <param name="value"></param>
		/// <returns></returns>
		[LuaMember("rawset")]
		public static LuaTable LuaRawSet(LuaTable t, object index, object value)
		{
			t.SetValue(index, value, true);
			return t;
		} // func LuaRawSet

		/// <summary></summary>
		/// <param name="index"></param>
		/// <param name="values"></param>
		/// <returns></returns>
		[LuaMember("select")]
		public static LuaResult LuaSelect(string index, params object[] values)
		{
			if (index == "#")
				return new LuaResult(values.Length);
			else
			{
				var idx = Convert.ToInt32(Lua.RtParseNumber(index, true) ?? 0);

				if (idx < 0)
				{
					idx = values.Length + idx;
					if (idx < 0)
						idx = 0;
				}
				if (idx < values.Length)
				{
					var r = new object[values.Length - idx];
					Array.Copy(values, idx, r, 0, r.Length);
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
		public static LuaTable LuaSetMetaTable(LuaTable t, LuaTable metaTable)
		{
			t.MetaTable = metaTable;
			return t;
		} // proc LuaSetMetaTable

		/// <summary></summary>
		/// <param name="v"></param>
		/// <param name="iBase"></param>
		/// <returns></returns>
		[LuaMember("tonumber")]
		public object LuaToNumber(object v, int? iBase = null)
		{
			if (v == null)
				return null;
			else
			{
				switch (LuaEmit.GetTypeCode(v.GetType()))
				{
					case LuaEmitTypeCode.String:
						if (iBase.HasValue)
							return Lua.RtParseNumber(null, (string)v, 0, iBase.Value, lua.FloatType == LuaFloatType.Double, false);
						else
							return Lua.RtParseNumber((string)v, lua.FloatType == LuaFloatType.Double, false);
					case LuaEmitTypeCode.SByte:
					case LuaEmitTypeCode.Byte:
					case LuaEmitTypeCode.Int16:
					case LuaEmitTypeCode.UInt16:
					case LuaEmitTypeCode.Int32:
					case LuaEmitTypeCode.UInt32:
					case LuaEmitTypeCode.Int64:
					case LuaEmitTypeCode.UInt64:
					case LuaEmitTypeCode.Single:
					case LuaEmitTypeCode.Double:
					case LuaEmitTypeCode.Decimal:
						return v;
					case LuaEmitTypeCode.Boolean:
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
		public static string LuaToString(object v)
			=> v == null
				? "nil"
				: (string)Lua.RtConvertValue(v, typeof(string));

		/// <summary></summary>
		/// <param name="v"></param>
		/// <param name="clr"></param>
		/// <returns></returns>
		[LuaMember("type")]
		public static string LuaTypeTest(object v, bool clr = false)
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
			else if (v is LuaFile f)
				return f.IsClosed ? "closed file" : "file";
			else
				return clr ? v.GetType().FullName : "userdata";
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
			catch (TargetInvocationException e)
			{
				return new LuaResult(false, msgh ?? e.InnerException.Message, e.InnerException);
			}
			catch (Exception e)
			{
				return new LuaResult(false, msgh ?? e.Message, e);
			}
		} // func LuaXPCall

		/// <summary></summary>
		[LuaMember("_VERSION")]
		public virtual string Version { get { return VersionString; } }

		#endregion

		#region -- Basic Libraries ----------------------------------------------------

		private LuaFilePackage io = null;
		private LuaLibraryPackage package = null;

		[LuaMember("coroutine")]
		private static dynamic LuaLibraryCoroutine => LuaType.GetType(typeof(LuaThread));

		[LuaMember("bit32")]
		private static dynamic LuaLibraryBit32 => LuaType.GetType(typeof(LuaLibraryBit32));

		[LuaMember("debug")]
		private static dynamic LuaLibraryDebug => LuaType.GetType(typeof(LuaLibraryDebug));

		[LuaMember("math")]
		private static dynamic LuaLibraryMath => LuaType.GetType(typeof(LuaLibraryMath));

		[LuaMember("string")]
		private static dynamic LuaLibraryString => LuaType.GetType(typeof(LuaLibraryString));

		[LuaMember("table")]
		private static dynamic LuaLibraryTable => LuaType.GetType(typeof(LuaTable));

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
		private static LuaType LuaLibraryOS => LuaType.GetType(typeof(LuaLibraryOS));

		#endregion

		/// <summary>Access to the assigned Lua script manager</summary>
		public Lua Lua => lua;
	} // class LuaGlobalPortable

	#endregion
}
