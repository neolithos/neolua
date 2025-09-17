#region -- copyright --
//
// Licensed to the Apache Software Foundation (ASF) under one
// or more contributor license agreements.  See the NOTICE file
// distributed with this work for additional information
// regarding copyright ownership.  The ASF licenses this file
// to you under the Apache License, Version 2.0 (the
// "License"); you may not use this file except in compliance
// with the License.  You may obtain a copy of the License at
// 
//   http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing,
// software distributed under the License is distributed on an
// "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY
// KIND, either express or implied.  See the License for the
// specific language governing permissions and limitations
// under the License.
//
#endregion
using System;
using System.Dynamic;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Text;

namespace Neo.IronLua
{
	#region -- class LuaResult --------------------------------------------------------

	/// <summary>Dynamic result object for lua functions.</summary>
	public sealed class LuaResult : ILuaValues, IDynamicMetaObjectProvider, System.Collections.IEnumerable, System.Collections.ICollection, IConvertible
	{
		#region -- enum CopyMode ------------------------------------------------------

		internal enum CopyMode
		{
			None
		} // enum CopyMode

		#endregion

		#region -- class LuaResultMetaObject ------------------------------------------

		/// <summary>Redirect mainly all calls to a dynamic expression, because 
		/// e.g. CSharp binder doesn't look for the DynamicMetaObjectProvider interface and 
		/// LuaResult only stores Object's.</summary>
		private class LuaResultMetaObject : DynamicMetaObject
		{
			public LuaResultMetaObject(Expression expression, object value)
				: base(expression, BindingRestrictions.Empty, value)
			{
			} // ctor

			private Expression GetValuesExpression()
				=> Expression.Property(Expression.Convert(Expression, typeof(LuaResult)), Lua.ResultValuesPropertyInfo);

			private Expression GetFirstResultExpression()
				=> LuaEmit.GetResultExpression(Expression, 0);

			private DynamicMetaObject GetTargetDynamicCall(CallSiteBinder binder, Type typeReturn, Expression[] exprs)
			{
				return new DynamicMetaObject(
					DynamicExpression.Dynamic(binder, typeReturn, exprs),
					Restrictions.Merge(BindingRestrictions.GetTypeRestriction(Expression, typeof(LuaResult)))
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
					Lua.EnsureType(arg.Expression, arg.LimitType, typeof(object))
					}
				);
			} // func GetTargetDynamicCall

			private DynamicMetaObject GetTargetDynamicCall(CallSiteBinder binder, Type typeReturn, DynamicMetaObject[] args)
			{
				return GetTargetDynamicCall(binder, typeReturn,
					LuaEmit.CreateDynamicArgs(GetFirstResultExpression(), typeof(object), args, mo => mo.Expression, mo => mo.LimitType)
				);
			} // func GetTargetDynamicCall

			public override DynamicMetaObject BindConvert(ConvertBinder binder)
			{
				var restrictions = Restrictions.Merge(BindingRestrictions.GetTypeRestriction(Expression, typeof(LuaResult)));
				var v = (LuaResult)Value;
				if (binder.Type == typeof(LuaResult))
					return new DynamicMetaObject(Expression.Convert(this.Expression, binder.ReturnType), restrictions, Value);
				else if (binder.Type == typeof(object[]))
					return new DynamicMetaObject(GetValuesExpression(), restrictions, v.result);
				else
					return new DynamicMetaObject(DynamicExpression.Dynamic(binder, binder.Type, GetFirstResultExpression()), restrictions);
			} // func BindConvert

			public override DynamicMetaObject BindGetIndex(GetIndexBinder binder, DynamicMetaObject[] indexes)
				=> base.BindGetIndex(binder, indexes);

			public override DynamicMetaObject BindSetIndex(SetIndexBinder binder, DynamicMetaObject[] indexes, DynamicMetaObject value)
			{
				return GetTargetDynamicCall(binder, binder.ReturnType,
					LuaEmit.CreateDynamicArgs(GetFirstResultExpression(), typeof(object), indexes, value, mo => mo.Expression, mo => mo.LimitType)
				);
			} // func BindSetIndex

			public override DynamicMetaObject BindGetMember(GetMemberBinder binder)
				=> GetTargetDynamicCall(binder, binder.ReturnType);

			public override DynamicMetaObject BindSetMember(SetMemberBinder binder, DynamicMetaObject value)
				=> GetTargetDynamicCall(binder, binder.ReturnType, value);

			public override DynamicMetaObject BindInvoke(InvokeBinder binder, DynamicMetaObject[] args)
				=> GetTargetDynamicCall(binder, binder.ReturnType, args);

			public override DynamicMetaObject BindInvokeMember(InvokeMemberBinder binder, DynamicMetaObject[] args)
				=> GetTargetDynamicCall(binder, binder.ReturnType, args);

			public override DynamicMetaObject BindUnaryOperation(UnaryOperationBinder binder)
				=> GetTargetDynamicCall(binder, binder.ReturnType);

			public override DynamicMetaObject BindBinaryOperation(BinaryOperationBinder binder, DynamicMetaObject arg)
				=> GetTargetDynamicCall(binder, binder.ReturnType, arg);
		} // class LuaResultMetaObject

		#endregion

		private readonly object[] result;

		#region -- Ctor/Dtor/MO -------------------------------------------------------

		/// <summary>Creates a empty result-object.</summary>
		public LuaResult()
		{
			result = emptyArray;
		} // ctor

		/// <summary>Creates a empty result-object.</summary>
		/// <param name="v">One result value</param>
		public LuaResult(object v)
		{
			if (v != null && v.GetType() == typeof(object[]))
				result = CopyResult((object[])v);
			else if (v is LuaResult r)
				result = r.result;
			else if (v is ILuaValues lv)
				result = lv.Values;
			else
				result = new object[] { v };
		} // ctor

		internal LuaResult(CopyMode copyMode, object[] values)
		{
			if (copyMode != CopyMode.None)
				throw new ArgumentOutOfRangeException(nameof(copyMode));

			result = values;
		} // ctor

		/// <summary>Creates a empty result-object.</summary>
		/// <param name="values">Result values</param>
		public LuaResult(params object[] values)
			=> result = CopyResult(values);

		/// <summary></summary>
		/// <returns></returns>
		public override string ToString()
		{
			if (result == null)
				return "<Empty>";
			else
			{
				var sb = new StringBuilder();
				for (var i = 0; i < 10 && i < result.Length; i++)
				{
					if (i > 0)
						sb.Append(", ");
					sb.Append('{').Append(result[i]).Append('}');
				}
				return sb.ToString();
			}
		} // func ToString

		private static object GetObject(object v)
			=> v is ILuaValues lv ? lv.Value : v;

		private static object[] CopyResult(object[] values)
		{
			// are there values
			if (values == null || values.Length == 0)
				return emptyArray;
			else if (values.Length == 1 && values[0] is ILuaValues lv) // Only on element, that is a result no copy necessary
				return lv.Values;
			else if (values[values.Length - 1] is ILuaValues lv2) // is the last result an an result -> concat the arrays
			{
				var l = lv2.Values;
				var n = new object[values.Length - 1 + l.Length];

				// copy the first values
				for (var i = 0; i < values.Length - 1; i++)
					n[i] = GetObject(values[i]);

				// enlarge from the last result
				for (var i = 0; i < l.Length; i++)
					n[i + values.Length - 1] = GetObject(l[i]);

				return n;
			}
			else
			{
				var n = new object[values.Length];

				for (var i = 0; i < values.Length; i++)
					n[i] = GetObject(values[i]);

				return n;
			}
		} // func CopyResult

		DynamicMetaObject IDynamicMetaObjectProvider.GetMetaObject(Expression parameter)
			=> new LuaResultMetaObject(parameter, this);

		#endregion

		#region -- ToXXXX -------------------------------------------------------------

		/// <summary></summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="iIndex"></param>
		/// <param name="default"></param>
		/// <returns></returns>
		public T GetValueOrDefault<T>(int iIndex, T @default)
		{
			var v = this[iIndex];
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
			=> Convert.ToBoolean(this[0], provider);

		/// <summary></summary>
		/// <param name="provider"></param>
		/// <returns></returns>
		public byte ToByte(IFormatProvider provider = null)
			=> Convert.ToByte(this[0], provider);

		/// <summary></summary>
		/// <param name="provider"></param>
		/// <returns></returns>
		public char ToChar(IFormatProvider provider = null)
			=> Convert.ToChar(this[0], provider);

		/// <summary></summary>
		/// <param name="provider"></param>
		/// <returns></returns>
		public DateTime ToDateTime(IFormatProvider provider = null)
			=> Convert.ToDateTime(this[0], provider);

		/// <summary></summary>
		/// <param name="provider"></param>
		/// <returns></returns>
		public decimal ToDecimal(IFormatProvider provider = null)
			=> Convert.ToDecimal(this[0], provider);

		/// <summary></summary>
		/// <param name="provider"></param>
		/// <returns></returns>
		public double ToDouble(IFormatProvider provider = null)
			=> Convert.ToDouble(this[0], provider);

		/// <summary></summary>
		/// <param name="provider"></param>
		/// <returns></returns>
		public short ToInt16(IFormatProvider provider = null)
			=> Convert.ToInt16(this[0], provider);

		/// <summary></summary>
		/// <param name="provider"></param>
		/// <returns></returns>
		public int ToInt32(IFormatProvider provider = null)
			=> Convert.ToInt32(this[0], provider);

		/// <summary></summary>
		/// <param name="provider"></param>
		/// <returns></returns>
		public long ToInt64(IFormatProvider provider = null)
			=> Convert.ToInt64(this[0], provider);

		/// <summary></summary>
		/// <param name="provider"></param>
		/// <returns></returns>
		public sbyte ToSByte(IFormatProvider provider = null)
			=> Convert.ToSByte(this[0], provider);

		/// <summary></summary>
		/// <param name="provider"></param>
		/// <returns></returns>
		public float ToSingle(IFormatProvider provider = null)
			=> Convert.ToSingle(this[0], provider);

		/// <summary></summary>
		/// <param name="provider"></param>
		/// <returns></returns>
		public string ToString(IFormatProvider provider = null)
			=> Convert.ToString(this[0], provider);

		/// <summary></summary>
		/// <param name="conversionType"></param>
		/// <param name="provider"></param>
		/// <returns></returns>
		public object ToType(Type conversionType, IFormatProvider provider = null)
			=> Convert.ChangeType(this[0], conversionType, provider);

		/// <summary></summary>
		/// <param name="provider"></param>
		/// <returns></returns>
		public ushort ToUInt16(IFormatProvider provider = null)
			=> Convert.ToUInt16(this[0], provider);

		/// <summary></summary>
		/// <param name="provider"></param>
		/// <returns></returns>
		public uint ToUInt32(IFormatProvider provider = null)
			=> Convert.ToUInt32(this[0], provider);

		/// <summary></summary>
		/// <param name="provider"></param>
		/// <returns></returns>
		public ulong ToUInt64(IFormatProvider provider = null)
			=> Convert.ToUInt64(this[0], provider);

		TypeCode IConvertible.GetTypeCode()
			=> TypeCode.Object;

		#endregion

		#region -- IEnumerable --------------------------------------------------------

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
			=> Values.GetEnumerator();

		#endregion

		#region -- ICollection --------------------------------------------------------

		void System.Collections.ICollection.CopyTo(Array array, int index)
			=> result.CopyTo(array, index);

		bool System.Collections.ICollection.IsSynchronized => false;
		object System.Collections.ICollection.SyncRoot => null;

		#endregion

		/// <summary>Return values.</summary>
		/// <param name="index"></param>
		/// <returns></returns>
		public object this[int index] => index >= 0 && index < result.Length ? result[index] : null;
		/// <summary>Access to the raw-result values.</summary>
		public object[] Values => result;
		/// <summary>First value.</summary>
		public object Value => result.Length > 0 ? result[0] : null;
		/// <summary>Get's the number of results.</summary>
		public int Count => result.Length;

		// -- Static --------------------------------------------------------------

#if NET451
		private static readonly object[] emptyArray = new object[0];
#else
		private static readonly object[] emptyArray = Array.Empty<object>();
#endif

		/// <summary></summary>
		/// <param name="r"></param>
		/// <returns></returns>
		public static implicit operator object[] (LuaResult r)
			=> r == null ? emptyArray : r.result;

		/// <summary></summary>
		/// <param name="v"></param>
		/// <returns></returns>
		public static implicit operator LuaResult(object[] v)
			=> new LuaResult(v);

		/// <summary>Represents a empty result</summary>
		public static LuaResult Empty { get; } = new();
	} // struct LuaResult

#endregion
}
