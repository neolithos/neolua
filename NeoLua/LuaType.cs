using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace Neo.IronLua
{
	#region -- enum LuaMethodEnumerate --------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Enumeration type.</summary>
	public enum LuaMethodEnumerate
	{
		/// <summary>Enumerate only public static methods.</summary>
		Static,
		/// <summary>Enumerate only public non-static methods.</summary>
		Typed,
		/// <summary>Enumerate public non-static and interface methods.</summary>
		Dynamic
	} // enum LuaMethodEnumerate

	#endregion

	#region -- interface ILuaTypeResolver -----------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Interface to the logic, that resolves the types.</summary>
	public interface ILuaTypeResolver
	{
		/// <summary>Gets called before the resolve of a assembly.</summary>
		void Refresh();
		/// <summary>Resolves the type.</summary>
		/// <param name="sTypeName">Name of the type.</param>
		/// <returns></returns>
		Type GetType(string sTypeName);
		/// <summary>Versioninformation for the re-resolve of types.</summary>
		int Version { get; }
	} // interface ILuaTypeResolver

	#endregion

	#region -- class LuaSimpleTypeResolver ----------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Simple resolver, that iterates over a list of static assemblies.</summary>
	public class LuaSimpleTypeResolver : ILuaTypeResolver
	{
		private List<Assembly> assemblies = new List<Assembly>();

		/// <summary>Creates a simple type resolver.</summary>
		public LuaSimpleTypeResolver()
		{
			Add(typeof(string).GetTypeInfo().Assembly);
			Add(GetType().GetTypeInfo().Assembly);
		} // ctor

		/// <summary>Appends a new assembly for the search.</summary>
		/// <param name="assembly">Assembly</param>
		public void Add(Assembly assembly)
		{
			lock (assemblies)
			{
				if (assemblies.IndexOf(assembly) == -1)
					assemblies.Add(assembly);
			}
		} // proc Add

		void ILuaTypeResolver.Refresh() { }

		Type ILuaTypeResolver.GetType(string sTypeName)
		{
			lock (assemblies)
			{
				foreach (var assembly in assemblies)
				{
					var type = assembly.ExportedTypes.FirstOrDefault(c => c.FullName == sTypeName);
					if (type != null)
						return type;
				}
				return null;
			}
		} // func ILuaTypeResolver.GetType

		int ILuaTypeResolver.Version
		{
			get
			{
				lock (assemblies)
					return assemblies.Count;
			}
		} // prop Version
	} // class LuaSimpleTypeResolver

	#endregion

	#region -- class LuaType ------------------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Base class for the Type-Wrapper.</summary>
	public sealed class LuaType : IDynamicMetaObjectProvider
	{
		#region -- class LuaTypeMetaObject ------------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private class LuaTypeMetaObject : DynamicMetaObject
		{
			#region -- Ctor/Dtor ------------------------------------------------------------

			public LuaTypeMetaObject(Expression expression, LuaType value)
				: base(expression, BindingRestrictions.Empty, value)
			{
			} // ctor

			#endregion

			#region -- BindGetMember --------------------------------------------------------

			public override DynamicMetaObject BindGetMember(GetMemberBinder binder)
			{
				var luaType = ((LuaType)Value);
				var type = luaType.Type;

				if (type != null) // there is a valid type
				{
					Expression expr;
					var r = LuaEmit.TryGetMember(null, type, binder.Name, binder.IgnoreCase, out expr);
					if (r == LuaTryGetMemberReturn.ValidExpression)
					{
						return new DynamicMetaObject(Lua.EnsureType(expr, binder.ReturnType), GetTypeResolvedRestriction(type));
					}
					else if (ExceptionOnMissingMember)
					{
						return new DynamicMetaObject(
							Lua.ThrowExpression(
								LuaEmitException.GetMessageText(r == LuaTryGetMemberReturn.NotReadable ? LuaEmitException.CanNotReadMember : LuaEmitException.MemberNotFound, type.Name, binder.Name),
								binder.ReturnType
							),
							GetTypeResolvedRestriction(type)
						);
					}
					else
						return new DynamicMetaObject(Expression.Default(binder.ReturnType), GetTypeResolvedRestriction(type));
				}
				else
				{
					// Get the index for the access, as long is there no type behind
					var expr = Expression.Condition(
						GetUpdateCondition(),
						binder.GetUpdateExpression(binder.ReturnType),
						Lua.EnsureType(Expression.Call(Lua.TypeGetTypeMethodInfoArgIndex, Expression.Constant(luaType.GetIndex(binder.Name, binder.IgnoreCase, null), typeof(int))), binder.ReturnType)
					);
					return new DynamicMetaObject(expr, GetTypeNotResolvedRestriction());
				}
			} // func BindGetMember

			#endregion

			#region -- BindSetMember --------------------------------------------------------

			public override DynamicMetaObject BindSetMember(SetMemberBinder binder, DynamicMetaObject value)
			{
				var luaType = (LuaType)Value;
				var type = luaType;

				if (type != null)
				{
					Expression result;
					var r = LuaEmit.TrySetMember(null, type, binder.Name, binder.IgnoreCase,
						(setType) =>
						{
							try
							{
								return LuaEmit.ConvertWithRuntime(Lua.GetRuntime(binder), value.Expression, value.LimitType, setType);
							}
							catch (LuaEmitException e)
							{
								return Lua.ThrowExpression(e.Message, setType);
							}
						},
						out result);

					if (r == LuaTrySetMemberReturn.ValidExpression)
						return new DynamicMetaObject(Lua.EnsureType(result, binder.ReturnType), GetTypeResolvedRestriction(type).Merge(Lua.GetSimpleRestriction(value)));
					else
					{
						return new DynamicMetaObject(
							Lua.ThrowExpression(LuaEmitException.GetMessageText(
								r == LuaTrySetMemberReturn.NotWritable ? LuaEmitException.CanNotWriteMember : LuaEmitException.MemberNotFound,
								type.Name, binder.Name), binder.ReturnType),
							GetTypeResolvedRestriction(type)
						);
					}
				}
				else
				{
					var expr = Expression.Condition(
						GetUpdateCondition(),
						binder.GetUpdateExpression(binder.ReturnType),
						Lua.ThrowExpression(String.Format(Properties.Resources.rsMemberNotWritable, "LuaType", binder.Name), binder.ReturnType)
					);
					return new DynamicMetaObject(expr, GetTypeNotResolvedRestriction());
				}
			} // proc BindSetMember

			#endregion

			#region -- BindGetIndex ---------------------------------------------------------

			public override DynamicMetaObject BindGetIndex(GetIndexBinder binder, DynamicMetaObject[] indexes)
			{
				LuaType val = (LuaType)Value;
				Type type = val.Type;
				if (type != null && indexes.Length == 0)
				{
					// create a array of the type
					return new DynamicMetaObject(
						Expression.Call(Lua.TypeGetTypeMethodInfoArgIndex,
							Expression.Constant(val.GetIndex("[]", true, () => type.MakeArrayType()), typeof(int))
						),
						GetTypeResolvedRestriction(type)
					);
				}
				else
				{
					if (indexes.Any(c => !c.HasValue))
						return binder.Defer(indexes);

					// is the current type a array
					if (indexes.All(c => LuaEmit.IsIntegerType(LuaEmit.GetTypeCode(c.LimitType))))
					{
						return new DynamicMetaObject(
							Expression.NewArrayBounds(type, from c in indexes select Lua.EnsureType(c.Expression, typeof(int))),
							Lua.GetMethodSignatureRestriction(this, indexes));
					}
					else
					{
						// create the generic type name
						StringBuilder sbTypeName = new StringBuilder();
						val.GetFullName(sbTypeName);
						sbTypeName.Append('`').Append(indexes.Length);

						// find the type
						Type typeGeneric = Type.GetType(sbTypeName.ToString(), false);
						if (typeGeneric == null)
						{
							return new DynamicMetaObject(
								Lua.ThrowExpression(String.Format(Properties.Resources.rsParseUnknownType, sbTypeName.ToString())),
								GetTypeResolvedRestriction(type).Merge(Lua.GetMethodSignatureRestriction(null, indexes))
							);
						}

						// check, only types are allowed
						if (indexes.Any(c => c.LimitType != typeof(LuaType) && c.LimitType != typeof(Type)))
						{
							return new DynamicMetaObject(
								Lua.ThrowExpression(Properties.Resources.rsClrGenericTypeExpected),
								GetTypeResolvedRestriction(type).Merge(Lua.GetMethodSignatureRestriction(null, indexes))
							);
						}

						// create the call to the runtime
						return new DynamicMetaObject(
							Expression.Call(Expression.Convert(Expression, typeof(LuaType)), Lua.TypeGetGenericItemMethodInfo,
								Expression.Constant(typeGeneric),
								Expression.NewArrayInit(typeof(LuaType), (from a in indexes select ConvertToLuaType(a)).AsEnumerable())
							),
							GetTypeResolvedRestriction(type).Merge(Lua.GetMethodSignatureRestriction(null, indexes))
						);
					}
				}
			} // func BindGetIndex

			#endregion

			#region -- BindSetIndex ---------------------------------------------------------

			public override DynamicMetaObject BindSetIndex(SetIndexBinder binder, DynamicMetaObject[] indexes, DynamicMetaObject value)
			{
				return new DynamicMetaObject(
					Lua.ThrowExpression(Properties.Resources.rsIndexNotFound, binder.ReturnType),
					BindingRestrictions.GetTypeRestriction(Expression, LimitType)
				);
			} // func BindSetIndex

			#endregion

			#region -- BindInvokeMember -----------------------------------------------------

			public override DynamicMetaObject BindInvokeMember(InvokeMemberBinder binder, DynamicMetaObject[] args)
			{
				Type type = ((LuaType)Value).Type;

				BindingRestrictions restrictions;
				Expression expr;
				if (type != null)
				{
					try
					{
						MethodInfo mi = LuaEmit.FindMethod(
							type.GetRuntimeMethods().Where(
								c => String.Compare(binder.Name, c.Name, binder.IgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) == 0 && c.IsStatic
							), binder.CallInfo, args, mo => mo.LimitType, false
						);
						if (mi == null)
						{
							var stringComparison = binder.IgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
							if (args.Length == 0 && String.Compare(binder.Name, "GetType", stringComparison) == 0)
							{
								restrictions = BindingRestrictions.GetInstanceRestriction(Expression, Value);
								expr = Lua.EnsureType(Expression.Property(Lua.EnsureType(Expression, typeof(LuaType)), Lua.TypeTypePropertyInfo), binder.ReturnType);
							}
							else if (String.Compare(binder.Name, "ctor", stringComparison) == 0)
							{
								return BindNewObject(type, binder.CallInfo, args, binder.ReturnType);
							}
							else
							{
								restrictions = Lua.GetMethodSignatureRestriction(this, args);
								expr = Lua.ThrowExpression(Properties.Resources.rsNilNotCallable, binder.ReturnType);
							}
						}
						else
						{
							restrictions = Lua.GetMethodSignatureRestriction(this, args);
							expr = Lua.EnsureType(LuaEmit.BindParameter(Lua.GetRuntime(binder),
								a => Expression.Call(null, mi, a),
								mi.GetParameters(),
								binder.CallInfo,
								args,
								mo => mo.Expression, mo => mo.LimitType, false), binder.ReturnType, true);
						}
					}
					catch (LuaEmitException e)
					{
						restrictions = BindingRestrictions.GetInstanceRestriction(Expression, Value);
						expr = Lua.ThrowExpression(e.Message, binder.ReturnType);
					}
					return new DynamicMetaObject(expr, restrictions.Merge(GetTypeResolvedRestriction(type)));
				}
				else
				{
					expr = Expression.Condition(
						 GetUpdateCondition(),
						 binder.GetUpdateExpression(binder.ReturnType),
						 Lua.ThrowExpression(Properties.Resources.rsNilNotCallable, binder.ReturnType)
					 );

					return new DynamicMetaObject(expr, GetTypeNotResolvedRestriction());
				}
			} // func BindInvokeMember

			#endregion

			#region -- BindInvoke -----------------------------------------------------------

			public override DynamicMetaObject BindInvoke(InvokeBinder binder, DynamicMetaObject[] args)
			{
				Type type = ((LuaType)Value).Type;

				if (type != null)
				{
					if (type.IsArray) // initialize the array
					{
						Expression expr;
						if (args.Length == 1)
						{
							expr = Expression.Call(Lua.InitArray1MethodInfo, Expression.Constant(type.GetElementType()), Lua.EnsureType(args[0].Expression, typeof(object)));
						}
						else
						{
							expr = Expression.Call(Lua.InitArrayNMethodInfo,
								 Expression.Constant(type.GetElementType()), Expression.NewArrayInit(typeof(object), from a in args select Lua.EnsureType(a.Expression, typeof(object)))
							);
						}

						return new DynamicMetaObject(expr, BindingRestrictions.GetInstanceRestriction(Expression, Value).Merge(Lua.GetMethodSignatureRestriction(null, args)));
					}
					else // call the constructor
						return BindNewObject(type, binder.CallInfo, args, binder.ReturnType);
				}
				else
				{
					Expression expr =
						Expression.Condition(
							GetUpdateCondition(),
							binder.GetUpdateExpression(binder.ReturnType),
							Lua.ThrowExpression(Properties.Resources.rsNullReference, binder.ReturnType)
						);
					return new DynamicMetaObject(expr, BindingRestrictions.GetInstanceRestriction(Expression, Value));
				}
			} // func BindInvoke

			#endregion

			#region -- BindConvert ----------------------------------------------------------

			public override DynamicMetaObject BindConvert(ConvertBinder binder)
			{
				if (binder.Type == typeof(Type))
				{
					return new DynamicMetaObject(
						Lua.EnsureType(Expression.Property(Lua.EnsureType(Expression, typeof(LuaType)), Lua.TypeTypePropertyInfo), typeof(Type)),
						BindingRestrictions.GetTypeRestriction(Expression, typeof(LuaType))
					);
				}
				return base.BindConvert(binder);
			} // func BindConvert

			#endregion

			private DynamicMetaObject BindNewObject(Type typeNew, CallInfo callInfo, DynamicMetaObject[] args, Type returnType)
			{
				Expression expr;
				try
				{
					var typeinfoNew = typeNew.GetTypeInfo();
					ConstructorInfo ci =
						typeinfoNew.IsValueType && args.Length == 0 ?  // value-types with zero arguments always constructable
							null :
							LuaEmit.FindMember(typeNew.GetTypeInfo().DeclaredConstructors.Where(c => c.IsPublic), callInfo, args, mo => mo.LimitType, false);

					// ctor not found for a class
					if (ci == null && !typeinfoNew.IsValueType)
						expr = Lua.ThrowExpression(String.Format(Properties.Resources.rsMemberNotResolved, typeNew.Name, "ctor"), returnType);

					// create the object
					expr = Lua.EnsureType(
						LuaEmit.BindParameter(null,
							a => ci == null ? Expression.New(typeNew) : Expression.New(ci, a),
							ci == null ? new ParameterInfo[0] : ci.GetParameters(),
							callInfo,
							args,
							mo => mo.Expression, mo => mo.LimitType, false),
						returnType, true
					);
				}
				catch (LuaEmitException e)
				{
					expr = Lua.ThrowExpression(e.Message, returnType);
				}
				return new DynamicMetaObject(expr, GetTypeResolvedRestriction(typeNew).Merge(Lua.GetMethodSignatureRestriction(null, args)));
			} // func BindNewObject

			private BindingRestrictions GetTypeNotResolvedRestriction()
			{
				// expr == typeof(LuaType) && (expr.Parent == value.Parent && expr.Type == null)
				//return BindingRestrictions.GetExpressionRestriction(
				//	Expression.AndAlso(
				//		Expression.TypeEqual(Expression, typeof(LuaType)),
				//		Expression.AndAlso(
				//			Expression.ReferenceEqual(
				//				Expression.Property(Expression.Convert(Expression, typeof(LuaType)), Lua.TypeParentPropertyInfo),
				//				Expression.Constant(((LuaType)Value).Parent)
				//			),
				//			Expression.Equal(
				//				Expression.Property(Lua.EnsureType(Expression, typeof(LuaType)), Lua.TypeTypePropertyInfo),
				//				Expression.Default(typeof(Type))
				//			)
				//		)
				//	)
				//);
				// fix = expr == value  :: consumes memory
				return BindingRestrictions.GetInstanceRestriction(Expression, Value);
			} // func GetTypeNotResolvedRestriction

			private BindingRestrictions GetTypeResolvedRestriction(Type type)
			{
				return BindingRestrictions.GetExpressionRestriction(
					Expression.AndAlso(
						Expression.TypeEqual(Expression, typeof(LuaType)),
						Expression.Equal(
							Expression.Property(Lua.EnsureType(Expression, typeof(LuaType)), Lua.TypeTypePropertyInfo),
							Expression.Constant(type)
						)
					)
				);
			} // func GetTypeResolvedRestriction

			private BinaryExpression GetUpdateCondition()
			{
				return Expression.NotEqual(
						Expression.Property(Expression.Convert(Expression, typeof(LuaType)), Lua.TypeTypePropertyInfo),
						Expression.Constant(null, typeof(Type))
				);
			} // func GetUpdateCondition

			public override IEnumerable<string> GetDynamicMemberNames()
			{
				return ((LuaType)Value).index.Keys;
			} // proc GetDynamicMemberNames
		} // class LuaTypeMetaObject

		#endregion

		private LuaType parent;           // Access to parent type or namespace
		private LuaType baseType;         // If the type is inherited, then this points to the base type
		private LuaType baseTypeGeneric;  // If this type is base on a generic typ
		private LuaType[] implementedInterfaces; // Interfaces, that this type implements
		private Type type;                // Type that is represented, null if it is not resolved until now

		private string name;							// Name of the unresolved type or namespace
		private string aliasName = null;	// Current alias name
		private int resolverVersion;			// Number of loaded assemblies or -1 if the type is resolved as a namespace

		private Dictionary<string, int> index = null; // Index to speed up the search in big namespaces
		private List<MethodInfo> extensionMethods = null; // Liste with extension methods

		#region -- Ctor/GetMetaObject -----------------------------------------------------

		private LuaType()
		{
			this.parent = null;
			this.SetType(null, false);
			this.baseType = null;
			this.name = null;
			this.resolverVersion = -2;
		} // ctor

		private LuaType(LuaType parent, string sName, Type type)
		{
			if (type == null)
				throw new ArgumentNullException();

			this.parent = parent;
			this.SetType(type, false);
			this.name = sName;
			this.resolverVersion = 0;
		} // ctor

		private LuaType(LuaType parent, string sName)
		{
			if (String.IsNullOrEmpty(sName))
				throw new ArgumentNullException();

			this.parent = parent;
			this.SetType(null, false);
			this.name = sName;
			this.resolverVersion = 0;
		} // ctor

		/// <summary></summary>
		/// <returns></returns>
		public override string ToString()
		{
			return FullName;
		} // func ToString

		/// <summary>Gets the dynamic interface of a type</summary>
		/// <param name="parameter"></param>
		/// <returns></returns>
		public DynamicMetaObject GetMetaObject(Expression parameter)
		{
			return new LuaTypeMetaObject(parameter, this);
		} // func GetMetaObject

		#endregion

		#region -- ResolveType, GetFullName -----------------------------------------------

		private void ResolveType()
		{
			if (parent != null && // the root has no type
					resolverVersion >= 0) // Namespace, there is no type
			{
				string sTypeName = FullName;

				resolverVersion = 1;

				// new assembly loaded?
				typeResolver.Refresh();
				if (resolverVersion != typeResolver.Version)
				{
					// Set the resolved assembly
					SetType(typeResolver.GetType(sTypeName), true);
					// Update the resolver version
					resolverVersion = typeResolver.Version;
				}
			}
		} // func GetItemType

		private bool SetType(Type type, bool lUpdateKnownTypes)
		{
			if (type == null)
				return false;
			else
			{
				var typeInfo = type.GetTypeInfo();

				// set the value
				this.type = type;

				// update the base type
				baseType = GetNoneGenericType(typeInfo.BaseType);

				// update the known types
				if (lUpdateKnownTypes)
				{
					lock (knownTypes)
						knownTypes[type.FullName] = LuaType.GetTypeIndex(this); // update type cache
				}

				// update implemented types
				implementedInterfaces =
					(from c in typeInfo.ImplementedInterfaces
					 let t = GetNoneGenericType(c)
					 where t != null
					 select t).ToArray();

				return true;
			}
		} // proc SetType

		private LuaType GetNoneGenericType(Type type)
		{
			var tmp = type;
			if (tmp != null && tmp.IsConstructedGenericType)
				tmp = tmp.GetGenericTypeDefinition();
			return tmp != null ? LuaType.GetType(tmp) : null;
		} // func GetNoneGenericType

		private void GetFullName(StringBuilder sb)
		{
			if (parent != null)
			{
				string sName = Name;
				if (parent.parent == null)
					sb.Append(sName);
				else
				{
					parent.GetFullName(sb);
					if (sName[0] != '`' && sName[0] != '[') // is generic type
						if (parent.IsNamespace)
							sb.Append('.');
						else
							sb.Append('+');
					sb.Append(sName);
				}
			}
		} // proc GetFullName

		#endregion

		#region -- Extensions -------------------------------------------------------------

		private void RegisterExtension(MethodInfo mi)
		{
			if (extensionMethods == null)
				extensionMethods = new List<MethodInfo>();

			lock (extensionMethods)
			{
				if (extensionMethods.IndexOf(mi) == -1)
					extensionMethods.Add(mi);
			}
		} // proc RegisterExtension

		#endregion

		#region -- GetIndex, GetItem ------------------------------------------------------

		internal int GetIndex(string sName, bool lIgnoreCase, Func<Type> buildType)
		{
			int iIndex = FindIndexByName(sName, lIgnoreCase);

			// Name not found, create a new one
			if (iIndex == -1)
			{
				// Create the new object
				if (buildType == null)
					iIndex = AddType(new LuaType(this, sName));
				else
					iIndex = AddType(new LuaType(this, sName, buildType()));

				// Update the local index
				if (index == null)
					index = new Dictionary<string, int>();

				index[sName] = iIndex;
			}

			// No type for this level, but sub-items -> it is a namespace
			if (resolverVersion >= 0 && GetType(iIndex).Type != null && Type == null)
			{
				LuaType c = this;
				while (c.parent != null && c.resolverVersion >= 0)
				{
					c.resolverVersion = -1;
					c = c.parent;
				}
			}

			return iIndex;
		} // func GetIndex

		private int FindIndexByName(string sName, bool lIgnoreCase)
		{
			int iIndex = -1;
			if (index != null)
			{
				if (!index.TryGetValue(sName, out iIndex))
				{
					if (lIgnoreCase)
					{
						foreach (var k in index)
						{
							if (String.Compare(sName, k.Key, StringComparison.OrdinalIgnoreCase) == 0)
							{
								iIndex = k.Value;
								break;
							}
						}
					}
					else
						iIndex = -1;
				}
			}
			return iIndex;
		} // func FindIndexByName

		/// <summary>Get the generic type</summary>
		/// <param name="typeGeneric">Generic type</param>
		/// <param name="arguments">Arguments for the generic type</param>
		/// <returns>Created type.</returns>
		public LuaType GetGenericItem(Type typeGeneric, LuaType[] arguments)
		{
			Type[] genericParameters = new Type[arguments.Length];

			// Build the typename
			StringBuilder sb = new StringBuilder();
			sb.Append('`').Append(arguments.Length).Append('[');
			for (int i = 0; i < arguments.Length; i++)
			{
				if (i > 0)
					sb.Append(',');

				Type typeTmp = genericParameters[i] = arguments[i].Type;
				if (typeTmp == null)
					throw new LuaRuntimeException(String.Format(Properties.Resources.rsClrGenericNoType, i), null);

				sb.Append('[').Append(typeTmp.AssemblyQualifiedName).Append(']');
			}
			sb.Append(']');

			// try to find the typename
			lock (this)
				return GetType(GetIndex(sb.ToString(), false, () => typeGeneric.MakeGenericType(genericParameters)));
		} // func GetGenericItem

		#endregion

		#region -- Properties -------------------------------------------------------------

		/// <summary>Name of the LuaType</summary>
		public string Name { get { return name; } }
		/// <summary>FullName of the Clr-Type</summary>
		public string FullName
		{
			get
			{
				StringBuilder sb = new StringBuilder();
				GetFullName(sb);
				return sb.ToString();
			}
		} // func FullName

		/// <summary>Alias name</summary>
		public string AliasName { get { return aliasName; } }
		/// <summary>Returns the alias or if it is <c>null</c> the full name</summary>
		public string AliasOrFullName
		{
			get { return aliasName ?? FullName; }
		} // prop AliasOrFullName

		/// <summary>Type that is represented by the LuaType</summary>
		public Type Type
		{
			get
			{
				lock (this)
				{
					if (type == null)  // no type found
						ResolveType();
					return type;
				}
			}
		} // prop Type

		/// <summary>Is the LuaType only a namespace at the time.</summary>
		public bool IsNamespace { get { return resolverVersion == -1 || type == null && resolverVersion >= 0; } }

		/// <summary>Parent type</summary>
		public LuaType Parent { get { return parent; } }

		#endregion

		private static bool IsCallableMethod(MethodBase methodInfo, bool searchStatic)
			=> methodInfo.IsPublic && !methodInfo.IsAbstract && (methodInfo.CallingConvention & CallingConventions.VarArgs) == 0 && methodInfo.IsStatic == searchStatic;

		private static bool IsCallableField(FieldInfo fieldInfo, bool searchStatic)
			=> fieldInfo.IsPublic && fieldInfo.IsStatic == searchStatic;

		private IEnumerable<T> EnumerateMembers<T>(bool searchStatic, Func<IEnumerable<MemberInfo>, IEnumerable<MemberInfo>> getDeclaredMembers)
				where T : MemberInfo
		{
			var typeInfo = type.GetTypeInfo();
			foreach (var c in (getDeclaredMembers == null ? typeInfo.DeclaredMembers : getDeclaredMembers(typeInfo.DeclaredMembers)))
			{
				if (!(c is T))
					continue;

				if (c is MethodBase && !IsCallableMethod((MethodBase)c, searchStatic))
					continue;
				else if (c is PropertyInfo && !IsCallableMethod(((PropertyInfo)c).GetMethod, searchStatic))
					continue;
				else if (c is FieldInfo && !IsCallableField((FieldInfo)c, searchStatic))
					continue;
				else if (c is EventInfo && !IsCallableMethod(((EventInfo)c).AddMethod, searchStatic))
					continue;
				else if (c is TypeInfo && !((TypeInfo)c).IsNestedPublic)
					continue;

				yield return (T)c;
			}
		} // func EnumerateMembers

		private IEnumerable<T> EnumerateMembers<T>(List<Type> enumeratedTypes, LuaMethodEnumerate searchType, Func<IEnumerable<MemberInfo>, IEnumerable<MemberInfo>> getDeclaredMembers)
			where T : MemberInfo
		{
			if (type == null || enumeratedTypes.IndexOf(type) >= 0)
				yield break;

			// avoid re-enum
			enumeratedTypes.Add(type);

			// Enum members
			foreach (var c in EnumerateMembers<T>(searchType == LuaMethodEnumerate.Static, getDeclaredMembers))
				yield return c;

			// Enum extensions
			if (extensionMethods != null && typeof(T).GetTypeInfo().IsAssignableFrom(typeof(MethodInfo).GetTypeInfo()))
			{
				lock (extensionMethods)
				{
					foreach (var mi in (getDeclaredMembers == null ? extensionMethods : getDeclaredMembers(extensionMethods)))
						yield return (T)(MemberInfo)mi;
				}
			}

			// Enum base members
			if (baseType != null)
			{
				foreach (var c in baseType.EnumerateMembers<T>(enumeratedTypes, searchType, getDeclaredMembers))
					yield return c;
			}

			// Enum interfaces
			if (searchType == LuaMethodEnumerate.Dynamic)
			{
				foreach (var interfaceType in implementedInterfaces)
				{
					foreach (var c in interfaceType.EnumerateMembers<T>(enumeratedTypes, LuaMethodEnumerate.Typed, getDeclaredMembers))
						yield return c;
				}
			}
		} // func EnumerateMembers

		/// <summary></summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="searchType"></param>
		/// <returns></returns>
		public IEnumerable<T> EnumerateMembers<T>(LuaMethodEnumerate searchType)
			where T : MemberInfo
		{
			var enumeratedTyped = new List<Type>();
			return EnumerateMembers<T>(enumeratedTyped, searchType, null);
		} // func EnumerateMembers

		/// <summary></summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="memberName"></param>
		/// <param name="ignoreCase"></param>
		/// <param name="searchType"></param>
		/// <returns></returns>
		public IEnumerable<T> EnumerateMembers<T>(LuaMethodEnumerate searchType, string memberName, bool ignoreCase)
			where T : MemberInfo
		{
			var enumeratedTyped = new List<Type>();
			var stringComparison = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
			return EnumerateMembers<T>(enumeratedTyped, searchType, declaredMembers => declaredMembers.Where(c => String.Compare(c.Name, memberName, stringComparison) == 0));
		} // func EnumerateMembers

		// -- Static --------------------------------------------------------------

		private static LuaType clr = new LuaType();                 // root type
		private static List<LuaType> types = new List<LuaType>();   // SubItems of this type
		private static Dictionary<string, int> knownTypes = new Dictionary<string, int>(); // index for well known types
		private static ILuaTypeResolver typeResolver;

		static LuaType()
		{
			// Find type resolver
			PropertyInfo piLookupReferencedAssemblies = null;
			var typeLuaDesktop = Type.GetType("Neo.IronLua.LuaDeskop, Neo.Lua.Desktop, Version=5.3.0.0, Culture=neutral, PublicKeyToken=fdb0cd4fe8a6e3b2", false);
			if (typeLuaDesktop != null)
			{
				piLookupReferencedAssemblies = typeLuaDesktop.GetRuntimeProperty("LookupReferencedAssemblies");
				var r = typeLuaDesktop.GetRuntimeProperty("LuaTypeResolver");
				if (r != null)
					typeResolver = r.GetValue(null) as ILuaTypeResolver;
			}

			if (typeResolver == null)
				typeResolver = new LuaSimpleTypeResolver();

			// Register basic types
			RegisterTypeAlias("byte", typeof(byte));
			RegisterTypeAlias("sbyte", typeof(sbyte));
			RegisterTypeAlias("short", typeof(short));
			RegisterTypeAlias("ushort", typeof(ushort));
			RegisterTypeAlias("int", typeof(int));
			RegisterTypeAlias("uint", typeof(uint));
			RegisterTypeAlias("long", typeof(long));
			RegisterTypeAlias("ulong", typeof(ulong));
			RegisterTypeAlias("float", typeof(float));
			RegisterTypeAlias("double", typeof(double));
			RegisterTypeAlias("decimal", typeof(decimal));
			RegisterTypeAlias("datetime", typeof(DateTime));
			RegisterTypeAlias("char", typeof(char));
			RegisterTypeAlias("string", typeof(string));
			RegisterTypeAlias("bool", typeof(bool));
			RegisterTypeAlias("object", typeof(object));
			RegisterTypeAlias("type", typeof(Type));
			RegisterTypeAlias("thread", typeof(LuaThread));
			RegisterTypeAlias("luatype", typeof(LuaType));
			RegisterTypeAlias("table", typeof(LuaTable));
			RegisterTypeAlias("result", typeof(LuaResult));
			RegisterTypeAlias("void", typeof(void));

			RegisterTypeExtension(typeof(LuaLibraryString));

			// add these methods as extensions
			RegisterMethodExtension(typeof(string), typeof(string), "Format");
			RegisterMethodExtension(typeof(string), typeof(string), "Join");

			if (piLookupReferencedAssemblies != null)
				piLookupReferencedAssemblies.SetValue(null, true);
		} // /sctor

		#region -- Operator ---------------------------------------------------------------

		/// <summary>implicit convert to type</summary>
		/// <param name="type">lua-type that should convert.</param>
		/// <returns>clr-type</returns>
		public static implicit operator Type(LuaType type)
		{
			return type == null ? null : type.Type;
		} // implicit to type

		#endregion

		#region -- AddType ----------------------------------------------------------------

		private static int AddType(LuaType type)
		{
			int iIndex;
			lock (types)
			{
				// add the type
				iIndex = types.Count;
				types.Add(type);
			}

			// update type cache
			if (type.Type != null)
				lock (knownTypes)
					knownTypes[type.FullName] = iIndex;

			return iIndex;
		} // proc Add

		private static int GetTypeIndex(LuaType type)
		{
			lock (types)
			{
				int iReturn = types.IndexOf(type);
#if DEBUG
				if (iReturn == -1)
					throw new InvalidOperationException();
#endif
				return iReturn;
			}
		} // func

		#endregion

		#region -- GetType ----------------------------------------------------------------

		internal static LuaType GetType(int iIndex)
		{
			lock (types)
				return types[iIndex];
		} // func GetType

		private static LuaType GetType(LuaType current, int iOffset, string sFullName, bool lIgnoreCase, Type type)
		{
			string sCurrentName;

			// search for the offset
			int iNextOffset;
			if (iOffset > 0 && sFullName[iOffset - 1] == '`') // parse namespace
			{
				int iBracketCount = 0;
				iNextOffset = iOffset;
				iOffset--;

				// search for the first [
				while (iNextOffset < sFullName.Length && sFullName[iNextOffset] != '[')
					iNextOffset++;
				while (iNextOffset < sFullName.Length)
				{
					if (sFullName[iNextOffset] == '[')
						iBracketCount++;
					else if (sFullName[iNextOffset] == ']')
					{
						iBracketCount--;
						if (iBracketCount == 0)
						{
							iNextOffset++;
							break;
						}
					}
					iNextOffset++;
				}

				if (iNextOffset == sFullName.Length)
					iNextOffset = -1;
			}
			else
				iNextOffset = sFullName.IndexOfAny(new char[] { '.', '+', '`' }, iOffset);

			// Cut out the current name
			if (iNextOffset == -1)
				sCurrentName = sFullName.Substring(iOffset);
			else
			{
				sCurrentName = sFullName.Substring(iOffset, iNextOffset - iOffset);
				iNextOffset++;
			}

			// try to find the name in the current namespace
			int iIndex = current.GetIndex(sCurrentName, lIgnoreCase, type == null || iNextOffset > 0 ? (Func<Type>)null : () => type);

			// end of the type change found
			if (iNextOffset == -1)
				return GetType(iIndex);
			else
				return GetType(GetType(iIndex), iNextOffset, sFullName, lIgnoreCase, type);
		} // func GetType

		/// <summary>Creates or looks up the LuaType for a clr-type.</summary>
		/// <param name="type">clr-type, that should wrap.</param>
		/// <returns>Wrapped Type</returns>
		public static LuaType GetType(Type type)
		{
			string sFullName = type.FullName;
			lock (knownTypes)
			{
				int iIndex;
				if (knownTypes.TryGetValue(sFullName, out iIndex))
					return GetType(iIndex);
			}
			return GetType(clr, 0, sFullName, false, type);
		} // func GetType

		/// <summary>Creates or looks up the LuaType for a clr-type.</summary>
		/// <param name="sTypeName">Full path to the type (clr-name).</param>
		/// <param name="lIgnoreCase"></param>
		/// <param name="lLateAllowed">Must the type exist or it is possible to bound the type later.</param>
		/// <returns>Wrapped Type, is lLate is <c>false</c> also <c>null</c> is possible.</returns>
		public static LuaType GetType(string sTypeName, bool lIgnoreCase = false, bool lLateAllowed = true)
		{
			// search the tyle in the cache
			LuaType luaType = GetCachedType(sTypeName);

			// create the lua type
			if (luaType == null)
				luaType = GetType(clr, 0, sTypeName, lIgnoreCase, Type.GetType(sTypeName, false));

			// Test the result
			if (lLateAllowed)
				return luaType;
			else if (luaType.Type != null)
				return luaType;
			else
				return null;
		} // func GetType

		/// <summary>Lookup for a well known type.</summary>
		/// <param name="sTypeName">Name of the type</param>
		/// <returns></returns>
		internal static LuaType GetCachedType(string sTypeName)
		{
			lock (knownTypes)
			{
				int iIndex;
				if (knownTypes.TryGetValue(sTypeName, out iIndex))
					return GetType(iIndex);
				else
					return null;
			}
		} // func GetCachedType

		/// <summary>Register a new type alias.</summary>
		/// <param name="sAlias">Name of the type alias. It should be a identifier.</param>
		/// <param name="type">Type of the alias</param>
		public static void RegisterTypeAlias(string sAlias, Type type)
		{
			if (sAlias.IndexOfAny(new char[] { '.', '+', ' ' }) >= 0)
				throw new ArgumentException(String.Format(Properties.Resources.rsTypeAliasInvalidName, sAlias));

			lock (knownTypes)
			{
				int iOldAlias;
				LuaType luaType = LuaType.GetType(type);
				if (knownTypes.TryGetValue(sAlias, out iOldAlias))
				{
					LuaType oldType = LuaType.GetType(iOldAlias);
					if (oldType != luaType)
						oldType.aliasName = null;
				}
				luaType.aliasName = sAlias;
				knownTypes[sAlias] = LuaType.GetTypeIndex(luaType);
			}
		} // proc RegisterTypeAlias

		/// <summary>Registers a type extension.</summary>
		/// <param name="type"></param>
		public static void RegisterTypeExtension(Type type)
		{
			var typeInfo = type.GetTypeInfo();
			if (typeInfo.IsSealed && typeInfo.IsAbstract)
			{
				// Enum all methods and register the extension methods
				LuaType lastType = null;
				foreach (MethodInfo mi in typeInfo.DeclaredMethods)
				{
					if (mi.GetCustomAttribute(typeof(ExtensionAttribute)) != null && mi.GetParameters().Length > 0)
					{
						// Get the lua type
						Type currentType = mi.GetParameters()[0].ParameterType;
						if (lastType == null || currentType != lastType.Type)
							lastType = LuaType.GetType(currentType);

						// register the method
						lastType.RegisterExtension(mi);
					}
				}
			}
			else
				throw new ArgumentException(String.Format(Properties.Resources.rsTypeExtentionInvalidType, type.Name));
		} // proc RegisterTypeExtension

		/// <summary>Registers a single extension method.</summary>
		/// <param name="mi">Method</param>
		public static void RegisterMethodExtension(MethodInfo mi)
		{
			if (mi.IsStatic && mi.IsPublic && mi.GetParameters().Length > 0)
				LuaType.GetType(mi.GetParameters()[0].ParameterType).RegisterExtension(mi);
			else
				throw new ArgumentException(String.Format(Properties.Resources.rsTypeExtentionInvalidMethod, mi.DeclaringType.Name, mi.Name));
		} // proc RegisterMethodExtension

		/// <summary></summary>
		/// <param name="typeToExtent"></param>
		/// <param name="type"></param>
		/// <param name="methodName"></param>
		public static void RegisterMethodExtension(Type typeToExtent, Type type, string methodName)
		{
			var luaTypeToExtent = LuaType.GetType(typeToExtent);
			var typeInfo = type.GetTypeInfo();
			foreach (var mi in typeInfo.DeclaredMethods)
			{
				if (mi.Name == methodName && mi.IsStatic && mi.IsPublic)
				{
					var parameters = mi.GetParameters();
					if (parameters.Length > 1 && parameters[0].ParameterType == typeToExtent)
						luaTypeToExtent.RegisterExtension(mi);
				}
			}
		} // proc RegisterMethodExtension

		#endregion

		internal static Expression ConvertToLuaType(DynamicMetaObject a)
		{
			if (a.LimitType == typeof(LuaType))
				return Expression.Convert(a.Expression, typeof(LuaType));
			else if (typeof(Type).GetTypeInfo().IsAssignableFrom(a.LimitType.GetTypeInfo()))
				return Expression.Convert(Expression.Call(Lua.TypeGetTypeMethodInfoArgType, Expression.Convert(a.Expression, typeof(Type))), typeof(object));
			else
				throw new ArgumentException();
		} // func ConvertToLuaType

		internal static Expression ConvertToType(DynamicMetaObject a)
		{
			if (a.LimitType == typeof(LuaType))
				return Expression.Convert(Expression.Property(Expression.Convert(a.Expression, typeof(LuaType)), Lua.TypeTypePropertyInfo), typeof(Type));
			else if (typeof(Type).GetTypeInfo().IsAssignableFrom(a.LimitType.GetTypeInfo()))
				return Expression.Convert(a.Expression, typeof(Type));
			else
				throw new ArgumentException();
		} // func ConvertToLuaType

		/// <summary>Root for all clr-types.</summary>
		public static LuaType Clr { get { return clr; } }
		/// <summary>Resolver for types.</summary>
		public static ILuaTypeResolver Resolver { get { return typeResolver; } set { typeResolver = value; } }

		/// <summary>Should LuaType throw an exception, if the type is not bindable (default: true).</summary>
		public static bool ExceptionOnMissingMember { get; set; } = true;
	} // class LuaType

	#endregion

	#region -- interface ILuaMethod -----------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public interface ILuaMethod
	{
		/// <summary>Name of the member.</summary>
		string Name { get; }
		/// <summary>Type that is the owner of the member list</summary>
		Type Type { get; }
		/// <summary>Instance, that belongs to the member.</summary>
		object Instance { get; }
	} // interface ILuaMethod

	#endregion

	#region -- class LuaMethod ----------------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Represents overloaded members.</summary>
	public sealed class LuaMethod : ILuaMethod, IDynamicMetaObjectProvider
	{
		#region -- class LuaMethodMetaObject ----------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private class LuaMethodMetaObject : DynamicMetaObject
		{
			public LuaMethodMetaObject(Expression expression, LuaMethod value)
				: base(expression, BindingRestrictions.Empty, value)
			{
			} // ctor

			public override DynamicMetaObject BindInvoke(InvokeBinder binder, DynamicMetaObject[] args)
			{
				LuaMethod val = (LuaMethod)Value;
				return LuaMethod.BindInvoke(Lua.GetRuntime(binder), Expression, val, val.method, binder.CallInfo, args, binder.ReturnType);
			} // proc BindInvoke

			public override DynamicMetaObject BindConvert(ConvertBinder binder)
			{
				if (typeof(Delegate).GetTypeInfo().IsAssignableFrom(binder.Type.GetTypeInfo())) // we expect a delegate
				{
					LuaMethod val = (LuaMethod)Value;
					return CreateDelegate(Expression, val, binder.Type, val.method, binder.ReturnType);
				}
				else if (typeof(MethodInfo).GetTypeInfo().IsAssignableFrom(binder.Type.GetTypeInfo()))
				{
					return new DynamicMetaObject(
						Expression.Property(Lua.EnsureType(Expression, typeof(LuaMethod)), Lua.MethodMethodPropertyInfo),
						BindingRestrictions.GetTypeRestriction(Expression, typeof(LuaMethod))
					);
				}
				else if (typeof(Type).GetTypeInfo().IsAssignableFrom(binder.Type.GetTypeInfo()))
				{
					return ConvertToType(Expression, binder.ReturnType);
				}
				else
					return base.BindConvert(binder);
			} // func BindConvert
		} // class LuaMethodMetaObject

		#endregion

		private readonly object instance;
		private readonly MethodInfo method;

		#region -- Ctor/Dtor --------------------------------------------------------------

		internal LuaMethod(object instance, MethodInfo method)
		{
			this.instance = instance;
			this.method = method;

			if (method == null)
				throw new ArgumentNullException();
		} // ctor

		/// <summary></summary>
		/// <param name="parameter"></param>
		/// <returns></returns>
		public DynamicMetaObject GetMetaObject(Expression parameter)
		{
			return new LuaMethodMetaObject(parameter, this);
		} // func GetMetaObject

		#endregion

		/// <summary></summary>
		/// <returns></returns>
		public override string ToString()
		{
			return method.ToString();
		} // func ToString

		/// <summary>Creates a delegate from the method</summary>
		/// <param name="typeDelegate"></param>
		/// <returns></returns>
		public Delegate CreateDelegate(Type typeDelegate)
		{
			return method.CreateDelegate(typeDelegate, instance);
		} // func CreateDelegate

		/// <summary>Name of the member.</summary>
		public string Name { get { return method.Name; } }
		/// <summary>Type that is the owner of the member list</summary>
		public Type Type { get { return method.DeclaringType; } }
		/// <summary>Instance, that belongs to the member.</summary>
		public object Instance { get { return instance; } }
		/// <summary>Access to the method.</summary>
		public MethodInfo Method { get { return method; } }
		/// <summary>Delegate of the Method</summary>
		public Delegate Delegate { get { return Parser.CreateDelegate(instance, Method); } }

		// -- Static --------------------------------------------------------------

		internal static DynamicMetaObject BindInvoke(Lua runtime, Expression methodExpression, ILuaMethod methodValue, MethodInfo mi, CallInfo callInfo, DynamicMetaObject[] args, Type typeReturn)
		{
			// create the call expression
			Expression expr = Lua.EnsureType(LuaEmit.BindParameter(runtime,
				a => Expression.Call(mi.IsStatic ? null : GetInstance(methodExpression, methodValue, methodValue.Type), mi, a),
				mi.GetParameters(),
				callInfo,
				args,
				mo => mo.Expression, mo => mo.LimitType, true), typeReturn, true);

			return new DynamicMetaObject(expr, BindInvokeRestrictions(methodExpression, methodValue).Merge(Lua.GetMethodSignatureRestriction(null, args)));
		} // func BindInvoke

		private static Expression GetInstance(Expression methodExpression, ILuaMethod methodValue, Type returnType)
		{
			return Lua.EnsureType(Expression.Property(Lua.EnsureType(methodExpression, typeof(ILuaMethod)), Lua.MethodInstancePropertyInfo), returnType);
		} //func GetInstance

		internal static DynamicMetaObject CreateDelegate(Expression methodExpression, ILuaMethod methodValue, Type typeDelegate, MethodInfo miTarget, Type typeReturn)
		{
			if (typeDelegate.GetTypeInfo().BaseType != typeof(MulticastDelegate))
			{
				ParameterInfo[] pis = miTarget.GetParameters();
				Type[] parameters = new Type[pis.Length + 1];
				for (int i = 0; i < parameters.Length - 1; i++)
					parameters[i] = pis[i].ParameterType;
				parameters[parameters.Length - 1] = miTarget.ReturnType;

				typeDelegate = Expression.GetDelegateType(parameters);
			}

			return new DynamicMetaObject(
				Lua.EnsureType(
					Expression.Call(
						Expression.Constant(miTarget),
						Lua.MethodInfoCreateDelegateMethodInfo,
						Expression.Constant(typeDelegate),
						GetInstance(methodExpression, methodValue, typeof(object)) ?? Expression.Default(typeof(object))
					), typeReturn
				),
				BindInvokeRestrictions(methodExpression, methodValue)
			);
		} // func CreateDelegate

		internal static BindingRestrictions BindInvokeRestrictions(Expression methodExpression, ILuaMethod methodValue)
		{
			// create the restrictions
			//   expr is typeof(ILuaMethod) && expr.Type == type && !args!
			return BindingRestrictions.GetExpressionRestriction(
					Expression.AndAlso(
						Expression.TypeEqual(methodExpression, methodValue.GetType()), // exact type, to make a difference between overload and none overload
						Expression.AndAlso(
							Expression.Equal(
								Expression.Property(Expression.Convert(methodExpression, typeof(ILuaMethod)), Lua.MethodTypePropertyInfo),
								Expression.Constant(methodValue.Type)
							),
							Expression.Equal(
								Expression.Property(Expression.Convert(methodExpression, typeof(ILuaMethod)), Lua.MethodNamePropertyInfo),
								Expression.Constant(methodValue.Name)
							)
						)
					)
				);
		} // func BindInvokeRestrictions

		internal static DynamicMetaObject ConvertToType(Expression methodExpression, Type typeReturn)
		{
			return new DynamicMetaObject(
				Lua.EnsureType(Expression.Property(Expression.Convert(methodExpression, typeof(ILuaMethod)), Lua.MethodTypePropertyInfo), typeReturn),
				BindingRestrictions.GetTypeRestriction(methodExpression, typeof(ILuaMethod))
			);
		} // func ConvertToType
	} // class LuaMethod

	#endregion

	#region -- class LuaOverloadedMethod ------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Represents overloaded members.</summary>
	public sealed class LuaOverloadedMethod : ILuaMethod, IDynamicMetaObjectProvider, IEnumerable<Delegate>
	{
		#region -- class LuaOverloadedMethodMetaObject ------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private class LuaOverloadedMethodMetaObject : DynamicMetaObject
		{
			public LuaOverloadedMethodMetaObject(Expression expression, LuaOverloadedMethod value)
				: base(expression, BindingRestrictions.GetTypeRestriction(expression, typeof(LuaOverloadedMethod)), value)
			{
			} // ctor

			public override DynamicMetaObject BindGetIndex(GetIndexBinder binder, DynamicMetaObject[] indexes)
			{
				LuaOverloadedMethod val = (LuaOverloadedMethod)Value;

				if (indexes.Any(c => !c.HasValue))
					return binder.Defer(indexes);

				// Access the normal index
				if (indexes.Length == 1 && indexes[0].LimitType == typeof(int))
					return binder.FallbackGetIndex(this, indexes);

				// check, only types are allowed
				if (indexes.Any(c => c.LimitType != typeof(LuaType) && !typeof(Type).GetTypeInfo().IsAssignableFrom(c.LimitType.GetTypeInfo())))
				{
					return new DynamicMetaObject(
						Lua.ThrowExpression(String.Format(Properties.Resources.rsClrGenericTypeExpected)),
						Lua.GetMethodSignatureRestriction(this, indexes)
					);
				}

				return new DynamicMetaObject(
					Expression.Call(
						Lua.EnsureType(Expression, typeof(LuaOverloadedMethod)),
						Lua.OverloadedMethodGetMethodMethodInfo,
						Expression.Constant(false),
						Expression.NewArrayInit(typeof(Type), (from a in indexes select LuaType.ConvertToType(a)).AsEnumerable())
					),
					Lua.GetMethodSignatureRestriction(this, indexes)
				);
			} // func BindGetIndex

			public override DynamicMetaObject BindInvoke(InvokeBinder binder, DynamicMetaObject[] args)
			{
				LuaOverloadedMethod val = (LuaOverloadedMethod)Value;
				MethodInfo mi = LuaEmit.FindMethod(val.methods, binder.CallInfo, args, mo => mo.LimitType, false);
				if (mi == null)
					return new DynamicMetaObject(
						Lua.ThrowExpression(String.Format(Properties.Resources.rsMemberNotResolved, val.Type, val.Name)),
						LuaMethod.BindInvokeRestrictions(Expression, val).Merge(Lua.GetMethodSignatureRestriction(null, args))
					);
				else
					return LuaMethod.BindInvoke(Lua.GetRuntime(binder), Expression, val, mi, binder.CallInfo, args, binder.ReturnType);
			} // proc BindInvoke

			public override DynamicMetaObject BindConvert(ConvertBinder binder)
			{
				if (typeof(Delegate).GetTypeInfo().IsAssignableFrom(binder.Type.GetTypeInfo()))
				{
					// get the parameters from the invoke method
					var miInvoke = binder.Type.GetRuntimeMethods().Where(c => c.IsPublic && !c.IsStatic && c.Name == "Invoke").FirstOrDefault();
					if (miInvoke == null)
						return base.BindConvert(binder);
					else
					{
						var val = (LuaOverloadedMethod)Value;
						var parameterInfo = miInvoke.GetParameters();
            var miTarget = LuaEmit.FindMethod(val.methods, new CallInfo(parameterInfo.Length), parameterInfo, p => p.ParameterType, false);
						return LuaMethod.CreateDelegate(Expression, val, binder.Type, miTarget, binder.ReturnType);
					}
				}
				else if (typeof(Type).GetTypeInfo().IsAssignableFrom(binder.Type.GetTypeInfo()))
					return LuaMethod.ConvertToType(Expression, binder.ReturnType);
				else
					return base.BindConvert(binder);
			} // func BindConvert
		} // class LuaOverloadedMethodMetaObject

		#endregion

		private readonly object instance;
		private readonly MethodInfo[] methods;

		#region -- Ctor/Dtor --------------------------------------------------------------

		internal LuaOverloadedMethod(object instance, MethodInfo[] methods)
		{
			this.instance = instance;
			this.methods = methods;

			if (methods.Length == 0)
				throw new ArgumentOutOfRangeException();
		} // ctor

		/// <summary></summary>
		/// <param name="parameter"></param>
		/// <returns></returns>
		public DynamicMetaObject GetMetaObject(Expression parameter)
		{
			return new LuaOverloadedMethodMetaObject(parameter, this);
		} // func GetMetaObject

		#endregion

		#region -- GetDelegate, GetMethod -------------------------------------------------

		private MethodInfo FindMethod(bool lExact, params Type[] types)
		{
			for (int i = 0; i < methods.Length; i++)
			{
				ParameterInfo[] parameters = methods[i].GetParameters();
				if (parameters.Length == types.Length)
				{
					bool lMatch = false;

					for (int j = 0; j < parameters.Length; j++)
					{
						bool lOutExact;
						if (LuaEmit.TypesMatch(parameters[j].ParameterType, types[j], out lOutExact) && (!lExact || lOutExact))
						{
							lMatch = true;
							break;
						}
					}

					if (lMatch || types.Length == 0)
						return methods[i];
				}
			}
			return null;
		} // func FindMethod

		/// <summary>Finds the delegate from the signature.</summary>
		/// <param name="lExact"><c>true </c>type must match exact. <c>false</c>, the types only should assignable.</param>
		/// <param name="types">Types</param>
		/// <returns></returns>
		public Delegate GetDelegate(bool lExact, params Type[] types)
		{
			MethodInfo mi = FindMethod(lExact, types);
			return mi == null ? null : Parser.CreateDelegate(instance, mi);
		} // func GetDelegate

		/// <summary>Gets the delegate from the index</summary>
		/// <param name="iIndex">Index</param>
		/// <returns></returns>
		public Delegate GetDelegate(int iIndex)
		{
			return iIndex >= 0 && iIndex < methods.Length ? Parser.CreateDelegate(instance, methods[iIndex]) : null;
		} // func GetDelegate

		/// <summary>Finds the method from the signature</summary>
		/// <param name="lExact"><c>true </c>type must match exact. <c>false</c>, the types only should assignable.</param>
		/// <param name="types"></param>
		/// <returns></returns>
		public LuaMethod GetMethod(bool lExact, params Type[] types)
		{
			MethodInfo mi = FindMethod(true, types);
			return mi == null ? null : new LuaMethod(instance, mi);
		} // func GetMethod

		/// <summary>Gets the method from the index</summary>
		/// <param name="iIndex">Index</param>
		/// <returns></returns>
		public LuaMethod GetMethod(int iIndex)
		{
			return iIndex >= 0 && iIndex < methods.Length ? new LuaMethod(instance, methods[iIndex]) : null;
		} // func GetMethod

		#endregion

		/// <summary></summary>
		/// <returns></returns>
		public IEnumerator<Delegate> GetEnumerator()
		{
			for (int i = 0; i < methods.Length; i++)
				yield return Parser.CreateDelegate(instance, methods[i]);
		} // func GetEnumerator

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		} // func System.Collections.IEnumerable.GetEnumerator

		/// <summary></summary>
		/// <returns></returns>
		public override string ToString()
		{
			return methods[0].DeclaringType.Name + "." + methods[0].Name + " overloaded";
		} // func ToString

		/// <summary></summary>
		/// <param name="iIndex"></param>
		/// <returns></returns>
		public LuaMethod this[int iIndex] { get { return GetMethod(iIndex); } }
		/// <summary></summary>
		/// <param name="types"></param>
		/// <returns></returns>
		public LuaMethod this[params Type[] types] { get { return GetMethod(true, types); } }

		/// <summary>Name of the member.</summary>
		public string Name { get { return methods[0].Name; } }
		/// <summary>Type that is the owner of the member list</summary>
		public Type Type { get { return methods[0].DeclaringType; } }
		/// <summary>Instance, that belongs to the member.</summary>
		public object Instance { get { return instance; } }
		/// <summary>Count of overloade members.</summary>
		public int Count { get { return methods.Length; } }
	} // class LuaOverloadedMethod

	#endregion

	#region -- class LuaEvent -----------------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public sealed class LuaEvent : ILuaMethod, IDynamicMetaObjectProvider
	{
		#region -- class LuaEventMetaObject -----------------------------------------------

		private class LuaEventMetaObject : DynamicMetaObject
		{
			private const string csAdd = "add";
			private const string csDel = "del";
			private const string csRemove = "remove";

			public LuaEventMetaObject(Expression parameter, LuaEvent value)
				: base(parameter, BindingRestrictions.Empty, value)
			{
			} // ctor

			#region -- BindAddMethod, BindRemoveMethod, BindGetMember -----------------------

			private DynamicMetaObject BindAddMethod(DynamicMetaObjectBinder binder, DynamicMetaObject[] args)
			{
				LuaEvent value = (LuaEvent)Value;
				return LuaMethod.BindInvoke(Lua.GetRuntime(binder), Expression, value, value.eventInfo.AddMethod, new CallInfo(args.Length), args, binder.ReturnType);
			} // func BindAddMethod

			private DynamicMetaObject BindRemoveMethod(DynamicMetaObjectBinder binder, DynamicMetaObject[] args)
			{
				LuaEvent value = (LuaEvent)Value;
				return LuaMethod.BindInvoke(Lua.GetRuntime(binder), Expression, value, value.eventInfo.RemoveMethod, new CallInfo(args.Length), args, binder.ReturnType);
			} // func BindRemoveMethod

			private DynamicMetaObject BindGetMember(DynamicMetaObjectBinder binder, PropertyInfo piMethodGet)
			{
				LuaEvent value = (LuaEvent)Value;
				return new DynamicMetaObject(
					Lua.EnsureType(
						Expression.New(Lua.MethodConstructorInfo,
							Expression.Property(Lua.EnsureType(Expression, typeof(ILuaMethod)), Lua.MethodInstancePropertyInfo),
							Expression.Property(Lua.EnsureType(Expression, typeof(LuaEvent)), piMethodGet)
						),
						binder.ReturnType
					),
					LuaMethod.BindInvokeRestrictions(Expression, value)
				);
			} // func BindGetMember

			#endregion

			#region -- Binder ---------------------------------------------------------------

			public override DynamicMetaObject BindBinaryOperation(BinaryOperationBinder binder, DynamicMetaObject arg)
			{
				if (binder.Operation == ExpressionType.LeftShift)  // << translate to add, not useable under lua
					return BindAddMethod(binder, new DynamicMetaObject[] { arg });
				else if (binder.Operation == ExpressionType.RightShift) // >> translate to remove, not useable under lua
					return BindRemoveMethod(binder, new DynamicMetaObject[] { arg });
				else
					return base.BindBinaryOperation(binder, arg);
			} // func BindBinaryOperation

			public override DynamicMetaObject BindGetMember(GetMemberBinder binder)
			{
				var stringComparison = binder.IgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
				if (String.Compare(binder.Name, csAdd, stringComparison) == 0)
				{
					return BindGetMember(binder, Lua.AddMethodInfoPropertyInfo);
				}
				else if (String.Compare(binder.Name, csDel, stringComparison) == 0 ||
					String.Compare(binder.Name, csRemove, stringComparison) == 0)
				{
					return BindGetMember(binder, Lua.RemoveMethodInfoPropertyInfo);
				}
				else
					return base.BindGetMember(binder);
			} // func BindGetMember

			public override DynamicMetaObject BindInvokeMember(InvokeMemberBinder binder, DynamicMetaObject[] args)
			{
				var stringComparison = binder.IgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
				if (String.Compare(binder.Name, csAdd, stringComparison) == 0)
				{
					return BindAddMethod(binder, args);
				}
				else if (String.Compare(binder.Name, csDel, stringComparison) == 0 ||
					String.Compare(binder.Name, csRemove, stringComparison) == 0)
				{
					return BindRemoveMethod(binder, args);
				}
				else
					return base.BindInvokeMember(binder, args);
			} // func BindInvokeMember

			#endregion
		} // class LuaEventMetaObject

		#endregion

		private readonly object instance;
		private readonly EventInfo eventInfo;

		#region -- Ctor/Dtor --------------------------------------------------------------

		internal LuaEvent(object instance, EventInfo eventInfo)
		{
			this.instance = instance;
			this.eventInfo = eventInfo;

			if (eventInfo == null)
				throw new ArgumentNullException();
		} // ctor

		/// <summary></summary>
		/// <param name="parameter"></param>
		/// <returns></returns>
		public DynamicMetaObject GetMetaObject(Expression parameter)
		{
			return new LuaEventMetaObject(parameter, this);
		} // func GetMetaObject

		#endregion

		/// <summary></summary>
		/// <returns></returns>
		public override string ToString()
		{
			return "event: " + eventInfo.Name;
		} // func ToString

		/// <summary>Name of the event.</summary>
		public string Name { get { return eventInfo.Name; } }
		/// <summary>Type that is the owner of the member list</summary>
		public Type Type { get { return eventInfo.DeclaringType; } }
		/// <summary>Instance, that belongs to the member.</summary>
		public object Instance { get { return instance; } }

		internal MethodInfo AddMethodInfo { get { return eventInfo.AddMethod; } }
		internal MethodInfo RemoveMethodInfo { get { return eventInfo.RemoveMethod; } }
		internal MethodInfo RaiseMethodInfo { get { return eventInfo.RaiseMethod; } }
	} // class LuaEvent

	#endregion
}
