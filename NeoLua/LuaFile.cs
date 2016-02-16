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

	#region -- class LuaFile ------------------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Lua compatible file access.</summary>
	public class LuaFile : IDisposable
	{
		private StreamReader tr;
		private StreamWriter tw;

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
				if (tr != null) { tr.Dispose(); tr = null; }
				if (tw != null) { tw.Dispose(); tw = null; }
			}
		} // proc Dispose

		/// <summary></summary>
		/// <returns></returns>
		public virtual LuaResult close()
		{
			Dispose();
			return LuaResult.Empty;
		} // func close

		/// <summary></summary>
		public virtual void flush()
		{
			lock (this)
			{
				if (tw != null)
					tw.Flush();
				if (tr != null)
					tr.DiscardBufferedData();
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
			LuaEmitTypeCode tc = LuaEmit.GetTypeCode(v.GetType());

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
						for (var i = 0; i < args.Length; i++)
						{
							var v = args[i];
							if (v != null)
								tw.Write((string)Lua.RtConvertValue(v, typeof(string)));
						}

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
		public virtual LuaResult seek(string whence, long offset = 0)
		{
			return new LuaResult(null, Properties.Resources.rsFileNotSeekable);
		} // func seek

		#endregion

		/// <summary></summary>
		/// <param name="mode"></param>
		/// <param name="size"></param>
		public void setvbuf(string mode, int size = 0)
		{
		} // proc setvbuf

		/// <summary>Is the file closed.</summary>
		public bool IsClosed { get { return tw == null && tr == null; } }

		/// <summary>Access to the internal TextReader.</summary>
		public StreamReader TextReader { get { return tr; } }
		/// <summary>Access to the internal TextWriter.</summary>
		public StreamWriter TextWriter { get { return tw; } }
		/// <summary>Length of the file.</summary>
		public virtual long Length { get { return -1; } }
	} // class LuaFile

	#endregion
}
