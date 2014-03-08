using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

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
        Lua.TryBindInvokeMember(binder, new DynamicMetaObject(Expression.Default((Type)Value), BindingRestrictions.Empty, null), args, out expr);
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
    #region -- String Manipulation ----------------------------------------------------

    ///////////////////////////////////////////////////////////////////////////////
    /// <summary></summary>
    private static class LuaLibraryString
    {
      private static string TranslateRegularExpression(string sRegEx)
      {
        StringBuilder sb = new StringBuilder();
        bool lEscape = false;

        for (int i = 0; i < sRegEx.Length; i++)
        {
          char c = sRegEx[i];
          if (lEscape)
          {
            if (c == '%')
            {
              sb.Append('%');
              lEscape = false;
            }
            else
            {
              switch (c)
              {
                case 'a': // all letters
                  sb.Append("[\\w-[\\d]]");
                  break;
                case 's': // all space characters
                  sb.Append("\\s");
                  break;
                case 'd': // all digits
                  sb.Append("\\d");
                  break;
                case 'w': // all alphanumeric characters
                  sb.Append("\\w");
                  break;
                case 'c': // all control characters
                case 'g': // all printable characters except space
                case 'l': // all lowercase letters
                case 'p': // all punctuation characters
                case 'u': // all uppercase letters
                case 'x': // all hexadecimal digits
                  throw new NotImplementedException();
                default:
                  sb.Append('\\');
                  sb.Append(c);
                  break;
              }
              lEscape = false;
            }
          }
          else if (c == '%')
          {
            lEscape = true;
          }
          else if (c == '\\')
          {
            sb.Append("\\\\");
          }
          else
            sb.Append(c);
        }

        return sb.ToString();
      } // func TranslateRegularExpression

      public static LuaResult @byte(string s, int i = 1, int j = int.MaxValue)
      {
        if(String.IsNullOrEmpty(s) || i > j)
          return LuaResult.Empty;

        if (i < 1)
          i = 1; // default for i is 1
        if (j == int.MaxValue)
          j = i; // default for j is i
        else if(j > s.Length)
          j = s.Length; // correct the length

        int iLen = j - i + 1; // how many chars to we need

        object[] r = new object[iLen];
        for (int a = 0; a < iLen; a++)
          r[a] = (int)s[i + a - 1];

        return r;
      } // func byte

      public static string @char(params int[] chars)
      {
        if(chars == null)
          return String.Empty;

        StringBuilder sb = new StringBuilder(chars.Length);
        for (int i = 0; i < chars.Length; i++)
          sb.Append((char)chars[i]);

        return sb.ToString();
      } // func char

      public static string dump(Delegate dlg)
      {
        throw new NotImplementedException();
      } // func dump

      public static LuaResult find(string s, string pattern, int init = 1, bool plain = false)
      {
        if (String.IsNullOrEmpty(s))
          return LuaResult.Empty;
        if (String.IsNullOrEmpty(pattern))
          return LuaResult.Empty;

        // correct the init parameter
        if (init < 0)
          init = s.Length + init + 1;
        if (init <= 0)
          init = 1;
        
        if (plain) // plain pattern
        {
          int iIndex = s.IndexOf(pattern, init - 1);
          return new LuaResult(iIndex + 1, iIndex + pattern.Length);
        }
        else
        {
          // translate the regular expression
          pattern = TranslateRegularExpression(pattern);

          Regex r = new Regex(pattern);
          Match m =  r.Match(s, init);
          if (m.Success)
          {
            object[] result = new object[m.Captures.Count + 2];

            result[0] = m.Index + 1;
            result[1] = m.Index + m.Length;
            for (int i = 0; i < m.Captures.Count; i++)
              result[i + 2] = m.Captures[i].Value;

            return result;
          }
          else
            return new LuaResult(0);
        }
      } // func find

      public static string format(string formatstring, params object[] args)
      {
        return AT.MIN.Tools.sprintf(formatstring, args);
      } // func format

      private static LuaResult matchEnum(object s, object current)
      {
        System.Collections.IEnumerator e = (System.Collections.IEnumerator)s;

        // return value
        if (e.MoveNext())
        {
          Match m = (Match)e.Current;
          return MatchResult(m);
        }
        else
          return LuaResult.Empty;
      } // func matchEnum

      public static LuaResult gmatch(string s, string pattern)
      {
        // f,s,v
        if (String.IsNullOrEmpty(s))
          return LuaResult.Empty;
        if (String.IsNullOrEmpty(pattern))
          return LuaResult.Empty;

        // translate the regular expression
        pattern = TranslateRegularExpression(pattern);

        Regex r = new Regex(pattern);
        MatchCollection m = r.Matches(s);
        System.Collections.IEnumerator e = m.GetEnumerator();

        return new LuaResult(new Func<object, object, LuaResult>(matchEnum), e, e);
      } // func gmatch

      public static string gsub(string s, string pattern, string repl, int n)
      {
        throw new NotImplementedException();
      } // func gsub

      public static int len(string s)
      {
        return s == null ? 0 : s.Length;
      } // func len

      public static string lower(string s)
      {
        if (String.IsNullOrEmpty(s))
          return s;
        return s.ToLower();
      } // func lower

      public static LuaResult match(string s, string pattern, int init = 1)
      {
        if (String.IsNullOrEmpty(s))
          return LuaResult.Empty;
        if (String.IsNullOrEmpty(pattern))
          return LuaResult.Empty;

        // correct the init parameter
        if (init < 0)
          init = s.Length + init + 1;
        if (init <= 0)
          init = 1;

        // translate the regular expression
        pattern = TranslateRegularExpression(pattern);

        Regex r = new Regex(pattern);
        return MatchResult(r.Match(s, init));
      } // func match

      private static LuaResult MatchResult(Match m)
      {
        if (m.Success)
        {
          object[] result = new object[m.Captures.Count];

          for (int i = 0; i < m.Captures.Count; i++)
            result[i] = m.Captures[i].Value;

          return result;
        }
        else
          return LuaResult.Empty;
      } // func MatchResult

      public static string rep(string s, int n, string sep = "")
      {
        if (String.IsNullOrEmpty(s) || n == 0)
          return s;
        return String.Join(sep, Enumerable.Repeat(s, n));
      } // func rep

      public static string reverse(string s)
      {
        if(String.IsNullOrEmpty(s) || s.Length == 1)
          return s;

        char[] a = s.ToCharArray();
        Array.Reverse(a);
        return new string(a);
      } // func reverse

      public static string sub(string s, int i, int j = -1)
      {
        if (String.IsNullOrEmpty(s) || j == 0)
          return String.Empty;

        if (i == 0)
          i = 1;

        int iStart;
        int iLen;
        if (i < 0) // Suffix mode
        {
          iStart = s.Length + i;
          if (iStart < 0)
            iStart = 0;
          iLen = (j < 0 ? s.Length + j + 1 : j) - iStart;
        }
        else // Prefix mode
        {
          iStart = i - 1;
          if (j < 0)
            j = s.Length + j + 1;
          iLen = j - iStart;
        }

        // correct the length
        if (iStart + iLen > s.Length)
          iLen = s.Length - iStart;

        // return the string
        if (iLen <= 0)
          return String.Empty;
        else
          return s.Substring(iStart, iLen);
      } // func sub

      public static string upper(string s)
      {
        if (String.IsNullOrEmpty(s))
          return s;
        return s.ToUpper();
      } // func lower

    } // class LuaLibraryString

    #endregion

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
          Array.Sort(values, (a, b) => ((Func<object, object, LuaResult>)sort)(a, b).ToInt32());

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

      public static LuaResult unpack(LuaTable t, int i = 0, int j = int.MaxValue)
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

      public static LuaResult modf(double x)
      {
        if (x < 0)
        {
          double y = Math.Ceiling(x);
          return new LuaResult(y, y - x);
        }
        else
        {
          double y = Math.Floor(x);
          return new LuaResult(y, x - y);
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

    #region -- Bitwise Operations -----------------------------------------------------

    ///////////////////////////////////////////////////////////////////////////////
    /// <summary></summary>
    private static class LuaLibraryBit32
    {
      public static int arshift(int x, int disp)
      {
        if (disp < 0)
          return unchecked(x << -disp);
        else if (disp > 0)
          return unchecked(x >> disp);
        else
          return x;
      } // func arshift

      public static uint band(params uint[] ands)
      {
        uint r = 0;
        if (ands != null)
          for (int i = 0; i < ands.Length; i++)
            r &= ands[i];
        return r;
      } // func band

      public static uint bnot(uint x)
      {
        return ~x;
      } // func bnot

      public static uint bor(params uint[] ors)
      {
        uint r = 0;
        if (ors != null)
          for (int i = 0; i < ors.Length; i++)
            r |= ors[i];
        return r;
      } // func bor

      public static bool btest(params uint[] tests)
      {
        return band(tests) != 0;
      } // func btest

      public static uint bxor(params uint[] xors)
      {
        uint r = 0;
        if (xors != null)
          for (int i = 0; i < xors.Length; i++)
            r ^= xors[i];
        return r;
      } // func bxor

      public static int extract(uint n, int field, int width = 1)
      {
        uint m = unchecked(((uint)1 << width) - 1);
        uint neg = (uint)1 << (width - 1);
        n = n >> field;
        uint v = n & m;

        if ((v & neg) != 0)
          return (int)-(~v & m) - 1;
        else
          return (int)v;
      } // func extract

      public static uint replace(uint n, int v, int field, int width = 1)
      {
        uint m = unchecked(((uint)1 << width - 1) - 1);
        uint r;
        if (v < 0)
          r = unchecked((((uint)(~(-v) + 1) & m) | ((uint)1 << (width - 1))) << field);
        else
          r = unchecked(((uint)v & m) << field);

        m = unchecked((((uint)1 << width) - 1) << field);
        return (n & ~m) | r;
      } // func replace

      public static uint lrotate(uint x, int disp)
      {
        return unchecked(x << disp | x >> (32 - disp));
      } // func lrotate

      public static uint lshift(uint x, int disp)
      {
        if (disp < 0)
          return rshift(x, -disp);
        else if (disp > 0)
          return x << disp;
        else
          return x;
      } // func lshift

      public static uint rrotate(uint x, int disp)
      {
        return unchecked(x >> disp | x << (32 - disp));
      } // func rrotate

      public static uint rshift(uint x, int disp)
      {
        if (disp < 0)
          return lshift(x, -disp);
        else if (disp > 0)
          return x >> disp;
        else
          return x;
      } // func rshift
    } // class LuaLibraryBit32

    #endregion

    #region -- Input and Output Facilities --------------------------------------------

    ///////////////////////////////////////////////////////////////////////////////
    /// <summary>default files are not supported.</summary>
    private static class LuaLibraryIO
    {
      private static LuaFile defaultOutput = null;
      private static LuaFile defaultInput = null;
      private static LuaFile tempFile = null;

      public static LuaResult open(string filename, string mode = "r")
      {
        try
        {
          return new LuaResult(new LuaFile(filename, mode));
        }
        catch (Exception e)
        {
          return new LuaResult(null, e.Message);
        }
      } // func open

      public static LuaResult lines(object[] args)
      {
        if (args == null || args.Length == 0)
          return defaultInput.lines(null);
        else
          return Lua.GetEnumIteratorResult(new LuaLinesEnumerator(new LuaFile((string)args[0], "r"), true, args, 1));
      } // func lines

      public static LuaResult close(LuaFile file = null)
      {
        if (file != null)
          return file.close();
        else if (defaultOutput != null)
        {
          LuaResult r = defaultOutput.close();
          defaultOutput = null;
          return r;
        }
        else
          return null;
      } // proc close

      public static LuaFile input(object file = null)
      {
        if (file is string)
        {
          if (defaultInput != null)
            defaultInput.close();
          defaultInput = new LuaFile((string)file, "r");

          return defaultInput;
        }
        else if (file is LuaFile)
        {
          if (defaultInput != null)
            defaultInput.close();
          defaultInput = (LuaFile)file;
        }
        return defaultInput;
      } // proc input

      public static LuaFile output(object file = null)
      {
        if (file is string)
        {
          if (defaultOutput != null)
            defaultOutput.close();
          defaultOutput = new LuaFile((string)file, "w");

          return defaultOutput;
        }
        else if (file is LuaFile)
        {
          if (defaultOutput != null)
            defaultOutput.close();
          defaultOutput = (LuaFile)file;
        }
        return defaultOutput;
      } // proc output

      public static void flush()
      {
        if (defaultOutput != null)
          defaultOutput.flush();
      } // proc flush

      public static LuaResult read(object[] args)
      {
        return defaultInput == null ? LuaResult.Empty : defaultInput.read(args);
      } // proc read

      public static LuaResult write(object [] args)
      {
        return defaultOutput == null ? LuaResult.Empty : defaultOutput.write(args);
      } // proc write

      public static LuaFile tmpfile()
      {
        if (tempFile == null)
          tempFile = tmpfilenew();
        return tempFile;
      } // func read

      public static LuaFile tmpfilenew()
      {
        return new LuaTempFile(Path.GetTempFileName());
      } // func read

      public static string type(object obj)
      {
        if (obj is LuaFile && !((LuaFile)obj).IsClosed)
          return "file";
        else
          return "file closed";
      } // func type

      public static LuaFile popen(string program, string mode = "r")
      {
        ProcessStartInfo psi = new ProcessStartInfo(null, program);
        psi.RedirectStandardOutput = mode.IndexOf('r') >= 0;
        psi.RedirectStandardInput = mode.IndexOf('w') >= 0;
        psi.UseShellExecute = false;
        psi.CreateNoWindow = true;
        return new LuaFile(Process.Start(psi));
      } // func popen
    } // class LuaLibraryOS

    #endregion

    #region -- Operating System Facilities --------------------------------------------

    ///////////////////////////////////////////////////////////////////////////////
    /// <summary></summary>
    private static class LuaLibraryOS
    {
      public static LuaResult clock()
      {
        return new LuaResult(Process.GetCurrentProcess().TotalProcessorTime.TotalSeconds);
      } // func clock

      public static object date(string format, string time)
      {
        throw new NotImplementedException();
      } // func date

      public static int difftime(object t2, object t1)
      {
        throw new NotImplementedException();
      } // func difftime

      public static LuaResult execute(string command)
      {
        if (command == null)
          return new LuaResult(true);
        try
        {
          using (Process p = Process.Start(null, command))
          {
            p.WaitForExit();
            return new LuaResult(true, "exit", p.ExitCode);
          }
        }
        catch (Exception e)
        {
          return new LuaResult(null, e.Message);
        }
      } // func execute

      public static void exit(int code = 0, bool close = true)
      {
        Environment.Exit(code);
      } // func exit

      public static string getenv(string varname)
      {
        return Environment.GetEnvironmentVariable(varname);
      } // func getenv

      public static LuaResult remove(string filename)
      {
        try
        {
          File.Delete(filename);
          return new LuaResult(true);
        }
        catch (Exception e)
        {
          return new LuaResult(null, e.Message);
        }
      } // func remove

      public static LuaResult rename(string oldname, string newname)
      {
        try
        {
          File.Move(oldname, newname);
          return new LuaResult(true);
        }
        catch (Exception e)
        {
          return new LuaResult(null, e.Message);
        }
      } // func rename

      public static void setlocale()
      {
        throw new NotImplementedException();
      } // func setlocale

      public static object time(LuaTable table)
      {
        throw new NotImplementedException();
      } // func time

      public static string tmpname()
      {
        return Path.GetTempFileName();
      } // func tmpname
    } // class LuaLibraryOS

    #endregion

    // -- Static --------------------------------------------------------------

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
   
    public static LuaResult GetEnumIteratorResult(System.Collections.IEnumerator e)
    {
      return new LuaResult(funcLuaEnumIterator, e, null);
    } // func GetEnumIteratorResult
  } // class Lua
}
