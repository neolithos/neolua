using System;
using System.Dynamic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.IronLua;

namespace LuaDLR.Test
{
	[TestClass]
	public class LuaEmitTests
	{
		private LuaType LuaTypeString = LuaType.GetType(typeof(string));

		[TestMethod]
		public void FindMember_StringJoin_WithLuaResultMatchesObjectArrayOverload()
		{
			var arguments = GetDynamicMetaObjectArguments(" ", new LuaResult("Hello", "World", "!!"));
			TestMethodInfoForArguments(typeof(string), nameof(string.Join), arguments, new[] { typeof(string), typeof(object[]) });
		}

		[TestMethod]
		public void FindMember_StringStringFormat_WithStringObj()
		{
			var arguments = GetDynamicMetaObjectArguments("Hello {0}", "World");
			TestMethodInfoForArguments(typeof(string), nameof(string.Format), arguments, new[] { typeof(string), typeof(object) });
		}

		[TestMethod]
		public void FindMember_StringStringFormat_WithStringObjArray()
		{
			var arguments = GetDynamicMetaObjectArguments("Hello {0}{1}", new object[] { "World", "!!" });
			TestMethodInfoForArguments(typeof(string), nameof(string.Format), arguments, new[] { typeof(string), typeof(object[]) });
		}

		[TestMethod]
		public void FindMember_StringConcat_WithStringsAndLuaResultMatchesObjectArrayOverload()
		{
			var arguments = GetDynamicMetaObjectArguments("Test", ":", new LuaResult("Hello", "World", "!!"));
			TestMethodInfoForArguments(typeof(string), nameof(string.Concat), arguments, new[] { typeof(object[]) });
		}

		[TestMethod]
		public void FindMember_LuaResultGSub()
		{
			var arguments = GetDynamicMetaObjectArguments("abc", "a", "b");
			TestMethodInfoForArguments(typeof(LuaLibraryString), nameof(LuaLibraryString.gsub), arguments, new[] { typeof(string) , typeof(string) , typeof(object), typeof(int) });
		}


		[TestMethod]
		public void FindMember_ParamArrayWithNoArg()
		{
			var arguments = GetDynamicMetaObjectArguments();
			TestMethodInfoForArguments(typeof(LuaFile), nameof(LuaFile.write), arguments, new[] { typeof(object[]) });
		}

		[TestMethod]
		public void FindMember_ParamArrayWithSingleArg()
		{
			var arguments = GetDynamicMetaObjectArguments("Hello World");
			TestMethodInfoForArguments(typeof(LuaFile), nameof(LuaFile.write), arguments, new[] { typeof(object[]) });
		}

		[TestMethod]
		public void FindMember_ParamArrayWithMultipleArgs()
		{
			var arguments = GetDynamicMetaObjectArguments("Hello World", "Some more", "and another");
			TestMethodInfoForArguments(typeof(LuaFile), nameof(LuaFile.write), arguments, new[] { typeof(object[]) });
		}

		void TestMethodInfoForArguments(Type type, string memberName, DynamicMetaObject[] arguments, Type[] argumentTypesForExpectedOverload)
		{
			var methodAlternatives = type.GetMember(memberName).Cast<MethodInfo>().ToArray();
			var callInfo = new CallInfo(arguments.Length);
			var methodInfo = LuaEmit.FindMember(methodAlternatives, callInfo, arguments, GetArgumentType, false);
			Assert.IsNotNull(methodInfo, $"Found no valid overload for {type.Name}:{memberName}");
			var expected = type.GetMethod(memberName, argumentTypesForExpectedOverload);
			Assert.AreEqual(expected, methodInfo);
		}

		static Type GetArgumentType(DynamicMetaObject obj) => obj.LimitType;

		DynamicMetaObject[] GetDynamicMetaObjectArguments(params object[] arguments)
		{
			DynamicMetaObject[] res = new DynamicMetaObject[arguments.Length];
			for (int i = 0; i < arguments.Length; i++)
			{
				object argument = arguments[i];
				var name = $"$arg{i + 1}";
				res[i] = CreateMetaObjectParameter(name, argument);
			}

			return res;
		}
		
		DynamicMetaObject CreateMetaObjectParameter(string name, object value)
		{
			var type = value is string ? LuaTypeString : value.GetType();
			return DynamicMetaObject.Create(value, Expression.Parameter(type, name));
		}
	}
}