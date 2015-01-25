using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text;

namespace Neo.IronLua
{
	#region -- enum LuaDebugLevel -------------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Descripes the debug-level.</summary>
	[Flags]
	public enum LuaDebugLevel
	{
		/// <summary>No debug info will be emitted.</summary>
		None,
		/// <summary>Before every new line is a DebugInfo emitted (Line exact).</summary>
		Line = 1,
		/// <summary>Every expression is wrap by a DebugInfo (Column exact).</summary>
		Expression = 2,
		/// <summary>Registriert die Methoden</summary>
		RegisterMethods = 4
	} // enum LuaDebugLevel

	#endregion

	#region -- interface ILuaDebug ------------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public interface ILuaDebug
	{
		/// <summary>Create the chunk</summary>
		/// <param name="expr">Content of the chunk.</param>
		/// <param name="lua"></param>
		/// <returns></returns>
		LuaChunk CreateChunk(Lua lua, LambdaExpression expr);
		/// <summary>How should the parser emit the DebugInfo's</summary>
		LuaDebugLevel Level { get; }
	} // interface ILuaDebug

	#endregion

	#region -- interface ILuaDebugInfo --------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Information about a position.</summary>
	public interface ILuaDebugInfo
	{
		/// <summary>Name of the chunk.</summary>
		string ChunkName { get; }
		/// <summary>Source of the position.</summary>
		string FileName { get; }
		/// <summary>Line</summary>
		int Line { get; }
		/// <summary>Column</summary>
		int Column { get; }
	} // interface ILuaDebugInfo

	#endregion

	#region -- class LuaStackTraceDebugger ----------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Debugger that creates the methods as dynamic assembly, with them
	/// it is possible to retrieve exact stacktraces. This debugger is good if
	/// you are running long scripts, they stay in memory.</summary>
	public sealed class LuaStackTraceDebugger : ILuaDebug
	{
		#region -- class LuaDebugInfo -----------------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private sealed class LuaDebugInfo : IComparable<LuaDebugInfo>, ILuaDebugInfo
		{
			private string sChunkName;
			private string sMethodName;
			private string sFileName;
			private int ilOffset;
			private int iLine;
			private int iColumn;

			public LuaDebugInfo(string sChunkName, string sMethodName, SymbolDocumentInfo document, int ilOffset, int iLine, int iColumn)
			{
				this.sChunkName = sChunkName;
				this.sMethodName = sMethodName;
				this.sFileName = document.FileName;
				this.ilOffset = ilOffset;
				this.iLine = iLine;
				this.iColumn = iColumn;
			} // ctor

			public int CompareTo(LuaDebugInfo other)
			{
				int iTmp = String.Compare(sMethodName, other.MethodName);
				return iTmp == 0 ? ilOffset - other.ILOffset : iTmp;
			} // func CompareTo

			public override string ToString()
			{
				return IsClear ? String.Format("{0}:{1}# Clear", sMethodName, ilOffset) : String.Format("{0}:{1}# {2}:{3},{4}", sMethodName, ilOffset, FileName, iLine, iColumn);
			} // func ToString

			public string ChunkName { get { return sChunkName; } }
			public string MethodName { get { return sMethodName; } }
			public string FileName { get { return sFileName; } }
			public int ILOffset { get { return ilOffset; } }
			public int Line { get { return iLine; } }
			public int Column { get { return iColumn; } }

			public bool IsClear { get { return iLine == 16707566; } }
		} // class LuaDebugInfo

		#endregion

		#region -- class LuaDebugInfoGenerator --------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private class LuaDebugInfoGenerator : DebugInfoGenerator
		{
			private LuaStackTraceChunk chunk;

			public LuaDebugInfoGenerator(LuaStackTraceChunk chunk)
			{
				this.chunk = chunk;
			} // ctor

			public override void MarkSequencePoint(LambdaExpression method, int ilOffset, DebugInfoExpression sequencePoint)
			{
				chunk.AddDebugInfo(method, ilOffset, sequencePoint);
			} // proc MarkSequencePoint
		} // class LuaDebugInfoGenerator

		#endregion

		#region -- class ReduceDynamic ----------------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private class ReduceDynamic : ExpressionVisitor, IEqualityComparer<object>
		{
			private struct FieldDefine
			{
				public FieldBuilder Field;
				public object InitValue;
			} // struct FieldDefine

			private Lua lua;
			private LuaStackTraceDebugger debug;
			private TypeBuilder type;

			private Dictionary<object, FieldDefine> fields;

			#region -- Ctor/Dtor ------------------------------------------------------------

			public ReduceDynamic(Lua lua, LuaStackTraceDebugger debug, TypeBuilder type)
			{
				this.lua = lua;
				this.debug = debug;
				this.type = type;

				this.fields = new Dictionary<object, FieldDefine>(this);
			} // ctor

			#endregion

			#region -- IEqualityComparer<object> --------------------------------------------

			bool IEqualityComparer<object>.Equals(object x, object y)
			{
				if (Object.ReferenceEquals(x, y))
					return true;
				else
					return Object.Equals(x, y);
			} // func Equals

			int IEqualityComparer<object>.GetHashCode(object obj)
			{
				return obj.GetHashCode();
			} // func GetHashCode

			#endregion

			#region -- Constant Creator -----------------------------------------------------

			private Expression CreateField(object value, Type fieldType, Func<object> initValue)
			{
				FieldDefine fd;
				if (fields.TryGetValue(value, out fd))
					return Lua.EnsureType(Expression.Field(null, fd.Field), fieldType);
				else
				{
					fields[value] = fd = new FieldDefine
					{
						Field = type.DefineField("$constant" + fields.Count.ToString(), fieldType, FieldAttributes.Private | FieldAttributes.Static),
						InitValue = initValue == null ? value : initValue()
					};
					return Expression.Field(null, fd.Field);
				}
			} // func CreateField

			#endregion

			#region -- VisitConstant --------------------------------------------------------

			protected override Expression VisitConstant(ConstantExpression node)
			{
				if (node.Value != null)
				{
					Type type = node.Value.GetType();
					if (Type.GetTypeCode(type) < TypeCode.Boolean && !typeof(Type).IsAssignableFrom(type))
						return Visit(CreateField(node.Value, type, null));
					else if (node.Type == typeof(object))
						return Visit(Expression.Convert(Expression.Constant(node.Value), typeof(object)));
				}
				return base.VisitConstant(node);
			} // func VisitConstant

			#endregion

			#region -- VisitDynamic ---------------------------------------------------------

			protected override Expression VisitDynamic(DynamicExpression node)
			{
				Type callSiteType = typeof(CallSite<>).MakeGenericType(node.DelegateType);
				Expression[] callSiteArguments = new Expression[node.Arguments.Count + 1];

				// create the callsite
				Expression getConstant = CreateField(node.Binder, callSiteType, () => CallSite.Create(node.DelegateType, node.Binder));
			
				//
				// site.Target.Invoke(s, targetObject, set)
				//
				// create the callsite replacement
				FieldInfo fiTarget = callSiteType.GetField("Target");
				MethodInfo miTargetInvoke = fiTarget.FieldType.GetMethod("Invoke");

				callSiteArguments[0] = getConstant;
				node.Arguments.CopyTo(callSiteArguments, 1);

				return Visit(Expression.Call(Expression.Field(getConstant, fiTarget), miTargetInvoke, callSiteArguments));
			} // func VisitDynamic

			#endregion

			public void InitMethods(Type typeFinished)
			{
				foreach (var c in fields)
				{
					FieldDefine fd = c.Value;

					//Debug.Print("Init: {0} : {1} = {2}", fd.Field.Name, fd.Field.FieldType.Name, fd.InitValue);
					typeFinished.GetField(fd.Field.Name, BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, fd.InitValue);
				}
			} // proc CompileInitMethod
		} // class ReduceDynamic

		#endregion

		#region -- class LuaStackTraceChunk -----------------------------------------------

		private class LuaStackTraceChunk : LuaChunk
		{
			private List<LuaDebugInfo> debugInfos = null;

			public LuaStackTraceChunk(Lua lua, string sName)
				: base(lua, sName, null)
			{
			} // ctor

			public LuaChunk InitChunk(Delegate chunk)
			{
				this.Chunk = chunk;

				// register the debug infos
				string sCurrentMethodName = null;
				debugInfos.ForEach(
					info =>
					{
						if (sCurrentMethodName != info.MethodName)
							RegisterMethod(sCurrentMethodName = info.MethodName);
					});
				
				return this;
			} // proc SetChunk

			public void AddDebugInfo(LambdaExpression method, int ilOffset, DebugInfoExpression sequencePoint)
			{
				if (debugInfos == null)
					debugInfos = new List<LuaDebugInfo>();

				LuaDebugInfo info = new LuaDebugInfo(ChunkName, method.Name, sequencePoint.Document, ilOffset, sequencePoint.StartLine, sequencePoint.StartColumn);
				int iPos = debugInfos.BinarySearch(info);
				if (iPos < 0)
					debugInfos.Insert(~iPos, info);
				else
					debugInfos[iPos] = info;
			} // proc AddDebugInfo

			private bool GetMethodRange(string sMethodName, out int iStart, out int iEnd)
			{
				iStart = -1;
				iEnd = -1;
				if (debugInfos == null)
					return false;

				int iLength = debugInfos.Count;

				// search the start
				for (int i = 0; i < iLength; i++)
				{
					if (debugInfos[i].MethodName == sMethodName)
					{
						iStart = i;
						break;
					}
				}
				if (iStart == -1)
					return false;

				// search the end
				for (int i = iStart; i < iLength; i++)
				{
					if (debugInfos[i].MethodName != sMethodName)
					{
						iEnd = i - 1;
						return true;
					}
				}
				
				iEnd = debugInfos.Count - 1;
				return true;
			} // func GetMethodRange

			protected internal override ILuaDebugInfo GetDebugInfo(MethodBase method, int ilOffset)
			{
				LuaDebugInfo info = null;

				// find method range
				int iStart;
				int iEnd;
				if (!GetMethodRange(method.Name, out iStart, out iEnd))
					return null;

				// find debug info
				if (debugInfos != null)
					for (int i = iStart; i <= iEnd; i++)
						if (debugInfos[i].ILOffset <= ilOffset)
							info = debugInfos[i];
						else if (debugInfos[i].ILOffset > ilOffset)
							break;

				// clear debug
				if (info != null && info.IsClear)
					info = null;

				return info;
			} // func GetDebugInfo

			public override bool HasDebugInfo { get { return debugInfos != null; } }
		} // class LuabStackTraceChunk

		#endregion

		private AssemblyBuilder assembly = null;
		private ModuleBuilder module = null;

		#region -- Ctor/Dtor --------------------------------------------------------------

		/// <summary>Creates a new StackTrace-Debugger</summary>
		public LuaStackTraceDebugger()
		{
		} // ctor

		#endregion

		#region -- CreateChunk ------------------------------------------------------------

		private string CreateUniqueTypeName(string sName)
		{
			int iIndex = 0;
			string sTypeName = sName;
			
			Type[] types = module.GetTypes();
			while (Array.Exists(types, c => c.Name == sTypeName))
				sTypeName = sName + (++iIndex).ToString();

			return sTypeName;
		} // func CreateUniqueTypeName

		LuaChunk ILuaDebug.CreateChunk(Lua lua, LambdaExpression expr)
		{
			lock (this)
			{
				// create the dynamic assembly
				if (assembly == null)
				{
					assembly = AppDomain.CurrentDomain.DefineDynamicAssembly(GetLuaDynamicName(), AssemblyBuilderAccess.RunAndCollect);
					module = assembly.DefineDynamicModule("lua", true);
				}

				// create a type for the expression
				TypeBuilder type = module.DefineType(CreateUniqueTypeName(expr.Name), TypeAttributes.NotPublic | TypeAttributes.Sealed);

				// transform the expression
				var reduce = new ReduceDynamic(lua, this, type);
				expr = (LambdaExpression)reduce.Visit(expr);

				// compile the function
				MethodBuilder method = type.DefineMethod(expr.Name, MethodAttributes.Static | MethodAttributes.Public);
				var chunk = new LuaStackTraceChunk(lua, expr.Name);
				var collectedDebugInfo = new LuaDebugInfoGenerator(chunk);
				expr.CompileToMethod(method, collectedDebugInfo);

				// create the type and build the delegate
				Type typeFinished = type.CreateType();
				
				// Initialize fields, create the static callsite's
				reduce.InitMethods(typeFinished);
				
				// return chunk
				return chunk.InitChunk(Delegate.CreateDelegate(expr.Type, typeFinished.GetMethod(expr.Name)));
			}
		} // func ILuaDebug.CreateChunk

		#endregion

    LuaDebugLevel ILuaDebug.Level { get { return LuaDebugLevel.Expression | LuaDebugLevel.RegisterMethods; } }

		// -- Static --------------------------------------------------------------

		private static object luaDynamicNameLock = new object();
		private static AssemblyName luaDynamicName = null;

		private static AssemblyName GetLuaDynamicName()
		{
			lock (luaDynamicNameLock)
			{
				if (luaDynamicName == null)
				{
					byte[] bKey;
					using (Stream src = typeof(Lua).Assembly.GetManifestResourceStream("Neo.IronLua.NeoLua.snk"))
					{
						bKey = new byte[src.Length];
						src.Read(bKey, 0, bKey.Length);
					}

					// create the strong name
					luaDynamicName = new AssemblyName();
					luaDynamicName.Name = "lua.dynamic";
					luaDynamicName.Version = new Version();
					luaDynamicName.Flags = AssemblyNameFlags.PublicKey;
					luaDynamicName.HashAlgorithm = System.Configuration.Assemblies.AssemblyHashAlgorithm.SHA1;
					luaDynamicName.KeyPair = new StrongNameKeyPair(bKey);
				}

				return luaDynamicName;
			}
		} // func GetLuaDynamicName
	} // class StackTraceDebugger

	#endregion
}
