using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
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
		private static void NormalizeStringArguments(string s, int i, int j, out int iStart, out int iLen)
		{
			if (i == 0)
				i = 1;
			
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
		} // proc NormalizeStringArguments

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

    public static LuaResult @byte(this string s, Nullable<int> i = null, Nullable<int> j  = null)
    {
			if (!i.HasValue)
				i = 1;
			if (!j.HasValue)
				j = i;

      if (String.IsNullOrEmpty(s) || i == 0)
        return LuaResult.Empty;

			int iStart;
			int iLen;
			NormalizeStringArguments(s, i.Value, j.Value, out iStart, out iLen);
			if (iLen <= 0)
				return LuaResult.Empty;

      object[] r = new object[iLen];
      for (int a = 0; a < iLen; a++)
        r[a] = (int)s[iStart + a];

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

		public static LuaResult find(this string s, string pattern, int init = 1, bool plain = false)
    {
			if (String.IsNullOrEmpty(s))
			{
				if (String.IsNullOrEmpty(pattern) && init == 1)
					return new LuaResult(1);
				else
					return LuaResult.Empty;
			}
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
        Match m = r.Match(s, init - 1);
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
          return LuaResult.Empty;
      }
    } // func find

		public static string format(this string formatstring, params object[] args)
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

		public static LuaResult gmatch(this string s, string pattern)
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

		public static string gsub(this string s, string pattern, string repl, int n)
    {
      throw new NotImplementedException();
    } // func gsub

		public static int len(this string s)
    {
      return s == null ? 0 : s.Length;
    } // func len

		public static string lower(this string s)
    {
      if (String.IsNullOrEmpty(s))
        return s;
      return s.ToLower();
    } // func lower

		public static LuaResult match(this string s, string pattern, int init = 1)
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

		public static string rep(this string s, int n, string sep = "")
    {
      if (n == 0)
        return String.Empty;
      return String.Join(sep, Enumerable.Repeat(s, n));
    } // func rep

		public static string reverse(this string s)
    {
      if (String.IsNullOrEmpty(s) || s.Length == 1)
        return s;

      char[] a = s.ToCharArray();
      Array.Reverse(a);
      return new string(a);
    } // func reverse

		public static string sub(this string s, int i, int j = -1)
    {
      if (String.IsNullOrEmpty(s) || j == 0)
        return String.Empty;
		
			int iStart;
			int iLen;
			NormalizeStringArguments(s, i, j, out iStart, out iLen);

      // return the string
      if (iLen <= 0)
        return String.Empty;
      else
        return s.Substring(iStart, iLen);
    } // func sub

		public static string upper(this string s)
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
			if (x == null || x.Length == 0)
				throw new LuaRuntimeException(Properties.Resources.rsNumberExpected, 1, true);

      double r = Double.MinValue;
      for (int i = 0; i < x.Length; i++)
        if (r < x[i])
          r = x[i];

      return r;
    } // func max

    public static double min(double[] x)
    {
			if (x == null || x.Length == 0)
				throw new LuaRuntimeException(Properties.Resources.rsNumberExpected, 1, true);

      double r = Double.MaxValue;
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

    public static void randomseed(object x)
    {
			int seed ;
			if (x == null)
				seed = Environment.TickCount;
			else
			{
				TypeCode tc = Type.GetTypeCode(x.GetType());
				if (tc >= TypeCode.Byte && tc <= TypeCode.Decimal)
					seed = Convert.ToInt32(x);
				else if (tc == TypeCode.DateTime)
					seed = unchecked((int)((DateTime)x).ToFileTime());
				else
					seed = x.GetHashCode();
			}
      rand = new Random(seed);
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

		public static string type(object x)
		{
			if (x == null)
				return null;
			else
			{
				switch (Type.GetTypeCode(x.GetType()))
				{
					case TypeCode.Byte:
					case TypeCode.SByte:
					case TypeCode.Int16:
					case TypeCode.UInt16:
					case TypeCode.Int32:
					case TypeCode.UInt32:
					case TypeCode.Int64:
					case TypeCode.UInt64:
						return "integer";
					case TypeCode.Double:
					case TypeCode.Single:
					case TypeCode.Decimal:
						return "float";
					default:
						return null;
				}
			}
		} // func type

		public static object tointeger(object x)
		{
			try
			{
				return (long)Lua.RtConvertValue(x, typeof(long));
			}
			catch
			{
				return null;
			}
		} // func tointeger

		public static bool ult(long m, long n)
		{
			return m < n;
		} // func ult

    public static double pi { get { return Math.PI; } }
    public static double e { get { return Math.E; } }
	
		public static int mininteger { get { return Int32.MinValue; } }
		public static int maxinteger { get { return Int32.MaxValue; } }
	} // clas LuaLibraryMath

  #endregion

  #region -- Bitwise Operations -------------------------------------------------------

  ///////////////////////////////////////////////////////////////////////////////
  /// <summary></summary>
  internal static class LuaLibraryBit32
	{
    public static uint arshift(int x, Nullable<int> disp = null)
    {
			if(!disp.HasValue)
				throw new LuaRuntimeException("Number for arg #2 (disp) expected.", null);
      else if (disp < 0)
        return (uint)unchecked(x << -disp.Value);
      else if (disp > 0)
				return (uint)unchecked(x >> disp.Value);
      else
        return (uint)x;
    } // func arshift

		public static uint band(params uint[] ands)
		{
			uint r = 0xFFFFFFFF;
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

		private static uint CreateBitMask(int field, int width)
		{
			if (field < 0 || width <= 0 || field + width > 32)
				throw new ArgumentException();

			return width == 32 ? (uint)0xFFFFFFFF : unchecked(((uint)1 << width) - 1);
		} // func CreateBitMask

		public static uint extract(uint n, int field, int width = 1)
		{
			return (n >> field) & CreateBitMask(field, width);
		} // func extract

    public static uint replace(uint n, uint v, int field, int width = 1)
    {
			uint m = CreateBitMask(field, width) << field;
      return (n & ~m) | ((v << field) & m);
    } // func replace

		public static uint lrotate(uint x, Nullable<int> disp = null)
    {
			if(!disp.HasValue)
				throw new LuaRuntimeException("Number for arg #2 (disp) expected.", null);
			
			return unchecked(x << disp.Value | x >> (32 - disp.Value));
    } // func lrotate

		public static uint lshift(uint x, Nullable<int> disp = null)
    {
			if(!disp.HasValue)
				throw new LuaRuntimeException("Number for arg #2 (disp) expected.", null);
      else if (disp.Value < 0)
				return rshift(x, -disp.Value);
			else if (disp > 0)
				return disp.Value > 31 ? 0 : x << disp.Value;
			else
				return x;
    } // func lshift

		public static uint rrotate(uint x, Nullable<int> disp = null)
    {
			if (!disp.HasValue)
				throw new LuaRuntimeException("Number for arg #2 (disp) expected.", null);
			
			return unchecked(x >> disp.Value | x << (32 - disp.Value));
    } // func rrotate

    public static uint rshift(uint x, Nullable<int> disp = null)
    {
			if(!disp.HasValue)
				throw new LuaRuntimeException("Number for arg #2 (disp) expected.", null);
      else if (disp.Value < 0)
				return lshift(x, -disp.Value);
			else if (disp.Value > 0)
				return disp.Value > 31 ? 0 : x >> disp.Value;
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

	#region -- Debug functions ----------------------------------------------------------

	internal static class LuaLibraryDebug
	{
		public static LuaResult getupvalue(object f, int index)
		{
			return Lua.RtGetUpValue(f as Delegate, index);
		} // func getupvalue

		public static LuaResult upvalueid(object f, int index)
		{
			return new LuaResult(Lua.RtUpValueId(f as Delegate, index));
		} // func upvalueid

		public static LuaResult setupvalue(object f, int index, object v)
		{
			return new LuaResult(Lua.RtSetUpValue(f as Delegate, index, v));
		} // func setupvalue

		public static void upvaluejoin(object f1, int n1, object f2, int n2)
		{
			Lua.RtUpValueJoin(f1 as Delegate, n1, f2 as Delegate, n2);
		} // func upvaluejoin
	} // class LuaLibraryDebug

	#endregion
}
