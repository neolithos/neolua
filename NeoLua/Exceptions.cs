using System;

namespace Neo.IronLua
{
	#region -- interface ILuaExceptionData --------------------------------------------

	/// <summary>Helps to make an exception extension visible for LuaRuntimeException.</summary>
	public interface ILuaExceptionData
	{
		/// <summary>Returns the formatted stacktrace.</summary>
		/// <param name="skipFrames">Level to start.</param>
		/// <param name="skipSClrFrame">Only Lua frames.</param>
		/// <returns></returns>
		string FormatStackTrace(int skipFrames = 0, bool skipSClrFrame = true);

		/// <summary>Returns the debug info from a frame.</summary>
		/// <param name="frame"></param>
		/// <returns></returns>
		ILuaDebugInfo this[int frame] { get; }
		
		/// <summary>Stacktrace length.</summary>
		int Count { get; }
	} // interface ILuaExceptionData

	#endregion

	#region -- class LuaException -----------------------------------------------------

	/// <summary>Base class for Lua-Exceptions</summary>
	public abstract class LuaException : Exception
	{
		/// <summary>Base class for Lua-Exceptions</summary>
		/// <param name="sMessage">Text</param>
		/// <param name="innerException">Inner Exception</param>
		protected LuaException(string sMessage, Exception innerException)
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

	#region -- class LuaParseException ------------------------------------------------

	/// <summary>Lua Exception for parse errors.</summary>
	public sealed class LuaParseException : LuaException
	{
		private readonly string fileName;
		private readonly int line;
		private readonly int column;
		private readonly long index;

		/// <summary>Lua Exception for parse errors.</summary>
		/// <param name="position"></param>
		/// <param name="message"></param>
		/// <param name="innerException"></param>
		internal LuaParseException(Position position, string message, Exception innerException)
			: base(message, innerException)
		{
			this.fileName = position.FileName;
			this.line = position.Line;
			this.column = position.Col;
			this.index = position.Index;
		} // ctor

		/// <summary>Source file name</summary>
		public override string FileName => fileName;
		/// <summary>Source line</summary>
		public override int Line => line;
		/// <summary>Source column</summary>
		public override int Column => column;
		/// <summary>Source index</summary>
		public long Index => index;
	} // class LuaParseException

	#endregion

	#region -- class LuaRuntimeException ----------------------------------------------

	/// <summary>Lua Exception for runtime errors.</summary>
	public class LuaRuntimeException : LuaException
	{
		private readonly int level = 0;
		private readonly bool skipClrFrames = false;

		/// <summary>Lua Exception for runtime errors.</summary>
		/// <param name="message">Error message</param>
		/// <param name="innerException">Inner Exception</param>
		public LuaRuntimeException(string message, Exception innerException)
			: base(message, innerException)
		{
		} // ctor

		/// <summary>Lua Exception for runtime errors.</summary>
		/// <param name="message">Error message</param>
		/// <param name="level">Frame that should skip.</param>
		/// <param name="skipClrFrames">Should the stacktrace show clr frames.</param>
		public LuaRuntimeException(string message, int level, bool skipClrFrames)
			: base(message, null)
		{
			this.level = level;
			this.skipClrFrames = skipClrFrames;
		} // ctor

		/// <summary>Returns the Lua StackTrace</summary>
		public override string StackTrace
		{
			get
			{
				if (Data[ExceptionDataKey] is ILuaExceptionData data)
					return data.FormatStackTrace(level, skipClrFrames);
				else
				{
					if (level == 0)
						return base.StackTrace;
					else
					{
						var stackTrace = base.StackTrace;
						if (String.IsNullOrEmpty(stackTrace))
							return stackTrace;
						else
						{
							var lines = stackTrace.Replace(Environment.NewLine, "\n").Split('\n');
							return level < lines.Length
								? String.Join(Environment.NewLine, lines, level, lines.Length - level)
								: stackTrace;
						}
					}
				}
			}
		} // prop StackTrace

		/// <summary>Source file name</summary>
		public override string FileName
			=> Data[ExceptionDataKey] is ILuaExceptionData data && level >= 0 && level < data.Count
				? data[level].FileName
				: null;

		/// <summary>Source line</summary>
		public override int Line
				=> Data[ExceptionDataKey] is ILuaExceptionData data && level >= 0 && level < data.Count
				? data[level].Line
				: 0;

		/// <summary>Source column</summary>
		public override int Column
				=> Data[ExceptionDataKey] is ILuaExceptionData data && level >= 0 && level < data.Count
				? data[level].Column
				: 0;

		/// <summary>Key of the ILuaExceptionData.</summary>
		public static readonly object ExceptionDataKey = new object();
	} // class LuaRuntimeException

	#endregion
}
