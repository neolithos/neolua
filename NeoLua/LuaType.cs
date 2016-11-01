using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Globalization;
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
		private const int ResolvedAsNamespace = -1; // type is resolved as a namespace
		private const int ResolvedAsType = -2; // type resolved
		private const int ResolvedAsRoot = -3; // root type

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
				var restrictions = BindingRestrictions.GetInstanceRestriction(Expression, Value);

				if (type != null) // there is a valid type
				{
					Expression expr;
					var r = LuaEmit.TryGetMember(null, type, binder.Name, binder.IgnoreCase, out expr);
					if (r == LuaTryGetMemberReturn.ValidExpression)
					{
						return new DynamicMetaObject(Lua.EnsureType(expr, binder.ReturnType), restrictions);
					}
					else if (r == LuaTryGetMemberReturn.NotReadable)
					{
						return new DynamicMetaObject(
							Lua.ThrowExpression(
								String.Format(Properties.Resources.rsMemberNotReadable, type.Name, binder.Name),
								binder.ReturnType
							),
							restrictions
						);
					}
					else
						return new DynamicMetaObject(Expression.Default(binder.ReturnType), restrictions);
				}
				else
				{
					// Get the index for the access, as long is there no type behind
					var expr = Expression.Condition(
						GetUpdateCondition(),
						binder.GetUpdateExpression(binder.ReturnType),
						Lua.EnsureType(Expression.Call(Lua.TypeGetTypeMethodInfoArgIndex, Expression.Constant(luaType.AddType(binder.Name, binder.IgnoreCase, null), typeof(int))), binder.ReturnType)
					);
					return new DynamicMetaObject(expr, restrictions);
				}
			} // func BindGetMember

			#endregion

			#region -- BindSetMember --------------------------------------------------------

			public override DynamicMetaObject BindSetMember(SetMemberBinder binder, DynamicMetaObject value)
			{
				var luaType = (LuaType)Value;
				var type = luaType;
				var restrictions = BindingRestrictions.GetInstanceRestriction(Expression, Value);

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
						return new DynamicMetaObject(Lua.EnsureType(result, binder.ReturnType), restrictions.Merge(Lua.GetSimpleRestriction(value)));
					else
					{
						return new DynamicMetaObject(
							Lua.ThrowExpression(String.Format(
								r == LuaTrySetMemberReturn.NotWritable ? Properties.Resources.rsMemberNotWritable : Properties.Resources.rsMemberNotResolved,
								type.Name, binder.Name), binder.ReturnType),
							restrictions
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
					return new DynamicMetaObject(expr, restrictions);
				}
			} // proc BindSetMember

			#endregion

			#region -- BindGetIndex ---------------------------------------------------------

			private static bool IsTypeCombatibleType(Type type)
				=> typeof(Type).GetTypeInfo().IsAssignableFrom(type.GetTypeInfo());

			private static Expression ConvertToLuaType(DynamicMetaObject mo)
			{
				if (mo.LimitType == typeof(LuaType))
					return Lua.EnsureType(mo.Expression, typeof(LuaType));
				else
					return Expression.Call(Lua.TypeGetTypeMethodInfoArgType, Lua.EnsureType(mo.Expression, typeof(Type)));
			} // func ConvertToLuaType

			public override DynamicMetaObject BindGetIndex(GetIndexBinder binder, DynamicMetaObject[] indexes)
			{
				var luaType = (LuaType)Value;

				if (indexes.Length == 0) // [], create an array type rank 1
				{
					return new DynamicMetaObject(
						Expression.Call(Lua.EnsureType(Expression, typeof(LuaType)),
							Lua.TypeMakeArrayLuaTypeMethodInfo,
							Expression.Constant(1),
							Expression.Constant(false)
						),
						BindingRestrictions.GetTypeRestriction(Expression, typeof(LuaType))
					);
				}
				else
				{
					if (indexes.Any(c => !c.HasValue))
						return binder.Defer(indexes);

					// all indexes are types -> create a generic luatype
					if (indexes.All(c => c.LimitType == typeof(LuaType) || IsTypeCombatibleType(c.LimitType)))
					{
						return new DynamicMetaObject(
							Expression.Call(Lua.EnsureType(Expression, typeof(LuaType)),
								Lua.TypeMakeGenericLuaTypeMethodInfo,
								Expression.NewArrayInit(typeof(LuaType),
									from c in indexes
									select ConvertToLuaType(c)
								),
								Expression.Constant(false)
							),
							BindingRestrictions.GetTypeRestriction(Expression, typeof(LuaType)).Merge(Lua.GetMethodSignatureRestriction(null, indexes))
						);
					}
					else // create an array instance
					{
						return new DynamicMetaObject(
							Expression.NewArrayBounds(luaType.Type,
								from c in indexes
								select LuaEmit.ConvertWithRuntime(Lua.GetRuntime(binder), c.Expression, c.LimitType, typeof(int))
							),
							BindingRestrictions.GetInstanceRestriction(Expression, Value).Merge(Lua.GetMethodSignatureRestriction(null, indexes))
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
				var luaType = (LuaType)Value;
				var type = luaType.Type;
				var stringComparison = binder.IgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
				var restrictions = BindingRestrictions.GetInstanceRestriction(Expression, Value);

				if (args.Length == 0 && String.Compare(binder.Name, "GetType", stringComparison) == 0) // :GetType is always existent
				{
					return new DynamicMetaObject(
						Lua.EnsureType(
							Expression.Property(Lua.EnsureType(Expression, typeof(LuaType)), Lua.TypeTypePropertyInfo),
							binder.ReturnType
						),
						restrictions
					);
				}
				else if (type == null) // type not resolved
				{
					Expression expr;

					var fallback = binder.FallbackInvokeMember(this, args, new DynamicMetaObject(Lua.ThrowExpression(String.Format(Properties.Resources.rsParseUnknownType, luaType.FullName), binder.ReturnType), restrictions));
					expr = fallback.Expression;
					restrictions = restrictions.Merge(fallback.Restrictions);

					// create a updatable expression
					expr = Expression.Condition(
						 GetUpdateCondition(),
						 binder.GetUpdateExpression(binder.ReturnType),
						 expr
					 );

					return new DynamicMetaObject(expr, restrictions);
				}
				else // type existing
				{
					restrictions = restrictions.Merge(Lua.GetMethodSignatureRestriction(null, args));
					try
					{
						// search for a member
						Expression expr;
						if (LuaEmit.TryInvokeMember<DynamicMetaObject>(Lua.GetRuntime(binder), luaType, null, binder.CallInfo, args, binder.Name, binder.IgnoreCase, a => a.Expression, a => a.LimitType, true, out expr))
							return new DynamicMetaObject(Lua.EnsureType(expr, binder.ReturnType), restrictions);
						else
						{
							if (String.Compare(binder.Name, "ctor", stringComparison) == 0)
								return BindNewObject(luaType, binder.CallInfo, args, binder.ReturnType);
							else
								return binder.FallbackInvokeMember(this, args, new DynamicMetaObject(Lua.ThrowExpression(String.Format(Properties.Resources.rsMemberNotResolved, luaType.FullName, binder.Name), binder.ReturnType), restrictions));
						}
					}
					catch (LuaEmitException e)
					{
						return binder.FallbackInvokeMember(this, args, new DynamicMetaObject(Lua.ThrowExpression(e.Message, binder.ReturnType), restrictions));
					}
				}
			} // func BindInvokeMember

			#endregion

			#region -- BindInvoke -----------------------------------------------------------

			public override DynamicMetaObject BindInvoke(InvokeBinder binder, DynamicMetaObject[] args)
			{
				var luaType = (LuaType)Value;
				var type = luaType.Type;

				if (type == null) // type not resolved
				{
					var restrictions = BindingRestrictions.GetInstanceRestriction(Expression, Value);

					var fallback = binder.FallbackInvoke(this, args, new DynamicMetaObject(Lua.ThrowExpression(Properties.Resources.rsNullReference, binder.ReturnType), restrictions));

					return new DynamicMetaObject(
						Expression.Condition(
							GetUpdateCondition(),
							binder.GetUpdateExpression(binder.ReturnType),
							fallback.Expression
						),
						restrictions.Merge(fallback.Restrictions)
					);
				}
				else if (type.IsArray) // is this a array type -> initialize the array
				{
					Expression expr;
					if (args.Length == 1)
					{
						expr = Expression.Call(Lua.InitArray1MethodInfo,
							Expression.Constant(type.GetElementType()),
							Lua.EnsureType(args[0].Expression, typeof(object))
						);
					}
					else
					{
						expr = Expression.Call(Lua.InitArrayNMethodInfo,
								 Expression.Constant(type.GetElementType()),
								 Expression.NewArrayInit(typeof(object), from a in args select Lua.EnsureType(a.Expression, typeof(object)))
						);
					}

					return new DynamicMetaObject(expr, BindingRestrictions.GetInstanceRestriction(Expression, Value).Merge(Lua.GetMethodSignatureRestriction(null, args)));
				}
				else // call the constructor
					return BindNewObject(luaType, binder.CallInfo, args, binder.ReturnType);
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

			#region -- BindNewObject --------------------------------------------------------

			private DynamicMetaObject BindNewObject(LuaType luaType, CallInfo callInfo, DynamicMetaObject[] args, Type returnType)
			{
				var type = luaType.Type;

				// find the ctor
				ConstructorInfo constructorInfo;
				var isValueType = type.GetTypeInfo().IsValueType;
				if (isValueType && args.Length == 0) // value-types with zero arguments always constructable
					constructorInfo = null;
				else
					constructorInfo = LuaEmit.FindMember<ConstructorInfo, DynamicMetaObject>(luaType.EnumerateMembers<ConstructorInfo>(LuaMethodEnumerate.Typed), callInfo, args, mo => mo.LimitType, false);

				var restrictions = BindingRestrictions.GetInstanceRestriction(Expression, Value).Merge(Lua.GetMethodSignatureRestriction(null, args));

				// ctor not found for a class
				if (constructorInfo == null && !isValueType)
					return new DynamicMetaObject(Lua.ThrowExpression(String.Format(Properties.Resources.rsMemberNotResolved, luaType.FullName, "ctor"), returnType), restrictions);
				else          // create the object
				{
					var expr = Lua.EnsureType(
						LuaEmit.BindParameter(null,
							a => constructorInfo == null ? Expression.New(type) : Expression.New(constructorInfo, a),
							constructorInfo == null ? new ParameterInfo[0] : constructorInfo.GetParameters(),
							callInfo,
							args,
							mo => mo.Expression, mo => mo.LimitType, false),
						returnType, true
					);
					return new DynamicMetaObject(expr, restrictions);
				}
			} // func BindNewObject

			#endregion

			private BinaryExpression GetUpdateCondition()
			{
				return Expression.NotEqual(
						Expression.Property(Expression.Convert(Expression, typeof(LuaType)), Lua.TypeTypePropertyInfo),
						Expression.Constant(null, typeof(Type))
				);
			} // func GetUpdateCondition

			public override IEnumerable<string> GetDynamicMemberNames()
			{
				return ((LuaType)Value).namespaceIndex.Keys;
			} // proc GetDynamicMemberNames
		} // class LuaTypeMetaObject

		#endregion

		private readonly object currentTypeLock = new object();

		private readonly LuaType parent;    // Access to parent type or namespace
		private LuaType baseType;           // If the type is inherited, then this points to the base type
		private Lazy<LuaType[]> implementedInterfaces; // Interfaces, that this type implements
		private Type type;                  // Type that is represented, null if it is not resolved until now

		private readonly string name;               // Name of the unresolved type or namespace
		private readonly string fullName;     // Holds the full name of the type
		private string aliasName = null;            // Current alias name

		private int resolvedVersion;                    // Number of loaded assemblies or -1 if the type is resolved as a namespace
		private object resolveArguments;

		private Dictionary<string, int> namespaceIndex = null; // Index to speed up the search in big namespaces
		private List<MethodInfo> extensionMethods = null; // List of extension methods
		private List<MethodInfo> genericExtensionMethods = null; // Liest of extension methods for the generic definition

		#region -- Ctor/GetMetaObject -----------------------------------------------------

		private LuaType()
			: this(null, null, ResolvedAsRoot, null)
		{
			this.baseType = null;
		} // ctor

		private LuaType(LuaType parent, string name, int resolvedVersion, object resolveArguments)
		{
			if (resolvedVersion != ResolvedAsRoot)
			{
				if (String.IsNullOrEmpty(name))
					throw new ArgumentNullException();
				else if (parent == null)
					throw new ArgumentNullException();
			}

			this.parent = parent;
			this.resolvedVersion = resolvedVersion;
			this.resolveArguments = resolveArguments;
			this.name = name;

			// set full name
			if (parent == null)
				fullName = String.Empty;
			else if (parent.parent == null)
				fullName = name;
			else if (name[0] != '[')  // is generic type or array
				if (parent.IsNamespace)
					fullName = parent.fullName + "." + name;
				else
					fullName = parent.fullName + "+" + name;
			else
				fullName = parent.fullName + name;
		} // ctor

		/// <summary></summary>
		/// <returns></returns>
		public override string ToString()
			=> $"LuaType: {{Name={name}, Fullname={fullName}}}";

		/// <summary>Gets the dynamic interface of a type</summary>
		/// <param name="parameter"></param>
		/// <returns></returns>
		public DynamicMetaObject GetMetaObject(Expression parameter)
			=> new LuaTypeMetaObject(parameter, this);

		#endregion

		#region -- ResolveType, SetType ---------------------------------------------------

		private void ResolveType()
		{
			if (resolvedVersion >= 0) // Namespace, there is no type
			{
				bool typeResolved;

				// new assembly loaded?
				typeResolver.Refresh();
				if (resolvedVersion != typeResolver.Version)
				{
					// check the resolve arguments
					if (resolveArguments == null) // resolve normal unknown type
					{
						typeResolved = SetType(typeResolver.GetType(FullName));
					}
					else if (resolveArguments is Type) // resolve normal known type
					{
						typeResolved = SetType((Type)resolveArguments);
					}
					else if (resolveArguments is int) // create an array
					{
						if (parent.Type != null)
							typeResolved = SetType(parent.MakeArrayClrType((int)resolveArguments));
						else
							typeResolved = false;
					}
					else if (resolveArguments is LuaType[]) // create a generic type
					{
						typeResolved = SetType(parent.MakeGenericClrType((LuaType[])resolveArguments, false));
					}
					else
						throw new InvalidOperationException();

					// Update the resolver version
					if (typeResolved)
					{
						resolveArguments = null;
						resolvedVersion = ResolvedAsType;
					}
					else
						resolvedVersion = typeResolver.Version;
				}
			}
		} // func GetItemType

		private bool SetType(Type type)
		{
			if (type == null)
				return false;
			else
			{
				var typeInfo = type.GetTypeInfo();

				if (typeInfo.IsGenericTypeDefinition) // it is only a type definition
					return false;
				else
				{
					// set the value
					this.type = type;

					// update the base type
					baseType = typeInfo.BaseType == null ? null : LuaType.GetType(typeInfo.BaseType);

					// update known types
					lock (knownTypes)
					{
						knownTypes[type] =
							knownTypeStrings[fullName] = LuaType.GetTypeIndex(this); // update type cache
					}

					// update implemented types
					implementedInterfaces = new Lazy<LuaType[]>(() => (from c in typeInfo.ImplementedInterfaces select LuaType.GetType(c)).ToArray(), true);
				}

				return true;
			}
		} // proc SetType

		#endregion

		#region -- Extensions -------------------------------------------------------------

		private void RegisterExtension(MethodInfo mi, bool checkFirstParameter)
		{
			// check the first argument of the method
			var parameterInfo = mi.GetParameters();
			if (checkFirstParameter && parameterInfo.Length == 0)
				throw new ArgumentException(String.Format(Properties.Resources.rsTypeExtentionInvalidMethod, mi.DeclaringType.Name, mi.Name));

			var firstType = parameterInfo[0].ParameterType;
			if (firstType.FullName == null && firstType.IsConstructedGenericType) // generic extension
			{
				if (checkFirstParameter && LuaType.GetType(firstType) != this)
					throw new ArgumentException(String.Format(Properties.Resources.rsTypeExtentionInvalidMethod, mi.DeclaringType.Name, mi.Name));

				lock (currentTypeLock)
				{
					if (genericExtensionMethods == null)
						genericExtensionMethods = new List<MethodInfo>();

					if (genericExtensionMethods.IndexOf(mi) == -1)
						genericExtensionMethods.Add(mi);
				}
			}
			else
			{
				if (checkFirstParameter && firstType != type)
					throw new ArgumentException(String.Format(Properties.Resources.rsTypeExtentionInvalidMethod, mi.DeclaringType.Name, mi.Name));

				lock (currentTypeLock)
				{
					// create the extensions
					if (extensionMethods == null)
						extensionMethods = new List<MethodInfo>();

					// add the method
					if (extensionMethods.IndexOf(mi) == -1)
						extensionMethods.Add(mi);
				}
			}
		} // proc RegisterExtension

		#endregion

		#region -- AddType, MakeGenericType, ... ------------------------------------------

		private void SetNamespace()
		{
			// to the root
			var c = this;
			while (c.parent != null && c.resolvedVersion >= 0)
			{
				c.resolvedVersion = ResolvedAsNamespace;
				c = c.parent;
			}
		} // proc SetNamespace

		internal int AddType(string name, bool ignoreCase, object resolveArguments)
		{
			// is the name already inserted 
			var index = FindIndexByName(name, ignoreCase);

			// create the name
			if (index == -1)
			{
				// create the type
				index = AddType(new LuaType(this, name, 0, resolveArguments));

				// update the local index
				lock (currentTypeLock)
				{
					if (namespaceIndex == null)
						namespaceIndex = new Dictionary<string, int>();
					namespaceIndex[name] = index;
				}
			}

			// check if the current level is a namespace or generic definition
			if (resolvedVersion >= 0 && Type == null)
			{
				var childType = GetType(index).Type;
				if (childType != null)
				{
					if (!childType.IsConstructedGenericType && Type == null)
						SetNamespace();
				}
			}

			return index;
		} // func AddType

		private int FindIndexByName(string sName, bool ignoreCase)
		{
			var index = -1;
			lock (currentTypeLock)
			{
				if (namespaceIndex != null)
				{
					if (!namespaceIndex.TryGetValue(sName, out index))
					{
						if (ignoreCase)
						{
							foreach (var k in namespaceIndex)
							{
								if (String.Compare(sName, k.Key, StringComparison.OrdinalIgnoreCase) == 0)
								{
									index = k.Value;
									break;
								}
							}
						}
						else
							index = -1;
					}
				}
			}
			return index;
		} // func FindIndexByName

		private Type MakeGenericClrType(LuaType[] genericArguments, bool throwException)
		{
			// get the type definition
			var genericTypeDefinitionString = fullName + "`" + genericArguments.Length.ToString(CultureInfo.InvariantCulture);
			var genericTypeDefinition = Type.GetType(genericTypeDefinitionString, throwException);
			if (genericTypeDefinition == null)
			{
				if (throwException)
					throw new ArgumentException(String.Format(Properties.Resources.rsParseUnknownType, genericTypeDefinitionString));
				else
					return null;
			}

			var genericClrArguments = new Type[genericArguments.Length];

			for (var i = 0; i < genericClrArguments.Length; i++)
			{
				var t = genericArguments[i].Type;
				if (t == null)
				{
					if (throwException)
						throw new ArgumentException(String.Format(Properties.Resources.rsParseUnknownType, "<" + i.ToString() + ">" + genericArguments[i].FullName));
					else
						return null;
				}
				genericClrArguments[i] = t;
			}

			return genericTypeDefinition.MakeGenericType(genericClrArguments);
		} // func MakeGenericClrType

		/// <summary>Get the generic type</summary>
		/// <param name="arguments">Arguments for the generic type</param>
		/// <param name="throwException"></param>
		/// <returns>Created type.</returns>
		public LuaType MakeGenericLuaType(LuaType[] arguments, bool throwException = true)
			=> LuaType.GetType(AddType("[" + String.Join(",", from c in arguments select c.FullName) + "]", false, MakeGenericClrType(arguments, throwException)));

		private Type MakeArrayClrType(int rank)
			=> rank == 1 ? Type.MakeArrayType() : Type.MakeArrayType(rank);

		/// <summary></summary>
		/// <param name="rank"></param>
		/// <param name="throwException"></param>
		/// <returns></returns>
		public LuaType MakeArrayLuaType(int rank = 1, bool throwException = true)
		{
			if (Type == null)
			{
				if (throwException)
					throw new ArgumentException(String.Format(Properties.Resources.rsParseUnknownType, FullName));
				else
					return null;
			}
			else
				return LuaType.GetType(AddType("[]", false, rank));
		} // func MakeArrayLuaType

		#endregion

		#region -- EnumerateMembers -------------------------------------------------------

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

			// Do we look for methods
			if (typeof(T).GetTypeInfo().IsAssignableFrom(typeof(MethodInfo).GetTypeInfo()))
			{
				// Enum extensions
				if (extensionMethods != null)
				{
					lock (currentTypeLock)
					{
						foreach (var mi in (getDeclaredMembers == null ? extensionMethods : getDeclaredMembers(extensionMethods)))
							yield return (T)(MemberInfo)mi;
					}
				}

				// Enum generic extensions
				if (parent != null && parent.genericExtensionMethods != null)
				{
					lock (parent.currentTypeLock)
					{
						foreach (var mi in (getDeclaredMembers == null ? parent.genericExtensionMethods : getDeclaredMembers(parent.genericExtensionMethods)))
						{
							var methodInfo = (MethodInfo)mi;

							// get first argument
							var firstArgumentType = methodInfo.GetParameters()[0].ParameterType;
							if (firstArgumentType.GetGenericTypeDefinition() == type.GetGenericTypeDefinition())
								yield return (T)(MemberInfo)mi;
						}
					}
				}
			}

			// Enum base members
			if (baseType != null)
			{
				foreach (var c in baseType.EnumerateMembers<T>(enumeratedTypes, searchType, getDeclaredMembers))
					yield return c;
			}

			// Enum interfaces
			if (searchType == LuaMethodEnumerate.Dynamic && implementedInterfaces != null)
			{
				foreach (var interfaceType in implementedInterfaces.Value)
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

		#endregion

		#region -- Properties -------------------------------------------------------------

		/// <summary>Name of the LuaType</summary>
		public string Name { get { return name; } }
		/// <summary>FullName of the Clr-Type</summary>
		public string FullName => fullName;

		/// <summary>Alias name</summary>
		public string AliasName => aliasName;
		/// <summary>Returns the alias or if it is <c>null</c> the full name</summary>
		public string AliasOrFullName => aliasName ?? fullName;

		/// <summary>Type that is represented by the LuaType</summary>
		public Type Type
		{
			get
			{
				lock (currentTypeLock)
				{
					if (type == null)  // no type found
						ResolveType();
					return type;
				}
			}
		} // prop Type

		/// <summary>Is the LuaType only a namespace at the time.</summary>
		public bool IsNamespace => resolvedVersion == ResolvedAsNamespace || type == null && resolvedVersion >= 0;

		/// <summary>Parent type</summary>
		public LuaType Parent => parent;

		#endregion

		// -- Static --------------------------------------------------------------

		private static LuaType clr = new LuaType();                 // root type

		private static List<LuaType> types = new List<LuaType>();   // SubItems of this type
		private static Dictionary<Type, int> knownTypes = new Dictionary<Type, int>(); // index for well known types
		private static Dictionary<string, int> knownTypeStrings = new Dictionary<string, int>(); // index for well known types

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
			RegisterMethodExtension(typeof(string), typeof(string), nameof(String.Format));
			RegisterMethodExtension(typeof(string), typeof(string), nameof(String.Join));

			// add linq extensions
			RegisterTypeExtension(typeof(Enumerable));

			if (piLookupReferencedAssemblies != null)
				piLookupReferencedAssemblies.SetValue(null, true);
		} // /sctor

		#region -- Operator ---------------------------------------------------------------

		/// <summary>implicit convert to type</summary>
		/// <param name="type">lua-type that should convert.</param>
		/// <returns>clr-type</returns>
		public static implicit operator Type(LuaType type)
			=> type == null ? null : type.Type;

		#endregion

		#region -- AddType ----------------------------------------------------------------

		private static int AddType(LuaType type)
		{
			int index;

			lock (types)
			{
				// add the type
				index = types.Count;
				types.Add(type);
			}

			// try resolve the type
			type.ResolveType();

			return index;
		} // proc Add

		private static int GetTypeIndex(LuaType type)
		{
			lock (types)
			{
				int index = types.IndexOf(type);
#if DEBUG
				if (index == -1)
					throw new InvalidOperationException();
#endif
				return index;
			}
		} // func GetTypeIndex

		#endregion

		#region -- GetType ----------------------------------------------------------------

		internal static LuaType GetType(int index)
		{
			lock (types)
				return types[index];
		} // func GetType

		private static Exception TypeParseException(int offset, string fullName, string expected)
			=> new FormatException(String.Format(Properties.Resources.rsTypeParseError, fullName, offset >= fullName.Length ? "eof" : fullName.Substring(offset, 1), offset, expected));

		private static LuaType GetTypeGenericArgument(ref int offset, string fullName, bool ignoreCase)
		{
			var startAt = offset;
			var bracketCount = 0;

			while (offset < fullName.Length)
			{
				var c = fullName[offset];
				if (bracketCount == 0 && (c == ',' || c == ']'))
				{
					if (fullName[startAt] == '[')
						return GetType(fullName.Substring(startAt + 1, offset - startAt - 2), ignoreCase, true);
					else
						return GetType(fullName.Substring(startAt, offset - startAt), ignoreCase, true);
				}
				else if (c == '[')
					bracketCount++;
				else if (c == ']')
					bracketCount--;

				offset++;
			}

			throw TypeParseException(offset, fullName, ",");
		} // func GetTypeGenericArgument

		private static LuaType GetType(LuaType current, int offset, string fullName, bool ignoreCase, Type type)
		{
			if (fullName.Length <= offset)
				throw TypeParseException(offset, fullName, "part");

			if (fullName[offset] == '[') // array or generic definition
			{
				offset++;
				if (fullName.Length <= offset)
					throw TypeParseException(offset, fullName, "array|generic");
				else if (fullName[offset] == ',' || fullName[offset] == ']') // array
				{
					#region -- array --
					// count the number of dimension
					var startAt = offset - 1;
					var rank = 1;
					while (fullName[offset] == ',')
					{
						rank++;
						offset++;
					}
					if (fullName[offset] == ']')
					{
						offset++;

						// more array definition might follow
						var lastElement = fullName.Length <= offset;
						var luaType = GetType(current.AddType(fullName.Substring(startAt, offset - startAt), false, rank));
						if (lastElement)
							return luaType;
						else
							return GetType(luaType, offset, fullName, false, null);
					}
					else
						throw TypeParseException(offset, fullName, "]");
					#endregion
				}
				else // generic definition
				{
					#region -- generic --
					// collect generic arguments
					var genericArguments = new List<LuaType>();
					var sbTypeName = new StringBuilder("[");
					do
					{
						var arg = GetTypeGenericArgument(ref offset, fullName, ignoreCase);

						// build name
						if (genericArguments.Count > 0)
							sbTypeName.Append(',');
						sbTypeName.Append(arg.Name);

						// collect
						genericArguments.Add(arg);

						// check position
						if (offset >= fullName.Length) // eof of type
							throw TypeParseException(offset, fullName, "]");
						else if (fullName[offset] == ',') // next argument
							offset++;
						else if (fullName[offset] == ']') // check array
						{
							offset++;
							break;
						}
						else
							throw TypeParseException(offset, fullName, "]");

					} while (true);

					sbTypeName.Append(']');

					// is a array following
					var lastElement = fullName.Length <= offset;

					// create the generic type
					var luaType = GetType(current.AddType(sbTypeName.ToString(), false, !lastElement || type == null ? (object)genericArguments.ToArray() : type));

					if (lastElement)
						return luaType;
					else
						return GetType(luaType, offset, fullName, ignoreCase, type);

					#endregion
				}
			}
			else // type or namespace
			{
				#region -- type, namespace --
				// get the current part of the name
				var nextOffset = fullName.IndexOfAny(new char[] { '.', '+', '[', ',' }, offset);
				var currentPart = nextOffset == -1 ? fullName.Substring(offset) : fullName.Substring(offset, nextOffset - offset);

				// if this is a generic type definition remove `n
				var genericMarker = currentPart.IndexOf('`');
				if (genericMarker > 0)
					currentPart = currentPart.Substring(0, genericMarker);

				if (nextOffset == -1) // last element of the chain
				{
					return GetType(current.AddType(currentPart, ignoreCase, type));
				}
				else if (fullName[nextOffset] == '[') // array or generic
				{
					if (fullName.Length == nextOffset + 1)
						throw TypeParseException(offset, fullName, "array|generic");
					else if (fullName[nextOffset + 1] == '[') // generic
					{
						return GetType(GetType(current.AddType(currentPart, ignoreCase, null)), nextOffset, fullName, ignoreCase, type);
					}
					else // array
					{
						var index = type == null ? current.AddType(currentPart, ignoreCase, null) :
							current.AddType(currentPart, ignoreCase, type.GetElementType());

						return GetType(GetType(index), nextOffset, fullName, ignoreCase, type);
					}
				}
				else // Sub-Class or namespace
				{
					return GetType(GetType(current.AddType(currentPart, ignoreCase, null)), nextOffset + 1, fullName, ignoreCase, type);
				}
				#endregion
			}
		} // func GetType

		/// <summary>Creates or looks up the LuaType for a clr-type.</summary>
		/// <param name="type">clr-type, that should wrapped.</param>
		/// <returns>Wrapped Type</returns>
		public static LuaType GetType(Type type)
		{
			if (type == null)
				return clr;

			// is this type well known
			lock (knownTypes)
			{
				int index;
				if (knownTypes.TryGetValue(type, out index))
					return GetType(index);
			}

			// the type is unknown parse the information
			var fullName = type.FullName;
			if (fullName == null)
			{
				if (type.IsConstructedGenericType)
				{
					fullName = type.Namespace + "." + type.Name;
					type = null;
				}
				else
					throw new ArgumentNullException(String.Format(Properties.Resources.rsTypeInvalidType, type.Name));
			}

			return GetType(clr, 0, fullName, false, type);
		} // func GetType

		/// <summary>Creates or looks up the LuaType for a clr-type.</summary>
		/// <param name="typeName">Full path to the type (clr-name).</param>
		/// <param name="ignoreCase"></param>
		/// <param name="lateAllowed">Must the type exist or it is possible to bound the type later.</param>
		/// <returns>Wrapped Type, is lLate is <c>false</c> also <c>null</c> is possible.</returns>
		public static LuaType GetType(string typeName, bool ignoreCase = false, bool lateAllowed = true)
		{
			// remove all stuff, that is not used
			StringBuilder sb = null;
			var bracketCount = 0;
			for (var i = 0; i < typeName.Length; i++)
			{
				var c = typeName[i];

				// escape counter
				if (c == '[')
					bracketCount++;
				else if (c == ']')
					bracketCount--;

				// white space delete
				if (Char.IsWhiteSpace(c))
				{
					if (sb == null)
						sb = new StringBuilder(typeName, 0, i, typeName.Length);
				}
				else if (bracketCount == 0 && c == ',') // cut assembly name
				{
					if (sb == null)
						typeName = typeName.Substring(0, i);
					break;
				}
				else if (sb != null)
					sb.Append(c);
			}
			if (sb != null)
				typeName = sb.ToString();

			// search the type in the cache
			var luaType = GetCachedType(typeName);

			// create the lua type
			if (luaType == null)
				luaType = GetType(clr, 0, typeName, ignoreCase, null);

			// Test the result
			if (lateAllowed)
				return luaType;
			else if (luaType.Type != null)
				return luaType;
			else
				return null;
		} // func GetType

		/// <summary>Lookup for a well known type.</summary>
		/// <param name="typeName">Name of the type (in neolua style)</param>
		/// <returns></returns>
		internal static LuaType GetCachedType(string typeName)
		{
			lock (knownTypes)
			{
				int index;
				if (knownTypeStrings.TryGetValue(typeName, out index))
					return GetType(index);
				else
					return null;
			}
		} // func GetCachedType

		#endregion

		#region -- RegisterTypeAlias ------------------------------------------------------

		/// <summary>Register a new type alias.</summary>
		/// <param name="aliasName">Name of the type alias. It should be a identifier.</param>
		/// <param name="type">Type of the alias</param>
		public static void RegisterTypeAlias(string aliasName, Type type)
		{
			if (aliasName.IndexOfAny(new char[] { '.', '+', ' ', '[', ']', ',' }) >= 0)
				throw new ArgumentException(String.Format(Properties.Resources.rsTypeAliasInvalidName, aliasName));

			lock (knownTypes)
			{
				int oldAliasIndex;
				var luaType = LuaType.GetType(type);
				if (knownTypeStrings.TryGetValue(aliasName, out oldAliasIndex))
				{
					var oldType = LuaType.GetType(oldAliasIndex);
					if (oldType != luaType)
						oldType.aliasName = null;
				}
				luaType.aliasName = aliasName;
				knownTypeStrings[aliasName] = LuaType.GetTypeIndex(luaType);
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
						var currentType = mi.GetParameters()[0].ParameterType;
						if (lastType == null || currentType != lastType.Type)
							lastType = LuaType.GetType(currentType);

						// register the method
						lastType.RegisterExtension(mi, false);
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
			var parameterInfo = mi?.GetParameters();
			if (parameterInfo == null || parameterInfo.Length == 0)
				throw new ArgumentNullException();

			LuaType.GetType(parameterInfo[0].ParameterType).RegisterExtension(mi, true);
		} // proc RegisterMethodExtension

		/// <summary></summary>
		/// <param name="typeToExtent"></param>
		/// <param name="type"></param>
		/// <param name="methodName"></param>
		public static void RegisterMethodExtension(Type typeToExtent, Type type, string methodName)
		{
			var luaTypeToExtent = LuaType.GetType(typeToExtent);
			var typeInfo = type.GetTypeInfo();
			foreach (var mi in typeInfo.DeclaredMethods.Where(c => c.IsStatic && c.IsPublic && c.Name == methodName))
			{
				var parameters = mi.GetParameters();
				if (parameters.Length > 1 && parameters[0].ParameterType == typeToExtent)
					luaTypeToExtent.RegisterExtension(mi, false);
			}
		} // proc RegisterMethodExtension

		#endregion

		/// <summary>Root for all clr-types.</summary>
		public static LuaType Clr => clr;
		/// <summary>Resolver for types.</summary>
		public static ILuaTypeResolver Resolver { get { return typeResolver; } set { typeResolver = value; } }
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
		/// <summary>Is the first parameter a self parameter.</summary>
		bool IsMemberCall { get; }
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
				var val = (LuaMethod)Value;
				return LuaMethod.BindInvoke(Lua.GetRuntime(binder), Expression, val, val.method, binder.CallInfo, args, binder.ReturnType);
			} // proc BindInvoke

			public override DynamicMetaObject BindConvert(ConvertBinder binder)
			{
				if (typeof(Delegate).GetTypeInfo().IsAssignableFrom(binder.Type.GetTypeInfo())) // we expect a delegate
				{
					var val = (LuaMethod)Value;
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
		private readonly bool isMemberCall;
		private readonly MethodInfo method;

		#region -- Ctor/Dtor --------------------------------------------------------------

		internal LuaMethod(object instance, MethodInfo method, bool isMemberCall = false)
		{
			this.instance = instance;
			this.method = method;
			this.isMemberCall = isMemberCall;

			if (method == null)
				throw new ArgumentNullException();
		} // ctor

		/// <summary></summary>
		/// <param name="parameter"></param>
		/// <returns></returns>
		public DynamicMetaObject GetMetaObject(Expression parameter)
			=> new LuaMethodMetaObject(parameter, this);

		#endregion

		/// <summary></summary>
		/// <returns></returns>
		public override string ToString()
			=> method.ToString();

		/// <summary>Creates a delegate from the method</summary>
		/// <param name="typeDelegate"></param>
		/// <returns></returns>
		public Delegate CreateDelegate(Type typeDelegate)
			=> method.CreateDelegate(typeDelegate, instance);

		/// <summary>Name of the member.</summary>
		public string Name => method.Name;
		/// <summary>Type that is the owner of the member list</summary>
		public Type Type => method.DeclaringType;
		/// <summary>Instance, that belongs to the member.</summary>
		public object Instance => instance;
		/// <summary>Use self parameter</summary>
		public bool IsMemberCall => isMemberCall;
		/// <summary>Access to the method.</summary>
		public MethodInfo Method => method;
		/// <summary>Delegate of the Method</summary>
		public Delegate Delegate => Parser.CreateDelegate(instance, Method);

		// -- Static --------------------------------------------------------------

		internal static DynamicMetaObject BindInvoke(Lua runtime, Expression methodExpression, ILuaMethod methodValue, MethodInfo mi, CallInfo callInfo, DynamicMetaObject[] args, Type typeReturn)
		{
			// create the call expression
			Expression expr;
			try
			{
				expr = Lua.EnsureType(
					LuaEmit.InvokeMethod<DynamicMetaObject>(runtime, mi,
						methodValue.Instance == null ? null : new DynamicMetaObject(GetInstance(methodExpression, methodValue, methodValue.Type), BindingRestrictions.Empty),
						callInfo,
						args,
						mo => mo.Expression,
						mo => mo.LimitType,
						true
					),
					typeReturn
				);
			}
			catch (LuaEmitException e)
			{
				expr = Lua.ThrowExpression(e.Message, typeReturn);
			}
			return new DynamicMetaObject(
				expr,
				BindInvokeRestrictions(methodExpression, methodValue).Merge(Lua.GetMethodSignatureRestriction(null, args))
			);
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
			//   expr.GetType() == methodValue.GetType() && expr.Type == methodValue.type && expr.Name == methodValue.Name && !args!
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

			#region -- BindGetIndex ---------------------------------------------------------

			private static Expression ConvertToType(DynamicMetaObject mo)
			{
				if (mo.LimitType == typeof(LuaType))
					return Expression.Convert(Expression.Property(Expression.Convert(mo.Expression, typeof(LuaType)), Lua.TypeTypePropertyInfo), typeof(Type));
				else
					return Expression.Convert(mo.Expression, typeof(Type));
			} // func ConvertToLuaType

			public override DynamicMetaObject BindGetIndex(GetIndexBinder binder, DynamicMetaObject[] indexes)
			{
				var val = (LuaOverloadedMethod)Value;

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
						Expression.NewArrayInit(typeof(Type), (from a in indexes select ConvertToType(a)).AsEnumerable())
					),
					Lua.GetMethodSignatureRestriction(this, indexes)
				);
			} // func BindGetIndex

			#endregion

			#region -- BindInvoke -----------------------------------------------------------

			public override DynamicMetaObject BindInvoke(InvokeBinder binder, DynamicMetaObject[] args)
			{
				var val = (LuaOverloadedMethod)Value;

				// find the method
				var mi = LuaEmit.FindMember<MethodInfo, DynamicMetaObject>(
					val.methods,
					binder.CallInfo,
					args,
					mo => mo.LimitType,
					val.Instance != null
				);

				if (mi == null)
				{
					var fallback = binder.FallbackInvoke(this, args,
						new DynamicMetaObject(
							Lua.ThrowExpression(String.Format(Properties.Resources.rsMemberNotResolved, val.Type, val.Name)),
							LuaMethod.BindInvokeRestrictions(Expression, val).Merge(Lua.GetMethodSignatureRestriction(null, args))
						)
					);

					return fallback;
				}
				else
				{
					return LuaMethod.BindInvoke(Lua.GetRuntime(binder), Expression, val, mi, binder.CallInfo, args, binder.ReturnType);
				}
			} // proc BindInvoke

			#endregion

			#region -- BindConvert ----------------------------------------------------------

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
						var miTarget = LuaEmit.FindMethod(val.methods, new CallInfo(parameterInfo.Length), null, parameterInfo, p => p.ParameterType, false);
						return LuaMethod.CreateDelegate(Expression, val, binder.Type, miTarget, binder.ReturnType);
					}
				}
				else if (typeof(Type).GetTypeInfo().IsAssignableFrom(binder.Type.GetTypeInfo()))
					return LuaMethod.ConvertToType(Expression, binder.ReturnType);
				else
					return base.BindConvert(binder);
			} // func BindConvert

			#endregion
		} // class LuaOverloadedMethodMetaObject

		#endregion

		private readonly object instance;
		private readonly bool isMemberCall;
		private readonly MethodInfo[] methods;

		#region -- Ctor/Dtor --------------------------------------------------------------

		internal LuaOverloadedMethod(object instance, MethodInfo[] methods, bool isMemberCall = false)
		{
			this.instance = instance;
			this.methods = methods;
			this.isMemberCall = isMemberCall;

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

		private MethodInfo FindMethod(bool exactMatchesOnly, CallInfo callInfo, params Type[] types)
		{
			if (exactMatchesOnly)
			{
				if (callInfo.ArgumentNames.Count > 0)
					throw new ArgumentException("Named parameters not allowed,");

				for (var i = 0; i < methods.Length; i++)
				{
					var parameterInfo = methods[i].GetParameters();
					if (parameterInfo.Length == types.Length)
					{
						var match = true;
						for (var j = 0; j < parameterInfo.Length; j++)
							if (!parameterInfo[j].ParameterType.GetTypeInfo().IsAssignableFrom(types[j].GetTypeInfo()))
							{
								match = false;
								break;
							}
						if (match)
							return methods[i];
					}
				}

				return null;
			}
			else
				return LuaEmit.FindMember<MethodInfo, Type>(methods, callInfo, types, t => t, true);
		} // func FindMethod

		/// <summary>Finds the delegate from the signature.</summary>
		/// <param name="exactMatchesOnly"><c>true </c>type must match exact. <c>false</c>, the types only should assignable.</param>
		/// <param name="types">Types</param>
		/// <returns></returns>
		public Delegate GetDelegate(bool exactMatchesOnly, params Type[] types)
		{
			var mi = FindMethod(exactMatchesOnly, new CallInfo(types.Length), types);
			return mi == null ? null : Parser.CreateDelegate(instance, mi);
		} // func GetDelegate

		/// <summary>Finds the delegate from the signature.</summary>
		/// <param name="callInfo"></param>
		/// <param name="types"></param>
		/// <returns></returns>
		public Delegate GetDelegate(CallInfo callInfo, params Type[] types)
		{
			var mi = FindMethod(false, callInfo, types);
			return mi == null ? null : Parser.CreateDelegate(instance, mi);
		} // func GetDelegate


		/// <summary>Gets the delegate from the index</summary>
		/// <param name="index">Index</param>
		/// <returns></returns>
		public Delegate GetDelegate(int index)
		{
			return index >= 0 && index < methods.Length ? Parser.CreateDelegate(instance, methods[index]) : null;
		} // func GetDelegate

		/// <summary>Finds the method from the signature</summary>
		/// <param name="exactMatchesOnly"><c>true </c>type must match exact. <c>false</c>, the types only should assignable.</param>
		/// <param name="types"></param>
		/// <returns></returns>
		public LuaMethod GetMethod(bool exactMatchesOnly, params Type[] types)
		{
			var mi = FindMethod(exactMatchesOnly, new CallInfo(types.Length), types);
			return mi == null ? null : new LuaMethod(instance, mi, false);
		} // func GetMethod

		/// <summary>Finds the method from the signature.</summary>
		/// <param name="callInfo"></param>
		/// <param name="types"></param>
		/// <returns></returns>
		public LuaMethod GetMethod(CallInfo callInfo, params Type[] types)
		{
			var mi = FindMethod(false, callInfo, types);
			return mi == null ? null : new LuaMethod(instance, mi, false);
		} // func GetMethod

		/// <summary>Gets the method from the index</summary>
		/// <param name="index">Index</param>
		/// <returns></returns>
		public LuaMethod GetMethod(int index)
			=> index >= 0 && index < methods.Length ? new LuaMethod(instance, methods[index], false) : null;

		#endregion

		/// <summary></summary>
		/// <returns></returns>
		public IEnumerator<Delegate> GetEnumerator()
			=> (from c in methods select Parser.CreateDelegate(instance, c)).GetEnumerator();

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
			=> GetEnumerator();

		/// <summary></summary>
		/// <returns></returns>
		public override string ToString()
			=> methods[0].DeclaringType.Name + "." + methods[0].Name + " overloaded";

		/// <summary></summary>
		/// <param name="index"></param>
		/// <returns></returns>
		public LuaMethod this[int index] => GetMethod(index);
		/// <summary></summary>
		/// <param name="types"></param>
		/// <returns></returns>
		public LuaMethod this[params Type[] types] => GetMethod(true, types);

		/// <summary>Name of the member.</summary>
		public string Name => methods[0].Name;
		/// <summary>Type that is the owner of the member list</summary>
		public Type Type => methods[0].DeclaringType;
		/// <summary>Instance, that belongs to the member.</summary>
		public object Instance => instance;
		/// <summary>Self parameter</summary>
		public bool IsMemberCall => isMemberCall;
		/// <summary>Count of overloade members.</summary>
		public int Count => methods.Length;
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
				var value = (LuaEvent)Value;
				return LuaMethod.BindInvoke(Lua.GetRuntime(binder), Expression, value, value.eventInfo.AddMethod, new CallInfo(args.Length), args, binder.ReturnType);
			} // func BindAddMethod

			private DynamicMetaObject BindRemoveMethod(DynamicMetaObjectBinder binder, DynamicMetaObject[] args)
			{
				var value = (LuaEvent)Value;
				return LuaMethod.BindInvoke(Lua.GetRuntime(binder), Expression, value, value.eventInfo.RemoveMethod, new CallInfo(args.Length), args, binder.ReturnType);
			} // func BindRemoveMethod

			private DynamicMetaObject BindGetMember(DynamicMetaObjectBinder binder, PropertyInfo piMethodGet)
			{
				var value = (LuaEvent)Value;
				return new DynamicMetaObject(
					Lua.EnsureType(
						Expression.New(Lua.MethodConstructorInfo,
							Expression.Property(Lua.EnsureType(Expression, typeof(ILuaMethod)), Lua.MethodInstancePropertyInfo),
							Expression.Property(Lua.EnsureType(Expression, typeof(LuaEvent)), piMethodGet),
							Expression.Constant(false)
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
			=> "event: " + eventInfo.Name;

		/// <summary>Name of the event.</summary>
		public string Name { get { return eventInfo.Name; } }
		/// <summary>Type that is the owner of the member list</summary>
		public Type Type { get { return eventInfo.DeclaringType; } }
		/// <summary>Instance, that belongs to the member.</summary>
		public object Instance { get { return instance; } }

		bool ILuaMethod.IsMemberCall => false;

		internal MethodInfo AddMethodInfo { get { return eventInfo.AddMethod; } }
		internal MethodInfo RemoveMethodInfo { get { return eventInfo.RemoveMethod; } }
		internal MethodInfo RaiseMethodInfo { get { return eventInfo.RaiseMethod; } }
	} // class LuaEvent

	#endregion
}