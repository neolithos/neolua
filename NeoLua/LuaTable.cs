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
  // todo: pow, concat, len, index, newindex
        
  ///////////////////////////////////////////////////////////////////////////////
  /// <summary></summary>
  public class LuaTable : IDynamicMetaObjectProvider, INotifyPropertyChanged, IEnumerable<KeyValuePair<object, object>>
  {
    internal const string csMetaTable = "__metatable";

    #region -- enum MemberAccessFlag --------------------------------------------------

    ///////////////////////////////////////////////////////////////////////////////
    /// <summary></summary>
    [Flags]
    protected enum MemberAccessFlag
    {
      /// <summary>A normal get expression.</summary>
      None = 0,
      /// <summary>Get the expression for write access.</summary>
      ForWrite = 1,
      /// <summary>Get the expression for member access.</summary>
      MemberInvoke = 2,
      /// <summary>Member name is not case sensitive.</summary>
      IgnoreCase = 4
    } // enum MemberAccessFlag

    #endregion

    #region -- class LuaTableMetaObject -----------------------------------------------

    ///////////////////////////////////////////////////////////////////////////////
    /// <summary></summary>
    private class LuaTableMetaObject : DynamicMetaObject
    {
      #region -- Ctor/Dtor ------------------------------------------------------------

      public LuaTableMetaObject(LuaTable value, Expression expression)
        : base(expression, BindingRestrictions.Empty, value)
      {
      } // ctor

      #endregion

      #region -- Bind Helper ----------------------------------------------------------

      private DynamicMetaObject BindBinaryCall(BinaryOperationBinder binder, MethodInfo mi, DynamicMetaObject arg)
      {
        return new DynamicMetaObject(
          Lua.EnsureType(
            BinaryOperationCall(binder, mi, arg), 
            binder.ReturnType
          ),
          GetBinaryRestrictions(arg)
        );
      } // func BindBinaryCall

      private Expression BinaryOperationCall(BinaryOperationBinder binder, MethodInfo mi, DynamicMetaObject arg)
      {
        return Expression.Call(
          Lua.EnsureType(Expression, typeof(LuaTable)),
          mi,
          LuaEmit.Convert(Lua.GetRuntime(binder), arg.Expression, arg.LimitType, typeof(object), false)
        );
      } // func BinaryOperationCall

      private DynamicMetaObject UnaryOperationCall(UnaryOperationBinder binder, MethodInfo mi)
      {
        return new DynamicMetaObject(
          Lua.EnsureType(Expression.Call(Lua.EnsureType(Expression, typeof(LuaTable)), mi), binder.ReturnType),
          BindingRestrictions.GetTypeRestriction(Expression, typeof(LuaTable))
        );
      } // func UnaryOperationCall

      private BindingRestrictions GetBinaryRestrictions(DynamicMetaObject arg)
      {
        return Restrictions.Merge(
            arg.Value == null ?
              BindingRestrictions.GetInstanceRestriction(arg.Expression, null) :
              BindingRestrictions.GetTypeRestriction(arg.Expression, arg.LimitType)
          );
      } // func GetBinaryRestrictions

      #endregion

      #region -- BindBinaryOperation --------------------------------------------------

      public override DynamicMetaObject BindBinaryOperation(BinaryOperationBinder binder, DynamicMetaObject arg)
      {
        switch (binder.Operation)
        {
          case ExpressionType.Add:
            return BindBinaryCall(binder, Lua.TableAddMethodInfo, arg);
          case ExpressionType.Subtract:
            return BindBinaryCall(binder, Lua.TableSubMethodInfo, arg);
          case ExpressionType.Multiply:
            return BindBinaryCall(binder, Lua.TableMulMethodInfo, arg);
          case ExpressionType.Divide:
            {
              var luaOpBinder = binder as Lua.LuaBinaryOperationBinder;
              if (luaOpBinder != null && luaOpBinder.IsInteger)
                return BindBinaryCall(binder, Lua.TableIDivMethodInfo, arg);
              else
                return BindBinaryCall(binder, Lua.TableDivMethodInfo, arg);
            }
          case ExpressionType.Modulo:
            return BindBinaryCall(binder, Lua.TableModMethodInfo, arg);
          case ExpressionType.And:
            return BindBinaryCall(binder, Lua.TableBAndMethodInfo, arg);
          case ExpressionType.Or:
            return BindBinaryCall(binder, Lua.TableBOrMethodInfo, arg);
          case ExpressionType.ExclusiveOr:
            return BindBinaryCall(binder, Lua.TableBXOrMethodInfo, arg);
          case ExpressionType.LeftShift:
            return BindBinaryCall(binder, Lua.TableShlMethodInfo, arg);
          case ExpressionType.RightShift:
            return BindBinaryCall(binder, Lua.TableShrMethodInfo, arg);
          case ExpressionType.Equal:
            return new DynamicMetaObject(Lua.EnsureType(BinaryOperationCall(binder, Lua.TableEqualMethodInfo, arg), binder.ReturnType), GetBinaryRestrictions(arg));
          case ExpressionType.NotEqual:
            return new DynamicMetaObject(Lua.EnsureType(Expression.Not(BinaryOperationCall(binder, Lua.TableEqualMethodInfo, arg)), binder.ReturnType), GetBinaryRestrictions(arg));
          case ExpressionType.LessThan:
            return new DynamicMetaObject(Lua.EnsureType(BinaryOperationCall(binder, Lua.TableLessThanMethodInfo, arg), binder.ReturnType), GetBinaryRestrictions(arg));
          case ExpressionType.LessThanOrEqual:
            return new DynamicMetaObject(Lua.EnsureType(BinaryOperationCall(binder, Lua.TableLessEqualMethodInfo, arg), binder.ReturnType), GetBinaryRestrictions(arg));
          case ExpressionType.GreaterThan:
            return new DynamicMetaObject(Lua.EnsureType(Expression.Not(BinaryOperationCall(binder, Lua.TableLessThanMethodInfo, arg)), binder.ReturnType), GetBinaryRestrictions(arg));
          case ExpressionType.GreaterThanOrEqual:
            return new DynamicMetaObject(Lua.EnsureType(Expression.Not(BinaryOperationCall(binder, Lua.TableLessEqualMethodInfo, arg)), binder.ReturnType), GetBinaryRestrictions(arg));
        }
        return base.BindBinaryOperation(binder, arg);
      } // func BindBinaryOperation

      #endregion

      #region -- BindUnaryOperation----------------------------------------------------

      public override DynamicMetaObject BindUnaryOperation(UnaryOperationBinder binder)
      {
        switch (binder.Operation)
        {
          case ExpressionType.Negate:
            return UnaryOperationCall(binder, Lua.TableUnMinusMethodInfo);
          case  ExpressionType.OnesComplement:
            return UnaryOperationCall(binder, Lua.TableBNotMethodInfo);
        }
        return base.BindUnaryOperation(binder);
      } // func BindUnaryOperation

      #endregion

      #region -- BindInvoke -----------------------------------------------------------

      public override DynamicMetaObject BindInvoke(InvokeBinder binder, DynamicMetaObject[] args)
      {
        return new DynamicMetaObject(
          Lua.EnsureType(
            Expression.Call(
              Lua.EnsureType(Expression, typeof(LuaTable)),
              Lua.TableCallMethodInfo,
              Expression.NewArrayInit(typeof(object), from a in args select Lua.EnsureType(a.Expression, typeof(object)))
            ),
            binder.ReturnType,
            true
          ),
          Lua.GetMethodSignatureRestriction(this, args)
        );
      } // func BindInvoke 

      #endregion

      #region -- BindSetIndex ---------------------------------------------------------

      public override DynamicMetaObject BindSetIndex(SetIndexBinder binder, DynamicMetaObject[] indexes, DynamicMetaObject value)
      {
        if (Array.Exists(indexes, mo => !mo.HasValue))
          return binder.Defer(indexes);
        if (!value.HasValue)
          return binder.Defer(value);

        var restrictions = BindingRestrictions.GetExpressionRestriction(Expression.TypeIs(Expression, typeof(LuaTable)));

        // create the result
        Expression exprSet;
        if (value.LimitType == typeof(LuaResult))
        {
          exprSet = LuaEmit.GetResultExpression(value.Expression, value.LimitType, 0);
          restrictions = restrictions.Merge(BindingRestrictions.GetExpressionRestriction(Expression.TypeEqual(value.Expression, typeof(LuaResult))));
        }
        else
        {
          exprSet = Lua.EnsureType(value.Expression, typeof(object));
          restrictions = restrictions.Merge(BindingRestrictions.GetExpressionRestriction(Expression.Not(Expression.TypeEqual(value.Expression, typeof(LuaResult)))));
        }

        if (indexes.Length == 1)
        {
          // the index is normaly an expression --> call setvalue
          var t = new DynamicMetaObject(
            Expression.Block(
              SetValueExpression(
                Expression.Convert(Expression, typeof(LuaTable)),
                indexes[0].Expression,
                exprSet),
              exprSet
            ),
            restrictions
          );
          return t;
        }
        else
        {
          return new DynamicMetaObject(
            Expression.Block(
              Expression.Call(
                Lua.EnsureType(Expression, typeof(LuaTable)),
                Lua.TableSetValueIdxNMethodInfo,
                Expression.NewArrayInit(typeof(object), from i in indexes select Lua.EnsureType(i.Expression, typeof(object))),
                exprSet
              ),
              exprSet
            ),
            restrictions
          );
        }
      } // func BindSetIndex

      #endregion

      #region -- BindGetIndex ---------------------------------------------------------

      public override DynamicMetaObject BindGetIndex(GetIndexBinder binder, DynamicMetaObject[] indexes)
      {
        if (indexes.Length == 1)
        {
          // the index is normaly an expression
          return new DynamicMetaObject(
            Expression.Call(
              Lua.EnsureType(Expression, typeof(LuaTable)),
              Lua.TableGetValueIdxMethodInfo,
              Lua.EnsureType(Lua.EnsureType(indexes[0].Expression, indexes[0].LimitType), typeof(object))
            ),
            Lua.GetMethodSignatureRestriction(this, indexes)
          );
        }
        else
        {
          return new DynamicMetaObject(
            Expression.Call(
              Lua.EnsureType(Expression, typeof(LuaTable)),
              Lua.TableGetValueIdxNMethodInfo,
              Expression.NewArrayInit(typeof(object), from i in indexes select Lua.EnsureType(Lua.EnsureType(i.Expression, i.LimitType), typeof(object)))
            ),
            Lua.GetMethodSignatureRestriction(this, indexes)
          );
        }
      } // func BindGetIndex

      #endregion

      #region -- BindGetMember, BindSetMember -----------------------------------------

      public override DynamicMetaObject BindGetMember(GetMemberBinder binder)
      {
        return ((LuaTable)Value).GetMemberAccess(binder, Expression, binder.Name, binder.IgnoreCase ? MemberAccessFlag.ForWrite : MemberAccessFlag.None);
      } // func BindGetMember

      public override DynamicMetaObject BindSetMember(SetMemberBinder binder, DynamicMetaObject value)
      {
        if (!value.HasValue)
          return binder.Defer(value);

        ParameterExpression tmp = Expression.Variable(typeof(object), "#tmp");
        DynamicMetaObject moGet = ((LuaTable)Value).GetMemberAccess(binder, Expression, binder.Name, MemberAccessFlag.ForWrite | (binder.IgnoreCase ? MemberAccessFlag.ForWrite : MemberAccessFlag.None));
        return new DynamicMetaObject(
          Expression.Block(new ParameterExpression[] { tmp },
            Expression.Assign(tmp, Expression.Convert(value.Expression, tmp.Type)),
            Expression.IfThen(Expression.NotEqual(tmp, moGet.Expression),
              Expression.Block(
                Expression.Assign(moGet.Expression, tmp),
                Expression.Call(Lua.EnsureType(Expression, typeof(LuaTable)), Lua.TableOnPropertyChangedMethodInfo, Expression.Constant(binder.Name, typeof(string)))
              )
            ),
            tmp
          ), moGet.Restrictions.Merge(Lua.GetSimpleRestriction(value))
        );
      } // func BindSetMember

      public override DynamicMetaObject BindInvokeMember(InvokeMemberBinder binder, DynamicMetaObject[] args)
      {
        LuaTable t = (LuaTable)Value;

        // Is there a member with this Name
        int iIndex = t.GetValueIndex(binder.Name, binder.IgnoreCase, false);
        if (iIndex >= 0 && (binder is Lua.LuaInvokeMemberBinder || t.IsIndexMarkedAsMethod(iIndex))) // check if the value is a method
        {
          // add the target and the self parameter
          Expression[] expanedArgs = new Expression[args.Length + 2];
          expanedArgs[0] = t.GetIndexAccess(iIndex);
          expanedArgs[1] = Expression;
          for (int i = 0; i < args.Length; i++)
            expanedArgs[i + 2] = args[i].Expression;

          Expression expr = Expression.Condition(
            t.CheckMethodVersionExpression(Expression),
            Expression.Dynamic(t.GetInvokeBinder(new CallInfo(args.Length + 1)), typeof(object), expanedArgs),
            binder.GetUpdateExpression(typeof(object)));

          return new DynamicMetaObject(expr, BindingRestrictions.GetInstanceRestriction(Expression, Value));
        }
        else // do a fallback to a normal invoke
        {
          DynamicMetaObject moGet = t.GetMemberAccess(binder, Expression, binder.Name, MemberAccessFlag.MemberInvoke | (binder.IgnoreCase ? MemberAccessFlag.IgnoreCase : MemberAccessFlag.None));
          if (binder is Lua.LuaInvokeMemberBinder) // use the lua-binder
          {
            return binder.FallbackInvoke(moGet, args, null);
          }
          else // call a different binder
          {
            Expression[] exprArgs = new Expression[args.Length + 1];
            exprArgs[0] = moGet.Expression;
            for (int i = 0; i < args.Length; i++)
              exprArgs[i + 1] = args[i].Expression;

            return new DynamicMetaObject(Expression.Dynamic(t.GetInvokeBinder(binder.CallInfo), typeof(object), exprArgs), moGet.Restrictions);
          }
        }
      } // BindInvokeMember

      #endregion

      /// <summary></summary>
      /// <returns></returns>
      public override IEnumerable<string> GetDynamicMemberNames()
      {
        LuaTable t = (LuaTable)Value;
        foreach (var c in t.names.Keys)
          if (c is string)
            yield return (string)c;
      } // func GetDynamicMemberNames
    } // class LuaTableMetaObject

    #endregion

    /// <summary>Value has changed.</summary>
    public event PropertyChangedEventHandler PropertyChanged;

    private List<int> methods = null;             // Contains the indexes, they are method declarations
    private List<object> values = null;           // Array with values
    private Dictionary<object, int> names = null; // Names or Indices in the value-Array
    private LuaTable metaTable = null;            // Currently attached metatable
    private int iLength = 0;
    
    private int iMethodVersion = 0;   // if the methods-array is changed, then this values gets increased
    private Dictionary<CallInfo, CallSiteBinder> invokeBinder = new Dictionary<CallInfo, CallSiteBinder>();

    #region -- Ctor/Dtor --------------------------------------------------------------

    /// <summary>Creates a new lua table</summary>
    public LuaTable()
    {
      this.methods = new List<int>();
      this.values = new List<object>();
      this.names = new Dictionary<object, int>();
    } // ctor

    /// <summary></summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    public override bool Equals(object obj)
    {
      if (Object.ReferenceEquals(this, obj))
        return true;
      else if (obj != null)
        return OnEqual(obj);
      else
        return false;
    } // func Equals

    /// <summary></summary>
    /// <returns></returns>
    public override int GetHashCode()
    {
      return base.GetHashCode();
    } // func GetHashCode

    #endregion

    #region -- IDynamicMetaObjectProvider members -------------------------------------

    /// <summary>Returns the Meta-Object</summary>
    /// <param name="parameter"></param>
    /// <returns></returns>
    public DynamicMetaObject GetMetaObject(Expression parameter)
    {
      return new LuaTableMetaObject(this, parameter);
    } // func GetMetaObject

    #endregion

    #region -- Dynamic Members --------------------------------------------------------

    /// <summary>Override to manipulate the member access.</summary>
    /// <param name="binder">Binder for the process.</param>
    /// <param name="exprTable">Expression for the binding process.</param>
    /// <param name="memberName">Name of the member.</param>
    /// <param name="flags">Flags for the bind expression.</param>
    /// <returns>MO</returns>
    protected virtual DynamicMetaObject GetMemberAccess(DynamicMetaObjectBinder binder, Expression exprTable, object memberName, MemberAccessFlag flags)
    {
      // Get the index of the name
      int iIndex = GetValueIndex(memberName, (flags & MemberAccessFlag.IgnoreCase) != 0, (flags & MemberAccessFlag.ForWrite) != 0);

      if (iIndex == -2) // create an access to metatable
      {
        return new DynamicMetaObject(
          Expression.Property(Lua.EnsureType(exprTable, typeof(LuaTable)), Lua.TableMetaTablePropertyInfo),
          BindingRestrictions.GetTypeRestriction(exprTable, typeof(LuaTable))
        );
      }
      else if (iIndex == -1) // Create an update rule
      {
        // no fallback, to hide the static typed interface
        // if the length of the value-Array changed, then rebind
        Expression expr = Expression.Condition(
          TableChangedExpression(),
          Expression.Default(typeof(object)),
          binder.GetUpdateExpression(typeof(object)));

        return new DynamicMetaObject(expr, BindingRestrictions.GetInstanceRestriction(exprTable, this));
      }
      else if ((flags & MemberAccessFlag.MemberInvoke) != 0)
      {
        Expression expr = Expression.Condition(
          CheckMethodVersionExpression(exprTable),
          GetIndexAccess(iIndex),
          binder.GetUpdateExpression(typeof(object)));

        return new DynamicMetaObject(expr, BindingRestrictions.GetInstanceRestriction(exprTable, this));
      }
      else
      {
        // Create MO with restriction
        return new DynamicMetaObject(GetIndexAccess(iIndex), BindingRestrictions.GetInstanceRestriction(exprTable, this));
      }
    } // func GetMemberAccess

    private Expression GetIndexAccess(int iIndex)
    {
      // IndexAccess expression
      return Expression.MakeIndex(Expression.Constant(values), Lua.ListItemPropertyInfo, new Expression[] { Expression.Constant(iIndex) });
    } // func GetIndexAccess

    private Expression TableChangedExpression()
    {
      return Expression.Equal(
        Expression.Property(Expression.Constant(values), Lua.ListCountPropertyInfo),
        Expression.Constant(values.Count, typeof(int)));
    } // func TableChangedExpression

    private Expression CheckMethodVersionExpression(Expression exprTable)
    {
      return Lua.EnsureType(
        Expression.Call(
          Lua.EnsureType(exprTable, typeof(LuaTable)), 
          Lua.TableCheckMethodVersionMethodInfo, 
          Expression.Constant(iMethodVersion)
        ), 
        typeof(bool));
    } // func CheckMethodVersionExpression

    private CallSiteBinder GetInvokeBinder(CallInfo callInfo)
    {
      CallSiteBinder b;
      lock (invokeBinder)
        if (!invokeBinder.TryGetValue(callInfo, out b))
          b = invokeBinder[callInfo] = new Lua.LuaInvokeBinder(null, callInfo);
      return b;
    } // func GetInvokeBinder

    #endregion

    #region -- RegisterFunction, UnregisterFunction -----------------------------------

    /// <summary></summary>
    /// <param name="sName"></param>
    /// <param name="function"></param>
    public void RegisterFunction(string sName, Delegate function)
    {
      if (String.IsNullOrEmpty(sName))
        throw new ArgumentNullException("name");
      if (function == null)
        throw new ArgumentNullException("function");

      this[sName] = function;
    } // proc RegisterFunction

    #endregion

    #region -- GetValue, SetValue -----------------------------------------------------

    /// <summary>Notify property changed</summary>
    /// <param name="sPropertyName">Name of property</param>
    protected void OnPropertyChanged(string sPropertyName)
    {
      if (PropertyChanged != null)
        PropertyChanged(this, new PropertyChangedEventArgs(sPropertyName));
    } // proc OnPropertyChanged

    private int GetValueIndex(object item, bool lIgnoreCase, bool lCanCreateIndex)
    {
      int iIndex = -1;

      if (item is string && (string)item == csMetaTable)
        return -2;
      else if (lIgnoreCase && item is string) // Lookup the name in the hash-table
      {
        if (String.Compare((string)item, csMetaTable, true) == 0)
          return -2;

        foreach (var c in names)
        {
          if (c.Key is string && string.Compare((string)c.Key, (string)item, true) == 0)
          {
            iIndex = c.Value;
            break;
          }
        }
      }
      else if (!names.TryGetValue(item, out iIndex))
        iIndex = -1;

      // No index in the hash-table, can we create one
      if (iIndex == -1 && lCanCreateIndex)
      {
        names[item] = iIndex = values.Count;
        values.Add(null);

        // Update length
        int iNameIndex = item is int ? (int)item : -1;
        if (iNameIndex == -1)
          iLength = -1; // no array, length seem's not defined
        else
        {
          if (iLength == iNameIndex)
            iLength++;
          else
            iLength = -1; // no sequence
        }
      }

      return iIndex;
    } // func GetValueIndex

    private bool SetIndexValue(int iIndex, object value, bool lMarkAsMethod)
    {
      object c = values[iIndex];
      if (!Object.Equals(c, value))
      {
        // Mark methods
        int iMarkIndex = methods.BinarySearch(iIndex);
        if (lMarkAsMethod)
        {
          if (iMarkIndex < 0)
          {
            methods.Insert(~iMarkIndex, iIndex);
            iMethodVersion++;
          }
        }
        else
        {
          if (iMarkIndex >= 0)
          {
            methods.RemoveAt(iMarkIndex);
            iMethodVersion++;
          }
        }

        // set the value
        values[iIndex] = value;
        return true;
      }
      else
        return false;
    } // proc SetIndexValue

    private object GetValue(object item)
    {
      // Search the name in the hash-table
      int iIndex = GetValueIndex(item, false, false);
      return iIndex >= 0 ? values[iIndex] : iIndex == -2 ? metaTable : null;
    } // func GetValue

    private object GetValue(object[] items)
    {
      return GetValue(items, 0);
    } // func GetValue

    private object GetValue(object[] items, int iIndex)
    {
      object o = GetValue(items[iIndex]);
      if (iIndex == items.Length - 1)
        return o;
      else
      {
        LuaTable t = o as LuaTable;
        if (t == null)
          return null;
        else
          return t.GetValue(items, iIndex++);
      }
    } // func GetValue

    private void SetValue(object item, object value, bool lMarkAsMethod)
    {
      // Get the Index for the value, if the value is null then do not create a new value
      int iIndex = GetValueIndex(item, false, value != null);

      if (iIndex == -2)
        metaTable = value as LuaTable;
      else if (iIndex != -1 && SetIndexValue(iIndex, value, lMarkAsMethod)) // Set the value, if there is a index
      {
        // Notify property changed
        string sPropertyName = item as string;
        if (sPropertyName != null)
          OnPropertyChanged(sPropertyName);
      }
    } // proc SetValue

    private void SetValue(object[] items, object value)
    {
      SetValue(items, 0, value);
    } // func SetValue

    private void SetValue(object[] items, int iIndex, object value)
    {
      if (iIndex == items.Length - 1)
      {
        SetValue(items[iIndex], value, false);
      }
      else
      {
        int i = GetValueIndex(items[iIndex], false, true);
        LuaTable t = i == -2 ? metaTable : (values[i] as LuaTable);
        if (t == null)
        {
          t = new LuaTable();
          values[i] = t;
        }
        t.SetValue(items, iIndex++, values);
      }
    } // func SetValue

    internal object SetMethod(string sMethodName, Delegate method)
    {
      SetValue(sMethodName, method, true);
      return method;
    } // proc SetMethod

    /// <summary>Defines a new method on the table.</summary>
    /// <param name="sMethodName">Name of the member/name.</param>
    /// <param name="method">Method that has as a first parameter a LuaTable.</param>
    public void DefineMethod(string sMethodName, Delegate method)
    {
      Type typeFirstParameter = method.Method.GetParameters()[0].ParameterType;
      if (!typeFirstParameter.IsAssignableFrom(typeof(LuaTable)))
        throw new ArgumentException("Methods must have a LuaTable as first parameter.");

      SetValue(sMethodName, method, true);
    } // func DefineMethod

    internal bool CheckMethodVersion(int iLastVersion)
    {
      return iMethodVersion == iLastVersion;
    } // func CheckMethodVersion

    internal bool IsIndexMarkedAsMethod(int iIndex)
    {
      return methods.BinarySearch(iIndex) >= 0;
    } // func IsIndexMarkedAsMethod

    /// <summary>Returns the value of the table.</summary>
    /// <typeparam name="T">Excpected type for the value</typeparam>
    /// <param name="sName">Name of the member.</param>
    /// <param name="default">Replace value, if the member not exists or can not converted.</param>
    /// <returns>Value or default.</returns>
    public T GetOptionalValue<T>(string sName, T @default)
    {
      try
      {
        object o = GetValue(sName);
        return (T)Lua.RtConvertValue(o, typeof(T));
      }
      catch
      {
        return @default;
      }
    } // func GetOptionalValue

    /// <summary>Checks if the Member exists.</summary>
    /// <param name="sName">Membername</param>
    /// <param name="lIgnoreCase"></param>
    /// <returns></returns>
    public bool ContainsKey(string sName, bool lIgnoreCase = false)
    {
      return GetValueIndex(sName, lIgnoreCase, false) != -1;
    } // func ContainsKey

    #endregion

    #region -- Metatable --------------------------------------------------------------

    private T GetMetaTableOperator<T>(string sKey, string sOpDef)
      where T : class
    {
      if (metaTable != null)
      {
        object o = metaTable[sKey];
        if (o != null)
        {
          T f = o as T;
          if (f != null)
            return f;
          else
            throw new LuaRuntimeException(String.Format(Properties.Resources.rsTableOperatorIncompatible, sKey, sOpDef), 0, true);
        }
      }
      throw new LuaRuntimeException(String.Format(Properties.Resources.rsTableOperatorNotFound, sKey), 0, true);
    } // func GetMetaTableOperator

    private object UnaryOperation(string sKey)
    {
      return GetMetaTableOperator<Func<object>>(sKey, "object f()")();
    } // proc UnaryOperation

    private object BinaryOperation(string sKey, object arg)
    {
      return GetMetaTableOperator<Func<object, object>>(sKey, "object f(object)")(arg);
    } // proc BinaryOperation

    private bool BinaryBoolOperation(string sKey, object arg)
    {
      return GetMetaTableOperator<Func<object, bool>>(sKey, "bool f(object)")(arg);
    } // proc BinaryBoolOperation

    /// <summary></summary>
    /// <param name="arg"></param>
    /// <returns></returns>
    protected virtual object OnAdd(object arg)
    {
      return BinaryOperation("__add", arg);
    } // func OnAdd

    /// <summary></summary>
    /// <param name="arg"></param>
    /// <returns></returns>
    protected virtual object OnSub(object arg)
    {
      return BinaryOperation("__sub", arg);
    } // func OnSub

    /// <summary></summary>
    /// <param name="arg"></param>
    /// <returns></returns>
    protected virtual object OnMul(object arg)
    {
      return BinaryOperation("__mul", arg);
    } // func OnMul

    /// <summary></summary>
    /// <param name="arg"></param>
    /// <returns></returns>
    protected virtual object OnDiv(object arg)
    {
      return BinaryOperation("__div", arg);
    } // func OnDiv

    /// <summary></summary>
    /// <param name="arg"></param>
    /// <returns></returns>
    protected virtual object OnMod(object arg)
    {
      return BinaryOperation("__mod", arg);
    } // func OnMod

    /// <summary></summary>
    /// <param name="arg"></param>
    /// <returns></returns>
    protected virtual object OnPow(object arg)
    {
      return BinaryOperation("__pow", arg);
    } // func OnPow

    /// <summary></summary>
    /// <returns></returns>
    protected virtual object OnUnMinus()
    {
      return UnaryOperation("__unm");
    } // func OnUnMinus

    /// <summary></summary>
    /// <param name="arg"></param>
    /// <returns></returns>
    protected virtual object OnIDiv(object arg)
    {
      return BinaryOperation("__idiv", arg);
    } // func OnIDiv

    /// <summary></summary>
    /// <param name="arg"></param>
    /// <returns></returns>
    protected virtual object OnBAnd(object arg)
    {
      return BinaryOperation("__band", arg);
    } // func OnBAnd

    /// <summary></summary>
    /// <param name="arg"></param>
    /// <returns></returns>
    protected virtual object OnBOr(object arg)
    {
      return BinaryOperation("__bor", arg);
    } // func OnBOr

    /// <summary></summary>
    /// <param name="arg"></param>
    /// <returns></returns>
    protected virtual object OnBXor(object arg)
    {
      return BinaryOperation("__bxor", arg);
    } // func OnBXor

    /// <summary></summary>
    /// <returns></returns>
    protected virtual object OnBNot()
    {
      return UnaryOperation("__bnot");
    } // func OnBNot

    /// <summary></summary>
    /// <param name="arg"></param>
    /// <returns></returns>
    protected virtual object OnShl(object arg)
    {
      return BinaryOperation("__shl", arg);
    } // func OnShl

    /// <summary></summary>
    /// <param name="arg"></param>
    /// <returns></returns>
    protected virtual object OnShr(object arg)
    {
      return BinaryOperation("__shr", arg);
    } // func OnShr

    /// <summary></summary>
    /// <param name="arg"></param>
    /// <returns></returns>
    protected virtual object OnConcat(object arg)
    {
      return BinaryOperation("__concat", arg);
    } // func OnShr

    /// <summary></summary>
    /// <returns></returns>
    protected virtual object OnLen()
    {
      return UnaryOperation("__len");
    } // func OnLen

    /// <summary></summary>
    /// <param name="arg"></param>
    /// <returns></returns>
    protected virtual bool OnEqual(object arg)
    {
      return BinaryBoolOperation("__eq", arg);
    } // func OnEqual

    /// <summary></summary>
    /// <param name="arg"></param>
    /// <returns></returns>
    protected virtual bool OnLessThan(object arg)
    {
      return BinaryBoolOperation("__lt", arg);
    } // func OnLessThan

    /// <summary></summary>
    /// <param name="arg"></param>
    /// <returns></returns>
    protected virtual bool OnLessEqual(object arg)
    {
      return BinaryBoolOperation("__le", arg);
    } // func OnLessEqual

    /// <summary></summary>
    /// <param name="key"></param>
    /// <returns></returns>
    protected virtual object OnIndex(object key)
    {
      return BinaryOperation("__index", key);
    } // func OnIndex

    /// <summary></summary>
    /// <param name="key"></param>
    /// <param name="value"></param>
    /// <returns></returns>
    protected virtual object OnNewIndex(object key, object value)
    {
      return GetMetaTableOperator<Func<object, object, object>>("__newindex", "object f(key, object)")(key, value);
    } // func OnIndex

    /// <summary></summary>
    /// <param name="args"></param>
    /// <returns></returns>
    protected virtual object OnCall(object[] args)
    {
      return GetMetaTableOperator<Func<object[], object>>("__call", "object f(object[])")(args);
    } // func OnCall

    #endregion

    #region -- Expressions ------------------------------------------------------------

    internal static Expression SetValueExpression(Expression table, Expression index, Expression set)
    {
      return Expression.Call(
        Lua.EnsureType(table, typeof(LuaTable)),
        Lua.TableSetValueIdxMethodInfo,
        Lua.EnsureType(index, typeof(object)),
        Lua.EnsureType(set, typeof(object)),
        Expression.Constant(false)
      );
    } // func SetValueExpression

    #endregion

    #region -- IEnumerator members ----------------------------------------------------

    /// <summary></summary>
    /// <returns></returns>
    public IEnumerator<KeyValuePair<object, object>> GetEnumerator()
    {
      foreach (var c in names)
      {
        var v = values[c.Value];
        if (v != null)
          yield return new KeyValuePair<object, object>(c.Key, v);
      }
    } // func IEnumerator<KeyValuePair<object, object>>

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
    {
      return GetEnumerator();
    } // func System.Collections.IEnumerable.GetEnumerator

    #endregion

    /// <summary>Returns or sets an value in the lua-table.</summary>
    /// <param name="iIndex">Index.</param>
    /// <returns>Value or <c>null</c></returns>
    public object this[int iIndex] { get { return GetValue(iIndex); } set { SetValue(iIndex, value, false); } }
    /// <summary>Returns or sets an value in the lua-table.</summary>
    /// <param name="sName">Index.</param>
    /// <returns>Value or <c>null</c></returns>
    public object this[string sName] { get { return GetValue(sName); } set { SetValue(sName, value, false); } }
    /// <summary>Returns or sets an value in the lua-table.</summary>
    /// <param name="item">Index.</param>
    /// <returns>Value or <c>null</c></returns>
    public object this[object item] { get { return GetValue(item); } set { SetValue(item, value, false); } }

    /// <summary>Length if it is an array.</summary>
    public int Length { get { return iLength; } }
    /// <summary>Access to the __metatable</summary>
    public LuaTable MetaTable { get { return metaTable; } set { metaTable = value; } }

    #region -- Table Manipulation -----------------------------------------------------

    /// <summary></summary>
    /// <param name="t"></param>
    /// <param name="sep"></param>
    /// <param name="i"></param>
    /// <param name="j"></param>
    /// <returns></returns>
    public static string concat(LuaTable t, string sep = null, int i = 0, int j = int.MaxValue)
    {
      StringBuilder sb = new StringBuilder();

      foreach (var c in t)
      {
        int k = c.Key is int ? (int)c.Key : -1;
        if (k >= i && k <= j)
        {
          if (!String.IsNullOrEmpty(sep) && sb.Length > 0)
            sb.Append(sep);
          sb.Append(c.Value);
        }
      }

      return sb.ToString();
    } // func concat

    /// <summary></summary>
    /// <param name="t"></param>
    /// <param name="pos"></param>
    /// <param name="value"></param>
    public static void insert(LuaTable t, object pos, object value = null)
    {
      // the pos is optional
      if (!(pos is int) && value == null)
      {
        value = pos;
        if (t.Length < 0)
          pos = 0;
        else
          pos = t.Length;
      }

      // insert the value at the position
      int iPos = Convert.ToInt32(pos);
      object c = value;
      while (true)
      {
        if (t[iPos] == null)
        {
          t[iPos] = c;
          break;
        }
        else
        {
          object tmp = t[iPos];
          t[iPos] = c;
          c = tmp;
        }
        iPos++;
      }
    } // proc insert

    /// <summary></summary>
    /// <param name="values"></param>
    /// <returns></returns>
    public static LuaTable pack(object[] values)
    {
      LuaTable t = new LuaTable();
      for (int i = 0; i < values.Length; i++)
        t[i] = values[i];
      return t;
    } // func pack

    /// <summary></summary>
    /// <param name="t"></param>
    /// <param name="pos"></param>
    public static void remove(LuaTable t, int pos = -1)
    {
      if (pos == -1)
        pos = t.Length;
      if (pos == -1)
        return;

      while (true)
      {
        if (t[pos] == null)
          break;
        t[pos] = t[pos + 1];
        pos++;
      }
    } // proc remove

    /// <summary></summary>
    /// <param name="t"></param>
    /// <param name="sort"></param>
    public static void sort(LuaTable t, Delegate sort = null)
    {
      object[] values = unpack(t); // unpack in a normal array

      // sort the array
      if (sort == null)
        Array.Sort(values);
      else
        Array.Sort(values, (a, b) => ((Func<object, object, LuaResult>)sort)(a, b).ToInt32());

      // copy the values back
      for (int i = 0; i < values.Length; i++)
        t[i] = values[i];

      // remove the overflow
      List<int> removeValues = new List<int>();
      foreach (var c in t)
      {
        int i = c.Key is int ? (int)c.Key : -1;
        if (i >= values.Length)
          removeValues.Add(i);
      }

      for (int i = 0; i < removeValues.Count; i++)
        t[removeValues[i]] = null;
    } // proc sort

    /// <summary></summary>
    /// <param name="t"></param>
    /// <param name="i"></param>
    /// <param name="j"></param>
    /// <returns></returns>
    public static LuaResult unpack(LuaTable t, int i = 0, int j = int.MaxValue)
    {
      List<object> r = new List<object>();

      foreach (var c in t)
      {
        int k = c.Key is int ? (int)c.Key : -1;
        if (k >= i && k <= j)
          r.Add(c.Value);
      }

      return r.ToArray();
    } // func unpack

    #endregion

    // -- Static --------------------------------------------------------------

    #region -- c#/vb.net operators ----------------------------------------------------

    /// <summary></summary>
    /// <param name="table"></param>
    /// <param name="arg"></param>
    /// <returns></returns>
    public static object operator +(LuaTable table, object arg)
    {
      return table.OnAdd(arg);
    } // operator +

    /// <summary></summary>
    /// <param name="table"></param>
    /// <param name="arg"></param>
    /// <returns></returns>
    public static object operator -(LuaTable table, object arg)
    {
      return table.OnSub(arg);
    } // operator -

    /// <summary></summary>
    /// <param name="table"></param>
    /// <param name="arg"></param>
    /// <returns></returns>
    public static object operator *(LuaTable table, object arg)
    {
      return table.OnMul(arg);
    } // operator *

    /// <summary></summary>
    /// <param name="table"></param>
    /// <param name="arg"></param>
    /// <returns></returns>
    public static object operator /(LuaTable table, object arg)
    {
      return table.OnDiv(arg);
    } // operator /

    /// <summary></summary>
    /// <param name="table"></param>
    /// <param name="arg"></param>
    /// <returns></returns>
    public static object operator %(LuaTable table, object arg)
    {
      return table.OnMod(arg);
    } // operator %

    /// <summary></summary>
    /// <param name="table"></param>
    /// <returns></returns>
    public static object operator -(LuaTable table)
    {
      return table.OnUnMinus();
    } // operator -

    /// <summary></summary>
    /// <param name="table"></param>
    /// <param name="arg"></param>
    /// <returns></returns>
    public static bool operator ==(LuaTable table, object arg)
    {
      if (Object.ReferenceEquals(table, null))
        return Object.ReferenceEquals(arg, null);
      else
        return table.Equals(arg);
    } // operator ==

    /// <summary></summary>
    /// <param name="table"></param>
    /// <param name="arg"></param>
    /// <returns></returns>
    public static bool operator !=(LuaTable table, object arg)
    {
      if (Object.ReferenceEquals(table, null))
        return !Object.ReferenceEquals(arg, null);
      else
        return !table.Equals(arg);
    } // operator !=

    /// <summary></summary>
    /// <param name="table"></param>
    /// <param name="arg"></param>
    /// <returns></returns>
    public static object operator <(LuaTable table, object arg)
    {
      return table.OnLessThan(arg);
    } // operator <

    /// <summary></summary>
    /// <param name="table"></param>
    /// <param name="arg"></param>
    /// <returns></returns>
    public static object operator >(LuaTable table, object arg)
    {
      return !table.OnLessThan(arg);
    } // operator >

    /// <summary></summary>
    /// <param name="table"></param>
    /// <param name="arg"></param>
    /// <returns></returns>
    public static object operator <=(LuaTable table, object arg)
    {
      return table.OnLessEqual(arg);
    } // operator <=

    /// <summary></summary>
    /// <param name="table"></param>
    /// <param name="arg"></param>
    /// <returns></returns>
    public static object operator >=(LuaTable table, object arg)
    {
      return !table.OnLessEqual(arg);
    } // operator >=

    /// <summary></summary>
    /// <param name="table"></param>
    /// <param name="arg"></param>
    /// <returns></returns>
    public static object operator >>(LuaTable table, int arg)
    {
      return table.OnShr(arg);
    } // operator >>

    /// <summary></summary>
    /// <param name="table"></param>
    /// <param name="arg"></param>
    /// <returns></returns>
    public static object operator <<(LuaTable table, int arg)
    {
      return table.OnShl(arg);
    } // operator <<

    /// <summary></summary>
    /// <param name="table"></param>
    /// <param name="arg"></param>
    /// <returns></returns>
    public static object operator &(LuaTable table, object arg)
    {
      return table.OnBAnd(arg);
    } // operator &

    /// <summary></summary>
    /// <param name="table"></param>
    /// <param name="arg"></param>
    /// <returns></returns>
    public static object operator |(LuaTable table, object arg)
    {
      return table.OnBOr(arg);
    } // operator |

    /// <summary></summary>
    /// <param name="table"></param>
    /// <param name="arg"></param>
    /// <returns></returns>
    public static object operator ^(LuaTable table, object arg)
    {
      return table.OnBXor(arg);
    } // operator ^

    /// <summary></summary>
    /// <param name="table"></param>
    /// <returns></returns>
    public static object operator ~(LuaTable table)
    {
      return table.OnBNot();
    } // operator ~

    #endregion
  } // class LuaTable
}
