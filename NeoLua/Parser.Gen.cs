using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace Neo.IronLua
{
  #region -- class Parser -------------------------------------------------------------

  ///////////////////////////////////////////////////////////////////////////////
  /// <summary></summary>
  internal static partial class Parser
  {
    internal static Expression ToTypeExpression(Expression expr, Type type = null, bool? lForce = null)
    {
      if (expr.Type == typeof(object[]))
        return ToTypeExpression(RuntimeHelperExpression(LuaRuntimeHelper.GetObject, Expression.Convert(expr, typeof(object[])), Expression.Constant(0, typeof(int))), type, lForce);
      else if (type != null && (lForce.HasValue && lForce.Value || expr.Type != type))
        return Expression.Convert(expr, type);
      else
        return expr;
    } // func ToTypeExpression

    private static Expression ToBooleanExpression(Expression expr)
    {
      if (expr.Type == typeof(bool))
        return expr;
      else if (typeof(bool).IsAssignableFrom(expr.Type))
        return Expression.Convert(expr, typeof(bool));
      else
        return Expression.Dynamic(Lua.ConvertToBooleanBinder, typeof(bool), ToTypeExpression(expr, typeof(object)));
    } // func ToBooleanExpression

    internal static Expression RuntimeHelperExpression(LuaRuntimeHelper runtimeHelper, params Expression[] args)
    {
      return Expression.Call(Lua.GetRuntimeHelper(runtimeHelper), args);
    } // func GetRuntimeHelper

    internal static Expression RuntimeHelperConvertExpression(Expression value, Type toType)
    {
      if (toType.IsAssignableFrom(value.Type))
        return ToTypeExpression(value, toType);
      else
        return ToTypeExpression(RuntimeHelperExpression(LuaRuntimeHelper.Convert, ToTypeExpression(value, typeof(object)), Expression.Constant(toType, typeof(Type))), toType);
    } // func RuntimeHelperConvertExpression

    #region -- Unary Operation Generator ----------------------------------------------

    internal static Expression UnaryOperationExpression(Lua runtime, ExpressionType op, Expression expr, Type type)
    {
      if (runtime == null || IsNumericType(type))
        return Expression.MakeUnary(op, ToTypeExpression(expr), type);
      else
        return Expression.Dynamic(runtime.GetUnaryOperationBinary(op), typeof(object), ToTypeExpression(expr, typeof(object)));
    } // func UnaryOperationExpression

    #endregion

    #region -- Binary Operation Generator ---------------------------------------------

    internal static Expression BinaryOperationExpression(Lua runtime, ExpressionType op, Expression expr1, Expression expr2)
    {
      return BinaryOperationExpression(runtime, op, expr1, expr1.Type, expr2, expr2.Type);
    } // func BinaryOperationExpression

    internal static Expression BinaryOperationExpression(Lua runtime, ExpressionType op, Expression expr1, Type type1, Expression expr2, Type type2)
    {
      if (op == ExpressionType.Equal || op == ExpressionType.NotEqual ||
        op == ExpressionType.LessThan || op == ExpressionType.LessThanOrEqual ||
        op == ExpressionType.GreaterThan || op == ExpressionType.GreaterThanOrEqual)
      {
        return BinaryOperationCompareExpression(runtime, op, expr1, type1, expr2, type2);
      }
      else
      {
        if (op == ExpressionType.Power) // power needs double
        {
          return Parser.RuntimeHelperConvertExpression(
              Expression.MakeBinary(
                ExpressionType.Power,
                Parser.RuntimeHelperConvertExpression(expr1, typeof(double)),
                Parser.RuntimeHelperConvertExpression(expr2, typeof(double))
              ),
              GetNumericResultType(expr1.Type, expr2.Type));
        }
        else if (runtime == null || IsNumericType(type1) && IsNumericType(type2))
        {
          // Make the types Equal
          Type typeForOp = GetNumericResultType(type1, type2);

          return Expression.MakeBinary(op,
            Parser.RuntimeHelperConvertExpression(expr1, typeForOp),
            Parser.RuntimeHelperConvertExpression(expr2, typeForOp)
            );
        }
        else
          return Expression.Dynamic(runtime.GetBinaryOperationBinder(op), typeof(object), ToTypeExpression(expr1, typeof(object)), ToTypeExpression(expr2, typeof(object)));
      }
    } // func BinaryOperationExpression

    private static Expression BinaryOperationCompareExpression(Lua runtime, ExpressionType op, Expression expr1, Type type1, Expression expr2, Type type2)
    {
      if (runtime != null && IsNumericType(type1) && IsNumericType(type2)) // compare of to integers, do not neet a dynamic
      {
        Type typeCompare = GetNumericResultType(type1, type2);
        return Expression.MakeBinary(op, ToTypeExpression(expr1, type1), ToTypeExpression(expr2, type2));
      }
      else
      {
        // Test for generic IComparable<>
        if (type1.IsClass && type1 == type2 && type1 != typeof(object))
        {
          Type typeCompare = typeof(IComparable<>).MakeGenericType(type1);
          if (typeCompare.IsAssignableFrom(type1))
            return ComparableExpression(op, typeCompare, expr1, expr2, type2);
        }
        else
        {
          Type typeCompare1 = type2.IsClass ? typeof(IComparable<>).MakeGenericType(type1) : null;
          Type typeCompare2 = type1.IsClass ? typeof(IComparable<>).MakeGenericType(type2) : null;
          if (typeCompare2 != null && typeCompare2.IsAssignableFrom(type1))
          {
            return ComparableExpression(op, typeCompare2, expr1, expr2, type2);
          }
          else if (typeCompare1 != null && typeCompare1.IsAssignableFrom(type2))
          {
            return ComparableExpression(ReverseBinaryOperation(op), typeCompare1, expr2, expr1, type1);
          }
        }

        if (!IsNumericType(type1) && !IsNumericType(type2))// Test for none generic IComparable only for none numeric types
        {
          if (typeof(IComparable).IsAssignableFrom(type1)) // ICompareable implemented
          {
            return ComparableExpression(op, typeof(IComparable), expr1, expr2, typeof(object));
          }
          else if (type1 != typeof(string) && typeof(IComparable).IsAssignableFrom(type2)) // ICompareable implemented, do the operation reverse
          {
            return ComparableExpression(ReverseBinaryOperation(op), typeof(IComparable), expr2, expr1, typeof(object));
          }
        }

        if (runtime == null) // Try a numeric operation
        {
          if (type1 != type2 && (op == ExpressionType.Equal || op == ExpressionType.NotEqual))
            return Expression.Constant(op != ExpressionType.Equal, typeof(bool));
          else
          {
            Type typeForOp = GetNumericResultType(type1, type2);
            return Expression.MakeBinary(op,
              Parser.RuntimeHelperConvertExpression(expr1, typeForOp),
              Parser.RuntimeHelperConvertExpression(expr2, typeForOp)
              );
          }
        }
        else // generate the dynamic operation
          return ToTypeExpression(Expression.Dynamic(runtime.GetBinaryOperationBinder(op), typeof(object), ToTypeExpression(expr1, typeof(object)), ToTypeExpression(expr2, typeof(object))), typeof(bool));
      }
    } // func BinaryOperationCompareExpression

    private static ExpressionType ReverseBinaryOperation(ExpressionType op)
    {
      if (op == ExpressionType.GreaterThan)
        op = ExpressionType.LessThan;
      else if (op == ExpressionType.LessThan)
        op = ExpressionType.GreaterThan;
      else if (op == ExpressionType.GreaterThanOrEqual)
        op = ExpressionType.LessThanOrEqual;
      else if (op == ExpressionType.LessThanOrEqual)
        op = ExpressionType.GreaterThanOrEqual;
      return op;
    } // func ReverseBinaryOperation

    private static Expression ComparableExpression(ExpressionType op, Type compare, Expression expr1, Expression expr2, Type type2)
    {
      MethodInfo mi = compare.GetMethod("CompareTo", BindingFlags.Public | BindingFlags.Instance | BindingFlags.InvokeMethod);
      return Expression.MakeBinary(op, Expression.Call(Expression.Convert(expr1, compare), mi, ToTypeExpression(expr2, type2)), Expression.Constant(0, typeof(int)));
    } // ComparableExpression

    #endregion

    private static Type GetNumericResultType(Type type1, Type type2)
    {
      TypeCode tc1 = Type.GetTypeCode(type1);
      TypeCode tc2 = Type.GetTypeCode(type2);
      if (tc1 == tc2)
        return type1;
      else if (tc1 >= TypeCode.SByte && tc1 <= TypeCode.Int32 &&
        tc2 >= TypeCode.SByte && tc2 <= TypeCode.Int32)
        return typeof(int);
      else if (tc1 >= TypeCode.SByte && tc1 <= TypeCode.Int64 &&
        tc2 >= TypeCode.SByte && tc2 <= TypeCode.Int64)
        return typeof(long);
      else if (tc1 >= TypeCode.SByte && tc1 <= TypeCode.UInt64 &&
        tc2 >= TypeCode.SByte && tc2 <= TypeCode.UInt64)
        return typeof(ulong);
      else if (tc1 >= TypeCode.SByte && tc1 <= TypeCode.Double &&
        tc2 >= TypeCode.SByte && tc2 <= TypeCode.Double)
        return typeof(double);
      else if (tc1 >= TypeCode.SByte && tc1 <= TypeCode.Decimal &&
        tc2 >= TypeCode.SByte && tc2 <= TypeCode.Decimal)
        return typeof(decimal);
      else
        return typeof(double);
    } // func GetNumericResultType

    private static Type GetAndOrResultType(Type type1, Type type2)
    {
      if (type1 == type2)
        return type1;
      else if (type1.IsSubclassOf(type2))
        return type2;
      else if (type2.IsSubclassOf(type1))
        return type1;
      else if (IsNumericType(type1) && IsNumericType(type2))
        return GetNumericResultType(type1, type2);
      else
        return typeof(object);
    } // func GetAndOrResultType

    private static bool IsNumericType(Type type)
    {
      switch (Type.GetTypeCode(type))
      {
        case TypeCode.Double:
        case TypeCode.Single:
        case TypeCode.Decimal:
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
    } // func IsNumericType
  } // class Parser

  #endregion
}
