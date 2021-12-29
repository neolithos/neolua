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
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Neo.IronLua
{
	#region -- ILuaBinder -------------------------------------------------------------

	internal interface ILuaBinder
	{
		Lua Lua { get; }
	} // interface ILuaBinder

	#endregion

	#region -- class Lua --------------------------------------------------------------

	public partial class Lua
	{
		#region -- enum BindResult ----------------------------------------------------

		/// <summary>Result for the binding of methods</summary>
		internal enum BindResult
		{
			Ok,
			MemberNotFound,
			NotReadable,
			NotWriteable
		} // enum BindResult

		#endregion

		#region -- class LuaGetMemberBinder -------------------------------------------

		internal class LuaGetMemberBinder : GetMemberBinder, ILuaBinder
		{
			private readonly Lua lua;

			public LuaGetMemberBinder(Lua lua, string name)
			  : base(name, false)
			{
				this.lua = lua;
			} // ctor

			public override DynamicMetaObject FallbackGetMember(DynamicMetaObject target, DynamicMetaObject errorSuggestion)
			{
				// defer the target, to get the type
				if (!target.HasValue)
					return Defer(target);

				if (target.Value == null) // no value for target, finish binding with an error or the suggestion
				{
					return errorSuggestion ??
						new DynamicMetaObject(
							ThrowExpression(Properties.Resources.rsNullReference, ReturnType),
							target.Restrictions.Merge(BindingRestrictions.GetInstanceRestriction(target.Expression, null))
						);
				}
				else
				{
					// restrictions
					var restrictions = target.Restrictions.Merge(BindingRestrictions.GetTypeRestriction(target.Expression, target.LimitType));

					// try to bind the member
					switch (LuaEmit.TryGetMember(target.Expression, target.LimitType, Name, IgnoreCase, out var expr))
					{
						case LuaTryGetMemberReturn.None:
							return errorSuggestion ?? new DynamicMetaObject(Expression.Default(ReturnType), restrictions);
						case LuaTryGetMemberReturn.NotReadable:
							return errorSuggestion ?? new DynamicMetaObject(ThrowExpression(LuaEmitException.GetMessageText(LuaEmitException.CanNotReadMember, target.LimitType.Name, Name), ReturnType), restrictions);
						case LuaTryGetMemberReturn.ValidExpression:
							return new DynamicMetaObject(EnsureType(expr, ReturnType), restrictions);
						default:
							throw new ArgumentException("return of TryGetMember.");
					}
				}
			} // func FallbackGetMember

			public Lua Lua => lua;
		} // class LuaGetMemberBinder

		#endregion

		#region -- class LuaSetMemberBinder -------------------------------------------

		internal class LuaSetMemberBinder : SetMemberBinder, ILuaBinder
		{
			private readonly Lua lua;

			public LuaSetMemberBinder(Lua lua, string name)
			  : base(name, false)
			{
				this.lua = lua;
			} // ctor

			public override DynamicMetaObject FallbackSetMember(DynamicMetaObject target, DynamicMetaObject value, DynamicMetaObject errorSuggestion)
			{
				// defer the target
				if (!target.HasValue)
					return Defer(target);

				if (target.Value == null)
				{
					return errorSuggestion ??
						new DynamicMetaObject(
							ThrowExpression(String.Format(Properties.Resources.rsMemberNotResolved, target.LimitType.Name, Name), ReturnType),
							target.Restrictions.Merge(BindingRestrictions.GetInstanceRestriction(target.Expression, null))
						);
				}
				else
				{
					// restrictions
					var restrictions = target.Restrictions.Merge(BindingRestrictions.GetTypeRestriction(target.Expression, target.LimitType));

					// try to bind the member
					switch (LuaEmit.TrySetMember(target.Expression, target.LimitType, Name, IgnoreCase,
						(setType) => LuaEmit.ConvertWithRuntime(Lua, value.Expression, value.LimitType, setType),
						out var expr))
					{
						case LuaTrySetMemberReturn.None:
							return errorSuggestion ?? new DynamicMetaObject(ThrowExpression(LuaEmitException.GetMessageText(LuaEmitException.MemberNotFound, target.LimitType.Name, Name), ReturnType), restrictions);
						case LuaTrySetMemberReturn.NotWritable:
							return errorSuggestion ?? new DynamicMetaObject(ThrowExpression(LuaEmitException.GetMessageText(LuaEmitException.CanNotWriteMember, target.LimitType.Name, Name), ReturnType), restrictions);
						case LuaTrySetMemberReturn.ValidExpression:
							return new DynamicMetaObject(EnsureType(expr, ReturnType), restrictions.Merge(GetSimpleRestriction(value)));
						default:
							throw new ArgumentException("return of TryGetMember.");
					}
				}
			} // func FallbackSetMember

			public Lua Lua => lua;
		} // class LuaSetMemberBinder

		#endregion

		#region -- class LuaGetIndexBinder --------------------------------------------

		internal class LuaGetIndexBinder : GetIndexBinder, ILuaBinder
		{
			private readonly Lua lua;

			public LuaGetIndexBinder(Lua lua, CallInfo callInfo)
			  : base(callInfo)
			{
				this.lua = lua;
			} // ctor

			public override DynamicMetaObject FallbackGetIndex(DynamicMetaObject target, DynamicMetaObject[] indexes, DynamicMetaObject errorSuggestion)
			{
				// Defer the parameters
				if (!target.HasValue || indexes.Any(c => !c.HasValue))
					return Defer(target, indexes);

				Expression expr;
				if (target.Value == null)
				{
					if (errorSuggestion != null)
						return errorSuggestion;
					expr = ThrowExpression(Properties.Resources.rsNullReference, ReturnType);
				}
				else
					try
					{
						expr = EnsureType(LuaEmit.GetIndex(lua, target, indexes, mo => mo.Expression, mo => mo.LimitType, false), ReturnType);
					}
					catch (LuaEmitException e)
					{
						if (errorSuggestion != null)
							return errorSuggestion;
						expr = ThrowExpression(e.Message, ReturnType);
					}

				return new DynamicMetaObject(expr, GetMethodSignatureRestriction(target, indexes));
			} // func FallbackGetIndex

			public Lua Lua => lua;
		} // class LuaGetIndexBinder

		#endregion

		#region -- class LuaSetIndexBinder --------------------------------------------

		internal class LuaSetIndexBinder : SetIndexBinder, ILuaBinder
		{
			private readonly Lua lua;

			public LuaSetIndexBinder(Lua lua, CallInfo callInfo)
			  : base(callInfo)
			{
				this.lua = lua;
			} // ctor

			public override DynamicMetaObject FallbackSetIndex(DynamicMetaObject target, DynamicMetaObject[] indexes, DynamicMetaObject value, DynamicMetaObject errorSuggestion)
			{
				// Defer the parameters
				if (!target.HasValue || indexes.Any(c => !c.HasValue))
				{
					var def = new DynamicMetaObject[indexes.Length + 1];
					def[0] = target;
					Array.Copy(indexes, 0, def, 1, indexes.Length);
					return Defer(def);
				}

				Expression expr;
				if (target.Value == null)
				{
					if (errorSuggestion != null)
						return errorSuggestion;
					expr = ThrowExpression(Properties.Resources.rsNullReference, ReturnType);
				}
				else
					try
					{
						expr = EnsureType(LuaEmit.SetIndex(lua, target, indexes, value, mo => mo.Expression, mo => mo.LimitType, false), ReturnType);
					}
					catch (LuaEmitException e)
					{
						if (errorSuggestion != null)
							return errorSuggestion;
						expr = ThrowExpression(e.Message, ReturnType);
					}

				return new DynamicMetaObject(expr, GetMethodSignatureRestriction(target, indexes).Merge(GetSimpleRestriction(value)));
			} // func FallbackSetIndex

			public Lua Lua => lua;
		} // class LuaSetIndexBinder

		#endregion

		#region -- class LuaInvokeBinder ----------------------------------------------

		internal class LuaInvokeBinder : InvokeBinder, ILuaBinder
		{
			private readonly Lua lua;

			public LuaInvokeBinder(Lua lua, CallInfo callInfo)
			  : base(callInfo)
			{
				this.lua = lua;
			} // ctor

			public override DynamicMetaObject FallbackInvoke(DynamicMetaObject target, DynamicMetaObject[] args, DynamicMetaObject errorSuggestion)
			{
				// defer the target and all arguments
				if (!target.HasValue || args.Any(c => !c.HasValue))
					return Defer(target, args);

				if (target.Value == null) // Invoke on null value
				{
					return errorSuggestion ??
						new DynamicMetaObject(
							ThrowExpression(Properties.Resources.rsNilNotCallable),
							BindingRestrictions.GetInstanceRestriction(target.Expression, null)
						);
				}
				else
				{
					var restrictions = GetMethodSignatureRestriction(target, args);
					Expression expr;

					if (target.Value is Delegate invokeTarget)
					{
						var methodParameters = invokeTarget.GetMethodInfo().GetParameters();
						var parameters = (ParameterInfo[])null;
						var mi = target.LimitType.GetTypeInfo().FindDeclaredMethod("Invoke", ReflectionFlag.Public | ReflectionFlag.Instance | ReflectionFlag.NoException | ReflectionFlag.NoArguments);
						if (mi != null)
						{
							var typeParameters = mi.GetParameters();
							if (typeParameters.Length != methodParameters.Length)
							{
								parameters = new ParameterInfo[typeParameters.Length];

								// the hidden parameters are normally at the beginning
								if (parameters.Length > 0)
									Array.Copy(methodParameters, methodParameters.Length - typeParameters.Length, parameters, 0, parameters.Length);
							}
							else
								parameters = methodParameters;
						}
						else
							parameters = methodParameters;

						try
						{
							expr = EnsureType(
								LuaEmit.BindParameter(lua,
									_args => Expression.Invoke(EnsureType(target.Expression, target.LimitType), _args),
									parameters,
									CallInfo,
									args,
									mo => mo.Expression, mo => mo.LimitType, false),
								typeof(object), true
							);
						}
						catch (LuaEmitException e)
						{
							if (errorSuggestion != null)
								return errorSuggestion;
							expr = ThrowExpression(e.Message, ReturnType);
						}
					}
					else if (IsTypeInitializable(target.LimitType) && args.Length == 1 && args[0].LimitType == typeof(LuaTable))
					{
						expr = Expression.Call(EnsureType(args[0].Expression, typeof(LuaTable)), TableSetObjectMemberMethodInfo, EnsureType(target.Expression, typeof(object)), Expression.Constant(false, typeof(bool)));
					}
					else
					{
						if (errorSuggestion != null)
							return errorSuggestion;
						expr = ThrowExpression(LuaEmitException.GetMessageText(LuaEmitException.InvokeNoDelegate, target.LimitType.Name), typeof(object));
					}

					return new DynamicMetaObject(expr, restrictions);
				}
			} // func FallbackInvoke

			public Lua Lua => lua;
		} // class LuaInvokeBinder

		#endregion

		#region -- class LuaInvokeMemberBinder ----------------------------------------

		internal class LuaInvokeMemberBinder : InvokeMemberBinder, ILuaBinder
		{
			private readonly Lua lua;

			public LuaInvokeMemberBinder(Lua lua, string name, CallInfo callInfo)
			  : base(name, false, callInfo)
			{
				this.lua = lua;
			} // ctor

			public override DynamicMetaObject FallbackInvoke(DynamicMetaObject target, DynamicMetaObject[] args, DynamicMetaObject errorSuggestion)
			{
				var binder = (LuaInvokeBinder)lua.GetInvokeBinder(CallInfo);
				return binder.Defer(target, args);
			} // func FallbackInvoke

			public override DynamicMetaObject FallbackInvokeMember(DynamicMetaObject target, DynamicMetaObject[] args, DynamicMetaObject errorSuggestion)
			{
				// defer target and all arguments
				if (!target.HasValue || args.Any(c => !c.HasValue))
					return Defer(target, args);

				if (target.Value == null)
				{
					return errorSuggestion ??
						new DynamicMetaObject(
							ThrowExpression(Properties.Resources.rsNilNotCallable, ReturnType),
							target.Restrictions.Merge(BindingRestrictions.GetInstanceRestriction(target.Expression, null))
						);
				}
				else
				{
					try
					{
						var luaType = LuaType.GetType(target.LimitType);
						if (LuaEmit.TryInvokeMember(lua, luaType, target, CallInfo, args, Name, IgnoreCase, mo => mo.Expression, mo => mo.LimitType, false, out var expr))
						{
							return new DynamicMetaObject(EnsureType(expr, ReturnType), GetMethodSignatureRestriction(target, args));
						}
						else
						{
							return errorSuggestion ??
								new DynamicMetaObject
									(ThrowExpression(String.Format(Properties.Resources.rsMemberNotResolved, luaType.FullName, Name), ReturnType),
									GetMethodSignatureRestriction(target, args)
								);
						}
					}
					catch (LuaEmitException e)
					{
						return errorSuggestion ??
							new DynamicMetaObject(ThrowExpression(e.Message, ReturnType), GetMethodSignatureRestriction(target, args));
					}
				}
			} // func FallbackInvokeMember

			public Lua Lua => lua;
		} // class LuaInvokeMemberBinder

		#endregion

		#region -- class LuaBinaryOperationBinder -------------------------------------

		internal class LuaBinaryOperationBinder : BinaryOperationBinder, ILuaBinder
		{
			private readonly Lua lua;
			private readonly bool isInteger;

			public LuaBinaryOperationBinder(Lua lua, ExpressionType operation, bool isInteger)
			  : base(operation)
			{
				this.lua = lua;
				this.isInteger = isInteger;
			} // ctor

			public override DynamicMetaObject FallbackBinaryOperation(DynamicMetaObject target, DynamicMetaObject arg, DynamicMetaObject errorSuggestion)
			{
				// defer target and all arguments
				if (!target.HasValue || !arg.HasValue)
					return Defer(target, arg);

				Expression expr;
				try
				{
					expr = EnsureType(LuaEmit.BinaryOperationExpression(lua,
						isInteger && Operation == ExpressionType.Divide ? Lua.IntegerDivide : Operation,
						target.Expression, target.LimitType,
						arg.Expression, arg.LimitType, false), this.ReturnType
					);
				}
				catch (LuaEmitException e)
				{
					if (errorSuggestion != null)
						return errorSuggestion;
					expr = ThrowExpression(e.Message, this.ReturnType);
				}

				// restrictions
				var restrictions = target.Restrictions
					.Merge(arg.Restrictions)
					.Merge(GetSimpleRestriction(target))
					.Merge(GetSimpleRestriction(arg)
				);

				return new DynamicMetaObject(expr, restrictions);
			} // func FallbackBinaryOperation

			public Lua Lua => lua;
			public bool IsInteger => isInteger;
		} // class LuaBinaryOperationBinder

		#endregion

		#region -- class LuaUnaryOperationBinder --------------------------------------

		internal class LuaUnaryOperationBinder : UnaryOperationBinder, ILuaBinder
		{
			private readonly Lua lua;

			public LuaUnaryOperationBinder(Lua lua, ExpressionType operation)
			  : base(operation)
			{
				this.lua = lua;
			} // ctor

			public override DynamicMetaObject FallbackUnaryOperation(DynamicMetaObject target, DynamicMetaObject errorSuggestion)
			{
				// defer the target
				if (!target.HasValue)
					return Defer(target);

				if (target.Value == null)
				{
					return errorSuggestion ??
						new DynamicMetaObject(
							ThrowExpression(Properties.Resources.rsNilOperatorError),
							target.Restrictions.Merge(BindingRestrictions.GetInstanceRestriction(target.Expression, null))
						);
				}
				else
				{
					Expression expr;
					try
					{
						expr = EnsureType(LuaEmit.UnaryOperationExpression(lua, Operation, target.Expression, target.LimitType, false), ReturnType);
					}
					catch (LuaEmitException e)
					{
						if (errorSuggestion != null)
							return errorSuggestion;
						expr = ThrowExpression(e.Message, this.ReturnType);
					}

					var restrictions = target.Restrictions.Merge(BindingRestrictions.GetTypeRestriction(target.Expression, target.LimitType));
					return new DynamicMetaObject(expr, restrictions);
				}
			} // func FallbackUnaryOperation

			public Lua Lua => lua;
		} // class LuaUnaryOperationBinder

		#endregion

		#region -- class LuaConvertBinder ---------------------------------------------

		internal class LuaConvertBinder : ConvertBinder, ILuaBinder
		{
			private readonly Lua lua;

			public LuaConvertBinder(Lua lua, Type toType)
				: base(toType, false)
			{
				this.lua = lua;
			} // ctor

			public override DynamicMetaObject FallbackConvert(DynamicMetaObject target, DynamicMetaObject errorSuggestion)
			{
				if (!target.HasValue)
					return Defer(target);

				if (target.Value == null) // get the default value
				{
					Expression expr;
					var restrictions = target.Restrictions.Merge(BindingRestrictions.GetInstanceRestriction(target.Expression, null));

					if (Type == typeof(LuaResult)) // replace null with empty LuaResult 
						expr = Expression.Property(null, ResultEmptyPropertyInfo);
					else
						expr = Expression.Default(Type);

					return new DynamicMetaObject(EnsureType(expr, ReturnType), restrictions);
				}
				else // convert the value
				{
					var restrictions = target.Restrictions.Merge(BindingRestrictions.GetTypeRestriction(target.Expression, target.LimitType));
					if (LuaEmit.TryConvert(target.Expression, target.LimitType, Type, null, out var result))
					{
						return new DynamicMetaObject(EnsureType((Expression)result, ReturnType), restrictions);
					}
					else if (errorSuggestion == null)
					{
						if (result == null)
							throw new ArgumentNullException("expr", "LuaEmit.TryConvert does not return a expression.");
						return new DynamicMetaObject(ThrowExpression(((LuaEmitException)result).Message, ReturnType), restrictions);
					}
					else
						return errorSuggestion;
				}
			} // func FallbackConvert

			public Lua Lua => lua;
		} // class LuaConvertBinder

		#endregion

		#region -- class MemberCallInfo -----------------------------------------------

		private sealed class MemberCallInfo
		{
			private readonly string memberName;
			private readonly CallInfo ci;

			public MemberCallInfo(string memberName, CallInfo ci)
			{
				this.memberName = memberName ?? throw new ArgumentNullException(nameof(memberName));
				this.ci = ci ?? throw new ArgumentNullException(nameof(ci));
			} // ctor

			public override int GetHashCode()
				=> 0x28000000 ^ memberName.GetHashCode() ^ ci.GetHashCode();

			public override bool Equals(object obj)
				=> obj is MemberCallInfo mci && mci.memberName == memberName && mci.ci.Equals(ci);

			public override string ToString()
				=> memberName + "#" + ci.ArgumentCount.ToString();
		} // struct MemberCallInfo

		#endregion

		#region -- Binder Cache -------------------------------------------------------

		private readonly Dictionary<ExpressionType, CallSiteBinder> operationBinder = new Dictionary<ExpressionType, CallSiteBinder>();
		private readonly Dictionary<string, CallSiteBinder> getMemberBinder = new Dictionary<string, CallSiteBinder>();
		private readonly Dictionary<string, CallSiteBinder> setMemberBinder = new Dictionary<string, CallSiteBinder>();
		private readonly Dictionary<CallInfo, CallSiteBinder> getIndexBinder = new Dictionary<CallInfo, CallSiteBinder>();
		private readonly Dictionary<CallInfo, CallSiteBinder> setIndexBinder = new Dictionary<CallInfo, CallSiteBinder>();
		private readonly Dictionary<CallInfo, CallSiteBinder> invokeBinder = new Dictionary<CallInfo, CallSiteBinder>();
		private readonly Dictionary<MemberCallInfo, CallSiteBinder> invokeMemberBinder = new Dictionary<MemberCallInfo, CallSiteBinder>();
		private readonly Dictionary<Type, CallSiteBinder> convertBinder = new Dictionary<Type, CallSiteBinder>();

		private void ClearBinderCache()
		{
			lock (operationBinder)
				operationBinder.Clear();
			lock (getMemberBinder)
				getMemberBinder.Clear();
			lock (setMemberBinder)
				setMemberBinder.Clear();
			lock (getIndexBinder)
				getIndexBinder.Clear();
			lock (setIndexBinder)
				setIndexBinder.Clear();
			lock (invokeBinder)
				invokeBinder.Clear();
			lock (invokeMemberBinder)
				invokeMemberBinder.Clear();
			lock (convertBinder)
				convertBinder.Clear();
		} // proc ClearBinderCache

		/// <summary>Writes the content of the rule cache to a file. For debug-reasons.</summary>
		/// <param name="tw"></param>
		public void DumpRuleCaches(TextWriter tw)
		{
			var sep = new string('=', 66);

			var fiCache = typeof(CallSiteBinder).GetTypeInfo().FindDeclaredField("Cache", ReflectionFlag.NonPublic | ReflectionFlag.Instance);

			tw.WriteLine(sep);
			tw.WriteLine("= Operation Binders");
			DumpRuleCache(tw, operationBinder, fiCache);
			tw.WriteLine();

			tw.WriteLine(sep);
			tw.WriteLine("= GetMember Binders");
			DumpRuleCache(tw, getMemberBinder, fiCache);
			tw.WriteLine();

			tw.WriteLine(sep);
			tw.WriteLine("= SetMember Binders");
			DumpRuleCache(tw, setMemberBinder, fiCache);
			tw.WriteLine();

			tw.WriteLine(sep);
			tw.WriteLine("= Get Index Binders");
			DumpRuleCache(tw, getIndexBinder, fiCache);
			tw.WriteLine();

			tw.WriteLine(sep);
			tw.WriteLine("= Set Index Binders");
			DumpRuleCache(tw, setIndexBinder, fiCache);
			tw.WriteLine();

			tw.WriteLine(sep);
			tw.WriteLine("= Invoke Binders");
			DumpRuleCache(tw, invokeBinder, fiCache);
			tw.WriteLine();

			tw.WriteLine(sep);
			tw.WriteLine("= Invoke Member Binders");
			DumpRuleCache(tw, invokeMemberBinder, fiCache);
			tw.WriteLine();

			tw.WriteLine(sep);
			tw.WriteLine("= Convert Binders");
			DumpRuleCache(tw, convertBinder, fiCache);
			tw.WriteLine();
		} // proc DumpRuleCaches

		private void DumpRuleCache<T>(TextWriter tw, Dictionary<T, CallSiteBinder> binder, FieldInfo fiCache)
		{
			lock (binder)
			{
				foreach (var c in binder)
				{
					var k = (object)c.Key;
					var keyName = typeof(CallInfo) == typeof(T) ? "Args" + ((CallInfo)k).ArgumentCount.ToString() : k.ToString();

					// get the cache
					var cache = (Dictionary<Type, object>)fiCache.GetValue(c.Value);
					if (cache == null)
						continue;

					foreach (var a in cache)
					{
						var t = a.Value.GetType();
						var rules = (Array)t.GetTypeInfo().FindDeclaredField("_rules", ReflectionFlag.Instance | ReflectionFlag.NonPublic).GetValue(a.Value);
						tw.WriteLine(String.Format("{0}: {1}", keyName, rules.Length));
						//for (int i = 0; i < rules.Length; i++)
						//{
						//	object r = rules.GetValue(i);
						//	if (r != null)
						//		tw.WriteLine("  {0}", r.GetType());
						//}
					}
				}
			}
		} // proc DumpRuleCache

		internal CallSiteBinder GetSetMemberBinder(string name)
		{
			lock (setMemberBinder)
			{
				if (!setMemberBinder.TryGetValue(name, out var b))
					b = setMemberBinder[name] = new LuaSetMemberBinder(this, name);
				return b;
			}
		} // func GetSetMemberBinder

		internal CallSiteBinder GetGetMemberBinder(string name)
		{
			lock (getMemberBinder)
			{
				if (!getMemberBinder.TryGetValue(name, out var b))
					b = getMemberBinder[name] = new LuaGetMemberBinder(this, name);
				return b;
			}
		} // func GetGetMemberBinder

		internal CallSiteBinder GetGetIndexMember(CallInfo callInfo)
		{
			lock (getIndexBinder)
			{
				if (!getIndexBinder.TryGetValue(callInfo, out var b))
					b = getIndexBinder[callInfo] = new LuaGetIndexBinder(this, callInfo);
				return b;
			}
		} // func GetGetIndexMember

		internal CallSiteBinder GetSetIndexMember(CallInfo callInfo)
		{
			lock (setIndexBinder)
			{
				if (!setIndexBinder.TryGetValue(callInfo, out var b))
					b = setIndexBinder[callInfo] = new LuaSetIndexBinder(this, callInfo);
				return b;
			}
		} // func GetSetIndexMember

		internal CallSiteBinder GetInvokeBinder(CallInfo callInfo)
		{
			lock (invokeBinder)
			{
				if (!invokeBinder.TryGetValue(callInfo, out var b))
					b = invokeBinder[callInfo] = new LuaInvokeBinder(this, callInfo);
				return b;
			}
		} // func GetInvokeBinder

		internal CallSiteBinder GetInvokeMemberBinder(string memberName, CallInfo callInfo)
		{
			var mci = new MemberCallInfo(memberName, callInfo);
			lock (invokeMemberBinder)
			{
				if (!invokeMemberBinder.TryGetValue(mci, out var b))
					b = invokeMemberBinder[mci] = new LuaInvokeMemberBinder(this, memberName, callInfo);
				return b;
			}
		} // func GetInvokeMemberBinder

		internal CallSiteBinder GetBinaryOperationBinder(ExpressionType expressionType)
		{
			lock (operationBinder)
			{
				if (!operationBinder.TryGetValue(expressionType, out var b))
					b = operationBinder[expressionType] =
					  expressionType == IntegerDivide
						? new LuaBinaryOperationBinder(this, ExpressionType.Divide, true)
						: new LuaBinaryOperationBinder(this, expressionType, false);
				return b;
			}
		} // func GetBinaryOperationBinder

		internal CallSiteBinder GetUnaryOperationBinary(ExpressionType expressionType)
		{
			lock (operationBinder)
			{
				if (!operationBinder.TryGetValue(expressionType, out var b))
					b = operationBinder[expressionType] = new LuaUnaryOperationBinder(this, expressionType);
				return b;
			}
		} // func GetUnaryOperationBinary

		internal ConvertBinder GetConvertBinder(Type type)
		{
			lock (convertBinder)
			{
				if (!convertBinder.TryGetValue(type, out var b))
					b = convertBinder[type] = new LuaConvertBinder(this, type);
				return (ConvertBinder)b;
			}
		} // func GetConvertBinder

		#endregion

		#region -- Binder Expression Helper -------------------------------------------

		internal static Lua GetRuntime(object v)
			=> v is ILuaBinder a ? a.Lua : null;

		internal static BindingRestrictions GetMethodSignatureRestriction(DynamicMetaObject target, DynamicMetaObject[] args)
		{
			var restrictions = BindingRestrictions.Combine(args);
			if (target != null)
			{
				restrictions = restrictions
				  .Merge(target.Restrictions)
				  .Merge(GetSimpleRestriction(target));
			}

			for (var i = 0; i < args.Length; i++)
				restrictions = restrictions.Merge(GetSimpleRestriction(args[i]));

			return restrictions;
		} // func GetMethodSignatureRestriction

		internal static BindingRestrictions GetSimpleRestriction(DynamicMetaObject mo)
		{
			if (mo.HasValue && mo.Value == null)
				return BindingRestrictions.GetInstanceRestriction(mo.Expression, null);
			else
				return BindingRestrictions.GetTypeRestriction(mo.Expression, mo.LimitType);
		} // func GetSimpleRestriction

		internal static Expression ThrowExpression(string message, Type type = null)
		{
			return Expression.Throw(
				Expression.New(
					RuntimeExceptionConstructorInfo,
					Expression.Constant(message, typeof(string)),
					Expression.Constant(null, typeof(Exception))
				),
				type ?? typeof(object)
			);
		} // func ThrowExpression

		internal static Expression EnsureType(Expression expr, Type returnType, bool forResult = false)
		{
			if (expr.Type == returnType)
				return expr;
			else if (expr.Type == typeof(void))
				if (forResult)
					return Expression.Block(expr, Expression.Property(null, Lua.ResultEmptyPropertyInfo));
				else
					return Expression.Block(expr, Expression.Default(returnType));
			else
				return Expression.Convert(expr, returnType);
		} // func EnsureType

		internal static Expression EnsureType(Expression expr, Type exprType, Type returnType, bool forResult = false)
		{
			if (expr.Type != exprType)
				expr = Expression.Convert(expr, exprType);
			return EnsureType(expr, returnType, forResult);
		} // func Expression

		internal static bool IsTypeInitializable(Type type)
			=> !type.IsPrimitive && type != typeof(string);

		#endregion
	} // class Lua

	#endregion
}
