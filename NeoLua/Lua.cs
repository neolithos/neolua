using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Text;

namespace Neo.IronLua
{
  ///////////////////////////////////////////////////////////////////////////////
  /// <summary>Manages the Lua-Script-Environment. At the time it holds the
  /// binder cache between the compiled scripts.</summary>
  public partial class Lua
  {
    #region -- class LuaDebugInfoGenerator --------------------------------------------

    ///////////////////////////////////////////////////////////////////////////////
    /// <summary></summary>
    private class LuaDebugInfoGenerator : DebugInfoGenerator
    {
      private Lua lua;
      private LuaChunk currentChunk;

      public LuaDebugInfoGenerator(Lua lua, LuaChunk currentChunk)
      {
        this.lua = lua;
        this.currentChunk = currentChunk;
      } // ctor

      public override void MarkSequencePoint(LambdaExpression method, int ilOffset, DebugInfoExpression sequencePoint)
      {
      } // proc MarkSequencePoint
    } // class LuaDebugInfoGenerator

    #endregion

    private bool lPrintExpressionTree = false;
    private Dictionary<string, LuaChunk> chunks = new Dictionary<string, LuaChunk>();

    public Lua()
    {
    } // ctor

    #region -- Compile ----------------------------------------------------------------

    /// <summary>Erzeugt ein Delegate aus dem Code, ohne ihn auszuführen.</summary>
    /// <param name="sFileName">Dateiname die gelesen werden soll.</param>
    /// <param name="args">Parameter für den Codeblock</param>
    /// <returns>Compiled chunk.</returns>
    public LuaChunk CompileChunk(string sFileName, params KeyValuePair<string, Type>[] args)
    {
      return CompileChunk(sFileName, new StreamReader(sFileName), args);
    } // func CompileChunk

    /// <summary>Erzeugt ein Delegate aus dem Code, ohne ihn auszuführen.</summary>
    /// <param name="sr">Inhalt</param>
    /// <param name="sName">Name der Datei</param>
    /// <param name="args">Parameter für den Codeblock</param>
    /// <returns>Compiled chunk.</returns>
    public LuaChunk CompileChunk(TextReader tr, string sName, params KeyValuePair<string, Type>[] args)
    {
      return CompileChunk(sName, tr, args);
    } // func CompileChunk

    /// <summary>Erzeugt ein Delegate aus dem Code, ohne ihn auszuführen.</summary>
    /// <param name="sCode">Code, der das Delegate darstellt.</param>
    /// <param name="sName">Name des Delegates</param>
    /// <param name="args">Argumente</param>
    /// <returns>Compiled chunk.</returns>
    public LuaChunk CompileChunk(string sCode, string sName, params KeyValuePair<string, Type>[] args)
    {
      return CompileChunk(sName, new StringReader(sCode), args);
    } // func CompileChunk

    internal LuaChunk CompileChunk(string sChunkName, TextReader tr, IEnumerable<KeyValuePair<string, Type>> args)
    { 
      using (LuaLexer l = new LuaLexer(sChunkName, tr))
      {
        LambdaExpression expr = Parser.ParseChunk(this, l, args);

        if (lPrintExpressionTree)
        {
          Console.WriteLine(Parser.ExpressionToString(expr));
          Console.WriteLine(new string('=', 79));
        }

        LuaChunk chunk;
        try
        {
          // Get the chunk
          lock (chunks)
          {
            if (!chunks.TryGetValue(expr.Name, out chunk))
              chunk = chunks[CreateEmptyChunk(expr.Name)];
          }

          // compile the chunk
          Delegate dlg = expr.Compile(new LuaDebugInfoGenerator(this, chunk));

          // complete the chunk
          chunk.Chunk = dlg;
          chunk.ChunkName = sChunkName;
        }
        catch
        {
          throw;
        }
        return chunk;
      }
    } // func CompileChunk

    internal string CreateEmptyChunk(string sName)
    {
      lock (chunks)
      {
        int iId = 0;
        string sCurrentName = sName;

        // create a unique name
        while(chunks.ContainsKey(sCurrentName))
          sCurrentName = String.Format("{0}#{1}", sName, ++iId);

        // add the empty chunk, to reserve the chunk name
        chunks[sCurrentName] = new LuaChunk(this, sCurrentName, sName);
        return sCurrentName;
      }
    } // func CreateEmptyChunk

    internal void RemoveChunk(string sName)
    {
      lock (chunks)
        chunks.Remove(sName);
    } // proc RemoveChunk

    #endregion

    /// <summary>Creates an empty environment for the lua function</summary>
    /// <returns>Initialized environment</returns>
    public LuaGlobal CreateEnvironment()
    {
      return new LuaGlobal(this);
    } // func CreateEnvironment

    internal bool PrintExpressionTree { get { return lPrintExpressionTree; } set { lPrintExpressionTree = value; } }
  } // class Lua
}
