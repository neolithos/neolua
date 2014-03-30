using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Dynamic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace Neo.IronLua
{
  #region -- enum LuaRuntimeHelper ----------------------------------------------------

  ///////////////////////////////////////////////////////////////////////////////
  /// <summary>Enumeration with the runtime-functions.</summary>
  internal enum LuaRuntimeHelper
  {
    /// <summary>Converts a value via the TypeConverter</summary>
    Convert,
    /// <summary>Concats the string.</summary>
    StringConcat,
    /// <summary>Sets the table from an initializion list.</summary>
    TableSetObjects,
    /// <summary>Concats Result-Array</summary>
    ConcatArrays
  } // enum LuaRuntimeHelper

  #endregion

  #region -- class Lua ----------------------------------------------------------------

  ///////////////////////////////////////////////////////////////////////////////
  /// <summary>All static methods for the language implementation</summary>
  public partial class Lua
  {
    private static object luaStaticLock = new object();
    private static Dictionary<LuaRuntimeHelper, MethodInfo> runtimeHelperCache = new Dictionary<LuaRuntimeHelper, MethodInfo>();
    private static ConstructorInfo ciResultConstructor = null;
    private static ConstructorInfo ciResultConstructor2 = null;
    private static PropertyInfo piResultIndex = null;
    private static PropertyInfo piResultValues = null;
    private static PropertyInfo piResultEmpty = null;
    private static MethodInfo miTableSetMethod = null;

    #region -- RtConcatArrays, RtStringConcat -----------------------------------------

    internal static Array RtConcatArrays(Type elementType, Array a, Array b, int iStartIndex)
    {
      int iCountB = b.Length - iStartIndex;

      Array r = Array.CreateInstance(elementType, a.Length + iCountB);
      if (a.Length > 0)
        Array.Copy(a, r, a.Length);
      if (iStartIndex < b.Length)
        for (int i = 0; i < iCountB; i++)
          r.SetValue(RtConvert(b.GetValue(i + iStartIndex), elementType), i + a.Length);

      return r;
    } // func RtConcatArrays

    internal static object RtStringConcat(string[] strings)
    {
      return String.Concat(strings);
    } // func RtStringConcat

    #endregion

    #region -- RtConvert --------------------------------------------------------------

    internal static object RtConvert(object value, Type typeTo)
    {
      if (typeTo == typeof(bool)) // Convert to bool
        return ConvertToBoolean(value);
      else if (value == null) // Convert from null
        if (typeTo.IsValueType)
          return Activator.CreateInstance(typeTo); // Default value
        else
          return null;
      else if (typeTo == typeof(string)) // Convert to string
      {
        if (value is string)
          return value;
        TypeConverter conv = TypeDescriptor.GetConverter(value.GetType());
        return conv.ConvertToInvariantString(value);
      }
      else
      {
        Type typeFrom = value.GetType();

        if (typeFrom == typeTo)
          return value;

        // Specials cases
        if (typeFrom.IsEnum)
          typeFrom = typeFrom.GetEnumUnderlyingType();

        if (typeTo.IsAssignableFrom(typeFrom))
          return value;
        else
        {
          TypeConverter conv = TypeDescriptor.GetConverter(typeTo);
          if (conv.CanConvertFrom(typeFrom))
            return conv.ConvertFrom(null, CultureInfo.InvariantCulture, value);
          else
          {
            conv = TypeDescriptor.GetConverter(typeFrom);
            if (conv.CanConvertTo(typeTo))
              return conv.ConvertTo(null, CultureInfo.InvariantCulture, value, typeTo);
            else
              throw new LuaRuntimeException(String.Format(Properties.Resources.rsConversationError, value, typeTo.Name), null);
          }
        }
      }
    } // func RtConvert

    private static bool ConvertToBoolean(object value)
    {
      if (value == null)
        return false;
      else if (value is bool)
        return (bool)value;
      else if (value is byte)
        return (byte)value != 0;
      else if (value is sbyte)
        return (sbyte)value != 0;
      else if (value is short)
        return (short)value != 0;
      else if (value is ushort)
        return (ushort)value != 0;
      else if (value is int)
        return (int)value != 0;
      else if (value is uint)
        return (uint)value != 0;
      else if (value is long)
        return (long)value != 0;
      else if (value is ulong)
        return (ulong)value != 0;
      else if (value is float)
        return (float)value != 0;
      else if (value is double)
        return (double)value != 0;
      else if (value is decimal)
        return (decimal)value != 0;
      else
        return true;
    } // func RtConvertToBoolean

    #endregion

    #region -- Table Objects ----------------------------------------------------------

    internal static object RtTableSetObjects(LuaTable t, object value, int iStartIndex)
    {
      if (value != null && (value is object[] || typeof(object[]).IsAssignableFrom(value.GetType())))
      {
        object[] v = (object[])value;

        for (int i = 0; i < v.Length; i++)
          t[iStartIndex++] = v[i];
      }
      else
        t[iStartIndex] = value;
      return t;
    } // func RtTableSetObjects

    #endregion

    #region -- GetRuntimeHelper -------------------------------------------------------

    internal static MethodInfo GetRuntimeHelper(LuaRuntimeHelper runtimeHelper)
    {
      MethodInfo mi;
      lock (luaStaticLock)
        if (!runtimeHelperCache.TryGetValue(runtimeHelper, out mi))
        {
          string sMemberName = "Rt" + runtimeHelper.ToString();

          mi = typeof(Lua).GetMethod(sMemberName, BindingFlags.NonPublic | BindingFlags.Static);
          if (mi == null)
            throw new ArgumentException(String.Format("RuntimeHelper {0} not resolved.", runtimeHelper));

          runtimeHelperCache[runtimeHelper] = mi;
        }
      return mi;
    } // func GetRuntimeHelper

    internal static ConstructorInfo ResultConstructorInfo
    {
      get
      {
        lock (luaStaticLock)
          if (ciResultConstructor == null)
            ciResultConstructor = typeof(LuaResult).GetConstructor(new Type[] { typeof(object[]) });
        return ciResultConstructor;
      }
    } // prop ResultConstructorInfo

    internal static ConstructorInfo ResultConstructorInfo2
    {
      get
      {
        lock (luaStaticLock)
          if (ciResultConstructor2 == null)
            ciResultConstructor2 = typeof(LuaResult).GetConstructor(new Type[] { typeof(object) });
        return ciResultConstructor2;
      }
    } // prop ResultConstructorInfo

    internal static PropertyInfo ResultIndexPropertyInfo
    {
      get
      {
        lock (luaStaticLock)
          if (piResultIndex == null)
            piResultIndex = typeof(LuaResult).GetProperty("Item", BindingFlags.Public | BindingFlags.GetProperty | BindingFlags.Instance);
        return piResultIndex;
      }
    } // prop ResultIndexPropertyInfo

    internal static PropertyInfo ResultEmptyPropertyInfo
    {
      get
      {
        lock (luaStaticLock)
          if (piResultEmpty == null)
            piResultEmpty = typeof(LuaResult).GetProperty("Empty", BindingFlags.Public | BindingFlags.GetProperty | BindingFlags.Static);
        return piResultEmpty;
      }
    } // prop ResultEmptyPropertyInfo

    internal static PropertyInfo ResultValuesPropertyInfo
    {
      get
      {
        lock (luaStaticLock)
          if (piResultValues == null)
            piResultValues = typeof(LuaResult).GetProperty("Values", BindingFlags.Public | BindingFlags.GetProperty | BindingFlags.Instance);
        return piResultValues;
      }
    } // prop ResultValuesPropertyInfo

    internal static MethodInfo TableSetMethodInfo
    {
      get
      {
        lock (luaStaticLock)
          if (miTableSetMethod == null)
            miTableSetMethod = typeof(LuaTable).GetMethod("SetMethod", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.InvokeMethod);
        return miTableSetMethod;
      }
    } // prop ResultValuesPropertyInfo

    #endregion

    #region -- Enumerator -------------------------------------------------------------

    private readonly static Func<object, object, LuaResult> funcLuaEnumIterator = new Func<object, object, LuaResult>(LuaEnumIteratorImpl);

    private static LuaResult LuaEnumIteratorImpl(object s, object c)
    {
      System.Collections.IEnumerator e = (System.Collections.IEnumerator)s;
      if (e.MoveNext())
        return new LuaResult(e.Current);
      else
        return LuaResult.Empty;
    } // func LuaEnumIteratorImpl

    /// <summary>Convert IEnumerator's to lua enumerator-functions.</summary>
    /// <param name="e"></param>
    /// <returns></returns>
    public static LuaResult GetEnumIteratorResult(System.Collections.IEnumerator e)
    {
      return new LuaResult(funcLuaEnumIterator, e, null);
    } // func GetEnumIteratorResult

    #endregion
  } // class Lua

  #endregion
}
