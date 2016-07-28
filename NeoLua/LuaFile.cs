using System;
using System.IO;
using System.Text;

namespace Neo.IronLua
{
	#region -- class LuaLinesEnumerator -------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
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
			if (file.IsClosed || file.TextReader.EndOfStream)
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
		{
			throw new NotImplementedException();
		} // proc Reset

		public object Current => returns == null ? null : returns[returnIndex];
	} // class LuaLinesEnumerator

	#endregion

	#region -- class LuaFile ------------------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Lua compatible file access.</summary>
	public class LuaFile : IDisposable
	{
		private readonly object syncLock = new object();
		private readonly StreamReader tr;
		private readonly StreamWriter tw;
		private bool isClosed = false;

		#region -- Ctor/Dtor --------------------------------------------------------------

		/// <summary></summary>
		/// <param name="tr"></param>
		/// <param name="tw"></param>
		protected LuaFile(StreamReader tr, StreamWriter tw)
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
			flush();
			lock (this)
			{
				isClosed = true;
				tr?.Dispose();
				tw?.Dispose();
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
				tr?.DiscardBufferedData();
			}
		} // proc flush

		#endregion

		#region -- Read, Lines ------------------------------------------------------------

		private string ReadNumber()
		{
			var state = 0;
			StringBuilder sb = new StringBuilder();
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
				if (tr.EndOfStream)
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
			else if (fmt is string)
			{
				var fmtString = (string)fmt;
				if (fmtString.Length > 0 && fmtString[0] == '*')
					fmtString = fmtString.Substring(1);

				if (fmtString == "n")
				{
					return Lua.RtParseNumber(ReadNumber(), true, false);
				}
				else if (fmtString == "a")
				{
					if (tr.EndOfStream)
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

						for (int i = 0; i < args.Length; i++)
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

		#region -- Write, Seek ------------------------------------------------------------

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

						tr?.DiscardBufferedData();
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

		/// <summary>Is the file closed.</summary>
		public bool IsClosed => isClosed;

		/// <summary>Access to the internal TextReader.</summary>
		public StreamReader TextReader => tr;
		/// <summary>Access to the internal TextWriter.</summary>
		public StreamWriter TextWriter => tw;
		/// <summary>Length of the file.</summary>
		public virtual long Length => -1;
	} // class LuaFile

	#endregion
}
