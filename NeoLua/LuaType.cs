using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace Neo.IronLua
{
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
      public LuaTypeMetaObject(Expression expression, LuaType value)
        : base(expression, BindingRestrictions.Empty, value)
      {
      } // ctor

      public override DynamicMetaObject BindGetMember(GetMemberBinder binder)
      {
        LuaType val = (LuaType)Value;

        Expression expr = null;

        Type type = val.Type;
        if (type != null) // we have a type, bind to the member
        {
          switch (Lua.TryBindGetMember(binder, new DynamicMetaObject(Expression.Default(type), BindingRestrictions.Empty, null), out expr))
          {
            case Lua.BindResult.Ok:
              expr = Parser.ToTypeExpression(expr, binder.ReturnType);
              break;
          }
        }
        else
        {
          // Get the index for the access, as long is there no type behind
          expr = Expression.Condition(
            Expression.NotEqual(
              Expression.Property(Expression.Convert(Expression, typeof(LuaType)), piType),
              Expression.Constant(null, typeof(Type))
            ),
            binder.GetUpdateExpression(typeof(object)),
            Expression.Convert(Expression.Call(LuaType.miGetTypeFromIndex, Expression.Constant(val.GetIndex(binder.Name, binder.IgnoreCase, null), typeof(int))), typeof(object))
          );
        }
        return new DynamicMetaObject(expr, BindingRestrictions.GetInstanceRestriction(Expression, val));
      } // func BindGetMember

      public override DynamicMetaObject BindGetIndex(GetIndexBinder binder, DynamicMetaObject[] indexes)
      {
        LuaType val = (LuaType)Value;

        if (indexes.Length == 0)
        {
          // create a array of the type
          return new DynamicMetaObject(
            Expression.Call(LuaType.miGetTypeFromIndex, Expression.Constant(val.GetIndex("[]", true, () => val.Type.MakeArrayType()), typeof(int))),
            BindingRestrictions.GetInstanceRestriction(Expression, Value));
        }
        else
        {
          if (indexes.Any(c => !c.HasValue))
            return binder.Defer(indexes);

          // create the generic type name
          StringBuilder sbTypeName = new StringBuilder();
          val.GetFullName(sbTypeName);
          sbTypeName.Append('`').Append(indexes.Length);

          // find the type
          Type typeGeneric = Type.GetType(sbTypeName.ToString(), false);
          if (typeGeneric == null)
            return new DynamicMetaObject(
              Lua.ThrowExpression(String.Format(Properties.Resources.rsParseUnknownType, sbTypeName.ToString())),
              Lua.GetMethodSignatureRestriction(this, indexes)
              );

          // check, only types are allowed
          if (indexes.Any(c => c.LimitType != typeof(LuaType) && c.LimitType != typeof(Type)))
          {
            return new DynamicMetaObject(
             Lua.ThrowExpression(Properties.Resources.rsClrGenericTypeExpected),
             Lua.GetMethodSignatureRestriction(this, indexes));
          }

          // create the call to the runtime
          return new DynamicMetaObject(
            Expression.Call(Expression.Convert(Expression, typeof(LuaType)), miGetGenericItem,
            Expression.Constant(typeGeneric),
            Expression.NewArrayInit(typeof(LuaType), (from a in indexes select ConvertToLuaType(a)).AsEnumerable())),
            Lua.GetMethodSignatureRestriction(this, indexes));
        }
      } // func BindGetIndex

      public override DynamicMetaObject BindInvokeMember(InvokeMemberBinder binder, DynamicMetaObject[] args)
      {
        Type type = ((LuaType)Value).Type;
        Expression expr;
        if (type != null)
        {
          if (String.Compare(binder.Name, "GetType", binder.IgnoreCase) == 0 && args.Length == 0)
          {
            return new DynamicMetaObject(
              Parser.ToTypeExpression(
                Expression.Property(Expression.Convert(Expression, typeof(LuaType)), piType), binder.ReturnType),
                BindingRestrictions.GetInstanceRestriction(Expression, Value), type);
          }
          else
          {
            bool lUseCtor = String.Compare(binder.Name, "ctor", binder.IgnoreCase) == 0; // Redirect to the ctor
            switch (Lua.TryBindInvokeMember(lUseCtor ? null : binder, new DynamicMetaObject(Expression.Default(type), BindingRestrictions.Empty, null), args, out expr))
            {
              case Lua.BindResult.Ok:
                return new DynamicMetaObject(expr, Lua.GetMethodSignatureRestriction(null, args).Merge(BindingRestrictions.GetInstanceRestriction(Expression, Value)));
              case Lua.BindResult.MemberNotFound:
                return binder.FallbackInvokeMember(new DynamicMetaObject(Expression.Default(type), BindingRestrictions.Empty, null), args);
              default:
                return new DynamicMetaObject(expr, Lua.GetMethodSignatureRestriction(null, args).Merge(BindingRestrictions.GetInstanceRestriction(Expression, Value)));
            }
          }
        }
        return base.BindInvokeMember(binder, args);
      } // func BindInvokeMember

      public override DynamicMetaObject BindInvoke(InvokeBinder binder, DynamicMetaObject[] args)
      {
        Type type = ((LuaType)Value).Type;
        Expression expr;

        if (type != null)
        {
          switch (Lua.TryBindInvokeMember(null, new DynamicMetaObject(Expression.Default(type), BindingRestrictions.Empty, null), args, out expr))
          {
            case Lua.BindResult.Ok:
              return new DynamicMetaObject(expr, Lua.GetMethodSignatureRestriction(null, args).Merge(BindingRestrictions.GetInstanceRestriction(Expression, Value)));
            case Lua.BindResult.MemberNotFound:
              return binder.FallbackInvoke(new DynamicMetaObject(Expression.Default(type), BindingRestrictions.Empty, null), args);
            default:
              return new DynamicMetaObject(expr, Lua.GetMethodSignatureRestriction(null, args).Merge(BindingRestrictions.GetInstanceRestriction(Expression, Value)));
          }
        }

        return base.BindInvoke(binder, args);
      } // func BindInvoke

      public override IEnumerable<string> GetDynamicMemberNames()
      {
        return ((LuaType)Value).index.Keys;
      } // proc GetDynamicMemberNames
    } // class LuaTypeMetaObject

    #endregion

    private LuaType parent;           // Access to parent type or namespace
    private Type type;                // Type that is represented, null if it is not resolved until now

    private string sName;             // Name of the unresolved type or namespace
    private int iAssemblyCount;       // Number of loaded assemblies or -1 if the type is resolved as a namespace

    private Dictionary<string, int> index = null; // Index to speed up the search in big namespaces

    #region -- Ctor/GetMetaObject -----------------------------------------------------

    private LuaType()
    {
      this.parent = null;
      this.type = null;
      this.sName = null;
      this.iAssemblyCount = -2;
    } // ctor

    private LuaType(LuaType parent, string sName, Type type)
    {
      if (type == null)
        throw new ArgumentNullException();

      this.parent = parent;
      this.type = type;
      this.sName = sName;
      this.iAssemblyCount = 0;
    } // ctor

    private LuaType(LuaType parent, string sName)
    {
      if (String.IsNullOrEmpty(sName))
        throw new ArgumentNullException();

      this.parent = parent;
      this.type = null;
      this.sName = sName;
      this.iAssemblyCount = 0;
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
          iAssemblyCount >= 0) // Namespace, there is no type7
      {
        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
        string sTypeName = FullName;

        // new assembly loaded?
        if (assemblies.Length != iAssemblyCount)
        {
          for (int i = iAssemblyCount; i < assemblies.Length; i++)
          {
            if ((type = assemblies[i].GetType(sTypeName, false)) != null)
            {
              lock (knownTypes)
                knownTypes[type.FullName] = LuaType.GetTypeIndex(this); // update type cache
              break;
            }
          }
          iAssemblyCount = assemblies.Length;
        }
      }
    } // func GetItemType

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
      if (iAssemblyCount >= 0 && GetType(iIndex).Type != null && Type == null)
      {
        LuaType c = this;
        while (c.parent != null && c.iAssemblyCount >= 0)
        {
          c.iAssemblyCount = -1;
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
            foreach (var k in index)
            {
              if (String.Compare(sName, k.Key, lIgnoreCase) == 0)
              {
                iIndex = k.Value;
                break;
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
    public string Name { get { return sName; } }
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
    public bool IsNamespace { get { return iAssemblyCount == -1 || type == null && iAssemblyCount >= 0; } }

    #endregion

    // -- Static --------------------------------------------------------------

    private static LuaType clr = new LuaType();                 // root type
    private static List<LuaType> types = new List<LuaType>();   // SubItems of this type
    private static Dictionary<string, int> knownTypes = new Dictionary<string, int>(); // index for well known types

    private static MethodInfo miGetTypeFromIndex; // access to the GetItem of the LuaType
    private static MethodInfo miGetGenericItem;   // access to the GetGenericItem of the LuaType
    internal static MethodInfo miGetTypeFromType; // access to GetType(Type);
    private static PropertyInfo piType;           // access to the type property

    #region -- sctor ------------------------------------------------------------------

    static LuaType()
    {
      miGetGenericItem = typeof(LuaType).GetMethod("GetGenericItem", BindingFlags.Public | BindingFlags.Instance | BindingFlags.InvokeMethod);
      miGetTypeFromIndex = typeof(LuaType).GetMethod("GetType", BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.InvokeMethod | BindingFlags.DeclaredOnly, null, new Type[] { typeof(int) }, null);
      miGetTypeFromType = typeof(LuaType).GetMethod("GetType", BindingFlags.Public | BindingFlags.Static | BindingFlags.InvokeMethod | BindingFlags.DeclaredOnly, null, new Type[] { typeof(Type) }, null);
      piType = typeof(LuaType).GetProperty("Type", BindingFlags.Public | BindingFlags.Instance | BindingFlags.GetProperty | BindingFlags.DeclaredOnly);
#if DEBUG
      if (miGetTypeFromIndex == null || piType == null || miGetGenericItem == null || miGetTypeFromType == null)
        throw new ArgumentNullException();
#endif
    } // ctor

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
      LuaType luaType = null;

      // search the tyle in the cache
      lock (knownTypes)
      {
        int iIndex;
        if (knownTypes.TryGetValue(sTypeName, out iIndex))
          luaType = GetType(iIndex);
      }

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

    #endregion

    internal static Expression ConvertToLuaType(DynamicMetaObject a)
    {
      if (a.LimitType == typeof(LuaType))
        return Parser.ToTypeExpression(a.Expression, typeof(LuaType));
      else if (typeof(Type).IsAssignableFrom(a.LimitType))
        return Parser.ToTypeExpression(Expression.Call(miGetTypeFromType, Parser.ToTypeExpression(a.Expression, typeof(Type))));
      else
        throw new ArgumentException();
    } // func ConvertToLuaType

    internal static Expression ConvertToType(DynamicMetaObject a)
    {
      if (a.LimitType == typeof(LuaType))
        return Parser.ToTypeExpression(Expression.Property(Parser.ToTypeExpression(a.Expression, typeof(LuaType)), piType), typeof(Type));
      else if (typeof(Type).IsAssignableFrom(a.LimitType))
        return Parser.ToTypeExpression(a.Expression, typeof(Type));
      else
        throw new ArgumentException();
    } // func ConvertToLuaType

    /// <summary>Root for all clr-types.</summary>
    public static LuaType Clr { get { return clr; } }
  } // class LuaType

  #endregion

  #region -- class LuaOverloadedMethod ------------------------------------------------

  ///////////////////////////////////////////////////////////////////////////////
  /// <summary>Represents overloaded members.</summary>
  public sealed class LuaOverloadedMethod : IDynamicMetaObjectProvider, IEnumerable<Delegate>
  {
    #region -- class LuaOverloadedMethodMetaObject ------------------------------------

    ///////////////////////////////////////////////////////////////////////////////
    /// <summary></summary>
    private class LuaOverloadedMethodMetaObject : DynamicMetaObject
    {
      public LuaOverloadedMethodMetaObject(Expression expression, LuaOverloadedMethod value)
        : base(expression, BindingRestrictions.Empty, value)
      {
      } // ctor

      public override DynamicMetaObject BindGetIndex(GetIndexBinder binder, DynamicMetaObject[] indexes)
      {
        LuaOverloadedMethod val = (LuaOverloadedMethod)Value;

        if (indexes.Any(c => !c.HasValue))
          return binder.Defer(indexes);

        // Access the normal index
        if (indexes.Length == 1 && indexes[0].LimitType == typeof(int))
          return base.BindGetIndex(binder, indexes);

        // check, only types are allowed
        if (indexes.Any(c => c.LimitType != typeof(LuaType) && !typeof(Type).IsAssignableFrom(c.LimitType)))
        {
          return new DynamicMetaObject(
           Lua.ThrowExpression(String.Format(Properties.Resources.rsClrGenericTypeExpected)),
           Lua.GetMethodSignatureRestriction(this, indexes));
        }

        return new DynamicMetaObject(
          Expression.Call(Parser.ToTypeExpression(Expression, typeof(LuaOverloadedMethod)),
            miGetExplicitDelegate,
            Expression.Constant(false),
            Expression.NewArrayInit(typeof(Type), (from a in indexes select LuaType.ConvertToType(a)).AsEnumerable())
          ),
          val.instance == null ? GetTypeRestriction(val) : GetInstanceRestriction(val)
        );
      } // func BindGetIndex

      public override DynamicMetaObject BindInvoke(InvokeBinder binder, DynamicMetaObject[] args)
      {
        LuaOverloadedMethod val = (LuaOverloadedMethod)Value;
        MethodBase miBind = Lua.BindFindInvokeMember<MethodInfo>(val.methods, args);

        // Method resolved
        if (miBind == null)
        {
          return new DynamicMetaObject(
            Lua.ThrowExpression(String.Format(Properties.Resources.rsMemberNotResolved, val.Name)), GetTypeRestriction(val));
        }

        // check if we need to make an non-generic call
        if (miBind.ContainsGenericParameters)
          miBind = Lua.MakeNonGenericMethod((MethodInfo)miBind, args);

        // Create the expression with the arguments
        return new DynamicMetaObject(
          Lua.InvokeMemberExpression(
            val.instance == null ?
              null :
              new DynamicMetaObject(Expression.Property(Expression.Convert(Expression, typeof(LuaOverloadedMethod)), piInstance), BindingRestrictions.Empty, val.instance),
            miBind, null, args
          ),
          val.instance == null ? GetTypeRestriction(val) : GetInstanceRestriction(val)
        );
      } // proc BindInvoke

      private BindingRestrictions GetInstanceRestriction(LuaOverloadedMethod val)
      {
        return BindingRestrictions.GetExpressionRestriction(
          Expression.AndAlso(
            Expression.TypeIs(Expression, typeof(LuaOverloadedMethod)),
            Expression.Equal(
              Expression.Property(Expression.Convert(Expression, typeof(LuaOverloadedMethod)), piInstance), 
              Expression.Constant(val.instance)
            )
          )
        );
      } // func GetInstanceRestriction

      private BindingRestrictions GetTypeRestriction(LuaOverloadedMethod val)
      {
        return BindingRestrictions.GetExpressionRestriction(
          Expression.AndAlso(
            Expression.TypeIs(Expression, typeof(LuaOverloadedMethod)),
            Expression.Equal(
              Expression.Property(Expression.Convert(Expression, typeof(LuaOverloadedMethod)), piType),
              Expression.Constant(val.Type)
            )
          )
        );
      } // func GetTypeRestriction
    } // class LuaOverloadedMethodMetaObject

    #endregion

    private object instance;
    private MethodInfo[] methods;

    internal LuaOverloadedMethod(object instance, MethodInfo[] methods)
    {
      this.instance = instance;
      this.methods = methods;

      if (methods.Length == 0)
        throw new ArgumentException();
    } // ctor

    /// <summary></summary>
    /// <param name="parameter"></param>
    /// <returns></returns>
    public DynamicMetaObject GetMetaObject(Expression parameter)
    {
      return new LuaOverloadedMethodMetaObject(parameter, this);
    } // func GetMetaObject

    /// <summary>Finds the delegate.</summary>
    /// <param name="types">Types</param>
    /// <param name="lExact"><c>true </c>type must match exact. <c>false</c>, the types only should assignable.</param>
    /// <returns></returns>
    public Delegate GetDelegate(bool lExact, params Type[] types)
    {
      for (int i = 0; i < methods.Length; i++)
      {
        ParameterInfo[] parameters = methods[i].GetParameters();
        if (parameters.Length == types.Length)
        {
          bool lMatch = true;

          for (int j = 0; j < parameters.Length; j++)
          {
            if ((!lExact || types[j] != parameters[j].ParameterType) &&
               (lExact || !parameters[j].ParameterType.IsAssignableFrom(types[j])))
            {
              lMatch = false;
              break;
            }
          }

          if (lMatch)
            return Parser.CreateDelegate(instance, methods[i]);
        }
      }
      return null;
    } // func GetDelegate

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
    /// <param name="iIndex"></param>
    /// <returns></returns>
    public Delegate this[int iIndex] { get { return Parser.CreateDelegate(instance, methods[iIndex]); } }

    /// <summary>Name of the overloaded member.</summary>
    public string Name { get { return methods[0].Name; } }
    /// <summary>Type that is the owner of the member list</summary>
    public Type Type { get { return methods[0].DeclaringType; } }
    /// <summary>Instance, that belongs to the member.</summary>
    public object Instance { get { return instance; } }
    /// <summary>Count of overloade members.</summary>
    public int Count { get { return methods.Length; } }

    // -- Static --------------------------------------------------------------

    internal static ConstructorInfo ciCtor;
    private static PropertyInfo piType;
    private static PropertyInfo piInstance;
    private static MethodInfo miGetExplicitDelegate;

    static LuaOverloadedMethod()
    {
      ciCtor = typeof(LuaOverloadedMethod).GetConstructor(BindingFlags.NonPublic | BindingFlags.CreateInstance | BindingFlags.DeclaredOnly | BindingFlags.Instance, null, new Type[] { typeof(object), typeof(MethodInfo[]) }, null);
      piType = typeof(LuaOverloadedMethod).GetProperty("Type", BindingFlags.Public | BindingFlags.Instance | BindingFlags.GetProperty | BindingFlags.DeclaredOnly);
      piInstance = typeof(LuaOverloadedMethod).GetProperty("Instance", BindingFlags.Public | BindingFlags.Instance | BindingFlags.GetProperty | BindingFlags.DeclaredOnly);
      miGetExplicitDelegate = typeof(LuaOverloadedMethod).GetMethod("GetDelegate", BindingFlags.Public | BindingFlags.Instance | BindingFlags.GetProperty | BindingFlags.DeclaredOnly);
#if DEBUG
      if (ciCtor == null || piType == null || piInstance == null)
        throw new ArgumentNullException();
#endif
    } // sctor
  } // class LuaOverloadedMethod

  #endregion
}
