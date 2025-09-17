﻿using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Threading.Tasks;
using ExtTest;
using Neo.IronLua;

namespace NeoTest1
{
	//public class Vector3
	//{
	//	public float X;
	//	public float Y;
	//	public float Z;

	//	public Vector3(float x, float y, float z)
	//	{
	//		X = x;
	//		Y = y;
	//		Z = z;
	//	}

	//	public override string ToString()
	//	{
	//		return string.Format("X = {0}, Y = {1}, Z = {2}", X, Y, Z);
	//	}
	//}


	public class Folder
	{
		public int SomeId { get; set; }
	}

	public class File
	{
		public Folder Folder { get; set; }
	}

	public class DynData : DynamicObject
	{
		public override bool TryGetIndex(GetIndexBinder binder, object[] indexes, out object result)
		{
			return base.TryGetIndex(binder, indexes, out result);
		}

		public override bool TryGetMember(GetMemberBinder binder, out object result)
		{
			if (binder.Name == "Part")
			{
				result = "Part returned";
				return true;
			}
			return base.TryGetMember(binder, out result);
		}
	}

	class Program
	{
		static async Task<int> TestAsync()
		{
			await Task.Yield();
			return 0;
		}

		#region -- Issue 81 --

		static bool NegBla(ref MyStruct my)
			=> !my.Bla;

		struct MyStruct
		{
			public bool Bla => true;
			public bool Awesome => NegBla(ref this);

		}
		static MyStruct getStruct()
			=> new MyStruct();
			
		public static void Example()
		{
			using (var l = new Lua())
			{
				dynamic g = l.CreateEnvironment();
				g.t = new LuaTable();
				((LuaTable)g.t).DefineFunction("getStruct", new Func<MyStruct>(getStruct));

				g.dochunk("local o = t.getStruct(); "+Environment.NewLine+"" +
					"print(o.Bla); print(o.Awesome); ", "test.lua");
			}
		}

		#endregion

		#region -- Issue 192 --

		public class ObjWrapper
		{
			public string Function { get; set; }
			public object[] Arguments { get; set; }

			public void yield(string function, params object[] args)
			{
				Function = function;
				Arguments = args;
			}
		}

		public static void NeoLua_Params()
		{
			var condition = 
				"function select(...)" + Environment.NewLine +
				"  return obj.yield('select', arg)" + Environment.NewLine +
				"end" + Environment.NewLine +
				"select('something', 'something2');";
			var lua = new Lua();
			var env = lua.CreateEnvironment();
			var wrapper = new ObjWrapper();
			env["obj"] = wrapper;
			env.DoChunk(condition, "code.lua");
			Console.WriteLine("{0} == select", wrapper.Function);
			Console.WriteLine("{0}, {1}, n={2} === something,something2,2", wrapper.Arguments[0], wrapper.Arguments[1], wrapper.Arguments.Length);
		}

		#endregion

		static void Main(string[] args)
		{
			NeoLua_Params();

			// Example();

			//Console.WriteLine(TestAsync().Result);

			//StartEx.Main1(args);
			//TestDynamic();

			//LinqTest2();
			Console.ReadKey();
		}

		private static void TestDynamic()
		{
			var lua = new Lua();
			var global = new LuaGlobal(lua) { ["workspace"] = new DynData() };

			var r = global.DoChunk("return workspace.Part", "Test.lua");

			Console.WriteLine(r.ToString());
		}

		private static void Test()
		{
			using (Lua l = new Lua())
			{
				var t = new LuaTable();
				var c = l.CompileChunk(
					String.Join(Environment.NewLine,
						"local v1 = 2;",
						"local v2 = 4;",
						"function f()",
						"  return v1 + v2;",
						"end;",
						"return f();"), "test", null);
				var r = c.Run(t);
				Console.WriteLine("Test: v1=[{0}], v2=[{1}], r={2}", Lua.RtGetUpValue(t.GetMemberValue("f") as Delegate, 1), Lua.RtGetUpValue(t.GetMemberValue("f") as Delegate, 2), r.ToInt32());
			}
		}

//		private static void LinqTest2()
//		{
//			LuaType.RegisterTypeExtension(typeof(Enumerable)); // generic geht nicht
//			List<int> lst = new List<int>();
//			lst.Add(1);
//			lst.Add(2);
//			lst.Add(3);
//			using (Lua l = new Lua())
//			{
//				var g = l.CreateEnvironment();

//				LuaResult r = g.DoChunk(String.Join(Environment.NewLine,
//					"return a.Select(function (c) return c; end).ToArray();"
//				), new KeyValuePair<string, object>("a", lst));

//				Console.WriteLine(r[0].ToString());
//			}
//		}

//		private static void LinqTest()
//		{
//			LuaTable t = new LuaTable();
//			for (int i = 1; i <= 26; i++)
//				t[i] = i * i;

//			string[] arr = t.ArrayList.Select(c => c.ToString()).ToArray();
			
//			foreach (string s in arr)
//				Console.Write("{0}, ", s);
//		}

//		private static void MasterTest()
//		{
//			using (Lua l = new Lua())
//			{
//				dynamic g = l.CreateEnvironment();

//				g.dofile(@"d:\temp\a\m.lua");

//				LuaTable t = (LuaTable)g.array;
//				Console.WriteLine("{0}", t[1]);
//			}
//		}

//		private static void Vector3Test()
//		{
//			string luaScript = @"
//local Vector3 = clr.NeoTest1.Vector3
//local v1 = Vector3(1, 1, 1)
//print(v1)
//local v2 = Vector3(v1.X, v1.Y, v1.Z)
//print(v2)
//";
//			using (Lua lua = new Lua())
//			{
//				LuaGlobal lg = lua.CreateEnvironment();
//				try
//				{
//					lg.DoChunk(luaScript, "dummy.lua");
//				}
//				catch (Exception ex)
//				{
//					Console.WriteLine(ex.Message);
//					Console.WriteLine(ex.StackTrace);
//				}
//			}
//		}
	}
}