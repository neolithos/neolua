using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace Neo.IronLua
{
  #region -- class LuaGlobal ----------------------------------------------------------

  ///////////////////////////////////////////////////////////////////////////////
  /// <summary></summary>
  public class LuaGlobal : LuaTable
  {
    private const string csMetaTable = "__metatable";
    public const string VersionString = "NeoLua 5.2";

    #region -- class LuaCoreMetaObject ------------------------------------------------

    ///////////////////////////////////////////////////////////////////////////////
    /// <summary></summary>
    protected class LuaCoreMetaObject : LuaMetaObject
    {
      public LuaCoreMetaObject(LuaGlobal lua, Expression parameter)
        : base(lua, parameter)
      {
      } // ctor

      private bool TryGetLuaSystem(string sName, out Expression expr)
      {
        Lua.CoreFunction f;
        IDynamicMetaObjectProvider lib;
        if (Lua.TryGetLuaFunction(sName, Value.GetType(), out f))
        {
          expr = Expression.Constant(f.GetDelegate(Value), typeof(object));
          return true;
        }
        else if (Lua.TryGetSystemLibrary(sName, out lib))
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
        else if (binder.Name == "_VERSION")
          return new DynamicMetaObject(Expression.Constant(VersionString, typeof(string)), BindingRestrictions.GetInstanceRestriction(Expression, Value));

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
    /// <summary>For every namespace we create an object for the access.</summary>
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

          // first ask the type behind
          Type type = val.GetItemType();
          if (type != null)
          {
            switch (Lua.TryBindGetMember(binder, new DynamicMetaObject(Expression.Default(type), BindingRestrictions.Empty, null), out expr))
            {
              case Lua.BindResult.MemberNotFound:
                expr = null;
                break;
              case Lua.BindResult.Ok:
                expr = Expression.Convert(expr, typeof(object));
                break;
            }
          }

          // Get the index for the access
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
            bool lUseCtor = binder.Name == "ctor"; // Redirect to the ctor

            switch (Lua.TryBindInvokeMember(binder, lUseCtor, new DynamicMetaObject(Expression.Default(type), BindingRestrictions.Empty, null), args, out expr))
            {
              case Lua.BindResult.Ok:
                return new DynamicMetaObject(expr, Lua.GetMethodSignatureRestriction(null, args).Merge(BindingRestrictions.GetInstanceRestriction(Expression, Value)));
              case Lua.BindResult.MemberNotFound:
                return binder.FallbackInvokeMember(new DynamicMetaObject(Expression.Default(type), BindingRestrictions.Empty, null), args);
              default:
                return new DynamicMetaObject(expr, Lua.GetMethodSignatureRestriction(null, args).Merge(BindingRestrictions.GetInstanceRestriction(Expression, Value)));
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

    #region -- class LuaIndexPairEnumerator -------------------------------------------

    private class LuaIndexPairEnumerator : System.Collections.IEnumerator
    {
      private LuaTable t;
      private int[] indexes;
      private int iCurrent  = -1;

      public LuaIndexPairEnumerator(LuaTable t)
      {
        this.t = t;

        List<int> lst = new List<int>();
        foreach (var c in t)
        {
          if (c.Key is int)
            lst.Add((int)c.Key);
        }
        lst.Sort();
        indexes = lst.ToArray();
      } // ctor

      public object Current
      {
        get
        {
          if (iCurrent >= 0 && iCurrent < indexes.Length)
          {
            int i = indexes[iCurrent];
            return new KeyValuePair<object, object>(i, t[i]);
          }
          else
            return null;
        }
      } // prop Current

      public bool MoveNext()
      {
        iCurrent++;
        return iCurrent < indexes.Length;
      } // func MoveNext

      public void Reset()
      {
        iCurrent = -1;
      } // proc Reset
    } // class LuaIndexPairEnumerator

    #endregion

    private Lua lua;

    public LuaGlobal(Lua lua)
    {
      if (lua == null)
        throw new ArgumentNullException("lua");

      this.lua = lua;
    } // ctor

    public override DynamicMetaObject GetMetaObject(Expression parameter)
    {
      return new LuaCoreMetaObject(this, parameter);
    } // func GetMetaObject

    /// <summary>Registers a type as an library.</summary>
    /// <param name="sName"></param>
    /// <param name="type"></param>
    public void RegisterPackage(string sName, Type type)
    {
      if (String.IsNullOrEmpty(sName))
        throw new ArgumentNullException("name");
      if (type == null)
        throw new ArgumentNullException("type");

      this[sName] = new LuaPackageProxy(type);
    } // func RegisterPackage

    #region -- DoChunk ----------------------------------------------------------------

    /// <summary>Führt die angegebene Datei aus.</summary>
    /// <param name="sFileName">Dateiname die gelesen werden soll.</param>
    /// <param name="args">Parameter für den Codeblock</param>
    /// <returns>Ergebnis der Ausführung.</returns>
    public object[] DoChunk(string sFileName, params KeyValuePair<string, object>[] args)
    {
      return DoChunk(sFileName, new StreamReader(sFileName), args);
    } // proc DoFile

    /// <summary>Führt den angegebene Stream aus.</summary>
    /// <param name="sr">Inhalt</param>
    /// <param name="sName">Name der Datei</param>
    /// <param name="args">Parameter für den Codeblock</param>
    /// <returns>Ergebnis der Ausführung.</returns>
    public object[] DoChunk(TextReader sr, string sName, params KeyValuePair<string, object>[] args)
    {
      return DoChunk(sName, sr, args);
    } // proc DoChunk

    /// <summary>Führt die angegebene Zeichenfolge aus.</summary>
    /// <param name="sCode">Code</param>
    /// <param name="sName">Name des Codes</param>
    /// <param name="args"Parameter für den Codeblock></param>
    /// <returns>Ergebnis der Ausführung.</returns>
    public object[] DoChunk(string sCode, string sName, params KeyValuePair<string, object>[] args)
    {
      return DoChunk(sName, new StringReader(sCode), args);
    } // func DoChunk

    private object[] DoChunk(string sChunkName, TextReader tr, KeyValuePair<string, object>[] args)
    {
      // Erzeuge die Parameter
      object[] callArgs;
      KeyValuePair<string, Type>[] callTypes;
      if (args != null)
      {
        callArgs = new object[args.Length];
        callTypes = new KeyValuePair<string, Type>[args.Length];
        for (int i = 0; i < args.Length; i++)
        {
          callArgs[i] = args[i].Value;
          callTypes[i] = new KeyValuePair<string, Type>(args[i].Key, args[i].Value == null ? typeof(object) : args[i].Value.GetType());
        }
      }
      else
      {
        callArgs = new object[0];
        callTypes = new KeyValuePair<string, Type>[0];
      }

      // Führe den Block aus
      using (LuaChunk chunk = lua.CompileChunk(sChunkName, false, tr, callTypes))
        return DoChunk(chunk, callArgs);
    } // func DoChunk

    public object[] DoChunk(LuaChunk chunk, params object[] callArgs)
    {
      if (!chunk.IsCompiled)
        throw new ArgumentException("Chunk is not compiled.");

      object[] args = new object[callArgs == null ? 0 : callArgs.Length + 1];
      args[0] = this;
      if (callArgs != null)
        Array.Copy(callArgs, 0, args, 1, callArgs.Length);

      return (object[])chunk.Chunk.DynamicInvoke(args);
    } // func DoChunk
    
    #endregion

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

    [LuaFunction("assert")]
    private object LuaAssert(object value, string sMessage)
    {
      Debug.Assert(IsTrue(value), sMessage);
      return value;
    } // func LuaAssert

    [LuaFunction("collectgarbage")]
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
          return Lua.EmptyResult;
      }
    } // func LuaCollectgarbage

    [LuaFunction("dofile")]
    private object[] LuaDoFile(string sFileName)
    {
      return DoChunk(sFileName);
    } // func LuaDoFile

    [LuaFunction("error")]
    private void LuaError(string sMessage, int level)
    {
      if (level == 0)
        level = 1;

      // level ist der StackTrace
      throw new LuaRuntimeException(sMessage, level, true);
    } // proc LuaError

    [LuaFunction("getmetatable")]
    private object LuaGetMetaTable(object obj)
    {
      LuaTable t = obj as LuaTable;
      if (t == null)
        return null;
      else
        return t[csMetaTable];
    } // func LuaGetMetaTable

    private object[] pairsEnum(object s, object current)
    {
      System.Collections.IEnumerator e = (System.Collections.IEnumerator)s;

      // return value
      if (e.MoveNext())
      {
        KeyValuePair<object, object> k = (KeyValuePair<object, object>)e.Current;
        return new object[] { k.Key, k.Value };
      }
      else
        return Lua.EmptyResult;
    } // func pairsEnum

    [LuaFunction("ipairs")]
    private object[] LuaIPairs(LuaTable t)
    {
      var e = new LuaIndexPairEnumerator(t);
      return new object[] { new Func<object, object, object[]>(pairsEnum), e, e };
    } // func ipairs

    [LuaFunction("pairs")]
    private object[] LuaPairs(LuaTable t)
    {
      var e = ((System.Collections.IEnumerable) t).GetEnumerator();
      return new object[] { new Func<object, object, object[]>(pairsEnum), e, e };
    } // func LuaPairs

    [LuaFunction("load")]
    private object LuaLoad(object ld, string source, string mode, LuaTable env)
    {
      throw new NotImplementedException();
    } // func LuaLoad

    [LuaFunction("loadfile")]
    private object LuaLoadFile(string filename, string mode, LuaTable env)
    {
      throw new NotImplementedException();
    } // func LuaLoadFile

    [LuaFunction("next")]
    private object LuaNext(LuaTable t, object next = null)
    {
      throw new NotImplementedException();
    } // func LuaNext

    [LuaFunction("pcall")]
    private object[] LuaPCall(Delegate dlg, params object[] args)
    {
      return LuaXPCall(dlg, null, args);
    } // func LuaPCall

    [LuaFunction("print")]
    private void LuaPrint(params object[] args)
    {
      if (args == null)
        return;

      for (int i = 0; i < args.Length; i++)
        Debug.Write(args[i]);
      Debug.WriteLine(String.Empty);
    } // proc LuaPrint

    [LuaFunction("rawequal")]
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

    [LuaFunction("rawget")]
    private object LuaRawGet(LuaTable t, object index)
    {
      return t[index];
    } // func LuaRawGet

    [LuaFunction("rawlen")]
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

    [LuaFunction("rawset")]
    private LuaTable LuaRawSet(LuaTable t, object index, object value)
    {
      t[index] = value;
      return t;
    } // func LuaRawSet

    [LuaFunction("select")]
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
        return Lua.EmptyResult;
    } // func LuaSelect

    [LuaFunction("setmetatable")]
    private LuaTable LuaSetMetaTable(LuaTable t, LuaTable metaTable)
    {
      t[csMetaTable] = metaTable;
      return t;
    } // proc LuaSetMetaTable

    [LuaFunction("tonumber")]
    private object LuaToNumber(object v, int iBase)
    {
      if (v == null)
        return null;
      else if (v is string)
        return Convert.ToInt32((string)v, iBase == 0 ? 10 : iBase); // todo: Incompatible to lua reference
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

    [LuaFunction("tostring")]
    private string LuaToString(object v)
    {
      if (v == null)
        return null;
      else
        return v.ToString();
    } // func LuaToString

    [LuaFunction("type")]
    private string LuaType(object v, bool lClr = false)
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
        return lClr ? "userdata" : v.GetType().FullName;
    } // func LuaType

    [LuaFunction("xpcall")]
    private object[] LuaXPCall(Delegate dlg, Delegate msgh, params object[] args)
    {
      try
      {
        // call the function save
        object _r = dlg.DynamicInvoke(args);
        object[] r = _r as object[];

        // create the result
        object[] result = new object[1 + (r == null ? 1 : r.Length)];
        result[0] = true;
        if (r != null)
          Array.Copy(r, 0, result, 1, r.Length);
        else
          result[1] = _r;

        return result;
      }
      catch (Exception e)
      {
        return new object[] { false, e.Message, e };
      }
    } // func LuaPCall

    #endregion
  } // class LuaGlobal

  #endregion
}
