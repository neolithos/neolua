using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace Neo.IronLua
{
  ///////////////////////////////////////////////////////////////////////////////
  /// <summary></summary>
  public partial class Lua
  {
    #region -- class LuaCoreMetaObject ------------------------------------------------

    ///////////////////////////////////////////////////////////////////////////////
    /// <summary></summary>
    protected class LuaCoreMetaObject : LuaMetaObject
    {
      public LuaCoreMetaObject(Lua lua, Expression parameter)
        : base(lua, parameter)
      {
      } // ctor

      private bool TryGetLuaSystem(string sName, out Expression expr)
      {
        CoreFunction f;
        IDynamicMetaObjectProvider lib;
        if (TryGetLuaFunction(sName, out f))
        {
          expr = Expression.Constant(f.GetDelegate(Value), typeof(object));
          return true;
        }
        else if (TryGetSystemLibrary(sName, out lib))
        {
          expr = Expression.Constant(lib, typeof(object));
          return true;
        }
        else
        {
          expr = null;
          return false;
        }
      } // func TryGetLuaSystem

      public override DynamicMetaObject BindGetMember(GetMemberBinder binder)
      {
        // Access to clr can not overload
        if (binder.Name == "clr")
          return new DynamicMetaObject(Expression.Constant(Clr, typeof(LuaClrClassObject)), BindingRestrictions.GetInstanceRestriction(Expression, Value));

        // Bind the value
        DynamicMetaObject moGet = base.BindGetMember(binder);

        // Check for system function or library
        Expression expr;
        if (TryGetLuaSystem(binder.Name, out expr))
        {
          return new DynamicMetaObject(
            Expression.Coalesce(moGet.Expression, expr),
            moGet.Restrictions);
        }
        else
          return moGet;
      } // proc BindGetMember

      // -- Static ------------------------------------------------------------

      private static LuaClrClassObject clr = new LuaClrClassObject(null, String.Empty, null);

      public static IDynamicMetaObjectProvider Clr { get { return clr; } }
    } // class LuaCoreMetaObject

    #endregion

    #region -- class LuaClrClassObject ------------------------------------------------

    ///////////////////////////////////////////////////////////////////////////////
    /// <summary>Für jeden Namespace wird ein Objekt für den Zugriff aufgebaut.</summary>
    private class LuaClrClassObject : IDynamicMetaObjectProvider
    {
      #region -- class LuaClrClassMetaObject ------------------------------------------

      ///////////////////////////////////////////////////////////////////////////////
      /// <summary></summary>
      private class LuaClrClassMetaObject : DynamicMetaObject
      {
        public LuaClrClassMetaObject(Expression expression, LuaClrClassObject value)
          : base(expression, BindingRestrictions.Empty, value)
        {
        } // ctor

        public override DynamicMetaObject BindGetMember(GetMemberBinder binder)
        {
          LuaClrClassObject val = (LuaClrClassObject)Value;

          Expression expr = null;

          // Unterliegende Typ wird als erstes abgefragt
          Type type = val.GetItemType();
          if (type != null)
          {
            switch (TryBindGetMember(binder, new DynamicMetaObject(Expression.Default(type), BindingRestrictions.Empty, null), out expr))
            {
              case BindResult.MemberNotFound:
                expr = null;
                break;
              case BindResult.Ok:
                expr = Expression.Convert(expr, typeof(object));
                break;
            }
          }

          // Suche den Index für den Zugriff
          if (expr == null)
            expr = val.GetIndex(binder.Name, binder.IgnoreCase);

          return new DynamicMetaObject(expr, BindingRestrictions.GetInstanceRestriction(Expression, val));
        } // func BindGetMember

        public override DynamicMetaObject BindSetMember(SetMemberBinder binder, DynamicMetaObject value)
        {
          return base.BindSetMember(binder, value);
        }

        public override DynamicMetaObject BindInvokeMember(InvokeMemberBinder binder, DynamicMetaObject[] args)
        {
          Type type = ((LuaClrClassObject)Value).GetItemType();
          Expression expr;
          if (type != null)
          {
            bool lUseCtor = binder.Name == "ctor"; // Leite auf den ctor um

            switch (TryBindInvokeMember(binder, lUseCtor, new DynamicMetaObject(Expression.Default(type), BindingRestrictions.Empty, null), args, out expr))
            {
              case BindResult.Ok:
                return new DynamicMetaObject(expr, GetMethodSignatureRestriction(null, args).Merge(BindingRestrictions.GetInstanceRestriction(Expression, Value)));
              case BindResult.MemberNotFound:
                return binder.FallbackInvokeMember(new DynamicMetaObject(Expression.Default(type), BindingRestrictions.Empty, null), args);
              default:
                return new DynamicMetaObject(expr, GetMethodSignatureRestriction(null, args).Merge(BindingRestrictions.GetInstanceRestriction(Expression, Value)));
            }
          }
          return base.BindInvokeMember(binder, args);
        } // func BindInvokeMember

        public override IEnumerable<string> GetDynamicMemberNames()
        {
          LuaClrClassObject val = (LuaClrClassObject)Value;
          if (val.subItems != null)
            for (int i = 0; i < val.subItems.Count; i++)
              yield return val.subItems[i].Name;
        } // func GetDynamicMemberNames
      } // class LuaClrClassMetaObject

      #endregion

      private LuaClrClassObject parent;   // Zugriff auf den Untergeordneten Namensraum
      private string sName;               // Bezeichnung
      private MethodInfo miGetValue;      // Methode für den Zugriff auf das Array

      private Type type = null;                         // Type, der hinter dem Name liegt, falls vorhanden
      private int iAssemblyCount = 0;                   // Anzahl der Assembly, als zuletzt versucht wurde ein Typ zu finden, -1 für Namespace
      private List<LuaClrClassObject> subItems = null;  // Liste alle untergeordneten abgefragten Typen (Namespace, Classes, SubClasses)
      // Die Indices werden als Konstante in die Expression gegossen, damit darf sich der Index nie ändern.
      private Dictionary<string, int> index = null;     // Index für die schnelle Suche von Namespaces und Klassen, wird erst ab 10 Einträgen angelegt

      #region -- Ctor/Dtor ------------------------------------------------------------

      public LuaClrClassObject(LuaClrClassObject parent, string sName, MethodInfo mi)
      {
        this.parent = parent;
        this.sName = sName;
        this.miGetValue = mi;

        if (miGetValue == null)
        {
          Func<int, LuaClrClassObject> f = new Func<int, LuaClrClassObject>(GetItem);
          miGetValue = f.Method;
        }
      } // ctor

      public DynamicMetaObject GetMetaObject(Expression parameter)
      {
        return new LuaClrClassMetaObject(parameter, this);
      } // func GetMetaObject

      #endregion

      #region -- GetItemType ----------------------------------------------------------

      public Type GetItemType()
      {
        if (type == null &&  // Type noch nicht ermittelt?
            parent != null && // Wurzel hat nie einen Typ
            iAssemblyCount >= 0 && // Dieser Knoten wurde als Namespace identifiziert
            AppDomain.CurrentDomain.GetAssemblies().Length != iAssemblyCount) // Anzahl der Assemblies hat sich geändert
        {
          type = Lua.GetType(FullName);
        }
        return type;
      } // func GetItemType

      private void GetFullName(StringBuilder sb)
      {
        if (parent != null)
        {
          if (parent.parent == null)
            sb.Append(sName);
          else
          {
            parent.GetFullName(sb);
            if (parent.IsNamespace)
              sb.Append('.');
            else
              sb.Append('+');
            sb.Append(sName);
          }
        }
      } // proc GetFullName

      #endregion

      #region -- GetIndex, GetClass ---------------------------------------------------

      private Expression GetIndex(string sName, bool lIgnoreCase)
      {
        int iIndex;

        if (subItems == null)
          subItems = new List<LuaClrClassObject>();

        if (index != null) // Index wurde angelegt
        {
          if (!index.TryGetValue(sName, out iIndex))
          {
            if (lIgnoreCase)
              iIndex = FindIndexByName(sName, lIgnoreCase);
            else
              iIndex = -1;
          }
        }
        else // Noch kein Index also suchen wir im Array
          iIndex = FindIndexByName(sName, lIgnoreCase);

        // Wurde ein kein Eintrag gefunden, so legen wir ihn an
        if (iIndex == -1)
        {
          iIndex = subItems.Count; // Setze den Index
          // Erzeuge das neue Objekt
          subItems.Add(new LuaClrClassObject(this, sName, miGetValue));

          // Soll der Index angelegt/gepflegt werden
          if (iIndex >= 10)
          {
            if (index == null)
            {
              index = new Dictionary<string, int>();
              for (int i = 0; i < subItems.Count; i++)
                index.Add(subItems[i].Name, i);
            }
            else
              index.Add(sName, iIndex);
          }
        }

        if (iAssemblyCount == 0 && GetItemType() == null) // Kein Type ermittelt, es gibt aber SubItems, dann ist es ein Namespace
          iAssemblyCount = -1;

        // Erzeuge die Expression für den Zugriff
        return Expression.Call(Expression.Constant(this, typeof(LuaClrClassObject)), miGetValue, Expression.Constant(iIndex, typeof(int)));
      } // func GetNameSpaceIndex

      private int FindIndexByName(string sName, bool lIgnoreCase)
      {
        int iIndex = -1;
        for (int i = 0; i < subItems.Count; i++)
          if (String.Compare(subItems[i].Name, sName, lIgnoreCase) == 0)
          {
            iIndex = i;
            break;
          } 
        return iIndex;
      } // func FindIndexByName

      public LuaClrClassObject GetItem(int iIndex)
      {
        return subItems[iIndex];
      } // func GetNameSpace

      #endregion

      public string Name { get { return sName; } }
      public string FullName
      {
        get
        {
          StringBuilder sb = new StringBuilder();
          GetFullName(sb);
          return sb.ToString();
        }
      } // func FullName

      public bool IsNamespace { get { return iAssemblyCount == -1; } }
    } // class LuaClrClassObject

    #endregion

    #region -- class LuaPackageProxy --------------------------------------------------

    ///////////////////////////////////////////////////////////////////////////////
    /// <summary>Little proxy for static classes that provide Library for Lua</summary>
    private class LuaPackageProxy : IDynamicMetaObjectProvider
    {
      #region -- class LuaPackageMetaObject -------------------------------------------

      ///////////////////////////////////////////////////////////////////////////////
      /// <summary></summary>
      private class LuaPackageMetaObject : DynamicMetaObject
      {
        public LuaPackageMetaObject(Expression expression, LuaPackageProxy value)
          : base(expression, BindingRestrictions.Empty, value)
        {
        } // ctor

        public override DynamicMetaObject BindGetMember(GetMemberBinder binder)
        {
          LuaPackageProxy val = (LuaPackageProxy)Value;
          Expression expr = null;

          // Call try to bind the static methods
          switch (TryBindGetMember(binder, new DynamicMetaObject(Expression.Default(val.type), BindingRestrictions.Empty, null), out expr))
          {
            case BindResult.Ok:
              expr = Expression.Convert(expr, typeof(object));
              break;
          }

          return new DynamicMetaObject(expr, BindingRestrictions.GetInstanceRestriction(Expression, Value));
        } // func BindGetMember

        public override DynamicMetaObject BindInvokeMember(InvokeMemberBinder binder, DynamicMetaObject[] args)
        {
          Expression expr;
          TryBindInvokeMember(binder, false, new DynamicMetaObject(Expression.Default((Type)Value), BindingRestrictions.Empty, null), args, out expr);
          return new DynamicMetaObject(expr, GetMethodSignatureRestriction(null, args).Merge(BindingRestrictions.GetInstanceRestriction(Expression, Value)));
        } // func BindInvokeMember
      } // class LuaPackageMetaObject

      #endregion

      private Type type;

      public LuaPackageProxy(Type type)
      {
        this.type = type;
      } // ctor

      public DynamicMetaObject GetMetaObject(Expression parameter)
      {
        return new LuaPackageMetaObject(parameter, this);
      } // func GetMetaObject
    } // class LuaPackageProxy

    #endregion

    public override DynamicMetaObject GetMetaObject(Expression parameter)
    {
      return new LuaCoreMetaObject(this, parameter);
    } // func GetMetaObject

    #region -- Basic Functions --------------------------------------------------------

    private bool IsTrue(object value)
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

    private object LuaAssert(object value, string sMessage = null)
    {
      Debug.Assert(IsTrue(value), sMessage);
      return value;
    } // func LuaAssert

    private object[] LuaCollectgarbage(string opt, object arg = null)
    {
      switch (opt)
      {
        case "collect":
          GC.Collect();
          return LuaCollectgarbage("count");
        case "count":
          long iMem = GC.GetTotalMemory(false);
          return new object[] { iMem / 1024.0, iMem % 1024 };
        case "isrunning":
          return new object[] { true };
        default:
          return emptyResult;
      }
    } // func Lua_collectgarbage

    private object[] LuaDoFile(string sFileName)
    {
      return DoChunk(sFileName);
    } // func Lua_dofile

    private void LuaError(string sMessage, int level = 1)
    {
      // level ist der StackTrace
      throw new LuaException(sMessage, null);
    } // proc Lua_error

    // todo: getmetatable

    // todo: ipairs

    // todo: load

    // todo: loadfile

    // todo: next

    // todo: pairs

    // todo: pcall

    private void LuaPrint(params object[] args)
    {
      if (args == null)
        return;

      for (int i = 0; i < args.Length; i++)
        Debug.Write(args[i]);
      Debug.WriteLine(String.Empty);
    } // proc LuaPrint

    private bool LuaRawEqual(object a, object b)
    {
      if (a == null && b == null)
        return true;
      else if (a != null && b != null)
      {
        if (a.GetType() == b.GetType())
        {
          if (a.GetType().IsValueType)
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

    private object LuaRawGet(LuaTable t, object index)
    {
      return t[index];
    } // func LuaRawGet

    private int LuaRawLen(object v)
    {
      if (v == null)
        return 0;
      else
      {
        PropertyInfo pi = v.GetType().GetProperty("Length", BindingFlags.Instance | BindingFlags.GetProperty | BindingFlags.Public);
        if (pi == null)
          return 0;
        return (int)pi.GetValue(v, null);
      }
    } // func LuaRawLen

    private LuaTable LuaRawSet(LuaTable t, object index, object value)
    {
      t[index] = value;
      return t;
    } // func LuaRawSet

    private object[] LuaSelect(int index, params object[] values)
    {
      if (index < 0)
      {
        index = values.Length + index;
        if (index < 0)
          index = 0;
      }

      if (index < values.Length)
      {
        object[] r = new object[values.Length - index];
        Array.Copy(values, index, r, 0, r.Length);
        return r;
      }
      else
        return emptyResult;
    } // func LuaSelect

    // todo: setmetatable

    private object LuaToNumber(object v, int iBase = 10)
    {
      if (v == null)
        return null;
      else if (v is string)
        return Convert.ToInt32((string)v, iBase); // todo: Incompatible to lua reference
      else if (v is int || v is double)
        return v;
      else if (v is byte ||
        v is sbyte ||
        v is ushort ||
        v is short)
        return Convert.ToInt32(v);
      else if (v is uint ||
        v is long ||
        v is ulong ||
        v is decimal ||
        v is float)
        return Convert.ToDouble(v);
      else if (v is bool)
        return (bool)v ? 1 : 0;
      else
        return null;
    } // func LuaToNumber

    private string LuaToString(object v)
    {
      if (v == null)
        return null;
      else
        return v.ToString();
    } // func LuaToString

    private string LuaType(object v)
    {
      if (v == null)
        return "nil";
      else if (v is int || v is double)
        return "number";
      else if (v is string)
        return "string";
      else if (v is bool)
        return "bool";
      else if (v is LuaTable)
        return "table";
      else if (v is Delegate)
        return "function";
      else
        return "userdata";
    } // func LuaType

    // Todo: xpcall

    #endregion

    // -- Static --------------------------------------------------------------

    #region -- struct CoreFunction ----------------------------------------------------

    ///////////////////////////////////////////////////////////////////////////////
    /// <summary></summary>
    private struct CoreFunction
    {
      public Delegate GetDelegate(object self)
      {
        return Delegate.CreateDelegate(DelegateType, self, Method);
      } // func GetDelegate

      public MethodInfo Method;
      public Type DelegateType;
    } // struct CoreFunction

    #endregion

    private static object luaStaticLock = new object();

    private static Dictionary<string, IDynamicMetaObjectProvider> luaSystemLibraries = new Dictionary<string,IDynamicMetaObjectProvider>(); // Array with system libraries
    private static Dictionary<string, Type> knownTypes = null; // Known types of the current AppDomain
    private static Dictionary<string, CoreFunction> luaFunctions = new Dictionary<string, CoreFunction>(); // Core functions for the object
    
    /// <summary>Gets the system library.</summary>
    /// <param name="library">Library</param>
    /// <returns>dynamic object for the library</returns>
    private static bool TryGetSystemLibrary(string sLibraryName, out IDynamicMetaObjectProvider lib)
    {
      lock (luaStaticLock)
      {
        if(luaSystemLibraries.Count == 0)
        {
          foreach (Type t in typeof(Lua).GetNestedTypes(BindingFlags.NonPublic))
          {
            if (t.Name.StartsWith("LuaLibrary", StringComparison.OrdinalIgnoreCase))
            {
              string sName = t.Name.Substring(10).ToLower();
              luaSystemLibraries[sName] = new LuaPackageProxy(t);
            }
          }
        }
        return luaSystemLibraries.TryGetValue(sLibraryName, out lib);
      }
    } // func GetSystemLibrary

    /// <summary>Resolve typename to a type.</summary>
    /// <param name="sTypeName">Fullname of the type</param>
    /// <returns>The resolved type or <c>null</c>.</returns>
    internal static Type GetType(string sTypeName)
    {
      Type type = Type.GetType(sTypeName, false);
      if (type == null)
        lock (luaStaticLock)
        {
          // Lookup the type in the cache
          if (knownTypes != null && knownTypes.TryGetValue(sTypeName, out type))
            return type;

          // Lookup the type in all loaded assemblies
          var asms = AppDomain.CurrentDomain.GetAssemblies();
          for (int i = 0; i < asms.Length; i++)
          {
            if ((type = asms[i].GetType(sTypeName, false)) != null)
              break;
          }

          // Put the type in the cache
          if (type != null)
          {
            if (knownTypes == null)
              knownTypes = new Dictionary<string, Type>();
            knownTypes[sTypeName] = type;
          }
        }
      return type;
    } // func GetType

    private static bool TryGetLuaFunction(string sName, out CoreFunction function)
    {
      lock (luaStaticLock)
      {
        if (luaFunctions.Count == 0) // Collect all lua sys functions
        {
          foreach (var mi in typeof(Lua).GetMethods(BindingFlags.NonPublic | BindingFlags.Instance))
            if (mi.Name.StartsWith("lua", StringComparison.OrdinalIgnoreCase))
            {
              Type typeDelegate = Expression.GetDelegateType((from p in mi.GetParameters() select p.ParameterType).Concat(new Type[] { mi.ReturnType }).ToArray());
              luaFunctions[mi.Name.Substring(3).ToLower()] = new CoreFunction { Method = mi, DelegateType = typeDelegate };
            }
        }

        // Get the cached function
        if (luaFunctions.TryGetValue(sName, out function))
          return true;

        return false;
      }
    } // func TryGetLuaFunction
  } // class Lua
}
