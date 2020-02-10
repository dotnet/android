using System;
using System.Collections.Generic;
using System.Linq;

namespace Xamarin.Android.Tasks
{
	class NativeTypeMappingData
	{
		public TypeMapGenerator.ModuleData[] Modules { get; }
		public IDictionary<string, string> AssemblyNames { get; }
		public string[] JavaTypeNames                    { get; }
		public TypeMapGenerator.TypeMapEntry[] JavaTypes { get; }

		public uint MapModuleCount { get; }
		public uint JavaTypeCount  { get; }
		public uint JavaNameWidth  { get; }

		public NativeTypeMappingData (Action<string> logger, TypeMapGenerator.ModuleData[] modules, int javaNameWidth)
		{
			Modules = modules ?? throw new ArgumentNullException (nameof (modules));

			MapModuleCount = (uint)modules.Length;
			JavaNameWidth = (uint)javaNameWidth;

			AssemblyNames = new Dictionary<string, string> (StringComparer.Ordinal);

			var tempJavaTypes = new Dictionary<string, TypeMapGenerator.TypeMapEntry> (StringComparer.Ordinal);
			int managedStringCounter = 0;
			var moduleComparer = new TypeMapGenerator.ModuleUUIDArrayComparer ();

			foreach (TypeMapGenerator.ModuleData data in modules) {
				data.AssemblyNameLabel = $"map_aname.{managedStringCounter++}";
				AssemblyNames.Add (data.AssemblyNameLabel, data.AssemblyName);

				int moduleIndex = Array.BinarySearch (modules, data, moduleComparer);
				if (moduleIndex < 0)
					throw new InvalidOperationException ($"Unable to map module with MVID {data.Mvid} to array index");

				foreach (TypeMapGenerator.TypeMapEntry entry in data.Types) {
					entry.ModuleIndex = moduleIndex;
					if (tempJavaTypes.ContainsKey (entry.JavaName))
						continue;
					tempJavaTypes.Add (entry.JavaName, entry);
				}
			}

			var javaNames = tempJavaTypes.Keys.ToArray ();
			Array.Sort (javaNames, StringComparer.Ordinal);

			var javaTypes = new TypeMapGenerator.TypeMapEntry[javaNames.Length];
			for (int i = 0; i < javaNames.Length; i++) {
				javaTypes[i] = tempJavaTypes[javaNames[i]];
			}

			JavaTypes = javaTypes;
			JavaTypeNames = javaNames;
			JavaTypeCount = (uint)JavaTypes.Length;
		}
	}
}
