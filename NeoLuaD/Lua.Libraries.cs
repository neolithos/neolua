using System;
using System.Diagnostics;
using System.IO;

namespace Neo.IronLua
{
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

			bool lUtc = format != null && format.Length > 0 && format[0] == '!';

			if (time == null)
				dt = lUtc ? DateTime.UtcNow : DateTime.Now;
			else if (time is DateTime)
			{
				dt = (DateTime)time;
				switch (dt.Kind)
				{
					case DateTimeKind.Utc:
						if (!lUtc)
							dt = dt.ToLocalTime();
						break;
					default:
						if (lUtc)
							dt = dt.ToUniversalTime();
						break;
				}
			}
			else
				dt = dtUnixStartTime.AddSeconds((long)Convert.ChangeType(time, typeof(long))).ToLocalTime();

			// Date and time expressed as coordinated universal time (UTC).
			if (lUtc)
				format = format.Substring(1);

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
