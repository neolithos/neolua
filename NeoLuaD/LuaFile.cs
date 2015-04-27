using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Neo.IronLua
{
  #region -- class LuaLinesEnumerator -------------------------------------------------

  ///////////////////////////////////////////////////////////////////////////////
  /// <summary></summary>
  internal class LuaLinesEnumerator : System.Collections.IEnumerator
  {
    private LuaFile file;
    private bool lCloseOnEnd;
    private object[] args;
    private object[] returns;
    private int iReturnIndex;

    public LuaLinesEnumerator(LuaFile file, bool lCloseOnEnd, object[] args, int iStartIndex)
    {
      this.file = file;
      this.lCloseOnEnd = lCloseOnEnd;
      this.returns = null;
      this.iReturnIndex = 0;

      if (iStartIndex > 0)
      {
        this.args = new object[args.Length - iStartIndex];
        if (args.Length > 0)
          Array.Copy(args, iStartIndex, this.args, 0, this.args.Length);
      }
      else
        this.args = args;
    } // ctor

    public bool MoveNext()
    {
      if (file.IsClosed || file.TextReader.EndOfStream)
      {
        if (lCloseOnEnd)
          file.close();
        return false;
      }
      else
      {
        iReturnIndex++;
        if (returns == null || iReturnIndex >= returns.Length) // read returns
        {
          iReturnIndex = 0;
          returns = file.read(args);
        }
        return true;
      }
    } // func MoveNext

    public void Reset()
    {
      throw new NotImplementedException();
    }

    public object Current { get { return returns == null ? null : returns[iReturnIndex]; } }
  } // class LuaLinesEnumerator

  #endregion

  #region -- class LuaFilePackage -----------------------------------------------------

  ///////////////////////////////////////////////////////////////////////////////
  /// <summary>default files are not supported.</summary>
  public sealed class LuaFilePackage
  {
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
        return new LuaResult(new LuaFile(filename, mode));
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
        return defaultInput.lines(null);
      else
        return Lua.GetEnumIteratorResult(new LuaLinesEnumerator(new LuaFile((string)args[0], "r"), true, args, 1));
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
    {
      if (file is string)
      {
        if (defaultInput != null)
          defaultInput.close();
        defaultInput = new LuaFile((string)file, "r");

        return defaultInput;
      }
      else if (file is LuaFile)
      {
        if (defaultInput != null)
          defaultInput.close();
        defaultInput = (LuaFile)file;
      }
      return defaultInput;
    } // proc input

    /// <summary></summary>
    /// <param name="file"></param>
    /// <returns></returns>
    public LuaFile output(object file = null)
    {
      if (file is string)
      {
        if (defaultOutput != null)
          defaultOutput.close();
        defaultOutput = new LuaFile((string)file, "w");

        return defaultOutput;
      }
      else if (file is LuaFile)
      {
        if (defaultOutput != null)
          defaultOutput.close();
        defaultOutput = (LuaFile)file;
      }
      return defaultOutput;
    } // proc output

    /// <summary></summary>
    public void flush()
    {
      if (defaultOutput != null)
        defaultOutput.flush();
    } // proc flush

    /// <summary></summary>
    /// <param name="args"></param>
    /// <returns></returns>
    public LuaResult read(object[] args)
    {
      return defaultInput == null ? LuaResult.Empty : defaultInput.read(args);
    } // proc read

    /// <summary></summary>
    /// <param name="args"></param>
    /// <returns></returns>
    public LuaResult write(object[] args)
    {
      return defaultOutput == null ? LuaResult.Empty : defaultOutput.write(args);
    } // proc write

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
    {
      return new LuaTempFile(Path.GetTempFileName());
    } // func read

    /// <summary></summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    public string type(object obj)
    {
      if (obj is LuaFile && !((LuaFile)obj).IsClosed)
        return "file";
      else
        return "file closed";
    } // func type

    /// <summary></summary>
    /// <param name="program"></param>
    /// <param name="mode"></param>
    /// <returns></returns>
    public LuaFile popen(string program, string mode = "r")
    {
			string sFileName;
			string sArguments;
			LuaLibraryOS.SplitCommand(program, out sFileName, out sArguments);
      ProcessStartInfo psi = new ProcessStartInfo(sFileName, sArguments);
      psi.RedirectStandardOutput = mode.IndexOf('r') >= 0;
      psi.RedirectStandardInput = mode.IndexOf('w') >= 0;
      psi.UseShellExecute = false;
      psi.CreateNoWindow = true;
      return new LuaFile(Process.Start(psi), psi.RedirectStandardOutput, psi.RedirectStandardInput);
    } // func popen  
  } // class LuaFilePackage

  #endregion

  #region -- class LuaFile ------------------------------------------------------------

  ///////////////////////////////////////////////////////////////////////////////
  /// <summary>Lua compatible file access.</summary>
  public class LuaFile : IDisposable
  {
    private static Encoding defaultEncoding = Encoding.ASCII;

    private Process process;
    private FileStream src;
    private StreamReader tr;
    private StreamWriter tw;

    #region -- Ctor/Dtor --------------------------------------------------------------

    /// <summary>Creates a new lua compatible file access.</summary>
    /// <param name="sFileName">Name of the file</param>
    /// <param name="sMode">mode</param>
    public LuaFile(string sFileName, string sMode)
    {
      if (String.IsNullOrEmpty(sMode))
        throw new ArgumentNullException("mode");

      FileMode mode = FileMode.Open;
      FileAccess access = (FileAccess)0;

      // interpret mode
      int i = 0;
      while (i < sMode.Length)
      {
        char c = sMode[i];
        bool lExtend = i < sMode.Length - 1 && sMode[i + 1] == '+';
        switch (c)
        {
          case 'r':
            access |= FileAccess.Read;
            if (lExtend)
            {
              access |= FileAccess.Write;
              mode = FileMode.Open;
            }
            break;
          case 'w':
            access |= FileAccess.Write;
						if (lExtend)
							mode = FileMode.Create;
						else
							mode = FileMode.OpenOrCreate;
            break;
          case 'a':
            access |= FileAccess.Write;
            mode = FileMode.Append;
            break;
          case 'b':
            break;
          default:
            throw new ArgumentException("mode", "Invalid mode format.");
        }
        i++;
        if (lExtend)
          i++;
      }

			// open the file
      this.process = null;
      this.src = new FileStream(sFileName, mode, access, (access & FileAccess.Write) != 0 ? FileShare.None : FileShare.Read);
      this.tr = (access & FileAccess.Read) == 0 ? null : new StreamReader(src, defaultEncoding);
      this.tw = (access & FileAccess.Write) == 0 ? null : new StreamWriter(src, defaultEncoding);
    } // ctor

    internal LuaFile(Process process, bool lStandardOutputRedirected, bool lStandardInputRedirected)
    {
      this.process = process;
			this.tr = lStandardOutputRedirected ? process.StandardOutput : null;
			this.tw = lStandardInputRedirected ? process.StandardInput : null;
    } // ctor

    /// <summary></summary>
    ~LuaFile()
    {
      Dispose(false);
    } // ctor

    /// <summary>Closes the file stream</summary>
    public void Dispose()
    {
      GC.SuppressFinalize(this);
      Dispose(true);
    } // proc Dispose

    /// <summary></summary>
    /// <param name="disposing"></param>
    protected virtual void Dispose(bool disposing)
    {
      flush();
      lock (this)
      {
        if (process != null) { process.Dispose(); process = null; }
        if (tr != null) { tr.Dispose(); tr = null; }
        if (tw != null) { tw.Dispose(); tw = null; }
        if (src != null) { src.Dispose(); src = null; }
      }
    } // proc Dispose

    /// <summary></summary>
    /// <returns></returns>
    public LuaResult close()
    {
      LuaResult r = LuaResult.Empty;
      if (process != null)
        r = new LuaResult(process.ExitCode);
      Dispose();
      return r;
    } // func close

    /// <summary></summary>
    public void flush()
    {
      lock (this)
      {
        if (tw != null)
          tw.Flush();
        if (tr != null)
          tr.DiscardBufferedData();

        if (src != null)
          src.Flush();
      }
    } // proc flush

    #endregion

    #region -- Read, Lines ------------------------------------------------------------

    private string ReadNumber()
    {
      int iState = 0;
      StringBuilder sb = new StringBuilder();
      while (true)
      {
        int iChar = tr.Read();
        char c = iChar == -1 ? '\0' : (char)iChar;
        switch (iState)
        {
          case 0:
						if (c == '\0')
							return sb.ToString();
						else if (c == '+' || c == '-' || Char.IsNumber(c))
							iState = 60;
						else
              return sb.ToString();
            break;
          #region -- 60 Number --------------------------------------------------------
          case 60:
            if (c == 'x' || c == 'X')
            {
              iState = 70;
            }
            else
              goto case 61;
            break;
          case 61:
            if (c == '.')
              iState = 62;
            else if (c == 'e' || c == 'E')
              iState = 63;
            else if (c >= '0' && c <= '9')
              iState = 61;
            else
              return sb.ToString();
            break;
          case 62:
            if (c == 'e' || c == 'E')
              iState = 63;
            else if (c >= '0' && c <= '9')
              iState = 62;
            else
              return sb.ToString();
            break;
          case 63:
            if (c == '-' || c == '+')
              iState = 64;
            else if (c >= '0' && c <= '9')
              iState = 64;
            else
              return sb.ToString();
            break;
          case 64:
            if (c >= '0' && c <= '9')
              iState = 64;
            else
              return sb.ToString();
            break;
          #endregion
          #region -- 70 HexNumber -----------------------------------------------------
          case 70:
            if (c == '.')
              iState = 71;
            else if (c == 'p' || c == 'P')
              iState = 72;
            else if (c >= '0' && c <= '9' || c >= 'a' && c <= 'f' || c >= 'A' && c <= 'F')
              iState = 70;
            else
              return sb.ToString();
            break;
          case 71:
            if (c == 'p' || c == 'P')
              iState = 72;
            else if (c >= '0' && c <= '9' || c >= 'a' && c <= 'f' || c >= 'A' && c <= 'F')
              iState = 71;
            else
              return sb.ToString();
            break;
          case 72:
            if (c == '-' || c == '+')
              iState = 73;
            else if (c >= '0' && c <= '9')
              iState = 73;
            else
              return sb.ToString();
            break;
          case 73:
            if (c >= '0' && c <= '9')
              iState = 73;
            else
              return sb.ToString();
            break;
          #endregion
        }
        sb.Append(c);
      }
    } // proc ReadNumber

		private bool IsFileIndex(object v)
		{
			if (v == null)
				return false;
			TypeCode tc = Type.GetTypeCode(v.GetType());

			return tc >= TypeCode.SByte && tc <= TypeCode.UInt32;
		} // func IsFileIndex

    private object ReadFormat(object fmt)
    {
			if (IsFileIndex(fmt))
      {
        if (tr.EndOfStream)
          return null;
        else
        {
          int iCharCount = Convert.ToInt32(fmt);
          if (iCharCount == 0)
            return String.Empty;
          else
          {
            char[] b = new char[iCharCount];
            int iReaded = tr.Read(b, 0, iCharCount);
            return new string(b, 0, iReaded);
          }
        }
      }
      else if (fmt is string)
      {
        string sFmt = (string)fmt;
				if (sFmt.Length > 0 && sFmt[0] == '*')
					sFmt = sFmt.Substring(1);

        if (sFmt == "n")
        {
          return Lua.RtParseNumber(ReadNumber(), true, false);
        }
        else if (sFmt == "a")
        {
          if (tr.EndOfStream)
            return String.Empty;
          else
            return tr.ReadToEnd();
        }
        else if (sFmt == "l" || sFmt == "L")
          return tr.ReadLine();
        else
          return null;
      }
      else
        return null;
    } // func ReadFormat

    /// <summary></summary>
    /// <param name="args"></param>
    /// <returns></returns>
    public LuaResult lines(object[] args)
    {
      return Lua.GetEnumIteratorResult(new LuaLinesEnumerator(this, false, args, 0));
    } // func lines

    /// <summary></summary>
    /// <param name="args"></param>
    /// <returns></returns>
    public LuaResult read(object[] args)
    {
			if (tr == null)
				return new LuaResult(null, Properties.Resources.rsFileNotReadable);

      lock (this)
        try
        {
          if (tw != null)
            tw.Flush();

          if (args == null || args.Length == 0)
            return new LuaResult(tr.ReadLine());
          else
          {
            object[] r = new object[args.Length];

            for (int i = 0; i < args.Length; i++)
              r[i] = ReadFormat(args[i]);

            return new LuaResult(r);
          }
        }
        catch (Exception e)
        {
          return new LuaResult(null, e.Message);
        }
    } // func read

    #endregion

    #region -- Write, Seek ------------------------------------------------------------

    private void WriteValue(object v)
    {
      if (v == null)
        return;
      else if (v is string)
        tw.Write((string)v);
      else
      {
        TypeConverter conv = TypeDescriptor.GetConverter(v);
        WriteValue(conv.ConvertToInvariantString(v));
      }
    } // proc WriteValue

    /// <summary></summary>
    /// <param name="args"></param>
    /// <returns></returns>
    public LuaResult write(object[] args)
    {
			if (tw == null)
				return new LuaResult(null, Properties.Resources.rsFileNotWriteable);

      lock (this)
        try
        {
          if (args != null || args.Length > 0)
          {
            for (int i = 0; i < args.Length; i++)
              WriteValue(args[i]);

            if (tr != null)
              tr.DiscardBufferedData();
          }
          return new LuaResult(this);
        }
        catch (Exception e)
        {
          return new LuaResult(null, e.Message);
        }
    } // func write

    /// <summary></summary>
    /// <param name="whence"></param>
    /// <param name="offset"></param>
    /// <returns></returns>
    public LuaResult seek(string whence, long offset = 0)
    {
      if (src == null || !src.CanSeek)
        return new LuaResult(null, Properties.Resources.rsFileNotSeekable);

      lock (this)
        try
        {
          SeekOrigin origin;

          if (tw != null)
            tw.Flush();
          if (tr != null)
            tr.DiscardBufferedData();

          if (whence == "set")
            origin = SeekOrigin.Begin;
          else if (whence == "end")
            origin = SeekOrigin.End;
          else
            origin = SeekOrigin.Current;

          return new LuaResult(src.Seek(offset, origin));
        }
        catch (Exception e)
        {
          return new LuaResult(null, e.Message);
        }
    } // func seek

    #endregion

    /// <summary></summary>
    /// <param name="mode"></param>
    /// <param name="size"></param>
    public void setvbuf(string mode, int size = 0)
    {
    } // proc setvbuf

    /// <summary>Is the file closed.</summary>
    public bool IsClosed { get { return src == null && tw == null && tr == null; } }

    /// <summary>Access to the internal TextReader.</summary>
    public StreamReader TextReader { get { return tr; } }
    /// <summary>Access to the internal TextWriter.</summary>
    public StreamWriter TextWriter { get { return tw; } }
    /// <summary>Length of the file.</summary>
		public long Length { get { return src == null ? -1 : src.Length; } }
  } // class LuaFile

  #endregion

  #region -- class LuaTempFile --------------------------------------------------------

  internal class LuaTempFile : LuaFile
  {
    private string sFileName;

    public LuaTempFile(string sFileName)
      : base(sFileName, "rw+")
    {
      this.sFileName = sFileName;
    } // ctor

    protected override void Dispose(bool disposing)
    {
      base.Dispose(disposing);
      try { File.Delete(sFileName); }
      catch { }
    } // proc Dispose
  } // class LuaTempFile

  #endregion
}
