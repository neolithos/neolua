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
    private LuaFilePackage io = null;

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
      if (sName == "io")
      {
        if (io == null)
          io = new LuaFilePackage();
        expr = Expression.Constant(io);
        return true;
      }
      else if (Lua.TryGetLuaFunction(sName, GetType(), out f))
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

    #region -- void RegisterPackage ---------------------------------------------------

    /// <summary>Registers a type as an library.</summary>
    /// <param name="sName"></param>
    /// <param name="type"></param>
    public void RegisterPackage(string sName, Type type)
    {
      if (String.IsNullOrEmpty(sName))
        throw new ArgumentNullException("name");
      if (type == null)
        throw new ArgumentNullException("type");

      this[sName] = Neo.IronLua.LuaType.GetType(type);
    } // func RegisterPackage

    #endregion

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
        case "step":
          return new LuaResult(true);
        case "setpause":
          return new LuaResult(false);
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
        throw new ArgumentException(); // no support for stdin
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
          return DoChunk((string)args[0], "dummy.lua");
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

    private object LuaLoadReturn(LuaChunk c, LuaGlobal env)
    {
      if (env == null)
        return new Func<LuaResult>(() => this.DoChunk(c));
      else
        return new Func<LuaResult>(() => env.DoChunk(c));
    } // func LuaLoadReturn

    /// <summary></summary>
    /// <param name="ld"></param>
    /// <param name="source"></param>
    /// <param name="mode"></param>
    /// <param name="env"></param>
    /// <returns></returns>
    [LuaFunction("load")]
    private object LuaLoad(object ld, string source, string mode, LuaGlobal env)
    {
      if (source == null)
        source = "=(load)";

      if (mode == "b" || !(ld is string)) // binary chunks are not implementeted
        throw new NotImplementedException();

      // create the chunk
      return LuaLoadReturn(lua.CompileChunk((string)ld, source, false), env); // is only disposed, when Lua-Script-Engine disposed.
    } // func LuaLoad

    /// <summary></summary>
    /// <param name="filename"></param>
    /// <param name="mode"></param>
    /// <param name="env"></param>
    /// <returns></returns>
    [LuaFunction("loadfile")]
    private object LuaLoadFile(string filename, string mode, LuaGlobal env)
    {
      if (mode == "b") // binary chunks are not implementeted
        throw new NotImplementedException();

      // create the chunk
      return LuaLoadReturn(lua.CompileChunk(filename, false), env);
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

      OnPrint(String.Join(" ", (from a in args select a == null ? String.Empty : a.ToString())));
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
      return ToNumber(v, iBase);
    } // func LuaToNumber

    internal static object ToNumber(object v, int iBase)
    {
      if (v == null)
        return null;
      else if (v is string)
      {
        string sValue = (string)v;
        if (iBase == 0)
        {
          if (sValue.StartsWith("0x", StringComparison.Ordinal))
          {
            iBase = 16;
            sValue = sValue.Substring(2);
          }
          else
            iBase = 10;
        }
        if (iBase == 10)
        {
          int iTmp;
          if (int.TryParse(sValue, out iTmp))
            return iTmp;
          else
            return Convert.ToDouble(sValue);
        }
        else
          return Convert.ToInt32((string)v, iBase); // todo: Incompatible to lua reference
      }
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
    } // func ToNumber

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
        return "boolean";
      else if (v is LuaTable)
        return "table";
      else if (v is Delegate)
        return "function";
      else if (v is LuaThread)
        return "thread";
      else if (v is LuaFile)
        return ((LuaFile)v).IsClosed ? "closed file" : "file";
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
  } // class LuaGlobal

  #endregion
}
