﻿#region -- copyright --
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
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.Scripting;
using Microsoft.Scripting.Debugging;
using Microsoft.Scripting.Debugging.CompilerServices;

namespace Neo.IronLua
{
	#region -- class LuaTraceLineEventArgs ----------------------------------------------

	
	/// <summary></summary>
	[DebuggerDisplay("{DebuggerDisplay, nq}")]
	[DebuggerTypeProxy(typeof(LuaTraceLineEventArgsDebuggerProxy))]
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

		private string DebuggerDisplay => $"{SourceName}:{SourceLine} {ScopeName}";

		class LuaTraceLineEventArgsDebuggerProxy
		{
			private readonly LuaTraceLineEventArgs eventArgs;
		    private KeyValuePair<string, object>[] locals;

		    public LuaTraceLineEventArgsDebuggerProxy(LuaTraceLineEventArgs eventArgs)
		    {
		        this.eventArgs = eventArgs;
		        var eventLocals = eventArgs.Locals;
		    }


		    [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
		    public KeyValuePair<string, object>[] Locals => locals ??= GetLocals();

		    private KeyValuePair<string, object>[] GetLocals()
		    {
		        var eventArgsLocals = eventArgs.Locals;
		        KeyValuePair<string, object>[] locals = new KeyValuePair<string, object>[eventArgsLocals.Count];
		        int i = 0;
		        foreach (var kv in eventArgsLocals)
		        {
		            locals[i++] = new KeyValuePair<string, object>(kv.Key.ToString()!, kv.Value);
		        }

		        return locals;
		    }
		}
	} // class LuaTraceLineEventArgs

	#endregion

	#region -- class LuaTraceLineExceptionEventArgs -------------------------------------

	/// <summary></summary>

	[DebuggerDisplay("{DebuggerDisplay,nq}")]
	public class LuaTraceLineExceptionEventArgs : LuaTraceLineEventArgs
	{
		private readonly Exception exception;

		internal LuaTraceLineExceptionEventArgs(string name, string sourceFile, int line, Func<IDictionary<object, object>> scopeCallback, Exception exception)
			: base(name, sourceFile, line, scopeCallback)
		{
			this.exception = exception;
		} // ctor

		/// <summary></summary>
		public Exception Exception => exception;

		private string DebuggerDisplay => $"{SourceName}:{SourceLine} {ScopeName} - {Exception.Message}";
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
			private readonly DebugContext context;
			private readonly DebugLambdaInfo debugLambdaInfo;

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

				return node.Parameters.Count > 0 && node.Parameters[0].Name == "$frame"
					? expr
					: context.TransformLambda((Expression<T>)expr, debugLambdaInfo);
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

				// remove useless debug info expressions
				foreach (var expr in expressions)
				{
					if (current > 0 && expr is DebugInfoExpression exprDebugInfo)
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

		private readonly TraceCallback callback;
		private readonly DebugContext context;
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

		LuaDebugLevel ILuaDebug.Level => LuaDebugLevel.Line;

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
