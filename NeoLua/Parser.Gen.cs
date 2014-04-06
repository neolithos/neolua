using System;
using System.Collections.Generic;
using System.Dynamic;
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
    private const string csImplicit = "op_Implicit";
    private const string csExplicit = "op_Explicit";

    #region -- Debug-Information ------------------------------------------------------

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

    #endregion

    internal static Expression RuntimeHelperExpression(LuaRuntimeHelper runtimeHelper, params Expression[] args)
    {
      return Expression.Call(Lua.GetRuntimeHelper(runtimeHelper), args);
    } // func GetRuntimeHelper
    
    #region -- ConvertValue -----------------------------------------------------------

    internal static object ConvertValue(object value, Type toType)
    {
      if (value == null)
        if (toType.IsValueType)
          return Activator.CreateInstance(toType);
        else
          return null;
      else
      {
        Type fromType = value.GetType();
        if (fromType == toType)
          return value;
        else if (fromType == typeof(LuaResult))
          return ConvertValue(((LuaResult)value)[0], toType);
        else if (toType == typeof(string))
        {
          foreach (MethodInfo mi in fromType.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.InvokeMethod))
          {
            if ((mi.Name == Parser.csExplicit || mi.Name == Parser.csImplicit) &&
              mi.ReturnType == typeof(string))
              return mi.Invoke(null, new object[] { value });
          }

          return value == null ? String.Empty : Convert.ToString(value, CultureInfo.InvariantCulture);
        }
        else
        {
          TypeCode tcFrom = Type.GetTypeCode(fromType);
          TypeCode tcTo = GetTypeCode(toType);
          if (tcTo == TypeCode.Object)
          {
            if (fromType.IsAssignableFrom(toType))
              return value;
            else
            {
              bool lImplicit = false;
              bool lExactTo = false;
              bool lExactFrom = false;
              MethodInfo mi = FindConvertMethod(toType.GetMethods(BindingFlags.Public | BindingFlags.Static), fromType, toType, ref lImplicit, ref lExactFrom, ref lExactTo);
              if (mi != null)
              {
                if (!lExactFrom)
                  value = ConvertValue(value, mi.GetParameters()[0].ParameterType);
                value = mi.Invoke(null, new object[] { value });
                if (!lExactTo)
                  value = ConvertValue(value, toType);
              }
              return value;
            }
          }
          else if (tcTo == TypeCode.DBNull)
            return DBNull.Value;
          else
          {
            // convert from string to number through lua parser
            if (tcFrom == TypeCode.String && tcTo >= TypeCode.SByte && tcTo <= TypeCode.Decimal)
              value = Lua.ParseNumber((string)value, (int)LuaIntegerType.Int64 | (int)LuaFloatType.Double);

            // convert to correct type
            switch (tcTo)
            {
              case TypeCode.Boolean:
                value = !Object.Equals(value, Activator.CreateInstance(toType));
                break;
              case TypeCode.Char:
                value = Convert.ToChar(value, CultureInfo.InvariantCulture);
                break;
              case TypeCode.DateTime:
                value = Convert.ToDateTime(value, CultureInfo.InvariantCulture);
                break;
              case TypeCode.SByte:
                value = Convert.ToSByte(value, CultureInfo.InvariantCulture);
                break;
              case TypeCode.Int16:
                value = Convert.ToInt16(value, CultureInfo.InvariantCulture);
                break;
              case TypeCode.Int32:
                value = Convert.ToInt32(value, CultureInfo.InvariantCulture);
                break;
              case TypeCode.Int64:
                value = Convert.ToInt64(value, CultureInfo.InvariantCulture);
                break;
              case TypeCode.Byte:
                value = Convert.ToByte(value, CultureInfo.InvariantCulture);
                break;
              case TypeCode.UInt16:
                value = Convert.ToUInt16(value, CultureInfo.InvariantCulture);
                break;
              case TypeCode.UInt32:
                value = Convert.ToUInt32(value, CultureInfo.InvariantCulture);
                break;
              case TypeCode.UInt64:
                value = Convert.ToUInt64(value, CultureInfo.InvariantCulture);
                break;
              case TypeCode.Single:
                value = Convert.ToSingle(value, CultureInfo.InvariantCulture);
                break;
              case TypeCode.Double:
                value = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                break;
              case TypeCode.Decimal:
                value = Convert.ToDecimal(value, CultureInfo.InvariantCulture);
                break;
              case TypeCode.String:
                value = Convert.ToString(value, CultureInfo.InvariantCulture);
                break;
              default:
                throw new InvalidOperationException("TypeCode unknown");
            }

            // check for generic and enum
            if (toType.IsGenericType && toType.GetGenericTypeDefinition() == typeof(Nullable<>))
              return Activator.CreateInstance(toType, value);
            else if (toType.IsEnum)
              return Enum.ToObject(toType, value);
            else
              return value;
          }
        }
      }
    } // func ConvertValue

    #endregion

    #region -- Convert Generator ------------------------------------------------------

    internal static Expression ConvertExpression(Lua runtime, Expression expr, Type toType)
    {
      return ConvertExpression(runtime, expr, expr.Type, toType);
    } // func ConvertExpression

    internal static Expression ConvertExpression(Lua runtime, Expression expr, Type fromType, Type toType, bool lConvertBinder = false)
    {
      if (expr.Type != fromType) // convert the type to the correct limit type
        expr = Expression.Convert(expr, fromType);

      // check if we nead another conversion
      if (fromType == toType)
      {
        return expr;
      }
      else if (fromType == typeof(LuaResult)) // LuaResult -> convert first value
      {
        return ConvertExpression(runtime, GetResultExpression(expr, 0), toType);
      }
      else if (toType == typeof(LuaResult)) // type to LuaResult
      {
        return Expression.New(Lua.ResultConstructorInfoArg1, ConvertExpression(runtime, expr, typeof(object)));
      }
      else if (runtime != null && !lConvertBinder && IsDynamicType(fromType)) // dynamic type -> dynamic convert
      {
        return Expression.Dynamic(runtime.GetConvertBinder(toType), toType, ConvertExpression(null, expr, fromType, typeof(object)));
      }
      else if (toType.IsAssignableFrom(fromType)) // Type is assignable
      {
        return Expression.Convert(expr, toType);
      }
      else if (toType == typeof(bool)) // we need a true or false
      {
        return BinaryOperationExpression(runtime, ExpressionType.NotEqual, expr, fromType, Expression.Default(fromType), fromType, !lConvertBinder);
      }
      else if (toType == typeof(string)) // convert to a string
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
          ConvertExpression(runtime, expr, fromType, typeof(object)),
          Expression.Property(null, Lua.CultureInvariantPropertyInfo)
        );
      }
      else if (fromType == typeof(string) && IsArithmeticType(toType)) // we expect a string and have a number
      {
        return ConvertExpression(runtime, ParseNumberExpression(runtime, expr, fromType), toType);
      }
      else
        try
        {
          return Expression.Convert(expr, toType);
        }
        catch
        {
          return Lua.ThrowExpression(String.Format(Properties.Resources.rsBindConversionNotDefined, toType.Name, fromType.Name), toType);
        }
    } // func ConvertExpression

    internal static Expression ParseNumberExpression(Lua runtime, Expression expr1, Type type1)
    {
      return Expression.Call(Lua.ParserNumberMethodInfo, ConvertExpression(runtime, expr1, type1, typeof(string)), Expression.Constant(runtime.NumberType));
    } // func ParseNumberExpression

    internal static bool TryConvertType(Type typeTo, ref Expression expr, ref Type exprType)
    {
      bool lExact;
      if (TypesMatch(typeTo, exprType, out lExact))// is the type compitible
      {
        expr = ConvertExpression(null, expr, exprType, typeTo);
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

    private static MethodInfo FindConvertMethod(MethodInfo[] methods, Type typeFrom, Type typeTo, ref bool lImplicit, ref bool lExactFrom, ref bool lExactTo)
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

    internal static Expression GetResultExpression(Expression target, int iIndex)
    {
      return Expression.MakeIndex(
        ConvertExpression(null, target, typeof(LuaResult)),
        Lua.ResultIndexPropertyInfo,
        new Expression[] { Expression.Constant(iIndex) }
        );
    } // func GetFirstValueExpression

    private static Expression ToBooleanExpression(Lua runtime, Expression expr)
    {
      return ConvertExpression(runtime, expr, typeof(bool));
    } // func ToBooleanExpression

    private static Expression ToBooleanExpression(Lua runtime, Expression expr, Type type)
    {
      return ConvertExpression(runtime, expr, type, typeof(bool));
    } // func ToBooleanExpression

    private static bool IsDynamicType(Type type)
    {
      return type == typeof(object) || typeof(IDynamicMetaObjectProvider).IsAssignableFrom(type);
    } // func IsDynamicType

    #endregion

    #region -- Unary Operation Generator ----------------------------------------------

    internal static Expression UnaryOperationExpression(Lua runtime, ExpressionType op, Expression expr)
    {
      return UnaryOperationExpression(runtime, op, expr, expr.Type, true);
    } // func UnaryOperationExpression

    internal static Expression UnaryOperationExpression(Lua runtime, ExpressionType op, Expression expr, Type type, bool lParse)
    {
      if (op == ExpressionType.Not)
        return Expression.Not(ToBooleanExpression(runtime, expr, type));
      else if (op == ExpressionType.ArrayLength)
      {
        if (type.IsArray)
          return Expression.ArrayLength(ConvertExpression(runtime, expr, type));
        else
          return Expression.Call(Lua.RtLengthMethodInfo, ConvertExpression(runtime, expr, type, typeof(object)));
      }
      else if (lParse && IsDynamicType(type))
        return Expression.Dynamic(runtime.GetUnaryOperationBinary(op), typeof(object), ConvertExpression(runtime, expr, type, typeof(object)));
      else
      {
        switch (op)
        {
          case ExpressionType.OnesComplement:
          case ExpressionType.Negate:
            return UnaryOperationArithmeticExpression(runtime, op, expr, type, lParse);
          default:
            return Expression.MakeUnary(op, ConvertExpression(runtime, expr, type, type), type);
        }
      }
    } // func UnaryOperationExpression

    #endregion

    #region -- Binary Operation Generator ---------------------------------------------

    internal static Expression BinaryOperationExpression(Lua runtime, ExpressionType op, Expression expr1, Expression expr2)
    {
      return BinaryOperationExpression(runtime, op, expr1, expr1.Type, expr2, expr2.Type, true);
    } // func BinaryOperationExpression

    internal static Expression BinaryOperationExpression(Lua runtime, ExpressionType op, Expression expr1, Type type1, Expression expr2, Type type2, bool lParse)
    {
      if (op == ExpressionType.OrElse || op == ExpressionType.AndAlso) // and, or are conditions no operations
        return BinaryOperationConditionExpression(runtime, op, expr1, type1, expr2, type2, lParse);
      else if (lParse && (IsDynamicType(type1) || IsDynamicType(type2))) // is one of the type a dynamic type, than make a dynamic expression
        return BinaryOperationDynamicExpression(runtime, op, expr1, type1, expr2, type2);
      else
        switch (op)
        {
          case ExpressionType.Equal:
          case ExpressionType.NotEqual:
          case ExpressionType.LessThan:
          case ExpressionType.LessThanOrEqual:
          case ExpressionType.GreaterThan:
          case ExpressionType.GreaterThanOrEqual:
            return BinaryOperationCompareExpression(runtime, op, expr1, type1, expr2, type2, lParse);
          case ExpressionType.Add:
          case ExpressionType.Subtract:
          case ExpressionType.Multiply:
          case ExpressionType.Divide:
          case ExpressionType.Modulo:
          case ExpressionType.And:
          case ExpressionType.ExclusiveOr:
          case ExpressionType.Or:
          case ExpressionType.LeftShift:
          case ExpressionType.RightShift:
          case IntegerDivide:
            return BinaryOperationArithmeticOrBitExpression(runtime, op, expr1, type1, expr2, type2, lParse);
          case ExpressionType.Power:
            if (!TryConvertType(typeof(double), ref expr1, ref type1))
              return Lua.ThrowExpression(String.Format(Properties.Resources.rsBindConversionNotDefined, type1.Name, typeof(double).Name));
            else if (!TryConvertType(typeof(double), ref expr2, ref type2))
              return Lua.ThrowExpression(String.Format(Properties.Resources.rsBindConversionNotDefined, type2.Name, typeof(double).Name));
            else
              return Expression.MakeBinary(op, ConvertExpression(null, expr1, type1), ConvertExpression(runtime, expr2, type2));
          default:
            return Expression.MakeBinary(op, expr1, expr2);
        }
    } // func BinaryOperationExpression

    private static Expression BinaryOperationDynamicExpression(Lua runtime, ExpressionType op, Expression expr1, Type type1, Expression expr2, Type type2)
    {
      return Expression.Dynamic(runtime.GetBinaryOperationBinder(op), typeof(object),
        ConvertExpression(runtime, expr1, type1, typeof(object)),
        ConvertExpression(runtime, expr2, type2, typeof(object)));
    } // func BinaryOperationDynamicExpression

    #endregion

    #region -- Binary Condition Operator Generator ------------------------------------

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
          ToBooleanExpression(runtime, exprTmp),
          ConvertExpression(runtime, expr2, type2, typeOp),
          ConvertExpression(runtime, exprTmp, typeOp)
        );
      }
      else if (op == ExpressionType.OrElse)
      {
        exprCondition = Expression.Condition(
          ToBooleanExpression(runtime, exprTmp),
          ConvertExpression(runtime, exprTmp, typeOp),
          ConvertExpression(runtime, expr2, type2, typeOp)
        );
      }
      else
        throw new InvalidOperationException();

      return Expression.Block(typeOp,
        new ParameterExpression[] { exprTmp },
        Expression.Assign(exprTmp, ConvertExpression(runtime, expr1, exprTmp.Type)),
        exprCondition
      );
    } // func BinaryOperationConditionExpression

    #endregion

    #region -- Binary Compare Operator Generator --------------------------------------

    private static Expression BinaryOperationCompareExpression(Lua runtime, ExpressionType op, Expression expr1, Type type1, Expression expr2, Type type2, bool lParse)
    {
      bool lIsArithmetic1 = IsArithmeticType(type1);
      bool lIsArithmetic2 = IsArithmeticType(type2);
      if (lIsArithmetic1 && lIsArithmetic2) // first is a arithmetic --> create a simple compare operation
      {
        Type typeOp;
        TypeCode tc1 = GetTypeCode(type1);
        TypeCode tc2 = GetTypeCode(type2);
       
        if (tc1 >= tc2)
          typeOp = type1;
        else
          typeOp = type2;

        return Expression.MakeBinary(op,
          ConvertExpression(runtime, expr1, type1, typeOp),
          ConvertExpression(runtime, expr2, type2, typeOp)
        );
      }

      ExpressionType opComplement = GetCompareComplement(op);
      BindingFlags bindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.InvokeMethod;

      // try find a compare operator
      Expression expr = BinaryOperationCompareOperatorExpression(runtime, bindingFlags, op, expr1, type1, expr2, type2, lParse);
      if (expr != null)
        return expr;

      // try find a complement compare operator
      if (type1 != type2)
      {
        expr = BinaryOperationCompareOperatorExpression(runtime, bindingFlags, opComplement, expr2, type2, expr1, type1, lParse);
        if (expr != null)
          return expr;
      }

      // try find a compare interface
      bool lExact = false;
      Type compareInterface1 = GetComparableInterface(type1, type2, ref lExact);
      Type compareInterface2 = null;
      if (!lExact && lIsArithmetic1) // arithmetic type compare interface, do not use because they only compare it self
        compareInterface1 = null;

      if (type1 != type2 && !lExact)
      {
        // try find complement interface
        compareInterface2 = GetComparableInterface(type2, type1, ref lExact);
        if (lExact)
          compareInterface1 = null;
        else if (compareInterface1 != null || lIsArithmetic2)
          compareInterface2 = null;
      }

      if (compareInterface1 != null)
        return BinaryOperationCompareToExpression(runtime, compareInterface1, op, expr1, type1, expr2, type2, lParse);
      if (compareInterface2 != null)
        return BinaryOperationCompareToExpression(runtime, compareInterface2, opComplement, expr2, type2, expr1, type1, lParse);

      // try lift to an other operator
      if (type1 != type2)
      {
        if (TryConvertType(type1, ref expr2, ref type2))
          return BinaryOperationCompareExpression(runtime, op, expr1, type1, expr2, type2, lParse);
        else if (TryConvertType(type2, ref expr1, ref type1))
          return BinaryOperationCompareExpression(runtime, op, expr1, type1, expr2, type2, lParse);
      }
      else if (op == ExpressionType.Equal || op == ExpressionType.NotEqual)
      {
        expr1 = ConvertExpression(runtime, expr1, type1, typeof(object));
        expr2 = ConvertExpression(runtime, expr2, type2, typeof(object));

        expr = Expression.OrElse(
          Expression.Call(Lua.ObjectReferenceEqualsMethodInfo, expr1, expr2),
          Expression.Call(Lua.ObjectEqualsMethodInfo, expr1, expr2)
        );
        if (op == ExpressionType.NotEqual)
          expr = Expression.Not(expr);

        return expr;
      }

      return Lua.ThrowExpression(String.Format(Properties.Resources.rsBindOperatorNotDefined, GetCompareComplement(op), type1.Name, type2.Name));
    } // func BinaryOperationCompareExpression

    private static Expression BinaryOperationCompareOperatorExpression(Lua runtime, BindingFlags bindingFlags, ExpressionType op, Expression expr1, Type type1, Expression expr2, Type type2, bool lParse)
    {
      MethodInfo miCompare = type1.GetMethod(GetOperationMethodName(op), bindingFlags, null, new Type[] { type1, type2 }, null);
      if (miCompare != null)
      {
        ParameterInfo[] parm = miCompare.GetParameters();
        return Expression.MakeBinary(op,
          ConvertExpression(runtime, expr1, type1, parm[0].ParameterType),
          ConvertExpression(runtime, expr2, type2, parm[1].ParameterType),
          true,
          miCompare
          );
      }
      else
        return null;
    } // func BinaryOperationCompareOperatorExpression

    private static Expression BinaryOperationCompareToExpression(Lua runtime, Type compareInterface, ExpressionType op, Expression expr1, Type type1, Expression expr2, Type type2, bool lParse)
    {
      MethodInfo miMethod = compareInterface.GetMethod("CompareTo", BindingFlags.Public | BindingFlags.Instance | BindingFlags.InvokeMethod);
      return Expression.MakeBinary(op,
        Expression.Call(
          ConvertExpression(runtime, expr1, type1, compareInterface),
          miMethod,
          ConvertExpression(runtime, expr2, type2, miMethod.GetParameters()[0].ParameterType)
        ),
        Expression.Constant(0, typeof(int)));
    } // func BinaryOperationCompareToExpression

    private static Type GetComparableInterface(Type type1, Type Type2, ref bool lExact)
    {
      Type compareInterface = null;
      foreach (Type typeTest in type1.GetInterfaces())
      {
        if (compareInterface == null && typeTest == typeof(IComparable))
        {
          compareInterface = typeTest;
          lExact = false;
        }
        else if (!lExact && IsGenericCompare(typeTest))
        {
          Type p = typeTest.GetGenericArguments()[0];
          if (p == Type2)
          {
            lExact = true;
            compareInterface = typeTest;
            return compareInterface;
          }
          if (compareInterface == null && TypesMatch(p, Type2, out lExact))
          {
            lExact = false;
            compareInterface = typeTest;
          }
        }
      }
      return compareInterface;
    } // func GetComparableInterface

    private static bool IsGenericCompare(Type type)
    {
      return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IComparable<>);
    } // func IsGenericCompare

    private static ExpressionType GetCompareComplement(ExpressionType op)
    {
      if (op == ExpressionType.GreaterThan)
        op = ExpressionType.LessThan;
      else if (op == ExpressionType.LessThan)
        op = ExpressionType.GreaterThan;
      else if (op == ExpressionType.GreaterThanOrEqual)
        op = ExpressionType.LessThanOrEqual;
      else if (op == ExpressionType.LessThanOrEqual)
        op = ExpressionType.GreaterThanOrEqual;
      else if (op == ExpressionType.NotEqual)
        op = ExpressionType.Equal;
      else if (op == ExpressionType.Equal)
        op = ExpressionType.NotEqual;
      return op;
    } // func GetCompareComplement

    #endregion

    #region -- Arithmetic Expression Generator ----------------------------------------

    private static Expression UnaryOperationArithmeticExpression(Lua runtime, ExpressionType op, Expression expr, Type type, bool lParse)
    {
      bool lIsArithmetic = IsArithmeticType(type);
      if (lIsArithmetic)
      {
        #region -- simple arithmetic --
        if (op == ExpressionType.OnesComplement)
        {
          expr = ConvertExpression(runtime, expr, type, Lua.GetIntegerType(runtime.NumberType));
          type = expr.Type;
        }

        Type typeEnum = null;
        if (type.IsEnum)
        {
          typeEnum = type; // save enum
          type = type.GetEnumUnderlyingType();
        }

        expr = Expression.MakeUnary(op, ConvertExpression(runtime, expr, type), type);

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
          ConvertExpression(runtime, expr, type, typeof(object))
        );
        #endregion
      }

      // try convert the type to an arithmetic type
      if (miOperation == null)
      {
        if (op == ExpressionType.OnesComplement && TryConvertType(Lua.GetIntegerType(runtime.NumberType), ref expr, ref type))
          return UnaryOperationArithmeticExpression(runtime, op, expr, type, lParse);
        else if (op == ExpressionType.Negate)
        {
          // is there a integer conversion
          bool lImplicit = false;
          bool lExactFrom = false;
          bool lExactTo = false;
          Type typeInt = Lua.GetIntegerType(runtime.NumberType);
          MethodInfo miConvert = FindConvertMethod(type.GetMethods(bindingFlags), type, typeInt, ref lImplicit, ref lExactFrom, ref lExactTo);
          if (lExactTo)
          {
            if (expr.Type != type)
              expr = Expression.Convert(expr, type);
            return UnaryOperationArithmeticExpression(runtime, op, Expression.Convert(expr, typeInt), typeInt, lParse);
          }
          else if (TryConvertType(Lua.GetFloatType(runtime.NumberType), ref expr, ref type))
            return UnaryOperationArithmeticExpression(runtime, op, expr, type, lParse);
        }
      }
      #endregion


      if (miOperation != null)
      {
        return Expression.MakeUnary(op,
          ConvertExpression(runtime, expr, type, miOperation.GetParameters()[0].ParameterType),
          null, miOperation);
      }
      else
        return Lua.ThrowExpression(String.Format(Properties.Resources.rsBindOperatorNotDefined, op, String.Empty, type.Name));
    } // func UnaryOperationArithmeticExpression

    private static Expression BinaryOperationArithmeticOrBitExpression(Lua runtime, ExpressionType op, Expression expr1, Type type1, Expression expr2, Type type2, bool lParse)
    {
      bool lIsArithmetic1 = IsArithmeticType(type1);
      bool lIsArithmetic2 = IsArithmeticType(type2);

      if (lIsArithmetic1 && lIsArithmetic2) // Arithmetic types --> create a simple arithmetic operation
      {
        #region -- simple arithmetic --
        Type typeOp;
        TypeCode tc1 = GetTypeCode(type1);
        TypeCode tc2 = GetTypeCode(type2);

        // bit operations work only on integers
        bool lIntegerOperation = op == ExpressionType.ExclusiveOr ||
            op == ExpressionType.Or ||
            op == ExpressionType.And ||
            op == ExpressionType.LeftShift ||
            op == ExpressionType.RightShift ||
            op == IntegerDivide;
        bool lFloatOperation = op == ExpressionType.Divide;

        if (lIntegerOperation && !IsIntegerType(tc1))
        {
          expr1 = ConvertExpression(runtime, expr1, type1, Lua.GetIntegerType(runtime.NumberType));
          type1 = expr1.Type;
        }
        if (lIntegerOperation && !IsIntegerType(tc2))
        {
          expr2 = ConvertExpression(runtime, expr2, type2, Lua.GetIntegerType(runtime.NumberType));
          type2 = expr2.Type;
        }
        if (lFloatOperation && !IsFloatType(tc1))
        {
          expr1 = ConvertExpression(runtime, expr1, type1, Lua.GetFloatType(runtime.NumberType));
          type1 = expr1.Type;
        }
        if (lFloatOperation && !IsFloatType(tc2))
        {
          expr2 = ConvertExpression(runtime, expr2, type2, Lua.GetFloatType(runtime.NumberType));
          type2 = expr2.Type;
        }
        if (op == IntegerDivide)
          op = ExpressionType.Divide;

        // find the correct type for the operation
        if (tc1 >= tc2)
          typeOp = type1;
        else
          typeOp = type2;

        Type typeEnum = null;
        if (typeOp.IsEnum)
        {
          if (type1.IsEnum && type2.IsEnum)
            typeEnum = typeOp; // save enum
          typeOp = typeOp.GetEnumUnderlyingType();
        }

        Expression expr = Expression.MakeBinary(op,
          ConvertExpression(runtime, expr1, type1, typeOp),
          ConvertExpression(runtime, expr2, type2, typeOp)
        );

        // convert to enum
        if (typeEnum != null)
          expr = Expression.Convert(expr, typeEnum);

        return expr;
        #endregion
      }

      // find a method, that can do the operation
      MethodInfo miOperation = null;
      string sMethodName = GetOperationMethodName(op);
#if DEBUG
      if (sMethodName == null)
        throw new InvalidOperationException(String.Format("Method for Operator {0} not defined.", op));
#endif

      #region -- find operator --
      BindingFlags bindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.InvokeMethod;
      Type[] parameterTypes = new Type[] { type1, type2 };

      // try to find a exact match for the operation
      miOperation = type1.GetMethod(sMethodName, bindingFlags, null, parameterTypes, null);
      if (miOperation == null && type1 != type2)
        miOperation = type2.GetMethod(sMethodName, bindingFlags, null, parameterTypes, null);

      if (miOperation == null)
      {
        // can we inject a string conversation --> create a dynamic operation, that results in a simple arithmetic operation
        if (type1 == typeof(string) && type2 == typeof(string) ||
          type1 == typeof(string) && lIsArithmetic2 ||
          lIsArithmetic1 && type2 == typeof(string))
        {
          #region -- string inject for arithmetic --
          if (type1 == typeof(string))
          {
            expr1 = ParseNumberExpression(runtime, expr1, type1);
            type1 = typeof(object);
          }
          if (type2 == typeof(string))
          {
            expr2 = ParseNumberExpression(runtime, expr2, type2);
            type2 = typeof(object);
          }

          return Expression.Dynamic(runtime.GetBinaryOperationBinder(op), typeof(object),
            ConvertExpression(runtime, expr1, type1, typeof(object)),
            ConvertExpression(runtime, expr2, type2, typeof(object))
          );
          #endregion
        }
      }

      // check if we have a type1 op type1 operation
      if (miOperation == null)
      {
        miOperation = type1.GetMethod(sMethodName, bindingFlags, null, new Type[] { type1, type1 }, null);
        if (miOperation == null || !TryConvertType(type1, ref expr2, ref type2))
          miOperation = null;
      }
      // check if we have a type2 op type2 operation
      if (miOperation == null && type1 != type2)
      {
        miOperation = type2.GetMethod(sMethodName, bindingFlags, null, new Type[] { type2, type2 }, null);
        if (miOperation == null || !TryConvertType(type2, ref expr1, ref type1))
          miOperation = null;
      }

      if (miOperation == null)
      {
        // check if there is a simple arithmetic operation for type1
        if (lIsArithmetic1 && TryConvertType(type1, ref expr2, ref type2))
          return BinaryOperationArithmeticOrBitExpression(runtime, op, expr1, type1, expr2, type2, lParse);
        // check if there is a simple arithmetic operation for type2
        if (miOperation == null && lIsArithmetic2 && TryConvertType(type2, ref expr1, ref type1))
          return BinaryOperationArithmeticOrBitExpression(runtime, op, expr1, type1, expr2, type2, lParse);
      }
      #endregion

      // generate the non arithmetic expressions
      if (miOperation != null)
      {
        // manipulate the cast to the correct parameters
        ParameterInfo[] parameterInfo = miOperation.GetParameters();

        return Expression.MakeBinary(op,
          ConvertExpression(runtime, expr1, type1, parameterInfo[0].ParameterType),
          ConvertExpression(runtime, expr2, type2, parameterInfo[1].ParameterType),
          true,
          miOperation); // try find a operator for this two expressions
      }
      else
        return Lua.ThrowExpression(String.Format(Properties.Resources.rsBindOperatorNotDefined, op, type1.Name, type2.Name));
    } // func BinaryOperationArithmeticOrBitExpression

    #endregion

    #region -- Concat Operation Generator ---------------------------------------------

    internal static Expression ConcatOperationExpression(Lua runtime, Expression[] args)
    {
      MethodInfo mi = typeof(String).GetMethod("Concat", BindingFlags.Public | BindingFlags.Static, null, new Type[] { typeof(string[]) }, null);

      return Expression.Call(mi, Expression.NewArrayInit(typeof(string),
        from e in args select ConvertExpression(runtime, e, typeof(string))));
    } // func ConcatOperationExpression

    #endregion

    #region -- Index Generator --------------------------------------------------------

    internal static Expression IndexSetExpression(Lua runtime, Expression instance, Expression[] indices, Expression set)
    {
      // Assign to an index
      Expression[] r = new Expression[indices.Length + 2];
      r[0] = ConvertExpression(runtime, instance, typeof(object));
      for (int i = 0; i < indices.Length; i++)
        r[i + 1] = ConvertExpression(runtime, indices[i], typeof(object));
      r[r.Length - 1] = ConvertExpression(runtime, set, typeof(object));
      return Expression.Dynamic(runtime.GetSetIndexMember(new CallInfo(indices.Length)), typeof(object), r);
    } // func IndexSetExpression

    internal static Expression IndexGetExpression(Lua runtime, Expression instance, Expression[] indices)
    {
      // Create the arguments for the index assign
      Expression[] r = new Expression[indices.Length + 1];
      r[0] = ConvertExpression(null, instance, typeof(object)); // Array instance
      for (int i = 0; i < indices.Length; i++)
        r[i + 1] = ConvertExpression(null, indices[i], typeof(object)); // Copy the index values

      return Expression.Dynamic(runtime.GetGetIndexMember(new CallInfo(indices.Length)), typeof(object), r);
    } // func IndexGetExpression

    #endregion

    #region -- Member Generator -------------------------------------------------------

    internal static Expression MemberSetExpression(Lua runtime, Expression instance, string sMember, bool lMethodMember, Expression set)
    {
      // Assign the value to a member
      if (lMethodMember)
        return Expression.Call(
          ConvertExpression(null, instance, typeof(LuaTable)),
          Lua.TableSetMethodInfo,
          Expression.Constant(sMember, typeof(string)),
          ConvertExpression(null, set, typeof(Delegate)));
      else
        return Expression.Dynamic(runtime.GetSetMemberBinder(sMember), typeof(object), instance, ConvertExpression(null, set, typeof(object)));
    } // func MemberSetExpression

    internal static Expression MemberGetExpression(Lua runtime, Expression instance, string sMember, bool lMethodMember)
    {
      return Expression.Dynamic(runtime.GetGetMemberBinder(sMember), typeof(object), ConvertExpression(null, instance, typeof(object)));
    } // func MemberGetExpression

    #endregion

    #region -- Member Call Generator --------------------------------------------------

    private static Expression InvokeExpression(Lua runtime, Expression instance, string sMember, bool lMethodMember, InvokeResult result, Expression[] arguments)
    {
      Expression[] r = new Expression[arguments.Length + 1];
      r[0] = ConvertExpression(null, instance, typeof(object)); // Delegate

      // All arguments are converted to an object, except of the last one (rollup)
      for (int i = 0; i < arguments.Length; i++)
        r[i + 1] = Expression.Convert(arguments[i], typeof(object)); // Convert the arguments, no convert of LuaResult

      // make the return
      //Lua.InvokeMemberExpression(
      Expression exprCall = Expression.Dynamic(sMember == null ?
        runtime.GetInvokeBinder(new CallInfo(arguments.Length)) :
        runtime.GetInvokeMemberBinder(sMember, new CallInfo(arguments.Length)), typeof(object), r
      );
      switch (result)
      {
        case InvokeResult.Object:
          return Expression.Dynamic(runtime.GetConvertBinder(typeof(object)), typeof(object), exprCall);
        case InvokeResult.LuaResult:
          return Expression.Dynamic(runtime.GetConvertBinder(typeof(LuaResult)), typeof(LuaResult), exprCall);
        default:
          return exprCall;
      }
    } // func InvokeExpression

    #endregion

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
        case IntegerDivide:
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

    internal static Type UnpackType(Type type)
    {
      if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
        type = type.GetGenericArguments()[0];
      if (type.IsEnum)
        type = type.GetEnumUnderlyingType();
      return type;
    } // func UnpackType

    private static TypeCode GetTypeCode(Type type)
    {
      if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
        type = type.GetGenericArguments()[0];
      return Type.GetTypeCode(type);
    } // func GetTypeCode

    private static bool TypesMatch(Type typeTo, Type typeFrom, out bool lExact)
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
              tcFrom >= TypeCode.SByte && tcFrom <= tcTo)
          return true;

        return false;
      }
    } // bool TypesMatch

    internal static bool IsArithmeticType(Type type)
    {
      return IsArithmeticType(GetTypeCode(type));
    } // func IsArithmeticType

    internal static bool IsArithmeticType(TypeCode typeCode)
    {
      return IsIntegerType(typeCode) || IsFloatType(typeCode);
    } // func IsArithmeticType

    internal static bool IsIntegerType(TypeCode typeCode)
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

    internal static bool IsFloatType(TypeCode typeCode)
    {
      switch (typeCode)
      {
        case TypeCode.Double:
        case TypeCode.Single:
        case TypeCode.Decimal:
          return true;
        default:
          return false;
      }
    } // func IsFloatType

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

    internal static Delegate CreateDelegate(object firstArgument, MethodInfo mi)
    {
      if ((mi.CallingConvention & CallingConventions.VarArgs) != 0)
        throw new ArgumentException("Call of VarArgs not implemented.");
      Type typeDelegate = GetDelegateType(mi);
      return Delegate.CreateDelegate(typeDelegate, firstArgument, mi);
    } // func CreateDelegateFromMethodInfo
  } // class Parser

  #endregion
}
