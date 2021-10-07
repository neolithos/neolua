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
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Dynamic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Neo.IronLua
{
	#region -- class LuaMemberAttribute -----------------------------------------------

	/// <summary>Marks a function or a GET property for the global namespace.</summary>
	[AttributeUsage(AttributeTargets.Property | AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
	public sealed class LuaMemberAttribute : Attribute
	{
		private readonly bool useDefault;
		private readonly bool isMethod;
		private readonly string name;

		/// <summary></summary>
		/// <param name="isMethod"></param>
		public LuaMemberAttribute(bool isMethod = false)
		{
			this.useDefault = true;
			this.isMethod = isMethod;
			this.name = "__default";
		} // ctor

		/// <summary>Marks global Members, they act normally as library</summary>
		/// <param name="name"></param>
		/// <param name="isMethod"></param>
		public LuaMemberAttribute(string name, bool isMethod = false)
		{
			this.useDefault = false;
			this.isMethod = isMethod;
			this.name = name;
		} // ctor

		/// <summary></summary>
		/// <returns></returns>
		public override string ToString()
			=> "LuaMember: " + LuaName;

		/// <summary>Use the name of the  method.</summary>
		public bool UseDefault => useDefault;
		/// <summary>Register this member as method.</summary>
		public bool IsMethod => isMethod;
		/// <summary>Global name of the function.</summary>
		public string LuaName => name;
	} // class LuaLibraryAttribute

	#endregion

	#region -- class LuaTable ---------------------------------------------------------

	/// <summary>Implementation of a the lua table. A lua table is a combination 
	/// of a hash dictionary, a string dictionary and a array list.</summary>
	public class LuaTable : IDynamicMetaObjectProvider, INotifyPropertyChanged, IDictionary<object, object>
	{
		/// <summary>Member name of the metatable</summary>
		public const string csMetaTable = "__metatable";
		private const int hiddenMemberCount = 1; // do not enumerate __metatable

		private const int indexNotFound = -1;
		private const int removedIndex = -3;

		#region -- class LuaTableMetaObject -----------------------------------------------

		private sealed class LuaTableMetaObject : DynamicMetaObject
		{
			#region -- Ctor/Dtor ------------------------------------------------------

			public LuaTableMetaObject(LuaTable value, Expression expression)
				: base(expression, BindingRestrictions.Empty, value)
			{
			} // ctor

			#endregion

			#region -- Bind Helper ----------------------------------------------------------

			private DynamicMetaObject BindBinaryCall(BinaryOperationBinder binder, MethodInfo mi, DynamicMetaObject arg)
			{
				return new DynamicMetaObject(
					Lua.EnsureType(
						BinaryOperationCall(binder, mi, arg),
						binder.ReturnType
					),
					GetBinaryRestrictions(arg)
				);
			} // func BindBinaryCall

			private Expression BinaryOperationCall(BinaryOperationBinder binder, MethodInfo mi, DynamicMetaObject arg)
			{
				return Expression.Call(
					Lua.EnsureType(Expression, typeof(LuaTable)),
					mi,
					Lua.EnsureType(arg.Expression, arg.LimitType, typeof(object))
				);
			} // func BinaryOperationCall

			private DynamicMetaObject UnaryOperationCall(UnaryOperationBinder binder, MethodInfo mi)
			{
				return new DynamicMetaObject(
					Lua.EnsureType(Expression.Call(Lua.EnsureType(Expression, typeof(LuaTable)), mi), binder.ReturnType),
					GetLuaTableRestriction()
				);
			} // func UnaryOperationCall

			private BindingRestrictions GetBinaryRestrictions(DynamicMetaObject arg)
			{
				return GetLuaTableRestriction().Merge(Lua.GetSimpleRestriction(arg));
			} // func GetBinaryRestrictions

			private BindingRestrictions GetLuaTableRestriction()
			{
				return BindingRestrictions.GetExpressionRestriction(Expression.TypeIs(Expression, typeof(LuaTable)));
			} // func GetLuaTableRestriction

			private Expression CreateSetExpresion(object binder, DynamicMetaObject value, Type typeConvertTo, ref BindingRestrictions restrictions)
			{
				Type typeFrom;
				Expression expr;
				if (value.LimitType == typeof(LuaResult))
				{
					restrictions = restrictions.Merge(BindingRestrictions.GetExpressionRestriction(Expression.TypeEqual(value.Expression, typeof(LuaResult))));
					typeFrom = typeof(object);
					expr = LuaEmit.GetResultExpression(value.Expression, 0);
				}
				else
				{
					restrictions = restrictions.Merge(BindingRestrictions.GetExpressionRestriction(Expression.Not(Expression.TypeEqual(value.Expression, typeof(LuaResult)))));
					typeFrom = value.LimitType;
					expr = value.Expression;
				}
				if (typeConvertTo == null)
					return Lua.EnsureType(expr, typeof(object));
				else
				{
					try
					{
						return LuaEmit.ConvertWithRuntime(Lua.GetRuntime(binder), expr, typeFrom, typeConvertTo);
					}
					catch (LuaEmitException e)
					{
						return Lua.ThrowExpression(e.Message, typeConvertTo);
					}
				}
			} // func CreateSetExpresion

			private static BindingRestrictions NoIndexKeyRestriction(BindingRestrictions restrictions, DynamicMetaObject arg)
			{
				restrictions = restrictions.Merge(BindingRestrictions.GetExpressionRestriction(
					Expression.Not(
						Expression.OrElse(
						Expression.OrElse(
						Expression.OrElse(
						Expression.OrElse(
						Expression.OrElse(
							Expression.TypeEqual(arg.Expression, typeof(string)),
							Expression.TypeEqual(arg.Expression, typeof(int))
							),
							Expression.TypeEqual(arg.Expression, typeof(sbyte))
							),
							Expression.TypeEqual(arg.Expression, typeof(byte))
							),
							Expression.TypeEqual(arg.Expression, typeof(short))
							),
							Expression.TypeEqual(arg.Expression, typeof(ushort))
						)
					)
				));
				return restrictions;
			} // func NoIndexKeyRestriction

			private static Expression ConvertToIndexKey(DynamicMetaObject arg)
			{
				if (arg.LimitType == typeof(sbyte)
					|| arg.LimitType == typeof(byte)
					|| arg.LimitType == typeof(short)
					|| arg.LimitType == typeof(ushort)
					|| arg.LimitType == typeof(uint)
					|| arg.LimitType == typeof(long)
					|| arg.LimitType == typeof(ulong))
					return Expression.Convert(Lua.EnsureType(arg.Expression, arg.LimitType), typeof(int));
				else if (arg.LimitType == typeof(int))
					return Lua.EnsureType(arg.Expression, typeof(int));
				else
					return Lua.ThrowExpression(LuaEmitException.GetMessageText(LuaEmitException.ConversationNotDefined, arg.LimitType.Name, "indexKey"), typeof(int));
			} // func ConvertToIndexKey

			#endregion

			#region -- BindBinaryOperation --------------------------------------------------

			public override DynamicMetaObject BindBinaryOperation(BinaryOperationBinder binder, DynamicMetaObject arg)
			{
				switch (binder.Operation)
				{
					case ExpressionType.Add:
						return BindBinaryCall(binder, Lua.TableAddMethodInfo, arg);
					case ExpressionType.Subtract:
						return BindBinaryCall(binder, Lua.TableSubMethodInfo, arg);
					case ExpressionType.Multiply:
						return BindBinaryCall(binder, Lua.TableMulMethodInfo, arg);
					case ExpressionType.Divide:
						{
							var luaOpBinder = binder as Lua.LuaBinaryOperationBinder;
							if (luaOpBinder != null && luaOpBinder.IsInteger)
								return BindBinaryCall(binder, Lua.TableIDivMethodInfo, arg);
							else
								return BindBinaryCall(binder, Lua.TableDivMethodInfo, arg);
						}
					case ExpressionType.Modulo:
						return BindBinaryCall(binder, Lua.TableModMethodInfo, arg);
					case ExpressionType.Power:
						return BindBinaryCall(binder, Lua.TablePowMethodInfo, arg);
					case ExpressionType.And:
						return BindBinaryCall(binder, Lua.TableBAndMethodInfo, arg);
					case ExpressionType.Or:
						return BindBinaryCall(binder, Lua.TableBOrMethodInfo, arg);
					case ExpressionType.ExclusiveOr:
						return BindBinaryCall(binder, Lua.TableBXOrMethodInfo, arg);
					case ExpressionType.LeftShift:
						return BindBinaryCall(binder, Lua.TableShlMethodInfo, arg);
					case ExpressionType.RightShift:
						return BindBinaryCall(binder, Lua.TableShrMethodInfo, arg);
					case ExpressionType.Equal:
						return new DynamicMetaObject(Lua.EnsureType(BinaryOperationCall(binder, Lua.TableEqualMethodInfo, arg), binder.ReturnType), GetBinaryRestrictions(arg));
					case ExpressionType.NotEqual:
						return new DynamicMetaObject(Lua.EnsureType(Expression.Not(BinaryOperationCall(binder, Lua.TableEqualMethodInfo, arg)), binder.ReturnType), GetBinaryRestrictions(arg));
					case ExpressionType.LessThan:
						return new DynamicMetaObject(Lua.EnsureType(BinaryOperationCall(binder, Lua.TableLessThanMethodInfo, arg), binder.ReturnType), GetBinaryRestrictions(arg));
					case ExpressionType.LessThanOrEqual:
						return new DynamicMetaObject(Lua.EnsureType(BinaryOperationCall(binder, Lua.TableLessEqualMethodInfo, arg), binder.ReturnType), GetBinaryRestrictions(arg));
					case ExpressionType.GreaterThan:
						return new DynamicMetaObject(Lua.EnsureType(Expression.Not(BinaryOperationCall(binder, Lua.TableLessEqualMethodInfo, arg)), binder.ReturnType), GetBinaryRestrictions(arg));
					case ExpressionType.GreaterThanOrEqual:
						return new DynamicMetaObject(Lua.EnsureType(Expression.Not(BinaryOperationCall(binder, Lua.TableLessThanMethodInfo, arg)), binder.ReturnType), GetBinaryRestrictions(arg));
				}
				return base.BindBinaryOperation(binder, arg);
			} // func BindBinaryOperation

			#endregion

			#region -- BindUnaryOperation----------------------------------------------------

			public override DynamicMetaObject BindUnaryOperation(UnaryOperationBinder binder)
			{
				switch (binder.Operation)
				{
					case ExpressionType.Negate:
						return UnaryOperationCall(binder, Lua.TableUnMinusMethodInfo);
					case ExpressionType.OnesComplement:
						return UnaryOperationCall(binder, Lua.TableBNotMethodInfo);
				}
				return base.BindUnaryOperation(binder);
			} // func BindUnaryOperation

			#endregion

			#region -- BindSetIndex ---------------------------------------------------------

			public override DynamicMetaObject BindSetIndex(SetIndexBinder binder, DynamicMetaObject[] indexes, DynamicMetaObject value)
			{
				if (Array.Exists(indexes, mo => !mo.HasValue))
					return binder.Defer(indexes);
				if (!value.HasValue)
					return binder.Defer(value);

				// Restriction
				BindingRestrictions restrictions = GetLuaTableRestriction();

				// create the set expression
				Expression expr;
				Expression exprSet = CreateSetExpresion(binder, value, null, ref restrictions);

				// create the call
				if (indexes.Length == 1)
				{
					var arg = indexes[0];

					if (arg.Value == null)
					{
						expr = Lua.ThrowExpression(Properties.Resources.rsTableKeyNotNullable, typeof(object));
						restrictions = restrictions.Merge(BindingRestrictions.GetInstanceRestriction(arg.Expression, null));
					}
					else if (IsIndexKey(arg.LimitType)) // integer access
					{
						expr = Expression.Call(Lua.EnsureType(Expression, typeof(LuaTable)), Lua.TableSetValueKeyIntMethodInfo,
							ConvertToIndexKey(arg),
							exprSet,
							Expression.Constant(false)
						);
						restrictions = restrictions.Merge(BindingRestrictions.GetTypeRestriction(arg.Expression, arg.LimitType));
					}
					else if (arg.LimitType == typeof(string))
					{
						expr = Expression.Call(Lua.EnsureType(Expression, typeof(LuaTable)), Lua.TableSetValueKeyStringMethodInfo,
							Lua.EnsureType(arg.Expression, typeof(string)),
							exprSet,
							Expression.Constant(false),
							Expression.Constant(false)
						);
						restrictions = restrictions.Merge(BindingRestrictions.GetTypeRestriction(arg.Expression, typeof(string)));
					}
					else
					{
						expr = Expression.Call(Lua.EnsureType(Expression, typeof(LuaTable)), Lua.TableSetValueKeyObjectMethodInfo,
							Lua.EnsureType(arg.Expression, typeof(object)),
							exprSet,
							Expression.Constant(false)
						);
						restrictions = NoIndexKeyRestriction(restrictions, arg);
					}
				}
				else
				{
					expr = Expression.Call(Lua.EnsureType(Expression, typeof(LuaTable)), Lua.TableSetValueKeyListMethodInfo,
						Expression.NewArrayInit(typeof(object), from i in indexes select Lua.EnsureType(i.Expression, typeof(object))),
						exprSet,
						Expression.Constant(false)
					);

					restrictions = restrictions.Merge(Lua.GetMethodSignatureRestriction(null, indexes));
				}

				return new DynamicMetaObject(expr, restrictions);
			} // func BindSetIndex

			#endregion

			#region -- BindGetIndex ---------------------------------------------------------

			public override DynamicMetaObject BindGetIndex(GetIndexBinder binder, DynamicMetaObject[] indexes)
			{
				if (Array.Exists(indexes, mo => !mo.HasValue))
					return binder.Defer(indexes);

				BindingRestrictions restrictions = GetLuaTableRestriction();
				Expression expr;

				if (indexes.Length == 1)
				{
					var arg = indexes[0];

					if (arg.Value == null)
					{
						expr = Expression.Default(typeof(object));
						restrictions = restrictions.Merge(BindingRestrictions.GetInstanceRestriction(arg.Expression, null));
					}
					else if (IsIndexKey(arg.LimitType)) // integer access
					{
						expr = Expression.Call(Lua.EnsureType(Expression, typeof(LuaTable)), Lua.TableGetValueKeyIntMethodInfo,
							ConvertToIndexKey(arg),
							Expression.Constant(false)
						);
						restrictions = restrictions.Merge(BindingRestrictions.GetTypeRestriction(arg.Expression, arg.LimitType));
					}
					else if (arg.LimitType == typeof(string))
					{
						expr = Expression.Call(Lua.EnsureType(Expression, typeof(LuaTable)), Lua.TableGetValueKeyStringMethodInfo,
							Lua.EnsureType(arg.Expression, typeof(string)),
							Expression.Constant(false),
							Expression.Constant(false)
						);
						restrictions = restrictions.Merge(BindingRestrictions.GetTypeRestriction(arg.Expression, typeof(string)));
					}
					else
					{
						expr = Expression.Call(Lua.EnsureType(Expression, typeof(LuaTable)), Lua.TableGetValueKeyObjectMethodInfo,
							Lua.EnsureType(arg.Expression, typeof(object)),
							Expression.Constant(false)
						);
						restrictions = NoIndexKeyRestriction(restrictions, arg);
					}
				}
				else
				{
					expr = Expression.Call(Lua.EnsureType(Expression, typeof(LuaTable)), Lua.TableGetValueKeyListMethodInfo,
						Expression.NewArrayInit(typeof(object), from i in indexes select Lua.EnsureType(i.Expression, typeof(object))),
						Expression.Constant(false)
					);

					restrictions = restrictions.Merge(Lua.GetMethodSignatureRestriction(null, indexes));
				}

				return new DynamicMetaObject(expr, restrictions);
			} // func BindGetIndex

			#endregion

			#region -- BindSetMember, BindGetMember -----------------------------------------

			private MemberExpression GetMemberValueAccess(int iEntryIndex, ref BindingRestrictions restrictions)
			{
				// restrict on correct type (deletion, new define is possible)
				restrictions = restrictions.Merge(BindingRestrictions.GetTypeRestriction(Expression, LimitType));

				// return the expression (entries[i].value)
				return Expression.Field(
					Expression.ArrayAccess(
						Expression.Field(Lua.EnsureType(Expression, typeof(LuaTable)), Lua.TableEntriesFieldInfo),
						Expression.Constant(iEntryIndex)
					),
					Lua.TableEntryValueFieldInfo
				);
			} // func GetMemberValueAccess

			private Expression GetDirectMemberAccess(int iEntryIndex, LuaTablePropertyDefine pd, bool generateRestriction, ref BindingRestrictions restrictions)
			{
				// static property
				var isStatic = pd.PropertyInfo.GetMethod.IsStatic;

				// generate restriction
				if (generateRestriction)
					restrictions = restrictions.Merge(BindingRestrictions.GetExpressionRestriction(Expression.TypeIs(Expression, pd.PropertyInfo.DeclaringType)));

				// access the property (t.value)
				return Expression.Property(
					isStatic ? null : Lua.EnsureType(Expression, pd.PropertyInfo.DeclaringType),
					pd.PropertyInfo
				);
			} // func GetDirectMemberAccess

			public override DynamicMetaObject BindSetMember(SetMemberBinder binder, DynamicMetaObject value)
			{
				if (!value.HasValue)
					return binder.Defer(value);

				LuaTable t = (LuaTable)Value;

				// search for the member
				int iEntryIndex = t.FindKey(binder.Name, GetMemberHashCode(binder.Name), binder.IgnoreCase ? compareStringIgnoreCase : compareString);
				if (iEntryIndex >= 0 && iEntryIndex < t.classDefinition.Count) // is the key a class member
				{
					Expression expr = null;
					Expression exprMember = null;
					BindingRestrictions restrictions = BindingRestrictions.Empty;
					var define = t.classDefinition[iEntryIndex];

					switch (define.mode)
					{
						case LuaTableDefineMode.Init:
						case LuaTableDefineMode.Default:
							// tmp = value; 
							// if (entries[i].value != tmp2)
							//   entries[i].value = tmp;
							//   OnPropertyChanged();
							exprMember = GetMemberValueAccess(iEntryIndex, ref restrictions);
							ParameterExpression varTmp = Expression.Variable(typeof(object), "tmp");
							expr = Expression.Block(new ParameterExpression[] { varTmp },
								Expression.Assign(varTmp, CreateSetExpresion(binder, value, null, ref restrictions)),
								Expression.IfThen(
									Expression.Not(Expression.Call(Lua.ObjectEqualsMethodInfo, exprMember, varTmp)),
									Expression.Block(
										Expression.Assign(exprMember, varTmp),
										Expression.Call(Lua.EnsureType(Expression, typeof(LuaTable)), Lua.TablePropertyChangedMethodInfo, Expression.Constant(binder.Name))
									)
								),
								varTmp
							);
							break;
						case LuaTableDefineMode.Direct:
							exprMember = GetDirectMemberAccess(iEntryIndex, (LuaTablePropertyDefine)define, true, ref restrictions);
							expr = Lua.EnsureType(Expression.Assign(exprMember, CreateSetExpresion(binder, value, exprMember.Type, ref restrictions)), typeof(object));
							break;
						default:
							throw new ArgumentOutOfRangeException();
					}

					return new DynamicMetaObject(expr, restrictions);
				}
				else
				{
					BindingRestrictions restrictions = GetLuaTableRestriction();
					Expression expr = Expression.Call(
						Lua.EnsureType(Expression, typeof(LuaTable)),
						Lua.TableSetValueKeyStringMethodInfo,
						Expression.Constant(binder.Name),
						CreateSetExpresion(binder, value, null, ref restrictions),
						Expression.Constant(binder.IgnoreCase),
						Expression.Constant(false)
					);
					return new DynamicMetaObject(expr, restrictions);
				}
			} // proc BindSetMember

			public override DynamicMetaObject BindGetMember(GetMemberBinder binder)
			{
				LuaTable t = (LuaTable)Value;

				// search for the member
				int iEntryIndex = t.FindKey(binder.Name, GetMemberHashCode(binder.Name), binder.IgnoreCase ? compareStringIgnoreCase : compareString);
				if (iEntryIndex >= 0 && iEntryIndex < t.classDefinition.Count) // is the key a class member
				{
					Expression expr = null;
					var restrictions = BindingRestrictions.Empty;
					var define = t.classDefinition[iEntryIndex];

					switch (define.mode)
					{
						case LuaTableDefineMode.Init:
							expr = GetMemberValueAccess(iEntryIndex, ref restrictions);
							break;
						case LuaTableDefineMode.Default:
							expr = Expression.Coalesce(
								GetMemberValueAccess(iEntryIndex, ref restrictions),
								Lua.EnsureType(GetDirectMemberAccess(iEntryIndex, (LuaTablePropertyDefine)define, false, ref restrictions), typeof(object))
							);
							break;
						case LuaTableDefineMode.Direct:
							expr = Lua.EnsureType(GetDirectMemberAccess(iEntryIndex, (LuaTablePropertyDefine)define, true, ref restrictions), typeof(object));
							break;
						default:
							throw new ArgumentOutOfRangeException();
					}
					return new DynamicMetaObject(expr, restrictions);
				}
				else // do the call to a normal member
				{
					Expression expr = Expression.Call(
						Lua.EnsureType(Expression, typeof(LuaTable)),
						Lua.TableGetValueKeyStringMethodInfo,
						Expression.Constant(binder.Name),
						Expression.Constant(binder.IgnoreCase),
						Expression.Constant(false)
					);

					return new DynamicMetaObject(expr, GetLuaTableRestriction());
				}
			} // func BindGetMember

			#endregion

			#region -- BindInvoke -----------------------------------------------------------

			public override DynamicMetaObject BindInvoke(InvokeBinder binder, DynamicMetaObject[] args)
			{
				return new DynamicMetaObject(
					Lua.EnsureType(
						Expression.Call(
							Lua.EnsureType(Expression, typeof(LuaTable)),
							Lua.TableCallMethodInfo,
							Expression.NewArrayInit(typeof(object), from a in args select Lua.EnsureType(a.Expression, typeof(object)))
						),
						binder.ReturnType,
						true
					),
					GetLuaTableRestriction().Merge(Lua.GetMethodSignatureRestriction(null, args))
				);
			} // func BindInvoke 

			#endregion

			#region -- BindInvokeMember -----------------------------------------------------

			private Expression GetDynamicCallExpression(LuaTable t, InvokeMemberBinder binder, ParameterExpression variableMethodExpresion, bool isDynamicCall, bool isMemberCall, DynamicMetaObject[] args)
			{
				var lua = Lua.GetRuntime(binder);
				var hiddenArguments = isMemberCall || (!isDynamicCall && lua != null) ? 2 : 1;
				var expressionArgs = new Expression[args.Length + hiddenArguments];

				// create argument set
				expressionArgs[0] = variableMethodExpresion;
				if (hiddenArguments > 1)
					expressionArgs[1] = Expression;
				for (var i = 0; i < args.Length; i++)
					expressionArgs[hiddenArguments + i] = args[i].Expression;

				return DynamicExpression.Dynamic(
					lua == null ?
						t.GetInvokeBinder(binder.CallInfo) :
						lua.GetInvokeBinder(binder.CallInfo),
					binder.ReturnType,
					expressionArgs
				);
			} // func GetDynamicCallExpression

			public override DynamicMetaObject BindInvokeMember(InvokeMemberBinder binder, DynamicMetaObject[] args)
			{
				var t = (LuaTable)Value;
				var restrictions = GetLuaTableRestriction();

				var variableMethodExpresion = Expression.Variable(typeof(object), "method");

				// generate:
				// switch(GetCallMethod(binder.Name, binder.IgnoreCase, false, out method)
				// ...
				var expr = Expression.Block(
					new ParameterExpression[] { variableMethodExpresion },
					Expression.Switch(
						Expression.Call(
							Lua.EnsureType(Expression, typeof(LuaTable)), Lua.TableGetCallMemberMethodInfo,
							Expression.Constant(binder.Name),
							Expression.Constant(binder.IgnoreCase),
							Expression.Constant(false),
							variableMethodExpresion
						),
						variableMethodExpresion,

						Expression.SwitchCase(Lua.ThrowExpression(String.Format(Properties.Resources.rsMemberNotResolved, "table", binder.Name), typeof(object)), Expression.Constant(CallMethod.Nil)),

						Expression.SwitchCase(GetDynamicCallExpression(t, binder, variableMethodExpresion, false, false, args), Expression.Constant(CallMethod.Delegate)),
						Expression.SwitchCase(GetDynamicCallExpression(t, binder, variableMethodExpresion, false, true, args), Expression.Constant(CallMethod.DelegateMember)),
						Expression.SwitchCase(GetDynamicCallExpression(t, binder, variableMethodExpresion, true, false, args), Expression.Constant(CallMethod.Dynamic)),
						Expression.SwitchCase(GetDynamicCallExpression(t, binder, variableMethodExpresion, true, true, args), Expression.Constant(CallMethod.DynamicMember))
					)
				);
				return new DynamicMetaObject(expr, restrictions.Merge(Lua.GetMethodSignatureRestriction(null, args)));
			} // BindInvokeMember

			#endregion

			#region -- BindConvert ----------------------------------------------------------

			public override DynamicMetaObject BindConvert(ConvertBinder binder)
			{
				// Automatic convert to a special type, only for classes and structure
				var typeInfo = binder.Type.GetTypeInfo();
				if (!typeInfo.IsPrimitive && // no primitiv
					!typeInfo.IsAssignableFrom(Value.GetType().GetTypeInfo()) && // not assignable by defaut
					binder.Type != typeof(LuaResult)) // no result
				{
					return new DynamicMetaObject(
						Lua.EnsureType(
							Expression.Call(Lua.EnsureType(Expression, typeof(LuaTable)), Lua.TableSetObjectMemberMethodInfo, Lua.EnsureType(Expression.New(binder.Type), typeof(object))),
							binder.ReturnType),
						GetLuaTableRestriction());
				}
				return base.BindConvert(binder);
			} // func BindConvert

			#endregion

			/// <summary></summary>
			/// <returns></returns>
			public override IEnumerable<string> GetDynamicMemberNames()
				=> ((LuaTable)Value).GetKeys();
		} // class LuaTableMetaObject

		#endregion

		#region -- class ArrayImplementation --------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary>Proxy for a interface to the array part of a table.</summary>
		private sealed class ArrayImplementation : IList<object>, IList, IReadOnlyList<object>
		{
			private readonly LuaTable table;

			#region -- Ctor/Dtor --------------------------------------------------------

			public ArrayImplementation(LuaTable table)
			{
				this.table = table;
			} // ctor

			#endregion

			#region -- IList<object>, IList, ICollection<object>, ICollection -----------

			public int Add(object value)
				=> table.ArrayOnlyAdd(value);

			void ICollection<object>.Add(object item)
				=> table.ArrayOnlyAdd(item);

			public void Insert(int index, object item)
				=> table.ArrayOnlyInsert(index, item);

			public bool Remove(object item)
				=> table.ArrayOnlyRemove(item);

			void IList.Remove(object value)
				=> table.ArrayOnlyRemove(value);

			public void RemoveAt(int index)
				=> table.ArrayOnlyRemoveAt(index);

			public void Clear()
				=> table.ArrayOnlyClear();

			public bool Contains(object item)
				=> table.ArrayOnlyIndexOf(item) >= 0;

			public int IndexOf(object item)
				=> table.ArrayOnlyIndexOf(item);

			public void CopyTo(Array array, int index)
				=> table.ArrayOnlyCopyTo(array, index);

			public void CopyTo(object[] array, int arrayIndex)
				=> table.ArrayOnlyCopyTo(array, arrayIndex);

			public int Count => table.arrayLength;
			public bool IsReadOnly => true;
			public bool IsSynchronized => false;
			public bool IsFixedSize => false;
			public object SyncRoot => null;

			public object this[int index] { get { return table.ArrayOnlyGetIndex(index); } set { table.ArrayOnlySetIndex(index, value); } }

			#endregion

			#region -- IEnumerable<object> ----------------------------------------------

			public IEnumerator<object> GetEnumerator()
				=> table.ArrayOnlyGetEnumerator();

			IEnumerator IEnumerable.GetEnumerator()
				=> table.ArrayOnlyGetEnumerator();

			#endregion
		} // class ArrayImplementation

		#endregion

		#region -- class MemberImplementation -------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary>Proxy for a interface to the members of a table.</summary>
		private sealed class MemberImplementation : IDictionary<string, object>
		{
			private readonly LuaTable table;

			#region -- Ctor/Dtor --------------------------------------------------------

			public MemberImplementation(LuaTable table)
			{
				this.table = table;
			} // ctor

			#endregion

			#region -- IDictionary<string, object> members ------------------------------

			#region -- class LuaTableStringKeyCollection --------------------------------

			/// <summary></summary>
			public sealed class LuaTableStringKeyCollection : ICollection<string>
			{
				private readonly LuaTable t;

				internal LuaTableStringKeyCollection(LuaTable t)
				{
					this.t = t;
				} // ctor

				/// <summary></summary>
				/// <param name="item"></param>
				/// <returns></returns>
				public bool Contains(string item)
					=> t.ContainsMember(item);

				/// <summary></summary>
				/// <param name="array"></param>
				/// <param name="arrayIndex"></param>
				public void CopyTo(string[] array, int arrayIndex)
				{
					if (arrayIndex < 0 || arrayIndex + Count > array.Length)
						throw new ArgumentOutOfRangeException();

					for (var i = hiddenMemberCount; i < t.entries.Length; i++)
					{
						if (t.entries[i].key is string memberName)
							array[arrayIndex++] = memberName;
					}
				} // proc CopyTo

				/// <summary></summary>
				/// <returns></returns>
				public IEnumerator<string> GetEnumerator()
				{
					var version = t.version;
					for (var i = hiddenMemberCount; i < t.entries.Length; i++)
					{
						if (version != t.version)
							throw new InvalidOperationException("table changed");

						if (t.entries[i].key is string memberName)
							yield return memberName;
					}
				} // func GetEnumerator

				IEnumerator IEnumerable.GetEnumerator()
					=> GetEnumerator();

				void ICollection<string>.Add(string item)
					=> throw new NotSupportedException();

				bool ICollection<string>.Remove(string item)
					=> throw new NotSupportedException();
				void ICollection<string>.Clear()
					=> throw new NotSupportedException();

				/// <summary></summary>
				public int Count => t.memberCount - hiddenMemberCount;
				/// <summary>Always true</summary>
				public bool IsReadOnly => true;
			} // class LuaTableStringKeyCollection

			#endregion

			#region -- class LuaTableStringValueCollection ------------------------------

			///////////////////////////////////////////////////////////////////////////////
			/// <summary></summary>
			public sealed class LuaTableStringValueCollection : ICollection<object>
			{
				private readonly LuaTable t;

				internal LuaTableStringValueCollection(LuaTable t)
				{
					this.t = t;
				} // ctor

				/// <summary></summary>
				/// <param name="value"></param>
				/// <returns></returns>
				public bool Contains(object value)
				{
					for (var i = 0; i < t.entries.Length; i++)
					{
						if (comparerObject.Equals(t.entries[i].value))
							return true;
					}
					return false;
				} // func Contains

				/// <summary></summary>
				/// <param name="array"></param>
				/// <param name="arrayIndex"></param>
				public void CopyTo(object[] array, int arrayIndex)
				{
					if (arrayIndex < 0 || arrayIndex + Count > array.Length)
						throw new ArgumentOutOfRangeException();

					for (var i = hiddenMemberCount; i < t.entries.Length; i++)
					{
						if (t.entries[i].key is string)
							array[arrayIndex++] = t.entries[i].value;
					}
				} // proc CopyTo

				/// <summary></summary>
				/// <returns></returns>
				public IEnumerator<object> GetEnumerator()
				{
					var version = t.version;
					for (var i = hiddenMemberCount; i < t.entries.Length; i++)
					{
						if (version != t.version)
							throw new InvalidOperationException("table changed");

						if (t.entries[i].key is string)
							yield return t.entries[i].value;
					}
				} // func GetEnumerator

				IEnumerator IEnumerable.GetEnumerator()
					=> GetEnumerator();

				void ICollection<object>.Add(object item)
					=> throw new NotSupportedException();
				bool ICollection<object>.Remove(object item)
					=> throw new NotSupportedException();

				void ICollection<object>.Clear()
					=> throw new NotSupportedException();

				/// <summary></summary>
				public int Count => t.memberCount - hiddenMemberCount;
				/// <summary>Always true</summary>
				public bool IsReadOnly => true;
			} // class LuaTableStringValueCollection

			#endregion

			private LuaTableStringKeyCollection stringKeyCollection = null;
			private LuaTableStringValueCollection stringValueCollection = null;

			public void Add(string key, object value)
				=> table.SetMemberValue(key, value, false, true);

			public bool TryGetValue(string key, out object value)
				=> (value = table.GetMemberValue(key, false, true)) != null;

			public bool ContainsKey(string key)
				=> table.ContainsMember(key, false);

			public bool Remove(string key)
			{
				if (key == null)
					throw new ArgumentNullException(Properties.Resources.rsTableKeyNotNullable);

				return table.SetMemberValueIntern(key, null, false, true, false, false) == removedIndex;
			} // func Remove

			public ICollection<string> Keys
			{
				get
				{
					if (stringKeyCollection == null)
						stringKeyCollection = new LuaTableStringKeyCollection(table);
					return stringKeyCollection;
				}
			} // prop Keys

			public ICollection<object> Values
			{
				get
				{
					if (stringValueCollection == null)
						stringValueCollection = new LuaTableStringValueCollection(table);
					return stringValueCollection;
				}
			} // prop Values

			public object this[string key]
			{
				get { return table.GetMemberValue(key, false, true); }
				set { table.SetMemberValue(key, value, false, true); }
			} // prop this

			#endregion

			#region -- ICollection<KeyValuePair<string, object>> members ----------------

			public void Add(KeyValuePair<string, object> item)
			{
				if (item.Key == null)
					throw new ArgumentNullException(Properties.Resources.rsTableKeyNotNullable);

				table.SetMemberValueIntern(item.Key, item.Value, false, false, true, false);
			} // proc Add

			public bool Remove(KeyValuePair<string, object> item)
			{
				if (item.Key == null)
					throw new ArgumentNullException(Properties.Resources.rsTableKeyNotNullable);

				return table.SetMemberValueIntern(item.Key, null, false, true, false, false) == removedIndex;
			} // func Remove

			public void Clear()
				=> table.ClearMembers();

			public bool Contains(KeyValuePair<string, object> item)
				=> table.ContainsMember(item.Key);

			public void CopyTo(KeyValuePair<string, object>[] array, int arrayIndex)
				=> table.MembersCopyTo(array, arrayIndex);

			public int Count => table.memberCount - hiddenMemberCount;
			public bool IsReadOnly => false;

			#endregion

			#region -- IEnumerator<KeyValuePair<string, object>> members ----------------

			public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
				=> table.MembersGetEnumerator();

			IEnumerator IEnumerable.GetEnumerator()
				=> table.MembersGetEnumerator();

			#endregion
		} // MemberImplementation

		#endregion

		#region -- struct LuaTableEntry -------------------------------------------------

		private struct LuaTableEntry
		{
			public int hashCode;
			public object key;
			public object value;
			public bool isMethod;

			/// <summary>points to the next entry with the same hashcode</summary>
			public int nextHash;

			public override string ToString()
				=> hashCode == -1 ? String.Format("_empty_ next: {0}", nextHash) : String.Format("key: {0}; value: {1}; next:{2}", key ?? "null", value ?? "null", nextHash);

			public bool SetValue(object newValue, bool markAsMethod)
			{
				if (comparerObject.Equals(newValue, value) && markAsMethod == isMethod)
					return false;

				value = newValue;
				isMethod = markAsMethod;
				return true;
			} // proc SetValue
		} // struct LuaTableEntry

		#endregion

		#region -- enum LuaTableDefineMode ----------------------------------------------

		/// <summary></summary>
		private enum LuaTableDefineMode
		{
			Init,
			Default,
			Direct
		} // enum LuaTableDefineMode

		#endregion

		#region -- class LuaTableDefine -------------------------------------------------

		/// <summary></summary>
		private abstract class LuaTableDefine
		{
			// fixed fields for fast (hopefully) access
			public LuaTableDefineMode mode;
			public Func<LuaTable, object> getValue;
			public Action<LuaTable, object> setValue;

			private readonly LuaMemberAttribute info;

			protected LuaTableDefine(LuaMemberAttribute info)
			{
				this.info = info;
			} // ctor

			/// <summary>Initial value for the table creation.</summary>
			public abstract object GetInitialValue(LuaTable table);

			public void CollectMember(List<LuaCollectedMember> collected)
				=> CollectMember(collected, info);

			protected abstract void CollectMember(List<LuaCollectedMember> collected, LuaMemberAttribute info);

			protected LuaMemberAttribute Info => info;

			public abstract string DefaultMemberName { get; }
			public string MemberName => info.UseDefault ? DefaultMemberName : info.LuaName;
			public abstract Type DeclaredType { get; }
			public virtual bool IsMemberCall => false;
		} // class LuaTableDefine

		#endregion

		#region -- class LuaTablePropertyDefine -----------------------------------------

		/// <summary></summary>
		private sealed class LuaTablePropertyDefine : LuaTableDefine
		{
			private readonly PropertyInfo pi;

			#region -- Ctor/Dtor --------------------------------------------------------

			public LuaTablePropertyDefine(LuaMemberAttribute info, PropertyInfo pi)
				: base(info)
			{
				this.pi = pi;

				var miGet = pi.GetMethod;
				var miSet = pi.SetMethod;

				if (miGet == null) // invalid property
					throw new InvalidOperationException("No get property.");
				else if (miGet.IsPrivate) // get is private, no code generation is possible (access will fail) -> init only
					mode = LuaTableDefineMode.Init;
				else
				{
					// generate get member
					if (pi.GetIndexParameters().Length > 0)
						throw new InvalidOperationException("Index on properties is not allowed.");

					getValue = miGet.IsStatic ?
						new Func<LuaTable, object>(GetPropertyStaticValue) :
						new Func<LuaTable, object>(GetPropertyInstanceValue);


					if (miSet == null || miSet.IsPrivate) // it is a default property
						mode = LuaTableDefineMode.Default;
					else // it is a direct property
					{
						mode = LuaTableDefineMode.Direct;

						setValue = miSet.IsStatic ?
							new Action<LuaTable, object>(SetPropertyStaticValue) :
							new Action<LuaTable, object>(SetPropertyInstanceValue);
					}
				}
			} // ctor

			public override string ToString()
				=> $"Property: {pi}";

			protected override void CollectMember(List<LuaCollectedMember> collected, LuaMemberAttribute info)
				=> collected.Add(new LuaCollectedMember { Define = this, Info = info, Member = pi });

			#endregion

			#region -- Get/Set/Default --------------------------------------------------

			public override object GetInitialValue(LuaTable table)
			{
				if (mode == LuaTableDefineMode.Init)
				{
					return pi.GetMethod.IsStatic ?
						GetPropertyStaticValue(table) :
						GetPropertyInstanceValue(table);
				}
				else
					return null;
			} // func GetInitialValue

			private object GetPropertyStaticValue(LuaTable t)
				=> pi.GetValue(null, null);

			private object GetPropertyInstanceValue(LuaTable t)
				=> pi.GetValue(t, null);

			private void SetPropertyStaticValue(LuaTable t, object value)
				=> pi.SetValue(null, Lua.RtConvertValue(value, pi.PropertyType), null);

			private void SetPropertyInstanceValue(LuaTable t, object value)
				=> pi.SetValue(t, Lua.RtConvertValue(value, pi.PropertyType), null);

			#endregion

			public PropertyInfo PropertyInfo => pi;
			public override Type DeclaredType => pi.DeclaringType;
			public override string DefaultMemberName => pi.Name;
		} // class LuaTablePropertyDefine

		#endregion

		#region -- class LuaTableMethodDefine -------------------------------------------

		/// <summary></summary>
		private sealed class LuaTableMethodDefine : LuaTableDefine
		{
			private readonly MethodInfo[] methods;

			public LuaTableMethodDefine(LuaMemberAttribute info, MethodInfo[] methods)
				: base(info)
			{
				this.mode = LuaTableDefineMode.Init; // methods get only initialized
				this.methods = methods;
			} // ctor

			public override string ToString()
				=> $"DefineMethod[{methods.Length}]: {methods[0]}";

			public override object GetInitialValue(LuaTable table)
			{
				var instance = methods[0].IsStatic ? null : table;
				return methods.Length == 1 ?
					(object)new LuaMethod(instance, methods[0], IsMemberCall) :
					(object)new LuaOverloadedMethod(instance, methods, IsMemberCall);
			} // func GetInitialValue

			protected override void CollectMember(List<LuaCollectedMember> collected, LuaMemberAttribute info)
			{
				foreach (var mi in methods)
					collected.Add(new LuaCollectedMember { Define = this, Info = info, Member = mi });
			} // proc CollectMember

			public MethodInfo[] Methods => methods;

			public override Type DeclaredType
			{
				get
				{
					var type = methods[0].DeclaringType;
					var typeInfo = type.GetTypeInfo();
					for (var i = 1; i < methods.Length; i++)
					{
						if (typeInfo.IsSubclassOf(methods[i].DeclaringType))
							type = methods[i].DeclaringType;
					}
					return type;
				}
			} // prop DeclaredType

			public override string DefaultMemberName => methods[0].Name;
			public override bool IsMemberCall => Info.IsMethod;
		} // class LuaTableMethodDefine

		#endregion

		#region -- struct LuaCollectedMember --------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private struct LuaCollectedMember
		{
			public LuaMemberAttribute Info;
			public MemberInfo Member;
			public LuaTableDefine Define;

			public override string ToString()
				=> $"{MemberName}{(Define == null ? String.Empty : "*")} ==> {Member}";

			public string MemberName
				=> LuaTableClass.GetEntryName(Info, Member);
		} // struct LuaCollectedMember

		#endregion

		#region -- class LuaTableClass --------------------------------------------------

		/// <summary></summary>
		private sealed class LuaTableClass
		{
			private readonly Type type;
			private readonly LuaTableDefine[] defines = null;

			#region -- Ctor/Dtor --------------------------------------------------------

			public LuaTableClass(Type type)
			{
				this.type = type;

				// collect the type information
				var collected = new List<LuaCollectedMember>();
				Collect(type, collected);

				// metatable must be first
				var index = collected.FindIndex(c => String.CompareOrdinal(c.MemberName, csMetaTable) == 0);
				if (index == -1)
					throw new InvalidOperationException();
				else if (index > 0)
				{
					collected.Insert(0, collected[index]);
					collected.RemoveAt(index + 1);
				}

				// create the defines
				this.defines = CreateDefines(collected);
			} // ctor

			#endregion

			#region -- Collect ----------------------------------------------------------

			private void Collect(Type type, List<LuaCollectedMember> collected)
			{
				// is the type collected
				var ti = type.GetTypeInfo();
				if (ti.BaseType != typeof(object))
				{
					var baseClass = GetClass(ti.BaseType);  // collect recursive

					// dump current defines
					for (var i = 0; i < baseClass.defines.Length; i++)
						baseClass.defines[i].CollectMember(collected);
				}

				// collect current level
				foreach (var mi in ti.DeclaredMembers)
				{
					var method = mi as MethodInfo;
					var property = mi as PropertyInfo;

					if (property == null && method == null) // test on properties and methods
						continue;

					foreach (var info in mi.GetCustomAttributes<LuaMemberAttribute>())
					{
						var memberName = GetEntryName(info, mi);
						if (memberName == null) // remove all member
						{
							for (var j = 0; j < collected.Count - 1; j++)
								if (IsOverrideOf(mi, collected[j].Member))
								{
									collected.RemoveAt(j);
									break;
								}
						}
						else
						{
							var startIndex = FindMember(collected, memberName);
							if (startIndex == -1)
							{
								collected.Add(new LuaCollectedMember { Info = info, Member = mi, Define = null });
							}
							else
							{
								// count the overloaded elements
								var nextIndex = startIndex;
								while (nextIndex < collected.Count && collected[nextIndex].MemberName == memberName)
									nextIndex++;

								// properties it can only exists one property
								if (property != null)
								{
									collected.RemoveRange(startIndex, nextIndex - startIndex);
									collected.Add(new LuaCollectedMember { Info = info, Member = property });
								}
								else if (method != null) // generate overload list
								{
									RemoveUseLessOverloads(collected, (MethodInfo)mi, startIndex, ref nextIndex);
									collected.Insert(nextIndex, new LuaCollectedMember { Info = info, Member = method });
								}
							}
						}
					} // foreach info
				} // for member
			} // proc Collect

			private void RemoveUseLessOverloads(List<LuaCollectedMember> collected, MethodInfo mi, int startIndex, ref int nextIndex)
			{
				while (startIndex < nextIndex)
				{
					var miTest = collected[startIndex].Member as MethodInfo;

					if (miTest == null || IsOverrideOf(mi, miTest) || SameArguments(mi, miTest))
					{
						collected.RemoveAt(startIndex);
						nextIndex--;
						continue;
					}

					startIndex++;
				}
			} // proc RemoveUseLessOverloads

			private bool IsOverrideOf(MemberInfo mi, MemberInfo miTest)
			{
				if (mi.GetType() == miTest.GetType() && mi.Name == miTest.Name)
				{
					if (mi is PropertyInfo)
						return IsOverridePropertyOf((PropertyInfo)mi, (PropertyInfo)miTest);
					else if (mi is MethodInfo)
						return IsOverrideMethodOf((MethodInfo)mi, (MethodInfo)miTest);
					else
						return false;
				}
				else
					return false;
			} // func IsOverrideOf

			private bool IsOverridePropertyOf(PropertyInfo pi, PropertyInfo piTest)
				=> IsOverrideMethodOf(pi.GetMethod, piTest.GetMethod);

			private bool IsOverrideMethodOf(MethodInfo mi, MethodInfo miTest)
			{
				var miCur = mi;
				while (true)
				{
					if (miCur == miTest)
						return true;
					else if (miCur == miCur.GetRuntimeBaseDefinition())
						return false;
					miCur = miCur.GetRuntimeBaseDefinition();
				}
			} // func IsOverrideMethodOf

			private bool SameArguments(MethodInfo mi1, MethodInfo mi2)
			{
				var parameterInfo1 = mi1.GetParameters();
				var parameterInfo2 = mi2.GetParameters();
				if (parameterInfo1.Length == parameterInfo2.Length)
				{
					for (var i = 0; i < parameterInfo1.Length; i++)
					{
						if (parameterInfo1[i].ParameterType != parameterInfo2[i].ParameterType ||
								parameterInfo1[i].Attributes != parameterInfo2[i].Attributes)
							return false;
					}

					return true;
				}
				else
					return false;
			} // func SameArguments

			private int FindMember(List<LuaCollectedMember> collected, string memberName)
			{
				for (var i = 0; i < collected.Count; i++)
				{
					if (collected[i].MemberName == memberName)
						return i;
				}
				return -1;
			} // func FindMember

			#endregion

			#region -- CreateDefines ----------------------------------------------------

			private LuaTableDefine[] CreateDefines(List<LuaCollectedMember> collected)
			{
				var defineList = new List<LuaTableDefine>(collected.Capacity);

				var i = 0;
				while (i < collected.Count)
				{
					var startAt = i;
					var count = 1;
					var currentName = collected[i].MemberName;

					// count same elements
					while (++i < collected.Count && currentName == collected[i].MemberName)
						count++;

					if (count == 1) // create single member
					{
						if (collected[startAt].Define != null) // already collected
							defineList.Add(collected[startAt].Define);
						else
						{
							var mi = collected[startAt].Member;
							if (mi is PropertyInfo)
								defineList.Add(new LuaTablePropertyDefine(collected[startAt].Info, (PropertyInfo)mi));
							else if (mi is MethodInfo)
								defineList.Add(new LuaTableMethodDefine(collected[startAt].Info, new MethodInfo[] { (MethodInfo)mi }));
							else
								throw new ArgumentException();
						}
					}
					else // create overloaded member
					{
						// create method array
						var methods = new MethodInfo[count];
						for (var j = 0; j < count; j++)
							methods[j] = (MethodInfo)collected[startAt + j].Member;

						// check if they are all static/instance
						var createNewDefine = collected[startAt].Define == null;
						for (var j = 1; j < methods.Length; j++)
						{
							if (methods[0].IsStatic != methods[j].IsStatic)
								throw new ArgumentException(String.Format(Properties.Resources.rsMethodStaticMix, methods[0]));

							if (!createNewDefine && collected[startAt].Define != collected[startAt + j].Define)
							{
								createNewDefine |= true;
								break;
							}
						}

						// create the define
						if (createNewDefine)
							defineList.Add(new LuaTableMethodDefine(collected[startAt].Info, methods));
						else
							defineList.Add(collected[startAt].Define);
					}
				}

				return defineList.ToArray();
			} // func CreateDefines

			#endregion

			public Type Type => type;
			public int Count => defines.Length;
			public LuaTableDefine this[int iIndex] => defines[iIndex];

			// -- Static ------------------------------------------------------------

			private static int iClassCount = 0;
			private static LuaTableClass[] classes = new LuaTableClass[0];
			private static object lockClass = new object();

			public static string GetEntryName(LuaMemberAttribute attribute, MemberInfo member)
				 => attribute.UseDefault ? member.Name : attribute.LuaName;

			public static LuaTableClass GetClass(Type type)
			{
				lock (lockClass)
				{
					// is the type collected
					var cls = Array.Find(classes, c => c != null && c.Type == type);
					if (cls == null) // collect the infomration
					{
						cls = new LuaTableClass(type);

						if (iClassCount == classes.Length)
						{
							var newClasses = new LuaTableClass[classes.Length + 4];
							Array.Copy(classes, 0, newClasses, 0, classes.Length);
							classes = newClasses;
						}

						classes[iClassCount++] = cls;
					}
					return cls;
				}
			} // func GetClass
		} // class LuaTableClass

		#endregion

		/// <summary>Value has changed.</summary>
		public event PropertyChangedEventHandler PropertyChanged;

		private LuaTable metaTable = null;                        // Currently attached metatable

		private LuaTableEntry[] entries = emptyLuaEntries;        // Key/Value part of the lua-table
		private LuaTableClass classDefinition = null;             // Class part of the lua table
		private int[] hashLists = emptyIntArray;                  // Hashcode entry point
		private object[] arrayList = emptyObjectArray;            // List with the array elements (this array is ZERO-based)

		private int freeTopIndex = -1;                            // Start of the free lists

		private int arrayLength = 0;                              // Current length of the array list
		private int memberCount = 0;                              // Current length of the member list 

		private int count = 0;                                    // Number of element in the Key/Value part

		private int version = 0;                                  // version for the data

		private readonly Dictionary<int, CallSite> callSites = new Dictionary<int, CallSite>(); // call site for calls

		#region -- Ctor/Dtor --------------------------------------------------------------

		/// <summary>Creates a new lua table</summary>
		public LuaTable()
		{
			InitClass();
		} // ctor

		private LuaTable(object[] values)
		{
			InitClass();

			// copy the values
			arrayList = new object[NextArraySize(arrayList.Length, values.Length)];
			Array.Copy(values, 0, arrayList, 0, values.Length);

			// count the elements
			while (arrayList[arrayLength] != null)
				arrayLength++;
		} // ctor

		/// <summary></summary>
		/// <param name="obj"></param>
		/// <returns></returns>
		public override bool Equals(object obj)
		{
			if (Object.ReferenceEquals(this, obj))
				return true;
			else if (obj != null)
			{
				bool r;
				if (TryInvokeMetaTableOperator<bool>("__eq", false, out r, this, obj))
					return r;
				return false;
			}
			else
				return false;
		} // func Equals

		/// <summary></summary>
		/// <returns></returns>
		public override int GetHashCode()
			=> base.GetHashCode();

		private void InitClass()
		{
			// get class definition for the lua table
			classDefinition = LuaTableClass.GetClass(GetType());

			// create the entries
			ResizeEntryList(classDefinition.Count);

			// generate the memberset
			for (var i = 0; i < classDefinition.Count; i++)
				InitDefinition(i, classDefinition[i]);
		} // proc InitClass

		private void InitDefinition(int index, LuaTableDefine define)
		{
			// Reserve the entry for the member
			memberCount++;
			var entryIndex = InsertValue(define.MemberName, GetMemberHashCode(define.MemberName), null, define.IsMemberCall);
#if DEBUG
			if (entryIndex != index)
				throw new InvalidOperationException("entryIndex");
#endif

			// Set the init value
			entries[entryIndex].value = define.GetInitialValue(this);
		} // proc InitDefinition

		#endregion

		#region -- Dynamic Members --------------------------------------------------------

		/// <summary>Returns the Meta-Object</summary>
		/// <param name="parameter"></param>
		/// <returns></returns>
		public DynamicMetaObject GetMetaObject(Expression parameter)
			=> new LuaTableMetaObject(this, parameter);

		/// <summary>Get the invoke binder.</summary>
		/// <param name="callInfo">CallInfo</param>
		/// <returns>Binder</returns>
		protected virtual CallSiteBinder GetInvokeBinder(CallInfo callInfo)
			=> new Lua.LuaInvokeBinder(null, callInfo);

		/// <summary></summary>
		/// <returns></returns>
		public override string ToString()
		{
			string r;
			if (TryInvokeMetaTableOperator<string>("__tostring", false, out r, this))
				return r;
			return "table";
		} // func ToString

		#endregion

		#region -- Core hash functionality --------------------------------------------

		private static int NextArraySize(int currentLength, int capacity)
		{
			if (currentLength == Int32.MaxValue)
				throw new OverflowException();
			if (currentLength == 0)
				currentLength = 16;

			Resize:
			currentLength = unchecked(currentLength << 1);

			if (currentLength == Int32.MinValue)
				currentLength = Int32.MaxValue;
			else if (capacity > currentLength)
				goto Resize;

			return currentLength;
		} // func NextArraySize

		/// <summary>Insert a value in the hash list</summary>
		/// <param name="key">Key of the item</param>
		/// <param name="hashCode">Hashcode of the key</param>
		/// <param name="value">Value that will be setted</param>
		/// <param name="isMethod">Is the value a method</param>
		/// <returns>Index of the setted entry</returns>
		private int InsertValue(object key, int hashCode, object value, bool isMethod)
		{
#if DEBUG
			if (key == null)
				throw new ArgumentNullException(Properties.Resources.rsTableKeyNotNullable);
#endif

			if (freeTopIndex == -1) // entry list is full -> enlarge
				ResizeEntryList();

			// get free item
			var freeItemIndex = freeTopIndex;
			freeTopIndex = entries[freeTopIndex].nextHash;

			// set the values
			entries[freeItemIndex].key = key;
			entries[freeItemIndex].value = value;
			entries[freeItemIndex].isMethod = isMethod;

			// create the hash list
			var hashIndex = (entries[freeItemIndex].hashCode = hashCode) % hashLists.Length;
			entries[freeItemIndex].nextHash = hashLists[hashIndex];
			hashLists[hashIndex] = freeItemIndex;

			count++;
			version++;

			return freeItemIndex;
		} // func InsertValue

		/// <summary>Search the key in the list</summary>
		/// <param name="key">Key of the item</param>
		/// <param name="hashCode">hash code of the key</param>
		/// <param name="comparer">Comparer for equality</param>
		/// <returns></returns>
		private int FindKey(object key, int hashCode, IEqualityComparer comparer)
		{
#if DEBUG
			//if (key == null)
			//	throw new ArgumentNullException(Properties.Resources.rsTableKeyNotNullable);
#endif
			var hashLength = hashLists.Length;
			if (hashLength == 0)
				return -1;

			var hashIndex = hashCode % hashLength;
			var lastIndex = -1;
			if (comparer == compareStringIgnoreCase)
			{
				for (var i = hashLists[hashIndex]; i >= 0; i = entries[i].nextHash)
				{
					if (entries[i].hashCode == hashCode && comparer.Equals(entries[i].key, key))
						lastIndex = i;
				}
				if (lastIndex >= 0)
					return lastIndex;
			}
			else
			{
				for (var i = hashLists[hashIndex]; i >= 0; i = entries[i].nextHash)
				{
					if (entries[i].hashCode == hashCode && comparer.Equals(entries[i].key, key))
						return i;
				}
			}
			return ~hashIndex;
		} // func FindKey

		private void RemoveValue(int index)
		{
#if DEBUG
			if (hashLists.Length == 0)
				throw new InvalidOperationException();
#endif

			var hashCode = entries[index].hashCode;
			var hashIndex = hashCode % hashLists.Length;

			// remove the item from hash list
			var currentIndex = hashLists[hashIndex];
			if (currentIndex == index)
			{
				hashLists[hashIndex] = entries[index].nextHash;
			}
			else
			{
				while (true)
				{
					var nextIndex = entries[currentIndex].nextHash;
					if (nextIndex == index)
					{
						entries[currentIndex].nextHash = entries[index].nextHash; // remove item from lest
						break;
					}
					currentIndex = nextIndex;

					if (currentIndex == -1)
						throw new InvalidOperationException();
				}
			}

			// add to free list
			entries[index].hashCode = -1;
			entries[index].key = null;
			entries[index].value = null;
			entries[index].isMethod = false;
			entries[index].nextHash = freeTopIndex;
			freeTopIndex = index;

			count--;
			version++;
		} // proc RemoveValue

		private void ResizeEntryList(int capacity = 0)
		{
			var newEntries = new LuaTableEntry[NextArraySize(entries.Length, capacity)];

			// copy the old values
			Array.Copy(entries, 0, newEntries, 0, entries.Length);

			// create the free list for the new entries
			freeTopIndex = entries.Length;
			var length = newEntries.Length - 1;
			for (var i = freeTopIndex; i < length; i++)
			{
				newEntries[i].hashCode = -1;
				newEntries[i].nextHash = i + 1;
			}
			// set the last element
			newEntries[length].hashCode = -1;
			newEntries[length].nextHash = -1;

			// real length
			length++;

			// update the array
			entries = newEntries;

			// create the hash table new
			hashLists = new int[length];
			for (var i = 0; i < hashLists.Length; i++)
				hashLists[i] = -1;

			// rehash all entries
			for (var i = 0; i < freeTopIndex; i++)
			{
				int iIndex = entries[i].hashCode % hashLists.Length;
				entries[i].nextHash = hashLists[iIndex];
				hashLists[iIndex] = i;
			}
		} // proc ResizeEntryList

		/// <summary>Empty the table</summary>
		public void Clear()
		{
			count = 0;
			arrayLength = 0;
			memberCount = 0;
			freeTopIndex = -1;
			version = 0;

			metaTable = null;

			entries = emptyLuaEntries;
			hashLists = emptyIntArray;
			arrayList = emptyObjectArray;

			InitClass();
		} // proc Clear

		#endregion

		#region -- Get/SetMemberValue -------------------------------------------------

		/// <summary>Notify property changed</summary>
		/// <param name="propertyName">Name of property</param>
		protected virtual void OnPropertyChanged(string propertyName)
		{
			if (metaTable is object)
			{
				var o = metaTable["__changed"];
				if (Lua.RtInvokeable(o))
					RtInvokeSite(o, this, propertyName);
			}

			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		} // proc OnPropertyChanged

		private static int GetMemberHashCode(string memberName)
			=> compareStringIgnoreCase.GetHashCode(memberName) & 0x7FFFFFFF;

		/// <summary>Set a value string key value</summary>
		/// <param name="memberName">Key</param>
		/// <param name="value">Value, <c>null</c> deletes the value.</param>
		/// <param name="ignoreCase">Ignore case of the member name</param>
		/// <param name="rawSet">If the value not exists, should we call OnNewIndex.</param>
		/// <returns>value</returns>
		public object SetMemberValue(string memberName, object value, bool ignoreCase = false, bool rawSet = false)
		{
			if (memberName == null)
				throw new ArgumentNullException(Properties.Resources.rsTableKeyNotNullable);

			SetMemberValueIntern(memberName, value, ignoreCase, rawSet, false, false);
			return value;
		} // func SetMemberValue

		private int SetMemberValueIntern(string memberName, object value, bool ignoreCase, bool rawSet, bool add, bool markAsMethod)
		{
			// look up the key in the member list
			var hashCode = GetMemberHashCode(memberName);
			var entryIndex = FindKey(memberName, hashCode, ignoreCase ? compareStringIgnoreCase : compareString);

			if (value == null) // key will be removed
			{
				if (entryIndex >= 0) // key exists
				{
					if (entryIndex < classDefinition.Count)
					{
						SetClassMemberValue(entryIndex, null, value, false);
					}
					else
					{
						var keyName = entries[entryIndex].key;
						// remove the value
						RemoveValue(entryIndex);
						// remove the item
						memberCount--;
						// notify that the property is changed
						OnPropertyChanged(ignoreCase ? (string)keyName : memberName);
					}
					return removedIndex;
				}
				else
				{
					if (!rawSet)
						OnNewIndex(memberName, value); // __newindex will take care
					return indexNotFound; // unknown index
				}
			}
			else if (entryIndex >= 0) // key will be setted
			{
				// only add is allowed
				if (add)
					throw new ArgumentException(String.Format(Properties.Resources.rsTableAddDuplicate, memberName));

				if (entryIndex < classDefinition.Count && SetClassMemberValue(entryIndex, rawSet ? null : entries[entryIndex].key, value, markAsMethod) ||
					entries[entryIndex].SetValue(value, markAsMethod))
				{
					// notify that the property is changed
					OnPropertyChanged(ignoreCase ? (string)entries[entryIndex].key : memberName);
				}

				return entryIndex;
			}
			else if (rawSet || !OnNewIndex(memberName, value)) // key will be added
			{
				// insert the value
				memberCount++;
				InsertValue(memberName, hashCode, value, markAsMethod);

				// notify that the property is changed
				OnPropertyChanged(memberName);

				return entryIndex;
			}
			else
				return indexNotFound;
		} // func SetMemberValueIntern

		private bool SetClassMemberValue(int entryIndex, object key, object value, bool markAsMethod)
		{
			switch (classDefinition[entryIndex].mode)
			{
				case LuaTableDefineMode.Default:
					return entries[entryIndex].SetValue(value, markAsMethod);
				case LuaTableDefineMode.Direct:
					classDefinition[entryIndex].setValue(this, value); // direct properties have to handle OnPropertyChanged on her own
					return false;
				default:
					if (key == null || entries[entryIndex].value != null || !OnNewIndex(key, value))
					{
						entries[entryIndex].SetValue(value, markAsMethod);
						return true;
					}
					else
						return false;
			}
		} // proc SetClassMemberValue

		/// <summary>Returns the value of a key.</summary>
		/// <param name="memberName">Key</param>
		/// <param name="ignoreCase">Ignore case of the member name</param>
		/// <param name="rawGet">Is OnIndex called, if no member exists.</param>
		/// <returns>The value or <c>null</c></returns>
		public object GetMemberValue(string memberName, bool ignoreCase = false, bool rawGet = false)
		{
			if (memberName == null)
				return null;

			// find the member
			var entryIndex = FindKey(memberName, GetMemberHashCode(memberName), ignoreCase ? compareStringIgnoreCase : comparerObject);
			if (entryIndex < 0)
			{
				if (rawGet)
					return null;
				else
					return OnIndex(memberName);
			}
			else if (entryIndex < classDefinition.Count)
			{
				return GetClassMemberValue(entryIndex, memberName, rawGet);
			}
			else
				return entries[entryIndex].value;
		} // func GetMemberValue

		private object GetClassMemberValue(int entryIndex, object key, bool rawGet)
		{
			switch (classDefinition[entryIndex].mode)
			{
				case LuaTableDefineMode.Default:
					return (entries[entryIndex].value ?? classDefinition[entryIndex].getValue(this)) ?? (rawGet ? null : OnIndex(key));

				case LuaTableDefineMode.Direct:
					return classDefinition[entryIndex].getValue(this) ?? (rawGet ? null : OnIndex(key));

				default:
					return entries[entryIndex].value ?? (rawGet ? null : OnIndex(key));
			}
		} // func GetClassMemberValue

		/// <summary>Checks if the Member exists.</summary>
		/// <param name="memberName">Membername</param>
		/// <param name="ignoreCase">Ignore case of the member name</param>
		/// <returns><c>true</c>, if the member is in the table.</returns>
		public bool ContainsMember(string memberName, bool ignoreCase = false)
		{
			if (memberName == null)
				return false;

			return FindKey(memberName, GetMemberHashCode(memberName), ignoreCase ? compareStringIgnoreCase : compareString) >= 0;
		} // func ContainsMember

		#endregion

		#region -- Get/SetArrayValue --------------------------------------------------

		private int FindKey(int index)
			=> FindKey(index, index.GetHashCode() & 0x7FFFFFFF, comparerInt);

		private void SetIndexCopyValuesToArray(object[] newArray, int startAt)
		{
			if (newArray.Length - startAt < entries.Length) // choose the less expensive way to copy the values, try to find values
			{
				for (var i = startAt; i < newArray.Length; i++)
				{
					var entryIndex = FindKey(i + 1);
					if (entryIndex >= 0)
					{
						newArray[i] = entries[entryIndex].value;
						RemoveValue(entryIndex);
						count++;
					}
				}
			}
			else // go through the array
			{
				for (var i = 0; i < entries.Length; i++)
				{
					if (entries[i].key is int k && startAt < k && k <= newArray.Length)
					{
						newArray[k - 1] = entries[i].value;
						RemoveValue(i);
						count++;
					}
				}
			}
		} // func SetIndexCopyValuesToArray

		/// <summary>Set the value in the array part of the table (if the index is greater Length + 1 it is set to the hash part)</summary>
		/// <param name="index">Index of the element</param>
		/// <param name="value">Value, <c>null</c> deletes the value.</param>
		/// <param name="rawSet">If the value not exists, should we call OnNewIndex.</param>
		/// <returns>value</returns>
		public object SetArrayValue(int index, object value, bool rawSet = false)
		{
			var arrayIndex = index - 1;
			if (unchecked((uint)arrayIndex < arrayList.Length)) // with in the current allocated array
			{
				var oldValue = arrayList[arrayIndex];
				if (value == null) // remove the value
				{
					if (oldValue != null)
					{
						arrayList[arrayIndex] = null;
						if (arrayIndex < arrayLength)
							arrayLength = arrayIndex; // arrayLength = arrayIndex - 1

						count--;
						version++;
					}
				}
				else if (rawSet // always set a value
					|| oldValue != null // reset the value
					|| !OnNewIndex(index, value)) // no value, notify __newindex to set the array element
				{
					if (oldValue == null)
						count++;

					arrayList[arrayIndex] = value;
					version++;

					// correct the array length
					if (arrayLength == arrayIndex) // arrayLength = arrayIndex - 1
					{
						// search for the end of the array
						arrayLength = index;
						while (arrayLength + 1 <= arrayList.Length && arrayList[arrayLength] != null)
							arrayLength++;

						// are the more values behind the array
						if (arrayLength == arrayList.Length)
						{
							var collected = new List<object>();

							// collect values
							int entryIndex;
							while ((entryIndex = FindKey(arrayLength + 1)) >= 0)
							{
								collected.Add(entries[entryIndex].value);
								RemoveValue(entryIndex);
								count++;

								arrayLength++;
							}

							// append the values to the array
							if (collected.Count > 0)
							{
								// enlarge array part, with the new values
								var newArray = new object[NextArraySize(arrayList.Length, arrayLength)];
								// copy the old array
								Array.Copy(arrayList, 0, newArray, 0, arrayList.Length);
								// copy the new array content
								collected.CopyTo(newArray, arrayList.Length);
								// collect values for buffer
								SetIndexCopyValuesToArray(newArray, arrayLength);

								arrayList = newArray;
							}
						}
					}
				}
			}
			else if (arrayIndex == arrayLength && value != null) // enlarge array part
			{
				if (value != null && (rawSet || !OnNewIndex(index, value)))
				{
					// create a new enlarged array
					var newArray = new object[NextArraySize(arrayList.Length, 0)];
					Array.Copy(arrayList, 0, newArray, 0, arrayList.Length);

					// copy the values from the key/value part to the array part
					SetIndexCopyValuesToArray(newArray, arrayList.Length);

					arrayList = newArray;

					// set the value in the index
					SetArrayValue(index, value, true);
				}
			}
			else // set the value in key/value part
			{
				var hashCode = index.GetHashCode() & 0x7FFFFFFF;
				var entryIndex = FindKey(index, hashCode, comparerInt);
				if (entryIndex >= 0)
				{
					if (value == null)
					{
						RemoveValue(entryIndex);
					}
					else
					{
						entries[entryIndex].value = value;
						version++;
					}
				}
				else if (rawSet || !OnNewIndex(index, value))
					InsertValue(index, hashCode, value, false);
			}

			return value;
		} // func SetArrayValue

		/// <summary>Get the value from the array part or from the hash part.</summary>
		/// <param name="index">Index of the element</param>
		/// <param name="rawGet">Is OnIndex called, if no index exists.</param>
		/// <returns></returns>
		public object GetArrayValue(int index, bool rawGet = false)
		{
			var arrayIndex = index - 1;
			if (unchecked((uint)arrayIndex < arrayList.Length)) // part of array
			{
				if (rawGet || arrayIndex < arrayLength)
					return arrayList[arrayIndex];
				else
					return arrayList[arrayIndex] ?? OnIndex(index);
			}
			else // check the hash part
			{
				var entryIndex = FindKey(index);
				if (entryIndex >= 0) // get the hashed value
					return entries[entryIndex].value;
				else if (rawGet) // get the default value
					return null;
				else // ask for a value
					return OnIndex(index);
			}
		} // func SetArrayValue

		/// <summary>Checks if the index is set.</summary>
		/// <param name="index">Index</param>
		/// <returns><c>true</c>, if the index is in the table.</returns>
		public bool ContainsIndex(int index)
		{
			if (index >= 1 && index <= arrayList.Length) // part of array
				return arrayList[index - 1] != null;
			else // hashed index
				return FindKey(index) >= 0;
		} // func ContainsIndex

		#endregion

		#region -- High level Array/Member functions ----------------------------------

		private void MembersCopyTo(KeyValuePair<string, object>[] array, int arrayIndex)
		{
			if (arrayIndex < 0 || arrayIndex + memberCount - hiddenMemberCount > array.Length)
				throw new ArgumentOutOfRangeException();

			for (var i = hiddenMemberCount; i < entries.Length; i++)
			{
				if (entries[i].key is string member)
					array[arrayIndex++] = new KeyValuePair<string, object>(member, entries[i].value);
			}
		} // proc MembersCopyTo

		private IEnumerator<KeyValuePair<string, object>> MembersGetEnumerator()
		{
			var version = this.version;
			for (var i = hiddenMemberCount; i < entries.Length; i++)
			{
				if (version != this.version)
					throw new InvalidOperationException();

				if (entries[i].key is string member)
				{
					yield return new KeyValuePair<string, object>(member,
						i < classDefinition.Count
							? GetClassMemberValue(i, member, true)
							: entries[i].value
					);
				}
			}
		} // func MembersGetEnumerator

		private void ClearMembers()
		{
			for (var i = hiddenMemberCount; i < entries.Length; i++)
			{
				if (i < classDefinition.Count)
				{
					if (classDefinition[i].mode == LuaTableDefineMode.Init)
						SetClassMemberValue(i, null, classDefinition[i].GetInitialValue(this), false);
					else
						SetClassMemberValue(i, null, null, false);
				}
				else if (entries[i].hashCode != -1 && entries[i].key is string)
					RemoveValue(i);
			}
		} // proc ClearMembers

		/// <summary>zero based</summary>
		/// <param name="value"></param>
		/// <returns></returns>
		private int ArrayOnlyIndexOf(object value)
			=> Array.IndexOf(arrayList, value, 0, arrayLength);

		private int ArrayOnlyAdd(object value)
		{
			if (value == null)
				throw new ArgumentNullException(nameof(value)); // null, values do not change arrayLength (a, nil, b) -> (a, b)

			var idx = arrayLength;
			SetArrayValue(arrayLength + 1, value, true);
			return idx;
		} // func ArrayOnlyAdd

		/// <summary>zero based</summary>
		/// <param name="iIndex"></param>
		/// <param name="value"></param>
		private void ArrayOnlyInsert(int iIndex, object value)
		{
			if (iIndex < 0 || iIndex > arrayLength)
				throw new ArgumentOutOfRangeException();

			object last;
			if (iIndex == arrayLength)
				last = value;
			else
			{
				last = arrayList[arrayLength - 1];
				if (iIndex != arrayLength - 1)
					Array.Copy(arrayList, iIndex, arrayList, iIndex + 1, arrayLength - iIndex - 1);
				arrayList[iIndex] = value;
			}

			SetArrayValue(arrayLength + 1, last, true);
		} // proc ArrayOnlyInsert 

		private void ArrayOnlyCopyTo(Array array, int arrayIndex)
		{
			if (arrayIndex + arrayLength > array.Length)
				throw new ArgumentOutOfRangeException();

			Array.Copy(arrayList, 0, array, arrayIndex, arrayLength);
		} // proc ArrayOnlyCopyTo

		private void ArrayOnlyClear()
		{
			Array.Clear(arrayList, 0, arrayLength);
			arrayLength = 0;
			version++;
		} // ArrayOnlyClear

		/// <summary>zero based</summary>
		/// <param name="index"></param>
		private void ArrayOnlyRemoveAt(int index)
		{
			if (index < 0 || index >= arrayLength)
				throw new ArgumentOutOfRangeException();

			Array.Copy(arrayList, index + 1, arrayList, index, arrayLength - index - 1);
			arrayList[--arrayLength] = null;

			version++;
		} // func ArrayOnlyRemoveAt

		private bool ArrayOnlyRemove(object value)
		{
			var index = ArrayOnlyIndexOf(value);
			if (index >= 0)
			{
				ArrayOnlyRemoveAt(index);
				return true;
			}
			else
				return false;
		} // func ArrayOnlyRemove

		private IEnumerator<object> ArrayOnlyGetEnumerator()
		{
			var version = this.version;
			for (var i = 0; i < arrayLength; i++)
			{
				if (version != this.version)
					throw new InvalidOperationException();

				yield return arrayList[i];
			}
		} // func ArrayOnlyGetEnumerator

		private object ArrayOnlyGetIndex(int index)
		{
			if (index >= 0 && index >= arrayLength)
				throw new ArgumentOutOfRangeException();
			return arrayList[index];
		} // func ArrayOnlyGetIndex

		private void ArrayOnlySetIndex(int index, object value)
		{
			if (index >= 0 && index >= arrayLength)
				throw new ArgumentOutOfRangeException();
			arrayList[index] = value;
		} // proc ArrayOnlySetIndex

		#endregion

		#region -- Simple Set/GetValue/Contains ---------------------------------------

		/// <summary>Is the type a index type.</summary>
		/// <param name="type"></param>
		/// <returns></returns>
		internal static bool IsIndexKey(Type type)
		{
			var tc = LuaEmit.GetTypeCode(type);
			return tc >= LuaEmitTypeCode.SByte && tc <= LuaEmitTypeCode.Int32;
		} // func IsIndexKey

		private static bool IsIndexKey(object item, out int index)
		{
			#region -- IsIndexKey --
			switch (LuaEmit.GetTypeCode(item.GetType()))
			{
				case LuaEmitTypeCode.Int32:
					index = (int)item;
					return true;
				case LuaEmitTypeCode.Byte:
					index = (byte)item;
					return true;
				case LuaEmitTypeCode.SByte:
					index = (sbyte)item;
					return true;
				case LuaEmitTypeCode.UInt16:
					index = (ushort)item;
					return true;
				case LuaEmitTypeCode.Int16:
					index = (short)item;
					return true;
				case LuaEmitTypeCode.UInt32:
					unchecked
					{
						var t = (uint)item;
						if (t < Int32.MaxValue)
						{
							index = (int)t;
							return true;
						}
						else
						{
							index = 0;
							return false;
						}
					}
				case LuaEmitTypeCode.Int64:
					unchecked
					{
						var t = (long)item;
						if (t < Int32.MaxValue)
						{
							index = (int)t;
							return true;
						}
						else
						{
							index = 0;
							return false;
						}
					}
				case LuaEmitTypeCode.UInt64:
					unchecked
					{
						var t = (ulong)item;
						if (t < Int32.MaxValue)
						{
							index = (int)t;
							return true;
						}
						else
						{
							index = 0;
							return false;
						}
					}
				case LuaEmitTypeCode.Single:
					{
						var f = (float)item;
						if (f % 1 == 0 && f >= 1 && f <= Int32.MaxValue)
						{
							index = Convert.ToInt32(f);
							return true;
						}
						else
						{
							index = 0;
							return false;
						}
					}
				case LuaEmitTypeCode.Double:
					{
						var f = (double)item;
						if (f % 1 == 0 && f >= 1 && f <= Int32.MaxValue)
						{
							index = Convert.ToInt32(f);
							return true;
						}
						else
						{
							index = 0;
							return false;
						}
					}
				case LuaEmitTypeCode.Decimal:
					{
						var f = (decimal)item;
						if (f % 1 == 0 && f >= 1 && f <= Int32.MaxValue)
						{
							index = Convert.ToInt32(f);
							return true;
						}
						else
						{
							index = 0;
							return false;
						}
					}
				default:
					index = 0;
					return false;
			}
			#endregion
		} // func IsIndexKey

		/// <summary>Set a value in of the table</summary>
		/// <param name="key">Key</param>
		/// <param name="value">Value, <c>null</c> deletes the value.</param>
		/// <param name="rawSet">If the value not exists, should we call OnNewIndex.</param>
		/// <returns>value</returns>
		public object SetValue(object key, object value, bool rawSet = false)
		{
			if (key == null)
				throw new ArgumentNullException(Properties.Resources.rsTableKeyNotNullable);
			else if (IsIndexKey(key, out var index)) // is a array element
				return SetArrayValue(index, value, rawSet);
			else if (key is string memberKey) // belongs to the member list
			{
				SetMemberValueIntern(memberKey, value, false, rawSet, false, false);
				return value;
			}
			else // something else
			{
				var hashCode = key.GetHashCode() & 0x7FFFFFFF;
				index = FindKey(key, hashCode, comparerObject); // find the value

				if (value == null) // remove value
					RemoveValue(index);
				else if (index < 0 && (rawSet || !OnNewIndex(key, value))) // insert value
					InsertValue(key, hashCode, value, false);
				else // update value
					entries[index].value = value;

				return value;
			}
		} // func SetValue

		/// <summary>Set multi indexed values.</summary>
		/// <param name="keyList">Keys</param>
		/// <param name="rawSet">If the value not exists, should we call OnNewIndex.</param>
		/// <param name="value"></param>
		public void SetValue(object[] keyList, object value, bool rawSet = false)
			=> SetValue(keyList, 0, value, rawSet);

		private void SetValue(object[] keyList, int index, object value, bool rawSet)
		{
			if (index == keyList.Length - 1)
			{
				SetValue(keyList[index], value, false);
			}
			else
			{
				var tNext = GetValue(keyList[index], false) as LuaTable;
				if (tNext == null)
				{
					tNext = new LuaTable();
					SetValue(keyList[index], tNext, rawSet); // set it, as it is
				}
				tNext.SetValue(keyList, index++, value, rawSet);
			}
		} // func SetValue

		/// <summary>Gets the value of a key</summary>
		/// <param name="key">Key</param>
		/// <param name="rawGet">Is OnIndex called, if no key exists.</param>
		/// <returns>The value or <c>null</c>.</returns>
		public object GetValue(object key, bool rawGet = false)
		{
			if (key == null)
				return null;
			else if (IsIndexKey(key, out var index))
				return GetArrayValue(index, rawGet);
			else if (key is string memberKey)
				return GetMemberValue(memberKey, false, rawGet);
			else
			{
				index = FindKey(key, key.GetHashCode() & 0x7FFFFFFF, comparerObject);
				if (index < 0)
					return rawGet ? null : OnIndex(key);
				else
					return entries[index].value;
			}
		} // func GetValue

		/// <summary>Get multi indexed values</summary>
		/// <param name="keyList">Keys</param>
		/// <param name="rawGet">Is OnIndex called, if no key exists.</param>
		/// <returns>Value</returns>
		public object GetValue(object[] keyList, bool rawGet = false)
			=> GetValue(keyList, 0, rawGet);

		private object GetValue(object[] keyList, int index, bool rawGet)
		{
			var o = GetValue(keyList[index], rawGet);

			if (index == keyList.Length - 1)
				return o;
			else
				return o is LuaTable tNext ? tNext.GetValue(keyList, index + 1, rawGet) : null;
		} // func GetValue

		/// <summary>Returns the value of the table.</summary>
		/// <typeparam name="T">Excpected type for the value</typeparam>
		/// <param name="name">Name of the member.</param>
		/// <param name="default">Replace value, if the member not exists or can not converted.</param>
		/// <param name="ignoreCase"></param>
		/// <param name="rawGet"></param>
		/// <returns>Value or default.</returns>
		public T GetOptionalValue<T>(string name, T @default, bool ignoreCase = false, bool rawGet = false)
		{
			try
			{
				var o = GetMemberValue(name, ignoreCase, rawGet);
				return o != null ? (T)Lua.RtConvertValue(o, typeof(T)) : @default;
			}
			catch
			{
				return @default;
			}
		} // func GetOptionalValue

		/// <summary>Checks if the key exists.</summary>
		/// <param name="key">key</param>
		/// <returns><c>true</c>, if the key is in the listtable</returns>
		public bool ContainsKey(object key)
		{
			if (key == null)
				return false;
			else if (IsIndexKey(key, out var iIndex))
				return ContainsIndex(iIndex);
			else if (key is string memberKey)
				return ContainsMember(memberKey, false);
			else
				return FindKey(key, key.GetHashCode() & 0x7FFFFFFF, comparerObject) >= 0;
		} // func ContainsKey

		#endregion

		#region -- DefineFunction, DefineMethod ---------------------------------------

		/// <summary>Defines a normal function attached to a table.</summary>
		/// <param name="functionName">Name of the member for the function.</param>
		/// <param name="function">function definition</param>
		/// <param name="ignoreCase">Ignore case of the member name</param>
		/// <returns>function</returns>
		/// <remarks>If you want to delete the define, call SetMemberValue with the function name and set the value to <c>null</c>.</remarks>
		public Delegate DefineFunction(string functionName, Delegate function, bool ignoreCase = false)
		{
			if (String.IsNullOrEmpty(functionName))
				throw new ArgumentNullException(nameof(functionName));
			if (function == null)
				throw new ArgumentNullException(nameof(function));

			SetMemberValueIntern(functionName, function, ignoreCase, false, false, false);
			return function;
		} // func DefineFunction

		/// <summary>Defines a new method on the table.</summary>
		/// <param name="methodName">Name of the member/name.</param>
		/// <param name="method">Method that has as a first parameter a LuaTable.</param>
		/// <param name="ignoreCase">Ignore case of the member name</param>
		/// <returns>method</returns>
		/// <remarks>If you want to delete the define, call SetMemberValue with the function name and set the value to <c>null</c>.</remarks>
		public Delegate DefineMethod(string methodName, Delegate method, bool ignoreCase = false)
		{
			if (String.IsNullOrEmpty(methodName))
				throw new ArgumentNullException(nameof(methodName));
			if (method == null)
				throw new ArgumentNullException(nameof(method));

			var typeFirstParameter = method.GetMethodInfo().GetParameters()[0].ParameterType;
			if (!typeFirstParameter.GetTypeInfo().IsAssignableFrom(typeof(LuaTable).GetTypeInfo()))
				throw new ArgumentException(String.Format(Properties.Resources.rsTableMethodExpected, methodName));

			SetMemberValueIntern(methodName, method, ignoreCase, false, false, true);
			return method;
		} // func DefineMethod

		internal Delegate DefineMethodLight(string methodName, Delegate method)
		{
			SetMemberValueIntern(methodName, method, false, false, false, true);
			return method;
		} // func DefineMethodLight

		#endregion

		#region -- CallMember ---------------------------------------------------------

		internal enum CallMethod
		{
			Nil,
			ReturnOnly,
			Delegate,
			DelegateMember,
			Dynamic,
			DynamicMember
		} // enum CallMethod

		internal CallMethod GetCallMember(string memberName, bool ignoreCase, bool rawGet, out object method)
		{
			bool memberCall;

			var entryIndex = FindKey(memberName, GetMemberHashCode(memberName), ignoreCase ? compareStringIgnoreCase : compareString);
			if (entryIndex < 0)
			{
				method = rawGet ? null : OnIndex(memberName);
				memberCall = (method as ILuaMethod)?.IsMemberCall ?? false;
			}
			else
			{
				memberCall = entries[entryIndex].isMethod;
				method = entries[entryIndex].value;
			}

			// create return value
			if (method == null)
				return CallMethod.Nil;
			else if (method is IDynamicMetaObjectProvider)
				return memberCall ? CallMethod.DynamicMember : CallMethod.Dynamic;
			else if (method is Delegate)
				return memberCall ? CallMethod.DelegateMember : CallMethod.Delegate;
			else
				return CallMethod.ReturnOnly;
		} // func GetCallMember

		/// <summary>Call a member</summary>
		/// <param name="memberName">Name of the member</param>
		/// <returns>Result of the function call.</returns>
		public LuaResult CallMember(string memberName)
			=> CallMemberDirect(memberName, emptyObjectArray);

		/// <summary>Call a member</summary>
		/// <param name="memberName">Name of the member</param>
		/// <param name="arg0">first argument</param>
		/// <returns>Result of the function call.</returns>
		public LuaResult CallMember(string memberName, object arg0)
			=> CallMemberDirect(memberName, new object[] { arg0, });

		/// <summary>Call a member</summary>
		/// <param name="memberName">Name of the member</param>
		/// <param name="arg0">first argument</param>
		/// <param name="arg1">second argument</param>
		/// <returns>Result of the function call.</returns>
		public LuaResult CallMember(string memberName, object arg0, object arg1)
			=> CallMemberDirect(memberName, new object[] { arg0, arg1 });

		/// <summary>Call a member</summary>
		/// <param name="memberName">Name of the member</param>
		/// <param name="arg0">first argument</param>
		/// <param name="arg1">second argument</param>
		/// <param name="arg2">third argument</param>
		/// <returns>Result of the function call.</returns>
		public LuaResult CallMember(string memberName, object arg0, object arg1, object arg2)
			=> CallMemberDirect(memberName, new object[] { arg0, arg1, arg2 });

		/// <summary>Call a member</summary>
		/// <param name="memberName">Name of the member</param>
		/// <param name="args">Arguments</param>
		/// <returns>Result of the function call.</returns>
		public LuaResult CallMember(string memberName, params object[] args)
			=> CallMemberDirect(memberName, args);

		/// <summary>Call a member (function or method) of the lua-table</summary>
		/// <param name="memberName">Name of the member</param>
		/// <param name="args">Arguments</param>
		/// <param name="ignoreCase">Ignore case of the member name</param>
		/// <param name="rawGet"></param>
		/// <param name="throwExceptions"><c>true</c>, throws a exception if something is going wrong. <c>false</c>, on a exception a empty LuaResult will be returned.</param>
		/// <param name="ignoreNilFunction"><c>true</c>, throws no exception n nil value.</param>
		/// <returns></returns>
		public LuaResult CallMemberDirect(string memberName, object[] args, bool ignoreCase = false, bool rawGet = false, bool throwExceptions = true, bool ignoreNilFunction = false)
		{
			if (memberName == null)
				throw new ArgumentNullException(Properties.Resources.rsTableKeyNotNullable);

			// look up the member
			try
			{
				switch (GetCallMember(memberName, ignoreCase, rawGet, out var method))
				{
					case CallMethod.Nil:
						if (throwExceptions && !ignoreNilFunction)
							throw new ArgumentNullException(String.Format(Properties.Resources.rsMemberNotResolved, "table", memberName));
						else
							return LuaResult.Empty;

					case CallMethod.Delegate:
					case CallMethod.Dynamic:
						{
							if (args == null || args.Length == 0)
							{
								args = new object[] { null, method };
							}
							else
							{
								var newArgs = new object[args.Length + 2];
								Array.Copy(args, 0, newArgs, 2, args.Length);
								newArgs[1] = method;
								args = newArgs;
							}
							return RtInvokeSiteCached(args);
						}
					case CallMethod.DelegateMember:
					case CallMethod.DynamicMember:
						{
							if (args == null || args.Length == 0)
							{
								args = new object[] { null, method, this };
							}
							else
							{
								var newArgs = new object[args.Length + 3];
								Array.Copy(args, 0, newArgs, 3, args.Length);
								newArgs[1] = method;
								newArgs[2] = this;
								args = newArgs;
							}
							return RtInvokeSiteCached(args);
						}

					default:
						return new LuaResult(method);
				}
			}
			catch (TargetInvocationException e)
			{
				if (throwExceptions)
					throw new TargetInvocationException(String.Format(Properties.Resources.rsTableCallMemberFailed, memberName), e.InnerException);
				return LuaResult.Empty;
			}
		} // func CallMemberDirect

		internal object RtInvokeSite(object target, params object[] args)
		{
			// create the argument array
			var newArgs = new object[args == null ? 2 : args.Length + 2];
			newArgs[1] = target;
			if (args != null)
				Array.Copy(args, 0, newArgs, 2, args.Length);

			return RtInvokeSiteCached(newArgs);
		} // func RtInvokeSite

		private LuaResult RtInvokeSiteCached(object[] args)
		{
			// get cached call site
			if (callSites.TryGetValue(args.Length, out var site))
				args[0] = site;

			// call site
			return new LuaResult(Lua.RtInvokeSite(GetInvokeBinder, (callInfo, callSite) => callSites[callInfo.ArgumentCount + 1] = callSite, args));
		} // func RtInvokeSiteCached

		#endregion

		#region -- SetObjectMember ----------------------------------------------------

		/// <summary>Sets the given object with the members of the table.</summary>
		/// <param name="obj"></param>
		public object SetObjectMember(object obj)
		{
			if (obj == null)
				return obj;

			var type = obj.GetType();

			// set all fields
			foreach (var field in type.GetRuntimeFields().Where(fi => fi.IsPublic && !fi.IsStatic && !fi.IsInitOnly))
			{
				var entryIndex = FindKey(field.Name, GetMemberHashCode(field.Name), compareString);
				if (entryIndex >= 0)
					field.SetValue(obj, Lua.RtConvertValue(entries[entryIndex].value, field.FieldType));
			}

			// set all properties
			foreach (var property in type.GetRuntimeProperties().Where(pi => pi.SetMethod != null && pi.SetMethod.IsPublic && !pi.SetMethod.IsStatic))
			{
				var entryIndex = FindKey(property.Name, GetMemberHashCode(property.Name), compareString);
				if (entryIndex >= 0)
					property.SetValue(obj, Lua.RtConvertValue(entries[entryIndex].value, property.PropertyType), null);
			}

			return obj;
		} // proc SetObjectMember

		#endregion

		#region -- Metatable ----------------------------------------------------------

		private bool TryInvokeMetaTableOperator<TRETURN>(string key, bool raise, out TRETURN r, params object[] args)
		{
			if (metaTable != null)
			{
				var o = metaTable[key];
				if (o != null)
				{
					if (Lua.RtInvokeable(o))
					{
						r = (TRETURN)Lua.RtConvertValue(RtInvokeSite(o, args), typeof(TRETURN));
						return true;
					}
					if (raise)
						throw new LuaRuntimeException(String.Format(Properties.Resources.rsTableOperatorIncompatible, key, "function"), 0, true);
				}
			}
			if (raise)
				throw new LuaRuntimeException(String.Format(Properties.Resources.rsTableOperatorNotFound, key), 0, true);

			r = default(TRETURN);
			return false;
		} // func GetMetaTableOperator

		private object UnaryOperation(string key)
			=> TryInvokeMetaTableOperator<object>(key, true, out var o, this) ? o : null;

		private object BinaryOperation(string key, object arg)
			=> TryInvokeMetaTableOperator<object>(key, true, out var o, this, arg) ? o : null;

		private bool BinaryBoolOperation(string key, object arg)
			=> TryInvokeMetaTableOperator<bool>(key, true, out var o, this, arg) ? o : false;

		/// <summary></summary>
		/// <param name="arg"></param>
		/// <returns></returns>
		protected virtual object OnAdd(object arg)
			=> BinaryOperation("__add", arg);

		/// <summary></summary>
		/// <param name="arg"></param>
		/// <returns></returns>
		protected virtual object OnSub(object arg)
			=> BinaryOperation("__sub", arg);

		/// <summary></summary>
		/// <param name="arg"></param>
		/// <returns></returns>
		protected virtual object OnMul(object arg)
			=> BinaryOperation("__mul", arg);

		/// <summary></summary>
		/// <param name="arg"></param>
		/// <returns></returns>
		protected virtual object OnDiv(object arg)
			=> BinaryOperation("__div", arg);

		/// <summary></summary>
		/// <param name="arg"></param>
		/// <returns></returns>
		protected virtual object OnMod(object arg)
			=> BinaryOperation("__mod", arg);

		/// <summary></summary>
		/// <param name="arg"></param>
		/// <returns></returns>
		protected virtual object OnPow(object arg)
			=> BinaryOperation("__pow", arg);

		/// <summary></summary>
		/// <returns></returns>
		protected virtual object OnUnMinus()
			=> UnaryOperation("__unm");

		/// <summary></summary>
		/// <param name="arg"></param>
		/// <returns></returns>
		protected virtual object OnIDiv(object arg)
			=> BinaryOperation("__idiv", arg);

		/// <summary></summary>
		/// <param name="arg"></param>
		/// <returns></returns>
		protected virtual object OnBAnd(object arg)
			=> BinaryOperation("__band", arg);

		/// <summary></summary>
		/// <param name="arg"></param>
		/// <returns></returns>
		protected virtual object OnBOr(object arg)
			=> BinaryOperation("__bor", arg);

		/// <summary></summary>
		/// <param name="arg"></param>
		/// <returns></returns>
		protected virtual object OnBXor(object arg)
			=> BinaryOperation("__bxor", arg);

		/// <summary></summary>
		/// <returns></returns>
		protected virtual object OnBNot()
			=> UnaryOperation("__bnot");

		/// <summary></summary>
		/// <param name="arg"></param>
		/// <returns></returns>
		protected virtual object OnShl(object arg)
			=> BinaryOperation("__shl", arg);

		/// <summary></summary>
		/// <param name="arg"></param>
		/// <returns></returns>
		protected virtual object OnShr(object arg)
			=> BinaryOperation("__shr", arg);

		internal object InternConcat(object arg)
			=> OnConcat(arg);

		/// <summary></summary>
		/// <param name="arg"></param>
		/// <returns></returns>
		protected virtual object OnConcat(object arg)
			=> BinaryOperation("__concat", arg);

		internal int InternLen()
			=> OnLen();

		/// <summary></summary>
		/// <returns></returns>
		protected virtual int OnLen()
			=> TryInvokeMetaTableOperator<int>("__len", false, out var l, this) ? l : Length;

		/// <summary></summary>
		/// <param name="arg"></param>
		/// <returns></returns>
		protected virtual bool OnEqual(object arg)
			=> Equals(arg);

		/// <summary></summary>
		/// <param name="arg"></param>
		/// <returns></returns>
		protected virtual bool OnLessThan(object arg)
			=> BinaryBoolOperation("__lt", arg);

		/// <summary></summary>
		/// <param name="arg"></param>
		/// <returns></returns>
		protected virtual bool OnLessEqual(object arg)
			=> BinaryBoolOperation("__le", arg);

		/// <summary></summary>
		/// <param name="key"></param>
		/// <returns></returns>
		protected virtual object OnIndex(object key)
		{
			if (metaTable is null)
				return null;

			var index = metaTable["__index"];
			if (index is LuaTable t) // default table
				return t.GetValue(key, false);
			else if (Lua.RtInvokeable(index)) // default function
				return new LuaResult(RtInvokeSite(index, this, key))[0];
			else
				return null;
		} // func OnIndex

		/// <summary></summary>
		/// <param name="key"></param>
		/// <param name="value"></param>
		/// <returns><c>true</c>, if __newindex is defined.</returns>
		protected virtual bool OnNewIndex(object key, object value)
		{
			if (metaTable is null)
				return false;

			var o = metaTable["__newindex"];
			if (Lua.RtInvokeable(o))
			{
				RtInvokeSite(o, this, key, value);
				return true;
			}
			return false;
		} // func OnIndex

		/// <summary></summary>
		/// <param name="args"></param>
		/// <returns></returns>
		protected virtual LuaResult OnCall(object[] args)
		{
			if (args == null || args.Length == 0)
				return TryInvokeMetaTableOperator<LuaResult>("__call", true, out var o, this) ? o : LuaResult.Empty;
			else
			{
				var argsEnlarged = new object[args.Length + 1];
				argsEnlarged[0] = this;
				Array.Copy(args, 0, argsEnlarged, 1, args.Length);
				return TryInvokeMetaTableOperator<LuaResult>("__call", false, out var o, argsEnlarged) ? o : LuaResult.Empty;
			}
		} // func OnCall

		#endregion

		#region -- IDictionary<object,object> members -------------------------------------

		#region -- class LuaTableHashKeyCollection ----------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		public class LuaTableHashKeyCollection : ICollection<object>
		{
			private LuaTable t;

			internal LuaTableHashKeyCollection(LuaTable t)
			{
				this.t = t;
			} // ctor

			/// <summary></summary>
			/// <param name="item"></param>
			/// <returns></returns>
			public bool Contains(object item)
				=> t.ContainsKey(item);

			/// <summary></summary>
			/// <param name="array"></param>
			/// <param name="arrayIndex"></param>
			public void CopyTo(object[] array, int arrayIndex)
			{
				if (arrayIndex < 0 || arrayIndex + Count > array.Length)
					throw new ArgumentOutOfRangeException();

				for (var i = 0; i < t.arrayList.Length; i++)
				{
					if (t.arrayList[i] != null)
						array[arrayIndex++] = i + 1;
				}

				for (var i = hiddenMemberCount; i < t.entries.Length; i++)
				{
					if (t.entries[i].hashCode != -1)
						array[arrayIndex++] = t.entries[i].key;
				}
			} // proc CopyTo

			/// <summary></summary>
			/// <returns></returns>
			public IEnumerator<object> GetEnumerator()
			{
				var version = t.version;

				for (var i = 0; i < t.arrayList.Length; i++)
				{
					if (version != t.version)
						throw new InvalidOperationException("table changed");

					if (t.arrayList[i] != null)
						yield return i + 1;
				}
				for (var i = hiddenMemberCount; i < t.entries.Length; i++)
				{
					if (version != t.version)
						throw new InvalidOperationException("table changed");

					if (t.entries[i].hashCode != -1)
						yield return t.entries[i].key;
				}
			} // func GetEnumerator

			IEnumerator IEnumerable.GetEnumerator()
				=> GetEnumerator();

			void ICollection<object>.Add(object item) { throw new NotSupportedException(); }
			bool ICollection<object>.Remove(object item) { throw new NotSupportedException(); }
			void ICollection<object>.Clear() { throw new NotSupportedException(); }

			/// <summary></summary>
			public int Count => t.count - hiddenMemberCount;
			/// <summary>Always true</summary>
			public bool IsReadOnly => true;
		} // class LuaTableHashKeyCollection

		#endregion

		#region -- class LuaTableHashValueCollection --------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		public class LuaTableHashValueCollection : ICollection<object>
		{
			private LuaTable t;

			internal LuaTableHashValueCollection(LuaTable t)
			{
				this.t = t;
			} // ctor

			/// <summary></summary>
			/// <param name="value"></param>
			/// <returns></returns>
			public bool Contains(object value)
			{
				for (var i = 0; i < t.arrayList.Length; i++)
				{
					if (t.arrayList[i] != null && comparerObject.Equals(t.arrayList[i], value))
						return true;
				}

				for (var i = hiddenMemberCount; i < t.classDefinition.Count; i++)
				{
					if (comparerObject.Equals(t.GetClassMemberValue(i, t.entries[i].key, true), value))
						return true;
				}

				for (var i = t.classDefinition.Count; i < t.entries.Length; i++)
				{
					if (t.entries[i].hashCode != -1 && comparerObject.Equals(t.entries[i].value, value))
						return true;
				}

				return false;
			} // func Contains

			/// <summary></summary>
			/// <param name="array"></param>
			/// <param name="arrayIndex"></param>
			public void CopyTo(object[] array, int arrayIndex)
			{
				if (arrayIndex < 0 || arrayIndex + Count > array.Length)
					throw new ArgumentOutOfRangeException();

				for (var i = 0; i < t.arrayList.Length; i++)
				{
					if (t.arrayList[i] != null)
						array[arrayIndex++] = t.arrayList[i];
				}

				for (var i = hiddenMemberCount; i < t.classDefinition.Count; i++)
				{
					array[arrayIndex++] = t.GetClassMemberValue(i, t.entries[i].key, true);
				}

				for (var i = t.classDefinition.Count; i < t.entries.Length; i++)
				{
					if (t.entries[i].hashCode != -1)
						array[arrayIndex++] = t.entries[i].value;
				}
			} // proc CopyTo

			/// <summary></summary>
			/// <returns></returns>
			public IEnumerator<object> GetEnumerator()
			{
				var version = t.version;

				for (var i = 0; i < t.arrayList.Length; i++)
				{
					if (version != t.version)
						throw new InvalidOperationException("table changed");

					if (t.arrayList[i] != null)
						yield return t.arrayList[i];
				}

				for (var i = hiddenMemberCount; i < t.classDefinition.Count; i++)
				{
					if (version != t.version)
						throw new InvalidOperationException("table changed");

					yield return t.GetClassMemberValue(i, t.entries[i].key, true);
				}

				for (var i = t.classDefinition.Count; i < t.entries.Length; i++)
				{
					if (version != t.version)
						throw new InvalidOperationException("table changed");

					if (t.entries[i].hashCode != -1)
						yield return t.entries[i].value;
				}
			} // func GetEnumerator

			IEnumerator IEnumerable.GetEnumerator()
				=> GetEnumerator();

			void ICollection<object>.Add(object item) { throw new NotSupportedException(); }
			bool ICollection<object>.Remove(object item) { throw new NotSupportedException(); }
			void ICollection<object>.Clear() { throw new NotSupportedException(); }

			/// <summary></summary>
			public int Count => t.count - hiddenMemberCount;
			/// <summary>Always true</summary>
			public bool IsReadOnly => true;
		} // class LuaTableHashValueCollection

		#endregion

		private LuaTableHashKeyCollection hashKeyCollection = null;
		private LuaTableHashValueCollection hashValueCollection = null;

		/// <summary>Support for the array initalizer.</summary>
		/// <param name="value">Value that is added to the array part.</param>
		public void Add(object value)
			=> ArrayOnlyAdd(value);

		/// <summary>Support for a key value initializer.</summary>
		/// <param name="key">key of the value.</param>
		/// <param name="value">value</param>
		public void Add(object key, object value)
			=> SetValue(key, value, true);

		void IDictionary<object, object>.Add(object key, object value)
		{
			if (ContainsKey(key))
				throw new ArgumentException(String.Format(Properties.Resources.rsTableAddDuplicate, key));

			SetValue(key, value, true);
		} // proc IDictionary<object, object>.Add

		bool IDictionary<object, object>.TryGetValue(object key, out object value)
			=> (value = GetValue(key, true)) != null;

		bool IDictionary<object, object>.ContainsKey(object key)
			=> ContainsKey(key);

		bool IDictionary<object, object>.Remove(object key)
		{
			if (ContainsKey(key))
			{
				SetValue(key, null, true);
				return true;
			}
			else
				return false;
		} // func IDictionary<object, object>.Remove

		ICollection<object> IDictionary<object, object>.Keys
		{
			get
			{
				if (hashKeyCollection == null)
					hashKeyCollection = new LuaTableHashKeyCollection(this);
				return hashKeyCollection;
			}
		} // IDictionary<object, object>.Keys

		ICollection<object> IDictionary<object, object>.Values
		{
			get
			{
				if (hashValueCollection == null)
					hashValueCollection = new LuaTableHashValueCollection(this);
				return hashValueCollection;
			}
		} // func IDictionary<object, object>.Values

		object IDictionary<object, object>.this[object key]
		{
			get { return GetValue(key, true); }
			set { SetValue(key, value, true); }
		} // prop IDictionary<object, object>.this

		/// <summary>Implementation of the lua next.</summary>
		/// <param name="next"></param>
		/// <returns></returns>
		public object NextKey(object next)
		{
			object NextHashKey(int startIndex)
			{
				var entryIndex = Array.FindIndex(entries, startIndex, c => c.hashCode != -1);
				return entryIndex == -1 ? null : entries[entryIndex].key;
			} // func NextHashKey

			object NextHashKey2(int currrentIndex)
			{
				if (currrentIndex < 0 || currrentIndex == entries.Length - 1)
					return null;
				return NextHashKey(currrentIndex + 1);
			} // func NextHashKey2

			switch (next)
			{
				case null:
					if (arrayLength == 0)
						return NextHashKey(hiddenMemberCount);
					else
						return 1;
				case int key:
					if (key < arrayLength)
						return key + 1;
					else if (key < arrayList.Length)
					{
						// zero based until, now
						while (key < arrayList.Length)
						{
							if (arrayList[key] != null)
								return key + 1;
							key++;
						}
						return NextHashKey(hiddenMemberCount);
					}
					else
						goto default;
				case string memberName:
					return NextHashKey2(FindKey(next, GetMemberHashCode(memberName), compareString));
				default:
					return NextHashKey2(FindKey(next, next.GetHashCode() & 0x7FFFFFFF, comparerObject));
			}
		} // func NextKey

		/// <summary>Return visible keys.</summary>
		/// <returns></returns>
		protected virtual IEnumerable<string> GetKeys()
			=> ((IDictionary<object, object>)this).Keys.Select(c => (string)Lua.RtConvertValue(c, typeof(string)));

		#endregion

		#region -- ICollection<KeyValuePair<object, object>> ------------------------------

		void ICollection<KeyValuePair<object, object>>.Add(KeyValuePair<object, object> item)
		{
			if (ContainsKey(item.Key))
				throw new ArgumentException(String.Format(Properties.Resources.rsTableAddDuplicate, item.Key));

			SetValue(item.Key, item.Value);
		} // proc ICollection<KeyValuePair<object, object>>.Add

		bool ICollection<KeyValuePair<object, object>>.Remove(KeyValuePair<object, object> item)
		{
			if (ContainsKey(item.Key))
			{
				SetValue(item.Key, null);
				return true;
			}
			else
				return false;
		} // func ICollection<KeyValuePair<object, object>>.Remove

		void ICollection<KeyValuePair<object, object>>.Clear()
		{
			Clear();
		} // proc ICollection<KeyValuePair<object, object>>.Clear

		bool ICollection<KeyValuePair<object, object>>.Contains(KeyValuePair<object, object> item)
			=> ContainsKey(item.Key);

		void ICollection<KeyValuePair<object, object>>.CopyTo(KeyValuePair<object, object>[] array, int arrayIndex)
		{
			if (arrayIndex + count > array.Length)
				throw new ArgumentOutOfRangeException();

			// copy the array part
			for (var i = 0; i < arrayList.Length; i++)
			{
				if (arrayList[i] != null)
					array[arrayIndex++] = new KeyValuePair<object, object>(i + 1, arrayList[i]);
			}

			// copy the class part
			for (var i = hiddenMemberCount; i < classDefinition.Count; i++)
			{
				object value = GetClassMemberValue(i, null, true);
				if (value != null)
					array[arrayIndex++] = new KeyValuePair<object, object>(entries[i].key, value);
			}

			// copy the  hash part
			for (var i = classDefinition.Count; i < entries.Length; i++)
			{
				if (entries[i].hashCode != -1)
					array[arrayIndex++] = new KeyValuePair<object, object>(entries[i].key, entries[i].value);
			}
		} // proc ICollection<KeyValuePair<object, object>>.CopyTo

		int ICollection<KeyValuePair<object, object>>.Count => count - hiddenMemberCount;
		bool ICollection<KeyValuePair<object, object>>.IsReadOnly => false;

		#endregion

		#region -- IEnumerator<object, object> members ------------------------------------

		/// <summary></summary>
		/// <returns></returns>
		public IEnumerator<KeyValuePair<object, object>> GetEnumerator()
		{
			int iVersion = this.version;

			// enumerate the array part
			for (int i = 0; i < arrayList.Length; i++)
			{
				if (iVersion != this.version)
					throw new InvalidOperationException();

				if (arrayList[i] != null)
					yield return new KeyValuePair<object, object>(i + 1, arrayList[i]);
			}

			// enumerate the class part
			for (int i = hiddenMemberCount; i < classDefinition.Count; i++)
			{
				if (iVersion != this.version)
					throw new InvalidOperationException();

				object value = GetClassMemberValue(i, null, true);
				if (value != null)
					yield return new KeyValuePair<object, object>(entries[i].key, value);
			}

			// enumerate the hash part
			for (int i = classDefinition.Count; i < entries.Length; i++)
			{
				if (iVersion != this.version)
					throw new InvalidOperationException();

				if (entries[i].hashCode != -1)
					yield return new KeyValuePair<object, object>(entries[i].key, entries[i].value);
			}
		} // func IEnumerator<KeyValuePair<object, object>>

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
			=> GetEnumerator();

		#endregion

		/// <summary>Returns or sets an value in the lua-table.</summary>
		/// <param name="index">Index.</param>
		/// <returns>Value or <c>null</c></returns>
		public object this[int index] { get => GetArrayValue(index, false); set => SetArrayValue(index, value, false); }
		/// <summary>Returns or sets an value in the lua-table.</summary>
		/// <param name="key">Index.</param>
		/// <returns>Value or <c>null</c></returns>
		public object this[string key] { get => GetMemberValue(key, false, false); set => SetMemberValue(key, value, false, false); }
		/// <summary>Returns or sets an value in the lua-table.</summary>
		/// <param name="key">Index.</param>
		/// <returns>Value or <c>null</c></returns>
		public object this[object key] { get => GetValue(key, false); set => SetValue(key, value, false); }
		/// <summary>Returns or sets an value in the lua-table.</summary>
		/// <param name="keyList">Index list.</param>
		/// <returns>Value or <c>null</c></returns>
		public object this[params object[] keyList] { get => GetValue(keyList, false); set => SetValue(keyList, value, false); }

		/// <summary>Access to the array part</summary>
		public IList<object> ArrayList => new ArrayImplementation(this);
		/// <summary>Access to all members</summary>
		public IDictionary<string, object> Members => new MemberImplementation(this);
		/// <summary>Access to all values.</summary>
		public IDictionary<object, object> Values => this;

		/// <summary>Length if it is an array.</summary>
		public int Length => arrayLength;
		/// <summary>Access to the __metatable</summary>
		[LuaMember(csMetaTable)]
		public LuaTable MetaTable { get => metaTable; set => metaTable = value; }

		// -- Static --------------------------------------------------------------

		private static readonly IEqualityComparer comparerObject = EqualityComparer<object>.Default;
		private static readonly IEqualityComparer comparerInt = EqualityComparer<int>.Default;
		private static readonly IEqualityComparer compareString = StringComparer.Ordinal;
		private static readonly IEqualityComparer compareStringIgnoreCase = StringComparer.OrdinalIgnoreCase;

		private static readonly LuaTableEntry[] emptyLuaEntries = new LuaTableEntry[0];
		private static readonly object[] emptyObjectArray = new object[0];
		private static readonly int[] emptyIntArray = new int[0];

		#region -- Table Manipulation ---------------------------------------------------

		#region -- concat --

		/// <summary></summary>
		/// <param name="t"></param>
		/// <param name="sep"></param>
		/// <param name="i"></param>
		/// <param name="j"></param>
		/// <returns></returns>
		public static string concat(LuaTable t, string sep = null, Nullable<int> i = null, Nullable<int> j = null)
		{
			if (!i.HasValue)
				i = 1;
			if (!j.HasValue)
				j = t.arrayLength;

			var r = collect<string>(t, i.Value, j.Value, null);
			return r == null ? String.Empty : String.Join(sep == null ? String.Empty : sep, r);
		} // func concat

		#endregion

		#region -- insert --

		/// <summary></summary>
		/// <param name="t"></param>
		/// <param name="value"></param>
		public static void insert(LuaTable t, object value)
		{
			// the pos is optional
			insert(t, t.Length <= 0 ? 1 : t.Length + 1, value);
		} // proc insert

		/// <summary></summary>
		/// <param name="t"></param>
		/// <param name="pos"></param>
		/// <param name="value"></param>
		public static void insert(LuaTable t, object pos, object value)
		{
			if (value == null && pos != null) // check for wrong overload
				insert(t, pos);
			else
			{
				// insert the value at the position
				int index;
				if (IsIndexKey(pos, out index) && index >= 1 && index <= t.arrayLength + 1)
					t.ArrayOnlyInsert(index - 1, value);
				else
					t.SetValue(pos, value, true);
			}
		} // proc insert

		#endregion

		#region -- move --

		/// <summary></summary>
		/// <param name="t1"></param>
		/// <param name="f"></param>
		/// <param name="e"></param>
		/// <param name="t"></param>
		public static void move(LuaTable t1, int f, int e, int t)
			=> move(t1, f, e, t, t1);

		/// <summary></summary>
		/// <param name="t1"></param>
		/// <param name="f"></param>
		/// <param name="e"></param>
		/// <param name="t"></param>
		/// <param name="t2"></param>
		public static void move(LuaTable t1, int f, int e, int t, LuaTable t2)
		{
			if (f < 0)
				throw new ArgumentOutOfRangeException(nameof(f));
			if (t < 0)
				throw new ArgumentOutOfRangeException(nameof(t));
			if (f > e)
				return;

			while (f < e)
				t2[t++] = t1[f++];
		} // proc move

		#endregion

		#region -- merge --

		/// <summary>merge the second table into the first one.</summary>
		/// <param name="targetTable"></param>
		/// <param name="mergeTable"></param>
		/// <param name="overwrite"></param>
		/// <returns></returns>
		public static LuaTable merge(LuaTable targetTable, LuaTable mergeTable, bool overwrite = true)
		{
			foreach (var kv in mergeTable)
			{
				if (kv.Value is LuaTable m)
				{
					var v = targetTable[kv.Key];
					if (v is LuaTable t)
						merge(t, m, overwrite);
					else if (v == null || (v != null && overwrite))
						targetTable[kv.Key] = merge(new LuaTable(), m, true);
				}
				else if (overwrite || kv.Value == null)
					targetTable[kv.Key] = kv.Value;
			}

			return targetTable;
		} // proc merge

		#endregion

		#region -- pack --

		/// <summary>Returns a new table with all parameters stored into keys 1, 2, etc. and with a field &quot;n&quot; 
		/// with the total number of parameters. Note that the resulting table may not be a sequence.</summary>
		/// <param name="values"></param>
		/// <returns></returns>
		public static LuaTable pack(object[] values)
		{
			var t = new LuaTable(values);
			t.SetMemberValueIntern("n", values.Length, false, true, false, false); // set the element count, because it can be different
			return t;
		} // func pack

		/// <summary>Returns a new table with all parameters stored into keys 1, 2, etc. and with a field &quot;n&quot; 
		/// with the total number of parameters. Note that the resulting table may not be a sequence.</summary>
		/// <param name="values"></param>
		/// <returns></returns>
		public static LuaTable pack<T>(T[] values)
		{
			var v = new object[values.Length];
			for (var i = 0; i < values.Length; i++)
				v[i] = values[i];
			return pack(v);
		} // func pack

		#endregion

		#region -- remove --

		/// <summary>Removes from list the last element.</summary>
		/// <param name="t"></param>
		public static object remove(LuaTable t)
			=> remove(t, t.Length);

		/// <summary>Removes from list the element at position pos, returning the value of the removed element.</summary>
		/// <param name="t"></param>
		/// <param name="pos"></param>
		public static object remove(LuaTable t, int pos)
		{
			object r;
			int index;
			if (IsIndexKey(pos, out index))
			{
				if (index >= 1 && index <= t.arrayLength)  // remove the element and shift the follower
				{
					r = t.arrayList[index - 1];
					t.ArrayOnlyRemoveAt(index - 1);
				}
				else
				{
					r = t.GetArrayValue(index, true);
					t.SetArrayValue(index, null, true); // just remove the element
				}
			}
			else
			{
				r = t.GetValue(pos, true);
				t.SetValue(pos, null, true); // just remove the key
			}
			return r;
		} // proc remove

		#endregion

		#region -- sort --

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private sealed class SortComparer : IComparer<object>
		{
			private readonly LuaTable t;
			private readonly object compare;

			public SortComparer(LuaTable t, object compare)
			{
				this.t = t;
				this.compare = compare;
			} // ctor

			public int Compare(object x, object y)
			{
				if (compare == null)
					return Comparer<object>.Default.Compare(x, y);
				else
				{
					// Call the comparer
					var r = t.RtInvokeSite(compare, x, y);
					if (r is LuaResult result)
						r = result[0];

					// check the value
					if (r is int i)
						return i;
					else if (r is bool b)
						return b ? -1 : 1;
					else if ((bool)Lua.RtConvertValue(r, typeof(bool)))
						return -1;
					else if (Comparer<object>.Default.Compare(x, y) == 0)
						return 0;
					else
						return 1;
				}
			} // func Compare
		} // class SortComparer

		/// <summary></summary>
		/// <param name="t"></param>
		/// <param name="sort"></param>
		public static void sort(LuaTable t, object sort = null)
			=> Array.Sort(t.arrayList, 0, t.arrayLength, new SortComparer(t, sort));

		#endregion

		#region -- unpack --

		/// <summary>Returns the elements from the given table.</summary>
		/// <param name="t"></param>
		/// <returns></returns>
		public static LuaResult unpack(LuaTable t)
			=> unpack(t, 1, t.Length);

		/// <summary>Returns the elements from the given table.</summary>
		/// <param name="t"></param>
		/// <param name="i"></param>
		/// <returns></returns>
		public static LuaResult unpack(LuaTable t, int i)
			=> unpack(t, i, t.Length);

		/// <summary>Returns the elements from the given table.</summary>
		/// <param name="t"></param>
		/// <param name="i"></param>
		/// <param name="j"></param>
		/// <returns></returns>
		public static LuaResult unpack(LuaTable t, int i, int j)
			=> new LuaResult(LuaResult.CopyMode.None, unpack(t, i, j, LuaResult.Empty.Values));

		/// <summary>Returns the elements from the given table as a sequence.</summary>
		/// <param name="t"></param>
		/// <param name="i"></param>
		/// <param name="j"></param>
		/// <param name="empty">Return value for empty lists</param>
		/// <returns></returns>
		public static T[] unpack<T>(LuaTable t, int i, int j, T[] empty)
		{
			if (j < i)
				return empty;

			T[] list = new T[j - i + 1];
			for (int k = 0; k < list.Length; k++)
				list[k] = (T)Lua.RtConvertValue(t[k + i], typeof(T));

			return list;
		} // func unpack

		#endregion

		#region -- collect --

		/// <summary>Returns the elements from the given table as a sequence.</summary>
		/// <param name="t"></param>
		/// <returns></returns>
		public static LuaResult collect(LuaTable t)
			=> collect(t, 1, t.Length);

		/// <summary>Returns the elements from the given table as a sequence.</summary>
		/// <param name="t"></param>
		/// <param name="i"></param>
		/// <returns></returns>
		public static LuaResult collect(LuaTable t, int i)
			=> collect(t, i, t.Length);

		/// <summary>Returns the elements from the given table as a sequence.</summary>
		/// <param name="t"></param>
		/// <param name="i"></param>
		/// <param name="j"></param>
		/// <returns></returns>
		public static LuaResult collect(LuaTable t, int i, int j)
			=> new LuaResult(LuaResult.CopyMode.None, collect(t, i, j, LuaResult.Empty.Values));

		/// <summary>Returns the elements from the given table as a sequence.</summary>
		/// <param name="t"></param>
		/// <param name="i"></param>
		/// <param name="j"></param>
		/// <param name="empty">Return value for empty lists</param>
		/// <returns></returns>
		public static T[] collect<T>(LuaTable t, int i, int j, T[] empty)
		{
			if (j < i)
				return empty;

			if (i >= 1 && i <= t.arrayLength && j >= 1 && j <= t.arrayLength) // within the array
			{

				var list = new T[j - i + 1];

				// convert the values
				int iLength = list.Length;
				for (int k = 0; k < iLength; k++)
					list[k] = (T)Lua.RtConvertValue(t.arrayList[i + k - 1], typeof(T));

				return list;
			}
			else
			{
				var indexList = new List<KeyValuePair<int, T>>(Math.Max(Math.Min(j - i + 1, t.count), 1));

				// scan array part
				if (i <= t.arrayList.Length && j >= 1)
				{
					int idxStart = Math.Max(i - 1, 0);
					int idxEnd = Math.Min(t.arrayList.Length - 1, j - 1);
					for (int k = idxStart; k <= idxEnd; k++)
						if (t.arrayList[k] != null)
							indexList.Add(new KeyValuePair<int, T>(k + 1, (T)Lua.RtConvertValue(t.arrayList[k], typeof(T))));
				}

				// scan hash part
				for (var k = 0; k < t.entries.Length; k++)
				{
					if (t.entries[k].key is int)
					{
						int l = (int)t.entries[k].key;
						if (l >= i && l <= j)
							indexList.Add(new KeyValuePair<int, T>(l, (T)Lua.RtConvertValue(t.entries[k].value, typeof(T))));
					}
				}

				if (indexList.Count == 0)
					return empty;
				else
				{
					// sort the result
					indexList.Sort((a, b) => a.Key - b.Key);

					// create the result array
					var result = new T[indexList.Count];
					for (int k = 0; k < result.Length; k++)
						result[k] = indexList[k].Value;

					return result;
				}
			}
		} // func unpack

		#endregion

		#endregion

		#region -- Lua Script Object Notation -- To -----------------------------------


		/// <summary>Convert the table to a string</summary>
		/// <param name="table"></param>
		/// <param name="tw"></param>
		/// <param name="prettyFormatting"></param>
		/// <param name="currentLevel"></param>
		/// <param name="indent"></param>
		public static void ToLsonCore(LuaTable table, TextWriter tw, bool prettyFormatting = true, string indent = "\t", int currentLevel = 1)
		{
			void WriteIndent()
			{
				if (!prettyFormatting)
					return;

				tw.WriteLine();
				for (var i = 0; i < currentLevel; i++)
					tw.Write(indent);
			} // proc WriteIndent

			bool IsMember(string member)
			{
				if ((member?.Length ?? 0) == 0)
					return false;

				if (!Char.IsLetter(member[0]) && member[0] != '_')
					return false;

				for (var i = 1; i < member.Length; i++)
				{
					if (!LuaLexer.IsIdentifierChar(member[i]))
						return false;
				}

				if (LuaLexer.IsKeyWord(member))
					return false;

				return true;
			} // func IsMember

			void WriteMember(string member)
			{
				tw.Write(member);
			} // proc WriteMember

			void WriteKey(object key)
			{
				tw.Write("[");
				Lua.RtWriteValue(tw, key, prettyFormatting, currentLevel + 1, indent);
				tw.Write("]");
			} // proc WriteKey

			if (tw == null)
				throw new ArgumentNullException(nameof(tw));

			if (currentLevel > 100)
				throw new ArgumentOutOfRangeException(nameof(table), Properties.Resources.rsTableRecursionLevelError);

			var lastIndex = 0;
			if (table.Values.Count > 0)
			{
				tw.Write("{");
				var first = true;
				var skipCommand = false;
				foreach (var kv in table.Values)
				{
					// comma
					if (skipCommand)
						skipCommand = false;
					else
					{
						if (first)
							first = false;
						else
							tw.Write(',');
					}

					// formatting
					WriteIndent();

					// use array notation
					var isIndex = false;
					if ((isIndex = IsIndexKey(kv.Key, out var index)) && lastIndex + 1 == index && kv.Value != null)
					{
						lastIndex = index;
						Lua.RtWriteValue(tw, kv.Value, prettyFormatting, currentLevel + 1, indent);
					}
					else if (kv.Value != null) // use key/value pair notation
					{
						if (isIndex)
							WriteKey(index);
						else if (kv.Key is string member && IsMember(member))
							WriteMember(member);
						else
							WriteKey(kv.Key);
						tw.Write(prettyFormatting ? " = " : "=");
						Lua.RtWriteValue(tw, kv.Value, prettyFormatting, currentLevel + 1, indent);
					}
					else
						skipCommand = true;
				}

				// formatting
				currentLevel--;
				WriteIndent();

				tw.Write("}");
			}
			else
				tw.Write("{}");
		}

		/// <summary>Convert the table to a string</summary>
		/// <param name="table"></param>
		/// <param name="prettyFormatting"></param>
		/// <param name="indent"></param>
		public static string ToLson(LuaTable table, bool prettyFormatting = true, string indent = "\t")
		{
			using (var sw = new StringWriter())
			{
				ToLsonCore(table, sw, prettyFormatting, indent, 1);
				return sw.GetStringBuilder().ToString();
			}
		} // func ToLson

		/// <summary>Convert the table to a string</summary>
		/// <param name="prettyFormatting"></param>
		/// <param name="indent"></param>
		/// <returns></returns>
		public string ToLson(bool prettyFormatting = true, string indent = "\t")
			=> ToLson(this, prettyFormatting, indent);

		#endregion

		#region -- Lua Script Object Notation -- From ---------------------------------

		/// <summary></summary>
		/// <param name="lex"></param>
		/// <returns></returns>
		public static LuaTable FromLson(ILuaLexer lex)
		{
			var result = new LuaTable();

			void ParseTableField(ref int defaultIndex)
			{
				if (lex.Current.Typ == LuaToken.BracketSquareOpen)
				{
					// Parse the index
					lex.Next();
					var index = Lua.RtReadValue(lex);
					Parser.FetchToken(LuaToken.BracketSquareClose, lex);
					Parser.FetchToken(LuaToken.Assign, lex);

					// Expression that results in a value
					result[index] = Lua.RtReadValue(lex);
				}
				else if (lex.Current.Typ == LuaToken.Identifier && lex.LookAhead.Typ == LuaToken.Assign)
				{
					// Read the identifier
					var memberName = lex.Current.Value;
					lex.Next();
					Parser.FetchToken(LuaToken.Assign, lex);

					// Expression
					result[memberName] = Lua.RtReadValue(lex);
				}
				else
				{
					result[defaultIndex++] = Lua.RtReadValue(lex);
				}
			} // proc ParseTableField

			if (Parser.FetchToken(LuaToken.BracketCurlyOpen, lex, true) != null)
			{
				if (lex.Current.Typ != LuaToken.BracketCurlyClose)
				{
					var index = 1;

					// fiest field
					ParseTableField(ref index);

					// collect more table fields
					while (lex.Current.Typ == LuaToken.Comma || lex.Current.Typ == LuaToken.Semicolon)
					{
						lex.Next();

						// Optional last separator
						if (lex.Current.Typ == LuaToken.BracketCurlyClose)
							break;

						// Parse the field
						ParseTableField(ref index);
					}

					// Closing bracket
					Parser.FetchToken(LuaToken.BracketCurlyClose, lex);
				}
				else
					Parser.FetchToken(LuaToken.BracketCurlyClose, lex, false);
			}
			else
				throw Parser.ParseError(lex.Current, String.Format(Properties.Resources.rsParseUnexpectedToken, LuaLexer.GetTokenName(lex.Current.Typ), "{"));

			return result;
		} // proc FromLson

		/// <summary></summary>
		/// <param name="tr"></param>
		/// <returns></returns>
		public static LuaTable FromLson(TextReader tr)
		{
			using (var lex = LuaLexer.Create("lson.lua", tr))
			{
				lex.Next();
				return FromLson(lex);
			}
		} // proc FromLson

		/// <summary>Parse a lua table</summary>
		/// <param name="value"></param>
		/// <returns></returns>
		public static LuaTable FromLson(string value)
		{
			using (var tr = new StringReader(value))
				return FromLson(tr);
		} // func FromLson

		#endregion

		#region -- Java Script Object Notation -- To ----------------------------------

		private static void ToJson(LuaTable table, TextWriter tw, bool prettyFormatted, int currentLevel, string indent)
		{
			void WriteIndent()
			{
				if (!prettyFormatted)
					return;

				tw.WriteLine();
				for (var i = 0; i < currentLevel; i++)
					tw.Write(indent);
			} // proc WriteIndent

			void WriteValue(object value)
			{
				var type = value.GetType();
				var typeCode = LuaEmit.GetTypeCode(type);
				switch (typeCode)
				{
					case LuaEmitTypeCode.Boolean:
						tw.Write((bool)value ? "true" : "false");
						break;
					case LuaEmitTypeCode.String:
						{
							var s = (string)value;
							tw.Write("\"");
							for (var i = 0; i < s.Length; i++)
							{
								switch (s[i])
								{
									case '\0':
										tw.Write("\\u0000");
										break;
									case '\\':
										tw.Write("\\\\");
										break;
									case '"':
										tw.Write("\\\"");
										break;
									case '\b':
										tw.Write("\\b");
										break;
									case '\n':
										tw.Write("\\n");
										break;
									case '\r':
										tw.Write("\\r");
										break;
									case '\t':
										tw.Write("\\t");
										break;
									default:
										tw.Write(s[i]);
										break;
								}
							}
							tw.Write("\"");
						}
						break;
					case LuaEmitTypeCode.Char:
						value = value.ToString();
						goto case LuaEmitTypeCode.String;

					case LuaEmitTypeCode.Byte:
					case LuaEmitTypeCode.SByte:
					case LuaEmitTypeCode.Int16:
					case LuaEmitTypeCode.UInt16:
					case LuaEmitTypeCode.Int32:
					case LuaEmitTypeCode.UInt32:
					case LuaEmitTypeCode.Int64:
					case LuaEmitTypeCode.UInt64:
						tw.Write(value);
						break;

					case LuaEmitTypeCode.Single:
					case LuaEmitTypeCode.Double:
					case LuaEmitTypeCode.Decimal:
						{
							var num = Convert.ToString(value, CultureInfo.InvariantCulture);
							if (num.IndexOfAny(new char[] { '.', 'e', 'E' }) == -1)
							{
								tw.Write(num);
								tw.Write(".0");
							}
							else
								tw.Write(num);
						}
						break;

					case LuaEmitTypeCode.DateTime:
						value = ((DateTime)value).ToString("o"); // ISO8601
						goto case LuaEmitTypeCode.String;

					case LuaEmitTypeCode.Object:
						if (type == typeof(LuaTable))
						{
							ToJson((LuaTable)value, tw, prettyFormatted, currentLevel + 1, indent);
							break;
						}
						else if (type == typeof(Guid))
						{
							value = ((Guid)value).ToString("B");
							goto case LuaEmitTypeCode.String;
						}
						else if (type == typeof(char[]))
						{
							value = new string((char[])value);
							goto case LuaEmitTypeCode.String;
						}
						else
							goto default;
					default:
						throw new ArgumentException(String.Format(Properties.Resources.rsTypeIsNotSupported, type.Name));
				}
			} // proc WriteValue

			void WriteKey(object key)
				=> WriteValue(key.ToString());

			if (currentLevel > 100)
				throw new ArgumentOutOfRangeException(nameof(table), Properties.Resources.rsTableRecursionLevelError);

			// test if table has members
			var isObject = table.Members.Count > 0;

			var lastIndex = 0;
			if (table.Values.Count > 0)
			{
				tw.Write(isObject ? "{" : "[");
				var first = true;
				foreach (var kv in table.Values)
				{
					// comma
					if (kv.Value == null)
						continue;
					else
					{
						if (first)
							first = false;
						else
							tw.Write(',');
					}

					// formatting
					if (isObject)
						WriteIndent();

					// write values
					if (isObject)
					{
						WriteKey(kv.Key);
						tw.Write(prettyFormatted ? ": " : ":");
						WriteValue(kv.Value);
					}
					else if (IsIndexKey(kv.Key, out var index))
					{
						while (++lastIndex < index)
							tw.Write(prettyFormatted ? "null ," : "null,");
						WriteValue(kv.Value);
						lastIndex = index;
					}
					else
						throw new ArgumentOutOfRangeException("key", kv.Key, "Index expected.");

				}

				// formatting
				currentLevel--;
				if (isObject)
					WriteIndent();

				tw.Write(isObject ? "}" : "]");
			}
			else
				tw.Write("{}");
		} // proc ToLson

		/// <summary>Convert the table to a json-string</summary>
		/// <param name="table"></param>
		/// <param name="prettyFormatting"></param>
		/// <param name="indent"></param>
		public static string ToJson(LuaTable table, bool prettyFormatting = true, string indent = "\t")
		{
			using (var sw = new StringWriter())
			{
				ToJson(table, sw, prettyFormatting, indent);
				return sw.GetStringBuilder().ToString();
			}
		} // func ToLson

		/// <summary>Convert the table to a json-string</summary>
		/// <param name="table"></param>
		/// <param name="tw"></param>
		/// <param name="prettyFormatting"></param>
		/// <param name="indent"></param>
		public static void ToJson(LuaTable table, TextWriter tw, bool prettyFormatting = true, string indent = "\t")
			=> ToJson(table, tw, prettyFormatting, 1, indent);

		/// <summary>Convert the table to a json-string</summary>
		/// <param name="prettyFormatting"></param>
		/// <param name="indent"></param>
		/// <returns></returns>
		public string ToJson(bool prettyFormatting = true, string indent = "\t")
			=> ToJson(this, prettyFormatting, indent);

		#endregion

		#region -- Java Script Object Notation - From ---------------------------------

		private static LuaTable FromJsonParse(LuaCharLexer lex)
		{
			void ParseWhitespaces()
			{
				while (!lex.IsEof && Char.IsWhiteSpace(lex.Cur))
					lex.Next();
			} // proc ParseWhitespaces

			void CheckEof()
			{
				if (lex.IsEof)
					throw new LuaParseException(lex.CurrentPosition, "Unexpected eof.");
			} // proc CheckEof

			Exception UnExpected(string expected)
				=> new LuaParseException(lex.CurrentPosition, String.Format("{0} (found: {1})", expected, lex.Cur)); // todo: translate

			void ParseChar(char c, string expected = null)
			{
				if (lex.Cur != c)
					throw UnExpected(expected ?? "Expected " + c);
				lex.Next();
			} // func ParseChar

			int ParseHex(int nums)
			{
				var value = 0;

				while (nums-- > 0)
				{
					var c = lex.Cur;

					value <<= 4;

					if (c >= '0' && c <= '9')
						value |= c - '0';
					else if (c >= 'A' && c <= 'F')
						value |= c - 'A' + 10;
					else if (c >= 'a' && c <= 'f')
						value |= c - 'a' + 10;
					else
						throw UnExpected("Hex expected.");
				}

				return value;
			} // func ParseHex

			string GetCurrentValue()
			{
				var v = lex.CurValue;
				lex.ResetCurValue();
				return v;
			} // func GetCurrentValue

			string ParseIdentifier()
			{
				while (Char.IsLetterOrDigit(lex.Cur))
					lex.Eat();
				return GetCurrentValue();
			} // func ParseIdentifier

			object ParseNumber()
			{
				var state = 0;

				var isDouble = false;
				var isNeg = lex.Cur == '-';
				if (isNeg)
					lex.Next();

				var isNegExp = false;
				var expValue = 0;
				var fractionDigit = 0;
				var integerValue = 0L;
				var doubleValue = 0.0;
				var isParsing = true;
				const long integerBorder = Int64.MaxValue / 10;
				while (isParsing)
				{
					CheckEof();

					var c = lex.Cur;
					switch (state)
					{
						case 0: // integer part
							if (c == '.')
							{
								if (!isDouble)
								{
									doubleValue = integerValue;
									isDouble = true;
								}
								state = 1;
							}
							else if (c == 'E' || c == 'e')
							{
								if (!isDouble)
								{
									doubleValue = integerValue;
									isDouble = true;
								}
								state = 10;
							}
							else if (c >= '0' && c <= '9')
							{
								if (integerValue > integerBorder)
								{
									doubleValue = integerValue;
									isDouble = true;
								}
								if (isDouble)
									doubleValue = doubleValue * 10 + (c - '0');
								else
									integerValue = integerValue * 10 + (c - '0');
							}
							else
								isParsing = false;
							break;

						case 1: // fraction
							if (c == 'E' || c == 'e')
							{
								if (!isDouble)
								{
									doubleValue = integerValue;
									isDouble = true;
								}
								state = 10;
							}
							else if (c >= '0' && c <= '9')
							{
								fractionDigit++;
								doubleValue = doubleValue * 10 + (c - '0');
							}
							else
								isParsing = false;
							break;

						case 10:
							if (c == '-')
								isNegExp = true;
							else if (c == '+')
								isNegExp = false;
							else
							{
								state = 11;
								goto case 11;
							}
							break;

						case 11:
							if (c >= '0' && c <= '9')
								expValue = expValue * 10 + (c - '0');
							else
								isParsing = false;
							break;

						default:
							throw new InvalidOperationException();
					}

					if (isParsing)
						lex.Next();
				}

				if (isDouble)
				{
					if (isNeg)
						doubleValue = -doubleValue;
					if (isNegExp)
						expValue = -expValue;

					var realExp = expValue - fractionDigit;
					doubleValue = doubleValue * Math.Pow(10, realExp);

					return doubleValue;
				}
				else if (integerValue < Int32.MaxValue)
					return isNeg ? -(int)integerValue : (int)integerValue;
				else
					return isNeg ? -integerValue : integerValue;
			} // func ParseNumber

			string ParseString(bool asMember)
			{
				ParseChar('"', asMember ? "Member expected." : "String expected.");

				while (lex.Cur != '"')
				{
					CheckEof();

					#region -- escape sequence --
					if (lex.Cur == '\\')
					{
						lex.Next();
						switch (lex.Cur)
						{
							case '"':
								lex.Eat();
								break;
							case '\\':
								lex.Eat();
								break;
							case '/':
								lex.Eat();
								break;
							case 'b':
								lex.AppendValue('\b');
								lex.Next();
								break;
							case 'n':
								lex.AppendValue('\n');
								lex.Next();
								break;
							case 'r':
								lex.AppendValue('\r');
								lex.Next();
								break;
							case 't':
								lex.AppendValue('\t');
								lex.Next();
								break;
							case 'u':
								lex.Next();
								lex.AppendValue((char)ParseHex(4));
								break;
							default:
								throw new LuaParseException(lex.CurrentPosition, "Invalid escape sequence.");
						}
					}
					#endregion
					else
						lex.Eat();
				}
				lex.Next();

				return GetCurrentValue();
			} // func ParseString

			LuaTable ParseArray()
			{
				var table = new LuaTable();

				ParseChar('[');
				ParseWhitespaces();

				while (lex.Cur != ']')
				{
					CheckEof();

					var value = ParseElement();
					if (value != null)
						table.ArrayOnlyAdd(value);

					if (lex.Cur == ',')
					{
						lex.Next();
						ParseWhitespaces();
					}
					else if (lex.Cur != ']')
						throw UnExpected(", expected.");
				}
				lex.Next();

				return table;
			} // func ParseArray

			LuaTable ParseObject()
			{
				var table = new LuaTable();

				ParseChar('{');
				ParseWhitespaces();

				while (lex.Cur != '}')
				{
					CheckEof();

					var memberName = ParseString(true);
					ParseWhitespaces();
					ParseChar(':', "Value expected.");
					var value = ParseElement();
					if (value != null)
						table[memberName] = value;

					if (lex.Cur == ',')
					{
						lex.Next();
						ParseWhitespaces();
					}
					else if (lex.Cur != '}')
						throw UnExpected(", expected.");
				}
				lex.Next();

				return table;
			} // func ParseObject

			object ParseValue()
			{
				switch (lex.Cur)
				{
					case '{': // object
						return ParseObject();
					case '[': // array
						return ParseArray();
					case '"': // string
						return ParseString(false);
					case '0':
					case '1':
					case '2':
					case '3':
					case '4':
					case '5':
					case '6':
					case '7':
					case '8':
					case '9':
					case '-': // number
						return ParseNumber();
					default:
						if (Char.IsLetter(lex.Cur)) // identifier
						{
							var ident = ParseIdentifier();
							switch (ident)
							{
								case "true":
									return true;
								case "false":
									return false;
								case "null":
									return null;
								default:
									throw new LuaParseException(lex.CurrentPosition, String.Format("Unexpected token: {0}", ident)); // todo: translate
							}
						}
						else
							throw new LuaParseException(lex.CurrentPosition, String.Format("Unexpected character: {0}", lex.Cur)); // todo: translate
				}
			}

			object ParseElement()
			{
				ParseWhitespaces();
				try
				{
					return ParseValue();
				}
				finally
				{
					ParseWhitespaces();
				}
			} // func ReadElement

			var r = ParseElement();
			return r is LuaTable t
				? t
				: new LuaTable() { [1] = r };
		} // func FromJsonParse
		  /// <returns></returns>
		public static LuaTable FromJson(TextReader tr)
		{
			using (var lex = new LuaCharLexer("json", tr, 1))
				return FromJsonParse(lex);
		} // func FromJson

		/// <summary>Parse json to a lua table</summary>
		/// <param name="value"></param>
		/// <returns></returns>
		public static LuaTable FromJson(string value)
		{
			using (var tr = new StringReader(value))
				return FromJson(tr);
		} // func FromJson

		#endregion

		#region -- c#/vb.net operators ------------------------------------------------

		/// <summary></summary>
		/// <param name="table"></param>
		/// <param name="arg"></param>
		/// <returns></returns>
		public static object operator +(LuaTable table, object arg)
			=> table.OnAdd(arg);

		/// <summary></summary>
		/// <param name="table"></param>
		/// <param name="arg"></param>
		/// <returns></returns>
		public static object operator -(LuaTable table, object arg)
			=> table.OnSub(arg);

		/// <summary></summary>
		/// <param name="table"></param>
		/// <param name="arg"></param>
		/// <returns></returns>
		public static object operator *(LuaTable table, object arg)
			=> table.OnMul(arg);

		/// <summary></summary>
		/// <param name="table"></param>
		/// <param name="arg"></param>
		/// <returns></returns>
		public static object operator /(LuaTable table, object arg)
			=> table.OnDiv(arg);

		/// <summary></summary>
		/// <param name="table"></param>
		/// <param name="arg"></param>
		/// <returns></returns>
		public static object operator %(LuaTable table, object arg)
			=> table.OnMod(arg);

		/// <summary></summary>
		/// <param name="table"></param>
		/// <returns></returns>
		public static object operator -(LuaTable table)
			=> table.OnUnMinus();

		/// <summary></summary>
		/// <param name="table"></param>
		/// <param name="arg"></param>
		/// <returns></returns>
		public static bool operator ==(LuaTable table, object arg)
			=> table is null ? arg is null : table.Equals(arg);

		/// <summary></summary>
		/// <param name="table"></param>
		/// <param name="arg"></param>
		/// <returns></returns>
		public static bool operator !=(LuaTable table, object arg)
			=> table is null ? !(arg is null) : !table.Equals(arg);

		/// <summary></summary>
		/// <param name="table"></param>
		/// <param name="arg"></param>
		/// <returns></returns>
		public static object operator <(LuaTable table, object arg)
			=> table.OnLessThan(arg);

		/// <summary></summary>
		/// <param name="table"></param>
		/// <param name="arg"></param>
		/// <returns></returns>
		public static object operator >(LuaTable table, object arg)
			=> !table.OnLessThan(arg);

		/// <summary></summary>
		/// <param name="table"></param>
		/// <param name="arg"></param>
		/// <returns></returns>
		public static object operator <=(LuaTable table, object arg)
			=> table.OnLessEqual(arg);

		/// <summary></summary>
		/// <param name="table"></param>
		/// <param name="arg"></param>
		/// <returns></returns>
		public static object operator >=(LuaTable table, object arg)
			=> !table.OnLessEqual(arg);

		/// <summary></summary>
		/// <param name="table"></param>
		/// <param name="arg"></param>
		/// <returns></returns>
		public static object operator >>(LuaTable table, int arg)
			=> table.OnShr(arg);

		/// <summary></summary>
		/// <param name="table"></param>
		/// <param name="arg"></param>
		/// <returns></returns>
		public static object operator <<(LuaTable table, int arg)
			=> table.OnShl(arg);

		/// <summary></summary>
		/// <param name="table"></param>
		/// <param name="arg"></param>
		/// <returns></returns>
		public static object operator &(LuaTable table, object arg)
			=> table.OnBAnd(arg);

		/// <summary></summary>
		/// <param name="table"></param>
		/// <param name="arg"></param>
		/// <returns></returns>
		public static object operator |(LuaTable table, object arg)
			=> table.OnBOr(arg);

		/// <summary></summary>
		/// <param name="table"></param>
		/// <param name="arg"></param>
		/// <returns></returns>
		public static object operator ^(LuaTable table, object arg)
			=> table.OnBXor(arg);

		/// <summary></summary>
		/// <param name="table"></param>
		/// <returns></returns>
		public static object operator ~(LuaTable table)
			=> table.OnBNot();

		#endregion
	} // class LuaTable

	#endregion
}
