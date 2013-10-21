using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace Neo.IronLua
{
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
      MemberNotUnique,
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
          restrictions.Merge(BindingRestrictions.GetInstanceRestriction(target.Expression, target.LimitType));
        else
          restrictions.Merge(BindingRestrictions.GetTypeRestriction(target.Expression, target.LimitType));

        // try to bind the member
        Expression expr;
        switch (TryBindGetMember(this, target, out expr))
        {
          case BindResult.Ok:
            return new DynamicMetaObject(Parser.ToObjectExpression(expr), restrictions);
          case BindResult.MemberNotFound:
            return new DynamicMetaObject(Expression.Constant(null, typeof(object)), restrictions);
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
              return errorSuggestion ?? new DynamicMetaObject(ThrowExpression(String.Format("The property '{0}' is not writable.", Name)), restrictions);
            type = pi.PropertyType;
          }
          else if (member.MemberType == MemberTypes.Field)
          {
            FieldInfo fi = (FieldInfo)member;
            type = fi.FieldType;
          }
          else // Member kann nicht gesetzt werden
            return errorSuggestion ?? new DynamicMetaObject(ThrowExpression(String.Format("'{0}' is no property/field.", Name)), restrictions);

          return new DynamicMetaObject(
              Parser.ToObjectExpression(
                Expression.Assign(
                  Expression.MakeMemberAccess(Expression.Convert(target.Expression, member.DeclaringType), member),
                  Expression.Convert(Expression.Convert(value.Expression, value.LimitType), type)
                )
              ),
              restrictions.Merge(value.Restrictions).Merge(BindingRestrictions.GetTypeRestriction(value.Expression, value.LimitType))
            );
        }
        else if (members.Length == 0)
          return errorSuggestion ?? new DynamicMetaObject(ThrowExpression(String.Format("Property/field '{0}' can not be found.", Name)), restrictions);
        else
          return errorSuggestion ?? new DynamicMetaObject(ThrowExpression(String.Format("Property/field should be unique. [{0}]", Name)), restrictions);
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
        {
          DynamicMetaObject[] def = new DynamicMetaObject[indexes.Length + 1];
          def[0] = target;
          Array.Copy(indexes, 0, def, 1, indexes.Length);
          return Defer(def);
        }

        Expression expr;
        if (GetIndexAccessExpression(target, indexes, out expr))
          return new DynamicMetaObject(Parser.ToObjectExpression(expr), Lua.GetMethodSignatureRestriction(target, indexes));
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
        expr = Parser.ToObjectExpression(Expression.Assign(expr, Parser.RuntimeHelperConvertExpression(value.Expression, expr.Type)));

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
    private class LuaInvokeBinder : InvokeBinder
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
          return new DynamicMetaObject(ThrowExpression("Can not call nil value."), BindingRestrictions.GetInstanceRestriction(target.Expression, target.Value));

        return BindFallbackInvoke(target, args, errorSuggestion);
      } // func FallbackInvoke
    } // class LuaInvokeBinder

    #endregion

    #region -- class LuaInvokeMemberBinder --------------------------------------------

    ///////////////////////////////////////////////////////////////////////////////
    /// <summary></summary>
    private class LuaInvokeMemberBinder : InvokeMemberBinder
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
        switch (TryBindInvokeMember(this, false, target, args, out expr))
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

        Expression expr;

        if (Operation == ExpressionType.Equal || Operation == ExpressionType.NotEqual)
        {
          if (target.LimitType == arg.LimitType)
            expr = Expression.MakeBinary(Operation, Expression.Convert(target.Expression, target.LimitType), Expression.Convert(arg.Expression, arg.LimitType));
          else
            expr = Expression.Constant(Operation != ExpressionType.Equal, typeof(bool));
        }
        else
        {
          // Korrigiere die Typen
          Type limitType;
          if (target.LimitType != arg.LimitType)
            limitType = typeof(double);
          else
            limitType = target.LimitType;

          if (Operation == ExpressionType.Power && limitType != typeof(double)) // Power funktioniert nur mit double
            expr = Parser.RuntimeHelperConvertExpression(
                Expression.MakeBinary(
                  ExpressionType.Power,
                  Parser.RuntimeHelperConvertExpression(target.Expression, typeof(double)),
                  Parser.RuntimeHelperConvertExpression(arg.Expression, typeof(double))
                ), limitType);
          else
            expr = Expression.MakeBinary(
                      Operation,
                      Parser.RuntimeHelperConvertExpression(target.Expression, limitType),
                      Parser.RuntimeHelperConvertExpression(arg.Expression, limitType)
                    );
        }
        return new DynamicMetaObject(Parser.ToObjectExpression(expr), restrictions);
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
              ThrowExpression("Nil not defined for this operator."),
              target.Restrictions.Merge(BindingRestrictions.GetInstanceRestriction(target.Expression, null))
            );
        }
        else
          return new DynamicMetaObject(
              Parser.ToObjectExpression(
                Expression.MakeUnary(Operation, Expression.Convert(target.Expression, target.LimitType), target.LimitType)
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
      public LuaConvertFunctionResultBinder()
        : base(typeof(object[]), false)
      {
      } // ctor

      public override DynamicMetaObject FallbackConvert(DynamicMetaObject target, DynamicMetaObject errorSuggestion)
      {
        if (!target.HasValue)
          return Defer(target);

        if (target.LimitType == typeof(object[]) || typeof(object[]).IsAssignableFrom(target.LimitType))
          return new DynamicMetaObject(Expression.Convert(target.Expression, typeof(object[])), target.Restrictions.Merge(BindingRestrictions.GetTypeRestriction(target.Expression, target.LimitType)));
        else if (target.Value != null)
          return new DynamicMetaObject(Expression.NewArrayInit(typeof(object), Expression.Convert(target.Expression, typeof(object))), target.Restrictions.Merge(BindingRestrictions.GetTypeRestriction(target.Expression, target.LimitType)));
        else
          return new DynamicMetaObject(Expression.Constant(emptyResult, typeof(object[])), target.Restrictions.Merge(BindingRestrictions.GetInstanceRestriction(target.Expression, target.Value)));
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
    private static readonly CallSiteBinder functionResultBinder = new LuaConvertFunctionResultBinder();
    private static readonly CallSiteBinder convertToBooleanBinder = new LuaConvertToBooleanBinder();

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
        throw new ArgumentException("HasValue muss true sein");
#endif
      Type type = target.LimitType;

      // Suche die Member
      MemberInfo[] members = type.GetMember(binder.Name, GetBindingFlags(target.Value != null, binder.IgnoreCase));

      if (members.Length == 0)// Nothing found
      {
        expr = ThrowExpression(String.Format("Member '{0}' not resolved.", binder.Name));
        return BindResult.MemberNotFound;
      }
      else if (members.Length > 1) // only one member is allowed
      {
        expr = ThrowExpression(String.Format("Property/field must be unique. [{0}]", binder.Name));
        return BindResult.MemberNotUnique;
      }
      else // Member must be unique
      {
        var member = members[0];

        if (member.MemberType == MemberTypes.Property ||
          member.MemberType == MemberTypes.Field)
        {
          if (member.MemberType == MemberTypes.Property && !((PropertyInfo)member).CanRead)
          {
            expr = ThrowExpression(String.Format("The property '{0}' is not writable.", binder.Name));
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
          MethodInfo mi = (MethodInfo)member;
          Type typeDelegate = Expression.GetDelegateType((from p in mi.GetParameters() select p.ParameterType).Concat(new Type[] { mi.ReturnType }).ToArray());
          Delegate dlg = Delegate.CreateDelegate(typeDelegate, mi);
          expr = Expression.Constant(dlg, typeof(Delegate));
          return BindResult.Ok;
        }
        else if (member.MemberType == MemberTypes.NestedType)
        {
          expr = ThrowExpression(String.Format("'{0}' is not readable.", binder.Name));
          return BindResult.MemberNotFound;
        }
        else // Member is not readable
        {
          expr = ThrowExpression(String.Format("'{0}' is not readable.", binder.Name));
          return BindResult.NotReadable;
        }
      }
    } // func TryBindGetMember

    #endregion

    #region -- TryBindInvokeMember ----------------------------------------------------

    private static object[] emptyResult = new object[0];

    internal static BindResult TryBindInvokeMember(InvokeMemberBinder binder, bool lUseCtor, DynamicMetaObject target, DynamicMetaObject[] arguments, out Expression expr)
    {
      MethodBase miBind = lUseCtor ?
        (MethodBase)BindFindInvokeMember<ConstructorInfo>(null, false, target, arguments) :
        (MethodBase)BindFindInvokeMember<MethodInfo>(binder.Name, binder.IgnoreCase, target, arguments);

      // Method resolved
      if (miBind == null)
      {
        expr = ThrowExpression(String.Format("Method '{0}' not resolved.", binder.Name));
        return BindResult.MemberNotFound;
      }

      // Create the expression with the arguments
      expr = InvokeMemberExpression(target, miBind, arguments);
      return BindResult.Ok;
    } // func TryBindInvokeMember

    private static Expression InvokeMemberExpression(DynamicMetaObject target, MethodBase miBind, DynamicMetaObject[] arguments)
    {
      Expression expr;
      List<ParameterExpression> vars = new List<ParameterExpression>(); // Variables for the result
      List<Expression> callBlock = new List<Expression>();              // Instructions for the call
      Expression[] exprPara = BindInvokeParameters(miBind.GetParameters(), arguments, vars, callBlock);

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
            Expression.Constant(emptyResult, typeof(object[]))
            );
        }
        else if (returnType == typeof(object[])) // Call has the correct result-array
          expr = exprCall;
        else // Only one result, cast it to an array
          expr = Expression.NewArrayInit(typeof(object), Expression.Convert(exprCall, typeof(object)));
      else // Return the variables in the correct order
      {
        Expression[] r = new Expression[vars.Count + 1];
        r[0] = Parser.ToObjectExpression(exprCall);
        for (int i = 0; i < vars.Count; i++)
          r[i + 1] = Parser.ToObjectExpression(vars[i]);
        callBlock.Add(Expression.NewArrayInit(typeof(object), r));
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
                if (j == iCount - 1 && c.Type == typeof(object[])) // Start the array counting
                {
                  iLastArgIndex = i + j;
                  iLastIndexCount = 0;
                  break;
                }
                exprArray.Add(Parser.RuntimeHelperConvertExpression(c, elementType));
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
                  Expression.Convert(arguments[iLastArgIndex].Expression, typeof(Array)),
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

          // The last argument is an array (object[] eg. function), start the stretching of the array
          if (i == arguments.Length - 1 && arguments[i].LimitType == typeof(object[]))
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
            exprGet = Parser.RuntimeHelperConvertExpression(Parser.RuntimeHelperExpression(LuaRuntimeHelper.GetObject, arguments[iLastArgIndex].Expression, Expression.Constant(iLastIndexCount++, typeof(int))), typeParameter); // We stretch the last argument
          else if (i < arguments.Length)
            exprGet = Parser.RuntimeHelperConvertExpression(arguments[i].Expression, typeParameter); // Convert the Argument
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
              throw new ArgumentNullException("vars and callblock");
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
        members = type.GetMember(sName, GetBindingFlags(target.Value != null, lIgnoreCase) | BindingFlags.GetProperty | BindingFlags.SetProperty);
      else
        throw new ArgumentException();

      int iMaxParameterLength = 0;    // Max length of the argument list, can also MaxInt for variable argument length
      T miBind = null;                // Member that matches best
      int iCurParameterLength = 0;    // Length of the arguments of the current match
      int iCurMatchCount = -1;        // How many arguments match of this list

      // Get the max. list of arguments we want to consume
      if (arguments.Length > 0)
      {
        iMaxParameterLength =
          arguments[arguments.Length - 1].LimitType == typeof(object[]) ?
          Int32.MaxValue :
          arguments.Length;
      }

      for (int i = 0; i < members.Length; i++)
      {
        T miCur = members[i] as T;
        if (miCur != null)
        {
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
              int iCount;

              // Count the parameters of the current match, because they are not collected
              if (curParameters != null)
                iCurMatchCount = 0;

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
                if (curParameters != null && IsMatchParameter(j, curParameters, arguments))
                  iCurMatchCount++;
                if (IsMatchParameter(j, parameters, arguments))
                  iNewMatchCount++;
              }
              if (iNewMatchCount > iCurMatchCount)
              {
                miBind = miCur;
                iCurParameterLength = iParametersLength;
                iCurMatchCount = iNewMatchCount;
              }
            }
            else if (iMaxParameterLength != iCurParameterLength && iCurParameterLength < iParametersLength)
            {
              // if the new parameter length is greater then the old one, it matches best
              miBind = miCur;
              iCurParameterLength = iParametersLength;
              iCurMatchCount = -1;
            }
          }
          else
          {
            // First match, take it
            miBind = miCur;
            iCurParameterLength = iParametersLength;
            iCurMatchCount = -1;
          }
        }
      }
      return miBind;
    } // proc BindFindInvokeMember

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

    private static bool IsMatchParameter(int j, ParameterInfo[] parameters, DynamicMetaObject[] arguments)
    {
      if (j < parameters.Length && j < arguments.Length)
        return parameters[j].ParameterType == arguments[j].LimitType || parameters[j].ParameterType.IsAssignableFrom(arguments[j].LimitType);
      else
        return false;
    } // func IsMatchParameter

    internal static BindingRestrictions GetMethodSignatureRestriction(DynamicMetaObject target, DynamicMetaObject[] args)
    {
      BindingRestrictions restrictions;
      if (target != null)
      {
        restrictions = target.Restrictions.Merge(BindingRestrictions.Combine(args));
        if (target.Value == null)
          restrictions = restrictions.Merge(BindingRestrictions.GetInstanceRestriction(target.Expression, target.Value));
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

    private static DynamicMetaObject BindFallbackInvoke(DynamicMetaObject target, DynamicMetaObject[] args, DynamicMetaObject errorSuggestion)
    {
      var restrictions = target.Restrictions.Merge(BindingRestrictions.GetTypeRestriction(target.Expression, target.LimitType));

      // Handelt es sich um ein Delegate
      if (target.LimitType.IsSubclassOf(typeof(Delegate)))
      {
        // Suche die Methode
        MethodInfo mi = target.LimitType.GetMethod("Invoke", BindingFlags.Public | BindingFlags.Instance);
        if (mi == null)
        {
          return errorSuggestion ??
            new DynamicMetaObject(
            ThrowExpression(String.Format("Type '{0}' has no invoke method.", target.LimitType.Name)),
            restrictions
            );
        }

        // Erzeuge den Call-Befehl
        return new DynamicMetaObject(
          InvokeMemberExpression(target, mi, args),
          restrictions
        );
      }
      else
        return errorSuggestion ??
          new DynamicMetaObject(
            ThrowExpression(String.Format("Type '{0}' is not callable.", target.LimitType.Name)),
            restrictions);
    } // proc BindFallbackInvoke

    private static bool GetIndexAccessExpression(DynamicMetaObject target, DynamicMetaObject[] indexes, out Expression expr)
    {

      if (typeof(Array).IsAssignableFrom(target.LimitType)) // Is the target an array
      {
        Expression[] args = new Expression[indexes.Length];

        // The indexes should be casted on integer
        for (int i = 0; i < args.Length; i++)
          args[i] = Parser.RuntimeHelperConvertExpression(indexes[i].Expression, typeof(int));

        expr = Expression.ArrayAccess(Expression.Convert(target.Expression, target.LimitType), args);
        return true;
      }
      else
      {
        PropertyInfo pi = BindFindInvokeMember<PropertyInfo>("Item", false, target, indexes);

        if (pi == null) // No Index found
        {
          expr = ThrowExpression(String.Format("No index found for type '{0}'.", target.LimitType.Name));
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
        ciLuaException = typeof(LuaBindException).GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, null, new Type[] { typeof(string), typeof(Exception) }, null);

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

    internal static CallSiteBinder FunctionResultBinder { get { return functionResultBinder; } }
    internal static CallSiteBinder ConvertToBooleanBinder { get { return convertToBooleanBinder; } }

    /// <summary>Returns the instance for an empty result.</summary>
    public static object[] EmptyResult { get { return emptyResult; } }
  } // class Lua
}
