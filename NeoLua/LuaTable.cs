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
  ///////////////////////////////////////////////////////////////////////////////
  /// <summary></summary>
  public class LuaTable : IDynamicMetaObjectProvider, INotifyPropertyChanged, IEnumerable<KeyValuePair<object, object>>
  {
    #region -- class LuaMetaObject ----------------------------------------------------

    ///////////////////////////////////////////////////////////////////////////////
    /// <summary></summary>
    protected class LuaMetaObject : DynamicMetaObject
    {
      public LuaMetaObject(LuaTable value, Expression expression)
        : base(expression, BindingRestrictions.Empty, value)
      {
      } // ctor

      public override DynamicMetaObject BindSetIndex(SetIndexBinder binder, DynamicMetaObject[] indexes, DynamicMetaObject value)
      {
        if (indexes.Length == 1)
        {
          // the index is normaly an expression --> call setvalue
          return new DynamicMetaObject(
            Expression.Block(
              Expression.Call(
                Expression.Convert(Expression, typeof(LuaTable)),
                typeof(LuaTable).GetMethod("SetValue", BindingFlags.Instance | BindingFlags.NonPublic, null, new Type[] { typeof(object), typeof(object) }, null),
                Expression.Convert(indexes[0].Expression, typeof(object)),
                value.Expression
              ),
              value.Expression
            ),
            BindingRestrictions.GetInstanceRestriction(Expression, Value));
        }
        else
        {
          Expression[] args = new Expression[indexes.Length];

          // Convert the indexes
          for (int i = 0; i < indexes.Length; i++)
            args[i] = Expression.Convert(indexes[i].Expression, typeof(object));

          return new DynamicMetaObject(
            Expression.Block(
              Expression.Call(
                Expression.Convert(Expression, typeof(LuaTable)),
                typeof(LuaTable).GetMethod("SetValue", BindingFlags.Instance | BindingFlags.NonPublic, null, new Type[] { typeof(object[]), typeof(object) }, null),
                Expression.NewArrayInit(typeof(object), args),
                value.Expression
              ),
              value.Expression
            ),
            BindingRestrictions.GetInstanceRestriction(Expression, Value));
        }
      } // func BindSetIndex

      public override DynamicMetaObject BindGetIndex(GetIndexBinder binder, DynamicMetaObject[] indexes)
      {
        if (indexes.Length == 1)
        {
          // the index is normaly an expression
          return new DynamicMetaObject(
            Expression.Call(
              Expression.Convert(Expression, typeof(LuaTable)),
              typeof(LuaTable).GetMethod("GetValue", BindingFlags.Instance | BindingFlags.NonPublic, null, new Type[] { typeof(object) }, null),
              Expression.Convert(indexes[0].Expression, typeof(object))
            ),
            BindingRestrictions.GetInstanceRestriction(Expression, Value));
        }
        else
        {
          Expression[] args = new Expression[indexes.Length];

          // Convert the indexes
          for (int i = 0; i < indexes.Length; i++)
            args[i] = Expression.Convert(indexes[i].Expression, typeof(object));

          return new DynamicMetaObject(
            Expression.Call(
              Expression.Convert(Expression, typeof(LuaTable)),
              typeof(LuaTable).GetMethod("GetValue", BindingFlags.Instance | BindingFlags.NonPublic, null, new Type[] { typeof(object[]) }, null),
              Expression.NewArrayInit(typeof(object), args)
            ),
            BindingRestrictions.GetInstanceRestriction(Expression, Value));
        }
      } // func BindGetIndex

      private DynamicMetaObject GetMemberAccess(DynamicMetaObjectBinder binder, object memberName, bool lIgnoreCase, bool lForWrite)
      {
        LuaTable t = (LuaTable)Value;

        // Get the index of the name
        int iIndex = t.GetValueIndex(memberName, lIgnoreCase, lForWrite);

        if (iIndex == -1) // Create an update rule
        {
          // no fallback, to hide the static typed interface
          // if the length of the value-Array changed, then rebind
          Expression expr = Expression.Condition(
            Expression.Equal(
              Expression.Property(Expression.Constant(t.values, typeof(List<object>)), typeof(List<object>), "Count"),
              Expression.Constant(t.values.Count, typeof(int))),
            Expression.Constant(null, typeof(object)),
            binder.GetUpdateExpression(typeof(object)));

          return new DynamicMetaObject(expr, BindingRestrictions.GetInstanceRestriction(Expression, Value));
        }
        else
        {
          PropertyInfo piItemIndex = typeof(List<object>).GetProperty("Item", BindingFlags.Public | BindingFlags.Instance);

          // IndexAccess expression
          Expression expr = Expression.MakeIndex(Expression.Constant(t.values, typeof(List<object>)), piItemIndex, new Expression[] { Expression.Constant(iIndex, typeof(int)) });

          // Create MO with restriction
          return new DynamicMetaObject(expr, BindingRestrictions.GetInstanceRestriction(Expression, Value));
        }
      } // func GetMemberAccess

      public override DynamicMetaObject BindGetMember(GetMemberBinder binder)
      {
        return GetMemberAccess(binder, binder.Name, binder.IgnoreCase, false);
      } // func BindGetMember

      public override DynamicMetaObject BindSetMember(SetMemberBinder binder, DynamicMetaObject value)
      {
        if (!value.HasValue)
          return binder.Defer(value);

        ParameterExpression tmp = Expression.Variable(typeof(object), "#tmp");
        DynamicMetaObject moGet = GetMemberAccess(binder, binder.Name, binder.IgnoreCase, true);
        return new DynamicMetaObject(
          Expression.Block(new ParameterExpression[] { tmp },
            Expression.Assign(tmp, Expression.Convert(value.Expression, typeof(object))),
            Expression.IfThen(Expression.NotEqual(tmp, moGet.Expression),
              Expression.Block(
                Expression.Assign(moGet.Expression, tmp),
                Expression.Call(Expression.Constant(Value, typeof(LuaTable)), typeof(LuaTable).GetMethod("OnPropertyChanged", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.InvokeMethod), Expression.Constant(binder.Name, typeof(string)))
              )
            ),
            tmp
          ), moGet.Restrictions);
      } // func BindSetMember

      public override DynamicMetaObject BindInvokeMember(InvokeMemberBinder binder, DynamicMetaObject[] args)
      { 
        // Member calls add a hidden parameter to the argument list
        DynamicMetaObject[] argsEnlarged ;
        if (args != null && args.Length > 0)
        {
          argsEnlarged = new DynamicMetaObject[args.Length + 1];
          Array.Copy(args, 0, argsEnlarged, 1, args.Length);
        }
        else
          argsEnlarged = new DynamicMetaObject[1];
        argsEnlarged[0] = new DynamicMetaObject(this.Expression, BindingRestrictions.Empty, Value);
        
        // We can only call delegates
        return binder.FallbackInvoke(GetMemberAccess(binder, binder.Name, binder.IgnoreCase, false), argsEnlarged, null);
      } // BindInvokeMember

      public override IEnumerable<string> GetDynamicMemberNames()
      {
        LuaTable t = (LuaTable)Value;
        foreach (var c in t.names.Keys)
          if (c is string)
            yield return (string)c;
      } // func GetDynamicMemberNames
    } // class LuaMetaObject

    #endregion

    /// <summary>Value has changed.</summary>
    public event PropertyChangedEventHandler PropertyChanged;

    private List<object> values = null;           // Array with values
    private Dictionary<object, int> names = null; // Names or Indices in the value-Array
    private int iLength = 0;

    #region -- Ctor/Dtor --------------------------------------------------------------

    /// <summary>Creates a new lua table</summary>
    public LuaTable()
    {
      this.values = new List<object>();
      this.names = new Dictionary<object, int>();
    } // ctor

    #endregion

    #region -- IDynamicMetaObjectProvider members -------------------------------------

    /// <summary>Returns the Meta-Object</summary>
    /// <param name="parameter"></param>
    /// <returns></returns>
    public virtual DynamicMetaObject GetMetaObject(Expression parameter)
    {
      return new LuaMetaObject(this, parameter);
    } // func GetMetaObject

    #endregion

    #region -- RegisterFunction, UnregisterFunction -----------------------------------

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

      // Lookup the name in the hash-table
      if (lIgnoreCase && item is string)
      {
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

    private bool SetIndexValue(int iIndex, object value)
    {
      object c = values[iIndex];
      if (!Object.Equals(c, value))
      {
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
      return iIndex >= 0 ? values[iIndex] : null;
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

    private void SetValue(object item, object value)
    {
      // Get the Index for the value, if the value is null then do not create a new value
      int iIndex = GetValueIndex(item, false, value != null);

      // Set the value, if there is a index
      if (iIndex != -1 && SetIndexValue(iIndex, value))
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
        SetValue(items[iIndex], value);
      }
      else
      {
        int i = GetValueIndex(items[iIndex], false, true);
        LuaTable t = values[i] as LuaTable;
        if (t == null)
        {
          t = new LuaTable();
          values[i] = t;
        }
        t.SetValue(items, iIndex++, values);
      }
    } // func SetValue

    #endregion

    #region -- IEnumerator members ----------------------------------------------------

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
    public object this[int iIndex] { get { return GetValue(iIndex); } set { SetValue(iIndex, value); } }
    /// <summary>Returns or sets an value in the lua-table.</summary>
    /// <param name="iIndex">Index.</param>
    /// <returns>Value or <c>null</c></returns>
    public object this[string sName] { get { return GetValue(sName); } set { SetValue(sName, value); } }
    /// <summary>Returns or sets an value in the lua-table.</summary>
    /// <param name="iIndex">Index.</param>
    /// <returns>Value or <c>null</c></returns>
    public object this[object item] { get { return GetValue(item); } set { SetValue(item, value); } }

    /// <summary>Length if it is an array.</summary>
    public int Length { get { return iLength; } }
  } // class LuaTable
}
