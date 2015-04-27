using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Neo.IronLua
{
	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public static class LuaDeskopHelper
	{
		/// <summary>Erzeugt ein Delegate aus dem Code, ohne ihn auszuführen.</summary>
		/// <param name="sFileName">Dateiname die gelesen werden soll.</param>
		/// <param name="options">Options for the compile process.</param>
		/// <param name="args">Parameter für den Codeblock</param>
		/// <returns>Compiled chunk.</returns>
		public LuaChunk CompileChunk(string sFileName, LuaCompileOptions options, params KeyValuePair<string, Type>[] args)
		{
			return CompileChunk(sFileName, options, new StreamReader(sFileName), args);
		} // func CompileChunk

	} // class LuaDeskopHelper
}

#region -- class Lua --

		//#region -- CreateEnvironment ------------------------------------------------------

		///// <summary>Creates an empty environment.</summary>
		///// <returns>Initialized environment</returns>
		//public virtual LuaGlobal CreateEnvironment()
		//{
		//	return new LuaGlobal(this);
		//} // func CreateEnvironment

		///// <summary>Create an empty environment</summary>
		///// <typeparam name="T"></typeparam>
		///// <returns></returns>
		//public T CreateEnvironment<T>()
		//	where T : LuaGlobal
		//{
		//	return (T)Activator.CreateInstance(typeof(T), this);
		//} // func CreateEnvironment

		//#endregion

//#region -- Require ----------------------------------------------------------------

		//internal LuaChunk LuaRequire(LuaGlobal global, object modname)
		//{
		//	if (modname is string)
		//	{
		//		string sModName = (string)modname;
		//		string sFileName;
		//		DateTime dtStamp;
		//		if (global.LuaRequireFindFile(sModName, out sFileName, out dtStamp))
		//		{
		//			lock (packageLock)
		//			{
		//				WeakReference rc;
		//				LuaChunk c;
		//				string sCacheId = sFileName + ";" + dtStamp.ToString("o");

		//				// is the modul loaded
		//				if (loadedModuls == null ||
		//					!loadedModuls.TryGetValue(sCacheId, out rc) ||
		//					!rc.IsAlive)
		//				{
		//					// compile the modul
		//					c = CompileChunk(sFileName, null);

		//					// Update Cache
		//					if (loadedModuls == null)
		//						loadedModuls = new Dictionary<string, WeakReference>();
		//					loadedModuls[sCacheId] = new WeakReference(c);
		//				}
		//				else
		//					c = (LuaChunk)rc.Target;

		//				return c;
		//			}
		//		}
		//	}
		//	return null;
		//} // func LuaRequire

		//#endregion

		///// <summary>Default path for the package loader</summary>
		//public string[] StandardPackagesPaths
		//{
		//	get
		//	{
		//		if (standardPackagePaths == null)
		//		{
		//			string sExecutingDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
		//			standardPackagePaths = new string[]
		//			{
		//				"%currentdirectory%",
		//				sExecutingDirectory,
		//				Path.Combine(sExecutingDirectory, "lua")
		//			};
		//		}
		//		return standardPackagePaths;
		//	}
		//} // prop StandardPackagesPaths

		///// <summary>Default path for the package loader</summary>
		//public string StandardPackagesPath
		//{
		//	get { return String.Join(";", StandardPackagesPaths); }
		//	set
		//	{
		//		if (String.IsNullOrEmpty(value))
		//			standardPackagePaths = null;
		//		else
		//			standardPackagePaths = value.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
		//	}
		//} // prop StandardPackagesPath

		///// <summary>Returns a default StackTrace-Debug Engine</summary>
		//public static LuaCompileOptions DefaultDebugEngine
		//{
		//	get
		//	{
		//		lock (lockDefaultDebugEngine)
		//			if (defaultDebugEngine == null)
		//				defaultDebugEngine = new LuaCompileOptions() { DebugEngine = new LuaStackTraceDebugger() };
		//		return defaultDebugEngine;
		//	}
		//} // prop DefaultDebugEngine

		///// <summary>Returns the Version of the assembly</summary>
		//public static Version Version
		//{
		//	get
		//	{
		//		AssemblyFileVersionAttribute attr = (AssemblyFileVersionAttribute)Attribute.GetCustomAttribute(typeof(Lua).Assembly, typeof(AssemblyFileVersionAttribute));
		//		return attr == null ? new Version() : new Version(attr.Version);
		//	}
		//} // prop Version
#endregion