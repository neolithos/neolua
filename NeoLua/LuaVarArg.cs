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

namespace Neo.IronLua
{
	#region -- class LuaVarArg --------------------------------------------------------

	/// <summary>Variable arguments, one-based.</summary>
	public sealed class LuaVarArg : IDynamicMetaObjectProvider, ILuaValues, System.Collections.IEnumerable, System.Collections.ICollection
	{
		#region -- class LuaVarArgMetaObject ------------------------------------------

		private class LuaVarArgMetaObject : DynamicMetaObject
		{
			public LuaVarArgMetaObject(Expression expression, object value)
				: base(expression, BindingRestrictions.Empty, value)
			{
			} // ctor

			private Expression GetValuesExpression()
				=> Expression.Property(Expression.Convert(Expression, typeof(LuaVarArg)), Lua.ResultValuesPropertyInfo);

			private Expression GetFirstResultExpression()
				=> LuaEmit.GetResultExpression(Expression, 1);

			private DynamicMetaObject GetTargetDynamicCall(CallSiteBinder binder, Type typeReturn, Expression[] exprs)
			{
				return new DynamicMetaObject(
					DynamicExpression.Dynamic(binder, typeReturn, exprs),
					Restrictions.Merge(BindingRestrictions.GetTypeRestriction(Expression, typeof(LuaVarArg)))
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
				var restrictions = Restrictions.Merge(BindingRestrictions.GetTypeRestriction(Expression, typeof(LuaVarArg)));
				var v = (LuaVarArg)Value;
				if (binder.Type == typeof(LuaVarArg))
					return new DynamicMetaObject(Expression.Convert(this.Expression, binder.ReturnType), restrictions, Value);
				else if (binder.Type == typeof(object[]))
					return new DynamicMetaObject(GetValuesExpression(), restrictions, v.args);
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
		} // class LuaVarArgMetaObject

		#endregion

		private readonly object[] args;

		#region -- Ctor/Dtor ----------------------------------------------------------

		/// <summary></summary>
		public LuaVarArg()
		{
			args = LuaResult.Empty.Values;
		} // ctor

		/// <summary></summary>
		/// <param name="args"></param>
		public LuaVarArg(params object[] args)
		{
			this.args = args == null || args.Length == 0 ? emptyArray : args;
		} // ctor

		DynamicMetaObject IDynamicMetaObjectProvider.GetMetaObject(Expression parameter)
			=> new LuaVarArgMetaObject(parameter, this);

		#endregion

		#region -- IEnumerable --------------------------------------------------------

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
			=> Values.GetEnumerator();

		#endregion

		#region -- ICollection --------------------------------------------------------

		void System.Collections.ICollection.CopyTo(Array array, int index)
			=> args.CopyTo(array, index);

		bool System.Collections.ICollection.IsSynchronized => false;
		object System.Collections.ICollection.SyncRoot => null;

		#endregion

		/// <summary></summary>
		/// <param name="index"></param>
		/// <returns></returns>
		public object this[int index] => index >= 1 && index <= args.Length ? args[index - 1] : null;
		/// <summary>All values</summary>
		public object[] Values => args;
		/// <summary>First value.</summary>
		public object Value => args.Length > 0 ? args[0] : null;
		/// <summary>Number of values.</summary>
		public int Count => args.Length;

		// -- Static --------------------------------------------------------------

		/// <summary></summary>
		/// <param name="r"></param>
		/// <returns></returns>
		public static implicit operator object[](LuaVarArg r)
			=> r == null ? emptyArray : r.args;

		private static object[] emptyArray = LuaResult.Empty.Values;
		/// <summary>Empty</summary>
		public static LuaVarArg Empty { get; } = new LuaVarArg();
	} // class LuaVarArg

	#endregion
}
