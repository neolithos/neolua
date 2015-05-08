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
	#region -- class LuaFileStream ------------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	internal class LuaFileStream : LuaFile
	{
		private FileStream src;

		#region -- Ctor/Dtor --------------------------------------------------------------

		protected LuaFileStream(FileStream src, StreamReader tr, StreamWriter tw)
			: base(tr, tw)
		{
			if (src == null)
				throw new ArgumentNullException("src");
			
			this.src = src;
		} // ctor

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				if (src != null) { src.Dispose(); src = null; }
			}
			base.Dispose(disposing);
		} // proc Dispose

		public override void flush()
		{
			base.flush();
			src.Flush();
		} // proc flush

		public override LuaResult seek(string whence, long offset = 0)
		{
			if (src == null || !src.CanSeek)
				return seek(whence, offset);

			lock (this)
			{
				try
				{
					SeekOrigin origin;

					flush();

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
			}
		} // func seek

		public override long Length { get { return src.Length; } }

		#endregion

		#region -- OpenFile ---------------------------------------------------------------

		/// <summary>Creates a new lua compatible file access.</summary>
		/// <param name="sFileName">Name of the file</param>
		/// <param name="sMode">mode</param>
		/// <param name="encoding"></param>
		public static LuaFile OpenFile(string sFileName, string sMode, Encoding encoding)
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
			var src = new FileStream(sFileName, mode, access, (access & FileAccess.Write) != 0 ? FileShare.None : FileShare.Read);
			return new LuaFileStream(src,
				(access & FileAccess.Read) == 0 ? null : new StreamReader(src, encoding),
				(access & FileAccess.Write) == 0 ? null : new StreamWriter(src, encoding)
			);
		} // proc OpenFile

		#endregion
	} // class LuaFileStream

	#endregion

	#region -- class LuaTempFile --------------------------------------------------------

	internal class LuaTempFile : LuaFileStream
	{
		private string sFileName;

		public LuaTempFile(string sFileName, FileStream src, StreamReader tr, StreamWriter tw )
			: base(src, tr,tw)
		{
			this.sFileName = sFileName;
		} // ctor

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);
			try { File.Delete(sFileName); }
			catch { }
		} // proc Dispose

		public static LuaFile Create(string sFileName, Encoding encoding)
		{
			var src = new FileStream(sFileName, FileMode.Create, FileAccess.ReadWrite);
			return new LuaTempFile(sFileName, src, new StreamReader(src, encoding), new StreamWriter(src, encoding));
		} // func Create
	} // class LuaTempFile

	#endregion

	#region -- class LuaFileProcess -----------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	internal sealed class LuaFileProcess : LuaFile
	{
		private Process process;

		internal LuaFileProcess(Process process, bool lStandardOutputRedirected, bool lStandardInputRedirected)
			: base(lStandardOutputRedirected ? process.StandardOutput : null, lStandardInputRedirected ? process.StandardInput : null)
		{
			this.process = process;
		} // ctor

		protected override void Dispose(bool disposing)
		{
			if (disposing)
				if (process != null) { process.Dispose(); process = null; }
			base.Dispose(disposing);
		} // proc Dispose

		public override LuaResult close()
		{
			base.close();
			return new LuaResult(process.ExitCode);
		} // func close
	} // class LuaFileProcess

	#endregion

	#region -- class LuaFilePackage -----------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
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
        return defaultInput.lines(null);
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
    {
      if (file is string)
      {
        if (defaultInput != null)
          defaultInput.close();
        defaultInput = LuaFileStream.OpenFile((string)file, "r", defaultEncoding);

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
        defaultOutput = LuaFileStream.OpenFile((string)file, "w", defaultEncoding);

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
      return LuaTempFile.Create(Path.GetTempFileName(), defaultEncoding);
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
			if (psi.RedirectStandardOutput)
				psi.StandardOutputEncoding = defaultEncoding;
      psi.RedirectStandardInput = mode.IndexOf('w') >= 0;
      psi.UseShellExecute = false;
      psi.CreateNoWindow = true;
      return new LuaFileProcess(Process.Start(psi), psi.RedirectStandardOutput, psi.RedirectStandardInput);
    } // func popen

		/// <summary>Defines the encoding for stdout</summary>
		public Encoding DefaultEncoding
		{
			get { return defaultEncoding; }
			set
			{
				if (value == null)
					defaultEncoding = Encoding.ASCII;
				else defaultEncoding = value;
			}
		} // prop DefaultEncoding
  } // class LuaFilePackage

  #endregion
}
