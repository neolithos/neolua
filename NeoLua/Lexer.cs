using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace Neo.IronLua
{
	#region -- enum LuaToken ------------------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
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
		/// <summary>Invalid comment</summary>
		InvalidComment,

		/// <summary>NewLine</summary>
		[Description("\\n")]
		NewLine,
		/// <summary>Space</summary>
		Whitespace,
		/// <summary>Comment</summary>
		Comment,
		/// <summary>string</summary>
		[Description("string")]
		String,
		/// <summary>Integer or floating point number</summary>
		[Description("number")]
		Number,
		/// <summary>Identifier</summary>
		Identifier,

		/// <summary>Keyword and</summary>
		[Description("and")]
		KwAnd,
		/// <summary>Keyword break</summary>
		[Description("break")]
		KwBreak,
		/// <summary>Keyword cast</summary>
		[Description("cast")]
		KwCast,
		/// <summary>Keyword const</summary>
		[Description("const")]
		KwConst,
		/// <summary>Keyword do</summary>
		[Description("do")]
		KwDo,
		/// <summary>Keyword else</summary>
		[Description("else")]
		KwElse,
		/// <summary>Keyword elseif</summary>
		[Description("elseif")]
		KwElseif,
		/// <summary>Keyword end</summary>
		[Description("end")]
		KwEnd,
		/// <summary>Keyword false</summary>
		[Description("false")]
		KwFalse,
		/// <summary>Keyword for</summary>
		[Description("for")]
		KwFor,
		/// <summary>Keyword foreach</summary>
		[Description("foreach")]
		KwForEach,
		/// <summary>Keyword function</summary>
		[Description("function")]
		KwFunction,
		/// <summary>Keyword goto</summary>
		[Description("goto")]
		KwGoto,
		/// <summary>Keyword if</summary>
		[Description("if")]
		KwIf,
		/// <summary>Keyword in</summary>
		[Description("in")]
		KwIn,
		/// <summary>Keyword local</summary>
		[Description("local")]
		KwLocal,
		/// <summary>Keyword nil</summary>
		[Description("nil")]
		KwNil,
		/// <summary>Keyword not</summary>
		[Description("not")]
		KwNot,
		/// <summary>Keyword or</summary>
		[Description("or")]
		KwOr,
		/// <summary>Keyword repeat</summary>
		[Description("repeat")]
		KwRepeat,
		/// <summary>Keyword return</summary>
		[Description("return")]
		KwReturn,
		/// <summary>Keyword then</summary>
		[Description("then")]
		KwThen,
		/// <summary>Keyword true</summary>
		[Description("true")]
		KwTrue,
		/// <summary>Keyword until</summary>
		[Description("until")]
		KwUntil,
		/// <summary>Keyword while</summary>
		[Description("while")]
		KwWhile,

		/// <summary>+</summary>
		[Description("+")]
		Plus,
		/// <summary>-</summary>
		[Description("-")]
		Minus,
		/// <summary>*</summary>
		[Description("*")]
		Star,
		/// <summary>/</summary>
		[Description("/")]
		Slash,
		/// <summary>//</summary>
		[Description("//")]
		SlashShlash,
		/// <summary>%</summary>
		[Description("%")]
		Percent,
		/// <summary>^</summary>
		[Description("^")]
		Caret,
		/// <summary>&amp;</summary>
		[Description("&")]
		BitAnd,
		/// <summary>|</summary>
		[Description("|")]
		BitOr,
		/// <summary>~</summary>
		[Description("~")]
		Dilde,
		/// <summary>#</summary>
		[Description("#")]
		Cross,
		/// <summary>==</summary>
		[Description("==")]
		Equal,
		/// <summary>~=</summary>
		[Description("~=")]
		NotEqual,
		/// <summary>&lt;=</summary>
		[Description("<=")]
		LowerEqual,
		/// <summary>&gt;=</summary>
		[Description(">=")]
		GreaterEqual,
		/// <summary>&lt;</summary>
		[Description("<")]
		Lower,
		/// <summary>&gt;</summary>
		[Description(">")]
		Greater,
		/// <summary>&lt;&lt;</summary>
		[Description("<<")]
		ShiftLeft,
		/// <summary>&gt;&gt;</summary>
		[Description(">>")]
		ShiftRight,
		/// <summary>=</summary>
		[Description("=")]
		Assign,
		/// <summary>(</summary>
		[Description("(")]
		BracketOpen,
		/// <summary>)</summary>
		[Description(")")]
		BracketClose,
		/// <summary>{</summary>
		[Description("{")]
		BracketCurlyOpen,
		/// <summary>}</summary>
		[Description("}")]
		BracketCurlyClose,
		/// <summary>[</summary>
		[Description("[")]
		BracketSquareOpen,
		/// <summary>]</summary>
		[Description("]")]
		BracketSquareClose,
		/// <summary>;</summary>
		[Description(";")]
		Semicolon,
		/// <summary>:</summary>
		[Description(":")]
		Colon,
		/// <summary>::</summary>
		[Description("::")]
		ColonColon,
		/// <summary>,</summary>
		[Description(",")]
		Comma,
		/// <summary>.</summary>
		[Description(".")]
		Dot,
		/// <summary>..</summary>
		[Description("..")]
		DotDot,
		/// <summary>...</summary>
		[Description("...")]
		DotDotDot
	} // enum LuaToken

	#endregion

	#region -- struct Position ----------------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Position in the source file</summary>
	[TypeConverter(typeof(ExpandableObjectConverter))]
	public struct Position
	{
		private SymbolDocumentInfo document;
		private int iColumn;
		private int iLine;
		private long iIndex;

		internal Position(SymbolDocumentInfo document, int iLine, int iColumn, long iIndex)
		{
			this.document = document;
			this.iLine = iLine;
			this.iColumn = iColumn;
			this.iIndex = iIndex;
		} // ctor

		/// <summary>Umwandlung in ein übersichtliche Darstellung.</summary>
		/// <returns>Zeichenfolge mit Inhalt</returns>
		public override string ToString()
		{
			return String.Format("({0}; {1}; {2})", Line, Col, Index);
		} // func ToString

		/// <summary>Dateiname in der dieser Position sich befindet.</summary>
		public string FileName { get { return document.FileName; } }
		/// <summary>Document where the token was found.</summary>
		internal SymbolDocumentInfo Document { get { return document; } }
		/// <summary>Zeile, bei 1 beginnent.</summary>
		public int Line { get { return iLine; } }
		/// <summary>Spalte, bei 1 beginnent.</summary>
		public int Col { get { return iColumn; } }
		/// <summary>Index bei 0 beginnend.</summary>
		public long Index { get { return iIndex; } }
	} // struct Position

	#endregion

	#region -- class Token --------------------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Represents a token of the lua source file.</summary>
	[TypeConverter(typeof(ExpandableObjectConverter))]
	public class Token
	{
		// -- Position innerhalb der Datei --
		private Position fStart;
		private Position fEnd;
		// -- Token-Wert --
		private LuaToken iKind;
		private string sValue;

		/// <summary>Erzeugt einen Token.</summary>
		/// <param name="iKind">Type des Wertes.</param>
		/// <param name="sValue">Der Wert.</param>
		/// <param name="fStart">Beginn des Tokens</param>
		/// <param name="fEnd">Ende des Tokens</param>
		internal Token(LuaToken iKind, string sValue, Position fStart, Position fEnd)
		{
			this.iKind = iKind;
			this.fStart = fStart;
			this.fEnd = fEnd;
			this.sValue = sValue;
		} // ctor

		/// <summary>Umwandlung in ein übersichtliche Darstellung.</summary>
		/// <returns>Zeichenfolge mit Inhalt</returns>
		public override string ToString()
		{
			return String.Format("[{0,4},{1,4} - {2,4},{3,4}] {4}='{5}'", Start.Line, Start.Col, End.Line, End.Col, Typ, Value);
		} // func ToString

		/// <summary>Art des Wertes</summary>
		public LuaToken Typ { get { return iKind; } }
		/// <summary>Wert selbst</summary>
		public string Value { get { return sValue; } }

		/// <summary>Start des Tokens</summary>
		public Position Start { get { return fStart; } }
		/// <summary>Ende des Tokens</summary>
		public Position End { get { return fEnd; } }
		/// <summary>Länge des Tokens</summary>
		public int Length { get { unchecked { return (int)(fEnd.Index - fStart.Index); } } }
	} // class Token

	#endregion

	#region -- class LuaLexer -----------------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Lexer for the lua syntax.</summary>
	public sealed class LuaLexer : IDisposable
	{
		private Token lookahead = null;
		private Token current = null;

		private Position fStart;                  // Start of the current token
		private Position fEnd;                    // Posible end of the current token
		private char cCur;                        // Current char
		private bool lEof;												// End of file reached
		private int iState;                       // Current state
		private StringBuilder sbCur = null;       // Currently collected chars

		private TextReader tr;                    // Source for the lexer
		private SymbolDocumentInfo document;      // Information about the source document
		private int iCurrentLine;                 // Line in the source file
		private int iCurrentColumn = 1;           // Column in the source file
		private long iCurrentIndex = 0;           // Index in the source file


		private bool lSkipComments = true;				// Should the scanner skip comments

		#region -- Ctor/Dtor --------------------------------------------------------------

		/// <summary>Creates the lexer für the lua parser</summary>
		/// <param name="sFileName">Filename</param>
		/// <param name="tr">Input for the scanner, will be disposed on the lexer dispose.</param>
		/// <param name="iCurrentLine"></param>
		/// <param name="iCurrentColumn"></param>
		public LuaLexer(string sFileName, TextReader tr, int iCurrentLine = 1, int iCurrentColumn = 1)
		{
			this.document = Expression.SymbolDocument(sFileName);
			this.iCurrentLine = iCurrentLine;
			this.iCurrentColumn = iCurrentColumn;
			this.tr = tr;

			lEof = false;
			fStart =
				fEnd = new Position(document, iCurrentLine, iCurrentColumn, iCurrentIndex);
			cCur = Read(); // Lies das erste Zeichen aus dem Buffer
		} // ctor

		/// <summary>Destroy the lexer and the TextReader</summary>
		public void Dispose()
		{
			if (tr != null)
			{
				tr.Dispose();
				tr = null;
			}
		} // proc Dispose

		#endregion

		#region -- Buffer -----------------------------------------------------------------

		/// <summary>Liest Zeichen aus den Buffer</summary>
		/// <returns>Zeichen oder <c>\0</c>, für das Ende.</returns>
		private char Read()
		{
			int i = -1;
			if (tr == null) // Source file is readed
			{
				lEof = true;
				return '\0';
			}
			else
			{
				i = tr.Read();
				if (i == -1) // End of file reached
				{
					tr.Dispose();
					tr = null;
					lEof = true;
					return '\0';
				}
				else
				{
					char c = (char)i;

					iCurrentIndex++;

					// Normalize new line
					if (c == '\n')
					{
						if (tr.Peek() == 13)
							tr.Read();

						iCurrentColumn = 1;
						iCurrentLine++;

						return '\n';
					}
					else if (c == '\r')
					{
						iCurrentColumn = 1;
						iCurrentLine++;
						if (tr.Peek() == 10)
							tr.Read();

						return '\n';
					}
					else
					{
						iCurrentColumn++;

						return c;
					}
				}
			}
		} // func Read

		#endregion

		#region -- Scanner Operationen ----------------------------------------------------

		/// <summary>Fügt einen Wert an.</summary>
		/// <param name="cCur"></param>
		private void AppendValue(char cCur)
		{
			if (sbCur == null)
				sbCur = new StringBuilder();

			sbCur.Append(cCur);
		} // proc AppendValue

		/// <summary>Kopiert das Zeichen in den Wert-Buffer</summary>
		/// <param name="iNewState">Neuer Status des Scanners</param>
		private void EatChar(int iNewState)
		{
			AppendValue(cCur);
			NextChar(iNewState);
		} // proc EatChar

		/// <summary>Nächstes Zeichen ohne eine Kopie anzufertigen</summary>
		/// <param name="iNewState">Neuer Status des Scanners</param>
		private void NextChar(int iNewState)
		{
			fEnd = new Position(document, iCurrentLine, iCurrentColumn, iCurrentIndex);
			cCur = Read();
			iState = iNewState;
		} // proc NextChar

		/// <summary>Erzeugt einen Token</summary>
		/// <param name="iKind">Art des Tokens</param>
		/// <param name="iNewState"></param>
		/// <returns>Token</returns>
		private Token CreateToken(int iNewState, LuaToken iKind)
		{
			iState = iNewState;
			Token tok = new Token(iKind, CurValue, fStart, fEnd);
			fStart = fEnd;
			sbCur = null;
			return tok;
		} // func CreateToken

		/// <summary>Erzeugt einen Token</summary>
		/// <param name="iKind">Art des Tokens</param>
		/// <param name="iNewState"></param>
		/// <returns>Token</returns>
		private Token NextCharAndCreateToken(int iNewState, LuaToken iKind)
		{
			NextChar(iNewState);
			return CreateToken(iNewState, iKind);
		} // func CreateToken

		/// <summary>Erzeugt einen Token</summary>
		/// <param name="iKind">Art des Tokens</param>
		/// <param name="iNewState"></param>
		/// <returns>Token</returns>
		private Token EatCharAndCreateToken(int iNewState, LuaToken iKind)
		{
			EatChar(iNewState);
			return CreateToken(iNewState, iKind);
		} // func CreateToken

		/// <summary>Akuelles Zeichen</summary>
		private char Cur { get { return cCur; } }
		/// <summary>Aktueller Wert</summary>
		private string CurValue { get { return sbCur == null ? "" : sbCur.ToString(); } }
		/// <summary>Aktueller Status des Scanners</summary>
		private int CurState { get { return iState; } }

		#endregion

		#region -- Token Operationen ------------------------------------------------------

		private Token NextTokenWithSkipRules()
		{
			Token next = NextToken();
			if (lSkipComments && next.Typ == LuaToken.Comment)
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
			if (lookahead == null) // Erstinitialisierung der Lookaheads notwendig
			{
				current = NextTokenWithSkipRules();
				lookahead = NextTokenWithSkipRules();
			}
			else
			{
				current = lookahead;
				lookahead = NextTokenWithSkipRules();
			}
		} // proc Next

		/// <summary>Next token</summary>
		public Token LookAhead { get { return lookahead; } }
		/// <summary>Current token</summary>
		public Token Current { get { return current; } }
		/// <summary>Should the scanner skip comments</summary>
		public bool SkipComments { get { return lSkipComments; } set { lSkipComments = value; } }

		#endregion

		#region -- NextToken --------------------------------------------------------------

		private Token NextToken()
		{
			char cStringMode = '\0';
			byte bChar = 0;
			while (true)
			{
				char c = Cur;

				switch (CurState)
				{
					#region -- 0 ----------------------------------------------------------------
					case 0:
						if (lEof)
							return CreateToken(0, LuaToken.Eof);
						else if (c == '\n')
							return NextCharAndCreateToken(0, LuaToken.NewLine);
						else if (Char.IsWhiteSpace(c))
							NextChar(10);

						else if (c == '+')
							return NextCharAndCreateToken(0, LuaToken.Plus);
						else if (c == '-')
							NextChar(50);
						else if (c == '*')
							return NextCharAndCreateToken(0, LuaToken.Star);
						else if (c == '/')
							NextChar(28);
						else if (c == '%')
							return NextCharAndCreateToken(0, LuaToken.Percent);
						else if (c == '^')
							return NextCharAndCreateToken(0, LuaToken.Caret);
						else if (c == '&')
							return NextCharAndCreateToken(0, LuaToken.BitAnd);
						else if (c == '|')
							return NextCharAndCreateToken(0, LuaToken.BitOr);
						else if (c == '#')
							return NextCharAndCreateToken(0, LuaToken.Cross);
						else if (c == '=')
							NextChar(20);
						else if (c == '~')
							NextChar(21);
						else if (c == '<')
							NextChar(22);
						else if (c == '>')
							NextChar(23);
						else if (c == '(')
							return NextCharAndCreateToken(0, LuaToken.BracketOpen);
						else if (c == ')')
							return NextCharAndCreateToken(0, LuaToken.BracketClose);
						else if (c == '{')
							return NextCharAndCreateToken(0, LuaToken.BracketCurlyOpen);
						else if (c == '}')
							return NextCharAndCreateToken(0, LuaToken.BracketCurlyClose);
						else if (c == '[')
							NextChar(27);
						else if (c == ']')
							return NextCharAndCreateToken(0, LuaToken.BracketSquareClose);
						else if (c == ';')
							return NextCharAndCreateToken(0, LuaToken.Semicolon);
						else if (c == ':')
							NextChar(30);
						else if (c == ',')
							return NextCharAndCreateToken(0, LuaToken.Comma);
						else if (c == '.')
							NextChar(24);

						else if (c == '"')
						{
							cStringMode = c;
							NextChar(40);
						}
						else if (c == '\'')
						{
							cStringMode = c;
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
							return EatCharAndCreateToken(0, LuaToken.InvalidChar);
						break;
					#endregion
					#region -- 10 Whitespaces ---------------------------------------------------
					case 10:
						if (c == '\n' || lEof || !Char.IsWhiteSpace(c))
							return CreateToken(0, LuaToken.Whitespace);
						else
							NextChar(10);
						break;
					#endregion
					#region -- 20 ---------------------------------------------------------------
					case 20:
						if (c == '=')
							return NextCharAndCreateToken(0, LuaToken.Equal);
						else
							return CreateToken(0, LuaToken.Assign);
					case 21:
						if (c == '=')
							return NextCharAndCreateToken(0, LuaToken.NotEqual);
						else
							return CreateToken(0, LuaToken.Dilde);
					case 22:
						if (c == '=')
							return NextCharAndCreateToken(0, LuaToken.LowerEqual);
						else if (c == '<')
							return NextCharAndCreateToken(0, LuaToken.ShiftLeft);
						else
							return CreateToken(0, LuaToken.Lower);
					case 23:
						if (c == '=')
							return NextCharAndCreateToken(0, LuaToken.GreaterEqual);
						else if (c == '>')
							return NextCharAndCreateToken(0, LuaToken.ShiftRight);
						else
							return CreateToken(0, LuaToken.Greater);
					case 24:
						if (c == '.')
							NextChar(25);
						else if (c >= '0' && c <= '9')
						{
							AppendValue('.');
							EatChar(62);
						}
						else
							return CreateToken(0, LuaToken.Dot);
						break;
					case 25:
						if (c == '.')
							NextChar(26);
						else
							return CreateToken(0, LuaToken.DotDot);
						break;
					case 26:
						if (c == '.')
							return NextCharAndCreateToken(0, LuaToken.DotDotDot);
						else
							return CreateToken(0, LuaToken.DotDotDot);
					case 27:
						if (c == '=' || c == '[')
							return ReadTextBlock(true);
						else
							return CreateToken(0, LuaToken.BracketSquareOpen);
					case 28:
						if (c == '/')
							return NextCharAndCreateToken(0, LuaToken.SlashShlash);
						else
							return CreateToken(0, LuaToken.Slash);
					#endregion
					#region -- 30 Label ---------------------------------------------------------
					case 30:
						if (c == ':')
							NextChar(31);
						else
							return CreateToken(0, LuaToken.Colon);
						break;
					case 31:
						if (c == ':')
							return NextCharAndCreateToken(0, LuaToken.ColonColon);
						else
							return CreateToken(0, LuaToken.ColonColon);
					#endregion
					#region -- 40 String --------------------------------------------------------
					case 40:
						if (c == cStringMode)
							return NextCharAndCreateToken(0, LuaToken.String);
						else if (c == '\\')
							NextChar(41);
						else if (lEof || c == '\n')
							return CreateToken(0, LuaToken.InvalidString);
						else
							EatChar(40);
						break;
					case 41:
						if (c == 'a') { AppendValue('\a'); NextChar(40); }
						else if (c == 'b') { AppendValue('\b'); NextChar(40); }
						else if (c == 'f') { AppendValue('\f'); NextChar(40); }
						else if (c == 'n') { AppendValue('\n'); NextChar(40); }
						else if (c == 'r') { AppendValue('\r'); NextChar(40); }
						else if (c == 't') { AppendValue('\t'); NextChar(40); }
						else if (c == 'v') { AppendValue('\v'); NextChar(40); }
						else if (c == '\\') { AppendValue('\\'); NextChar(40); }
						else if (c == '"') { AppendValue('"'); NextChar(40); }
						else if (c == '\'') { AppendValue('\''); NextChar(40); }
						else if (c == 'x')
							NextChar(45);
						else if (c == 'z')
							NextChar(48);

						else if (c >= '0' && c <= '9')
						{
							bChar = unchecked((byte)(c - '0'));
							NextChar(42);
						}
						else
							EatChar(40);
						break;
					case 42:
						if (c >= '0' && c <= '9')
						{
							bChar = unchecked((byte)(bChar * 10 + (c - '0')));
							NextChar(43);
						}
						else
						{
							AppendValue((char)bChar);
							goto case 40;
						}
						break;
					case 43:
						if (c >= '0' && c <= '9')
						{
							bChar = unchecked((byte)(bChar * 10 + (c - '0')));
							AppendValue((char)bChar);
							NextChar(40);
						}
						else
						{
							AppendValue((char)bChar);
							goto case 40;
						}
						break;
					case 45:
						if (c >= '0' && c <= '9')
						{
							bChar = unchecked((byte)(c - '0'));
							NextChar(46);
						}
						else if (c >= 'a' && c <= 'f')
						{
							bChar = unchecked((byte)(c - 'a' + 10));
							NextChar(46);
						}
						else if (c >= 'A' || c <= 'F')
						{
							bChar = unchecked((byte)(c - 'A' + 10));
							NextChar(46);
						}
						else
						{
							AppendValue('x');
							goto case 40;
						}
						break;
					case 46:
						if (c >= '0' && c <= '9')
						{
							bChar = unchecked((byte)((bChar << 4) + (c - '0')));
							AppendValue((char)bChar);
							NextChar(40);
						}
						else if (c >= 'a' && c <= 'f')
						{
							bChar = unchecked((byte)((bChar << 4) + (c - 'a' + 10)));
							AppendValue((char)bChar);
							NextChar(40);
						}
						else if (c >= 'A' || c <= 'F')
						{
							bChar = unchecked((byte)((bChar << 4) + (c - 'A' + 10)));
							AppendValue((char)bChar);
							NextChar(40);
						}
						else
						{
							AppendValue((char)bChar);
							goto case 40;
						}
						break;
					case 48:
						if (Char.IsWhiteSpace(c))
							NextChar(48);
						else
							goto case 40;
						break;
					#endregion
					#region -- 50 Kommentar -----------------------------------------------------
					case 50:
						if (c == '-') // Kommentar
							NextChar(51);
						else
							return CreateToken(0, LuaToken.Minus);
						break;
					case 51:
						if (c == '[')
						{
							NextChar(51);
							return ReadTextBlock(false);
						}
						else if (c == '\n')
							return CreateToken(0, LuaToken.Comment);
						else
							NextChar(52);
						break;
					case 52:
						if (lEof)
							return CreateToken(0, LuaToken.Comment);
						else if (c == '\n')
							return NextCharAndCreateToken(0, LuaToken.Comment);
						else
							NextChar(52);
						break;
					#endregion
					#region -- 60 Number --------------------------------------------------------
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
							return CreateToken(0, LuaToken.Number);
						break;
					case 62:
						if (c == 'e' || c == 'E')
							EatChar(63);
						else if (c >= '0' && c <= '9')
							EatChar(62);
						else
							return CreateToken(0, LuaToken.Number);
						break;
					case 63:
						if (c == '-' || c == '+')
							EatChar(64);
						else if (c >= '0' && c <= '9')
							EatChar(64);
						else
							return CreateToken(0, LuaToken.Number);
						break;
					case 64:
						if (c >= '0' && c <= '9')
							EatChar(64);
						else
							return CreateToken(0, LuaToken.Number);
						break;
					#endregion
					#region -- 70 HexNumber -----------------------------------------------------
					case 70:
						if (c == '.')
							EatChar(71);
						else if (c == 'p' || c == 'P')
							EatChar(72);
						else if (c >= '0' && c <= '9' || c >= 'a' && c <= 'f' || c >= 'A' && c <= 'F')
							EatChar(70);
						else
							return CreateToken(0, LuaToken.Number);
						break;
					case 71:
						if (c == 'p' || c == 'P')
							EatChar(72);
						else if (c >= '0' && c <= '9' || c >= 'a' && c <= 'f' || c >= 'A' && c <= 'F')
							EatChar(71);
						else
							return CreateToken(0, LuaToken.Number);
						break;
					case 72:
						if (c == '-' || c == '+')
							EatChar(73);
						else if (c >= '0' && c <= '9')
							EatChar(73);
						else
							return CreateToken(0, LuaToken.Number);
						break;
					case 73:
						if (c >= '0' && c <= '9')
							EatChar(73);
						else
							return CreateToken(0, LuaToken.Number);
						break;
					#endregion
					#region -- 1000 Ident or Keyword --------------------------------------------
					case 1000:
						if (c == '_' || Char.IsLetterOrDigit(c))
							EatChar(1000);
						else
							return CreateToken(0, LuaToken.Identifier);
						break;
					// and
					case 1010: if (c == 'n') EatChar(1011); else goto case 1000; break;
					case 1011: if (c == 'd') EatChar(1012); else goto case 1000; break;
					case 1012: if (!Char.IsLetterOrDigit(c)) return CreateToken(0, LuaToken.KwAnd); else goto case 1000;
					// break
					case 1020: if (c == 'r') EatChar(1021); else goto case 1000; break;
					case 1021: if (c == 'e') EatChar(1022); else goto case 1000; break;
					case 1022: if (c == 'a') EatChar(1023); else goto case 1000; break;
					case 1023: if (c == 'k') EatChar(1024); else goto case 1000; break;
					case 1024: if (!Char.IsLetterOrDigit(c)) return CreateToken(0, LuaToken.KwBreak); else goto case 1000;
					// do
					case 1030: if (c == 'o') EatChar(1031); else goto case 1000; break;
					case 1031: if (!Char.IsLetterOrDigit(c)) return CreateToken(0, LuaToken.KwDo); else goto case 1000;
					// else, elseif end
					case 1040: if (c == 'n') EatChar(1041); else if (c == 'l') EatChar(1043); else goto case 1000; break;
					case 1041: if (c == 'd') EatChar(1042); else goto case 1000; break;
					case 1042: if (!Char.IsLetterOrDigit(c)) return CreateToken(0, LuaToken.KwEnd); else goto case 1000;
					case 1043: if (c == 's') EatChar(1044); else goto case 1000; break;
					case 1044: if (c == 'e') EatChar(1045); else goto case 1000; break;
					case 1045: if (c == 'i') EatChar(1046); else if (!Char.IsLetterOrDigit(c)) return CreateToken(0, LuaToken.KwElse); else goto case 1000; break;
					case 1046: if (c == 'f') EatChar(1047); else goto case 1000; break;
					case 1047: if (!Char.IsLetterOrDigit(c)) return CreateToken(0, LuaToken.KwElseif); else goto case 1000;
					// false, for, function
					case 1050: if (c == 'a') EatChar(1051); else if (c == 'o') EatChar(1055); else if (c == 'u') EatChar(1057); else goto case 1000; break;
					case 1051: if (c == 'l') EatChar(1052); else goto case 1000; break;
					case 1052: if (c == 's') EatChar(1053); else goto case 1000; break;
					case 1053: if (c == 'e') EatChar(1054); else goto case 1000; break;
					case 1054: if (!Char.IsLetterOrDigit(c)) return CreateToken(0, LuaToken.KwFalse); else goto case 1000;
					case 1055: if (c == 'r') EatChar(1056); else goto case 1000; break;
					case 1056: if (c == 'e') EatChar(10000); else if (!Char.IsLetterOrDigit(c)) return CreateToken(0, LuaToken.KwFor); else goto case 1000; break;
					case 1057: if (c == 'n') EatChar(1058); else goto case 1000; break;
					case 1058: if (c == 'c') EatChar(1059); else goto case 1000; break;
					case 1059: if (c == 't') EatChar(1060); else goto case 1000; break;
					case 1060: if (c == 'i') EatChar(1061); else goto case 1000; break;
					case 1061: if (c == 'o') EatChar(1062); else goto case 1000; break;
					case 1062: if (c == 'n') EatChar(1063); else goto case 1000; break;
					case 1063: if (!Char.IsLetterOrDigit(c)) return CreateToken(0, LuaToken.KwFunction); else goto case 1000;
					case 10000: if (c == 'a') EatChar(10001); else goto case 1000; break;
					case 10001: if (c == 'c') EatChar(10002); else goto case 1000; break;
					case 10002: if (c == 'h') EatChar(10003); else goto case 1000; break;
					case 10003: if (!Char.IsLetterOrDigit(c)) return CreateToken(0, LuaToken.KwForEach); else goto case 1000;
					// goto
					case 1065: if (c == 'o') EatChar(1066); else goto case 1000; break;
					case 1066: if (c == 't') EatChar(1067); else goto case 1000; break;
					case 1067: if (c == 'o') EatChar(1068); else goto case 1000; break;
					case 1068: if (!Char.IsLetterOrDigit(c)) return CreateToken(0, LuaToken.KwGoto); else goto case 1000;
					// if, in
					case 1070: if (c == 'f') EatChar(1071); else if (c == 'n') EatChar(1072); else goto case 1000; break;
					case 1071: if (!Char.IsLetterOrDigit(c)) return CreateToken(0, LuaToken.KwIf); else goto case 1000;
					case 1072: if (!Char.IsLetterOrDigit(c)) return CreateToken(0, LuaToken.KwIn); else goto case 1000;
					// local
					case 1080: if (c == 'o') EatChar(1081); else goto case 1000; break;
					case 1081: if (c == 'c') EatChar(1082); else goto case 1000; break;
					case 1082: if (c == 'a') EatChar(1083); else goto case 1000; break;
					case 1083: if (c == 'l') EatChar(1084); else goto case 1000; break;
					case 1084: if (!Char.IsLetterOrDigit(c)) return CreateToken(0, LuaToken.KwLocal); else goto case 1000;
					// nil, not
					case 1090: if (c == 'i') EatChar(1091); else if (c == 'o') EatChar(1093); else goto case 1000; break;
					case 1091: if (c == 'l') EatChar(1092); else goto case 1000; break;
					case 1092: if (!Char.IsLetterOrDigit(c)) return CreateToken(0, LuaToken.KwNil); else goto case 1000;
					case 1093: if (c == 't') EatChar(1094); else goto case 1000; break;
					case 1094: if (!Char.IsLetterOrDigit(c)) return CreateToken(0, LuaToken.KwNot); else goto case 1000;
					// or
					case 1100: if (c == 'r') EatChar(1101); else goto case 1000; break;
					case 1101: if (!Char.IsLetterOrDigit(c)) return CreateToken(0, LuaToken.KwOr); else goto case 1000;
					// repeat, return
					case 1110: if (c == 'e') EatChar(1111); else goto case 1000; break;
					case 1111: if (c == 'p') EatChar(1112); else if (c == 't') EatChar(1116); else goto case 1000; break;
					case 1112: if (c == 'e') EatChar(1113); else goto case 1000; break;
					case 1113: if (c == 'a') EatChar(1114); else goto case 1000; break;
					case 1114: if (c == 't') EatChar(1115); else goto case 1000; break;
					case 1115: if (!Char.IsLetterOrDigit(c)) return CreateToken(0, LuaToken.KwRepeat); else goto case 1000;
					case 1116: if (c == 'u') EatChar(1117); else goto case 1000; break;
					case 1117: if (c == 'r') EatChar(1118); else goto case 1000; break;
					case 1118: if (c == 'n') EatChar(1119); else goto case 1000; break;
					case 1119: if (!Char.IsLetterOrDigit(c)) return CreateToken(0, LuaToken.KwReturn); else goto case 1000;
					// then, true
					case 1120: if (c == 'h') EatChar(1121); else if (c == 'r') EatChar(1124); else goto case 1000; break;
					case 1121: if (c == 'e') EatChar(1122); else goto case 1000; break;
					case 1122: if (c == 'n') EatChar(1123); else goto case 1000; break;
					case 1123: if (!Char.IsLetterOrDigit(c)) return CreateToken(0, LuaToken.KwThen); else goto case 1000;
					case 1124: if (c == 'u') EatChar(1125); else goto case 1000; break;
					case 1125: if (c == 'e') EatChar(1126); else goto case 1000; break;
					case 1126: if (!Char.IsLetterOrDigit(c)) return CreateToken(0, LuaToken.KwTrue); else goto case 1000;
					// until
					case 1130: if (c == 'n') EatChar(1131); else goto case 1000; break;
					case 1131: if (c == 't') EatChar(1132); else goto case 1000; break;
					case 1132: if (c == 'i') EatChar(1133); else goto case 1000; break;
					case 1133: if (c == 'l') EatChar(1134); else goto case 1000; break;
					case 1134: if (!Char.IsLetterOrDigit(c)) return CreateToken(0, LuaToken.KwUntil); else goto case 1000;
					// while
					case 1140: if (c == 'h') EatChar(1141); else goto case 1000; break;
					case 1141: if (c == 'i') EatChar(1142); else goto case 1000; break;
					case 1142: if (c == 'l') EatChar(1143); else goto case 1000; break;
					case 1143: if (c == 'e') EatChar(1144); else goto case 1000; break;
					case 1144: if (!Char.IsLetterOrDigit(c)) return CreateToken(0, LuaToken.KwWhile); else goto case 1000;
					// cast
					case 1150: if (c == 'a') EatChar(1151); else if (c == 'o') EatChar(1160); else goto case 1000; break;
					case 1151: if (c == 's') EatChar(1152); else goto case 1000; break;
					case 1152: if (c == 't') EatChar(1153); else goto case 1000; break;
					case 1153: if (!Char.IsLetterOrDigit(c)) return CreateToken(0, LuaToken.KwCast); else goto case 1000;
					// const
					case 1160: if (c == 'n') EatChar(1161); else goto case 1000; break;
					case 1161: if (c == 's') EatChar(1162); else goto case 1000; break;
					case 1162: if (c == 't') EatChar(1163); else goto case 1000; break;
					case 1163: if (!Char.IsLetterOrDigit(c)) return CreateToken(0, LuaToken.KwConst); else goto case 1000;
					#endregion
				}
			}
		} // func NextToken

		private Token ReadTextBlock(bool lStringMode)
		{
			int iSearch = 0;
			int iFind = 0;

			// Zähle die =
			while (Cur == '=')
			{
				NextChar(0);
				iSearch++;
			}
			if (Cur != '[')
				return NextCharAndCreateToken(0, lStringMode ? LuaToken.InvalidString : LuaToken.InvalidComment);
			NextChar(0);

			// Überspringe WhiteSpace bis zum ersten Zeilenumbruch
			while (!lEof && Char.IsWhiteSpace(Cur))
			{
				if (Cur == '\n')
				{
					NextChar(0);
					break;
				}
				else
					NextChar(0);
			}

			// Suche das Ende
		ReadChars:
			while (Cur != ']')
			{
				if (lEof)
					return NextCharAndCreateToken(0, lStringMode ? LuaToken.InvalidString : LuaToken.InvalidComment);
				else if (lStringMode)
					EatChar(0);
				else
					NextChar(0);
			}

			// Zähle die =
			iFind = 0;
			NextChar(0);
			while (Cur == '=')
			{
				NextChar(0);
				iFind++;
			}
			if (Cur == ']' && iFind == iSearch)
				return NextCharAndCreateToken(0, lStringMode ? LuaToken.String : LuaToken.Comment);
			else
			{
				AppendValue(']');
				for (int i = 0; i < iFind; i++)
					AppendValue('=');
				goto ReadChars;
			}
		} // proc ReadTextBlock

		#endregion

		/// <summary>Resolves the name of the token.</summary>
		/// <param name="typ"></param>
		/// <returns></returns>
		public static string GetTokenName(LuaToken typ)
		{
			Type t = typeof(LuaToken);
			string sName = t.GetEnumName(typ);
			FieldInfo fi = t.GetField(sName);
			if (fi != null)
			{
				DescriptionAttribute attr = (DescriptionAttribute)Attribute.GetCustomAttribute(fi, typeof(DescriptionAttribute));
				if (attr != null)
					return attr.Description;
			}
			return sName;
		} // func GetTokenName
	} // class LuaLexer

	#endregion
}
