using System;
using System.Collections.Generic;
using System.Linq;

namespace Xamarin.Android.Tasks
{
	class NativeTypeMappingData
	{
		public TypeMapGenerator.ModuleReleaseData[] Modules { get; }
		public TypeMapGenerator.TypeMapReleaseEntry[] JavaTypes { get; }

		public uint MapModuleCount { get; }
		public uint JavaTypeCount  { get; }

		public NativeTypeMappingData (Action<string> logger, TypeMapGenerator.ModuleReleaseData[] modules)
		{
			Modules = modules ?? throw new ArgumentNullException (nameof (modules));

			MapModuleCount = (uint)modules.Length;

			var tempJavaTypes = new Dictionary<string, TypeMapGenerator.TypeMapReleaseEntry> (StringComparer.Ordinal);
			var moduleComparer = new TypeMapGenerator.ModuleUUIDArrayComparer ();

			foreach (TypeMapGenerator.ModuleReleaseData data in modules) {
				int moduleIndex = Array.BinarySearch (modules, data, moduleComparer);
				if (moduleIndex < 0)
					throw new InvalidOperationException ($"Unable to map module with MVID {data.Mvid} to array index");

				foreach (TypeMapGenerator.TypeMapReleaseEntry entry in data.Types) {
					entry.ModuleIndex = moduleIndex;
					if (tempJavaTypes.ContainsKey (entry.JavaName))
						continue;
					tempJavaTypes.Add (entry.JavaName, entry);
				}
			}

			JavaTypes = tempJavaTypes.Values.ToArray ();
			JavaTypeCount = (uint)JavaTypes.Length;
		}
	}
}
