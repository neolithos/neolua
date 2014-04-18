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
      #region -- Ctor/Dtor ------------------------------------------------------------

      public LuaTypeMetaObject(Expression expression, LuaType value)
        : base(expression, BindingRestrictions.Empty, value)
      {
      } // ctor

      #endregion

      #region -- BindGetMember --------------------------------------------------------

      public override DynamicMetaObject BindGetMember(GetMemberBinder binder)
      {
        LuaType val = ((LuaType)Value);
        Type type = val.Type;
        Expression expr;

        if (type != null) // we have a type, bind to the member
        {
          try
          {
            expr = Lua.EnsureType(LuaEmit.GetMember(Lua.GetRuntime(binder), null, type, binder.Name, binder.IgnoreCase, false), binder.ReturnType);
          }
          catch (LuaEmitException e)
          {
            expr = Lua.ThrowExpression(e.Message, binder.ReturnType);
          }
          return new DynamicMetaObject(expr, GetTypeResolvedRestriction(type));
        }
        else
        {
          // Get the index for the access, as long is there no type behind
          expr = Expression.Condition(
            GetUpdateCondition(),
            binder.GetUpdateExpression(binder.ReturnType),
            Lua.EnsureType(Expression.Call(Lua.TypeGetTypeMethodInfoArgIndex, Expression.Constant(val.GetIndex(binder.Name, binder.IgnoreCase, null), typeof(int))), binder.ReturnType)
          );
          return new DynamicMetaObject(expr, GetTypeNotResolvedRestriction());
        }
      } // func BindGetMember

      #endregion

      #region -- BindSetMember --------------------------------------------------------

      public override DynamicMetaObject BindSetMember(SetMemberBinder binder, DynamicMetaObject value)
      {
        Type type = ((LuaType)Value).Type;

        Expression expr;
        if (type != null)
        {
          try
          {
            expr = Lua.EnsureType(LuaEmit.SetMember(Lua.GetRuntime(binder), null, type, binder.Name, binder.IgnoreCase, value.Expression, value.LimitType, false), binder.ReturnType);
          }
          catch (LuaEmitException e)
          {
            expr = Lua.ThrowExpression(e.Message, binder.ReturnType);
          }
          return new DynamicMetaObject(expr, GetTypeResolvedRestriction(type));
        }
        else
        {
          expr = Expression.Condition(
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
            Expression.Call(Expression.Convert(Expression, typeof(LuaType)), Lua.TypeGetGenericItemMethodInfo,
              Expression.Constant(typeGeneric),
              Expression.NewArrayInit(typeof(LuaType), (from a in indexes select ConvertToLuaType(a)).AsEnumerable())
            ),
            BindingRestrictions.GetTypeRestriction(Expression, typeof(LuaType))
              .Merge(Lua.GetMethodSignatureRestriction(null, indexes))
          );
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
            MethodInfo mi = LuaEmit.FindMethod((MethodInfo[])type.GetMember(binder.Name, MemberTypes.Method, Lua.GetBindingFlags(false, binder.IgnoreCase)), args, mo => mo.LimitType);
            if (mi == null)
            {
              if (args.Length == 0 && String.Compare(binder.Name, "GetType", binder.IgnoreCase) == 0)
              {
                restrictions = BindingRestrictions.GetInstanceRestriction(Expression, Value);
                expr = Lua.EnsureType(Expression.Property(Lua.EnsureType(Expression, typeof(LuaType)), Lua.TypeTypePropertyInfo), binder.ReturnType);
              }
              else if (String.Compare(binder.Name, "ctor", binder.IgnoreCase) == 0)
              {
                return BindNewObject(type, args, binder.ReturnType);
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
                args,
                mo => mo.Expression, mo => mo.LimitType, false), binder.ReturnType, true);
            }
          }
          catch (LuaEmitException e)
          {
            restrictions = BindingRestrictions.GetInstanceRestriction(Expression, Value);
            expr = Lua.ThrowExpression(e.Message, binder.ReturnType);
          }
          return new DynamicMetaObject(expr, GetTypeResolvedRestriction(type));
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
          return BindNewObject(type, args, binder.ReturnType);
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

      private DynamicMetaObject BindNewObject(Type typeNew, DynamicMetaObject[] args, Type returnType)
      {
        Expression expr;
        try
        {
          ConstructorInfo ci = LuaEmit.FindMember(typeNew.GetConstructors(BindingFlags.Public | BindingFlags.CreateInstance | BindingFlags.Instance), args, mo => mo.LimitType);
          expr = Lua.EnsureType(
            LuaEmit.BindParameter(null,
              a => Expression.New(ci, a),
              ci.GetParameters(),
              args,
              mo => mo.Expression, mo => mo.LimitType, false),
            returnType, true
          );
        }
        catch (LuaEmitException e)
        {
          expr = Lua.ThrowExpression(e.Message, returnType);
        }
        return new DynamicMetaObject(expr, GetTypeResolvedRestriction(typeNew));
      } // func BindNewObject

      private BindingRestrictions GetTypeNotResolvedRestriction()
      {
        return BindingRestrictions.GetExpressionRestriction(
          Expression.AndAlso(
            Expression.TypeEqual(Expression, typeof(LuaType)),
            Expression.Equal(
              Expression.Property(Lua.EnsureType(Expression, typeof(LuaType)), Lua.TypeTypePropertyInfo),
              Expression.Default(typeof(Type))
            )
          )
        );
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
        return Expression.Convert(a.Expression, typeof(LuaType));
      else if (typeof(Type).IsAssignableFrom(a.LimitType))
        return Expression.Convert(Expression.Call(Lua.TypeGetTypeMethodInfoArgType, Expression.Convert(a.Expression, typeof(Type))), typeof(object));
      else
        throw new ArgumentException();
    } // func ConvertToLuaType

    internal static Expression ConvertToType(DynamicMetaObject a)
    {
      if (a.LimitType == typeof(LuaType))
        return Expression.Convert(Expression.Property(Expression.Convert(a.Expression, typeof(LuaType)), Lua.TypeTypePropertyInfo), typeof(Type));
      else if (typeof(Type).IsAssignableFrom(a.LimitType))
        return Expression.Convert(a.Expression, typeof(Type));
      else
        throw new ArgumentException();
    } // func ConvertToLuaType

    /// <summary>Root for all clr-types.</summary>
    public static LuaType Clr { get { return clr; } }
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
        return LuaMethod.BindInvoke(Lua.GetRuntime(binder), Expression, val, val.method, args, binder.ReturnType);
      } // proc BindInvoke

      public override DynamicMetaObject BindConvert(ConvertBinder binder)
      {
        if (typeof(Delegate).IsAssignableFrom(binder.Type)) // do we expect a delegate
        {
          LuaMethod val = (LuaMethod)Value;
          return CreateDelegate(Expression, val, binder.Type, val.method, binder.ReturnType); 
        }
        else if (typeof(MethodInfo).IsAssignableFrom(binder.Type))
        {
          return new DynamicMetaObject(
            Expression.Property(Lua.EnsureType(Expression, typeof(LuaMethod)), Lua.MethodMethodPropertyInfo),
            BindingRestrictions.GetTypeRestriction(Expression, typeof(LuaMethod))
          );
        }
        else if (typeof(Type).IsAssignableFrom(binder.Type))
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

    /// <summary>Creates a delegate from the method</summary>
    /// <param name="typeDelegate"></param>
    /// <returns></returns>
    public Delegate CreateDelegate(Type typeDelegate)
    {
      return Delegate.CreateDelegate(typeDelegate, instance, method);
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

    internal static DynamicMetaObject BindInvoke(Lua runtime, Expression methodExpression, ILuaMethod methodValue, MethodInfo mi, DynamicMetaObject[] args, Type typeReturn)
    {
      // create the call expression
      Expression expr = Lua.EnsureType(LuaEmit.BindParameter(runtime,
        a => Expression.Call(GetInstance(methodExpression, methodValue, methodValue.Type), mi, a),
        mi.GetParameters(),
        args,
        mo => mo.Expression, mo => mo.LimitType, false), typeReturn, true);

      return new DynamicMetaObject(expr, BindInvokeRestrictions(methodExpression, methodValue).Merge(Lua.GetMethodSignatureRestriction(null, args)));
    } // func BindInvoke

    private static Expression GetInstance(Expression methodExpression, ILuaMethod methodValue, Type returnType)
    {
      return methodValue.Instance == null ?
        null :
        Lua.EnsureType(Expression.Property(Lua.EnsureType(methodExpression, typeof(ILuaMethod)), Lua.MethodInstancePropertyInfo), returnType);
    } //func GetInstance

    internal static DynamicMetaObject CreateDelegate(Expression methodExpression, ILuaMethod methodValue, Type typeDelegate, MethodInfo miTarget, Type typeReturn)
    {
      if (typeDelegate.BaseType != typeof(MulticastDelegate))
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
          Expression.Call(Lua.CreateDelegateMethodInfo,
            Expression.Constant(typeDelegate),
            GetInstance(methodExpression, methodValue, typeof(object)) ?? Expression.Default(typeof(object)),
            Expression.Constant(miTarget)
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
            Expression.TypeIs(methodExpression, typeof(ILuaMethod)),
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
        if (indexes.Any(c => c.LimitType != typeof(LuaType) && !typeof(Type).IsAssignableFrom(c.LimitType)))
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
        MethodInfo mi = LuaEmit.FindMethod(val.methods, args, mo => mo.LimitType);
        if (mi == null)
          return new DynamicMetaObject(
            Lua.ThrowExpression(String.Format(Properties.Resources.rsMemberNotResolved, val.Type, val.Name)),
            LuaMethod.BindInvokeRestrictions(Expression, val).Merge(Lua.GetMethodSignatureRestriction(null, args))
          );
        else
          return LuaMethod.BindInvoke(Lua.GetRuntime(binder), Expression, val, mi, args, binder.ReturnType);
      } // proc BindInvoke

      public override DynamicMetaObject BindConvert(ConvertBinder binder)
      {
        if (typeof(Delegate).IsAssignableFrom(binder.Type))
        {
          // get the parameters from the invoke method
          MethodInfo miInvoke = binder.Type.GetMethod("Invoke", BindingFlags.Public | BindingFlags.Instance | BindingFlags.InvokeMethod);
          if (miInvoke == null)
            return base.BindConvert(binder);
          else
          {
            LuaOverloadedMethod val = (LuaOverloadedMethod)Value;
            MethodInfo miTarget = LuaEmit.FindMethod(val.methods, miInvoke.GetParameters(), p => p.ParameterType);
            return LuaMethod.CreateDelegate(Expression, val, binder.Type, miTarget, binder.ReturnType);
          }
        }
        else if (typeof(Type).IsAssignableFrom(binder.Type))
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
        return LuaMethod.BindInvoke(Lua.GetRuntime(binder), Expression, value, value.eventInfo.GetAddMethod(), args, binder.ReturnType);
      } // func BindAddMethod

      private DynamicMetaObject BindRemoveMethod(DynamicMetaObjectBinder binder, DynamicMetaObject[] args)
      {
        LuaEvent value = (LuaEvent)Value;
        return LuaMethod.BindInvoke(Lua.GetRuntime(binder), Expression, value, value.eventInfo.GetRemoveMethod(), args, binder.ReturnType);
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
        if (String.Compare(binder.Name, csAdd, binder.IgnoreCase) == 0)
        {
          return BindGetMember(binder, Lua.AddMethodInfoPropertyInfo);
        }
        else if (String.Compare(binder.Name, csDel, binder.IgnoreCase) == 0 ||
          String.Compare(binder.Name, csRemove, binder.IgnoreCase) == 0)
        {
          return BindGetMember(binder, Lua.RemoveMethodInfoPropertyInfo);
        }
        else
          return base.BindGetMember(binder);
      } // func BindGetMember

      public override DynamicMetaObject BindInvokeMember(InvokeMemberBinder binder, DynamicMetaObject[] args)
      {
        if (String.Compare(binder.Name, csAdd, binder.IgnoreCase) == 0)
        {
          return BindAddMethod(binder, args);
        }
        else if (String.Compare(binder.Name, csDel, binder.IgnoreCase) == 0 ||
          String.Compare(binder.Name, csRemove, binder.IgnoreCase) == 0)
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

    /// <summary>Name of the event.</summary>
    public string Name { get { return eventInfo.Name; } }
    /// <summary>Type that is the owner of the member list</summary>
    public Type Type { get { return eventInfo.DeclaringType; } }
    /// <summary>Instance, that belongs to the member.</summary>
    public object Instance { get { return instance; } }

    internal MethodInfo AddMethodInfo { get { return eventInfo.GetAddMethod(); } }
    internal MethodInfo RemoveMethodInfo { get { return eventInfo.GetRemoveMethod(); } }
    internal MethodInfo RaiseMethodInfo { get { return eventInfo.GetRaiseMethod(); } }
  } // class LuaEvent

  #endregion
}
