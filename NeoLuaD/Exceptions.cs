using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace Neo.IronLua
{
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
	public class LuaStackFrame : ILuaDebugInfo
	{
		private readonly StackFrame frame;
		private readonly ILuaDebugInfo info;

		/// <summary></summary>
		/// <param name="frame"></param>
		/// <param name="info"></param>
		public LuaStackFrame(StackFrame frame, ILuaDebugInfo info)
		{
			this.frame = frame;
			this.info = info;
		} // ctor

		/// <summary></summary>
		/// <param name="sb"></param>
		/// <param name="printType"></param>
		/// <returns></returns>
		public StringBuilder ToString(StringBuilder sb, bool printType)
		{
			sb.Append(Properties.Resources.rsStackTraceAt);

			if (printType)
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
				var comma = false;
				if (info != null)
				{
					sb.Append(m.Name);
					if (m.IsGenericMethod)
					{
						sb.Append('<');
						foreach (Type g in m.GetGenericArguments())
						{
							if (comma)
								sb.Append(',');
							else
								comma = true;
							sb.Append(g.Name);
						}
						sb.Append('>');
					}
				}
				else
					sb.Append(MethodName);

				// print parameters
				comma = false;
				sb.Append('(');
				foreach (ParameterInfo pi in m.GetParameters())
				{
					if (typeof(Closure).IsAssignableFrom(pi.ParameterType))
						continue;

					if (comma)
						sb.Append(',');
					else
						comma = true;

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
			var chunkName = info.ChunkName;
			var methodName = Method.Name;

			if (methodName.StartsWith(chunkName))
				return chunkName;
			else
				return chunkName + "#" + methodName;
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

		string ILuaDebugInfo.ChunkName => MethodName;
		int ILuaDebugInfo.Line => LineNumber;
		int ILuaDebugInfo.Column => ColumnNumber;
	} // class LuaStackFrame

	#endregion

	#region -- class LuaExceptionData ---------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Class to extent the any Exception with Lua debug information.</summary>
	[Serializable]
	public sealed class LuaExceptionData : IList<LuaStackFrame>, ILuaExceptionData
	{
		private LuaStackFrame[] stackTrace;

		/// <summary>Creates a exception data with a stack trace.</summary>
		/// <param name="stackTrace"></param>
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

		/// <summary>Change the StackTrace</summary>
		/// <param name="stackTrace"></param>
		public void UpdateStackTrace(LuaStackFrame[] newStackTrace)
		{
			stackTrace = new LuaStackFrame[newStackTrace.Length];
			newStackTrace.CopyTo(stackTrace, 0);
		} // porc UpdateStackTrace

		/// <summary></summary>
		/// <returns></returns>
		public IEnumerator<LuaStackFrame> GetEnumerator()
		{
			var length = Count;
			for (int i = 0; i < length; i++)
				yield return stackTrace[i];
		} // func GetEnumerator

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
			=> stackTrace.GetEnumerator();

		/// <summary>Stackframes</summary>
		public int Count { get { return stackTrace.Length; } }
		/// <summary>Always <c>true</c></summary>
		public bool IsReadOnly { get { return true; } }

		[Obsolete("Use FormatStackTrace")]
		public string GetStackTrace(int luaSkipFrames, bool skipClrFrames)
			=> FormatStackTrace(luaSkipFrames, skipClrFrames);

		/// <summary>Get StackTrace format as an string.</summary>
		/// <param name="luaSkipFrames">Lua frame to skip.</param>
		/// <param name="skipClrFrames">Skip all clr-frames.</param>
		/// <returns>Formatted stackframe</returns>
		public string FormatStackTrace(int luaSkipFrames, bool skipClrFrames)
		{
			var unknownFrame = false;
			var sb = new StringBuilder();
			foreach (LuaStackFrame c in this)
			{
				// Skip the frames
				if (luaSkipFrames > 0)
				{
					if (c.Type == LuaStackFrameType.Lua)
						luaSkipFrames--;
				}
				// Skip unknwon frames
				else if (unknownFrame)
				{
					if (c.Type == LuaStackFrameType.Unknown)
						continue;
					else if (!skipClrFrames)
						sb.AppendLine(Properties.Resources.rsStackTraceInternal);
				}
				else
				{
					if (c.Type == LuaStackFrameType.Unknown)
					{
						unknownFrame = true;
						continue;
					}
				}
				if (luaSkipFrames <= 0 && (!skipClrFrames || c.Type == LuaStackFrameType.Lua))
					c.ToString(sb, !skipClrFrames).AppendLine();
			}
			return sb.ToString();
		} // func FormatStackTrace

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
			=> FormatStackTrace(0, false);

		ILuaDebugInfo ILuaExceptionData.this[int frame]
			=> this[frame];

		void IList<LuaStackFrame>.Insert(int index, LuaStackFrame item) { throw new NotImplementedException(); }
		void IList<LuaStackFrame>.RemoveAt(int index) { throw new NotImplementedException(); }
		void ICollection<LuaStackFrame>.Add(LuaStackFrame item) { throw new NotImplementedException(); }
		void ICollection<LuaStackFrame>.Clear() { throw new NotImplementedException(); }
		bool ICollection<LuaStackFrame>.Remove(LuaStackFrame item) { throw new NotImplementedException(); }

		// -- Static --------------------------------------------------------------

		/// <summary>Get the LuaStackFrame from a .net StackFrame, if someone exists. In the other case, the return LuaStackFrame is only proxy to the orginal frame.</summary>
		/// <param name="frame">.net stackframe</param>
		/// <returns>LuaStackFrame</returns>
		public static LuaStackFrame GetStackFrame(StackFrame frame)
		{
			ILuaDebugInfo info = null;

			// find the lua debug info
			var method = frame.GetMethod();
			var chunk = Lua.GetChunkFromMethodInfo(method);
			if (chunk != null)
				info = chunk.GetDebugInfo(method, frame.GetILOffset());

			return new LuaStackFrame(frame, info);
		} // func GetStackFrame
				
		/// <summary>Converts a whole StackTrace.</summary>
		/// <param name="trace">.net stacktrace</param>
		/// <returns>LuaStackFrames</returns>
		public static LuaStackFrame[] GetStackTrace(StackTrace trace)
		{
			var frames = new LuaStackFrame[trace.FrameCount];
			var length = frames.Length;
			for (var i = 0; i < length; i++)
				frames[i] = GetStackFrame(trace.GetFrame(i));
			return frames;
		} // func GetStackTrace

		/// <summary>Converts a whole StackTrace and it is possible to use a special resolver.</summary>
		/// <param name="trace">.net stacktrace</param>
		/// <returns>LuaStackFrames</returns>
		public static IEnumerable<LuaStackFrame> GetStackTrace(IEnumerable<StackFrame> trace, Func<StackFrame, LuaStackFrame> resolveFrame)
		{
			if (trace == null)
				throw new ArgumentNullException("resolveFrame");
			if (resolveFrame == null)
				throw new ArgumentNullException("resolveFrame");

			foreach (var frame in trace)
				yield return resolveFrame(frame) ?? GetStackFrame(frame);
		} // func GetStackTrace

		/// <summary>Retrieves the debug information for an exception.</summary>
		/// <param name="ex">Exception</param>
		/// <param name="resolveStackTrace"></param>
		/// <returns>Debug Information</returns>
		public static LuaExceptionData GetData(Exception ex, bool resolveStackTrace = true)
		{
			var data = ex.Data[LuaRuntimeException.ExceptionDataKey] as LuaExceptionData;
			if (data == null)
			{
				// retrieve the stacktrace
				data = new LuaExceptionData(
					resolveStackTrace ?
						GetStackTrace(new StackTrace(ex, true)) :
						new LuaStackFrame[0]
				);

				// set the data
				ex.Data[LuaRuntimeException.ExceptionDataKey] = data;
			}
			return data;
		} // func GetData
	} // class LuaExceptionData

	#endregion
}
