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

    #region -- Emit Helper ------------------------------------------------------------

    private static Expression SafeExpression(Func<Expression> f, Token tStart)
    {
      try
      {
        return f();
      }
      catch (LuaEmitException e)
      {
        throw ParseError(tStart, e.Message);
      }
    } // func SafeExpression

    private static Expression ConvertObjectExpression(Lua runtime, Token tStart, Expression expr, bool lConvertToObject = false)
    {
      if (expr.Type == typeof(LuaResult))
        return GetResultExpression(runtime, tStart, expr, 0);
      else if (expr.Type == typeof(object) && expr.NodeType == ExpressionType.Dynamic)
      {
        DynamicExpression exprDynamic = (DynamicExpression)expr;
        if (exprDynamic.Binder is InvokeBinder || exprDynamic.Binder is InvokeMemberBinder)
          return Expression.Dynamic(runtime.GetConvertBinder(typeof(object)), typeof(object), expr);
        else if (lConvertToObject)
          return Lua.EnsureType(expr, typeof(object));
        else
          return expr;
      }
      else if (lConvertToObject)
        return Lua.EnsureType(expr, typeof(object));
      else
        return expr;
    } // func ConvertObjectExpression

    private static Expression ConvertExpression(Lua runtime, Token tStart, Expression expr, Type toType)
    {
      return SafeExpression(() => LuaEmit.Convert(runtime, expr, expr.Type, toType, true), tStart);
    } // func ConvertExpression

    private static Expression GetResultExpression(Lua runtime, Token tStart, Expression expr, int iIndex)
    {
      return SafeExpression(() => LuaEmit.GetResultExpression(expr, expr.Type, iIndex), tStart);
    } // func GetResultExpression

    private static Expression UnaryOperationExpression(Lua runtime, Token tStart, ExpressionType op, Expression expr)
    {
      if (op != ExpressionType.ArrayLength)
        expr = ConvertObjectExpression(runtime, tStart, expr);
      return SafeExpression(() => LuaEmit.UnaryOperationExpression(runtime, op, expr, expr.Type, true), tStart);
    } // func UnaryOperationExpression

    private static Expression BinaryOperationExpression(Lua runtime, Token tStart, ExpressionType op, Expression expr1, Expression expr2)
    {
      expr1 = ConvertObjectExpression(runtime, tStart, expr1);
      expr2 = ConvertObjectExpression(runtime, tStart, expr2);
      return SafeExpression(() => LuaEmit.BinaryOperationExpression(runtime, op, expr1, expr1.Type, expr2, expr2.Type, true), tStart);
    } // func BinaryOperationExpression

    private static Expression ConcatOperationExpression(Lua runtime, Token tStart, Expression[] args)
    {
      if (Array.Exists(args, c => LuaEmit.IsDynamicType(c.Type))) // we have a dynamic type in the list -> to the concat on runtime
      {
        return SafeExpression(() => Expression.Call(Lua.ConcatStringMethodInfo, Expression.NewArrayInit(typeof(object),
          from e in args select ConvertObjectExpression(runtime, tStart, e, true))), tStart);
      }
      else
      {
        return SafeExpression(() => Expression.Call(Lua.StringConcatMethodInfo, Expression.NewArrayInit(typeof(string),
          from e in args select LuaEmit.Convert(runtime, e, e.Type, typeof(string), true))), tStart);
      }
    } // func ConcatOperationExpression

    private static Expression MemberGetExpression(Lua runtime, Token tStart, Expression instance, string sMember, bool lMethodMember)
    {
      return SafeExpression(() => LuaEmit.GetMember(runtime, instance, instance.Type, sMember, false, true), tStart);
    } // func MemberGetExpression

    private static Expression MemberSetExpression(Lua runtime, Token tStart, Expression instance, string sMember, bool lMethodMember, Expression set)
    {
      // Assign the value to a member
      if (lMethodMember)
      {
        return Expression.Call(
           ConvertExpression(runtime, null, instance, typeof(LuaTable)),
           Lua.TableSetMethodMethodInfo,
           Expression.Constant(sMember, typeof(string)),
           ConvertExpression(runtime, null, set, typeof(Delegate)));
      }
      else
        return SafeExpression(() => LuaEmit.SetMember(runtime, instance, instance.Type, sMember, false, set, set.Type, true), tStart);
    } // func MemberSetExpression

    private static Expression IndexGetExpression(Lua runtime, Token tStart, Expression instance, Expression[] indexes)
    {
      if (instance.Type == typeof(LuaTable))
      {
        if (indexes.Length == 1)
          return Expression.Call(instance, Lua.TableGetValueIdxMethodInfo, ConvertObjectExpression(runtime, tStart, indexes[0], true));
        else
          return Expression.Call(instance, Lua.TableGetValueIdxNMethodInfo, Expression.NewArrayInit(typeof(object), from i in indexes select ConvertObjectExpression(runtime, tStart,i, true)));
      }
      else if (instance.Type == typeof(LuaResult) && indexes.Length == 1)
      {
        return Expression.MakeIndex(
          instance,
          Lua.ResultIndexPropertyInfo,
          new Expression[] { ConvertExpression(runtime, tStart, indexes[0], typeof(int)) }
        );
      }
      else
        return SafeExpression(() => LuaEmit.GetIndex(runtime, instance, indexes, e => e, e => e.Type, true), tStart);
    } // func IndexGetExpression

    private static Expression IndexSetExpression(Lua runtime, Token tStart, Expression instance, Expression[] indexes, Expression set, bool lNoResult = true)
    {
      if (instance.Type == typeof(LuaTable))
      {
        Expression expr;
        if (indexes.Length == 1)
          expr = Expression.Call(instance, Lua.TableSetValueIdxMethodInfo,
            ConvertExpression(runtime, tStart, indexes[0], typeof(object)),
            ConvertObjectExpression(runtime, tStart, set, true),
            Expression.Constant(false)
          );
        else
          expr = Expression.Call(instance, Lua.TableSetValueIdxNMethodInfo,
            Expression.NewArrayInit(typeof(object), from i in indexes select ConvertObjectExpression(runtime, tStart, i, true)),
            ConvertObjectExpression(runtime, tStart, set, true)
          );
        if (lNoResult)
          return expr;
        else
          return Expression.Block(expr, set);
      }
      else
        return SafeExpression(() => LuaEmit.SetIndex(runtime, instance, indexes, set, e => e, e => e.Type, true), tStart);
    } // func IndexSetExpression

    private static Expression InvokeExpression(Lua runtime, Token tStart, Expression instance, InvokeResult result, Expression[] arguments, bool lParse)
    {
      MethodInfo mi;
      ConstantExpression constInstance = instance as ConstantExpression;
      LuaType t;
      if (constInstance != null && (t = constInstance.Value as LuaType) != null && t.Type != null) // we have a type, bind the ctor
      {
        Type type = t.Type;
        ConstructorInfo ci = LuaEmit.FindMember(type.GetConstructors(BindingFlags.Public | BindingFlags.CreateInstance | BindingFlags.Instance), arguments, e => e.Type);
        if (ci == null)
          throw ParseError(tStart, String.Format(Properties.Resources.rsMemberNotResolved, type.Name, "ctor"));

        return LuaEmit.BindParameter(runtime,
          args => Expression.New(ci, args),
          ci.GetParameters(),
          arguments,
          e => e, e => e.Type, true);
      }
      else if (LuaEmit.IsDynamicType(instance.Type))
      {
        // fallback is a dynamic call
        return EnsureInvokeResult(runtime, tStart,
          Expression.Dynamic(runtime.GetInvokeBinder(new CallInfo(arguments.Length)),
            typeof(object),
            new Expression[] { ConvertExpression(runtime, tStart, instance, typeof(object)) }.Union(
            from c in arguments select ConvertExpression(runtime, tStart, c, typeof(object)))
          ),
          result
        );
      }
      else if (typeof(Delegate).IsAssignableFrom(instance.Type) &&  // test if the type is assignable from delegate
        (mi = instance.Type.GetMethod("Invoke")) != null) // Search the Invoke method for the arguments
      {
        return EnsureInvokeResult(runtime, tStart,
          SafeExpression(() => LuaEmit.BindParameter<Expression>(
            runtime,
            args => Expression.Invoke(instance, args),
            mi.GetParameters(),
            arguments,
            e => e, e => e.Type, true), tStart),
          result
        );
      }
      else
        throw ParseError(tStart, LuaEmitException.GetMessageText(LuaEmitException.InvokeNoDelegate, instance.Type.Name));
    }  // func InvokeExpression

    private static Expression InvokeMemberExpression(Lua runtime, Token tStart, Expression instance, string sMember, InvokeResult result, Expression[] arguments)
    {
      if (LuaEmit.IsDynamicType(instance.Type))
      {
        return EnsureInvokeResult(runtime, tStart,
          Expression.Dynamic(runtime.GetInvokeMemberBinder(sMember, new CallInfo(arguments.Length)), typeof(object),
            new Expression[] { ConvertExpression(runtime, tStart, instance, typeof(object)) }.Union(
              from a in arguments select ConvertExpression(runtime, tStart, a, typeof(object))
            ).ToArray()
          ),
          result
         );
      }
      else
      {
        // look up the method
        MethodInfo method = LuaEmit.FindMethod(
          (MethodInfo[])instance.Type.GetMember(sMember, MemberTypes.Method, BindingFlags.Public | BindingFlags.Instance | BindingFlags.InvokeMethod),
          arguments,
          e => e.Type);

        if (method != null)
        {
          return SafeExpression(() => EnsureInvokeResult(runtime, tStart,
            LuaEmit.BindParameter(runtime,
              args => Expression.Call(instance, method, args),
              method.GetParameters(),
              arguments,
              e => e, e => e.Type, true),
            result
          ), tStart);
        }
        else
          throw ParseError(tStart, LuaEmitException.GetMessageText(LuaEmitException.MemberNotFound, sMember));
      }
    } // func InvokeMemberExpression

    private static Expression EnsureInvokeResult(Lua runtime, Token tStart, Expression expr, InvokeResult result)
    {
      switch (result)
      {
        case InvokeResult.LuaResult:
          return ConvertExpression(runtime, tStart, expr, typeof(LuaResult));
        case InvokeResult.Object:
          if (LuaEmit.IsDynamicType(expr.Type))
            return Expression.Dynamic(runtime.GetConvertBinder(typeof(object)), typeof(object), ConvertExpression(runtime, tStart, expr, typeof(object)));
          else
            return expr;
        default:
          return expr;
      }
    } // func EnsureInvokeResult

    #endregion

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
