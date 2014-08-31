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
				LuaExceptionData data = LuaExceptionData.GetData(this);
				if (data == null)
					return base.StackTrace;
				else
					return data.GetStackTrace(iLevel, lSkipClrFrames);
			}
		} // prop StackTrace

		/// <summary>Source file name</summary>
		public override string FileName
		{
			get
			{
				LuaExceptionData data = LuaExceptionData.GetData(this);
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
				LuaExceptionData data = LuaExceptionData.GetData(this);
				if (data == null || iLevel < 0 || iLevel >= data.Count)
					return 0;
				else
					return data[iLevel].LineNumber;
			}
		} // prop Line

		/// <summary>Source column</summary>
		public override int Column
		{
			get
			{
				LuaExceptionData data = LuaExceptionData.GetData(this);
				if (data == null || iLevel < 0 || iLevel >= data.Count)
					return 0;
				else
					return data[iLevel].ColumnNumber;
			}
		} // prop Column
	} // class LuaRuntimeException

	#endregion

	#region -- enum LuaStackFrameType ---------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public enum LuaStackFrameType
	{
		/// <summary></summary>
		Unknown,
		/// <summary></summary>
		Clr,
		/// <summary></summary>
		Lua
	} // enum LuaStackFrameType

	#endregion

	#region -- class LuaStackFrame ------------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	[Serializable]
	public class LuaStackFrame
	{
		private readonly StackFrame frame;
		private readonly ILuaDebugInfo info;

		internal LuaStackFrame(StackFrame frame, ILuaDebugInfo info)
		{
			this.frame = frame;
			this.info = info;
		} // ctor

		/// <summary></summary>
		/// <param name="sb"></param>
		/// <param name="lPrintType"></param>
		/// <returns></returns>
		public StringBuilder ToString(StringBuilder sb, bool lPrintType)
		{
			sb.Append(Properties.Resources.rsStackTraceAt);

			if (lPrintType)
				sb.Append('[').Append(Type.ToString()[0]).Append("] ");

			// at type if it is clr or unknown
			MethodBase m = Method;
			if (m != null && info == null && m.DeclaringType != null)
				sb.Append(m.DeclaringType.FullName).Append('.');

			// at type method
			if (m == null)
				sb.Append(Properties.Resources.rsStackTraceUnknownMethod);
			else
			{
				bool lComma = false;
				if (info != null)
				{
					sb.Append(m.Name);
					if (m.IsGenericMethod)
					{
						sb.Append('<');
						foreach (Type g in m.GetGenericArguments())
						{
							if (lComma)
								sb.Append(',');
							else
								lComma = true;
							sb.Append(g.Name);
						}
						sb.Append('>');
					}
				}
				else
					sb.Append(MethodName);

				// print parameters
				lComma = false;
				sb.Append('(');
				foreach (ParameterInfo pi in m.GetParameters())
				{
					if (typeof(Closure).IsAssignableFrom(pi.ParameterType))
						continue;

					if (lComma)
						sb.Append(',');
					else
						lComma = true;

					sb.Append(pi.ParameterType.Name);

					if (!String.IsNullOrEmpty(pi.Name))
					{
						sb.Append(' ');
						sb.Append(pi.Name);
					}
				}
				sb.Append(')');
			}

			// and now the fileinformation
			if (!String.IsNullOrEmpty(FileName))
			{
				sb.Append(Properties.Resources.rsStackTraceLine);
				sb.Append(FileName);
				if (LineNumber > 0)
					sb.Append(':').Append(LineNumber);
				if (ColumnNumber > 0)
					sb.Append(':').Append(ColumnNumber);
			}

			return sb;
		} // func ToString

		/// <summary></summary>
		/// <returns></returns>
		public override string ToString()
		{
			StringBuilder sb = new StringBuilder();
			ToString(sb, true);
			return sb.ToString();
		} // func ToString

		private string GetChunkName()
		{
			string sChunk = info.ChunkName;
			string sMethod = Method.Name;

			if (sMethod.StartsWith(sChunk))
				return sChunk;
			else
				return sChunk + "#" + sMethod;
		} // func GetChunkName

		/// <summary></summary>
		public LuaStackFrameType Type
		{
			get
			{
				if (info != null)
					return LuaStackFrameType.Lua;
				else if (frame.GetFileLineNumber() > 0)
					return LuaStackFrameType.Clr;
				else
					return LuaStackFrameType.Unknown;
			}
		} // func Type

		/// <summary></summary>
		public string MethodName { get { return info == null ? Method.Name : GetChunkName(); } }
		/// <summary></summary>
		public MethodBase Method { get { return frame.GetMethod(); } }
		/// <summary></summary>
		public int ILOffset { get { return frame.GetILOffset(); } }
		/// <summary></summary>
		public int NativeOffset { get { return frame.GetNativeOffset(); } }
		/// <summary></summary>
		public string FileName { get { return info == null ? frame.GetFileName() : info.FileName; } }
		/// <summary></summary>
		public int ColumnNumber { get { return info == null ? frame.GetFileColumnNumber() : info.Column; } }
		/// <summary></summary>
		public int LineNumber { get { return info == null ? frame.GetFileLineNumber() : info.Line; } }
	} // class LuaStackFrame

	#endregion

	#region -- class LuaExceptionData ---------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Class to extent the any Exception with Lua debug information.</summary>
	[Serializable]
	public sealed class LuaExceptionData : IList<LuaStackFrame>
	{
		private LuaStackFrame[] stackTrace;

		internal LuaExceptionData(LuaStackFrame[] stackTrace)
		{
			this.stackTrace = stackTrace;
		} // ctor

		/// <summary></summary>
		/// <param name="item"></param>
		/// <returns></returns>
		public int IndexOf(LuaStackFrame item) { return Array.IndexOf(stackTrace, item); }
		/// <summary></summary>
		/// <param name="item"></param>
		/// <returns></returns>
		public bool Contains(LuaStackFrame item) { return IndexOf(item) != -1; }
		/// <summary></summary>
		/// <param name="array"></param>
		/// <param name="arrayIndex"></param>
		public void CopyTo(LuaStackFrame[] array, int arrayIndex) { Array.Copy(stackTrace, 0, array, arrayIndex, Count); }

		/// <summary></summary>
		/// <returns></returns>
		public IEnumerator<LuaStackFrame> GetEnumerator()
		{
			int iLength = Count;
			for (int i = 0; i < iLength; i++)
				yield return stackTrace[i];
		} // func GetEnumerator

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return stackTrace.GetEnumerator();
		} // func System.Collections.IEnumerable.GetEnumerator

		/// <summary>Stackframes</summary>
		public int Count { get { return stackTrace.Length; } }
		/// <summary>Always <c>true</c></summary>
		public bool IsReadOnly { get { return true; } }

		/// <summary>Get StackTrace format as an string.</summary>
		/// <param name="iLuaSkipFrames">Lua frame to skip.</param>
		/// <param name="lSkipClrFrames">Skip all clr-frames.</param>
		/// <returns>Formatted stackframe</returns>
		public string GetStackTrace(int iLuaSkipFrames, bool lSkipClrFrames)
		{
			bool lUnknownFrame = false;
			StringBuilder sb = new StringBuilder();
			foreach (LuaStackFrame c in this)
			{
				// Skip the frames
				if (iLuaSkipFrames > 0)
				{
					if (c.Type == LuaStackFrameType.Lua)
						iLuaSkipFrames--;
				}
				// Skip unknwon frames
				else if (lUnknownFrame)
				{
					if (c.Type == LuaStackFrameType.Unknown)
						continue;
					else if (!lSkipClrFrames)
						sb.AppendLine(Properties.Resources.rsStackTraceInternal);
				}
				else
				{
					if (c.Type == LuaStackFrameType.Unknown)
					{
						lUnknownFrame = true;
						continue;
					}
				}
				if (iLuaSkipFrames <= 0 && (!lSkipClrFrames || c.Type == LuaStackFrameType.Lua))
					c.ToString(sb, !lSkipClrFrames).AppendLine();
			}
			return sb.ToString();
		} // func GetStackTrace

		/// <summary></summary>
		/// <param name="index"></param>
		/// <returns></returns>
		public LuaStackFrame this[int index]
		{
			get { return stackTrace[index]; }
			set { throw new NotImplementedException(); }
		} // this

		/// <summary>Formatted StackTrace</summary>
		public string StackTrace
		{
			get { return GetStackTrace(0, false); }
		} // prop StackTrace

		void IList<LuaStackFrame>.Insert(int index, LuaStackFrame item) { throw new NotImplementedException(); }
		void IList<LuaStackFrame>.RemoveAt(int index) { throw new NotImplementedException(); }
		void ICollection<LuaStackFrame>.Add(LuaStackFrame item) { throw new NotImplementedException(); }
		void ICollection<LuaStackFrame>.Clear() { throw new NotImplementedException(); }
		bool ICollection<LuaStackFrame>.Remove(LuaStackFrame item) { throw new NotImplementedException(); }

		// -- Static --------------------------------------------------------------

		private static readonly object luaStackTraceDataKey = new object();

		/// <summary>Get the LuaStackFrame from a .net StackFrame, if someone exists. In the other case, the return LuaStackFrame is only proxy to the orginal frame.</summary>
		/// <param name="frame">.net stackframe</param>
		/// <returns>LuaStackFrame</returns>
		public static LuaStackFrame GetStackFrame(StackFrame frame)
		{
			ILuaDebugInfo info = null;

			// find the lua debug info
			MethodBase method = frame.GetMethod();
			LuaChunk chunk = Lua.GetChunkFromMethodInfo(method);
			if (chunk != null)
				info = chunk.GetDebugInfo(method, frame.GetILOffset());

			return new LuaStackFrame(frame, info);
		} // func GetStackFrame

		/// <summary>Converts a whole StackTrace.</summary>
		/// <param name="trace">.net stacktrace</param>
		/// <returns>LuaStackFrames</returns>
		public static LuaStackFrame[] GetStackTrace(StackTrace trace)
		{
			LuaStackFrame[] frames = new LuaStackFrame[trace.FrameCount];
			int iLength = frames.Length;
			for (int i = 0; i < iLength; i++)
				frames[i] = GetStackFrame(trace.GetFrame(i));
			return frames;
		} // func GetStackTrace

		/// <summary>Retrieves the debug information for an exception.</summary>
		/// <param name="ex">Exception</param>
		/// <returns>Debug Information</returns>
		public static LuaExceptionData GetData(Exception ex)
		{
			LuaExceptionData data = ex.Data[luaStackTraceDataKey] as LuaExceptionData;
			if (data == null)
			{
				// retrieve the stacktrace
				data = new LuaExceptionData(GetStackTrace(new StackTrace(ex, true)));

				// set the data
				ex.Data[luaStackTraceDataKey] = data;
			}
			return data;
		} // func GetData
	} // class LuaExceptionData

	#endregion
}
