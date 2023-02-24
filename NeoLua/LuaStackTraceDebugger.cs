#region -- copyright --
//
// Licensed to the Apache Software Foundation (ASF) under one
// or more contributor license agreements.  See the NOTICE file
// distributed with this work for additional information
// regarding copyright ownership.  The ASF licenses this file
// to you under the Apache License, Version 2.0 (the
// "License"); you may not use this file except in compliance
// with the License.  You may obtain a copy of the License at
// 
//   http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing,
// software distributed under the License is distributed on an
// "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY
// KIND, either express or implied.  See the License for the
// specific language governing permissions and limitations
// under the License.
//
#endregion
#if !NETSTANDARD2_0 && !NETCOREAPP2_1 && !NET6_0
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace Neo.IronLua
{
	#region -- class LuaStackTraceDebugger --------------------------------------------

	/// <summary>Debugger that creates the methods as dynamic assembly, with them
	/// it is possible to retrieve exact stacktraces. This debugger is good if
	/// you are running long scripts, they stay in memory.</summary>
	public sealed class LuaStackTraceDebugger : ILuaDebug
	{
		#region -- class LuaDebugInfo -------------------------------------------------

		private sealed class LuaDebugInfo : IComparable<LuaDebugInfo>, ILuaDebugInfo
		{
			private readonly string chunkName;
			private readonly string methodName;
			private readonly string fileName;
			private readonly int ilOffset;
			private readonly int line;
			private readonly int column;

			public LuaDebugInfo(string chunkName, string methodName, SymbolDocumentInfo document, int ilOffset, int line, int column)
			{
				this.chunkName = chunkName;
				this.methodName = methodName;
				this.fileName = document.FileName;
				this.ilOffset = ilOffset;
				this.line = line;
				this.column = column;
			} // ctor

			public int CompareTo(LuaDebugInfo other)
			{
				var tmp = String.Compare(methodName, other.MethodName);
				return tmp == 0 ? ilOffset - other.ILOffset : tmp;
			} // func CompareTo

			public override string ToString()
				=> IsClear ? String.Format("{0}:{1}# Clear", methodName, ilOffset) : String.Format("{0}:{1}# {2}:{3},{4}", methodName, ilOffset, FileName, line, column);

			public string ChunkName => chunkName;
			public string MethodName => methodName;
			public string FileName => fileName;
			public int ILOffset => ilOffset;
			public int Line => line;
			public int Column => column;

			public bool IsClear => line == 16707566;
		} // class LuaDebugInfo

		#endregion

		#region -- class LuaDebugInfoGenerator ----------------------------------------

		private class LuaDebugInfoGenerator : DebugInfoGenerator
		{
			private readonly LuaStackTraceChunk chunk;

			public LuaDebugInfoGenerator(LuaStackTraceChunk chunk)
			{
				this.chunk = chunk ?? throw new ArgumentNullException(nameof(chunk));
			} // ctor

			public override void MarkSequencePoint(LambdaExpression method, int ilOffset, DebugInfoExpression sequencePoint)
				=> chunk.AddDebugInfo(method, ilOffset, sequencePoint);
		} // class LuaDebugInfoGenerator

		#endregion

		#region -- class ReduceDynamic ------------------------------------------------

		private class ReduceDynamic : ExpressionVisitor, IEqualityComparer<object>
		{
			private struct FieldDefine
			{
				public FieldBuilder Field;
				public object InitValue;
			} // struct FieldDefine

			private struct CallSiteToken : IEquatable<CallSiteToken>
			{
				public CallSiteBinder Binder;
				public Type DelegateType;

				public override string ToString()
					=> nameof(CallSiteToken) + ": " + DelegateType.FullName;

				public override int GetHashCode()
					=> Binder.GetHashCode() ^ DelegateType.GetHashCode();

				public override bool Equals(object obj)
					=> obj is CallSiteToken ? Equals((CallSiteToken)obj) : false;

				public bool Equals(CallSiteToken other)
					=> Binder == other.Binder && DelegateType == other.DelegateType;
			} // struct CallSiteToken

			private readonly Lua lua;
			private readonly LuaStackTraceDebugger debug;
			private readonly TypeBuilder type;

			private bool isFirstLambdaDone = false;
			private Dictionary<object, FieldDefine> fields;

			#region -- Ctor/Dtor ------------------------------------------------------

			public ReduceDynamic(Lua lua, LuaStackTraceDebugger debug, TypeBuilder type)
			{
				this.lua = lua ?? throw new ArgumentNullException(nameof(lua));
				this.debug = debug ?? throw new ArgumentNullException(nameof(debug));
				this.type = type ?? throw new ArgumentNullException(nameof(type));

				this.fields = new Dictionary<object, FieldDefine>(this);
			} // ctor

			#endregion

			#region -- IEqualityComparer<object> --------------------------------------

			bool IEqualityComparer<object>.Equals(object x, object y)
				=> ReferenceEquals(x, y) ? true : Equals(x, y);

			int IEqualityComparer<object>.GetHashCode(object obj)
				=> obj.GetHashCode();

			#endregion

			#region -- Constant Creator -----------------------------------------------

			private Expression CreateField(object value, Type fieldType, Func<object> initValue)
			{
				if (fields.TryGetValue(value, out var fd))
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

			#region -- VisitConstant --------------------------------------------------

			protected override Expression VisitConstant(ConstantExpression node)
			{
				if (node.Value != null)
				{
					var type = node.Value.GetType();
					if (Type.GetTypeCode(type) < TypeCode.Boolean && !typeof(Type).IsAssignableFrom(type))
						return Visit(CreateField(node.Value, type, null));
					else if (node.Type == typeof(object))
						return Visit(Expression.Convert(Expression.Constant(node.Value), typeof(object)));
				}
				return base.VisitConstant(node);
			} // func VisitConstant

			#endregion

			#region -- VisitDynamic ---------------------------------------------------

			protected override Expression VisitDynamic(DynamicExpression node)
			{
				var callSiteType = typeof(CallSite<>).MakeGenericType(node.DelegateType);
				var callSiteArguments = new Expression[node.Arguments.Count + 1];

				// create the callsite
				var getConstant = CreateField(
					new CallSiteToken() { Binder = node.Binder, DelegateType = callSiteType },
					callSiteType,
					() => CallSite.Create(node.DelegateType, node.Binder)
				);

				//
				// site.Target.Invoke(s, targetObject, set)
				//
				// create the callsite replacement
				var fiTarget = callSiteType.GetField("Target");
				var miTargetInvoke = fiTarget.FieldType.GetMethod("Invoke");

				callSiteArguments[0] = getConstant;
				node.Arguments.CopyTo(callSiteArguments, 1);

				return Visit(Expression.Call(Expression.Field(getConstant, fiTarget), miTargetInvoke, callSiteArguments));
			} // func VisitDynamic

			#endregion

			protected override Expression VisitLambda<T>(Expression<T> node)
			{
				if (!isFirstLambdaDone) // first block should be a try catch
				{
					isFirstLambdaDone = true;
					var exceptionE = Expression.Parameter(typeof(Exception), "$e");
					return Visit(
						Expression.Lambda<T>(
							Expression.TryCatch(
								node.Body,
								Expression.Catch(exceptionE,
									Expression.Block(
										Expression.Call(luaExceptionDataGetDataMethodInfo, exceptionE, Expression.Constant(true)),
										Expression.Throw(null, node.Body.Type)
									)
								)
							),
							node.Name,
							node.TailCall,
							node.Parameters
						)
					);
				}
				else
					return base.VisitLambda<T>(node);
			} // func VisitLambda

			public void InitMethods(Type typeFinished)
			{
				foreach (var c in fields)
				{
					var fd = c.Value;

					// Debug.Print("Init: {0} : {1} = {2}", fd.Field.Name, fd.Field.FieldType.Name, fd.InitValue);
					typeFinished.GetField(fd.Field.Name, BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, fd.InitValue);
				}
			} // proc CompileInitMethod
		} // class ReduceDynamic

		#endregion

		#region -- class LuaStackTraceChunk -------------------------------------------

		private class LuaStackTraceChunk : LuaChunk
		{
			private List<LuaDebugInfo> debugInfos = null;

			public LuaStackTraceChunk(Lua lua, string name)
				: base(lua, name, null)
			{
			} // ctor

			public LuaChunk InitChunk(Delegate chunk)
			{
				Chunk = chunk;

				// register the debug infos
				var currentMethodName = (string)null;
				debugInfos.ForEach(
					info =>
					{
						if (currentMethodName != info.MethodName)
							RegisterMethod(currentMethodName = info.MethodName);
					}
				);

				return this;
			} // proc SetChunk

			public void AddDebugInfo(LambdaExpression method, int ilOffset, DebugInfoExpression sequencePoint)
			{
				if (debugInfos == null)
					debugInfos = new List<LuaDebugInfo>();

				var info = new LuaDebugInfo(ChunkName, method.Name, sequencePoint.Document, ilOffset, sequencePoint.StartLine, sequencePoint.StartColumn);
				var pos = debugInfos.BinarySearch(info);
				if (pos < 0)
					debugInfos.Insert(~pos, info);
				else
					debugInfos[pos] = info;
			} // proc AddDebugInfo

			private bool GetMethodRange(string sMethodName, out int startAt, out int endAt)
			{
				startAt = -1;
				endAt = -1;
				if (debugInfos == null)
					return false;

				var length = debugInfos.Count;

				// search the start
				for (var i = 0; i < length; i++)
				{
					if (debugInfos[i].MethodName == sMethodName)
					{
						startAt = i;
						break;
					}
				}
				if (startAt == -1)
					return false;

				// search the end
				for (var i = startAt; i < length; i++)
				{
					if (debugInfos[i].MethodName != sMethodName)
					{
						endAt = i - 1;
						return true;
					}
				}

				endAt = debugInfos.Count - 1;
				return true;
			} // func GetMethodRange

			protected internal override ILuaDebugInfo GetDebugInfo(MethodBase method, int ilOffset)
			{
				var info = (LuaDebugInfo)null;

				// find method range
				if (!GetMethodRange(method.Name, out var startAt, out var endAt))
					return null;

				// find debug info
				if (debugInfos != null)
				{
					for (var i = startAt; i <= endAt; i++)
					{
						if (debugInfos[i].ILOffset <= ilOffset)
							info = debugInfos[i];
						else if (debugInfos[i].ILOffset > ilOffset)
							break;
					}
				}

				// clear debug
				if (info != null && info.IsClear)
					info = null;

				return info;
			} // func GetDebugInfo

			public override bool HasDebugInfo => debugInfos != null;
		} // class LuabStackTraceChunk

		#endregion

		private AssemblyBuilder assembly = null;
		private ModuleBuilder module = null;

		#region -- Ctor/Dtor ----------------------------------------------------------

		/// <summary>Creates a new StackTrace-Debugger</summary>
		public LuaStackTraceDebugger()
		{
		} // ctor

		#endregion

		#region -- CreateChunk --------------------------------------------------------

		private string CreateUniqueTypeName(string name)
		{
			var index = 0;
			var typeName = name;

			var types = module.GetTypes();
			while (Array.Exists(types, c => c.Name == typeName))
				typeName = name + (++index).ToString();

			return typeName;
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
				var type = module.DefineType(CreateUniqueTypeName(expr.Name), TypeAttributes.NotPublic | TypeAttributes.Sealed);

				// transform the expression
				var reduce = new ReduceDynamic(lua, this, type);
				expr = (LambdaExpression)reduce.Visit(expr);

				// compile the function
				var method = type.DefineMethod(expr.Name, MethodAttributes.Static | MethodAttributes.Public);
				var chunk = new LuaStackTraceChunk(lua, expr.Name);
				var collectedDebugInfo = new LuaDebugInfoGenerator(chunk);
				expr.CompileToMethod(method, collectedDebugInfo);

				// create the type and build the delegate
				var typeFinished = type.CreateType();

				// Initialize fields, create the static callsite's
				reduce.InitMethods(typeFinished);

				// return chunk
				return chunk.InitChunk(Delegate.CreateDelegate(expr.Type, typeFinished.GetMethod(expr.Name)));
			}
		} // func ILuaDebug.CreateChunk

		#endregion

		LuaDebugLevel ILuaDebug.Level => LuaDebugLevel.Expression | LuaDebugLevel.RegisterMethods;

		// -- Static ----------------------------------------------------------

		private static readonly object luaDynamicNameLock = new object();
		private static AssemblyName luaDynamicName = null;
		private static readonly ILuaDebug stackTraceDebugger = new LuaStackTraceDebugger();

		private static readonly MethodInfo luaExceptionDataGetDataMethodInfo;

		static LuaStackTraceDebugger()
		{
			var tiLuaExceptionData = typeof(LuaExceptionData).GetTypeInfo();
			luaExceptionDataGetDataMethodInfo = tiLuaExceptionData.FindDeclaredMethod("GetData", ReflectionFlag.Static | ReflectionFlag.NoArguments);
		} // ctor

		private static AssemblyName GetLuaDynamicName()
		{
			lock (luaDynamicNameLock)
			{
				if (luaDynamicName == null)
				{
					byte[] bKey;
					using (var src = typeof(Lua).Assembly.GetManifestResourceStream("Neo.IronLua.NeoLua.snk"))
					{
						bKey = new byte[src.Length];
						src.Read(bKey, 0, bKey.Length);
					}

					// create the strong name
					luaDynamicName = new AssemblyName
					{
						Name = "lua.dynamic",
						Version = new Version(),
						Flags = AssemblyNameFlags.PublicKey,
						HashAlgorithm = System.Configuration.Assemblies.AssemblyHashAlgorithm.SHA1,
						KeyPair = new StrongNameKeyPair(bKey)
					};
				}

				return luaDynamicName;
			}
		} // func GetLuaDynamicName

		/// <summary>Default debugger for the stack trace debugger.</summary>
		public static ILuaDebug Default => stackTraceDebugger;
	} // class LuaStackTraceDebugger

	#endregion
}
#endif
