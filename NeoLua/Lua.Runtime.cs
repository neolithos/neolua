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

      public override DynamicMetaObject BindGetMember(GetMemberBinder binder)
      {
        // Zugriff aud die Clr
        if (binder.Name == "clr")
          return new DynamicMetaObject(Expression.Constant(Clr, typeof(LuaClrClassObject)), BindingRestrictions.GetInstanceRestriction(Expression, Value));
        
        return base.BindGetMember(binder);
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
          type = Type.GetType(FullName, false);
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

          // Unterliegende Typ wird als erstes abgefragt
          switch (TryBindGetMember(binder, new DynamicMetaObject(Expression.Default(val.type), BindingRestrictions.Empty, null), out expr))
          {
            case BindResult.Ok:
              expr = Expression.Convert(expr, typeof(object));
              break;
          }

          var restrictions = BindingRestrictions.GetInstanceRestriction(Expression, Value);
          return new DynamicMetaObject(expr, restrictions);
        } // func BindGetMember

        public override DynamicMetaObject BindInvokeMember(InvokeMemberBinder binder, DynamicMetaObject[] args)
        {
          Expression expr;

          switch (TryBindInvokeMember(binder, false, new DynamicMetaObject(Expression.Default((Type)Value), BindingRestrictions.Empty, null), args, out expr))
          {
            case BindResult.Ok:
              return new DynamicMetaObject(expr, GetMethodSignatureRestriction(null, args).Merge(BindingRestrictions.GetInstanceRestriction(Expression, Value)));
            default:
              return new DynamicMetaObject(expr, GetMethodSignatureRestriction(null, args).Merge(BindingRestrictions.GetInstanceRestriction(Expression, Value)));
          }
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

    private Dictionary<string, Delegate> luaFunctions = new Dictionary<string, Delegate>();

    public override DynamicMetaObject GetMetaObject(Expression parameter)
    {
      return new LuaCoreMetaObject(this, parameter);
    } // func GetMetaObject

    private bool TryGetLuaFunction(string sName, out Delegate function)
    {
      // Ist die Funktion schon gecacht
      if (luaFunctions.TryGetValue(sName, out function))
        return true;

      MethodInfo mi = typeof(Lua).GetMethod("Lua_" + sName, BindingFlags.Instance | BindingFlags.NonPublic);
      if (mi == null)
      {
        function = null;
        return false;
      }
      else
      {
        Type typeDelegate = Expression.GetDelegateType((from p in mi.GetParameters() select p.ParameterType).Concat(new Type[] { mi.ReturnType }).ToArray());
        function = Delegate.CreateDelegate(typeDelegate, this, mi);
        return true;
      }
    } // func TryGetLuaFunction

    public override object GetValue(object item)
    {
      object r = base.GetValue(item);

      if (r == null && item is string)
      {
        string sName = (string)item;
        Delegate function;

        if (TryGetLuaFunction(sName, out function))
          r = function;
        else
          switch (sName)
          {
            case "string":
              r = LuaStringProxy;
              break;
            case "math":
              r = LuaMathProxy;
              break;
            case "os":
              r = LuaOsProxy;
              break;
          }
      }

      return r;
    } // func GetValue

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

    private object Lua_assert(object value, string sMessage = null)
    {
      Debug.Assert(IsTrue(value), sMessage);
      return value;
    } // func LuaAssert

    private object[] Lua_collectgarbage(string opt, object arg = null)
    {
      switch (opt)
      {
        case "collect":
          GC.Collect();
          return Lua_collectgarbage("count");
        case "count":
          long iMem = GC.GetTotalMemory(false);
          return new object[] { iMem / 1024.0, iMem % 1024 };
        case "isrunning":
          return new object[] { true };
        default:
          return emptyResult;
      }
    } // func Lua_collectgarbage

    private object[] Lua_dofile(string sFileName)
    {
      return DoChunk(sFileName);
    } // func Lua_dofile

    private void Lua_error(string sMessage, int level = 1)
    {
      // level ist der StackTrace
      throw new LuaException(sMessage, null);
    } // proc Lua_error

    private void Lua_print(params object[] args)
    {
      if (args == null)
        return;

      for (int i = 0; i < args.Length; i++)
        Debug.Write(args[i]);
      Debug.WriteLine(String.Empty);
    } // proc LuaPrint

    #endregion

    #region -- String Manipulation ----------------------------------------------------

    ///////////////////////////////////////////////////////////////////////////////
    /// <summary></summary>
    private static class LuaString
    {

    } // class LuaString

    #endregion

    #region -- Mathematical Functions -------------------------------------------------

    private static class LuaMath
    {
      private static Random rand = null;

      public static double abs(double x)
      {
        return Math.Abs(x);
      } // func abs

      public static double acos(double x)
      {
        return Math.Acos(x);
      } // func acos

      public static double asin(double x)
      {
        return Math.Asin(x);
      } // func asin

      public static double atan(double x)
      {
        return Math.Atan(x);
      } // func atan

      public static double atan2(double y, double x)
      {
        return Math.Atan2(y, x);
      } // func atan2

      public static double ceil(double x)
      {
        return Math.Ceiling(x);
      } // func ceil

      public static double cos(double x)
      {
        return Math.Cos(x);
      } // func Cos

      public static double cosh(double x)
      {
        return Math.Cosh(x);
      } // func cosh

      public static double deg(double x)
      {
        return x * 180.0 / Math.PI;
      } // func deg

      public static double exp(double x)
      {
        return Math.Exp(x);
      } // func exp

      public static double floor(double x)
      {
        return Math.Floor(x);
      } // func floor

      public static double fmod(double x, double y)
      {
        return x % y;
      } // func fmod

      public static double frexp(double x)
      {
        // Returns m and e such that x = m2e, e is an integer and the absolute value of m is in the range [0.5, 1) (or zero when x is zero).
        throw new NotImplementedException();
      } // func frexp

      // The value HUGE_VAL, a value larger than or equal to any other numerical value.
      public static double huge { get { throw new NotImplementedException(); } }

      public static double ldexp(double m, double e)
      {
        // Returns m2e (e should be an integer).
        throw new NotImplementedException();
      } // func ldexp

      public static double log(double x, double b = Math.E)
      {
        return Math.Log(x, b);
      } // func log

      public static double max(double[] x)
      {
        double r = Double.MinValue;
        for (int i = 0; i < x.Length; i++)
          if (r < x[i])
            r = x[i];
        return r;
      } // func max

      public static double min(double[] x)
      {
        double r = Double.MinValue;
        for (int i = 0; i < x.Length; i++)
          if (r > x[i])
            r = x[i];
        return r;
      } // func min

      public static object[] modf(double x)
      {
        if (x < 0)
        {
          double y = Math.Ceiling(x);
          return new object[] { y, y - x };
        }
        else
        {
          double y = Math.Floor(x);
          return new object[] { y, x - y };
        }
      } // func modf

      public static double pow(double x, double y)
      {
        return Math.Pow(x, y);
      } // func pow

      public static double rad(double x)
      {
        return x * Math.PI / 180.0;
      } // func rad

      public static object random(object m = null, object n = null)
      {
        if (rand == null)
          rand = new Random();

        if (m == null && n == null)
          return rand.NextDouble();
        else if (m != null && n == null)
          return rand.Next(1, Convert.ToInt32(m));
        else
          return rand.Next(Convert.ToInt32(m), Convert.ToInt32(n));
      } // func random

      public static void randomseed(int x)
      {
        rand = new Random(x);
      } // proc randomseed

      public static double sin (double x)
      {
        return Math.Sin(x);
      } // func sin

      public static double sinh (double x)
      {
        return Math.Sinh(x);
      } // func sinh

      public static double sqrt(double x)
      {
        return Math.Sqrt(x);
      } // func sqrt

      public static double tan(double x)
      {
        return Math.Tan(x);
      } // func tan

      public static double tanh(double x)
      {
        return Math.Tanh(x);
      } // func tanh

      public static double pi { get { return Math.PI; } }
      public static double e { get { return Math.E; } }
    } // clas LuaMath
    
    #endregion

    #region -- Operating System Facilities --------------------------------------------

    ///////////////////////////////////////////////////////////////////////////////
    /// <summary></summary>
    private static class LuaOS
    {
      public static int clock()
      {
        return Environment.TickCount;
      } // func clock
    } // class LuaOS

    #endregion

    // -- Static --------------------------------------------------------------

    private static IDynamicMetaObjectProvider luaStringProxy = null;
    private static IDynamicMetaObjectProvider luaMathProxy = null;
    private static IDynamicMetaObjectProvider luaOsProxy = null;

    private static IDynamicMetaObjectProvider LuaStringProxy
    {
      get
      {
        if (luaStringProxy == null)
          luaStringProxy = new LuaPackageProxy(typeof(LuaString));
        return luaStringProxy;
      }
    } // prop LuaOs

    private static IDynamicMetaObjectProvider LuaMathProxy
    {
      get
      {
        if (luaMathProxy == null)
          luaMathProxy = new LuaPackageProxy(typeof(LuaMath));
        return luaMathProxy;
      }
    } // prop LuaMathProxy

    private static IDynamicMetaObjectProvider LuaOsProxy
    {
      get
      {
        if (luaOsProxy == null)
          luaOsProxy = new LuaPackageProxy(typeof(LuaOS));
        return luaOsProxy;
      }
    } // prop LuaOs
  } // class Lua
}
