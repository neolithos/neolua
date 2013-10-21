using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Text;

namespace Neo.IronLua
{
  #region -- class Parser -------------------------------------------------------------

  ///////////////////////////////////////////////////////////////////////////////
  /// <summary></summary>
  internal static partial class Parser
  {
    /// <summary>Converts to result to an object. Is the expression result an array, then we take the first value.</summary>
    /// <param name="expr">Expression that will be converted.</param>
    /// <param name="lConvert">Add the object cast to the expression.</param>
    /// <returns>Object-Expression</returns>
    internal static Expression ToObjectExpression(Expression expr, bool lConvert = true)
    {
      if (expr.Type == typeof(object))
        return expr;
      else if (expr.Type == typeof(object[]) || typeof(object[]).IsAssignableFrom(expr.Type))
      {
        return RuntimeHelperExpression(LuaRuntimeHelper.GetObject, Expression.Convert(expr, typeof(object[])), Expression.Constant(0, typeof(int)));
      }
      else if (lConvert)
        return Expression.Convert(expr, typeof(object));
      else
        return expr;
    } // func ToObjectExpression

    private static Expression ToBooleanExpression(Expression expr)
    {
      if (expr.Type == typeof(bool))
        return expr;
      else if (typeof(bool).IsAssignableFrom(expr.Type))
        return Expression.Convert(expr, typeof(bool));
      else
        return Expression.Dynamic(Lua.ConvertToBooleanBinder, typeof(bool), ToObjectExpression(expr));
    } // func ToBooleanExpression

    internal static Expression RuntimeHelperExpression(LuaRuntimeHelper runtimeHelper, params Expression[] args)
    {
      return Expression.Call(Lua.GetRuntimeHelper(runtimeHelper), args);
    } // func GetRuntimeHelper

    internal static Expression RuntimeHelperConvertExpression(Expression value, Type toType)
    {
      if (toType.IsAssignableFrom(value.Type))
        return Expression.Convert(value, toType);
      else
        return Expression.Convert(RuntimeHelperExpression(LuaRuntimeHelper.Convert, ToObjectExpression(value), Expression.Constant(toType, typeof(Type))), toType);
    } // func RuntimeHelperConvertExpression

  } // class Parser

  #endregion
}
