using System;
using System.Collections.Generic;
using System.IO;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tasks
{
	class ArchAssemblyBlob : AssemblyBlob
	{
		readonly Dictionary<string, List<BlobAssemblyInfo>> assemblies;
		HashSet<string> seenArchAssemblyNames;

		public ArchAssemblyBlob (string apkName, string archiveAssembliesPrefix, TaskLoggingHelper log)
			: base (apkName, archiveAssembliesPrefix, log)
		{
			assemblies = new Dictionary<string, List<BlobAssemblyInfo>> (StringComparer.OrdinalIgnoreCase);
		}

		public override void WriteIndex (List<AssemblyBlobIndexEntry> globalIndex)
		{
			throw new InvalidOperationException ("Architecture-specific assembly blob cannot contain global assembly index");
		}

		public override void Add (BlobAssemblyInfo blobAssembly)
		{
			if (String.IsNullOrEmpty (blobAssembly.Abi)) {
				throw new InvalidOperationException ($"Architecture-agnostic assembly cannot be added to an architecture-specific blob ({blobAssembly.FilesystemAssemblyPath})");
			}

			if (!assemblies.ContainsKey (blobAssembly.Abi)) {
				assemblies.Add (blobAssembly.Abi, new List<BlobAssemblyInfo> ());
			}

			Log.LogMessage (MessageImportance.Low, $"AssemblyBlobGenerator: adding Arch '{blobAssembly.Abi}' assembly {blobAssembly.FilesystemAssemblyPath}");
			List<BlobAssemblyInfo> blobAssemblies = assemblies[blobAssembly.Abi];
			blobAssemblies.Add (blobAssembly);

			if (seenArchAssemblyNames == null) {
				seenArchAssemblyNames = new HashSet<string> (StringComparer.Ordinal);
			}

			string assemblyName = GetAssemblyName (blobAssembly);
			if (seenArchAssemblyNames.Contains (assemblyName)) {
				return;
			}

			seenArchAssemblyNames.Add (assemblyName);
			AssemblyIndex.Add (new AssemblyBlobIndexEntry (assemblyName, ID));
		}

		public override void Generate (string outputDirectory, List<AssemblyBlobIndexEntry> globalIndex, List<string> blobPaths)
		{
			if (assemblies.Count == 0) {
				return;
			}

			// TODO: make sure all the lists are identical
			foreach (var kvp in assemblies) {
				string abi = kvp.Key;
				List<BlobAssemblyInfo> archAssemblies = kvp.Value;

				if (archAssemblies.Count == 0) {
					continue;
				}

				Generate (Path.Combine (outputDirectory, $"{ApkName}_{BlobPrefix}_{abi}{BlobExtension}"), archAssemblies, globalIndex, blobPaths);
			}

		}
	}
}
