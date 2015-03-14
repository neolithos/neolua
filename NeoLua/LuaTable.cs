using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Dynamic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace Neo.IronLua
{
	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
  public class LuaTable : IDynamicMetaObjectProvider, INotifyPropertyChanged, IDictionary<string, object>, IList<object>, IDictionary<object, object>, ITypedList
	{
		/// <summary>Member name of the metatable</summary>
		public const string csMetaTable = "__metatable";
		private const int HiddenMemberCount = 1; // do not enumerate __metatable

		private const int IndexNotFound = -1;
		private const int RemovedIndex = -3;

		#region -- class LuaTableMetaObject -----------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private sealed class LuaTableMetaObject : DynamicMetaObject
		{
			#region -- Ctor/Dtor ------------------------------------------------------------

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
					LuaEmit.Convert(Lua.GetRuntime(binder), arg.Expression, arg.LimitType, typeof(object), false)
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
					expr = LuaEmit.GetResultExpression(value.Expression, value.LimitType, 0);
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
					return LuaEmit.Convert(Lua.GetRuntime(binder), expr, typeFrom, typeConvertTo, false);
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
							LuaEmit.Convert(Lua.GetRuntime(binder), arg.Expression, arg.LimitType, typeof(int), false),
							exprSet,
							Expression.Constant(false)
						);
						restrictions = restrictions.Merge(BindingRestrictions.GetTypeRestriction(arg.Expression, arg.LimitType));
					}
					else if (arg.LimitType == typeof(uint) || arg.LimitType == typeof(long) || arg.LimitType == typeof(ulong))
					{
						expr = Expression.Call(Lua.EnsureType(Expression, typeof(LuaTable)), Lua.TableSetValueKeyIntMethodInfo,
							LuaEmit.Convert(Lua.GetRuntime(binder), arg.Expression, arg.LimitType, typeof(int), false),
							exprSet,
							Expression.Constant(false)
						);
						restrictions = restrictions.Merge(BindingRestrictions.GetExpressionRestriction(
							Expression.AndAlso(
								Expression.TypeEqual(arg.Expression, arg.LimitType),
								Expression.AndAlso(
									Expression.GreaterThanOrEqual(Lua.EnsureType(arg.Expression, arg.LimitType), Lua.EnsureType(Expression.Constant(1), arg.LimitType)),
									Expression.LessThanOrEqual(Lua.EnsureType(arg.Expression, arg.LimitType), Lua.EnsureType(Expression.Constant(Int32.MaxValue), arg.LimitType))
								)
							)
						));
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
						expr = Lua.ThrowExpression(Properties.Resources.rsTableKeyNotNullable, typeof(object));
						restrictions = restrictions.Merge(BindingRestrictions.GetInstanceRestriction(arg.Expression, null));
					}
					else if (IsIndexKey(arg.LimitType)) // integer access
					{
						expr = Expression.Call(Lua.EnsureType(Expression, typeof(LuaTable)), Lua.TableGetValueKeyIntMethodInfo,
							LuaEmit.Convert(Lua.GetRuntime(binder), arg.Expression, arg.LimitType, typeof(int), false),
							Expression.Constant(false)
						);
						restrictions = restrictions.Merge(BindingRestrictions.GetTypeRestriction(arg.Expression, arg.LimitType));
					}
					else if (arg.LimitType == typeof(uint) || arg.LimitType == typeof(long) || arg.LimitType == typeof(ulong))
					{
						expr = Expression.Call(Lua.EnsureType(Expression, typeof(LuaTable)), Lua.TableGetValueKeyIntMethodInfo,
							LuaEmit.Convert(Lua.GetRuntime(binder), arg.Expression, arg.LimitType, typeof(int), false),
							Expression.Constant(false)
						);
						restrictions = restrictions.Merge(BindingRestrictions.GetExpressionRestriction(
							Expression.AndAlso(
								Expression.TypeEqual(arg.Expression, arg.LimitType),
								Expression.AndAlso(
									Expression.GreaterThanOrEqual(Lua.EnsureType(arg.Expression, arg.LimitType), Lua.EnsureType(Expression.Constant(1), arg.LimitType)),
									Expression.LessThanOrEqual(Lua.EnsureType(arg.Expression, arg.LimitType), Lua.EnsureType(Expression.Constant(Int32.MaxValue), arg.LimitType))
								)
							)
						));
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

			private Expression GetDirectMemberAccess(int iEntryIndex, LuaTablePropertyDefine pd, bool lGenerateRestriction, ref BindingRestrictions restrictions)
			{
				// static property
				bool lStatic = pd.PropertyInfo.GetGetMethod().IsStatic;

				// generate restriction
				if (lGenerateRestriction)
					restrictions = restrictions.Merge(BindingRestrictions.GetExpressionRestriction(Expression.TypeIs(Expression, pd.PropertyInfo.DeclaringType)));

				// access the property (t.value)
				return Expression.Property(
					lStatic ? null : Lua.EnsureType(Expression, pd.PropertyInfo.DeclaringType),
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
							expr = Expression.Assign(exprMember, CreateSetExpresion(binder, value, exprMember.Type, ref restrictions));
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
					BindingRestrictions restrictions = BindingRestrictions.Empty;
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
				else // do the call to a nromal member
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

			public override DynamicMetaObject BindInvokeMember(InvokeMemberBinder binder, DynamicMetaObject[] args)
			{
				LuaTable t = (LuaTable)Value;
				BindingRestrictions restrictions = GetLuaTableRestriction();

				if (binder is Lua.LuaInvokeMemberBinder) // always call member like a member
				{
					// get the member
					Expression exprGetMember = Expression.Call(
						Lua.EnsureType(Expression, typeof(LuaTable)),
						Lua.TableGetValueKeyStringMethodInfo,
						Expression.Constant(binder.Name),
						Expression.Constant(binder.IgnoreCase),
						Expression.Constant(false)
					);

					// add the self parameter
					DynamicMetaObject[] newArgs = new DynamicMetaObject[args.Length + 1];
					newArgs[0] = this;
					for (int i = 0; i < args.Length; i++)
						newArgs[i + 1] = args[i];

					return binder.FallbackInvoke(new DynamicMetaObject(exprGetMember, restrictions), newArgs, null);
				}
				else // get the method and do invoke or a member call
				{
					ParameterExpression exprIsMethod = Expression.Variable(typeof(bool), "isMethod");
					ParameterExpression exprDelegate = Expression.Variable(typeof(object), "delegate");

					// generate member call
					Expression[] exprArgs = new Expression[args.Length + 1];
					exprArgs[0] = exprDelegate;
					for (int i = 0; i < args.Length; i++)
						exprArgs[i + 1] = args[i].Expression;

					Expression exprCallMember = Expression.Dynamic(t.GetInvokeBinder(binder.CallInfo), typeof(object), exprArgs);

					// Method Call
					exprArgs = new Expression[args.Length + 2];
					exprArgs[0] = exprDelegate;
					exprArgs[1] = Expression;
					for (int i = 0; i < args.Length; i++)
						exprArgs[i + 2] = args[i].Expression;

					Expression exprCallMethod = Expression.Dynamic(t.GetInvokeBinder(new CallInfo(binder.CallInfo.ArgumentCount + 1)), typeof(object), exprArgs);

					// Get Member
					Expression expr = Expression.Block(new ParameterExpression[] { exprIsMethod, exprDelegate },
						Expression.Assign(exprDelegate,
							Expression.Call(Lua.EnsureType(Expression, typeof(LuaTable)), Lua.TableGetCallMemberMethodInfo,
								Expression.Constant(binder.Name),
								Expression.Constant(binder.IgnoreCase),
								Expression.Constant(false),
								exprIsMethod
							)
						)
						,
						Expression.Condition(exprIsMethod,
							exprCallMethod,
							exprCallMember
						)
					);
					return new DynamicMetaObject(expr, restrictions.Merge(Lua.GetMethodSignatureRestriction(null, args)));
				}
			} // BindInvokeMember

			#endregion

			#region -- BindConvert ----------------------------------------------------------

			public override DynamicMetaObject BindConvert(ConvertBinder binder)
			{
				// Automatic convert to a special type, only for classes and structure
				if (Type.GetTypeCode(binder.Type) == TypeCode.Object && !binder.Type.IsAssignableFrom(Value.GetType()))
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
			{
				return ((IDictionary<string, object>)Value).Keys;
			} // func GetDynamicMemberNames
		} // class LuaTableMetaObject

		#endregion

		#region -- struct LuaTableEntry ---------------------------------------------------

		private struct LuaTableEntry
		{
			public int hashCode;
			public object key;
			public object value;
			public bool isMethod;

			/// <summary>points to the next entry with the same hashcode</summary>
			public int nextHash;

			public override string ToString()
			{
				return hashCode == -1 ? String.Format("_empty_ next: {0}", nextHash) : String.Format("key: {0}; value: {1}; next:{2}", key ?? "null", value ?? "null", nextHash);
			} // func ToString

			public bool SetValue(object newValue, bool lMarkAsMethod)
			{
				if (comparerObject.Equals(newValue, value) && lMarkAsMethod == isMethod)
					return false;

				value = newValue;
				isMethod = lMarkAsMethod;
				return true;
			} // proc SetValue
		} // struct LuaTableEntry

		#endregion

		#region -- enum LuaTableDefineMode ------------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private enum LuaTableDefineMode
		{
			Init,
			Default,
			Direct
		} // enum LuaTableDefineMode

		#endregion

		#region -- class LuaTableDefine ---------------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private abstract class LuaTableDefine
		{
			// fixed fields for fast (hopefully) access
			public LuaTableDefineMode mode;
			public Func<LuaTable, object> getValue;
			public Action<LuaTable, object> setValue;

			private LuaMemberAttribute info;
      private PropertyDescriptor descriptor;

			protected LuaTableDefine(LuaMemberAttribute info)
			{
				this.info = info;
			} // ctor

			/// <summary>Initial value for the table creation.</summary>
			public abstract object GetInitialValue(LuaTable table);

			public void CollectMember(List<LuaCollectedMember> collected)
			{
				CollectMember(collected, info);
			} // proc CollectMember

			protected abstract void CollectMember(List<LuaCollectedMember> collected, LuaMemberAttribute info);

      protected abstract PropertyDescriptor CreatePropertyDescriptor(int iIndex);

      public PropertyDescriptor GetPropertyDescriptor(int iIndex)
      {
        lock (this)
          if (descriptor == null)
          {
            descriptor = CreatePropertyDescriptor(iIndex);
            if (descriptor == null)
              throw new ArgumentNullException("descriptor", "no descriptor created");
          }
        return descriptor;
      } // func GetPropertyDescriptor

			public string MemberName { get { return info.Name; } }
      public abstract Type DeclaredType { get; }
		} // class LuaTableDefine

		#endregion

		#region -- class LuaTablePropertyDefine -------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private sealed class LuaTablePropertyDefine : LuaTableDefine
		{
			private readonly PropertyInfo pi;

			#region -- Ctor/Dtor ------------------------------------------------------------

			public LuaTablePropertyDefine(LuaMemberAttribute info, PropertyInfo pi)
				: base(info)
			{
				this.pi = pi;

				MethodInfo miGet = pi.GetGetMethod(true);
				MethodInfo miSet = pi.GetSetMethod(false);

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
			{
				return String.Format("Property: {0}", pi);
			} // func ToString

			protected override void CollectMember(List<LuaCollectedMember> collected, LuaMemberAttribute info)
			{
				collected.Add(new LuaCollectedMember { Define = this, Info = info, Member = pi });
			} // proc CollectMember

			#endregion

			#region -- Get/Set/Default ------------------------------------------------------

			public override object GetInitialValue(LuaTable table)
			{
				if (mode == LuaTableDefineMode.Init)
				{
					return pi.GetGetMethod(true).IsStatic ?
						GetPropertyStaticValue(table) :
						GetPropertyInstanceValue(table);
				}
				else
					return null;
			} // func GetInitialValue

			private object GetPropertyStaticValue(LuaTable t)
			{
				return pi.GetValue(null, null);
			} // func GetProperty

			private object GetPropertyInstanceValue(LuaTable t)
			{
				return pi.GetValue(t, null);
			} // func GetPropertyStaticValue

			private void SetPropertyStaticValue(LuaTable t, object value)
			{
				pi.SetValue(null, value, null);
			} // func SetPropertyStaticValue

			private void SetPropertyInstanceValue(LuaTable t, object value)
			{
				pi.SetValue(t, value, null);
			} // func SetPropertyInstanceValue

			#endregion

      #region -- CreatePropertyDescriptor ---------------------------------------------

      protected override PropertyDescriptor CreatePropertyDescriptor(int iIndex)
      {
        if (mode == LuaTableDefineMode.Direct)
          return TypeDescriptor.GetProperties(pi.DeclaringType, null, true)[pi.Name];
        else
          return new LuaTableClassPropertyDescriptor(this, iIndex);
      } // func CreatePropertyDescriptor

      #endregion

			public PropertyInfo PropertyInfo { get { return pi; } }
      public override Type DeclaredType { get { return pi.DeclaringType; } }
		} // class LuaTablePropertyDefine

		#endregion

		#region -- class LuaTableMethodDefine ---------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
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
			{
				return String.Format("DefineMethod[{0}]: {1}", methods.Length, methods[0]);
			} // func ToString

			public override object GetInitialValue(LuaTable table)
			{
				object instance = methods[0].IsStatic ? null : table;
				if (methods.Length == 1)
					return new LuaMethod(instance, methods[0]);
				else
					return new LuaOverloadedMethod(instance, methods);
			} // func GetInitialValue

			protected override void CollectMember(List<LuaCollectedMember> collected, LuaMemberAttribute info)
			{
				Array.ForEach(methods, c => collected.Add(new LuaCollectedMember { Define = this, Info = info, Member = c }));
			} // proc CollectMember

      protected override PropertyDescriptor CreatePropertyDescriptor(int iIndex)
      {
        return new LuaTableClassPropertyDescriptor(this, iIndex);
      } // func CreatePropertyDescriptor

			public MethodInfo[] Methods { get { return methods; } }

      public override Type DeclaredType
      {
        get
        {
          Type type = methods[0].DeclaringType;
          for (int i = 1; i < methods.Length; i++)
            if (type.IsSubclassOf(methods[i].DeclaringType))
              type = methods[i].DeclaringType;
          return type;
        }
      } // prop DeclaredType
		} // class LuaTableMethodDefine

		#endregion

		#region -- struct LuaCollectedMember ----------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private struct LuaCollectedMember
		{
			public LuaMemberAttribute Info;
			public MemberInfo Member;
			public LuaTableDefine Define;

			public override string ToString()
			{
				return String.Format("{0}{1} ==> {2}", Info.Name, Define == null ? String.Empty : "*", Member);
			} // func ToString
		} // struct LuaCollectedMember

		#endregion

		#region -- class LuaTableClass ----------------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private sealed class LuaTableClass
		{
			private Type type;
			private LuaTableDefine[] defines = null;

			#region -- Ctor/Dtor ------------------------------------------------------------

			public LuaTableClass(Type type)
			{
				this.type = type;

				// collect the type information
				var collected = new List<LuaCollectedMember>();
				Collect(type, collected);

				// metatable must be first
				int iIndex = collected.FindIndex(c => String.CompareOrdinal(c.Info.Name, csMetaTable) == 0);
				if (iIndex == -1)
					throw new InvalidOperationException();
				else if (iIndex > 0)
				{
					collected.Insert(0, collected[iIndex]);
					collected.RemoveAt(iIndex + 1);
				}

				// create the defines
				this.defines = CreateDefines(collected);
			} // ctor

			#endregion

			#region -- Collect --------------------------------------------------------------

			private void Collect(Type type, List<LuaCollectedMember> collected)
			{
				// is the type collected

				if (type.BaseType != typeof(object))
				{
					LuaTableClass baseClass = GetClass(type.BaseType);  // collect recursive
					Array.ForEach(baseClass.defines, d => d.CollectMember(collected)); // dump current defines
				}

				// collect current level
				foreach (MemberInfo mi in type.GetMembers(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.InvokeMethod | BindingFlags.GetProperty | BindingFlags.DeclaredOnly))
				{
					LuaMemberAttribute[] info = (LuaMemberAttribute[])Attribute.GetCustomAttributes(mi, typeof(LuaMemberAttribute));

					for (int i = 0; i < info.Length; i++)
					{
						if (info[i].Name == null) // remove all member
						{
							for (int j = 0; j < collected.Count - 1; j++)
								if (IsOverrideOf(mi, collected[j].Member))
								{
									collected.RemoveAt(j);
									break;
								}
						}
						else
						{
							int iStartIndex = FindMember(collected, info[i].Name);
							if (iStartIndex == -1)
							{
								collected.Add(new LuaCollectedMember { Info = info[i], Member = mi, Define = null });
							}
							else
							{
								// count the overloaded elements
								int iNextIndex = iStartIndex;
								while (iNextIndex < collected.Count && collected[iNextIndex].Info.Name == info[i].Name)
									iNextIndex++;

								// properties it can only exists one property
								if (mi.MemberType == MemberTypes.Property)
								{
									collected.RemoveRange(iStartIndex, iNextIndex - iStartIndex);
									collected.Add(new LuaCollectedMember { Info = info[i], Member = mi });
								}
								else // generate overload list
								{
									RemoveUseLessOverloads(collected, (MethodInfo)mi, iStartIndex, ref iNextIndex);
									collected.Insert(iNextIndex, new LuaCollectedMember { Info = info[i], Member = mi });
								}
							}
						}
					} // for info
				} // for member
			} // proc Collect

			private void RemoveUseLessOverloads(List<LuaCollectedMember> collected, MethodInfo mi, int iStartIndex, ref int iNextIndex)
			{
				while (iStartIndex < iNextIndex)
				{
					MethodInfo miTest = collected[iStartIndex].Member as MethodInfo;

					if (miTest == null || IsOverrideOf(mi, miTest) || SameArguments(mi, miTest))
					{
						collected.RemoveAt(iStartIndex);
						iNextIndex--;
						continue;
					}

					iStartIndex++;
				}
			} // proc RemoveUseLessOverloads

			private bool IsOverrideOf(MemberInfo mi, MemberInfo miTest)
			{
				if (mi.GetType() == miTest.GetType() && mi.Name == miTest.Name)
				{
					if (mi.MemberType == MemberTypes.Property)
						return IsOverridePropertyOf((PropertyInfo)mi, (PropertyInfo)miTest);
					else if (mi.MemberType == MemberTypes.Method)
						return IsOverrideMethodOf((MethodInfo)mi, (MethodInfo)miTest);
					else
						return false;
				}
				else
					return false;
			} // func IsOverrideOf

			private bool IsOverridePropertyOf(PropertyInfo pi, PropertyInfo piTest)
			{
				return IsOverrideMethodOf(pi.GetGetMethod(true), piTest.GetGetMethod(true));
			} // func IsOverridePropertyOf

			private bool IsOverrideMethodOf(MethodInfo mi, MethodInfo miTest)
			{
				MethodInfo miCur = mi;
				while (true)
				{
					if (miCur == miTest)
						return true;
					else if (miCur == miCur.GetBaseDefinition())
						return false;
					miCur = miCur.GetBaseDefinition();
				}
			} // func IsOverrideMethodOf

			private bool SameArguments(MethodInfo mi1, MethodInfo mi2)
			{
				ParameterInfo[] parameterInfo1 = mi1.GetParameters();
				ParameterInfo[] parameterInfo2 = mi2.GetParameters();
				if (parameterInfo1.Length == parameterInfo2.Length)
				{
					for (int i = 0; i < parameterInfo1.Length; i++)
						if (parameterInfo1[i].ParameterType != parameterInfo2[i].ParameterType ||
								parameterInfo1[i].Attributes != parameterInfo2[i].Attributes)
							return false;

					return true;
				}
				else
					return false;
			} // func SameArguments

			private int FindMember(List<LuaCollectedMember> collected, string sName)
			{
				for (int i = 0; i < collected.Count; i++)
					if (collected[i].Info.Name == sName)
						return i;
				return -1;
			} // func FindMember

			#endregion

			#region -- CreateDefines --------------------------------------------------------

			private LuaTableDefine[] CreateDefines(List<LuaCollectedMember> collected)
			{
				List<LuaTableDefine> defineList = new List<LuaTableDefine>(collected.Capacity);

				int i = 0;
				while (i < collected.Count)
				{
					int iStart = i;
					int iCount = 1;
					string sCurrentName = collected[i].Info.Name;

					// count same elements
					while (++i < collected.Count && sCurrentName == collected[i].Info.Name)
						iCount++;

					if (iCount == 1) // create single member
					{
						if (collected[iStart].Define != null) // already collected
							defineList.Add(collected[iStart].Define);
						else
						{
							MemberInfo mi = collected[iStart].Member;
							if (mi.MemberType == MemberTypes.Property)
								defineList.Add(new LuaTablePropertyDefine(collected[iStart].Info, (PropertyInfo)mi));
							else if (mi.MemberType == MemberTypes.Method)
								defineList.Add(new LuaTableMethodDefine(collected[iStart].Info, new MethodInfo[] { (MethodInfo)mi }));
							else
								throw new ArgumentException();
						}
					}
					else // create overloaded member
					{
						// create method array
						MethodInfo[] methods = new MethodInfo[iCount];
						for (int j = 0; j < iCount; j++)
							methods[j] = (MethodInfo)collected[iStart + j].Member;

						// check if they are all static/instance
						bool lCreateNewDefine = collected[iStart].Define == null;
						for (int j = 1; j < methods.Length; j++)
						{
							if (methods[0].IsStatic != methods[j].IsStatic)
                throw new ArgumentException(String.Format(Properties.Resources.rsMethodStaticMix, methods[0]));

							if (!lCreateNewDefine && collected[iStart].Define != collected[iStart + j].Define)
							{
								lCreateNewDefine |= true;
								break;
							}
						}

						// create the define
						if (lCreateNewDefine)
							defineList.Add(new LuaTableMethodDefine(collected[iStart].Info, methods));
						else
							defineList.Add(collected[iStart].Define);
					}
				}

				return defineList.ToArray();
			} // func CreateDefines

			#endregion

			public Type Type { get { return type; } }
			public int Count { get { return defines.Length; } }
			public LuaTableDefine this[int iIndex] { get { return defines[iIndex]; } }

			// -- Static ------------------------------------------------------------

			private static int iClassCount = 0;
			private static LuaTableClass[] classes = new LuaTableClass[0];
			private static object lockClass = new object();

			public static LuaTableClass GetClass(Type type)
			{
				lock (lockClass)
				{
					// is the type collected
					LuaTableClass cls = Array.Find(classes, c => c != null && c.Type == type);
					if (cls == null) // collect the infomration
					{
						cls = new LuaTableClass(type);

						if (iClassCount == classes.Length)
						{
							LuaTableClass[] newClasses = new LuaTableClass[classes.Length + 4];
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

    #region -- class LuaTablePropertyDescriptor ---------------------------------------

    private abstract class LuaTablePropertyDescriptor : PropertyDescriptor
    {
      private Dictionary<object, int> attachedHandler = null;

      protected LuaTablePropertyDescriptor(MemberDescriptor member)
        : base(member)
      {
      } // ctor

      protected LuaTablePropertyDescriptor(string sName, Attribute[] attrs)
        : base(sName, attrs)
      {
      } // ctor

      public override void AddValueChanged(object component, EventHandler handler)
      {
        if (component == null)
          throw new ArgumentNullException("component");
        if (handler == null)
          throw new ArgumentNullException("handler");

        if (attachedHandler == null)
          attachedHandler = new Dictionary<object, int>();

        int iAttachedReferences;
        if (attachedHandler.TryGetValue(component, out iAttachedReferences))
          iAttachedReferences++;
        else
        {
          attachedHandler[component] = 1;
          Lua.NotifyPropertyChangedEventInfo.AddEventHandler(component, new PropertyChangedEventHandler(OnPropertyChanged));
        }

        base.AddValueChanged(component, handler);
      } // proc AddValueChanged

      public override void RemoveValueChanged(object component, EventHandler handler)
      {
        if (component == null)
          throw new ArgumentNullException("component");
        if (handler == null)
          throw new ArgumentNullException("handler");

        if (attachedHandler != null)
        {
          int iRef = attachedHandler[component];
          if (iRef == 1)
          {
            Lua.NotifyPropertyChangedEventInfo.RemoveEventHandler(component, handler);
            attachedHandler.Remove(component);
          }
          else
            attachedHandler[component] = iRef - 1;
        }
        base.RemoveValueChanged(component, handler);
      } // proc RemoveValueChanged

      private void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
      {
        if (!String.IsNullOrEmpty(e.PropertyName) && String.Compare(e.PropertyName, Name, true) == 0)
          OnValueChanged(sender, e);
      } // proc OnPropertyChanged

      public override Type ComponentType { get { return typeof(LuaTable); } }
    } // class LuaTablePropertyDescriptor

    #endregion

    #region -- class LuaTableClassPropertyDescriptor ----------------------------------

    private sealed class LuaTableClassPropertyDescriptor : LuaTablePropertyDescriptor
    {
      private LuaTableDefine define;
      private int iIndex;

      public LuaTableClassPropertyDescriptor(LuaTableDefine define, int iIndex)
        : base(define.MemberName, null)
      {
        if (define.mode == LuaTableDefineMode.Direct)
          throw new ArgumentException("is not allowed");

        this.define = define;
        this.iIndex = iIndex;
      } // ctor

      public override bool CanResetValue(object component)
      {
        return true;
      } // func CanResetValue

      public override void ResetValue(object component)
      {
        SetValue(component, define.GetInitialValue((LuaTable)component));
      } // proc ResetValue

      public override bool ShouldSerializeValue(object component)
      {
        LuaTable t = (LuaTable)component;
        return !comparerObject.Equals(t.GetClassMemberValue(iIndex, true), define.GetInitialValue(t));
      } // func ShouldSerializeValue

      public override object GetValue(object component)
      {
        return ((LuaTable)component).GetClassMemberValue(iIndex, false);
      } // func GetValue

      public override void SetValue(object component, object value)
      {
        ((LuaTable)component).SetClassMemberValue(iIndex, value);
      } // proc SetValue

      public override bool IsReadOnly { get { return false; } }
      public override Type ComponentType { get { return define.DeclaredType; } }
      public override Type PropertyType { get { return typeof(object); } }
    } // class LuaTableClassPropertyDescriptor

    #endregion

    #region -- class LuaTableValuePropertyDescriptor ----------------------------------

    private sealed class LuaTableValuePropertyDescriptor : LuaTablePropertyDescriptor
    {
      public LuaTableValuePropertyDescriptor(string sMemberName)
        : base(sMemberName, null)
      {
      } // ctor

      public override bool CanResetValue(object component)
      {
        return true;
      } // func CanResetValue

      public override bool ShouldSerializeValue(object component)
      {
        return GetValue(component) != null;
      } // func ShouldSerializeValue

      public override void ResetValue(object component)
      {
        SetValue(component, null);
      } // func ResetValue

      public override object GetValue(object component)
      {
        return ((LuaTable)component).GetMemberValue(this.Name, false, false);
      } // func GetValue

      public override void SetValue(object component, object value)
      {
        ((LuaTable)component).SetMemberValue(Name, value, false, false);
      } // proc SetValue

      public override bool IsReadOnly { get { return false; } }
      public override Type PropertyType { get { return typeof(object); } }

      // -- Static ------------------------------------------------------------

      private static Dictionary<string, PropertyDescriptor> descriptorCache = new Dictionary<string, PropertyDescriptor>();

      public static PropertyDescriptor Get(string sMemberName)
      {
        PropertyDescriptor descriptor;
        lock (descriptorCache)
          if (descriptorCache.TryGetValue(sMemberName, out descriptor))
            return descriptor;
          else
          {
            descriptorCache[sMemberName] = descriptor = new LuaTableValuePropertyDescriptor(sMemberName);
            return descriptor;
          }
      } // func Get
    } // class LuaTableValuePropertyDescriptor

    #endregion

		/// <summary>Value has changed.</summary>
		public event PropertyChangedEventHandler PropertyChanged;

		private LuaTable metaTable = null;												// Currently attached metatable

		private LuaTableEntry[] entries = emptyLuaEntries;				// Key/Value part of the lua-table
		private LuaTableClass classDefinition = null;							// Class part of the lua table
		private int[] hashLists = emptyIntArray;									// Hashcode entry point
		private object[] arrayList = emptyObjectArray;						// List with the array elements (this array is ZERO-based)

		private int iFreeTop = -1;																// Start of the free lists

		private int iArrayLength = 0;															// Current length of the array list
		private int iMemberCount = 0;															// Current length of the member list 

		private int iCount = 0;																		// Number of element in the Key/Value part

		private int iVersion = 0;																	// version for the data

		private Dictionary<int, CallSite> callSites = new Dictionary<int, CallSite>(); // call site for calls

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
			while (arrayList[iArrayLength] != null)
				iArrayLength++;
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
		{
			return base.GetHashCode();
		} // func GetHashCode

		private void InitClass()
		{
			// get class definition for the lua table
			classDefinition = LuaTableClass.GetClass(GetType());

			// create the entries
			ResizeEntryList(classDefinition.Count);

			// generate the memberset
			for (int i = 0; i < classDefinition.Count; i++)
				InitDefinition(i, classDefinition[i]);
		} // proc InitClass

		private void InitDefinition(int iIndex, LuaTableDefine define)
		{
			// Reserve the entry for the member
			iMemberCount++;
			int iEntryIndex = InsertValue(define.MemberName, GetMemberHashCode(define.MemberName), null, false);
#if DEBUG
			if (iEntryIndex != iIndex)
				throw new InvalidOperationException("entryIndex");
#endif

			// Set the init value
			entries[iEntryIndex].value = define.GetInitialValue(this);
		} // proc InitDefinition

		#endregion

		#region -- Dynamic Members --------------------------------------------------------

		/// <summary>Returns the Meta-Object</summary>
		/// <param name="parameter"></param>
		/// <returns></returns>
		public DynamicMetaObject GetMetaObject(Expression parameter)
		{
			return new LuaTableMetaObject(this, parameter);
		} // func GetMetaObject

		/// <summary>Get the invoke binder.</summary>
		/// <param name="callInfo">CallInfo</param>
		/// <returns>Binder</returns>
		protected virtual CallSiteBinder GetInvokeBinder(CallInfo callInfo)
		{
			return new Lua.LuaInvokeBinder(null, callInfo);
		} // func GetInvokeBinder

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

		#region -- Core hash functionality ------------------------------------------------

		private static int NextArraySize(int iCurrentLength, int iCapacity)
		{
			if (iCurrentLength == Int32.MaxValue)
				throw new OverflowException();
			if (iCurrentLength == 0)
				iCurrentLength = 16;

		Resize:
			iCurrentLength = unchecked(iCurrentLength << 1);

			if (iCurrentLength == Int32.MinValue)
				iCurrentLength = Int32.MaxValue;
			else if (iCapacity > iCurrentLength)
				goto Resize;

			return iCurrentLength;
		} // func NextArraySize

		/// <summary>Insert a value in the hash list</summary>
		/// <param name="key">Key of the item</param>
		/// <param name="hashCode">Hashcode of the key</param>
		/// <param name="value">Value that will be setted</param>
		/// <param name="lIsMethod">Is the value a method</param>
		/// <returns>Index of the setted entry</returns>
		private int InsertValue(object key, int hashCode, object value, bool lIsMethod)
		{
#if DEBUG
			if (key == null)
				throw new ArgumentNullException(Properties.Resources.rsTableKeyNotNullable);
#endif

			if (iFreeTop == -1) // entry list is full -> enlarge
				ResizeEntryList();

			// get free item
			int iFreeItem = iFreeTop;
			iFreeTop = entries[iFreeTop].nextHash;

			// set the values
			entries[iFreeItem].key = key;
			entries[iFreeItem].value = value;
			entries[iFreeItem].isMethod = lIsMethod;

			// create the hash list
			int iHashIndex = (entries[iFreeItem].hashCode = hashCode) % hashLists.Length;
			entries[iFreeItem].nextHash = hashLists[iHashIndex];
			hashLists[iHashIndex] = iFreeItem;

			iCount++;
			iVersion++;

			return iFreeItem;
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
			int iHashLength = hashLists.Length;
			if (iHashLength == 0)
				return -1;

			int iHashIndex = hashCode % iHashLength;
			int iLastIndex = -1;
			if (comparer == compareStringIgnoreCase)
			{
				for (int i = hashLists[iHashIndex]; i >= 0; i = entries[i].nextHash)
				{
					if (entries[i].hashCode == hashCode && comparer.Equals(entries[i].key, key))
						iLastIndex = i;
				}
				if (iLastIndex >= 0)
					return iLastIndex;
			}
			else
			{
				for (int i = hashLists[iHashIndex]; i >= 0; i = entries[i].nextHash)
				{
					if (entries[i].hashCode == hashCode && comparer.Equals(entries[i].key, key))
						return i;
				}
			}
			return ~iHashIndex;
		} // func FindKey

		private void RemoveValue(int iIndex)
		{
#if DEBUG
			if (hashLists.Length == 0)
				throw new InvalidOperationException();
#endif

			int iHashCode = entries[iIndex].hashCode;
			int iHashIndex = iHashCode % hashLists.Length;

			// remove the item from hash list
			int iCurrentIndex = hashLists[iHashIndex];
			if (iCurrentIndex == iIndex)
			{
				hashLists[iHashIndex] = entries[iIndex].nextHash;
			}
			else
			{
				while (true)
				{
					int iNext = entries[iCurrentIndex].nextHash;
					if (iNext == iIndex)
					{
						entries[iCurrentIndex].nextHash = entries[iIndex].nextHash; // remove item from lest
						break;
					}
					iCurrentIndex = iNext;

					if (iCurrentIndex == -1)
						throw new InvalidOperationException();
				}
			}

			// add to free list
			entries[iIndex].hashCode = -1;
			entries[iIndex].key = null;
			entries[iIndex].value = null;
			entries[iIndex].isMethod = false;
			entries[iIndex].nextHash = iFreeTop;
			iFreeTop = iIndex;

			iCount--;
			iVersion++;
		} // proc RemoveValue

		private void ResizeEntryList(int iCapacity = 0)
		{
			LuaTableEntry[] newEntries = new LuaTableEntry[NextArraySize(entries.Length, iCapacity)];

			// copy the old values
			Array.Copy(entries, 0, newEntries, 0, entries.Length);

			// create the free list for the new entries
			iFreeTop = entries.Length;
			int iLength = newEntries.Length - 1;
			for (int i = iFreeTop; i < iLength; i++)
			{
				newEntries[i].hashCode = -1;
				newEntries[i].nextHash = i + 1;
			}
			// set the last element
			newEntries[iLength].hashCode = -1;
			newEntries[iLength].nextHash = -1;

			// real length
			iLength++;

			// update the array
			entries = newEntries;

			// create the hash table new
			hashLists = new int[iLength];
			for (int i = 0; i < hashLists.Length; i++)
				hashLists[i] = -1;

			// rehash all entries
			for (int i = 0; i < iFreeTop; i++)
			{
				int iIndex = entries[i].hashCode % hashLists.Length;
				entries[i].nextHash = hashLists[iIndex];
				hashLists[iIndex] = i;
			}
		} // proc ResizeEntryList

		/// <summary>Empty the table</summary>
		public void Clear()
		{
			iCount = 0;
			iArrayLength = 0;
			iMemberCount = 0;
			iFreeTop = -1;
			iVersion = 0;

			metaTable = null;

			entries = emptyLuaEntries;
			hashLists = emptyIntArray;
			arrayList = emptyObjectArray;

			InitClass();
		} // proc Clear

		#endregion

		#region -- Get/SetMemberValue -----------------------------------------------------

		/// <summary>Notify property changed</summary>
		/// <param name="sPropertyName">Name of property</param>
		protected void OnPropertyChanged(string sPropertyName)
		{
			if (PropertyChanged != null)
				PropertyChanged(this, new PropertyChangedEventArgs(sPropertyName));
		} // proc OnPropertyChanged

		private static int GetMemberHashCode(string sMemberName)
		{
			return compareStringIgnoreCase.GetHashCode(sMemberName) & 0x7FFFFFFF;
		} // func GetMemberHashCode

		/// <summary>Set a value string key value</summary>
		/// <param name="sMemberName">Key</param>
		/// <param name="value">Value, <c>null</c> deletes the value.</param>
		/// <param name="lIgnoreCase">Ignore case of the member name</param>
		/// <param name="lRawSet">If the value not exists, should we call OnNewIndex.</param>
		/// <returns>value</returns>
		public object SetMemberValue(string sMemberName, object value, bool lIgnoreCase = false, bool lRawSet = false)
		{
			if (sMemberName == null)
				throw new ArgumentNullException(Properties.Resources.rsTableKeyNotNullable);

			SetMemberValueIntern(sMemberName, value, lIgnoreCase, lRawSet, false, false);
			return value;
		} // func SetMemberValue

		private int SetMemberValueIntern(string sMemberName, object value, bool lIgnoreCase, bool lRawSet, bool lAdd, bool lMarkAsMethod)
		{
			// look up the key in the member list
			int hashCode = GetMemberHashCode(sMemberName);
			int iEntryIndex = FindKey(sMemberName, hashCode, lIgnoreCase ? compareStringIgnoreCase : compareString);

			if (value == null) // key will be removed
			{
				if (iEntryIndex >= 0)
				{
					if (iEntryIndex < classDefinition.Count)
					{
						SetClassMemberValue(iEntryIndex, null, value, false);
					}
					else
					{
						// remove the value
						RemoveValue(iEntryIndex);
						// remove the item
						iMemberCount--;
					}
					return RemovedIndex;
				}
				else
					return IndexNotFound;
			}
			else if (iEntryIndex >= 0) // key will be setted
			{
				// only add is allowed
				if (lAdd)
					throw new ArgumentException(String.Format(Properties.Resources.rsTableAddDuplicate, sMemberName));

				if (iEntryIndex < classDefinition.Count && SetClassMemberValue(iEntryIndex, lRawSet ? null : entries[iEntryIndex].key, value, lMarkAsMethod) ||
					entries[iEntryIndex].SetValue(value, lMarkAsMethod))
				{
					// notify that the property is changed
					OnPropertyChanged(lIgnoreCase ? (string)entries[iEntryIndex].key : sMemberName);
				}

				return iEntryIndex;
			}
			else if (lRawSet || !OnNewIndex(sMemberName, value)) // key will be added
			{
				// insert the value
				iMemberCount++;
				InsertValue(sMemberName, hashCode, value, lMarkAsMethod);

				// notify that the property is changed
				OnPropertyChanged(sMemberName);

				return iEntryIndex;
			}
			else
				return IndexNotFound;
		} // func SetMemberValueIntern

    private void SetClassMemberValue(int iEntryIndex, object value)
    {
      object key = entries[iEntryIndex].key;
      if (SetClassMemberValue(iEntryIndex, key, value, false))
        OnPropertyChanged((string)key);
    } // proc SetClassMemberValue

		private bool SetClassMemberValue(int iEntryIndex, object key, object value, bool lMarkAsMethod)
		{
			switch (classDefinition[iEntryIndex].mode)
			{
				case LuaTableDefineMode.Default:
					return entries[iEntryIndex].SetValue(value, lMarkAsMethod);
				case LuaTableDefineMode.Direct:
          classDefinition[iEntryIndex].setValue(this, value); // direct properties have to handle OnPropertyChanged on her own
						return false;
				default:
					if (key == null || entries[iEntryIndex].value != null || !OnNewIndex(key, value))
					{
						entries[iEntryIndex].SetValue(value, lMarkAsMethod);
						return true;
					}
					else
						return false;
			}
		} // proc SetClassMemberValue

		/// <summary>Returns the value of a key.</summary>
		/// <param name="sMemberName">Key</param>
		/// <param name="lIgnoreCase">Ignore case of the member name</param>
		/// <param name="lRawGet">Is OnIndex called, if no member exists.</param>
		/// <returns>The value or <c>null</c></returns>
		public object GetMemberValue(string sMemberName, bool lIgnoreCase = false, bool lRawGet = false)
		{
			if (sMemberName == null)
				throw new ArgumentNullException(Properties.Resources.rsTableKeyNotNullable);

			// find the member
			int iEntryIndex = FindKey(sMemberName, GetMemberHashCode(sMemberName), lIgnoreCase ? compareStringIgnoreCase : comparerObject);
			if (iEntryIndex < 0)
			{
				if (lRawGet)
					return null;
				else
					return OnIndex(sMemberName);
			}
			else if (iEntryIndex < classDefinition.Count)
			{
				return GetClassMemberValue(iEntryIndex, sMemberName, lRawGet);
			}
			else
				return entries[iEntryIndex].value;
		} // func GetMemberValue

    private object GetClassMemberValue(int iEntryIndex, bool lRawGet)
    {
      return GetClassMemberValue(iEntryIndex, entries[iEntryIndex].key, lRawGet);
    } // func GetClassMemberValue

		private object GetClassMemberValue(int iEntryIndex, object key, bool lRawGet)
		{
			switch (classDefinition[iEntryIndex].mode)
			{
				case LuaTableDefineMode.Default:
					return (entries[iEntryIndex].value ?? classDefinition[iEntryIndex].getValue(this)) ?? (lRawGet ? null : OnIndex(key));

				case LuaTableDefineMode.Direct:
					return classDefinition[iEntryIndex].getValue(this) ?? (lRawGet ? null : OnIndex(key));

				default:
					return entries[iEntryIndex].value ?? (lRawGet ? null : OnIndex(key));
			}
		} // func GetClassMemberValue

		/// <summary>Checks if the Member exists.</summary>
		/// <param name="sMemberName">Membername</param>
		/// <param name="lIgnoreCase">Ignore case of the member name</param>
		/// <returns><c>true</c>, if the member is in the table.</returns>
		public bool ContainsMember(string sMemberName, bool lIgnoreCase = false)
		{
			if (sMemberName == null)
				throw new ArgumentNullException(Properties.Resources.rsTableKeyNotNullable);

			return FindKey(sMemberName, GetMemberHashCode(sMemberName), lIgnoreCase ? compareStringIgnoreCase : compareString) >= 0;
		} // func ContainsMember

		#endregion

		#region -- Get/SetArrayValue ------------------------------------------------------

		private int FindKey(int iIndex)
		{
			return FindKey(iIndex, iIndex.GetHashCode() & 0x7FFFFFFF, comparerInt);
		} // func FindKey

		private void SetIndexCopyValuesToArray(object[] newArray, int iStart)
		{
			if (newArray.Length - iStart < entries.Length) // choose the less expensive way to copy the values, try to find values
			{
				for (int i = iStart; i < newArray.Length; i++)
				{
					int iEntryIndex = FindKey(i + 1);
					if (iEntryIndex >= 0)
					{
						newArray[i] = entries[iEntryIndex].value;
						RemoveValue(iEntryIndex);
						iCount++;
					}
				}
			}
			else // go through the array
			{
				for (int i = 0; i < entries.Length; i++)
				{
					if (entries[i].key is int)
					{
						int k = (int)entries[i].key;

						if (iStart < k && k <= newArray.Length)
						{
							newArray[k - 1] = entries[i].value;
							RemoveValue(i);
							iCount++;
						}
					}
				}
			}
		} // func SetIndexCopyValuesToArray

		/// <summary>Set the value in the array part of the table (if the index is greater Length + 1 it is set to the hash part)</summary>
		/// <param name="iIndex">Index of the element</param>
		/// <param name="value">Value, <c>null</c> deletes the value.</param>
		/// <param name="lRawSet">If the value not exists, should we call OnNewIndex.</param>
		/// <returns>value</returns>
		public object SetArrayValue(int iIndex, object value, bool lRawSet = false)
		{
			int iArrayIndex = iIndex - 1;
			if (unchecked((uint)iArrayIndex < arrayList.Length)) // with in the current allocated array
			{
				object oldValue = arrayList[iArrayIndex];
				if (value == null) // remove the value
				{
					if (oldValue != null)
					{
						arrayList[iArrayIndex] = null;
						if (iArrayIndex < iArrayLength)
							iArrayLength = iArrayIndex; // iArrayLength = iIndex - 1

						iCount--;
						iVersion++;
					}
				}
				else if (lRawSet || // always set a value
					oldValue != null || // reset the value
					!OnNewIndex(iIndex, value)) // no value, notify __newindex to set the array element
				{
					if (oldValue == null)
						iCount++;

					arrayList[iArrayIndex] = value;
					iVersion++;

					// correct the array length
					if (iArrayLength == iArrayIndex) // iArrayLength == iIndex - 1
					{
						// search for the end of the array
						iArrayLength = iIndex;
						while (iArrayLength + 1 <= arrayList.Length && arrayList[iArrayLength] != null)
							iArrayLength++;

						// are the more values behind the array
						if (iArrayLength == arrayList.Length)
						{
							List<object> collected = new List<object>();

							// collect values
							int iEntryIndex;
							while ((iEntryIndex = FindKey(iArrayLength + 1)) >= 0)
							{
								collected.Add(entries[iEntryIndex].value);
								RemoveValue(iEntryIndex);
								iCount++;

								iArrayLength++;
							}

							// append the values to the array
							if (collected.Count > 0)
							{
								// enlarge array part, with the new values
								object[] newArray = new object[NextArraySize(arrayList.Length, iArrayLength)];
								// copy the old array
								Array.Copy(arrayList, 0, newArray, 0, arrayList.Length);
								// copy the new array content
								collected.CopyTo(newArray, arrayList.Length);
								// collect values for buffer
								SetIndexCopyValuesToArray(newArray, iArrayLength);

								arrayList = newArray;
							}
						}
					}
				}
			}
			else if (iArrayIndex == iArrayLength && value != null) // enlarge array part
			{
				if (value != null && (lRawSet || !OnNewIndex(iIndex, value)))
				{
					// create a new enlarged array
					object[] newArray = new object[NextArraySize(arrayList.Length, 0)];
					Array.Copy(arrayList, 0, newArray, 0, arrayList.Length);

					// copy the values from the key/value part to the array part
					SetIndexCopyValuesToArray(newArray, arrayList.Length);

					arrayList = newArray;

					// set the value in the index
					SetArrayValue(iIndex, value, true);
				}
			}
			else // set the value in key/value part
			{
				int hashCode = iIndex.GetHashCode() & 0x7FFFFFFF;
				int iEntryIndex = FindKey(iIndex, hashCode, comparerInt);
				if (iEntryIndex >= 0)
				{
					if (value == null)
					{
						RemoveValue(iEntryIndex);
					}
					else
					{
						entries[iEntryIndex].value = value;
						iVersion++;
					}
				}
				else if (lRawSet || !OnNewIndex(iIndex, value))
					InsertValue(iIndex, hashCode, value, false);
			}

			return value;
		} // func SetArrayValue

		/// <summary>Get the value from the array part or from the hash part.</summary>
		/// <param name="iIndex">Index of the element</param>
		/// <param name="lRawGet">Is OnIndex called, if no index exists.</param>
		/// <returns></returns>
		public object GetArrayValue(int iIndex, bool lRawGet = false)
		{
			int iArrayIndex = iIndex - 1;
			if (unchecked((uint)iArrayIndex < arrayList.Length)) // part of array
			{
				if (lRawGet || iArrayIndex < iArrayLength)
					return arrayList[iArrayIndex];
				else
					return arrayList[iArrayIndex] ?? OnIndex(iIndex);
			}
			else // check the hash part
			{
				int iEntryIndex = FindKey(iIndex);
				if (iEntryIndex >= 0) // get the hashed value
					return entries[iEntryIndex].value;
				else if (lRawGet) // get the default value
					return null;
				else // ask for a value
					return OnIndex(iIndex);
			}
		} // func SetArrayValue

		/// <summary>Checks if the index is set.</summary>
		/// <param name="iIndex">Index</param>
		/// <returns><c>true</c>, if the index is in the table.</returns>
		public bool ContainsIndex(int iIndex)
		{
			if (iIndex >= 1 && iIndex <= arrayList.Length) // part of array
				return arrayList[iIndex - 1] != null;
			else // hashed index
				return FindKey(iIndex) >= 0;
		} // func ContainsIndex

		#endregion

		#region -- High level Array functions ---------------------------------------------

		/// <summary>zero based</summary>
		/// <param name="value"></param>
		/// <returns></returns>
		private int ArrayOnlyIndexOf(object value)
		{
			return Array.IndexOf(arrayList, value, 0, iArrayLength);
		} // func ArrayOnlyIndexOf

		/// <summary>zero based</summary>
		/// <param name="iIndex"></param>
		/// <param name="value"></param>
		private void ArrayOnlyInsert(int iIndex, object value)
		{
			if (iIndex < 0 || iIndex > iArrayLength)
				throw new ArgumentOutOfRangeException();

			object last;
			if (iIndex == iArrayLength)
				last = value;
			else
			{
				last = arrayList[iArrayLength - 1];
				if (iIndex != iArrayLength - 1)
					Array.Copy(arrayList, iIndex, arrayList, iIndex + 1, iArrayLength - iIndex - 1);
				arrayList[iIndex] = value;
			}

			SetArrayValue(iArrayLength + 1, last, true);
		} // proc ArrayOnlyInsert 

		/// <summary>zero based</summary>
		/// <param name="iIndex"></param>
		private void ArrayOnlyRemoveAt(int iIndex)
		{
			if (iIndex < 0 || iIndex >= iArrayLength)
				throw new ArgumentOutOfRangeException();

			Array.Copy(arrayList, iIndex + 1, arrayList, iIndex, iArrayLength - iIndex - 1);
			arrayList[--iArrayLength] = null;

			iVersion++;
		} // func ArrayOnlyRemoveAt

		#endregion

		#region -- Simple Set/GetValue/Contains -------------------------------------------

		/// <summary>Is the type a index type.</summary>
		/// <param name="type"></param>
		/// <returns></returns>
		internal static bool IsIndexKey(Type type)
		{
			TypeCode tc = Type.GetTypeCode(type);
			return tc >= TypeCode.SByte && tc <= TypeCode.Int32;
		} // func IsIndexKey

		private static bool IsIndexKey(object item, out int iIndex)
		{
			#region -- IsIndexKey --
			switch (Type.GetTypeCode(item.GetType()))
			{
				case TypeCode.Int32:
					iIndex = (int)item;
					return true;
				case TypeCode.Byte:
					iIndex = (byte)item;
					return true;
				case TypeCode.SByte:
					iIndex = (sbyte)item;
					return true;
				case TypeCode.UInt16:
					iIndex = (ushort)item;
					return true;
				case TypeCode.Int16:
					iIndex = (short)item;
					return true;
				case TypeCode.UInt32:
					unchecked
					{
						uint t = (uint)item;
						if (t < Int32.MaxValue)
						{
							iIndex = (int)t;
							return true;
						}
						else
						{
							iIndex = 0;
							return false;
						}
					}
				case TypeCode.Int64:
					unchecked
					{
						long t = (uint)item;
						if (t < Int32.MaxValue)
						{
							iIndex = (int)t;
							return true;
						}
						else
						{
							iIndex = 0;
							return false;
						}
					}
				case TypeCode.UInt64:
					unchecked
					{
						ulong t = (uint)item;
						if (t < Int32.MaxValue)
						{
							iIndex = (int)t;
							return true;
						}
						else
						{
							iIndex = 0;
							return false;
						}
					}
				case TypeCode.Single:
					{
						float f = (float)item;
						if (f % 1 == 0 && f >= 1 && f <= Int32.MaxValue)
						{
							iIndex = Convert.ToInt32(f);
							return true;
						}
						else
						{
							iIndex = 0;
							return false;
						}
					}
				case TypeCode.Double:
					{
						double f = (double)item;
						if (f % 1 == 0 && f >= 1 && f <= Int32.MaxValue)
						{
							iIndex = Convert.ToInt32(f);
							return true;
						}
						else
						{
							iIndex = 0;
							return false;
						}
					}
				case TypeCode.Decimal:
					{
						decimal f = (decimal)item;
						if (f % 1 == 0 && f >= 1 && f <= Int32.MaxValue)
						{
							iIndex = Convert.ToInt32(f);
							return true;
						}
						else
						{
							iIndex = 0;
							return false;
						}
					}
				default:
					iIndex = 0;
					return false;
			}
			#endregion
		} // func IsIndexKey

		/// <summary>Set a value in of the table</summary>
		/// <param name="key">Key</param>
		/// <param name="value">Value, <c>null</c> deletes the value.</param>
		/// <param name="lRawSet">If the value not exists, should we call OnNewIndex.</param>
		/// <returns>value</returns>
		public object SetValue(object key, object value, bool lRawSet = false)
		{
			int iIndex;
			string sKey;

			if (key == null)
			{
				throw new ArgumentNullException(Properties.Resources.rsTableKeyNotNullable);
			}
			else if (IsIndexKey(key, out iIndex)) // is a array element
			{
				return SetArrayValue(iIndex, value, lRawSet);
			}
			else if ((sKey = (key as string)) != null) // belongs to the member list
			{
				SetMemberValueIntern(sKey, value, false, lRawSet, false, false);
				return value;
			}
			else // something else
			{
				int hashCode = key.GetHashCode() & 0x7FFFFFFF;
				iIndex = FindKey(key, hashCode, comparerObject); // find the value

				if (value == null) // remove value
					RemoveValue(iIndex);
				else if (iIndex < 0 && (lRawSet || !OnNewIndex(key, value))) // insert value
					InsertValue(key, hashCode, value, false);
				else // update value
					entries[iIndex].value = value;

				return value;
			}
		} // func SetValue

		/// <summary>Set multi indexed values.</summary>
		/// <param name="keyList">Keys</param>
		/// <param name="lRawSet">If the value not exists, should we call OnNewIndex.</param>
		/// <param name="value"></param>
		public void SetValue(object[] keyList, object value, bool lRawSet = false)
		{
			SetValue(keyList, 0, value, lRawSet);
		} // func SetValue

		private void SetValue(object[] keyList, int iIndex, object value, bool lRawSet)
		{
			if (iIndex == keyList.Length - 1)
			{
				SetValue(keyList[iIndex], value, false);
			}
			else
			{
				LuaTable tNext = GetValue(keyList[iIndex], false) as LuaTable;
				if (tNext == null)
				{
					tNext = new LuaTable();
					SetValue(keyList[iIndex], tNext, lRawSet); // set it, as it is
				}
				tNext.SetValue(keyList, iIndex++, value, lRawSet);
			}
		} // func SetValue

		/// <summary>Gets the value of a key</summary>
		/// <param name="key">Key</param>
		/// <param name="lRawGet">Is OnIndex called, if no key exists.</param>
		/// <returns>The value or <c>null</c>.</returns>
		public object GetValue(object key, bool lRawGet = false)
		{
			int iIndex;
			string sKey;

			if (key == null)
			{
				throw new ArgumentNullException(Properties.Resources.rsTableKeyNotNullable);
			}
			else if (IsIndexKey(key, out iIndex))
			{
				return GetArrayValue(iIndex, lRawGet);
			}
			else if ((sKey = (key as string)) != null)
			{
				return GetMemberValue(sKey, false, lRawGet);
			}
			else
			{
				iIndex = FindKey(key, key.GetHashCode() & 0x7FFFFFFF, comparerObject);
				if (iIndex < 0)
					return lRawGet ? null : OnIndex(key);
				else
					return entries[iIndex].value;
			}
		} // func GetValue

		/// <summary>Get multi indexed values</summary>
		/// <param name="keyList">Keys</param>
		/// <param name="lRawGet">Is OnIndex called, if no key exists.</param>
		/// <returns>Value</returns>
		public object GetValue(object[] keyList, bool lRawGet = false)
		{
			return GetValue(keyList, 0, lRawGet);
		} // func GetValue

		private object GetValue(object[] keyList, int iIndex, bool lRawGet)
		{
			object o = GetValue(keyList[iIndex], lRawGet);

			if (iIndex == keyList.Length - 1)
				return o;
			else
			{
				LuaTable tNext = o as LuaTable;
				if (tNext == null)
					return null;
				else
					return tNext.GetValue(keyList, iIndex + 1, lRawGet);
			}
		} // func GetValue

		/// <summary>Returns the value of the table.</summary>
		/// <typeparam name="T">Excpected type for the value</typeparam>
		/// <param name="sName">Name of the member.</param>
		/// <param name="default">Replace value, if the member not exists or can not converted.</param>
		/// <param name="lIgnoreCase"></param>
		/// <param name="lRawGet"></param>
		/// <returns>Value or default.</returns>
		public T GetOptionalValue<T>(string sName, T @default, bool lIgnoreCase = false, bool lRawGet = false)
		{
			try
			{
				object o = GetMemberValue(sName, lIgnoreCase, lRawGet);
				return (T)Lua.RtConvertValue(o, typeof(T));
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
			int iIndex;
			string sKey;
			if (key == null)
				throw new ArgumentNullException(Properties.Resources.rsTableKeyNotNullable);
			else if (IsIndexKey(key, out iIndex))
				return ContainsIndex(iIndex);
			else if ((sKey = (key as string)) != null)
				return ContainsMember(sKey, false);
			else
				return FindKey(key, key.GetHashCode() & 0x7FFFFFFF, comparerObject) >= 0;
		} // func ContainsKey

		#endregion

		#region -- DefineFunction, DefineMethod -------------------------------------------

		/// <summary>Defines a normal function attached to a table.</summary>
		/// <param name="sFunctionName">Name of the member for the function.</param>
		/// <param name="function">function definition</param>
		/// <param name="lIgnoreCase">Ignore case of the member name</param>
		/// <returns>function</returns>
		/// <remarks>If you want to delete the define, call SetMemberValue with the function name and set the value to <c>null</c>.</remarks>
		public Delegate DefineFunction(string sFunctionName, Delegate function, bool lIgnoreCase = false)
		{
			if (String.IsNullOrEmpty(sFunctionName))
				throw new ArgumentNullException("functionName");
			if (function == null)
				throw new ArgumentNullException("function");

			SetMemberValueIntern(sFunctionName, function, lIgnoreCase, false, false, false);
			return function;
		} // func DefineFunction

		/// <summary>Defines a new method on the table.</summary>
		/// <param name="sMethodName">Name of the member/name.</param>
		/// <param name="method">Method that has as a first parameter a LuaTable.</param>
		/// <param name="lIgnoreCase">Ignore case of the member name</param>
		/// <returns>method</returns>
		/// <remarks>If you want to delete the define, call SetMemberValue with the function name and set the value to <c>null</c>.</remarks>
		public Delegate DefineMethod(string sMethodName, Delegate method, bool lIgnoreCase = false)
		{
			if (String.IsNullOrEmpty(sMethodName))
				throw new ArgumentNullException("methodName");
			if (method == null)
				throw new ArgumentNullException("method");

			Type typeFirstParameter = method.Method.GetParameters()[0].ParameterType;
			if (!typeFirstParameter.IsAssignableFrom(typeof(LuaTable)))
				throw new ArgumentException(String.Format(Properties.Resources.rsTableMethodExpected, sMethodName));

			SetMemberValueIntern(sMethodName, method, lIgnoreCase, false, false, true);
			return method;
		} // func DefineMethod

		internal Delegate DefineMethodLight(string sMethodName, Delegate method)
		{
			SetMemberValueIntern(sMethodName, method, false, false, false, true);
			return method;
		} // func DefineMethodLight

		#endregion

		#region -- CallMember -------------------------------------------------------------

		/// <summary>Call a member</summary>
		/// <param name="sMemberName">Name of the member</param>
		/// <returns>Result of the function call.</returns>
		public LuaResult CallMember(string sMemberName)
		{
			return CallMemberDirect(sMemberName, emptyObjectArray);
		} // func CallMember

		/// <summary>Call a member</summary>
		/// <param name="sMemberName">Name of the member</param>
		/// <param name="arg0">first argument</param>
		/// <returns>Result of the function call.</returns>
		public LuaResult CallMember(string sMemberName, object arg0)
		{
			return CallMemberDirect(sMemberName, new object[] { arg0, });
		} // func CallMember

		/// <summary>Call a member</summary>
		/// <param name="sMemberName">Name of the member</param>
		/// <param name="arg0">first argument</param>
		/// <param name="arg1">second argument</param>
		/// <returns>Result of the function call.</returns>
		public LuaResult CallMember(string sMemberName, object arg0, object arg1)
		{
			return CallMemberDirect(sMemberName, new object[] { arg0, arg1 });
		} // func CallMember

		/// <summary>Call a member</summary>
		/// <param name="sMemberName">Name of the member</param>
		/// <param name="arg0">first argument</param>
		/// <param name="arg1">second argument</param>
		/// <param name="arg2">third argument</param>
		/// <returns>Result of the function call.</returns>
		public LuaResult CallMember(string sMemberName, object arg0, object arg1, object arg2)
		{
			return CallMemberDirect(sMemberName, new object[] { arg0, arg1, arg2 });
		} // func CallMember

		/// <summary>Call a member</summary>
		/// <param name="sMemberName">Name of the member</param>
		/// <param name="args">Arguments</param>
		/// <returns>Result of the function call.</returns>
		public LuaResult CallMember(string sMemberName, params object[] args)
		{
			return CallMemberDirect(sMemberName, args);
		} // func CallMember

		internal object GetCallMember(string sMemberName, bool lIgnoreCase, bool lRawGet, out bool lIsMethod)
		{
			int iEntryIndex = FindKey(sMemberName, GetMemberHashCode(sMemberName), lIgnoreCase ? compareStringIgnoreCase : compareString);
			if (iEntryIndex < 0)
			{
				lIsMethod = false;
				return lRawGet ? null : OnIndex(sMemberName);
			}
			else
			{
				lIsMethod = entries[iEntryIndex].isMethod;
				return entries[iEntryIndex].value;
			}
		} // func GetCallMember

		/// <summary>Call a member (function or method) of the lua-table</summary>
		/// <param name="sMemberName">Name of the member</param>
		/// <param name="args">Arguments</param>
		/// <param name="lIgnoreCase">Ignore case of the member name</param>
		/// <param name="lRawGet"></param>
		/// <param name="lThrowExceptions"><c>true</c>, throws a exception if something is going wrong. <c>false</c>, on a exception a empty LuaResult will be returned.</param>
		/// <returns></returns>
		public LuaResult CallMemberDirect(string sMemberName, object[] args, bool lIgnoreCase = false, bool lRawGet = false, bool lThrowExceptions = true)
		{
			if (sMemberName == null)
				throw new ArgumentNullException(Properties.Resources.rsTableKeyNotNullable);

			// look up the member
			bool lIsMethod;
			object value = GetCallMember(sMemberName, lIgnoreCase, lRawGet, out lIsMethod);
			if (value == null)
			{
				if (lThrowExceptions)
					throw new ArgumentNullException(Properties.Resources.rsNilNotCallable);
				else
					return LuaResult.Empty;
			}

			// create the argument lists
			if (lIsMethod)
			{
				if (args.Length == 0)
					args = new object[] { null, value, this };
				else
				{
					object[] newArgs = new object[args.Length + 3];
					Array.Copy(args, 0, newArgs, 3, args.Length);
					newArgs[1] = value;
					newArgs[2] = this;
					args = newArgs;
				}
			}
			else
			{
				if (args.Length == 0)
					args = new object[] { null, value };
				else
				{
					object[] newArgs = new object[args.Length + 2];
					Array.Copy(args, 0, newArgs, 2, args.Length);
					newArgs[1] = value;
					args = newArgs;
				}
			}

			// call the method
			try
			{
				return RtInvokeSiteCached(args);
			}
			catch (TargetInvocationException e)
			{
				if (lThrowExceptions)
					throw new TargetInvocationException(String.Format(Properties.Resources.rsTableCallMemberFailed, sMemberName), e.InnerException);
				return LuaResult.Empty;
			}
			catch (Exception e)
			{
				if (lThrowExceptions)
					throw new TargetInvocationException(String.Format(Properties.Resources.rsTableCallMemberFailed, sMemberName), e);
				return LuaResult.Empty;
			}
		} // func CallMemberDirect

		internal object RtInvokeSite(object target, params object[] args)
		{
			// create the argument array
			object[] newArgs = new object[args.Length + 2];
			newArgs[1] = target;
			Array.Copy(args, 0, newArgs, 2, args.Length);

			return RtInvokeSiteCached(newArgs);
		} // func RtInvokeSite

		private LuaResult RtInvokeSiteCached(object[] args)
		{
			// get cached call site
			CallSite site;
			if (callSites.TryGetValue(args.Length, out site))
				args[0] = site;
			// call site
			return new LuaResult(Lua.RtInvokeSite(GetInvokeBinder, (callInfo, callSite) => callSites[callInfo.ArgumentCount + 1] = callSite, args));
		} // func RtInvokeSiteCached

		#endregion

		#region -- SetObjectMember --------------------------------------------------------

		/// <summary>Sets the given object with the members of the table.</summary>
		/// <param name="obj"></param>
		public object SetObjectMember(object obj)
		{
			if (obj == null)
				return obj;

			Type type = obj.GetType();

			// set all fields
			foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.SetField))
			{
				int iEntryIndex = FindKey(field.Name, GetMemberHashCode(field.Name), compareString);
				if (iEntryIndex >= 0)
					field.SetValue(obj, Lua.RtConvertValue(entries[iEntryIndex].value, field.FieldType));
			}

			// set all properties
			foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.SetProperty))
			{
				int iEntryIndex = FindKey(property.Name, GetMemberHashCode(property.Name), compareString);
				if (iEntryIndex >= 0)
					property.SetValue(obj, Lua.RtConvertValue(entries[iEntryIndex].value, property.PropertyType), null);
			}

			return obj;
		} // proc SetObjectMember

		#endregion

		#region -- Metatable --------------------------------------------------------------

		private bool TryInvokeMetaTableOperator<TRETURN>(string sKey, bool lRaise, out TRETURN r, params object[] args)
		{
			if (metaTable != null)
			{
				object o = metaTable[sKey];
				if (o != null)
				{
					if (Lua.IsCallable(o))
					{
						r = (TRETURN)Lua.RtConvertValue(RtInvokeSite(o, args), typeof(TRETURN));
						return true;
					}
					if (lRaise)
						throw new LuaRuntimeException(String.Format(Properties.Resources.rsTableOperatorIncompatible, sKey, "function"), 0, true);
				}
			}
			if (lRaise)
				throw new LuaRuntimeException(String.Format(Properties.Resources.rsTableOperatorNotFound, sKey), 0, true);

			r = default(TRETURN);
			return false;
		} // func GetMetaTableOperator

		private object UnaryOperation(string sKey)
		{
			object o;
			TryInvokeMetaTableOperator<object>(sKey, true, out o, this);
			return o;
		} // proc UnaryOperation

		private object BinaryOperation(string sKey, object arg)
		{
			object o;
			TryInvokeMetaTableOperator<object>(sKey, true, out o, this, arg);
			return o;
		} // proc BinaryOperation

		private bool BinaryBoolOperation(string sKey, object arg)
		{
			bool o;
			TryInvokeMetaTableOperator<bool>(sKey, true, out o, this, arg);
			return o;
		} // proc BinaryBoolOperation

		/// <summary></summary>
		/// <param name="arg"></param>
		/// <returns></returns>
		protected virtual object OnAdd(object arg)
		{
			return BinaryOperation("__add", arg);
		} // func OnAdd

		/// <summary></summary>
		/// <param name="arg"></param>
		/// <returns></returns>
		protected virtual object OnSub(object arg)
		{
			return BinaryOperation("__sub", arg);
		} // func OnSub

		/// <summary></summary>
		/// <param name="arg"></param>
		/// <returns></returns>
		protected virtual object OnMul(object arg)
		{
			return BinaryOperation("__mul", arg);
		} // func OnMul

		/// <summary></summary>
		/// <param name="arg"></param>
		/// <returns></returns>
		protected virtual object OnDiv(object arg)
		{
			return BinaryOperation("__div", arg);
		} // func OnDiv

		/// <summary></summary>
		/// <param name="arg"></param>
		/// <returns></returns>
		protected virtual object OnMod(object arg)
		{
			return BinaryOperation("__mod", arg);
		} // func OnMod

		/// <summary></summary>
		/// <param name="arg"></param>
		/// <returns></returns>
		protected virtual object OnPow(object arg)
		{
			return BinaryOperation("__pow", arg);
		} // func OnPow

		/// <summary></summary>
		/// <returns></returns>
		protected virtual object OnUnMinus()
		{
			return UnaryOperation("__unm");
		} // func OnUnMinus

		/// <summary></summary>
		/// <param name="arg"></param>
		/// <returns></returns>
		protected virtual object OnIDiv(object arg)
		{
			return BinaryOperation("__idiv", arg);
		} // func OnIDiv

		/// <summary></summary>
		/// <param name="arg"></param>
		/// <returns></returns>
		protected virtual object OnBAnd(object arg)
		{
			return BinaryOperation("__band", arg);
		} // func OnBAnd

		/// <summary></summary>
		/// <param name="arg"></param>
		/// <returns></returns>
		protected virtual object OnBOr(object arg)
		{
			return BinaryOperation("__bor", arg);
		} // func OnBOr

		/// <summary></summary>
		/// <param name="arg"></param>
		/// <returns></returns>
		protected virtual object OnBXor(object arg)
		{
			return BinaryOperation("__bxor", arg);
		} // func OnBXor

		/// <summary></summary>
		/// <returns></returns>
		protected virtual object OnBNot()
		{
			return UnaryOperation("__bnot");
		} // func OnBNot

		/// <summary></summary>
		/// <param name="arg"></param>
		/// <returns></returns>
		protected virtual object OnShl(object arg)
		{
			return BinaryOperation("__shl", arg);
		} // func OnShl

		/// <summary></summary>
		/// <param name="arg"></param>
		/// <returns></returns>
		protected virtual object OnShr(object arg)
		{
			return BinaryOperation("__shr", arg);
		} // func OnShr

		internal object InternConcat(object arg)
		{
			return OnConcat(arg);
		} // func InternConcat

		/// <summary></summary>
		/// <param name="arg"></param>
		/// <returns></returns>
		protected virtual object OnConcat(object arg)
		{
			return BinaryOperation("__concat", arg);
		} // func OnShr

		internal int InternLen()
		{
			return OnLen();
		} // func InternLen

		/// <summary></summary>
		/// <returns></returns>
		protected virtual int OnLen()
		{
			int iLen;
			if (TryInvokeMetaTableOperator<int>("__len", false, out iLen, this))
				return iLen;
			return Length;
		} // func OnLen

		/// <summary></summary>
		/// <param name="arg"></param>
		/// <returns></returns>
		protected virtual bool OnEqual(object arg)
		{
			return Equals(arg);
		} // func OnEqual

		/// <summary></summary>
		/// <param name="arg"></param>
		/// <returns></returns>
		protected virtual bool OnLessThan(object arg)
		{
			return BinaryBoolOperation("__lt", arg);
		} // func OnLessThan

		/// <summary></summary>
		/// <param name="arg"></param>
		/// <returns></returns>
		protected virtual bool OnLessEqual(object arg)
		{
			return BinaryBoolOperation("__le", arg);
		} // func OnLessEqual

		/// <summary></summary>
		/// <param name="key"></param>
		/// <returns></returns>
		protected virtual object OnIndex(object key)
		{
			if (Object.ReferenceEquals(metaTable, null))
				return null;

			object index = metaTable["__index"];
			LuaTable t;

			if ((t = index as LuaTable) != null) // default table
				return t.GetValue(key, false);
			else if (Lua.IsCallable(index)) // default function
				return new LuaResult(RtInvokeSite(index, this, key))[0];
			else
				return null;
		} // func OnIndex

		/// <summary></summary>
		/// <param name="key"></param>
		/// <param name="value"></param>
		/// <returns></returns>
		protected virtual bool OnNewIndex(object key, object value)
		{
			if (Object.ReferenceEquals(metaTable, null))
				return false;

			object o = metaTable["__newindex"];
			if (Lua.IsCallable(o))
			{
				RtInvokeSite(o, this, key, value);
				return true;
			}
			return false;
		} // func OnIndex

		/// <summary></summary>
		/// <param name="args"></param>
		/// <returns></returns>
		protected virtual object OnCall(object[] args)
		{
			if (args == null || args.Length == 0)
			{
				object o;
				TryInvokeMetaTableOperator<object>("__call", true, out o, this);
				return o;
			}
			else
			{
				object[] argsEnlarged = new object[args.Length + 1];
				argsEnlarged[0] = this;
				Array.Copy(args, 0, argsEnlarged, 1, args.Length);
				object o;
				TryInvokeMetaTableOperator<object>("__call", false, out o, argsEnlarged);
				return o;
			}
		} // func OnCall

		#endregion

		#region -- IDictionary<string, object> members ------------------------------------

		#region -- class LuaTableStringKeyCollection --------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		public class LuaTableStringKeyCollection : ICollection<string>
		{
			private LuaTable t;

			internal LuaTableStringKeyCollection(LuaTable t)
			{
				this.t = t;
			} // ctor

			/// <summary></summary>
			/// <param name="item"></param>
			/// <returns></returns>
			public bool Contains(string item)
			{
				return t.ContainsMember(item);
			} // func Contains

			/// <summary></summary>
			/// <param name="array"></param>
			/// <param name="arrayIndex"></param>
			public void CopyTo(string[] array, int arrayIndex)
			{
				if (arrayIndex < 0 || arrayIndex + Count > array.Length)
					throw new ArgumentOutOfRangeException();

				for (int i = HiddenMemberCount; i < t.entries.Length; i++)
				{
					object key = t.entries[i].key;
					if (key is string)
						array[arrayIndex++] = (string)key;
				}
			} // proc CopyTo

			/// <summary></summary>
			/// <returns></returns>
			public IEnumerator<string> GetEnumerator()
			{
				int iVersion = t.iVersion;
				for (int i = HiddenMemberCount; i < t.entries.Length; i++)
				{
					if (iVersion != t.iVersion)
						throw new InvalidOperationException("table changed");
					object key = t.entries[i].key;
					if (key is string)
						yield return (string)key;
				}
			} // func GetEnumerator

			IEnumerator IEnumerable.GetEnumerator()
			{
				return GetEnumerator();
			} // func IEnumerable.GetEnumerator

			void ICollection<string>.Add(string item) { throw new NotSupportedException(); }
			bool ICollection<string>.Remove(string item) { throw new NotSupportedException(); }
			void ICollection<string>.Clear() { throw new NotSupportedException(); }

			/// <summary></summary>
			public int Count { get { return t.iMemberCount - HiddenMemberCount; } }
			/// <summary>Always true</summary>
			public bool IsReadOnly { get { return true; } }
		} // class LuaTableStringKeyCollection

		#endregion

		#region -- class LuaTableStringValueCollection ------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		public class LuaTableStringValueCollection : ICollection<object>
		{
			private LuaTable t;

			internal LuaTableStringValueCollection(LuaTable t)
			{
				this.t = t;
			} // ctor

			/// <summary></summary>
			/// <param name="value"></param>
			/// <returns></returns>
			public bool Contains(object value)
			{
				for (int i = 0; i < t.entries.Length; i++)
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

				for (int i = HiddenMemberCount; i < t.entries.Length; i++)
				{
					if (t.entries[i].key is string)
						array[arrayIndex++] = t.entries[i].value;
				}
			} // proc CopyTo

			/// <summary></summary>
			/// <returns></returns>
			public IEnumerator<object> GetEnumerator()
			{
				int iVersion = t.iVersion;
				for (int i = HiddenMemberCount; i < t.entries.Length; i++)
				{
					if (iVersion != t.iVersion)
						throw new InvalidOperationException("table changed");

					if (t.entries[i].key is string)
						yield return t.entries[i].value;
				}
			} // func GetEnumerator

			IEnumerator IEnumerable.GetEnumerator()
			{
				return GetEnumerator();
			} // func IEnumerable.GetEnumerator

			void ICollection<object>.Add(object item) { throw new NotSupportedException(); }
			bool ICollection<object>.Remove(object item) { throw new NotSupportedException(); }
			void ICollection<object>.Clear() { throw new NotSupportedException(); }

			/// <summary></summary>
			public int Count { get { return t.iMemberCount - HiddenMemberCount; } }
			/// <summary>Always true</summary>
			public bool IsReadOnly { get { return true; } }
		} // class LuaTableStringValueCollection

		#endregion

		private LuaTableStringKeyCollection stringKeyCollection = null;
		private LuaTableStringValueCollection stringValueCollection = null;

		void IDictionary<string, object>.Add(string key, object value)
		{
			SetMemberValue(key, value, false, true);
		} // proc IDictionary<string, object>.Add

		bool IDictionary<string, object>.TryGetValue(string key, out object value)
		{
			return (value = GetMemberValue(key, false, true)) != null;
		} // func IDictionary<string, object>.TryGetValue

		bool IDictionary<string, object>.ContainsKey(string key)
		{
			return ContainsMember(key, false);
		} // func IDictionary<string, object>.ContainsKey

		bool IDictionary<string, object>.Remove(string key)
		{
			if (key == null)
				throw new ArgumentNullException(Properties.Resources.rsTableKeyNotNullable);

			return SetMemberValueIntern(key, null, false, true, false, false) == RemovedIndex;
		} // func IDictionary<string, object>.Remove

		ICollection<string> IDictionary<string, object>.Keys
		{
			get
			{
				if (stringKeyCollection == null)
					stringKeyCollection = new LuaTableStringKeyCollection(this);
				return stringKeyCollection;
			}
		} // prop IDictionary<string, object>.Keys

		ICollection<object> IDictionary<string, object>.Values
		{
			get
			{
				if (stringValueCollection == null)
					stringValueCollection = new LuaTableStringValueCollection(this);
				return stringValueCollection;
			}
		} // prop IDictionary<string, object>.Values

		object IDictionary<string, object>.this[string key]
		{
			get { return GetMemberValue(key, false, true); }
			set { SetMemberValue(key, value, false, true); }
		} // prop IDictionary<string, object>.this

		#endregion

		#region -- ICollection<KeyValuePair<string, object>> members ----------------------

		void ICollection<KeyValuePair<string, object>>.Add(KeyValuePair<string, object> item)
		{
			if (item.Key == null)
				throw new ArgumentNullException(Properties.Resources.rsTableKeyNotNullable);

			SetMemberValueIntern(item.Key, item.Value, false, false, true, false);
		} // proc ICollection<KeyValuePair<string, object>>.Add

		bool ICollection<KeyValuePair<string, object>>.Remove(KeyValuePair<string, object> item)
		{
			if (item.Key == null)
				throw new ArgumentNullException(Properties.Resources.rsTableKeyNotNullable);

			return SetMemberValueIntern(item.Key, null, false, true, false, false) == RemovedIndex;
		} // func ICollection<KeyValuePair<string, object>>.Remove

		void ICollection<KeyValuePair<string, object>>.Clear()
		{
			// remove the values
			for (int i = HiddenMemberCount; i < entries.Length; i++)
			{
				if (i < classDefinition.Count)
					if (classDefinition[i].mode == LuaTableDefineMode.Init)
						SetClassMemberValue(i, null, classDefinition[i].GetInitialValue(this), false);
					else
						SetClassMemberValue(i, null, null, false);
				else if (entries[i].hashCode != -1)
					RemoveValue(i);
			}
		} // proc ICollection<KeyValuePair<string, object>>.Clear

		bool ICollection<KeyValuePair<string, object>>.Contains(KeyValuePair<string, object> item)
		{
			return ContainsMember(item.Key);
		} // func ICollection<KeyValuePair<string, object>>.Contains

		void ICollection<KeyValuePair<string, object>>.CopyTo(KeyValuePair<string, object>[] array, int arrayIndex)
		{
			if (arrayIndex < 0 || arrayIndex + iMemberCount - HiddenMemberCount > array.Length)
				throw new ArgumentOutOfRangeException();

			for (int i = HiddenMemberCount; i < entries.Length; i++)
			{
				object key = entries[i].key;
				if (key is string)
					array[arrayIndex++] = new KeyValuePair<string, object>((string)key, entries[i].value);
			}
		} // proc ICollection<KeyValuePair<string, object>>.CopyTo

		int ICollection<KeyValuePair<string, object>>.Count
		{
			get { return iMemberCount - HiddenMemberCount; }
		} // func ICollection<KeyValuePair<string, object>>.Count

		bool ICollection<KeyValuePair<string, object>>.IsReadOnly { get { return false; } }

		#endregion

		#region -- IEnumerator<KeyValuePair<string, object>> members ----------------------

		IEnumerator<KeyValuePair<string, object>> IEnumerable<KeyValuePair<string, object>>.GetEnumerator()
		{
			int iVersion = this.iVersion;
			for (int i = HiddenMemberCount; i < entries.Length; i++)
			{
				if (iVersion != this.iVersion)
					throw new InvalidOperationException();

				object key = entries[i].key;
				if (key is string)
					yield return new KeyValuePair<string, object>((string)key, entries[i].value);
			}
		} // func IEnumerable<KeyValuePair<string, object>>.GetEnumerator

		#endregion

		#region -- IList<object> members --------------------------------------------------

		int IList<object>.IndexOf(object item)
		{
			return ArrayOnlyIndexOf(item);
		} // func IList<object>.IndexOf

		void IList<object>.Insert(int index, object item)
		{
			ArrayOnlyInsert(index, item);
		} // proc IList<object>.Insert

		void IList<object>.RemoveAt(int index)
		{
			ArrayOnlyRemoveAt(index);
		} // proc IList<object>.RemoveAt

		object IList<object>.this[int iIndex]
		{
			get
			{
				if (iIndex >= 0 && iIndex >= iArrayLength)
					throw new ArgumentOutOfRangeException();
				return arrayList[iIndex];
			}
			set
			{
				if (iIndex >= 0 && iIndex >= iArrayLength)
					throw new ArgumentOutOfRangeException();
				arrayList[iIndex] = value;
			}
		} // prop IList<object>.this

		#endregion

		#region -- ICollection<object> ----------------------------------------------------

		void ICollection<object>.Add(object item)
		{
			ArrayOnlyInsert(iArrayLength, item);
		} // proc ICollection<object>.Add

		bool ICollection<object>.Remove(object item)
		{
			int iIndex = ArrayOnlyIndexOf(item);
			if (iIndex >= 0)
			{
				ArrayOnlyRemoveAt(iIndex);
				return true;
			}
			else
				return false;
		} // func ICollection<object>.Remove

		void ICollection<object>.Clear()
		{
			Array.Clear(arrayList, 0, iArrayLength);
			iArrayLength = 0;
			iVersion++;
		} // proc ICollection<object>.Clear

		bool ICollection<object>.Contains(object item)
		{
			return ArrayOnlyIndexOf(item) >= 0;
		} // func ICollection<object>.Contains

		void ICollection<object>.CopyTo(object[] array, int arrayIndex)
		{
			if (arrayIndex + iArrayLength > array.Length)
				throw new ArgumentOutOfRangeException();

			Array.Copy(arrayList, 0, array, arrayIndex, iArrayLength);
		} // proc ICollection<object>.CopyTo

		int ICollection<object>.Count { get { return iArrayLength; } }

		bool ICollection<object>.IsReadOnly { get { return true; } }

		#endregion

		#region -- IEnumerable<object> ----------------------------------------------------

		IEnumerator<object> IEnumerable<object>.GetEnumerator()
		{
			int iVersion = this.iVersion;
			for (int i = 0; i < iArrayLength; i++)
			{
				if (iVersion != this.iVersion)
					throw new InvalidOperationException();

				yield return arrayList[i];
			}
		} // func IEnumerable<object>.GetEnumerator

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
			{
				return t.ContainsKey(item);
			} // func Contains

			/// <summary></summary>
			/// <param name="array"></param>
			/// <param name="arrayIndex"></param>
			public void CopyTo(object[] array, int arrayIndex)
			{
				if (arrayIndex < 0 || arrayIndex + t.iCount > array.Length)
					throw new ArgumentOutOfRangeException();

				for (int i = 0; i < t.arrayList.Length; i++)
					array[arrayIndex++] = i + 1;

				for (int i = HiddenMemberCount; i < t.entries.Length; i++)
					if (t.entries[i].hashCode != -1)
						array[arrayIndex++] = t.entries[i].key;
			} // proc CopyTo

			/// <summary></summary>
			/// <returns></returns>
			public IEnumerator<object> GetEnumerator()
			{
				int iVersion = t.iVersion;

				for (int i = 0; i < t.arrayList.Length; i++)
				{
					if (iVersion != t.iVersion)
						throw new InvalidOperationException("table changed");

					yield return i + 1;
				}
				for (int i = HiddenMemberCount; i < t.entries.Length; i++)
				{
					if (iVersion != t.iVersion)
						throw new InvalidOperationException("table changed");

					if (t.entries[i].hashCode != -1)
						yield return t.entries[i].key;
				}
			} // func GetEnumerator

			IEnumerator IEnumerable.GetEnumerator()
			{
				return GetEnumerator();
			} // func IEnumerable.GetEnumerator

			void ICollection<object>.Add(object item) { throw new NotSupportedException(); }
			bool ICollection<object>.Remove(object item) { throw new NotSupportedException(); }
			void ICollection<object>.Clear() { throw new NotSupportedException(); }

			/// <summary></summary>
			public int Count { get { return t.iCount - HiddenMemberCount; } }
			/// <summary>Always true</summary>
			public bool IsReadOnly { get { return true; } }
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
				for (int i = 0; i < t.arrayList.Length; i++)
				{
					if (t.arrayList[i] != null && comparerObject.Equals(t.arrayList[i], value))
						return true;
				}

				for (int i = HiddenMemberCount; i < t.classDefinition.Count; i++)
				{
					if (comparerObject.Equals(t.GetClassMemberValue(i, t.entries[i].key, true), value))
						return true;
				}

				for (int i = t.classDefinition.Count; i < t.entries.Length; i++)
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
				if (arrayIndex < 0 || arrayIndex + t.iCount > array.Length)
					throw new ArgumentOutOfRangeException();

				for (int i = 0; i < t.arrayList.Length; i++)
				{
					if (t.arrayList[i] != null)
						array[arrayIndex++] = t.arrayList[i];
				}

				for (int i = HiddenMemberCount; i < t.classDefinition.Count; i++)
				{
					array[arrayIndex++] = t.GetClassMemberValue(i, t.entries[i].key, true);
				}

				for (int i = t.classDefinition.Count; i < t.entries.Length; i++)
				{
					if (t.entries[i].hashCode != -1)
						array[arrayIndex++] = t.entries[i].value;
				}
			} // proc CopyTo

			/// <summary></summary>
			/// <returns></returns>
			public IEnumerator<object> GetEnumerator()
			{
				int iVersion = t.iVersion;

				for (int i = 0; i < t.arrayList.Length; i++)
				{
					if (iVersion != t.iVersion)
						throw new InvalidOperationException("table changed");

					if (t.arrayList[i] != null)
						yield return t.arrayList[i];
				}

				for (int i = HiddenMemberCount; i < t.classDefinition.Count; i++)
				{
					if (iVersion != t.iVersion)
						throw new InvalidOperationException("table changed");

					yield return t.GetClassMemberValue(i, t.entries[i].key, true);
				}

				for (int i = t.classDefinition.Count; i < t.entries.Length; i++)
				{
					if (iVersion != t.iVersion)
						throw new InvalidOperationException("table changed");

					if (t.entries[i].hashCode != -1)
						yield return t.entries[i].value;
				}
			} // func GetEnumerator

			IEnumerator IEnumerable.GetEnumerator()
			{
				return GetEnumerator();
			} // func IEnumerable.GetEnumerator

			void ICollection<object>.Add(object item) { throw new NotSupportedException(); }
			bool ICollection<object>.Remove(object item) { throw new NotSupportedException(); }
			void ICollection<object>.Clear() { throw new NotSupportedException(); }

			/// <summary></summary>
			public int Count { get { return t.iCount - HiddenMemberCount; } }
			/// <summary>Always true</summary>
			public bool IsReadOnly { get { return true; } }
		} // class LuaTableHashValueCollection

		#endregion

		private LuaTableHashKeyCollection hashKeyCollection = null;
		private LuaTableHashValueCollection hashValueCollection = null;

		void IDictionary<object, object>.Add(object key, object value)
		{
			if (ContainsKey(key))
				throw new ArgumentException(String.Format(Properties.Resources.rsTableAddDuplicate, key));

			SetValue(key, value, true);
		} // proc IDictionary<object, object>.Add

		bool IDictionary<object, object>.TryGetValue(object key, out object value)
		{
			return (value = GetValue(key, true)) != null;
		} // func IDictionary<object, object>.TryGetValue

		bool IDictionary<object, object>.ContainsKey(object key)
		{
			return ContainsKey(key);
		} // func IDictionary<object, object>.ContainsKey

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

		internal object NextKey(object next)
		{
			if (next == null)
			{
				if (iArrayLength == 0)
					return NextHashKey(HiddenMemberCount);
				else
					return 1;
			}
			else if (next is int)
			{
				int iKey = (int)next;
				if (iKey < iArrayLength)
					return iKey + 1;
				else
				{
					iKey--;
					while (iKey < arrayList.Length)
					{
						if (arrayList[iKey] != null)
							return iKey + 1;
						iKey++;
					}
				}
				return NextHashKey(HiddenMemberCount);
			}
			else
			{
				int iCurrentEntryIndex = Array.FindIndex(entries, c => comparerObject.Equals(c.key, next));
				if (iCurrentEntryIndex == -1 || iCurrentEntryIndex == entries.Length - 1)
					return null;
				return NextHashKey(iCurrentEntryIndex + 1);
			}
		} // func NextKey

		private object NextHashKey(int iStartIndex)
		{
			int iEntryIndex = Array.FindIndex(entries, iStartIndex, c => c.hashCode != -1);
			if (iEntryIndex == -1)
				return null;
			else
				return entries[iEntryIndex].key;
		} // func FirstHashKey

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
		{
			return ContainsKey(item.Key);
		} // func ICollection<KeyValuePair<object, object>>.Contains

		void ICollection<KeyValuePair<object, object>>.CopyTo(KeyValuePair<object, object>[] array, int arrayIndex)
		{
			if (arrayIndex + iCount > array.Length)
				throw new ArgumentOutOfRangeException();

			// copy the array part
			for (int i = 0; i < arrayList.Length; i++)
			{
				if (arrayList[i] != null)
					array[arrayIndex++] = new KeyValuePair<object, object>(i + 1, arrayList[i]);
			}

			// copy the class part
			for (int i = HiddenMemberCount; i < classDefinition.Count; i++)
			{
				object value = GetClassMemberValue(i, null, true);
				if (value != null)
					array[arrayIndex++] = new KeyValuePair<object, object>(entries[i].key, value);
			}

			// copy the  hash part
			for (int i = classDefinition.Count; i < entries.Length; i++)
			{
				if (entries[i].hashCode != -1)
					array[arrayIndex++] = new KeyValuePair<object, object>(entries[i].key, entries[i].value);
			}
		} // proc ICollection<KeyValuePair<object, object>>.CopyTo

		int ICollection<KeyValuePair<object, object>>.Count { get { return iCount - HiddenMemberCount; } }
		bool ICollection<KeyValuePair<object, object>>.IsReadOnly { get { return false; } }

		#endregion

		#region -- IEnumerator<object, object> members ------------------------------------

		/// <summary></summary>
		/// <returns></returns>
		public IEnumerator<KeyValuePair<object, object>> GetEnumerator()
		{
			int iVersion = this.iVersion;

			// enumerate the array part
			for (int i = 0; i < arrayList.Length; i++)
			{
				if (iVersion != this.iVersion)
					throw new InvalidOperationException();

				if (arrayList[i] != null)
					yield return new KeyValuePair<object, object>(i + 1, arrayList[i]);
			}

			// enumerate the class part
			for (int i = HiddenMemberCount; i < classDefinition.Count; i++)
			{
				if (iVersion != this.iVersion)
					throw new InvalidOperationException();

				object value = GetClassMemberValue(i, null, true);
				if (value != null)
					yield return new KeyValuePair<object, object>(entries[i].key, value);
			}

			// enumerate the hash part
			for (int i = classDefinition.Count; i < entries.Length; i++)
			{
				if (iVersion != this.iVersion)
					throw new InvalidOperationException();

				if (entries[i].hashCode != -1)
					yield return new KeyValuePair<object, object>(entries[i].key, entries[i].value);
			}
		} // func IEnumerator<KeyValuePair<object, object>>

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		} // func System.Collections.IEnumerable.GetEnumerator

    #endregion

    #region -- ITypedList.GetItemProperties members -----------------------------------

    private T FindTypeList<T>(LuaTable current, PropertyDescriptor[] listAccessors, int iIndex, Func<LuaTable, PropertyDescriptor, T> action, Func<ITypedList, PropertyDescriptor[], T> continueAction)
    {
      if (iIndex < listAccessors.Length)
      {
        object newCurrent = listAccessors[iIndex].GetValue(current);
        if (newCurrent is LuaTable)
          return FindTypeList((LuaTable)newCurrent, listAccessors, iIndex + 1, action, continueAction);
        else if (newCurrent is ITypedList)
        {
          if (iIndex < listAccessors.Length - 1)
          {
            PropertyDescriptor[] newListAccessors = new PropertyDescriptor[listAccessors.Length - iIndex - 1];
            Array.Copy(listAccessors, iIndex + 1, newListAccessors, 0, newListAccessors.Length);
            return continueAction((ITypedList)newCurrent, newListAccessors);
          }
          else
            return continueAction((ITypedList)newCurrent, null);
        }
        else
          return default(T);
      }
      else
        return action(current, listAccessors[iIndex - 1]);
    } // func FindTypeList

    PropertyDescriptorCollection ITypedList.GetItemProperties(PropertyDescriptor[] listAccessors)
    {
      return FindTypeList<PropertyDescriptorCollection>(this, listAccessors, 0, GetItemProperties, (t, la) => t.GetItemProperties(la));
    } /// func ITypedList.GetItemProperties

    string ITypedList.GetListName(PropertyDescriptor[] listAccessors)
    {
      if (listAccessors == null || listAccessors.Length == 0)
        return String.Empty;

      return FindTypeList<string>(this, listAccessors, 0, (t, p) => p.Name, (t, la) => t.GetListName(la));
    } // func ITypedList.GetListName 

    private static PropertyDescriptorCollection GetItemProperties(LuaTable table, PropertyDescriptor p)
    {
      return table.GetItemProperties();
    } // func PropertyDescriptorCollection

    /// <summary>Returns the current property table.</summary>
    /// <param name="properties">Properties, that must be in the list.</param>
    /// <returns>PropertyCollection</returns>
    public PropertyDescriptorCollection GetItemProperties(params string[] properties)
    {
      PropertyDescriptorCollection c = new PropertyDescriptorCollection(null, false);

      // Return the class properties
      for (int i = 0; i < classDefinition.Count; i++)
        c.Add(classDefinition[i].GetPropertyDescriptor(i));

      // Return the dynamic properties
      for (int i = classDefinition.Count; i < entries.Length; i++)
      {
        if (entries[i].key is string)
          c.Add(LuaTableValuePropertyDescriptor.Get((string)entries[i].key));
      }

      // Return extra properties
      for (int i = 0; i < properties.Length; i++)
        if (c.Find(properties[i], true) == null)
          c.Add(LuaTableValuePropertyDescriptor.Get(properties[i]));

      return c;
    } // func GetItemProperties

    /// <summary>Returns the property descriptor for exact one member</summary>
    /// <param name="sMemberName">Name of the member</param>
    /// <returns>Descriptor, for the property. Every memberName gets only one instance</returns>
    public PropertyDescriptor GetItemProperty(string sMemberName)
    {
      int iEntryIndex = FindKey(sMemberName, GetMemberHashCode(sMemberName), compareStringIgnoreCase);
      if (iEntryIndex < classDefinition.Count)
        return classDefinition[iEntryIndex].GetPropertyDescriptor(iEntryIndex);
      else
        return LuaTableValuePropertyDescriptor.Get(sMemberName);
    } // proc GetItemProperty

		#endregion

		/// <summary>Returns or sets an value in the lua-table.</summary>
		/// <param name="iIndex">Index.</param>
		/// <returns>Value or <c>null</c></returns>
		public object this[int iIndex] { get { return GetArrayValue(iIndex, false); } set { SetArrayValue(iIndex, value, false); } }
		/// <summary>Returns or sets an value in the lua-table.</summary>
		/// <param name="sName">Index.</param>
		/// <returns>Value or <c>null</c></returns>
		public object this[string sName] { get { return GetMemberValue(sName, false, false); } set { SetMemberValue(sName, value, false, false); } }
		/// <summary>Returns or sets an value in the lua-table.</summary>
		/// <param name="key">Index.</param>
		/// <returns>Value or <c>null</c></returns>
		public object this[object key] { get { return GetValue(key, false); } set { SetValue(key, value, false); } }
		/// <summary>Returns or sets an value in the lua-table.</summary>
		/// <param name="keyList">Index list.</param>
		/// <returns>Value or <c>null</c></returns>
		public object this[params object[] keyList] { get { return GetValue(keyList, false); } set { SetValue(keyList, value, false); } }

		/// <summary>Access to the array part</summary>
		public IList<object> ArrayList { get { return this; } }
		/// <summary>Access to all members</summary>
		public IDictionary<string, object> Members { get { return this; } }
		/// <summary>Access to all values.</summary>
		public IDictionary<object, object> Values { get { return this; } }

		/// <summary>Length if it is an array.</summary>
		public int Length { get { return iArrayLength; } }
		/// <summary>Access to the __metatable</summary>
    [
    LuaMember(csMetaTable),
    Browsable(false),
    ]
		public LuaTable MetaTable { get { return metaTable; } set { metaTable = value; } }

		// -- Static --------------------------------------------------------------

		private static readonly IEqualityComparer comparerObject = EqualityComparer<object>.Default;
		private static readonly IEqualityComparer comparerInt = EqualityComparer<int>.Default;
		private static readonly IEqualityComparer compareString = StringComparer.Ordinal;
		private static readonly IEqualityComparer compareStringIgnoreCase = StringComparer.OrdinalIgnoreCase;

		private static readonly LuaTableEntry[] emptyLuaEntries = new LuaTableEntry[0];
		private static readonly object[] emptyObjectArray = new object[0];
		private static readonly int[] emptyIntArray = new int[0];

		#region -- Table Manipulation -----------------------------------------------------

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
				j = t.iArrayLength;

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
			// insert the value at the position
			int iIndex;
			if (IsIndexKey(pos, out iIndex) && iIndex >= 1 && iIndex <= t.iArrayLength + 1)
				t.ArrayOnlyInsert(iIndex - 1, value);
			else
				t.SetValue(pos, value, true);
		} // proc insert

		#endregion

		#region -- move --

		/// <summary></summary>
		/// <param name="t1"></param>
		/// <param name="f"></param>
		/// <param name="e"></param>
		/// <param name="t"></param>
		public static void move(LuaTable t1, int f, int e, int t)
		{
			move(t1, f, e, t, t1);
		} // proc move

		/// <summary></summary>
		/// <param name="t1"></param>
		/// <param name="f"></param>
		/// <param name="e"></param>
		/// <param name="t"></param>
		/// <param name="t2"></param>
		public static void move(LuaTable t1, int f, int e, int t, LuaTable t2)
		{
			if (f < 0)
				throw new ArgumentOutOfRangeException("f");
			if (t < 0)
				throw new ArgumentOutOfRangeException("t");
			if (f > e)
				return;

			while (f < e)
				t2[t++] = t1[f++];
		} // proc move

		#endregion

		#region -- pack --

		/// <summary>Returns a new table with all parameters stored into keys 1, 2, etc. and with a field &quot;n&quot; 
		/// with the total number of parameters. Note that the resulting table may not be a sequence.</summary>
		/// <param name="values"></param>
		/// <returns></returns>
		public static LuaTable pack(object[] values)
		{
			LuaTable t = new LuaTable(values);
			t.SetMemberValueIntern("n", values.Length, false, true, false, false); // set the element count, because it can be different
			return t;
		} // func pack

		/// <summary>Returns a new table with all parameters stored into keys 1, 2, etc. and with a field &quot;n&quot; 
		/// with the total number of parameters. Note that the resulting table may not be a sequence.</summary>
		/// <param name="values"></param>
		/// <returns></returns>
		public static LuaTable pack<T>(T[] values)
		{
			object[] v = new object[values.Length];
			for (int i = 0; i < values.Length; i++)
				v[i] = values[i];
			return pack(v);
		} // func pack

		#endregion

		#region -- remove --

		/// <summary>Removes from list the last element.</summary>
		/// <param name="t"></param>
		public static object remove(LuaTable t)
		{
			return remove(t, t.Length);
		} // proc remove

		/// <summary>Removes from list the element at position pos, returning the value of the removed element.</summary>
		/// <param name="t"></param>
		/// <param name="pos"></param>
		public static object remove(LuaTable t, int pos)
		{
			object r;
			int iIndex;
			if (IsIndexKey(pos, out iIndex))
			{
				if (iIndex >= 1 && iIndex <= t.iArrayLength)  // remove the element and shift the follower
				{
					r = t.arrayList[iIndex - 1];
					t.ArrayOnlyRemoveAt(iIndex - 1);
				}
				else
				{
					r = t.GetArrayValue(iIndex, true);
					t.SetArrayValue(iIndex, null, true); // just remove the element
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
			private LuaTable t;
			private object compare;

			public SortComparer(LuaTable t, object compare)
			{
				this.t = t;
				this.compare = compare;
			} // ctor

			public int Compare(object x, object y)
			{
				if (compare == null)
					return Comparer.Default.Compare(x, y);
				else
				{
					// Call the comparer
					object r = t.RtInvokeSite(compare, x, y);
					if (r is LuaResult)
						r = ((LuaResult)r)[0];

					// check the value
					if (r is int)
						return (int)r;
					else if ((bool)Lua.RtConvertValue(r, typeof(bool)))
						return -1;
					else if (Comparer.Default.Compare(x, y) == 0)
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
		{
			Array.Sort(t.arrayList, 0, t.iArrayLength, new SortComparer(t, sort));
		} // proc sort

		#endregion

		#region -- unpack --

		/// <summary>Returns the elements from the given table.</summary>
		/// <param name="t"></param>
		/// <returns></returns>
		public static LuaResult unpack(LuaTable t)
		{
			return unpack(t, 1, t.Length);
		} // func unpack

		/// <summary>Returns the elements from the given table.</summary>
		/// <param name="t"></param>
		/// <param name="i"></param>
		/// <returns></returns>
		public static LuaResult unpack(LuaTable t, int i)
		{
			return unpack(t, i, t.Length);
		} // func unpack

		/// <summary>Returns the elements from the given table.</summary>
		/// <param name="t"></param>
		/// <param name="i"></param>
		/// <param name="j"></param>
		/// <returns></returns>
		public static LuaResult unpack(LuaTable t, int i, int j)
		{
			return new LuaResult(LuaResult.CopyMode.None, unpack(t, i, j, LuaResult.Empty.Values));
		} // func unpack

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
		{
			return collect(t, 1, t.Length);
		} // func unpack

		/// <summary>Returns the elements from the given table as a sequence.</summary>
		/// <param name="t"></param>
		/// <param name="i"></param>
		/// <returns></returns>
		public static LuaResult collect(LuaTable t, int i)
		{
			return collect(t, i, t.Length);
		} // func unpack

		/// <summary>Returns the elements from the given table as a sequence.</summary>
		/// <param name="t"></param>
		/// <param name="i"></param>
		/// <param name="j"></param>
		/// <returns></returns>
		public static LuaResult collect(LuaTable t, int i, int j)
		{
			return new LuaResult(LuaResult.CopyMode.None, collect(t, i, j, LuaResult.Empty.Values));
		} // func unpack

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

			if (i >= 1 && i <= t.iArrayLength && j >= 1 && j <= t.iArrayLength) // within the array
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
				var indexList = new List<KeyValuePair<int, T>>(Math.Max(Math.Min(j - i + 1, t.iCount), 1));

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
				for (int k = 0; k < t.entries.Length; k++)
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
					T[] result = new T[indexList.Count];
					for (int k = 0; k < result.Length; k++)
						result[k] = indexList[k].Value;

					return result;
				}
			}
		} // func unpack

		#endregion

		#endregion

		#region -- c#/vb.net operators ----------------------------------------------------

		/// <summary></summary>
		/// <param name="table"></param>
		/// <param name="arg"></param>
		/// <returns></returns>
		public static object operator +(LuaTable table, object arg)
		{
			return table.OnAdd(arg);
		} // operator +

		/// <summary></summary>
		/// <param name="table"></param>
		/// <param name="arg"></param>
		/// <returns></returns>
		public static object operator -(LuaTable table, object arg)
		{
			return table.OnSub(arg);
		} // operator -

		/// <summary></summary>
		/// <param name="table"></param>
		/// <param name="arg"></param>
		/// <returns></returns>
		public static object operator *(LuaTable table, object arg)
		{
			return table.OnMul(arg);
		} // operator *

		/// <summary></summary>
		/// <param name="table"></param>
		/// <param name="arg"></param>
		/// <returns></returns>
		public static object operator /(LuaTable table, object arg)
		{
			return table.OnDiv(arg);
		} // operator /

		/// <summary></summary>
		/// <param name="table"></param>
		/// <param name="arg"></param>
		/// <returns></returns>
		public static object operator %(LuaTable table, object arg)
		{
			return table.OnMod(arg);
		} // operator %

		/// <summary></summary>
		/// <param name="table"></param>
		/// <returns></returns>
		public static object operator -(LuaTable table)
		{
			return table.OnUnMinus();
		} // operator -

		/// <summary></summary>
		/// <param name="table"></param>
		/// <param name="arg"></param>
		/// <returns></returns>
		public static bool operator ==(LuaTable table, object arg)
		{
			if (Object.ReferenceEquals(table, null))
				return Object.ReferenceEquals(arg, null);
			else
				return table.Equals(arg);
		} // operator ==

		/// <summary></summary>
		/// <param name="table"></param>
		/// <param name="arg"></param>
		/// <returns></returns>
		public static bool operator !=(LuaTable table, object arg)
		{
			if (Object.ReferenceEquals(table, null))
				return !Object.ReferenceEquals(arg, null);
			else
				return !table.Equals(arg);
		} // operator !=

		/// <summary></summary>
		/// <param name="table"></param>
		/// <param name="arg"></param>
		/// <returns></returns>
		public static object operator <(LuaTable table, object arg)
		{
			return table.OnLessThan(arg);
		} // operator <

		/// <summary></summary>
		/// <param name="table"></param>
		/// <param name="arg"></param>
		/// <returns></returns>
		public static object operator >(LuaTable table, object arg)
		{
			return !table.OnLessThan(arg);
		} // operator >

		/// <summary></summary>
		/// <param name="table"></param>
		/// <param name="arg"></param>
		/// <returns></returns>
		public static object operator <=(LuaTable table, object arg)
		{
			return table.OnLessEqual(arg);
		} // operator <=

		/// <summary></summary>
		/// <param name="table"></param>
		/// <param name="arg"></param>
		/// <returns></returns>
		public static object operator >=(LuaTable table, object arg)
		{
			return !table.OnLessEqual(arg);
		} // operator >=

		/// <summary></summary>
		/// <param name="table"></param>
		/// <param name="arg"></param>
		/// <returns></returns>
		public static object operator >>(LuaTable table, int arg)
		{
			return table.OnShr(arg);
		} // operator >>

		/// <summary></summary>
		/// <param name="table"></param>
		/// <param name="arg"></param>
		/// <returns></returns>
		public static object operator <<(LuaTable table, int arg)
		{
			return table.OnShl(arg);
		} // operator <<

		/// <summary></summary>
		/// <param name="table"></param>
		/// <param name="arg"></param>
		/// <returns></returns>
		public static object operator &(LuaTable table, object arg)
		{
			return table.OnBAnd(arg);
		} // operator &

		/// <summary></summary>
		/// <param name="table"></param>
		/// <param name="arg"></param>
		/// <returns></returns>
		public static object operator |(LuaTable table, object arg)
		{
			return table.OnBOr(arg);
		} // operator |

		/// <summary></summary>
		/// <param name="table"></param>
		/// <param name="arg"></param>
		/// <returns></returns>
		public static object operator ^(LuaTable table, object arg)
		{
			return table.OnBXor(arg);
		} // operator ^

		/// <summary></summary>
		/// <param name="table"></param>
		/// <returns></returns>
		public static object operator ~(LuaTable table)
		{
			return table.OnBNot();
		} // operator ~

		#endregion
	} // class LuaTable
}
