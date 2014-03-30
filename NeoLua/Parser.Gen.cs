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
    private static Expression WrapDebugInfo(bool lWrap, bool lAfter, Token tStart, Token tEnd, Expression expr)
    {
      if (lWrap)
      {
        if (lAfter)
        {
          if (expr.Type == typeof(void))
            return Expression.Block(expr, GetDebugInfo(tStart, tEnd));
          else
          {
            ParameterExpression r = Expression.Variable(expr.Type);
            return Expression.Block(r.Type, new ParameterExpression[] { r },
              Expression.Assign(r, expr),
              GetDebugInfo(tStart, tEnd),
              r);
          }
        }
        else
          return Expression.Block(GetDebugInfo(tStart, tEnd), expr);
      }
      else
        return expr;
    } // func WrapDebugInfo

    private static Expression GetDebugInfo(Token tStart, Token tEnd)
    {
      return Expression.DebugInfo(tStart.Start.Document, tStart.Start.Line, tStart.Start.Col, tEnd.End.Line, tEnd.End.Col);
    } // func GetDebugInfo

    internal static Expression ToTypeExpression(Expression expr, Type type = null, bool? lForce = null)
    {
      if (expr.Type == typeof(LuaResult))
        return ToTypeExpression(GetResultExpression(expr, 0), type, lForce);
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

    internal static Expression GetResultExpression(Expression target, int iIndex)
    {
      return Expression.MakeIndex(
        Expression.Convert(target, typeof(LuaResult)),
        Lua.ResultIndexPropertyInfo,
        new Expression[] { Expression.Constant(iIndex) }
        );
    } // func GetFirstValueExpression

    internal static Expression RuntimeHelperExpression(LuaRuntimeHelper runtimeHelper, params Expression[] args)
    {
      return Expression.Call(Lua.GetRuntimeHelper(runtimeHelper), args);
    } // func GetRuntimeHelper

    internal static Expression RuntimeHelperConvertExpression(Expression value, Type fromType, Type toType)
    {
      if (toType.IsAssignableFrom(fromType))
        return ToTypeExpression(value, toType);
      else
        return ToTypeExpression(
          RuntimeHelperExpression(LuaRuntimeHelper.Convert,
            ToTypeExpression(value, typeof(object)),
            Expression.Constant(toType, typeof(Type))
          ),
          toType
        );
    } // func RuntimeHelperConvertExpression

    #region -- Unary Operation Generator ----------------------------------------------

    internal static Expression UnaryOperationExpression(Lua runtime, ExpressionType op, Expression expr, Type type)
    {
      if (runtime == null || IsNumericType(type) || op == ExpressionType.Not)
        if (op == ExpressionType.Not)
          return Expression.MakeUnary(op, ToBooleanExpression(expr), type);
        else
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
          return RuntimeHelperConvertExpression(
              Expression.MakeBinary(
                ExpressionType.Power,
                RuntimeHelperConvertExpression(expr1, type1, typeof(double)),
                RuntimeHelperConvertExpression(expr2, type2, typeof(double))
              ),
              typeof(double),
              GetNumericResultType(type1, type2));
        }
        else if (runtime == null || IsNumericType(type1) && IsNumericType(type2))
        {
          // Make the types Equal
          Type typeForOp = GetNumericResultType(type1, type2);

          return Expression.MakeBinary(op,
            RuntimeHelperConvertExpression(expr1, type1, typeForOp),
            RuntimeHelperConvertExpression(expr2, type2, typeForOp)
            );
        }
        else
          return Expression.Dynamic(runtime.GetBinaryOperationBinder(op), typeof(object), ToTypeExpression(expr1, typeof(object)), ToTypeExpression(expr2, typeof(object)));
      }
    } // func BinaryOperationExpression

    private static Expression BinaryOperationCompareExpression(Lua runtime, ExpressionType op, Expression expr1, Type type1, Expression expr2, Type type2)
    {
      if (IsNumericType(type1) && IsNumericType(type2)) // compare of to integers, do not neet a dynamic
      {
        Type typeCompare = GetNumericResultType(type1, type2);
        return Expression.MakeBinary(op, RuntimeHelperConvertExpression(expr1, type1, typeCompare), RuntimeHelperConvertExpression(expr2, type2, typeCompare));
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
              Parser.RuntimeHelperConvertExpression(expr1, type1, typeForOp),
              Parser.RuntimeHelperConvertExpression(expr2, type2, typeForOp)
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

    internal static bool IsNumericType(Type type)
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

    internal static Type GetDelegateType(MethodInfo mi)
    {
      return Expression.GetDelegateType(
        (
          from p in mi.GetParameters()
          select p.ParameterType
        ).Concat(
          new Type[] { mi.ReturnType }
        ).ToArray()
      );
    } // func GetDelegateType

    internal static Delegate CreateDelegate(MethodInfo mi)
    {
      if ((mi.CallingConvention & CallingConventions.VarArgs) != 0)
        throw new ArgumentException("Call of VarArgs not implemented.");
      Type typeDelegate = GetDelegateType(mi);
      return Delegate.CreateDelegate(typeDelegate, mi);
    } // func CreateDelegateFromMethodInfo

  } // class Parser

  #endregion
}
