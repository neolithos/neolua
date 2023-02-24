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
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace Neo.IronLua
{
	#region -- enum LuaStackFrameType -------------------------------------------------

	/// <summary>Stackframe notation.</summary>
	public enum LuaStackFrameType
	{
		/// <summary>Unknown location of the frame.</summary>
		Unknown,
		/// <summary>Frame is within the .net source.</summary>
		Clr,
		/// <summary>Frame is within a lua script.</summary>
		Lua
	} // enum LuaStackFrameType

	#endregion

	#region -- interface ILuaExceptionData --------------------------------------------

	/// <summary>Exception data interface to provide more information about the stack.</summary>
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

		/// <summary>Exception to use for not implementated functions, that might be implemented later.</summary>
		/// <param name="caller"></param>
		/// <returns></returns>
		public static NotImplementedException GetNotImplementedException([CallerMemberName] string caller = "function")
			=> new NotImplementedException(String.Format("'{0}' is not implemented. Please open a kind request or better a pull request on https://github.com/neolithos/neolua", caller));

		/// <summary>Exception to use for functions, that can not implemented by design.</summary>
		/// <param name="caller"></param>
		/// <returns></returns>
		public static NotSupportedException GetNotSupportedException([CallerMemberName] string caller = "function")
			=> new NotSupportedException(String.Format("'{0}' is not supported.", caller));
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
		public LuaParseException(Position position, string message, Exception innerException = null)
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

	#region -- class LuaStackFrame ----------------------------------------------------

	/// <summary>Stack frame implementation.</summary>
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
			var m = Method;
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
						foreach (var g in m.GetGenericArguments())
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
				foreach (var pi in m.GetParameters())
				{
#if NET451
					if (typeof(System.Runtime.CompilerServices.Closure).IsAssignableFrom(pi.ParameterType))
						continue;
#endif

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
			=> ToString(new StringBuilder(), true).ToString();

		private string GetChunkName()
		{
			var chunkName = info.ChunkName;
			var methodName = Method.Name;

			return methodName.StartsWith(chunkName)
				? chunkName
				: chunkName + "#" + methodName;
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
		public string MethodName => info == null ? Method.Name : GetChunkName();
		/// <summary></summary>
		public MethodBase Method => frame.GetMethod();
		/// <summary></summary>
		public int ILOffset => frame.GetILOffset();
		/// <summary></summary>
		public int NativeOffset => frame.GetNativeOffset();
		/// <summary></summary>
		public string FileName => info?.FileName ?? frame.GetFileName();
		/// <summary></summary>
		public int ColumnNumber => info?.Column ?? frame.GetFileColumnNumber();
		/// <summary></summary>
		public int LineNumber => info == null ? frame.GetFileLineNumber() : info.Line;

		string ILuaDebugInfo.ChunkName => MethodName;
		int ILuaDebugInfo.Line => LineNumber;
		int ILuaDebugInfo.Column => ColumnNumber;
	} // class LuaStackFrame

	#endregion

	#region -- class LuaExceptionData -------------------------------------------------

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
		/// <param name="newStackTrace"></param>
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
			for (var i = 0; i < length; i++)
				yield return stackTrace[i];
		} // func GetEnumerator

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
			=> stackTrace.GetEnumerator();

		/// <summary>Stackframes</summary>
		public int Count { get { return stackTrace.Length; } }
		/// <summary>Always <c>true</c></summary>
		public bool IsReadOnly { get { return true; } }

		/// <summary>Get StackTrace format as an string.</summary>
		/// <param name="luaSkipFrames">Lua frame to skip.</param>
		/// <param name="skipClrFrames">Skip all clr-frames.</param>
		/// <returns>Formatted stackframe</returns>
		public string FormatStackTrace(int luaSkipFrames, bool skipClrFrames)
		{
			var unknownFrame = false;
			var sb = new StringBuilder();
			foreach (var c in this)
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
			get => stackTrace[index];
			set => throw new NotSupportedException();
		} // this

		/// <summary>Formatted StackTrace</summary>
		public string StackTrace
			=> FormatStackTrace(0, false);

		ILuaDebugInfo ILuaExceptionData.this[int frame]
			=> this[frame];

		void IList<LuaStackFrame>.Insert(int index, LuaStackFrame item) 
			=> throw new NotSupportedException();
		void IList<LuaStackFrame>.RemoveAt(int index) 
			=> throw new NotSupportedException();
		void ICollection<LuaStackFrame>.Add(LuaStackFrame item) 
			=> throw new NotSupportedException();
		void ICollection<LuaStackFrame>.Clear() 
			=> throw new NotSupportedException();
		bool ICollection<LuaStackFrame>.Remove(LuaStackFrame item) 
			=> throw new NotSupportedException();

		// -- Static ----------------------------------------------------------

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
		/// <param name="resolveFrames"></param>
		/// <returns>LuaStackFrames</returns>
		public static IEnumerable<LuaStackFrame> GetStackTrace(IEnumerable<StackFrame> trace, Func<StackFrame, LuaStackFrame> resolveFrames)
		{
			if (trace == null)
				throw new ArgumentNullException("resolveFrame");
			if (resolveFrames == null)
				throw new ArgumentNullException("resolveFrame");

			foreach (var frame in trace)
				yield return resolveFrames(frame) ?? GetStackFrame(frame);
		} // func GetStackTrace

		/// <summary>Retrieves the debug information for an exception.</summary>
		/// <param name="ex">Exception</param>
		/// <param name="resolveStackTrace"></param>
		/// <returns>Debug Information</returns>
		public static LuaExceptionData GetData(Exception ex, bool resolveStackTrace = true)
		{
			if (ex.Data[LuaRuntimeException.ExceptionDataKey] is LuaExceptionData data)
				return data;
			
			// retrieve the stacktrace
			data = new LuaExceptionData(
				resolveStackTrace ?
					GetStackTrace(new StackTrace(ex, true)) :
					new LuaStackFrame[0]
			);

			// set the data
			ex.Data[LuaRuntimeException.ExceptionDataKey] = data;
			
			return data;
		} // func GetData

		/// <summary>Unwind exception implementation.</summary>
		/// <param name="ex"></param>
		/// <param name="createDebugInfo"></param>
		public static void UnwindException(Exception ex, Func<ILuaDebugInfo> createDebugInfo)
		{
			var luaFrames = new List<LuaStackFrame>();
			var offsetForRecalc = 0;
			LuaExceptionData currentData = null;

			// get default exception data
			if (ex.Data[LuaRuntimeException.ExceptionDataKey] is LuaExceptionData)
			{
				currentData = GetData(ex);
				offsetForRecalc = currentData.Count;
				luaFrames.AddRange(currentData);
			}
			else
				currentData = GetData(ex, resolveStackTrace: false);

			// re-trace the stack frame
			var trace = new StackTrace(ex, true);
			for (var i = offsetForRecalc; i < trace.FrameCount - 1; i++)
				luaFrames.Add(GetStackFrame(trace.GetFrame(i)));

			// add trace point
			luaFrames.Add(new LuaStackFrame(trace.GetFrame(trace.FrameCount - 1), createDebugInfo()));
			
			currentData.UpdateStackTrace(luaFrames.ToArray());
		} // func UnwindException
	} // class LuaExceptionData

	#endregion
}
