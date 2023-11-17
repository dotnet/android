using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Xamarin.Android.AssemblyStore;
using Xamarin.Android.Tools;
using Xamarin.Android.Tasks;
using Xamarin.ProjectTools;
using Xamarin.Tools.Zip;

namespace Xamarin.Android.Build.Tests
{
	public class ArchiveAssemblyHelper
	{
		public const string DefaultAssemblyStoreEntryPrefix = "{storeReader}";
		const int AssemblyStoreReadBufferSize = 8192;

		static readonly HashSet<string> SpecialExtensions = new HashSet<string> (StringComparer.OrdinalIgnoreCase) {
			".dll",
			".config",
			".pdb",
		};

		static readonly ArrayPool<byte> buffers = ArrayPool<byte>.Shared;

		readonly string archivePath;
		readonly string assembliesRootDir;
		bool useAssemblyStores;
		bool haveMultipleRids;
		List<string> archiveContents;

		public string ArchivePath => archivePath;

		public ArchiveAssemblyHelper (string archivePath, bool useAssemblyStores = true, string[] rids = null)
		{
			if (String.IsNullOrEmpty (archivePath)) {
				throw new ArgumentException ("must not be null or empty", nameof (archivePath));
			}

			this.archivePath = archivePath;
			this.useAssemblyStores = useAssemblyStores;
			haveMultipleRids = rids != null && rids.Length > 1;

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

		public Stream? ReadEntry (string path, AndroidTargetArch arch = AndroidTargetArch.None, bool uncompressIfNecessary = false)
		{
			if (useAssemblyStores) {
				return ReadStoreEntry (path, arch, uncompressIfNecessary);
			}

			return ReadZipEntry (path, arch, uncompressIfNecessary);
		}

		Stream? ReadZipEntry (string path, AndroidTargetArch arch, bool uncompressIfNecessary)
		{
			using (var zip = ZipHelper.OpenZip (archivePath)) {
				ZipEntry entry = zip.ReadEntry (path);
				var ret = new MemoryStream ();
				entry.Extract (ret);
				ret.Flush ();
				return ret;
			}
		}

		Stream? ReadStoreEntry (string path, AndroidTargetArch arch, bool uncompressIfNecessary)
		{
			string name = Path.GetFileNameWithoutExtension (path);
			(IList<AssemblyStoreExplorer>? explorers, string? errorMessage) = AssemblyStoreExplorer.Open (archivePath);
			AssemblyStoreExplorer? explorer = SelectExplorer (explorers, arch);
			if (explorer == null) {
				Console.WriteLine ($"Failed to read assembly '{name}' from '{archivePath}'. {errorMessage}");
				return null;
			}

			IList<AssemblyStoreItem>? assemblies = explorer.Find (name, arch);
			if (assemblies == null) {
				Console.WriteLine ($"Failed to locate assembly '{name}' in assembly store for architecture '{arch}', in archive '{archivePath}'");
				return null;
			}

			AssemblyStoreItem? assembly = null;
			foreach (AssemblyStoreItem item in assemblies) {
				if (arch == AndroidTargetArch.None || item.TargetArch == arch) {
					assembly = item;
					break;
				}
			}

			if (assembly == null) {
				Console.WriteLine ($"Failed to find assembly '{name}' in assembly store for architecture '{arch}', in archive '{archivePath}'");
				return null;
			}

			return explorer.Read (assembly, uncompressIfNecessary);
		}

		public List<string> ListArchiveContents (string storeEntryPrefix = DefaultAssemblyStoreEntryPrefix, bool forceRefresh = false, AndroidTargetArch arch = AndroidTargetArch.None)
		{
			if (!forceRefresh && archiveContents != null) {
				return archiveContents;
			}

			if (String.IsNullOrEmpty (storeEntryPrefix)) {
				throw new ArgumentException (nameof (storeEntryPrefix), "must not be null or empty");
			}

			var entries = new List<string> ();
			using (var zip = ZipArchive.Open (archivePath, FileMode.Open)) {
				foreach (var entry in zip) {
					entries.Add (entry.FullName);
				}
			}

			archiveContents = entries;
			if (!useAssemblyStores) {
				Console.WriteLine ("Not using assembly stores");
				return entries;
			}

			Console.WriteLine ($"Creating AssemblyStoreExplorer for archive '{archivePath}'");
			(IList<AssemblyStoreExplorer>? explorers, string? errorMessage) = AssemblyStoreExplorer.Open (archivePath);
			AssemblyStoreExplorer? explorer = SelectExplorer (explorers, arch);
			Console.WriteLine ($"Explorer found {explorer.AssemblyCount} assemblies");

			foreach (AssemblyStoreItem asm in explorer.Assemblies) {
				string prefix = storeEntryPrefix;

				if (haveMultipleRids && asm.TargetArch != AndroidTargetArch.None) {
					string abi = MonoAndroidHelper.ArchToAbi (asm.TargetArch);
					prefix = $"{prefix}{abi}/";
				}

				entries.Add ($"{prefix}{asm.Name}.dll");
				if (asm.DebugOffset > 0) {
					entries.Add ($"{prefix}{asm.Name}.pdb");
				}

				if (asm.ConfigOffset > 0) {
					entries.Add ($"{prefix}{asm.Name}.dll.config");
				}
			}

			Console.WriteLine ("Archive entries with synthetised assembly storeReader entries:");
			foreach (string e in entries) {
				Console.WriteLine ($"  {e}");
			}

			return entries;
		}

		AssemblyStoreExplorer? SelectExplorer (IList<AssemblyStoreExplorer>? explorers, AndroidTargetArch arch)
		{
			if (explorers == null || explorers.Count == 0) {
				return null;
			}

			// If we don't care about target architecture, we check the first store, since all of them will have the same
			// assemblies. Otherwise we try to locate the correct store.
			if (arch == AndroidTargetArch.None) {
				return explorers[0];
			}

			foreach (AssemblyStoreExplorer e in explorers) {
				if (e.TargetArch == null || e.TargetArch != arch) {
					continue;
				}
				return e;
			}


			Console.WriteLine ($"Failed to find assembly store for architecture '{arch}' in archive '{archivePath}'");
			return null;
		}

		public int GetNumberOfAssemblies (bool countAbiAssembliesOnce = true, bool forceRefresh = false)
		{
			List<string> contents = ListArchiveContents (assembliesRootDir, forceRefresh);
			var dlls = contents.Where (x => x.EndsWith (".dll", StringComparison.OrdinalIgnoreCase));

			if (!countAbiAssembliesOnce) {
				return dlls.Count ();
			}

			var cache = new HashSet<string> (StringComparer.OrdinalIgnoreCase);
			return dlls.Where (x => {
				string name = Path.GetFileName (x);
				if (cache.Contains (name)) {
					return false;
				}

				cache.Add (name);
				return true;
			}).Count ();
		}

		public bool Exists (string entryPath, bool forceRefresh = false)
		{
			List<string> contents = ListArchiveContents (assembliesRootDir, forceRefresh);
			if (contents.Count == 0) {
				return false;
			}

			return contents.Contains (entryPath);
		}

		public void Contains (string[] fileNames, out List<string> existingFiles, out List<string> missingFiles, out List<string> additionalFiles, AndroidTargetArch arch = AndroidTargetArch.None)
		{
			if (fileNames == null) {
				throw new ArgumentNullException (nameof (fileNames));
			}

			if (fileNames.Length == 0) {
				throw new ArgumentException ("must not be empty", nameof (fileNames));
			}

			if (useAssemblyStores) {
				StoreContains (fileNames, out existingFiles, out missingFiles, out additionalFiles, arch);
			} else {
				ArchiveContains (fileNames, out existingFiles, out missingFiles, out additionalFiles, arch);
			}
		}

		void ArchiveContains (string[] fileNames, out List<string> existingFiles, out List<string> missingFiles, out List<string> additionalFiles, AndroidTargetArch arch)
		{
			using (var zip = ZipHelper.OpenZip (archivePath)) {
				existingFiles = zip.Where (a => a.FullName.StartsWith (assembliesRootDir, StringComparison.InvariantCultureIgnoreCase)).Select (a => a.FullName).ToList ();
				missingFiles = fileNames.Where (x => !zip.ContainsEntry (assembliesRootDir + x)).ToList ();
				additionalFiles = existingFiles.Where (x => !fileNames.Contains (x.Replace (assembliesRootDir, string.Empty))).ToList ();
			}
		}

		void StoreContains (string[] fileNames, out List<string> existingFiles, out List<string> missingFiles, out List<string> additionalFiles, AndroidTargetArch arch)
		{
			var assemblyNames = fileNames.Where (x => x.EndsWith (".dll", StringComparison.OrdinalIgnoreCase)).ToList ();
			var configFiles = fileNames.Where (x => x.EndsWith (".config", StringComparison.OrdinalIgnoreCase)).ToList ();
			var debugFiles = fileNames.Where (x => x.EndsWith (".pdb", StringComparison.OrdinalIgnoreCase)).ToList ();
			var otherFiles = fileNames.Where (x => !SpecialExtensions.Contains (Path.GetExtension (x))).ToList ();

			existingFiles = new List<string> ();
			missingFiles = new List<string> ();
			additionalFiles = new List<string> ();

			if (otherFiles.Count > 0) {
				using (var zip = ZipHelper.OpenZip (archivePath)) {
					foreach (string file in otherFiles) {
						string fullPath = assembliesRootDir + file;
						if (zip.ContainsEntry (fullPath)) {
							existingFiles.Add (file);
						}
					}
				}
			}

			(IList<AssemblyStoreExplorer>? explorers, string? errorMessage) = AssemblyStoreExplorer.Open (archivePath);
			AssemblyStoreExplorer? explorer = SelectExplorer (explorers, arch);
			if (explorer == null) {
				return;
			}

			foreach (var f in explorer.AssembliesByName) {
				Console.WriteLine ($"DEBUG!\tKey:{f.Key}");
			}

			if (explorer.AssembliesByName.Count != 0) {
				existingFiles.AddRange (explorer.AssembliesByName.Keys);

				// We need to fake config and debug files since they have no named entries in the storeReader
				foreach (string file in configFiles) {
					AssemblyStoreItem asm = GetStoreAssembly (file);
					if (asm == null) {
						continue;
					}

					if (asm.ConfigOffset > 0) {
						existingFiles.Add (file);
					}
				}

				foreach (string file in debugFiles) {
					AssemblyStoreItem asm = GetStoreAssembly (file);
					if (asm == null) {
						continue;
					}

					if (asm.DebugOffset > 0) {
						existingFiles.Add (file);
					}
				}
			}

			foreach (string file in fileNames) {
				if (existingFiles.Contains (file)) {
					continue;
				}
				missingFiles.Add (file);
			}

			additionalFiles = existingFiles.Where (x => !fileNames.Contains (x)).ToList ();

			AssemblyStoreItem GetStoreAssembly (string file)
			{
				string assemblyName = Path.GetFileNameWithoutExtension (file);
				if (!explorer.AssembliesByName.TryGetValue (assemblyName, out AssemblyStoreItem asm) || asm == null) {
					return null;
				}

				return asm;
			}
		}
	}
}
