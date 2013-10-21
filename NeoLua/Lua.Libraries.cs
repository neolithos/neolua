using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;

namespace Neo.IronLua
{
  #region -- class LuaPackageProxy ----------------------------------------------------

  ///////////////////////////////////////////////////////////////////////////////
  /// <summary>Little proxy for static classes that provide Library for Lua</summary>
  internal class LuaPackageProxy : IDynamicMetaObjectProvider
  {
    #region -- class LuaPackageMetaObject ---------------------------------------------

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
        switch (Lua.TryBindGetMember(binder, new DynamicMetaObject(Expression.Default(val.type), BindingRestrictions.Empty, null), out expr))
        {
          case Lua.BindResult.Ok:
            expr = Expression.Convert(expr, typeof(object));
            break;
        }

        return new DynamicMetaObject(expr, BindingRestrictions.GetInstanceRestriction(Expression, Value));
      } // func BindGetMember

      public override DynamicMetaObject BindInvokeMember(InvokeMemberBinder binder, DynamicMetaObject[] args)
      {
        Expression expr;
        Lua.TryBindInvokeMember(binder, false, new DynamicMetaObject(Expression.Default((Type)Value), BindingRestrictions.Empty, null), args, out expr);
        return new DynamicMetaObject(expr, Lua.GetMethodSignatureRestriction(null, args).Merge(BindingRestrictions.GetInstanceRestriction(Expression, Value)));
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

  ///////////////////////////////////////////////////////////////////////////////
  /// <summary>Static libraries for lua</summary>
  public partial class Lua
  {
    #region -- Table Manipulation -----------------------------------------------------

    ///////////////////////////////////////////////////////////////////////////////
    /// <summary></summary>
    private static class LuaLibraryTable
    {
      public static string concat(LuaTable t, string sep = null, int i = 0, int j = int.MaxValue)
      {
        StringBuilder sb = new StringBuilder();

        foreach (var c in t)
        {
          int k = c.Key is int ? (int)c.Key : -1;
          if (k >= i && k <= j)
          {
            if (!String.IsNullOrEmpty(sep) && sb.Length > 0)
              sb.Append(sep);
            sb.Append(c.Value);
          }
        }

        return sb.ToString();
      } // func concat

      public static void insert(LuaTable t, object pos, object value = null)
      {
        // the pos is optional
        if (!(pos is int) && value == null)
        {
          value = pos;
          if (t.Length < 0)
            pos = 0;
          else
            pos = t.Length;
        }

        // insert the value at the position
        int iPos = Convert.ToInt32(pos);
        object c = value;
        while (true)
        {
          if (t[iPos] == null)
          {
            t[iPos] = c;
            break;
          }
          else
          {
            object tmp = t[iPos];
            t[iPos] = c;
            c = tmp;
          }
          iPos++;
        }
      } // proc insert

      public static LuaTable pack(object[] values)
      {
        LuaTable t = new LuaTable();
        for (int i = 0; i < values.Length; i++)
          t[i] = values[i];
        return t;
      } // func pack

      public static void remove(LuaTable t, int pos = -1)
      {
        if (pos == -1)
          pos = t.Length;
        if (pos == -1)
          return;

        while (true)
        {
          if (t[pos] == null)
            break;
          t[pos] = t[pos + 1];
          pos++;
        }
      } // proc remove

      public static void sort(LuaTable t, Delegate sort = null)
      {
        object[] values = unpack(t); // unpack in a normal array

        // sort the array
        if (sort == null)
          Array.Sort(values);
        else
          Array.Sort(values, (a, b) => Convert.ToInt32(Lua.RtGetObject(((Func<object, object, object[]>)sort)(a, b), 0)));

        // copy the values back
        for (int i = 0; i < values.Length; i++)
          t[i] = values[i];

        // remove the overflow
        List<int> removeValues = new List<int>();
        foreach (var c in t)
        {
          int i = c.Key is int ? (int)c.Key : -1;
          if (i >= values.Length)
            removeValues.Add(i);
        }

        for (int i = 0; i < removeValues.Count; i++)
          t[removeValues[i]] = null;
      } // proc sort

      public static object[] unpack(LuaTable t, int i = 0, int j = int.MaxValue)
      {
        List<object> r = new List<object>();

        foreach (var c in t)
        {
          int k = c.Key is int ? (int)c.Key : -1;
          if (k >= i && k <= j)
            r.Add(c.Value);
        }

        return r.ToArray();
      } // func unpack
    } // class LuaLibraryTable

    #endregion

    #region -- Mathematical Functions -------------------------------------------------

    private static class LuaLibraryMath
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

      /// <summary>Returns m and e such that x = m2e, e is an integer and the absolute value of m is in the range [0.5, 1) (or zero when x is zero).</summary>
      /// <param name="x"></param>
      /// <returns></returns>
      public static double frexp(double x)
      {
        throw new NotImplementedException();
      } // func frexp

      // The value HUGE_VAL, a value larger than or equal to any other numerical value.
      public static double huge { get { return double.MaxValue; } }

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

      public static double sin(double x)
      {
        return Math.Sin(x);
      } // func sin

      public static double sinh(double x)
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
    } // clas LuaLibraryMath

    #endregion

    #region -- Operating System Facilities --------------------------------------------

    ///////////////////////////////////////////////////////////////////////////////
    /// <summary></summary>
    private static class LuaLibraryOS
    {
      public static int clock()
      {
        return Environment.TickCount;
      } // func clock
    } // class LuaLibraryOS

    #endregion
  } // class Lua
}
