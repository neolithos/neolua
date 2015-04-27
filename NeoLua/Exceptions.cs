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
				// todo:
				//LuaExceptionData data = LuaExceptionData.GetData(this);
				//if (data == null)
					return base.StackTrace;
				//else
				//	return data.GetStackTrace(iLevel, lSkipClrFrames);
			}
		} // prop StackTrace

		/// <summary>Source file name</summary>
		public override string FileName
		{
			get
			{
				// todo:
				//LuaExceptionData data = LuaExceptionData.GetData(this);
				//if (data == null || iLevel < 0 || iLevel >= data.Count)
					return null;
				//else
				//	return data[iLevel].FileName;
			}
		} // pro FileName

		/// <summary>Source line</summary>
		public override int Line
		{
			get
			{
				//LuaExceptionData data = LuaExceptionData.GetData(this);
				//if (data == null || iLevel < 0 || iLevel >= data.Count)
					return 0;
				//else
				//	return data[iLevel].LineNumber;
			}
		} // prop Line

		/// <summary>Source column</summary>
		public override int Column
		{
			get
			{
				//LuaExceptionData data = LuaExceptionData.GetData(this);
				//if (data == null || iLevel < 0 || iLevel >= data.Count)
					return 0;
				//else
				//	return data[iLevel].ColumnNumber;
			}
		} // prop Column
	} // class LuaRuntimeException

	#endregion
}
