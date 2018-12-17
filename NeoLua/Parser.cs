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
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace Neo.IronLua
{
	#region -- class Parser -------------------------------------------------------------

	/// <summary>Internal parser code to generate te expression tree.</summary>
	internal static partial class Parser
	{
		private const string csReturnLabel = "#return";
		private const string csBreakLabel = "#break";
		private const string csContinueLabel = "#continue";
		private const string csEnv = "_G";
		private const string csArgListP = "#arglistP";
		private const string csArgList = "#arglist";
		private const string csClr = "clr";

		#region -- class Scope ------------------------------------------------------------

		/// <summary>Scope, that builds a block.</summary>
		private class Scope
		{
			private readonly Scope parent;   // Parent-Scope, that is accessable
			private Dictionary<string, Expression> scopeVariables = null; // local declared variables or const definitions
			private readonly List<Expression> block = new List<Expression>();  // Instructions in the current block
			private bool isBlockGenerated = false; // Is the block generated

			#region -- Ctor -----------------------------------------------------------------

			/// <summary>Creates the scope</summary>
			/// <param name="parent">parent-scope</param>
			public Scope(Scope parent)
			{
				this.parent = parent;
			} // ctor

			#endregion

			#region -- LookupParameter ------------------------------------------------------

			/// <summary>Creates a new variable in the current scope.</summary>
			/// <param name="type">Type of the variable</param>
			/// <param name="sName">Name of the variable</param>
			/// <returns>The expression that represents the access to the variable.</returns>
			public ParameterExpression RegisterVariable(Type type, string sName)
				=> RegisterVariable(Expression.Variable(type, sName));

			/// <summary></summary>
			/// <param name="expr"></param>
			/// <returns></returns>
			public ParameterExpression RegisterVariable(ParameterExpression expr)
			{
				RegisterVariableOrConst(expr.Name, expr);
				return expr;
			} // proc RegisterVariable

			public void RegisterConst(string name, ConstantExpression expr)
				=> RegisterVariableOrConst(name, expr);

			private void RegisterVariableOrConst(string name, Expression expr)
			{
				if (scopeVariables == null)
					scopeVariables = new Dictionary<string, Expression>();
				scopeVariables[name] = expr;
			} // proc EnsureVariables

			/// <summary>Looks up the variable/parameter/const through the scopes.</summary>
			/// <param name="name">Name of the variable</param>
			/// <param name="isLocalOnly"></param>
			/// <returns>The access-expression for the variable, parameter or <c>null</c>, if it is not registered.</returns>
			public virtual Expression LookupExpression(string name, bool isLocalOnly = false)
			{
				// Lookup the current scope
				Expression p;
				if (scopeVariables != null && scopeVariables.TryGetValue(name, out p))
					return p;

				return parent != null && !isLocalOnly ? // lookup the parent scope
					parent.LookupExpression(name) :
					null;
			} // func LookupParameter

			#endregion

			#region -- LookupLabel ----------------------------------------------------------

			/// <summary>Create a named label or look for an existing</summary>
			/// <param name="type">Returntype for the label</param>
			/// <param name="sName">Name for the label</param>
			/// <returns>Labeltarget</returns>
			public virtual LabelTarget LookupLabel(Type type, string sName)
				=> parent.LookupLabel(type, sName);

			#endregion

			#region -- Expression Block -----------------------------------------------------

			[Conditional("DEBUG")]
			private void CheckBlockGenerated()
			{
				if (isBlockGenerated)
					throw new InvalidOperationException();
			} // proc CheckBlockGenerated

			public void InsertExpression(int iIndex, Expression expr)
			{
				CheckBlockGenerated();
				block.Insert(iIndex, expr);
			} // proc AddExpression

			public void AddExpression(Expression expr)
			{
				CheckBlockGenerated();
				block.Add(expr);
			} // proc AddExpression

			/// <summary>Close the expression block and return it.</summary>
			public virtual Expression ExpressionBlock
			{
				get
				{
					CheckBlockGenerated();
					isBlockGenerated = true;

					if (block.Count == 0)
						return Expression.Empty();
					else
					{
						var variables = Variables;
						if (variables.Length == 0)
							return Expression.Block(block);
						else if (ExpressionBlockType == null)
							return Expression.Block(variables, block);
						else
							return Expression.Block(ExpressionBlockType, variables, block);
					}
				}
			} // func ExpressionBlock

			public Type ExpressionBlockType { get; set; }
			public bool ExistExpressions => block.Count > 0;

			#endregion

			/// <summary>Access to the Lua-Binders</summary>
			public virtual Lua Runtime => parent.Runtime;
			/// <summary>Emit-Debug-Information</summary>
			public virtual LuaDebugLevel EmitDebug => parent.EmitDebug;
			/// <summary>Options</summary>
			public virtual LuaCompileOptions Options => parent.Options;
			/// <summary>DebugInfo on expression level</summary>
			public bool EmitExpressionDebug => (EmitDebug & LuaDebugLevel.Expression) != 0;
			/// <summary>Return type of the current Lambda-Scope</summary>
			public virtual Type ReturnType => parent.ReturnType;
			/// <summary></summary>
			public ParameterExpression[] Variables => scopeVariables == null ? new ParameterExpression[0] : (from v in scopeVariables.Values where v is ParameterExpression select (ParameterExpression)v).ToArray();
		} // class Scope

		#endregion

		#region -- class LoopScope --------------------------------------------------------

		/// <summary>Scope that represents the loop content.</summary>
		private sealed class LoopScope : Scope
		{
			private readonly LabelTarget continueLabel = Expression.Label(csContinueLabel);
			private readonly LabelTarget breakLabel = Expression.Label(csBreakLabel);

			#region -- Ctor -----------------------------------------------------------------

			/// <summary>Scope that represents the loop content.</summary>
			/// <param name="parent"></param>
			public LoopScope(Scope parent)
				: base(parent)
			{
			} // ctor

			#endregion

			#region -- LookupLabel ----------------------------------------------------------

			/// <summary>Creates or lookup the label</summary>
			/// <param name="type">Type of the label. Is ignored on std. labels.</param>
			/// <param name="name">Name of the label.</param>
			/// <returns>LabelTarget</returns>
			public override LabelTarget LookupLabel(Type type, string name)
			{
				switch (name)
				{
					case csBreakLabel:
						return breakLabel;
					case csContinueLabel:
						return continueLabel;
					default:
						return base.LookupLabel(type, name);
				}
			} // func LookupLabel

			#endregion

			/// <summary>Default break position.</summary>
			public LabelTarget BreakLabel => breakLabel;
			/// <summary>Default continue position.</summary>
			public LabelTarget ContinueLabel => continueLabel;
		} // class LambdaScope

		#endregion

		#region -- class LambdaScope ------------------------------------------------------

		/// <summary>Lambda-Scope with labels and parameters.</summary>
		private class LambdaScope : Scope
		{
#if DEBUG
			private bool isReturnLabelUsed = false;
#endif
			private LabelTarget returnLabel;
			private Expression returnDefaultValue;
			private Dictionary<string, LabelTarget> labels = null;
			private Dictionary<string, ParameterExpression> parameters = new Dictionary<string, ParameterExpression>();

			#region -- Ctor -----------------------------------------------------------------

			/// <summary>Creates the lambda-scope, that manages labels and arguments.</summary>
			/// <param name="parent"></param>
			/// <param name="returnType"></param>
			/// <param name="returnDefaultValue"></param>
			public LambdaScope(Scope parent, Type returnType = null, Expression returnDefaultValue = null)
				: base(parent)
			{
				if (returnType != null)
					ResetReturnLabel(returnType, returnDefaultValue);
			} // ctor

			#endregion

			#region -- RegisterParameter, LookupParameter -----------------------------------

			/// <summary>Registers arguments for the function.</summary>
			/// <param name="type">Type of the argument</param>
			/// <param name="name">Name of the argument</param>
			/// <returns>Access to the argument</returns>
			public ParameterExpression RegisterParameter(Type type, string name)
				=> parameters[name] = Expression.Parameter(type, name);

			/// <summary>Lookup the variables and arguments.</summary>
			/// <param name="name">Name of the parameter/variable.</param>
			/// <param name="isLocalOnly"></param>
			/// <returns></returns>
			public override Expression LookupExpression(string name, bool isLocalOnly = false)
			{
				if (parameters.TryGetValue(name, out var p))
					return p;
				return base.LookupExpression(name, isLocalOnly);
			} // func LookupParameter

			#endregion

			#region -- LookupLabel ----------------------------------------------------------

			public override LabelTarget LookupLabel(Type type, string name)
			{
				if (name == csReturnLabel)
					return returnLabel;
				if (labels == null)
					labels = new Dictionary<string, LabelTarget>();
				if (type == null)
					type = typeof(void);

				// Lookup the label
				if (labels.TryGetValue(name, out var l))
					return l;

				// Create the label, if it is not internal
				if (name[0] == '#')
					throw new ArgumentException("Internal label does not exist.");

				return labels[name] = Expression.Label(type, name);
			} // func LookupLabel

			#endregion

			public void ResetReturnLabel(Type returnType, Expression returnDefaultValue)
			{
#if DEBUG
				if (isReturnLabelUsed)
					throw new InvalidOperationException("Reset is not allowed after expressions added.");
#endif
				this.returnLabel = Expression.Label(returnType, csReturnLabel);
				this.returnDefaultValue = returnDefaultValue;
			} // proc ResetReturnLabel

			public override Expression ExpressionBlock
			{
				get
				{
					AddExpression(Expression.Label(returnLabel, returnDefaultValue == null ? Expression.Default(returnLabel.Type) : returnDefaultValue));
					return base.ExpressionBlock;
				}
			} // prop ExpressionBlock

			public LabelTarget ReturnLabel
			{
				get
				{
#if DEBUG
					isReturnLabelUsed = true;
#endif
					return returnLabel;
				}
			} // prop ReturnLabel

			public override Type ReturnType => returnLabel.Type;
		} // class LambdaScope

		#endregion

		#region -- class GlobalScope ------------------------------------------------------

		/// <summary>Global parse-scope.</summary>
		private sealed class GlobalScope : LambdaScope
		{
			private readonly Lua runtime;
			private readonly LuaCompileOptions options;
			private readonly LuaDebugLevel debug;

			/// <summary>Global parse-scope</summary>
			/// <param name="runtime">Runtime and binder of the global scope.</param>
			/// <param name="options"></param>
			/// <param name="returnType"></param>
			/// <param name="returnDefaultValue"></param>
			public GlobalScope(Lua runtime, LuaCompileOptions options, Type returnType, Expression returnDefaultValue)
				: base(null, returnType, returnDefaultValue)
			{
				this.runtime = runtime;
				this.options = options;
				this.debug = options.DebugEngine == null ? LuaDebugLevel.None : options.DebugEngine.Level;
			} // ctor

			/// <summary>Access to the binders</summary>
			public override Lua Runtime => runtime;
			/// <summary>Emit-Debug-Information</summary>
			public override LuaDebugLevel EmitDebug => debug;
			/// <summary>Options</summary>
			public override LuaCompileOptions Options => options;
		} // class GlobalScope

		#endregion

		#region -- class CatchScope -------------------------------------------------------

		/// <summary></summary>
		private sealed class CatchScope : Scope
		{
			private readonly ParameterExpression exceptionVariable;

			public CatchScope(Scope parent, ParameterExpression exceptionVariable)
				: base(parent)
			{
				this.exceptionVariable = exceptionVariable;
			} // ctor

			public override Expression LookupExpression(string name, bool isLocalOnly = false)
			{
				if (name == exceptionVariable.Name)
					return exceptionVariable;
				return base.LookupExpression(name, isLocalOnly);
			} // func LookupExpression

			public ParameterExpression ExceptionVariable => exceptionVariable;
		} // class CatchScope

		#endregion

		#region -- enum InvokeResult ------------------------------------------------------

		public enum InvokeResult
		{
			None,
			Object,
			LuaResult
		} // enum GenerateResult

		#endregion

		#region -- class ArgumentsList ----------------------------------------------------

		/// <summary>Class to hold arguments.</summary>
		private sealed class ArgumentsList
		{
			private readonly List<Expression> arguments = new List<Expression>();
			private readonly List<string> names = new List<string>();

			private Lazy<CallInfo> callInfo;

			public ArgumentsList(params Expression[] expr)
			{
				arguments.AddRange(expr);

				callInfo = new Lazy<CallInfo>(() => new CallInfo(arguments.Count, names));
			} // ctor

			public void AddPositionalArgument(Token position, Expression expr)
			{
				if (callInfo.IsValueCreated)
					throw new InvalidOperationException("internal: no manipulation after CallInfo creation.");

				if (names.Count > 0)
					throw ParseError(position, Properties.Resources.rsParseInvalidArgList);

				arguments.Add(expr);
			} // proc AddArgument

			public void AddNamedArgument(Token name, Expression expr)
			{
				if (callInfo.IsValueCreated)
					throw new InvalidOperationException("internal: no manipulation after CallInfo creation.");

				names.Add(name.Value);
				arguments.Add(expr);
			} // proc AddArgument

			public void WrapArgument(int index, bool emitExpressionDebug, Token position)
			{
				arguments[index] = WrapDebugInfo(emitExpressionDebug, true, position, position, arguments[index]);
			} // proc WrapArgument

			public Expression[] Expressions
				=> arguments.ToArray();

			public CallInfo CallInfo
				=> callInfo.Value;

			public int Count
				=> arguments.Count;
		} // class ArgumentsList

		#endregion

		#region -- class PrefixMemberInfo -------------------------------------------------

		/// <summary>Mini-Parse-Tree for resolve of prefix expressions</summary>
		private class PrefixMemberInfo
		{
			public PrefixMemberInfo(Token position, Expression instance, string sMember, Expression[] indices, ArgumentsList arguments)
			{
				this.Position = position;
				this.Instance = instance;
				this.Member = sMember;
				this.Indices = indices;
				this.Arguments = arguments;
			} // ctor

			public Expression GenerateSet(Scope scope, Expression exprToSet)
			{
				Expression expr;
				if (Instance != null && Member == null && Indices != null && Arguments == null)
					expr = IndexSetExpression(scope.Runtime, Position, Instance, Indices, exprToSet);
				else if (Instance != null && Member != null && Indices == null && Arguments == null)
					return MemberSetExpression(scope.Runtime, Position, Instance, Member, MethodMember, exprToSet);
				else if (Instance != null && Member == null && Indices == null && Arguments == null && Instance is ParameterExpression)
				{
					// Assign the value to a variable
					expr = Expression.Assign(Instance, ConvertExpression(scope.Runtime, Position, exprToSet, Instance.Type));
				}
				else
					throw ParseError(Position, Properties.Resources.rsParseExpressionNotAssignable);

				return expr;
			} // func GenerateSet

			public Expression GenerateGet(Scope scope, InvokeResult result)
			{
				if (Instance != null && Member == null && Indices != null && Arguments == null)
				{
					if (Indices.Length > 0)
					{
						// First the arguments are pushed on the stack, and later comes the call, so we wrap the last parameter
						Indices[Indices.Length - 1] = WrapDebugInfo(scope.EmitExpressionDebug, true, Position, Position, Indices[Indices.Length - 1]); // Let the type as it is
					}

					Instance = IndexGetExpression(scope, Position, Instance, Indices);
					Indices = null;
				}
				else if (Instance != null && Member != null && Indices == null && Arguments == null && !MethodMember)
				{
					// Convert the member to an instance
					Instance = WrapDebugInfo(scope.EmitExpressionDebug, true, Position, Position, Instance);
					Instance = MemberGetExpression(scope, Position, Instance, Member);
					Member = null;
					MethodMember = false;
				}
				else if (Instance != null && Member == null && Indices == null && Arguments == null)
				{
					// Nothing to todo, we have already an instance
				}
				else if (Instance != null && Indices == null && (Arguments != null || MethodMember))
				{
					Arguments = Arguments ?? new ArgumentsList();
					if (Arguments.Count > 0)
					{
						// First the arguments are pushed on the stack, and later comes the call, so we wrap the last parameter
						Arguments.WrapArgument(Arguments.Count - 1, scope.EmitExpressionDebug, Position); // Let the type as it is
					}
					else
						Instance = WrapDebugInfo(scope.EmitExpressionDebug, true, Position, Position, Instance);

					if (String.IsNullOrEmpty(Member))
						Instance = InvokeExpression(scope, Position, Instance, result, Arguments, true);
					else
						Instance = InvokeMemberExpression(scope, Position, Instance, Member, result, Arguments);

					Member = null;
					MethodMember = false;
					Arguments = null;
				}
				else
					throw ParseError(Position, Properties.Resources.rsParseExpressionNoResult);

				return Instance;
			} // func GenerateGet

			public void SetMember(Token tMember, bool lMethod)
			{
				Position = tMember;
				MethodMember = lMethod;
				Member = tMember.Value;
			} // proc SetMember

			public Token Position { get; set; }
			public Expression Instance { get; set; }
			public string Member { get; private set; }
			public bool MethodMember { get; private set; }
			public Expression[] Indices { get; set; }
			public ArgumentsList Arguments { get; set; }
		} // class PrefixMemberInfo

		#endregion

		#region -- Parse Chunk, Block -----------------------------------------------------

		/// <summary>Parses the chunk to an function.</summary>
		/// <param name="runtime">Binder</param>
		/// <param name="options">Compile options for the script.</param>
		/// <param name="hasEnvironment">Creates the _G parameter.</param>
		/// <param name="code">Lexer for the code.</param>
		/// <param name="typeDelegate">Type for the delegate. <c>null</c>, for an automatic type</param>
		/// <param name="returnType">Defines the return type of the chunk.</param>
		/// <param name="args">Arguments of the function.</param>
		/// <returns>Expression-Tree for the code.</returns>
		public static LambdaExpression ParseChunk(Lua runtime, LuaCompileOptions options, bool hasEnvironment, LuaLexer code, Type typeDelegate, Type returnType, IEnumerable<KeyValuePair<string, Type>> args)
		{
			var parameters = new List<ParameterExpression>();
			if (returnType == null)
				returnType = typeof(LuaResult);
			var globalScope = new GlobalScope(runtime, options, returnType, returnType == typeof(LuaResult) ? Expression.Property(null, Lua.ResultEmptyPropertyInfo) : null);

			// Registers the global LuaTable
			if (hasEnvironment)
				parameters.Add(globalScope.RegisterParameter(typeof(LuaTable), csEnv));

			if (args != null)
			{
				foreach (var c in args)
				{
					if (c.Key == "..." && c.Value.IsArray)
						ParseLamdaDefinitionArgList(globalScope, parameters);
					else
						parameters.Add(globalScope.RegisterParameter(c.Value, c.Key)); // Add alle arguments
				}
			}

			// Get the first token
			if (code.Current == null)
				code.Next();

			// Get the name for the chunk and clean it from all unwanted chars
			var chunkName = CreateNameFromFile(code.Current.Start.FileName);
			if ((globalScope.EmitDebug & LuaDebugLevel.RegisterMethods) == LuaDebugLevel.RegisterMethods)
				chunkName = Lua.RegisterUniqueName(chunkName);

			// Create the block
			ParseBlock(globalScope, code);

			if (code.Current.Typ != LuaToken.Eof)
				throw ParseError(code.Current, Properties.Resources.rsParseEof);

			// Create the function
			return typeDelegate == null ?
				Expression.Lambda(globalScope.ExpressionBlock, chunkName, parameters) :
				Expression.Lambda(typeDelegate, globalScope.ExpressionBlock, chunkName, parameters);
		} // func ParseChunk

		private static string CreateNameFromFile(string fileName)
		{
			var name = new StringBuilder(Path.GetFileNameWithoutExtension(fileName));
			var length = name.Length;
			for (var i = 0; i < length; i++)
			{
				switch (name[i])
				{
					case '.':
					case ';':
					case ',':
					case '+':
					case ':':
						name[i] = '_';
						break;
				}
			}
			return name.ToString();
		} // func CreateNameFromFile

		private static void ParseBlock(Scope scope, LuaLexer code, bool activateRethrow = false)
		{
			// Lese die Statement
			var lastDebugInfo = -1;
			var inLoop = true;
			while (inLoop)
			{
				var debugInfoEmitted = false;

				if ((scope.EmitDebug & LuaDebugLevel.Line) != 0) // debug info for line
				{
					if (code.Current.Start.Line != lastDebugInfo)
					{
						lastDebugInfo = code.Current.Start.Line;
						scope.AddExpression(GetDebugInfo(code.Current, code.Current));
						debugInfoEmitted = true;
					}
				}

				switch (code.Current.Typ)
				{
					case LuaToken.Eof: // End of file
						inLoop = false;
						break;

					case LuaToken.KwReturn: //  The return-statement is only allowed on the end of a scope
						ParseReturn(scope, code);
						break;

					case LuaToken.Semicolon: // End of statement => ignore
						code.Next();
						break;

					default:
						if (!debugInfoEmitted && (scope.EmitDebug & LuaDebugLevel.Expression) != 0) // Start every statement with a debug point
							scope.AddExpression(GetDebugInfo(code.Current, code.Current));

						if (activateRethrow && code.Current.Typ == LuaToken.Identifier && code.Current.Value == "rethrow")
						{
							code.Next();
							scope.AddExpression(Expression.Rethrow());
						}
						else if (!ParseStatement(scope, code)) // Parse normal statements
							inLoop = false;
						break;
				}
			}
			if (scope.EmitDebug != LuaDebugLevel.None)
				scope.AddExpression(Expression.ClearDebugInfo(code.Current.Start.Document)); // Clear debug info
		} // func ParseBlock

		private static void ParseReturn(Scope scope, LuaLexer code)
		{
			// eat return
			code.Next();

			// Build the return expression for all parameters
			Expression exprReturnValue;

			if (IsExpressionStart(code)) // there is a return value
			{
				if (scope.ReturnType == typeof(LuaResult))
				{
					exprReturnValue = GetLuaResultExpression(scope, code.Current, ParseExpressionList(scope, code).ToArray());
				}
				else if (scope.ReturnType.IsArray)
				{
					var typeArray = scope.ReturnType.GetElementType();
					exprReturnValue = Expression.NewArrayInit(
						typeArray,
						from c in ParseExpressionList(scope, code) select ConvertExpression(scope.Runtime, code.Current, c, typeArray));
				}
				else
				{
					var exprList = new List<Expression>(ParseExpressionList(scope, code));

					if (exprList.Count == 1)
						exprReturnValue = ConvertExpression(scope.Runtime, code.Current, exprList[0], scope.ReturnType);
					else
					{
						var tmpVar = Expression.Variable(scope.ReturnType);
						exprList[0] = Expression.Assign(tmpVar, ConvertExpression(scope.Runtime, code.Current, exprList[0], scope.ReturnType));
						exprList.Add(tmpVar);
						exprReturnValue = Expression.Block(scope.ReturnType, new ParameterExpression[] { tmpVar }, exprList);
					}
				}
			}
			else // use the default-value
			{
				if (scope.ReturnType == typeof(LuaResult))
					exprReturnValue = Expression.Property(null, Lua.ResultEmptyPropertyInfo);
				else if (scope.ReturnType.IsArray)
					exprReturnValue = Expression.NewArrayInit(scope.ReturnType.GetElementType());
				else
					exprReturnValue = Expression.Default(scope.ReturnType);
			}

			if (code.Current.Typ == LuaToken.Semicolon)
				code.Next();

			scope.AddExpression(Expression.Goto(scope.LookupLabel(scope.ReturnType, csReturnLabel), exprReturnValue));
		} // func ParseReturn

		private static Expression GetLuaResultExpression(Scope scope, Token tStart, Expression[] exprs)
		{
			Expression exprReturnValue;
			if (exprs.Length == 1)
			{
				exprReturnValue = exprs[0].Type == typeof(LuaResult)
					  ? exprs[0]
					  : Expression.New(Lua.ResultConstructorInfoArg1, ConvertExpression(scope.Runtime, tStart, exprs[0], typeof(object)));
			}
			else
			{
				exprReturnValue = Expression.New(Lua.ResultConstructorInfoArgN,
					Expression.NewArrayInit(typeof(object),
						from c in exprs select Expression.Convert(c, typeof(object))
					)
				);
			}
			return exprReturnValue;
		} // func GetLuaResultExpression

		private static bool IsExpressionStart(LuaLexer code)
		{
			return code.Current.Typ == LuaToken.BracketOpen ||
				code.Current.Typ == LuaToken.Identifier ||
				code.Current.Typ == LuaToken.DotDotDot ||
				code.Current.Typ == LuaToken.String ||
				code.Current.Typ == LuaToken.Number ||
				code.Current.Typ == LuaToken.KwTrue ||
				code.Current.Typ == LuaToken.KwFalse ||
				code.Current.Typ == LuaToken.KwNil ||
				code.Current.Typ == LuaToken.BracketCurlyOpen ||
				code.Current.Typ == LuaToken.Minus ||
				code.Current.Typ == LuaToken.Dilde ||
				code.Current.Typ == LuaToken.Cross ||
				code.Current.Typ == LuaToken.KwNot ||
				code.Current.Typ == LuaToken.KwFunction ||
				code.Current.Typ == LuaToken.KwCast;
		} // func IsExpressionStart

		#endregion

		#region -- Parse Statement --------------------------------------------------------

		private static bool ParseStatement(Scope scope, LuaLexer code)
		{
			switch (code.Current.Typ)
			{
				case LuaToken.Identifier: // Expression
				case LuaToken.DotDotDot:
				case LuaToken.BracketOpen:
				case LuaToken.String:
				case LuaToken.Number:
				case LuaToken.KwFalse:
				case LuaToken.KwTrue:
				case LuaToken.KwNil:
				case LuaToken.BracketCurlyOpen:
				case LuaToken.Minus:
				case LuaToken.KwCast:
					ParseExpressionStatement(scope, code, false);
					return true;

				case LuaToken.ColonColon: // Start of a label
					ParseLabel(scope, code);
					return true;

				case LuaToken.KwBreak:
					ParseBreak(scope, code);
					return true;

				case LuaToken.KwGoto:
					ParseGoto(scope, code);
					return true;

				case LuaToken.KwDo:
					ParseDoLoop(scope, code);
					return true;

				case LuaToken.KwWhile:
					ParseWhileLoop(scope, code);
					return true;

				case LuaToken.KwRepeat:
					ParseRepeatLoop(scope, code);
					return true;

				case LuaToken.KwIf:
					ParseIfStatement(scope, code);
					return true;

				case LuaToken.KwFor:
					ParseForLoop(scope, code);
					return true;

				case LuaToken.KwForEach:
					ParseForEachLoop(scope, code);
					return true;

				case LuaToken.KwFunction:
					ParseFunction(scope, code, false);
					return true;

				case LuaToken.KwLocal:
					code.Next();
					if (code.Current.Typ == LuaToken.KwFunction)
						ParseFunction(scope, code, true);
					else
						ParseExpressionStatement(scope, code, true);
					return true;
				case LuaToken.KwConst:
					code.Next();
					ParseConst(scope, code);
					return true;

				case LuaToken.InvalidString:
					throw ParseError(code.Current, Properties.Resources.rsParseInvalidString);
				case LuaToken.InvalidComment:
					throw ParseError(code.Current, Properties.Resources.rsParseInvalidComment);
				case LuaToken.InvalidChar:
					throw ParseError(code.Current, Properties.Resources.rsParseInvalidChar);

				default:
					return false;
			}
		}  // func ParseStatement

		private static void ParseExpressionStatement(Scope scope, LuaLexer code, bool isLocal)
		{
			var registerLocals = (List<ParameterExpression>)null;
			var prefixes = new List<PrefixMemberInfo>();

			// parse the assgiee list (var0, var1, var2, ...)
			while (true)
			{
				if (isLocal) // parse local variables
				{
					ParseIdentifierAndType(scope, code, out var tVar, out var typeVar);

					var exprVar = scope.LookupExpression(tVar.Value, true) as ParameterExpression;
					if (exprVar == null)
					{
						exprVar = Expression.Variable(typeVar, tVar.Value);
						if (registerLocals == null)
							registerLocals = new List<ParameterExpression>();
						registerLocals.Add(exprVar);
					}
					else if (exprVar.Type != typeVar)
						throw ParseError(tVar, Properties.Resources.rsParseTypeRedef);

					prefixes.Add(new PrefixMemberInfo(tVar, exprVar, null, null, null));
				}
				else // parse a assignee
				{
					// parse as a prefix
					prefixes.Add(ParsePrefix(scope, code));
				}

				// is there another prefix
				if (code.Current.Typ == LuaToken.Comma)
					code.Next();
				else
					break;
			}

			// Optional assign
			if (code.Current.Typ == LuaToken.Assign)
			{
				code.Next();

				// parse all expressions
				var expr = ParseExpressionList(scope, code).GetEnumerator();
				expr.MoveNext();

				if (prefixes.Count == 1) // one expression, one variable?
				{
					scope.AddExpression(
						prefixes[0].GenerateSet(scope, expr.Current != null ? expr.Current : Expression.Constant(null, typeof(object)))
					);
				}
				else if (expr.Current == null) // No expression, assign null
				{
					for (var i = 0; i < prefixes.Count; i++)
						scope.AddExpression(prefixes[i].GenerateSet(scope, Expression.Constant(null, typeof(object))));
				}
				else // assign on an unknown number of expressions
				{
					#region -- unknown number --
					var assignTempVars = new List<ParameterExpression>();
					var assignExprs = new List<Expression>();
					int expressionVarOffset;

					// Safe the prefixes in variables
					for (var k = 0; k < prefixes.Count; k++)
					{
						var p = prefixes[k];
						if (p.Member != null || prefixes[k].Indices != null)
						{
							p.Instance = ParseExpressionStatementExchangeToTempVar(assignTempVars, assignExprs, p.Instance);

							if (p.Indices != null)
							{
								for (var l = 0; l < p.Indices.Length; l++)
									p.Indices[l] = ParseExpressionStatementExchangeToTempVar(assignTempVars, assignExprs, p.Indices[l]);
							}
						}
					}

					// collect the results of the expressions
					expressionVarOffset = assignTempVars.Count;
					do
					{
						ParseExpressionStatementExchangeToTempVar(assignTempVars, assignExprs, expr.Current);
					} while (expr.MoveNext());

					// Assign the Result to the prefixes
					var i = 0;
					var j = 0;
					var lastVariable = (ParameterExpression)null;
					while (i < prefixes.Count)
					{
						if (i < assignTempVars.Count - expressionVarOffset) // are the variables
						{
							if (i == assignTempVars.Count - expressionVarOffset - 1 && assignTempVars[i + expressionVarOffset].Type == typeof(LuaResult)) // check if the last expression is a LuaResult
							{
								lastVariable = assignTempVars[i + expressionVarOffset];
								assignExprs.Add(prefixes[i].GenerateSet(scope, GetResultExpression(scope.Runtime, code.Current, lastVariable, j++)));
							}
							else
							{
								assignExprs.Add(prefixes[i].GenerateSet(scope, assignTempVars[i + expressionVarOffset]));
							}
						}
						else if (lastVariable != null) // we enroll the last expression
						{
							assignExprs.Add(prefixes[i].GenerateSet(scope, GetResultExpression(scope.Runtime, code.Current, lastVariable, j++)));
						}
						else // no variable left
						{
							assignExprs.Add(prefixes[i].GenerateSet(scope, Expression.Default(typeof(object))));
						}
						i++;
					}

					// add the block
					scope.AddExpression(Expression.Block(assignTempVars, assignExprs));

					#endregion
				}

				// Führe die restlichen Expressions aus
				while (expr.MoveNext())
					scope.AddExpression(expr.Current);
			}
			else if (!isLocal)
			{
				for (var i = 0; i < prefixes.Count; i++)
				{
					if (prefixes[i].Arguments == null) // do not execute getMember
						throw ParseError(prefixes[i].Position, Properties.Resources.rsParseAssignmentExpected);

					scope.AddExpression(prefixes[i].GenerateGet(scope, InvokeResult.None));
				}
			}

			// register the variables
			if (registerLocals != null)
			{
				for (var i = 0; i < registerLocals.Count; i++)
					scope.RegisterVariable(registerLocals[i]);
			}
		} // proc ParseExpressionStatement

		private static ParameterExpression ParseExpressionStatementExchangeToTempVar(List<ParameterExpression> assignTempVars, List<Expression> assignExprs, Expression expr)
		{
			var tmpVar = Expression.Variable(expr.Type);
			assignTempVars.Add(tmpVar);
			assignExprs.Add(Expression.Assign(tmpVar, expr));
			return tmpVar;
		} // func ParseExpressionStatementExchangeToTempVar

		private static void ParseIfStatement(Scope scope, LuaLexer code)
		{
			// if expr then block { elseif expr then block } [ else block ] end
			FetchToken(LuaToken.KwIf, code);
			var expr = ConvertExpression(scope.Runtime, code.Current, ParseExpression(scope, code, InvokeResult.Object, scope.EmitExpressionDebug), typeof(bool));
			FetchToken(LuaToken.KwThen, code);

			scope.AddExpression(Expression.IfThenElse(expr, ParseIfElseBlock(scope, code), ParseElseStatement(scope, code)));
		} // proc ParseIfStatement

		private static Expression ParseElseStatement(Scope scope, LuaLexer code)
		{
			if (code.Current.Typ == LuaToken.KwElseif)
			{
				code.Next();
				var expr = ConvertExpression(scope.Runtime, code.Current, ParseExpression(scope, code, InvokeResult.Object, scope.EmitExpressionDebug), typeof(bool));
				FetchToken(LuaToken.KwThen, code);

				return Expression.IfThenElse(expr, ParseIfElseBlock(scope, code), ParseElseStatement(scope, code));
			}
			else if (code.Current.Typ == LuaToken.KwElse)
			{
				code.Next();
				var block = ParseIfElseBlock(scope, code);
				FetchToken(LuaToken.KwEnd, code);
				return block;
			}
			else if (code.Current.Typ == LuaToken.KwEnd)
			{
				code.Next();
				return Expression.Empty();
			}
			else
				throw ParseError(code.Current, Properties.Resources.rsParseUnexpectedTokenElse);
		} // func ParseElseStatement

		private static Expression ParseIfElseBlock(Scope parent, LuaLexer code)
		{
			var scope = new Scope(parent);
			ParseBlock(scope, code);
			return scope.ExpressionBlock;
		} // func ParseIfElseBlock

		private static void ParseConst(Scope scope, LuaLexer code)
		{
			// const ::= variable '=' ( expr | clr '.' Type )
			ParseIdentifierAndType(scope, code, out var tVarName, out var typeVar);

			if (code.Current.Typ == LuaToken.Identifier || code.Current.Value == "typeof")
			{
				code.Next();

				// Parse the type
				scope.RegisterConst(tVarName.Value, Expression.Constant(ParseType(scope, code, false)));
			}
			else
			{
				FetchToken(LuaToken.Assign, code);

				var exprConst = ParseExpression(scope, code, InvokeResult.Object, false); // No Debug-Emits
				if (typeVar != typeof(object))
					exprConst = ConvertExpression(scope.Runtime, tVarName, exprConst, typeVar);

				// Try to eval the statement
				if (exprConst.Type == typeof(object) || exprConst.Type == typeof(LuaResult)) // dynamic calls, no constant possible
					throw ParseError(tVarName, Properties.Resources.rsConstExpressionNeeded);
				else
					try
					{
						var r = EvaluateExpression(exprConst);
						if (r == null) // Eval via compile
						{
							Type typeFunc = Expression.GetFuncType(exprConst.Type);
							LambdaExpression exprEval = Expression.Lambda(typeFunc, exprConst);
							Delegate dlg = exprEval.Compile();
							r = dlg.DynamicInvoke();
						}
						scope.RegisterConst(tVarName.Value, Expression.Constant(r, exprConst.Type));
					}
					catch (Exception e)
					{
						throw ParseError(tVarName, String.Format(Properties.Resources.rsConstExpressionEvalError, e.Message));
					}
			}
		} // func ParseConst

		#endregion

		#region -- Evaluate Expression ----------------------------------------------------

		private static object EvaluateExpression(Expression expr)
			=> expr is ConstantExpression cexpr
				? EvaluateConstantExpression(cexpr)
				: null;

		private static object EvaluateConstantExpression(ConstantExpression expr)
			=> Lua.RtConvertValue(expr.Value, expr.Type);

		#endregion

		#region -- Parse Prefix, Suffix ---------------------------------------------------

		private static PrefixMemberInfo ParsePrefix(Scope scope, LuaLexer code)
		{
			// prefix ::= Identifier suffix_opt |  '(' exp ')' suffix | literal | tablector

			var tStart = code.Current;
			PrefixMemberInfo info;
			switch (tStart.Typ)
			{
				case LuaToken.BracketOpen: // Parse eine Expression
					{
						code.Next();
						var expr = ConvertObjectExpression(scope.Runtime, tStart, ParseExpression(scope, code, InvokeResult.Object, scope.EmitExpressionDebug));
						FetchToken(LuaToken.BracketClose, code);

						info = new PrefixMemberInfo(tStart, expr, null, null, null);
					}
					break;

				case LuaToken.DotDotDot:
				case LuaToken.Identifier:
				case LuaToken.KwForEach:
					var t = code.Current;
					if (t.Value == csClr) // clr is a special package, that always exists
					{
						code.Next();
						info = new PrefixMemberInfo(tStart, Expression.Property(null, Lua.TypeClrPropertyInfo), null, null, null);
					}
					else
					{
						string memberName;
						if (t.Typ == LuaToken.DotDotDot)
							memberName = csArgList;
						else if (t.Typ == LuaToken.KwCast)
							memberName = "cast";
						else if (t.Typ == LuaToken.KwForEach)
							memberName = "foreach";
						else
							memberName = t.Value;
						var p = scope.LookupExpression(memberName);
						if (t.Typ == LuaToken.DotDotDot && p == null)
							throw ParseError(t, Properties.Resources.rsParseNoArgList);
						code.Next();
						if (p == null) // No local variable found
							info = new PrefixMemberInfo(tStart, scope.LookupExpression(csEnv), t.Value, null, null);
						else
							info = new PrefixMemberInfo(tStart, p, null, null, null);
					}
					break;

				case LuaToken.KwCast:
					info = new PrefixMemberInfo(tStart, ParsePrefixCast(scope, code), null, null, null);
					break;

				case LuaToken.String: // Literal String
					info = new PrefixMemberInfo(tStart, Expression.Constant(FetchToken(LuaToken.String, code).Value, typeof(string)), null, null, null);
					break;

				case LuaToken.Number: // Literal Zahl
					info = new PrefixMemberInfo(tStart, ParseNumber(scope.Runtime, FetchToken(LuaToken.Number, code)), null, null, null);
					break;

				case LuaToken.KwTrue: // Literal TRUE
					code.Next();
					info = new PrefixMemberInfo(tStart, Expression.Constant(true, typeof(bool)), null, null, null);
					break;

				case LuaToken.KwFalse: // Literal FALSE
					code.Next();
					info = new PrefixMemberInfo(tStart, Expression.Constant(false, typeof(bool)), null, null, null);
					break;

				case LuaToken.KwNil: // Literal NIL
					code.Next();
					info = new PrefixMemberInfo(tStart, Expression.Constant(null, typeof(object)), null, null, null);
					break;

				case LuaToken.BracketCurlyOpen: // tablector
					info = new PrefixMemberInfo(tStart, ParseTableConstructor(scope, code), null, null, null);
					break;

				case LuaToken.KwFunction: // Function definition
					code.Next();
					info = new PrefixMemberInfo(tStart, ParseLamdaDefinition(scope, code, "lambda", false, null), null, null, null);
					break;

				default:
					throw ParseError(code.Current, Properties.Resources.rsParseUnexpectedTokenPrefix);
			}

			return ParseSuffix(scope, code, info);
		} // func ParsePrefix

		private static PrefixMemberInfo ParseSuffix(Scope scope, LuaLexer code, PrefixMemberInfo info)
		{
			// suffix_opt ::= [ suffix ]
			// suffix ::= { '[' exp ']'  | '.' Identifier | args | ':' Identifier args }
			// args ::= tablector | string | '(' explist ')'

			while (true)
			{
				switch (code.Current.Typ)
				{
					case LuaToken.BracketSquareOpen: // Index
						code.Next();
						info.GenerateGet(scope, InvokeResult.Object);
						if (code.Current.Typ == LuaToken.BracketSquareClose)
							info.Indices = new Expression[0];
						else
							info.Indices = ParseExpressionList(scope, code).ToArray();
						FetchToken(LuaToken.BracketSquareClose, code);
						break;

					case LuaToken.Dot: // Property of an class
						code.Next();
						info.GenerateGet(scope, InvokeResult.Object);
						info.SetMember(FetchToken(LuaToken.Identifier, code), false);
						break;

					case LuaToken.BracketOpen: // List of arguments
						info.GenerateGet(scope, InvokeResult.Object);
						info.Arguments = ParseArgumentList(scope, code);
						break;

					case LuaToken.BracketCurlyOpen: // LuaTable as an argument
						info.GenerateGet(scope, InvokeResult.Object);
						info.Arguments = new ArgumentsList(ParseTableConstructor(scope, code));
						break;

					case LuaToken.String: // String as an argument
						info.GenerateGet(scope, InvokeResult.Object);
						info.Arguments = new ArgumentsList(Expression.Constant(FetchToken(LuaToken.String, code).Value, typeof(object)));
						break;

					case LuaToken.Colon: // Methodenaufruf
						code.Next();

						// Lese den Namen um den Member zu belegen
						info.GenerateGet(scope, InvokeResult.Object);
						info.SetMember(FetchToken(LuaToken.Identifier, code), true);

						// Parse die Parameter
						switch (code.Current.Typ)
						{
							case LuaToken.BracketOpen: // Argumentenliste
								info.Arguments = ParseArgumentList(scope, code);
								break;

							case LuaToken.BracketCurlyOpen: // LuaTable als Argument
								info.Arguments = new ArgumentsList(ParseTableConstructor(scope, code));
								break;

							case LuaToken.String: // String als Argument
								info.Arguments = new ArgumentsList(Expression.Constant(FetchToken(LuaToken.String, code).Value, typeof(string)));
								break;
						}
						break;

					default:
						return info;
				}
			}
		} // func ParsePrefix

		private static ArgumentsList ParseArgumentList(Scope scope, LuaLexer code)
		{
			FetchToken(LuaToken.BracketOpen, code);

			// exprArgumentList := '(' [ exprArg { , exprArg } ] ')'
			var argumentsList = new ArgumentsList();
			while (code.Current.Typ != LuaToken.BracketClose)
			{
				Token tName = null;
				if (code.LookAhead.Typ == LuaToken.Assign) // named argument
				{
					tName = FetchToken(LuaToken.Identifier, code);
					code.Next(); // equal
				}

				// parse the expression
				var tFirst = code.Current;
				var expr = ParseExpression(scope, code, InvokeResult.LuaResult, scope.EmitExpressionDebug);

				if (tName == null)
					argumentsList.AddPositionalArgument(tFirst, expr);
				else
					argumentsList.AddNamedArgument(tName, expr);

				// optinal comma
				FetchToken(LuaToken.Comma, code, true);
			}
			code.Next();
			return argumentsList;
		} // func ParseArgumentList

		#endregion

		#region -- Parse Numer, HexNumber -------------------------------------------------

		private static Expression ParseNumber(Lua runtime, Token t)
		{
			var number = t.Value;
			if (String.IsNullOrEmpty(number))
				return Expression.Constant(0, Lua.GetIntegerType(runtime.NumberType));
			else
			{
				var v = Lua.RtParseNumber(number, runtime.FloatType == LuaFloatType.Double, false);
				if (v != null)
					return Expression.Constant(v);
				else
					throw ParseError(t, String.Format(Properties.Resources.rsParseConvertNumberError, number));
			}
		} // func ParseNumber

		#endregion

		#region -- Parse Expressions ------------------------------------------------------

		private static IEnumerable<Expression> ParseExpressionList(Scope scope, LuaLexer code)
		{
			while (true)
			{
				yield return ParseExpression(scope, code, InvokeResult.LuaResult, scope.EmitExpressionDebug);

				// Noch eine Expression
				if (code.Current.Typ == LuaToken.Comma)
					code.Next();
				else
					break;
			}
		} // func ParseExpressionList

		private static Expression ParseExpression(Scope scope, LuaLexer code, InvokeResult result, bool isDebug)
		{
			var tStart = code.Current;
			var doWrap = false;
			var expr = ParseExpression(scope, code, result, ref doWrap);
			if (doWrap && isDebug)
				return WrapDebugInfo(true, false, tStart, code.Current, expr);
			else
				return expr;
		} // func ParseExpression

		private static Expression ParseExpression(Scope scope, LuaLexer code, InvokeResult result, ref bool doWrap)
		{
			// expr ::= exprOr
			return ParseExpressionOr(scope, code, result, ref doWrap);
		} // func ParseExpression

		private static Expression ParseExpressionOr(Scope scope, LuaLexer code, InvokeResult result, ref bool doWrap)
		{
			// exprOr ::= exprAnd { or exprAnd }
			var expr = ParseExpressionAnd(scope, code, result, ref doWrap);

			while (code.Current.Typ == LuaToken.KwOr)
			{
				code.Next();
				expr = BinaryOperationExpression(scope.Runtime, code.Current, ExpressionType.OrElse, expr, ParseExpressionAnd(scope, code, InvokeResult.Object, ref doWrap));
				doWrap |= true;
			}

			return expr;
		} // func ParseExpressionOr

		private static Expression ParseExpressionAnd(Scope scope, LuaLexer code, InvokeResult result, ref bool doWrap)
		{
			// exprAnd ::= exprBitOr { and exprBitOr }
			var expr = ParseExpressionBitOr(scope, code, result, ref doWrap);

			while (code.Current.Typ == LuaToken.KwAnd)
			{
				code.Next();
				expr = BinaryOperationExpression(scope.Runtime, code.Current, ExpressionType.AndAlso, expr, ParseExpressionBitOr(scope, code, InvokeResult.Object, ref doWrap));
				doWrap |= true;
			}

			return expr;
		} // func ParseExpressionAnd

		private static Expression ParseExpressionBitOr(Scope scope, LuaLexer code, InvokeResult result, ref bool doWrap)
		{
			// exprBitOr ::= exprBitXOr { | exprBitXOr }
			var expr = ParseExpressionBitXOr(scope, code, result, ref doWrap);

			while (code.Current.Typ == LuaToken.BitOr)
			{
				code.Next();
				expr = BinaryOperationExpression(scope.Runtime, code.Current, ExpressionType.Or,
					expr,
					ParseExpressionBitXOr(scope, code, InvokeResult.Object, ref doWrap)
				);
				doWrap |= true;
			}

			return expr;
		} // func ParseExpressionBitOr

		private static Expression ParseExpressionBitXOr(Scope scope, LuaLexer code, InvokeResult result, ref bool doWrap)
		{
			// exprBitXOr ::= exprBitAnd { ~ exprBitAnd }
			var expr = ParseExpressionBitAnd(scope, code, result, ref doWrap);

			while (code.Current.Typ == LuaToken.Dilde)
			{
				code.Next();
				expr = BinaryOperationExpression(scope.Runtime, code.Current, ExpressionType.ExclusiveOr,
					expr,
					ParseExpressionBitAnd(scope, code, InvokeResult.Object, ref doWrap)
				);
				doWrap |= true;
			}

			return expr;
		} // func ParseExpressionBitXOr

		private static Expression ParseExpressionBitAnd(Scope scope, LuaLexer code, InvokeResult result, ref bool doWrap)
		{
			// exprBitAnd ::= exprCmp { & exprCmp }
			var expr = ParseExpressionCmp(scope, code, result, ref doWrap);

			while (code.Current.Typ == LuaToken.BitAnd)
			{
				code.Next();
				expr = BinaryOperationExpression(scope.Runtime, code.Current, ExpressionType.And,
					expr,
					ParseExpressionCmp(scope, code, InvokeResult.Object, ref doWrap)
				);
				doWrap |= true;
			}

			return expr;
		} // func ParseExpressionBitAnd

		private static Expression ParseExpressionCmp(Scope scope, LuaLexer code, InvokeResult result, ref bool doWrap)
		{
			// expCmd ::= expCon { ( < | > | <= | >= | ~= | == ) expCon }
			var tStart = code.Current;
			var expr = ParseExpressionCon(scope, code, result, ref doWrap);

			while (true)
			{
				var tokenTyp = code.Current.Typ;
				ExpressionType exprTyp;
				if (tokenTyp == LuaToken.Lower)
					exprTyp = ExpressionType.LessThan;
				else if (tokenTyp == LuaToken.Greater)
					exprTyp = ExpressionType.GreaterThan;
				else if (tokenTyp == LuaToken.LowerEqual)
					exprTyp = ExpressionType.LessThanOrEqual;
				else if (tokenTyp == LuaToken.GreaterEqual)
					exprTyp = ExpressionType.GreaterThanOrEqual;
				else if (tokenTyp == LuaToken.NotEqual)
					exprTyp = ExpressionType.NotEqual;
				else if (tokenTyp == LuaToken.Equal)
					exprTyp = ExpressionType.Equal;
				else
					return expr;
				code.Next();

				expr = BinaryOperationExpression(scope.Runtime, code.Current, exprTyp, expr, ParseExpressionCon(scope, code, InvokeResult.Object, ref doWrap));
				doWrap |= true;
			}
		} // func ParseExpressionCmp

		private static Expression ParseExpressionCon(Scope scope, LuaLexer code, InvokeResult result, ref bool doWrap)
		{
			// exprCon::= exprShift { '..' exprShift }
			var exprs = new List<Expression>();
			exprs.Add(ParseExpressionShift(scope, code, result, ref doWrap));

			while (code.Current.Typ == LuaToken.DotDot)
			{
				code.Next();
				exprs.Add(ParseExpressionShift(scope, code, InvokeResult.Object, ref doWrap));
			}

			// Erzeuge Concat
			if (exprs.Count > 1)
			{
				doWrap |= true;
				return ConcatOperationExpression(scope.Runtime, code.Current, exprs.ToArray());
			}
			else
				return exprs[0];
		} // func ParseExpressionCon

		private static Expression ParseExpressionShift(Scope scope, LuaLexer code, InvokeResult result, ref bool doWrap)
		{
			// exprBitAnd ::= exprCmp { ( << | >> ) exprCmp }
			var expr = ParseExpressionPlus(scope, code, result, ref doWrap);

			while (true)
			{
				var tokenTyp = code.Current.Typ;
				ExpressionType exprTyp;

				if (tokenTyp == LuaToken.ShiftLeft)
					exprTyp = ExpressionType.LeftShift;
				else if (tokenTyp == LuaToken.ShiftRight)
					exprTyp = ExpressionType.RightShift;
				else
					return expr;

				code.Next();
				expr = BinaryOperationExpression(scope.Runtime, code.Current, exprTyp, expr, ParseExpressionPlus(scope, code, InvokeResult.Object, ref doWrap));
				doWrap |= true;
			}
		} // func ParseExpressionShift

		private static Expression ParseExpressionPlus(Scope scope, LuaLexer code, InvokeResult result, ref bool doWrap)
		{
			// expPlus ::= expMul { ( + | - ) expMul}
			var expr = ParseExpressionMultiply(scope, code, result, ref doWrap);

			while (true)
			{
				var tokenTyp = code.Current.Typ;
				ExpressionType exprTyp;
				if (tokenTyp == LuaToken.Plus)
					exprTyp = ExpressionType.Add;
				else if (tokenTyp == LuaToken.Minus)
					exprTyp = ExpressionType.Subtract;
				else
					return expr;

				code.Next();
				expr = BinaryOperationExpression(scope.Runtime, code.Current, exprTyp, expr, ParseExpressionMultiply(scope, code, InvokeResult.Object, ref doWrap));
				doWrap |= true;
			}
		} // func ParseExpressionPlus

		private static Expression ParseExpressionMultiply(Scope scope, LuaLexer code, InvokeResult result, ref bool doWrap)
		{
			// expMul ::= expUn { ( * | / | // | % ) expUn }
			var expr = ParseExpressionUnary(scope, code, result, ref doWrap);

			while (true)
			{
				var tokenTyp = code.Current.Typ;
				ExpressionType exprTyp;
				if (tokenTyp == LuaToken.Star)
					exprTyp = ExpressionType.Multiply;
				else if (tokenTyp == LuaToken.Slash)
					exprTyp = ExpressionType.Divide;
				else if (tokenTyp == LuaToken.SlashShlash)
					exprTyp = Lua.IntegerDivide;
				else if (tokenTyp == LuaToken.Percent)
					exprTyp = ExpressionType.Modulo;
				else
					return expr;

				code.Next();

				expr = BinaryOperationExpression(scope.Runtime, code.Current, exprTyp, expr, ParseExpressionUnary(scope, code, InvokeResult.Object, ref doWrap));
				doWrap |= true;
			}
		} // func ParseExpressionMultiply

		private static Expression ParseExpressionUnary(Scope scope, LuaLexer code, InvokeResult result, ref bool doWrap)
		{
			// expUn ::= { 'not' | - | # | ~ } expPow
			var typ = code.Current.Typ;
			if (typ == LuaToken.KwNot ||
					typ == LuaToken.Minus ||
					typ == LuaToken.Dilde ||
					typ == LuaToken.Cross)
			{
				code.Next();
				var expr = ParseExpressionUnary(scope, code, InvokeResult.Object, ref doWrap);
				doWrap |= true;

				ExpressionType exprType;
				if (typ == LuaToken.KwNot)
					exprType = ExpressionType.Not;
				else if (typ == LuaToken.Minus)
					exprType = ExpressionType.Negate;
				else if (typ == LuaToken.Dilde)
					exprType = ExpressionType.OnesComplement;
				else
					exprType = ExpressionType.ArrayLength;

				doWrap |= true;
				return UnaryOperationExpression(scope.Runtime, code.Current, exprType, expr);
			}
			else
				return ParseExpressionPower(scope, code, result, ref doWrap);
		} // func ParseExpressionUnary

		private static Expression ParseExpressionPower(Scope scope, LuaLexer code, InvokeResult result, ref bool doWrap)
		{
			// expPow ::= cast [ ^ expPow ]
			var expr = ParseExpressionCast(scope, code, result, ref doWrap);

			if (code.Current.Typ == LuaToken.Caret)
			{
				code.Next();
				doWrap |= true;
				return BinaryOperationExpression(scope.Runtime, code.Current, ExpressionType.Power, expr, ParseExpressionPower(scope, code, InvokeResult.Object, ref doWrap));
			}
			else
				return expr;
		} // func ParseExpressionPower

		private static Expression ParseExpressionCast(Scope scope, LuaLexer code, InvokeResult result, ref bool doWrap)
		{
			// cast ::= cast(type, expr)
			if (code.Current.Typ == LuaToken.KwCast)
			{
				var tStart = code.Current;
				doWrap |= true;
				var prefix = new PrefixMemberInfo(tStart, ParsePrefixCast(scope, code), null, null, null);
				ParseSuffix(scope, code, prefix);
				return prefix.GenerateGet(scope, result);
			}
			else
				return ParsePrefix(scope, code).GenerateGet(scope, result);
		} // func ParseExpressionCast

		private static Expression ParsePrefixCast(Scope scope, LuaLexer code)
		{
			LuaType luaType;
			var t = code.Current;
			code.Next();

			FetchToken(LuaToken.BracketOpen, code);

			// Read the type
			luaType = ParseType(scope, code, true);
			FetchToken(LuaToken.Comma, code);

			var doWrap = scope.EmitExpressionDebug;
			var expr = ParseExpression(scope, code, InvokeResult.Object, ref doWrap);

			FetchToken(LuaToken.BracketClose, code);

			return ConvertExpression(scope.Runtime, t, expr, luaType);
		} // func ParsePrefixCast

		private static void ParseIdentifierAndType(Scope scope, LuaLexer code, out Token tokenName, out Type type)
		{
			// var ::= name ':' type
			tokenName = FetchToken(LuaToken.Identifier, code);
			if (code.Current.Typ == LuaToken.Colon)
			{
				code.Next();
				type = ParseType(scope, code, true);
			}
			else
				type = typeof(object);
		} // func ParseIdentifierAndType

		private static LuaType ParseType(Scope scope, LuaLexer code, bool needType)
		{
			// is the first token an alias
			var currentType = ParseFirstType(scope, code);

			while (code.Current.Typ == LuaToken.Dot ||
						code.Current.Typ == LuaToken.Plus ||
						code.Current.Typ == LuaToken.BracketSquareOpen)
			{
				if (code.Current.Typ == LuaToken.BracketSquareOpen)
				{
					var genericeTypes = new List<LuaType>();
					code.Next();
					if (code.Current.Typ != LuaToken.BracketSquareClose)
					{
						genericeTypes.Add(ParseType(scope, code, needType));
						while (code.Current.Typ == LuaToken.Comma)
						{
							code.Next();
							genericeTypes.Add(ParseType(scope, code, needType));
						}
					}
					FetchToken(LuaToken.BracketSquareClose, code);

					if (genericeTypes.Count == 0) // create a array at the end
					{
						if (currentType.Type == null)
							throw ParseError(code.Current, String.Format(Properties.Resources.rsParseUnknownType, currentType.FullName));

						currentType = LuaType.GetType(currentType.AddType("[]", false, 1));
					}
					else // build a generic type
					{
						try
						{
							currentType = currentType.MakeGenericLuaType(genericeTypes.ToArray(), true);
						}
						catch (ArgumentException e)
						{
							throw ParseError(code.Current, e.Message);
						}
					}
				}
				else
				{
					code.Next();
					currentType = LuaType.GetType(currentType.AddType(FetchToken(LuaToken.Identifier, code).Value, false, null));
				}
			}

			if (needType && currentType.Type == null)
				throw ParseError(code.Current, String.Format(Properties.Resources.rsParseUnknownType, currentType.FullName));

			return currentType;
		} // func ParseType

		private static LuaType ParseFirstType(Scope scope, LuaLexer code)
		{
			var typeName = FetchToken(LuaToken.Identifier, code).Value;
			var luaType = LuaType.GetCachedType(typeName);
			if (luaType == null)
			{
				return scope.LookupExpression(typeName, false) is ConstantExpression cexpr && cexpr.Type == typeof(LuaType)
					? (LuaType)cexpr.Value
					: LuaType.GetType(LuaType.Clr.AddType(typeName, false, null));
			}
			else
				return luaType;
		} // func ParseFirstType

		#endregion

		#region -- Parse Goto, Label ------------------------------------------------------

		private static void ParseGoto(Scope scope, LuaLexer code)
		{
			// goto Identifier
			FetchToken(LuaToken.KwGoto, code);

			var t = FetchToken(LuaToken.Identifier, code);
			scope.AddExpression(Expression.Goto(scope.LookupLabel(null, t.Value)));
		} // proc ParseGoto

		private static void ParseLabel(Scope scope, LuaLexer code)
		{
			// ::identifier::
			FetchToken(LuaToken.ColonColon, code);

			// Erzeuge das Label
			scope.AddExpression(Expression.Label(scope.LookupLabel(null, FetchToken(LuaToken.Identifier, code).Value)));

			FetchToken(LuaToken.ColonColon, code);
		} // proc ParseLabel

		#endregion

		#region -- Parse Loops ------------------------------------------------------------

		private static void ParseDoLoop(Scope scope, LuaLexer code)
		{
			// doloop ::= do '(' name { ',' name } = expr { ',' expr }  ')' block end '(' { 'function' '(' var ':' type ')' block 'end' ',' } ')'  
			//

			// create empty block, that can used as an loop
			var outerScope = new Scope(scope);
			var exprDispose = (Expression[])null;

			// fetch do
			FetchToken(LuaToken.KwDo, code);
			if (code.Current.Typ == LuaToken.BracketOpen) // look for disposable variables
			{
				code.Next();
				ParseExpressionStatement(outerScope, code, true);

				// Build finally-Block for the declared variables
				exprDispose = (
					from c in outerScope.Variables
					select Expression.IfThen(
						Expression.TypeIs(c, typeof(IDisposable)),
						Expression.Call(Expression.Convert(c, typeof(IDisposable)), Lua.DisposeDisposeMethodInfo)
					)).ToArray();

				FetchToken(LuaToken.BracketClose, code);
			}

			var loopScope = new LoopScope(outerScope);

			// Add the Contine label after the declaration
			loopScope.AddExpression(Expression.Label(loopScope.ContinueLabel));
			// parse the block
			ParseBlock(loopScope, code);
			// create the break label
			loopScope.AddExpression(Expression.Label(loopScope.BreakLabel));

			FetchToken(LuaToken.KwEnd, code);

			// check for catch blocks
			var exprFinallyBlock = (Expression)null;
			var exprCatch = new List<CatchBlock>();
			if (code.Current.Typ == LuaToken.BracketOpen)
			{
				code.Next();

				while (code.Current.Typ != LuaToken.BracketClose)
				{
					FetchToken(LuaToken.KwFunction, code);
					if (code.Current.Typ == LuaToken.BracketOpen)
					{
						code.Next();

						var exceptionName = FetchToken(LuaToken.Identifier, code);
						Type exceptionType;

						if (code.Current.Typ == LuaToken.Colon)
						{
							code.Next();

							// check if the type is inherited from Exception
							exceptionType = ParseType(scope, code, true).Type;
							if (!typeof(Exception).GetTypeInfo().IsAssignableFrom(exceptionType.GetTypeInfo()))
								throw ParseError(code.Current, String.Format(Properties.Resources.rsParseCatchVarTypeMustAssignableToException, exceptionType.FullName));
						}
						else
							exceptionType = typeof(Exception);

						FetchToken(LuaToken.BracketClose, code);

						var exceptionScope = new CatchScope(outerScope, Expression.Parameter(exceptionType, exceptionName.Value));
						ParseBlock(exceptionScope, code, true);

						exprCatch.Add(Expression.MakeCatchBlock(exceptionType, exceptionScope.ExceptionVariable, exceptionScope.ExpressionBlock, null));
					}
					else
					{
						var finallyScope = new Scope(outerScope);
						ParseBlock(finallyScope, code);

						exprFinallyBlock = finallyScope.ExpressionBlock;
					}

					FetchToken(LuaToken.KwEnd, code);
					FetchToken(LuaToken.Comma, code, true);
				}

				FetchToken(LuaToken.BracketClose, code);
			}

			// build finally block
			if (exprDispose != null && exprDispose.Length > 0)
			{
				if (exprFinallyBlock != null)
					exprFinallyBlock = Expression.MakeTry(typeof(void), exprFinallyBlock, Expression.Block(exprDispose), null, null);
				else
					exprFinallyBlock = Expression.Block(exprDispose);
			}

			if (exprFinallyBlock != null || exprCatch.Count > 0)
			{
				outerScope.AddExpression(
					Expression.MakeTry(typeof(void),
						loopScope.ExpressionBlock,
						exprFinallyBlock,
						null,
						exprCatch
					)
				);
				scope.AddExpression(outerScope.ExpressionBlock);
			}
			else
				scope.AddExpression(loopScope.ExpressionBlock);
		} // ParseDoLoop

		private static void ParseWhileLoop(Scope scope, LuaLexer code)
		{
			// while expr do block end;
			var loopScope = new LoopScope(scope);

			// get the expression
			FetchToken(LuaToken.KwWhile, code);

			loopScope.AddExpression(Expression.Label(loopScope.ContinueLabel));
			loopScope.AddExpression(
				Expression.IfThenElse(
					ConvertExpression(scope.Runtime, code.Current, ParseExpression(scope, code, InvokeResult.Object, scope.EmitExpressionDebug), typeof(bool)),
					Expression.Empty(),
					Expression.Goto(loopScope.BreakLabel)
				)
			);

			// append the block
			FetchToken(LuaToken.KwDo, code);
			ParseBlock(loopScope, code);
			FetchToken(LuaToken.KwEnd, code);

			// goto continue
			loopScope.AddExpression(Expression.Goto(loopScope.ContinueLabel));
			loopScope.AddExpression(Expression.Label(loopScope.BreakLabel));

			scope.AddExpression(loopScope.ExpressionBlock);
		} // func ParseWhileLoop

		private static void ParseRepeatLoop(Scope scope, LuaLexer code)
		{
			var loopScope = new LoopScope(scope);

			// continue label
			loopScope.AddExpression(Expression.Label(loopScope.ContinueLabel));

			// loop content
			FetchToken(LuaToken.KwRepeat, code);
			ParseBlock(loopScope, code);

			// get the loop expression
			FetchToken(LuaToken.KwUntil, code);
			loopScope.AddExpression(
				Expression.IfThenElse(
					ConvertExpression(scope.Runtime, code.Current, ParseExpression(scope, code, InvokeResult.Object, scope.EmitExpressionDebug), typeof(bool)),
					Expression.Empty(),
					Expression.Goto(loopScope.ContinueLabel)
				)
			);

			loopScope.AddExpression(Expression.Label(loopScope.BreakLabel));

			scope.AddExpression(loopScope.ExpressionBlock);
		} // func ParseRepeatLoop

		private static void ParseForLoop(Scope scope, LuaLexer code)
		{
			// for name
			FetchToken(LuaToken.KwFor, code);
			ParseIdentifierAndType(scope, code, out var tLoopVar, out var typeLoopVar);
			if (code.Current.Typ == LuaToken.Assign)
			{
				// = exp, exp [, exp] do block end
				FetchToken(LuaToken.Assign, code);
				var loopStart = ParseExpression(scope, code, InvokeResult.Object, scope.EmitExpressionDebug);
				FetchToken(LuaToken.Comma, code);
				var loopEnd = ParseExpression(scope, code, InvokeResult.Object, scope.EmitExpressionDebug);
				Expression loopStep;
				if (code.Current.Typ == LuaToken.Comma)
				{
					code.Next();
					loopStep = ParseExpression(scope, code, InvokeResult.Object, scope.EmitExpressionDebug);
				}
				else
					loopStep = Expression.Constant(1, loopStart.Type);

				var loopScope = new LoopScope(scope);
				var loopVarParameter = loopScope.RegisterVariable(typeLoopVar == typeof(object) ? LuaEmit.LiftType(LuaEmit.LiftType(loopStart.Type, loopEnd.Type), loopStep.Type) : typeLoopVar, tLoopVar.Value);

				FetchToken(LuaToken.KwDo, code);
				ParseBlock(loopScope, code);
				FetchToken(LuaToken.KwEnd, code);
				scope.AddExpression(GenerateForLoop(loopScope, tLoopVar, loopVarParameter, loopStart, loopEnd, loopStep));
			}
			else
			{
				// {, name} in explist do block end

				// fetch all loop variables
				var loopScope = new LoopScope(scope);
				var loopVars = new List<ParameterExpression>
				{
					loopScope.RegisterVariable(typeLoopVar, tLoopVar.Value)
				};
				while (code.Current.Typ == LuaToken.Comma)
				{
					code.Next();
					ParseIdentifierAndType(scope, code, out tLoopVar, out typeLoopVar);
					loopVars.Add(loopScope.RegisterVariable(typeLoopVar, tLoopVar.Value));
				}

				// get the loop expressions
				FetchToken(LuaToken.KwIn, code);
				var explist = ParseExpressionList(scope, code).ToArray();

				// parse the loop body
				FetchToken(LuaToken.KwDo, code);
				ParseBlock(loopScope, code);
				FetchToken(LuaToken.KwEnd, code);

				scope.AddExpression(GenerateForLoop(loopScope, tLoopVar, loopVars, explist));
			}
		} // func ParseForLoop

		private static void ParseForEachLoop(Scope scope, LuaLexer code)
		{
			var varEnumerable = Expression.Variable(typeof(System.Collections.IEnumerable), "$enumerable");
			var varEnumerator = Expression.Variable(typeof(System.Collections.IEnumerator), "$enumerator");
			var varDisposable = Expression.Variable(typeof(IDisposable), "$disposeable");

			// foreach name in exp do block end;
			code.Next(); // foreach

			// fetch the loop variable
			var loopScope = new LoopScope(scope);
			ParseIdentifierAndType(scope, code, out var tLoopVar, out var typeLoopVar);
			var loopVar = loopScope.RegisterVariable(typeLoopVar, tLoopVar.Value);

			// get the enumerable expression
			FetchToken(LuaToken.KwIn, code);
			var exprEnum = Lua.EnsureType(ParseExpression(scope, code, InvokeResult.None, scope.EmitExpressionDebug), typeof(object));

			// parse the loop body
			FetchToken(LuaToken.KwDo, code);
			ParseBlock(loopScope, code);
			FetchToken(LuaToken.KwEnd, code);

			loopScope.InsertExpression(0, Expression.Assign(loopVar, ConvertExpression(scope.Runtime, tLoopVar, Expression.Property(varEnumerator, Lua.EnumeratorCurrentPropertyInfo), loopVar.Type)));
			scope.AddExpression(
				Expression.Block(new ParameterExpression[] { varEnumerable, varEnumerator, loopVar },
				// local enumerable = exprEnum as IEnumerator
				Expression.Assign(varEnumerable, Expression.TypeAs(exprEnum, typeof(System.Collections.IEnumerable))),

				// if enumerable == nil then error
				Expression.IfThen(Expression.Equal(varEnumerable, Expression.Constant(null, typeof(object))), Lua.ThrowExpression(Properties.Resources.rsExpressionNotEnumerable)),

				// local enum = exprEnum.GetEnumerator()
				Expression.Assign(varEnumerator, Expression.Call(varEnumerable, Lua.EnumerableGetEnumeratorMethodInfo)),

				Expression.TryFinally(
					Expression.Block(

						// while enum.MoveNext() do
						Expression.Label(loopScope.ContinueLabel),
						Expression.IfThenElse(Expression.Call(varEnumerator, Lua.EnumeratorMoveNextMethodInfo), Expression.Empty(), Expression.Goto(loopScope.BreakLabel)),

						//   loopVar = enum.Current
						loopScope.ExpressionBlock,

						// end;
						Expression.Goto(loopScope.ContinueLabel),
						Expression.Label(loopScope.BreakLabel)
					),
					Expression.Block(
						new ParameterExpression[] { varDisposable },
						Expression.Assign(varDisposable, Expression.TypeAs(varEnumerator, typeof(IDisposable))),
						Expression.IfThen(Expression.NotEqual(varDisposable, Expression.Constant(null, typeof(IDisposable))),
							Expression.Call(varDisposable, Lua.DisposeDisposeMethodInfo)
						)
					)
				)
			));
		} // proc ParseForEachLoop

		private static Expression GenerateForLoop(LoopScope loopScope, Token tStart, ParameterExpression loopVar, Expression loopStart, Expression loopEnd, Expression loopStep)
		{
			const string csVar = "#var";
			const string csEnd = "#end";
			const string csStep = "#step";
			var internLoopVar = Expression.Variable(loopVar.Type, csVar);
			var endVar = Expression.Variable(loopEnd.Type, csEnd);
			var stepVar = Expression.Variable(loopStep.Type, csStep);
			var labelLoop = Expression.Label("#loop");

			// Erzeuge CodeBlock
			loopScope.InsertExpression(0, Expression.Assign(loopVar, internLoopVar));

			// Erzeuge den Schleifenblock
			return Expression.Block(new ParameterExpression[] { internLoopVar, endVar, stepVar },
				Expression.Assign(internLoopVar, ConvertExpression(loopScope.Runtime, tStart, loopStart, internLoopVar.Type)),
				Expression.Assign(endVar, loopEnd),
				Expression.Assign(stepVar, loopStep),

				Expression.Label(labelLoop),

				Expression.IfThenElse(
					BinaryOperationExpression(loopScope.Runtime, tStart, ExpressionType.OrElse,
						BinaryOperationExpression(loopScope.Runtime, tStart, ExpressionType.AndAlso,
							ConvertExpression(loopScope.Runtime, tStart, BinaryOperationExpression(loopScope.Runtime, tStart, ExpressionType.GreaterThan, stepVar, Expression.Constant(0, typeof(int))), typeof(bool)),
							ConvertExpression(loopScope.Runtime, tStart, BinaryOperationExpression(loopScope.Runtime, tStart, ExpressionType.LessThanOrEqual, internLoopVar, endVar), typeof(bool))
						),
						BinaryOperationExpression(loopScope.Runtime, tStart, ExpressionType.AndAlso,
							ConvertExpression(loopScope.Runtime, tStart, BinaryOperationExpression(loopScope.Runtime, tStart, ExpressionType.LessThanOrEqual, stepVar, Expression.Constant(0, typeof(int))), typeof(bool)),
							ConvertExpression(loopScope.Runtime, tStart, BinaryOperationExpression(loopScope.Runtime, tStart, ExpressionType.GreaterThanOrEqual, internLoopVar, endVar), typeof(bool))
						)
					),
					loopScope.ExpressionBlock,
					Expression.Goto(loopScope.BreakLabel)
				),
				Expression.Label(loopScope.ContinueLabel),

				Expression.Assign(internLoopVar, ConvertExpression(loopScope.Runtime, tStart, BinaryOperationExpression(loopScope.Runtime, tStart, ExpressionType.Add, internLoopVar, stepVar), internLoopVar.Type)),

				Expression.Goto(labelLoop),
				Expression.Label(loopScope.BreakLabel)
			);
		} // func GenerateForLoop

		private static Expression GenerateForLoop(LoopScope loopScope, Token tStart, List<ParameterExpression> loopVars, Expression[] explist)
		{
			const string csFunc = "#f";
			const string csState = "#s";
			const string csVar = "#var";

			var varTmp = Expression.Variable(typeof(LuaResult), "#tmp");
			var varFunc = Expression.Variable(explist.Length > 0 && typeof(Delegate).GetTypeInfo().IsAssignableFrom(explist[0].Type.GetTypeInfo()) ? explist[0].Type : typeof(object), csFunc);
			var varState = Expression.Variable(typeof(object), csState);
			var varVar = Expression.Variable(typeof(object), csVar);

			// local var1, ..., varn = tmp;
			for (var i = 0; i < loopVars.Count; i++)
				loopScope.InsertExpression(i, Expression.Assign(loopVars[i], ConvertExpression(loopScope.Runtime, tStart, GetResultExpression(loopScope.Runtime, tStart, varTmp, i), loopVars[i].Type)));

			return Expression.Block(new ParameterExpression[] { varTmp, varFunc, varState, varVar },
				// fill the local loop variables initial
				// local #f, #s, #var = explist
				Expression.Assign(varTmp, GetLuaResultExpression(loopScope, tStart, explist)),
				Expression.Assign(varFunc, ConvertExpression(loopScope.Runtime, tStart, GetResultExpression(loopScope.Runtime, tStart, varTmp, 0), varFunc.Type)),
				Expression.Assign(varState, GetResultExpression(loopScope.Runtime, tStart, varTmp, 1)),
				Expression.Assign(varVar, GetResultExpression(loopScope.Runtime, tStart, varTmp, 2)),

				Expression.Label(loopScope.ContinueLabel),

				// local tmp = f(s, var)
				Expression.Assign(varTmp, InvokeExpression(loopScope, tStart, varFunc, InvokeResult.LuaResult,
					new ArgumentsList(varState, varVar), true)
				),

				// var = tmp[0]
				Expression.Assign(varVar, GetResultExpression(loopScope.Runtime, tStart, varTmp, 0)),

				// if var == nil then goto break;
				Expression.IfThenElse(Expression.Equal(varVar, Expression.Constant(null, typeof(object))),
					Expression.Goto(loopScope.BreakLabel),
					loopScope.ExpressionBlock), // LoopBody

				Expression.Goto(loopScope.ContinueLabel),
				Expression.Label(loopScope.BreakLabel)
			);
		} // func GenerateForLoop

		private static void ParseBreak(Scope scope, LuaLexer code)
		{
			FetchToken(LuaToken.KwBreak, code);

			// Erzeuge die Expression
			scope.AddExpression(Expression.Goto(scope.LookupLabel(null, csBreakLabel)));

			// Optionales Semicolon
			FetchToken(LuaToken.Semicolon, code, true);
		} // func ParseBreak

		#endregion

		#region -- Parse Function, Lambda -------------------------------------------------

		private static void ParseFunction(Scope scope, LuaLexer code, bool isLocal)
		{
			FetchToken(LuaToken.KwFunction, code);

			if (isLocal) // Local function, only one identifier is allowed
			{
				var t = FetchToken(LuaToken.Identifier, code);
				ParameterExpression funcVar = null;
				var exprFunction = ParseLamdaDefinition(scope, code, t.Value, false,
					typeDelegate => funcVar = scope.RegisterVariable(typeDelegate, t.Value)
				);
				scope.AddExpression(Expression.Assign(funcVar, exprFunction));
			}
			else // Function that is assigned to a table. A chain of identifiers is allowed.
			{
				Expression assignee = null;
				var tCurrent = FetchToken(LuaToken.Identifier, code);
				var memberName = tCurrent.Value;

				// Collect the chain of members
				while (code.Current.Typ == LuaToken.Dot)
				{
					code.Next();

					// Create the get-member for the current assignee
					assignee = ParseFunctionAddChain(scope, tCurrent, assignee, memberName);
					memberName = FetchToken(LuaToken.Identifier, code).Value;
				}
				// add a method to the table. methods get a hidden parameter and will bo marked
				bool lMethodMember;
				if (code.Current.Typ == LuaToken.Colon)
				{
					code.Next();

					// add the last member to the assignee chain
					assignee = ParseFunctionAddChain(scope, tCurrent, assignee, memberName);
					// fetch the method name
					memberName = FetchToken(LuaToken.Identifier, code).Value;
					lMethodMember = true;
				}
				else
				{
					if (assignee == null)
						assignee = scope.LookupExpression(csEnv); // create a global function
					lMethodMember = false;
				}

				// generate lambda
				scope.AddExpression(MemberSetExpression(scope.Runtime, tCurrent, assignee, memberName, lMethodMember, ParseLamdaDefinition(scope, code, memberName, lMethodMember, null)));
			}
		} // proc ParseLamdaDefinition

		private static Expression ParseFunctionAddChain(Scope scope, Token tStart, Expression assignee, string memberName)
		{
			if (assignee == null)
			{
				var expr = scope.LookupExpression(memberName);
				if (expr == null)
					assignee = ParseFunctionAddChain(scope, tStart, scope.LookupExpression(csEnv), memberName);
				else
					assignee = expr;
			}
			else
				assignee = MemberGetExpression(scope, tStart, assignee, memberName);
			return assignee;
		} // proc ParseFunctionAddChain

		private static Expression ParseLamdaDefinition(Scope parent, LuaLexer code, string name, bool hasSelfParameter, Action<Type> functionTypeCollected)
		{
			var parameters = new List<ParameterExpression>();
			var scope = new LambdaScope(parent);

			// Lese die Parameterliste ein
			FetchToken(LuaToken.BracketOpen, code);
			if (hasSelfParameter)
				parameters.Add(scope.RegisterParameter(typeof(object), "self"));

			if (code.Current.Typ == LuaToken.Identifier || code.Current.Typ == LuaToken.DotDotDot)
			{
				if (code.Current.Typ == LuaToken.DotDotDot)
				{
					code.Next();
					ParseLamdaDefinitionArgList(scope, parameters);
				}
				else
				{
					ParseIdentifierAndType(scope, code, out var tName, out var typeArgument);
					parameters.Add(scope.RegisterParameter(typeArgument, tName.Value));

					while (code.Current.Typ == LuaToken.Comma)
					{
						code.Next();
						if (code.Current.Typ == LuaToken.DotDotDot)
						{
							code.Next();
							ParseLamdaDefinitionArgList(scope, parameters); // last argument
							break;
						}
						else
						{
							ParseIdentifierAndType(scope, code, out tName, out typeArgument);
							parameters.Add(scope.RegisterParameter(typeArgument, tName.Value));
						}
					}
				}
			}
			FetchToken(LuaToken.BracketClose, code);

			// Is there a specific result 
			if (code.Current.Typ == LuaToken.Colon)
			{
				var t = code.Current;
				code.Next();
				var typeResult = ParseType(scope, code, true);
				scope.ResetReturnLabel(typeResult, null);
			}
			else
				scope.ResetReturnLabel(typeof(LuaResult), Expression.Property(null, Lua.ResultEmptyPropertyInfo));

			// register the delegate
			if (functionTypeCollected != null)
			{
				var functionType = scope.ReturnType == typeof(void) 
					? Expression.GetActionType((from p in parameters select p.Type).ToArray())
					: Expression.GetFuncType((from p in parameters select p.Type).Concat(new Type[] { scope.ReturnType }).ToArray());
				functionTypeCollected(functionType);
			}

			// Lese den Code-Block
			ParseBlock(scope, code);

			FetchToken(LuaToken.KwEnd, code);
			return Expression.Lambda(
				scope.ExpressionBlock,
				(parent.EmitDebug & LuaDebugLevel.RegisterMethods) == LuaDebugLevel.RegisterMethods ? Lua.RegisterUniqueName(name) : name,
				parameters);
		} // proc ParseLamdaDefinition

		private static void ParseLamdaDefinitionArgList(LambdaScope scope, List<ParameterExpression> parameters)
		{
			var paramArgList = scope.RegisterParameter(typeof(object[]), csArgListP);
			var varArgList = scope.RegisterVariable(typeof(LuaResult), csArgList);
			parameters.Add(paramArgList);
			scope.AddExpression(Expression.Assign(varArgList, Expression.New(Lua.ResultConstructorInfoArgN, paramArgList)));
		} // proc ParseLamdaDefinitionArgList

		#endregion

		#region -- Parse TableConstructor -------------------------------------------------

		private static Expression ParseTableConstructor(Scope scope, LuaLexer code)
		{
			// table ::= '{' [field] { fieldsep field } [fieldsep] '}'
			// fieldsep ::= ',' | ';'
			FetchToken(LuaToken.BracketCurlyOpen, code);

			if (code.Current.Typ != LuaToken.BracketCurlyClose)
			{
				var index = 1;
				var scopeTable = new Scope(scope);

				// Create the variable for the table
				var tableVar = scopeTable.RegisterVariable(typeof(LuaTable), "#table");
				scopeTable.AddExpression(Expression.Assign(tableVar, CreateEmptyTableExpression()));

				// fiest field
				ParseTableField(tableVar, scopeTable, code, ref index);

				// collect more table fields
				while (code.Current.Typ == LuaToken.Comma || code.Current.Typ == LuaToken.Semicolon)
				{
					code.Next();

					// Optional last separator
					if (code.Current.Typ == LuaToken.BracketCurlyClose)
						break;

					// Parse the field
					ParseTableField(tableVar, scopeTable, code, ref index);
				}

				scopeTable.AddExpression(tableVar);
				scopeTable.ExpressionBlockType = typeof(LuaTable);

				// Closing bracket
				FetchToken(LuaToken.BracketCurlyClose, code);

				return scopeTable.ExpressionBlock;
			}
			else
			{
				FetchToken(LuaToken.BracketCurlyClose, code);
				return CreateEmptyTableExpression();
			}
		} // func ParseTableConstructor

		private static void ParseTableField(ParameterExpression tableVar, Scope scope, LuaLexer code, ref int iIndex)
		{
			// field ::= '[' exp ']' '=' exp | Name '=' exp | exp
			if (code.Current.Typ == LuaToken.BracketSquareOpen)
			{
				// Parse the index
				code.Next();
				var index = ParseExpression(scope, code, InvokeResult.Object, scope.EmitExpressionDebug);
				FetchToken(LuaToken.BracketSquareClose, code);
				FetchToken(LuaToken.Assign, code);

				// Expression that results in a value
				scope.AddExpression(
					IndexSetExpression(scope.Runtime, code.Current, tableVar, new Expression[] { index },
						ParseExpression(scope, code, InvokeResult.Object, scope.EmitExpressionDebug)
					)
				);
			}
			else if (code.Current.Typ == LuaToken.Identifier && code.LookAhead.Typ == LuaToken.Assign)
			{
				// Read the identifier
				var tokenMember = code.Current;
				code.Next();
				FetchToken(LuaToken.Assign, code);

				// Expression
				scope.AddExpression(
					IndexSetExpression(scope.Runtime, code.Current, tableVar, new Expression[] { Expression.Constant(tokenMember.Value) },
						ParseExpression(scope, code, InvokeResult.Object, scope.EmitExpressionDebug)
					)
				);
			}
			else
			{
				var tStart = code.Current;
				var expr = ParseExpression(scope, code, InvokeResult.None, scope.EmitExpressionDebug);

				// Last assign, enroll parameter
				if (code.Current.Typ == LuaToken.BracketCurlyClose && LuaEmit.IsDynamicType(expr.Type))
				{
					scope.AddExpression(
						Expression.Call(Lua.TableSetObjectsMethod,
							tableVar,
							Expression.Convert(expr, typeof(object)),
							Expression.Constant(iIndex, typeof(int))
						)
					);
				}
				else // Normal index set
				{
					scope.AddExpression(
						IndexSetExpression(scope.Runtime, code.Current, tableVar, new Expression[] { Expression.Constant(iIndex++, typeof(object)) }, expr)
					);
				}
			}
		} // proc ParseTableField

		private static Expression CreateEmptyTableExpression()
			=> Expression.New(typeof(LuaTable));
		
		#endregion

		#region -- FetchToken, ParseError -------------------------------------------------

		public static Token FetchToken(LuaToken typ, LuaLexer code, bool isOptional = false)
		{
			if (code.Current.Typ == typ)
			{
				var t = code.Current;
				code.Next();
				return t;
			}
			else if (isOptional)
				return null;
			else
				throw ParseError(code.Current, String.Format(Properties.Resources.rsParseUnexpectedToken, LuaLexer.GetTokenName(code.Current.Typ), LuaLexer.GetTokenName(typ)));
		} // proc FetchToken

		public static LuaParseException ParseError(Token start, string sMessage)
			=> new LuaParseException(start.Start, sMessage, null);

		#endregion

		#region -- ExpressionToString -----------------------------------------------------

		private static PropertyInfo propertyDebugView = null;

		public static string ExpressionToString(Expression expr)
		{
			if (propertyDebugView == null)
				propertyDebugView = typeof(Expression).GetTypeInfo().FindDeclaredProperty("DebugView", ReflectionFlag.NoException | ReflectionFlag.NonPublic | ReflectionFlag.Instance);

			return (string)propertyDebugView.GetValue(expr, null);
		} // func ExpressionToString

		#endregion
	} // class Parser

	#endregion
}
