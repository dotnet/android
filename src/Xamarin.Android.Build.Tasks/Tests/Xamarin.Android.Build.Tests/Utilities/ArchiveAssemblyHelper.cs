using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Xamarin.Android.AssemblyBlobReader;
using Xamarin.ProjectTools;
using Xamarin.Tools.Zip;

namespace Xamarin.Android.Build.Tests
{
	public class ArchiveAssemblyHelper
	{
		public const string DefaultBlobEntryPrefix = "{blob}";

		static readonly HashSet<string> SpecialExtensions = new HashSet<string> (StringComparer.OrdinalIgnoreCase) {
			".dll",
			".config",
			".pdb",
			".mdb",
		};

		static readonly Dictionary<string, string> ArchToAbi = new Dictionary<string, string> (StringComparer.OrdinalIgnoreCase) {
			{"x86", "x86"},
			{"x86_64", "x86_64"},
			{"armeabi_v7a", "armeabi-v7a"},
			{"arm64_v8a", "arm64-v8a"},
		};

		readonly string archivePath;
		readonly string assembliesRootDir;
		bool useAssemblyBlobs;
		List<string> archiveContents;

		public string ArchivePath => archivePath;

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
				assembliesRootDir = String.Empty;
			}
		}

		public List<string> ListArchiveContents (string blobEntryPrefix = DefaultBlobEntryPrefix, bool forceRefresh = false)
		{
			if (!forceRefresh && archiveContents != null) {
				return archiveContents;
			}

			if (String.IsNullOrEmpty (blobEntryPrefix)) {
				throw new ArgumentException (nameof (blobEntryPrefix), "must not be null or empty");
			}

			var entries = new List<string> ();
			using (var zip = ZipArchive.Open (archivePath, FileMode.Open)) {
				foreach (var entry in zip) {
					entries.Add (entry.FullName);
				}
			}

			archiveContents = entries;
			if (!useAssemblyBlobs) {
				Console.WriteLine ("Not using assembly blobs");
				return entries;
			}

			var explorer = new BlobExplorer (archivePath);
			foreach (var asm in explorer.Assemblies) {
				string prefix = blobEntryPrefix;

				if (!String.IsNullOrEmpty (asm.Blob.Arch)) {
					string arch = ArchToAbi[asm.Blob.Arch];
					prefix = $"{prefix}{arch}/";
				}

				entries.Add ($"{prefix}{asm.Name}.dll");
				if (asm.DebugDataOffset > 0) {
					entries.Add ($"{prefix}{asm.Name}.pdb");
				}

				if (asm.ConfigDataOffset > 0) {
					entries.Add ($"{prefix}{asm.Name}.dll.config");
				}
			}

			Console.WriteLine ("Archive entries with synthetised assembly blob entries:");
			foreach (string e in entries) {
				Console.WriteLine ($"  {e}");
			}

			return entries;
		}

		public bool Exists (string entryPath, bool forceRefresh = false)
		{
			List<string> contents = ListArchiveContents (assembliesRootDir, forceRefresh);
			if (contents.Count == 0) {
				return false;
			}

			return contents.Contains (entryPath);
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
