using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Text;

namespace Neo.IronLua
{
	#region -- interface ILuaExceptionData ----------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Helps to make an exception extension visible for LuaRuntimeException.</summary>
	public interface ILuaExceptionData
	{
		/// <summary>Returns the formatted stacktrace.</summary>
		/// <param name="iLevel">Level to start.</param>
		/// <param name="lSkipSClrFrame">Only Lua frames.</param>
		/// <returns></returns>
		string FormatStackTrace(int iLevel = 0, bool lSkipSClrFrame = true);

		/// <summary>Returns the debug info from a frame.</summary>
		/// <param name="iLevel"></param>
		/// <returns></returns>
		ILuaDebugInfo this[int iLevel] { get; }
		
		/// <summary>Stacktrace length.</summary>
		int Count { get; }
	} // interface ILuaExceptionData

	#endregion

	#region -- class LuaException -------------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Base class for Lua-Exceptions</summary>
	public abstract class LuaException : Exception
	{
		/// <summary>Base class for Lua-Exceptions</summary>
		/// <param name="sMessage">Text</param>
		/// <param name="innerException">Inner Exception</param>
		internal LuaException(string sMessage, Exception innerException)
			: base(sMessage, innerException)
		{
		} // ctor

		/// <summary>Source file name</summary>
		public abstract string FileName { get; }
		/// <summary>Source line</summary>
		public abstract int Line { get; }
		/// <summary>Source column</summary>
		public abstract int Column { get; }
	} // class LuaException

	#endregion

	#region -- class LuaParseException --------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Lua Exception for parse errors.</summary>
	public class LuaParseException : LuaException
	{
		private string sFileName;
		private int iLine;
		private int iColumn;
		private long iIndex;

		/// <summary>Lua Exception for parse errors.</summary>
		/// <param name="position"></param>
		/// <param name="sMessage"></param>
		/// <param name="innerException"></param>
		internal LuaParseException(Position position, string sMessage, Exception innerException)
			: base(sMessage, innerException)
		{
			this.sFileName = position.FileName;
			this.iLine = position.Line;
			this.iColumn = position.Col;
			this.iIndex = position.Index;
		} // ctor

		/// <summary>Source file name</summary>
		public override string FileName { get { return sFileName; } }
		/// <summary>Source line</summary>
		public override int Line { get { return iLine; } }
		/// <summary>Source column</summary>
		public override int Column { get { return iColumn; } }
		/// <summary>Source index</summary>
		public long Index { get { return iIndex; } }
	} // class LuaParseException

	#endregion

	#region -- class LuaRuntimeException ------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Lua Exception for runtime errors.</summary>
	public class LuaRuntimeException : LuaException
	{
		private int iLevel = 0;
		private bool lSkipClrFrames = false;

		/// <summary>Lua Exception for runtime errors.</summary>
		/// <param name="sMessage">Error message</param>
		/// <param name="innerException">Inner Exception</param>
		internal LuaRuntimeException(string sMessage, Exception innerException)
			: base(sMessage, innerException)
		{
		} // ctor

		/// <summary>Lua Exception for runtime errors.</summary>
		/// <param name="sMessage">Error message</param>
		/// <param name="iLevel">Frame that should skip.</param>
		/// <param name="lSkipClrFrames">Should the stacktrace show clr frames.</param>
		internal LuaRuntimeException(string sMessage, int iLevel, bool lSkipClrFrames)
			: base(sMessage, null)
		{
			this.iLevel = iLevel;
			this.lSkipClrFrames = lSkipClrFrames;
		} // ctor

		/// <summary>Returns the Lua StackTrace</summary>
		public override string StackTrace
		{
			get
			{
				var data = Data[ExceptionDataKey] as ILuaExceptionData;
				if (data == null)
				{
					if (iLevel == 0)
						return base.StackTrace;
					else
					{
						string sStackTrace = base.StackTrace;
						if (String.IsNullOrEmpty(sStackTrace))
							return sStackTrace;
						else
						{
							string[] lines = sStackTrace.Replace(Environment.NewLine, "\n").Split('\n');
							if (iLevel < lines.Length)
								return String.Join(Environment.NewLine, lines, iLevel, lines.Length - iLevel);
							else
								return sStackTrace;
						}
					}
				}
				else
					return data.FormatStackTrace(iLevel, lSkipClrFrames);
			}
		} // prop StackTrace

		/// <summary>Source file name</summary>
		public override string FileName
		{
			get
			{
				var data = Data[ExceptionDataKey] as ILuaExceptionData;
				if (data == null || iLevel < 0 || iLevel >= data.Count)
					return null;
				else
					return data[iLevel].FileName;
			}
		} // pro FileName

		/// <summary>Source line</summary>
		public override int Line
		{
			get
			{
				var data = Data[ExceptionDataKey] as ILuaExceptionData;
				if (data == null || iLevel < 0 || iLevel >= data.Count)
					return 0;
				else
					return data[iLevel].Line;
			}
		} // prop Line

		/// <summary>Source column</summary>
		public override int Column
		{
			get
			{
				var data = Data[ExceptionDataKey] as ILuaExceptionData;
				if (data == null || iLevel < 0 || iLevel >= data.Count)
					return 0;
				else
					return data[iLevel].Column;
			}
		} // prop Column

		/// <summary>Key of the ILuaExceptionData.</summary>
		public static readonly object ExceptionDataKey = new object();
	} // class LuaRuntimeException

	#endregion
}
