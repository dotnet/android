using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

using Mono.Cecil;
using Xamarin.Android.AssemblyStore;
using Xamarin.Android.AssemblyStore.V1;

namespace tmt
{
	class ApkManagedTypeResolver : ManagedTypeResolver
	{
		readonly Dictionary<string, string>? individualAssemblies;
		readonly Dictionary<string, AssemblyStoreAssembly>? blobAssemblies;
		readonly string apkPath;
		readonly AssemblyStoreExplorer? assemblyStoreExplorer;

		public ApkManagedTypeResolver (string apkPath, ZipArchive apk, string assemblyEntryPrefix)
		{
			this.apkPath = Path.GetFullPath (apkPath);

			if (apk.GetEntry ($"{assemblyEntryPrefix}assemblies.blob") != null) {
				var assemblies = new Dictionary<string, AssemblyStoreAssembly> (StringComparer.Ordinal);
				blobAssemblies = assemblies;
				assemblyStoreExplorer = new AssemblyStoreExplorer (apk, assemblyEntryPrefix, keepStoreInMemory: true);
				LoadAssemblyBlobs (assemblyStoreExplorer, assemblies);
			} else {
				var assemblies = new Dictionary<string, string> (StringComparer.Ordinal);
				individualAssemblies = assemblies;
				LoadIndividualAssemblies (apk, assemblyEntryPrefix, assemblies);
			}
		}

		void LoadAssemblyBlobs (AssemblyStoreExplorer explorer, Dictionary<string, AssemblyStoreAssembly> assemblies)
		{
			foreach (AssemblyStoreAssembly assembly in explorer.Assemblies) {
				string assemblyName = assembly.Name;
				string dllName = assembly.DllName;

				if (!String.IsNullOrEmpty (assembly.Store.Arch)) {
					assemblyName = $"{assembly.Store.Arch}/{assemblyName}";
					dllName = $"{assembly.Store.Arch}/{dllName}";
				}

				assemblies.Add (assemblyName, assembly);
				assemblies.Add (dllName, assembly);
			}
		}

		void LoadIndividualAssemblies (ZipArchive apkArchive, string assemblyEntryPrefix, Dictionary<string, string> assemblies)
		{
			foreach (ZipArchiveEntry entry in apkArchive.Entries) {
				if (!entry.FullName.StartsWith (assemblyEntryPrefix, StringComparison.Ordinal)) {
					continue;
				}

				if (!entry.FullName.EndsWith (".dll", StringComparison.Ordinal)) {
					continue;
				}

				string relativeName = entry.FullName.Substring (assemblyEntryPrefix.Length);
				string? dir = Path.GetDirectoryName (relativeName);
				string name = Path.GetFileNameWithoutExtension (relativeName);
				if (!String.IsNullOrEmpty (dir)) {
					name = $"{dir}/{name}";
				}

				assemblies.Add (name, entry.FullName);
				assemblies.Add (entry.FullName, entry.FullName);
			}
		}

		protected override string? FindAssembly (string assemblyName)
		{
			if (individualAssemblies != null) {
				if (individualAssemblies.Count == 0) {
					return null;
				}

				if (!individualAssemblies.TryGetValue (assemblyName, out string? entryName)) {
					return null;
				}

				return entryName;
			}

			if (blobAssemblies == null || !blobAssemblies.TryGetValue (assemblyName, out AssemblyStoreAssembly? assembly) || assembly == null) {
				return null;
			}

			return assembly.Name;
		}

		Stream GetAssemblyStream (string assemblyPath)
		{
			if (individualAssemblies != null) {
				if (!individualAssemblies.TryGetValue (assemblyPath, out string? entryName)) {
					// Should "never" happen - if the assembly wasn't there, FindAssembly should have returned `null`
					throw new InvalidOperationException ($"Should not happen: assembly '{assemblyPath}' not found in the APK archive.");
				}

				// Managed names are resolved after Loader has disposed its archive.
				using ZipArchive apk = ZipFile.OpenRead (apkPath);
				ZipArchiveEntry entry = apk.GetEntry (entryName) ??
					throw new InvalidOperationException ($"Assembly '{assemblyPath}' not found in the APK archive.");
				using Stream input = entry.Open ();
				var output = new MemoryStream ();
				var ownsOutput = false;
				try {
					input.CopyTo (output);
					output.Seek (0, SeekOrigin.Begin);
					ownsOutput = true;
					return output;
				} finally {
					if (!ownsOutput)
						output.Dispose ();
				}
			}

			if (blobAssemblies == null) {
				throw new InvalidOperationException ("Internal error: blobAssemblies shouldn't be null");
			}

			if (!blobAssemblies.TryGetValue (assemblyPath, out AssemblyStoreAssembly? assembly) || assembly == null) {
				// Should "never" happen - if the assembly wasn't there, FindAssembly should have returned `null`
				throw new InvalidOperationException ($"Should not happen: assembly '{assemblyPath}' not found in the assembly blob.");
			}

			var stream = new MemoryStream ();
			assembly.ExtractImage (stream);
			stream.Seek (0, SeekOrigin.Begin);
			return stream;
		}

		protected override AssemblyDefinition ReadAssembly (string assemblyPath)
		{
			Stream stream = GetAssemblyStream (assemblyPath);
			var decompressed = new MemoryStream ();
			if (AssemblyCompression.TryDecompress (stream, decompressed, out _)) {
				stream.Dispose ();
				decompressed.Seek (0, SeekOrigin.Begin);
				stream = decompressed;
			} else {
				decompressed.Dispose ();
			}

			return AssemblyDefinition.ReadAssembly (stream);
		}
	}
}
