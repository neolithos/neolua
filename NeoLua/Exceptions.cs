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
  /// <summary></summary>
  public abstract class LuaException : Exception
  {
    internal LuaException(string sMessage, Exception innerException)
      : base(sMessage, innerException)
    {
    } // ctor

    public abstract string FileName { get; }
    public abstract int Line { get; }
    public abstract int Column { get; }
  } // class LuaException

  #endregion

  #region -- class LuaParseException --------------------------------------------------

  ///////////////////////////////////////////////////////////////////////////////
  /// <summary></summary>
  public class LuaParseException : LuaException
  {
    private string sFileName;
    private int iLine;
    private int iColumn;
    private long iIndex;

    internal LuaParseException(Position position, string sMessage, Exception innerException)
      : base(sMessage, innerException)
    {
      this.sFileName = position.FileName;
      this.iLine = position.Line;
      this.iColumn = position.Col;
      this.iIndex = position.Index;
    } // ctor

    public override string FileName { get { return sFileName; } }
    public override int Line { get { return iLine; } }
    public override int Column { get { return iColumn; } }
    public long Index { get { return iIndex; } }
  } // class LuaParseException

  #endregion

  #region -- class LuaRuntimeException ------------------------------------------------

  ///////////////////////////////////////////////////////////////////////////////
  /// <summary></summary>
  public class LuaRuntimeException : LuaException
  {
    private int iLevel = 0;
    private bool lSkipClrFrames = false;

    internal LuaRuntimeException(string sMessage, Exception innerException)
      : base( sMessage, innerException)
    {
    } // ctor

    internal LuaRuntimeException(string sMessage, int iLevel, bool lSkipClrFrames)
      : base(sMessage, null)
    {
      this.iLevel = iLevel;
      this.lSkipClrFrames = lSkipClrFrames;
    } // ctor

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
    Unknown,
    Clr,
    Lua
  } // enum LuaStackFrameType

  #endregion

  #region -- class LuaStackFrame ------------------------------------------------------

  ///////////////////////////////////////////////////////////////////////////////
  /// <summary></summary>
  [Serializable]
  public class LuaStackFrame
  {
    private StackFrame frame;
    private LuaDebugInfo info;

    internal LuaStackFrame(StackFrame frame, LuaDebugInfo info)
    {
      this.frame = frame;
      this.info = info;
    } // ctor

    public StringBuilder ToString(StringBuilder sb, bool lPrintType)
    {
      sb.Append(" at ");

      if (lPrintType)
        sb.Append('[').Append(Type.ToString()[0]).Append("] ");

      // at type if it is clr or unknown
      MethodBase m = Method;
      if (m != null && info == null && m.DeclaringType != null)
        sb.Append(m.DeclaringType.FullName).Append('.');

      // at type method
      if (m == null)
        sb.Append("<unknown method>");
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
        sb.Append(" line ");
        sb.Append(FileName);
        if (LineNumber > 0)
          sb.Append(':').Append(LineNumber);
        if (ColumnNumber > 0)
          sb.Append(':').Append(ColumnNumber);
      }

      return sb;
    } // func ToString

    public override string ToString()
    {
      StringBuilder sb = new StringBuilder();
      ToString(sb, true);
      return sb.ToString();
    } // func ToString

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

    public string MethodName { get { return info == null ? Method.Name : info.ChunkName; } }
    public MethodBase Method { get { return frame.GetMethod(); } }
    public int ILOffset { get { return frame.GetILOffset(); } }
    public int NativeOffset { get { return frame.GetNativeOffset(); } }
    public string FileName { get { return info == null ? frame.GetFileName() : info.FileName; } }
    public int ColumnNumber { get { return info == null ? frame.GetFileColumnNumber() : info.Column; } }
    public int LineNumber { get { return info == null ? frame.GetFileLineNumber() : info.Line; } }
  } // class LuaStackFrame

  #endregion

  #region -- class LuaExceptionData ---------------------------------------------------

  ///////////////////////////////////////////////////////////////////////////////
  /// <summary></summary>
  [Serializable]
  public sealed class LuaExceptionData : IList<LuaStackFrame>
  {
    private LuaStackFrame[] stackTrace;

    internal LuaExceptionData(LuaStackFrame[] stackTrace)
    {
      this.stackTrace = stackTrace;
    } // ctor

    public int IndexOf(LuaStackFrame item) { return Array.IndexOf(stackTrace, item); }
    public bool Contains(LuaStackFrame item) { return IndexOf(item) != -1; }
    public void CopyTo(LuaStackFrame[] array, int arrayIndex) { Array.Copy(stackTrace, 0, array, arrayIndex, Count); }

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

    public int Count { get { return stackTrace.Length; } }
    public bool IsReadOnly { get { return true; } }

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
            sb.AppendLine(" -- internal --");
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

    public LuaStackFrame this[int index]
    {
      get { return stackTrace[index]; }
      set { throw new NotImplementedException(); }
    } // this

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

    public static LuaStackFrame GetStackFrame(StackFrame frame)
    {
      LuaDebugInfo info = null;

      // find the lua debug info
      LuaChunk chunk = Lua.GetChunk(frame.GetMethod().Name);
      if (chunk != null)
        info = chunk.GetDebugInfo(frame.GetILOffset());

      return new LuaStackFrame(frame, info);
    } // func GetStackFrame

    public static LuaStackFrame[] GetStackTrace(StackTrace trace)
    {
      LuaStackFrame[] frames = new LuaStackFrame[trace.FrameCount];
      int iLength = frames.Length;
      for (int i = 0; i < iLength; i++)
        frames[i] = GetStackFrame(trace.GetFrame(i));
      return frames;
    } // func GetStackTrace

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
