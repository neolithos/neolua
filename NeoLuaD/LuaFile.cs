using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Neo.IronLua
{
	#region -- class LuaFileStream ------------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	internal class LuaFileStream : LuaFile
	{
		private readonly FileStream src;

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
			try
			{
				base.Dispose(disposing);
			}
			finally
			{
				if (disposing)
					src?.Dispose();
			}
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

		public override long Length => src.Length;

		#endregion

		#region -- OpenFile ---------------------------------------------------------------

		/// <summary>Creates a new lua compatible file access.</summary>
		/// <param name="fileName">Name of the file</param>
		/// <param name="mode">mode</param>
		/// <param name="encoding"></param>
		public static LuaFile OpenFile(string fileName, string mode, Encoding encoding)
		{
			if (String.IsNullOrEmpty(mode))
				throw new ArgumentNullException("mode");

			var fileMode = FileMode.Open;
			var fileAccess = (FileAccess)0;

			// interpret mode
			var i = 0;
			while (i < mode.Length)
			{
				var c = mode[i];
				var isExtend = i < mode.Length - 1 && mode[i + 1] == '+';
				switch (c)
				{
					case 'r':
						fileAccess |= FileAccess.Read;
						if (isExtend)
						{
							fileAccess |= FileAccess.Write;
							fileMode = FileMode.Open;
						}
						break;
					case 'w':
						fileAccess |= FileAccess.Write;
						if (isExtend)
							fileMode = FileMode.Create;
						else
							fileMode = FileMode.OpenOrCreate;
						break;
					case 'a':
						fileAccess |= FileAccess.Write;
						fileMode = FileMode.Append;
						break;
					case 'b':
						break;
					default:
						throw new ArgumentException("mode", "Invalid mode format.");
				}
				i++;
				if (isExtend)
					i++;
			}

			// open the file
			var src = new FileStream(fileName, fileMode, fileAccess, (fileAccess & FileAccess.Write) != 0 ? FileShare.None : FileShare.Read);
			return new LuaFileStream(src,
				(fileAccess & FileAccess.Read) == 0 ? null : new StreamReader(src, encoding),
				(fileAccess & FileAccess.Write) == 0 ? null : new StreamWriter(src, encoding)
			);
		} // proc OpenFile

		#endregion
	} // class LuaFileStream

	#endregion

	#region -- class LuaTempFile --------------------------------------------------------

	internal class LuaTempFile : LuaFileStream
	{
		private readonly string fileName;

		public LuaTempFile(string fileName, FileStream src, StreamReader tr, StreamWriter tw)
			: base(src, tr, tw)
		{
			this.fileName = fileName;
		} // ctor

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);
			try { File.Delete(fileName); }
			catch { }
		} // proc Dispose

		public static LuaFile Create(string fileName, Encoding encoding)
		{
			var src = new FileStream(fileName, FileMode.Create, FileAccess.ReadWrite);
			return new LuaTempFile(fileName, src, new StreamReader(src, encoding), new StreamWriter(src, encoding));
		} // func Create
	} // class LuaTempFile

	#endregion

	#region -- class LuaFileProcess -----------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	internal sealed class LuaFileProcess : LuaFile
	{
		private readonly Process process;

		internal LuaFileProcess(Process process, bool doStandardOutputRedirected, bool doStandardInputRedirected)
			: base(doStandardOutputRedirected ? process.StandardOutput : null, doStandardInputRedirected ? process.StandardInput : null)
		{
			this.process = process;
		} // ctor

		protected override void Dispose(bool disposing)
		{
			try
			{
				base.Dispose(disposing);
			}
			finally
			{
				if (disposing)
					process?.Dispose();
			}
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
				defaultInput?.close();
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
				defaultOutput?.close();
				defaultOutput = LuaFileStream.OpenFile((string)file, "w", defaultEncoding);

				return defaultOutput;
			}
			else if (file is LuaFile)
			{
				defaultOutput?.close();
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
			=> defaultInput?.read(args) ?? LuaResult.Empty;

		/// <summary></summary>
		/// <param name="args"></param>
		/// <returns></returns>
		public LuaResult write(object[] args)
		 => defaultOutput?.write(args) ?? LuaResult.Empty;

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
			=> LuaTempFile.Create(Path.GetTempFileName(), defaultEncoding);

		/// <summary></summary>
		/// <param name="obj"></param>
		/// <returns></returns>
		public string type(object obj)
			=> obj is LuaFile && !((LuaFile)obj).IsClosed ? "file" : "file closed";

		/// <summary></summary>
		/// <param name="program"></param>
		/// <param name="mode"></param>
		/// <returns></returns>
		public LuaFile popen(string program, string mode = "r")
		{
			string fileName;
			string arguments;
			LuaLibraryOS.SplitCommand(program, out fileName, out arguments);

			var psi = new ProcessStartInfo(fileName, arguments);
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
