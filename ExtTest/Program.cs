using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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

	class Program
	{
		static async Task<int> TestAsync()
		{
			await Task.Yield();
			return 0;
		}

		static void Main(string[] args)
		{
			Console.Write(TestAsync().Result);
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

			//LinqTest2();
			Console.ReadKey();
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