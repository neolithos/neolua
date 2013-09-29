using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using TecWare.Core.Stuff;

namespace Neo.IronLua
{
  ///////////////////////////////////////////////////////////////////////////////
  /// <summary></summary>
  public class LuaTable : IDynamicMetaObjectProvider, IEnumerable<KeyValuePair<object, object>>
  {
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
        return new DynamicMetaObject(
          Expression.Block(
            Expression.Call(
              Expression.Convert(Expression, typeof(LuaTable)),
              typeof(LuaTable).GetMethod("SetValue"),
              Expression.Convert(indexes[0].Expression, typeof(object)),
              Expression.Convert(value.Expression, typeof(object))
            ),
            Expression.Convert(value.Expression, typeof(object))
          ),
          BindingRestrictions.GetTypeRestriction(indexes[0].Expression, indexes[0].LimitType).Merge(BindingRestrictions.GetTypeRestriction(value.Expression, value.LimitType))
        );
      }

      public override DynamicMetaObject BindGetIndex(GetIndexBinder binder, DynamicMetaObject[] indexes)
      {
        return new DynamicMetaObject(
          Expression.Convert(Expression.Call(
            Expression.Convert(Expression, typeof(LuaTable)),
            typeof(LuaTable).GetMethod("GetValue"),
            Expression.Convert(indexes[0].Expression, typeof(object))
          ),
          typeof(object)),
          BindingRestrictions.GetTypeRestriction(indexes[0].Expression, indexes[0].LimitType)
        );
      }

      public override DynamicMetaObject BindGetMember(GetMemberBinder binder)
      {
        // Todo: Fallback Methode beachten
        return new DynamicMetaObject(
          Expression.Call(Expression.Convert(Expression, typeof(LuaTable)), typeof(LuaTable).GetMethod("GetValue"), Expression.Constant(binder.Name)),
          BindingRestrictions.GetTypeRestriction(Expression, Value.GetType()));
      }

      public override DynamicMetaObject BindSetMember(SetMemberBinder binder, DynamicMetaObject value)
      {
        return new DynamicMetaObject(
          Expression.Block(
            Expression.Call(Expression.Convert(Expression, typeof(LuaTable)), typeof(LuaTable).GetMethod("SetValue"), Expression.Constant(binder.Name, typeof(string)), Expression.Convert(value.Expression, typeof(object))),
            Expression.Convert(value.Expression, typeof(object))
          ),
          BindingRestrictions.GetTypeRestriction(Expression, Value.GetType())
        );
      }
    } // class LuaMetaObject
    
    private Dictionary<object, object> variables = null;      // Aktuelle Variablen des Scripts

    public virtual DynamicMetaObject GetMetaObject(Expression parameter)
    {
      return new LuaMetaObject(this, parameter);
    } // func GetMetaObject

    public virtual object GetValue(object item)
    {
      object v;
      if (variables != null && variables.TryGetValue(item, out v))
        return v;
      return null;
    } // func GetValue

    public void SetValue(object item, object value)
    {
      if (value == null) // Lösche den Wert
      {
        if (variables != null)
          variables.Remove(item);
      }
      else
      {
        if (variables == null)
          variables = new Dictionary<object, object>();
        variables[item] = value;
      }
    } // proc SetValue

    public IEnumerator<KeyValuePair<object, object>> GetEnumerator()
    {
      if (variables != null)
        foreach (var c in variables)
          yield return c;
    } // func IEnumerator<KeyValuePair<object, object>>

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
    {
      return GetEnumerator();
    } // func System.Collections.IEnumerable.GetEnumerator

    public object this[object item] { get { return GetValue(item); } set { SetValue(item, value); } }

    // public int Length {get;}
  } // class LuaTable
}
