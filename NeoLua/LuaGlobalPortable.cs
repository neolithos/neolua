using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Neo.IronLua
{
	#region -- class LuaGlobalPortable --------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Basic implementation of all lua packages and functions, that 
	/// are compatible with the portable library.</summary>
	public class LuaGlobalPortable : LuaTable
	{
		/// <summary></summary>
		public const string VersionString = "NeoLua 5.3";

		private readonly Lua lua;

		#region -- Ctor/Dtor --------------------------------------------------------------

		/// <summary>Create a new environment for the lua script manager/compiler.</summary>
		/// <param name="lua">The lua script compiler.</param>
		public LuaGlobalPortable(Lua lua)
		{
			if (lua == null)
				throw new ArgumentNullException("lua");

			this.lua = lua;
		} // ctor

		/// <summary>Redirects the invoke binder to the script manager/compiler.</summary>
		/// <param name="callInfo"></param>
		/// <returns></returns>
		protected override CallSiteBinder GetInvokeBinder(CallInfo callInfo)
		{
			return lua.GetInvokeBinder(callInfo);
		} // func GetInvokeBinder

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

		/// <summary>Compiles and executes code.</summary>
		/// <param name="sCode">Lua-Code</param>
		/// <param name="sName">Name of the lua-code</param>
		/// <param name="args">Parameter definition for the lua-code.</param>
		/// <returns>Return values of the lua-code.</returns>
		public LuaResult DoChunk(string sCode, string sName, params KeyValuePair<string, object>[] args)
		{
			using (var tr = new StringReader(sCode))
				return DoChunk(tr, sName, args);
		} // func DoChunk

		/// <summary>Compiles and execute the stream.</summary>
		/// <param name="tr">Stream</param>
		/// <param name="sName">Name of the stream</param>
		/// <param name="args">Parameter definition for the stream.</param>
		/// <returns>Return values of the stream.</returns>
		public LuaResult DoChunk(TextReader tr, string sName, params KeyValuePair<string, object>[] args)
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
			return DoChunk(lua.CompileChunk(sName, null, tr, callTypes), callArgs);
		} // proc DoChunk

		/// <summary>Executes a precompiled chunk on the lua environment.</summary>
		/// <param name="chunk">Compiled chunk.</param>
		/// <param name="callArgs">Arguments for the chunk.</param>
		/// <returns>Return values of the chunk.</returns>
		public LuaResult DoChunk(LuaChunk chunk, params object[] callArgs)
		{
			if (chunk == null)
				throw new ArgumentException(Properties.Resources.rsChunkNotCompiled, "chunk");
			if (lua != chunk.Lua)
				throw new ArgumentException(Properties.Resources.rsChunkWrongScriptManager, "chunk");

			return chunk.Run(this, callArgs);
		} // func DoChunk

		#endregion

		#region -- Basic Functions --------------------------------------------------------

		#region -- class ArrayIndexEnumerator ---------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
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
		private static object LuaAssert(object value, string message)
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

		/// <summary></summary>
		/// <param name="message"></param>
		/// <param name="level"></param>
		[LuaMember("error")]
		private static void LuaError(string message, int level)
		{
			if (level == 0)
				level = 1;

			// level ist der StackTrace
			throw new LuaRuntimeException(message, level, true);
		} // proc LuaError

		/// <summary></summary>
		/// <param name="obj"></param>
		/// <returns></returns>
		[LuaMember("getmetatable")]
		private static LuaTable LuaGetMetaTable(object obj)
		{
			var t = obj as LuaTable;
			return t == null ? null : t.MetaTable;
		} // func LuaGetMetaTable

		[LuaMember("rawmembers")]
		private IEnumerable<KeyValuePair<string, object>> LuaRawMembers(LuaTable t)
		{
			if (t == null)
				throw new ArgumentNullException("#1");

			return t.Members;
		} // func LuaRawMembers

		[LuaMember("rawarray")]
		private IList<object> LuaRawArray(LuaTable t)
		{
			if (t == null)
				throw new ArgumentNullException("#1");

			return t.ArrayList;
		} // func LuaRawArray

		private static LuaResult pairsEnum<TKey>(object s, object current)
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
		private LuaResult LuaIPairs(LuaTable t)
		{
			if (t == null)
				throw new ArgumentNullException("#1");

			var e = new ArrayIndexEnumerator(t.ArrayList).GetEnumerator();
			return new LuaResult(new Func<object, object, LuaResult>(pairsEnum<int>), e, e);
		} // func ipairs

		/// <summary></summary>
		/// <param name="t"></param>
		/// <returns></returns>
		[LuaMember("mpairs")]
		private LuaResult LuaMPairs(LuaTable t)
		{
			if (t == null)
				throw new ArgumentNullException("#1");

			var e = t.Members.GetEnumerator();
			return new LuaResult(new Func<object, object, LuaResult>(pairsEnum<string>), e, e);
		} // func LuaPairs

		/// <summary></summary>
		/// <param name="t"></param>
		/// <returns></returns>
		[LuaMember("pairs")]
		private LuaResult LuaPairs(LuaTable t)
		{
			if (t == null)
				throw new ArgumentNullException("#1");

			var e = ((System.Collections.IEnumerable)t).GetEnumerator();
			return new LuaResult(new Func<object, object, LuaResult>(pairsEnum<object>), e, e);
		} // func LuaPairs

		/// <summary></summary>
		/// <param name="t"></param>
		/// <param name="next"></param>
		/// <returns></returns>
		[LuaMember("next")]
		private static object LuaNext(LuaTable t, object next)
			=> t == null ? null : t.NextKey(next);

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
		private static bool LuaRawEqual(object a, object b)
		{
			if (a == null && b == null)
				return true;
			else if (a != null && b != null)
			{
				if (a.GetType() == b.GetType())
				{
					if (a.GetType().GetTypeInfo().IsValueType)
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
			=> t.GetValue(index, true);

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
			catch (TargetInvocationException e)
			{
				return new LuaResult(false, e.InnerException.Message, e.InnerException);
			}
			catch (Exception e)
			{
				return new LuaResult(false, e.Message, e);
			}
		} // func LuaXPCall

		/// <summary></summary>
		[LuaMember("_VERSION")]
		public virtual string Version { get { return VersionString; } }

		#endregion

		#region -- Basic Libraries --------------------------------------------------------

		[LuaMember("coroutine")]
		private static LuaType LuaLibraryCoroutine
		{
			get { return LuaType.GetType(typeof(LuaThread)); }
		} // prop LuaLibraryTable

		[LuaMember("bit32")]
		private static LuaType LuaLibraryBit32
		{
			get { return LuaType.GetType(typeof(LuaLibraryBit32)); }
		} // prop LuaLibraryTable

		[LuaMember("debug")]
		private static LuaType LuaLibraryDebug
		{
			get { return LuaType.GetType(typeof(LuaLibraryDebug)); }
		} // prop LuaLibraryDebug

		[LuaMember("math")]
		private static LuaType LuaLibraryMath
		{
			get { return LuaType.GetType(typeof(LuaLibraryMath)); }
		} // prop LuaLibraryTable

		[LuaMember("string")]
		private static LuaType LuaLibraryString
		{
			get { return LuaType.GetType(typeof(LuaLibraryString)); }
		} // prop LuaLibraryTable

		[LuaMember("table")]
		private static LuaType LuaLibraryTable
		{
			get { return LuaType.GetType(typeof(LuaTable)); }
		} // prop LuaLibraryTable

		#endregion

		/// <summary>Access to the assigned Lua script manager</summary>
		public Lua Lua { get { return lua; } }
	} // class LuaGlobalPortable

	#endregion
}
