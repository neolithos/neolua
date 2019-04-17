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
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Neo.IronLua
{
	#region -- enum LuaDebugLevel -----------------------------------------------------

	/// <summary>Descripes the debug-level.</summary>
	[Flags]
	public enum LuaDebugLevel
	{
		/// <summary>No debug info will be emitted.</summary>
		None,
		/// <summary>Before every new line is a DebugInfo emitted (Line exact).</summary>
		Line = 1,
		/// <summary>Every expression is wrap by a DebugInfo (Column exact).</summary>
		Expression = 2,
		/// <summary>Registriert die Methoden</summary>
		RegisterMethods = 4
	} // enum LuaDebugLevel

	#endregion

	#region -- interface ILuaDebug ----------------------------------------------------

	/// <summary></summary>
	public interface ILuaDebug
	{
		/// <summary>Create the chunk</summary>
		/// <param name="expr">Content of the chunk.</param>
		/// <param name="lua"></param>
		/// <returns></returns>
		LuaChunk CreateChunk(Lua lua, LambdaExpression expr);
		/// <summary>How should the parser emit the DebugInfo's</summary>
		LuaDebugLevel Level { get; }
	} // interface ILuaDebug

	#endregion

	#region -- interface ILuaDebugInfo ------------------------------------------------

	/// <summary>Information about a position.</summary>
	public interface ILuaDebugInfo
	{
		/// <summary>Name of the chunk.</summary>
		string ChunkName { get; }
		/// <summary>Source of the position.</summary>
		string FileName { get; }
		/// <summary>Line</summary>
		int Line { get; }
		/// <summary>Column</summary>
		int Column { get; }
	} // interface ILuaDebugInfo

	#endregion
	
	#region -- class LuaExceptionDebugger ---------------------------------------------

	/// <summary>Create lua chunks, that will have stacktrace in case of an exception (only with line numbers).</summary>
	public sealed class LuaExceptionDebugger : ILuaDebug
	{
		#region -- class LuaExceptionLineInfo -----------------------------------------

		private sealed class LuaExceptionLineInfo : ILuaDebugInfo
		{
			public LuaExceptionLineInfo(LuaChunk chunk, string fileName, int lineNumber)
			{
				ChunkName = chunk?.ChunkName;
				FileName = fileName;
				Line = lineNumber;
			} // ctur

			public string ChunkName { get; }
			public string FileName { get; }
			public int Line { get; }
			public int Column => 0;
		} // class LuaExceptionLineInfo

		#endregion

		#region -- class LuaExceptionVisitor ------------------------------------------

		private sealed class LuaExceptionVisitor : ExpressionVisitor
		{
			#region -- class FrameInfo ------------------------------------------------

			private sealed class FrameInfo
			{
				private int lastLineEmitted;

				public FrameInfo()
				{
					LineNumberVariable = Expression.Parameter(typeof(int), "__lineNumber");
					FileNameConstant = Expression.Constant(null, typeof(string));
					lastLineEmitted = -1;
				} // ctor

				public Expression GetLineInfoExpression(DebugInfoExpression exprDebugInfo)
				{
					var oldFileName = FileNameConstant.Value as string;
					var newFileName = exprDebugInfo.Document?.FileName;
					if (oldFileName != newFileName)
						FileNameConstant = Expression.Constant(newFileName, typeof(string));

					if (exprDebugInfo.StartLine == 16707566)
					{
						if (lastLineEmitted != -1)
						{

							lastLineEmitted = -1;
							return Expression.Assign(LineNumberVariable, Expression.Constant(lastLineEmitted));
						}
						else
							return null;
					}
					else
					{
						if (exprDebugInfo.StartLine != lastLineEmitted)
						{
							lastLineEmitted = exprDebugInfo.StartLine;
							return Expression.Assign(LineNumberVariable, Expression.Constant(lastLineEmitted));
						}
						else
							return null;
					}
				} // func GetLineInfoExpression

				public ConstantExpression FileNameConstant { get; private set; }
				public ParameterExpression LineNumberVariable { get; }
			} // class FrameInfo

			#endregion

			private readonly LuaExceptionChunk chunk;
			private readonly Stack<FrameInfo> frames = new Stack<FrameInfo>();

			public LuaExceptionVisitor(LuaExceptionChunk chunk)
			{
				this.chunk = chunk ?? throw new ArgumentNullException(nameof(chunk));
			} // ctor

			private IEnumerable<Expression> LineProgressExpression(IReadOnlyList<Expression> expressions)
			{
				var newExpressions = new Expression[expressions.Count];
				var current = 0;
				var lineInfoEmitted = false;
				foreach (var expr in expressions)
				{
					if (expr is DebugInfoExpression exprDebugInfo)
					{
						var debugLine = frames.Peek().GetLineInfoExpression(exprDebugInfo);
						if (debugLine != null)
						{
							if (lineInfoEmitted)
								newExpressions[current - 1] = debugLine;
							else
								newExpressions[current++] = debugLine;
							lineInfoEmitted = true;
						}
					}
					else
					{
						newExpressions[current++] = expr;
						lineInfoEmitted = false;
					}
				}

				if (lineInfoEmitted)
					current--;

				return newExpressions.Take(current);
			} // func LineProgressExpression

			protected override Expression VisitBlock(BlockExpression node)
				=> base.VisitBlock(Expression.Block(node.Type, node.Variables, LineProgressExpression(node.Expressions)));

			protected override Expression VisitDebugInfo(DebugInfoExpression node)
				=> throw new InvalidOperationException();

			private static Expression ExtendBody(MethodCallExpression exprCall, Expression body)
			{
				return body is BlockExpression blockBody
					? Expression.Block(blockBody.Type, blockBody.Variables, new Expression[] { exprCall }.Concat(blockBody.Expressions))
					: Expression.Block(exprCall, body);
			} // func ExtendBody

			private static CatchBlock GetRethrowCatchBlock(Type nodeType) 
				=> Expression.MakeCatchBlock(typeof(Exception), null, Expression.Rethrow(nodeType), null);

			private Expression EnforceTryCatch(Expression body)
			{
				if (body is TryExpression tryBody)
				{
					if (tryBody.Handlers.FirstOrDefault(c => c.Test == typeof(Exception)) != null) // has correct handler
						return body;
					else // add handler
					{
						return Expression.MakeTry(tryBody.Type, tryBody.Body, tryBody.Finally, tryBody.Fault,
							tryBody.Handlers.Concat(new CatchBlock[] { GetRethrowCatchBlock(tryBody.Type) })
						);
					}
				}
				else
					return Expression.TryCatch(body, GetRethrowCatchBlock(body.Type));
			} // func EnforceTryCatch

			protected override CatchBlock VisitCatchBlock(CatchBlock node)
			{
				var expceptionVariable = node.Variable ?? Expression.Parameter(node.Test, "__e");
				var frame = frames.Peek();
				var exprCall = Expression.Call(Expression.Constant(chunk), LuaExceptionChunk.UnwindExceptionMethodInfo, expceptionVariable, frame.FileNameConstant, frame.LineNumberVariable);
				return base.VisitCatchBlock(
					Expression.MakeCatchBlock(node.Test, 
						expceptionVariable,
						ExtendBody(exprCall, node.Body), 
						node.Filter
					)
				);
			} // func CatchBlock

			protected override Expression VisitLambda<T>(Expression<T> node)
			{
				var currentFrame = new FrameInfo();
				frames.Push(currentFrame);
				try
				{
					var body = node.Body;
					var newBody = Expression.Block(body.Type,
						new ParameterExpression[] { currentFrame.LineNumberVariable },
						EnforceTryCatch(body)
					);
					return base.VisitLambda(Expression.Lambda<T>(newBody, node.Name, node.TailCall, node.Parameters));
				}
				finally
				{
					frames.Pop();
				}
			} // func VisitLambda
		} // class LuaExceptionVisitor

		#endregion

		#region -- class LuaExceptionChunk --------------------------------------------

		private sealed class LuaExceptionChunk : LuaChunk
		{
			public LuaExceptionChunk(Lua lua, LambdaExpression expr) 
				: base(lua, expr.Name, null)
			{
				var lambda = (LambdaExpression)new LuaExceptionVisitor(this).Visit(expr);
				Chunk = lambda.Compile();
			} // ctor

			public void UnwindException(Exception e, string fileName, int lineNumber)
				=> LuaExceptionData.UnwindException(e, () => new LuaExceptionLineInfo(this, fileName, lineNumber));
	
			public static readonly MethodInfo UnwindExceptionMethodInfo;

			static LuaExceptionChunk()
			{
				var typeInfo = typeof(LuaExceptionChunk).GetTypeInfo();
				UnwindExceptionMethodInfo = typeInfo.FindDeclaredMethod(nameof(UnwindException), ReflectionFlag.Public | ReflectionFlag.Instance, typeof(Exception), typeof(string), typeof(int));
			} // sctor
		} // class LuaExceptionChunk

		#endregion

		private LuaExceptionDebugger()
		{
		} // ctor
		
		LuaChunk ILuaDebug.CreateChunk(Lua lua, LambdaExpression expr)
			=> new LuaExceptionChunk(lua, expr);

		LuaDebugLevel ILuaDebug.Level => LuaDebugLevel.Line;

		/// <summary>Instance of the exception debugger.</summary>
		public static ILuaDebug Default { get; } = new LuaExceptionDebugger();
	} // class LuaExceptionDebugger

	#endregion
}
