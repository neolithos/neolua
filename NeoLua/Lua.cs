using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

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

	#region -- enum LuaSandboxResult ----------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Defines the sandbox type</summary>
	public enum LuaSandboxResult
	{
		/// <summary>No sandbox</summary>
		None,
		/// <summary>Access is not allowed.</summary>
		Restrict,
		/// <summary>Check the access during runtime.</summary>
		Dynamic
	} // enum LuaSandboxResult

	#endregion

	#region -- class LuaCompileOptions --------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Defines option for the parse and compile of a script.</summary>
	public class LuaCompileOptions
	{
		/// <summary>Action on access diened.</summary>
		/// <returns></returns>
		protected virtual Expression RestrictAccess()
		{
			return Expression.Throw(Expression.New(typeof(UnauthorizedAccessException)));
		} // func RestrictAccess

		/// <summary>Most core method, that gets called to sandbox a value.</summary>
		/// <param name="expression">Expression, that should be sandboxed.</param>
		/// <param name="instance">Optional: Instance, that was called to get the expression.</param>
		/// <param name="sMember">Optional: Name of the member that was used to resolve the expression.</param>
		/// <returns>Sandboxed expression</returns>
		protected internal virtual Expression SandboxCore(Expression expression, Expression instance, string sMember)
		{
			switch (Sandbox(expression.Type, instance == null ? null : instance.Type, sMember))
			{
				case LuaSandboxResult.Dynamic:
					if (DynamicSandbox == null)
						return expression;
					else 
						return Lua.EnsureType(Expression.Invoke(Expression.Constant(DynamicSandbox), expression), expression.Type);

				case LuaSandboxResult.Restrict:
					return RestrictAccess();
				
				default:
					return expression;
			}
		} // func SandboxCore

		/// <summary>Higher level method to restict access to types.</summary>
		/// <param name="expressionType">Type of the sandbox value</param>
		/// <param name="instanceType">Optional: Instance, that was called to get the expression.</param>
		/// <param name="sMember">Optional: Name of the member that was used to resolve the expression.</param>
		/// <returns>Sandbox action</returns>
		protected virtual LuaSandboxResult Sandbox(Type expressionType, Type instanceType, string sMember)
		{
			return DynamicSandbox == null ? LuaSandboxResult.None : LuaSandboxResult.Dynamic;
		} // func Sandbox

		/// <summary>Gets called if the sandbox will resolved during runtime.</summary>
		public Func<object, object> DynamicSandbox { get; set; }
		/// <summary>Set this member to compile the script with Debug-Infos.</summary>
		public ILuaDebug DebugEngine { get; set; }
	} // class LuaCompileOptions

	#endregion

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Manages the Lua-Script-Environment. At the time it holds the
	/// binder cache between the compiled scripts.</summary>
	public partial class Lua : IDisposable
	{
		private TextWriter printExpressionTree = null;

		private int numberType = (int)LuaIntegerType.Int32 | (int)LuaFloatType.Double;

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

		#region -- CreateEnvironment ------------------------------------------------------

		/// <summary>Creates a Environment</summary>
		/// <returns></returns>
		public LuaGlobalPortable CreateEnvironment()
			=> new LuaGlobalPortable(this);

		/// <summary>Create an empty environment</summary>
		/// <typeparam name="T"></typeparam>
		/// <returns></returns>
		public T CreateEnvironment<T>()
			where T : LuaTable
			=> (T)Activator.CreateInstance(typeof(T), this);

		#endregion

		#region -- Compile ----------------------------------------------------------------

		/// <summary>Erzeugt ein Delegate aus dem Code, ohne ihn auszuführen.</summary>
		/// <param name="tr">Inhalt</param>
		/// <param name="name">Name der Datei</param>
		/// <param name="options">Options for the compile process.</param>
		/// <param name="args">Parameter für den Codeblock</param>
		/// <returns>Compiled chunk.</returns>
		public LuaChunk CompileChunk(TextReader tr, string name, LuaCompileOptions options, params KeyValuePair<string, Type>[] args)
		{
			return CompileChunk(name, options, tr, args);
		} // func CompileChunk

		/// <summary>Erzeugt ein Delegate aus dem Code, ohne ihn auszuführen.</summary>
		/// <param name="code">Code, der das Delegate darstellt.</param>
		/// <param name="name">Name des Delegates</param>
		/// <param name="options">Options for the compile process.</param>
		/// <param name="args">Argumente</param>
		/// <returns>Compiled chunk.</returns>
		public LuaChunk CompileChunk(string code, string name, LuaCompileOptions options, params KeyValuePair<string, Type>[] args)
		{
			return CompileChunk(name, options, new StringReader(code), args);
		} // func CompileChunk

		internal LuaChunk CompileChunk(string chunkName, LuaCompileOptions options, TextReader tr, IEnumerable<KeyValuePair<string, Type>> args)
		{
			if (String.IsNullOrEmpty(chunkName))
				throw new ArgumentNullException("chunkname");
			if (options == null)
				options = new LuaCompileOptions();

			using (var l = new LuaLexer(chunkName, tr))
			{
				var registerMethods = options.DebugEngine != null && (options.DebugEngine.Level & LuaDebugLevel.RegisterMethods) == LuaDebugLevel.RegisterMethods;
				if (registerMethods)
					BeginCompile();
				try
				{
					var expr = Parser.ParseChunk(this, options, true, l, null, typeof(LuaResult), args);

					if (printExpressionTree != null)
					{
						printExpressionTree.WriteLine(Parser.ExpressionToString(expr));
						printExpressionTree.WriteLine(new string('=', 79));
					}

					// compile the chunk
					if (options.DebugEngine == null)
						return new LuaChunk(this, expr.Name, expr.Compile());
					else
						return options.DebugEngine.CreateChunk(this, expr);
				}
				finally
				{
					if (registerMethods)
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
			using (var l = new LuaLexer(sName, new StringReader(sCode)))
			{
				var expr = Parser.ParseChunk(this, new LuaCompileOptions(), false, l, typeDelegate, returnType, arguments);

				if (printExpressionTree != null)
				{
					printExpressionTree.WriteLine(Parser.ExpressionToString(expr));
					printExpressionTree.WriteLine(new string('=', 79));
				}

				return expr.Compile();
			}
		} // func CreateLambda

		/// <summary>Creates a simple lua-delegate without any environment.</summary>
		/// <param name="name">Name of the delegate</param>
		/// <param name="code">Code of the delegate.</param>
		/// <param name="argumentNames">Possible to override the argument names.</param>
		/// <returns></returns>
		public T CreateLambda<T>(string name, string code, params string[] argumentNames)
			where T : class
		{
			var typeDelegate = typeof(T);
			var mi = typeDelegate.GetTypeInfo().FindDeclaredMethod("Invoke", ReflectionFlag.Instance | ReflectionFlag.NoArguments);
			var parameters = mi.GetParameters();
			var arguments = new KeyValuePair<string, Type>[parameters.Length];

			// create the argument list
			for (var i = 0; i < parameters.Length; i++)
			{
				var p = parameters[i];

				if (p.ParameterType.IsByRef)
					throw new ArgumentException(Properties.Resources.rsDelegateCouldNotHaveOut);

				arguments[i] = new KeyValuePair<string, Type>(
					argumentNames != null && i < argumentNames.Length ? argumentNames[i] : p.Name,
					p.ParameterType);
			}

			return (T)(object)CreateLambda(name, code, typeDelegate, mi.ReturnParameter.ParameterType, arguments);
		} // func CreateLambda

		#endregion

		#region -- Numbers ----------------------------------------------------------------

		internal static Type GetIntegerType(int numberType)
		{
			switch ((LuaIntegerType)(numberType & (int)LuaIntegerType.Mask))
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

		internal static Type GetFloatType(int numberType)
		{
			switch ((LuaFloatType)(numberType & (int)LuaFloatType.Mask))
			{
				case LuaFloatType.Float:
					return typeof(float);
				case LuaFloatType.Double:
					return typeof(double);
				default:
					throw new ArgumentException();
			}
		} // func GetFloatType

		/// <summary>Parses a string to a lua number.</summary>
		/// <param name="number">String representation of the number.</param>
		/// <returns></returns>
		public object ParseNumber(string number)
		{
			return Lua.RtParseNumber(number, FloatType == LuaFloatType.Double, false);
		} // func ParseNumber

		/// <summary>Parses a string to a lua number.</summary>
		/// <param name="number">String representation of the number.</param>
		/// <param name="iBase">Base fore the number</param>
		/// <returns></returns>
		public object ParseNumber(string number, int iBase)
		{
			return Lua.RtParseNumber(null, number, 0, iBase, FloatType == LuaFloatType.Double, false);
		} // func ParseNumber

		internal int NumberType => numberType;

		/// <summary>Default type for the non floating point numbers. Only short, int, long is allowed.</summary>
		public LuaIntegerType IntegerType
		{
			get { return (LuaIntegerType)(numberType & (int)LuaIntegerType.Mask); }
			private set
			{
				if (value == LuaIntegerType.Int16 ||
					value == LuaIntegerType.Int32 ||
					value == LuaIntegerType.Int64)
					numberType = (numberType & (int)LuaFloatType.Mask) | (int)value;
				else
					throw new ArgumentException();
			}
		} // prop IntegerType

		/// <summary>Default type for the floating point numbers. Only float, double, decimal is allowed.</summary>
		public LuaFloatType FloatType
		{
			get { return (LuaFloatType)(numberType & (int)LuaFloatType.Mask); }
			private set
			{
				if (value == LuaFloatType.Float ||
					value == LuaFloatType.Double)
					numberType = (numberType & (int)LuaIntegerType.Mask) | (int)value;
				else
					throw new ArgumentException();
			}
		} // prop FloatType

		#endregion

		internal TextWriter PrintExpressionTree { get { return printExpressionTree; } set { printExpressionTree = value; } }

    // -- Static --------------------------------------------------------------

		private static Version versionInfo = null;

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
					var deletes = new List<string>();
					foreach (var c in registeredChunks)
						if (c.Value == null || c.Value.Target == null)
							deletes.Remove(c.Key);

					// remove them
					for (var i = 0; i < deletes.Count; i++)
						registeredChunks.Remove(deletes[i]);
				}
			}
		} // proc EndCompile

		internal static string RegisterUniqueName(string sName)
		{
			lock (registeredChunks)
			{
        // create a unique name
				int iIndex = 0;
				string sUniqueName = sName;
				while (registeredChunks.ContainsKey(sUniqueName))
					sUniqueName = sName + (++iIndex).ToString();

        // reserve the name
        registeredChunks.Add(sUniqueName, null);
        
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
				if (registeredChunks.TryGetValue(sName, out r) && r != null)
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

		/// <summary>Returns the version of lua.</summary>
		public static Version Version
		{
			get
			{
				if (versionInfo == null)
				{
					var versionAttribute = typeof(Lua).GetTypeInfo().Assembly.GetCustomAttribute<AssemblyFileVersionAttribute>();
					versionInfo = versionAttribute == null ? new Version(0, 0) : new Version(versionAttribute.Version);
				}
				return versionInfo;
			}
		} // prop Version
	} // class Lua
}
