using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
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

	#region -- class LuaEmit ------------------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	internal static class LuaEmit
	{
		public const string csImplicit = "op_Implicit";
		public const string csExplicit = "op_Explicit";

		#region -- IsDynamic, IsArithmetic, ... -------------------------------------------

		public static bool IsDynamicType(Type type)
		{
			return type == typeof(object) || typeof(IDynamicMetaObjectProvider).IsAssignableFrom(type);
		} // func IsDynamicType

		private static Type UnpackType(Type type)
		{
			if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
				type = type.GetGenericArguments()[0];
			if (type.IsEnum)
				type = type.GetEnumUnderlyingType();
			return type;
		} // func UnpackType

		public static TypeCode GetTypeCode(Type type)
		{
			if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
				type = type.GetGenericArguments()[0];
			return Type.GetTypeCode(type);
		} // func GetTypeCode

		internal static bool TypesMatch(Type typeTo, Type typeFrom, out bool lExact)
		{
			typeTo = UnpackType(typeTo);
			typeFrom = UnpackType(typeFrom);

			if (typeTo == typeFrom)
			{
				lExact = true;
				return true;
			}
			else if (typeTo.IsAssignableFrom(typeFrom))
			{
				lExact = false;
				return true;
			}
			else
			{
				TypeCode tcTo = Type.GetTypeCode(typeTo);
				TypeCode tcFrom = Type.GetTypeCode(typeFrom);

				lExact = false;

				if (tcTo == TypeCode.String)
					return true;
				else if (tcTo >= TypeCode.SByte && tcTo <= TypeCode.Double &&
							(tcFrom >= TypeCode.SByte && tcFrom <= tcTo || tcTo == TypeCode.Single && tcFrom == TypeCode.Double)) // exception for single -> double
					return true;
				else if (tcFrom == TypeCode.String &&
					tcTo >= TypeCode.SByte && tcTo <= TypeCode.Double)
					return true;

				return false;
			}
		} // bool TypesMatch

		private static bool IsArithmeticType(Type type)
		{
			return IsArithmeticType(GetTypeCode(type));
		} // func IsArithmeticType

		private static bool IsArithmeticType(TypeCode typeCode)
		{
			return IsIntegerType(typeCode) || IsFloatType(typeCode);
		} // func IsArithmeticType

		public static bool IsIntegerType(TypeCode typeCode)
		{
			switch (typeCode)
			{
				case TypeCode.Byte:
				case TypeCode.Int16:
				case TypeCode.Int32:
				case TypeCode.Int64:
				case TypeCode.SByte:
				case TypeCode.UInt16:
				case TypeCode.UInt32:
				case TypeCode.UInt64:
					return true;
				default:
					return false;
			}
		} // func IsIntegerType

		private static bool IsFloatType(TypeCode typeCode)
		{
			switch (typeCode)
			{
				case TypeCode.Double:
				case TypeCode.Single:
					return true;
				default:
					return false;
			}
		} // func IsFloatType

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
			bool lExactTo = false;
			bool lExactFrom = false;
			bool lImplicit = false;
			MethodInfo miConvert = FindConvertMethod(
				typeTo.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.InvokeMethod),
				exprType, typeTo,
				ref lImplicit, ref lExactFrom, ref lExactTo);

			if (!lImplicit || !lExactFrom || !lExactTo)
				miConvert = FindConvertMethod(
					exprType.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.InvokeMethod),
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

		public static MethodInfo FindConvertMethod(MethodInfo[] methods, Type typeFrom, Type typeTo, ref bool lImplicit, ref bool lExactFrom, ref bool lExactTo)
		{
			MethodInfo miCurrent = null;
			for (int i = 0; i < methods.Length; i++)
			{
				MethodInfo mi = methods[i];
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

		private static BindingFlags GetBindingFlags(bool lInstance, bool lIgnoreCase)
		{
			BindingFlags flags = BindingFlags.Public;
			if (lInstance)
				flags |= BindingFlags.Instance;
			else
				flags |= BindingFlags.Static;
			if (lIgnoreCase)
				flags |= BindingFlags.IgnoreCase;
			return flags;
		} // func GetBindingFlags

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
							expr = Expression.Dynamic(runtime.GetConvertBinder(toType), toType, Convert(runtime, expr, expr.Type, typeof(object), false));
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
				return Expression.Dynamic(runtime.GetConvertBinder(toType), toType, Convert(null, expr, fromType, typeof(object), false));
			}
			else if (toType == typeof(object) || toType.IsAssignableFrom(fromType)) // Type is assignable
			{
				return Expression.Convert(expr, toType);
			}
			else if (toType == typeof(bool)) // we need a true or false
			{
				if (fromType.IsValueType)
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
					foreach (MethodInfo mi in fromType.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.InvokeMethod))
					{
						if ((mi.Name == csExplicit || mi.Name == csImplicit) &&
							mi.ReturnType == typeof(string))
							return Expression.Convert(expr, toType, mi);
					}

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
			else if (toType.BaseType == typeof(MulticastDelegate) && toType.BaseType == fromType.BaseType)
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
				return Expression.Dynamic(runtime.GetUnaryOperationBinary(op), typeof(object), Convert(runtime, expr, type, typeof(object), lParse));
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
			return Expression.Dynamic(runtime.GetBinaryOperationBinder(op), typeof(object),
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

		private static Expression BinaryOperationCompareToExpression(Lua runtime, Type compareInterface, ExpressionType op, Expression expr1, Type type1, Expression expr2, Type type2, bool lParse)
		{
			MethodInfo miMethod = compareInterface.GetMethod("CompareTo", BindingFlags.Public | BindingFlags.Instance | BindingFlags.InvokeMethod);
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
			Type typeParam = equalableInterface.GetGenericArguments()[0];
			MethodInfo miMethod = equalableInterface.GetMethod("Equals", BindingFlags.Public | BindingFlags.Instance | BindingFlags.InvokeMethod, null, new Type[] { typeParam }, null);
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
			foreach (Type typeTest in type1.GetInterfaces())
			{
				if (compareInterface == null && typeTest == typeof(IComparable) && TypesMatch(type1, Type2, out lExact))
					return typeTest;
				else if (!lExact && IsGenericCompare(typeTest))
				{
					Type p = typeTest.GetGenericArguments()[0];
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
			return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IComparable<>);
		} // func IsGenericCompare

		private static Type GetEqualableInterface(Type type1, Type Type2, ref bool lExact)
		{
			Type equalableInterface = null;
			foreach (Type typeTest in type1.GetInterfaces())
			{
				if (!lExact && typeTest.IsGenericType && typeTest.GetGenericTypeDefinition() == typeof(IEquatable<>))
				{
					Type p = typeTest.GetGenericArguments()[0];
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
			TypeCode tc = GetTypeCode(type);
			bool lIsArithmetic = IsArithmeticType(tc);

			if (lIsArithmetic) // simple arithmetic complement
			{
				#region -- simple arithmetic --
				Type typeOp = type;
				Type typeEnum = null;
				if (type.IsEnum)
				{
					typeEnum = type;
					typeOp = type.GetEnumUnderlyingType();
					tc = GetTypeCode(typeOp);
				}

				switch (tc)
				{
					case TypeCode.Double:
						typeOp = typeof(long);
						break;
					case TypeCode.Single:
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
			MemberInfo[] operators = type.GetMember(GetOperationMethodName(ExpressionType.OnesComplement), BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.InvokeMethod);
			MethodInfo miOperator = FindMethod(Array.ConvertAll(operators, o => (MethodInfo)o), new Type[] { type }, t => t, false);
			if (miOperator != null)
				return Expression.OnesComplement(Convert(runtime, expr, type, miOperator.GetParameters()[0].ParameterType, lParse), miOperator);
			#endregion

			#region -- inject convert --
			if (type == typeof(string))
				return Expression.Dynamic(runtime.GetUnaryOperationBinary(ExpressionType.OnesComplement), typeof(object), ParseNumberExpression(runtime, expr, type));
			#endregion

			#region -- try convert to integer --
			if (TryConvertType(runtime, LiftIntegerType(runtime, type), ref expr, ref type))
				return UnaryOperationComplementExpression(runtime, expr, type, lParse);
			#endregion

			throw new LuaEmitException(LuaEmitException.OperatorNotDefined, ExpressionType.OnesComplement, String.Empty, type.Name);
		} // func UnaryOperationComplementExpression

		private static Expression UnaryOperationNegateExpression(Lua runtime, Expression expr, Type type, bool lParse)
		{
			TypeCode tc = GetTypeCode(type);
			bool lIsArithmetic = IsArithmeticType(tc);

			if (lIsArithmetic) // simple arithmetic complement
			{
				#region -- simple arithmetic --
				Type typeOp = type;
				Type typeEnum = null;
				if (type.IsEnum)
				{
					typeEnum = type;
					typeOp = type.GetEnumUnderlyingType();
					tc = GetTypeCode(typeOp);
				}

				expr = Expression.OnesComplement(Convert(runtime, expr, type, LiftTypeSigned(tc, tc), lParse));

				if (typeEnum != null)
					expr = Expression.Convert(expr, typeEnum);

				return expr;
				#endregion
			}

			#region -- find operator --

			MemberInfo[] operators = type.GetMember(GetOperationMethodName(ExpressionType.Negate), BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.InvokeMethod);
			MethodInfo miOperator = FindMethod(Array.ConvertAll(operators, o => (MethodInfo)o), new Type[] { type }, t => t, false);
			if (miOperator != null)
				return Expression.Negate(Convert(runtime, expr, type, miOperator.GetParameters()[0].ParameterType, lParse), miOperator);
			#endregion

			#region -- inject convert --
			if (type == typeof(string))
				return Expression.Dynamic(runtime.GetUnaryOperationBinary(ExpressionType.Negate), typeof(object), ParseNumberExpression(runtime, expr, type));
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
				if (type.IsEnum)
				{
					typeEnum = type; // save enum
					type = type.GetEnumUnderlyingType();
				}

				if (op == ExpressionType.OnesComplement)
				{
					expr = Convert(runtime, expr, type, LiftIntegerType(runtime, type), lParse);
					type = expr.Type;
				}
				else if (op == ExpressionType.Negate)
				{
					TypeCode tc = GetTypeCode(type);
					switch (tc)
					{
						case TypeCode.Byte:
							expr = Convert(runtime, expr, type, typeof(short), lParse);
							type = expr.Type;
							break;
						case TypeCode.UInt16:
							expr = Convert(runtime, expr, type, typeof(int), lParse);
							type = expr.Type;
							break;
						case TypeCode.UInt32:
							expr = Convert(runtime, expr, type, typeof(long), lParse);
							type = expr.Type;
							break;
						case TypeCode.UInt64:
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
			BindingFlags bindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.InvokeMethod;

			// try to find a exact match for the operation

			miOperation = type.GetMethod(sMethodName, bindingFlags, null, new Type[] { type }, null);

			// can we inject a string conversation --> create a dynamic operation, that results in a simple arithmetic operation
			if (miOperation == null && type == typeof(string))
			{
				#region -- string inject for arithmetic --
				expr = ParseNumberExpression(runtime, expr, type);
				type = typeof(object);

				return Expression.Dynamic(runtime.GetUnaryOperationBinary(op), typeof(object),
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
					MethodInfo miConvert = FindConvertMethod(type.GetMethods(bindingFlags), type, typeInt, ref lImplicit, ref lExactFrom, ref lExactTo);
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
			TypeCode tc1 = GetTypeCode(type1);
			TypeCode tc2 = GetTypeCode(type2);
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
							case TypeCode.SByte:
							case TypeCode.Byte:
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
							case TypeCode.SByte:
							case TypeCode.Byte:
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
				if (typeOp.IsEnum)
				{
					if (type1.IsEnum && type2.IsEnum)
						typeEnum = typeOp; // save enum
					typeOp = typeOp.GetEnumUnderlyingType();
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
			BindingFlags bindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.InvokeMethod;
			string sOperationName = GetOperationMethodName(op);
			if (!String.IsNullOrEmpty(sOperationName))
			{
				// create a list of all operators
				MemberInfo[] members1 = type1.GetMember(sOperationName, bindingFlags);
				MemberInfo[] members2 = type2.GetMember(sOperationName, bindingFlags);
				MethodInfo[] members3 = new MethodInfo[members1.Length + members2.Length];
				if (members3.Length > 0)
				{
					Array.Copy(members1, 0, members3, 0, members1.Length);
					Array.Copy(members2, 0, members3, members1.Length, members2.Length);

					// Find the correct method
					MethodInfo miOperator = FindMethod(members3, parameterTypes, t => t, false);
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
						return Expression.Dynamic(runtime.GetBinaryOperationBinder(op), typeof(object),
							Convert(runtime, expr1, type1, typeof(object), false),
							ParseNumberExpression(runtime, expr2, type2)
						);
					}
					else if (type1 == typeof(string) && lIsArithmetic2)
					{
						return Expression.Dynamic(runtime.GetBinaryOperationBinder(op), typeof(object),
							ParseNumberExpression(runtime, expr1, type1),
							Convert(runtime, expr2, type2, typeof(object), false)
						);
					}
					else if (type1 == typeof(string) && type2 == typeof(string))
					{
						return Expression.Dynamic(runtime.GetBinaryOperationBinder(op), typeof(object),
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

			TypeCode tc1 = Type.GetTypeCode(type1);
			TypeCode tc2 = Type.GetTypeCode(type2);

			if (IsArithmeticType(tc1) && IsArithmeticType(tc2)) // process only arithmetic types
				return LiftType(type1, tc1, type2, tc2, lSigned);
			else
				return typeof(object);
		} // func LiftType

		private static Type LiftType(Type type1, TypeCode tc1, Type type2, TypeCode tc2, bool lSigned)
		{
			// Achtung: this code depends on the numeric representation of TypeCode

			if (IsFloatType(tc1) && IsFloatType(tc2)) // both are floats
				return tc1 < tc2 ? type2 : type1; // -> use the higest
			else if (IsFloatType(tc1)) // the first one is a float, the other one is a integer
				return type1; // -> use the float
			else if (IsFloatType(tc2)) // the second one is a float, the other one is a integer
				return type2; // -> use the float

			else if ((((int)tc1) & 1) == 1 && (((int)tc2) & 1) == 1) // both types are signed integers
				return tc1 < tc2 ? type2 : type1; // -> use the highest
			else if ((((int)tc1) & 1) == 1) // the first one is signed integer
			{
				if (tc1 > tc2) // the unsigned is lower then the signed
					return type1; // -> use the signed
				else // -> we need a higher signed integer
					return LiftTypeSigned(tc1, tc2);
			}
			else if ((((int)tc2) & 1) == 1)
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

		private static Type LiftTypeSigned(TypeCode tc1, TypeCode tc2)
		{
			switch (tc2)
			{
				case TypeCode.Byte:
					return typeof(short);
				case TypeCode.UInt16:
					return typeof(int);
				case TypeCode.UInt32:
					return typeof(long);
				case TypeCode.UInt64:
					return typeof(double);
				default:
					throw new InvalidOperationException(String.Format("Internal error in lift type ({0} vs. {1})", tc1, tc2));
			}
		} // func LiftTypeSigned

		private static Type LiftTypeNext(Lua runtime, Type type)
		{
			switch (Type.GetTypeCode(type))
			{
				case TypeCode.SByte:
					return typeof(short);
				case TypeCode.Byte:
					return typeof(ushort);
				case TypeCode.Int16:
					return typeof(int);
				case TypeCode.UInt16:
					return typeof(uint);
				case TypeCode.Int32:
					return typeof(long);
				case TypeCode.UInt32:
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
				case TypeCode.SByte:
				case TypeCode.Byte:
				case TypeCode.Int16:
				case TypeCode.UInt16:
				case TypeCode.Int32:
				case TypeCode.UInt32:
				case TypeCode.Int64:
				case TypeCode.UInt64:
					return type;
				case TypeCode.Single:
					return typeof(int);
				case TypeCode.Double:
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
					type.GetMember(sMemberName, GetBindingFlags(instance != null, lIgnoreCase)); // get the current members

			if (members == null || members.Length == 0) // no member found, try later again
			{
				if (lParse)
					return Expression.Dynamic(runtime.GetGetMemberBinder(sMemberName), typeof(object), Convert(runtime, instance, type, typeof(object), false));
				else
					return Expression.Default(typeof(object));
			}
			else if (members.Length > 1 && members[0].MemberType == MemberTypes.Method) // multiple member
			{
				return Expression.New(Lua.OverloadedMethodConstructorInfo, instance == null ? Expression.Default(typeof(object)) : instance, Expression.Constant(Lua.RtConvertArray(members, typeof(MethodInfo)), typeof(MethodInfo[])));
			}
			else // return the one member
			{
				var member = members[0];

				if (instance != null)
					instance = Convert(runtime, instance, type, type, false);

				if (member.MemberType == MemberTypes.Field)
				{
					return Expression.MakeMemberAccess(instance, member);
				}
				else if (member.MemberType == MemberTypes.Property)
				{
					PropertyInfo pi = (PropertyInfo)member;
					if (!pi.CanRead)
						throw new LuaEmitException(LuaEmitException.CanNotReadMember, type.Name, sMemberName);

					return Expression.MakeMemberAccess(instance, member);
				}
				else if (member.MemberType == MemberTypes.Method)
				{
					return Expression.New(Lua.MethodConstructorInfo, instance == null ? Expression.Default(typeof(object)) : instance, Expression.Constant(member, typeof(MethodInfo)));
				}
				else if (member.MemberType == MemberTypes.Event)
				{
					return Expression.New(Lua.EventConstructorInfo, instance == null ? Expression.Default(typeof(object)) : instance, Expression.Constant((EventInfo)member));
				}
				else if (member.MemberType == MemberTypes.NestedType)
				{
					return Expression.Call(Lua.TypeGetTypeMethodInfoArgType, Expression.Constant((Type)member));
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
				return Expression.Dynamic(runtime.GetSetMemberBinder(sMemberName), typeof(object),
					Convert(runtime, instance, type, typeof(object), false),
					Convert(runtime, set, typeSet, typeof(object), false)
				);
			}

			MemberInfo[] members = type.GetMember(sMemberName, GetBindingFlags(instance != null, lIgnoreCase));
			instance = instance == null ? null : Convert(runtime, instance, type, type, lParse);

			if (members == null || members.Length == 0)
				throw new LuaEmitException(LuaEmitException.MemberNotFound, type.Name, sMemberName);
			else if (members.Length > 1)
				throw new LuaEmitException(LuaEmitException.MemberNotUnique, type.Name, sMemberName);
			else
			{
				if (members[0].MemberType == MemberTypes.Property)
				{
					PropertyInfo pi = (PropertyInfo)members[0];
					if (!pi.CanWrite)
						throw new LuaEmitException(LuaEmitException.CanNotWriteMember, type.Name, sMemberName);
					return Expression.Assign(Expression.Property(instance, pi), Convert(runtime, set, typeSet, pi.PropertyType, lParse));
				}
				else if (members[0].MemberType == MemberTypes.Field)
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
				return Expression.Dynamic(runtime.GetGetIndexMember(new CallInfo(arguments.Length)), typeof(object),
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
				return Expression.Dynamic(runtime.GetSetIndexMember(new CallInfo(arguments.Length)), typeof(object),
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
			if (typeof(Array).IsAssignableFrom(instanceType)) // type is an array
			{
				// create index as integers
				Expression[] indexes = new Expression[arguments.Length];
				for (int i = 0; i < indexes.Length; i++)
					indexes[i] = Convert(runtime, getExpr(arguments[i]), getType(arguments[i]), typeof(int), lParse);

				return Expression.ArrayAccess(Convert(runtime, instance, instanceType, instanceType, lParse), indexes);
			}
			else // try find a property
			{
				PropertyInfo[] properties =
					(
						from m in instanceType.GetMembers(GetBindingFlags(instance != null, false) | BindingFlags.GetProperty)
						where m is PropertyInfo && ((PropertyInfo)m).GetIndexParameters().Length > 0
						select (PropertyInfo)m
					).ToArray();

				PropertyInfo piIndex = FindMember(properties, arguments, getType);

				if (piIndex == null)
					throw new LuaEmitException(LuaEmitException.IndexNotFound, instanceType.Name);
				else
					return BindParameter(runtime,
						args => Expression.MakeIndex(Convert(runtime, instance, instanceType, instanceType, lParse), piIndex, args),
						piIndex.GetIndexParameters(),
						arguments,
						getExpr, getType, lParse);
			}
		} // func GetIndexAccess

		#endregion

		#region -- BindParameter ----------------------------------------------------------

		public static Expression BindParameter<T>(Lua runtime, Func<Expression[], Expression> emitCall, ParameterInfo[] parameters, T[] arguments, Func<T, Expression> getExpr, Func<T, Type> getType, bool lParse)
		{
			Expression[] exprPara = new Expression[parameters.Length]; // Parameters for the function
			int iParameterIndex = 0;                  // Index of the parameter that is processed
			int iArgumentsIndex = 0;                  // Index of the argument that is processed
			int iParameterCount = parameters.Length;  // Parameter count
			ParameterExpression varLuaResult = null;  // variable that olds the reference to the last argument (that is a LuaResult)
			int iLastArgumentStretchCount = -1;       // counter for the LuaResult return
			ParameterInfo parameter = null;           // Current parameter that is processed

			List<ParameterExpression> variablesToReturn = new List<ParameterExpression>();
			List<Expression> callBlock = new List<Expression>();

			while (iParameterIndex < iParameterCount)
			{
				parameter = parameters[iParameterIndex];

				if (iParameterIndex == iParameterCount - 1 && parameter.ParameterType.IsArray)
				{
					#region -- generate vararg for an array --
					Type typeArray = parameter.ParameterType.GetElementType();
					if (varLuaResult != null) // create a array of the LuaResult for the last parameter
					{
						exprPara[iParameterIndex] = Expression.Convert(Expression.Call(Lua.GetResultValuesMethodInfo, varLuaResult, Expression.Constant(iLastArgumentStretchCount), Expression.Constant(typeArray)), parameter.ParameterType);
					}
					else if (iArgumentsIndex == arguments.Length - 1 && getType(arguments[iArgumentsIndex]).IsArray) // last parameter expect a array and we have an array
					{
						exprPara[iParameterIndex] = Convert(runtime, getExpr(arguments[iArgumentsIndex]), getType(arguments[iArgumentsIndex]), parameter.ParameterType, lParse);
						iArgumentsIndex++;
					}
					else if (iArgumentsIndex >= arguments.Length) // no arguments left
					{
						exprPara[iParameterIndex] = Expression.NewArrayInit(typeArray);
					}
					else
					{
						List<Expression> exprCollectedArguments = new List<Expression>();

						// collect all arguments that are left
						for (; iArgumentsIndex < arguments.Length - 1; iArgumentsIndex++)
							exprCollectedArguments.Add(Convert(runtime, getExpr(arguments[iArgumentsIndex]), getType(arguments[iArgumentsIndex]), typeArray, lParse));

						// the last argument is a LuaResult
						Expression tmpExpr = getExpr(arguments[iArgumentsIndex]);
						Type tmpType = getType(arguments[iArgumentsIndex]);
						iArgumentsIndex++;
						if (tmpType == typeof(LuaResult))
						{
							if (exprCollectedArguments.Count == 0) // no arguments collected, convert the array
							{
								exprPara[iParameterIndex] = Expression.Convert(Expression.Call(Lua.GetResultValuesMethodInfo, Convert(runtime, tmpExpr, tmpType, tmpType, false), Expression.Constant(0), Expression.Constant(typeArray)), parameter.ParameterType);
							}
							else // combine the arguments and the last result to the correct array
							{
								exprPara[iParameterIndex] = Expression.Convert(
									Expression.Call(null, Lua.CombineArrayWithResultMethodInfo,
										Expression.Convert(Expression.NewArrayInit(typeArray, exprCollectedArguments), typeof(Array)),
										Convert(runtime, tmpExpr, tmpType, tmpType, lParse),
										Expression.Constant(typeArray)
									),
									parameter.ParameterType
								);
							}
						}
						else // normal argument
						{
							exprCollectedArguments.Add(Convert(runtime, tmpExpr, tmpType, typeArray, lParse));

							exprPara[iParameterIndex] = Expression.NewArrayInit(typeArray, exprCollectedArguments);
						}
					}
					#endregion
				}
				else
				{
					#region -- generate set argument --
					Expression exprGet; // Holds the argument get

					// get the type for the parameter
					Type typeParameter = parameter.ParameterType.IsByRef ? parameter.ParameterType.GetElementType() : parameter.ParameterType;

					if (parameter.IsOut) // out-param no value neede
					{
						exprGet = null;
					}
					else if (iArgumentsIndex == arguments.Length - 1 && getType(arguments[iArgumentsIndex]) == typeof(LuaResult)) // The last argument is a LuaResult (eg. function), start the stretching of the array
					{
						varLuaResult = Expression.Variable(typeof(LuaResult), "#result");
						exprGet = GetResultExpression(runtime,
							Expression.Assign(varLuaResult, Convert(runtime, getExpr(arguments[iArgumentsIndex]), typeof(LuaResult), typeof(LuaResult), false)),
							typeof(LuaResult),
							0,
							typeParameter,
							GetDefaultParameterExpression(parameter, typeParameter),
							lParse
						);
						iLastArgumentStretchCount = 1;
						iArgumentsIndex++;
					}
					else if (iLastArgumentStretchCount > 0) // get the expression of the LuaResult to stretch it
					{
						exprGet = GetResultExpression(runtime, varLuaResult, typeof(LuaResult), iLastArgumentStretchCount++, typeParameter, GetDefaultParameterExpression(parameter, typeParameter), lParse);
					}
					else if (iArgumentsIndex < arguments.Length) // assign a normal parameter
					{
						exprGet = Convert(runtime, getExpr(arguments[iArgumentsIndex]), getType(arguments[iArgumentsIndex]), typeParameter, lParse);
						iArgumentsIndex++;
					}
					else // No arguments left, if we have a default value, set it or use the default of the type
						exprGet = GetDefaultParameterExpression(parameter, typeParameter);

					// Create a variable for the byref parameters
					if (parameter.ParameterType.IsByRef)
					{
						ParameterExpression r = Expression.Variable(typeParameter, "r" + iParameterIndex.ToString());
						variablesToReturn.Add(r);
						if (exprGet != null)
							callBlock.Add(Expression.Assign(r, exprGet));
						exprPara[iParameterIndex] = exprGet = r;
					}

					exprPara[iParameterIndex] = exprGet;
					#endregion
				}

				iParameterIndex++;
			}

			bool lArgumentsLeft = iArgumentsIndex < arguments.Length;
			if (variablesToReturn.Count > 0 || varLuaResult != null || (lParse && lArgumentsLeft)) // we have variables or arguments are left out
			{
				// add the call
				Expression exprCall = emitCall(exprPara);
				ParameterExpression varReturn = null;
				if (exprCall.Type != typeof(void) && (lArgumentsLeft || variablesToReturn.Count > 0)) // create a return variable, if we have variables or arguments left
				{
					varReturn = Expression.Variable(exprCall.Type, "#return");
					callBlock.Add(Expression.Assign(varReturn, exprCall));
					variablesToReturn.Insert(0, varReturn);
				}
				else // add the call normally
					callBlock.Add(exprCall);

				// argument left
				if (lArgumentsLeft)
				{
					for (; iArgumentsIndex < arguments.Length; iArgumentsIndex++)
						callBlock.Add(getExpr(arguments[iArgumentsIndex]));
				}

				// create the variable definition
				int iVarResultExists = varLuaResult != null ? 1 : 0;
				ParameterExpression[] variables = new ParameterExpression[variablesToReturn.Count + iVarResultExists];
				variablesToReturn.CopyTo(variables, iVarResultExists);
				if (iVarResultExists > 0)
					variables[0] = varLuaResult;

				if (variablesToReturn.Count == 0) // no multi or return variables results
				{
					return Expression.Block(lArgumentsLeft ? typeof(void) : exprCall.Type, variables, callBlock);
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
				return emitCall(exprPara);
		} // func BindParameter

		private static Expression GetDefaultParameterExpression(ParameterInfo parameter, Type typeParameter)
		{
			if (parameter.IsOptional)
				return Expression.Constant(parameter.DefaultValue, typeParameter);
			else
				return Expression.Default(typeParameter);
		} // func GetDefaultParameterExpression

		#endregion

		#region -- FindMember -------------------------------------------------------------

		public static MethodInfo FindMethod<TARG>(MethodInfo[] members, TARG[] arguments, Func<TARG, Type> getType, bool lExtension)
			where TARG : class
		{
			MethodInfo mi = (MethodInfo)FindMember(members, arguments, getType, lExtension);

			// create a non generic version
			if (mi != null && mi.ContainsGenericParameters)
				mi = MakeNonGenericMethod(mi, arguments, getType);

			return mi;
		} // func FindMethod

		public static TMEMBERTYPE FindMember<TMEMBERTYPE, TARG>(TMEMBERTYPE[] members, TARG[] arguments, Func<TARG, Type> getType, bool lExtension = false)
			where TMEMBERTYPE : MemberInfo
			where TARG : class
		{
			int iMaxParameterLength = 0;    // Max length of the argument list, can also MaxInt for variable argument length
			TMEMBERTYPE miBind = null;      // Member that matches best
			int iCurParameterLength = 0;    // Length of the arguments of the current match
			int iCurMatchCount = -1;        // How many arguments match of this list
			int iCurMatchExactCount = -1;   // How many arguments match exact of this list
			bool lLastArgumentIsArray = arguments.Length > 0 && getType(arguments[arguments.Length - 1]).IsArray;

			// Get the max. list of arguments we want to consume
			if (arguments.Length > 0)
			{
				iMaxParameterLength =
					getType(arguments[arguments.Length - 1]) == typeof(LuaResult) ?
					Int32.MaxValue :
					arguments.Length;
			}

			for (int i = 0; i < members.Length; i++)
			{
				TMEMBERTYPE miCur = members[i];
				if (miCur != null)
				{
					// do not test methods with __arglist
					MethodInfo methodInfo = miCur as MethodInfo;
					if (methodInfo != null && (methodInfo.CallingConvention & CallingConventions.VarArgs) != 0)
						continue;

					// Get the Parameters
					ParameterInfo[] parameters = GetMemberParameter(miCur, lExtension);

					// How many parameters we have
					int iParametersLength = parameters.Length;
					if (iParametersLength > 0 && !lLastArgumentIsArray && parameters[iParametersLength - 1].ParameterType.IsArray)
						iParametersLength = Int32.MaxValue;

					// We have already a match, is the new one better
					if (miBind != null)
					{
						if (iParametersLength == iMaxParameterLength && iCurParameterLength == iMaxParameterLength)
						{
							// Get the parameter of the current match
							ParameterInfo[] curParameters = iCurMatchCount == -1 ? GetMemberParameter(miBind, lExtension) : null;
							int iNewMatchCount = 0;
							int iNewMatchExactCount = 0;
							int iCount;

							// Count the parameters of the current match, because they are not collected
							if (curParameters != null)
							{
								iCurMatchCount = 0;
								iCurMatchExactCount = 0;
							}

							// Max length of the parameters
							if (iCurParameterLength == Int32.MaxValue)
							{
								iCount = parameters.Length;
								if (curParameters != null && iCount < curParameters.Length)
									iCount = curParameters.Length;
							}
							else
								iCount = iCurParameterLength;

							// Check the matches
							for (int j = 0; j < iCount; j++)
							{
								bool lExact;
								if (curParameters != null && IsMatchParameter(j, curParameters, arguments, getType, out lExact))
								{
									iCurMatchCount++;
									if (lExact)
										iCurMatchExactCount++;
								}
								if (IsMatchParameter(j, parameters, arguments, getType, out lExact))
								{
									iNewMatchCount++;
									if (lExact)
										iNewMatchExactCount++;
								}
							}
							if (iNewMatchCount > iCurMatchCount ||
								iNewMatchCount == iCurMatchCount && iNewMatchExactCount > iCurMatchExactCount)
							{
								miBind = miCur;
								iCurParameterLength = iParametersLength;
								iCurMatchCount = iNewMatchCount;
								iCurMatchExactCount = iNewMatchExactCount;
							}
						}
						else if (iMaxParameterLength == iParametersLength && iCurParameterLength != iMaxParameterLength ||
							iMaxParameterLength != iCurParameterLength && iCurParameterLength < iParametersLength)
						{
							// if the new parameter length is greater then the old one, it matches best
							miBind = miCur;
							iCurParameterLength = iParametersLength;
							iCurMatchCount = -1;
							iCurMatchExactCount = -1;
						}
					}
					else
					{
						// First match, take it
						miBind = miCur;
						iCurParameterLength = iParametersLength;
						iCurMatchCount = -1;
						iCurMatchExactCount = -1;
					}
				}
			}

			return miBind;
		} // func FindMember

		private static ParameterInfo[] GetMemberParameter<TMEMBERTYPE>(TMEMBERTYPE mi, bool lExtensions)
			where TMEMBERTYPE : MemberInfo
		{
			MethodBase mb = mi as MethodBase;
			PropertyInfo pi = mi as PropertyInfo;

			if (mb != null)
			{
				if (lExtensions && mb.IsStatic)
					return mb.GetParameters().Skip(1).ToArray();
				else
					return mb.GetParameters();
			}
			else if (pi != null)
				return pi.GetIndexParameters();
			else
				throw new ArgumentException();
		} // func GetMemberParameter

		private static bool IsMatchParameter<TARG>(int j, ParameterInfo[] parameters, TARG[] arguments, Func<TARG, Type> getType, out bool lExact)
		{
			if (j < parameters.Length && j < arguments.Length)
			{
				Type type1 = parameters[j].ParameterType;
				Type type2 = getType(arguments[j]);

				if (type1 == type2) // exact equal types
				{
					lExact = true;
					return true;
				}
				else if (type1.IsGenericParameter) // the parameter is a generic type
				{
					#region -- check generic --
					Type[] typeConstraints = type1.GetGenericParameterConstraints();

					lExact = false;

					// check "class"
					if ((type1.GenericParameterAttributes & GenericParameterAttributes.ReferenceTypeConstraint) != 0 && type2.IsValueType)
						return false;

					// check struct
					if ((type1.GenericParameterAttributes & GenericParameterAttributes.NotNullableValueTypeConstraint) != 0 && !type2.IsValueType)
						return false;

					// check new()
					if ((type1.GenericParameterAttributes & GenericParameterAttributes.DefaultConstructorConstraint) != 0)
					{
						// check default ctor
						if (type2.GetConstructor(new Type[0]) == null)
							return false;
					}

					// no contraints, all is allowed
					if (typeConstraints.Length == 0)
						return true;

					// search for the constraint
					bool lNoneExactMatch = false;
					for (int i = 0; i < typeConstraints.Length; i++)
					{
						if (typeConstraints[i] == type2)
						{
							lExact = true;
							return true;
						}
						else if (typeConstraints[i].IsAssignableFrom(type2))
							lNoneExactMatch = true;
					}

					if (lNoneExactMatch)
					{
						lExact = false;
						return true;
					}
					#endregion
				}
				else if (TypesMatch(type1, type2, out lExact)) // is at least assignable
				{
					return true;
				}
			}

			lExact = false;
			return false;
		} // func IsMatchParameter

		private static MethodInfo MakeNonGenericMethod<TARG>(MethodInfo mi, TARG[] arguments, Func<TARG, Type> getType)
			where TARG : class
		{
			ParameterInfo[] parameters = mi.GetParameters();
			Type[] genericArguments = mi.GetGenericArguments();
			Type[] genericParameter = new Type[genericArguments.Length];

			for (int i = 0; i < genericArguments.Length; i++)
			{
				Type t = null;

				// look for the typ
				for (int j = 0; j < parameters.Length; j++)
				{
					if (parameters[j].ParameterType == genericArguments[i])
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
			else if (t.IsAssignableFrom(type))
				return t;
			else if (type.IsAssignableFrom(t))
				return type;
			else
				return typeof(object);
		} // func CombineType

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
