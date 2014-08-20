using System;
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
  #region -- class LuaResult ----------------------------------------------------------

  ///////////////////////////////////////////////////////////////////////////////
  /// <summary>Dynamic result object for lua functions.</summary>
  public sealed class LuaResult : IDynamicMetaObjectProvider, IConvertible, System.Collections.IEnumerable
  {
    #region -- class LuaResultMetaObject ----------------------------------------------

    ///////////////////////////////////////////////////////////////////////////////
    /// <summary></summary>
    private class LuaResultMetaObject : DynamicMetaObject
    {
      public LuaResultMetaObject(Expression expression, object value)
        : base(expression, BindingRestrictions.Empty, value)
      {
      } // ctor

      private Expression GetValuesExpression()
      {
        return Expression.Property(Expression.Convert(Expression, typeof(LuaResult)), Lua.ResultValuesPropertyInfo);
      } // func GetValuesExpression

      private Expression GetFirstResultExpression()
      {
        return LuaEmit.GetResultExpression(Expression, typeof(LuaResult), 0);
      } // func GetFirstResultExpression

      private DynamicMetaObject GetTargetDynamicCall(CallSiteBinder binder, Type typeReturn, Expression[] exprs)
      {
        return new DynamicMetaObject(
          Expression.Dynamic(binder, typeReturn, exprs),
          BindingRestrictions.GetTypeRestriction(Expression, typeof(LuaResult))
        );
      } // func GetTargetDynamicCall

      private DynamicMetaObject GetTargetDynamicCall(CallSiteBinder binder, Type typeReturn)
      {
        return GetTargetDynamicCall(binder, typeReturn,
          new Expression[] { GetFirstResultExpression() }
        );
      } // func GetTargetDynamicCall

      private DynamicMetaObject GetTargetDynamicCall(CallSiteBinder binder, Type typeReturn, DynamicMetaObject arg)
      {
        return GetTargetDynamicCall(binder, typeReturn,
          new Expression[] 
          {
            GetFirstResultExpression(),
            LuaEmit.Convert(Lua.GetRuntime(binder), arg.Expression, arg.LimitType, typeof(object), false)
          }
        );
      } // func GetTargetDynamicCall

      private DynamicMetaObject GetTargetDynamicCall(CallSiteBinder binder, Type typeReturn, DynamicMetaObject[] args)
      {
        return GetTargetDynamicCall(binder, typeReturn,
          LuaEmit.CreateDynamicArgs(Lua.GetRuntime(binder), GetFirstResultExpression(), typeof(object), args, mo => mo.Expression, mo => mo.LimitType)
        );
      } // func GetTargetDynamicCall

      public override DynamicMetaObject BindConvert(ConvertBinder binder)
      {
        BindingRestrictions r = BindingRestrictions.GetTypeRestriction(Expression, typeof(LuaResult));
        LuaResult v = (LuaResult)Value;
        if (binder.Type == typeof(LuaResult))
          return new DynamicMetaObject(Expression.Convert(this.Expression, binder.ReturnType), r);
        else if (binder.Type == typeof(object[]))
          return new DynamicMetaObject(GetValuesExpression(), r, v.result);
        else
          return new DynamicMetaObject(Expression.Dynamic(binder, binder.Type, GetFirstResultExpression()), r);
      } // func BindConvert

      public override DynamicMetaObject BindInvoke(InvokeBinder binder, DynamicMetaObject[] args)
      {
        return GetTargetDynamicCall(binder, binder.ReturnType, args);
      } // func BindInvoke

      public override DynamicMetaObject BindInvokeMember(InvokeMemberBinder binder, DynamicMetaObject[] args)
      {
        return GetTargetDynamicCall(binder, binder.ReturnType, args);
      } // func BindInvokeMember

      public override DynamicMetaObject BindBinaryOperation(BinaryOperationBinder binder, DynamicMetaObject arg)
      {
        return GetTargetDynamicCall(binder, binder.ReturnType, arg);
      } // func BindBinaryOperation

      public override DynamicMetaObject BindGetIndex(GetIndexBinder binder, DynamicMetaObject[] indexes)
      {
        return binder.FallbackGetIndex(this, indexes);
      } // func BindGetIndex

      public override DynamicMetaObject BindSetIndex(SetIndexBinder binder, DynamicMetaObject[] indexes, DynamicMetaObject value)
      {
        return GetTargetDynamicCall(binder, binder.ReturnType,
          LuaEmit.CreateDynamicArgs(Lua.GetRuntime(binder), GetFirstResultExpression(), typeof(object), indexes, value, mo => mo.Expression, mo => mo.LimitType)
        );
      } // func BindSetIndex

      public override DynamicMetaObject BindGetMember(GetMemberBinder binder)
      {
        return GetTargetDynamicCall(binder, binder.ReturnType);
      } // func BindGetMember

      public override DynamicMetaObject BindSetMember(SetMemberBinder binder, DynamicMetaObject value)
      {
        return GetTargetDynamicCall(binder, binder.ReturnType, value);
      } // func BindSetMember

      public override DynamicMetaObject BindUnaryOperation(UnaryOperationBinder binder)
      {
        return GetTargetDynamicCall(binder, binder.ReturnType);
      } // func BindUnaryOperation
    } // class LuaResultMetaObject

    #endregion

    private readonly object[] result;

    #region -- Ctor/Dtor/MO -----------------------------------------------------------

    /// <summary>Creates a empty result-object.</summary>
    public LuaResult()
    {
      this.result = emptyArray;
    } // ctor

    /// <summary>Creates a empty result-object.</summary>
    /// <param name="v">One result value</param>
    public LuaResult(object v)
    {
      if (v is object[])
        this.result = CopyResult((object[])v);
      else if (v is LuaResult)
        this.result = (LuaResult)v;
      else
        this.result = new object[] { v };
    } // ctor

    /// <summary>Creates a empty result-object.</summary>
    /// <param name="values">Result values</param>
    public LuaResult(params object[] values)
    {
      this.result = CopyResult(values);
    } // ctor

    /// <summary></summary>
    /// <returns></returns>
    public override string ToString()
    {
      if (result == null)
        return "<Empty>";
      else
      {
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < 10 && i < result.Length; i++)
        {
          if (i > 0)
            sb.Append(", ");
          sb.Append('{').Append(result[i]).Append('}');
        }
        return sb.ToString();
      }
    } // func ToString

    private static object GetObject(object v)
    {
      return v is LuaResult ? ((LuaResult)v)[0] : v;
    } // func GetObject

    private static object[] CopyResult(object[] values)
    {
      // are there values
      if (values == null || values.Length == 0)
        return emptyArray;
      else if (values.Length == 1 && values[0] is LuaResult) // Only on element, that is a result no copy necessary
        return (LuaResult)values[0];
      else if (values[values.Length - 1] is LuaResult) // is the last result an an result -> concat the arrays
      {
        object[] l = (LuaResult)values[values.Length - 1];
        object[] n = new object[values.Length - 1 + l.Length];

        // copy the first values
        for (int i = 0; i < values.Length - 1; i++)
          n[i] = GetObject(values[i]);

        // enlarge from the last result
        for (int i = 0; i < l.Length; i++)
          n[i + values.Length - 1] = GetObject(l[i]);

        return n;
      }
      else
      {
        object[] n = new object[values.Length];

        for (int i = 0; i < values.Length; i++)
          n[i] = GetObject(values[i]);

        return n;
      }
    } // func CopyResult

    /// <summary></summary>
    /// <param name="parameter"></param>
    /// <returns></returns>
    public DynamicMetaObject GetMetaObject(Expression parameter)
    {
      return new LuaResultMetaObject(parameter, this);
    } // func GetMetaObject

    #endregion

    #region -- IConvertible members ---------------------------------------------------

    /// <summary></summary>
    /// <returns></returns>
    public TypeCode GetTypeCode()
    {
      return TypeCode.Object;
    } // func GetTypeCode

    /// <summary></summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="iIndex"></param>
    /// <param name="default"></param>
    /// <returns></returns>
    public T GetValueOrDefault<T>(int iIndex, T @default)
    {
      object v = this[iIndex];
      try
      {
        return (T)Lua.RtConvertValue(v, typeof(T)); 
      }
      catch
      {
        return @default;
      }
    } // func GetValueOrDefault

    /// <summary></summary>
    /// <param name="provider"></param>
    /// <returns></returns>
    public bool ToBoolean(IFormatProvider provider = null)
    {
      return Convert.ToBoolean(this[0], provider);
    } // func ToBoolean

    /// <summary></summary>
    /// <param name="provider"></param>
    /// <returns></returns>
    public byte ToByte(IFormatProvider provider = null)
    {
      return Convert.ToByte(this[0], provider);
    } // func ToByte

    /// <summary></summary>
    /// <param name="provider"></param>
    /// <returns></returns>
    public char ToChar(IFormatProvider provider = null)
    {
      return Convert.ToChar(this[0], provider);
    } // func ToChar

    /// <summary></summary>
    /// <param name="provider"></param>
    /// <returns></returns>
    public DateTime ToDateTime(IFormatProvider provider = null)
    {
      return Convert.ToDateTime(this[0], provider);
    } // func ToDateTime

    /// <summary></summary>
    /// <param name="provider"></param>
    /// <returns></returns>
    public decimal ToDecimal(IFormatProvider provider = null)
    {
      return Convert.ToDecimal(this[0], provider);
    } // func ToDecimal

    /// <summary></summary>
    /// <param name="provider"></param>
    /// <returns></returns>
    public double ToDouble(IFormatProvider provider = null)
    {
      return Convert.ToDouble(this[0], provider);
    } // func ToDouble

    /// <summary></summary>
    /// <param name="provider"></param>
    /// <returns></returns>
    public short ToInt16(IFormatProvider provider = null)
    {
      return Convert.ToInt16(this[0], provider);
    } // func ToInt16

    /// <summary></summary>
    /// <param name="provider"></param>
    /// <returns></returns>
    public int ToInt32(IFormatProvider provider = null)
    {
      return Convert.ToInt32(this[0], provider);
    } // func ToInt32

    /// <summary></summary>
    /// <param name="provider"></param>
    /// <returns></returns>
    public long ToInt64(IFormatProvider provider = null)
    {
      return Convert.ToInt64(this[0], provider);
    } // func ToInt64

    /// <summary></summary>
    /// <param name="provider"></param>
    /// <returns></returns>
    public sbyte ToSByte(IFormatProvider provider = null)
    {
      return Convert.ToSByte(this[0], provider);
    } // func ToSByte

    /// <summary></summary>
    /// <param name="provider"></param>
    /// <returns></returns>
    public float ToSingle(IFormatProvider provider = null)
    {
      return Convert.ToSingle(this[0], provider);
    } // func ToSingle

    /// <summary></summary>
    /// <param name="provider"></param>
    /// <returns></returns>
    public string ToString(IFormatProvider provider = null)
    {
      return Convert.ToString(this[0], provider);
    } // func ToString

    /// <summary></summary>
    /// <param name="conversionType"></param>
    /// <param name="provider"></param>
    /// <returns></returns>
    public object ToType(Type conversionType, IFormatProvider provider = null)
    {
      object o = this[0];
      if (o == null)
        return null;

      TypeConverter conv = TypeDescriptor.GetConverter(o);
      return conv.ConvertTo(o, conversionType);
    } // func ToType

    /// <summary></summary>
    /// <param name="provider"></param>
    /// <returns></returns>
    public ushort ToUInt16(IFormatProvider provider = null)
    {
      return Convert.ToUInt16(this[0], provider);
    } // func ToUInt16

    /// <summary></summary>
    /// <param name="provider"></param>
    /// <returns></returns>
    public uint ToUInt32(IFormatProvider provider = null)
    {
      return Convert.ToUInt32(this[0], provider);
    } // func ToUInt32

    /// <summary></summary>
    /// <param name="provider"></param>
    /// <returns></returns>
    public ulong ToUInt64(IFormatProvider provider = null)
    {
      return Convert.ToUInt64(this[0], provider);
    } // func ToUInt64

    #endregion

    #region -- IEnumerable ------------------------------------------------------------

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
    {
      return Values.GetEnumerator();
    }

    #endregion

    /// <summary>Return values.</summary>
    /// <param name="iIndex"></param>
    /// <returns></returns>
    public object this[int iIndex] { get { return result != null && iIndex >= 0 && iIndex < result.Length ? result[iIndex] : null; } }
    /// <summary>Access to the raw-result values.</summary>
    public object[] Values { get { return result; } }
    /// <summary>Get's the number of results.</summary>
    public int Count { get { return result.Length; } }

    // -- Static --------------------------------------------------------------

    private static object[] emptyArray = new object[0];
    private static LuaResult empty = new LuaResult();

    /// <summary></summary>
    /// <param name="r"></param>
    /// <returns></returns>
    public static implicit operator object[](LuaResult r)
    {
      return r == null ? emptyArray : r.result;
    } // operator object[]
    
    /// <summary></summary>
    /// <param name="v"></param>
    /// <returns></returns>
    public static implicit operator LuaResult(object[] v)
    {
      return new LuaResult(v);
    } // operator LuaResult

    /// <summary>Represents a empty result</summary>
    public static LuaResult Empty
    {
      get { return empty; }
    } // prop Empty
  } // struct LuaResult

  #endregion

  #region -- ILuaBinder ---------------------------------------------------------------

  internal interface ILuaBinder
  {
    Lua Lua { get; }
  } // interface ILuaBinder

  #endregion

  #region -- class Lua ----------------------------------------------------------------

  ///////////////////////////////////////////////////////////////////////////////
  /// <summary></summary>
  public partial class Lua
  {
    #region -- enum BindResult --------------------------------------------------------

    ///////////////////////////////////////////////////////////////////////////////
    /// <summary>Result for the binding of methods</summary>
    internal enum BindResult
    {
      Ok,
      MemberNotFound,
      NotReadable,
      NotWriteable
    } // enum BindResult

    #endregion

    #region -- class LuaGetMemberBinder -----------------------------------------------

    ///////////////////////////////////////////////////////////////////////////////
    /// <summary></summary>
    private class LuaGetMemberBinder : GetMemberBinder, ILuaBinder
    {
      private Lua lua;

      public LuaGetMemberBinder(Lua lua, string sName)
        : base(sName, false)
      {
        this.lua = lua;
      } // ctor

      public override DynamicMetaObject FallbackGetMember(DynamicMetaObject target, DynamicMetaObject errorSuggestion)
      {
        // defer the target, to get the type
        if (!target.HasValue)
          return Defer(target);

        if (target.Value == null)
        {
          return errorSuggestion ??
            new DynamicMetaObject(
              ThrowExpression(Properties.Resources.rsNullReference, ReturnType),
              target.Restrictions.Merge(BindingRestrictions.GetInstanceRestriction(target.Expression, null))
            );
        }
        else
        {
          // try to bind the member
          Expression expr;
          try
          {
            expr = EnsureType(LuaEmit.GetMember(lua, target.Expression, target.LimitType, Name, IgnoreCase, false), ReturnType);
          }
          catch (LuaEmitException e)
          {
            if (errorSuggestion != null)
              return errorSuggestion;
            expr = ThrowExpression(e.Message, ReturnType);
          }

          // restrictions
          var restrictions = target.Restrictions.Merge(BindingRestrictions.GetTypeRestriction(target.Expression, target.LimitType));

          return new DynamicMetaObject(expr, restrictions);
        }
      } // func FallbackGetMember

      public Lua Lua { get { return lua; } }
    } // class LuaGetMemberBinder

    #endregion

    #region -- class LuaSetMemberBinder -----------------------------------------------

    ///////////////////////////////////////////////////////////////////////////////
    /// <summary></summary>
    private class LuaSetMemberBinder : SetMemberBinder, ILuaBinder
    {
      private Lua lua;

      public LuaSetMemberBinder(Lua lua, string sName)
        : base(sName, false)
      {
        this.lua = lua;
      } // ctor

      public override DynamicMetaObject FallbackSetMember(DynamicMetaObject target, DynamicMetaObject value, DynamicMetaObject errorSuggestion)
      {
        // defer the target
        if (!target.HasValue)
          return Defer(target);

        if (target.Value == null)
        {
          return errorSuggestion ??
            new DynamicMetaObject(
              ThrowExpression(String.Format(Properties.Resources.rsMemberNotResolved, target.LimitType.Name, Name), ReturnType),
              target.Restrictions.Merge(BindingRestrictions.GetInstanceRestriction(target.Expression, null))
            );
        }
        else
        {
          Expression expr;
          try
          {
            expr = Lua.EnsureType(LuaEmit.SetMember(lua, target.Expression, target.LimitType, Name, IgnoreCase, value.Expression, value.LimitType, false), ReturnType);
          }
          catch (LuaEmitException e)
          {
            if (errorSuggestion != null)
              return errorSuggestion;
            expr = ThrowExpression(e.Message, ReturnType);
          }

          return new DynamicMetaObject(
            expr,
            target.Restrictions.Merge(BindingRestrictions.GetTypeRestriction(target.Expression, target.LimitType).Merge(Lua.GetSimpleRestriction(value)))
          );
        }
      } // func FallbackSetMember

      public Lua Lua { get { return lua; } }
    } // class LuaSetMemberBinder

    #endregion

    #region -- class LuaGetIndexBinder ------------------------------------------------

    ///////////////////////////////////////////////////////////////////////////////
    /// <summary></summary>
    private class LuaGetIndexBinder : GetIndexBinder, ILuaBinder
    {
      private Lua lua;

      public LuaGetIndexBinder(Lua lua, CallInfo callInfo)
        : base(callInfo)
      {
        this.lua = lua;
      } // ctor

      public override DynamicMetaObject FallbackGetIndex(DynamicMetaObject target, DynamicMetaObject[] indexes, DynamicMetaObject errorSuggestion)
      {
        // Defer the parameters
        if (!target.HasValue || indexes.Any(c => !c.HasValue))
          return Defer(target, indexes);

        Expression expr;
        if (target.Value == null)
        {
          if (errorSuggestion != null)
            return errorSuggestion;
          expr = ThrowExpression(Properties.Resources.rsNullReference, ReturnType);
        }
        else
          try
          {
            expr = Lua.EnsureType(LuaEmit.GetIndex(lua, target, indexes, mo => mo.Expression, mo => mo.LimitType, false), ReturnType);
          }
          catch (LuaEmitException e)
          {
            if (errorSuggestion != null)
              return errorSuggestion;
            expr = ThrowExpression(e.Message, ReturnType);
          }

        return new DynamicMetaObject(expr, GetMethodSignatureRestriction(target, indexes));
      } // func FallbackGetIndex

      public Lua Lua { get { return lua; } }
    } // class LuaGetIndexBinder

    #endregion

    #region -- class LuaSetIndexBinder ------------------------------------------------

    ///////////////////////////////////////////////////////////////////////////////
    /// <summary></summary>
    private class LuaSetIndexBinder : SetIndexBinder, ILuaBinder
    {
      private Lua lua;

      public LuaSetIndexBinder(Lua lua, CallInfo callInfo)
        : base(callInfo)
      {
        this.lua = lua;
      } // ctor

      public override DynamicMetaObject FallbackSetIndex(DynamicMetaObject target, DynamicMetaObject[] indexes, DynamicMetaObject value, DynamicMetaObject errorSuggestion)
      {
        // Defer the parameters
        if (!target.HasValue || indexes.Any(c => !c.HasValue))
        {
          DynamicMetaObject[] def = new DynamicMetaObject[indexes.Length + 1];
          def[0] = target;
          Array.Copy(indexes, 0, def, 1, indexes.Length);
          return Defer(def);
        }

        Expression expr;
        if (target.Value == null)
        {
          if (errorSuggestion != null)
            return errorSuggestion;
          expr = ThrowExpression(Properties.Resources.rsNullReference, ReturnType);
        }
        else
          try
          {
            expr = Lua.EnsureType(LuaEmit.SetIndex(lua, target, indexes, value, mo => mo.Expression, mo => mo.LimitType, false), ReturnType);
          }
          catch (LuaEmitException e)
          {
            if (errorSuggestion != null)
              return errorSuggestion;
            expr = ThrowExpression(e.Message, ReturnType);
          }

        return new DynamicMetaObject(expr, GetMethodSignatureRestriction(target, indexes).Merge(Lua.GetSimpleRestriction(value)));
      } // func FallbackSetIndex

      public Lua Lua { get { return lua; } }
    } // class LuaSetIndexBinder

    #endregion

    #region -- class LuaInvokeBinder --------------------------------------------------

    ///////////////////////////////////////////////////////////////////////////////
    /// <summary></summary>
    internal class LuaInvokeBinder : InvokeBinder, ILuaBinder
    {
      private Lua lua;

      public LuaInvokeBinder(Lua lua, CallInfo callInfo)
        : base(callInfo)
      {
        this.lua = lua;
      } // ctor

      public override DynamicMetaObject FallbackInvoke(DynamicMetaObject target, DynamicMetaObject[] args, DynamicMetaObject errorSuggestion)
      {
        //defer the target and all arguments
        if (!target.HasValue || args.Any(c => !c.HasValue))
          return Defer(target, args);

        if (target.Value == null) // Invoke on null value
          return errorSuggestion ?? 
            new DynamicMetaObject(
              ThrowExpression(Properties.Resources.rsNilNotCallable),
              BindingRestrictions.GetInstanceRestriction(target.Expression, null)
            );
        else
        {
          BindingRestrictions restrictions = GetMethodSignatureRestriction(target, args);
          Expression expr;
          Delegate invokeTarget = target.Value as Delegate;

          if (invokeTarget == null)
          {
            if (errorSuggestion != null)
              return errorSuggestion;
            expr = ThrowExpression(LuaEmitException.GetMessageText(LuaEmitException.InvokeNoDelegate, target.LimitType.Name), typeof(object));
          }
          else
          {
            ParameterInfo[] methodParameters = invokeTarget.Method.GetParameters();
            ParameterInfo[] parameters = null;
            MethodInfo mi = target.LimitType.GetMethod("Invoke");
            if (mi != null)
            {
              ParameterInfo[] typeParameters = mi.GetParameters();
              if (typeParameters.Length != methodParameters.Length)
              {
                parameters = new ParameterInfo[typeParameters.Length];
                // the hidden parameters are normally at the beginning
                if (parameters.Length > 0)
                  Array.Copy(methodParameters, methodParameters.Length - typeParameters.Length, parameters, 0, parameters.Length);
              }
              else
                parameters = methodParameters;
            }
            else
              parameters = methodParameters;

            try
            {
              expr =
                EnsureType(
                  LuaEmit.BindParameter(lua,
                    _args => Expression.Invoke(EnsureType(target.Expression, target.LimitType), _args),
                    parameters,
                    args,
                    mo => mo.Expression, mo => mo.LimitType, false),
                  typeof(object), true);
            }
            catch(LuaEmitException e)
            {
              if (errorSuggestion != null)
                return errorSuggestion;
              expr = ThrowExpression(e.Message, ReturnType);
            }
          }

          return new DynamicMetaObject(expr, restrictions);
        }
      } // func FallbackInvoke

      public Lua Lua { get { return lua; } }
    } // class LuaInvokeBinder

    #endregion

    #region -- class LuaInvokeMemberBinder --------------------------------------------

    ///////////////////////////////////////////////////////////////////////////////
    /// <summary></summary>
    internal class LuaInvokeMemberBinder : InvokeMemberBinder, ILuaBinder
    {
      private Lua lua;

      public LuaInvokeMemberBinder(Lua lua, string sName, CallInfo callInfo)
        : base(sName, false, callInfo)
      {
        this.lua = lua;
      } // ctor

      public override DynamicMetaObject FallbackInvoke(DynamicMetaObject target, DynamicMetaObject[] args, DynamicMetaObject errorSuggestion)
      {
        LuaInvokeBinder binder = new LuaInvokeBinder(null, CallInfo);
        return binder.Defer(target, args);
      } // func FallbackInvoke

      public override DynamicMetaObject FallbackInvokeMember(DynamicMetaObject target, DynamicMetaObject[] args, DynamicMetaObject errorSuggestion)
      {
        // defer target and all arguments
        if (!target.HasValue || args.Any(c => !c.HasValue))
          return Defer(target, args);

        if (target.Value == null)
          return errorSuggestion ??
            new DynamicMetaObject(
              ThrowExpression(Properties.Resources.rsNilNotCallable, typeof(object)),
              target.Restrictions.Merge(BindingRestrictions.GetInstanceRestriction(target.Expression, null))
            );
        else
        {
          MethodInfo method =
            LuaEmit.FindMethod(
              (MethodInfo[])target.LimitType.GetMember(Name, MemberTypes.Method, GetBindingFlags(true, IgnoreCase)),
              args,
              mo => mo.LimitType);

          if (method == null)
          {
            if (errorSuggestion != null)
              return errorSuggestion;
            return new DynamicMetaObject(ThrowExpression(String.Format(Properties.Resources.rsMemberNotResolved, target.LimitType.Name,  Name), typeof(object)), GetMethodSignatureRestriction(target, args));
          }

          try
          {
            return
              new DynamicMetaObject(
                EnsureType(
                  LuaEmit.BindParameter(lua,
                    a => Expression.Call(EnsureType(target.Expression, target.LimitType), method, a),
                    method.GetParameters(),
                    args,
                    mo => mo.Expression, mo => mo.LimitType, false),
                  typeof(object),
                  true),
                GetMethodSignatureRestriction(target, args)
              );
          }
          catch (LuaEmitException e)
          {
            if (errorSuggestion != null)
              return errorSuggestion;
            return new DynamicMetaObject(ThrowExpression(e.Message, typeof(object)), GetMethodSignatureRestriction(target, args));
          }
        }
      } // func FallbackInvokeMember

      public Lua Lua { get { return lua; } }
    } // class LuaInvokeMemberBinder

    #endregion

    #region -- class LuaBinaryOperationBinder -----------------------------------------

    ///////////////////////////////////////////////////////////////////////////////
    /// <summary></summary>
    internal class LuaBinaryOperationBinder : BinaryOperationBinder, ILuaBinder
    {
      private Lua lua;
      private bool lInteger;

      public LuaBinaryOperationBinder(Lua lua, ExpressionType operation, bool lInteger)
        : base(operation)
      {
        this.lua = lua;
        this.lInteger = lInteger;
      } // ctor

      public override DynamicMetaObject FallbackBinaryOperation(DynamicMetaObject target, DynamicMetaObject arg, DynamicMetaObject errorSuggestion)
      {
        // defer target and all arguments
        if (!target.HasValue || !arg.HasValue)
          return Defer(target, arg);

        Expression expr;
        try
        {
          expr = EnsureType(LuaEmit.BinaryOperationExpression(lua, 
            lInteger && Operation == ExpressionType.Divide ? Lua.IntegerDivide : Operation, 
            target.Expression, target.LimitType, 
            arg.Expression, arg.LimitType, false), this.ReturnType);
        }
        catch (LuaEmitException e)
        {
          if (errorSuggestion != null)
            return errorSuggestion;
          expr = ThrowExpression(e.Message, this.ReturnType);
        }

        // restrictions
        var restrictions = target.Restrictions
          .Merge(arg.Restrictions)
          .Merge(Lua.GetSimpleRestriction(target))
          .Merge(Lua.GetSimpleRestriction(arg));

        return new DynamicMetaObject(expr, restrictions);
      } // func FallbackBinaryOperation

      public Lua Lua { get { return lua; } }
      public bool IsInteger { get { return lInteger; } }
    } // class LuaBinaryOperationBinder

    #endregion

    #region -- class LuaUnaryOperationBinder ------------------------------------------

    ///////////////////////////////////////////////////////////////////////////////
    /// <summary></summary>
    private class LuaUnaryOperationBinder : UnaryOperationBinder, ILuaBinder
    {
      private Lua lua;

      public LuaUnaryOperationBinder(Lua lua, ExpressionType operation)
        : base(operation)
      {
        this.lua = lua;
      } // ctor

      public override DynamicMetaObject FallbackUnaryOperation(DynamicMetaObject target, DynamicMetaObject errorSuggestion)
      {
        // defer the target
        if (!target.HasValue)
          return Defer(target);

        if (target.Value == null)
        {
          return errorSuggestion ??
            new DynamicMetaObject(
              ThrowExpression(Properties.Resources.rsNilOperatorError),
              target.Restrictions.Merge(BindingRestrictions.GetInstanceRestriction(target.Expression, null))
            );
        }
        else
        {
          Expression expr;
          try
          {
            expr = EnsureType(LuaEmit.UnaryOperationExpression(lua, Operation, target.Expression, target.LimitType, false), this.ReturnType);
          }
          catch (LuaEmitException e)
          {
            if (errorSuggestion != null)
              return errorSuggestion;
            expr = ThrowExpression(e.Message, this.ReturnType);
          }

          var restrictions = target.Restrictions.Merge(BindingRestrictions.GetTypeRestriction(target.Expression, target.LimitType));
          return new DynamicMetaObject(expr, restrictions);
        }
      } // func FallbackUnaryOperation

      public Lua Lua { get { return lua; } }
    } // class LuaUnaryOperationBinder

    #endregion

    #region -- class LuaConvertBinder -------------------------------------------------

    ///////////////////////////////////////////////////////////////////////////////
    /// <summary></summary>
    private class LuaConvertBinder : ConvertBinder, ILuaBinder
    {
      private static readonly MethodInfo miGetType;
      private static readonly MethodInfo miIsArithmetic;

      private Lua lua;

      public LuaConvertBinder(Lua lua, Type type)
        : base(type, false)
      {
        this.lua = lua;
      } // ctor

      public override DynamicMetaObject FallbackConvert(DynamicMetaObject target, DynamicMetaObject errorSuggestion)
      {
        if (!target.HasValue)
          return Defer(target);

        Expression expr = target.Expression;
        BindingRestrictions restrictions = null;

        if (target.Value == null) // get the default value
        {
          restrictions = target.Restrictions.Merge(BindingRestrictions.GetInstanceRestriction(target.Expression, target.Value));
          if (this.Type == typeof(LuaResult)) // replace null with empty LuaResult 
            expr = Expression.Property(null, Lua.ResultEmptyPropertyInfo);
          else if (this.Type == typeof(string)) // replace null with empty String
            expr = Expression.Field(null, Lua.StringEmptyFieldInfo);
          else
            expr = Expression.Default(this.Type);
        }
        else // convert the value
        {
          restrictions = target.Restrictions.Merge(BindingRestrictions.GetTypeRestriction(target.Expression, target.LimitType));
          try
          {
            expr = LuaEmit.Convert(lua, target.Expression, target.LimitType, this.Type, false); // no dynamic allowed
          }
          catch (LuaEmitException e)
          {
            if (errorSuggestion != null)
              return errorSuggestion;
            expr = ThrowExpression(e.Message, this.Type);
          }
        }

        return new DynamicMetaObject(expr, restrictions);
      } // func FallbackConvert

      static LuaConvertBinder()
      {
        miGetType = typeof(object).GetMethod("GetType", BindingFlags.Public | BindingFlags.Instance | BindingFlags.InvokeMethod);
        miIsArithmetic = typeof(LuaEmit).GetMethod("IsArithmeticType", BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.InvokeMethod, null, new Type[] { typeof(Type) }, null);
#if DEBUG
        if (miGetType == null || miIsArithmetic == null)
          throw new ArgumentNullException();
#endif
      } // sctor

      public Lua Lua { get { return lua; } }
    } // class LuaConvertBinder

    #endregion

    #region -- class MemberCallInfo ---------------------------------------------------

    ///////////////////////////////////////////////////////////////////////////////
    /// <summary></summary>
    private class MemberCallInfo
    {
      private string sMember;
      private CallInfo ci;

      public MemberCallInfo(string sMember, CallInfo ci)
      {
        this.sMember = sMember;
        this.ci = ci;
      } // ctor

      public override int GetHashCode()
      {
        return 0x28000000 ^ sMember.GetHashCode() ^ ci.GetHashCode();
      } // func GetHashCode

      public override bool Equals(object obj)
      {
        MemberCallInfo mci = obj as MemberCallInfo;
        return mci != null && mci.sMember == sMember && mci.ci.Equals(ci);
      } // func Equals
    } // struct MemberCallInfo

    #endregion

    #region -- Binder Cache -----------------------------------------------------------

    private Dictionary<ExpressionType, CallSiteBinder> operationBinder = new Dictionary<ExpressionType, CallSiteBinder>();
    private Dictionary<string, CallSiteBinder> getMemberBinder = new Dictionary<string, CallSiteBinder>();
    private Dictionary<string, CallSiteBinder> setMemberBinder = new Dictionary<string, CallSiteBinder>();
    private Dictionary<CallInfo, CallSiteBinder> getIndexBinder = new Dictionary<CallInfo, CallSiteBinder>();
    private Dictionary<CallInfo, CallSiteBinder> setIndexBinder = new Dictionary<CallInfo, CallSiteBinder>();
    private Dictionary<CallInfo, CallSiteBinder> invokeBinder = new Dictionary<CallInfo, CallSiteBinder>();
    private Dictionary<MemberCallInfo, CallSiteBinder> invokeMemberBinder = new Dictionary<MemberCallInfo, CallSiteBinder>();
    private Dictionary<Type, CallSiteBinder> convertBinder = new Dictionary<Type, CallSiteBinder>();

    private void ClearBinderCache()
    {
      lock (operationBinder)
        operationBinder.Clear();
      lock (getMemberBinder)
        getMemberBinder.Clear();
      lock (setMemberBinder)
        setMemberBinder.Clear();
      lock (getIndexBinder)
        getIndexBinder.Clear();
      lock (setIndexBinder)
        setIndexBinder.Clear();
      lock (invokeBinder)
        invokeBinder.Clear();
      lock (invokeMemberBinder)
        invokeMemberBinder.Clear();
    } // proc ClearBinderCache

    internal CallSiteBinder GetSetMemberBinder(string sName)
    {
      CallSiteBinder b;
      lock (setMemberBinder)
        if (!setMemberBinder.TryGetValue(sName, out b))
          b = setMemberBinder[sName] = new LuaSetMemberBinder(this, sName);
      return b;
    } // func GetSetMemberBinder

    internal CallSiteBinder GetGetMemberBinder(string sName)
    {
      CallSiteBinder b;
      lock (getMemberBinder)
        if (!getMemberBinder.TryGetValue(sName, out b))
          b = getMemberBinder[sName] = new LuaGetMemberBinder(this, sName);
      return b;
    } // func GetGetMemberBinder

    internal CallSiteBinder GetGetIndexMember(CallInfo callInfo)
    {
      CallSiteBinder b;
      lock (getIndexBinder)
        if (!getIndexBinder.TryGetValue(callInfo, out b))
          b = getIndexBinder[callInfo] = new LuaGetIndexBinder(this, callInfo);
      return b;
    } // func GetGetIndexMember

    internal CallSiteBinder GetSetIndexMember(CallInfo callInfo)
    {
      CallSiteBinder b;
      lock (setIndexBinder)
        if (!setIndexBinder.TryGetValue(callInfo, out b))
          b = setIndexBinder[callInfo] = new LuaSetIndexBinder(this, callInfo);
      return b;
    } // func GetSetIndexMember

    internal CallSiteBinder GetInvokeBinder(CallInfo callInfo)
    {
      CallSiteBinder b;
      lock (invokeBinder)
        if (!invokeBinder.TryGetValue(callInfo, out b))
          b = invokeBinder[callInfo] = new LuaInvokeBinder(this, callInfo);
      return b;
    } // func GetInvokeBinder

    internal CallSiteBinder GetInvokeMemberBinder(string sMember, CallInfo callInfo)
    {
      CallSiteBinder b;
      MemberCallInfo mci = new MemberCallInfo(sMember, callInfo);
      lock (invokeMemberBinder)
        if (!invokeMemberBinder.TryGetValue(mci, out b))
          b = invokeMemberBinder[mci] = new LuaInvokeMemberBinder(this, sMember, callInfo);
      return b;
    } // func GetInvokeMemberBinder

    internal CallSiteBinder GetBinaryOperationBinder(ExpressionType expressionType)
    {
      CallSiteBinder b;
      lock (operationBinder)
        if (!operationBinder.TryGetValue(expressionType, out b))
          b = operationBinder[expressionType] =
            expressionType == IntegerDivide ?
              new LuaBinaryOperationBinder(this, ExpressionType.Divide, true) :
              new LuaBinaryOperationBinder(this, expressionType, false);
      return b;
    } // func GetBinaryOperationBinder

    internal CallSiteBinder GetUnaryOperationBinary(ExpressionType expressionType)
    {
      CallSiteBinder b;
      lock (operationBinder)
        if (!operationBinder.TryGetValue(expressionType, out b))
          b = operationBinder[expressionType] = new LuaUnaryOperationBinder(this, expressionType);
      return b;
    } // func GetUnaryOperationBinary

    internal CallSiteBinder GetConvertBinder(Type type)
    {
      CallSiteBinder b;
      lock (convertBinder)
        if (!convertBinder.TryGetValue(type, out b))
          b = convertBinder[type] = new LuaConvertBinder(this, type);
      return b;
    } // func GetConvertBinder

    #endregion

    #region -- Binder Expression Helper -----------------------------------------------

    internal static Lua GetRuntime(object v)
    {
      ILuaBinder a = v as ILuaBinder;
      return a != null ? a.Lua : null;
    } // func GetRuntime

    internal static BindingRestrictions GetMethodSignatureRestriction(DynamicMetaObject target, DynamicMetaObject[] args)
    {
      BindingRestrictions restrictions = BindingRestrictions.Combine(args);
      if (target != null)
      {
        restrictions = restrictions
          .Merge(target.Restrictions)
          .Merge(GetSimpleRestriction(target));
      }

      for (int i = 0; i < args.Length; i++)
        restrictions = restrictions.Merge(GetSimpleRestriction(args[i]));

      return restrictions;
    } // func GetMethodSignatureRestriction

    internal static BindingRestrictions GetSimpleRestriction(DynamicMetaObject mo)
    {
      if (mo.HasValue && mo.Value == null)
        return BindingRestrictions.GetInstanceRestriction(mo.Expression, null);
      else
        return BindingRestrictions.GetTypeRestriction(mo.Expression, mo.LimitType);
    } // func GetSimpleRestriction

    internal static Expression ThrowExpression(string sMessage, Type type = null)
    {
      return Expression.Throw(
         Expression.New(
           Lua.RuntimeExceptionConstructorInfo,
           Expression.Constant(sMessage, typeof(string)),
           Expression.Constant(null, typeof(Exception))
         ),
         type ?? typeof(object)
       );
    } // func ThrowExpression
    
    internal static Expression EnsureType(Expression expr, Type returnType, bool lResult = false)
    {
      if (expr.Type == returnType)
        return expr;
      else if (expr.Type == typeof(void))
        if (lResult)
          return Expression.Block(expr, Expression.Property(null, Lua.ResultEmptyPropertyInfo));
        else
          return Expression.Block(expr, Expression.Default(returnType));
      else
        return Expression.Convert(expr, returnType);
    } // func EnsureType

    internal static BindingFlags GetBindingFlags(bool lInstance, bool lIgnoreCase)
    {
      BindingFlags flags = BindingFlags.Public | BindingFlags.Static;
      if (lInstance)
        flags |= BindingFlags.Instance;
      if (lIgnoreCase)
        flags |= BindingFlags.IgnoreCase;
      return flags;
    } // func GetBindingFlags

    #endregion
  } // class Lua

  #endregion
}
