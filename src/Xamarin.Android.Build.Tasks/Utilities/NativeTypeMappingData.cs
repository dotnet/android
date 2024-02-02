using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.Build.Utilities;
using Microsoft.Android.Build.Tasks;

namespace Xamarin.Android.Tasks
{
	class NativeTypeMappingData
	{
		public TypeMapGenerator.ModuleReleaseData[] Modules { get; }
		public TypeMapGenerator.TypeMapReleaseEntry[] JavaTypes { get; }

		public uint MapModuleCount { get; }
		public uint JavaTypeCount  { get; }

		public NativeTypeMappingData (TaskLoggingHelper log, TypeMapGenerator.ModuleReleaseData[] modules)
		{
			Modules = modules ?? throw new ArgumentNullException (nameof (modules));

			MapModuleCount = (uint)modules.Length;

			var tempJavaTypes = new Dictionary<string, TypeMapGenerator.TypeMapReleaseEntry> (StringComparer.Ordinal);
			var moduleComparer = new TypeMapGenerator.ModuleUUIDArrayComparer ();

			TypeMapGenerator.ModuleReleaseData? monoAndroid = null;
			foreach (TypeMapGenerator.ModuleReleaseData data in modules) {
				if (data.AssemblyName == "Mono.Android") {
					monoAndroid = data;
					break;
				}
			}

			if (monoAndroid != null) {
				ProcessModule (monoAndroid);
			}

			foreach (TypeMapGenerator.ModuleReleaseData data in modules) {
				if (data.AssemblyName == "Mono.Android") {
					continue;
				}
				ProcessModule (data);
			};

			void ProcessModule (TypeMapGenerator.ModuleReleaseData data)
			{
				int moduleIndex = Array.BinarySearch (modules, data, moduleComparer);
				if (moduleIndex < 0)
					throw new InvalidOperationException ($"Unable to map module with MVID {data.Mvid} to array index");

				foreach (TypeMapGenerator.TypeMapReleaseEntry entry in data.Types) {
					entry.ModuleIndex = moduleIndex;
					if (tempJavaTypes.ContainsKey (entry.JavaName)) {
						log.LogDebugMessage ($"Skipping typemap entry for `{entry.ManagedTypeName}, {data.AssemblyName}`; `{entry.JavaName}` is already mapped.");
						continue;
					}
					tempJavaTypes.Add (entry.JavaName, entry);
				}
			}

			JavaTypes = tempJavaTypes.Values.ToArray ();
			JavaTypeCount = (uint)JavaTypes.Length;
		}
	}
}
