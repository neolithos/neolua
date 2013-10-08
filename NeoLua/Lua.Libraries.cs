using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;

namespace Neo.IronLua
{
  public partial class Lua
  {
    #region -- String Manipulation ----------------------------------------------------

    ///////////////////////////////////////////////////////////////////////////////
    /// <summary></summary>
    private static class LuaLibraryString
    {

    } // class LuaLibraryString

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
