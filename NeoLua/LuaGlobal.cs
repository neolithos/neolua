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
    /// <summary></summary>
    public const string VersionString = "NeoLua 5.2";

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
            expr = val.GetIndexExpression(binder.Name, binder.IgnoreCase);

          return new DynamicMetaObject(expr, BindingRestrictions.GetInstanceRestriction(Expression, val));
        } // func BindGetMember

        public override DynamicMetaObject BindGetIndex(GetIndexBinder binder, DynamicMetaObject[] indexes)
        {
          LuaClrClassObject val = (LuaClrClassObject)Value;
          if (indexes.Any(c => !c.HasValue))
            return binder.Defer(indexes);

          // create the generic type name
          StringBuilder sbTypeName = new StringBuilder();
          val.GetFullName(sbTypeName);
          sbTypeName.Append('`').Append(indexes.Length);

          // find the type
          Type typeGeneric = Lua.GetType(sbTypeName.ToString());
          if (typeGeneric == null)
            return new DynamicMetaObject(
              Lua.ThrowExpression(String.Format(Properties.Resources.rsParseUnknownType, sbTypeName.ToString())),
              Lua.GetMethodSignatureRestriction(null, indexes)
              );

          // check, only types are allowed
          if (indexes.Any(c => c.LimitType != typeof(LuaClrClassObject)))
          {
            return new DynamicMetaObject(
             Lua.ThrowExpression(Properties.Resources.rsClrGenericTypeExpected),
             Lua.GetMethodSignatureRestriction(null, indexes));
          }

          // create the call to the runtime
          MethodInfo miGetGenericItem = typeof(LuaClrClassObject).GetMethod("GetGenericItem", BindingFlags.Public | BindingFlags.Instance | BindingFlags.InvokeMethod);
          return new DynamicMetaObject(
            Expression.Call(Expression.Constant(val, typeof(LuaClrClassObject)), miGetGenericItem,
            Expression.Constant(typeGeneric),
            Expression.NewArrayInit(typeof(LuaClrClassObject), (from a in indexes select Expression.Convert(a.Expression, a.LimitType)).AsEnumerable())),
            Lua.GetMethodSignatureRestriction(null, indexes));
        } // func BindGetIndex
        
        public override DynamicMetaObject BindInvokeMember(InvokeMemberBinder binder, DynamicMetaObject[] args)
        {
          Type type = ((LuaClrClassObject)Value).GetItemType();
          Expression expr;
          if (type != null)
          {
            if (String.Compare(binder.Name, "GetType", binder.IgnoreCase) == 0 && args.Length == 0)
            {
              return new DynamicMetaObject(Expression.Constant(type, typeof(Type)), BindingRestrictions.GetInstanceRestriction(Expression, Value), type);
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
          Type type = ((LuaClrClassObject)Value).GetItemType();
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
          LuaClrClassObject val = (LuaClrClassObject)Value;
          if (val.subItems != null)
            for (int i = 0; i < val.subItems.Count; i++)
              yield return val.subItems[i].Name;
        } // func GetDynamicMemberNames
      } // class LuaClrClassMetaObject

      #endregion

      private LuaClrClassObject parent;   // Access to the parent name space
      private string sName;               // Name of the entity
      private MethodInfo miGetValue;      // Method for the access to the array

      private Type type;                                // Type, type behind the name, if it exists
      private int iAssemblyCount = 0;                   // Anzahl der Assembly, als zuletzt versucht wurde ein Typ zu finden, -1 für Namespace
      private List<LuaClrClassObject> subItems = null;  // Liste alle untergeordneten abgefragten Typen (Namespace, Classes, SubClasses)
      // Die Indices werden als Konstante in die Expression gegossen, damit darf sich der Index nie ändern.
      private Dictionary<string, int> index = null;     // Index für die schnelle Suche von Namespaces und Klassen, wird erst ab 10 Einträgen angelegt

      #region -- Ctor/Dtor ------------------------------------------------------------

      public LuaClrClassObject(LuaClrClassObject parent, string sName, Type type, MethodInfo mi)
      {
        this.parent = parent;
        this.sName = sName;
        this.type = type;
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
        int iNewCount;
        if (type == null &&  // no type found
            parent != null && // the root has no type
            iAssemblyCount >= 0 && // Namespace, there is no type
            (iNewCount = AppDomain.CurrentDomain.GetAssemblies().Length) != iAssemblyCount) // new assembly count
        {
          iAssemblyCount = iNewCount;
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
            if (sName[0] != '`') // is generic type
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

      private int GetIndex(string sName, bool lIgnoreCase, Func<Type> buildType)
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
          // Create the new object
          subItems.Add(new LuaClrClassObject(this, sName, buildType == null ? null : buildType(), miGetValue));

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

        if (iAssemblyCount >= 0 && GetItemType() == null) // Kein Type ermittelt, es gibt aber SubItems, dann ist es ein Namespace
          iAssemblyCount = -1;
        return iIndex;
      } // func GetIndex

      private Expression GetIndexExpression(string sName, bool lIgnoreCase, Func<Type> buildType = null)
      {
        // Erzeuge die Expression für den Zugriff
        return Expression.Call(
          Expression.Constant(this, typeof(LuaClrClassObject)), miGetValue, 
          Expression.Constant(GetIndex(sName, lIgnoreCase, buildType), typeof(int)));
      } // func GetIndexExpression

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

      public LuaClrClassObject GetGenericItem(Type genericType, LuaClrClassObject[] arguments)
      {
        Type[] genericParameters = new Type[arguments.Length];

        // Build the typename
        StringBuilder sb = new StringBuilder();
        sb.Append('`').Append(arguments.Length).Append('[');
        for (int i = 0; i < arguments.Length; i++)
        {
          if (i > 0)
            sb.Append(',');

          Type typeTmp = genericParameters[i] = arguments[i].GetItemType();
          if (typeTmp == null)
            throw new LuaRuntimeException(String.Format(Properties.Resources.rsClrGenericNoType, i), null);

          sb.Append('[').Append(typeTmp.AssemblyQualifiedName).Append(']');
        }
        sb.Append(']');

        // try to find the typename
        return GetItem(GetIndex(sb.ToString(), false, () => genericType.MakeGenericType(genericParameters)));
      } // func GetGenericItem

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

    #region -- Ctor/Dtor --------------------------------------------------------------

    /// <summary>Create a new environment for the lua script manager.</summary>
    /// <param name="lua"></param>
    public LuaGlobal(Lua lua)
    {
      if (lua == null)
        throw new ArgumentNullException("lua");

      this.lua = lua;
    } // ctor

    #endregion

    #region -- Dynamic Members --------------------------------------------------------

    private bool TryGetLuaSystem(string sName, out Expression expr)
    {
      Lua.CoreFunction f;
      IDynamicMetaObjectProvider lib;
      if (Lua.TryGetLuaFunction(sName, GetType(), out f))
      {
        expr = Expression.Constant(f.GetDelegate(this), typeof(object));
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

    /// <summary></summary>
    /// <param name="binder"></param>
    /// <param name="exprTable"></param>
    /// <param name="memberName"></param>
    /// <param name="flags"></param>
    /// <returns></returns>
    protected override DynamicMetaObject GetMemberAccess(DynamicMetaObjectBinder binder, Expression exprTable, object memberName, MemberAccessFlag flags)
    {
      if ((flags & MemberAccessFlag.ForWrite) == 0  && memberName is string)
      {
        string sMemberName = (string)memberName;

        // Access to clr can not overload
        if (String.Compare(sMemberName, "_VERSION", (flags & MemberAccessFlag.IgnoreCase) !=0) == 0)
          return new DynamicMetaObject(Expression.Constant(VersionString, typeof(string)), BindingRestrictions.GetInstanceRestriction(exprTable, this));

        // Bind the value
        DynamicMetaObject moGet = base.GetMemberAccess(binder, exprTable, memberName, flags);

        // Check for system function or library
        Expression expr;
        if (TryGetLuaSystem(sMemberName, out expr))
        {
          return new DynamicMetaObject(
          Expression.Coalesce(moGet.Expression, expr),
          moGet.Restrictions);
        }
        else
          return moGet;
      }
      else
        return base.GetMemberAccess(binder, exprTable, memberName, flags);
    } // func GetMemberAccess

    #endregion

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

    /// <summary>Compiles and execute the filename.</summary>
    /// <param name="sFileName">Name of the lua file.</param>
    /// <param name="args">Parameter definition for the file.</param>
    /// <returns>Return values of the file.</returns>
    public LuaResult DoChunk(string sFileName, params KeyValuePair<string, object>[] args)
    {
      return DoChunk(sFileName, new StreamReader(sFileName), args);
    } // proc DoFile

    /// <summary>Compiles and execute the stream.</summary>
    /// <param name="sr">Stream</param>
    /// <param name="sName">Name of the stream</param>
    /// <param name="args">Parameter definition for the stream.</param>
    /// <returns>Return values of the stream.</returns>
    public LuaResult DoChunk(TextReader sr, string sName, params KeyValuePair<string, object>[] args)
    {
      return DoChunk(sName, sr, args);
    } // proc DoChunk

    /// <summary>Compiles and executes code.</summary>
    /// <param name="sCode">Lua-Code</param>
    /// <param name="sName">Name of the lua-code</param>
    /// <param name="args">Parameter definition for the lua-code.</param>
    /// <returns>Return values of the lua-code.</returns>
    public LuaResult DoChunk(string sCode, string sName, params KeyValuePair<string, object>[] args)
    {
      return DoChunk(sName, new StringReader(sCode), args);
    } // func DoChunk

    private LuaResult DoChunk(string sChunkName, TextReader tr, KeyValuePair<string, object>[] args)
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

    /// <summary>Executes a precompiled chunk on the lua environment.</summary>
    /// <param name="chunk">Compiled chunk.</param>
    /// <param name="callArgs">Arguments for the chunk.</param>
    /// <returns>Return values of the chunk.</returns>
    public LuaResult DoChunk(LuaChunk chunk, params object[] callArgs)
    {
      if (!chunk.IsCompiled)
        throw new ArgumentException(Properties.Resources.rsChunkNotCompiled, "chunk");
      if (lua != chunk.Lua)
        throw new ArgumentException(Properties.Resources.rsChunkWrongScriptManager, "chunk");

      object[] args = new object[callArgs == null ? 0 : callArgs.Length + 1];
      args[0] = this;
      if (callArgs != null)
        Array.Copy(callArgs, 0, args, 1, callArgs.Length);
      
      object r = chunk.Chunk.DynamicInvoke(args);
      return r is LuaResult ? (LuaResult)r : new LuaResult(r);
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

    /// <summary></summary>
    /// <param name="value"></param>
    /// <param name="sMessage"></param>
    /// <returns></returns>
    [LuaFunction("assert")]
    private object LuaAssert(object value, string sMessage)
    {
      Debug.Assert(IsTrue(value), sMessage);
      return value;
    } // func LuaAssert

    /// <summary></summary>
    /// <param name="opt"></param>
    /// <param name="arg"></param>
    /// <returns></returns>
    [LuaFunction("collectgarbage")]
    private LuaResult LuaCollectgarbage(string opt, object arg = null)
    {
      switch (opt)
      {
        case "collect":
          GC.Collect();
          return LuaCollectgarbage("count");
        case "count":
          long iMem = GC.GetTotalMemory(false);
          return new LuaResult(iMem / 1024.0, iMem % 1024);
        case "isrunning":
          return new LuaResult(true);
        default:
          return LuaResult.Empty;
      }
    } // func LuaCollectgarbage

    /// <summary></summary>
    /// <param name="args"></param>
    /// <returns></returns>
    [LuaFunction("dofile")]
    private LuaResult LuaDoFile(object[] args)
    {
      if (args == null || args.Length == 0)
        throw new ArgumentException();
      else if (args.Length == 1)
        return DoChunk((string)args[0]);
      else
        return DoChunk((string)args[0], CreateArguments(1, args));
    } // func LuaDoFile

    /// <summary></summary>
    /// <param name="args"></param>
    /// <returns></returns>
    [LuaFunction("dochunk")]
    private LuaResult LuaDoChunk(object[] args)
    {
      if (args == null || args.Length == 0)
        throw new ArgumentException();
      if (args[0] is LuaChunk)
      {
        if (args.Length == 1)
          return DoChunk((LuaChunk)args[0]);
        else
        {
          object[] p = new object[args.Length - 1];
          Array.Copy(args, 1, p, 0, p.Length);
          return DoChunk((LuaChunk)args[0], p);
        }
      }
      else if (args[0] is string)
      {
        if (args.Length == 1)
          throw new ArgumentOutOfRangeException();
        else if (args.Length == 2)
          return DoChunk((string)args[0], (string)args[1]);
        else
          return DoChunk((string)args[0], (string)args[1], CreateArguments(2, args));
      }
      else if (args[0] is TextReader)
      {
        if (args.Length == 1)
          throw new ArgumentOutOfRangeException();
        else if (args.Length == 2)
          return DoChunk((TextReader)args[0], (string)args[1]);
        else
          return DoChunk((TextReader)args[0], (string)args[1], CreateArguments(2, args));
      }
      else
        throw new ArgumentException();
    } // func LuaDoChunk

    private static KeyValuePair<string, object>[] CreateArguments(int iOffset, object[] args)
    {
      KeyValuePair<string, object>[] p = new KeyValuePair<string, object>[(args.Length - iOffset + 1) / 2]; // on 3 arguments we have 1 parameter

      // create parameter
      for (int i = 0; i < p.Length; i++)
      {
        int j = 2 + i * 2;
        string sName = (string)args[j++];
        object value = j < args.Length ? args[j] : null;
        p[i] = new KeyValuePair<string, object>(sName, value);
      }
      return p;
    } // func CreateArguments
    
    /// <summary></summary>
    /// <param name="sMessage"></param>
    /// <param name="level"></param>
    [LuaFunction("error")]
    private void LuaError(string sMessage, int level)
    {
      if (level == 0)
        level = 1;

      // level ist der StackTrace
      throw new LuaRuntimeException(sMessage, level, true);
    } // proc LuaError

    /// <summary></summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    [LuaFunction("getmetatable")]
    private object LuaGetMetaTable(object obj)
    {
      LuaTable t = obj as LuaTable;
      if (t == null)
        return null;
      else
        return t[csMetaTable];
    } // func LuaGetMetaTable

    private LuaResult pairsEnum(object s, object current)
    {
      System.Collections.IEnumerator e = (System.Collections.IEnumerator)s;

      // return value
      if (e.MoveNext())
      {
        KeyValuePair<object, object> k = (KeyValuePair<object, object>)e.Current;
        return new LuaResult(k.Key, k.Value);
      }
      else
        return LuaResult.Empty;
    } // func pairsEnum

    /// <summary></summary>
    /// <param name="t"></param>
    /// <returns></returns>
    [LuaFunction("ipairs")]
    private LuaResult LuaIPairs(LuaTable t)
    {
      var e = new LuaIndexPairEnumerator(t);
      return new LuaResult(new Func<object, object, LuaResult>(pairsEnum), e, e);
    } // func ipairs

    /// <summary></summary>
    /// <param name="t"></param>
    /// <returns></returns>
    [LuaFunction("pairs")]
    private LuaResult LuaPairs(LuaTable t)
    {
      var e = ((System.Collections.IEnumerable) t).GetEnumerator();
      return new LuaResult(new Func<object, object, LuaResult>(pairsEnum), e, e);
    } // func LuaPairs

    /// <summary></summary>
    /// <param name="ld"></param>
    /// <param name="source"></param>
    /// <param name="mode"></param>
    /// <param name="env"></param>
    /// <returns></returns>
    [LuaFunction("load")]
    private object LuaLoad(object ld, string source, string mode, LuaTable env)
    {
      throw new NotImplementedException();
    } // func LuaLoad

    /// <summary></summary>
    /// <param name="filename"></param>
    /// <param name="mode"></param>
    /// <param name="env"></param>
    /// <returns></returns>
    [LuaFunction("loadfile")]
    private object LuaLoadFile(string filename, string mode, LuaTable env)
    {
      throw new NotImplementedException();
    } // func LuaLoadFile

    /// <summary></summary>
    /// <param name="t"></param>
    /// <param name="next"></param>
    /// <returns></returns>
    [LuaFunction("next")]
    private object LuaNext(LuaTable t, object next = null)
    {
      throw new NotImplementedException();
    } // func LuaNext

    /// <summary></summary>
    /// <param name="dlg"></param>
    /// <param name="args"></param>
    /// <returns></returns>
    [LuaFunction("pcall")]
    private LuaResult LuaPCall(Delegate dlg, params object[] args)
    {
      return LuaXPCall(dlg, null, args);
    } // func LuaPCall

    /// <summary></summary>
    /// <param name="sText"></param>
    protected virtual void OnPrint(string sText)
    {
      Debug.WriteLine(sText);
    } // proc OnPrint

    /// <summary></summary>
    /// <param name="args"></param>
    [LuaFunction("print")]
    private void LuaPrint(params object[] args)
    {
      if (args == null)
        return;

      OnPrint(String.Concat((from a in args select a == null ? String.Empty : a.ToString())));
    } // proc LuaPrint

    /// <summary></summary>
    /// <param name="a"></param>
    /// <param name="b"></param>
    /// <returns></returns>
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

    /// <summary></summary>
    /// <param name="t"></param>
    /// <param name="index"></param>
    /// <returns></returns>
    [LuaFunction("rawget")]
    private object LuaRawGet(LuaTable t, object index)
    {
      return t[index];
    } // func LuaRawGet

    /// <summary></summary>
    /// <param name="v"></param>
    /// <returns></returns>
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

    /// <summary></summary>
    /// <param name="t"></param>
    /// <param name="index"></param>
    /// <param name="value"></param>
    /// <returns></returns>
    [LuaFunction("rawset")]
    private LuaTable LuaRawSet(LuaTable t, object index, object value)
    {
      t[index] = value;
      return t;
    } // func LuaRawSet

    /// <summary></summary>
    /// <param name="index"></param>
    /// <param name="values"></param>
    /// <returns></returns>
    [LuaFunction("select")]
    private LuaResult LuaSelect(int index, params object[] values)
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
        return LuaResult.Empty;
    } // func LuaSelect

    /// <summary></summary>
    /// <param name="t"></param>
    /// <param name="metaTable"></param>
    /// <returns></returns>
    [LuaFunction("setmetatable")]
    private LuaTable LuaSetMetaTable(LuaTable t, LuaTable metaTable)
    {
      t[csMetaTable] = metaTable;
      return t;
    } // proc LuaSetMetaTable

    /// <summary></summary>
    /// <param name="v"></param>
    /// <param name="iBase"></param>
    /// <returns></returns>
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

    /// <summary></summary>
    /// <param name="v"></param>
    /// <returns></returns>
    [LuaFunction("tostring")]
    private string LuaToString(object v)
    {
      if (v == null)
        return null;
      else
        return v.ToString();
    } // func LuaToString

    /// <summary></summary>
    /// <param name="v"></param>
    /// <param name="lClr"></param>
    /// <returns></returns>
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

    /// <summary></summary>
    /// <param name="dlg"></param>
    /// <param name="msgh"></param>
    /// <param name="args"></param>
    /// <returns></returns>
    [LuaFunction("xpcall")]
    private LuaResult LuaXPCall(Delegate dlg, Delegate msgh, params object[] args)
    {
      // call the function save
      try
      {
        return new LuaResult(true, dlg.DynamicInvoke(args));
      }
      catch (Exception e)
      {
        return new LuaResult(false, e.Message, e);
      }
    } // func LuaPCall

    #endregion

    // -- Static ------------------------------------------------------------

    private static LuaClrClassObject clr = new LuaClrClassObject(null, String.Empty, null, null);

    /// <summary></summary>
    internal static IDynamicMetaObjectProvider Clr { get { return clr; } }
  } // class LuaGlobal

  #endregion
}
