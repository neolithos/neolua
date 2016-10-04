using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.Scripting;
using Microsoft.Scripting.Debugging;
using Microsoft.Scripting.Debugging.CompilerServices;

namespace Neo.IronLua
{
	#region -- class LuaTraceLineEventArgs ----------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public class LuaTraceLineEventArgs : EventArgs
	{
		private readonly string name;
		private readonly string sourceFile;
		private readonly int line;

		private readonly Lazy<IDictionary<object, object>> locals;

		internal LuaTraceLineEventArgs(string name, string sourceFile, int line, Func<IDictionary<object, object>> scopeCallback)
		{
			this.name = name;
			this.sourceFile = sourceFile;
			this.line = line;
			this.locals = new Lazy<IDictionary<object, object>>(scopeCallback);
		} // ctor

		/// <summary></summary>
		public string ScopeName => name;
		/// <summary></summary>
		public string SourceName => sourceFile;
		/// <summary></summary>
		public int SourceLine => line;

		/// <summary></summary>
		public IDictionary<object, object> Locals => locals.Value;
	} // class LuaTraceLineEventArgs

  #endregion

  #region -- class LuaTraceLineExceptionEventArgs -------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
  public class LuaTraceLineExceptionEventArgs : LuaTraceLineEventArgs
  {
    private Exception exception;

		internal LuaTraceLineExceptionEventArgs(string name, string sourceFile, int line, Func<IDictionary<object, object>> scopeCallback, Exception exception)
			: base(name, sourceFile, line, scopeCallback)
		{
			this.exception = exception;
		} // ctor

		/// <summary></summary>
		public Exception Exception => exception;
  } // class LuaTraceLineExceptionEventArgs

  #endregion
  
  #region -- class LuaTraceLineDebugger -----------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
  public class LuaTraceLineDebugger : ILuaDebug
  {
    #region -- class TraceCallback ----------------------------------------------------

    /// <summary>Trace proxy</summary>
    private class TraceCallback : ITraceCallback
    {
      private LuaTraceLineDebugger traceLineDebugger;

      public TraceCallback(LuaTraceLineDebugger traceLineDebugger)
      {
        this.traceLineDebugger = traceLineDebugger;
      } // ctor

			public void OnTraceEvent(TraceEventKind kind, string name, string sourceFileName, SourceSpan sourceSpan, Func<IDictionary<object, object>> scopeCallback, object payload, object customPayload)
      {
        switch (kind)
        {
          case TraceEventKind.ExceptionUnwind:
            traceLineDebugger.OnExceptionUnwind(new LuaTraceLineExceptionEventArgs(name, sourceFileName, sourceSpan.Start.Line, scopeCallback, (Exception)payload));
            break;
          case TraceEventKind.FrameEnter:
            traceLineDebugger.OnFrameEnter(new LuaTraceLineEventArgs(name, sourceFileName, sourceSpan.Start.Line, scopeCallback)); 
            break;
          case TraceEventKind.FrameExit:
            traceLineDebugger.OnFrameExit();
            break;
          case TraceEventKind.TracePoint:
            traceLineDebugger.OnTracePoint(new LuaTraceLineEventArgs(name, sourceFileName, sourceSpan.Start.Line, scopeCallback));
            break;
        }
      } // proc OnTraceEvent
		} // class TraceCallback

    #endregion

    #region -- class TransformAllLambda -----------------------------------------------

    /// <summary>Create tracable lambda</summary>
    private class TransformAllLambda : ExpressionVisitor, IDebugCompilerSupport
    {
      private DebugContext context;
      private DebugLambdaInfo debugLambdaInfo;

      public TransformAllLambda(DebugContext context)
      {
        this.context = context;
        this.debugLambdaInfo = new DebugLambdaInfo(this, null, false, null, null, null);
      } // ctor

			protected override Expression VisitBlock(BlockExpression node)
				=> base.VisitBlock(Expression.Block(node.Type, node.Variables, ReduceDebugExpressions(node.Expressions)));

      protected override Expression VisitLambda<T>(Expression<T> node)
      {
        var expr = base.VisitLambda(node);

        if (node.Parameters.Count > 0 && node.Parameters[0].Name == "$frame")
          return expr;
        else
          return context.TransformLambda((Expression<T>)expr, debugLambdaInfo);
      } // func VisitLambda

			public bool DoesExpressionNeedReduction(Expression expression)
				=> true;

			public bool IsCallToDebuggableLambda(Expression expression)
				=> true;

      public Expression QueueExpressionForReduction(Expression expression)
        => expression;
      
      private static IEnumerable<Expression> ReduceDebugExpressions(ReadOnlyCollection<Expression> expressions)
      {
        var newBlock = new Expression[expressions.Count];
        var current = 0;
        foreach (Expression expr in expressions)
        {
          var exprDebugInfo = expr as DebugInfoExpression;
          if (current > 0 && exprDebugInfo != null)
          {
            if (exprDebugInfo.StartLine != 16707566)
            {
              if (newBlock[current - 1] is DebugInfoExpression)
                newBlock[current - 1] = expr;
              else
                newBlock[current++] = expr;
            }
          }
          else
            newBlock[current++] = expr;
        }
        if (newBlock[current - 1] is DebugInfoExpression)
          current--;

        return newBlock.Take(current);
      } // func ReduceDebugExpressions
    } // class TransformAllLambda

		#endregion

		#region -- class LuaTraceChunk ----------------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary>Chunk definition.</summary>
		protected class LuaTraceChunk : LuaChunk
    {
			/// <summary></summary>
			/// <param name="lua"></param>
			/// <param name="name"></param>
			/// <param name="chunk"></param>
      public LuaTraceChunk(Lua lua, string name, Delegate chunk)
        : base(lua, name, chunk)
      {
      } // ctor
    } // class LuaTraceChunk

    #endregion

    private TraceCallback callback;
    private DebugContext context;
    private ITracePipeline pipeline;

		/// <summary></summary>
    public LuaTraceLineDebugger()
    {
      callback = new TraceCallback(this);
      context = DebugContext.CreateInstance();
      pipeline = TracePipeline.CreateInstance(context);
      pipeline.TraceCallback = callback;
    } // ctor

		#region -- ILuaDebug members ------------------------------------------------------

		/// <summary></summary>
		/// <param name="lua"></param>
		/// <param name="expr"></param>
		/// <returns></returns>
		protected virtual LuaTraceChunk CreateChunk(Lua lua, LambdaExpression expr)
			=> new LuaTraceChunk(lua, expr.Name, expr.Compile());

		LuaChunk ILuaDebug.CreateChunk(Lua lua, LambdaExpression expr)
    {
      var transform = new TransformAllLambda(context);
      expr = (LambdaExpression)transform.Visit(expr);

      return CreateChunk(lua, expr);
    } // func CreateCunk

    LuaDebugLevel ILuaDebug.Level { get { return LuaDebugLevel.Line; } }

    #endregion

		/// <summary></summary>
		/// <param name="e"></param>
		protected virtual void OnFrameEnter(LuaTraceLineEventArgs e)
    {
    } // proc OnFrameEnter

		/// <summary></summary>
		/// <param name="e"></param>
		protected virtual void OnTracePoint(LuaTraceLineEventArgs e)
    {
    } // proc OnTracePoint

		/// <summary></summary>
		/// <param name="e"></param>
    protected virtual void OnExceptionUnwind(LuaTraceLineExceptionEventArgs e)
    {
		} // proc OnExceptionUnwind

		/// <summary></summary>
		protected virtual void OnFrameExit()
    {
    } // proc OnFrameExit
  } // class LuaTraceLineDebugger

  #endregion
}
