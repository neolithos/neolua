using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace Neo.IronLua
{
	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Extension for the desktop lua version.</summary>
	public static class LuaDeskop
	{
		#region -- class AssemblyCacheItem ------------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private sealed class AssemblyCacheList : IEnumerable<Assembly>, ILuaTypeResolver
		{
			#region -- CacheItem ------------------------------------------------------------

			///////////////////////////////////////////////////////////////////////////////
			/// <summary></summary>
			private class CacheItem
			{
				private AssemblyName assemblyName;
				public Assembly assembly = null;			// Reference to the loaded assembly
				public Assembly reflected = null;			// Reflected assembly
				public CacheItem next = null;					// Next item
				public CacheItem prev = null;					// Prev item

				public CacheItem(AssemblyName assemblyName)
				{
					this.assemblyName = assemblyName;
				} // ctor

				public override string ToString()
				{
					return assemblyName.Name;
				} // func ToString

				public AssemblyName Name { get { return assemblyName; } }
			} // class AssemblyCacheItem

			#endregion

			#region -- AssemblyCacheEnumerator ----------------------------------------------

			private sealed class AssemblyCacheEnumerator : IEnumerator<Assembly>
			{
				private AssemblyCacheList owner;
				private Assembly currentAssembly = null;
				private CacheItem current = null;

				public AssemblyCacheEnumerator(AssemblyCacheList owner)
				{
					this.owner = owner;
					Reset();
				} // ctor

				public void Dispose()
				{
				} // proc Dispose

				public bool MoveNext()
				{
					if (current == null)
						current = owner.first;
					else
						current = current.next;

				Retry:
					if (current == null)
						return false;
					else
					{
						lock (current)
						{
							currentAssembly = current.assembly ?? current.reflected;

							if (currentAssembly == null && LookupReferencedAssemblies)
							{
								try
								{
									currentAssembly =
										current.reflected =
										Assembly.ReflectionOnlyLoad(current.Name.FullName);
								}
								catch
								{
									// current reflect load failed, try next
									var t = current;
									current = current.next;
									owner.RemoveAssembly(t);

									goto Retry;
								}
							}

							return currentAssembly != null;
						}
					}
				} // func MoveNext

				public void Reset()
				{
					currentAssembly = null;
					current = null;
				} // proc Reset

				public Assembly Current { get { return currentAssembly; } }
				object System.Collections.IEnumerator.Current { get { return Current; } }
			} // class AssemblyCacheEnumerator

			#endregion

			private Dictionary<string, CacheItem> cache = new Dictionary<string, CacheItem>(StringComparer.OrdinalIgnoreCase);
			private CacheItem first = null;
			private CacheItem lastLoaded = null;
			private CacheItem lastReflected = null;

			private int iAssemblyCount = 0;

			public AssemblyCacheList()
			{
				AssemblyName assemblyName = typeof(string).GetTypeInfo().Assembly.GetName(); // mscorlib is always first
				cache[assemblyName.Name] =
					first =
					lastReflected =
					lastLoaded = new CacheItem(assemblyName);
			} // ctor

			public void Refresh()
			{
				lock (this)
				{
					Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();

					if (iAssemblyCount < assemblies.Length) // New assemblies loaded 
					{
						for (int i = iAssemblyCount; i < assemblies.Length; i++) // add the new assemblies
						{
							Assembly asm = assemblies[i];

							// check if the assembly is in the list, if not create the item
							CacheItem item;
							AssemblyName assemblyName = asm.GetName();
							if (!cache.TryGetValue(assemblyName.Name, out item))
								item = AddCacheItem(assemblyName, true);
							else if (lastLoaded != item)
							{
								// Remove item
								RemoveAssembly(item);

								InsertLoaded(item);
							}

							UpdateLoadedAssembly(item, asm);
						}

						// Update the assembly count
						iAssemblyCount = assemblies.Length;
					}
				}
			} // proc Refresh

			private void UpdateLoadedAssembly(CacheItem item, Assembly assembly)
			{
				if (item.assembly == assembly)
					return;

				// update the assembly
				lock (item)
				{
					item.assembly = assembly;
					item.reflected = null;
				}

				// add direct referenced assemblies
				foreach (var r in assembly.GetReferencedAssemblies())
				{
					if (!cache.ContainsKey(r.Name))
						AddCacheItem(r, false);
				}
			} // proc UpdateLoadedAssembly

			private CacheItem AddCacheItem(AssemblyName assemblyName, bool lLastLoaded)
			{
				lock (this)
				{
					var n = new CacheItem(assemblyName);

					// add the item to the list
					if (lLastLoaded)
						InsertLoaded(n);
					else
					{
						n.prev = lastReflected;
						if (lastReflected != null)
						{
							lastReflected.next = n;
						}
						lastReflected = n;
					}

					// add the item to the cache
					cache[assemblyName.Name] = n;

					return n;
				}
			} // proc AddCacheItem

			private void InsertLoaded(CacheItem n)
			{
				// Insert after last loaded
				var after = lastLoaded.next;

				n.prev = lastLoaded;
				n.next = after;

				lastLoaded.next = n;
				lastLoaded = n;

				if (after != null)
					after.prev = n;
				else
					lastReflected = lastLoaded;
			} // proc InsertLoaded

			private void RemoveAssembly(CacheItem item)
			{
				lock (this)
				{
					if (item.prev != null)
						item.prev.next = item.next;
					if (item.next == null)
						lastReflected = item.prev;
					else
						item.next.prev = item.prev;

					item.next = null;
					item.prev = null;
				}
			} // proc RemoveAssembly

			Type ILuaTypeResolver.GetType(string sTypeName)
			{
				foreach (Assembly asm in this)
				{
					// Search the type in the assembly
					Type t = asm.GetType(sTypeName, false);

					if (t != null)
					{
						// the type is reflected, load the assembly and get the type
						if (asm.ReflectionOnly)
							t = Type.GetType(t.AssemblyQualifiedName);

						if (t != null)
							return t;
					}
				}
				return null;
			} // func ILuaTypeResolver.GetType

			public IEnumerator<Assembly> GetEnumerator()
			{
				return new AssemblyCacheEnumerator(this);
			} // func GetEnumerator

			System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
			{
				return new AssemblyCacheEnumerator(this);
			} // func System.Collections.IEnumerable.GetEnumerator

			/// <summary>Number of loaded assemblies</summary>
			public int AssemblyCount { get { return iAssemblyCount; } }
			int ILuaTypeResolver.Version { get { return iAssemblyCount; } }
		} // class AssemblyCacheList

		#endregion

		private static bool lLookupReferencedAssemblies = false;	// reference search for types
		private static ILuaTypeResolver typeResolver = new AssemblyCacheList();
		private static LuaCompileOptions stackTraceCompileOptions = new LuaCompileOptions { DebugEngine = LuaStackTraceDebugger.Default };

		/// <summary>Erzeugt ein Delegate aus dem Code, ohne ihn auszuführen.</summary>
		/// <param name="lua"></param>
		/// <param name="sFileName">Dateiname die gelesen werden soll.</param>
		/// <param name="options">Options for the compile process.</param>
		/// <param name="args">Parameter für den Codeblock</param>
		/// <returns>Compiled chunk.</returns>
		public static LuaChunk CompileChunk(this Lua lua, string sFileName, LuaCompileOptions options, params KeyValuePair<string, Type>[] args)
		{
			using (StreamReader sr = new StreamReader(sFileName))
				return lua.CompileChunk(sr, Path.GetFileName(sFileName), options, args);
		} // func CompileChunk

		/// <summary>Desktop type resolver.</summary>
		public static ILuaTypeResolver LuaTypeResolver { get { return typeResolver; } }
		/// <summary>Should the type resolve also scan references assemblies.</summary>
		public static bool LookupReferencedAssemblies { get { return lLookupReferencedAssemblies; } set { lLookupReferencedAssemblies = value; } }
		/// <summary></summary>
		public static LuaCompileOptions StackTraceCompileOptions { get { return stackTraceCompileOptions; } }
	} // class LuaDeskop
}
