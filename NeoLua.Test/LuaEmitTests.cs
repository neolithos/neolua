using System;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Reflection;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.IronLua;

namespace LuaDLR.Test
{
	[TestClass]
	public class LuaEmitTests : TestHelper
	{
		private class MockArgument
		{
			public MockArgument(Type type)
				=> Type = type;

			public MockArgument(string name, Type type)
			{
				Name = name;
				Type = type;
			} // ctor

			public string Name {  get; }
			public Type Type { get;  }

			public bool IsNamed => !String.IsNullOrEmpty(Name);
		} // class MockArgument

		private static Type GetArgumentType(MockArgument arg)
			=> arg.Type;

		private static MockArgument[] CreateCallInfo(params object[] types)
			=> types.Select(c => c is MockArgument a ? a : new MockArgument((Type)c)).ToArray();

		private static MockArgument Arg(string name, Type type)
			=> new MockArgument(name, type);

		private static Type[] CreateSignature(params Type[] types)
			=> types.ToArray();

		private void TestMethodInfoForArguments(Type type, string memberName, MockArgument[] arguments, Type[] argumentTypesForExpectedOverload)
		{
			var methods = memberName == "#ctor"
				? type.GetConstructors(BindingFlags.Instance | BindingFlags.Public)
				: type.GetMember(memberName, BindingFlags.Public | BindingFlags.InvokeMethod | BindingFlags.Static | BindingFlags.Instance);

			var callInfo = new CallInfo(arguments.Length, arguments.Where(c => c.IsNamed).Select(c => c.Name).ToArray());
			var methodInfo = LuaEmit.FindMember(methods, callInfo, arguments, GetArgumentType, false);

			Assert.IsNotNull(methodInfo, $"Found no valid overload for {type.Name}:{memberName}");
			var expected = memberName == "#ctor"
				? (MemberInfo)type.GetConstructor(argumentTypesForExpectedOverload)
				: type.GetMethod(memberName, argumentTypesForExpectedOverload);
			Assert.AreEqual(expected, methodInfo);
		} // proc TestMethodInfoForArguments

		[TestMethod]
		public void FindMemberJoinWithLuaResultMatchesObjectArrayOverload()
		{
			TestMethodInfoForArguments(typeof(string), nameof(string.Join),
				CreateCallInfo(typeof(string), typeof(LuaResult)),
				CreateSignature(typeof(string), typeof(object[]))
			);
		} // proc FindMemberJoinWithLuaResultMatchesObjectArrayOverload

		[TestMethod]
		public void FindMemberStringFormat01()
		{
			TestMethodInfoForArguments(typeof(string), nameof(string.Format),
				CreateCallInfo(typeof(string), typeof(string)),
				CreateSignature(typeof(string), typeof(object))
			);
		} // proc FindMemberStringFormat01

		[TestMethod]
		public void FindMemberStringFormat02()
		{
			TestMethodInfoForArguments(typeof(string), nameof(string.Format),
				CreateCallInfo(typeof(string), typeof(string), typeof(string), typeof(string), typeof(string), typeof(string), typeof(string), typeof(string), typeof(string), typeof(string), typeof(string)),
				CreateSignature(typeof(string), typeof(object[]))
			);
		} // proc FindMemberStringFormat02

		[TestMethod]
		public void FindMemberStringFormat03()
		{
			TestMethodInfoForArguments(typeof(string), nameof(string.Format),
				CreateCallInfo(typeof(string), typeof(object[])),
				CreateSignature(typeof(string), typeof(object[]))
			);
		} // proc FindMemberStringFormat03

		[TestMethod]
		public void FindMemberStringFormat04()
		{
			TestMethodInfoForArguments(typeof(string), nameof(string.Format),
				CreateCallInfo(typeof(string), typeof(string), typeof(string), typeof(string)),
				CreateSignature(typeof(string), typeof(object), typeof(object), typeof(object))
			);
		} // proc FindMemberStringFormat04

		[TestMethod]
		public void FindMemberStringConcat01()
		{
			TestMethodInfoForArguments(typeof(string), nameof(string.Concat),
				CreateCallInfo(typeof(string), typeof(string), typeof(LuaResult)),
				CreateSignature(typeof(object[]))
			);
		} // proc FindMemberStringConcat01

		[TestMethod]
		public void FindMemberLuaResultGSub()
		{
			TestMethodInfoForArguments(typeof(LuaLibraryString), nameof(LuaLibraryString.gsub),
				CreateCallInfo(typeof(string), typeof(string), typeof(string)),
				CreateSignature(typeof(string), typeof(string), typeof(object), typeof(int))
			);
		} // proc FindMemberLuaResultGSub

		[TestMethod]
		public void FindMemberParamArrayWithNoArg()
		{
			TestMethodInfoForArguments(typeof(LuaFile), nameof(LuaFile.write),
				CreateCallInfo(),
				CreateSignature(typeof(object[]))
			);
		} // proc FindMemberParamArrayWithNoArg

		[TestMethod]
		public void FindMemberParamArrayWithSingleArg()
		{
			TestMethodInfoForArguments(typeof(LuaFile), nameof(LuaFile.write),
				CreateCallInfo(typeof(string)),
				CreateSignature(typeof(object[]))
			);
		} // proc FindMemberParamArrayWithSingleArg

		[TestMethod]
		public void FindMemberParamArrayWithMultipleArgs()
		{
			TestMethodInfoForArguments(typeof(LuaFile), nameof(LuaFile.write),
				CreateCallInfo(typeof(string), typeof(string), typeof(string)),
				CreateSignature(typeof(object[]))
			);
		} // func FindMemberParamArrayWithMultipleArgs

		[TestMethod]
		public void FindMemberVersionNamed()
		{
			TestMethodInfoForArguments(typeof(Version), "#ctor",
				CreateCallInfo(Arg("major", typeof(int)), Arg("minor", typeof(int))),
				CreateSignature(typeof(int), typeof(int))
			);
		}

		[TestMethod]
		public void FindMemberVersionMixed()
		{
			TestMethodInfoForArguments(typeof(Version), "#ctor",
				CreateCallInfo(typeof(int), Arg("minor", typeof(int))),
				CreateSignature(typeof(int), typeof(int))
			);
		}

		[TestMethod]
		public void FindMemberSplit()
		{
			TestMethodInfoForArguments(typeof(String), nameof(String.Split),
				CreateCallInfo(typeof(string)),
				CreateSignature(typeof(char[]))
			);
		} // proc FindMemberSplit

		[TestMethod]
		public void FindMemberEnum()
		{
			TestMethodInfoForArguments(typeof(ComplexTestClass), nameof(ComplexTestClass.GetColor),
				CreateCallInfo(typeof(int)),
				CreateSignature(typeof(ConsoleColor))
			);
		} // proc FindMemberEnum

		[TestMethod]
		public void FindMemberIPEndPoint01()
		{
			// issue #117
			// >> uses Long because it comes first
			TestMethodInfoForArguments(typeof(IPEndPoint), "#ctor",
				CreateCallInfo(typeof(LuaResult), typeof(int)),
				CreateSignature(typeof(long), typeof(int))
			);
		} // proc FindMemberIPEndPoint01

		[TestMethod]
		public void FindMemberIPEndPoint02()
		{
			// issue #117
			// >> uses Long because it comes first
			TestMethodInfoForArguments(typeof(IPEndPoint), "#ctor",
				CreateCallInfo(typeof(object), typeof(int)),
				CreateSignature(typeof(long), typeof(int))
			);
		} // proc FindMemberIPEndPoint02

		[TestMethod]
		public void FindMemberIPEndPoint03()
		{
			// issue #117
			// >> uses Long because it comes first
			TestMethodInfoForArguments(typeof(IPEndPoint), "#ctor",
				CreateCallInfo(typeof(IPAddress), typeof(int)),
				CreateSignature(typeof(IPAddress), typeof(int))
			);
		} // proc FindMemberIPEndPoint03

		[TestMethod]
		public void FindMemberStringTrim01()
		{
			// issue #117
			// >> uses Long because it comes first
			TestMethodInfoForArguments(typeof(String), nameof(String.Trim),
				CreateCallInfo(typeof(string)),
				CreateSignature(typeof(char[]))
			);
		} // proc FindMemberStringTrim01

		[TestMethod]
		public void FindMemberWriteLine()
		{
			TestMethodInfoForArguments(typeof(Console), nameof(Console.WriteLine),
				CreateCallInfo(typeof(LuaResult)),
				CreateSignature(typeof(string), typeof(object[]))
			);
		} // proc FindMemberWriteLine

		[TestMethod]
		public void FindCtorXElement()
		{
			TestMethodInfoForArguments(typeof(XElement), "#ctor",
				CreateCallInfo(typeof(string), typeof(string)),
				CreateSignature(typeof(XName), typeof(object))
			);
		}

		[TestMethod]
		public void FindMemberStringCombine()
		{
			//TestMethodInfoForArguments(typeof(Path), nameof(Path.Combine),
			//	CreateCallInfo(typeof(string), typeof(object)),
			//	CreateSignature(typeof(string), typeof(string))
			//);

			TestCode(Lines(
				"const Path typeof System.IO.Path;",
				"const FileInfo typeof System.IO.FileInfo;",
				"local fi : FileInfo = FileInfo('c:\\\\b.txt');",
				"local t = { p = 'a.txt' };",
				"return Path:Combine(fi.DirectoryName, t.p);"
				), "c:\\a.txt"
			);
		}
	}
}