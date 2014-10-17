using System;
using Neo.IronLua;

namespace NeoTest1
{
	public class Vector3
	{
		public float X;
		public float Y;
		public float Z;

		public Vector3(float x, float y, float z)
		{
			X = x;
			Y = y;
			Z = z;
		}

		public override string ToString()
		{
			return string.Format("X = {0}, Y = {1}, Z = {2}", X, Y, Z);
		}
	}
	class Program
	{
		static void Main(string[] args)
		{
			string luaScript = @"
local Vector3 = clr.NeoTest1.Vector3
local v1 = Vector3(1, 1, 1)
print(v1)
local v2 = Vector3(v1.X, v1.Y, v1.Z)
print(v2)
";
			using (Lua lua = new Lua())
			{
				LuaGlobal lg = lua.CreateEnvironment();
				try
				{
					lg.DoChunk(luaScript, "dummy.lua");
				}
				catch (Exception ex)
				{
					Console.WriteLine(ex.Message);
					Console.WriteLine(ex.StackTrace);
				}
			}
			Console.ReadKey();
		}
	}
}