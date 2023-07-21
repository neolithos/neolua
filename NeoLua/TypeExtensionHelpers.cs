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
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace Neo.IronLua
{
	internal static class TypeExtensionHelpers
	{
		public static bool HasImplicitConversionToType(this Type declaringType, Type targetType)
			=> declaringType.TryGetImplicitAssignmentOperatorToType(targetType, out _);

		public static bool HasImplicitConversionFromType(this Type declaringType, Type sourceType)
			=> declaringType.TryGetImplicitAssignmentOperatorFromType(sourceType, out _);

		public static bool CanConvertTo(this Type sourceType, Type targetType)
		{
			Debug.Assert(targetType != sourceType);
			return GetAllImplicitOperators(sourceType).Any(CanImplicitlyCastToTarget);

			bool CanImplicitlyCastToTarget(MethodInfo mi)
			{
				var parameterInfos = mi.GetParameters();
				Debug.Assert(parameterInfos[0].ParameterType == sourceType);
				var canConvertTo = CanAssign(mi.ReturnType, targetType);
				return canConvertTo;
			}
		}

		/// <summary>
		/// True if the source type can be converted to the target type.
		/// </summary>
		/// <param name="targetType"></param>
		/// <param name="sourceType"></param>
		/// <returns></returns>
		public static bool CanConvertFrom(this Type targetType, Type sourceType)
		{
			Debug.Assert(targetType != sourceType);
			return GetAllImplicitOperators(targetType).Any(CanImplicitlyConstructFromSource);

			bool CanImplicitlyConstructFromSource(MethodInfo mi)
			{
				var parameterInfos = mi.GetParameters();
				var parameterType = parameterInfos[0].ParameterType;
				var canConvertTo = CanAssign(sourceType, parameterType);
				return canConvertTo;
			}
		}

		public static bool CanAssignFromPrimitiveType(this Type targetType, Type sourceType)
		{
			return CanAssignValueType(sourceType, targetType);
		}

		private static bool CanAssign(Type from, Type to)
		{
			if (from == to) return true;
			if (from.IsAssignableFrom(to)) return true;
			if (from.IsValueType && to.IsValueType && CanAssignValueType(from, to)) return true;

			return false;
		}

		private static bool TryGetImplicitAssignmentOperatorToType(this Type declaringType, Type toType, out MethodInfo implicitOperator)
		{
			Debug.Assert(declaringType != toType);
			implicitOperator = declaringType.GetMethods(BindingFlags.Public | BindingFlags.Static)
				.FirstOrDefault(mi => mi.Name == "op_Implicit" && mi.ReturnType == toType);
			if (implicitOperator is not null)
			{
				Debug.Assert(implicitOperator.GetParameters()[0].ParameterType == declaringType);
				return true;
			}

			return false;
		}

		private static bool TryGetImplicitAssignmentOperatorFromType(this Type declaringType, Type fromType, out MethodInfo implicitOperator)
		{
			Debug.Assert(declaringType != fromType);
			// There can only be a single implicit operator from a type
			var op = GetImplicitOperator(declaringType, fromType);
			if (op is not null)
			{
				Debug.Assert(op.ReturnType == declaringType);
				implicitOperator = op;
				return true;
			}

			implicitOperator = null;
			return false;
		}

		private static MethodInfo GetImplicitOperator(Type declaringType, Type fromType)
			=> declaringType.GetMethod("op_Implicit", BindingFlags.Public | BindingFlags.Static, binder: null, new[] { fromType }, EmptyParameterModifier);

		private static IEnumerable<MethodInfo> GetAllImplicitOperators(Type declaringType)
			=> declaringType.GetMethods(BindingFlags.Public | BindingFlags.Static).Where(mi => mi.Name == "op_ImplicitO");

		private static ParameterModifier[] EmptyParameterModifier = new ParameterModifier[0];

		private static bool CanAssignValueType(Type argType, Type parameterType)
		{
			if (argType == typeof(Int16))
				return parameterType == typeof(Int16) || parameterType == typeof(Int32) || parameterType == typeof(Int64)
					   || parameterType == typeof(Single) || parameterType == typeof(Double);
			if (argType == typeof(Int32))
				return parameterType == typeof(Int32) || parameterType == typeof(Int64) || parameterType == typeof(Single) || parameterType == typeof(Double);
			if (argType == typeof(Int64))
				return parameterType == typeof(Int64) || parameterType == typeof(Double);

			if (argType == typeof(Single))
				return parameterType == typeof(Single) || parameterType == typeof(Double);
			if (argType == typeof(Double)) return parameterType == typeof(Double);
			return false;
		}

		public static bool IsParamArray(this ParameterInfo parameterInfo)
			=> parameterInfo.GetCustomAttribute(typeof(ParamArrayAttribute), false) is not null;
	}
}