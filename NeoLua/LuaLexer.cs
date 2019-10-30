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
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace Neo.IronLua
{
	#region -- class TokenNameAttribute -----------------------------------------------

	/// <summary></summary>
	[AttributeUsage(AttributeTargets.Field)]
	internal sealed class TokenNameAttribute : Attribute
	{
		public TokenNameAttribute(string name)
		{
			this.Name = name ?? throw new ArgumentNullException(nameof(name));
		} // ctor

		public string Name { get; private set; }
	} // class TokenName

	#endregion

	#region -- enum LuaToken ----------------------------------------------------------

	/// <summary>Tokens</summary>
	public enum LuaToken
	{
		/// <summary>Not defined token</summary>
		None,
		/// <summary>End of file</summary>
		Eof,

		/// <summary>Invalid char</summary>
		InvalidChar,
		/// <summary>Invalid string</summary>
		InvalidString,
		/// <summary>Invalid string opening</summary>
		InvalidStringOpening,
		/// <summary>Invalid comment</summary>
		InvalidComment,

		/// <summary>NewLine</summary>
		[TokenName("\\n")]
		NewLine,
		/// <summary>Space</summary>
		Whitespace,
		/// <summary>Comment</summary>
		Comment,
		/// <summary>string</summary>
		[TokenName("string")]
		String,
		/// <summary>Integer or floating point number</summary>
		[TokenName("number")]
		Number,
		/// <summary>Identifier</summary>
		Identifier,

		/// <summary>Keyword and</summary>
		[TokenName("and")]
		KwAnd,
		/// <summary>Keyword break</summary>
		[TokenName("break")]
		KwBreak,
		/// <summary>Keyword cast</summary>
		[TokenName("cast")]
		KwCast,
		/// <summary>Keyword const</summary>
		[TokenName("const")]
		KwConst,
		/// <summary>Keyword do</summary>
		[TokenName("do")]
		KwDo,
		/// <summary>Keyword else</summary>
		[TokenName("else")]
		KwElse,
		/// <summary>Keyword elseif</summary>
		[TokenName("elseif")]
		KwElseif,
		/// <summary>Keyword end</summary>
		[TokenName("end")]
		KwEnd,
		/// <summary>Keyword false</summary>
		[TokenName("false")]
		KwFalse,
		/// <summary>Keyword for</summary>
		[TokenName("for")]
		KwFor,
		/// <summary>Keyword foreach</summary>
		[TokenName("foreach")]
		KwForEach,
		/// <summary>Keyword function</summary>
		[TokenName("function")]
		KwFunction,
		/// <summary>Keyword goto</summary>
		[TokenName("goto")]
		KwGoto,
		/// <summary>Keyword if</summary>
		[TokenName("if")]
		KwIf,
		/// <summary>Keyword in</summary>
		[TokenName("in")]
		KwIn,
		/// <summary>Keyword local</summary>
		[TokenName("local")]
		KwLocal,
		/// <summary>Keyword nil</summary>
		[TokenName("nil")]
		KwNil,
		/// <summary>Keyword not</summary>
		[TokenName("not")]
		KwNot,
		/// <summary>Keyword or</summary>
		[TokenName("or")]
		KwOr,
		/// <summary>Keyword repeat</summary>
		[TokenName("repeat")]
		KwRepeat,
		/// <summary>Keyword return</summary>
		[TokenName("return")]
		KwReturn,
		/// <summary>Keyword then</summary>
		[TokenName("then")]
		KwThen,
		/// <summary>Keyword true</summary>
		[TokenName("true")]
		KwTrue,
		/// <summary>Keyword until</summary>
		[TokenName("until")]
		KwUntil,
		/// <summary>Keyword while</summary>
		[TokenName("while")]
		KwWhile,

		/// <summary>+</summary>
		[TokenName("+")]
		Plus,
		/// <summary>-</summary>
		[TokenName("-")]
		Minus,
		/// <summary>*</summary>
		[TokenName("*")]
		Star,
		/// <summary>/</summary>
		[TokenName("/")]
		Slash,
		/// <summary>//</summary>
		[TokenName("//")]
		SlashShlash,
		/// <summary>%</summary>
		[TokenName("%")]
		Percent,
		/// <summary>^</summary>
		[TokenName("^")]
		Caret,
		/// <summary>&amp;</summary>
		[TokenName("&")]
		BitAnd,
		/// <summary>|</summary>
		[TokenName("|")]
		BitOr,
		/// <summary>~</summary>
		[TokenName("~")]
		Dilde,
		/// <summary>#</summary>
		[TokenName("#")]
		Cross,
		/// <summary>==</summary>
		[TokenName("==")]
		Equal,
		/// <summary>~=</summary>
		[TokenName("~=")]
		NotEqual,
		/// <summary>&lt;=</summary>
		[TokenName("<=")]
		LowerEqual,
		/// <summary>&gt;=</summary>
		[TokenName(">=")]
		GreaterEqual,
		/// <summary>&lt;</summary>
		[TokenName("<")]
		Lower,
		/// <summary>&gt;</summary>
		[TokenName(">")]
		Greater,
		/// <summary>&lt;&lt;</summary>
		[TokenName("<<")]
		ShiftLeft,
		/// <summary>&gt;&gt;</summary>
		[TokenName(">>")]
		ShiftRight,
		/// <summary>=</summary>
		[TokenName("=")]
		Assign,
		/// <summary>(</summary>
		[TokenName("(")]
		BracketOpen,
		/// <summary>)</summary>
		[TokenName(")")]
		BracketClose,
		/// <summary>{</summary>
		[TokenName("{")]
		BracketCurlyOpen,
		/// <summary>}</summary>
		[TokenName("}")]
		BracketCurlyClose,
		/// <summary>[</summary>
		[TokenName("[")]
		BracketSquareOpen,
		/// <summary>]</summary>
		[TokenName("]")]
		BracketSquareClose,
		/// <summary>;</summary>
		[TokenName(";")]
		Semicolon,
		/// <summary>:</summary>
		[TokenName(":")]
		Colon,
		/// <summary>::</summary>
		[TokenName("::")]
		ColonColon,
		/// <summary>,</summary>
		[TokenName(",")]
		Comma,
		/// <summary>.</summary>
		[TokenName(".")]
		Dot,
		/// <summary>..</summary>
		[TokenName("..")]
		DotDot,
		/// <summary>...</summary>
		[TokenName("...")]
		DotDotDot
	} // enum LuaToken

	#endregion

	#region -- struct Position --------------------------------------------------------

	/// <summary>Position in the source file</summary>
	public struct Position
	{
		private readonly SymbolDocumentInfo document;
		private readonly int line;
		private readonly int column;
		private readonly long index;

		internal Position(SymbolDocumentInfo document, int line, int column, long index)
		{
			this.document = document;
			this.line = line;
			this.column = column;
			this.index = index;
		} // ctor

		/// <summary>Umwandlung in ein übersichtliche Darstellung.</summary>
		/// <returns>Zeichenfolge mit Inhalt</returns>
		public override string ToString()
			=> String.Format("({0}; {1}; {2})", Line, Col, Index);

		/// <summary>Dateiname in der dieser Position sich befindet.</summary>
		public string FileName => document.FileName;
		/// <summary>Document where the token was found.</summary>
		internal SymbolDocumentInfo Document => document;
		/// <summary>Zeile, bei 1 beginnent.</summary>
		public int Line => line;
		/// <summary>Spalte, bei 1 beginnent.</summary>
		public int Col => column;
		/// <summary>Index bei 0 beginnend.</summary>
		public long Index => index;
	} // struct Position

	#endregion

	#region -- class Token ------------------------------------------------------------

	/// <summary>Represents a token of the lua source file.</summary>
	public class Token
	{
		// -- Position innerhalb der Datei --
		private readonly Position start;
		private readonly Position end;
		// -- Token-Wert --
		private readonly LuaToken kind;
		private readonly string value;

		/// <summary>Erzeugt einen Token.</summary>
		/// <param name="kind">Type des Wertes.</param>
		/// <param name="value">Der Wert.</param>
		/// <param name="start">Beginn des Tokens</param>
		/// <param name="end">Ende des Tokens</param>
		internal Token(LuaToken kind, string value, Position start, Position end)
		{
			this.kind = kind;
			this.start = start;
			this.end = end;
			this.value = value;
		} // ctor

		/// <summary>Umwandlung in ein übersichtliche Darstellung.</summary>
		/// <returns>Zeichenfolge mit Inhalt</returns>
		public override string ToString()
			=> String.Format("[{0,4},{1,4} - {2,4},{3,4}] {4}='{5}'", Start.Line, Start.Col, End.Line, End.Col, Typ, Value);

		/// <summary>Art des Wertes</summary>
		public LuaToken Typ => kind;
		/// <summary>Wert selbst</summary>
		public string Value => value;

		/// <summary>Start des Tokens</summary>
		public Position Start => start;
		/// <summary>Ende des Tokens</summary>
		public Position End => end;
		/// <summary>Länge des Tokens</summary>
		public int Length { get { unchecked { return (int)(end.Index - start.Index); } } }
	} // class Token

	#endregion

	#region -- interface ILuaLexer ----------------------------------------------------

	/// <summary>Lexer interface for parser.</summary>
	public interface ILuaLexer : IDisposable
	{
		/// <summary>Read next token.</summary>
		void Next();

		/// <summary>Look a head token, aka next token.</summary>
		Token LookAhead { get; }
		/// <summary>Look a head token, aka next, next token.</summary>
		Token LookAhead2 { get; }
		/// <summary></summary>
		Token Current { get; }
	} // interface ILuaLexer

	#endregion

	#region -- class LuaCharLexer -----------------------------------------------------

	/// <summary>Base class for the lua lexer.</summary>
	public sealed class LuaCharLexer : IDisposable
	{
		private readonly SymbolDocumentInfo document; // Information about the source document
		private TextReader tr;						// Source for the lexer, is set to zero on eof
		private readonly bool leaveOpen;            // do not dispose the text reader at the end

		private readonly char[] lookAheadBuffer;
		private readonly byte[] moveIndex;
		private int lookAheadOffset;
		private int lookAheadEof;

		private int currentLine;                    // Line in the source file
		private int currentColumn;                  // Column in the source file
		private readonly int firstColumnIndex;      // Index of the first char in line
		private long currentIndex;                  // Index in the source file
		
		private Position startPosition;             // Start of the current token
		private StringBuilder currentStringBuilder = new StringBuilder(); // Char buffer, for collected chars
		
		private bool isDisposed = false;

		#region -- Ctor/Dtor ----------------------------------------------------------

		/// <summary>Creates the lexer for the lua parser</summary>
		/// <param name="fileName">Filename</param>
		/// <param name="tr">Input for the scanner, will be disposed on the lexer dispose.</param>
		/// <param name="lookAheadLength">defines the look a head length for parsers</param>
		/// <param name="leaveOpen"></param>
		/// <param name="currentLine">Start line for the text reader.</param>
		/// <param name="currentColumn"></param>
		/// <param name="firstColumnIndex"></param>
		public LuaCharLexer(string fileName, TextReader tr, int lookAheadLength, bool leaveOpen = false, int currentLine = 1, int currentColumn = 1, int firstColumnIndex = 1)
		{
			if (lookAheadLength < 1)
				throw new ArgumentOutOfRangeException(nameof(lookAheadLength), lookAheadLength, "Must be greater zero.");

			this.document = Expression.SymbolDocument(fileName);
			this.tr = tr;
			this.leaveOpen = leaveOpen;

			this.lookAheadBuffer = new char[lookAheadLength];
			this.moveIndex = new byte[lookAheadLength];
			this.lookAheadOffset = lookAheadLength - 1;
			this.lookAheadEof = -1;

			this.currentLine = currentLine;
			this.currentColumn = currentColumn - 1;
			this.firstColumnIndex = firstColumnIndex;
			this.currentIndex = 0;

			// inital fill buffer
			for (var i = 0; i < lookAheadLength - 1; i++)
			{
				if (lookAheadEof >= 0)
				{
					lookAheadBuffer[i] = '\0';
					moveIndex[i] = 0;
				}
				else
				{
					var c = ReadCore(out var len);
					if (len == 0)
					{
						lookAheadEof = i;
						lookAheadBuffer[i] = '\0';
						moveIndex[i] = 0;
					}
					else
					{
						lookAheadBuffer[i] = (char)c;
						moveIndex[i] = len;
					}
				}
			}
			moveIndex[lookAheadOffset] = 0;

			// read first char from the buffer
			Next();
			startPosition = CurrentPosition;
		} // ctor

		/// <summary>Destroy the lexer and the TextReader</summary>
		public void Dispose()
		{
			if (isDisposed)
				throw new ObjectDisposedException(nameof(LuaCharLexer));
			isDisposed = true;

			if (!leaveOpen)
				tr?.Dispose();
		} // proc Dispose

		#endregion

		#region -- Buffer -------------------------------------------------------------

		/// <summary>Append a char to the current buffer.</summary>
		/// <param name="value"></param>
		public void AppendValue(char value)
			=> currentStringBuilder.Append(value);

		/// <summary>Append a char to the current buffer.</summary>
		/// <param name="value"></param>
		public void AppendValue(string value)
			=> currentStringBuilder.Append(Cur);

		private char ReadCore(out byte len)
		{
			if (tr == null) // Source file is readed
			{
				len = 0;
				return '\0';
			}
			else
			{
				var i = tr.Read();
				if (i == -1) // End of file reached
				{
					if (!leaveOpen)
						tr.Dispose();
					tr = null;

					len = 0;
					return '\0';
				}
				else if (i == 10)
				{
					if (tr.Peek() == 13)
					{
						tr.Read();
						len = 2;
						return (char)i;
					}
					else
					{
						len = 1;
						return (char)i;
					}
				}
				else if (i == 13)
				{
					if (tr.Peek() == 10)
					{
						tr.Read();
						len = 2;
						return '\n';
					}
					else
					{
						len = 1;
						return '\n';
					}
				}

				len = 1;
				return (char)i;
			}
		} // func ReadCore

		private bool ReadCharToBuffer()
		{
			if (lookAheadEof < 0)
			{
				var c = ReadCore(out var len);
				if (len == 0)
				{
					lookAheadEof = lookAheadOffset;
					lookAheadBuffer[lookAheadOffset] = '\0';
					moveIndex[lookAheadOffset] = 0;
				}
				else
				{
					lookAheadBuffer[lookAheadOffset] = (char)c;
					moveIndex[lookAheadOffset] = len;
				}

				return true;
			}
			else if (lookAheadEof != lookAheadOffset)
			{
				lookAheadBuffer[lookAheadOffset] = '\0';
				moveIndex[lookAheadOffset] = 0;
				return true;
			}
			else
				return false;
		} // func ReadCharToBuffer
		
		/// <summary>Move the char stream and discard the buffer.</summary>
		public void Next()
		{
			currentIndex += moveIndex[lookAheadOffset];
			if (lookAheadBuffer[lookAheadOffset] == '\n')
			{
				currentColumn = firstColumnIndex;
				currentLine++;
			}
			else
				currentColumn++;

			if (ReadCharToBuffer())
			{
				if (lookAheadBuffer.Length > 1)
				{
					lookAheadOffset++;
					if (lookAheadOffset >= lookAheadBuffer.Length)
						lookAheadOffset = 0;
				}
			}
		} // proc Next

		/// <summary>Move the char stream and discard the buffer.</summary>
		/// <param name="skip"></param>
		public void Next(int skip)
		{
			while (skip-- > 0)
				Next();
		} // proc Next

		/// <summary>Append the current char to the buffer and move the char stream.</summary>
		public void Eat()
		{
			AppendValue(Cur);
			Next();
		} // proc Eat

		/// <summary>Read a whole line</summary>
		/// <returns></returns>
		public string ReadLine()
		{
			while (!IsEof && Cur != '\n')
				Eat();
			if (Cur == '\n')
				Next();

			var curValue = CurValue;
			ResetCurValue();
			return curValue;
		} // func ReadLine

		/// <summary>Skip white spaces</summary>
		public void SkipWhiteSpaces()
		{
			while (!IsEof && Cur != '\n' && Char.IsWhiteSpace(Cur))
				Next();
		} // proc SkipWhiteSpaces

		/// <summary>Create a new token with the current buffer.</summary>
		/// <param name="kind">Token type</param>
		/// <returns>Token</returns>
		public Token CreateToken(LuaToken kind)
		{
			var endPosition = CurrentPosition;
			var tok = LuaLexer.CreateToken(kind, CurValue, startPosition, endPosition);
			startPosition = endPosition;
			currentStringBuilder.Clear();
			return tok;
		} // func CreateToken

		/// <summary>Create a new token.</summary>
		/// <param name="kind">Token type</param>
		/// <param name="value"></param>
		/// <returns>Token</returns>
		public Token CreateTokenAtStart(LuaToken kind, string value = null)
		{
			var pos = StartPosition;
			return LuaLexer.CreateToken(kind, value, pos, pos);
		} // func CreateTokenAtStart

		/// <summary>Create a new token.</summary>
		/// <param name="kind">Token type</param>
		/// <param name="value"></param>
		/// <returns>Token</returns>
		public Token CreateTokenAtEnd(LuaToken kind, string value = null)
		{
			var pos = CurrentPosition;
			return LuaLexer.CreateToken(kind, value, pos, pos);
		} // func CreateTokenAtEnd

		/// <summary></summary>
		/// <param name="c"></param>
		/// <param name="idx"></param>
		/// <returns></returns>
		public bool IsLookAHead(char c, int idx)
		{
			if (idx >= lookAheadBuffer.Length)
				throw new ArgumentOutOfRangeException(nameof(idx), lookAheadBuffer.Length, "Look a head buffer is to small.");

			var i = (lookAheadOffset + idx) % lookAheadBuffer.Length;
			return lookAheadBuffer[i] == c;
		} // func IsLookAHead

		/// <summary>Check the next following chars.</summary>
		/// <param name="value"></param>
		/// <param name="offset"></param>
		/// <returns></returns>
		public bool IsLookAHead(string value, int offset = 0)
		{
			if (String.IsNullOrEmpty(value))
				throw new ArgumentNullException(nameof(value));
			if (value.Length + offset > lookAheadBuffer.Length)
				throw new ArgumentOutOfRangeException(nameof(value), lookAheadBuffer.Length, "Look a head buffer is to small.");

			var c = lookAheadOffset;
			if (offset > 0)
				c = (c + offset) % lookAheadBuffer.Length;

			for (var i = 0; i < value.Length; i++)
			{
				if (lookAheadBuffer[c] != value[i])
					return false;
				c++;
				if (c >= lookAheadBuffer.Length)
					c = 0;
			}

			return true;
		} // func LookAHead

		/// <summary>Get look a head.</summary>
		/// <param name="idx"></param>
		/// <returns></returns>
		public char GetLookAHead(int idx = 0)
		{
			if (idx > lookAheadBuffer.Length)
				throw new ArgumentOutOfRangeException(nameof(idx), lookAheadBuffer.Length, "Look a head buffer is to small.");

			var i = (lookAheadOffset + idx) % lookAheadBuffer.Length;
			return lookAheadBuffer[i];
		}// func GetLookAHead
		
		/// <summary></summary>
		public void ResetCurValue()
		{
			startPosition = CurrentPosition;
			currentStringBuilder.Clear();
		} // proc ResetCurValue

		#endregion

		/// <summary>Current start position.</summary>
		public Position StartPosition => startPosition;
		/// <summary>Current char position.</summary>
		public Position CurrentPosition => new Position(document, currentLine, currentColumn, currentIndex);
		/// <summary>Current active char</summary>
		public char Cur => lookAheadBuffer[lookAheadOffset];
		/// <summary>End of file</summary>
		public bool IsEof => lookAheadEof == lookAheadOffset;
		/// <summary>Currently collected chars.</summary>
		public string CurValue => currentStringBuilder.ToString();
		/// <summary>Currently collected chars.</summary>
		public bool HasCurValue => currentStringBuilder.Length > 0;
	} // class LuaCharLexer

	#endregion

	#region -- class LuaLexer ---------------------------------------------------------

	/// <summary>Lexer for the lua syntax.</summary>
	public sealed class LuaLexer : ILuaLexer, IDisposable
	{
		private readonly IEnumerator<Token> tokenStream;

		private readonly Token[] tokens = new Token[3];

		#region -- Ctor/Dtor ----------------------------------------------------------

		/// <summary>Creates the lexer for the lua parser</summary>
		/// <param name="fileName">Filename</param>
		/// <param name="tr">Input for the scanner, will be disposed on the lexer dispose.</param>
		/// <param name="leaveOpen"></param>
		/// <param name="currentLine">Start line for the text reader.</param>
		/// <param name="currentColumn"></param>
		[Obsolete("Use create")]
		public LuaLexer(string fileName, TextReader tr, bool leaveOpen = false, int currentLine = 1, int currentColumn = 1)
			: this(CreateTokenStream(new LuaCharLexer(fileName, tr, 1, false, currentLine, currentColumn, 1)).GetEnumerator())
		{
		} // ctor

		/// <summary>Create a lexer from a token stream.</summary>
		/// <param name="tokenStream"></param>
		public LuaLexer(IEnumerator<Token> tokenStream)
		{
			this.tokenStream = tokenStream ?? throw new ArgumentNullException(nameof(tokenStream));
		} // ctor
		
		/// <summary></summary>
		public void Dispose()
			=> tokenStream.Dispose();

		#endregion

		#region -- Token Operationen --------------------------------------------------

		private Token NextTokenWithSkipRules()
		{
			var next = tokenStream.MoveNext()
				? tokenStream.Current
				: (tokens[0].Typ == LuaToken.Eof
					? tokens[0]
					: new Token(LuaToken.Eof, String.Empty, tokens[0].End, tokens[0].End)
				);

			if (SkipComments && next.Typ == LuaToken.Comment)
			{
				next = NextTokenWithSkipRules();
				if (next.Typ == LuaToken.NewLine)
					return NextTokenWithSkipRules();
				else
					return next;
			}
			else if (next.Typ == LuaToken.Whitespace || next.Typ == LuaToken.NewLine)
				return NextTokenWithSkipRules();
			else
				return next;
		} // func NextTokenWithSkipRules

		/// <summary>Reads the next token from the stream</summary>
		public void Next()
		{
			if (tokens[0] == null) // Erstinitialisierung der Lookaheads notwendig
			{
				for (var i = 0; i < tokens.Length; i++)
					tokens[i] = NextTokenWithSkipRules();
			}
			else
			{

				for (var i = 0; i < tokens.Length - 1; i++)
					tokens[i] = tokens[i + 1];
				tokens[tokens.Length - 1] = NextTokenWithSkipRules();
			}
		} // proc Next

		/// <summary>Next token</summary>
		public Token LookAhead => tokens[1];
		/// <summary>Next token</summary>
		public Token LookAhead2 => tokens[2];
		/// <summary>Current token</summary>
		public Token Current => tokens[0];
		/// <summary>Should the scanner skip comments</summary>
		public bool SkipComments { get; set; } = true;

		#endregion

		#region -- NextToken ----------------------------------------------------------

		/// <summary>Read a token from the char stream.</summary>
		/// <param name="chars"></param>
		/// <returns></returns>
		public static Token NextToken(LuaCharLexer chars)
		{
			var stringMode = '\0';
			var byteChar = (byte)0;
			var state = 0;

			void NextChar(int newState)
			{
				chars.Next();
				state = newState;
			} // NextChar

			void EatChar(int newState)
			{
				chars.Eat();
				state = newState;
			} // proc EatChar

			Token CreateToken(LuaToken token)
				=> chars.CreateToken(token);

			Token NextCharAndCreateToken(LuaToken token)
			{
				NextChar(0);
				return CreateToken(token);
			} // proc NextCharAndCreateToken

			Token EatCharAndCreateToken(LuaToken token)
			{
				EatChar(0);
				return CreateToken(token);
			} // proc NextCharAndCreateToken

			while (true)
			{
				var c = chars.Cur;

				switch (state)
				{
					#region -- 0 ------------------------------------------------------
					case 0:
						if (chars.IsEof)
							return CreateToken(LuaToken.Eof);
						else if (c == '\n')
							return NextCharAndCreateToken(LuaToken.NewLine);
						else if (Char.IsWhiteSpace(c))
							NextChar(10);

						else if (c == '+')
							return NextCharAndCreateToken(LuaToken.Plus);
						else if (c == '-')
							NextChar(50);
						else if (c == '*')
							return NextCharAndCreateToken(LuaToken.Star);
						else if (c == '/')
							NextChar(28);
						else if (c == '%')
							return NextCharAndCreateToken(LuaToken.Percent);
						else if (c == '^')
							return NextCharAndCreateToken(LuaToken.Caret);
						else if (c == '&')
							return NextCharAndCreateToken(LuaToken.BitAnd);
						else if (c == '|')
							return NextCharAndCreateToken(LuaToken.BitOr);
						else if (c == '#')
							return NextCharAndCreateToken(LuaToken.Cross);
						else if (c == '=')
							NextChar(20);
						else if (c == '~')
							NextChar(21);
						else if (c == '<')
							NextChar(22);
						else if (c == '>')
							NextChar(23);
						else if (c == '(')
							return NextCharAndCreateToken(LuaToken.BracketOpen);
						else if (c == ')')
							return NextCharAndCreateToken(LuaToken.BracketClose);
						else if (c == '{')
							return NextCharAndCreateToken(LuaToken.BracketCurlyOpen);
						else if (c == '}')
							return NextCharAndCreateToken(LuaToken.BracketCurlyClose);
						else if (c == '[')
							NextChar(27);
						else if (c == ']')
							return NextCharAndCreateToken(LuaToken.BracketSquareClose);
						else if (c == ';')
							return NextCharAndCreateToken(LuaToken.Semicolon);
						else if (c == ':')
							NextChar(30);
						else if (c == ',')
							return NextCharAndCreateToken(LuaToken.Comma);
						else if (c == '.')
							NextChar(24);

						else if (c == '"')
						{
							stringMode = c;
							NextChar(40);
						}
						else if (c == '\'')
						{
							stringMode = c;
							NextChar(40);
						}

						else if (c == '0')
							EatChar(60);
						else if (c >= '1' && c <= '9')
							EatChar(61);

						else if (c == 'a')
							EatChar(1010);
						else if (c == 'b')
							EatChar(1020);
						else if (c == 'c')
							EatChar(1150);
						else if (c == 'd')
							EatChar(1030);
						else if (c == 'e')
							EatChar(1040);
						else if (c == 'f')
							EatChar(1050);
						else if (c == 'g')
							EatChar(1065);
						else if (c == 'i')
							EatChar(1070);
						else if (c == 'l')
							EatChar(1080);
						else if (c == 'n')
							EatChar(1090);
						else if (c == 'o')
							EatChar(1100);
						else if (c == 'r')
							EatChar(1110);
						else if (c == 't')
							EatChar(1120);
						else if (c == 'u')
							EatChar(1130);
						else if (c == 'w')
							EatChar(1140);
						else if (Char.IsLetter(c) || c == '_')
							EatChar(1000);
						else
							return EatCharAndCreateToken(LuaToken.InvalidChar);
						break;
					#endregion
					#region -- 10 Whitespaces -----------------------------------------
					case 10:
						if (c == '\n' || chars.IsEof || !Char.IsWhiteSpace(c))
							return CreateToken(LuaToken.Whitespace);
						else
							NextChar(10);
						break;
					#endregion
					#region -- 20 -----------------------------------------------------
					case 20:
						if (c == '=')
							return NextCharAndCreateToken(LuaToken.Equal);
						else
							return CreateToken(LuaToken.Assign);
					case 21:
						if (c == '=')
							return NextCharAndCreateToken(LuaToken.NotEqual);
						else
							return CreateToken(LuaToken.Dilde);
					case 22:
						if (c == '=')
							return NextCharAndCreateToken(LuaToken.LowerEqual);
						else if (c == '<')
							return NextCharAndCreateToken(LuaToken.ShiftLeft);
						else
							return CreateToken(LuaToken.Lower);
					case 23:
						if (c == '=')
							return NextCharAndCreateToken(LuaToken.GreaterEqual);
						else if (c == '>')
							return NextCharAndCreateToken(LuaToken.ShiftRight);
						else
							return CreateToken(LuaToken.Greater);
					case 24:
						if (c == '.')
							NextChar(25);
						else if (c >= '0' && c <= '9')
						{
							chars.AppendValue('.');
							EatChar(62);
						}
						else
							return CreateToken(LuaToken.Dot);
						break;
					case 25:
						if (c == '.')
							NextChar(26);
						else
							return CreateToken(LuaToken.DotDot);
						break;
					case 26:
						if (c == '.')
							return NextCharAndCreateToken(LuaToken.DotDotDot);
						else
							return CreateToken(LuaToken.DotDotDot);
					case 27:
						if (c == '=' || c == '[')
						{
							state = 0;
							return ReadTextBlock(chars, true) ?? chars.CreateToken(LuaToken.InvalidStringOpening);
						}
						else
							return CreateToken(LuaToken.BracketSquareOpen);
					case 28:
						if (c == '/')
							return NextCharAndCreateToken(LuaToken.SlashShlash);
						else
							return CreateToken(LuaToken.Slash);
					#endregion
					#region -- 30 Label -----------------------------------------------
					case 30:
						if (c == ':')
							NextChar(31);
						else
							return CreateToken(LuaToken.Colon);
						break;
					case 31:
						if (c == ':')
							return NextCharAndCreateToken(LuaToken.ColonColon);
						else
							return CreateToken(LuaToken.ColonColon);
					#endregion
					#region -- 40 String ----------------------------------------------
					case 40:
						if (c == stringMode)
							return NextCharAndCreateToken(LuaToken.String);
						else if (c == '\\')
							NextChar(41);
						else if (chars.IsEof || c == '\n')
							return CreateToken(LuaToken.InvalidString);
						else
							EatChar(40);
						break;
					case 41:
						if (c == 'a') { chars.AppendValue('\a'); NextChar(40); }
						else if (c == 'b') { chars.AppendValue('\b'); NextChar(40); }
						else if (c == 'f') { chars.AppendValue('\f'); NextChar(40); }
						else if (c == 'n') { chars.AppendValue('\n'); NextChar(40); }
						else if (c == 'r') { chars.AppendValue('\r'); NextChar(40); }
						else if (c == 't') { chars.AppendValue('\t'); NextChar(40); }
						else if (c == 'v') { chars.AppendValue('\v'); NextChar(40); }
						else if (c == '\\') { chars.AppendValue('\\'); NextChar(40); }
						else if (c == '"') { chars.AppendValue('"'); NextChar(40); }
						else if (c == '\'') { chars.AppendValue('\''); NextChar(40); }
						else if (c == '\n') { chars.AppendValue("\n"); NextChar(40); }
						else if (c == 'x')
							NextChar(45);
						else if (c == 'z')
							NextChar(48);

						else if (c >= '0' && c <= '9')
						{
							byteChar = unchecked((byte)(c - '0'));
							NextChar(42);
						}
						else
							return NextCharAndCreateToken(LuaToken.InvalidString);
						break;
					case 42:
						if (c >= '0' && c <= '9')
						{
							byteChar = unchecked((byte)(byteChar * 10 + (c - '0')));
							NextChar(43);
						}
						else
						{
							chars.AppendValue((char)byteChar);
							goto case 40;
						}
						break;
					case 43:
						if (c >= '0' && c <= '9')
						{
							byteChar = unchecked((byte)(byteChar * 10 + (c - '0')));
							chars.AppendValue((char)byteChar);
							NextChar(40);
						}
						else
						{
							chars.AppendValue((char)byteChar);
							goto case 40;
						}
						break;
					case 45:
						if (c >= '0' && c <= '9')
						{
							byteChar = unchecked((byte)(c - '0'));
							NextChar(46);
						}
						else if (c >= 'a' && c <= 'f')
						{
							byteChar = unchecked((byte)(c - 'a' + 10));
							NextChar(46);
						}
						else if (c >= 'A' && c <= 'F')
						{
							byteChar = unchecked((byte)(c - 'A' + 10));
							NextChar(46);
						}
						else
							return NextCharAndCreateToken(LuaToken.InvalidString);
						break;
					case 46:
						if (c >= '0' && c <= '9')
						{
							byteChar = unchecked((byte)((byteChar << 4) + (c - '0')));
							chars.AppendValue((char)byteChar);
							NextChar(40);
						}
						else if (c >= 'a' && c <= 'f')
						{
							byteChar = unchecked((byte)((byteChar << 4) + (c - 'a' + 10)));
							chars.AppendValue((char)byteChar);
							NextChar(40);
						}
						else if (c >= 'A' && c <= 'F')
						{
							byteChar = unchecked((byte)((byteChar << 4) + (c - 'A' + 10)));
							chars.AppendValue((char)byteChar);
							NextChar(40);
						}
						else
							return NextCharAndCreateToken(LuaToken.InvalidString);
						break;
					case 48:
						if (Char.IsWhiteSpace(c))
							NextChar(48);
						else
							goto case 40;
						break;
					#endregion
					#region -- 50 Kommentar -------------------------------------------
					case 50:
						if (c == '-') // Kommentar
							NextChar(51);
						else
							return CreateToken(LuaToken.Minus);
						break;
					case 51:
						if (c == '[')
						{
							NextChar(0);
							var t = ReadTextBlock(chars, false);
							if (t != null)
								return t;
							goto case 52;
						}
						else if (c == '\n')
							return CreateToken(LuaToken.Comment);
						else
							NextChar(52);
						break;
					case 52:
						if (chars.IsEof)
							return CreateToken(LuaToken.Comment);
						else if (c == '\n')
							return NextCharAndCreateToken(LuaToken.Comment);
						else
							NextChar(52);
						break;
					#endregion
					#region -- 60 Number ----------------------------------------------
					case 60:
						if (c == 'x' || c == 'X')
							EatChar(70);
						else
							goto case 61;
						break;
					case 61:
						if (c == '.')
							EatChar(62);
						else if (c == 'e' || c == 'E')
							EatChar(63);
						else if (c >= '0' && c <= '9')
							EatChar(61);
						else
							return CreateToken(LuaToken.Number);
						break;
					case 62:
						if (c == 'e' || c == 'E')
							EatChar(63);
						else if (c >= '0' && c <= '9')
							EatChar(62);
						else
							return CreateToken(LuaToken.Number);
						break;
					case 63:
						if (c == '-' || c == '+')
							EatChar(64);
						else if (c >= '0' && c <= '9')
							EatChar(64);
						else
							return CreateToken(LuaToken.Number);
						break;
					case 64:
						if (c >= '0' && c <= '9')
							EatChar(64);
						else
							return CreateToken(LuaToken.Number);
						break;
					#endregion
					#region -- 70 HexNumber -------------------------------------------
					case 70:
						if (c == '.')
							EatChar(71);
						else if (c == 'p' || c == 'P')
							EatChar(72);
						else if (c >= '0' && c <= '9' || c >= 'a' && c <= 'f' || c >= 'A' && c <= 'F')
							EatChar(70);
						else
							return CreateToken(LuaToken.Number);
						break;
					case 71:
						if (c == 'p' || c == 'P')
							EatChar(72);
						else if (c >= '0' && c <= '9' || c >= 'a' && c <= 'f' || c >= 'A' && c <= 'F')
							EatChar(71);
						else
							return CreateToken(LuaToken.Number);
						break;
					case 72:
						if (c == '-' || c == '+')
							EatChar(73);
						else if (c >= '0' && c <= '9')
							EatChar(73);
						else
							return CreateToken(LuaToken.Number);
						break;
					case 73:
						if (c >= '0' && c <= '9')
							EatChar(73);
						else
							return CreateToken(LuaToken.Number);
						break;
					#endregion
					#region -- 1000 Ident or Keyword ----------------------------------
					case 1000:
						if (IsIdentifierChar(c))
							EatChar(1000);
						else
							return CreateToken(LuaToken.Identifier);
						break;
					// and
					case 1010: if (c == 'n') EatChar(1011); else goto case 1000; break;
					case 1011: if (c == 'd') EatChar(1012); else goto case 1000; break;
					case 1012: if (!IsIdentifierChar(c)) return CreateToken(LuaToken.KwAnd); else goto case 1000;
					// break
					case 1020: if (c == 'r') EatChar(1021); else goto case 1000; break;
					case 1021: if (c == 'e') EatChar(1022); else goto case 1000; break;
					case 1022: if (c == 'a') EatChar(1023); else goto case 1000; break;
					case 1023: if (c == 'k') EatChar(1024); else goto case 1000; break;
					case 1024: if (!IsIdentifierChar(c)) return CreateToken(LuaToken.KwBreak); else goto case 1000;
					// do
					case 1030: if (c == 'o') EatChar(1031); else goto case 1000; break;
					case 1031: if (!IsIdentifierChar(c)) return CreateToken(LuaToken.KwDo); else goto case 1000;
					// else, elseif end
					case 1040: if (c == 'n') EatChar(1041); else if (c == 'l') EatChar(1043); else goto case 1000; break;
					case 1041: if (c == 'd') EatChar(1042); else goto case 1000; break;
					case 1042: if (!IsIdentifierChar(c)) return CreateToken(LuaToken.KwEnd); else goto case 1000;
					case 1043: if (c == 's') EatChar(1044); else goto case 1000; break;
					case 1044: if (c == 'e') EatChar(1045); else goto case 1000; break;
					case 1045: if (c == 'i') EatChar(1046); else if (!IsIdentifierChar(c)) return CreateToken(LuaToken.KwElse); else goto case 1000; break;
					case 1046: if (c == 'f') EatChar(1047); else goto case 1000; break;
					case 1047: if (!IsIdentifierChar(c)) return CreateToken(LuaToken.KwElseif); else goto case 1000;
					// false, for, function
					case 1050: if (c == 'a') EatChar(1051); else if (c == 'o') EatChar(1055); else if (c == 'u') EatChar(1057); else goto case 1000; break;
					case 1051: if (c == 'l') EatChar(1052); else goto case 1000; break;
					case 1052: if (c == 's') EatChar(1053); else goto case 1000; break;
					case 1053: if (c == 'e') EatChar(1054); else goto case 1000; break;
					case 1054: if (!IsIdentifierChar(c)) return CreateToken(LuaToken.KwFalse); else goto case 1000;
					case 1055: if (c == 'r') EatChar(1056); else goto case 1000; break;
					case 1056: if (c == 'e') EatChar(10000); else if (!Char.IsLetterOrDigit(c)) return CreateToken(LuaToken.KwFor); else goto case 1000; break;
					case 1057: if (c == 'n') EatChar(1058); else goto case 1000; break;
					case 1058: if (c == 'c') EatChar(1059); else goto case 1000; break;
					case 1059: if (c == 't') EatChar(1060); else goto case 1000; break;
					case 1060: if (c == 'i') EatChar(1061); else goto case 1000; break;
					case 1061: if (c == 'o') EatChar(1062); else goto case 1000; break;
					case 1062: if (c == 'n') EatChar(1063); else goto case 1000; break;
					case 1063: if (!IsIdentifierChar(c)) return CreateToken(LuaToken.KwFunction); else goto case 1000;
					case 10000: if (c == 'a') EatChar(10001); else goto case 1000; break;
					case 10001: if (c == 'c') EatChar(10002); else goto case 1000; break;
					case 10002: if (c == 'h') EatChar(10003); else goto case 1000; break;
					case 10003: if (!IsIdentifierChar(c)) return CreateToken(LuaToken.KwForEach); else goto case 1000;
					// goto
					case 1065: if (c == 'o') EatChar(1066); else goto case 1000; break;
					case 1066: if (c == 't') EatChar(1067); else goto case 1000; break;
					case 1067: if (c == 'o') EatChar(1068); else goto case 1000; break;
					case 1068: if (!IsIdentifierChar(c)) return CreateToken(LuaToken.KwGoto); else goto case 1000;
					// if, in
					case 1070: if (c == 'f') EatChar(1071); else if (c == 'n') EatChar(1072); else goto case 1000; break;
					case 1071: if (!IsIdentifierChar(c)) return CreateToken(LuaToken.KwIf); else goto case 1000;
					case 1072: if (!IsIdentifierChar(c)) return CreateToken(LuaToken.KwIn); else goto case 1000;
					// local
					case 1080: if (c == 'o') EatChar(1081); else goto case 1000; break;
					case 1081: if (c == 'c') EatChar(1082); else goto case 1000; break;
					case 1082: if (c == 'a') EatChar(1083); else goto case 1000; break;
					case 1083: if (c == 'l') EatChar(1084); else goto case 1000; break;
					case 1084: if (!IsIdentifierChar(c)) return CreateToken(LuaToken.KwLocal); else goto case 1000;
					// nil, not
					case 1090: if (c == 'i') EatChar(1091); else if (c == 'o') EatChar(1093); else goto case 1000; break;
					case 1091: if (c == 'l') EatChar(1092); else goto case 1000; break;
					case 1092: if (!IsIdentifierChar(c)) return CreateToken(LuaToken.KwNil); else goto case 1000;
					case 1093: if (c == 't') EatChar(1094); else goto case 1000; break;
					case 1094: if (!IsIdentifierChar(c)) return CreateToken(LuaToken.KwNot); else goto case 1000;
					// or
					case 1100: if (c == 'r') EatChar(1101); else goto case 1000; break;
					case 1101: if (!IsIdentifierChar(c)) return CreateToken(LuaToken.KwOr); else goto case 1000;
					// repeat, return
					case 1110: if (c == 'e') EatChar(1111); else goto case 1000; break;
					case 1111: if (c == 'p') EatChar(1112); else if (c == 't') EatChar(1116); else goto case 1000; break;
					case 1112: if (c == 'e') EatChar(1113); else goto case 1000; break;
					case 1113: if (c == 'a') EatChar(1114); else goto case 1000; break;
					case 1114: if (c == 't') EatChar(1115); else goto case 1000; break;
					case 1115: if (!IsIdentifierChar(c)) return CreateToken(LuaToken.KwRepeat); else goto case 1000;
					case 1116: if (c == 'u') EatChar(1117); else goto case 1000; break;
					case 1117: if (c == 'r') EatChar(1118); else goto case 1000; break;
					case 1118: if (c == 'n') EatChar(1119); else goto case 1000; break;
					case 1119: if (!IsIdentifierChar(c)) return CreateToken(LuaToken.KwReturn); else goto case 1000;
					// then, true
					case 1120: if (c == 'h') EatChar(1121); else if (c == 'r') EatChar(1124); else goto case 1000; break;
					case 1121: if (c == 'e') EatChar(1122); else goto case 1000; break;
					case 1122: if (c == 'n') EatChar(1123); else goto case 1000; break;
					case 1123: if (!IsIdentifierChar(c)) return CreateToken(LuaToken.KwThen); else goto case 1000;
					case 1124: if (c == 'u') EatChar(1125); else goto case 1000; break;
					case 1125: if (c == 'e') EatChar(1126); else goto case 1000; break;
					case 1126: if (!IsIdentifierChar(c)) return CreateToken(LuaToken.KwTrue); else goto case 1000;
					// until
					case 1130: if (c == 'n') EatChar(1131); else goto case 1000; break;
					case 1131: if (c == 't') EatChar(1132); else goto case 1000; break;
					case 1132: if (c == 'i') EatChar(1133); else goto case 1000; break;
					case 1133: if (c == 'l') EatChar(1134); else goto case 1000; break;
					case 1134: if (!IsIdentifierChar(c)) return CreateToken(LuaToken.KwUntil); else goto case 1000;
					// while
					case 1140: if (c == 'h') EatChar(1141); else goto case 1000; break;
					case 1141: if (c == 'i') EatChar(1142); else goto case 1000; break;
					case 1142: if (c == 'l') EatChar(1143); else goto case 1000; break;
					case 1143: if (c == 'e') EatChar(1144); else goto case 1000; break;
					case 1144: if (!IsIdentifierChar(c)) return CreateToken(LuaToken.KwWhile); else goto case 1000;
					// cast
					case 1150: if (c == 'a') EatChar(1151); else if (c == 'o') EatChar(1160); else goto case 1000; break;
					case 1151: if (c == 's') EatChar(1152); else goto case 1000; break;
					case 1152: if (c == 't') EatChar(1153); else goto case 1000; break;
					case 1153: if (!IsIdentifierChar(c)) return CreateToken(LuaToken.KwCast); else goto case 1000;
					// const
					case 1160: if (c == 'n') EatChar(1161); else goto case 1000; break;
					case 1161: if (c == 's') EatChar(1162); else goto case 1000; break;
					case 1162: if (c == 't') EatChar(1163); else goto case 1000; break;
					case 1163: if (!IsIdentifierChar(c)) return CreateToken(LuaToken.KwConst); else goto case 1000;
						#endregion
				}
			}
		} // func NextToken

		private static Token ReadTextBlock(LuaCharLexer chars, bool stringMode)
		{
			var search = 0;
			var find = 0;

			// Zähle die =
			while (chars.Cur == '=')
			{
				chars.Next();
				search++;
			}
			if (chars.Cur != '[')
			{
				chars.Next();
				return null;
			}
			chars.Next();

			// Skip WhiteSpace until the first new line
			while (!chars.IsEof && Char.IsWhiteSpace(chars.Cur))
			{
				if (chars.Cur == '\n')
				{
					chars.Next();
					break;
				}
				else
					chars.Next();
			}

			// Suche das Ende
			ReadChars:
			while (chars.Cur != ']')
			{
				if (chars.IsEof)
					return chars.CreateToken(stringMode ? LuaToken.InvalidString : LuaToken.InvalidComment);
				else if (stringMode)
					chars.Eat();
				else
					chars.Next();
			}

			// Zähle die =
			find = 0;
			chars.Next();
			while (chars.Cur == '=')
			{
				chars.Next();
				find++;
			}
			if (chars.Cur == ']' && find == search)
			{
				chars.Next();
				return chars.CreateToken(stringMode ? LuaToken.String : LuaToken.Comment);
			}
			else
			{
				chars.AppendValue(']');
				for (var i = 0; i < find; i++)
					chars.AppendValue('=');
				goto ReadChars;
			}
		} // proc ReadTextBlock

		#endregion
		
		private static readonly Lazy<string[]> keywords;

		static LuaLexer()
		{
			keywords = new Lazy<string[]>(
				() =>
				(
					from fi in typeof(LuaToken).GetTypeInfo().DeclaredFields
					let a = fi.GetCustomAttribute<TokenNameAttribute>()
					where a != null && fi.IsStatic && fi.Name.StartsWith("Kw")
					orderby a.Name
					select a.Name
				).ToArray(), true
			);
		} // sctor

		#region -- Plain Lua file lexer -----------------------------------------------

		private static IEnumerable<Token> CreateTokenStream(LuaCharLexer chars)
		{
			try
			{
				while (true)
				{
					var tok = NextToken(chars);
					yield return tok;
					if (tok.Typ == LuaToken.Eof)
						break;
				}
			}
			finally
			{
				chars.Dispose();
			}
		} // func CreateTokenStream

		/// <summary>Creates the lexer for the lua parser</summary>
		/// <param name="fileName">Filename</param>
		/// <param name="tr">Input for the scanner, will be disposed on the lexer dispose.</param>
		/// <param name="leaveOpen"></param>
		/// <param name="currentLine">Start line for the text reader.</param>
		/// <param name="currentColumn"></param>
		/// <param name="firstColumnIndex"></param>
		/// <returns></returns>
		public static ILuaLexer Create(string fileName, TextReader tr, bool leaveOpen = false, int currentLine = 1, int currentColumn = 1, int firstColumnIndex = 1)
			=> Create(new LuaCharLexer(fileName, tr, 1, leaveOpen, currentLine, currentColumn, firstColumnIndex));

		/// <summary>Creates the lexer for the lua parser</summary>
		/// <param name="charLexer"></param>
		/// <returns></returns>
		public static ILuaLexer Create(LuaCharLexer charLexer)
			=> new LuaLexer(CreateTokenStream(charLexer).GetEnumerator());

		#endregion

		#region -- Html embedded lua --------------------------------------------------

		private static IEnumerable<Token> CreateHtmlTokenStream(LuaCharLexer chars, bool codeEmitted, IEnumerable<Token> scriptPreamble)
		{
			var isFirst = !codeEmitted;
			var state = 0;
			
			while (!chars.IsEof)
			{
				var c = chars.Cur;


				switch (state)
				{
					#region -- 0 - Basis --
					case 0: // Basis
						switch(c)
						{
							case '<':
								if (chars.IsLookAHead("%", 1)) // open code section
								{
									if (!isFirst)
									{
										if (!codeEmitted && scriptPreamble != null)
										{
											foreach (var preamble in scriptPreamble)
												yield return preamble;
										}
										yield return chars.CreateTokenAtStart(LuaToken.Identifier, "print");
										yield return chars.CreateToken(LuaToken.String);
										yield return chars.CreateTokenAtStart(LuaToken.Semicolon);
									}

									codeEmitted = true;
									chars.Next(2);
									if (chars.Cur == '=')
									{
										chars.Next();

										yield return chars.CreateTokenAtEnd(LuaToken.Identifier, "printValue");
										yield return chars.CreateTokenAtEnd(LuaToken.BracketOpen);

										state = 2;
									}
									else
										state = 1;
								}
								else if (chars.IsLookAHead("!--", 1)) // open comment
								{
									chars.Next(4);
									state = 10;
								}
								else
									goto default;
								break;
							default:
								if (!isFirst) // skip leading spaces
									chars.Eat();
								else if (!Char.IsWhiteSpace(c))
								{
									isFirst = false;
									chars.Eat();
								}
								else
									chars.Next();
								break;
						}
						break;
					#endregion
					#region -- 1 - Code --
					case 1: // check type of bracket
						if (c == '%' && chars.IsLookAHead("%>"))
						{
							chars.Next(2);
							state = 0;
						}
						else
							yield return NextToken(chars);
						break;
					#endregion
					#region -- 2 - Var --
					case 2:
						if (c == ':' && chars.IsLookAHead(':', 1)) // next is format
						{
							chars.Next(2);
							state = 3;
						}
						else if (c == '%' && chars.IsLookAHead('>', 1)) // end of var
							goto case 3;
						else
							yield return NextToken(chars);

						break;
					case 3:
						if (c == '%' && chars.IsLookAHead('>', 1))
						{
							if (chars.HasCurValue)
							{
								yield return chars.CreateTokenAtStart(LuaToken.Comma);
								yield return chars.CreateToken(LuaToken.String);
							}
							goto case 4;
						}
						else
							chars.Eat();
						break;
					case 4:
						{
							var startAt = chars.CurrentPosition;
							chars.Next(2);
							var endAt = chars.CurrentPosition;
							yield return CreateToken(LuaToken.BracketClose, String.Empty, startAt, endAt);
							yield return CreateToken(LuaToken.Semicolon, String.Empty, startAt, endAt);
							state = 0;
						}
						break;
					#endregion
					#region -- 10 - Comment --
					case 10:
						if (c == '-' && chars.IsLookAHead("->",1))
						{
							chars.Next(3);
							state = 0;
						}
						else
							chars.Next();
						break;
					#endregion
					default:
						throw new InvalidOperationException();
				}
			}

			if (codeEmitted) // something emitted
			{
				if (chars.HasCurValue && !String.IsNullOrWhiteSpace(chars.CurValue)) // we have something in the buffer
				{
					yield return chars.CreateTokenAtStart(LuaToken.Identifier, "print");
					yield return chars.CreateToken(LuaToken.String);
					yield return chars.CreateTokenAtStart(LuaToken.Semicolon);
				}
			}
			else // no code emitted --> create a single print statement
			{
				yield return chars.CreateTokenAtStart(LuaToken.Identifier, "print");
				yield return chars.CreateToken(LuaToken.String);
			}
			yield return chars.CreateToken(LuaToken.Eof);
		} // func CreateHtmlTokenStream

		/// <summary></summary>
		/// <param name="charStream"></param>
		/// <param name="scriptPreamble"></param>
		/// <returns></returns>
		public static ILuaLexer CreateHtml(LuaCharLexer charStream, params Token[] scriptPreamble)
			=> new LuaLexer(CreateHtmlTokenStream(charStream, false, scriptPreamble).GetEnumerator());

		/// <summary></summary>
		/// <param name="charStream"></param>
		/// <param name="enforceCode"></param>
		/// <returns></returns>
		public static ILuaLexer CreateHtml(LuaCharLexer charStream, bool enforceCode)
			=> new LuaLexer(CreateHtmlTokenStream(charStream, enforceCode, null).GetEnumerator());

		/// <summary></summary>
		public const int HtmlCharStreamLookAHead = 4;

		#endregion

		/// <summary>Check for A-Z,0-9 and _</summary>
		/// <param name="c"></param>
		/// <returns></returns>
		public static bool IsIdentifierChar(char c)
			=> Char.IsLetterOrDigit(c) || c == '_';

		/// <summary>Is the given identifier a keyword.</summary>
		/// <param name="token"></param>
		/// <returns></returns>
		public static bool IsKeyWord(LuaToken token)
		{
			switch(token)
			{
				case LuaToken.KwAnd:
				case LuaToken.KwBreak:
				case LuaToken.KwCast:
				case LuaToken.KwConst:
				case LuaToken.KwDo:
				case LuaToken.KwElse:
				case LuaToken.KwElseif:
				case LuaToken.KwEnd:
				case LuaToken.KwFalse:
				case LuaToken.KwFor:
				case LuaToken.KwForEach:
				case LuaToken.KwFunction:
				case LuaToken.KwGoto:
				case LuaToken.KwIf:
				case LuaToken.KwIn:
				case LuaToken.KwLocal:
				case LuaToken.KwNil:
				case LuaToken.KwNot:
				case LuaToken.KwOr:
				case LuaToken.KwRepeat:
				case LuaToken.KwReturn:
				case LuaToken.KwThen:
				case LuaToken.KwTrue:
				case LuaToken.KwUntil:
				case LuaToken.KwWhile:
					return true;
				default:
					return false;
			}
		} // func IsKeyWord

		/// <summary>Is the given identifier a keyword.</summary>
		/// <param name="member"></param>
		/// <returns></returns>
		public static bool IsKeyWord(string member)
			=> Array.BinarySearch(keywords.Value, member) >= 0;

		/// <summary></summary>
		/// <param name="token"></param>
		/// <returns></returns>
		public static string GetDefaultTokenValue(LuaToken token)
			=> IsKeyWord(token)
				? GetTokenName(token)
				: String.Empty; 

		/// <summary>Resolves the name of the token.</summary>
		/// <param name="type"></param>
		/// <returns></returns>
		public static string GetTokenName(LuaToken type)
		{
			var tokenType = typeof(LuaToken);
			var ti = tokenType.GetTypeInfo();
			var name = Enum.GetName(tokenType, type);

			var fi = ti.GetDeclaredField(name);
			if (fi != null)
			{
				var tokenName = fi.GetCustomAttribute<TokenNameAttribute>();
				if (tokenName != null)
					name = tokenName.Name;
			}

			return name;
		} // func GetTokenName

		/// <summary>Create a new token.</summary>
		/// <param name="kind"></param>
		/// <param name="value"></param>
		/// <param name="startAt"></param>
		/// <param name="endAt"></param>
		/// <returns></returns>
		public static Token CreateToken(LuaToken kind, string value, Position startAt, Position endAt)
			=> new Token(kind, value ?? GetDefaultTokenValue(kind), startAt, endAt);
	} // class LuaLexer

	#endregion
}
