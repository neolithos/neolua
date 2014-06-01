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
  #region -- String Manipulation ------------------------------------------------------

  ///////////////////////////////////////////////////////////////////////////////
  /// <summary></summary>
  internal static class LuaLibraryString
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
      if (String.IsNullOrEmpty(s) || i > j)
        return LuaResult.Empty;

      if (i < 1)
        i = 1; // default for i is 1
      if (j == int.MaxValue)
        j = i; // default for j is i
      else if (j > s.Length)
        j = s.Length; // correct the length

      int iLen = j - i + 1; // how many chars to we need

      object[] r = new object[iLen];
      for (int a = 0; a < iLen; a++)
        r[a] = (int)s[i + a - 1];

      return r;
    } // func byte

    public static string @char(params int[] chars)
    {
      if (chars == null)
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
        Match m = r.Match(s, init);
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
      if (String.IsNullOrEmpty(s) || s.Length == 1)
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

    // todo: packfloat
    // todo: packint
    // todo: unpackfloat
    // todo: unpackint
  } // class LuaLibraryString

  #endregion

  #region -- Mathematical Functions ---------------------------------------------------

  internal static class LuaLibraryMath
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

    // todo: type

    public static double pi { get { return Math.PI; } }
    public static double e { get { return Math.E; } }
  } // clas LuaLibraryMath

  #endregion

  #region -- Bitwise Operations -------------------------------------------------------

  ///////////////////////////////////////////////////////////////////////////////
  /// <summary></summary>
  internal static class LuaLibraryBit32
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

  #region -- Operating System Facilities ----------------------------------------------

  ///////////////////////////////////////////////////////////////////////////////
  /// <summary></summary>
  internal static class LuaLibraryOS
  {
    private static readonly DateTime dtUnixStartTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
    
    public static LuaResult clock()
    {
      return new LuaResult(Process.GetCurrentProcess().TotalProcessorTime.TotalSeconds);
    } // func clock

    /// <summary>Converts a number representing the date and time back to some higher-level representation.</summary>
    /// <param name="format">Format string. Same format as the C <see href="http://www.cplusplus.com/reference/ctime/strftime/">strftime()</see> function.</param>
    /// <param name="time">Numeric date-time. It defaults to the current date and time.</param>
    /// <returns>Formatted date string, or table of time information.</returns>
    /// <remarks>by PapyRef</remarks>
    public static object date(string format, object time)
    {
      // Unix timestamp is seconds past epoch. Epoch date for time_t is 00:00:00 UTC, January 1, 1970.
      DateTime dt;

      var firstC = string.IsNullOrEmpty(format) ? (char?)null : format[0];
      if (firstC == '!')
      {
        // Date and time expressed as coordinated universal time (UTC).
        dt = time == null ? DateTime.UtcNow : dtUnixStartTime.AddSeconds((int)time).ToUniversalTime();
        format = format.Substring(1);
      }
      else
      {
        // Date and time adjusted to the local time zone.
        dt = time == null ? DateTime.Now : dtUnixStartTime.AddSeconds((int)time).ToLocalTime(); 
      }

      if (String.Compare(format, "*t", false) == 0)
      {
        LuaTable lt = new LuaTable();
        lt["year"] = dt.Year;
        lt["month"] = dt.Month;
        lt["day"] = dt.Day;
        lt["hour"] = dt.Hour;
        lt["min"] = dt.Minute;
        lt["sec"] = dt.Second;
        lt["wday"] = (int)dt.DayOfWeek;
        lt["yday"] = dt.DayOfYear;
        lt["isdst"] = (dt.Kind == DateTimeKind.Local ? true : false);
        return lt;
      }
      else
        return AT.MIN.Tools.ToStrFTime(dt, format);
    } // func date

    /// <summary>Calculate the current date and time, coded as a number. That number is the number of seconds since 
    /// Epoch date, that is 00:00:00 UTC, January 1, 1970. When called with a table, it returns the number representing 
    /// the date and time described by the table.</summary>
    /// <param name="table">Table representing the date and time</param>
    /// <returns>The time in system seconds. </returns>
    /// <remarks>by PapyRef</remarks>
    public static LuaResult time(LuaTable table)
    {
      TimeSpan ts;

      if (table == null)
      {
        // Returns the current time when called without arguments
        ts = DateTime.Now.Subtract(dtUnixStartTime);  //DateTime.UtcNow.Subtract(unixStartTime);
      }
      else
      {
        try
        {
          ts = datetime(table).Subtract(dtUnixStartTime);
        }
        catch (Exception e)
        {
          return new LuaResult(null, e.Message);
        }
      }

      return new LuaResult(Convert.ToInt64(ts.TotalSeconds));
    } // func time

    /// <summary>Converts a time to a .net DateTime</summary>
    /// <param name="time"></param>
    /// <returns></returns>
    public static DateTime datetime(object time)
    {
      if (time is LuaTable)
      {
        LuaTable table = (LuaTable)time;
        return new DateTime(
          table.ContainsKey("year") ? (int)table["year"] < 1970 ? 1970 : (int)table["year"] : 1970,
          table.ContainsKey("month") ? (int)table["month"] : 1,
          table.ContainsKey("day") ? (int)table["day"] : 1,
          table.ContainsKey("hour") ? (int)table["hour"] : 0,
          table.ContainsKey("min") ? (int)table["min"] : 0,
          table.ContainsKey("sec") ? (int)table["sec"] : 0,
          table.ContainsKey("isdst") ? (table.ContainsKey("isdst") == true) ? DateTimeKind.Local : DateTimeKind.Utc : DateTimeKind.Local
        );
      }
      else if (time is int)
        return dtUnixStartTime.AddSeconds((int)time);
      else if (time is double)
        return dtUnixStartTime.AddSeconds((double)time);
      else
        throw new ArgumentException();
    } // func datetime

    /// <summary>Calculate the number of seconds between time t1 to time t2.</summary>
    /// <param name="t2">Higher bound of the time interval whose length is calculated.</param>
    /// <param name="t1">Lower bound of the time interval whose length is calculated. If this describes a time point later than end, the result is negative.</param>
    /// <returns>The number of seconds from time t1 to time t2. In other words, the result is t2 - t1.</returns>
    /// <remarks>by PapyRef</remarks>
    public static long difftime(object t2, object t1)
    {
      long time2 = Convert.ToInt64(t2 is LuaTable ? time((LuaTable)t2)[0] : t2);
      long time1 = Convert.ToInt64(t1 is LuaTable ? time((LuaTable)t1)[0] : t1);

      return time2 - time1;
    } // func difftime

		internal static void SplitCommand(string command, out string sFileName, out string sArguments)
		{
			// check the parameter
			if (command == null)
				throw new ArgumentNullException("command");
			command = command.Trim();
			if (command.Length == 0)
				throw new ArgumentNullException("command");

			// split the command
			if (command[0] == '"')
			{
				int iPos = command.IndexOf('"', 1);
				if (iPos == -1)
				{
					sFileName = command;
					sArguments = null;
				}
				else
				{
					sFileName = command.Substring(1, iPos - 1).Trim();
					sArguments = command.Substring(iPos + 1).Trim();
				}
			}
			else
			{
				sFileName = Path.Combine(Environment.SystemDirectory, "cmd.exe");
				sArguments = "/c " + command;
			}
		} // proc SplitCommand

		public static LuaResult execute(string command, Func<string, LuaResult> output, Func<string, LuaResult> error)
    {
      if (command == null)
        return new LuaResult(true);
      try
      {
				string sFileName;
				string sArguments;
				SplitCommand(command, out sFileName, out sArguments);
        using (Process p = Process.Start(sFileName, sArguments))
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

    public static string tmpname()
    {
      return Path.GetTempFileName();
    } // func tmpname
  } // class LuaLibraryOS

  #endregion
}
