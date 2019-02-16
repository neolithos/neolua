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
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Neo.IronLua
{
	#region -- class LuaLinesEnumerator -----------------------------------------------

	internal class LuaLinesEnumerator : System.Collections.IEnumerator
	{
		private readonly LuaFile file;
		private readonly bool closeOnEnd;
		private readonly object[] args;

		private object[] returns;
		private int returnIndex;

		public LuaLinesEnumerator(LuaFile file, bool closeOnEnd, object[] args, int startIndex)
		{
			this.file = file;
			this.closeOnEnd = closeOnEnd;
			this.returns = null;
			this.returnIndex = 0;

			if (startIndex > 0)
			{
				this.args = new object[args.Length - startIndex];
				if (args.Length > 0)
					Array.Copy(args, startIndex, this.args, 0, this.args.Length);
			}
			else
				this.args = args;
		} // ctor

		public bool MoveNext()
		{
			if (file.IsClosed || file.IsEndOfStream)
			{
				if (closeOnEnd)
					file.close();
				return false;
			}
			else
			{
				returnIndex++;
				if (returns == null || returnIndex >= returns.Length) // read returns
				{
					returnIndex = 0;
					returns = file.read(args);
				}
				return true;
			}
		} // func MoveNext

		public void Reset()
			=> throw new NotSupportedException();
		
		public object Current => returns?[returnIndex];
	} // class LuaLinesEnumerator

	#endregion

	#region -- class LuaFile ----------------------------------------------------------

	/// <summary>Lua compatible file access.</summary>
	public class LuaFile : IDisposable
	{
		private readonly object syncLock = new object();
		private readonly TextReader tr;
		private readonly TextWriter tw;
		private bool isClosed = false;

		#region -- Ctor/Dtor ----------------------------------------------------------

		/// <summary></summary>
		/// <param name="tr"></param>
		/// <param name="tw"></param>
		public LuaFile(TextReader tr, TextWriter tw)
		{
			this.tr = tr;
			this.tw = tw;

			if (tr == null && tw == null)
				throw new ArgumentNullException();
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
			if (disposing)
			{
				flush();
				lock (syncLock)
				{
					isClosed = true;
					tr?.Dispose();
					tw?.Dispose();
				}
			}
			else
			{
				lock (syncLock)
					isClosed = true;
			}
		} // proc Dispose

		/// <summary></summary>
		/// <returns></returns>
		public virtual LuaResult close()
		{
			if (!isClosed)
				Dispose();
			return LuaResult.Empty;
		} // func close

		/// <summary></summary>
		public virtual void flush()
		{
			lock (syncLock)
			{
				tw?.Flush();
				DiscardBufferedData();
			}
		} // proc flush

		private void DiscardBufferedData()
		{
			if (tr is StreamReader sr)
				sr.DiscardBufferedData();
		} // proc DiscardBufferedData

		#endregion

		#region -- Read, Lines --------------------------------------------------------

		private string ReadNumber()
		{
			var state = 0;
			var sb = new StringBuilder();
			while (true)
			{
				var charIndex = tr.Read();
				var c = charIndex == -1 ? '\0' : (char)charIndex;
				switch (state)
				{
					case 0:
						if (c == '\0')
							return sb.ToString();
						else if (c == '+' || c == '-' || Char.IsNumber(c))
							state = 60;
						else
							return sb.ToString();
						break;
					#region -- 60 Number --------------------------------------------------------
					case 60:
						if (c == 'x' || c == 'X')
						{
							state = 70;
						}
						else
							goto case 61;
						break;
					case 61:
						if (c == '.')
							state = 62;
						else if (c == 'e' || c == 'E')
							state = 63;
						else if (c >= '0' && c <= '9')
							state = 61;
						else
							return sb.ToString();
						break;
					case 62:
						if (c == 'e' || c == 'E')
							state = 63;
						else if (c >= '0' && c <= '9')
							state = 62;
						else
							return sb.ToString();
						break;
					case 63:
						if (c == '-' || c == '+')
							state = 64;
						else if (c >= '0' && c <= '9')
							state = 64;
						else
							return sb.ToString();
						break;
					case 64:
						if (c >= '0' && c <= '9')
							state = 64;
						else
							return sb.ToString();
						break;
					#endregion
					#region -- 70 HexNumber -----------------------------------------------------
					case 70:
						if (c == '.')
							state = 71;
						else if (c == 'p' || c == 'P')
							state = 72;
						else if (c >= '0' && c <= '9' || c >= 'a' && c <= 'f' || c >= 'A' && c <= 'F')
							state = 70;
						else
							return sb.ToString();
						break;
					case 71:
						if (c == 'p' || c == 'P')
							state = 72;
						else if (c >= '0' && c <= '9' || c >= 'a' && c <= 'f' || c >= 'A' && c <= 'F')
							state = 71;
						else
							return sb.ToString();
						break;
					case 72:
						if (c == '-' || c == '+')
							state = 73;
						else if (c >= '0' && c <= '9')
							state = 73;
						else
							return sb.ToString();
						break;
					case 73:
						if (c >= '0' && c <= '9')
							state = 73;
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

			var tc = LuaEmit.GetTypeCode(v.GetType());
			return tc >= LuaEmitTypeCode.SByte && tc <= LuaEmitTypeCode.UInt32;
		} // func IsFileIndex

		private object ReadFormat(object fmt)
		{
			if (IsFileIndex(fmt))
			{
				if (IsEndOfStream)
					return null;
				else
				{
					var charCount = Convert.ToInt32(fmt);
					if (charCount == 0)
						return String.Empty;
					else
					{
						var b = new char[charCount];
						var readed = tr.Read(b, 0, charCount);
						return new string(b, 0, readed);
					}
				}
			}
			else if (fmt is string fmtString)
			{
				if (fmtString.Length > 0 && fmtString[0] == '*')
					fmtString = fmtString.Substring(1);

				if (fmtString == "n")
				{
					return Lua.RtParseNumber(ReadNumber(), true, false);
				}
				else if (fmtString == "a")
				{
					if (IsEndOfStream)
						return String.Empty;
					else
						return tr.ReadToEnd();
				}
				else if (fmtString == "l" || fmtString == "L")
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
			=> Lua.GetEnumIteratorResult(new LuaLinesEnumerator(this, false, args, 0));

		/// <summary></summary>
		/// <param name="args"></param>
		/// <returns></returns>
		public LuaResult read(object[] args)
		{
			if (tr == null)
				return new LuaResult(null, Properties.Resources.rsFileNotReadable);

			lock (syncLock)
			{
				try
				{
					tw?.Flush();

					if (args == null || args.Length == 0)
						return new LuaResult(tr.ReadLine());
					else
					{
						var r = new object[args.Length];

						for (var i = 0; i < args.Length; i++)
							r[i] = ReadFormat(args[i]);

						return new LuaResult(r);
					}
				}
				catch (Exception e)
				{
					return new LuaResult(null, e.Message);
				}
			}
		} // func read

		#endregion

		#region -- Write, Seek --------------------------------------------------------

		/// <summary></summary>
		/// <param name="args"></param>
		/// <returns></returns>
		public LuaResult write(object[] args)
		{
			if (tw == null)
				return new LuaResult(null, Properties.Resources.rsFileNotWriteable);

			lock (syncLock)
			{
				try
				{
					if (args != null || args.Length > 0)
					{
						for (var i = 0; i < args.Length; i++)
						{
							var v = args[i];
							if (v != null)
								tw.Write((string)Lua.RtConvertValue(v, typeof(string)));
						}

						DiscardBufferedData();
					}
					return new LuaResult(this);
				}
				catch (Exception e)
				{
					return new LuaResult(null, e.Message);
				}
			}
		} // func write

		/// <summary></summary>
		/// <param name="whence"></param>
		/// <param name="offset"></param>
		/// <returns></returns>
		public virtual LuaResult seek(string whence, long offset = 0)
			=> new LuaResult(null, Properties.Resources.rsFileNotSeekable);

		#endregion

		/// <summary></summary>
		/// <param name="mode"></param>
		/// <param name="size"></param>
		public void setvbuf(string mode, int size = 0)
		{
		} // proc setvbuf

		/// <summary>Is the end of stream reached.</summary>
		public bool IsEndOfStream
			=> isClosed || tr.Peek() == -1;

		/// <summary>Is the file closed.</summary>
		public bool IsClosed => isClosed;

		/// <summary>Access to the internal TextReader.</summary>
		public TextReader TextReader => tr;
		/// <summary>Access to the internal TextWriter.</summary>
		public TextWriter TextWriter => tw;
		/// <summary>Length of the file.</summary>
		public virtual long Length => -1;
	} // class LuaFile

	#endregion

	#region -- class LuaFileStream ----------------------------------------------------

	/// <summary></summary>
	public class LuaFileStream : LuaFile
	{
		private readonly FileStream src;

		#region -- Ctor/Dtor ----------------------------------------------------------

		/// <summary></summary>
		/// <param name="src"></param>
		/// <param name="tr"></param>
		/// <param name="tw"></param>
		protected LuaFileStream(FileStream src, StreamReader tr, StreamWriter tw)
			: base(tr, tw)
		{
			this.src = src ?? throw new ArgumentNullException(nameof(src));
		} // ctor

		/// <summary></summary>
		/// <param name="disposing"></param>
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

		/// <summary>Invoke filestream flush.</summary>
		public override void flush()
		{
			base.flush();
			src.Flush();
		} // proc flush

		/// <summary>Seek implementation.</summary>
		/// <param name="whence"></param>
		/// <param name="offset"></param>
		/// <returns></returns>
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

		/// <summary>Length of the file</summary>
		public override long Length => src.Length;

		#endregion

		#region -- OpenFile -----------------------------------------------------------

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
			return OpenFile(fileName, encoding, fileMode, fileAccess);
		} // proc OpenFile

		/// <summary>Creates a new lua compatible file access.</summary>
		/// <param name="fileName">Name of the file.</param>
		/// <param name="encoding">Encoding for the text access.</param>
		/// <param name="fileMode">Open mode</param>
		/// <param name="fileAccess">Access mode.</param>
		/// <returns></returns>
		public static LuaFile OpenFile(string fileName, Encoding encoding, FileMode fileMode, FileAccess fileAccess)
			=> OpenFile(new FileStream(fileName, fileMode, fileAccess, (fileAccess & FileAccess.Write) != 0 ? FileShare.None : FileShare.Read), encoding);

		/// <summary>Creates a new lua compatible file access.</summary>
		/// <param name="src">File stream.</param>
		/// <param name="encoding">Encoding for the text access.</param>
		/// <returns></returns>
		public static LuaFile OpenFile(FileStream src, Encoding encoding = null)
		{
			return new LuaFileStream(src,
				src.CanRead ? new StreamReader(src, encoding ?? Encoding.UTF8) : null,
				src.CanWrite ? new StreamWriter(src, encoding ?? Encoding.UTF8) : null
			);
		} // func OpenFile

		#endregion
	} // class LuaFileStream

	#endregion

	#region -- class LuaTempFile ------------------------------------------------------

	/// <summary>Create a temp file, will be deteted on close.</summary>
	public class LuaTempFile : LuaFileStream
	{
		private readonly string fileName;

		/// <summary></summary>
		/// <param name="fileName"></param>
		/// <param name="src"></param>
		/// <param name="tr"></param>
		/// <param name="tw"></param>
		protected LuaTempFile(string fileName, FileStream src, StreamReader tr, StreamWriter tw)
			: base(src, tr, tw)
		{
			this.fileName = fileName ?? throw new ArgumentNullException(nameof(fileName));
		} // ctor

		/// <summary></summary>
		/// <param name="disposing"></param>
		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);
			try { File.Delete(fileName); }
			catch { }
		} // proc Dispose

		/// <summary>Create the temp file.</summary>
		/// <param name="fileName"></param>
		/// <param name="encoding"></param>
		/// <returns></returns>
		public static LuaFile Create(string fileName, Encoding encoding)
		{
			var src = new FileStream(fileName, FileMode.Create, FileAccess.ReadWrite);
			return new LuaTempFile(fileName, src, new StreamReader(src, encoding), new StreamWriter(src, encoding));
		} // func Create
	} // class LuaTempFile

	#endregion

	#region -- class LuaFileProcess ---------------------------------------------------

	/// <summary></summary>
	internal sealed class LuaFileProcess : LuaFile
	{
		private readonly Process process;
		private int? exitCode = null;

		internal LuaFileProcess(Process process, bool doStandardOutputRedirected, bool doStandardInputRedirected)
			: base(doStandardOutputRedirected ? process.StandardOutput : null, doStandardInputRedirected ? process.StandardInput : null)
		{
			this.process = process ?? throw new ArgumentNullException(nameof(process));
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
				{
					if (!exitCode.HasValue && process.HasExited)
						exitCode = process.ExitCode;

					process.Dispose();
				}
			}
		} // proc Dispose

		public override LuaResult close()
		{
			base.close(); // call dispose only once
			return exitCode.HasValue ? new LuaResult(exitCode) : LuaResult.Empty;
		} // func close
	} // class LuaFileProcess

	#endregion
}
