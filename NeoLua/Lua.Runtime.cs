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
    private static ConstructorInfo ciResultConstructorArg1 = null;
    private static ConstructorInfo ciResultConstructorArgN = null;
    private static PropertyInfo piResultIndex = null;
    private static PropertyInfo piResultValues = null;
    private static PropertyInfo piResultEmpty = null;
    private static MethodInfo miTableSetMethod = null;
    private static MethodInfo miParseNumber = null;
    private static MethodInfo miConvertValue = null;
    private static MethodInfo miEquals = null;
    private static MethodInfo miReferenceEquals = null;
    private static MethodInfo miToString = null;
    private static FieldInfo fiStringEmpty = null;
    private static PropertyInfo piCultureInvariant = null;
    private static MethodInfo miRuntimeLength = null;

    #region -- sctor ------------------------------------------------------------------

    static Lua()
    {
      ciResultConstructorArg1 = typeof(LuaResult).GetConstructor(new Type[] { typeof(object) });
      ciResultConstructorArgN = typeof(LuaResult).GetConstructor(new Type[] { typeof(object[]) });
      piResultIndex = typeof(LuaResult).GetProperty("Item", BindingFlags.Public | BindingFlags.GetProperty | BindingFlags.Instance);
      piResultEmpty = typeof(LuaResult).GetProperty("Empty", BindingFlags.Public | BindingFlags.GetProperty | BindingFlags.Static);
      piResultValues = typeof(LuaResult).GetProperty("Values", BindingFlags.Public | BindingFlags.GetProperty | BindingFlags.Instance);
      
      miTableSetMethod = typeof(LuaTable).GetMethod("SetMethod", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.InvokeMethod);
      
      miConvertValue = typeof(Parser).GetMethod("ConvertValue", BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.InvokeMethod, null, new Type[] { typeof(object), typeof(Type) }, null);
      
      miParseNumber = typeof(Lua).GetMethod("ParseNumber", BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.InvokeMethod);
      miRuntimeLength = typeof(Lua).GetMethod("RtLength", BindingFlags.Public | BindingFlags.Static | BindingFlags.InvokeMethod);

      miEquals = typeof(Object).GetMethod("Equals", BindingFlags.Public | BindingFlags.Static | BindingFlags.InvokeMethod);
      miReferenceEquals = typeof(Object).GetMethod("ReferenceEquals", BindingFlags.Public | BindingFlags.Static | BindingFlags.InvokeMethod);
      
      miToString = typeof(Convert).GetMethod("ToString", BindingFlags.Public | BindingFlags.Static | BindingFlags.InvokeMethod, null, new Type[] { typeof(object), typeof(CultureInfo) }, null);

      fiStringEmpty = typeof(String).GetField("Empty", BindingFlags.Public | BindingFlags.Static | BindingFlags.GetField);
      piCultureInvariant = typeof(CultureInfo).GetProperty("InvariantCulture", BindingFlags.Public | BindingFlags.Static | BindingFlags.GetProperty);
      
#if DEBUG
      if (ciResultConstructorArg1 == null ||
          ciResultConstructorArgN == null ||
          piResultIndex == null ||
          piResultEmpty == null ||
          piResultValues == null ||
          miTableSetMethod == null ||
          miParseNumber == null ||
          miConvertValue == null ||
          miEquals == null ||
          miReferenceEquals == null ||
          miToString == null ||
          fiStringEmpty == null ||
          piCultureInvariant == null ||
          miRuntimeLength == null)
        throw new ArgumentNullException();
#endif
    } // sctor

    #endregion

    #region -- RtLength ---------------------------------------------------------------

    /// <summary>Get's the length of an value.</summary>
    /// <param name="v">Value</param>
    /// <returns>Length of the value or 0.</returns>
    public static int RtLength(object v)
    {
      if (v == null)
        return 0;
      else if (v is LuaTable)
        return ((LuaTable)v).Length;
      else if (v is LuaFile)
        return unchecked((int)((LuaFile)v).Length);
      else if (v is String)
        return ((String)v).Length;
      else if (v is System.IO.Stream)
        return unchecked((int)((System.IO.Stream)v).Length);
      else if (v is System.Collections.ICollection)
        return ((System.Collections.ICollection)v).Count;
      else
      {
        Type t = v.GetType();
        PropertyInfo  pi;

        // search for a generic collection
        foreach (Type tInterface in t.GetInterfaces())
          if (tInterface.IsGenericType && tInterface.GetGenericTypeDefinition() == typeof(ICollection<>))
          {
            pi = tInterface.GetProperty("Count");
            return (int)pi.GetValue(v, null);
          }

        // try find a Length or Count property
        pi = t.GetProperty("Count", BindingFlags.Public | BindingFlags.Instance | BindingFlags.GetProperty, null, typeof(int), new Type[0], null);
        if (pi != null)
          return (int)pi.GetValue(v, null);
        pi = t.GetProperty("Length", BindingFlags.Public | BindingFlags.Instance | BindingFlags.GetProperty, null, typeof(int), new Type[0], null);
        if (pi != null)
          return (int)pi.GetValue(v, null);
        
        return 0;
      }
    } // func RtLength

    #endregion

    #region -- RtConcatArrays, RtStringConcat -----------------------------------------

    internal static Array RtConcatArrays(Type elementType, Array a, Array b, int iStartIndex)
    {
      int iCountB = b.Length - iStartIndex;

      Array r = Array.CreateInstance(elementType, a.Length + iCountB);
      if (a.Length > 0)
        Array.Copy(a, r, a.Length);
      if (iStartIndex < b.Length)
        for (int i = 0; i < iCountB; i++)
          r.SetValue(Parser.ConvertValue(b.GetValue(i + iStartIndex), elementType), i + a.Length);

      return r;
    } // func RtConcatArrays

    internal static object RtStringConcat(string[] strings)
    {
      return String.Concat(strings);
    } // func RtStringConcat

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

    internal static ConstructorInfo ResultConstructorInfoArg1 { get { return ciResultConstructorArg1; } }
    internal static ConstructorInfo ResultConstructorInfoArgN { get { return ciResultConstructorArgN; } }
    internal static PropertyInfo ResultIndexPropertyInfo { get { return piResultIndex; } }
    internal static PropertyInfo ResultEmptyPropertyInfo { get { return piResultEmpty; } }
    internal static PropertyInfo ResultValuesPropertyInfo { get { return piResultValues; } }
    internal static MethodInfo TableSetMethodInfo { get { return miTableSetMethod; } }
    internal static MethodInfo ParserNumberMethodInfo { get { return miParseNumber; } }
    internal static MethodInfo ConvertValueMethodInfo { get { return miConvertValue; } }
    internal static MethodInfo ObjectEqualsMethodInfo { get { return miEquals; } }
    internal static MethodInfo ObjectReferenceEqualsMethodInfo { get { return miReferenceEquals; } }
    internal static MethodInfo ConvertToStringMethodInfo { get { return miToString; } }
    internal static FieldInfo StringEmptyFieldInfo { get { return fiStringEmpty; } }
    internal static PropertyInfo CultureInvariantPropertyInfo { get { return piCultureInvariant; } }
    internal static MethodInfo RtLengthMethodInfo { get { return miRuntimeLength; } }

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
