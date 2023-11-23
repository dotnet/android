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
			Stream? ret;
			if (useAssemblyStores) {
				ret = ReadStoreEntry (path, arch, uncompressIfNecessary);
			} else {
				ret = ReadZipEntry (path, arch, uncompressIfNecessary);
			}

			ret?.Seek (0, SeekOrigin.Begin);
			return ret;
		}

		Stream? ReadZipEntry (string path, AndroidTargetArch arch, bool uncompressIfNecessary)
		{
			List<string>? potentialEntries = TransformArchiveAssemblyPath (path, arch);
			if (potentialEntries == null || potentialEntries.Count == 0) {
				return null;
			}

			using var zip = ZipHelper.OpenZip (archivePath);
			foreach (string assemblyPath in potentialEntries) {
				if (!zip.ContainsEntry (assemblyPath)) {
					continue;
				}

				ZipEntry entry = zip.ReadEntry (assemblyPath);
				var ret = new MemoryStream ();
				entry.Extract (ret);
				ret.Flush ();
				return ret;
			}

			return null;
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

			if (arch == AndroidTargetArch.None) {
				if (explorer.TargetArch == null) {
					throw new InvalidOperationException ($"Internal error: explorer should not have its TargetArch unset");
				}

				arch = (AndroidTargetArch)explorer.TargetArch;
			}

			Console.WriteLine ($"Trying to read store entry: {name}");
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

			return explorer.ReadImageData (assembly, uncompressIfNecessary);
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

			if (arch == AndroidTargetArch.None) {
				if (explorers == null || explorers.Count == 0) {
					return entries;
				}

				foreach (AssemblyStoreExplorer? explorer in explorers) {
					SynthetizeAssemblies (explorer);
				}
			} else {
				SynthetizeAssemblies (SelectExplorer (explorers, arch));
			}

			Console.WriteLine ("Archive entries with synthetised assembly storeReader entries:");
			foreach (string e in entries) {
				Console.WriteLine ($"  {e}");
			}

			return entries;

			void SynthetizeAssemblies (AssemblyStoreExplorer? explorer)
			{
				if (explorer == null) {
					return;
				}

				Console.WriteLine ($"Explorer for {explorer.TargetArch} found {explorer.AssemblyCount} assemblies");
				foreach (AssemblyStoreItem asm in explorer.Assemblies) {
					string prefix = storeEntryPrefix;
					string abi = MonoAndroidHelper.ArchToAbi (asm.TargetArch);
					prefix = $"{prefix}{abi}/";

					entries.Add ($"{prefix}{asm.Name}");
					if (asm.DebugOffset > 0) {
						entries.Add ($"{prefix}{Path.GetFileNameWithoutExtension (asm.Name)}.pdb");
					}

					if (asm.ConfigOffset > 0) {
						entries.Add ($"{prefix}{asm.Name}.config");
					}
				}
			}
		}

		AssemblyStoreExplorer? SelectExplorer (IList<AssemblyStoreExplorer>? explorers, string rid)
		{
			return SelectExplorer (explorers, MonoAndroidHelper.RidToArch (rid));
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

		public int GetNumberOfAssemblies (bool countAbiAssembliesOnce = true, bool forceRefresh = false, AndroidTargetArch arch = AndroidTargetArch.None)
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

		/// <summary>
		/// Takes "old style" `assemblies/assembly.dll` path and returns (if possible) a set of paths that reflect the new
		/// location of `assemblies/{ARCH}/assembly.dll`. A list is returned because, if `arch` is `None`, we'll return all
		/// the possible architectural paths.
		/// An exception is thrown if we cannot transform the path for some reason. It should **not** be handled.
		/// </summary>
		static List<string>? TransformArchiveAssemblyPath (string path, AndroidTargetArch arch)
		{
			const string AssembliesPath = "assemblies";
			const string AssembliesPathTerminated = AssembliesPath + "/";

			if (String.IsNullOrEmpty (path)) {
				throw new ArgumentException (nameof (path), "must not be null or empty");
			}

			if (!path.StartsWith (AssembliesPathTerminated, StringComparison.Ordinal)) {
				return new List<string> { path };
			}

			string[] parts = path.Split ('/');
			if (parts.Length < 2) {
				throw new InvalidOperationException ($"Path '{path}' must consist of at least two segments separated by `/`");
			}

			// We accept:
			//   assemblies/assembly.dll
			//   assemblies/{CULTURE}/assembly.dll
			//   assemblies/{ARCH}/assembly.dll
			//   assemblies/{ARCH}/{CULTURE}/assembly.dll
			if (parts.Length > 4) {
				throw new InvalidOperationException ($"Path '{path}' must not consist of more than 4 segments separated by `/`");
			}

			var ret = new List<string> ();
			if (parts.Length == 4) {
				// It's a full satellite assembly path that includes the ABI, no need to change anything
				ret.Add (path);
				return ret;
			}

			if (parts.Length == 3) {
				// We need to check whether the middle part is a culture or an ABI
				if (MonoAndroidHelper.IsValidAbi (parts[1])) {
					// Nothing more to do
					ret.Add (path);
					return ret;
				}
			}

			// We need to add the ABI(s)
			var newParts = new List<string> {
				String.Empty, // ABI placeholder
			};

			for (int i = 1; i < parts.Length; i++) {
				newParts.Add (parts[i]);
			}

			if (arch != AndroidTargetArch.None) {
				ret.Add (MakeAbiArchivePath (arch));
			} else {
				foreach (AndroidTargetArch targetArch in MonoAndroidHelper.SupportedTargetArchitectures) {
					ret.Add (MakeAbiArchivePath (targetArch));
				}
			}

			return ret;

			string MakeAbiArchivePath (AndroidTargetArch targetArch)
			{
				newParts[0] = MonoAndroidHelper.ArchToAbi (targetArch);
				return MonoAndroidHelper.MakeZipArchivePath (AssembliesPath, newParts);
			}
		}

		static bool ArchiveContains (List<string> archiveContents, string entryPath, AndroidTargetArch arch)
		{
			if (archiveContents.Count == 0) {
				return false;
			}

			List<string>? potentialEntries = TransformArchiveAssemblyPath (entryPath, arch);
			if (potentialEntries == null || potentialEntries.Count == 0) {
				return false;
			}

			foreach (string existingEntry in archiveContents) {
				foreach (string wantedEntry in potentialEntries) {
					if (String.Compare (existingEntry, wantedEntry, StringComparison.Ordinal) == 0) {
						return true;
					}
				}
			}

			return false;
		}

		public bool Exists (string entryPath, bool forceRefresh = false, AndroidTargetArch arch = AndroidTargetArch.None)
		{
			return ArchiveContains (ListArchiveContents (assembliesRootDir, forceRefresh), entryPath, arch);
		}

		public void Contains (ICollection<string> fileNames, out List<string> existingFiles, out List<string> missingFiles, out List<string> additionalFiles, IEnumerable<AndroidTargetArch>? targetArches = null)
		{
			if (fileNames == null) {
				throw new ArgumentNullException (nameof (fileNames));
			}

			if (fileNames.Count == 0) {
				throw new ArgumentException ("must not be empty", nameof (fileNames));
			}

			if (useAssemblyStores) {
				StoreContains (fileNames, out existingFiles, out missingFiles, out additionalFiles, targetArches);
			} else {
				ArchiveContains (fileNames, out existingFiles, out missingFiles, out additionalFiles, targetArches);
			}
		}

		List<AndroidTargetArch> GetSupportedArches (IEnumerable<AndroidTargetArch>? runtimeIdentifiers)
		{
			var rids = new List<AndroidTargetArch> ();
			if (runtimeIdentifiers != null) {
				rids.AddRange (runtimeIdentifiers);
			}

			if (rids.Count == 0) {
				rids.AddRange (MonoAndroidHelper.SupportedTargetArchitectures);
			}

			return rids;
		}

		void ListFiles (List<string> existingFiles, List<string> missingFiles, List<string> additionalFiles)
		{
			Console.WriteLine ("Archive contents:");
			ListFiles ("existing files", existingFiles);
			ListFiles ("missing files", missingFiles);
			ListFiles ("additional files", additionalFiles);

			void ListFiles (string label, List<string> list)
			{
				Console.WriteLine ($"  {label}:");
				if (list.Count == 0) {
					Console.WriteLine ("    none");
					return;
				}

				foreach (string file in list) {
					Console.WriteLine ($"    {file}");
				}
			}
		}

		(string prefixAssemblies, string prefixLib) GetArchivePrefixes (string abi) => ($"{MonoAndroidHelper.MakeZipArchivePath (assembliesRootDir, abi)}/", $"lib/{abi}/");

		void ArchiveContains (ICollection<string> fileNames, out List<string> existingFiles, out List<string> missingFiles, out List<string> additionalFiles, IEnumerable<AndroidTargetArch>? targetArches = null)
		{
			using var zip = ZipHelper.OpenZip (archivePath);
			existingFiles = zip.Where (a => a.FullName.StartsWith (assembliesRootDir, StringComparison.InvariantCultureIgnoreCase)).Select (a => a.FullName).ToList ();
			existingFiles.AddRange (zip.Where (a => a.FullName.StartsWith ("lib/", StringComparison.OrdinalIgnoreCase)).Select (a => a.FullName));

			List<AndroidTargetArch> arches = GetSupportedArches (targetArches);

			missingFiles = new List<string> ();
			additionalFiles = new List<string> ();
			foreach (AndroidTargetArch arch in arches) {
				string abi = MonoAndroidHelper.ArchToAbi (arch);
				missingFiles.AddRange (GetMissingFilesForAbi (abi));
				additionalFiles.AddRange (GetAdditionalFilesForAbi (abi, existingFiles));
			}
			ListFiles (existingFiles, missingFiles, additionalFiles);

			IEnumerable<string> GetMissingFilesForAbi (string abi)
			{
				(string prefixAssemblies, string prefixLib) = GetArchivePrefixes (abi);
				return fileNames.Where (x => !zip.ContainsEntry (MonoAndroidHelper.MakeZipArchivePath (prefixAssemblies, x)) && !zip.ContainsEntry (MonoAndroidHelper.MakeZipArchivePath (prefixLib, x)));
			}

			IEnumerable<string> GetAdditionalFilesForAbi (string abi, List<string> existingFiles)
			{
				(string prefixAssemblies, string prefixLib) = GetArchivePrefixes (abi);
				return existingFiles.Where (x => !fileNames.Contains (x.Replace (prefixAssemblies, string.Empty)) && !fileNames.Contains (x.Replace (prefixLib, String.Empty)));
			}
		}

		void StoreContains (ICollection<string> fileNames, out List<string> existingFiles, out List<string> missingFiles, out List<string> additionalFiles, IEnumerable<AndroidTargetArch>? targetArches = null)
		{
			var assemblyNames = fileNames.Where (x => x.EndsWith (".dll", StringComparison.OrdinalIgnoreCase)).ToList ();
			var configFiles = fileNames.Where (x => x.EndsWith (".config", StringComparison.OrdinalIgnoreCase)).ToList ();
			var debugFiles = fileNames.Where (x => x.EndsWith (".pdb", StringComparison.OrdinalIgnoreCase)).ToList ();
			var otherFiles = fileNames.Where (x => !SpecialExtensions.Contains (Path.GetExtension (x))).ToList ();

			existingFiles = new List<string> ();
			missingFiles = new List<string> ();
			additionalFiles = new List<string> ();

			using ZipArchive? zip = ZipHelper.OpenZip (archivePath);

			List<AndroidTargetArch> arches = GetSupportedArches (targetArches);
			(IList<AssemblyStoreExplorer>? explorers, string? errorMessage) = AssemblyStoreExplorer.Open (archivePath);

			foreach (AndroidTargetArch arch in arches) {
				AssemblyStoreExplorer? explorer = SelectExplorer (explorers, arch);
				if (explorer == null) {
					continue;
				}

				if (otherFiles.Count > 0) {
					(string prefixAssemblies, string prefixLib) = GetArchivePrefixes (MonoAndroidHelper.ArchToAbi (arch));

					foreach (string file in otherFiles) {
						string fullPath = prefixAssemblies + file;
						if (zip.ContainsEntry (fullPath)) {
							existingFiles.Add (file);
						}

						fullPath = prefixLib + file;
						if (zip.ContainsEntry (fullPath)) {
							existingFiles.Add (file);
						}
					}
				}

				foreach (var f in explorer.AssembliesByName) {
					Console.WriteLine ($"DEBUG!\tKey:{f.Key}");
				}

				if (explorer.AssembliesByName.Count != 0) {
					existingFiles.AddRange (explorer.AssembliesByName.Keys);

					// We need to fake config and debug files since they have no named entries in the storeReader
					foreach (string file in configFiles) {
						AssemblyStoreItem asm = GetStoreAssembly (explorer, file);
						if (asm == null) {
							continue;
						}

						if (asm.ConfigOffset > 0) {
							existingFiles.Add (file);
						}
					}

					foreach (string file in debugFiles) {
						AssemblyStoreItem asm = GetStoreAssembly (explorer, file);
						if (asm == null) {
							continue;
						}

						if (asm.DebugOffset > 0) {
							existingFiles.Add (file);
						}
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
			ListFiles (existingFiles, missingFiles, additionalFiles);

			AssemblyStoreItem GetStoreAssembly (AssemblyStoreExplorer explorer, string file)
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
