using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Dynamic;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Text;

namespace Neo.IronLua
{
	#region -- class LuaResult ----------------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Dynamic result object for lua functions.</summary>
	public sealed class LuaResult : IDynamicMetaObjectProvider, IConvertible, System.Collections.IEnumerable, System.Collections.ICollection
	{
		#region -- enum CopyMode ----------------------------------------------------------

		internal enum CopyMode
		{
			None
		} // enum CopyMode

		#endregion

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
			if (v != null && v.GetType() == typeof(object[]))
				this.result = CopyResult((object[])v);
			else if (v is LuaResult)
				this.result = (LuaResult)v;
			else
				this.result = new object[] { v };
		} // ctor

		internal LuaResult(CopyMode copyMode, object[] values)
		{
			this.result = values;
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

		#region -- ICollection ------------------------------------------------------------

		void System.Collections.ICollection.CopyTo(Array array, int index)
		{
			result.CopyTo(array, index);
		} // func CopyTo

		bool System.Collections.ICollection.IsSynchronized { get { return false; } }
		object System.Collections.ICollection.SyncRoot { get { return this; } }

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
}
