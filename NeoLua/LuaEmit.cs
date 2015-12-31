using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace Neo.IronLua
{
	#region -- class LuaEmitException ---------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
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

		private int iCode;

		public LuaEmitException(int iCode, object arg0 = null, object arg1 = null, object arg2 = null)
			: base(GetMessageText(iCode, arg0, arg1, arg2))
		{
			this.iCode = iCode;
		} // ctor

		public int Code { get { return iCode; } }

		public static string GetMessageText(int iCode, object arg0 = null, object arg1 = null, object arg2 = null)
		{
			switch (iCode)
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
					return String.Format("Code={0};Arg0={1};Arg1={2}", iCode, arg0, arg1);
			}
		} // func GetMessageText
	} // class LuaEmitException

	#endregion

	#region -- enum LuaEmitTypeCode -----------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
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

	#region -- class LuaEmit ------------------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	internal static class LuaEmit
	{
		public const string csImplicit = "op_Implicit";
		public const string csExplicit = "op_Explicit";

		private static readonly TypeInfo DynamicMetaObjectProviderTypeInfo = typeof(IDynamicMetaObjectProvider).GetTypeInfo();

		#region -- IsDynamic, IsArithmetic, ... -------------------------------------------

		public static bool IsDynamicType(Type type)
		{
			return type == typeof(object) || DynamicMetaObjectProviderTypeInfo.IsAssignableFrom(type.GetTypeInfo());
		} // func IsDynamicType

		private static TypeInfo UnpackTypeInfo(Type type)
		{
			var ti = type.GetTypeInfo();
			if (ti.IsGenericType && ti.GetGenericTypeDefinition() == typeof(Nullable<>))
				ti = ti.GenericTypeArguments[0].GetTypeInfo();
			if (ti.IsEnum)
				ti = Enum.GetUnderlyingType(ti.AsType()).GetTypeInfo();
			return ti;
		} // func UnpackType

		public static LuaEmitTypeCode GetTypeCode(Type type)
		{
			return GetTypeCode(UnpackTypeInfo(type));
		} // func GetTypeCode

		private static LuaEmitTypeCode GetTypeCode(TypeInfo ti)
		{
			Type type = ti.AsType();
			switch (type.Name[0])
			{
				case 'B':
					if (type == typeof(Boolean))
						return LuaEmitTypeCode.Boolean;
					else if (type == typeof(Byte))
						return LuaEmitTypeCode.Byte;
					else
						return LuaEmitTypeCode.Object;
				case 'C':
					return type == typeof(Char) ? LuaEmitTypeCode.Char : LuaEmitTypeCode.Object;
				case 'D':
					if (type == typeof(Double))
						return LuaEmitTypeCode.Double;
					else if (type == typeof(Decimal))
						return LuaEmitTypeCode.Decimal;
					else if (type == typeof(DateTime))
						return LuaEmitTypeCode.DateTime;
					else
						return LuaEmitTypeCode.Object;
				case 'I':
					if (type == typeof(Int32))
						return LuaEmitTypeCode.Int32;
					else if (type == typeof(Int16))
						return LuaEmitTypeCode.Int16;
					else if (type == typeof(Int64))
						return LuaEmitTypeCode.Int64;
					else
						return LuaEmitTypeCode.Object;
				case 'S':
					if (type == typeof(String))
						return LuaEmitTypeCode.String;
					else if (type == typeof(SByte))
						return LuaEmitTypeCode.SByte;
					else if (type == typeof(Single))
						return LuaEmitTypeCode.Single;
					else
						return LuaEmitTypeCode.Object;
				case 'U':
					if (type == typeof(UInt32))
						return LuaEmitTypeCode.UInt32;
					else if (type == typeof(UInt16))
						return LuaEmitTypeCode.UInt16;
					else if (type == typeof(UInt64))
						return LuaEmitTypeCode.UInt64;
					else
						return LuaEmitTypeCode.Object;
				default:
					return LuaEmitTypeCode.Object;
			}
		} // func GetTypeCode

		internal static bool TypesMatch(Type typeTo, Type typeFrom, out bool lExact)
		{
			if (typeTo == typeFrom)
			{
				lExact = true;
				return true;
			}

			var tiTo = UnpackTypeInfo(typeTo);
			var tiFrom = UnpackTypeInfo(typeFrom);

			if (tiTo == tiFrom)
			{
				lExact = true;
				return true;
			}
			else if (tiTo.IsAssignableFrom(tiFrom))
			{
				lExact = false;
				return true;
			}
			else
			{
				var tcTo = GetTypeCode(tiTo);
				var tcFrom = GetTypeCode(tiFrom);

				lExact = false;

				if (tcTo == LuaEmitTypeCode.String)
					return true;
				else if (tcTo >= LuaEmitTypeCode.SByte && tcTo <= LuaEmitTypeCode.Double &&
							(tcFrom >= LuaEmitTypeCode.SByte && tcFrom <= tcTo || tcTo == LuaEmitTypeCode.Single && tcFrom == LuaEmitTypeCode.Double)) // exception for single -> double
					return true;
				else if (tcFrom == LuaEmitTypeCode.String &&
					tcTo >= LuaEmitTypeCode.SByte && tcTo <= LuaEmitTypeCode.Double)
					return true;

				return false;
			}
		} // bool TypesMatch

		private static bool IsArithmeticType(Type type)
		{
			return IsArithmeticType(GetTypeCode(type));
		} // func IsArithmeticType


		private static bool IsArithmeticType(TypeInfo ti)
		{
			return IsArithmeticType(GetTypeCode(ti));
		} // func IsArithmeticType

		private static bool IsArithmeticType(LuaEmitTypeCode typeCode)
		{
			return IsIntegerType(typeCode) || IsFloatType(typeCode);
		} // func IsArithmeticType

		public static bool IsIntegerType(LuaEmitTypeCode typeCode)
		{
			return (LuaEmitTypeCode.IntegerFlag & typeCode) != 0;
		} // func IsIntegerType

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

		internal static bool IsConvertOperator(MethodInfo methodInfo)
		{
			return methodInfo.IsPublic && methodInfo.IsStatic && methodInfo.IsSpecialName && (methodInfo.Name == csImplicit || methodInfo.Name == csExplicit);
		} // func IsConvertOperator

		private static bool TryConvertType(Lua runtime, Type typeTo, ref Expression expr, ref Type exprType)
		{
			bool lExact;
			if (TypesMatch(typeTo, exprType, out lExact)) // is the type compitible
			{
				expr = Convert(runtime, expr, exprType, typeTo, false);
				exprType = typeTo;
				return true;
			}

			// search for conversion operator
			TypeInfo tiTo = typeTo.GetTypeInfo();
			TypeInfo tiExpr = exprType.GetTypeInfo();

			bool lExactTo = false;
			bool lExactFrom = false;
			bool lImplicit = false;
			MethodInfo miConvert = FindConvertMethod(
				tiTo.DeclaredMethods.Where(IsConvertOperator),
				exprType, typeTo,
				ref lImplicit, ref lExactFrom, ref lExactTo);

			if (!lImplicit || !lExactFrom || !lExactTo)
				miConvert = FindConvertMethod(
					tiExpr.DeclaredMethods.Where(IsConvertOperator),
					exprType, typeTo,
					ref lImplicit, ref lExactFrom, ref lExactTo);

			if (miConvert == null)
				return false;
			else
			{
				if (expr.Type != exprType)
					expr = Expression.Convert(expr, exprType); // unbox

				Type typeParam = miConvert.GetParameters()[0].ParameterType;
				if (typeParam != exprType)
					expr = Expression.Convert(expr, exprType);

				expr = Expression.Convert(expr, miConvert.ReturnType, miConvert);

				if (typeTo != expr.Type)
					expr = Expression.Convert(expr, typeTo);

				exprType = typeTo;
				return true;
			}
		} // func TryConvertType

		public static MethodInfo FindConvertMethod(IEnumerable<MethodInfo> methods, Type typeFrom, Type typeTo, ref bool lImplicit, ref bool lExactFrom, ref bool lExactTo)
		{
			MethodInfo miCurrent = null;
			foreach (var mi in methods)
			{
				ParameterInfo[] parameters = mi.GetParameters();

				bool lTestImplicit;
				bool lTestExactTo;
				bool lTestExactFrom;

				if (parameters.Length != 1)
					continue;

				// test name
				if (mi.Name == csImplicit)
					lTestImplicit = true;
				else if (mi.Name == csExplicit)
					lTestImplicit = false;
				else
					continue;

				// parameter ergo from
				if (!TypesMatch(parameters[0].ParameterType, typeFrom, out lTestExactFrom))
					continue;
				if (!TypesMatch(typeTo, mi.ReturnType, out lTestExactTo))
					continue;

				if (lTestExactTo)
				{
					if (lTestExactFrom)
					{
						if (lTestImplicit) // perfect match
						{
							lExactTo =
								lExactFrom =
								lImplicit = true;
							miCurrent = mi;
							break;
						}
						else // nearly perfect
						{
							lExactTo =
								lExactFrom = true;
							lImplicit = false;
							miCurrent = mi;
						}
					}
					else if (!lExactFrom)
					{
						lExactTo = true;
						lExactFrom = false;
						lImplicit = lTestImplicit;
						miCurrent = mi;
					}
				}
				else if (miCurrent == null) // no match until now, take first that fits
				{
					lExactFrom = lTestExactFrom;
					lExactTo = lTestExactTo;
					lImplicit = lTestImplicit;
					miCurrent = mi;
				}
			}

			return miCurrent;
		} // func FindConvertMethod

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

		internal static Expression[] CreateDynamicArgs<TARG>(Lua runtime, Expression instance, Type instanceType, TARG[] arguments, Func<TARG, Expression> getExpr, Func<TARG, Type> getType)
			where TARG : class
		{
			Expression[] dynArgs = new Expression[arguments.Length + 1];
			dynArgs[0] = Lua.EnsureType(instance, typeof(object));
			for (int i = 0; i < arguments.Length; i++)
				dynArgs[i + 1] = Convert(runtime, getExpr(arguments[i]), getType(arguments[i]), typeof(object), false);
			return dynArgs;
		} // func CreateDynamicArgs

		internal static Expression[] CreateDynamicArgs<TARG>(Lua runtime, Expression instance, Type instanceType, TARG[] arguments, TARG setTo, Func<TARG, Expression> getExpr, Func<TARG, Type> getType)
			where TARG : class
		{
			Expression[] dynArgs = new Expression[arguments.Length + 2];
			dynArgs[0] = Lua.EnsureType(instance, typeof(object));
			for (int i = 0; i < arguments.Length; i++)
				dynArgs[i + 1] = Convert(runtime, getExpr(arguments[i]), getType(arguments[i]), typeof(object), false);
			dynArgs[dynArgs.Length - 1] = Convert(runtime, getExpr(setTo), getType(setTo), typeof(object), false);
			return dynArgs;
		} // func CreateDynamicArgs

		#endregion

		#region -- Emit Convert -----------------------------------------------------------

		public static Expression Convert(Lua runtime, Expression expr, Type fromType, Type toType, bool lParse)
		{
			if (expr.Type != fromType) // convert the type to the correct limit type
				expr = Expression.Convert(expr, fromType);
			else if (lParse && runtime != null && (expr.Type == typeof(LuaResult) || expr.Type == typeof(object)))
			{
				if (expr.NodeType == ExpressionType.Dynamic) // avoid double dynamic converts or unessary LuaResult-Objects
				{
					DynamicExpression exprDynamic = (DynamicExpression)expr;
					if (exprDynamic.Binder is ConvertBinder) // unpack the expression
					{
						expr = exprDynamic.Arguments.First();
						if (expr.Type == toType) // repack to an object convert, because we do not want to let out the convert
							expr = DynamicExpression.Dynamic(runtime.GetConvertBinder(toType), toType, Convert(runtime, expr, expr.Type, typeof(object), false));
						fromType = expr.Type;
					}
				}
				else if (expr.NodeType == ExpressionType.New)
				{
					NewExpression exprNew = (NewExpression)expr;
					if (exprNew.Constructor == Lua.ResultConstructorInfoArg1 || exprNew.Constructor == Lua.ResultConstructorInfoArgN)
					{
						Expression exprTmp = exprNew.Arguments.First(); // only unpack, repack is not necessary
						if (exprTmp.NodeType != ExpressionType.Dynamic)
						{
							if (exprTmp.NodeType == ExpressionType.Convert && exprTmp.Type == typeof(object))
								exprTmp = ((UnaryExpression)exprTmp).Operand;

							expr = exprTmp;
							fromType = expr.Type;
						}
					}
				}
			}

			// check if we nead another conversion
			if (fromType == toType)
			{
				return expr;
			}
			else if (fromType == typeof(LuaResult)) // LuaResult -> convert first value
			{
				return GetResultExpression(runtime, expr, fromType, 0, toType, null, lParse);
			}
			else if (toType == typeof(LuaResult)) // type to LuaResult
			{
				return Expression.New(Lua.ResultConstructorInfoArg1, Convert(runtime, expr, fromType, typeof(object), false));
			}
			else if (runtime != null && lParse && IsDynamicType(fromType)) // dynamic type -> dynamic convert
			{
				return DynamicExpression.Dynamic(runtime.GetConvertBinder(toType), toType, Convert(null, expr, fromType, typeof(object), false));
			}
			else
			{
				TypeInfo fromTypeInfo = fromType.GetTypeInfo();
				TypeInfo toTypeInfo = toType.GetTypeInfo();
				if (toType == typeof(object) || toTypeInfo.IsAssignableFrom(fromTypeInfo)) // Type is assignable
				{
					return Expression.Convert(expr, toType);
				}
				else if (toType == typeof(bool)) // we need a true or false
				{
					if (fromType.GetTypeInfo().IsValueType)
						return Expression.Constant(true);
					else
						return BinaryOperationExpression(runtime, ExpressionType.NotEqual, expr, fromType, Expression.Constant(null, fromType), fromType, lParse);
				}
				else if (toType == typeof(string)) // convert to a string
				{
					if (fromType == typeof(bool))
						return Expression.Condition(expr, Expression.Constant("true"), Expression.Constant("false"));
					else
					{
						// try find a conversion
						var convertMethod = (from mi in fromTypeInfo.DeclaredMethods where IsConvertOperator(mi) && mi.ReturnType == typeof(string) select mi).FirstOrDefault();
						if (convertMethod != null)
							return Expression.Convert(expr, toType, convertMethod);

						// call convert to string
						return Expression.Call(Lua.ConvertToStringMethodInfo,
							Convert(runtime, expr, fromType, typeof(object), false),
							Expression.Property(null, Lua.CultureInvariantPropertyInfo)
						);
					}
				}
				else if (fromType == typeof(string) && IsArithmeticType(toType)) // we expect a string and have a number
				{
					return Convert(runtime, ParseNumberExpression(runtime, expr, fromType), typeof(object), toType, true); // allow dynamic converts
				}
				else if (fromType == typeof(string) && toType == typeof(char))
				{
					return Expression.Property(Convert(runtime, expr, fromType, fromType, false), Lua.StringItemPropertyInfo, Expression.Constant(0));
				}
				else if (toTypeInfo.BaseType == typeof(MulticastDelegate) && toTypeInfo.BaseType == fromTypeInfo.BaseType)
				{
					return Expression.Convert(
						Expression.Call(Lua.ConvertDelegateMethodInfo,
							Expression.Constant(toType, typeof(Type)),
							Convert(runtime, expr, fromType, typeof(Delegate), lParse)
						),
						toType
					);
				}
				else if (fromType.IsArray && toType.IsArray)
				{
					return Expression.Convert(Expression.Call(Lua.ConvertArrayMethodInfo, Convert(runtime, expr, fromType, typeof(Array), lParse), Expression.Constant(toType.GetElementType())), toType);
				}
				else
					try
					{
						return Expression.Convert(expr, toType);
					}
					catch
					{
						throw new LuaEmitException(LuaEmitException.ConversationNotDefined, fromType.Name, toType.Name);
					}
			}
		} // func Convert

		private static Expression ParseNumberExpression(Lua runtime, Expression expr1, Type type1)
		{
			return Expression.Call(Lua.ParseNumberMethodInfo, Convert(runtime, expr1, type1, typeof(string), false),
				Expression.Constant(runtime == null || (runtime.NumberType & (int)LuaFloatType.Mask) != (int)LuaFloatType.Float),
				Expression.Constant(true)
			);
		} // func ParseNumberExpression

		public static Expression GetResultExpression(Expression target, Type type, int iIndex)
		{
			return Expression.MakeIndex(
				Convert(null, target, type, typeof(LuaResult), false),
				Lua.ResultIndexPropertyInfo,
				new Expression[] { Expression.Constant(iIndex) }
			);
		} // func GetResultExpression

		public static Expression GetResultExpression(Lua runtime, Expression expr, Type type, int iIndex, Type typeReturn, Expression defaultReturn, bool lParse)
		{
			Expression exprGet = GetResultExpression(expr, type, iIndex);
			if (defaultReturn != null)
				exprGet = Expression.Coalesce(exprGet, defaultReturn);
			return Convert(runtime, exprGet, exprGet.Type, typeReturn, lParse);
		} // func GetResultExpression

		#endregion

		#region -- Emit Unary Operation ---------------------------------------------------

		public static Expression UnaryOperationExpression(Lua runtime, ExpressionType op, Expression expr, Type type, bool lParse)
		{
			if (op == ExpressionType.Not)
				return Expression.Not(Convert(runtime, expr, type, typeof(bool), lParse));
			else if (op == ExpressionType.ArrayLength)
			{
				if (type.IsArray)
					return Expression.ArrayLength(Convert(runtime, expr, type, type, false));
				else
					return Expression.Call(Lua.RuntimeLengthMethodInfo, Convert(runtime, expr, type, typeof(object), lParse));
			}
			else if (lParse && IsDynamicType(type))
				return DynamicExpression.Dynamic(runtime.GetUnaryOperationBinary(op), typeof(object), Convert(runtime, expr, type, typeof(object), lParse));
			else
			{
				switch (op)
				{
					case ExpressionType.OnesComplement:
						return UnaryOperationComplementExpression(runtime, expr, type, lParse);
					case ExpressionType.Negate:
						return UnaryOperationArithmeticExpression(runtime, op, expr, type, lParse);
					default:
						return Expression.MakeUnary(op, Convert(runtime, expr, type, type, lParse), type);
				}
			}
		} // func UnaryOperationExpression

		#endregion

		#region -- Emit Binary Operation --------------------------------------------------

		public static Expression BinaryOperationExpression(Lua runtime, ExpressionType op, Expression expr1, Type type1, Expression expr2, Type type2, bool lParse)
		{
			if (op == ExpressionType.OrElse || op == ExpressionType.AndAlso) // and, or are conditions no operations
				return BinaryOperationConditionExpression(runtime, op, expr1, type1, expr2, type2, lParse);
			else if (lParse && (IsDynamicType(type1) || IsDynamicType(type2))) // is one of the type a dynamic type, than make a dynamic expression
				return BinaryOperationDynamicExpression(runtime, op, expr1, type1, expr2, type2);
			else if (op == ExpressionType.Power)
			{
				if (!TryConvertType(runtime, typeof(double), ref expr1, ref type1))
					throw new LuaEmitException(LuaEmitException.ConversationNotDefined, type1.Name, typeof(double).Name);
				else if (!TryConvertType(runtime, typeof(double), ref expr2, ref type2))
					throw new LuaEmitException(LuaEmitException.ConversationNotDefined, type2.Name, typeof(double).Name);
				else
					return Expression.MakeBinary(op, expr1, expr2);
			}
			else
				return BinaryOperationArithmeticExpression(runtime, op, expr1, type1, expr2, type2, lParse);
		} // func BinaryOperationExpression

		#endregion

		#region -- BinaryOperationDynamicExpression ---------------------------------------

		private static Expression BinaryOperationDynamicExpression(Lua runtime, ExpressionType op, Expression expr1, Type type1, Expression expr2, Type type2)
		{
			return DynamicExpression.Dynamic(runtime.GetBinaryOperationBinder(op), typeof(object),
				Convert(runtime, expr1, type1, typeof(object), false),
				Convert(runtime, expr2, type2, typeof(object), false));
		} // func BinaryOperationDynamicExpression

		#endregion

		#region -- Emit Binary Condition Operator -----------------------------------------

		private static Expression BinaryOperationConditionExpression(Lua runtime, ExpressionType op, Expression expr1, Type type1, Expression expr2, Type type2, bool lParse)
		{
			Type typeOp;
			bool lExact;
			if (type1 == type2)
				typeOp = type1;
			else if (TypesMatch(type1, type2, out lExact))
				typeOp = type1;
			else if (TypesMatch(type2, type1, out lExact))
				typeOp = type2;
			else
				typeOp = typeof(object);

			// create condition
			Type typeVariable = expr1.Type;
			if (typeVariable == typeof(LuaResult))
				typeVariable = typeof(object);

			ParameterExpression exprTmp = Expression.Variable(typeVariable, "#tmp");
			Expression exprCondition;

			// Create a condition to follow lua language rules
			if (op == ExpressionType.AndAlso)
			{
				exprCondition = Expression.Condition(
					Convert(runtime, exprTmp, exprTmp.Type, typeof(bool), lParse),
					Convert(runtime, expr2, type2, typeOp, lParse),
					Convert(runtime, exprTmp, exprTmp.Type, typeOp, lParse)
				);
			}
			else if (op == ExpressionType.OrElse)
			{
				exprCondition = Expression.Condition(
					Convert(runtime, exprTmp, exprTmp.Type, typeof(bool), lParse),
					Convert(runtime, exprTmp, exprTmp.Type, typeOp, lParse),
					Convert(runtime, expr2, type2, typeOp, lParse)
				);
			}
			else
				throw new InvalidOperationException();

			return Expression.Block(typeOp,
				new ParameterExpression[] { exprTmp },
				Expression.Assign(exprTmp, Convert(runtime, expr1, expr1.Type, exprTmp.Type, lParse)),
				exprCondition
			);
		} // func BinaryOperationConditionExpression

		#endregion

		#region -- Emit Binary Compare Equalable helper -----------------------------------

		private static bool TestParameter(ParameterInfo[] parameters, params Type[] args)
		{
			if (parameters.Length != args.Length)
				return false;

			for (int i = 0; i < parameters.Length; i++)
				if (parameters[i].ParameterType != args[i])
					return false;

			return true;
		} // func TestParameter

		private static Expression BinaryOperationCompareToExpression(Lua runtime, Type compareInterface, ExpressionType op, Expression expr1, Type type1, Expression expr2, Type type2, bool lParse)
		{
			MethodInfo miMethod = (
				from mi in compareInterface.GetTypeInfo().DeclaredMethods
				where mi.IsPublic && !mi.IsStatic && mi.Name == "CompareTo" && mi.ReturnType == typeof(int)
				select mi
			).FirstOrDefault();

			return Expression.MakeBinary(op,
				Expression.Call(
					Convert(runtime, expr1, type1, compareInterface, false),
					miMethod,
					Convert(runtime, expr2, type2, miMethod.GetParameters()[0].ParameterType, lParse)
				),
				Expression.Constant(0, typeof(int)));
		} // func BinaryOperationCompareToExpression

		private static Expression BinaryOperationEqualableToExpression(Lua runtime, Type equalableInterface, ExpressionType op, Expression expr1, Type type1, Expression expr2, Type type2, bool lParse)
		{
			TypeInfo equalableInterfaceTypeInfo = equalableInterface.GetTypeInfo();
			Type typeParam = equalableInterfaceTypeInfo.GenericTypeArguments[0];

			MethodInfo miMethod = (
				from mi in equalableInterfaceTypeInfo.DeclaredMethods
				where mi.IsPublic && !mi.IsStatic && mi.Name == "Equals" && TestParameter(mi.GetParameters(), typeParam)
				select mi
			).FirstOrDefault();

			Expression expr = Expression.Call(
				Convert(runtime, expr1, type1, equalableInterface, false),
				miMethod,
				Convert(runtime, expr2, type2, typeParam, lParse)
			);
			return op == ExpressionType.NotEqual ? Expression.Not(expr) : expr;
		} // func BinaryOperationCompareToExpression

		private static Type GetComparableInterface(Type type1, Type Type2, ref bool lExact)
		{
			Type compareInterface = null;
			foreach (Type typeTest in type1.GetTypeInfo().ImplementedInterfaces)
			{
				if (compareInterface == null && typeTest == typeof(IComparable) && TypesMatch(type1, Type2, out lExact))
					return typeTest;
				else if (!lExact && IsGenericCompare(typeTest))
				{
					Type p = typeTest.GenericTypeArguments[0];
					if (TypesMatch(p, Type2, out lExact))
					{
						compareInterface = typeTest;
						if (lExact)
							break;
					}
				}
			}
			return compareInterface;
		} // func GetComparableInterface

		private static bool IsGenericCompare(Type type)
		{
			return type.IsConstructedGenericType && type.GetGenericTypeDefinition() == typeof(IComparable<>);
		} // func IsGenericCompare

		private static Type GetEqualableInterface(Type type1, Type Type2, ref bool lExact)
		{
			Type equalableInterface = null;
			foreach (Type typeTest in type1.GetTypeInfo().ImplementedInterfaces)
			{
				if (!lExact && typeTest.IsConstructedGenericType && typeTest.GetGenericTypeDefinition() == typeof(IEquatable<>))
				{
					Type p = typeTest.GenericTypeArguments[0];
					if (TypesMatch(p, Type2, out lExact))
					{
						equalableInterface = typeTest;
						if (lExact)
							break;
					}
				}
			}
			return equalableInterface;
		} // func GetEqualableInterface

		#endregion

		#region -- Emit Arithmetic Expression ---------------------------------------------

		private static Expression UnaryOperationComplementExpression(Lua runtime, Expression expr, Type type, bool lParse)
		{
			var tc = GetTypeCode(type);
			bool lIsArithmetic = IsArithmeticType(tc);

			if (lIsArithmetic) // simple arithmetic complement
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

				expr = Expression.OnesComplement(Convert(runtime, expr, type, typeOp, lParse));

				if (typeEnum != null)
					expr = Expression.Convert(expr, typeEnum);

				return expr;
				#endregion
			}

			#region -- find operator --
			var operators = type.GetRuntimeMethods().Where(mi => mi.Name == GetOperationMethodName(ExpressionType.OnesComplement) && mi.IsStatic);
			MethodInfo miOperator = FindMethod(operators, new CallInfo(1), new Type[] { type }, t => t, false);
			if (miOperator != null)
				return Expression.OnesComplement(Convert(runtime, expr, type, miOperator.GetParameters()[0].ParameterType, lParse), miOperator);
			#endregion

			#region -- inject convert --
			if (type == typeof(string))
				return DynamicExpression.Dynamic(runtime.GetUnaryOperationBinary(ExpressionType.OnesComplement), typeof(object), ParseNumberExpression(runtime, expr, type));
			#endregion

			#region -- try convert to integer --
			if (TryConvertType(runtime, LiftIntegerType(runtime, type), ref expr, ref type))
				return UnaryOperationComplementExpression(runtime, expr, type, lParse);
			#endregion

			throw new LuaEmitException(LuaEmitException.OperatorNotDefined, ExpressionType.OnesComplement, String.Empty, type.Name);
		} // func UnaryOperationComplementExpression

		private static Expression UnaryOperationNegateExpression(Lua runtime, Expression expr, Type type, bool lParse)
		{
			var tc = GetTypeCode(type);
			bool lIsArithmetic = IsArithmeticType(tc);

			if (lIsArithmetic) // simple arithmetic complement
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

				expr = Expression.OnesComplement(Convert(runtime, expr, type, LiftTypeSigned(tc, tc), lParse));

				if (typeEnum != null)
					expr = Expression.Convert(expr, typeEnum);

				return expr;
				#endregion
			}

			#region -- find operator --

			var operators = type.GetRuntimeMethods().Where(mi => mi.Name == GetOperationMethodName(ExpressionType.Negate) && mi.IsStatic);
			MethodInfo miOperator = FindMethod(operators, new CallInfo(1), new Type[] { type }, t => t, false);
			if (miOperator != null)
				return Expression.Negate(Convert(runtime, expr, type, miOperator.GetParameters()[0].ParameterType, lParse), miOperator);
			#endregion

			#region -- inject convert --
			if (type == typeof(string))
				return DynamicExpression.Dynamic(runtime.GetUnaryOperationBinary(ExpressionType.Negate), typeof(object), ParseNumberExpression(runtime, expr, type));
			#endregion

			#region -- try convert to integer --
			if (TryConvertType(runtime, LiftIntegerType(runtime, type), ref expr, ref type))
				return UnaryOperationNegateExpression(runtime, expr, type, lParse);
			#endregion

			throw new LuaEmitException(LuaEmitException.OperatorNotDefined, ExpressionType.Negate, String.Empty, type.Name);
		} // func UnaryOperationNegateExpression

		private static Expression UnaryOperationArithmeticExpression(Lua runtime, ExpressionType op, Expression expr, Type type, bool lParse)
		{
			bool lIsArithmetic = IsArithmeticType(type);
			if (lIsArithmetic)
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
					expr = Convert(runtime, expr, type, LiftIntegerType(runtime, type), lParse);
					type = expr.Type;
				}
				else if (op == ExpressionType.Negate)
				{
					var tc = GetTypeCode(type);
					switch (tc)
					{
						case LuaEmitTypeCode.Byte:
							expr = Convert(runtime, expr, type, typeof(short), lParse);
							type = expr.Type;
							break;
						case LuaEmitTypeCode.UInt16:
							expr = Convert(runtime, expr, type, typeof(int), lParse);
							type = expr.Type;
							break;
						case LuaEmitTypeCode.UInt32:
							expr = Convert(runtime, expr, type, typeof(long), lParse);
							type = expr.Type;
							break;
						case LuaEmitTypeCode.UInt64:
							expr = Convert(runtime, expr, type, typeof(double), lParse);
							type = expr.Type;
							break;
					}
				}

				expr = Expression.MakeUnary(op, Convert(runtime, expr, type, type, false), type);

				// convert to enum
				if (typeEnum != null)
					expr = Expression.Convert(expr, typeEnum);

				return expr;
				#endregion
			}

			MethodInfo miOperation = null;
			string sMethodName = GetOperationMethodName(op);
#if DEBUG
			if (sMethodName == null)
				throw new InvalidOperationException(String.Format("Method for Operator {0} not defined.", op));
#endif
			#region -- find operator --

			// try to find a exact match for the operation
			miOperation = type.GetRuntimeMethods().Where(mi => mi.IsStatic && mi.Name == sMethodName && CheckArguments(mi.GetParameters(), new Type[] { type })).FirstOrDefault();

			// can we inject a string conversation --> create a dynamic operation, that results in a simple arithmetic operation
			if (miOperation == null && type == typeof(string))
			{
				#region -- string inject for arithmetic --
				expr = ParseNumberExpression(runtime, expr, type);
				type = typeof(object);

				return DynamicExpression.Dynamic(runtime.GetUnaryOperationBinary(op), typeof(object),
					Convert(runtime, expr, type, typeof(object), true)
				);
				#endregion
			}

			// try convert the type to an arithmetic type
			if (miOperation == null)
			{
				if (op == ExpressionType.OnesComplement && TryConvertType(runtime, LiftIntegerType(runtime, type), ref expr, ref type))
					return UnaryOperationArithmeticExpression(runtime, op, expr, type, lParse);
				else if (op == ExpressionType.Negate)
				{
					// is there a integer conversion
					bool lImplicit = false;
					bool lExactFrom = false;
					bool lExactTo = false;
					Type typeInt = LiftIntegerType(runtime, type);
					MethodInfo miConvert = FindConvertMethod(type.GetRuntimeMethods().Where(mi => mi.IsStatic), type, typeInt, ref lImplicit, ref lExactFrom, ref lExactTo);
					if (lExactTo)
					{
						if (expr.Type != type)
							expr = Expression.Convert(expr, type);
						return UnaryOperationArithmeticExpression(runtime, op, Expression.Convert(expr, typeInt), typeInt, lParse);
					}
					else if (TryConvertType(runtime, runtime == null ? typeof(double) : Lua.GetFloatType(runtime.NumberType), ref expr, ref type))
						return UnaryOperationArithmeticExpression(runtime, op, expr, type, lParse);
				}
			}
			#endregion

			if (miOperation != null)
			{
				return Expression.MakeUnary(op,
					Convert(runtime, expr, type, miOperation.GetParameters()[0].ParameterType, lParse),
					null, miOperation);
			}
			else
				throw new LuaEmitException(LuaEmitException.OperatorNotDefined, op, String.Empty, type.Name);
		} // func UnaryOperationArithmeticExpression

		private static Expression BinaryOperationArithmeticExpression(Lua runtime, ExpressionType op, Expression expr1, Type type1, Expression expr2, Type type2, bool lParse)
		{
			var tc1 = GetTypeCode(type1);
			var tc2 = GetTypeCode(type2);
			bool lIsArithmetic1 = IsArithmeticType(tc1);
			bool lIsArithmetic2 = IsArithmeticType(tc2);

			if (lIsArithmetic1 && lIsArithmetic2) // both are arithmetic --> simple arithmetic operation
			{
				Type typeOp;
				bool shift = false;

				#region -- Get the type for the operation --
				switch (op)
				{
					case ExpressionType.And:
					case ExpressionType.ExclusiveOr:
					case ExpressionType.Or:
						// both type should the same
						typeOp = LiftIntegerType(runtime, LiftType(type1, tc1, type2, tc2, false));
						break;

					case ExpressionType.LeftShift:
					case ExpressionType.RightShift:
						// the right one must be a interger
						typeOp = LiftIntegerType(runtime, type1);
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
								typeOp = LiftTypeNext(runtime, typeOp);
								break;
						}
						break;

					case ExpressionType.Divide:
						// both types must be a float type
						if (type1 == typeof(double) || type2 == typeof(double))
							typeOp = typeof(double);
						else if (type1 == typeof(float) || type2 == typeof(float))
							typeOp = typeof(float);
						else if (runtime == null)
							typeOp = typeof(double);
						else
							typeOp = Lua.GetFloatType(runtime.NumberType);
						break;

					case Lua.IntegerDivide:
						// both must be a integer
						op = ExpressionType.Divide;
						typeOp = LiftIntegerType(runtime, LiftType(type1, tc1, type2, tc2, false));
						switch (GetTypeCode(typeOp))
						{
							case LuaEmitTypeCode.SByte:
							case LuaEmitTypeCode.Byte:
								typeOp = LiftTypeNext(runtime, typeOp);
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
					Convert(runtime, expr1, type1, typeOp, lParse),
					Convert(runtime, expr2, type2, shift ? typeof(int) : typeOp, lParse)
				);

				// convert to enum
				if (typeEnum != null)
					expr = Expression.Convert(expr, typeEnum);

				return expr;
				#endregion
			}

			#region -- Find the the binary operator --
			Type[] parameterTypes = new Type[] { type1, type2 };
			string sOperationName = GetOperationMethodName(op);
			if (!String.IsNullOrEmpty(sOperationName))
			{
				// create a list of all operators
				var members1 = type1.GetRuntimeMethods().Where(mi => mi.IsStatic && mi.Name == sOperationName).ToArray();
				var members2 = type2.GetRuntimeMethods().Where(mi => mi.IsStatic && mi.Name == sOperationName).ToArray();
				var members3 = new MethodInfo[members1.Length + members2.Length];
				if (members3.Length > 0)
				{
					Array.Copy(members1, 0, members3, 0, members1.Length);
					Array.Copy(members2, 0, members3, members1.Length, members2.Length);

					// Find the correct method
					MethodInfo miOperator = FindMethod(members3, new CallInfo(2), parameterTypes, t => t, false);
					if (miOperator != null)
					{
						// Get the argumentslist
						ParameterInfo[] parameterInfo = miOperator.GetParameters();
						if (op == Lua.IntegerDivide)
							op = ExpressionType.Divide;

						// Check if the arguments are valid
						Expression exprOperatorArgument1 = expr1;
						Type typeOperatorArgument1 = type1;
						Expression exprOperatorArgument2 = expr2;
						Type typeOperatorArgument2 = type2;

						if (TryConvertType(runtime, parameterInfo[0].ParameterType, ref exprOperatorArgument1, ref typeOperatorArgument1) &&
							TryConvertType(runtime, parameterInfo[1].ParameterType, ref exprOperatorArgument2, ref typeOperatorArgument2))
						{
							return Expression.MakeBinary(op,
								exprOperatorArgument1,
								exprOperatorArgument2,
								true,
								miOperator
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
					if (lIsArithmetic1 && type2 == typeof(string))
					{
						return DynamicExpression.Dynamic(runtime.GetBinaryOperationBinder(op), typeof(object),
							Convert(runtime, expr1, type1, typeof(object), false),
							ParseNumberExpression(runtime, expr2, type2)
						);
					}
					else if (type1 == typeof(string) && lIsArithmetic2)
					{
						return DynamicExpression.Dynamic(runtime.GetBinaryOperationBinder(op), typeof(object),
							ParseNumberExpression(runtime, expr1, type1),
							Convert(runtime, expr2, type2, typeof(object), false)
						);
					}
					else if (type1 == typeof(string) && type2 == typeof(string))
					{
						return DynamicExpression.Dynamic(runtime.GetBinaryOperationBinder(op), typeof(object),
							ParseNumberExpression(runtime, expr1, type1),
							ParseNumberExpression(runtime, expr2, type2)
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
						bool lExact = false;
						Type compareInterface = GetComparableInterface(type1, type2, ref lExact);
						if (!lExact)
						{
							bool lExact2 = false;
							Type compareInterface2 = GetComparableInterface(type2, type1, ref lExact2);
							if (lExact2)
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
								return BinaryOperationCompareToExpression(runtime, compareInterface2, op, expr2, type2, expr1, type1, lParse);
							}
						}
						if (compareInterface != null)
							return BinaryOperationCompareToExpression(runtime, compareInterface, op, expr1, type1, expr2, type2, lParse);
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
						bool lExact = false;
						Type equalableInterface = GetEqualableInterface(type1, type2, ref lExact);
						if (!lExact)
						{
							bool lExact2 = false;
							Type equalableInterface2 = GetEqualableInterface(type2, type1, ref lExact2);
							if (lExact2)
								return BinaryOperationEqualableToExpression(runtime, equalableInterface, op, expr2, type2, expr1, type1, lParse);

						}
						if (equalableInterface != null)
							return BinaryOperationEqualableToExpression(runtime, equalableInterface, op, expr1, type1, expr2, type2, lParse);
						else
						{
							expr1 = Convert(runtime, expr1, type1, typeof(object), lParse);
							expr2 = Convert(runtime, expr2, type2, typeof(object), lParse);

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
			if (type1 != type2)
			{
				if (TryConvertType(runtime, type2, ref expr1, ref type1))
					return BinaryOperationArithmeticExpression(runtime, op, expr1, type1, Convert(runtime, expr2, type2, type1, lParse), type1, lParse);
				else if (TryConvertType(runtime, type1, ref expr2, ref type2))
					return BinaryOperationArithmeticExpression(runtime, op, expr1, type1, Convert(runtime, expr2, type2, type1, lParse), type1, lParse);
			}
			#endregion

			throw new LuaEmitException(LuaEmitException.OperatorNotDefined, op, type1.Name, type2.Name);
		} // func BinaryOperationArithmeticExpression

		/// <summary>Compares the to types and returns the "higest".</summary>
		/// <param name="type1"></param>
		/// <param name="type2"></param>
		/// <param name="lSigned"></param>
		/// <returns></returns>
		public static Type LiftType(Type type1, Type type2, bool lSigned = false)
		{
			if (type1 == type2)
				return type1;

			var tc1 = GetTypeCode(type1);
			var tc2 = GetTypeCode(type2);

			if (IsArithmeticType(tc1) && IsArithmeticType(tc2)) // process only arithmetic types
				return LiftType(type1, tc1, type2, tc2, lSigned);
			else
				return typeof(object);
		} // func LiftType

		private static Type LiftType(Type type1, LuaEmitTypeCode tc1, Type type2, LuaEmitTypeCode tc2, bool lSigned)
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
			else if (lSigned) // force unsigned
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

		private static Type LiftIntegerType(Lua runtime, Type type)
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
					return runtime == null ? typeof(int) : Lua.GetIntegerType(runtime.NumberType);
			}
		} // func LiftIntegerType

		#endregion

		#region -- Emit GetMember ---------------------------------------------------------

		public static Expression GetMember(Lua runtime, Expression instance, Type type, string sMemberName, bool lIgnoreCase, bool lParse)
		{
			MemberInfo[] members =
				lParse && IsDynamicType(type) ? null :  // dynamic type --> resolve later
					GetRuntimeMembers(type.GetTypeInfo(), sMemberName, instance == null, lIgnoreCase).ToArray(); // get the current members

			if (members == null || members.Length == 0) // no member found, try later again
			{
				if (lParse)
					return DynamicExpression.Dynamic(runtime.GetGetMemberBinder(sMemberName), typeof(object), Convert(runtime, instance, type, typeof(object), false));
				else
					return Expression.Default(typeof(object));
			}
			else if (members.Length > 1 && members[0] is MethodInfo) // multiple member
			{
				return Expression.New(Lua.OverloadedMethodConstructorInfo, instance == null ? Expression.Default(typeof(object)) : instance, Expression.Constant(Lua.RtConvertArray(members, typeof(MethodInfo)), typeof(MethodInfo[])));
			}
			else // return the one member
			{
				var member = members[0];

				if (instance != null)
					instance = Convert(runtime, instance, type, type, false);

				if (member is FieldInfo)
				{
					return Expression.MakeMemberAccess(instance, member);
				}
				else if (member is PropertyInfo)
				{
					PropertyInfo pi = (PropertyInfo)member;
					if (!pi.CanRead)
						throw new LuaEmitException(LuaEmitException.CanNotReadMember, type.Name, sMemberName);

					return Expression.MakeMemberAccess(instance, member);
				}
				else if (member is MethodInfo)
				{
					return Expression.New(Lua.MethodConstructorInfo, instance == null ? Expression.Default(typeof(object)) : instance, Expression.Constant(member, typeof(MethodInfo)));
				}
				else if (member is EventInfo)
				{
					return Expression.New(Lua.EventConstructorInfo, instance == null ? Expression.Default(typeof(object)) : instance, Expression.Constant((EventInfo)member));
				}
				else if (member is TypeInfo)
				{
					return Expression.Call(Lua.TypeGetTypeMethodInfoArgType, Expression.Constant(((TypeInfo)member).AsType()));
				}
				else
					throw new LuaEmitException(LuaEmitException.CanNotReadMember, type.Name, sMemberName);
			}
		} // func GetMember

		#endregion

		#region -- Emit SetMember ---------------------------------------------------------

		public static Expression SetMember(Lua runtime, Expression instance, Type type, string sMemberName, bool lIgnoreCase, Expression set, Type typeSet, bool lParse)
		{
			if (lParse && IsDynamicType(type))
			{
				return DynamicExpression.Dynamic(runtime.GetSetMemberBinder(sMemberName), typeof(object),
					Convert(runtime, instance, type, typeof(object), false),
					Convert(runtime, set, typeSet, typeof(object), false)
				);
			}

			MemberInfo[] members = type.GetTypeInfo().GetRuntimeMembers(sMemberName, instance == null, lIgnoreCase).ToArray();
			instance = instance == null ? null : Convert(runtime, instance, type, type, lParse);

			if (members == null || members.Length == 0)
				throw new LuaEmitException(LuaEmitException.MemberNotFound, type.Name, sMemberName);
			//else if (members.Length > 1)
			//	throw new LuaEmitException(LuaEmitException.MemberNotUnique, type.Name, sMemberName);
			else
			{
				if (members[0] is PropertyInfo)
				{
					PropertyInfo pi = (PropertyInfo)members[0];
					if (!pi.CanWrite)
						throw new LuaEmitException(LuaEmitException.CanNotWriteMember, type.Name, sMemberName);
					return Expression.Assign(Expression.Property(instance, pi), Convert(runtime, set, typeSet, pi.PropertyType, lParse));
				}
				else if (members[0] is FieldInfo)
				{
					FieldInfo fi = (FieldInfo)members[0];
					return Expression.Assign(Expression.Field(instance, fi), Convert(runtime, set, typeSet, fi.FieldType, lParse));
				}
				else
					throw new LuaEmitException(LuaEmitException.CanNotWriteMember, type.Name, sMemberName);
			}
		} // func SetMember

		#endregion

		#region -- Emit GetIndex, SetIndex ------------------------------------------------

		public static Expression GetIndex<TARG>(Lua runtime, TARG instance, TARG[] arguments, Func<TARG, Expression> getExpr, Func<TARG, Type> getType, bool lParse)
			where TARG : class
		{
			Type instanceType = getType(instance);
			if (lParse && IsDynamicType(instanceType))
			{
				return DynamicExpression.Dynamic(runtime.GetGetIndexMember(new CallInfo(arguments.Length)), typeof(object),
					CreateDynamicArgs(runtime, getExpr(instance), instanceType, arguments, getExpr, getType)
				);
			}

			return GetIndexAccess<TARG>(runtime, getExpr(instance), instanceType, arguments, getExpr, getType, lParse);
		} // func GetIndex

		public static Expression SetIndex<TARG>(Lua runtime, TARG instance, TARG[] arguments, TARG setTo, Func<TARG, Expression> getExpr, Func<TARG, Type> getType, bool lParse)
			 where TARG : class
		{
			Type instanceType = getType(instance);
			if (lParse && IsDynamicType(instanceType))
			{
				return DynamicExpression.Dynamic(runtime.GetSetIndexMember(new CallInfo(arguments.Length)), typeof(object),
					CreateDynamicArgs<TARG>(runtime, getExpr(instance), instanceType, arguments, setTo, getExpr, getType)
				);
			}

			// Emit the index set
			Expression exprIndexAccess = GetIndexAccess(runtime, getExpr(instance), instanceType, arguments, getExpr, getType, lParse);
			return Expression.Assign(exprIndexAccess, Convert(runtime, getExpr(setTo), getType(setTo), exprIndexAccess.Type, lParse));
		} // func SetIndex

		private static Expression GetIndexAccess<TARG>(Lua runtime, Expression instance, Type instanceType, TARG[] arguments, Func<TARG, Expression> getExpr, Func<TARG, Type> getType, bool lParse)
			where TARG : class
		{
			if (typeof(Array).GetTypeInfo().IsAssignableFrom(instanceType.GetTypeInfo())) // type is an array
			{
				// create index as integers
				Expression[] indexes = new Expression[arguments.Length];
				for (int i = 0; i < indexes.Length; i++)
					indexes[i] = Convert(runtime, getExpr(arguments[i]), getType(arguments[i]), typeof(int), lParse);

				return Expression.ArrayAccess(Convert(runtime, instance, instanceType, instanceType, lParse), indexes);
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
					return BindParameter(runtime,
						args => Expression.MakeIndex(Convert(runtime, instance, instanceType, instanceType, lParse), piIndex, args),
						piIndex.GetIndexParameters(),
						callInfo,
						arguments,
						getExpr, getType, lParse);
			}
		} // func GetIndexAccess

		#endregion

		#region -- BindParameter ----------------------------------------------------------

		public static Expression BindParameter<T>(Lua runtime, Func<Expression[], Expression> emitCall, ParameterInfo[] parameterInfo, CallInfo callInfo, T[] arguments, Func<T, Expression> getExpr, Func<T, Type> getType, bool forParse)
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

				if (parameter.IsOut) // out-param no value neede
				{
					argumentExpression = null;
				}
				else if (argumentsIndex < argumentsCount) // positional argument exists
				{
					argumentExpression = Convert(runtime, getExpr(arguments[argumentsIndex]), getType(arguments[argumentsIndex]), parameterType, forParse);
					if (argumentsWorkedWith != null)
						argumentsWorkedWith[argumentsIndex] = true;
					argumentsIndex++;
				}
				else if (lastArgumentStretchCount > 0) // stretch LuaResult
				{
					argumentExpression = GetResultExpression(runtime, varLuaResult, typeof(LuaResult), lastArgumentStretchCount++, parameterType, GetDefaultParameterExpression(parameter, parameterType), forParse);
				}
				else if (lastArgumentIsResult) // start stretch of LuaResult
				{
					varLuaResult = Expression.Variable(typeof(LuaResult), "#result");
					argumentExpression = GetResultExpression(runtime,
						Expression.Assign(varLuaResult, Convert(runtime, getExpr(arguments[argumentsIndex]), typeof(LuaResult), typeof(LuaResult), false)),
						typeof(LuaResult),
						0,
						parameterType,
						GetDefaultParameterExpression(parameter, parameterType),
						forParse
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
						argumentExpression = Convert(runtime, getExpr(arguments[argumentsIndex]), getType(arguments[argumentsIndex]), parameterType, forParse);
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
						collectedArguments.Add(Convert(runtime, getExpr(arguments[argumentsIndex]), getType(arguments[argumentsIndex]), arrayType, forParse));

					// the last argument is a LuaResult
					if (lastArgumentIsResult)
					{
						// combine the arguments and the last result to the correct array
						var tmpExpr = getExpr(arguments[arguments.Length - 1]);
						var tmpType = getType(arguments[arguments.Length - 1]);
						argumentExpression = Expression.Convert(
							Expression.Call(null, Lua.CombineArrayWithResultMethodInfo,
								Expression.Convert(Expression.NewArrayInit(arrayType, collectedArguments), typeof(Array)),
								Convert(runtime, tmpExpr, tmpType, tmpType, forParse),
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
							Convert(runtime, tmpExpr, tmpType, tmpType, false),
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
						for (int i = 0; i < argumentsWorkedWith.Length; i++)
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
						from v in variablesToReturn select Convert(runtime, v, v.Type, typeof(object), false))));

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

		#region -- FindMember -------------------------------------------------------------

		#region -- enum MemberMatchValue --------------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private enum MemberMatchValue
		{
			None,
			Exact,
			Implicit,
			Explicit
		} // enum MemberMatchValue

		#endregion

		#region -- struct MemberMatchInfo -------------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary>Holds the result of the member.</summary>
		private sealed class MemberMatchInfo<TMEMBERTYPE>
			where TMEMBERTYPE : MemberInfo
		{
			private readonly int argumentsLength;     // number of arguments we need to match
			private readonly bool unboundedArguments; // is the number endless (LuaResult as last argument)
			internal int matchesOnBeginning;
			internal int exactMatches;
			internal int implicitMatches;
			internal int explicitMatches;

			private TMEMBERTYPE currentMember;
			internal int currentParameterLength;

			public MemberMatchInfo(bool unboundedArguments, int argumentsLength)
			{
				this.unboundedArguments = unboundedArguments;
				this.argumentsLength = argumentsLength;
			} // ctor

			public override string ToString()
			{
				return String.Format("OnBegin={0}, Exact={1}, Implicit={2}, Explicit={3}, ParameterLength={4}, IsPerfect={5}",
					matchesOnBeginning, exactMatches, implicitMatches, explicitMatches, currentParameterLength, IsPerfect);
			} // func ToString

			public void Reset(TMEMBERTYPE member, ParameterInfo[] parameter)
			{
				this.matchesOnBeginning = 0;
				this.exactMatches = 0;
				this.implicitMatches = 0;
				this.explicitMatches = 0;
				this.currentMember = member;

				// get the parameter length
				this.currentParameterLength = parameter.Length > 0 ?
						parameter[parameter.Length - 1].ParameterType.IsArray ?
							Int32.MaxValue :
							parameter.Length :
					0;
			} // proc Reset

			public void SetMatch(MemberMatchValue value, bool positional)
			{
				switch (value)
				{
					case MemberMatchValue.Exact:
						if (positional && explicitMatches == matchesOnBeginning)
							matchesOnBeginning++;
						exactMatches++;
						goto case MemberMatchValue.Implicit;
					case MemberMatchValue.Implicit:
						implicitMatches++;
						goto case MemberMatchValue.Explicit;
					case MemberMatchValue.Explicit:
						explicitMatches++;
						break;
					default:
						throw new InvalidOperationException();
				}
			} // proc SetMatch

			public bool IsBetter(MemberMatchInfo<TMEMBERTYPE> other) // is this better than other
			{
				if (other.IsPerfect)
				{
					Debug.WriteLine("  ==> other IsPerfect");
					return true;
				}

				if (unboundedArguments && currentParameterLength > other.currentParameterLength)
				{
					Debug.WriteLine("  ==> other {0} && {1} > {2}", unboundedArguments, currentParameterLength, other.currentParameterLength);
					return true;
				}
				else if (!unboundedArguments || currentParameterLength == other.currentParameterLength)
				{
					if (argumentsLength == 0 && currentParameterLength == 0) // zero arguments
					{
						Debug.WriteLine("  ==> other Zero Args");
						return true;
					}

					else if (matchesOnBeginning > other.matchesOnBeginning ||
						exactMatches > other.exactMatches ||
						implicitMatches > other.implicitMatches) // good matches (more)
					{
						Debug.WriteLine("  ==> other is better");
						return true;
					}
					else if (matchesOnBeginning == other.matchesOnBeginning ||
						exactMatches == other.exactMatches ||
						implicitMatches == other.implicitMatches) // good matches (equal)
					{
						var r = NoneMatches < other.NoneMatches; // too much bad machtes, so it might be not better
#if DEBUG
						if (r)
							Debug.WriteLine("  ==> other is equal, but less bad matches");
#endif
						return r;
					}

					else if (explicitMatches > other.explicitMatches)
					{
						Debug.WriteLine("   ==> other has more explicit matches");
						return true;
					}
					else if (explicitMatches == other.explicitMatches)
					{
						var r = NoneMatches < other.NoneMatches;
#if DEBUG
						if (r)
							Debug.WriteLine("  ==> other is equal, but less bad matches");
#endif
						return r;
					}

					else
						return false;
				}
				else
					return false;
			} // func IsBetter

			public TMEMBERTYPE CurrentMember => currentMember;

			public bool IsPerfect => currentParameterLength == argumentsLength && exactMatches == argumentsLength;
			public int NoneMatches => currentParameterLength < Int32.MaxValue ? currentParameterLength - explicitMatches : 0;
		} // struct MemberMatchInfo

		#endregion

		#region -- class MemberMatch ------------------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary>Holds the description of the arguments and the compare algorithm.</summary>
		private sealed class MemberMatch<TMEMBERTYPE, TARG>
			where TMEMBERTYPE : MemberInfo
			where TARG : class
		{
			private readonly CallInfo callInfo;
			private readonly int positionalArguments;
			private readonly TARG[] arguments;
			private readonly bool lastIsExpandable;
			private readonly Func<TARG, Type> getType;
			private readonly Action<ParameterInfo[], MemberMatchInfo<TMEMBERTYPE>> resetAlgorithm;

			public MemberMatch(CallInfo callInfo, TARG[] arguments, Func<TARG, Type> getType)
			{
				// init reset parameter
				this.callInfo = callInfo;
				this.positionalArguments = arguments.Length - callInfo.ArgumentNames.Count; // number of positional arguments
				this.arguments = arguments;
				this.getType = getType;
				this.lastIsExpandable = arguments.Length > 0 && getType(arguments[arguments.Length - 1]) == typeof(LuaResult);

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
				Debug.WriteLine("Call: {0}", argsDebug.ToString());
#endif

				// choose the algorithm
				if (arguments.Length == 0 || positionalArguments == arguments.Length)
				{
					if (lastIsExpandable)
						resetAlgorithm = ResetPositionalMax;
					else
						resetAlgorithm = ResetPositional;
				}
				else
					resetAlgorithm = ResetNamed;

				Debug.WriteLine("Algorithm: {0}", resetAlgorithm.GetMethodInfo().Name);
			} // ctor

			public void Reset(TMEMBERTYPE member, bool isMemberCall, MemberMatchInfo<TMEMBERTYPE> target)
			{
				Debug.WriteLine("Reset member: {0}", member);

				var parameterInfo = GetMemberParameter(member, isMemberCall);
				target.Reset(member, parameterInfo);
				resetAlgorithm(parameterInfo, target);
				Debug.WriteLine("      Result: {0}", target);
			} // proc Reset
			
			private static ParameterInfo[] GetMemberParameter(TMEMBERTYPE mi, bool isMemberCall)
			{
				var mb = mi as MethodBase;
				if (mb != null)
				{
					return isMemberCall && mb.IsStatic ?
						mb.GetParameters().Skip(1).ToArray() :
						mb.GetParameters();
				}
				else
				{
					var pi = mi as PropertyInfo;
					if (pi != null)
					{
						return pi.GetIndexParameters();
					}
					else
						throw new ArgumentException();
				}
			} // func GetMemberParameter

			private void ResetPositionalPart(ParameterInfo[] parameterInfo, int length, MemberMatchInfo<TMEMBERTYPE> target)
			{
				for (var i = 0; i < length; i++)
					target.SetMatch(GetParameterMatch(parameterInfo[i].ParameterType.GetTypeInfo(), getType(arguments[i]).GetTypeInfo()), true);
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
						var paramLastType = parameterInfo[parameterInfo.Length - 1].ParameterType;
						if (argLastType.IsArray && paramLastType.IsArray)
						{
							target.SetMatch(GetParameterMatch(argLastType.GetTypeInfo(), paramLastType.GetTypeInfo()), true);
							return;
						}
					}

					// test the array
					var rest = lastIsExpandable ? Int32.MaxValue - length : arguments.Length - length;
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
				var checkArguments = arguments.Length - 1;
				if (checkArguments >= parameterInfo.Length)
					ResetPositionalPart(parameterInfo, parameterInfo.Length, target);
				else // check the positional part
					ResetPositionalPart(parameterInfo, checkArguments, target);

				// the last part will match
				target.explicitMatches = Int32.MaxValue;
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
							target.SetMatch(GetParameterMatch(parameterInfo[index].ParameterType.GetTypeInfo(), getType(arguments[i++]).GetTypeInfo()), false);
					}
				}
			} // proc ResetNamed

			private MemberMatchValue GetParameterMatch(TypeInfo parameterType, TypeInfo argumentType)
			{
				bool exact;
				if (parameterType == argumentType)
					return MemberMatchValue.Exact;
				else if (parameterType.IsGenericParameter) // special checks for generic parameter
				{
					#region -- check generic --
					var typeConstraints = parameterType.GetGenericParameterConstraints();

					exact = false;

					// check "class"
					if ((parameterType.GenericParameterAttributes & GenericParameterAttributes.ReferenceTypeConstraint) != 0 && argumentType.IsValueType)
						return MemberMatchValue.Explicit;

					// check struct
					if ((parameterType.GenericParameterAttributes & GenericParameterAttributes.NotNullableValueTypeConstraint) != 0 && !argumentType.IsValueType)
						return MemberMatchValue.Explicit;

					// check new()
					if ((parameterType.GenericParameterAttributes & GenericParameterAttributes.DefaultConstructorConstraint) != 0)
					{
						// check default ctor
						if (argumentType.FindDeclaredConstructor(ReflectionFlag.Public | ReflectionFlag.Instance, new Type[0]) == null)
							return MemberMatchValue.Explicit;
					}

					// no contraints, all is allowed
					if (typeConstraints.Length == 0)
						return MemberMatchValue.Implicit;

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

					return noneExactMatch ? MemberMatchValue.Implicit : MemberMatchValue.Explicit;
					#endregion
				}
				else if (TypesMatch(parameterType.AsType(), argumentType.AsType(), out exact)) // is at least assignable
				{
					return exact ? MemberMatchValue.Exact : MemberMatchValue.Implicit;
				}
				else
					return MemberMatchValue.Explicit;
			} // func GetParameterMatch
		} // class MemberMatch

		#endregion

		public static MethodInfo FindMethod<TARG>(IEnumerable<MethodInfo> members, CallInfo callInfo, TARG[] arguments, Func<TARG, Type> getType, bool lExtension)
			where TARG : class
		{
			var mi = FindMember(members, callInfo, arguments, getType, lExtension);

			// create a non generic version
			if (mi != null && mi.ContainsGenericParameters)
				mi = MakeNonGenericMethod(mi, arguments, getType);

			return mi;
		} // func FindMethod

		public static TMEMBERTYPE FindMember<TMEMBERTYPE, TARG>(IEnumerable<TMEMBERTYPE> members, CallInfo callInfo, TARG[] arguments, Func<TARG, Type> getType, bool isMemberCall)
			where TMEMBERTYPE : MemberInfo
			where TARG : class
		{
			var unboundedArguments = callInfo.ArgumentNames.Count == 0 && arguments.Length > 0 ? getType(arguments[arguments.Length - 1]) == typeof(LuaResult) : false;
			var memberMatch = new MemberMatch<TMEMBERTYPE, TARG>(callInfo, arguments, getType);
			var memberMatchBind = new MemberMatchInfo<TMEMBERTYPE>(unboundedArguments, arguments.Length);

			// get argument list
			var memberEnum = members.Where(CanCallMember).GetEnumerator();

			// reset the result with the first one
			if (memberEnum.MoveNext())
				memberMatch.Reset(memberEnum.Current, isMemberCall, memberMatchBind);
			else
				return null;

			// text the rest if there is better one
			if (memberEnum.MoveNext() && !memberMatchBind.IsPerfect)
			{
				var memberMatchCurrent = new MemberMatchInfo<TMEMBERTYPE>(unboundedArguments, arguments.Length);

				// test
				memberMatch.Reset(memberEnum.Current, isMemberCall, memberMatchCurrent);
				if (memberMatchCurrent.IsBetter(memberMatchBind))
				{
					memberMatchBind = memberMatchCurrent;
					memberMatchCurrent = new MemberMatchInfo<TMEMBERTYPE>(unboundedArguments, arguments.Length);
				}

				while (memberEnum.MoveNext() && !memberMatchBind.IsPerfect)
				{
					// test
					memberMatch.Reset(memberEnum.Current, isMemberCall, memberMatchCurrent);
					if (memberMatchCurrent.IsBetter(memberMatchBind))
					{
						memberMatchBind = memberMatchCurrent;
						memberMatchCurrent = new MemberMatchInfo<TMEMBERTYPE>(unboundedArguments, arguments.Length);
					}
				}
			}

			Debug.WriteLine("USED: {0}", memberMatchBind.CurrentMember);

			return memberMatchBind.CurrentMember;
		} // func FindMember

		private static bool CanCallMember<TMEMBERTYPE>(TMEMBERTYPE mi)
		{
			var methodInfo = mi as MethodInfo;
			return methodInfo == null || (methodInfo.CallingConvention & CallingConventions.VarArgs) == 0;
		} // func CanCallMember

		private static MethodInfo MakeNonGenericMethod<TARG>(MethodInfo mi, TARG[] arguments, Func<TARG, Type> getType)
			where TARG : class
		{
			var parameterInfo = mi.GetParameters();
			var genericArguments = mi.GetGenericArguments();
			var genericParameter = new Type[genericArguments.Length];

			for (var i = 0; i < genericArguments.Length; i++)
			{
				Type t = null;

				// look for the typ
				for (int j = 0; j < parameterInfo.Length; j++)
				{
					if (parameterInfo[j].ParameterType == genericArguments[i])
					{
						t = CombineType(t, getType(arguments[j]));
						break;
					}
				}
				genericParameter[i] = t;
			}

			if (genericParameter.Any(t => t == null))
				throw new ArgumentException(String.Format("Can not create method for generic {0}:{1} [{2}].", mi.DeclaringType.GetType().FullName, mi.Name, mi.ToString()));

			return mi.MakeGenericMethod(genericParameter);
		} // func MakeNonGenericMethod

		private static Type CombineType(Type t, Type type)
		{
			if (t == null)
				return type;
			else if (t.GetTypeInfo().IsAssignableFrom(type.GetTypeInfo()))
				return t;
			else if (type.GetTypeInfo().IsAssignableFrom(t.GetTypeInfo()))
				return type;
			else
				return typeof(object);
		} // func CombineType

		#endregion

		#region -- Reflection Helper ------------------------------------------------------

		private static bool CheckArguments(ParameterInfo[] args, Type[] arguments)
		{
			if (args.Length != arguments.Length)
				return false;
			else
			{
				for (int i = 0; i < args.Length; i++)
					if (args[i].ParameterType != arguments[i])
						return false;
			}
			return true;
		} // func CheckArguments

		private static IEnumerable<T> FilterName<T>(IEnumerable<T> list, string sName, ReflectionFlag flags)
			where T : MemberInfo
		{
			StringComparison stringComparison = (flags & ReflectionFlag.IgnoreCase) != 0 ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
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

		private static T GetOneResult<T>(TypeInfo ti, string sName, ReflectionFlag flags, IEnumerable<T> list, [CallerMemberName] string sCaller = null)
		{
			if ((flags & ReflectionFlag.NoException) != 0)
				return list.FirstOrDefault();
			else
			{
				var e = list.GetEnumerator();
				if (e.MoveNext()) // first element for return
				{
					var miFind = e.Current;
					if (e.MoveNext())
						throw new ArgumentException(String.Format("{0} for {1}.{2}, is not unique.", sCaller, ti.Name, sName));
					return miFind;
				}
				else
					throw new ArgumentException(String.Format("{0} failed for {1}.{2}.", sCaller, ti.Name, sName));
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

		public static IEnumerable<MemberInfo> GetRuntimeMembers(TypeInfo typeInfo, string sMemberName, bool lStatic, StringComparison stringComparison)
		{
			// Current
			foreach (var member in typeInfo.DeclaredMembers)
				if (String.Compare(sMemberName, member.Name, stringComparison) == 0)
				{
					if (member is MethodBase && ((MethodBase)member).IsStatic != lStatic)
						continue;
					else if (member is PropertyInfo && ((PropertyInfo)member).GetMethod.IsStatic != lStatic)
						continue;
					else if (member is FieldInfo && ((FieldInfo)member).IsStatic != lStatic)
						continue;
					else if (member is EventInfo && ((EventInfo)member).AddMethod.IsStatic != lStatic)
						continue;

					yield return member;
				}
			// Base type
			if (typeInfo.BaseType != null)
			{
				foreach (var member in GetRuntimeMembers(typeInfo.BaseType.GetTypeInfo(), sMemberName, lStatic, stringComparison))
					yield return member;
			}
		} // func GetRuntimeMembers

		public static IEnumerable<MemberInfo> GetRuntimeMembers(this TypeInfo typeInfo, string sMemberName, bool lStatic, bool lIgnoreCase)
		{
			return GetRuntimeMembers(typeInfo, sMemberName, lStatic, lIgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
		} // func GetRuntimeMembers

		#endregion
	} // class LuaEmit

	#endregion
}
				//switch (op)
				//{
				//	case ExpressionType.Add:
				//	case ExpressionType.AddChecked:
				//		typeOp = LiftType(type1, type2, false);
				//		if (typeOp == typeof(float) || typeOp == typeof(double))
				//		{
				//			return Expression.Add(
				//				 Convert(runtime, expr1, type1, typeOp, lParse),
				//				 Convert(runtime, expr2, type2, typeOp, lParse)
				//			 );
				//		}
				//		else
				//		{
				//			Type typeOpNext = LiftTypeNext(runtime, typeOp);

				//			return Expression.TryCatch(
				//				Expression.Convert(
				//					Expression.AddChecked(
				//						Convert(runtime, expr1, type1, typeOp, lParse),
				//						Convert(runtime, expr2, type2, typeOp, lParse)
				//					),
				//					typeof(object)
				//				),
				//				Expression.Catch(
				//					typeof(OverflowException),
				//					Expression.Convert(
				//						Expression.Add(
				//							Convert(runtime, expr1, type1, typeOpNext, lParse),
				//							Convert(runtime, expr2, type2, typeOpNext, lParse)
				//						),
				//						typeof(object)
				//					)
				//				)
				//			);
				//		}
				//	case ExpressionType.Subtract:
				//	case ExpressionType.SubtractChecked:
				//		typeOp = LiftType(type1, type2, true);
				//		if (typeOp == typeof(float) || typeOp == typeof(double))
				//		{
				//			return Expression.Subtract(
				//				 Convert(runtime, expr1, type1, typeOp, lParse),
				//				 Convert(runtime, expr2, type2, typeOp, lParse)
				//			 );
				//		}
				//		else
				//		{
				//			Type typeOpNext = LiftTypeNext(runtime, typeOp);

				//			return Expression.TryCatch(
				//				Expression.Convert(
				//					Expression.SubtractChecked(
				//						Convert(runtime, expr1, type1, typeOp, lParse),
				//						Convert(runtime, expr2, type2, typeOp, lParse)
				//					),
				//					typeof(object)
				//				),
				//				Expression.Catch(
				//					typeof(OverflowException),
				//					Expression.Convert(
				//						Expression.Subtract(
				//							Convert(runtime, expr1, type1, typeOpNext, lParse),
				//							Convert(runtime, expr2, type2, typeOpNext, lParse)
				//						),
				//						typeof(object)
				//					)
				//				)
				//			);
				//		}
				//	case ExpressionType.Multiply:
				//	case ExpressionType.MultiplyChecked:
				//		typeOp = LiftType(type1, type2, false);
				//		if (typeOp == typeof(float) || typeOp == typeof(double))
				//		{
				//			return Expression.Multiply(
				//				 Convert(runtime, expr1, type1, typeOp, lParse),
				//				 Convert(runtime, expr2, type2, typeOp, lParse)
				//			 );
				//		}
				//		else
				//		{
				//			Type typeOpNext = LiftTypeNext(runtime, typeOp);

				//			return Expression.TryCatch(
				//				Expression.Convert(
				//					Expression.MultiplyChecked(
				//						Convert(runtime, expr1, type1, typeOp, lParse),
				//						Convert(runtime, expr2, type2, typeOp, lParse)
				//					),
				//					typeof(object)
				//				),
				//				Expression.Catch(
				//					typeof(OverflowException),
				//					Expression.Convert(
				//						Expression.Multiply(
				//							Convert(runtime, expr1, type1, typeOpNext, lParse),
				//							Convert(runtime, expr2, type2, typeOpNext, lParse)
				//						),
				//						typeof(object)
				//					)
				//				)
				//			);
				//		}
				//}
