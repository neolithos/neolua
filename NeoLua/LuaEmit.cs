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
using System.Diagnostics;
using System.Dynamic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Type = System.Type;

namespace Neo.IronLua
{
	#region -- enum LuaTryGetMemberReturn ---------------------------------------------

	internal enum LuaTryGetMemberReturn
	{
		None,
		ValidExpression,
		NotReadable
	} // enum LuaTryGetMemberReturn

	#endregion

	#region -- enum LuaTrySetMemberReturn ---------------------------------------------

	internal enum LuaTrySetMemberReturn
	{
		None,
		ValidExpression,
		NotWritable
	} // enum LuaTrySetMemberReturn

	#endregion

	#region -- class LuaEmitException -------------------------------------------------

	internal class LuaEmitException : Exception
	{
		public const int ConversationNotDefined = -1;
		public const int OperatorNotDefined = -2;
		public const int InvokeNoDelegate = -3;
		public const int CanNotReadMember = -4;
		public const int CanNotWriteMember = -5;
		public const int MemberNotFound = -6;
		public const int MemberNotUnique = -7;
		public const int IndexNotFound = -8;

		private readonly int errorCode;

		public LuaEmitException(int errorCode, object arg0 = null, object arg1 = null, object arg2 = null)
			: base(GetMessageText(errorCode, arg0, arg1, arg2))
		{
			this.errorCode = errorCode;
		} // ctor

		public int Code => errorCode;

		public static string GetMessageText(int errorCode, object arg0 = null, object arg1 = null, object arg2 = null)
		{
			switch (errorCode)
			{
				case ConversationNotDefined:
					return String.Format(Properties.Resources.rsBindConversionNotDefined, arg0, arg1);
				case OperatorNotDefined:
					return String.Format(Properties.Resources.rsBindOperatorNotDefined, arg0, arg1, arg2);
				case InvokeNoDelegate:
					return String.Format(Properties.Resources.rsInvokeNoDelegate, arg0);
				case CanNotReadMember:
					return String.Format(Properties.Resources.rsMemberNotReadable, arg0, arg1);
				case CanNotWriteMember:
					return String.Format(Properties.Resources.rsMemberNotWritable, arg0, arg1);
				case MemberNotFound:
					return String.Format(Properties.Resources.rsMemberNotResolved, arg0, arg1);
				case MemberNotUnique:
					return String.Format(Properties.Resources.rsMemberNotUnique, arg0, arg1);
				case IndexNotFound:
					return String.Format(Properties.Resources.rsIndexNotFound, arg0);
				default:
					return String.Format("Code={0};Arg0={1};Arg1={2}", errorCode, arg0, arg1);
			}
		} // func GetMessageText
	} // class LuaEmitException

	#endregion

	#region -- enum LuaEmitTypeCode ---------------------------------------------------

	/// <summary>Translates type definitions to codes, that are easier to compare</summary>
	internal enum LuaEmitTypeCode
	{
		Object = 0,
		Boolean = 0x10,
		Char = 0x20,
		String = 0x30,

		SByte = 0x41,
		Byte = 0x53,
		Int16 = 0x61,
		UInt16 = 0x73,
		Int32 = 0x81,
		UInt32 = 0x93,
		Int64 = 0xA1,
		UInt64 = 0xB3,

		Single = 0xC0,
		Double = 0xD0,

		Decimal = 0xE0,
		DateTime = 0xF0,

		IntegerFlag = 0x01,
		UnsignedFlag = 0x02
	} // enum LuaEmitTypeCode

	#endregion

	#region -- class LuaEmit ----------------------------------------------------------

	internal static class LuaEmit
	{
		private const string csImplicit = "op_Implicit";
		private const string csExplicit = "op_Explicit";
		private const string csParse = "Parse";
		private const string csToString = "ToString";

#pragma warning disable IDE1006 // Naming Styles
		private static readonly TypeInfo DynamicMetaObjectProviderTypeInfo = typeof(IDynamicMetaObjectProvider).GetTypeInfo();
#pragma warning restore IDE1006 // Naming Styles

		#region -- Type Helper --------------------------------------------------------

		/// <summary>Should the type thread as dynamic.</summary>
		/// <param name="type"></param>
		/// <returns><c>true</c>, if the type is object or implements IDynamicMetaObjectProvider</returns>
		public static bool IsDynamicType(Type type)
			=> type == typeof(object) || DynamicMetaObjectProviderTypeInfo.IsAssignableFrom(type.GetTypeInfo());

		private static TypeInfo UnpackTypeInfo(Type type)
		{
			var ti = type.GetTypeInfo();
			if (ti.IsGenericType && ti.GetGenericTypeDefinition() == typeof(Nullable<>))
				ti = ti.GenericTypeArguments[0].GetTypeInfo();
			if (ti.IsEnum)
				ti = Enum.GetUnderlyingType(ti.AsType()).GetTypeInfo();
			return ti;
		} // func UnpackType

		/// <summary>Gets a type code for the type.</summary>
		/// <param name="type"></param>
		/// <returns></returns>
		public static LuaEmitTypeCode GetTypeCode(Type type)
			=> GetTypeCode(UnpackTypeInfo(type));

		private static LuaEmitTypeCode GetTypeCode(TypeInfo ti)
		{
			var type = ti.AsType();
			switch (type.Name[0])
			{
				case 'B':
					if (type == typeof(bool))
						return LuaEmitTypeCode.Boolean;
					else if (type == typeof(byte))
						return LuaEmitTypeCode.Byte;
					else
						return LuaEmitTypeCode.Object;
				case 'C':
					if (type == typeof(char))
						return LuaEmitTypeCode.Char;
					else
						return LuaEmitTypeCode.Object;
				case 'D':
					if (type == typeof(double))
						return LuaEmitTypeCode.Double;
					else if (type == typeof(decimal))
						return LuaEmitTypeCode.Decimal;
					else if (type == typeof(DateTime))
						return LuaEmitTypeCode.DateTime;
					else
						return LuaEmitTypeCode.Object;
				case 'I':
					if (type == typeof(int))
						return LuaEmitTypeCode.Int32;
					else if (type == typeof(short))
						return LuaEmitTypeCode.Int16;
					else if (type == typeof(long))
						return LuaEmitTypeCode.Int64;
					else
						return LuaEmitTypeCode.Object;
				case 'S':
					if (type == typeof(string))
						return LuaEmitTypeCode.String;
					else if (type == typeof(sbyte))
						return LuaEmitTypeCode.SByte;
					else if (type == typeof(float))
						return LuaEmitTypeCode.Single;
					else
						return LuaEmitTypeCode.Object;
				case 'U':
					if (type == typeof(uint))
						return LuaEmitTypeCode.UInt32;
					else if (type == typeof(ushort))
						return LuaEmitTypeCode.UInt16;
					else if (type == typeof(ulong))
						return LuaEmitTypeCode.UInt64;
					else
						return LuaEmitTypeCode.Object;
				default:
					return LuaEmitTypeCode.Object;
			}
		} // func GetTypeCode

		internal static bool TypesMatch(Type typeTo, Type typeFrom, out MemberMatchValue match, bool stringAutoConvert = true, bool toTypeIsParamsArray = false)
		{
			if (typeTo == typeFrom)
			{
				match = MemberMatchValue.Exact;
				return true;
			}

			var tiTo = UnpackTypeInfo(typeTo);
			var tiFrom = UnpackTypeInfo(typeFrom);

			if (tiTo == tiFrom)
			{
				match = MemberMatchValue.Exact;
				return true;
			}
			else if (tiTo.IsAssignableFrom(tiFrom))
			{
				bool arrayDecay = !typeTo.IsArray && (typeFrom.IsArray || typeFrom == typeof(LuaResult));
				match = arrayDecay ? MemberMatchValue.ArrayDecay : MemberMatchValue.AssignableMatch;
				return true;
			}
			else if (toTypeIsParamsArray)
			{
				var elementTypeMatches = TypesMatch(typeTo.GetElementType(), typeFrom, out match, stringAutoConvert, toTypeIsParamsArray: false);
				return elementTypeMatches;
			}
			else
			{
				var tcTo = GetTypeCode(tiTo);
				var tcFrom = GetTypeCode(tiFrom);

				if (stringAutoConvert && tcTo == LuaEmitTypeCode.String && tcFrom != LuaEmitTypeCode.Object)
				{
					match = MemberMatchValue.StringAutoConvert;
					return true;
				}
					
				else if (tcTo is >= LuaEmitTypeCode.SByte and <= LuaEmitTypeCode.Double &&
				         (tcFrom >= LuaEmitTypeCode.SByte && tcFrom <= tcTo ||
				          tcTo == LuaEmitTypeCode.Single &&
				          tcFrom == LuaEmitTypeCode.Double)) // exception for single -> double
				{
					match = MemberMatchValue.NumericImplicitConvert;
					return true;
				}
					
				else if (stringAutoConvert && tcFrom == LuaEmitTypeCode.String && tcTo is >= LuaEmitTypeCode.SByte and <= LuaEmitTypeCode.Double)
				{
					match = MemberMatchValue.StringAutoConvert;
					return true;
				}

				match = MemberMatchValue.None;
				return false;
			}
		} // bool TypesMatch

		private static bool IsArithmeticType(Type type)
			=> IsArithmeticType(GetTypeCode(type));

		private static bool IsArithmeticType(TypeInfo ti)
			=> IsArithmeticType(GetTypeCode(ti));

		internal static bool IsArithmeticType(LuaEmitTypeCode typeCode)
			=> IsIntegerType(typeCode) || IsFloatType(typeCode);

		public static bool IsIntegerType(LuaEmitTypeCode typeCode)
			=> (LuaEmitTypeCode.IntegerFlag & typeCode) != 0;

		private static bool IsFloatType(LuaEmitTypeCode typeCode)
		{
			switch (typeCode)
			{
				case LuaEmitTypeCode.Double:
				case LuaEmitTypeCode.Single:
					return true;
				default:
					return false;
			}
		} // func IsFloatType

		internal static MethodInfo FindConvertOperator(Type fromType, Type toType)
		{
			var implicitMethod = false;
			var isExactFrom = false;
			var isExactTo = false;
			return FindConvertOperator(fromType, toType, null, ref implicitMethod, ref isExactFrom, ref isExactTo);
		} // func FindConvertOperator

		private static MethodInfo FindConvertOperator(Type fromType, Type toType, MethodInfo currentMethodInfo, ref bool implicitMethod, ref bool isExactFrom, ref bool isExactTo)
		{
			foreach (var mi in LuaType.GetType(fromType).EnumerateMembers<MethodInfo>(LuaMethodEnumerate.Static))
			{
				var parameters = mi.GetParameters();

				bool testImplicit;

				// check the number of arguments
				if (parameters.Length != 1)
					continue;

				// test name
				if (mi.IsSpecialName)
				{
					if (mi.Name == csImplicit)
						testImplicit = true;
					else if (mi.Name == csExplicit)
						testImplicit = false;
					else
						continue;
				}
				else
					continue;

				// parameter ergo from
				if (!TypesMatch(parameters[0].ParameterType, fromType, out var testExactFrom))
					continue;
				// return type
				if (!TypesMatch(toType, mi.ReturnType, out var testExactTo))
					continue;

				if (currentMethodInfo == null) // no match until now, take first that fits
				{
					isExactFrom = testExactFrom == MemberMatchValue.Exact;
					isExactTo = testExactTo == MemberMatchValue.Exact;
					implicitMethod = testImplicit;
					currentMethodInfo = mi;
				}
				else if (!isExactTo && testExactTo == MemberMatchValue.Exact) // exactTo is matching -> is most important
				{
					isExactFrom = testExactFrom == MemberMatchValue.Exact;
					isExactTo = true;
					implicitMethod = testImplicit;
					currentMethodInfo = mi;
				}
				else if (testExactTo == MemberMatchValue.Exact) // check only testExactFrom
				{
					if (testExactFrom == MemberMatchValue.Exact) // nice
					{
						if (testImplicit) // perfect
						{
							isExactTo =
								isExactFrom =
								implicitMethod = true;
							currentMethodInfo = mi;
							break;
						}
						else // nearly
						{
							isExactTo =
								isExactFrom = true;
							implicitMethod = false;
							currentMethodInfo = mi;
						}
					}
					else // check if the type code is better
					{
						var tcCurrent = GetTypeCode(currentMethodInfo.GetParameters()[0].ParameterType);
						var tcNew = GetTypeCode(parameters[0].ParameterType);
						if (IsArithmeticType(tcCurrent) && IsArithmeticType(tcNew) && tcCurrent < tcNew)
						{
							isExactFrom = false;
							isExactTo = false;
							implicitMethod = testImplicit;
							currentMethodInfo = mi;
						}
					}
				}
				else if (isExactFrom)
				{
					var tcCurrent = GetTypeCode(currentMethodInfo.ReturnType);
					var tcNew = GetTypeCode(mi.ReturnType);
					if (IsArithmeticType(tcCurrent) && IsArithmeticType(tcNew) && tcCurrent < tcNew)
					{
						isExactFrom = testExactFrom == MemberMatchValue.Exact;
						isExactTo = false;
						implicitMethod = testImplicit;
						currentMethodInfo = mi;
					}
				}
			}
			return currentMethodInfo;
		} // func FindConvertOperator

		private static MethodInfo FindParseMethod(Type toType, out bool withCultureInfo)
		{
			MethodInfo currentMethodInfo = null;
			withCultureInfo = false;

			foreach (var mi in toType.GetRuntimeMethods().Where(c => c.IsPublic && c.IsStatic && c.Name == csParse))
			{
				var parameters = mi.GetParameters();
				if (parameters.Length == 1 && parameters[0].ParameterType == typeof(string))
					currentMethodInfo = mi;
				else if (parameters.Length == 2 && parameters[0].ParameterType == typeof(string) && parameters[1].ParameterType == typeof(IFormatProvider))
				{
					withCultureInfo = true;
					return mi;
				}
			}

			return currentMethodInfo;
		} // func FindParseMethod

		private static string GetOperationMethodName(ExpressionType op)
		{
			switch (op)
			{
				case ExpressionType.Add:
					return "op_Addition";
				case ExpressionType.Subtract:
					return "op_Subtraction";
				case ExpressionType.Multiply:
					return "op_Multiply";
				case ExpressionType.Divide:
				case Lua.IntegerDivide:
					return "op_Division";
				case ExpressionType.Modulo:
					return "op_Modulus";

				case ExpressionType.Negate:
					return "op_UnaryNegation";
				case ExpressionType.OnesComplement:
					return "op_OnesComplement";

				case ExpressionType.And:
					return "op_BitwiseAnd";
				case ExpressionType.Or:
					return "op_BitwiseOr";
				case ExpressionType.ExclusiveOr:
					return "op_ExclusiveOr";

				case ExpressionType.RightShift:
					return "op_RightShift";
				case ExpressionType.LeftShift:
					return "op_LeftShift";

				case ExpressionType.GreaterThan:
					return "op_GreaterThan";
				case ExpressionType.GreaterThanOrEqual:
					return "op_GreaterThanOrEqual";
				case ExpressionType.LessThan:
					return "op_LessThan";
				case ExpressionType.LessThanOrEqual:
					return "op_LessThanOrEqual";
				case ExpressionType.Equal:
					return "op_Equality";
				case ExpressionType.NotEqual:
					return "op_Inequality";

				default:
					return null;
			}
		} // func GetOperationMethodName

		internal static Expression[] CreateDynamicArgs<TARG>(Expression instance, Type instanceType, TARG[] arguments, Func<TARG, Expression> getExpr, Func<TARG, Type> getType)
			where TARG : class
		{
			var dynArgs = new Expression[arguments.Length + 1];
			dynArgs[0] = Lua.EnsureType(instance, typeof(object));
			for (var i = 0; i < arguments.Length; i++)
				dynArgs[i + 1] = Convert(getExpr(arguments[i]), getType(arguments[i]), typeof(object), null); // should not generate a exception
			return dynArgs;
		} // func CreateDynamicArgs

		internal static Expression[] CreateDynamicArgs<TARG>(Expression instance, Type instanceType, TARG[] arguments, TARG setTo, Func<TARG, Expression> getExpr, Func<TARG, Type> getType)
			where TARG : class
		{
			var dynArgs = new Expression[arguments.Length + 2];
			dynArgs[0] = Lua.EnsureType(instance, typeof(object));
			for (var i = 0; i < arguments.Length; i++)
				dynArgs[i + 1] = Convert(getExpr(arguments[i]), getType(arguments[i]), typeof(object), null); // should not generate a exception
			dynArgs[dynArgs.Length - 1] = Convert(getExpr(setTo), getType(setTo), typeof(object), null); // should not generate a exception
			return dynArgs;
		} // func CreateDynamicArgs

		private static Expression ParseNumberExpression(Expression expr, Type type, Type toType)
			=> TryParseNumberExpression(expr, type, toType, out var result)
				? (Expression)result
				: throw (LuaEmitException)result;

		private static bool TryParseNumberExpression(Expression expr, Type type, Type toType, out object result)
		{
			if (!TryConvert(expr, type, typeof(string), null, out var arg0))
			{
				result = arg0;
				return false;
			}

			result = Lua.EnsureType(
				toType == typeof(object)
					? Expression.Call(Lua.ParseNumberObjectMethodInfo, (Expression)arg0)
					: Expression.Call(Lua.ParseNumberTypedMethodInfo, (Expression)arg0, Expression.Constant(toType))
				,
				toType
			);

			return true;
		} // func TryParseNumberExpression

		#endregion

		#region -- Emit Convert -------------------------------------------------------

		#region -- TryConvertCore -----------------------------------------------------

		private static bool TryConvertCore(Expression expr, Type toType, Func<Type, ConvertBinder> getDynamicConvertBinder, out object result)
		{
			var fromType = expr.Type;

			// -- convert lua/dlr rules --
			if (fromType == toType)
			{
				result = expr;
				return true;
			}
			else if (fromType == typeof(LuaResult)) // LuaResult -> convert first value
			{
				return TryConvertCore(GetResultExpression(expr, 0), toType, getDynamicConvertBinder, out result);
			}
			else if (toType == typeof(LuaResult)) // type to LuaResult
			{
				result = Expression.New(Lua.ResultConstructorInfoArg1, Lua.EnsureType(expr, typeof(object)));
				return true;
			}
			else if (getDynamicConvertBinder != null && IsDynamicType(fromType)) // dynamic type -> dynamic convert
			{
				result = DynamicExpression.Dynamic(getDynamicConvertBinder(toType), toType, Lua.EnsureType(expr, typeof(object)));
				return true;
			}

			// -- special rules --
			var fromTypeInfo = fromType.GetTypeInfo();
			var toTypeInfo = toType.GetTypeInfo();
			if (toType == typeof(object) || toTypeInfo.IsAssignableFrom(fromTypeInfo)) // Type is assignable
			{
				result = Expression.Convert(expr, toType);
				return true;
			}
			else if (toType == typeof(bool)) // we need a true or false
			{
				if (fromType.GetTypeInfo().IsValueType)
				{
					result = Expression.Constant(true);
					return true;
				}
				else
				{
					result = Expression.NotEqual(Lua.EnsureType(expr, fromType), Expression.Constant(null, fromType)); // todo: call BinaryOperation?
					return true;
				}
			}
			else if (toType == typeof(string)) // convert to a string
			{
				if (fromType == typeof(bool))
				{
					result = Expression.Condition(expr, Expression.Constant("true"), Expression.Constant("false"));
					return true;
				}
				else
				{
					// try find a conversion (implicit or explicit)
					if (TryConvertWithOperator(expr, fromType, toType, getDynamicConvertBinder, out result))
						return true;

					// just call to string or specialized to string
					result = Expression.Call(Lua.RtConvertToStringMethodInfo,
						Lua.EnsureType(expr, typeof(object))
					);
					return true;
				}
			}
			else if (fromType == typeof(string))
			{
				if (IsArithmeticType(toType) && !toTypeInfo.IsEnum) // we expect a string and have a number
				{
					return TryParseNumberExpression(expr, fromType, toType, out result);
				}
				else if (toType == typeof(char)) // char
				{
					result = Expression.Property(Lua.EnsureType(expr, fromType), Lua.StringItemPropertyInfo, Expression.Constant(0)); // todo: fix Length == 0?
					return true;
				}
				else if (toTypeInfo.IsEnum)
				{
					result = Expression.Call(Lua.EnumParseMethodInfo,
						Expression.Constant(toType),
						expr
					);
					return true;
				}
				else // find parse method,
				{
					var methodInfo = FindParseMethod(toType, out var withCultureInfo);

					result = withCultureInfo ?
						(Expression)Expression.Call(methodInfo, expr, Expression.Property(null, Lua.CultureInvariantPropertyInfo)) :
						(Expression)Expression.Convert(expr, toType, methodInfo);
					return true;
				}
				// fallback to default
			}
			else if (toTypeInfo.BaseType == typeof(MulticastDelegate) && toTypeInfo.BaseType == fromTypeInfo.BaseType)
			{
				result = Expression.Convert(
					Expression.Call(Lua.ConvertDelegateMethodInfo,
						Expression.Constant(toType, typeof(Type)),
						Expression.Convert(expr, typeof(Delegate))
					),
					toType
				);
				return true;
			}
			else if (fromType.IsArray && toType.IsArray)
			{
				result = Expression.Convert(
					Expression.Call(Lua.ConvertArrayMethodInfo,
						Expression.Convert(expr, typeof(Array)),
						Expression.Constant(toType.GetElementType())
					),
					toType
				);
				return true;
			}

			// -- default fallback --
			if (TryConvertWithOperator(expr, fromType, toType, getDynamicConvertBinder, out result))
				return true;

			try
			{
				result = Expression.Convert(expr, toType);
				return true;
			}
			catch
			{
				result = new LuaEmitException(LuaEmitException.ConversationNotDefined, fromType.Name, toType.Name);
				return false;
			}
		} // func TryConvertCore

		#endregion

		public static Expression ConvertToSingleResultExpression(Expression expr, Type fromType, Type toType, Func<Type, ConvertBinder> getDynamicConvertBinder)
		{
			// correct to the limit type
			if (fromType == null)
				fromType = expr.Type;
			else if (expr.Type != fromType)
				expr = Expression.Convert(expr, fromType);

			if (getDynamicConvertBinder != null)
			{
				if (expr.Type == typeof(LuaResult) && toType != typeof(LuaResult)) // shortcut for LuaResult ==> expr[0]
				{
					if (expr.NodeType == ExpressionType.New) // new LuaResult(?)
					{
						var newExpression = (NewExpression)expr;
						if (newExpression.Constructor == Lua.ResultConstructorInfoArg1 || newExpression.Constructor == Lua.ResultConstructorInfoArgN)
							return ConvertToSingleResultExpression(newExpression.Arguments.First(), null, toType, getDynamicConvertBinder);
					}
					else if (expr.NodeType == ExpressionType.Dynamic) // (LuaResult)?
					{
						var dynamicExpression = (DynamicExpression)expr;
						if (dynamicExpression.Binder is ConvertBinder)
							return ConvertToSingleResultExpression(DynamicExpression.Dynamic(getDynamicConvertBinder(toType), toType, dynamicExpression.Arguments.First()), null, toType, getDynamicConvertBinder);
					}

					return GetResultExpression(expr, 0); // is forced by default
				}
				else if (expr.Type == typeof(object) && expr.NodeType == ExpressionType.Dynamic) // wrap dynamic Invokes
				{
					var exprDynamic = (DynamicExpression)expr;
					if (exprDynamic.Binder is InvokeBinder || exprDynamic.Binder is InvokeMemberBinder) // convert the result of a invoke to object
						return ConvertToSingleResultExpression(DynamicExpression.Dynamic(getDynamicConvertBinder(toType), toType, expr), null, toType, getDynamicConvertBinder);
					else if (exprDynamic.Binder is ConvertBinder && exprDynamic.Type != toType)
						return ConvertToSingleResultExpression(DynamicExpression.Dynamic(getDynamicConvertBinder(toType), toType, exprDynamic.Arguments.First()), null, toType, getDynamicConvertBinder);

					// fall to forceType
				}
			}

			return expr;
		} // func ConvertToSingleResultExpression

		public static bool TryConvert(Expression expr, Type fromType, Type toType, Func<Type, ConvertBinder> getDynamicConvertBinder, out object result)
			=> TryConvertCore(ConvertToSingleResultExpression(expr, fromType, toType, getDynamicConvertBinder), toType, getDynamicConvertBinder, out result);

		public static Expression Convert(Expression expr, Type fromType, Type toType, Func<Type, ConvertBinder> getDynamicConvertBinder)
			=> TryConvertCore(ConvertToSingleResultExpression(expr, fromType, toType, getDynamicConvertBinder), toType, getDynamicConvertBinder, out var result)
				? (Expression)result
				: throw (LuaEmitException) result;

		public static Expression ConvertWithRuntime(Lua lua, Expression expr, Type fromType, Type toType)
		{
			var getDynamicConvertBinder = lua == null ? null : new Func<Type, ConvertBinder>(lua.GetConvertBinder);
			return TryConvertCore(ConvertToSingleResultExpression(expr, fromType, toType, getDynamicConvertBinder), toType, getDynamicConvertBinder, out var result)
				? (Expression)result
				: throw (LuaEmitException)result;
		} // func ConvertWithRuntime

		private static bool TryConvertWithOperator(Expression target, Type targetType, Type toType, Func<Type, ConvertBinder> getDynamicConvertBinder, out object result)
		{
			var convertTo = FindConvertOperator(targetType, toType);
			if (convertTo != null)
			{
				// convert the parameter
				var argParameterType = convertTo.GetParameters()[0].ParameterType;
				if (!TryConvertCore(Lua.EnsureType(target, targetType), argParameterType, getDynamicConvertBinder, out result))
					return false;

				// convert
				var expr = Expression.Convert((Expression)result, convertTo.ReturnType, convertTo);
				return TryConvertCore(expr, toType, getDynamicConvertBinder, out result);
			}
			result = null;
			return false;
		} // func TryConvertWithOperator

		private static bool TryConvertWithRuntime(Lua lua, ref Expression expr, ref Type fromType, Type toType)
		{
			var getDynamicConvertBinder = lua == null ? null : new Func<Type, ConvertBinder>(lua.GetConvertBinder);
			if (TryConvertCore(ConvertToSingleResultExpression(expr, fromType, toType, getDynamicConvertBinder), toType, getDynamicConvertBinder, out var result))
			{
				expr = (Expression)result;
				fromType = toType;
				return true;
			}
			else
				return false;
		} // func TryConvertWithRuntime

		public static Expression GetResultExpression(Expression target, int index)
		{
			return Expression.MakeIndex(
				Lua.EnsureType(target, typeof(LuaResult)),
				Lua.ResultIndexPropertyInfo,
				new Expression[] { Expression.Constant(index) }
			);
		} // func GetResultExpression

		public static Expression GetResultExpression(Lua lua, Expression target, int index, Type returnType, Expression defaultReturn)
		{
			var getExpression = GetResultExpression(target, index);

			// ?? operator
			if (defaultReturn != null)
				getExpression = Expression.Coalesce(getExpression, defaultReturn);

			// convert to return type
			return ConvertWithRuntime(lua, getExpression, getExpression.Type, returnType);
		} // func GetResultExpression

		#endregion

		#region -- Emit Unary Operation -----------------------------------------------

		public static Expression UnaryOperationExpression(Lua lua, ExpressionType op, Expression expr, Type type, bool forParse)
		{
			if (op == ExpressionType.Not)
			{
				return Expression.Not(ConvertWithRuntime(lua, expr, type, typeof(bool)));
			}
			else if (op == ExpressionType.ArrayLength)
			{
				return type.IsArray 
					? (Expression)Expression.ArrayLength(Lua.EnsureType(expr, type)) 
					: Expression.Call(Lua.RuntimeLengthMethodInfo, Lua.EnsureType(expr, typeof(object)));
			}
			else if (forParse && IsDynamicType(type))
				return DynamicExpression.Dynamic(lua.GetUnaryOperationBinary(op), typeof(object), ConvertWithRuntime(lua, expr, type, typeof(object)));
			else
			{
				switch (op)
				{
					case ExpressionType.OnesComplement:
						return UnaryOperationComplementExpression(lua, expr, type);
					case ExpressionType.Negate:
						return UnaryOperationArithmeticExpression(lua, op, expr, type);
					default:
						return Expression.MakeUnary(op, Lua.EnsureType(expr, type), type);
				}
			}
		} // func UnaryOperationExpression

		#endregion

		#region -- Emit Binary Operation ----------------------------------------------

		public static Expression BinaryOperationExpression(Lua lua, ExpressionType op, Expression expr1, Type type1, Expression expr2, Type type2, bool forParse)
		{
			if (op == ExpressionType.OrElse || op == ExpressionType.AndAlso) // and, or are conditions no operations
				return BinaryOperationConditionExpression(lua, op, expr1, type1, expr2, type2);
			else if (forParse && (IsDynamicType(type1) || IsDynamicType(type2))) // is one of the type a dynamic type, than make a dynamic expression
				return BinaryOperationDynamicExpression(lua, op, expr1, type1, expr2, type2);
			else if (op == ExpressionType.Power)
			{
				if (!TryConvertWithRuntime(lua, ref expr1, ref type1, typeof(double)))
					throw new LuaEmitException(LuaEmitException.ConversationNotDefined, type1.Name, typeof(double).Name);
				else if (!TryConvertWithRuntime(lua, ref expr2, ref type2, typeof(double)))
					throw new LuaEmitException(LuaEmitException.ConversationNotDefined, type2.Name, typeof(double).Name);
				else
					return Expression.MakeBinary(op, expr1, expr2);
			}
			else
				return BinaryOperationArithmeticExpression(lua, op, expr1, type1, expr2, type2, type1, type2);
		} // func BinaryOperationExpression

		#endregion

		#region -- BinaryOperationDynamicExpression -----------------------------------

		private static Expression BinaryOperationDynamicExpression(Lua lua, ExpressionType op, Expression expr1, Type type1, Expression expr2, Type type2)
		{
			return DynamicExpression.Dynamic(lua.GetBinaryOperationBinder(op), typeof(object),
				ConvertWithRuntime(lua, expr1, type1, typeof(object)),
				ConvertWithRuntime(lua, expr2, type2, typeof(object))
			);
		} // func BinaryOperationDynamicExpression

		#endregion

		#region -- Emit Binary Condition Operator -------------------------------------

		private static Expression BinaryOperationConditionExpression(Lua lua, ExpressionType op, Expression expr1, Type type1, Expression expr2, Type type2)
		{
			Type typeOp;
			if (type1 == type2)
				typeOp = type1;
			else if (TypesMatch(type1, type2, out var match) && match == MemberMatchValue.Exact)
				typeOp = type1;
			else if (TypesMatch(type2, type1, out match) && match == MemberMatchValue.Exact)
				typeOp = type2;
			else
				typeOp = typeof(object);

			// create condition
			var typeVariable = expr1.Type;
			if (typeVariable == typeof(LuaResult))
				typeVariable = typeof(object);

			var exprTmp = Expression.Variable(typeVariable, "#tmp");
			Expression exprCondition;

			// Create a condition to follow lua language rules
			if (op == ExpressionType.AndAlso)
			{
				exprCondition = Expression.Condition(
					ConvertWithRuntime(lua, exprTmp, exprTmp.Type, typeof(bool)),
					ConvertWithRuntime(lua, expr2, type2, typeOp),
					ConvertWithRuntime(lua, exprTmp, exprTmp.Type, typeOp)
				);
			}
			else if (op == ExpressionType.OrElse)
			{
				exprCondition = Expression.Condition(
					ConvertWithRuntime(lua, exprTmp, exprTmp.Type, typeof(bool)),
					ConvertWithRuntime(lua, exprTmp, exprTmp.Type, typeOp),
					ConvertWithRuntime(lua, expr2, type2, typeOp)
				);
			}
			else
				throw new InvalidOperationException();

			return Expression.Block(typeOp,
				new ParameterExpression[] { exprTmp },
				Expression.Assign(exprTmp, ConvertWithRuntime(lua, expr1, expr1.Type, exprTmp.Type)),
				exprCondition
			);
		} // func BinaryOperationConditionExpression

		#endregion

		#region -- Emit Binary Compare Equalable helper -------------------------------

		private static bool TestParameter(ParameterInfo[] parameters, params Type[] args)
		{
			if (parameters.Length != args.Length)
				return false;

			for (var i = 0; i < parameters.Length; i++)
			{
				if (parameters[i].ParameterType != args[i])
					return false;
			}

			return true;
		} // func TestParameter

		private static Expression BinaryOperationCompareToExpression(Lua lua, Type compareInterface, ExpressionType op, Expression expr1, Type type1, Expression expr2, Type type2)
		{
			var compareMethodInfo = (
				from mi in compareInterface.GetTypeInfo().DeclaredMethods
				where mi.IsPublic && !mi.IsStatic && mi.Name == "CompareTo" && mi.ReturnType == typeof(int)
				select mi
			).FirstOrDefault();

			return Expression.MakeBinary(op,
				Expression.Call(
					ConvertWithRuntime(null, expr1, type1, compareInterface),
					compareMethodInfo,
					ConvertWithRuntime(lua, expr2, type2, compareMethodInfo.GetParameters()[0].ParameterType)
				),
				Expression.Constant(0, typeof(int))
			);
		} // func BinaryOperationCompareToExpression

		private static Expression BinaryOperationEqualableToExpression(Lua lua, Type equalableInterface, ExpressionType op, Expression expr1, Type type1, Expression expr2, Type type2)
		{
			var equalableInterfaceTypeInfo = equalableInterface.GetTypeInfo();
			var typeParam = equalableInterfaceTypeInfo.GenericTypeArguments[0];

			var equalsMethodInfo = (
				from mi in equalableInterfaceTypeInfo.DeclaredMethods
				where mi.IsPublic && !mi.IsStatic && mi.Name == "Equals" && TestParameter(mi.GetParameters(), typeParam)
				select mi
			).FirstOrDefault();

			Expression expr = Expression.Call(
				ConvertWithRuntime(null, expr1, type1, equalableInterface),
				equalsMethodInfo,
				ConvertWithRuntime(lua, expr2, type2, typeParam)
			);
			return op == ExpressionType.NotEqual ? Expression.Not(expr) : expr;
		} // func BinaryOperationCompareToExpression

		private static Type GetComparableInterface(Type type1, Type type2, ref bool isExact)
		{
			Type compareInterface = null;
			foreach (var typeTest in type1.GetTypeInfo().ImplementedInterfaces)
			{
				if (compareInterface == null && typeTest == typeof(IComparable) &&
				    TypesMatch(type1, type2, out var match, false))
				{
					isExact = match == MemberMatchValue.Exact;
					return typeTest;
				}
					
				else if (!isExact && IsGenericCompare(typeTest))
				{
					var p = typeTest.GenericTypeArguments[0];
					if (TypesMatch(p, type2, out match, false))
					{
						isExact = match == MemberMatchValue.Exact;
						compareInterface = typeTest;
						if (isExact)
							break;
					}
				}
			}
			return compareInterface;
		} // func GetComparableInterface

		private static bool IsGenericCompare(Type type)
			=> type.IsConstructedGenericType && type.GetGenericTypeDefinition() == typeof(IComparable<>);

		private static Type GetEqualableInterface(Type type1, Type Type2, ref bool isExact)
		{
			Type equalableInterface = null;
			foreach (var typeTest in type1.GetTypeInfo().ImplementedInterfaces)
			{
				if (!isExact && typeTest.IsConstructedGenericType && typeTest.GetGenericTypeDefinition() == typeof(IEquatable<>))
				{
					var p = typeTest.GenericTypeArguments[0];
					if (TypesMatch(p, Type2, out var match, false))
					{
						isExact = match == MemberMatchValue.Exact;
						equalableInterface = typeTest;
						if (isExact)
							break;
					}
				}
			}
			return equalableInterface;
		} // func GetEqualableInterface

		#endregion

		#region -- Emit Arithmetic Expression -----------------------------------------

		private static Expression UnaryOperationComplementExpression(Lua lua, Expression expr, Type type)
		{
			var tc = GetTypeCode(type);
			var isArithmetic = IsArithmeticType(tc);

			if (isArithmetic) // simple arithmetic complement
			{
				#region -- simple arithmetic --
				Type typeOp = type;
				Type typeEnum = null;
				if (type.GetTypeInfo().IsEnum)
				{
					typeEnum = type;
					typeOp = Enum.GetUnderlyingType(type);
				}

				switch (tc)
				{
					case LuaEmitTypeCode.Double:
						typeOp = typeof(long);
						break;
					case LuaEmitTypeCode.Single:
						typeOp = typeof(int);
						break;
				}

				expr = Expression.OnesComplement(ConvertWithRuntime(lua, expr, type, typeOp));

				if (typeEnum != null)
					expr = Expression.Convert(expr, typeEnum);

				return expr;
				#endregion
			}

			#region -- find operator --

			var operatorMethodInfo = FindMethod(
					LuaType.GetType(type).EnumerateMembers<MethodInfo>(LuaMethodEnumerate.Static, GetOperationMethodName(ExpressionType.OnesComplement), false),
					new CallInfo(1),
					null,
					new Type[] { type },
					t => t, false
			);
			if (operatorMethodInfo != null)
				return Expression.OnesComplement(ConvertWithRuntime(lua, expr, type, operatorMethodInfo.GetParameters()[0].ParameterType), operatorMethodInfo);

			#endregion

			#region -- inject convert --

			if (lua != null && type == typeof(string))
				return DynamicExpression.Dynamic(lua.GetUnaryOperationBinary(ExpressionType.OnesComplement), typeof(object), ParseNumberExpression(expr, type, typeof(object)));

			#endregion

			#region -- try convert to integer --

			if (TryConvertWithRuntime(lua, ref expr, ref type, LiftIntegerType(lua, type)))
				return UnaryOperationComplementExpression(lua, expr, type);

			#endregion

			throw new LuaEmitException(LuaEmitException.OperatorNotDefined, ExpressionType.OnesComplement, String.Empty, type.Name);
		} // func UnaryOperationComplementExpression

		private static Expression UnaryOperationNegateExpression(Lua lua, Expression expr, Type type)
		{
			var tc = GetTypeCode(type);
			var isArithmetic = IsArithmeticType(tc);

			if (isArithmetic) // simple arithmetic complement
			{
				#region -- simple arithmetic --
				Type typeOp = type;
				Type typeEnum = null;
				if (type.GetTypeInfo().IsEnum)
				{
					typeEnum = type;
					typeOp = Enum.GetUnderlyingType(type);
					tc = GetTypeCode(typeOp);
				}

				expr = Expression.OnesComplement(ConvertWithRuntime(lua, expr, type, LiftTypeSigned(tc, tc)));

				if (typeEnum != null)
					expr = Expression.Convert(expr, typeEnum);

				return expr;
				#endregion
			}

			#region -- find operator --

			var operatorMethodInfo = FindMethod(
					LuaType.GetType(type).EnumerateMembers<MethodInfo>(LuaMethodEnumerate.Static, GetOperationMethodName(ExpressionType.Negate), false),
					new CallInfo(1),
					null,
					new Type[] { type },
					t => t, false
			);
			if (operatorMethodInfo != null)
				return Expression.Negate(ConvertWithRuntime(lua, expr, type, operatorMethodInfo.GetParameters()[0].ParameterType), operatorMethodInfo);

			#endregion

			#region -- inject convert --

			if (lua != null && type == typeof(string))
				return DynamicExpression.Dynamic(lua.GetUnaryOperationBinary(ExpressionType.Negate), typeof(object), ParseNumberExpression(expr, type, typeof(object)));

			#endregion

			#region -- try convert to integer --

			if (TryConvertWithRuntime(lua, ref expr, ref type, LiftIntegerType(lua, type)))
				return UnaryOperationNegateExpression(lua, expr, type);

			#endregion

			throw new LuaEmitException(LuaEmitException.OperatorNotDefined, ExpressionType.Negate, String.Empty, type.Name);
		} // func UnaryOperationNegateExpression

		private static Expression UnaryOperationArithmeticExpression(Lua lua, ExpressionType op, Expression expr, Type type)
		{
			var isArithmetic = IsArithmeticType(type);
			if (isArithmetic)
			{
				#region -- simple arithmetic --

				Type typeEnum = null;
				if (type.GetTypeInfo().IsEnum)
				{
					typeEnum = type; // save enum
					type = Enum.GetUnderlyingType(type);
				}

				if (op == ExpressionType.OnesComplement)
				{
					expr = ConvertWithRuntime(lua, expr, type, LiftIntegerType(lua, type));
					type = expr.Type;
				}
				else if (op == ExpressionType.Negate)
				{
					var tc = GetTypeCode(type);
					switch (tc)
					{
						case LuaEmitTypeCode.Byte:
							expr = ConvertWithRuntime(lua, expr, type, typeof(short));
							type = expr.Type;
							break;
						case LuaEmitTypeCode.UInt16:
							expr = ConvertWithRuntime(lua, expr, type, typeof(int));
							type = expr.Type;
							break;
						case LuaEmitTypeCode.UInt32:
							expr = ConvertWithRuntime(lua, expr, type, typeof(long));
							type = expr.Type;
							break;
						case LuaEmitTypeCode.UInt64:
							expr = ConvertWithRuntime(lua, expr, type, typeof(double));
							type = expr.Type;
							break;
					}
				}

				expr = Expression.MakeUnary(op, Lua.EnsureType(expr, type), type);

				// convert to enum
				if (typeEnum != null)
					expr = Expression.Convert(expr, typeEnum);

				return expr;
				#endregion
			}

			MethodInfo operatorMethodInfo = null;
			var methodName = GetOperationMethodName(op);
#if DEBUG
			if (methodName == null)
				throw new InvalidOperationException(String.Format("Method for Operator {0} not defined.", op));
#endif

			#region -- find operator --

			// try to find a exact match for the operation
			operatorMethodInfo = FindMethod(LuaType.GetType(type).EnumerateMembers<MethodInfo>(LuaMethodEnumerate.Static, methodName, false), new CallInfo(1), null, new Type[] { type }, t => t, false);

			// can we inject a string conversation --> create a dynamic operation, that results in a simple arithmetic operation
			if (lua != null && operatorMethodInfo == null && type == typeof(string))
			{
				#region -- string inject for arithmetic --

				expr = ParseNumberExpression(expr, type, typeof(object));
				type = typeof(object);

				return DynamicExpression.Dynamic(lua.GetUnaryOperationBinary(op), typeof(object), expr);

				#endregion
			}

			// try convert the type to an arithmetic type
			if (operatorMethodInfo == null)
			{
				if (op == ExpressionType.OnesComplement && TryConvertWithRuntime(lua, ref expr, ref type, LiftIntegerType(lua, type)))
					return UnaryOperationArithmeticExpression(lua, op, expr, type);
				else if (op == ExpressionType.Negate)
				{
					// is there a integer conversion
					var implicitMethod = false;
					var isExactFrom = false;
					var isExactTo = false;
					var typeInt = LiftIntegerType(lua, type);
					var convertMethodInfo = FindConvertOperator(type, typeInt, null, ref implicitMethod, ref isExactFrom, ref isExactTo);
					if (isExactTo)
					{
						if (expr.Type != type)
							expr = Expression.Convert(expr, type);
						return UnaryOperationArithmeticExpression(lua, op, Expression.Convert(expr, typeInt), typeInt);
					}
					else if (TryConvertWithRuntime(lua, ref expr, ref type, lua == null ? typeof(double) : Lua.GetFloatType(lua.NumberType)))
						return UnaryOperationArithmeticExpression(lua, op, expr, type);
				}
			}

			#endregion

			if (operatorMethodInfo != null)
			{
				return Expression.MakeUnary(op,
					ConvertWithRuntime(lua, expr, type, operatorMethodInfo.GetParameters()[0].ParameterType),
					null,
					operatorMethodInfo
				);
			}
			else
				throw new LuaEmitException(LuaEmitException.OperatorNotDefined, op, String.Empty, type.Name);
		} // func UnaryOperationArithmeticExpression

		private static Expression BinaryOperationArithmeticExpression(Lua lua, ExpressionType op, Expression expr1, Type type1, Expression expr2, Type type2, Type type1org, Type type2org)
		{
			var liftAllowed = true;
			var tc1 = GetTypeCode(type1);
			var tc2 = GetTypeCode(type2);
			var isArithmetic1 = IsArithmeticType(tc1);
			var isArithmetic2 = IsArithmeticType(tc2);

			if (isArithmetic1 && isArithmetic2) // both are arithmetic --> simple arithmetic operation
			{
				Type typeOp;
				var shift = false;

				#region -- Get the type for the operation --

				switch (op)
				{
					case ExpressionType.And:
					case ExpressionType.ExclusiveOr:
					case ExpressionType.Or:
						// both type should the same
						typeOp = LiftIntegerType(lua, LiftType(type1, tc1, type2, tc2, false));
						break;

					case ExpressionType.LeftShift:
					case ExpressionType.RightShift:
						// the right one must be a interger
						typeOp = LiftIntegerType(lua, type1);
						shift = true;
						break;

					case ExpressionType.Add:
					case ExpressionType.Subtract:
					case ExpressionType.Multiply:
					case ExpressionType.Modulo:
						// both types must be the same
						typeOp = LiftType(type1, tc1, type2, tc2, false);
						switch (GetTypeCode(typeOp))
						{
							case LuaEmitTypeCode.SByte:
							case LuaEmitTypeCode.Byte:
								typeOp = LiftTypeNext(lua, typeOp);
								break;
						}
						break;

					case ExpressionType.Divide:
						// both types must be a float type
						if (type1 == typeof(double) || type2 == typeof(double))
							typeOp = typeof(double);
						else if (type1 == typeof(float) || type2 == typeof(float))
							typeOp = typeof(float);
						else if (lua == null)
							typeOp = typeof(double);
						else
							typeOp = Lua.GetFloatType(lua.NumberType);
						break;

					case Lua.IntegerDivide:
						// both must be a integer
						op = ExpressionType.Divide;
						typeOp = LiftIntegerType(lua, LiftType(type1, tc1, type2, tc2, false));
						switch (GetTypeCode(typeOp))
						{
							case LuaEmitTypeCode.SByte:
							case LuaEmitTypeCode.Byte:
								typeOp = LiftTypeNext(lua, typeOp);
								break;
						}
						op = ExpressionType.Divide;
						break;

					case ExpressionType.Equal:
					case ExpressionType.NotEqual:
					case ExpressionType.LessThan:
					case ExpressionType.LessThanOrEqual:
					case ExpressionType.GreaterThan:
					case ExpressionType.GreaterThanOrEqual:
						typeOp = LiftType(type1, tc1, type2, tc2, false);
						break;

					default:
						throw new InvalidOperationException("no typeOp");
				}

				#endregion

				#region -- simple enum safe operation --

				Type typeEnum = null;
				if (typeOp.GetTypeInfo().IsEnum)
				{
					if (type1.GetTypeInfo().IsEnum && type2.GetTypeInfo().IsEnum &&
							op != ExpressionType.Equal &&
							op != ExpressionType.NotEqual &&
							op != ExpressionType.LessThan &&
							op != ExpressionType.LessThanOrEqual &&
							op != ExpressionType.GreaterThan &&
							op != ExpressionType.GreaterThanOrEqual)
					{
						typeEnum = typeOp; // save enum
					}
					typeOp = Enum.GetUnderlyingType(typeOp);
				}

				Expression expr = Expression.MakeBinary(op,
					ConvertWithRuntime(lua, expr1, type1, typeOp),
					ConvertWithRuntime(lua, expr2, type2, shift ? typeof(int) : typeOp)
				);

				// convert to enum
				if (typeEnum != null)
					expr = Expression.Convert(expr, typeEnum);

				return expr;

				#endregion
			}

			#region -- Find the the binary operator --

			var operationName = GetOperationMethodName(op);
			if (!String.IsNullOrEmpty(operationName))
			{
				// create a list of all operators
				var members = LuaType.GetType(type1).EnumerateMembers<MethodInfo>(LuaMethodEnumerate.Static, operationName, false);
				var operatorMethodInfo = FindMethod(members, new CallInfo(2), null, new Type[] { type1, type2 }, t => t, false);

				if (operatorMethodInfo != null)
				{
					// Get the argumentslist
					var parameters = operatorMethodInfo.GetParameters();
					if (op == Lua.IntegerDivide)
						op = ExpressionType.Divide;

					if ((op == ExpressionType.Equal || op == ExpressionType.NotEqual) &&
						!type1.GetTypeInfo().IsClass && type2.GetTypeInfo().IsClass)
					{
						// no convert, because of the default value
					}
					else
					{
						// Check if the arguments are valid
						var exprOperatorArgument1 = expr1;
						var typeOperatorArgument1 = type1;
						var exprOperatorArgument2 = expr2;
						var typeOperatorArgument2 = type2;

						if (TryConvertWithRuntime(lua, ref exprOperatorArgument1, ref typeOperatorArgument1, parameters[0].ParameterType) &&
							TryConvertWithRuntime(lua, ref exprOperatorArgument2, ref typeOperatorArgument2, parameters[1].ParameterType))
						{
							return Expression.MakeBinary(op,
								exprOperatorArgument1,
								exprOperatorArgument2,
								true,
								operatorMethodInfo
							);
						}
					}
				}
			}

			#endregion

			#region -- Is it allowed to convert to an arithmetic type --

			switch (op)
			{
				case ExpressionType.And:
				case ExpressionType.ExclusiveOr:
				case ExpressionType.Or:

				case ExpressionType.LeftShift:
				case ExpressionType.RightShift:

				case ExpressionType.Add:
				case ExpressionType.Subtract:
				case ExpressionType.Multiply:
				case ExpressionType.Divide:
				case Lua.IntegerDivide:
				case ExpressionType.Modulo:
					if (lua != null && isArithmetic1 && type2 == typeof(string))
					{
						return DynamicExpression.Dynamic(lua.GetBinaryOperationBinder(op), typeof(object),
							Convert(expr1, type1, typeof(object), lua.GetConvertBinder),
							ParseNumberExpression(expr2, type2, typeof(object))
						);
					}
					else if (lua != null && type1 == typeof(string) && isArithmetic2)
					{
						return DynamicExpression.Dynamic(lua.GetBinaryOperationBinder(op), typeof(object),
							ParseNumberExpression(expr1, type1, typeof(object)),
							Convert(expr2, type2, typeof(object), lua.GetConvertBinder)
						);
					}
					else if (lua != null && type1 == typeof(string) && type2 == typeof(string))
					{
						return DynamicExpression.Dynamic(lua.GetBinaryOperationBinder(op), typeof(object),
							ParseNumberExpression(expr1, type1, typeof(object)),
							ParseNumberExpression(expr2, type2, typeof(object))
						);
					}
					break;
			}

			#endregion

			#region -- IComparable interface --

			switch (op)
			{
				case ExpressionType.Equal:
				case ExpressionType.NotEqual:
				case ExpressionType.LessThan:
				case ExpressionType.LessThanOrEqual:
				case ExpressionType.GreaterThan:
				case ExpressionType.GreaterThanOrEqual:
					{
						var isExact = false;
						var compareInterface = GetComparableInterface(type1, type2, ref isExact);
						if (!isExact)
						{
							var isExact2 = false;
							var compareInterface2 = GetComparableInterface(type2, type1, ref isExact2);
							if (isExact2)
							{
								switch (op)
								{
									case ExpressionType.LessThan:
										op = ExpressionType.GreaterThanOrEqual;
										break;
									case ExpressionType.LessThanOrEqual:
										op = ExpressionType.GreaterThan;
										break;
									case ExpressionType.GreaterThan:
										op = ExpressionType.LessThanOrEqual;
										break;
									case ExpressionType.GreaterThanOrEqual:
										op = ExpressionType.LessThan;
										break;
								}
								return BinaryOperationCompareToExpression(lua, compareInterface2, op, expr2, type2, expr1, type1);
							}
						}
						if (compareInterface != null)
							return BinaryOperationCompareToExpression(lua, compareInterface, op, expr1, type1, expr2, type2);

						// ignore lift for string <-> number converts
						if (tc1 == LuaEmitTypeCode.String && tc2 >= LuaEmitTypeCode.SByte && tc2 <= LuaEmitTypeCode.Double)
							liftAllowed = false;
						else if (tc2 == LuaEmitTypeCode.String && tc1 != LuaEmitTypeCode.Object)
							liftAllowed = false;

					}
					break;
			}

			#endregion

			#region -- IEquatable interface or Object.Equal --

			switch (op)
			{
				case ExpressionType.Equal:
				case ExpressionType.NotEqual:
					{
						var isExact = false;
						var equalableInterface = GetEqualableInterface(type1, type2, ref isExact);
						if (!isExact)
						{
							var isExact2 = false;
							var equalableInterface2 = GetEqualableInterface(type2, type1, ref isExact2);
							if (isExact2)
								return BinaryOperationEqualableToExpression(lua, equalableInterface, op, expr2, type2, expr1, type1);

						}
						if (equalableInterface != null)
							return BinaryOperationEqualableToExpression(lua, equalableInterface, op, expr1, type1, expr2, type2);
						else
						{
							// exception for char vs string comparisions
							if (type1 == typeof(char) && type2 == typeof(string))
							{
								expr1 = ConvertWithRuntime(lua, expr1, type1, type2);
								type1 = type2;
							}
							if (type2 == typeof(string) && type2 == typeof(char))
							{
								expr2 = ConvertWithRuntime(lua, expr2, type2, type1);
								type2 = type1;
							}

							expr1 = ConvertWithRuntime(lua, expr1, type1, typeof(object));
							expr2 = ConvertWithRuntime(lua, expr2, type2, typeof(object));

							Expression expr = Expression.OrElse(
								Expression.Call(Lua.ObjectReferenceEqualsMethodInfo, expr1, expr2),
								Expression.Call(Lua.ObjectEqualsMethodInfo, expr1, expr2)
							);

							return op == ExpressionType.NotEqual ? Expression.Not(expr) : expr;
						}
					}
			}

			#endregion

			#region -- Try to lift type --

			if (liftAllowed && type1 != type2)
			{
				if (TryConvertWithRuntime(lua, ref expr1, ref type1, type2))
					return BinaryOperationArithmeticExpression(lua, op, expr1, type1, expr2, type2, type1org, type2org);
				else if (TryConvertWithRuntime(lua, ref expr2, ref type2, type1))
					return BinaryOperationArithmeticExpression(lua, op, expr1, type1, expr2, type2, type1org, type2org);
			}

			#endregion

			throw new LuaEmitException(LuaEmitException.OperatorNotDefined, op, type1org.Name, type2org.Name);
		} // func BinaryOperationArithmeticExpression

		/// <summary>Compares the to types and returns the "higest".</summary>
		/// <param name="type1"></param>
		/// <param name="type2"></param>
		/// <param name="signed"></param>
		/// <returns></returns>
		public static Type LiftType(Type type1, Type type2, bool signed = false)
		{
			if (type1 == type2)
				return type1;

			var tc1 = GetTypeCode(type1);
			var tc2 = GetTypeCode(type2);

			if (IsArithmeticType(tc1) && IsArithmeticType(tc2)) // process only arithmetic types
				return LiftType(type1, tc1, type2, tc2, signed);
			else
				return typeof(object);
		} // func LiftType

		private static Type LiftType(Type type1, LuaEmitTypeCode tc1, Type type2, LuaEmitTypeCode tc2, bool signed)
		{
			// Achtung: this code depends on the numeric representation of TypeCode

			if (IsFloatType(tc1) && IsFloatType(tc2)) // both are floats
				return tc1 < tc2 ? type2 : type1; // -> use the higest
			else if (IsFloatType(tc1)) // the first one is a float, the other one is a integer
				return type1; // -> use the float
			else if (IsFloatType(tc2)) // the second one is a float, the other one is a integer
				return type2; // -> use the float

			else if ((tc1 & LuaEmitTypeCode.UnsignedFlag) != LuaEmitTypeCode.UnsignedFlag &&
							 (tc2 & LuaEmitTypeCode.UnsignedFlag) != LuaEmitTypeCode.UnsignedFlag) // both types are signed integers
				return tc1 < tc2 ? type2 : type1; // -> use the highest
			else if ((tc1 & LuaEmitTypeCode.UnsignedFlag) != LuaEmitTypeCode.UnsignedFlag) // the first one is signed integer
			{
				if (tc1 > tc2) // the unsigned is lower then the signed
					return type1; // -> use the signed
				else // -> we need a higher signed integer
					return LiftTypeSigned(tc1, tc2);
			}
			else if ((tc2 & LuaEmitTypeCode.UnsignedFlag) != LuaEmitTypeCode.UnsignedFlag)
			{
				if (tc2 > tc1)
					return type2;
				else
					return LiftTypeSigned(tc2, tc1);
			}
			else if (signed) // force unsigned
			{
				if (tc1 > tc2)
					return LiftTypeSigned(tc1, tc2);
				else
					return LiftTypeSigned(tc2, tc1);
			}
			else // both are unsigned
				return tc1 < tc2 ? type2 : type1; // -> use the highest
		} // func LiftType

		private static Type LiftTypeSigned(LuaEmitTypeCode tc1, LuaEmitTypeCode tc2)
		{
			switch (tc2)
			{
				case LuaEmitTypeCode.Byte:
					return typeof(short);
				case LuaEmitTypeCode.UInt16:
					return typeof(int);
				case LuaEmitTypeCode.UInt32:
					return typeof(long);
				case LuaEmitTypeCode.UInt64:
					return typeof(double);
				default:
					throw new InvalidOperationException(String.Format("Internal error in lift type ({0} vs. {1})", tc1, tc2));
			}
		} // func LiftTypeSigned

		private static Type LiftTypeNext(Lua runtime, Type type)
		{
			switch (GetTypeCode(type))
			{
				case LuaEmitTypeCode.SByte:
					return typeof(short);
				case LuaEmitTypeCode.Byte:
					return typeof(ushort);
				case LuaEmitTypeCode.Int16:
					return typeof(int);
				case LuaEmitTypeCode.UInt16:
					return typeof(uint);
				case LuaEmitTypeCode.Int32:
					return typeof(long);
				case LuaEmitTypeCode.UInt32:
					return typeof(ulong);
				default:
					if (runtime == null)
						return typeof(double);
					else
						return Lua.GetFloatType(runtime.NumberType);
			}
		} // func LiftTypeNext

		private static Type LiftIntegerType(Lua lua, Type type)
		{
			switch (GetTypeCode(type))
			{
				case LuaEmitTypeCode.SByte:
				case LuaEmitTypeCode.Byte:
				case LuaEmitTypeCode.Int16:
				case LuaEmitTypeCode.UInt16:
				case LuaEmitTypeCode.Int32:
				case LuaEmitTypeCode.UInt32:
				case LuaEmitTypeCode.Int64:
				case LuaEmitTypeCode.UInt64:
					return type;
				case LuaEmitTypeCode.Single:
					return typeof(int);
				case LuaEmitTypeCode.Double:
					return typeof(long);
				default:
					return lua == null ? typeof(int) : Lua.GetIntegerType(lua.NumberType);
			}
		} // func LiftIntegerType

		#endregion

		#region -- Emit GetMember -----------------------------------------------------

		public static LuaTryGetMemberReturn TryGetMember(Expression target, Type targetType, string memberName, bool ignoreCase, out Expression result)
		{
			var luaTargetType = LuaType.GetType(targetType);
			var enumerateType = GetMethodEnumeratorType(target, targetType);

			using (var memberEnum = luaTargetType.EnumerateMembers<MemberInfo>(enumerateType, memberName, ignoreCase).GetEnumerator())
			{
				if (!memberEnum.MoveNext()) // no member found
				{
					result = null;
					return LuaTryGetMemberReturn.None;
				}

				var methodInfo = memberEnum.Current as MethodInfo; // check for method member
				if (methodInfo != null)
				{
					if (memberEnum.MoveNext()) // more than one overload
					{
						result = Expression.New(Lua.OverloadedMethodConstructorInfo,
							target ?? Expression.Default(typeof(object)),
							Expression.Constant(luaTargetType.EnumerateMembers<MethodInfo>(enumerateType, memberName, ignoreCase).ToArray()),
							Expression.Constant(false)
						);
						return LuaTryGetMemberReturn.ValidExpression;
					}
					else // only one method member -> return the member
					{
						result = Expression.New(Lua.MethodConstructorInfo,
							target ?? Expression.Default(typeof(object)),
							Expression.Constant(methodInfo, typeof(MethodInfo)),
							Expression.Constant(false)
						);
						return LuaTryGetMemberReturn.ValidExpression;
					}
				}
				else // return a property
				{
					var memberInfo = memberEnum.Current;

					if (target != null && target.Type != targetType) // limitType can be different to the act
						target = Expression.Convert(target, targetType);

					if (memberInfo is FieldInfo)
					{
						result = Expression.MakeMemberAccess(target, memberInfo);
						return LuaTryGetMemberReturn.ValidExpression;
					}
					else if (memberInfo is PropertyInfo propertyInfo)
					{
						if (!propertyInfo.CanRead)
						{
							result = null;
							return LuaTryGetMemberReturn.NotReadable;
						}

						result = Expression.MakeMemberAccess(target, memberInfo);
						return LuaTryGetMemberReturn.ValidExpression;
					}
					else if (memberInfo is EventInfo eventInfo)
					{
						result = Expression.New(Lua.EventConstructorInfo,
							target ?? Expression.Default(typeof(object)),
							Expression.Constant(eventInfo)
						);
						return LuaTryGetMemberReturn.ValidExpression;
					}
					else if (memberInfo is TypeInfo typeInfo)
					{
						result = Expression.Call(Lua.TypeGetTypeMethodInfoArgType, Expression.Constant(typeInfo.AsType()));
						return LuaTryGetMemberReturn.ValidExpression;
					}
					else
					{
						result = null;
						return LuaTryGetMemberReturn.NotReadable;
					}
				}
			}
		} // func GetMember

		private static LuaMethodEnumerate GetMethodEnumeratorType<TARG>(TARG target, Func<TARG, Expression> getExpr, Func<TARG, Type> getType)
			where TARG : class
			=> target == null ? LuaMethodEnumerate.Static : GetMethodEnumeratorType(getExpr(target), getType(target));

		private static LuaMethodEnumerate GetMethodEnumeratorType(Expression target, Type targetType)
		{
			LuaMethodEnumerate enumerateType;
			if (target == null)
				enumerateType = LuaMethodEnumerate.Static;
			else if (target.Type == targetType)
				enumerateType = LuaMethodEnumerate.Typed;
			else
				enumerateType = LuaMethodEnumerate.Dynamic;
			return enumerateType;
		} // func GetMethodEnumeratorType

		#endregion

		#region -- Emit SetMember -----------------------------------------------------

		public static LuaTrySetMemberReturn TrySetMember(Expression target, Type targetType, string memberName, bool ignoreCase, Func<Type, Expression> set, out Expression result)
		{
			var luaType = LuaType.GetType(targetType);
			using (var memberEnum = luaType.EnumerateMembers<MemberInfo>(GetMethodEnumeratorType(target, targetType), memberName, ignoreCase).GetEnumerator())
			{
				if (!memberEnum.MoveNext())
				{
					result = null;
					return LuaTrySetMemberReturn.None;
				}

				var memberInfo = memberEnum.Current;
				if (memberInfo is PropertyInfo propertyInfo)
				{
					if (!propertyInfo.CanWrite)
					{
						result = null;
						return LuaTrySetMemberReturn.NotWritable;
					}
					result = Expression.Assign(Expression.Property(target != null ? Lua.EnsureType(target, targetType) : null, propertyInfo), set(propertyInfo.PropertyType));
					return LuaTrySetMemberReturn.ValidExpression;
				}
				else if (memberInfo is FieldInfo fieldInfo)
				{
					result = Expression.Assign(Expression.Field(target != null ? Lua.EnsureType(target, targetType) : null, fieldInfo), set(fieldInfo.FieldType));
					return LuaTrySetMemberReturn.ValidExpression;
				}
				else if (memberInfo is EventInfo eventInfo)
				{
					result = target == null
						? Expression.Call(eventInfo.AddMethod, set(eventInfo.EventHandlerType))
						: Expression.Call(target, eventInfo.AddMethod, set(eventInfo.EventHandlerType));
					return LuaTrySetMemberReturn.ValidExpression;
				}
				else
				{
					result = null;
					return LuaTrySetMemberReturn.NotWritable;
				}
			}
		} // func TrySetMember

		#endregion

		#region -- Emit GetIndex, SetIndex --------------------------------------------

		public static Expression GetIndex<TARG>(Lua runtime, TARG instance, TARG[] arguments, Func<TARG, Expression> getExpr, Func<TARG, Type> getType, bool isParse)
			where TARG : class
		{
			var instanceType = getType(instance);
			if (isParse && IsDynamicType(instanceType))
			{
				return DynamicExpression.Dynamic(runtime.GetGetIndexMember(new CallInfo(arguments.Length)), typeof(object),
					CreateDynamicArgs(getExpr(instance), instanceType, arguments, getExpr, getType)
				);
			}

			return GetIndexAccess(runtime, getExpr(instance), instanceType, arguments, getExpr, getType, isParse);
		} // func GetIndex

		public static Expression SetIndex<TARG>(Lua runtime, TARG instance, TARG[] arguments, TARG setTo, Func<TARG, Expression> getExpr, Func<TARG, Type> getType, bool isParse)
			 where TARG : class
		{
			var instanceType = getType(instance);
			if (isParse && IsDynamicType(instanceType))
			{
				return DynamicExpression.Dynamic(runtime.GetSetIndexMember(new CallInfo(arguments.Length)), typeof(object),
					CreateDynamicArgs(getExpr(instance), instanceType, arguments, setTo, getExpr, getType)
				);
			}

			// Emit the index set
			var exprIndexAccess = GetIndexAccess(runtime, getExpr(instance), instanceType, arguments, getExpr, getType, isParse);
			return Expression.Assign(exprIndexAccess, ConvertWithRuntime(runtime, getExpr(setTo), getType(setTo), exprIndexAccess.Type));
		} // func SetIndex

		private static Expression GetIndexAccess<TARG>(Lua runtime, Expression instance, Type instanceType, TARG[] arguments, Func<TARG, Expression> getExpr, Func<TARG, Type> getType, bool isParse)
			where TARG : class
		{
			if (typeof(Array).GetTypeInfo().IsAssignableFrom(instanceType.GetTypeInfo())) // type is an array
			{
				// create index as integers
				var indexes = new Expression[arguments.Length];
				for (var i = 0; i < indexes.Length; i++)
					indexes[i] = ConvertWithRuntime(runtime, getExpr(arguments[i]), getType(arguments[i]), typeof(int));

				return Expression.ArrayAccess(ConvertWithRuntime(runtime, instance, instanceType, instanceType), indexes);
			}
			else // try find a property
			{
				var properties =
					(
						from pi in instanceType.GetRuntimeProperties()
						where pi.GetMethod.IsStatic == (instance == null) && pi.GetIndexParameters().Length > 0
						select pi
					).ToArray();

				var callInfo = new CallInfo(arguments.Length);
				var piIndex = FindMember(properties, callInfo, arguments, getType, false);

				if (piIndex == null)
					throw new LuaEmitException(LuaEmitException.IndexNotFound, instanceType.Name);
				else
				{
					return BindParameter(runtime,
						  args => Expression.MakeIndex(ConvertWithRuntime(runtime, instance, instanceType, instanceType), piIndex, args),
						  piIndex.GetIndexParameters(),
						  callInfo,
						  arguments,
						  getExpr, getType, isParse
					  );
				}
			}
		} // func GetIndexAccess

		#endregion

		#region -- Emit Invoke Member -------------------------------------------------

		#region -- BindParameter ------------------------------------------------------

		public static Expression BindParameter<T>(Lua lua, Func<Expression[], Expression> emitCall, ParameterInfo[] parameterInfo, CallInfo callInfo, T[] arguments, Func<T, Expression> getExpr, Func<T, Type> getType, bool forParse)
		{
			var argumentExpressions = new Expression[parameterInfo.Length]; // argument-array for the call
			var variablesToReturn = new List<ParameterExpression>(); // variables the are needed for the call
			var callBlock = new List<Expression>(); // expression of the call block, for the call

			var argumentsWorkedWith = callInfo.ArgumentNames.Count != 0 ? new bool[arguments.Length] : null;
			var positionalArguments = arguments.Length - callInfo.ArgumentNames.Count; // number of positional arguments

			var argumentsIndex = 0; // index of the argument, that is processed
			var lastArgumentStretchCount = 0; // numer of LuaResult arguments, processed
			var lastArgumentIsResult = arguments.Length > 0 && argumentsWorkedWith == null && getType(arguments[arguments.Length - 1]) == typeof(LuaResult); // is last argumetn a result
			var argumentsCount = lastArgumentIsResult ? positionalArguments - 1 : positionalArguments;

			var parameterIndex = 0; // index of the parameter, that is processed
			var lastParameterIsArray = parameterInfo.Length > 0 && argumentsWorkedWith == null && parameterInfo[parameterInfo.Length - 1].ParameterType.IsArray; // is the last argument a array
			var parameterCount = lastParameterIsArray ? parameterInfo.Length - 1 : (argumentsWorkedWith == null ? parameterInfo.Length : positionalArguments); // number of "normal" parameters

			ParameterExpression varLuaResult = null;

			if (argumentsWorkedWith != null)
			{
				for (var i = 0; i < argumentsWorkedWith.Length; i++)
					argumentsWorkedWith[i] = false;
			}

			#region -- fill all match parameters to arguments, positional --
			while (parameterIndex < parameterCount)
			{
				Expression argumentExpression = null;
				var parameter = parameterInfo[parameterIndex];
				var parameterType = parameter.ParameterType.IsByRef ? parameter.ParameterType.GetElementType() : parameter.ParameterType;

				if (parameter.IsOut && !parameter.IsIn) // out-param no value needed
				{
					argumentExpression = null;
				}
				else if (argumentsIndex < argumentsCount) // positional argument exists
				{
					argumentExpression = ConvertWithRuntime(lua, getExpr(arguments[argumentsIndex]), getType(arguments[argumentsIndex]), parameterType);
					if (argumentsWorkedWith != null)
						argumentsWorkedWith[argumentsIndex] = true;
					argumentsIndex++;
				}
				else if (lastArgumentStretchCount > 0) // stretch LuaResult
				{
					argumentExpression = GetResultExpression(lua, varLuaResult, lastArgumentStretchCount++, parameterType, GetDefaultParameterExpression(parameter, parameterType));
				}
				else if (lastArgumentIsResult) // start stretch of LuaResult
				{

					varLuaResult = Expression.Variable(typeof(LuaResult), "#result");
					argumentExpression = GetResultExpression(
						lua,
						Expression.Assign(varLuaResult, ConvertWithRuntime(null, getExpr(arguments[argumentsIndex]), typeof(LuaResult), typeof(LuaResult))),
						0,
						parameterType,
						GetDefaultParameterExpression(parameter, parameterType)
					);

					lastArgumentStretchCount = 1;
					argumentsIndex++; // move of last
				}
				else // No arguments left, if we have a default value, set it or use the default of the type
					argumentExpression = GetDefaultParameterExpression(parameter, parameterType);

				// Create a variable for the byref parameters
				if (parameter.ParameterType.IsByRef)
					TransformArgumentToVariable(ref argumentExpression, parameterType, parameterIndex, callBlock, variablesToReturn);

				argumentExpressions[parameterIndex] = argumentExpression;
				parameterIndex++;
			}
			#endregion

			// fill last argument
			if (argumentsWorkedWith != null)
			{
				#region -- named argument mode --
				parameterCount = parameterInfo.Length;
				while (parameterIndex < parameterCount)
				{
					Expression argumentExpression = null;
					var parameter = parameterInfo[parameterIndex];
					var parameterType = parameter.ParameterType.IsByRef ? parameter.ParameterType.GetElementType() : parameter.ParameterType;

					var nameIndex = callInfo.ArgumentNames.IndexOf(parameter.Name);
					if (nameIndex >= 0) // named argument for the parameter exists
					{
						argumentsIndex = positionalArguments + nameIndex;
						argumentExpression = ConvertWithRuntime(lua, getExpr(arguments[argumentsIndex]), getType(arguments[argumentsIndex]), parameterType);
						argumentsWorkedWith[argumentsIndex] = true;
					}
					else // set default
					{
						argumentExpression = GetDefaultParameterExpression(parameter, parameterType);
					}

					// Create a variable for the byref parameters
					if (parameter.ParameterType.IsByRef)
						TransformArgumentToVariable(ref argumentExpression, parameterType, parameterIndex, callBlock, variablesToReturn);

					argumentExpressions[parameterIndex] = argumentExpression;
					parameterIndex++;
				}
				#endregion
			}
			else if (lastParameterIsArray) // extent call with the last argument
			{
				#region -- generate vararg for an array --
				Expression argumentExpression;
				var lastParameter = parameterInfo[parameterCount];
				var arrayType = lastParameter.ParameterType.GetElementType();

				if (lastArgumentStretchCount > 0) // finish LuaResult
				{
					argumentExpression = Expression.Convert(
						Expression.Call(
							Lua.GetResultValuesMethodInfo,
							varLuaResult,
							Expression.Constant(lastArgumentStretchCount), Expression.Constant(arrayType)),
						lastParameter.ParameterType
					);
				}
				else if (argumentsCount - argumentsIndex == 1 && !lastArgumentIsResult && getType(arguments[argumentsIndex]).IsArray)
				{
					if (getType(arguments[argumentsIndex]) == lastParameter.ParameterType) // same type
						argumentExpression = Expression.Convert(getExpr(arguments[argumentsIndex]), lastParameter.ParameterType);
					else
					{
						argumentExpression = Expression.Convert(
							Expression.Call(Lua.ConvertArrayMethodInfo,
								Expression.Convert(getExpr(arguments[argumentsIndex]), typeof(Array)),
								Expression.Constant(arrayType)
							),
							lastParameter.ParameterType
						);
					}
				}
				else if (argumentsIndex < argumentsCount) // normal arguments left
				{
					var collectedArguments = new List<Expression>();

					// collect all arguments that are left
					for (; argumentsIndex < argumentsCount; argumentsIndex++)
						collectedArguments.Add(ConvertWithRuntime(lua, getExpr(arguments[argumentsIndex]), getType(arguments[argumentsIndex]), arrayType));

					// the last argument is a LuaResult
					if (lastArgumentIsResult)
					{
						// combine the arguments and the last result to the correct array
						var tmpExpr = getExpr(arguments[arguments.Length - 1]);
						var tmpType = getType(arguments[arguments.Length - 1]);
						argumentExpression = Expression.Convert(
							Expression.Call(null, Lua.CombineArrayWithResultMethodInfo,
								Expression.Convert(Expression.NewArrayInit(arrayType, collectedArguments), typeof(Array)),
								ConvertWithRuntime(lua, tmpExpr, tmpType, tmpType),
								Expression.Constant(arrayType)
							),
							lastParameter.ParameterType
						);
					}
					else // create a array of the collected arguments
						argumentExpression = Expression.NewArrayInit(arrayType, collectedArguments);
				}
				else if (lastArgumentIsResult) // there is a result to take care of
				{
					var tmpExpr = getExpr(arguments[arguments.Length - 1]);
					var tmpType = getType(arguments[arguments.Length - 1]);
					argumentExpression = Expression.Convert(
						Expression.Call(
							Lua.GetResultValuesMethodInfo,
							ConvertWithRuntime(null, tmpExpr, tmpType, tmpType),
							Expression.Constant(0),
							Expression.Constant(arrayType)),
						lastParameter.ParameterType
					);
				}
				else // nothing left, create empty array
					argumentExpression = Expression.NewArrayInit(arrayType);

				argumentExpressions[parameterIndex] = argumentExpression;
				parameterIndex++;
				#endregion
			}

			#region -- emit call block --
			var argumentsLeft = argumentsWorkedWith == null ? argumentsIndex < arguments.Length : Array.Exists(argumentsWorkedWith, c => c == false); // not argumentsCount, because we include really all arguments
			if (variablesToReturn.Count > 0 || varLuaResult != null || (forParse && argumentsLeft)) // we have variables or arguments are left out
			{
				// add the call
				var exprCall = emitCall(argumentExpressions);
				ParameterExpression varReturn = null;
				if (exprCall.Type != typeof(void) && (argumentsLeft || variablesToReturn.Count > 0)) // create a return variable, if we have variables or arguments left
				{
					varReturn = Expression.Variable(exprCall.Type, "#return");
					callBlock.Add(Expression.Assign(varReturn, exprCall));
					variablesToReturn.Insert(0, varReturn);
				}
				else // add the call normally
					callBlock.Add(exprCall);

				// argument left
				if (argumentsLeft)
				{
					if (argumentsWorkedWith == null)
					{
						for (; argumentsIndex < arguments.Length; argumentsIndex++)
							callBlock.Add(getExpr(arguments[argumentsIndex]));
					}
					else
					{
						for (var i = 0; i < argumentsWorkedWith.Length; i++)
						{
							if (!argumentsWorkedWith[i])
								callBlock.Add(getExpr(arguments[i]));
						}
					}
				}

				// create the variable definition
				var varResultExists = varLuaResult != null ? 1 : 0;
				var variables = new ParameterExpression[variablesToReturn.Count + varResultExists];
				variablesToReturn.CopyTo(variables, varResultExists);
				if (varResultExists > 0)
					variables[0] = varLuaResult;

				if (variablesToReturn.Count == 0) // no multi or return variables results
				{
					return Expression.Block(argumentsLeft ? typeof(void) : exprCall.Type, variables, callBlock);
				}
				else if (variablesToReturn.Count == 1) // only one return or variable
				{
					callBlock.Add(variablesToReturn[0]);
					return Expression.Block(variablesToReturn[0].Type, variables, callBlock);
				}
				else // multi result return
				{
					callBlock.Add(Expression.New(Lua.ResultConstructorInfoArgN, Expression.NewArrayInit(typeof(object),
						from v in variablesToReturn select ConvertWithRuntime(null, v, v.Type, typeof(object)))));

					return Expression.Block(typeof(LuaResult), variables, callBlock);
				}
			}
			else
				return emitCall(argumentExpressions);
			#endregion
		} // func BindParameter

		private static void TransformArgumentToVariable(ref Expression argumentExpression, Type parameterType, int parameterIndex, List<Expression> callBlock, List<ParameterExpression> variablesToReturn)
		{
			var r = Expression.Variable(parameterType, "r" + parameterIndex.ToString());
			variablesToReturn.Add(r);
			if (argumentExpression != null)
				callBlock.Add(Expression.Assign(r, argumentExpression));
			argumentExpression = r;
		} // proc TransformArgumentToVariable

		private static Expression GetDefaultParameterExpression(ParameterInfo parameter, Type typeParameter)
			=> parameter.IsOptional ?
				(Expression)Expression.Constant(parameter.DefaultValue, typeParameter) :
				(Expression)Expression.Default(typeParameter);

		#endregion

		#region -- FindMember ---------------------------------------------------------

		private static ParameterInfo[] GetMemberParameter<TMEMBERTYPE>(TMEMBERTYPE mi, bool isMemberCall)
		{
			if (mi is MethodBase mb)
			{
				return isMemberCall && mb.IsStatic ?
					mb.GetParameters().Skip(1).ToArray() :
					mb.GetParameters();
			}

			return mi is PropertyInfo pi ? pi.GetIndexParameters() : new ParameterInfo[0];
		} // func GetMemberParameter

		#region -- enum MemberMatchValue ----------------------------------------------

		internal enum MemberMatchValue
		{
			None,
			Exact,
			GenericMatch,
			AssignableMatch,
			ImplicitConversion,
			ArraySplatting,
			ArrayDecay,
			StringAutoConvert,
			Explicit,
			NumericImplicitConvert
		} // enum MemberMatchValue

		#endregion

		#region -- struct MemberMatchInfo ---------------------------------------------

		/// <summary>Holds the result of the member.</summary>
		[DebuggerDisplay("{DebuggerDisplay,nq}")]
		private sealed class MemberMatchInfo<TMEMBERTYPE> : IComparable<MemberMatchInfo<TMEMBERTYPE>>
			where TMEMBERTYPE : MemberInfo
		{
			private readonly int argumentsLength;     // number of arguments we need to match
			private readonly bool unboundedArguments; // is the number endless (LuaResult as last argument)
			internal int matchesOnBeginning;
			internal int exactMatches;
			internal int implicitMatches;
			internal int explicitMatches;

			// exaxt match has no penalty,
			// generic match has a penalty of 1
			// implicit conversion match has a penalty of 2
			// array splatting has a penalty of 3
			internal int conversionPenalty;

			private TMEMBERTYPE currentMember;
			internal int currentParameterLength;
			private bool lastParameterIsArray;

			private const int GenericMatchPenalty = 0;
			private const int AssignableMatchPenalty = 0;
			private const int ImplicitConversionPenalty = 2;
			private const int ArraySplattingPenalty = 1;
			private const int OtherMatchPenalty = 4;
			private const int ArrayDecayToObjectPenalty = 5;
			private const int ExplicitConvertPenalty = 7;
			private const int StringAutoConvertPenalty = 10;

			public MemberMatchInfo(bool unboundedArguments, int argumentsLength)
			{
				this.unboundedArguments = unboundedArguments;
				this.argumentsLength = argumentsLength;
			} // ctor

			/// <summary>
			/// Compares the current member with the new member.
			/// </summary>
			/// <param name="other"></param>
			/// <returns>negative is this member is a better match than <paramref name="other"/>, 0 if they are equal (should never happen) and positive if the other is a better overload.</returns>
			public int CompareTo(MemberMatchInfo<TMEMBERTYPE> other)
			{
				if (IsPerfect)
				{
					Debug.WriteLine($"{this}  ==> IsPerfect");
					return -1;
				}

				if (other.IsPerfect)
				{
					Debug.WriteLine($"{other}  ==> other IsPerfect");
					return 1;
				}

				if (unboundedArguments && currentParameterLength > other.currentParameterLength)
				{
					Debug.WriteLine("  ==> other {unboundedArguments} && {currentParameterLength} > {other.currentParameterLength}");
					return -1;
				}
				if (!unboundedArguments || currentParameterLength == other.currentParameterLength)
				{
					if (argumentsLength == 0 && currentParameterLength == 0) // zero arguments
					{
						Debug.WriteLine("  ==> other Zero Args");
						return -1;
					}

					return conversionPenalty.CompareTo(other.conversionPenalty);
				}

				Debug.Assert(false, "Algorithm error! Two overloads should never be equally good");
				return 0;
			}

			public override string ToString()
			{
				return DebuggerDisplay;
			} // func ToString

			public void Reset(TMEMBERTYPE member, ParameterInfo[] parameter)
			{
				this.matchesOnBeginning = 0;
				this.exactMatches = 0;
				this.implicitMatches = 0;
				this.explicitMatches = 0;
				this.conversionPenalty = 0;
				this.currentMember = member;

				// get the parameter length
				this.currentParameterLength = parameter.Length;
				lastParameterIsArray =
					currentParameterLength > 0 && parameter[parameter.Length - 1].ParameterType.IsArray;

			} // proc Reset

			public void SetMatch(MemberMatchValue value, bool positional)
			{
				var inc = value switch
				{
					MemberMatchValue.Exact => 0,
					MemberMatchValue.GenericMatch => GenericMatchPenalty,
					MemberMatchValue.AssignableMatch => AssignableMatchPenalty,
					MemberMatchValue.ImplicitConversion => ImplicitConversionPenalty,
					MemberMatchValue.ArraySplatting => ArraySplattingPenalty,
					MemberMatchValue.ArrayDecay => ArrayDecayToObjectPenalty,
					MemberMatchValue.StringAutoConvert => StringAutoConvertPenalty,
					MemberMatchValue.Explicit => ExplicitConvertPenalty,
					_ => OtherMatchPenalty
				};
				conversionPenalty += inc;
			}// proc SetMatch



			public TMEMBERTYPE CurrentMember => currentMember;

			public bool IsPerfect => currentParameterLength == argumentsLength && conversionPenalty == 0;
			public int NoneMatches => lastParameterIsArray ? 0 : currentParameterLength - explicitMatches;

			public string DebuggerDisplay => $"{currentMember} convPenalty: {conversionPenalty}";
		} // struct MemberMatchInfo

		#endregion

		#region -- class MemberMatch --------------------------------------------------

		/// <summary>Holds the description of the arguments and the compare algorithm.</summary>
		private sealed class MemberMatch<TMEMBERTYPE, TARG>
			where TMEMBERTYPE : MemberInfo
			where TARG : class
		{
			private readonly CallInfo callInfo;
			private readonly int positionalArguments;
			private readonly TARG[] arguments;
			private readonly bool lastArgumentIsExpandable;
			private readonly Func<TARG, Type> getType;
			private readonly Action<ParameterInfo[], MemberMatchInfo<TMEMBERTYPE>> resetAlgorithm;

			public MemberMatch(CallInfo callInfo, TARG[] arguments, Func<TARG, Type> getType)
			{
				// init reset parameter
				this.callInfo = callInfo;
				this.positionalArguments = arguments.Length - callInfo.ArgumentNames.Count; // number of positional arguments
				this.arguments = arguments;
				this.getType = getType;
				this.lastArgumentIsExpandable = arguments.Length > 0 && getType(arguments[arguments.Length - 1]) == typeof(LuaResult);

#if DEBUG
				var argsDebug = new StringBuilder();
				for (var i = 0; i < arguments.Length; i++)
				{
					if (argsDebug.Length > 0)
						argsDebug.Append(", ");
					var argNameIndex = i - arguments.Length + callInfo.ArgumentNames.Count;
					if (argNameIndex >= 0)
						argsDebug.Append(callInfo.ArgumentNames[argNameIndex]).Append(": ");
					argsDebug.Append(getType(arguments[i]));
				}
				Debug.WriteLine($"Call: {argsDebug}");
#endif

				// choose the algorithm
				if (arguments.Length == 0 || positionalArguments == arguments.Length)
				{
					if (lastArgumentIsExpandable)
						resetAlgorithm = ResetPositionalMax;
					else
						resetAlgorithm = ResetPositional;
				}
				else
					resetAlgorithm = ResetNamed;

				Debug.WriteLine($"Algorithm: {resetAlgorithm.GetMethodInfo().Name}");
			} // ctor

			public void Reset(TMEMBERTYPE member, bool isMemberCall, MemberMatchInfo<TMEMBERTYPE> target)
			{
				Debug.WriteLine($"Reset member: {member}");

				var parameterInfo = GetMemberParameter(member, isMemberCall);
				target.Reset(member, parameterInfo);
				resetAlgorithm(parameterInfo, target);
				Debug.WriteLine($"      Result: {target}");
			} // proc Reset



			private void ResetPositionalPart(ParameterInfo[] parameterInfos, int length, MemberMatchInfo<TMEMBERTYPE> target)
			{
				var lastParameterIsParamArray = parameterInfos.Length > 0 && parameterInfos[parameterInfos.Length - 1].IsParamArray();
				for (var i = 0; i < length; i++)
				{
					var parameterInfo = parameterInfos[i];
					if (lastArgumentIsExpandable && i == length - 1)
					{
						if (lastParameterIsParamArray)
						{
							target.SetMatch(MemberMatchValue.ArraySplatting, true);
						}
						else
						{
							var argumentType = getType(arguments[i]).GetTypeInfo();
							var memberMatchValue = GetParameterMatch(parameterInfo, argumentType);
							target.SetMatch(MemberMatchValue.ArrayDecay, true);
							target.SetMatch(memberMatchValue, true);
						}
						for (int argIndex = i; argIndex < arguments.Length; argIndex++)
						{
							var argumentType = getType(arguments[argIndex]).GetTypeInfo();
							var memberMatchValue = GetParameterMatch(parameterInfo, argumentType, lastParameterIsParamArray);
							memberMatchValue = memberMatchValue == MemberMatchValue.ArrayDecay ? MemberMatchValue.AssignableMatch : memberMatchValue;
							target.SetMatch(memberMatchValue, true);
						}
					}
					else
					{
						var argumentType = getType(arguments[i]).GetTypeInfo();
						var memberMatchValue = GetParameterMatch(parameterInfo, argumentType);
						target.SetMatch(memberMatchValue, true);
					}

				}

			} // proc ResetPositionalPart

			private void ResetPositional(ParameterInfo[] parameterInfo, MemberMatchInfo<TMEMBERTYPE> target)
			{
				if (target.currentParameterLength == Int32.MaxValue)
				{
					// check first part
					var length = Math.Min(parameterInfo.Length - 1, arguments.Length);
					ResetPositionalPart(parameterInfo, length, target);

					// array to array match
					if (arguments.Length == parameterInfo.Length)
					{
						var argLastType = getType(arguments[arguments.Length - 1]);
						var paramLastInfo = parameterInfo[parameterInfo.Length - 1];
						if (argLastType.IsArray && paramLastInfo.ParameterType.IsArray)
						{
							var memberMatchValue = GetParameterMatch(paramLastInfo, argLastType.GetTypeInfo(), parameterIsParamsArray: paramLastInfo.IsParamArray());
							target.SetMatch(memberMatchValue, true);
							return;
						}
					}

					// test the array
					var rest = lastArgumentIsExpandable ? Int32.MaxValue - length : arguments.Length - length;
					var elementType = parameterInfo[parameterInfo.Length - 1].ParameterType.GetElementType();
					if (elementType == typeof(object)) // all is possible
					{
						//if (target.explicitMatches == target.matchesOnBeginning)
						//	target.matchesOnBeginning += rest;
						target.implicitMatches += rest;
					}
					target.explicitMatches += rest;
				}
				else
				{
					var length = Math.Min(parameterInfo.Length, arguments.Length);
					ResetPositionalPart(parameterInfo, length, target);
				}
			} // proc ResetPositional

			private void ResetPositionalMax(ParameterInfo[] parameterInfo, MemberMatchInfo<TMEMBERTYPE> target)
			{
				var lastParam = parameterInfo[parameterInfo.Length - 1];
				var lastParamIsArray = lastParam.ParameterType.IsArray;
				var checkArguments = arguments.Length == parameterInfo.Length || lastParamIsArray ? arguments.Length : arguments.Length - 1;

				var paramsToCheck =  checkArguments >= parameterInfo.Length ? parameterInfo.Length : checkArguments;

				ResetPositionalPart(parameterInfo, paramsToCheck, target);

				// the last part will match
				target.explicitMatches = lastArgumentIsExpandable && lastParamIsArray ? 0 : int.MaxValue;
			} // proc ResetPositionalMax

			private void ResetNamed(ParameterInfo[] parameterInfo, MemberMatchInfo<TMEMBERTYPE> target)
			{
				// check the positional part
				ResetPositionalPart(parameterInfo, Math.Min(parameterInfo.Length, positionalArguments), target);

				// check the named
				if (positionalArguments < parameterInfo.Length)
				{
					var i = positionalArguments;
					foreach (var cur in callInfo.ArgumentNames)
					{
						var index = Array.FindIndex(parameterInfo, positionalArguments, c => c.Name == cur);
						if (index != -1)
							target.SetMatch(GetParameterMatch(parameterInfo[index], getType(arguments[i++]).GetTypeInfo()), true); // -> mark as position, because the argument has the correct position
					}
				}
			} // proc ResetNamed

			private MemberMatchValue GetParameterMatch(ParameterInfo parameterInfo, TypeInfo argumentType, bool parameterIsParamsArray = false)
			{
				var parameterType = parameterInfo.ParameterType.GetTypeInfo();
				if (parameterType == argumentType)
					return MemberMatchValue.Exact;
				else if (parameterType.IsGenericParameter) // special checks for generic parameter
				{
					#region -- check generic --
					var typeConstraints = parameterType.GetGenericParameterConstraints();

					// check "class"
					if ((parameterType.GenericParameterAttributes & GenericParameterAttributes.ReferenceTypeConstraint) != 0 && argumentType.IsValueType)
						return MemberMatchValue.GenericMatch;

					// check struct
					if ((parameterType.GenericParameterAttributes & GenericParameterAttributes.NotNullableValueTypeConstraint) != 0 && !argumentType.IsValueType)
						return MemberMatchValue.GenericMatch;

					// check new()
					if ((parameterType.GenericParameterAttributes & GenericParameterAttributes.DefaultConstructorConstraint) != 0)
					{
						// check default ctor
						if (argumentType.FindDeclaredConstructor(ReflectionFlag.Public | ReflectionFlag.Instance, Type.EmptyTypes) == null)
							return MemberMatchValue.Exact;
					}

					// no contraints, all is allowed
					if (typeConstraints.Length == 0)
						return MemberMatchValue.GenericMatch;

					// search for the constraint
					var noneExactMatch = false;
					var t = argumentType.AsType();
					for (int i = 0; i < typeConstraints.Length; i++)
					{
						if (typeConstraints[i] == t)
							return MemberMatchValue.Exact;
						else if (typeConstraints[i].GetTypeInfo().IsAssignableFrom(argumentType))
							noneExactMatch = true;
					}

					return noneExactMatch ? MemberMatchValue.ImplicitConversion : MemberMatchValue.Explicit;
					#endregion
				}
				else if (TypesMatch(parameterType.AsType(), argumentType.AsType(), out var match, toTypeIsParamsArray: parameterIsParamsArray)) // is at least assignable
				{
					return match;
				}
				else
				{
					var implicitMatch = false;
					var isExactFrom = false;
					var isExactTo = false;
					FindConvertOperator(argumentType.AsType(), parameterType.AsType(), null, ref implicitMatch, ref isExactFrom, ref isExactTo);
					return implicitMatch ? MemberMatchValue.ImplicitConversion : MemberMatchValue.None;
				}
			} // func GetParameterMatch
		} // class MemberMatch

		#endregion

		public static MethodInfo FindMethod<TARG>(IEnumerable<MethodInfo> members, CallInfo callInfo, TARG instance, TARG[] arguments, Func<TARG, Type> getType, bool isMemberCall)
			where TARG : class
		{
			var mi = FindMember(members, callInfo, arguments, getType, isMemberCall);

			// create a non generic version
			if (mi != null && mi.ContainsGenericParameters)
			{
				if (isMemberCall && instance != null && mi.IsStatic)
					arguments = (new TARG[] { instance }).Concat(arguments).ToArray();
				mi = MakeNonGenericMethod(mi, arguments, getType);
			}

			return mi;
		} // func FindMethod


		public static TMEMBERTYPE FindMember<TMEMBERTYPE, TARG>(IEnumerable<TMEMBERTYPE> members, CallInfo callInfo, TARG[] arguments, Func<TARG, Type> getType, bool isMemberCall)
			where TMEMBERTYPE : MemberInfo
			where TARG : class
		{
			var filteredMembers = GetMatchingOverloads(members, arguments, isMemberCall);
			var bestOverload = SelectBestOverload(filteredMembers, arguments, isMemberCall, callInfo, getType);
			return bestOverload;
		} // func FindMember

		/// <summary>
		/// Select the best overload from a set of callable candidate overloads.
		/// </summary>
		/// <typeparam name="TMEMBERTYPE">The type of the <paramref name="overloadCandidates"/>.</typeparam>
		/// <typeparam name="TARG">The type of the <paramref name="arguments"/>.</typeparam>
		/// <param name="overloadCandidates"></param>
		/// <param name="arguments"></param>
		/// <param name="isMemberCall"><see langword="true"/> if this is a lua member call, otherwise <see langword="false"/>.</param>
		/// <param name="callInfo"></param>
		/// <param name="getType">A <see cref="Func{TARG, Type}"/> that gets the type given one of the arguments in <paramref name="arguments"/>. </param>
		/// <returns>The best matching overload in <paramref name="overloadCandidates"/>.</returns>
		private static TMEMBERTYPE SelectBestOverload<TMEMBERTYPE, TARG>(IEnumerable<TMEMBERTYPE> overloadCandidates,
			TARG[] arguments, bool isMemberCall, CallInfo callInfo, Func<TARG, Type> getType)
			where TMEMBERTYPE : MemberInfo where TARG : class
		{
			var unboundedArguments = callInfo.ArgumentNames.Count == 0 && arguments.Length > 0 ? arguments[arguments.Length - 1] is LuaResult { Count: > 1 } : false;
			var memberMatch = new MemberMatch<TMEMBERTYPE, TARG>(callInfo, arguments, getType);
			
			var bestSoFar = GetBestOverload(overloadCandidates);

			var currentMember = bestSoFar?.CurrentMember;
			Debug.WriteLine(currentMember is null ? "NO MATCH" : $"USED: {currentMember}");

			return currentMember;

			// GetBestOverload creates new MemberMatchInfo objects, and uses the Min algorithm to compare them and select the smallest one, according to the CompareTo method.
			MemberMatchInfo<TMEMBERTYPE> GetBestOverload(IEnumerable<TMEMBERTYPE> memberInfos) => memberInfos.Select(CreateMemberMatchInfo).Min();

			// CreateMemberMatchInfo creates a new MemberMatchInfo object, given an possible overload MemberInfo.
			MemberMatchInfo<TMEMBERTYPE> CreateMemberMatchInfo(TMEMBERTYPE member)
			{
				var match = new MemberMatchInfo<TMEMBERTYPE>(unboundedArguments, arguments.Length);
				memberMatch.Reset(member, isMemberCall, match);
				return match;
			}
		}

		/// <summary>
		/// Get all members that are invokable given the arguments.
		/// </summary>
		/// <typeparam name="TMEMBERTYPE">The type of the <paramref name="candidateOverloads"/>. A Subclass of <see cref="MemberInfo"/></typeparam>
		/// <typeparam name="TARG">The type of the <paramref name="arguments"/></typeparam>
		/// <param name="candidateOverloads">A set of members that are potentially callable given the <paramref name="arguments"/>.</param>
		/// <param name="arguments">The arguments to use to determine if the members are callable.</param>
		/// <param name="isMemberCall"></param>
		/// <returns></returns>
		internal static IEnumerable<TMEMBERTYPE> GetMatchingOverloads<TMEMBERTYPE, TARG>(IEnumerable<TMEMBERTYPE> candidateOverloads, TARG[] arguments,
			bool isMemberCall) where TMEMBERTYPE : MemberInfo where TARG : class
		{
#if DEBUG
			var candidateMembers = candidateOverloads.ToList();
			IEnumerable<TMEMBERTYPE> filteredMembers = candidateMembers;
			for (int i = candidateMembers.Count - 1; i >= 0; i--)
			{
				var candidateMember = candidateMembers[i];
				if (!IsMemberCandidate(candidateMember, arguments, isMemberCall))
				{
					candidateMembers.RemoveAt(i);
				}
			}
#else
			var filteredMembers = members.Where(c => IsMemberCandidate(c, arguments, isMemberCall));
#endif
			return filteredMembers.Distinct();
		}

		/// <summary>
		/// Determines if the <paramref name="candidateMember"/> is a possible to call given the <paramref name="arguments"/>.
		/// </summary>
		/// <typeparam name="TMEMBERTYPE">The type of the candidate member.</typeparam>
		/// <typeparam name="TARG">The type of the arguments</typeparam>
		/// <param name="candidateMember">The <see cref="MemberInfo"/> to check for if it can be invoked.</param>
		/// <param name="arguments">The arguments used to determine if it is possible to invoke <paramref name="candidateMember"/>.</param>
		/// <param name="isMemberCall"></param>
		/// <returns><see langword="true"/> if <paramref name="candidateMember"/> can be invoked with <paramref name="arguments"/>, otherwise <see langword="false"/>.</returns>
		internal static bool IsMemberCandidate<TMEMBERTYPE, TARG>(TMEMBERTYPE candidateMember, TARG[] arguments, bool isMemberCall) where TMEMBERTYPE : MemberInfo where TARG : class
		{
			var parameterInfo = GetMemberParameter(candidateMember, isMemberCall);
			var argumentsLength = arguments.Length;
			var parameterInfoLength = parameterInfo.Length;
			if (parameterInfoLength == 0 && argumentsLength > 0) return false;
			
			if (argumentsLength == 0)
			{
				return parameterInfoLength switch
				{
					0 => true,
					1 when parameterInfo[0] is { } paramInfo && (paramInfo.IsOptional || paramInfo.IsParamArray()) => true,
					_ => false
				};
			}

			var luaResult = arguments[argumentsLength - 1] as LuaResult;
			var lastIsLuaResult = luaResult is not null;

			
			if (argumentsLength <= parameterInfoLength)
			{
				for (int i = 0; i < argumentsLength; i++)
				{
					var paramInfo = parameterInfo[i];
					var argInfo = arguments[i];
					var isParamArrayParameter = i == parameterInfoLength - 1 && paramInfo.ParameterType.IsArray && paramInfo.IsParamArray();
					
					if (!IsPossibleParameter(paramInfo.ParameterType, argInfo, isParamArrayParameter))
						return false;
				}
				// all parameters checked out. We have an exact match, or a match with implicit conversions
				
				if (argumentsLength == parameterInfoLength) return true;


				// OK if the remaining parameters are out or optional
				for (int i = argumentsLength; i < parameterInfoLength; i++)
				{
					if (!(parameterInfo[i].IsOut || parameterInfo[i].IsOptional)) return false;
				}

				return true;
			}
			
			if (argumentsLength > parameterInfoLength)
			{
				if (parameterInfo[parameterInfoLength - 1].GetCustomAttribute<ParamArrayAttribute>() is null)
				{
					// Error, we don't have a params array.
					return false;
				}
				// Check if the arguments matches the available parameters
				for (int i = 0; i < parameterInfoLength; i++)
				{
					var paramInfo = parameterInfo[i];
					var argInfo = arguments[i];

					if (i == parameterInfoLength - 1)
					{
						// params array. Ensure remaining arguments can be converted to the params array type
						var parameterType = paramInfo.ParameterType.GetElementType()!;
						if (parameterType == typeof(object)) return true; // everything can be converted to object
						
						for (int j = i; j < argumentsLength; j++)
						{
							if (!IsPossibleParameter(parameterType, argInfo)) return false;
						}

						return true;
					}
					else
					{
						if (!IsPossibleParameter(paramInfo.ParameterType, argInfo)) return false;
					}
				}

				return true;
			}

			return false;

			static bool IsPossibleParameter(Type parameterType, object arg, bool isParamArrayParameter = false)
			{
				if (parameterType.IsGenericParameter || parameterType == typeof(object)) return true;

				var argType = arg switch
				{
					DynamicMetaObject dmo => dmo.LimitType,
					Expression ex => ex.Type,
					Type t => t,
					_ => arg.GetType()
				};
				if (argType == parameterType || argType == typeof(LuaResult)) return true;

				if (isParamArrayParameter)
				{
					if (IsPossibleParameter(parameterType.GetElementType(), arg, false)) return true;
				}

				if (parameterType.IsByRef && parameterType.GetElementType() == argType) return true;
				if (GenericTypesMatches(argType, parameterType)) return true;

				if (argType.IsPrimitive && (parameterType.IsPrimitive || parameterType == typeof(string)))
				{
					return true;
				}

				if (parameterType.IsArray && arg is LuaResult)
				{
					return true;
				}

				return parameterType.IsAssignableFrom(argType)
			       || argType.HasImplicitConversionToType(parameterType)
			       || parameterType.HasImplicitConversionFromType(argType)
			       || argType.CanConvertTo(parameterType)
			       || parameterType.CanConvertFrom(argType);

			}

			static bool GenericTypesMatches(Type sourceType, Type destinationType)
			{
				if (sourceType.IsGenericType && destinationType.IsGenericType)
				{
					var argTypeGenericTypeArguments = sourceType.GenericTypeArguments;
					var parameterTypeGenericTypeArguments = destinationType.GenericTypeArguments;
					if (argTypeGenericTypeArguments.Length == parameterTypeGenericTypeArguments.Length)
					{
						for (int i = 0; i < argTypeGenericTypeArguments.Length; i++)
						{
							var paramType = parameterTypeGenericTypeArguments[i];
							var argType = argTypeGenericTypeArguments[i];
							if (paramType.IsGenericParameter || paramType == argType)
								continue;
							return false;
						}

						return true;
					}
				}

				return false;
			}
		}

	
		private static MethodInfo MakeNonGenericMethod<TARG>(MethodInfo mi, TARG[] arguments, Func<TARG, Type> getType)
			where TARG : class
		{
			var parameterInfo = mi.GetParameters();
			var genericArguments = mi.GetGenericArguments();
			var genericParameter = new Type[genericArguments.Length];

			for (var i = 0; i < genericArguments.Length; i++)
			{
				Type t = null;
				var currentArgument = genericArguments[i];

				// look for the typ
				for (int j = 0; j < parameterInfo.Length; j++)
				{
					t = CombineType(t, FindGenericParameterType(parameterInfo[j].ParameterType, currentArgument, getType(arguments[j])));
				}

				genericParameter[i] = t;
			}

			if (genericParameter.Any(t => t == null))
				throw new ArgumentException(String.Format("Can not create method for generic {0}:{1} [{2}].", mi.DeclaringType.GetType().FullName, mi.Name, mi.ToString()));

			return mi.MakeGenericMethod(genericParameter);
		} // func MakeNonGenericMethod

		private static Type FindGenericParameterType(Type parameterType, Type genericArgumentType, Type actualArgumentType)
		{
			if (parameterType == genericArgumentType)
				return actualArgumentType;
			else if (parameterType.IsConstructedGenericType)
			{
				var parameterGenericTypeDefinition = parameterType.GetGenericTypeDefinition();

				for (var i = 0; i < parameterType.GenericTypeArguments.Length; i++)
				{
					// is the actualArgumentType the same generic type
					var r = FindGenericParameterType(parameterType, genericArgumentType, parameterGenericTypeDefinition, i, actualArgumentType);
					if (r != null)
						return r;

					// find a implemented interface
					foreach (var interfaceType in actualArgumentType.GetTypeInfo().ImplementedInterfaces)
					{
						r = FindGenericParameterType(parameterType, genericArgumentType, parameterGenericTypeDefinition, i, interfaceType);
						if (r != null)
							return r;
					}
				}

				return null;
			}
			else
				return null;
		} // proc FindGenericParameterType

		private static Type FindGenericParameterType(Type parameterType, Type genericArgumentType, Type parameterGenericTypeDefinition, int i, Type testType)
		{
			if (testType.IsConstructedGenericType && parameterGenericTypeDefinition == testType.GetGenericTypeDefinition())
				return FindGenericParameterType(parameterType.GenericTypeArguments[i], genericArgumentType, testType.GenericTypeArguments[i]);
			return null;
		} // func FindGenericParameterType

		private static Type CombineType(Type t, Type type)
		{
			if (t == null)
				return type;
			else if (type == null)
				return t;
			else if (t.GetTypeInfo().IsAssignableFrom(type.GetTypeInfo()))
				return t;
			else if (type.GetTypeInfo().IsAssignableFrom(t.GetTypeInfo()))
				return type;
			else
				return typeof(object);
		} // func CombineType

#endregion

		public static bool TryInvokeMember<TARG>(Lua lua, LuaType targetType, TARG target, CallInfo callInfo, TARG[] arguments, string memberName, bool ignoreCase, Func<TARG, Expression> getExpr, Func<TARG, Type> getType, bool allowDynamic, out Expression result)
			where TARG : class
		{
			// find the member
			var memberInfo = LuaEmit.FindMember(
				targetType.EnumerateMembers<MemberInfo>(GetMethodEnumeratorType(target, getExpr, getType), memberName, ignoreCase),
				callInfo, arguments, getType, target != null
			);

			if (memberInfo == null) // no member found
			{
				result = null;
				return false;
			}
			else // bind member
			{
				if (memberInfo is MethodInfo)
				{
					result = InvokeMethod(lua, (MethodInfo)memberInfo, target, callInfo, arguments, getExpr, getType, allowDynamic);
					return true;
				}
				else if (memberInfo is PropertyInfo)
				{
					var pi = (PropertyInfo)memberInfo;

					if (!pi.CanRead)
						throw new LuaEmitException(LuaEmitException.CanNotReadMember, targetType.FullName, memberName);

					var instance = target == null ? null : Lua.EnsureType(getExpr(target), getType(target));

					var parameterInfo = pi.GetIndexParameters();
					if (parameterInfo != null && parameterInfo.Length > 0)
					{
						result = BindParameter<TARG>(lua,
							convertedArguments => Expression.Property(instance, pi, convertedArguments),
							parameterInfo,
							callInfo,
							arguments,
							getExpr,
							getType,
							allowDynamic
						);
						return true;
					}
					else
					{
						result = Expression.Property(instance, pi);
						return true;
					}
				}
				else if (memberInfo is FieldInfo)
				{
					var instance = target == null ? null : Lua.EnsureType(getExpr(target), getType(target));
					result = Expression.Field(instance, (FieldInfo)memberInfo);
					return true;
				}
				else if (memberInfo is EventInfo)
				{
					var instance = target == null ? Expression.Default(typeof(object)) : Lua.EnsureType(getExpr(target), getType(target));
					result = Expression.New(Lua.EventConstructorInfo,
						instance,
						Expression.Constant((EventInfo)memberInfo)
					);
					return true;
				}
				else if (memberInfo is TypeInfo)
				{
					result = Expression.Call(Lua.TypeGetTypeMethodInfoArgType, Expression.Constant(((TypeInfo)memberInfo).AsType()));
					return true;
				}
				else
				{
					result = null;
					return false;
				}
			}
		} // func TryInvokeMember

		internal static Expression InvokeMethod<TARG>(Lua lua, MethodInfo methodInfo, TARG target, CallInfo callInfo, TARG[] arguments, Func<TARG, Expression> getExpr, Func<TARG, Type> getType, bool allowDynamic)
			where TARG : class
		{
			Expression result;
			Func<Expression[], Expression> emitCall;
			if (target != null) // member call
			{
				if (methodInfo.IsStatic) // extension method
				{
					arguments = (new TARG[] { target }).Concat(arguments).ToArray();
					if (methodInfo.ContainsGenericParameters)
						methodInfo = MakeNonGenericMethod(methodInfo, arguments, getType);
					emitCall = convertedArguments => Expression.Call(null, methodInfo, convertedArguments);
				}
				else
				{
					if (methodInfo.ContainsGenericParameters)
						methodInfo = MakeNonGenericMethod(methodInfo, arguments, getType);
					var targetExpression = Lua.EnsureType(getExpr(target), getType(target));
					emitCall = convertedArguments => Expression.Call(targetExpression, methodInfo, convertedArguments);
				}
			}
			else
			{
				if (methodInfo.ContainsGenericParameters)
					methodInfo = MakeNonGenericMethod(methodInfo, arguments, getType);
				emitCall = convertedArguments => Expression.Call(null, methodInfo, convertedArguments);
			}

			result = BindParameter(lua,
				 emitCall,
				 methodInfo.GetParameters(),
				 callInfo,
				 arguments,
				 getExpr,
				 getType,
				 allowDynamic
			 );
			return result;
		} // func InvokeMethod

#endregion

		#region -- Reflection Helper --------------------------------------------------

		private static bool CheckArguments(ParameterInfo[] args, Type[] arguments)
		{
			if (args.Length != arguments.Length)
				return false;
			else
			{
				for (var i = 0; i < args.Length; i++)
				{
					if (args[i].ParameterType != arguments[i])
						return false;
				}
			}
			return true;
		} // func CheckArguments

		private static IEnumerable<T> FilterName<T>(IEnumerable<T> list, string sName, ReflectionFlag flags)
			where T : MemberInfo
		{
			var stringComparison = (flags & ReflectionFlag.IgnoreCase) != 0 ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
			return list.Where(mi => String.Compare(mi.Name, sName, stringComparison) == 0);
		} // func FilterName

		private static IEnumerable<T> FilterNeeded<T>(IEnumerable<T> list, ReflectionFlag flags, ReflectionFlag mask, Func<IEnumerable<T>, ReflectionFlag, IEnumerable<T>> filter)
			where T : MemberInfo
		{
			var test = (flags & mask);
			if (test != 0 && test != mask)
				return filter(list, flags);
			else
				return list;
		} // func FilterNeeded

		private static T GetOneResult<T>(TypeInfo ti, string name, ReflectionFlag flags, IEnumerable<T> list, [CallerMemberName] string caller = null)
		{
			if ((flags & ReflectionFlag.NoException) != 0)
				return list.FirstOrDefault();
			else
			{
				using (var e = list.GetEnumerator())
				{
					if (e.MoveNext()) // first element for return
					{
						var miFind = e.Current;
						if (e.MoveNext())
							throw new ArgumentException(String.Format("{0} for {1}.{2}, is not unique.", caller, ti.Name, name));
						return miFind;
					}
					else
						throw new ArgumentException(String.Format("{0} failed for {1}.{2}.", caller, ti.Name, name));
				}
			}
		} // func GetOneResult

		public static FieldInfo FindDeclaredField(this TypeInfo ti, string sName, ReflectionFlag flags)
		{
			var fields = FilterName(ti.DeclaredFields, sName, flags);

			// Static member?
			fields = FilterNeeded(fields, flags, ReflectionFlag.Instance | ReflectionFlag.Static, (l, f) => (f & ReflectionFlag.Static) != 0 ? l.Where(fi => fi.IsStatic) : l.Where(fi => !fi.IsStatic));
			// Public
			fields = FilterNeeded(fields, flags, ReflectionFlag.Public | ReflectionFlag.NonPublic, (l, f) => (f & ReflectionFlag.Public) != 0 ? l.Where(fi => fi.IsPublic) : l.Where(fi => !fi.IsPublic));

			return GetOneResult(ti, sName, flags, fields);
		} // func FindDeclaredField

		public static PropertyInfo FindDeclaredProperty(this TypeInfo ti, string sName, ReflectionFlag flags, params Type[] arguments)
		{
			var properties = FilterName(ti.DeclaredProperties, sName, flags);

			// Static member?
			properties = FilterNeeded(properties, flags, ReflectionFlag.Instance | ReflectionFlag.Static, (l, f) => (f & ReflectionFlag.Static) != 0 ? l.Where(pi => pi.GetMethod.IsStatic) : l.Where(pi => !pi.GetMethod.IsStatic));
			// Public
			properties = FilterNeeded(properties, flags, ReflectionFlag.Public | ReflectionFlag.NonPublic, (l, f) => (f & ReflectionFlag.Public) != 0 ? l.Where(pi => pi.GetMethod.IsPublic) : l.Where(pi => !pi.GetMethod.IsPublic));

			// Arguments
			if (arguments.Length > 0)
				properties = properties.Where(pi => CheckArguments(pi.GetIndexParameters(), arguments));

			return GetOneResult(ti, sName, flags, properties);
		} // func FindDeclaredProperty

		public static ConstructorInfo FindDeclaredConstructor(this TypeInfo ti, ReflectionFlag flags, params Type[] arguments)
		{
			var constructors = ti.DeclaredConstructors;

			// Public
			constructors = FilterNeeded(constructors, flags, ReflectionFlag.Public | ReflectionFlag.NonPublic, (l, f) => (f & ReflectionFlag.Public) != 0 ? l.Where(ci => ci.IsPublic) : l.Where(ci => !ci.IsPublic));

			// Arguments
			if ((flags & ReflectionFlag.NoArguments) == 0)
				constructors = constructors.Where(ci => CheckArguments(ci.GetParameters(), arguments));

			return GetOneResult(ti, "ctor", flags, constructors);
		} // func FindDeclaredConstructor

		public static MethodInfo FindDeclaredMethod(this TypeInfo ti, string sName, ReflectionFlag flags, params Type[] arguments)
		{
			var methods = FilterName(ti.DeclaredMethods, sName, flags);

			// Static member?
			methods = FilterNeeded(methods, flags, ReflectionFlag.Instance | ReflectionFlag.Static, (l, f) => (f & ReflectionFlag.Static) != 0 ? l.Where(mi => mi.IsStatic) : l.Where(mi => !mi.IsStatic));
			// Public
			methods = FilterNeeded(methods, flags, ReflectionFlag.Public | ReflectionFlag.NonPublic, (l, f) => (f & ReflectionFlag.Public) != 0 ? l.Where(mi => mi.IsPublic) : l.Where(mi => !mi.IsPublic));
			// Arguments
			if ((flags & ReflectionFlag.NoArguments) == 0)
				methods = methods.Where(mi => CheckArguments(mi.GetParameters(), arguments));

			return GetOneResult(ti, sName, flags, methods);
		} // func FindDeclaredMethod

		#endregion
	} // class LuaEmit

#endregion
}