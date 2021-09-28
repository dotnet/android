using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Xamarin.Android.AssemblyBlobReader;
using Xamarin.ProjectTools;
using Xamarin.Tools.Zip;

namespace Xamarin.Android.Build.Tests
{
	class ArchiveAssemblyHelper
	{
		public const string DefaultBlobEntryPrefix = "{blob}";

		static readonly HashSet<string> SpecialExtensions = new HashSet<string> (StringComparer.OrdinalIgnoreCase) {
			".dll",
			".config",
			".pdb",
			".mdb",
		};

		readonly string archivePath;
		readonly string assembliesRootDir;
		bool useAssemblyBlobs;

		public ArchiveAssemblyHelper (string archivePath, bool useAssemblyBlobs)
		{
			if (String.IsNullOrEmpty (archivePath)) {
				throw new ArgumentException ("must not be null or empty", nameof (archivePath));
			}

			this.archivePath = archivePath;
			this.useAssemblyBlobs = useAssemblyBlobs;

			string extension = Path.GetExtension (archivePath) ?? String.Empty;
			if (String.Compare (".aab", extension, StringComparison.OrdinalIgnoreCase) == 0) {
				assembliesRootDir = "base/root/assemblies";
			} else if (String.Compare (".apk", extension, StringComparison.OrdinalIgnoreCase) == 0) {
				assembliesRootDir = "assemblies/";
			} else if (String.Compare (".zip", extension, StringComparison.OrdinalIgnoreCase) == 0) {
				assembliesRootDir = "root/assemblies/";
			} else {
				throw new InvalidOperationException ($"Unrecognized archive extension '{extension}'");
			}
		}

		public List<string> ListArchiveContents (string blobEntryPrefix = DefaultBlobEntryPrefix)
		{
			if (String.IsNullOrEmpty (blobEntryPrefix)) {
				throw new ArgumentException (nameof (blobEntryPrefix), "must not be null or empty");
			}

			var entries = new List<string> ();
			using (var zip = ZipArchive.Open (archivePath, FileMode.Open)) {
				foreach (var entry in zip) {
					entries.Add (entry.FullName);
				}
			}

			if (!useAssemblyBlobs) {
				Console.WriteLine ("Not using assembly blobs");
				return entries;
			}

			var explorer = new BlobExplorer (archivePath);
			Console.WriteLine ($"explorer.AssembliesByName count: {explorer.AssembliesByName.Count}");
			foreach (var kvp in explorer.AssembliesByName) {
				string name = kvp.Key;
				BlobAssembly asm = kvp.Value;

				entries.Add ($"{blobEntryPrefix}{name}.dll");
				if (asm.DebugDataOffset > 0) {
					entries.Add ($"{blobEntryPrefix}{name}.pdb");
				}

				if (asm.ConfigDataOffset > 0) {
					entries.Add ($"{blobEntryPrefix}{name}.dll.config");
				}
			}

			return entries;
		}

		public void Contains (string[] fileNames, out List<string> existingFiles, out List<string> missingFiles, out List<string> additionalFiles)
		{
			if (fileNames == null) {
				throw new ArgumentNullException (nameof (fileNames));
			}

			if (fileNames.Length == 0) {
				throw new ArgumentException ("must not be empty", nameof (fileNames));
			}

			if (useAssemblyBlobs) {
				BlobContains (fileNames, out existingFiles, out missingFiles, out additionalFiles);
			} else {
				ArchiveContains (fileNames, out existingFiles, out missingFiles, out additionalFiles);
			}
		}

		void ArchiveContains (string[] fileNames, out List<string> existingFiles, out List<string> missingFiles, out List<string> additionalFiles)
		{
			using (var zip = ZipHelper.OpenZip (archivePath)) {
				existingFiles = zip.Where (a => a.FullName.StartsWith (assembliesRootDir, StringComparison.InvariantCultureIgnoreCase)).Select (a => a.FullName).ToList ();
				missingFiles = fileNames.Where (x => !zip.ContainsEntry (assembliesRootDir + Path.GetFileName (x))).ToList ();
				additionalFiles = existingFiles.Where (x => !fileNames.Contains (Path.GetFileName (x))).ToList ();
			}
		}

		void BlobContains (string[] fileNames, out List<string> existingFiles, out List<string> missingFiles, out List<string> additionalFiles)
		{
			var assemblyNames = fileNames.Where (x => x.EndsWith (".dll", StringComparison.OrdinalIgnoreCase)).ToList ();
			var configFiles = fileNames.Where (x => x.EndsWith (".config", StringComparison.OrdinalIgnoreCase)).ToList ();
			var debugFiles = fileNames.Where (x => x.EndsWith (".pdb", StringComparison.OrdinalIgnoreCase) || x.EndsWith (".mdb", StringComparison.OrdinalIgnoreCase)).ToList ();
			var otherFiles = fileNames.Where (x => !SpecialExtensions.Contains (Path.GetExtension (x))).ToList ();

			existingFiles = new List<string> ();
			missingFiles = new List<string> ();
			additionalFiles = new List<string> ();

			if (otherFiles.Count > 0) {
				using (var zip = ZipHelper.OpenZip (archivePath)) {
					foreach (string file in otherFiles) {
						string fullPath = assembliesRootDir + Path.GetFileName (file);
						if (zip.ContainsEntry (fullPath)) {
							existingFiles.Add (file);
						}
					}
				}
			}

			var explorer = new BlobExplorer (archivePath);

			// Blobs don't store the assembly extension
			var blobAssemblies = explorer.AssembliesByName.Keys.Select (x => $"{x}.dll");
			if (explorer.AssembliesByName.Count != 0) {
				existingFiles.AddRange (blobAssemblies);

				// We need to fake config and debug files since they have no named entries in the blob
				foreach (string file in configFiles) {
					BlobAssembly asm = GetBlobAssembly (file);
					if (asm == null) {
						continue;
					}

					if (asm.ConfigDataOffset > 0) {
						existingFiles.Add (file);
					}
				}

				foreach (string file in debugFiles) {
					BlobAssembly asm = GetBlobAssembly (file);
					if (asm == null) {
						continue;
					}

					if (asm.DebugDataOffset > 0) {
						existingFiles.Add (file);
					}
				}
			}

			foreach (string file in fileNames) {
				if (existingFiles.Contains (Path.GetFileName (file))) {
					continue;
				}
				missingFiles.Add (file);
			}

			additionalFiles = existingFiles.Where (x => !fileNames.Contains (x)).ToList ();

			BlobAssembly GetBlobAssembly (string file)
			{
				string assemblyName = Path.GetFileNameWithoutExtension (file);
				if (assemblyName.EndsWith (".dll", StringComparison.OrdinalIgnoreCase)) {
					assemblyName = Path.GetFileNameWithoutExtension (assemblyName);
				}

				if (!explorer.AssembliesByName.TryGetValue (assemblyName, out BlobAssembly asm) || asm == null) {
					return null;
				}

				return asm;
			}
		}
	}
}
