using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using Microsoft.Scripting.Debugging;
using Microsoft.Scripting.Debugging.CompilerServices;

namespace Neo.IronLua
{
  #region -- class LuaTraceLineEventArgs ----------------------------------------------

  public class LuaTraceLineEventArgs : EventArgs
  {
    private string sName;
    private string sSourceFile;
    private int iLine;
    private Func<Microsoft.Scripting.IAttributesCollection> scopeCallback;
    
    private IDictionary<object, object> locals;

    internal LuaTraceLineEventArgs(string sName, string sSourceFile, int iLine, Func<Microsoft.Scripting.IAttributesCollection> scopeCallback)
    {
      this.sName = sName;
      this.sSourceFile = sSourceFile;
      this.iLine = iLine;
      this.scopeCallback = scopeCallback;
    } // ctor

    public string ScopeName { get { return sName; } }
    public string SourceName { get { return sSourceFile; } }
    public int SourceLine { get { return iLine; } }

    public IDictionary<object, object> Locals
    {
      get
      {
        if (locals == null)
          locals = scopeCallback().AsObjectKeyedDictionary();
        return locals;
      }
    } // prop Locals
  } // class LuaTraceLineEventArgs

  #endregion

  #region -- class LuaTraceLineExceptionEventArgs -------------------------------------

  public class LuaTraceLineExceptionEventArgs : LuaTraceLineEventArgs
  {
    private Exception exception;

    internal LuaTraceLineExceptionEventArgs(string sName, string sSourceFile, int iLine, Func<Microsoft.Scripting.IAttributesCollection> scopeCallback, Exception exception)
      : base(sName, sSourceFile, iLine, scopeCallback)
    {
      this.exception = exception;
    } // ctor

    public Exception Exception { get { return exception; } }
  } // class LuaTraceLineExceptionEventArgs

  #endregion
  
  #region -- class LuaTraceLineDebugger -----------------------------------------------

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

      public void OnTraceEvent(TraceEventKind kind, string name, string sourceFileName, Microsoft.Scripting.SourceSpan sourceSpan, Func<Microsoft.Scripting.IAttributesCollection> scopeCallback, object payload, object customPayload)
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
      {
        return base.VisitBlock(Expression.Block(node.Type, node.Variables, ReduceDebugExpressions(node.Expressions)));
      } // func VisitBlock

      protected override Expression VisitLambda<T>(Expression<T> node)
      {
        Expression expr = base.VisitLambda(node);

        if (node.Parameters.Count > 0 && node.Parameters[0].Name == "$frame")
          return expr;
        else
          return context.TransformLambda((Expression<T>)expr, debugLambdaInfo);
      } // func VisitLambda

      public bool DoesExpressionNeedReduction(Expression expression)
      {
        return true;
      } // func DoesExpressionNeedReduction

      public bool IsCallToDebuggableLambda(Expression expression)
      {
        return true;
      } // func IsCallToDebuggableLambda

      public Expression QueueExpressionForReduction(Expression expression)
      {
        return expression;
      } // func QueueExpressionForReduction

      private static IEnumerable<Expression> ReduceDebugExpressions(ReadOnlyCollection<Expression> expressions)
      {
        Expression[] newBlock = new Expression[expressions.Count];
        int iCurrent = 0;
        foreach (Expression expr in expressions)
        {
          DebugInfoExpression exprDebugInfo = expr as DebugInfoExpression;
          if (iCurrent > 0 && exprDebugInfo != null)
          {
            if (exprDebugInfo.StartLine != 16707566)
            {
              if (newBlock[iCurrent - 1] is DebugInfoExpression)
                newBlock[iCurrent - 1] = expr;
              else
                newBlock[iCurrent++] = expr;
            }
          }
          else
            newBlock[iCurrent++] = expr;
        }
        if (newBlock[iCurrent - 1] is DebugInfoExpression)
          iCurrent--;

        return newBlock.Take(iCurrent);
      } // func ReduceDebugExpressions
    } // class TransformAllLambda

    #endregion

    #region -- class LuaTraceChunk ----------------------------------------------------

    private class LuaTraceChunk : LuaChunk
    {
      public LuaTraceChunk(Lua lua, string sName, Delegate chunk)
        : base(lua, sName, chunk)
      {
      } // ctor
    } // class LuaTraceChunk

    #endregion

    private TraceCallback callback;
    private DebugContext context;
    private ITracePipeline pipeline;

    public LuaTraceLineDebugger()
    {
      callback = new TraceCallback(this);
      context = DebugContext.CreateInstance();
      pipeline = TracePipeline.CreateInstance(context);
      pipeline.TraceCallback = callback;
    } // ctor

    #region -- ILuaDebug members ------------------------------------------------------

    LuaChunk ILuaDebug.CreateChunk(Lua lua, LambdaExpression expr)
    {
      var transform = new TransformAllLambda(context);
      expr = (LambdaExpression)transform.Visit(expr);

      return new LuaTraceChunk(lua, expr.Name, expr.Compile());
    } // func CreateCunk

    LuaDebugLevel ILuaDebug.Level { get { return LuaDebugLevel.Line; } }

    #endregion

    protected virtual void OnFrameEnter(LuaTraceLineEventArgs e)
    {
    } // proc OnFrameEnter

    protected virtual void OnTracePoint(LuaTraceLineEventArgs e)
    {
    } // proc OnTracePoint

    protected virtual void OnExceptionUnwind(LuaTraceLineExceptionEventArgs e)
    {
    } // proc OnExceptionUnwind

    protected virtual void OnFrameExit()
    {
    } // proc OnFrameExit
  } // class LuaTraceLineDebugger

  #endregion
}
