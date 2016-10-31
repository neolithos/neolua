using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;

namespace Neo.IronLua
{
	#region -- String Manipulation ------------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Reimplements methods of the string package.</summary>
	public static class LuaLibraryString
	{
		private static bool translateRegEx = true;

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

		private static string TranslateRegularExpression(string regEx)
		{
			if (!translateRegEx)
				return regEx;

			var sb = new StringBuilder();
			var escapeCode = false;
			var inCharList = false;

			for (var i = 0; i < regEx.Length; i++)
			{
				char c = regEx[i];
				if (escapeCode)
				{
					if (c == '%')
					{
						sb.Append('%');
						escapeCode = false;
					}
					else
					{
						switch (c)
						{
							case 'a': // all letters
								sb.Append("\\p{L}");
								break;
							case 'A': // all Non letters
								sb.Append("\\P{L}");
								break;

							case 's': // all space characters
								sb.Append("\\s");
								break;
							case 'S': // all NON space characters
								sb.Append("\\S");
								break;

							case 'd': // all digits
								sb.Append("\\d");
								break;
							case 'D': // all NON digits
								sb.Append("\\D");
								break;

							case 'w': // all alphanumeric characters
								sb.Append("\\w");
								break;
							case 'W': // all NON alphanumeric characters
								sb.Append("\\W");
								break;

							case 'c': // all control characters
								sb.Append("\\p{C}");
								break;
							case 'C': // all NON control characters
								sb.Append("[\\P{C}]");
								break;

							case 'g': // all printable characters except space
								sb.Append("[^\\p{C}\\s]");
								break;
							case 'G': // all NON printable characters including space
								sb.Append("[\\p{C}\\s]");
								break;

							case 'p': // all punctuation characters
								sb.Append("\\p{P}");
								break;
							case 'P': // all NON punctuation characters
								sb.Append("\\P{P}");
								break;

							case 'l': // all lowercase letters
								sb.Append("\\p{Ll}");
								break;
							case 'L': // all NON lowercase letters
								sb.Append("\\P{Ll}");
								break;

							case 'u': // all uppercase letters
								sb.Append("\\p{Lu}");
								break;
							case 'U': // all NON uppercase letters
								sb.Append("\\P{Lu}");
								break;

							case 'x': // all hexadecimal digits
								sb.Append("[0-9A-Fa-f]");
								break;
							case 'X': // all NON hexadecimal digits
								sb.Append("[^0-9A-Fa-f]");
								break;

							case 'b': // github #12
								if (i < regEx.Length - 2)
								{
									char c1 = regEx[i + 1];
									char c2 = regEx[i + 2];
									//Example for %b()
									//(\((?>(?<n>\()|(?<-n>\))|(?:[^\(\)]*))*\)(?(n)(?!)))
									//Example for %bab
									//(a(?>(?<n>a)|(?<-n>b)|(?:[^ab]*))*b(?(n)(?!)))
									sb.Append("(");
									sb.Append(Regex.Escape(c1.ToString()));
									sb.Append("(?>(?<n>");
									sb.Append(Regex.Escape(c1.ToString()));
									sb.Append(")|(?<-n>");
									sb.Append(Regex.Escape(c2.ToString()));
									sb.Append(")|(?:[^");
									sb.Append(Regex.Escape(c1.ToString()));
									sb.Append(Regex.Escape(c2.ToString()));
									sb.Append("]*))*");
									sb.Append(Regex.Escape(c2.ToString()));
									sb.Append("(?(n)(?!)))");
									i += 2;
								}
								else
									throw new ArgumentOutOfRangeException();
								break;
						
						    default:
								sb.Append('\\');
								sb.Append(c);
								break;
						}
						escapeCode = false;
					}
				}
				else if (c == '%')
				{
					escapeCode = true;
				}
				else if (c == '\\')
				{
					sb.Append("\\\\");
				}
				else if (inCharList)
				{
					if (c == ']')
						inCharList = false;
					sb.Append(c);
				}
				else if (c == '-')
				{
					sb.Append("*?");
				}
				else if (c == '[')
				{
					sb.Append('[');
					inCharList = true;
				}
				else
					sb.Append(c);
			}

			return sb.ToString();
		} // func TranslateRegularExpression

		/// <summary>Implamentation of http://www.lua.org/manual/5.3/manual.html#pdf-string.byte </summary>
		/// <param name="s"></param>
		/// <param name="i"></param>
		/// <param name="j"></param>
		/// <returns></returns>
		public static LuaResult @byte(this string s, Nullable<int> i = null, Nullable<int> j = null)
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

		/// <summary>Implementation of http://www.lua.org/manual/5.3/manual.html#pdf-string.char </summary>
		/// <param name="chars"></param>
		/// <returns></returns>
		public static string @char(params int[] chars)
		{
			if (chars == null)
				return String.Empty;

			StringBuilder sb = new StringBuilder(chars.Length);
			for (int i = 0; i < chars.Length; i++)
				sb.Append((char)chars[i]);

			return sb.ToString();
		} // func char

		/// <summary>Not implemented</summary>
		/// <param name="dlg"></param>
		/// <returns></returns>
		public static string dump(Delegate dlg)
		{
			throw new NotImplementedException();
		} // func dump

		/// <summary>Implementation of http://www.lua.org/manual/5.3/manual.html#pdf-string.find </summary>
		/// <param name="s"></param>
		/// <param name="pattern"></param>
		/// <param name="init"></param>
		/// <param name="plain"></param>
		/// <returns></returns>
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
				var index = s.IndexOf(pattern, init - 1);
				return index == -1 ? 
					null : 
					new LuaResult(index + 1, index + pattern.Length);
			}
			else
			{
				// translate the regular expression
				pattern = TranslateRegularExpression(pattern);

				Regex r = new Regex(pattern);
				Match m = r.Match(s, init - 1);
				if (m.Success)
				{
					object[] result = new object[m.Groups.Count + 1]; // first group is all, so add 2 - 1

					// offset of the match
					result[0] = m.Index + 1;
					result[1] = m.Index + m.Length;

					// copy the groups
					for (var i = 1; i < m.Groups.Count; i++)
						result[i + 1] = m.Groups[i].Value;

					return result;
				}
				else
					return LuaResult.Empty;
			}
		} // func find

		/// <summary>Implementation of http://www.lua.org/manual/5.3/manual.html#pdf-string.format </summary>
		/// <param name="formatstring"></param>
		/// <param name="args"></param>
		/// <returns></returns>
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

		/// <summary>Implementation of http://www.lua.org/manual/5.3/manual.html#pdf-string.gmatch </summary>
		/// <param name="s"></param>
		/// <param name="pattern"></param>
		/// <returns></returns>
		public static LuaResult gmatch(this string s, string pattern)
		{
			// f,s,v
			if (String.IsNullOrEmpty(s))
				return LuaResult.Empty;
			if (String.IsNullOrEmpty(pattern))
				return LuaResult.Empty;

			// translate the regular expression
			pattern = TranslateRegularExpression(pattern);

			// Find Matches
			System.Collections.IEnumerator e = Regex.Matches(s, pattern).GetEnumerator();
			return new LuaResult(new Func<object, object, LuaResult>(matchEnum), e, e);
		} // func gmatch

		#region -- class GSubMatchEvaluator -----------------------------------------------

		#region -- class GSubMatchEvaluator -----------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private abstract class GSubMatchEvaluator
		{
			private int iMatchCount = 0;

			public string MatchEvaluator(Match m)
			{
				iMatchCount++;
				return MatchEvaluatorImpl(m);
			} // func MatchEvaluator

			protected abstract string MatchEvaluatorImpl(Match m);

			public int MatchCount { get { return iMatchCount; } }
		} // class GSubMatchEvaluator

		#endregion

		#region -- class GSubLuaTableMatchEvaluator ---------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private sealed class GSubLuaTableMatchEvaluator : GSubMatchEvaluator
		{
			private LuaTable t;
			private bool lIgnoreCase;

			public GSubLuaTableMatchEvaluator(LuaTable t)
			{
				this.t = t;
				this.lIgnoreCase = (bool)Lua.RtConvertValue(t.GetMemberValue("__IgnoreCase"), typeof(bool));
			} // ctor

			protected override string MatchEvaluatorImpl(Match m)
			{
				return (string)Lua.RtConvertValue(t.GetMemberValue(m.Groups[1].Value, lIgnoreCase), typeof(string));
			} // func MatchEvaluator
		} // class GSubLuaTableMatchEvaluator

		#endregion

		#region -- class GSubFunctionMatchEvaluator ---------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private sealed class GSubFunctionMatchEvaluator : GSubMatchEvaluator
		{
			private CallSite callSite = null;
			private object funcCall;

			public GSubFunctionMatchEvaluator(object funcCall)
			{
				this.funcCall = funcCall;
			} // ctor

			private void UpdateCallSite(CallInfo callInfo, CallSite callSite)
			{
				this.callSite = callSite;
			} // proc UpdateCallSite

			protected override string MatchEvaluatorImpl(Match m)
			{
				string[] args = new string[m.Groups.Count - 1];
				for (int i = 1; i < m.Groups.Count; i++)
					args[i - 1] = m.Groups[i].Value;

				return (string)Lua.RtConvertValue(Lua.RtInvokeSite(callSite, callInfo => new Lua.LuaInvokeBinder(null, callInfo), UpdateCallSite, funcCall, args), typeof(string));
			} // func MatchEvaluator
		} // class GSubLuaTableMatchEvaluator

		#endregion

		#region -- class GSubStringMatchEvaluator -----------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private sealed class GSubStringMatchEvaluator : GSubMatchEvaluator
		{
			private object[] replaces;

			public GSubStringMatchEvaluator(string sRepl)
			{
				List<object> lst = new List<object>();
				int i = 0;
				int iStart = 0;

				while (i < sRepl.Length)
				{
					if (sRepl[i] == '%')
					{
						if (++i >= sRepl.Length)
							break;

						if (sRepl[i] == '%') // Parse a number (0-9)
						{
							Add(lst, sRepl, iStart, i);
							iStart = i + 1;
						}
						else if (sRepl[i] >= '0' && sRepl[i] <= '9') // Add what we find until now
						{
							Add(lst, sRepl, iStart, i - 1);
							lst.Add(sRepl[i] - '0');
							iStart = i + 1;
						}
					}

					i++;
				}

				// Add the rest
				Add(lst, sRepl, iStart, i);

				replaces = lst.ToArray();
			} // ctor

			private void Add(List<object> lst, string sRepl, int iStart, int iCurrent)
			{
				int iLength = iCurrent - iStart;
				if (iLength == 0)
					return;

				lst.Add(sRepl.Substring(iStart, iLength));
			} // proc Add

			protected override string MatchEvaluatorImpl(Match m)
			{
				string[] result = new string[replaces.Length];

				for (int i = 0; i < result.Length; i++)
				{
					if (replaces[i] is string)
						result[i] = (string)replaces[i];
					else if (replaces[i] is int)
					{
						int iIndex = (int)replaces[i];
						if (iIndex == 0)
							result[i] = m.Value;
						else if (iIndex <= m.Groups.Count)
							result[i] = m.Groups[iIndex].Value;
						else
							result[i] = String.Empty;
					}
					else
						result[i] = String.Empty;
				}

				return String.Concat(result);
			} // func MatchEvaluator
		} // class GSubLuaTableMatchEvaluator

		#endregion

		#endregion

		/// <summary>Implementation of http://www.lua.org/manual/5.3/manual.html#pdf-string.gsub </summary>
		/// <param name="s"></param>
		/// <param name="pattern"></param>
		/// <param name="repl"></param>
		/// <param name="n"></param>
		/// <returns></returns>
		public static LuaResult gsub(this string s, string pattern, object repl, int n)
		{
			Regex regex = new Regex(TranslateRegularExpression(pattern));

			if (n <= 0)
				n = Int32.MaxValue;

			GSubMatchEvaluator matchEvaluator;
			if (repl is LuaTable)
				matchEvaluator = new GSubLuaTableMatchEvaluator((LuaTable)repl);
			else if (repl is Delegate || repl is ILuaMethod)
				matchEvaluator = new GSubFunctionMatchEvaluator(repl);
			else
				matchEvaluator = new GSubStringMatchEvaluator((string)Lua.RtConvertValue(repl, typeof(string)));

			string r = regex.Replace(s, matchEvaluator.MatchEvaluator, n);

			return new LuaResult(r, matchEvaluator.MatchCount);
		} // func gsub

		/// <summary>Implementation of http://www.lua.org/manual/5.3/manual.html#pdf-string.len </summary>
		/// <param name="s"></param>
		/// <returns></returns>
		public static int len(this string s)
		{
			return s == null ? 0 : s.Length;
		} // func len

		/// <summary>Implementation of http://www.lua.org/manual/5.3/manual.html#pdf-string.lower </summary>
		/// <param name="s"></param>
		/// <returns></returns>
		public static string lower(this string s)
		{
			if (String.IsNullOrEmpty(s))
				return s;
			return s.ToLower();
		} // func lower

		/// <summary>Implementation of http://www.lua.org/manual/5.3/manual.html#pdf-string.match </summary>
		/// <param name="s"></param>
		/// <param name="pattern"></param>
		/// <param name="init"></param>
		/// <returns></returns>
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
			return MatchResult(r.Match(s, init - 1));
		} // func match

		private static LuaResult MatchResult(Match m)
		{
			if (m.Success)
			{
				if (m.Groups.Count > 1) // the expression uses groups, return the groups
				{
					object[] result = new object[m.Groups.Count - 1];

					for (int i = 1; i < m.Groups.Count; i++)
						result[i - 1] = m.Groups[i].Value;
					
					return result;
				}
				else // no groups, return the captures
				{
					object[] result = new object[m.Captures.Count];

					for (int i = 0; i < m.Captures.Count; i++)
						result[i] = m.Captures[i].Value;

					return result;
				}
			}
			else
				return LuaResult.Empty;
		} // func MatchResult

		/// <summary>Implementation of http://www.lua.org/manual/5.3/manual.html#pdf-string.rep </summary>
		/// <param name="s"></param>
		/// <param name="n"></param>
		/// <param name="sep"></param>
		/// <returns></returns>
		public static string rep(this string s, int n, string sep = "")
		{
			if (n == 0)
				return String.Empty;
			return String.Join(sep, Enumerable.Repeat(s, n));
		} // func rep

		/// <summary>Implementation of http://www.lua.org/manual/5.3/manual.html#pdf-string.reverse </summary>
		/// <param name="s"></param>
		/// <returns></returns>
		public static string reverse(this string s)
		{
			if (String.IsNullOrEmpty(s) || s.Length == 1)
				return s;

			char[] a = s.ToCharArray();
			Array.Reverse(a);
			return new string(a);
		} // func reverse

		/// <summary>Implementation of http://www.lua.org/manual/5.3/manual.html#pdf-string.sub </summary>
		/// <param name="s"></param>
		/// <param name="i"></param>
		/// <param name="j"></param>
		/// <returns></returns>
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

		/// <summary>Implementation of http://www.lua.org/manual/5.3/manual.html#pdf-string.upper </summary>
		/// <param name="s"></param>
		/// <returns></returns>
		public static string upper(this string s)
		{
			if (String.IsNullOrEmpty(s))
				return s;
			return s.ToUpper();
		} // func lower

		// todo: pack
		// todo: packsize
		// todo: unpack

		/// <summary>Set this member to <c>false</c> to use native (.net) regex syntax.</summary>
		public static bool __TranslateRegEx { get { return translateRegEx; } set { translateRegEx = value; } }
	} // class LuaLibraryString

	#endregion
	
	#region -- Mathematical Functions ---------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Reimplements methods of the math package.</summary>
	public static class LuaLibraryMath
	{
		private static Random rand = null;

		/// <summary>Implementation of http://www.lua.org/manual/5.3/manual.html#pdf-math.abs </summary>
		/// <param name="x"></param>
		/// <returns></returns>
		public static double abs(double x)
		{
			return Math.Abs(x);
		} // func abs

		/// <summary>Implementation of http://www.lua.org/manual/5.3/manual.html#pdf-math.acos </summary>
		/// <param name="x"></param>
		/// <returns></returns>
		public static double acos(double x)
		{
			return Math.Acos(x);
		} // func acos

		/// <summary>Implementation of http://www.lua.org/manual/5.3/manual.html#pdf-math.asin </summary>
		/// <param name="x"></param>
		/// <returns></returns>
		public static double asin(double x)
		{
			return Math.Asin(x);
		} // func asin

		/// <summary>Implementation of http://www.lua.org/manual/5.3/manual.html#pdf-math.atan </summary>
		/// <param name="x"></param>
		/// <returns></returns>
		public static double atan(double x)
		{
			return Math.Atan(x);
		} // func atan

		/// <summary>Implementation of http://www.lua.org/manual/5.2/manual.html#pdf-math.atan2 </summary>
		/// <param name="y"></param>
		/// <param name="x"></param>
		/// <returns></returns>
		public static double atan2(double y, double x)
		{
			return Math.Atan2(y, x);
		} // func atan2

		/// <summary>Implementation of http://www.lua.org/manual/5.3/manual.html#pdf-math.ceil </summary>
		/// <param name="x"></param>
		/// <returns></returns>
		public static double ceil(double x)
		{
			return Math.Ceiling(x);
		} // func ceil

		/// <summary>Implementation of http://www.lua.org/manual/5.3/manual.html#pdf-math.cos </summary>
		/// <param name="x"></param>
		/// <returns></returns>
		public static double cos(double x)
		{
			return Math.Cos(x);
		} // func Cos

		/// <summary>Implementation of http://www.lua.org/manual/5.2/manual.html#pdf-math.cosh </summary>
		/// <param name="x"></param>
		/// <returns></returns>
		public static double cosh(double x)
		{
			return Math.Cosh(x);
		} // func cosh

		/// <summary>Implementation of http://www.lua.org/manual/5.3/manual.html#pdf-math.deg </summary>
		/// <param name="x"></param>
		/// <returns></returns>
		public static double deg(double x)
		{
			return x * 180.0 / Math.PI;
		} // func deg

		/// <summary>Implementation of  </summary>
		/// <param name="x"></param>
		/// <returns></returns>
		public static double exp(double x)
		{
			return Math.Exp(x);
		} // func exp

		/// <summary>Implementation of http://www.lua.org/manual/5.3/manual.html#pdf-math.floor </summary>
		/// <param name="x"></param>
		/// <returns></returns>
		public static double floor(double x)
		{
			return Math.Floor(x);
		} // func floor

		/// <summary>Implementation of http://www.lua.org/manual/5.3/manual.html#pdf-math.fmod </summary>
		/// <param name="x"></param>
		/// <param name="y"></param>
		/// <returns></returns>
		public static double fmod(double x, double y)
		{
			return x % y;
		} // func fmod

		/// <summary>Returns m and e such that x = m2e, e is an integer and the absolute value of m is in the range [0.5, 1) (or zero when x is zero, http://www.lua.org/manual/5.2/manual.html#pdf-math.frexp).</summary>
		/// <param name="x"></param>
		/// <returns></returns>
		public static double frexp(double x)
		{
			throw new NotImplementedException();
		} // func frexp

		/// <summary>The value HUGE_VAL, a value larger than or equal to any other numerical value (http://www.lua.org/manual/5.3/manual.html#pdf-math.huge).</summary>
		public static double huge { get { return double.MaxValue; } }

		/// <summary>Implementation of http://www.lua.org/manual/5.2/manual.html#pdf-math.ldexp </summary>
		/// <param name="m"></param>
		/// <param name="e"></param>
		/// <returns></returns>
		public static double ldexp(double m, double e)
		{
			// Returns m2e (e should be an integer).
			throw new NotImplementedException();
		} // func ldexp

		/// <summary>Implementation of http://www.lua.org/manual/5.3/manual.html#pdf-math.log </summary>
		/// <param name="x"></param>
		/// <param name="b"></param>
		/// <returns></returns>
		public static double log(double x, double b = Math.E)
		{
			return Math.Log(x, b);
		} // func log

		/// <summary>Implementation of http://www.lua.org/manual/5.3/manual.html#pdf-math.max </summary>
		/// <param name="x"></param>
		/// <returns></returns>
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

		/// <summary>Implementation of http://www.lua.org/manual/5.3/manual.html#pdf-math.min </summary>
		/// <param name="x"></param>
		/// <returns></returns>
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

		/// <summary>Implementation of http://www.lua.org/manual/5.3/manual.html#pdf-math.modf </summary>
		/// <param name="x"></param>
		/// <returns></returns>
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

		/// <summary>Implementation of http://www.lua.org/manual/5.2/manual.html#pdf-math.pow </summary>
		/// <param name="x"></param>
		/// <param name="y"></param>
		/// <returns></returns>
		public static double pow(double x, double y)
		{
			return Math.Pow(x, y);
		} // func pow

		/// <summary>Implementation of http://www.lua.org/manual/5.3/manual.html#pdf-math.rad </summary>
		/// <param name="x"></param>
		/// <returns></returns>
		public static double rad(double x)
		{
			return x * Math.PI / 180.0;
		} // func rad

		/// <summary>Implementation of http://www.lua.org/manual/5.3/manual.html#pdf-math.random </summary>
		/// <param name="m"></param>
		/// <param name="n"></param>
		/// <returns></returns>
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

		/// <summary>Implementation of http://www.lua.org/manual/5.3/manual.html#pdf-math.randomseed </summary>
		/// <param name="x"></param>
		public static void randomseed(object x)
		{
			int seed;
			if (x == null)
				seed = Environment.TickCount;
			else
			{
				var tc = LuaEmit.GetTypeCode(x.GetType());
				if (tc >= LuaEmitTypeCode.Byte && tc <= LuaEmitTypeCode.Decimal)
					seed = Convert.ToInt32(x);
				else if (tc == LuaEmitTypeCode.DateTime)
					seed = unchecked((int)((DateTime)x).ToFileTime());
				else
					seed = x.GetHashCode();
			}
			rand = new Random(seed);
		} // proc randomseed

		/// <summary>Implementation of http://www.lua.org/manual/5.3/manual.html#pdf-math.sin </summary>
		/// <param name="x"></param>
		/// <returns></returns>
		public static double sin(double x)
		{
			return Math.Sin(x);
		} // func sin

		/// <summary>Implementation of http://www.lua.org/manual/5.2/manual.html#pdf-math.sinh </summary>
		/// <param name="x"></param>
		/// <returns></returns>
		public static double sinh(double x)
		{
			return Math.Sinh(x);
		} // func sinh

		/// <summary>Implementation of http://www.lua.org/manual/5.3/manual.html#pdf-math.sqrt </summary>
		/// <param name="x"></param>
		/// <returns></returns>
		public static double sqrt(double x)
		{
			return Math.Sqrt(x);
		} // func sqrt

		/// <summary>Implementation of http://www.lua.org/manual/5.3/manual.html#pdf-math.tan </summary>
		/// <param name="x"></param>
		/// <returns></returns>
		public static double tan(double x)
		{
			return Math.Tan(x);
		} // func tan

		/// <summary>Implementation of http://www.lua.org/manual/5.2/manual.html#pdf-math.tanh </summary>
		/// <param name="x"></param>
		/// <returns></returns>
		public static double tanh(double x)
		{
			return Math.Tanh(x);
		} // func tanh

		/// <summary>Implementation of http://www.lua.org/manual/5.3/manual.html#pdf-math.type </summary>
		/// <param name="x"></param>
		/// <returns></returns>
		public static string type(object x)
		{
			if (x == null)
				return null;
			else
			{
				switch (LuaEmit.GetTypeCode(x.GetType()))
				{
					case LuaEmitTypeCode.Byte:
					case LuaEmitTypeCode.SByte:
					case LuaEmitTypeCode.Int16:
					case LuaEmitTypeCode.UInt16:
					case LuaEmitTypeCode.Int32:
					case LuaEmitTypeCode.UInt32:
					case LuaEmitTypeCode.Int64:
					case LuaEmitTypeCode.UInt64:
						return "integer";
					case LuaEmitTypeCode.Double:
					case LuaEmitTypeCode.Single:
					case LuaEmitTypeCode.Decimal:
						return "float";
					default:
						return null;
				}
			}
		} // func type

		/// <summary>Implementation of http://www.lua.org/manual/5.3/manual.html#pdf-math.tointeger </summary>
		/// <param name="x"></param>
		/// <returns></returns>
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

		/// <summary>Implementation of http://www.lua.org/manual/5.3/manual.html#pdf-math.ult </summary>
		/// <param name="m"></param>
		/// <param name="n"></param>
		/// <returns></returns>
		public static bool ult(long m, long n)
		{
			return m < n;
		} // func ult

		/// <summary>Implementation of http://www.lua.org/manual/5.3/manual.html#pdf-math.pi </summary>
		public static double pi { get { return Math.PI; } }
		/// <summary>Maps Math.E</summary>
		public static double e { get { return Math.E; } }

		/// <summary>Implementation of http://www.lua.org/manual/5.3/manual.html#pdf-math.mininteger </summary>
		public static int mininteger { get { return Int32.MinValue; } }
		/// <summary>Implementation of http://www.lua.org/manual/5.3/manual.html#pdf-math.maxinteger </summary>
		public static int maxinteger { get { return Int32.MaxValue; } }
	} // clas LuaLibraryMath

	#endregion

	#region -- Bitwise Operations -------------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Reimplements methods of the bit32 package.</summary>
	public static class LuaLibraryBit32
	{
		/// <summary>Implementation of http://www.lua.org/manual/5.2/manual.html#pdf-bit32.arshift </summary>
		/// <param name="x"></param>
		/// <param name="disp"></param>
		/// <returns></returns>
		public static uint arshift(int x, Nullable<int> disp = null)
		{
			if (!disp.HasValue)
				throw new LuaRuntimeException("Number for arg #2 (disp) expected.", null);
			else if (disp < 0)
				return (uint)unchecked(x << -disp.Value);
			else if (disp > 0)
				return (uint)unchecked(x >> disp.Value);
			else
				return (uint)x;
		} // func arshift

		/// <summary>Implementation of http://www.lua.org/manual/5.2/manual.html#pdf-bit32.band </summary>
		/// <param name="ands"></param>
		/// <returns></returns>
		public static uint band(params uint[] ands)
		{
			uint r = 0xFFFFFFFF;
			for (int i = 0; i < ands.Length; i++)
				r &= ands[i];
			return r;
		} // func band

		/// <summary>Implementation of http://www.lua.org/manual/5.2/manual.html#pdf-bit32.bnot </summary>
		/// <param name="x"></param>
		/// <returns></returns>
		public static uint bnot(uint x)
		{
			return ~x;
		} // func bnot

		/// <summary>Implementation of http://www.lua.org/manual/5.2/manual.html#pdf-bit32.bor </summary>
		/// <param name="ors"></param>
		/// <returns></returns>
		public static uint bor(params uint[] ors)
		{
			uint r = 0;
			if (ors != null)
				for (int i = 0; i < ors.Length; i++)
					r |= ors[i];
			return r;
		} // func bor

		/// <summary>Implementation of http://www.lua.org/manual/5.2/manual.html#pdf-bit32.btest </summary>
		/// <param name="tests"></param>
		/// <returns></returns>
		public static bool btest(params uint[] tests)
		{
			return band(tests) != 0;
		} // func btest

		/// <summary>Implementation of http://www.lua.org/manual/5.2/manual.html#pdf-bit32.bxor </summary>
		/// <param name="xors"></param>
		/// <returns></returns>
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

		/// <summary>Implementation of http://www.lua.org/manual/5.2/manual.html#pdf-bit32.extract </summary>
		/// <param name="n"></param>
		/// <param name="field"></param>
		/// <param name="width"></param>
		/// <returns></returns>
		public static uint extract(uint n, int field, int width = 1)
		{
			return (n >> field) & CreateBitMask(field, width);
		} // func extract

		/// <summary>Implementation of http://www.lua.org/manual/5.2/manual.html#pdf-bit32.replace </summary>
		/// <param name="n"></param>
		/// <param name="v"></param>
		/// <param name="field"></param>
		/// <param name="width"></param>
		/// <returns></returns>
		public static uint replace(uint n, uint v, int field, int width = 1)
		{
			uint m = CreateBitMask(field, width) << field;
			return (n & ~m) | ((v << field) & m);
		} // func replace

		/// <summary>Implementation of http://www.lua.org/manual/5.2/manual.html#pdf-bit32.lrotate </summary>
		/// <param name="x"></param>
		/// <param name="disp"></param>
		/// <returns></returns>
		public static uint lrotate(uint x, Nullable<int> disp = null)
		{
			if (!disp.HasValue)
				throw new LuaRuntimeException("Number for arg #2 (disp) expected.", null);

			return unchecked(x << disp.Value | x >> (32 - disp.Value));
		} // func lrotate

		/// <summary>Implementation of http://www.lua.org/manual/5.2/manual.html#pdf-bit32.lshift </summary>
		/// <param name="x"></param>
		/// <param name="disp"></param>
		/// <returns></returns>
		public static uint lshift(uint x, Nullable<int> disp = null)
		{
			if (!disp.HasValue)
				throw new LuaRuntimeException("Number for arg #2 (disp) expected.", null);
			else if (disp.Value < 0)
				return rshift(x, -disp.Value);
			else if (disp > 0)
				return disp.Value > 31 ? 0 : x << disp.Value;
			else
				return x;
		} // func lshift

		/// <summary>Implementation of http://www.lua.org/manual/5.2/manual.html#pdf-bit32.rrotate </summary>
		/// <param name="x"></param>
		/// <param name="disp"></param>
		/// <returns></returns>
		public static uint rrotate(uint x, Nullable<int> disp = null)
		{
			if (!disp.HasValue)
				throw new LuaRuntimeException("Number for arg #2 (disp) expected.", null);

			return unchecked(x >> disp.Value | x << (32 - disp.Value));
		} // func rrotate

		/// <summary>Implementation of http://www.lua.org/manual/5.2/manual.html#pdf-bit32.rshift </summary>
		/// <param name="x"></param>
		/// <param name="disp"></param>
		/// <returns></returns>
		public static uint rshift(uint x, Nullable<int> disp = null)
		{
			if (!disp.HasValue)
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
