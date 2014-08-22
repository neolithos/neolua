using System;
using System.Collections;
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
      /// <summary>Get the expression for member access.</summary>
      MemberInvoke = 1,
      /// <summary>Member name is not case sensitive.</summary>
      IgnoreCase = 2
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
          GetLuaTableRestriction()
        );
      } // func UnaryOperationCall

      private BindingRestrictions GetBinaryRestrictions(DynamicMetaObject arg)
      {
        return GetLuaTableRestriction().Merge(Lua.GetSimpleRestriction(arg));
      } // func GetBinaryRestrictions

      private BindingRestrictions GetLuaTableRestriction()
      {
        return BindingRestrictions.GetExpressionRestriction(Expression.TypeIs(Expression, typeof(LuaTable)));
      } // func GetLuaTableRestriction

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
          case ExpressionType.Power:
            return BindBinaryCall(binder, Lua.TablePowMethodInfo, arg);
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
            return new DynamicMetaObject(Lua.EnsureType(Expression.Not(BinaryOperationCall(binder, Lua.TableLessEqualMethodInfo, arg)), binder.ReturnType), GetBinaryRestrictions(arg));
          case ExpressionType.GreaterThanOrEqual:
            return new DynamicMetaObject(Lua.EnsureType(Expression.Not(BinaryOperationCall(binder, Lua.TableLessThanMethodInfo, arg)), binder.ReturnType), GetBinaryRestrictions(arg));
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
          GetLuaTableRestriction().Merge(Lua.GetMethodSignatureRestriction(null, args))
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
              Expression.Call(
                Lua.EnsureType(Expression, typeof(LuaTable)),
                Lua.TableSetValueIdxMethodInfo,
                Lua.EnsureType(indexes[0].Expression, typeof(object)),
                exprSet,
                Expression.Constant(false)
              ),
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
        return ((LuaTable)Value).GetMemberAccess(binder, Expression, binder.Name, binder.IgnoreCase ? MemberAccessFlag.IgnoreCase : MemberAccessFlag.None);
      } // func BindGetMember

      public override DynamicMetaObject BindSetMember(SetMemberBinder binder, DynamicMetaObject value)
      {
        if (!value.HasValue)
          return binder.Defer(value);

        LuaTable val = (LuaTable)Value;
        int iIndex = val.GetValueIndex(binder.Name, binder.IgnoreCase, false);
        if (iIndex == -2)
        {
          return new DynamicMetaObject(
            Expression.Assign(
              Expression.Property(
                Lua.EnsureType(Expression, typeof(LuaTable)),
                Lua.TableMetaTablePropertyInfo
              ),
              LuaEmit.Convert(Lua.GetRuntime(binder), value.Expression, value.LimitType, typeof(LuaTable), false)
            ),
            GetLuaTableRestriction().Merge(Lua.GetSimpleRestriction(value))
          );
        }
        else
        {
          Expression exprValue = LuaEmit.Convert(Lua.GetRuntime(binder), value.Expression, value.LimitType, typeof(object), false);
          if (iIndex == -1) // new index
          {
            Expression expr = Expression.Condition(
              val.TableChangedExpression(),
              Expression.Block(
                Expression.Call(Lua.EnsureType(Expression, typeof(LuaTable)), Lua.TableNewIndexMethodInfo,
                  Expression.Constant(binder.Name, typeof(object)),
                  Expression.Constant(binder.IgnoreCase),
                  exprValue,
                  Expression.Constant(false)
                ),
                exprValue
              ),
              binder.GetUpdateExpression(typeof(object))
            );
            return new DynamicMetaObject(expr,
              GetLuaTableRestriction().Merge(Lua.GetSimpleRestriction(value))
            );
          }
          else
          {
            ParameterExpression tmp = Expression.Variable(typeof(object), "#tmp");
            return new DynamicMetaObject(
              Expression.Block(new ParameterExpression[] { tmp },
                Expression.Assign(tmp, exprValue),
                Expression.IfThen(Expression.NotEqual(tmp, val.GetIndexAccess(iIndex)),
                  Expression.Block(
                    Expression.Assign(val.GetIndexAccess(iIndex), tmp),
                    Expression.Call(Lua.EnsureType(Expression, typeof(LuaTable)), Lua.TableOnPropertyChangedMethodInfo, Expression.Constant(binder.Name, typeof(string)))
                  )
                ),
                tmp
              ), BindingRestrictions.GetInstanceRestriction(Expression, Value).Merge(Lua.GetSimpleRestriction(value))
            );
          }
        }
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

      #region -- BindConvert ----------------------------------------------------------

			public override DynamicMetaObject BindConvert(ConvertBinder binder)
			{
				// Automatic convert to a special type, only for classes and structure
				if (Type.GetTypeCode(binder.Type) == TypeCode.Object && !binder.Type.IsAssignableFrom(Value.GetType()))
				{
					return new DynamicMetaObject(
						Lua.EnsureType(
							Expression.Call(Lua.EnsureType(Expression, typeof(LuaTable)), Lua.TableSetObjectMember, Lua.EnsureType(Expression.New(binder.Type), typeof(object))),
							binder.ReturnType),
						GetLuaTableRestriction());
				}
				return base.BindConvert(binder);
			} // func BindConvert

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
      {
        bool r;
        if (TryInvokeMetaTableOperator<bool>("__eq", false, out r, this, obj))
          return r;
        return false;
      }
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
      int iIndex = GetValueIndex(memberName, (flags & MemberAccessFlag.IgnoreCase) != 0, false);

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
          Expression.Call(Lua.EnsureType(exprTable, typeof(LuaTable)), Lua.TableIndexMethodInfo, Expression.Constant(memberName)),
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

    #region -- SetObjectMember --------------------------------------------------------

    /// <summary>Todo: performs not so well</summary>
    /// <param name="obj"></param>
    public object SetObjectMember(object obj)
    {
      if (obj == null)
        return obj;

      Type type = obj.GetType();
      foreach (var c in names)
      {
        string sMemberName = c.Key as string;
        if (!String.IsNullOrEmpty(sMemberName))
        {
          object val = values[c.Value];
          if (val != null)
          {
            FieldInfo fi = type.GetField(sMemberName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.SetField);
            if (fi != null)
              fi.SetValue(obj, Lua.RtConvertValue(val, fi.FieldType));
            else
            {
              PropertyInfo pi = type.GetProperty(sMemberName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.SetProperty);
              if (pi != null)
                pi.SetValue(obj, Lua.RtConvertValue(val, pi.PropertyType), null);
            }
          }
        }
      }

      return obj;
    } // proc SetObjectMember

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

		internal object GetValue(object item)
    {
      return GetValue(item, false);
    } // func GetValue

    internal object GetValue(object item, bool lRawGet)
    {
      // Search the name in the hash-table
      int iIndex = GetValueIndex(item, false, false);
      if (iIndex >= 0)
        return values[iIndex];
      else if (iIndex == -2)
        return metaTable;
      else if (lRawGet)
        return null;
      else
        return OnIndex(item);
    } // func GetRawValue

    internal object GetValue(object[] items)
    {
      return GetValue(this, items, 0);
    } // func GetValue

    private object GetValue(LuaTable p, object[] items, int iIndex)
    {
      object o = GetValue(items[iIndex]);
      if (iIndex == items.Length - 1)
        return o == null ? OnIndex(items[iIndex]) : o;
      else
      {
        LuaTable t = o as LuaTable;
        if (t == null)
          return p.OnIndex(items[iIndex]);
        else
          return t.GetValue(t, items, iIndex++);
      }
    } // func GetValue

    internal void SetValue(object item, object value, bool lMarkAsMethod)
    {
      // Get the Index for the value, if the value is null then do not create a new value
      int iIndex = GetValueIndex(item, false, false);

      if (iIndex == -2)
        metaTable = value as LuaTable;
      else if (iIndex == -1)
      {
        OnNewIndex(item, false, value, lMarkAsMethod);
        NotifyValueChanged(item);
      }
      else // Set the value, if there is a index
      {
        if (SetIndexValue(iIndex, value, lMarkAsMethod))
          NotifyValueChanged(item);
      }
    } // proc SetValue

    internal void SetRawValue(object item, object value)
    {
      int iIndex = GetValueIndex(item, false, true);
      if (SetIndexValue(iIndex, value, false))
        NotifyValueChanged(item);
    } // func SetRawValue

    private void NotifyValueChanged(object item)
    {
      // Notify property changed
      string sPropertyName = item as string;
      if (sPropertyName != null)
        OnPropertyChanged(sPropertyName);
    } // proc NotifyValueChanged

    internal void SetValue(object[] items, object value)
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

    private bool TryInvokeMetaTableOperator<TRETURN>(string sKey, bool lRaise, out TRETURN r, params object[] args)
    {
      if (metaTable != null)
      {
        object o = metaTable[sKey];
        if (o != null)
        {
          Delegate dlg = o as Delegate;
          if (dlg != null)
          {
            r = (TRETURN)Lua.RtConvertValue(Lua.RtInvoke(dlg, args), typeof(TRETURN));
            return true;
          }
          if (lRaise)
            throw new LuaRuntimeException(String.Format(Properties.Resources.rsTableOperatorIncompatible, sKey, "function"), 0, true);
        }
      }
      if (lRaise)
        throw new LuaRuntimeException(String.Format(Properties.Resources.rsTableOperatorNotFound, sKey), 0, true);

      r = default(TRETURN);
      return false;
    } // func GetMetaTableOperator

    private object UnaryOperation(string sKey)
    {
      object o;
      TryInvokeMetaTableOperator<object>(sKey, true, out o, this);
      return o;
    } // proc UnaryOperation

    private object BinaryOperation(string sKey, object arg)
    {
      object o;
      TryInvokeMetaTableOperator<object>(sKey, true, out o, this, arg);
      return o;
    } // proc BinaryOperation

    private bool BinaryBoolOperation(string sKey, object arg)
    {
      bool o;
      TryInvokeMetaTableOperator<bool>(sKey, true, out o, this, arg);
      return o;
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

    internal object InternConcat(object arg)
    {
      return OnConcat(arg);
    } // func InternConcat

    /// <summary></summary>
    /// <param name="arg"></param>
    /// <returns></returns>
    protected virtual object OnConcat(object arg)
    {
      return BinaryOperation("__concat", arg);
    } // func OnShr

    internal int InternLen()
    {
      return OnLen();
    } // func InternLen

    /// <summary></summary>
    /// <returns></returns>
    protected virtual int OnLen()
    {
      int iLen;
      if (TryInvokeMetaTableOperator<int>("__len", false, out iLen, this))
        return iLen;
      return Length;
    } // func OnLen

    /// <summary></summary>
    /// <param name="arg"></param>
    /// <returns></returns>
    protected virtual bool OnEqual(object arg)
    {
      return Equals(arg);
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
      if (metaTable == null)
        return null;

      object index = metaTable["__index"];
      LuaTable t;
      Delegate dlg;

      if ((t = index as LuaTable) != null) // default table
        return t.GetValue(key);
      else if ((dlg = index as Delegate) != null) // default function
        return Lua.RtInvoke(dlg, this, key);
      else
        return null;
    } // func OnIndex

    /// <summary></summary>
    /// <param name="key"></param>
    /// <param name="lIgnoreCase"></param>
    /// <param name="value"></param>
    /// <param name="lMarkAsMethod"></param>
    /// <returns></returns>
    protected virtual bool OnNewIndex(object key, bool lIgnoreCase, object value, bool lMarkAsMethod)
    {
      if (metaTable != null)
      {
        Delegate dlg = metaTable["__newindex"] as Delegate;
        if (dlg != null)
        {
          Lua.RtInvoke(dlg, this, key, value);
          return true;
        }
      }
      SetIndexValue(GetValueIndex(key, lIgnoreCase, true), value, lMarkAsMethod);
      return false;
    } // func OnIndex

    /// <summary></summary>
    /// <param name="args"></param>
    /// <returns></returns>
    protected virtual object OnCall(object[] args)
    {
      if (args == null || args.Length == 0)
      {
        object o;
        TryInvokeMetaTableOperator<object>("__call", true, out o, this);
        return o;
      }
      else
      {
        object[] argsEnlarged = new object[args.Length + 1];
        argsEnlarged[0] = this;
        Array.Copy(args, 0, argsEnlarged, 1, args.Length);
        object o;
        TryInvokeMetaTableOperator<object>("__call", false,out o, argsEnlarged);
        return o;
      }
    } // func OnCall

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

		private int CalcLength()
		{
			int[] indexes = new int[names.Count + 1];
			
			// Create a Index with the values
			foreach (var c in names)
				if (c.Key is int)
				{
					int i = (int)c.Key;
					if (i >= 1 && i < indexes.Length)
						indexes[i] = values[c.Value] == null ? 0 : 1;
				}

			// Find the highest
			int iLength = 1;
			while (iLength < indexes.Length && indexes[iLength] != 0)
				iLength++;
			return iLength - 1;
		} // proc CalcLength

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
    public int Length { get { return CalcLength(); } }
    /// <summary>Access to the __metatable</summary>
    public LuaTable MetaTable { get { return metaTable; } set { metaTable = value; } }

    #region -- Table Manipulation -----------------------------------------------------

		#region -- concat --

		/// <summary></summary>
		/// <param name="t"></param>
		/// <returns></returns>
		public static string concat(LuaTable t)
		{
			return concat(t, String.Empty, 1, t.Length);
		} // func concat

		/// <summary></summary>
		/// <param name="t"></param>
		/// <param name="sep"></param>
		/// <returns></returns>
		public static string concat(LuaTable t, string sep)
		{
			return concat(t, sep, 1, t.Length);
		} // func concat

		/// <summary></summary>
    /// <param name="t"></param>
    /// <param name="sep"></param>
    /// <param name="i"></param>
    /// <returns></returns>
		public static string concat(LuaTable t, string sep, int i)
    {
			return concat(t, sep, i, t.Length);
		} // func concat

		/// <summary></summary>
    /// <param name="t"></param>
    /// <param name="sep"></param>
    /// <param name="i"></param>
    /// <param name="j"></param>
    /// <returns></returns>
		public static string concat(LuaTable t, string sep, int i, int j)
		{
			if (i > j)
				return String.Empty;

			sep = sep == null ? String.Empty : sep;

			if (i >= 1 && j <= t.Length) // within the array
			{
				int[] map = mapArray(t, i, j);
				string[] list = new string[map.Length];

				// convert the values
				int iLength = list.Length;
				for (int k = 0; k < iLength; k++)
					list[k] = (string)Lua.RtConvertValue(t.values[map[k]], typeof(string));

				// call join
				return String.Join(sep, list);
			}
			else
			{
				List<string> list = new List<string>(Math.Max(Math.Min(j - i + 1, t.names.Count), 1));

				foreach (var c in t)
				{
					if (c.Key is int)
					{
						int k = (int)c.Key;
						if (k >= i && k <= j)
							list.Add((string)Lua.RtConvertValue(c.Value, typeof(string)));
					}
				}

				return String.Join(sep, list);
			}
		} // func concat

		#endregion

		#region -- insert --

		/// <summary></summary>
    /// <param name="t"></param>
    /// <param name="value"></param>
		public static void insert(LuaTable t, object value)
		{
			// the pos is optional
			insert(t, t.Length <= 0 ? 1 : t.Length + 1, value);
		} // proc insert

    /// <summary></summary>
    /// <param name="t"></param>
    /// <param name="pos"></param>
    /// <param name="value"></param>
    public static void insert(LuaTable t, object pos, object value)
    {
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

		#endregion

		#region -- pack --

		/// <summary>Returns a new table with all parameters stored into keys 1, 2, etc. and with a field &quot;n&quot; 
		/// with the total number of parameters. Note that the resulting table may not be a sequence.</summary>
    /// <param name="values"></param>
    /// <returns></returns>
    public static LuaTable pack(object[] values)
    {
      LuaTable t = new LuaTable();

			// copy the values in the table
			t.values.AddRange(values);

			// create the indexes
			int iLength = values.Length;
			for (int i = 0; i < iLength; i++)
				t.names.Add(i + 1, i);

			// set the element count
			t["n"] = values.Length;

      return t;
    } // func pack

		#endregion

		#region -- remove --

		/// <summary>Removes from list the last element.</summary>
		/// <param name="t"></param>
		public static object remove(LuaTable t)
		{
			return remove(t, t.Length);
		} // proc remove

		/// <summary>Removes from list the element at position pos, returning the value of the removed element.</summary>
    /// <param name="t"></param>
    /// <param name="pos"></param>
		public static object remove(LuaTable t, int pos)
    {
			object r;
			int iLength = t.Length;
			if (pos >= 1 && pos <= iLength) // remove the element and shift the follower
			{
				int[] map = mapArray(t, pos, iLength);

				// Copy the values
				r = t.values[map[0]];
				for (int i = 0; i < map.Length - 1; i++)
					t.values[map[i]] = t.values[map[i + 1]];

				t.values[map[map.Length - 1]] = null;
			}
			else // just remove the element
			{
				r = t[pos];
				t[pos] = null;
			}
			return r;
    } // proc remove

		#endregion

		#region -- sort --

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private sealed class SortTable : IComparer<object>
		{
			private LuaTable t;
			private Delegate compare;
			private int[] map;
			private object[] values;

			public SortTable(LuaTable t, Delegate compare)
			{
				this.t = t;
				this.compare = compare;

				// Create the indexes
				this.map = mapArray(t, 1, t.Length);
				this.values = new object[map.Length];

				// Copy the array
				int iLength = map.Length;
				for (int i = 0; i < iLength; i++)
					values[i] = t.values[map[i]];
			} // ctor

			public int Compare(object x, object y)
			{
				if (compare == null)
					return Comparer.Default.Compare(x, y);
				else
				{
					// Call the comparer
					object r = Lua.RtInvoke(compare, x, y);
					if (r is LuaResult)
						r = ((LuaResult)r)[0];

					// check the value
					if (r is int)
						return (int)r;
					else if ((bool)Lua.RtConvertValue(r, typeof(bool)))
						return -1;
					else if (Comparer.Default.Compare(x, y) == 0)
						return 0;
					else
						return 1;
				}
			} // func Compare

			public void Sort()
			{
				// sort the map
				Array.Sort(values, this);

				// exchange the values
				int iLength = map.Length;
				for (int i = 0; i < iLength; i++)
					t.values[map[i]] = values[i];
			} // proc Sort
		} // class SortTable


    /// <summary></summary>
    /// <param name="t"></param>
    /// <param name="sort"></param>
    public static void sort(LuaTable t, Delegate sort = null)
    {
			new SortTable(t, sort).Sort();
    } // proc sort

		#endregion

		#region -- unpack --

		/// <summary>Returns the elements from the given table.</summary>
		/// <param name="t"></param>
		/// <returns></returns>
		public static LuaResult unpack(LuaTable t)
		{
			return unpack(t, 1, t.Length);
		} // func unpack

		/// <summary>Returns the elements from the given table.</summary>
		/// <param name="t"></param>
		/// <param name="i"></param>
		/// <returns></returns>
		public static LuaResult unpack(LuaTable t, int i)
		{
			return unpack(t, i, t.Length);
		} // func unpack

		/// <summary>Returns the elements from the given table.</summary>
    /// <param name="t"></param>
    /// <param name="i"></param>
    /// <param name="j"></param>
    /// <returns></returns>
    public static LuaResult unpack(LuaTable t, int i, int j)
    {
			List<object> r = new List<object>(Math.Max(Math.Min(j - i + 1, t.names.Count), 1));

			foreach (var c in t)
			{
				if (c.Key is int)
				{
					int k = (int)c.Key;
					if (k >= i && k <= j)
						r.Add(c.Value);
				}
			}

			return new LuaResult(false, r.ToArray());
    } // func unpack

		#endregion

		#region -- toArray, mapArray --

		/// <summary>Extracts the array part of a table</summary>
		/// <param name="t"></param>
		/// <returns></returns>
		public static object[] toArray(LuaTable t)
		{
			return toArray(t, 1, t.Length);
		} // func toArray

		/// <summary>Extracts the array part of a table</summary>
		/// <param name="t"></param>
		/// <param name="i"></param>
		/// <returns></returns>
		public static object[] toArray(LuaTable t, int i)
		{
			return toArray(t, i, t.Length);
		} // func toArray

		/// <summary>Extracts the array part of a table</summary>
		/// <param name="t"></param>
		/// <param name="i"></param>
		/// <param name="j"></param>
		/// <returns></returns>
		public static object[] toArray(LuaTable t, int i, int j)
		{
			int[] map = mapArray(t, i, t.Length);
			object[] r = new object[map.Length];
			
			// copy the values
			for (int k = 0; k < map.Length; k++)
				r[k] = t.values[map[k]];

			return r;
		} // func toArray

		private static int[] mapArray(LuaTable t, int i, int j)
		{
			if (i < 1 || j > t.Length)
				throw new ArgumentOutOfRangeException();

		  int[]	map = new int[j - i + 1];

			if (map.Length < t.names.Count >> 1)
			{
				int l = 0;
				int tmp;
				for (int k = i; k <= j; k++)
					map[l++] = t.names.TryGetValue(k, out tmp) ? tmp : -1;
			}
			else
			{
#if DEBUG
				for (int k = 0; k < map.Length; k++)
					map[k] = -1;
#endif

				// Collect the direct indexes
				foreach (var c in t.names)
				{
					if (c.Key is int)
					{
						int iIndex = (int)c.Key - i;
						if (iIndex >= 0 && iIndex < map.Length)
							map[iIndex] = c.Value;
					}
				}

#if DEBUG
				if (Array.Exists(map, c => c == -1))
					throw new InvalidOperationException("map failed.");
#endif
			}
			return map;
		} // func mapArray

		#endregion

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
