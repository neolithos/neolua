using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text;

namespace Neo.IronLua
{
	#region -- enum LuaIntegerType ------------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public enum LuaIntegerType : byte
	{
		/// <summary></summary>
		Int16 = 0x01,
		/// <summary></summary>
		Int32 = 0x02,
		/// <summary></summary>
		Int64 = 0x03,
		/// <summary></summary>
		Mask = 0x07
	} // enum LuaIntegerType

	#endregion

	#region -- enum LuaFloatType --------------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public enum LuaFloatType : byte
	{
		/// <summary></summary>
		Float = 0x10,
		/// <summary></summary>
		Double = 0x20,
		/// <summary></summary>
		Mask = 0x70
	} // enum LuaFloatType

	#endregion

	#region -- enum LuaNumberFlags ------------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public enum LuaNumberFlags : byte
	{
		/// <summary></summary>
		HexNumber = 0x08,
		/// <summary></summary>
		NoFormatError = 0x80
	} // enum LuaNumberFlags

	#endregion

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Manages the Lua-Script-Environment. At the time it holds the
	/// binder cache between the compiled scripts.</summary>
	public partial class Lua : IDisposable
	{
		private bool lPrintExpressionTree = false;

		private object packageLock = new object();
		private Dictionary<string, WeakReference> loadedModuls = null;
		private string[] standardPackagePaths = null;

		private int iNumberType = (int)LuaIntegerType.Int32 | (int)LuaFloatType.Double;

		#region -- Ctor/Dtor --------------------------------------------------------------

		/// <summary>Create a new lua-script-manager.</summary>
		public Lua()
		{
		} // ctor

		/// <summary>Create a new lua-script-manager.</summary>
		/// <param name="integerType"></param>
		/// <param name="floatType"></param>
		public Lua(LuaIntegerType integerType, LuaFloatType floatType)
		{
			this.IntegerType = integerType;
			this.FloatType = floatType;
		} // ctor

		/// <summary>Clear the cache.</summary>
		~Lua()
		{
			Dispose(false);
		} // dtor

		/// <summary>Destroy script manager</summary>
		public void Dispose()
		{
			Dispose(true);
		} // proc Dispose

		/// <summary></summary>
		/// <param name="disposing"></param>
		protected virtual void Dispose(bool disposing)
		{
			Clear();
		} // proc Dispose

		/// <summary>Removes all chunks, binders and compiled assemblies.</summary>
		public virtual void Clear()
		{
			ClearBinderCache();
		} // proc Clear

		#endregion

		#region -- Compile ----------------------------------------------------------------

		/// <summary>Erzeugt ein Delegate aus dem Code, ohne ihn auszuführen.</summary>
		/// <param name="sFileName">Dateiname die gelesen werden soll.</param>
		/// <param name="debug">Compile with debug infos</param>
		/// <param name="args">Parameter für den Codeblock</param>
		/// <returns>Compiled chunk.</returns>
		public LuaChunk CompileChunk(string sFileName, ILuaDebug debug, params KeyValuePair<string, Type>[] args)
		{
			return CompileChunk(sFileName, debug, new StreamReader(sFileName), args);
		} // func CompileChunk

		/// <summary>Erzeugt ein Delegate aus dem Code, ohne ihn auszuführen.</summary>
		/// <param name="tr">Inhalt</param>
		/// <param name="sName">Name der Datei</param>
		/// <param name="debug">Compile with debug infos</param>
		/// <param name="args">Parameter für den Codeblock</param>
		/// <returns>Compiled chunk.</returns>
		public LuaChunk CompileChunk(TextReader tr, string sName, ILuaDebug debug, params KeyValuePair<string, Type>[] args)
		{
			return CompileChunk(sName, debug, tr, args);
		} // func CompileChunk

		/// <summary>Erzeugt ein Delegate aus dem Code, ohne ihn auszuführen.</summary>
		/// <param name="sCode">Code, der das Delegate darstellt.</param>
		/// <param name="sName">Name des Delegates</param>
		/// <param name="debug">Compile with debug infos</param>
		/// <param name="args">Argumente</param>
		/// <returns>Compiled chunk.</returns>
		public LuaChunk CompileChunk(string sCode, string sName, ILuaDebug debug, params KeyValuePair<string, Type>[] args)
		{
			return CompileChunk(sName, debug, new StringReader(sCode), args);
		} // func CompileChunk

		internal LuaChunk CompileChunk(string sChunkName, ILuaDebug debug, TextReader tr, IEnumerable<KeyValuePair<string, Type>> args)
		{
			if (String.IsNullOrEmpty(sChunkName))
				throw new ArgumentNullException("chunkname");

			using (LuaLexer l = new LuaLexer(sChunkName, tr))
			{
				if (debug != null && (debug.Level & LuaDebugLevel.RegsiterMethods) == LuaDebugLevel.RegsiterMethods)
					BeginCompile();
				try
				{
					LambdaExpression expr = Parser.ParseChunk(this, debug == null ? LuaDebugLevel.None : debug.Level, true, l, null, typeof(LuaResult), args);

					if (lPrintExpressionTree)
					{
						Console.WriteLine(Parser.ExpressionToString(expr));
						Console.WriteLine(new string('=', 79));
					}

					// compile the chunk
					if (debug == null)
						return new LuaChunk(this, expr.Name, expr.Compile());
					else
						return debug.CreateChunk(this, expr);
				}
				finally
				{
					if (debug != null && (debug.Level & LuaDebugLevel.RegsiterMethods) == LuaDebugLevel.RegsiterMethods)
						EndCompile();
				}
			}
		} // func CompileChunk

		/// <summary>Creates a simple lua-lambda-expression without any environment.</summary>
		/// <param name="sName">Name of the delegate</param>
		/// <param name="sCode">Code of the delegate.</param>
		/// <param name="typeDelegate">Delegate type. <c>null</c> is allowed.</param>
		/// <param name="returnType">Return-Type of the delegate</param>
		/// <param name="arguments">Arguments of the delegate.</param>
		/// <returns></returns>
		public Delegate CreateLambda(string sName, string sCode, Type typeDelegate, Type returnType, params KeyValuePair<string, Type>[] arguments)
		{
			using (LuaLexer l = new LuaLexer(sName, new StringReader(sCode)))
			{
				LambdaExpression expr = Parser.ParseChunk(this, LuaDebugLevel.None, false, l, typeDelegate, returnType, arguments);

				if (lPrintExpressionTree)
				{
					Console.WriteLine(Parser.ExpressionToString(expr));
					Console.WriteLine(new string('=', 79));
				}

				return expr.Compile();
			}
		} // func CreateLambda

		/// <summary>Creates a simple lua-delegate without any environment.</summary>
		/// <param name="sName">Name of the delegate</param>
		/// <param name="sCode">Code of the delegate.</param>
		/// <param name="argumentNames">Possible to override the argument names.</param>
		/// <returns></returns>
		public T CreateLambda<T>(string sName, string sCode, params string[] argumentNames)
			where T : class
		{
			Type typeDelegate = typeof(T);
			MethodInfo mi = typeDelegate.GetMethod("Invoke");
			ParameterInfo[] parameters = mi.GetParameters();
			KeyValuePair<string, Type>[] arguments = new KeyValuePair<string, Type>[parameters.Length];

			// create the argument list
			for (int i = 0; i < parameters.Length; i++)
			{
				ParameterInfo p = parameters[i];

				if (p.ParameterType.IsByRef)
					throw new ArgumentException(Properties.Resources.rsDelegateCouldNotHaveOut);

				arguments[i] = new KeyValuePair<string, Type>(
					argumentNames != null && i < argumentNames.Length ? argumentNames[i] : p.Name,
					p.ParameterType);
			}

			return (T)(object)CreateLambda(sName, sCode, typeDelegate, mi.ReturnParameter.ParameterType, arguments);
		} // func CreateLambda

		#endregion

		#region -- Require ------------------------------------------------------------------

		internal LuaChunk LuaRequire(LuaGlobal global, object modname)
		{
			if (modname is string)
			{
				string sModName = (string)modname;
				string sFileName;
				DateTime dtStamp;
				if (global.LuaRequireFindFile(sModName, out sFileName, out dtStamp))
				{
					lock (packageLock)
					{
						WeakReference rc;
						LuaChunk c;
						string sCacheId = sFileName + ";" + dtStamp.ToString("o");

						// is the modul loaded
						if (loadedModuls == null ||
							!loadedModuls.TryGetValue(sCacheId, out rc) ||
							!rc.IsAlive)
						{
							// compile the modul
							c = CompileChunk(sFileName, null);

							// Update Cache
							if (loadedModuls == null)
								loadedModuls = new Dictionary<string, WeakReference>();
							loadedModuls[sCacheId] = new WeakReference(c);
						}
						else
							c = (LuaChunk)rc.Target;

						return c;
					}
				}
			}
			return null;
		} // func LuaRequire

		#endregion

		/// <summary>Creates an empty environment for the lua functions.</summary>
		/// <returns>Initialized environment</returns>
		public virtual LuaGlobal CreateEnvironment()
		{
			return new LuaGlobal(this);
		} // func CreateEnvironment

		#region -- Numbers ----------------------------------------------------------------

		internal static Type GetIntegerType(int iNumberType)
		{
			switch ((LuaIntegerType)(iNumberType & (int)LuaIntegerType.Mask))
			{
				case LuaIntegerType.Int16:
					return typeof(short);
				case LuaIntegerType.Int32:
					return typeof(int);
				case LuaIntegerType.Int64:
					return typeof(long);
				default:
					throw new ArgumentException();
			}
		} // func GetIntegerType

		internal static Type GetFloatType(int iNumberType)
		{
			switch ((LuaFloatType)(iNumberType & (int)LuaFloatType.Mask))
			{
				case LuaFloatType.Float:
					return typeof(float);
				case LuaFloatType.Double:
					return typeof(double);
				default:
					throw new ArgumentException();
			}
		} // func GetFloatType

		private static object ParseInteger(string sNumber, int integerType)
		{
			NumberStyles style = ((byte)integerType & 8) != 0 ? NumberStyles.HexNumber : NumberStyles.Integer;
			switch ((LuaIntegerType)(integerType & (byte)LuaIntegerType.Mask))
			{
				case LuaIntegerType.Int16:
					{
						short t;
						return Int16.TryParse(sNumber, style, CultureInfo.InvariantCulture, out t) ? t : ThrowFormatExpression(integerType, sNumber, "short");
					}
				case LuaIntegerType.Int32:
					{
						int t;
						return Int32.TryParse(sNumber, style, CultureInfo.InvariantCulture, out t) ? t : ThrowFormatExpression(integerType, sNumber, "int");
					}
				case LuaIntegerType.Int64:
					{
						long t;
						return Int64.TryParse(sNumber, style, CultureInfo.InvariantCulture, out t) ? t : ThrowFormatExpression(integerType, sNumber, "long");
					}
				default:
					throw new InvalidOperationException();
			}
		} // func ParseInteger

		private static object ParseFloat(string sNumber, int floatType)
		{
			switch ((LuaFloatType)(floatType & (byte)LuaFloatType.Mask))
			{
				case LuaFloatType.Float:
					{
						float t;
						return Single.TryParse(sNumber, NumberStyles.Float, CultureInfo.InvariantCulture, out t) ? t : ThrowFormatExpression(floatType, sNumber, "float");
					}
				case LuaFloatType.Double:
					{
						double t;
						return Double.TryParse(sNumber, NumberStyles.Float, CultureInfo.InvariantCulture, out t) ? t : ThrowFormatExpression(floatType, sNumber, "double");
					}
				default:
					throw new InvalidOperationException();
			}
		} // func ParseFloat

		private static object ThrowFormatExpression(int numberType, string sNumber, string sType)
		{
			if ((numberType & (int)LuaNumberFlags.NoFormatError) != 0)
				return null;
			else
				throw new FormatException(String.Format(Properties.Resources.rsFormatError, sNumber, sType));
		} // func ThrowFormatExpression

		/// <summary>Parses a string to a lua number.</summary>
		/// <param name="sNumber">String representation of the number.</param>
		/// <param name="lHexNumber">Is the string a hex number</param>
		/// <returns></returns>
		public object ParseNumber(string sNumber, bool lHexNumber = false)
		{
			int numberType = iNumberType;
			if (lHexNumber)
				numberType |= 8;
			return Lua.RtParseNumber(sNumber, numberType | 0x80);
		} // func ParseNumber

		internal int NumberType { get { return iNumberType; } }

		/// <summary>Default type for the non floating point numbers. Only short, int, long is allowed.</summary>
		public LuaIntegerType IntegerType
		{
			get { return (LuaIntegerType)(iNumberType & (int)LuaIntegerType.Mask); }
			private set
			{
				if (value == LuaIntegerType.Int16 ||
					value == LuaIntegerType.Int32 ||
					value == LuaIntegerType.Int64)
					iNumberType = (iNumberType & (int)LuaFloatType.Mask) | (int)value;
				else
					throw new ArgumentException();
			}
		} // prop IntegerType

		/// <summary>Default type for the floating point numbers. Only float, double, decimal is allowed.</summary>
		public LuaFloatType FloatType
		{
			get { return (LuaFloatType)(iNumberType & (int)LuaFloatType.Mask); }
			private set
			{
				if (value == LuaFloatType.Float ||
					value == LuaFloatType.Double)
					iNumberType = (iNumberType & (int)LuaIntegerType.Mask) | (int)value;
				else
					throw new ArgumentException();
			}
		} // prop FloatType

		#endregion

		internal bool PrintExpressionTree { get { return lPrintExpressionTree; } set { lPrintExpressionTree = value; } }

		/// <summary>Default path for the package loader</summary>
		public string[] StandardPackagesPaths
		{
			get
			{
				if (standardPackagePaths == null)
				{
					string sExecutingDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
					standardPackagePaths = new string[]
					{
						"%currentdirectory%",
						sExecutingDirectory,
						Path.Combine(sExecutingDirectory, "lua")
					};
				}
				return standardPackagePaths;
			}
		} // prop StandardPackagesPaths

		/// <summary>Default path for the package loader</summary>
		public string StandardPackagesPath
		{
			get { return String.Join(";", StandardPackagesPaths); }
			set
			{
				if (String.IsNullOrEmpty(value))
					standardPackagePaths = null;
				else
					standardPackagePaths = value.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
			}
		} // prop StandardPackagesPath

		// -- Static --------------------------------------------------------------

		private static object lockDefaultDebugEngine = new object();
		private static ILuaDebug defaultDebugEngine = null;

		private static int iRegisteredChunkLock = 0;
		private static Dictionary<string, WeakReference> registeredChunks = new Dictionary<string, WeakReference>();

		#region -- Chunk Register ---------------------------------------------------------

		private static void BeginCompile()
		{
			lock (registeredChunks)
				iRegisteredChunkLock++;
		} // proc BeginCompile

		private static void EndCompile()
		{
			lock (registeredChunks)
			{
				iRegisteredChunkLock--;
				if (iRegisteredChunkLock <= 0) // clean up
				{
					// collect all chunks they have ne reference or target
					List<string> deletes = new List<string>();
					foreach (var c in registeredChunks)
						if (c.Value == null || c.Value.Target == null)
							deletes.Remove(c.Key);

					// remove them
					for (int i = 0; i < deletes.Count; i++)
						registeredChunks.Remove(deletes[i]);
				}
			}
		} // proc EndCompile

		internal static string RegisterUniqueName(string sName)
		{
			lock (registeredChunks)
			{
				int iIndex = 0;
				string sUniqueName = sName;
				while (registeredChunks.ContainsKey(sUniqueName))
					sUniqueName = sName + (++iIndex).ToString();
				return sUniqueName;
			}
		} // func RegisterUniqueName

		internal static void RegisterMethod(string sUniqueName, LuaChunk chunk)
		{
			lock (registeredChunks)
				registeredChunks[sUniqueName] = new WeakReference(chunk);
		} // proc RegsiterMethod

		internal static void UnregisterMethod(string sUniqueName, LuaChunk chunk)
		{
			WeakReference r;
			lock (registeredChunks)
			{
				if (registeredChunks.TryGetValue(sUniqueName, out r) && r.Target == chunk)
					registeredChunks.Remove(sUniqueName);
			}
		} // proc UnregisterMethod

		/// <summary></summary>
		/// <param name="sName"></param>
		/// <returns></returns>
		public static LuaChunk GetChunkFromMethodName(string sName)
		{
			WeakReference r;
			lock (registeredChunks)
				if (registeredChunks.TryGetValue(sName, out r))
					return (LuaChunk)r.Target;
				else
					return null;
		} // func GetChunkFromMethodName

		/// <summary></summary>
		/// <param name="mi"></param>
		/// <returns></returns>
		public static LuaChunk GetChunkFromMethodInfo(MethodBase mi)
		{
			return GetChunkFromMethodName(mi.Name);
		} // func GetChunkFromMethodInfo

		#endregion

		/// <summary>Returns a default StackTrace-Debug Engine</summary>
		public static ILuaDebug DefaultDebugEngine
		{
			get
			{
				lock (lockDefaultDebugEngine)
					if (defaultDebugEngine == null)
						defaultDebugEngine = new LuaStackTraceDebugger();
				return defaultDebugEngine;
			}
		} // prop DefaultDebugEngine

		/// <summary>Returns the Version of the assembly</summary>
		public static Version Version
		{
			get
			{
				AssemblyFileVersionAttribute attr = (AssemblyFileVersionAttribute)Attribute.GetCustomAttribute(typeof(Lua).Assembly, typeof(AssemblyFileVersionAttribute));
				return attr == null ? new Version() : new Version(attr.Version);
			}
		} // prop Version
	} // class Lua
}
