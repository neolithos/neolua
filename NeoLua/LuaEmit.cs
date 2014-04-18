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
              tcFrom >= TypeCode.SByte && tcFrom <= tcTo)
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

    private static bool IsIntegerType(TypeCode typeCode)
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
        case TypeCode.Decimal:
          return true;
        default:
          return false;
      }
    } // func IsFloatType

    private static bool TryConvertType(Type typeTo, ref Expression expr, ref Type exprType)
    {
      bool lExact;
      if (TypesMatch(typeTo, exprType, out lExact))// is the type compitible
      {
        expr = Convert(null, expr, exprType, typeTo, false);
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

    private static Expression[] CreateDynamicArgs<TARG>(Lua runtime, Expression instance, Type instanceType, TARG[] arguments, Func<TARG, Expression> getExpr, Func<TARG, Type> getType)
      where TARG : class
    {
      Expression[] dynArgs = new Expression[arguments.Length + 1];
      dynArgs[0] = Convert(runtime, instance, instanceType, typeof(object), false);
      for (int i = 0; i < arguments.Length; i++)
        dynArgs[i + 1] = Convert(runtime, getExpr(arguments[i]), getType(arguments[i]), typeof(object), false);
      return dynArgs;
    } // func CreateDynamicArgs

    private static Expression[] CreateDynamicArgs<TARG>(Lua runtime, Expression instance, Type instanceType, TARG[] arguments, TARG setTo, Func<TARG, Expression> getExpr, Func<TARG, Type> getType)
      where TARG : class
    {
      Expression[] dynArgs = new Expression[arguments.Length + 2];
      dynArgs[0] = Convert(runtime, instance, instanceType, typeof(object), false);
      for (int i = 0; i < arguments.Length; i++)
        dynArgs[i + 1] = Convert(runtime, getExpr(arguments[i]), getType(arguments[i]), typeof(object), false);
      dynArgs[dynArgs.Length - 1] = Convert(runtime, getExpr(setTo), getType(setTo), typeof(object), false);
      return dynArgs;
    } // func CreateDynamicArgs

    private static BindingFlags GetBindingFlags(bool lInstance, bool lIgnoreCase)
    {
      BindingFlags flags = BindingFlags.Public | BindingFlags.Static;
      if (lInstance)
        flags |= BindingFlags.Instance;
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
            expr = exprNew.Arguments.First(); // only unpack, repack is not necessary
            if (expr.NodeType == ExpressionType.Convert && expr.Type == typeof(object))
              expr = ((UnaryExpression)expr).Operand;
            fromType = expr.Type;
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
        return GetResultExpression(runtime, expr, fromType, 0, toType, lParse);
      }
      else if (toType == typeof(LuaResult)) // type to LuaResult
      {
        return Expression.New(Lua.ResultConstructorInfoArg1, Convert(runtime, expr, fromType, typeof(object), false));
      }
      else if (runtime != null && lParse && IsDynamicType(fromType)) // dynamic type -> dynamic convert
      {
        return Expression.Dynamic(runtime.GetConvertBinder(toType), toType, Convert(null, expr, fromType, typeof(object), false));
      }
      else if (toType.IsAssignableFrom(fromType)) // Type is assignable
      {
        return Expression.Convert(expr, toType);
      }
      else if (toType == typeof(bool)) // we need a true or false
      {
        return BinaryOperationExpression(runtime, ExpressionType.NotEqual, expr, fromType, Expression.Default(fromType), fromType, lParse);
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
          Convert(runtime, expr, fromType, typeof(object), false),
          Expression.Property(null, Lua.CultureInvariantPropertyInfo)
        );
      }
      else if (fromType == typeof(string) && IsArithmeticType(toType)) // we expect a string and have a number
      {
        return Convert(runtime, ParseNumberExpression(runtime, expr, fromType), typeof(object), toType, true); // allow dynamic converts
      }
      else if (fromType.IsArray && toType.IsArray)
      {
        return Expression.Convert(Expression.Call(Lua.ConvertArrayMethodInfo, Convert(runtime, expr, fromType, toType, lParse), Expression.Constant(toType.GetElementType())), toType);
      }
      else
        try
        {
          return Expression.Convert(expr, toType);
        }
        catch
        {
          throw new LuaEmitException(LuaEmitException.ConversationNotDefined, toType.Name, fromType.Name);
        }
    } // func Convert

    private static Expression ParseNumberExpression(Lua runtime, Expression expr1, Type type1)
    {
      return Expression.Call(Lua.ParseNumberMethodInfo, Convert(runtime, expr1, type1, typeof(string), false), Expression.Constant(runtime.NumberType));
    } // func ParseNumberExpression

    public static Expression GetResultExpression(Expression target, Type type, int iIndex)
    {
      return Expression.MakeIndex(
        Convert(null, target, type, typeof(LuaResult), false),
        Lua.ResultIndexPropertyInfo,
        new Expression[] { Expression.Constant(iIndex) }
      );
    } // func GetResultExpression

    public static Expression GetResultExpression(Lua runtime, Expression expr, Type type, int iIndex, Type typeReturn, bool lParse)
    {
      Expression exprGet = GetResultExpression(expr, type, iIndex);
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
          case Lua.IntegerDivide:
            return BinaryOperationArithmeticOrBitExpression(runtime, op, expr1, type1, expr2, type2, lParse);
          case ExpressionType.Power:
            if (!TryConvertType(typeof(double), ref expr1, ref type1))
              throw new LuaEmitException(LuaEmitException.ConversationNotDefined, type1.Name, typeof(double).Name);
            else if (!TryConvertType(typeof(double), ref expr2, ref type2))
              throw new LuaEmitException(LuaEmitException.ConversationNotDefined, type2.Name, typeof(double).Name);
            else
              return Expression.MakeBinary(op, expr1, expr2);
          default:
            return Expression.MakeBinary(op, expr1, expr2);
        }
    } // func BinaryOperationExpression

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

    #region -- Emit Binary Compare Operator -------------------------------------------

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
          Convert(runtime, expr1, type1, typeOp, lParse),
          Convert(runtime, expr2, type2, typeOp, lParse)
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
        expr1 = Convert(runtime, expr1, type1, typeof(object), lParse);
        expr2 = Convert(runtime, expr2, type2, typeof(object), lParse);

        expr = Expression.OrElse(
          Expression.Call(Lua.ObjectReferenceEqualsMethodInfo, expr1, expr2),
          Expression.Call(Lua.ObjectEqualsMethodInfo, expr1, expr2)
        );
        if (op == ExpressionType.NotEqual)
          expr = Expression.Not(expr);

        return expr;
      }

      throw new LuaEmitException(LuaEmitException.OperatorNotDefined, GetCompareComplement(op), type1.Name, type2.Name);
    } // func BinaryOperationCompareExpression

    private static Expression BinaryOperationCompareOperatorExpression(Lua runtime, BindingFlags bindingFlags, ExpressionType op, Expression expr1, Type type1, Expression expr2, Type type2, bool lParse)
    {
      MethodInfo miCompare = type1.GetMethod(GetOperationMethodName(op), bindingFlags, null, new Type[] { type1, type2 }, null);
      if (miCompare != null)
      {
        ParameterInfo[] parm = miCompare.GetParameters();
        return Expression.MakeBinary(op,
          Convert(runtime, expr1, type1, parm[0].ParameterType, lParse),
          Convert(runtime, expr2, type2, parm[1].ParameterType, lParse),
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
          Convert(runtime, expr1, type1, compareInterface, false),
          miMethod,
          Convert(runtime, expr2, type2, miMethod.GetParameters()[0].ParameterType, lParse)
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

    #region -- Emit Arithmetic Expression ---------------------------------------------

    private static Expression UnaryOperationArithmeticExpression(Lua runtime, ExpressionType op, Expression expr, Type type, bool lParse)
    {
      bool lIsArithmetic = IsArithmeticType(type);
      if (lIsArithmetic)
      {
        #region -- simple arithmetic --
        if (op == ExpressionType.OnesComplement)
        {
          expr = Convert(runtime, expr, type, Lua.GetIntegerType(runtime.NumberType), lParse);
          type = expr.Type;
        }

        Type typeEnum = null;
        if (type.IsEnum)
        {
          typeEnum = type; // save enum
          type = type.GetEnumUnderlyingType();
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
          Convert(runtime, expr, type, miOperation.GetParameters()[0].ParameterType, lParse),
          null, miOperation);
      }
      else
        throw new LuaEmitException(LuaEmitException.OperatorNotDefined, op, String.Empty, type.Name);
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
            op == Lua.IntegerDivide;
        bool lFloatOperation = op == ExpressionType.Divide;

        if (lIntegerOperation && !IsIntegerType(tc1))
        {
          expr1 = Convert(runtime, expr1, type1, Lua.GetIntegerType(runtime.NumberType), lParse);
          type1 = expr1.Type;
        }
        if (lIntegerOperation && !IsIntegerType(tc2))
        {
          expr2 = Convert(runtime, expr2, type2, Lua.GetIntegerType(runtime.NumberType), lParse);
          type2 = expr2.Type;
        }
        if (lFloatOperation && !IsFloatType(tc1))
        {
          expr1 = Convert(runtime, expr1, type1, Lua.GetFloatType(runtime.NumberType), lParse);
          type1 = expr1.Type;
        }
        if (lFloatOperation && !IsFloatType(tc2))
        {
          expr2 = Convert(runtime, expr2, type2, Lua.GetFloatType(runtime.NumberType), lParse);
          type2 = expr2.Type;
        }
        if (op == Lua.IntegerDivide)
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
          Convert(runtime, expr1, type1, typeOp, lParse),
          Convert(runtime, expr2, type2, typeOp, lParse)
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
            Convert(runtime, expr1, type1, typeof(object), false),
            Convert(runtime, expr2, type2, typeof(object), false)
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
          Convert(runtime, expr1, type1, parameterInfo[0].ParameterType, lParse),
          Convert(runtime, expr2, type2, parameterInfo[1].ParameterType, lParse),
          true,
          miOperation); // try find a operator for this two expressions
      }
      else
        throw new LuaEmitException(LuaEmitException.OperatorNotDefined, op, type1.Name, type2.Name);
    } // func BinaryOperationArithmeticOrBitExpression

    #endregion

    #region -- Emit GetMember ---------------------------------------------------------

    public static Expression GetMember(Lua runtime, Expression instance, Type type, string sMemberName, bool lIgnoreCase, bool lParse)
    {
      MemberInfo[] members = 
        lParse && IsDynamicType(type) ? null :  // dynamic type --> resolve later
          type.GetMember(sMemberName, GetBindingFlags(instance != null, lIgnoreCase)); // get the current members

      if (members == null || members.Length == 0) // no member found, try later again
        if (lParse)
          return Expression.Dynamic(runtime.GetGetMemberBinder(sMemberName), typeof(object), Convert(runtime, instance, type, typeof(object), false));
        else
          return Expression.Default(typeof(object));
      else if (members.Length == 1) // return the one member
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
      else // multiple member
      {
        return Expression.New(Lua.OverloadedMethodConstructorInfo, instance == null ? Expression.Default(typeof(object)) : instance, Expression.Constant(Lua.RtConvertArray(members, typeof(MethodInfo)), typeof(MethodInfo[])));
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
          return Expression.Assign(Expression.Property(Convert(runtime, instance, type, type, lParse), pi), Convert(runtime, set, typeSet, pi.PropertyType, lParse));
        }
        else if (members[0].MemberType == MemberTypes.Field)
        {
          FieldInfo fi = (FieldInfo)members[0];
          return Expression.Assign(Expression.Field(Convert(runtime, instance, type, type, lParse), fi), Convert(runtime, set, typeSet, fi.FieldType, lParse));
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
          CreateDynamicArgs<TARG>(runtime, getExpr( instance), instanceType, arguments, setTo, getExpr, getType)
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
          else if (iArgumentsIndex == arguments.Length - 1 && getType( arguments[iArgumentsIndex]).IsArray) // last parameter expect a array and we have an array
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
            Expression tmpExpr = getExpr( arguments[iArgumentsIndex]);
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
          else if (iArgumentsIndex == arguments.Length - 1 && getType( arguments[iArgumentsIndex])== typeof(LuaResult)) // The last argument is a LuaResult (eg. function), start the stretching of the array
          {
            varLuaResult = Expression.Variable(typeof(LuaResult), "#result");
            exprGet = GetResultExpression(runtime,
              Expression.Assign(varLuaResult, Convert(runtime, getExpr(arguments[iArgumentsIndex]), typeof(LuaResult), typeof(LuaResult), false)),
              typeof(LuaResult),
              0,
              typeParameter,
              lParse
            );
            iLastArgumentStretchCount = 1;
            iArgumentsIndex++;
          }
          else if (iLastArgumentStretchCount > 0) // get the expression of the LuaResult to stretch it
          {
            exprGet = GetResultExpression(runtime, varLuaResult, typeof(LuaResult), iLastArgumentStretchCount++, typeParameter, lParse);
          }
          else if (iArgumentsIndex < arguments.Length) // assign a normal parameter
          {
            exprGet = Convert(runtime, getExpr(arguments[iArgumentsIndex]), getType(arguments[iArgumentsIndex]), typeParameter, lParse);
            iArgumentsIndex++;
          }
          else if (parameter.IsOptional) // No arguments left, if we have a default value, set it
          {
            exprGet = Expression.Constant(parameter.DefaultValue, typeParameter);
          }
          else // No arguments left, we have no default value --> use default of type
          {
            exprGet = Expression.Default(typeParameter);
          }

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

    #endregion

    #region -- FindMember -------------------------------------------------------------

    public static MethodInfo FindMethod<TARG>(MethodInfo[] members, TARG[] arguments, Func<TARG, Type> getType)
      where TARG : class
    {
      MethodInfo mi = (MethodInfo)FindMember(members, arguments, getType);

      // create a non generic version
      if (mi != null && mi.ContainsGenericParameters)
        mi = MakeNonGenericMethod(mi, arguments, getType);

      return mi;
    } // func FindMethod

    public static TMEMBERTYPE FindMember<TMEMBERTYPE, TARG>(TMEMBERTYPE[] members, TARG[] arguments, Func<TARG, Type> getType)
      where TMEMBERTYPE : MemberInfo
      where TARG : class
    {
      int iMaxParameterLength = 0;    // Max length of the argument list, can also MaxInt for variable argument length
      TMEMBERTYPE miBind = null;      // Member that matches best
      int iCurParameterLength = 0;    // Length of the arguments of the current match
      int iCurMatchCount = -1;        // How many arguments match of this list
      int iCurMatchExactCount = -1;   // How many arguments match exact of this list

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
          ParameterInfo[] parameters = GetMemberParameter(miCur);

          // How many parameters we have
          int iParametersLength = parameters.Length;
          if (iParametersLength > 0 && parameters[iParametersLength - 1].ParameterType.IsArray)
            iParametersLength = Int32.MaxValue;

          // We have already a match, is the new one better
          if (miBind != null)
          {
            if (iParametersLength == iMaxParameterLength && iCurParameterLength == iMaxParameterLength)
            {
              // Get the parameter of the current match
              ParameterInfo[] curParameters = iCurMatchCount == -1 ? GetMemberParameter(miBind) : null;
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

    private static ParameterInfo[] GetMemberParameter<TMEMBERTYPE>(TMEMBERTYPE mi)
      where TMEMBERTYPE : MemberInfo
    {
      MethodBase mb = mi as MethodBase;
      PropertyInfo pi = mi as PropertyInfo;

      if (mb != null)
        return mb.GetParameters();
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
