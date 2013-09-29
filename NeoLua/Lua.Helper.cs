using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using TecWare.Core.Compile;

namespace Neo.IronLua
{
  #region -- class Lua ----------------------------------------------------------------

  ///////////////////////////////////////////////////////////////////////////////
  /// <summary></summary>
  public partial class Lua
  {
    #region -- enum RuntimeHelper -----------------------------------------------------

    internal enum RuntimeHelper
    {
      GetObject,
      Convert,
      /// <summary>Erzeugt das Ergebnis für die Return-Anweisung</summary>
      ReturnResult,
      StringConcat,
      TableSetObjects,
      ConcatArrays
    } // enum RuntimeHelper

    #endregion

    private static Dictionary<RuntimeHelper, MethodInfo> runtimeHelperCache = new Dictionary<RuntimeHelper, MethodInfo>();

    #region -- ParseNumer, ParseHexNumber ---------------------------------------------

    internal static Expression ParseNumber(Token<LuaToken> t)
    {
      int i;
      double d;
      string sNumber = t.Value;
      if (String.IsNullOrEmpty(sNumber))
        return Expression.Constant(0, typeof(int));
      else if (Int32.TryParse(sNumber, NumberStyles.Integer, CultureInfo.InvariantCulture, out i))
        return Expression.Constant(i, typeof(int));
      else if (Double.TryParse(sNumber, NumberStyles.Float, CultureInfo.InvariantCulture, out d))
        return Expression.Constant(d, typeof(double));
      else
        return ThrowExpression(String.Format("Die Zahl '{0}' konnte nicht konvertiert werden.", sNumber));
    } // func ParseNumber

    internal static Expression ParseHexNumber(Token<LuaToken> t)
    {
      int i;
      //double d;
      string sNumber = t.Value;

      if (String.IsNullOrEmpty(sNumber))
        return Expression.Constant(0, typeof(int));
      else
      {
        // Entferne das 0x
        if (sNumber.Length > 2 && sNumber[0] == '0' && (sNumber[1] == 'x' || sNumber[1] == 'X'))
          sNumber = sNumber.Substring(2);

        // Konvertiere die Zahl
        if (Int32.TryParse(sNumber, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out i))
          return Expression.Constant(i, typeof(int));
        // Todo: Binäre Exponente???
        //else if (Double.TryParse(sNumber, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out d))
        //  return Expression.Constant(d, typeof(Double));
        else
          return ThrowExpression(String.Format("Die Zahl '{0}' konnte nicht konvertiert werden.", sNumber));
      }
    } // func ParseHexNumber

    #endregion

    #region -- ThrowExpression --------------------------------------------------------

    private static ConstructorInfo ciLuaException = null;
    internal static Expression ThrowExpression(string sMessage)
    {
      if (ciLuaException == null)
        ciLuaException = typeof(LuaBindException).GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, null, new Type[] { typeof(string), typeof(Exception) }, null);

      return Expression.Throw(
        Expression.New(
          ciLuaException,
          Expression.Constant(sMessage, typeof(string)),
          Expression.Constant(null, typeof(Exception))
        ),
        typeof(object)
      );
    } // func GenerateThrowExpression

    #endregion

    #region -- ConvertExpression ------------------------------------------------------

    /// <summary>Wandelt die Rückgabe in Objekt um. Wird ein Objekt-Array übergeben, so wird der erste Wert genommen oder nil.</summary>
    /// <param name="expr">Expression die gecastet werden soll.</param>
    /// <param name="lConvert">Konvertierung durchführen.</param>
    /// <returns>Expression, die ein Objekt zurück gibt. </returns>
    internal static Expression ToObjectExpression(Expression expr, bool lConvert = true)
    {
      if (expr.Type == typeof(object))
        return expr;
      else if (expr.Type == typeof(object[]) || typeof(object[]).IsAssignableFrom(expr.Type))
      {
        return RuntimeHelperExpression(RuntimeHelper.GetObject, Expression.Convert(expr, typeof(object[])), Expression.Constant(0, typeof(int)));
      }
      else if (lConvert)
        return Expression.Convert(expr, typeof(object));
      else
        return expr;
    } // func ToObjectExpression

    internal static Expression ToBooleanExpression(Expression expr)
    {
      if (expr.Type == typeof(bool) || typeof(bool).IsAssignableFrom(expr.Type))
        return Expression.Convert(expr, typeof(bool));
      else
        return Expression.Dynamic(Lua.ConvertToBooleanBinder, typeof(bool), ToObjectExpression(expr));
    } // func ToBooleanExpression

    /// <summary>Erzeugt ein entsprechendes Convert.</summary>
    /// <param name="expr"></param>
    /// <param name="typeFrom"></param>
    /// <param name="typeTo"></param>
    /// <returns></returns>
    internal static Expression ConvertTypeExpression(Expression expr, Type typeFrom, Type typeTo)
    {
      if (typeFrom == typeTo)
        return Expression.Convert(expr, typeTo);
      else
        return Expression.Convert(
          RuntimeHelperExpression(
            RuntimeHelper.Convert,
            ToObjectExpression(expr),
            Expression.Constant(typeTo, typeof(Type))
          ), 
          typeTo
        );
    } // func ConvertTypeExpression

    private static object RtConvert(object value, Type to)
    {
      if (to == typeof(bool))
        return ConvertToBoolean(value);
      else if (value == null)
        if (to.IsValueType)
          return Activator.CreateInstance(to);
        else
          return null;
      else if (to.IsAssignableFrom(value.GetType()))
        return value;
      else
      {
        TypeConverter conv = TypeDescriptor.GetConverter(to);
        if (value == null)
          throw new ArgumentNullException(); // Todo: LuaException
        else if (conv.CanConvertFrom(value.GetType()))
          return conv.ConvertFrom(null, CultureInfo.InvariantCulture, value);
        else
        {
          conv = TypeDescriptor.GetConverter(value.GetType());
          if (conv.CanConvertTo(to))
            return conv.ConvertTo(null, CultureInfo.InvariantCulture, value, to);
          else
            throw new LuaBindException(String.Format("'{0}' kann nicht in '{1}' konvertiert werden.", value, to.Name), null);
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

    #region -- Runtime Helper ---------------------------------------------------------

    private static MethodInfo GetRuntimeHelper(RuntimeHelper runtimeHelper)
    {
      MethodInfo mi;
      if (!runtimeHelperCache.TryGetValue(runtimeHelper, out mi))
      {
        string sMemberName = "Rt" + runtimeHelper.ToString();

        mi = typeof(Lua).GetMethod(sMemberName, BindingFlags.NonPublic | BindingFlags.Static);
        if (mi == null)
          throw new ArgumentException(String.Format("RuntimeHelper {0} nicht gefunden.", runtimeHelper));

        runtimeHelperCache[runtimeHelper] = mi;
      }
      return mi;
    } // func GetRuntimeHelper

    internal static Expression RuntimeHelperConvertExpression(Expression value, Type toType)
    {
      if (toType.IsAssignableFrom(value.Type))
        return Expression.Convert(value, toType);
      else
        return Expression.Convert(RuntimeHelperExpression(RuntimeHelper.Convert, Expression.Convert(value, typeof(object)), Expression.Constant(toType, typeof(Type))), toType);
    } // func RuntimeHelperConvertExpression

    internal static Expression RuntimeHelperExpression(RuntimeHelper runtimeHelper, params Expression[] args)
    {
      return Expression.Call(GetRuntimeHelper(runtimeHelper), args);
    } // func GetRuntimeHelper

    private static object RtGetObject(object[] values, int i)
    {
      if (values == null)
        return null;
      else if (i < values.Length)
        return values[i];
      else
        return null;
    } // func RtGetObject

    private static object[] RtReturnResult(object[] objects)
    {
      // Gibt es ein Ergebnis
      if (objects == null || objects.Length == 0)
        return objects;
      else if (objects[objects.Length - 1] is object[]) // Ist das letzte Ergebnis ein Objekt-Array
      {
        object[] l = (object[])objects[objects.Length - 1];
        object[] n = new object[objects.Length - 1 + l.Length];

        // Kopiere die ersten Ergebnisse
        for (int i = 0; i < objects.Length - 1; i++)
          if (objects[i] is object[])
          {
            object[] t = (object[])objects[i];
            n[i] = t == null || t.Length == 0 ? null : t[0];
          }
          else
            n[i] = objects[i];

        // Füge die vom letzten Result an
        for (int i = 0; i < l.Length; i++)
          n[i + objects.Length - 1] = l[i];

        return n;
      }
      else
      {
        for (int i = 0; i < objects.Length; i++)
          if (objects[i] is object[])
          {
            object[] t = (object[])objects[i];
            objects[i] = t == null || t.Length == 0 ? null : t[0];
          }
        return objects;
      }
    } // func RtReturnResult

    private static object RtStringConcat(string[] strings)
    {
      return String.Concat(strings);
    } // func RtStringConcat

    private static Array RtConcatArrays(Type elementType, Array a, Array b, int iStartIndex)
    {
      int iCountB = b.Length - iStartIndex;

      Array r = Array.CreateInstance(elementType, a.Length + iCountB);
      if (a.Length > 0)
        Array.Copy(a, r, a.Length);
      if (iStartIndex < b.Length)
        Array.Copy(b, iStartIndex, r, a.Length, iCountB);
     
      return r;
    } // func RtConcatArrays

    #endregion

    #region-- Table Objects -----------------------------------------------------------

    private static object RtTableSetObjects(LuaTable t, object value, int iStartIndex)
    {
      if (value != null && (value is object[] || typeof(object[]).IsAssignableFrom(value.GetType())))
      {
        object[] v = (object[])value;

        for (int i = 0; i < v.Length; i++)
          t.SetValue(iStartIndex++, v[i]);
      }
      else
        t.SetValue(iStartIndex, value);
      return t;
    } // func RtTableSetObjects

    internal static Expression CreateEmptyTable()
    {
      return Expression.New(typeof(LuaTable));
    } // func CreateEmptyTable

    #endregion
  } // class Lua

  #endregion
}
