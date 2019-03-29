#region -- copyright --
//
// Licensed to the Apache Software Foundation (ASF) under one
// or more contributor license agreements.  See the NOTICE file
// distributed with this work for additional information
// regarding copyright ownership.  The ASF licenses this file
// to you under the Apache License, Version 2.0 (the
// "License"); you may not use this file except in compliance
// with the License.  You may obtain a copy of the License at
// 
//   http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing,
// software distributed under the License is distributed on an
// "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY
// KIND, either express or implied.  See the License for the
// specific language governing permissions and limitations
// under the License.
//
#endregion
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;

#pragma warning disable IDE1006 // Naming Styles

namespace Neo.IronLua
{
	#region -- String Manipulation ----------------------------------------------------

	/// <summary>Reimplements methods of the string package.</summary>
	public static class LuaLibraryString
	{
		private static bool translateRegEx = true;

		private static void NormalizeStringArguments(string s, int i, int j, out int offset, out int len)
		{
			if (i == 0)
				i = 1;

			if (i < 0) // Suffix mode
			{
				offset = s.Length + i;
				if (offset < 0)
					offset = 0;
				len = (j < 0 ? s.Length + j + 1 : j) - offset;
			}
			else // Prefix mode
			{
				offset = i - 1;
				if (j < 0)
					j = s.Length + j + 1;
				len = j - offset;
			}

			// correct the length
			if (offset + len > s.Length)
				len = s.Length - offset;
		} // proc NormalizeStringArguments

		private static Tuple<string, bool[]> TranslateRegularExpression(string regEx)
		{
			if (!translateRegEx)
				return new Tuple<string, bool[]>(regEx, null);

			var sb = new StringBuilder();
			var escapeCode = false;
			var inCharList = false;
			var captures = new List<bool>
			{
				false // full result
			};

			for (var i = 0; i < regEx.Length; i++)
			{
				var c = regEx[i];
				if (escapeCode)
				{
					if (c == '%')
					{
						sb.Append('%');
						escapeCode = false;
					}
					else
					{
						#region -- char groups --
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
									var c1 = regEx[i + 1];
									var c2 = regEx[i + 2];
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
						#endregion
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
				else if (c == '^' && !inCharList)
				{
					sb.Append("\\G");
				}
				else if (c == '(')
				{
					sb.Append('(');
					captures.Add(i + 1 < regEx.Length && regEx[i + 1] == ')');
				}
				else
					sb.Append(c);
			}

			return new Tuple<string, bool[]>(sb.ToString(), captures.ToArray());
		} // func TranslateRegularExpression

		/// <summary>Implamentation of http://www.lua.org/manual/5.3/manual.html#pdf-string.byte </summary>
		/// <param name="s"></param>
		/// <param name="i"></param>
		/// <param name="j"></param>
		/// <returns></returns>
		public static LuaResult @byte(this string s, int? i = null, int? j = null)
		{
			if (!i.HasValue)
				i = 1;
			if (!j.HasValue)
				j = i;

			if (String.IsNullOrEmpty(s) || i == 0)
				return LuaResult.Empty;

			NormalizeStringArguments(s, i.Value, j.Value, out var ofs, out var len);
			if (len <= 0)
				return LuaResult.Empty;

			var r = new object[len];
			for (var a = 0; a < len; a++)
				r[a] = (int)s[ofs + a];

			return r;
		} // func byte

		/// <summary>Implementation of http://www.lua.org/manual/5.3/manual.html#pdf-string.char </summary>
		/// <param name="chars"></param>
		/// <returns></returns>
		public static string @char(params int[] chars)
		{
			if (chars == null)
				return String.Empty;

			var sb = new StringBuilder(chars.Length);
			for (int i = 0; i < chars.Length; i++)
				sb.Append((char)chars[i]);

			return sb.ToString();
		} // func char

		/// <summary>Not implemented</summary>
		/// <param name="dlg"></param>
		/// <returns></returns>
		public static string dump(Delegate dlg)
			=> throw new NotImplementedException();

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
				return String.IsNullOrEmpty(pattern) && init == 1
					? new LuaResult(1)
					: LuaResult.Empty;
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
				var tranlatedPattern = TranslateRegularExpression(pattern).Item1;

				var r = new Regex(tranlatedPattern);
				var m = r.Match(s, init - 1);
				if (m.Success)
				{
					var result = new object[m.Groups.Count + 1]; // first group is all, so add 2 - 1

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
			=> AT.MIN.Tools.sprintf(formatstring, args);

		private static LuaResult MatchEnum(object s, object current)
		{
			var e = (System.Collections.IEnumerator)s;

			// return value
			if (e.MoveNext())
			{
				var m = (Match)e.Current;
				return MatchResult(m, null);
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
			pattern = TranslateRegularExpression(pattern).Item1;

			// Find Matches
			var e = Regex.Matches(s, pattern).GetEnumerator(); // todo: possible memory leak if the enumeration does not reach the end
			return new LuaResult(new Func<object, object, LuaResult>(MatchEnum), e, e);
		} // func gmatch

		#region -- class GSubMatchEvaluator -------------------------------------------

		#region -- class GSubMatchEvaluator -------------------------------------------

		private abstract class GSubMatchEvaluator
		{
			private int matchCount = 0;

			public string MatchEvaluator(Match m)
			{
				matchCount++;
				return MatchEvaluatorImpl(m);
			} // func MatchEvaluator

			protected abstract string MatchEvaluatorImpl(Match m);

			public int MatchCount => matchCount;
		} // class GSubMatchEvaluator

		#endregion

		#region -- class GSubLuaTableMatchEvaluator -----------------------------------

		private sealed class GSubLuaTableMatchEvaluator : GSubMatchEvaluator
		{
			private readonly LuaTable t;
			private readonly bool ignoreCase;

			public GSubLuaTableMatchEvaluator(LuaTable t)
			{
				this.t = t ?? throw new ArgumentNullException(nameof(t));
				ignoreCase = (bool)Lua.RtConvertValue(t.GetMemberValue("__IgnoreCase"), typeof(bool));
			} // ctor

			protected override string MatchEvaluatorImpl(Match m)
				=> (string)Lua.RtConvertValue(t.GetMemberValue(m.Groups[1].Value, ignoreCase), typeof(string));
		} // class GSubLuaTableMatchEvaluator

		#endregion

		#region -- class GSubFunctionMatchEvaluator -----------------------------------

		private sealed class GSubFunctionMatchEvaluator : GSubMatchEvaluator
		{
			private CallSite callSite = null;
			private readonly object funcCall;

			public GSubFunctionMatchEvaluator(object funcCall)
			{
				this.funcCall = funcCall ?? throw new ArgumentNullException(nameof(funcCall));
			} // ctor

			private void UpdateCallSite(CallInfo callInfo, CallSite callSite)
			{
				this.callSite = callSite;
			} // proc UpdateCallSite

			protected override string MatchEvaluatorImpl(Match m)
			{
				var args = new string[m.Groups.Count - 1];
				for (var i = 1; i < m.Groups.Count; i++)
					args[i - 1] = m.Groups[i].Value;

				return (string)Lua.RtConvertValue(Lua.RtInvokeSite(callSite, callInfo => new Lua.LuaInvokeBinder(null, callInfo), UpdateCallSite, funcCall, args), typeof(string));
			} // func MatchEvaluator
		} // class GSubLuaTableMatchEvaluator

		#endregion

		#region -- class GSubStringMatchEvaluator -------------------------------------

		private sealed class GSubStringMatchEvaluator : GSubMatchEvaluator
		{
			private readonly object[] replaces;

			public GSubStringMatchEvaluator(string repl)
			{
				var lst = new List<object>();
				var i = 0;
				var ofs = 0;

				while (i < repl.Length)
				{
					if (repl[i] == '%')
					{
						if (++i >= repl.Length)
							break;

						if (repl[i] == '%') // Parse a number (0-9)
						{
							Add(lst, repl, ofs, i);
							ofs = i + 1;
						}
						else if (repl[i] >= '0' && repl[i] <= '9') // Add what we find until now
						{
							Add(lst, repl, ofs, i - 1);
							lst.Add(repl[i] - '0');
							ofs = i + 1;
						}
					}

					i++;
				}

				// Add the rest
				Add(lst, repl, ofs, i);

				replaces = lst.ToArray();
			} // ctor

			private static void Add(List<object> lst, string repl, int offset, int current)
			{
				var length = current - offset;
				if (length == 0)
					return;

				lst.Add(repl.Substring(offset, length));
			} // proc Add

			protected override string MatchEvaluatorImpl(Match m)
			{
				var result = new string[replaces.Length];

				for (var i = 0; i < result.Length; i++)
				{
					if (replaces[i] is string s)
						result[i] = s;
					else if (replaces[i] is int idx)
					{
						if (idx == 0)
							result[i] = m.Value;
						else if (idx <= m.Groups.Count)
							result[i] = m.Groups[idx].Value;
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
			var regex = new Regex(TranslateRegularExpression(pattern).Item1);

			if (n <= 0)
				n = Int32.MaxValue;

			GSubMatchEvaluator matchEvaluator;
			if (repl is LuaTable)
				matchEvaluator = new GSubLuaTableMatchEvaluator((LuaTable)repl);
			else if (repl is Delegate || repl is ILuaMethod)
				matchEvaluator = new GSubFunctionMatchEvaluator(repl);
			else
				matchEvaluator = new GSubStringMatchEvaluator((string)Lua.RtConvertValue(repl, typeof(string)));

			var r = regex.Replace(s, matchEvaluator.MatchEvaluator, n);

			return new LuaResult(r, matchEvaluator.MatchCount);
		} // func gsub

		/// <summary>Implementation of http://www.lua.org/manual/5.3/manual.html#pdf-string.len </summary>
		/// <param name="s"></param>
		/// <returns></returns>
		public static int len(this string s)
			=> s == null ? 0 : s.Length;

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
			var trans = TranslateRegularExpression(pattern);

			var r = new Regex(trans.Item1);
			return MatchResult(r.Match(s, init - 1), trans.Item2);
		} // func match

		private static LuaResult MatchResult(Match m, bool[] indexReturn)
		{
			if (m.Success)
			{
				if (m.Groups.Count > 1) // the expression uses groups, return the groups
				{
					var result = new object[m.Groups.Count - 1];

					for (var i = 1; i < m.Groups.Count; i++)
						result[i - 1] = MatchCaptureValue(i, m.Groups[i], indexReturn);

					return result;
				}
				else // no groups, return the captures
				{
					var result = new object[m.Captures.Count];

					for (var i = 0; i < m.Captures.Count; i++)
						result[i] = MatchCaptureValue(i, m.Captures[i], indexReturn);

					return result;
				}
			}
			else
				return LuaResult.Empty;
		} // func MatchResult

		private static object MatchCaptureValue(int i, Capture capture, bool[] indexReturn)
			=> indexReturn != null && i < indexReturn.Length && indexReturn[i] 
			? (object)(capture.Index + 1) 
			: capture.Value;

	
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

			var a = s.ToCharArray();
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

			NormalizeStringArguments(s, i, j, out var ofs, out var len);

			// return the string
			return len <= 0
				? String.Empty
				: s.Substring(ofs, len);
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

	#region -- Mathematical Functions -------------------------------------------------

	/// <summary>Reimplements methods of the math package.</summary>
	public static class LuaLibraryMath
	{
		private static Random rand = null;

		/// <summary>Implementation of http://www.lua.org/manual/5.3/manual.html#pdf-math.abs </summary>
		/// <param name="x"></param>
		/// <returns></returns>
		public static double abs(double x)
			=> Math.Abs(x);

		/// <summary>Implementation of http://www.lua.org/manual/5.3/manual.html#pdf-math.acos </summary>
		/// <param name="x"></param>
		/// <returns></returns>
		public static double acos(double x)
			=> Math.Acos(x);

		/// <summary>Implementation of http://www.lua.org/manual/5.3/manual.html#pdf-math.asin </summary>
		/// <param name="x"></param>
		/// <returns></returns>
		public static double asin(double x)
			=> Math.Asin(x);

		/// <summary>Implementation of http://www.lua.org/manual/5.3/manual.html#pdf-math.atan </summary>
		/// <param name="x"></param>
		/// <returns></returns>
		public static double atan(double x)
			=> Math.Atan(x);

		/// <summary>Implementation of http://www.lua.org/manual/5.2/manual.html#pdf-math.atan2 </summary>
		/// <param name="y"></param>
		/// <param name="x"></param>
		/// <returns></returns>
		public static double atan2(double y, double x)
			=> Math.Atan2(y, x);

		/// <summary>Implementation of http://www.lua.org/manual/5.3/manual.html#pdf-math.ceil </summary>
		/// <param name="x"></param>
		/// <returns></returns>
		public static double ceil(double x)
			=> Math.Ceiling(x);

		/// <summary>Implementation of http://www.lua.org/manual/5.3/manual.html#pdf-math.cos </summary>
		/// <param name="x"></param>
		/// <returns></returns>
		public static double cos(double x)
			=> Math.Cos(x);

		/// <summary>Implementation of http://www.lua.org/manual/5.2/manual.html#pdf-math.cosh </summary>
		/// <param name="x"></param>
		/// <returns></returns>
		public static double cosh(double x)
			=> Math.Cosh(x);

		/// <summary>Implementation of http://www.lua.org/manual/5.3/manual.html#pdf-math.deg </summary>
		/// <param name="x"></param>
		/// <returns></returns>
		public static double deg(double x)
			=> x * 180.0 / Math.PI;

		/// <summary>Implementation of  </summary>
		/// <param name="x"></param>
		/// <returns></returns>
		public static double exp(double x)
			=> Math.Exp(x);

		/// <summary>Implementation of http://www.lua.org/manual/5.3/manual.html#pdf-math.floor </summary>
		/// <param name="x"></param>
		/// <returns></returns>
		public static double floor(double x)
			=> Math.Floor(x);

		/// <summary>Implementation of http://www.lua.org/manual/5.3/manual.html#pdf-math.fmod </summary>
		/// <param name="x"></param>
		/// <param name="y"></param>
		/// <returns></returns>
		public static double fmod(double x, double y)
			=> x % y;

		/// <summary>Returns m and e such that x = m2e, e is an integer and the absolute value of m is in the range [0.5, 1) (or zero when x is zero, http://www.lua.org/manual/5.2/manual.html#pdf-math.frexp).</summary>
		/// <param name="x"></param>
		/// <returns></returns>
		public static LuaResult frexp(double x)
		{
			var exponent = (x == 0.0) ? 0 : (int)(1 + Math.Log(Math.Abs(x), 2));
			var mantissa = x * (Math.Pow(2, -exponent));
			return new LuaResult(mantissa, exponent);
		} // func frexp

		/// <summary>The value HUGE_VAL, a value larger than or equal to any other numerical value (http://www.lua.org/manual/5.3/manual.html#pdf-math.huge).</summary>
		public static double huge => Double.MaxValue;

		/// <summary>Implementation of http://www.lua.org/manual/5.2/manual.html#pdf-math.ldexp </summary>
		/// <param name="m"></param>
		/// <param name="e"></param>
		/// <returns></returns>
		public static double ldexp(double m, int e)
			=> m * Math.Pow(2, e); // Returns m2e (e should be an integer).

		/// <summary>Implementation of http://www.lua.org/manual/5.3/manual.html#pdf-math.log </summary>
		/// <param name="x"></param>
		/// <param name="b"></param>
		/// <returns></returns>
		public static double log(double x, double b = Math.E)
			=> Math.Log(x, b);

		/// <summary>Implementation of http://www.lua.org/manual/5.3/manual.html#pdf-math.max </summary>
		/// <param name="x"></param>
		/// <returns></returns>
		public static double max(double[] x)
		{
			if (x == null || x.Length == 0)
				throw new LuaRuntimeException(Properties.Resources.rsNumberExpected, 1, true);

			var r = Double.MinValue;
			for (var i = 0; i < x.Length; i++)
			{
				if (r < x[i])
					r = x[i];
			}

			return r;
		} // func max

		/// <summary>Implementation of http://www.lua.org/manual/5.3/manual.html#pdf-math.min </summary>
		/// <param name="x"></param>
		/// <returns></returns>
		public static double min(double[] x)
		{
			if (x == null || x.Length == 0)
				throw new LuaRuntimeException(Properties.Resources.rsNumberExpected, 1, true);

			var r = Double.MaxValue;
			for (var i = 0; i < x.Length; i++)
			{
				if (r > x[i])
					r = x[i];
			}

			return r;
		} // func min

		/// <summary>Implementation of http://www.lua.org/manual/5.3/manual.html#pdf-math.modf </summary>
		/// <param name="x"></param>
		/// <returns></returns>
		public static LuaResult modf(double x)
		{
			if (x < 0)
			{
				var y = Math.Ceiling(x);
				return new LuaResult(y, y - x);
			}
			else
			{
				var y = Math.Floor(x);
				return new LuaResult(y, x - y);
			}
		} // func modf

		/// <summary>Implementation of http://www.lua.org/manual/5.2/manual.html#pdf-math.pow </summary>
		/// <param name="x"></param>
		/// <param name="y"></param>
		/// <returns></returns>
		public static double pow(double x, double y)
			=> Math.Pow(x, y);

		/// <summary>Implementation of http://www.lua.org/manual/5.3/manual.html#pdf-math.rad </summary>
		/// <param name="x"></param>
		/// <returns></returns>
		public static double rad(double x)
			=> x * Math.PI / 180.0;

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
			=> Math.Sin(x);

		/// <summary>Implementation of http://www.lua.org/manual/5.2/manual.html#pdf-math.sinh </summary>
		/// <param name="x"></param>
		/// <returns></returns>
		public static double sinh(double x)
			=> Math.Sinh(x);

		/// <summary>Implementation of http://www.lua.org/manual/5.3/manual.html#pdf-math.sqrt </summary>
		/// <param name="x"></param>
		/// <returns></returns>
		public static double sqrt(double x)
			=> Math.Sqrt(x);

		/// <summary>Implementation of http://www.lua.org/manual/5.3/manual.html#pdf-math.tan </summary>
		/// <param name="x"></param>
		/// <returns></returns>
		public static double tan(double x)
			=> Math.Tan(x);

		/// <summary>Implementation of http://www.lua.org/manual/5.2/manual.html#pdf-math.tanh </summary>
		/// <param name="x"></param>
		/// <returns></returns>
		public static double tanh(double x)
			=> Math.Tanh(x);

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
			=> m < n;

		/// <summary>Implementation of http://www.lua.org/manual/5.3/manual.html#pdf-math.pi </summary>
		public static double pi => Math.PI;
		/// <summary>Maps Math.E</summary>
		public static double e => Math.E;

		/// <summary>Implementation of http://www.lua.org/manual/5.3/manual.html#pdf-math.mininteger </summary>
		public static int mininteger => Int32.MinValue;
		/// <summary>Implementation of http://www.lua.org/manual/5.3/manual.html#pdf-math.maxinteger </summary>
		public static int maxinteger => Int32.MaxValue;
	} // clas LuaLibraryMath

	#endregion

	#region -- Bitwise Operations -----------------------------------------------------

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
			for (var i = 0; i < ands.Length; i++)
				r &= ands[i];
			return r;
		} // func band

		/// <summary>Implementation of http://www.lua.org/manual/5.2/manual.html#pdf-bit32.bnot </summary>
		/// <param name="x"></param>
		/// <returns></returns>
		public static uint bnot(uint x)
			=> ~x;

		/// <summary>Implementation of http://www.lua.org/manual/5.2/manual.html#pdf-bit32.bor </summary>
		/// <param name="ors"></param>
		/// <returns></returns>
		public static uint bor(params uint[] ors)
		{
			uint r = 0;
			if (ors != null)
			{
				for (var i = 0; i < ors.Length; i++)
					r |= ors[i];
			}
			return r;
		} // func bor

		/// <summary>Implementation of http://www.lua.org/manual/5.2/manual.html#pdf-bit32.btest </summary>
		/// <param name="tests"></param>
		/// <returns></returns>
		public static bool btest(params uint[] tests)
			=> band(tests) != 0;

		/// <summary>Implementation of http://www.lua.org/manual/5.2/manual.html#pdf-bit32.bxor </summary>
		/// <param name="xors"></param>
		/// <returns></returns>
		public static uint bxor(params uint[] xors)
		{
			uint r = 0;
			if (xors != null)
			{
				for (var i = 0; i < xors.Length; i++)
					r ^= xors[i];
			}
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
			=> (n >> field) & CreateBitMask(field, width);

		/// <summary>Implementation of http://www.lua.org/manual/5.2/manual.html#pdf-bit32.replace </summary>
		/// <param name="n"></param>
		/// <param name="v"></param>
		/// <param name="field"></param>
		/// <param name="width"></param>
		/// <returns></returns>
		public static uint replace(uint n, uint v, int field, int width = 1)
		{
			var m = CreateBitMask(field, width) << field;
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

	#region -- Debug functions --------------------------------------------------------

	internal static class LuaLibraryDebug
	{
		public static LuaResult getupvalue(object f, int index)
			=> Lua.RtGetUpValue(f as Delegate, index);

		public static LuaResult upvalueid(object f, int index)
			=> new LuaResult(Lua.RtUpValueId(f as Delegate, index));

		public static LuaResult setupvalue(object f, int index, object v)
			=> new LuaResult(Lua.RtSetUpValue(f as Delegate, index, v));

		public static void upvaluejoin(object f1, int n1, object f2, int n2)
			=> Lua.RtUpValueJoin(f1 as Delegate, n1, f2 as Delegate, n2);
	} // class LuaLibraryDebug

	#endregion

	#region -- class LuaFilePackage ---------------------------------------------------

	/// <summary>default files are not supported.</summary>
	public sealed class LuaFilePackage
	{
		private Encoding defaultEncoding = Encoding.ASCII;

		private LuaFile defaultOutput = null;
		private LuaFile defaultInput = null;
		private LuaFile tempFile = null;

		/// <summary></summary>
		/// <param name="filename"></param>
		/// <param name="mode"></param>
		/// <returns></returns>
		public LuaResult open(string filename, string mode = "r")
		{
			try
			{
				return new LuaResult(LuaFileStream.OpenFile(filename, mode, defaultEncoding));
			}
			catch (Exception e)
			{
				return new LuaResult(null, e.Message);
			}
		} // func open

		/// <summary></summary>
		/// <param name="args"></param>
		/// <returns></returns>
		public LuaResult lines(object[] args)
		{
			if (args == null || args.Length == 0)
				return DefaultInput.lines(null);
			else
				return Lua.GetEnumIteratorResult(new LuaLinesEnumerator(LuaFileStream.OpenFile((string)args[0], "r", defaultEncoding), true, args, 1));
		} // func lines

		/// <summary></summary>
		/// <param name="file"></param>
		/// <returns></returns>
		public LuaResult close(LuaFile file = null)
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

		/// <summary></summary>
		/// <param name="file"></param>
		/// <returns></returns>
		public LuaFile input(object file = null)
			=> InOutOpen(file, defaultEncoding, ref defaultInput);

		/// <summary></summary>
		/// <param name="file"></param>
		/// <returns></returns>
		public LuaFile output(object file = null)
			=> InOutOpen(file, defaultEncoding, ref defaultOutput);

		private static LuaFile InOutOpen(object file, Encoding defaultEncoding, ref LuaFile fileVar)
		{
			switch (file)
			{
				case string fileName:
					fileVar?.close();
					fileVar = LuaFileStream.OpenFile(fileName, "w", defaultEncoding);
					break;
				case LuaFile handle:
					if (handle == defaultInOut.Value)
						fileVar = null;
					else
					{
						fileVar?.close();
						fileVar = handle;
					}
					break;
			}
			return fileVar ?? defaultInOut.Value;
		} // func InOutOpen

		/// <summary></summary>
		public void flush()
			=> DefaultOutput.flush();

		/// <summary></summary>
		/// <param name="args"></param>
		/// <returns></returns>
		public LuaResult read(object[] args)
			=> DefaultInput.read(args) ?? LuaResult.Empty;

		/// <summary></summary>
		/// <param name="args"></param>
		/// <returns></returns>
		public LuaResult write(object[] args)
		 => DefaultOutput.write(args) ?? LuaResult.Empty;

		/// <summary></summary>
		/// <returns></returns>
		public LuaFile tmpfile()
		{
			if (tempFile == null)
				tempFile = tmpfilenew();
			return tempFile;
		} // func read

		/// <summary></summary>
		/// <returns></returns>
		public LuaFile tmpfilenew()
			=> LuaTempFile.Create(Path.GetTempFileName(), defaultEncoding);

		/// <summary></summary>
		/// <param name="obj"></param>
		/// <returns></returns>
		public string type(object obj)
			=> obj is LuaFile f && !f.IsClosed ? "file" : "file closed";

		/// <summary></summary>
		/// <param name="program"></param>
		/// <param name="mode"></param>
		/// <returns></returns>
		public LuaFile popen(string program, string mode = "r")
		{
			LuaLibraryOS.SplitCommand(program, out var fileName, out var arguments);

			var psi = new ProcessStartInfo(fileName, arguments)
			{
				RedirectStandardOutput = mode.IndexOf('r') >= 0,
				RedirectStandardInput = mode.IndexOf('w') >= 0,
				UseShellExecute = false,
				CreateNoWindow = true
			};

			if (psi.RedirectStandardOutput)
				psi.StandardOutputEncoding = defaultEncoding;

			return new LuaFileProcess(Process.Start(psi), psi.RedirectStandardOutput, psi.RedirectStandardInput);
		} // func popen

		/// <summary>Defines the encoding for stdout</summary>
		public Encoding DefaultEncoding
		{
			get => defaultEncoding;
			set
			{
				if (value == null)
					defaultEncoding = Encoding.ASCII;
				else defaultEncoding = value;
			}
		} // prop DefaultEncoding

		private LuaFile DefaultInput => defaultInput ?? defaultInOut.Value;
		private LuaFile DefaultOutput => defaultOutput ?? defaultInOut.Value;

		#region -- class LuaProcessPipe -----------------------------------------------

		private sealed class LuaProcessPipe : LuaFile
		{
			public LuaProcessPipe()
				: base(Console.In, Console.Out)
			{
			}

			protected override void Dispose(bool disposing)
				=> flush();

			public override LuaResult close() => LuaResult.Empty;
		} // class LuaProcessPipe

		#endregion

		private static readonly Lazy<LuaFile> defaultInOut;

		static LuaFilePackage()
		{
			defaultInOut = new Lazy<LuaFile>(
				() => new LuaProcessPipe(),
				true
			);
		} // sctor
	} // class LuaFilePackage

	#endregion

	#region -- class LuaLibraryPackage ------------------------------------------------

	/// <summary></summary>
	public sealed class LuaLibraryPackage
	{
		/// <summary></summary>
		public const string CurrentDirectoryPathVariable = "%currentdirectory%";
		/// <summary></summary>
		public const string ExecutingDirectoryPathVariable = "%executingdirectory%";

		#region -- class LuaLoadedTable -----------------------------------------------

		private class LuaLoadedTable : LuaTable
		{
			private LuaGlobal global;

			public LuaLoadedTable(LuaGlobal global)
			{
				this.global = global;
			} // ctor

			protected override object OnIndex(object key)
			{
				if (global.loaded != null && global.loaded.TryGetValue(key, out var value))
					return value;
				return base.OnIndex(key);
			} // func OnIndex
		} // class LuaLoadedTable

		#endregion

		private readonly object packageLock = new object();
		private Dictionary<string, WeakReference> loadedModuls = null;

		private string[] paths;
		private LuaCompileOptions compileOptions = null;

		/// <summary></summary>
		/// <param name="global"></param>
		public LuaLibraryPackage(LuaGlobal global)
		{
			this.loaded = new LuaLoadedTable(global);
			this.path = CurrentDirectoryPathVariable;
		} // ctor

		internal LuaChunk LuaRequire(LuaGlobal global, string moduleName)
		{
			if (String.IsNullOrEmpty(moduleName))
				return null;

			if (LuaRequireFindFile(moduleName, out var fileName, out var stamp))
			{
				lock (packageLock)
				{
					LuaChunk chunk;
					var cacheId = fileName + ";" + stamp.ToString("o");

					// is the modul loaded
					if (loadedModuls == null 
						|| !loadedModuls.TryGetValue(cacheId, out var rc) 
						|| !rc.IsAlive)
					{
						// compile the modul
						chunk = global.Lua.CompileChunk(fileName, compileOptions);

						// Update Cache
						if (loadedModuls == null)
							loadedModuls = new Dictionary<string, WeakReference>();
						loadedModuls[cacheId] = new WeakReference(chunk);
					}
					else
						chunk = (LuaChunk)rc.Target;

					return chunk;
				}
			}
			else
				return null;
		} // func LuaRequire

		private DateTime? LuaRequireCheckFile(ref string fileName)
		{
			try
			{
				// replace variables
				if (fileName.Contains(CurrentDirectoryPathVariable))
					fileName = fileName.Replace(CurrentDirectoryPathVariable, Environment.CurrentDirectory);
				if (fileName.Contains(ExecutingDirectoryPathVariable))
					fileName = fileName.Replace(ExecutingDirectoryPathVariable, System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));

				// check if the file exists
				if (!File.Exists(fileName))
					return null;

				// get the time stamp
				return File.GetLastWriteTime(fileName);
			}
			catch (IOException)
			{
				return null;
			}
		} // func LuaRequireCheckFile

		private bool LuaRequireFindFile(string modulName, out string fileName, out DateTime stamp)
		{
			stamp = DateTime.MinValue;
			fileName = null;

			// replace dots blind to directory seperator, like lua it does.
			if (modulName.IndexOf(System.IO.Path.DirectorySeparatorChar) >= 0)
				modulName = modulName.Replace('.', System.IO.Path.DirectorySeparatorChar);
			// add .lua
			if (!modulName.EndsWith(".lua", StringComparison.OrdinalIgnoreCase))
				modulName += ".lua";

			foreach (var c in paths)
			{
				if (String.IsNullOrEmpty(c))
					continue;
				else
				{
					var testFileName = System.IO.Path.Combine(c, modulName);
					var testStamp = LuaRequireCheckFile(ref testFileName);
					if (testStamp.HasValue)
					{
						if (fileName == null || stamp < testStamp.Value)
						{
							fileName = testFileName;
							stamp = testStamp.Value;
						}
					}
				}
			}

			return fileName != null;
		} // func LuaRequireFindFile

		/// <summary></summary>
		public LuaTable loaded { get; private set; }
		/// <summary></summary>
		public string path
		{
			get => String.Join(";", paths);
			set
			{
				paths = String.IsNullOrEmpty(value)
					? null
					: value.Split(';');
			}
		} // prop Path

		/// <summary></summary>
		public string[] Path => paths;
		/// <summary></summary>
		public LuaCompileOptions CompileOptions { get => compileOptions; set => compileOptions = value; }
	} // class LuaLibraryPackage

	#endregion

	#region -- Operating System Facilities --------------------------------------------

	internal static class LuaLibraryOS
	{
		private static readonly DateTime unixStartTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Local);

		public static LuaResult clock()
			=> new LuaResult(Process.GetCurrentProcess().TotalProcessorTime.TotalSeconds);

		/// <summary>Converts a number representing the date and time back to some higher-level representation.</summary>
		/// <param name="format">Format string. Same format as the C <see href="http://www.cplusplus.com/reference/ctime/strftime/">strftime()</see> function.</param>
		/// <param name="time">Numeric date-time. It defaults to the current date and time.</param>
		/// <returns>Formatted date string, or table of time information.</returns>
		/// <remarks>by PapyRef</remarks>
		public static object date(string format, object time)
		{
			// Unix timestamp is seconds past epoch. Epoch date for time_t is 00:00:00 UTC, January 1, 1970.
			DateTime dt;

			var toUtc = format != null && format.Length > 0 && format[0] == '!';

			if (time == null)
				dt = toUtc ? DateTime.UtcNow : DateTime.Now;
			else if (time is DateTime dt2)
			{
				dt = dt2;
				switch (dt.Kind)
				{
					case DateTimeKind.Utc:
						if (!toUtc)
							dt = dt.ToLocalTime();
						break;
					default:
						if (toUtc)
							dt = dt.ToUniversalTime();
						break;
				}
			}
			else
			{
				dt = unixStartTime.AddSeconds((long)Lua.RtConvertValue(time, typeof(long)));
				if (toUtc)
					dt = dt.ToUniversalTime();
			}

			// Date and time expressed as coordinated universal time (UTC).
			if (toUtc)
				format = format.Substring(1);

			if (String.Compare(format, "*t", false) == 0)
			{
				var lt = new LuaTable
				{
					["year"] = dt.Year,
					["month"] = dt.Month,
					["day"] = dt.Day,
					["hour"] = dt.Hour,
					["min"] = dt.Minute,
					["sec"] = dt.Second,
					["wday"] = (int)dt.DayOfWeek,
					["yday"] = dt.DayOfYear,
					["isdst"] = (dt.Kind == DateTimeKind.Local ? true : false)
				};
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
				ts = DateTime.Now.Subtract(unixStartTime);
			}
			else
			{
				try
				{
					ts = datetime(table).Subtract(unixStartTime);
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
			switch (time)
			{
				case LuaTable table:
					return new DateTime(
						table.ContainsKey("year") ? (int)table["year"] < 1970 ? 1970 : (int)table["year"] : 1970,
						table.ContainsKey("month") ? (int)table["month"] : 1,
						table.ContainsKey("day") ? (int)table["day"] : 1,
						table.ContainsKey("hour") ? (int)table["hour"] : 0,
						table.ContainsKey("min") ? (int)table["min"] : 0,
						table.ContainsKey("sec") ? (int)table["sec"] : 0,
						table.ContainsKey("isdst") ? (table.ContainsKey("isdst") == true) ? DateTimeKind.Local : DateTimeKind.Utc : DateTimeKind.Local
					);
				case int i32:
					return unixStartTime.AddSeconds(i32);
				case long i64:
					return unixStartTime.AddSeconds(i64);
				case double d:
					return unixStartTime.AddSeconds(d);
				default:
					throw new ArgumentException();
			}
		} // func datetime

		/// <summary>Calculate the number of seconds between time t1 to time t2.</summary>
		/// <param name="t2">Higher bound of the time interval whose length is calculated.</param>
		/// <param name="t1">Lower bound of the time interval whose length is calculated. If this describes a time point later than end, the result is negative.</param>
		/// <returns>The number of seconds from time t1 to time t2. In other words, the result is t2 - t1.</returns>
		/// <remarks>by PapyRef</remarks>
		public static long difftime(object t2, object t1)
		{
			var time2 = Convert.ToInt64(t2 is LuaTable ? time((LuaTable)t2)[0] : t2);
			var time1 = Convert.ToInt64(t1 is LuaTable ? time((LuaTable)t1)[0] : t1);

			return time2 - time1;
		} // func difftime

		internal static void SplitCommand(string command, out string fileName, out string arguments)
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
				var pos = command.IndexOf('"', 1);
				if (pos == -1)
				{
					fileName = command;
					arguments = null;
				}
				else
				{
					fileName = command.Substring(1, pos - 1).Trim();
					arguments = command.Substring(pos + 1).Trim();
				}
			}
			else
			{
				fileName = Path.Combine(Environment.SystemDirectory, "cmd.exe");
				arguments = "/c " + command;
			}
		} // proc SplitCommand

		public static LuaResult execute(string command, Func<string, LuaResult> output, Func<string, LuaResult> error)
		{
			if (command == null)
				return new LuaResult(true);
			try
			{
				SplitCommand(command, out var fileName, out var arguments);
				using (var p = Process.Start(fileName, arguments))
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
			=> Environment.Exit(code);

		public static string getenv(string varname)
			=> Environment.GetEnvironmentVariable(varname);

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
			=> throw new NotImplementedException();

		public static string tmpname()
			=> Path.GetTempFileName();
	} // class LuaLibraryOS

	#endregion

	#region -- class LuaLibraryImplementTypes -----------------------------------------

	///// <summary></summary>
	//public sealed class LuaLibraryImplementTypes
	//{
	//	private readonly object threadLock = new object();
	//	private readonly Lua lua;

	//	private AssemblyBuilder assembly = null;
	//	private ModuleBuilder module = null;

	//	public LuaLibraryImplementTypes(Lua lua)
	//	{
	//		this.lua = lua;
	//	} // ctor

	//	private void CheckDynamicAssembly()
	//	{
	//		lock (threadLock)
	//		{
	//			if (assembly == null)
	//			{
	//				assembly = AppDomain.CurrentDomain.DefineDynamicAssembly(LuaDeskop.LuaDynamicName, AssemblyBuilderAccess.RunAndCollect);
	//				module = assembly.DefineDynamicModule("lua", true);
	//			}
	//		}
	//	} // proc CheckDynamicAssembly

	//	public LuaTable implement(params LuaType[] types)
	//	{
	//		if (types.Length == 0)
	//			return new LuaTable();

	//		// check if all tyoes are interfaces 
	//		foreach (var t in types)
	//		{
	//			if (t.Type == null)
	//				throw new ArgumentException(); // todo:
	//			if (!t.Type.IsInterface)
	//				throw new ArgumentException(); // todo:
	//		}

	//		CheckDynamicAssembly(); // create the dynamic assembly for the type implementations

	//		// define the new type or get it from cache
	//		var typeDefine = module.DefineType("test", TypeAttributes.NotPublic | TypeAttributes.Sealed, typeof(LuaTable)); // todo:

	//		foreach (var c in types)
	//		{
	//			var interfaceType = c.Type;
	//			typeDefine.AddInterfaceImplementation(interfaceType);
	//		}

	//		var methodDefine = typeDefine.DefineMethod("CompareTo", MethodAttributes.Public);


	//		var expr = Expression.Lambda(Expression.GetFuncType(new Type[] { typeof(object), typeof(object), typeof(int) }),
	//			Expression.Constant(42),
	//			"CompareTo",
	//			new ParameterExpression[] {
	//				Expression.Parameter(typeof(object)),
	//				Expression.Parameter(typeof(object))
	//			}
	//		);

	//		expr.CompileToMethod(methodDefine);

	//		var dynamicType = typeDefine.CreateType();

	//		return (LuaTable)Activator.CreateInstance(dynamicType);
	//	} // func implement
	//} // class LuaLibraryImplementTypes

	//	[TestMethod]
	//	public void ImplementInterface01()
	//	{
	//		using (var l = new Lua())
	//		{
	//			var t = new LuaLibraryImplementTypes(l);
	//			var table = t.implement(LuaType.GetType(typeof(System.Collections.IComparer)));

	//			var g = l.CreateEnvironment();
	//			g.SetMemberValue("t", table);
	//			g.DoChunk("function t:CompareTo(x, y) return 23; end;", "test");


	//			Assert.AreEqual(23, ((System.Collections.IComparer)table).Compare(1, 1));
	//		}
	//	}

	#endregion
}

#pragma warning restore IDE1006 // Naming Styles
