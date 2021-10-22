using System;
using System.Collections.Generic;
using System.IO;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tasks
{
	class ArchAssemblyStore : AssemblyStore
	{
		readonly Dictionary<string, List<AssemblyStoreAssemblyInfo>> assemblies;
		HashSet<string> seenArchAssemblyNames;

		public ArchAssemblyStore (string apkName, string archiveAssembliesPrefix, TaskLoggingHelper log, uint id, AssemblyStoreGlobalIndex globalIndexCounter)
			: base (apkName, archiveAssembliesPrefix, log, id, globalIndexCounter)
		{
			assemblies = new Dictionary<string, List<AssemblyStoreAssemblyInfo>> (StringComparer.OrdinalIgnoreCase);
		}

		public override string WriteIndex (List<AssemblyStoreIndexEntry> globalIndex)
		{
			throw new InvalidOperationException ("Architecture-specific assembly blob cannot contain global assembly index");
		}

		public override void Add (AssemblyStoreAssemblyInfo blobAssembly)
		{
			if (String.IsNullOrEmpty (blobAssembly.Abi)) {
				throw new InvalidOperationException ($"Architecture-agnostic assembly cannot be added to an architecture-specific blob ({blobAssembly.FilesystemAssemblyPath})");
			}

			if (!assemblies.ContainsKey (blobAssembly.Abi)) {
				assemblies.Add (blobAssembly.Abi, new List<AssemblyStoreAssemblyInfo> ());
			}

			List<AssemblyStoreAssemblyInfo> blobAssemblies = assemblies[blobAssembly.Abi];
			blobAssemblies.Add (blobAssembly);

			if (seenArchAssemblyNames == null) {
				seenArchAssemblyNames = new HashSet<string> (StringComparer.Ordinal);
			}

			string assemblyName = GetAssemblyName (blobAssembly);
			if (seenArchAssemblyNames.Contains (assemblyName)) {
				return;
			}

			seenArchAssemblyNames.Add (assemblyName);
		}

		public override void Generate (string outputDirectory, List<AssemblyStoreIndexEntry> globalIndex, List<string> blobPaths)
		{
			if (assemblies.Count == 0) {
				return;
			}

			var assemblyNames = new Dictionary<int, string> ();
			foreach (var kvp in assemblies) {
				string abi = kvp.Key;
				List<AssemblyStoreAssemblyInfo> archAssemblies = kvp.Value;

				// All the architecture blobs must have assemblies in exactly the same order
				archAssemblies.Sort ((AssemblyStoreAssemblyInfo a, AssemblyStoreAssemblyInfo b) => Path.GetFileName (a.FilesystemAssemblyPath).CompareTo (Path.GetFileName (b.FilesystemAssemblyPath)));
				if (assemblyNames.Count == 0) {
					for (int i = 0; i < archAssemblies.Count; i++) {
						AssemblyStoreAssemblyInfo info = archAssemblies[i];
						assemblyNames.Add (i, Path.GetFileName (info.FilesystemAssemblyPath));
					}
					continue;
				}

				if (archAssemblies.Count != assemblyNames.Count) {
					throw new InvalidOperationException ($"Assembly list for ABI '{abi}' has a different number of assemblies than other ABI lists (expected {assemblyNames.Count}, found {archAssemblies.Count}");
				}

				for (int i = 0; i < archAssemblies.Count; i++) {
					AssemblyStoreAssemblyInfo info = archAssemblies[i];
					string fileName = Path.GetFileName (info.FilesystemAssemblyPath);

					if (assemblyNames[i] != fileName) {
						throw new InvalidOperationException ($"Assembly list for ABI '{abi}' differs from other lists at index {i}. Expected '{assemblyNames[i]}', found '{fileName}'");
					}
				}
			}

			bool addToGlobalIndex = true;
			foreach (var kvp in assemblies) {
				string abi = kvp.Key;
				List<AssemblyStoreAssemblyInfo> archAssemblies = kvp.Value;

				if (archAssemblies.Count == 0) {
					continue;
				}

				// Android uses underscores in place of dashes in ABI names, let's follow the convention
				string androidAbi = abi.Replace ('-', '_');
				Generate (Path.Combine (outputDirectory, $"{ApkName}_{BlobPrefix}.{androidAbi}{BlobExtension}"), archAssemblies, globalIndex, blobPaths, addToGlobalIndex);

				// NOTE: not thread safe! The counter must grow monotonically but we also don't want to use different index values for the architecture-specific
				// assemblies with the same names, that would only waste space in the generated `libxamarin-app.so`.  To use the same index values for the same
				// assemblies in different architectures we need to move the counter back here.
				GlobalIndexCounter.Subtract ((uint)archAssemblies.Count);

				if (addToGlobalIndex) {
					// We want the architecture-specific assemblies to be added to the global index only once
					addToGlobalIndex = false;
				}
			}

		}
	}
}
