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
  public sealed class LuaResult : IDynamicMetaObjectProvider, IConvertible
  {
    [ThreadStatic]
    private static FieldInfo fiResult = null;
    [ThreadStatic]
    private static PropertyInfo piLength = null;

    #region -- class LuaResultMetaObject ----------------------------------------------

    ///////////////////////////////////////////////////////////////////////////////
    /// <summary></summary>
    private class LuaResultMetaObject : DynamicMetaObject
    {
      public LuaResultMetaObject(Expression expression, object value)
        : base(expression, BindingRestrictions.Empty, value)
      {
      } // ctor

      private Expression GetResultExpression()
      {
        if (fiResult == null)
          fiResult = typeof(LuaResult).GetField("result", BindingFlags.NonPublic | BindingFlags.GetField | BindingFlags.Instance);
        return Expression.Field(Expression.Convert(Expression, typeof(LuaResult)), fiResult);
      } // func GetResultExpression

      private Expression GetFirstResultExpression()
      {
        if (piLength == null)
          piLength = typeof(object[]).GetProperty("Length", BindingFlags.Instance | BindingFlags.Public | BindingFlags.GetProperty);
        
        return Expression.Condition(
          Expression.AndAlso(
            Expression.NotEqual(GetResultExpression(), Expression.Constant(null, typeof(object[]))),
            Expression.GreaterThan(Expression.Property(GetResultExpression(), piLength), Expression.Constant(0, typeof(int)))
          ),
          Expression.ArrayIndex(GetResultExpression(), new Expression[] { Expression.Constant(0, typeof(int)) }),
          Expression.Constant(null, typeof(object))
        );
      } // func GetFirstResultExpression

      private object GetFirstResult()
      {
        LuaResult v = (LuaResult)Value;
        return v.result != null && v.result.Length > 0 ? v.result[0] : null;
      } // GetFirstResult

      private DynamicMetaObject GetTargetMetaObject()
      {
        return new DynamicMetaObject(
          GetFirstResultExpression(),
          BindingRestrictions.GetTypeRestriction(Expression, typeof(LuaResult)),
          GetFirstResult()
        );
      } // func GetTargetMetaObject

      public override DynamicMetaObject BindConvert(ConvertBinder binder)
      {
        BindingRestrictions r = BindingRestrictions.GetTypeRestriction(Expression, typeof(LuaResult));
        LuaResult v = (LuaResult)Value;
        if (binder.Type == typeof(LuaResult))
          return new DynamicMetaObject(Expression.Convert(this.Expression, binder.ReturnType), r);
        else if (binder.Type == typeof(object[]))
          return new DynamicMetaObject(GetResultExpression(), r, v.result);
        else
          return binder.FallbackConvert(new DynamicMetaObject(
            GetFirstResultExpression(),
            r,
            GetFirstResult()));
      } // func BindConvert

      public override DynamicMetaObject BindInvoke(InvokeBinder binder, DynamicMetaObject[] args)
      {
        return binder.FallbackInvoke(GetTargetMetaObject(), args);
      } // func BindInvoke

      public override DynamicMetaObject BindInvokeMember(InvokeMemberBinder binder, DynamicMetaObject[] args)
      {
        return binder.FallbackInvokeMember(GetTargetMetaObject(), args);
      } // func BindInvokeMember

      public override DynamicMetaObject BindBinaryOperation(BinaryOperationBinder binder, DynamicMetaObject arg)
      {
        return binder.FallbackBinaryOperation(GetTargetMetaObject(), arg);
      } // func BindBinaryOperation

      public override DynamicMetaObject BindGetIndex(GetIndexBinder binder, DynamicMetaObject[] indexes)
      {
        return binder.FallbackGetIndex(this, indexes);
      } // func BindGetIndex

      public override DynamicMetaObject BindSetIndex(SetIndexBinder binder, DynamicMetaObject[] indexes, DynamicMetaObject value)
      {
        return binder.FallbackSetIndex(GetTargetMetaObject(), indexes, value);
      } // func BindSetIndex

      public override DynamicMetaObject BindGetMember(GetMemberBinder binder)
      {
        return binder.FallbackGetMember(GetTargetMetaObject());
      } // func BindGetMember

      public override DynamicMetaObject BindSetMember(SetMemberBinder binder, DynamicMetaObject value)
      {
        return binder.FallbackSetMember(GetTargetMetaObject(), value);
      } // func BindSetMember

      public override DynamicMetaObject BindUnaryOperation(UnaryOperationBinder binder)
      {
        return binder.FallbackUnaryOperation(GetTargetMetaObject());
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
      if (v == null)
        this.result = emptyArray;
      else if (v is object[])
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
        return (T)Lua.RtConvert(v, typeof(T)); 
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

    /// <summary>Return values.</summary>
    /// <param name="iIndex"></param>
    /// <returns></returns>
    public object this[int iIndex] { get { return result != null && iIndex >= 0 && iIndex < result.Length ? result[iIndex] : null; } }
    /// <summary>Access to the raw-result values.</summary>
    public object[] Values { get { return result; } }

    // -- Static --------------------------------------------------------------

    private static LuaResult empty = new LuaResult();
    private static object[] emptyArray = new object[0];

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
    private class LuaGetMemberBinder : GetMemberBinder
    {
      public LuaGetMemberBinder(string sName)
        : base(sName, false)
      {
      } // ctor

      public override DynamicMetaObject FallbackGetMember(DynamicMetaObject target, DynamicMetaObject errorSuggestion)
      {
        // defer the target, to get the type
        if (!target.HasValue)
          return Defer(target);

        // restrictions
        var restrictions = target.Restrictions;
        if (target.Value == null)
          restrictions = restrictions.Merge(BindingRestrictions.GetInstanceRestriction(target.Expression, null));
        else
          restrictions = restrictions.Merge(BindingRestrictions.GetTypeRestriction(target.Expression, target.LimitType));

        // try to bind the member
        Expression expr;
        switch (TryBindGetMember(this, target, out expr))
        {
          case BindResult.Ok:
            return new DynamicMetaObject(Parser.ToTypeExpression(expr, typeof(object)), restrictions);
          default:
            return errorSuggestion ?? new DynamicMetaObject(expr, restrictions);
        }
      } // func FallbackGetMember
    } // class LuaGetMemberBinder

    #endregion

    #region -- class LuaSetMemberBinder -----------------------------------------------

    ///////////////////////////////////////////////////////////////////////////////
    /// <summary></summary>
    private class LuaSetMemberBinder : SetMemberBinder
    {
      public LuaSetMemberBinder(string sName)
        : base(sName, false)
      {
      } // ctor

      public override DynamicMetaObject FallbackSetMember(DynamicMetaObject target, DynamicMetaObject value, DynamicMetaObject errorSuggestion)
      {
        // defer the target
        if (!target.HasValue)
          return Defer(target);

        // get the members of the type with the name
        MemberInfo[] members = target.LimitType.GetMember(Name, BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public);
        // restrictions
        var restrictions = target.Restrictions.Merge(BindingRestrictions.GetTypeRestriction(target.Expression, target.LimitType));

        // There should only one member with this name
        if (members.Length == 1)
        {
          Type type;
          var member = members[0];
          if (member.MemberType == MemberTypes.Property)
          {
            PropertyInfo pi = (PropertyInfo)member;
            if (!pi.CanWrite)
              return errorSuggestion ?? new DynamicMetaObject(ThrowExpression(String.Format(Properties.Resources.rsPropertyNotWritable, Name)), restrictions);
            type = pi.PropertyType;
          }
          else if (member.MemberType == MemberTypes.Field)
          {
            FieldInfo fi = (FieldInfo)member;
            type = fi.FieldType;
          }
          else // Member not setable
            return errorSuggestion ?? new DynamicMetaObject(ThrowExpression(String.Format(Properties.Resources.rsNoPropertyOrField, Name)), restrictions);

          return new DynamicMetaObject(
              Parser.ToTypeExpression(
                Expression.Assign(
                  Expression.MakeMemberAccess(Expression.Convert(target.Expression, member.DeclaringType), member),
                  Expression.Convert(Expression.Convert(value.Expression, value.LimitType), type)
                ),
                typeof(object)
              ),
              restrictions.Merge(value.Restrictions).Merge(BindingRestrictions.GetTypeRestriction(value.Expression, value.LimitType))
            );
        }
        else if (members.Length == 0)
          return errorSuggestion ?? new DynamicMetaObject(ThrowExpression(String.Format(Properties.Resources.rsPropertyNotFound, Name)), restrictions);
        else
          return errorSuggestion ?? new DynamicMetaObject(ThrowExpression(String.Format(Properties.Resources.rsPropertyNotUnique, Name)), restrictions);
      } // func FallbackSetMember
    } // class LuaSetMemberBinder

    #endregion

    #region -- class LuaGetIndexBinder ------------------------------------------------

    ///////////////////////////////////////////////////////////////////////////////
    /// <summary></summary>
    private class LuaGetIndexBinder : GetIndexBinder
    {
      public LuaGetIndexBinder(CallInfo callInfo)
        : base(callInfo)
      {
      } // ctor

      public override DynamicMetaObject FallbackGetIndex(DynamicMetaObject target, DynamicMetaObject[] indexes, DynamicMetaObject errorSuggestion)
      {
        // Defer the parameters
        if (!target.HasValue || indexes.Any(c => !c.HasValue))
          return Defer(target, indexes);

        Expression expr;
        if (GetIndexAccessExpression(target, indexes, out expr))
          return new DynamicMetaObject(Parser.ToTypeExpression(expr, typeof(object)), Lua.GetMethodSignatureRestriction(target, indexes));
        else
          return errorSuggestion ?? new DynamicMetaObject(expr, Lua.GetMethodSignatureRestriction(target, indexes));
      } // func FallbackGetIndex
    } // class LuaGetIndexBinder

    #endregion

    #region -- class LuaSetIndexBinder ------------------------------------------------

    ///////////////////////////////////////////////////////////////////////////////
    /// <summary></summary>
    private class LuaSetIndexBinder : SetIndexBinder
    {
      public LuaSetIndexBinder(CallInfo callInfo)
        : base(callInfo)
      {
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

        // Get the GetIndex-Expression
        Expression expr;
        if (!GetIndexAccessExpression(target, indexes, out expr))
          return errorSuggestion ?? new DynamicMetaObject(expr, Lua.GetMethodSignatureRestriction(target, indexes));

        // Create the Assign
        expr = Parser.ToTypeExpression(Expression.Assign(expr, Parser.RuntimeHelperConvertExpression(value.Expression, value.LimitType, expr.Type)), typeof(object));

        return new DynamicMetaObject(expr,
          Lua.GetMethodSignatureRestriction(target, indexes)
            .Merge(BindingRestrictions.GetTypeRestriction(value.Expression, value.LimitType))
          );
      } // func FallbackSetIndex
    } // class LuaSetIndexBinder

    #endregion

    #region -- class LuaInvokeBinder --------------------------------------------------

    ///////////////////////////////////////////////////////////////////////////////
    /// <summary></summary>
    internal class LuaInvokeBinder : InvokeBinder
    {
      public LuaInvokeBinder(CallInfo callInfo)
        : base(callInfo)
      {
      } // ctor

      public override DynamicMetaObject FallbackInvoke(DynamicMetaObject target, DynamicMetaObject[] args, DynamicMetaObject errorSuggestion)
      {
        //defer the target and all arguments
        if (!target.HasValue || args.Any(c => !c.HasValue))
          return Defer(target, args);

        if (target.Value == null) // Invoke on null value
          return new DynamicMetaObject(ThrowExpression(Properties.Resources.rsNilNotCallable), BindingRestrictions.GetInstanceRestriction(target.Expression, null));

        return BindFallbackInvoke(target, args, errorSuggestion);
      } // func FallbackInvoke
    } // class LuaInvokeBinder

    #endregion

    #region -- class LuaInvokeMemberBinder --------------------------------------------

    ///////////////////////////////////////////////////////////////////////////////
    /// <summary></summary>
    internal class LuaInvokeMemberBinder : InvokeMemberBinder
    {
      public LuaInvokeMemberBinder(string sName, CallInfo callInfo)
        : base(sName, false, callInfo)
      {
      } // ctor

      public override DynamicMetaObject FallbackInvoke(DynamicMetaObject target, DynamicMetaObject[] args, DynamicMetaObject errorSuggestion)
      {
        LuaInvokeBinder binder = new LuaInvokeBinder(CallInfo);
        return binder.Defer(target, args);
      } // func FallbackInvoke

      public override DynamicMetaObject FallbackInvokeMember(DynamicMetaObject target, DynamicMetaObject[] args, DynamicMetaObject errorSuggestion)
      {
        // defer target and all arguments
        if (!target.HasValue || args.Any(c => !c.HasValue))
          return Defer(target, args);

        var restrictions = GetMethodSignatureRestriction(target, args);
        Expression expr;
        switch (TryBindInvokeMember(this, target, args, out expr))
        {
          case BindResult.Ok:
            return new DynamicMetaObject(expr, restrictions);
          default:
            return errorSuggestion ?? new DynamicMetaObject(expr, restrictions);
        }
      } // func FallbackInvokeMember
    } // class LuaInvokeMemberBinder

    #endregion

    #region -- class LuaBinaryOperationBinder -----------------------------------------

    ///////////////////////////////////////////////////////////////////////////////
    /// <summary></summary>
    private class LuaBinaryOperationBinder : BinaryOperationBinder
    {
      public LuaBinaryOperationBinder(ExpressionType operation)
        : base(operation)
      {
      } // ctor

      public override DynamicMetaObject FallbackBinaryOperation(DynamicMetaObject target, DynamicMetaObject arg, DynamicMetaObject errorSuggestion)
      {
        // defer target and all arguments
        if (!target.HasValue || !arg.HasValue)
          return Defer(target, arg);

        // restrictions
        var restrictions = target.Restrictions
          .Merge(arg.Restrictions)
          .Merge(target.Value == null ? BindingRestrictions.GetInstanceRestriction(target.Expression, null) : BindingRestrictions.GetTypeRestriction(target.Expression, target.LimitType))
          .Merge(arg.Value == null ? BindingRestrictions.GetInstanceRestriction(arg.Expression, null) : BindingRestrictions.GetTypeRestriction(arg.Expression, arg.LimitType));

        Expression expr = Parser.ToTypeExpression(
          Parser.BinaryOperationExpression(null, 
            Operation,
            target.Expression,
            target.LimitType,
            arg.Expression,
            arg.LimitType), 
          typeof(object));

        return new DynamicMetaObject(expr, restrictions);
      } // func FallbackBinaryOperation
    } // class LuaBinaryOperationBinder

    #endregion

    #region -- class LuaUnaryOperationBinder ------------------------------------------

    ///////////////////////////////////////////////////////////////////////////////
    /// <summary></summary>
    private class LuaUnaryOperationBinder : UnaryOperationBinder
    {
      public LuaUnaryOperationBinder(ExpressionType operation)
        : base(operation)
      {
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
          return new DynamicMetaObject(
              Parser.ToTypeExpression(
                Parser.UnaryOperationExpression(null, Operation, target.Expression, target.LimitType),
                typeof(object)
              ),
              target.Restrictions.Merge(BindingRestrictions.GetTypeRestriction(target.Expression, target.LimitType))
            );
      } // func FallbackUnaryOperation
    } // class LuaUnaryOperationBinder

    #endregion

    #region -- class LuaConvertFunctionResultBinder -----------------------------------

    ///////////////////////////////////////////////////////////////////////////////
    /// <summary></summary>
    private class LuaConvertFunctionResultBinder : ConvertBinder
    {
      public LuaConvertFunctionResultBinder(Type type)
        : base(type, false)
      {
      } // ctor

      public override DynamicMetaObject FallbackConvert(DynamicMetaObject target, DynamicMetaObject errorSuggestion)
      {
        if (!target.HasValue)
          return Defer(target);

        if (this.Type == typeof(LuaResult))
        {
          if (target.LimitType == typeof(LuaResult))
            return new DynamicMetaObject(
              Expression.Convert(target.Expression, typeof(LuaResult)),
              target.Restrictions.Merge(BindingRestrictions.GetTypeRestriction(target.Expression, target.LimitType)));
          else if (target.Value != null)
            return new DynamicMetaObject(
              Expression.New(Lua.ResultConstructorInfo2, Expression.Convert(target.Expression, typeof(object))),
              target.Restrictions.Merge(BindingRestrictions.GetTypeRestriction(target.Expression, target.LimitType)));
          else
            return new DynamicMetaObject(
              Expression.Property(null, Lua.ResultEmptyPropertyInfo),
              target.Restrictions.Merge(BindingRestrictions.GetInstanceRestriction(target.Expression, target.Value)));
        }
        else if (this.Type == typeof(object))
        {
          if (target.LimitType == typeof(LuaResult))
          {
            return new DynamicMetaObject(
              Expression.Convert(Parser.GetResultExpression(target.Expression, 0), typeof(object)),
              target.Restrictions.Merge(BindingRestrictions.GetTypeRestriction(target.Expression, target.LimitType))
            );
          }
          else if (target.Value != null)
          {
            return new DynamicMetaObject(
              Expression.Convert(target.Expression, typeof(object)),
              target.Restrictions.Merge(BindingRestrictions.GetTypeRestriction(target.Expression, target.LimitType))
            );
          }
          else
          {
            return new DynamicMetaObject(
              Expression.Constant(null, typeof(object)),
              target.Restrictions.Merge(BindingRestrictions.GetInstanceRestriction(target.Expression, target.Value))
            );
          }
        }
        else
          throw new InvalidOperationException();
      } // func FallbackConvert
    } // class LuaConvertFunctionResultBinder

    #endregion

    #region -- class LuaConvertToBooleanBinder ----------------------------------------

    ///////////////////////////////////////////////////////////////////////////////
    /// <summary></summary>
    private class LuaConvertToBooleanBinder : ConvertBinder
    {
      public LuaConvertToBooleanBinder()
        : base(typeof(bool), false)
      {
      } // ctor

      private bool IsNumeric(Type type)
      {
        return type == typeof(byte) ||
          type == typeof(sbyte) ||
          type == typeof(short) ||
          type == typeof(ushort) ||
          type == typeof(int) ||
          type == typeof(uint) ||
          type == typeof(long) ||
          type == typeof(ulong) ||
          type == typeof(float) ||
          type == typeof(double) ||
          type == typeof(decimal);
      } // func IsNumeric

      public override DynamicMetaObject FallbackConvert(DynamicMetaObject target, DynamicMetaObject errorSuggestion)
      {
        if (!target.HasValue)
          return Defer(target);

        Expression expr;
        BindingRestrictions restrictions;
        if (target.Value == null)
        {
          expr = Expression.Constant(false, typeof(bool));
          restrictions = target.Restrictions.Merge(BindingRestrictions.GetInstanceRestriction(target.Expression, null));
        }
        else if (target.LimitType == typeof(bool))
        {
          expr = Expression.Convert(target.Expression, typeof(bool));
          restrictions = target.Restrictions.Merge(BindingRestrictions.GetTypeRestriction(target.Expression, typeof(bool)));
        }
        else if (target.LimitType.IsValueType)
        {
          if (IsNumeric(target.LimitType))
            expr = Expression.NotEqual(Expression.Convert(target.Expression, target.LimitType), Expression.Constant(0, target.LimitType));
          else
            expr = Expression.Constant(true, typeof(bool));
          restrictions = target.Restrictions.Merge(BindingRestrictions.GetTypeRestriction(target.Expression, target.LimitType));
        }
        else
        {
          expr = Expression.Constant(true, typeof(bool));
          restrictions = target.Restrictions.Merge(BindingRestrictions.GetTypeRestriction(target.Expression, target.LimitType));
        }
        return new DynamicMetaObject(expr, restrictions);
      } // func FallbackConvert
    } // class LuaConvertToBooleanBinder

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
    private static readonly CallSiteBinder functionLuaResultBinder = new LuaConvertFunctionResultBinder(typeof(LuaResult));
    private static readonly CallSiteBinder functionFirstResultBinder = new LuaConvertFunctionResultBinder(typeof(object));
    private static readonly CallSiteBinder convertToBooleanBinder = new LuaConvertToBooleanBinder();

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
          b = setMemberBinder[sName] = new LuaSetMemberBinder(sName);
      return b;
    } // func GetSetMemberBinder

    internal CallSiteBinder GetGetMemberBinder(string sName)
    {
      CallSiteBinder b;
      lock (getMemberBinder)
        if (!getMemberBinder.TryGetValue(sName, out b))
          b = getMemberBinder[sName] = new LuaGetMemberBinder(sName);
      return b;
    } // func GetGetMemberBinder

    internal CallSiteBinder GetGetIndexMember(CallInfo callInfo)
    {
      CallSiteBinder b;
      lock (getIndexBinder)
        if (!getIndexBinder.TryGetValue(callInfo, out b))
          b = getIndexBinder[callInfo] = new LuaGetIndexBinder(callInfo);
      return b;
    } // func GetGetIndexMember

    internal CallSiteBinder GetSetIndexMember(CallInfo callInfo)
    {
      CallSiteBinder b;
      lock (setIndexBinder)
        if (!setIndexBinder.TryGetValue(callInfo, out b))
          b = setIndexBinder[callInfo] = new LuaSetIndexBinder(callInfo);
      return b;
    } // func GetSetIndexMember

    internal CallSiteBinder GetInvokeBinder(CallInfo callInfo)
    {
      CallSiteBinder b;
      lock (invokeBinder)
        if (!invokeBinder.TryGetValue(callInfo, out b))
          b = invokeBinder[callInfo] = new LuaInvokeBinder(callInfo);
      return b;
    } // func GetInvokeBinder

    internal CallSiteBinder GetInvokeMemberBinder(string sMember, CallInfo callInfo)
    {
      CallSiteBinder b;
      MemberCallInfo mci = new MemberCallInfo(sMember, callInfo);
      lock (invokeMemberBinder)
        if (!invokeMemberBinder.TryGetValue(mci, out b))
          b = invokeMemberBinder[mci] = new LuaInvokeMemberBinder(sMember, callInfo);
      return b;
    } // func GetInvokeMemberBinder

    internal CallSiteBinder GetBinaryOperationBinder(ExpressionType expressionType)
    {
      CallSiteBinder b;
      lock (operationBinder)
        if (!operationBinder.TryGetValue(expressionType, out b))
          b = operationBinder[expressionType] = new LuaBinaryOperationBinder(expressionType);
      return b;
    } // func GetBinaryOperationBinder

    internal CallSiteBinder GetUnaryOperationBinary(ExpressionType expressionType)
    {
      CallSiteBinder b;
      lock (operationBinder)
        if (!operationBinder.TryGetValue(expressionType, out b))
          b = operationBinder[expressionType] = new LuaUnaryOperationBinder(expressionType);
      return b;
    } // func GetUnaryOperationBinary

    #endregion

    #region -- TryBindGetMember -------------------------------------------------------

    internal static BindResult TryBindGetMember(GetMemberBinder binder, DynamicMetaObject target, out Expression expr)
    {
#if DEBUG
      if (!target.HasValue)
        throw new ArgumentException("Can only bind defered values.");
#endif
      Type type = target.LimitType;

      // Suche die Member
      MemberInfo[] members = type.GetMember(binder.Name, GetBindingFlags(target.Value != null, binder.IgnoreCase));

      if (members.Length == 0)// Nothing found
      {
        expr = Expression.Constant(null, typeof(object));
        return BindResult.Ok;
      }
      else if (members.Length > 1) // only one member is allowed
      {
        expr = Expression.New(LuaOverloadedMethod.ciCtor,
          Expression.Convert(target.Expression, typeof(object)),
          Expression.NewArrayInit(typeof(MethodInfo), (from m in members select Expression.Constant((MethodInfo)m)))
        );
        return BindResult.Ok;
      }
      else // Member must be unique
      {
        var member = members[0];

        if (member.MemberType == MemberTypes.Property ||
          member.MemberType == MemberTypes.Field)
        {
          if (member.MemberType == MemberTypes.Property && !((PropertyInfo)member).CanRead)
          {
            expr = ThrowExpression(String.Format(Properties.Resources.rsPropertyNotReadable, binder.Name));
            return BindResult.NotReadable;
          }

          expr = Expression.MakeMemberAccess(
                  target.Value == null ?
                    (Expression)null :
                    (Expression)Expression.Convert(target.Expression, member.DeclaringType),
                  member);
          return BindResult.Ok;
        }
        else if (member.MemberType == MemberTypes.Method)
        {
          expr = Expression.Constant(Parser.CreateDelegate(target.Value, (MethodInfo)member), typeof(Delegate));
          return BindResult.Ok;
        }
        else if (member.MemberType == MemberTypes.NestedType)
        {
          expr = Expression.Call(LuaType.miGetTypeFromType, Expression.Constant((Type)member));
          return BindResult.Ok;
        }
        else // Member is not readable
        {
          expr = ThrowExpression(String.Format(Properties.Resources.rsMemberNotReadable, binder.Name));
          return BindResult.NotReadable;
        }
      }
    } // func TryBindGetMember

    #endregion

    #region -- TryBindInvokeMember ----------------------------------------------------

    internal static BindResult TryBindInvokeMember(InvokeMemberBinder binder, DynamicMetaObject target, DynamicMetaObject[] arguments, out Expression expr)
    {
      MethodBase miBind = binder == null ?
        (MethodBase)BindFindInvokeMember<ConstructorInfo>(null, false, target, arguments) :
        (MethodBase)BindFindInvokeMember<MethodInfo>(binder.Name, binder.IgnoreCase, target, arguments);

      // Method resolved
      if (miBind == null)
      {
        expr = ThrowExpression(String.Format(Properties.Resources.rsMemberNotResolved, binder == null ? "ctor" : binder.Name));
        return BindResult.MemberNotFound;
      }

      // check if we need to make an non-generic call
      if (miBind.ContainsGenericParameters)
        miBind = MakeNonGenericMethod((MethodInfo)miBind, arguments);

      // Create the expression with the arguments
      expr = InvokeMemberExpression(target, miBind, null, arguments);
      return BindResult.Ok;
    } // func TryBindInvokeMember

    internal static MethodInfo MakeNonGenericMethod(MethodInfo mi, DynamicMetaObject[] arguments)
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
            t = CombineType(t, arguments[j].LimitType);
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
      else if(t.IsAssignableFrom(type))
        return t;
      else if(type.IsAssignableFrom(t))
        return type;
      else
        return typeof(object);
    } // func CombineType

    internal static Expression InvokeMemberExpression(DynamicMetaObject target, MethodBase miBind, ParameterInfo[] alternativeParameters, DynamicMetaObject[] arguments)
    {
      Expression expr;
      List<ParameterExpression> vars = new List<ParameterExpression>(); // Variables for the result
      List<Expression> callBlock = new List<Expression>();              // Instructions for the call
      Expression[] exprPara = BindInvokeParameters(alternativeParameters ?? miBind.GetParameters(), arguments, vars, callBlock);

      // Return
      Type returnType;
      Expression exprCall;
      if (miBind is ConstructorInfo)
      {
        returnType = miBind.DeclaringType;
        exprCall = Expression.New((ConstructorInfo)miBind, exprPara);
      }
      else
      {
        MethodInfo mi = (MethodInfo)miBind;
        returnType = mi.ReturnType;
        exprCall = Expression.Call(target == null || target.Value == null ? null : Expression.Convert(target.Expression, target.LimitType), mi, exprPara);
      }

      // Create the call-block
      if (vars.Count == 0) // No return with variables
        if (returnType == typeof(void)) // No result at all
        {
          expr = Expression.Block(
            exprCall,
            Expression.Property(null, Lua.ResultEmptyPropertyInfo)
            );
        }
        else // the case will be done by the language
          expr = Expression.Convert(exprCall, typeof(object));
      else // Return the variables in the correct order
      {
        Expression[] r = new Expression[vars.Count + 1];
        r[0] = Parser.ToTypeExpression(exprCall, typeof(object));
        for (int i = 0; i < vars.Count; i++)
          r[i + 1] = Parser.ToTypeExpression(vars[i], typeof(object));
        callBlock.Add(Expression.Convert(Expression.New(Lua.ResultConstructorInfo, Expression.NewArrayInit(typeof(object), r)), typeof(object)));
        expr = Expression.Block(vars, callBlock);
      }

      return expr;
    } // func InvokeMemberExpression

    private static Expression[] BindInvokeParameters(ParameterInfo[] parameters, DynamicMetaObject[] arguments, List<ParameterExpression> vars, List<Expression> callBlock)
    {
      Expression[] exprPara = new Expression[parameters.Length]; // Parameters for the function

      int iLastArgIndex = -1;   // Index to the last object[] argument
      int iLastIndexCount = -1; // Index for the last object[] argument

      for (int i = 0; i < exprPara.Length; i++)
      {
        var p = parameters[i];

        // the last parameter is an array, stretch the argument list
        if (i == exprPara.Length - 1 && p.ParameterType.IsArray)
        {
          Type elementType = p.ParameterType.GetElementType();

          if (arguments.Length <= i && iLastArgIndex == -1) // No arguments left, create an empty array
            exprPara[i] = Expression.NewArrayInit(elementType);
          else
          {
            List<Expression> exprArray = new List<Expression>(); // Collect the last arguments

            // Attach the last arguments to the array
            if (iLastArgIndex == -1)
            {
              int iCount = arguments.Length - i;
              for (int j = 0; j < iCount; j++)
              {
                Expression c = arguments[i + j].Expression;
                if (j == iCount - 1 && c.Type == typeof(LuaResult)) // Start the array counting
                {
                  iLastArgIndex = i + j;
                  iLastIndexCount = 0;
                  break;
                }
                exprArray.Add(Parser.RuntimeHelperConvertExpression(c, arguments[i + j].LimitType, elementType));
              }
            }
            // Create the function that creates an complete array with all arguments
            if (iLastArgIndex >= 0)
            {
              exprPara[i] = Expression.Convert(
                Parser.RuntimeHelperExpression(
                  LuaRuntimeHelper.ConcatArrays,
                  Expression.Constant(elementType, typeof(Type)),
                  Expression.Convert(Expression.NewArrayInit(elementType, exprArray), typeof(Array)),
                  Expression.Convert(Expression.Property(arguments[iLastArgIndex].Expression, Lua.ResultValuesPropertyInfo), typeof(Array)),
                  Expression.Constant(iLastIndexCount, typeof(int))
                ),
                p.ParameterType);
            }
            else // Create the array with the arguments
              exprPara[i] = Expression.NewArrayInit(elementType, exprArray);
          }
        }
        else
        {
          // Holds the argument get
          Expression exprGet;

          // The last argument is a LuaResult (eg. function), start the stretching of the array
          if (i == arguments.Length - 1 && arguments[i].LimitType == typeof(LuaResult))
          {
            iLastArgIndex = i;
            iLastIndexCount = 0;
          }

          Type typeParameter;
          if (p.ParameterType.IsByRef)
            typeParameter = p.ParameterType.GetElementType();
          else
            typeParameter = p.ParameterType;

          // get-Expression for the argument
          if (iLastArgIndex >= 0)
            exprGet = Parser.RuntimeHelperConvertExpression(
              Parser.GetResultExpression(arguments[iLastArgIndex].Expression, iLastIndexCount++), 
              typeof(object), typeParameter); // We stretch the last argument
          else if (i < arguments.Length)
            exprGet = Parser.RuntimeHelperConvertExpression(arguments[i].Expression, arguments[i].LimitType, typeParameter); // Convert the Argument
          else if (p.IsOptional)
            exprGet = Expression.Constant(p.DefaultValue, typeParameter); // No argument, but we have a default value -> use it
          else
            exprGet = Expression.Default(typeParameter); // No argument, No default value -> use default of type

          // Create a variable for the byref parameters
          if (p.ParameterType.IsByRef)
            if (vars != null && callBlock != null)
            {
              ParameterExpression r = Expression.Variable(typeParameter, "r" + i.ToString());
              vars.Add(r);
              callBlock.Add(Expression.Assign(r, exprGet));
              exprPara[i] = r;
            }
            else
              throw new ArgumentNullException("vars|callblock");
          else
            exprPara[i] = exprGet; // Normal byval parameter
        }
      }
      return exprPara;
    } // func BindInvokeParameters

    private static T BindFindInvokeMember<T>(string sName, bool lIgnoreCase, DynamicMetaObject target, DynamicMetaObject[] arguments)
      where T : MemberInfo
    {
      Type type = target.LimitType;
      MemberInfo[] members;

      // Collect the member info's
      if (typeof(T) == typeof(ConstructorInfo))
        members = type.GetConstructors();
      else if (typeof(T) == typeof(MethodInfo))
        members = type.GetMember(sName, GetBindingFlags(target.Value != null, lIgnoreCase) | BindingFlags.InvokeMethod);
      else if (typeof(T) == typeof(PropertyInfo))
        if (String.IsNullOrEmpty(sName)) // look for indexer
          members = (from m in type.GetMembers(GetBindingFlags(true, false) | BindingFlags.GetProperty | BindingFlags.SetProperty) where m is PropertyInfo && ((PropertyInfo)m).GetIndexParameters().Length > 0 select m).ToArray();
        else
          members = type.GetMember(sName, GetBindingFlags(target.Value != null, lIgnoreCase) | BindingFlags.GetProperty | BindingFlags.SetProperty);
      else
        throw new ArgumentException();

      return BindFindInvokeMember<T>(members, arguments);
    } // proc BindFindInvokeMember

    internal static T BindFindInvokeMember<T>(MemberInfo[] members, DynamicMetaObject[] arguments)
      where T : MemberInfo
    {
      int iMaxParameterLength = 0;    // Max length of the argument list, can also MaxInt for variable argument length
      T miBind = null;                // Member that matches best
      int iCurParameterLength = 0;    // Length of the arguments of the current match
      int iCurMatchCount = -1;        // How many arguments match of this list
      int iCurMatchExactCount = -1;   // How many arguments match exact of this list

      // Get the max. list of arguments we want to consume
      if (arguments.Length > 0)
      {
        iMaxParameterLength =
          arguments[arguments.Length - 1].LimitType == typeof(LuaResult) ?
          Int32.MaxValue :
          arguments.Length;
      }

      for (int i = 0; i < members.Length; i++)
      {
        T miCur = members[i] as T;
        if (miCur != null)
        {
          // do not test methods with __arglist
          MethodInfo methodInfo = miCur as MethodInfo;
          if (methodInfo != null && (methodInfo.CallingConvention & CallingConventions.VarArgs) != 0)
            continue;

          // Get the Parameters
          ParameterInfo[] parameters = GetMemberParameter<T>(miCur);

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
              ParameterInfo[] curParameters = iCurMatchCount == -1 ? GetMemberParameter<T>(miBind) : null;
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
                if (curParameters != null && IsMatchParameter(j, curParameters, arguments, out lExact))
                {
                  iCurMatchCount++;
                  if (lExact)
                    iCurMatchExactCount++;
                }
                if (IsMatchParameter(j, parameters, arguments, out lExact))
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
    } // func BindFindInvokeMember

    private static ParameterInfo[] GetMemberParameter<T>(T mi)
      where T : MemberInfo
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

    private static bool IsMatchParameter(int j, ParameterInfo[] parameters, DynamicMetaObject[] arguments, out bool lExact)
    {
      if (j < parameters.Length && j < arguments.Length)
      {
        Type type1 = parameters[j].ParameterType;
        Type type2 = arguments[j].LimitType;

        if (type1 == type2) // exact equal types
        {
          lExact = true;
          return true;
        }
        else if (type1.IsAssignableFrom(type2)) // is at least assignable
        {
          lExact = false;
          return true;
        }
        else if (type1.IsGenericParameter) // the parameter is a generic type
        {
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
        }
      }

      lExact = false;
      return false;
    } // func IsMatchParameter

    internal static BindingRestrictions GetMethodSignatureRestriction(DynamicMetaObject target, DynamicMetaObject[] args)
    {
      BindingRestrictions restrictions;
      if (target != null)
      {
        restrictions = target.Restrictions.Merge(BindingRestrictions.Combine(args));
        if (target.Value == null)
          restrictions = restrictions.Merge(BindingRestrictions.GetInstanceRestriction(target.Expression, null));
        else
          restrictions = restrictions.Merge(BindingRestrictions.GetTypeRestriction(target.Expression, target.LimitType));
      }
      else
        restrictions = BindingRestrictions.Empty;

      for (int i = 0; i < args.Length; i++)
      {
        BindingRestrictions r;
        if (args[i].HasValue && args[i].Value == null)
          r = BindingRestrictions.GetInstanceRestriction(args[i].Expression, null);
        else
          r = BindingRestrictions.GetTypeRestriction(args[i].Expression, args[i].LimitType);
        restrictions = restrictions.Merge(r);
      }
      return restrictions;
    } // func GetMethodSignatureRestriction

    internal static DynamicMetaObject BindFallbackInvoke(DynamicMetaObject target, DynamicMetaObject[] args, DynamicMetaObject errorSuggestion)
    {
      var restrictions = target.Restrictions.Merge(BindingRestrictions.GetTypeRestriction(target.Expression, target.LimitType));

      // Handelt es sich um ein Delegate
      if (target.LimitType.IsSubclassOf(typeof(Delegate)))
      {
        // Suche die Methode
        MethodInfo mi;
        ParameterInfo[] parameters = null;

        if (target.Value != null)
          parameters = ((Delegate)target.Value).Method.GetParameters(); // use the correct parameters

        mi = target.LimitType.GetMethod("Invoke", BindingFlags.Public | BindingFlags.Instance);
        if (mi == null)
        {
          return errorSuggestion ??
            new DynamicMetaObject(
            ThrowExpression(String.Format(Properties.Resources.rsInvokeFailed, target.LimitType.Name)),
            restrictions
            );
        }

        if (mi.GetParameters().Length != parameters.Length) // closures or outer hidden parameters
          parameters = null;

        // Erzeuge den Call-Befehl
        return new DynamicMetaObject(
          InvokeMemberExpression(target, mi, parameters, args),
          restrictions
        );
      }
      else
        return errorSuggestion ??
          new DynamicMetaObject(
            ThrowExpression(String.Format(Properties.Resources.rsInvokeNoDelegate, target.LimitType.Name)),
            restrictions);
    } // proc BindFallbackInvoke

    private static bool GetIndexAccessExpression(DynamicMetaObject target, DynamicMetaObject[] indexes, out Expression expr)
    {
      if (typeof(Array).IsAssignableFrom(target.LimitType)) // Is the target an array
      {
        Expression[] args = new Expression[indexes.Length];

        // The indexes should be casted on integer
        for (int i = 0; i < args.Length; i++)
          args[i] = Parser.RuntimeHelperConvertExpression(indexes[i].Expression, indexes[i].LimitType, typeof(int));

        expr = Expression.ArrayAccess(Expression.Convert(target.Expression, target.LimitType), args);
        return true;
      }
      else
      {
        PropertyInfo pi = BindFindInvokeMember<PropertyInfo>(null, false, target, indexes);

        if (pi == null) // No Index found
        {
          expr = ThrowExpression(String.Format(Properties.Resources.rsNoIndexFound, target.LimitType.Name));
          return false;
        }
        else // Create the index expression
        {
          Expression[] args = BindInvokeParameters(pi.GetIndexParameters(), indexes, null, null);
          expr = Expression.MakeIndex(Expression.Convert(target.Expression, target.LimitType), pi, args);
          return true;
        }
      }
    } // func GetIndexAccessExpression

    #endregion

    #region -- ThrowExpression --------------------------------------------------------

    [ThreadStatic]
    private static ConstructorInfo ciLuaException = null;
    
    internal static Expression ThrowExpression(string sMessage)
    {
      if (ciLuaException == null)
        ciLuaException = typeof(LuaRuntimeException).GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, null, new Type[] { typeof(string), typeof(Exception) }, null);

      return Expression.Throw(
        Expression.New(
          ciLuaException,
          Expression.Constant(sMessage, typeof(string)),
          Expression.Constant(null, typeof(Exception))
        ),
        typeof(object)
      );
    } // func GenerateThrowExpression

    #endregion

    private static BindingFlags GetBindingFlags(bool lInstance, bool lIgnoreCase)
    {
      BindingFlags flags = BindingFlags.Public | BindingFlags.Static;
      if (lInstance)
        flags |= BindingFlags.Instance;
      if (lIgnoreCase)
        flags |= BindingFlags.IgnoreCase;
      return flags;
    } // func GetBindingFlags

    internal static CallSiteBinder FunctionLuaResultBinder { get { return functionLuaResultBinder; } }
    internal static CallSiteBinder FunctionFirstResultBinder { get { return functionFirstResultBinder; } }
    internal static CallSiteBinder ConvertToBooleanBinder { get { return convertToBooleanBinder; } }
  } // class Lua

  #endregion
}
