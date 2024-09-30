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
				assembliesRootDir = "base/lib/";
			} else if (String.Compare (".apk", extension, StringComparison.OrdinalIgnoreCase) == 0) {
				assembliesRootDir = "lib/";
			} else if (String.Compare (".zip", extension, StringComparison.OrdinalIgnoreCase) == 0) {
				assembliesRootDir = "lib/";
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

			if (ret == null) {
				return null;
			}

			ret.Flush ();
			ret.Seek (0, SeekOrigin.Begin);
			(ulong elfPayloadOffset, ulong elfPayloadSize, ELFPayloadError error) = Xamarin.Android.AssemblyStore.Utils.FindELFPayloadSectionOffsetAndSize (ret);

			if (error != ELFPayloadError.None) {
				string message = error switch {
					ELFPayloadError.NotELF           => $"Entry '{path}' is not a valid ELF binary",
					ELFPayloadError.LoadFailed       => $"Entry '{path}' could not be loaded",
					ELFPayloadError.NotSharedLibrary => $"Entry '{path}' is not a shared ELF library",
					ELFPayloadError.NotLittleEndian  => $"Entry '{path}' is not a little-endian ELF image",
					ELFPayloadError.NoPayloadSection => $"Entry '{path}' does not contain the 'payload' section",
					_                                => $"Unknown ELF payload section error for entry '{path}': {error}"
				};
				Console.WriteLine (message);
			} else {
				Console.WriteLine ($"Extracted content from ELF image '{path}'");
			}

			if (elfPayloadOffset == 0) {
				ret.Seek (0, SeekOrigin.Begin);
				return ret;
			}

			// Make a copy of JUST the payload section, so that it contains only the data the tests expect and support
			var payload = new MemoryStream ();
			var data = buffers.Rent (16384);
			int toRead = data.Length;
			int nRead = 0;
			ulong remaining = elfPayloadSize;

			ret.Seek ((long)elfPayloadOffset, SeekOrigin.Begin);
			while (remaining > 0 && (nRead = ret.Read (data, 0, toRead)) > 0) {
				payload.Write (data, 0, nRead);
				remaining -= (ulong)nRead;

				if (remaining < (ulong)data.Length) {
					// Make sure the last chunk doesn't gobble in more than we need
					toRead = (int)remaining;
				}
			}
			buffers.Return (data);

			payload.Flush ();
			ret.Dispose ();

			payload.Seek (0, SeekOrigin.Begin);
			return payload;
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

					int cultureIndex = asm.Name.IndexOf ('/');
					string? culture = null;
					string name;

					if (cultureIndex > 0) {
						culture = asm.Name.Substring (0, cultureIndex);
						name = asm.Name.Substring (cultureIndex + 1);
					} else {
						name = asm.Name;
					}

					// Mangle name in in the same fashion the discrete assembly entries are named, makes other
					// code in this class simpler.
					string mangledName = MonoAndroidHelper.MakeDiscreteAssembliesEntryName (name, culture);
					entries.Add ($"{prefix}{mangledName}");
					if (asm.DebugOffset > 0) {
						mangledName = MonoAndroidHelper.MakeDiscreteAssembliesEntryName (Path.ChangeExtension (name, "pdb"));
						entries.Add ($"{prefix}{mangledName}");
					}

					if (asm.ConfigOffset > 0) {
						mangledName = MonoAndroidHelper.MakeDiscreteAssembliesEntryName (Path.ChangeExtension (name, "config"));
						entries.Add ($"{prefix}{mangledName}");
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

		public int GetNumberOfAssemblies (bool forceRefresh = false, AndroidTargetArch arch = AndroidTargetArch.None)
		{
			List<string> contents = ListArchiveContents (assembliesRootDir, forceRefresh, arch);

			// We must count only .dll.so entries starting with the '-' and '_' characters, as they are the actual managed assemblies.
			// Other entries in `lib/{arch}` might be AOT shared libraries, which will also have the .dll.so extension.
			var dlls = contents.Where (x => {
				string fileName = Path.GetFileName (x);
				if (!fileName.EndsWith (".dll.so", StringComparison.OrdinalIgnoreCase)) {
					return false;
				}

				return fileName.StartsWith (MonoAndroidHelper.MANGLED_ASSEMBLY_REGULAR_ASSEMBLY_MARKER, StringComparison.OrdinalIgnoreCase) ||
				       fileName.StartsWith (MonoAndroidHelper.MANGLED_ASSEMBLY_SATELLITE_ASSEMBLY_MARKER, StringComparison.OrdinalIgnoreCase);
			});

			return dlls.Count ();
		}

		/// <summary>
		/// Takes "old style" `assemblies/assembly.dll` path and returns (if possible) a set of paths that reflect the new
		/// location of `lib/{ARCH}/assembly.dll.so`. A list is returned because, if `arch` is `None`, we'll return all
		/// the possible architectural paths.
		/// An exception is thrown if we cannot transform the path for some reason. It should **not** be handled.
		/// </summary>
		static List<string>? TransformArchiveAssemblyPath (string path, AndroidTargetArch arch)
		{
			if (String.IsNullOrEmpty (path)) {
				throw new ArgumentException (nameof (path), "must not be null or empty");
			}

			if (!path.StartsWith ("assemblies/", StringComparison.Ordinal)) {
				return new List<string> { path };
			}

			string[] parts = path.Split ('/');
			if (parts.Length < 2) {
				throw new InvalidOperationException ($"Path '{path}' must consist of at least two segments separated by `/`");
			}

			// We accept:
			//   assemblies/assembly.dll
			//   assemblies/{CULTURE}/assembly.dll
			//   assemblies/{ABI}/assembly.dll
			//   assemblies/{ABI}/{CULTURE}/assembly.dll
			if (parts.Length > 4) {
				throw new InvalidOperationException ($"Path '{path}' must not consist of more than 4 segments separated by `/`");
			}

			string? fileName = null;
			string? culture = null;
			string? abi = null;

			switch (parts.Length) {
				// Full satellite assembly path, with abi
				case 4:
					abi = parts[1];
					culture = parts[2];
					fileName = parts[3];
					break;

				// Assembly path with abi or culture
				case 3:
					// If the middle part isn't a valid abi, we treat it as a culture name
					if (MonoAndroidHelper.IsValidAbi (parts[1])) {
						abi = parts[1];
					} else {
						culture = parts[1];
					}
					fileName = parts[2];
					break;

				// Assembly path without abi or culture
				case 2:
					fileName = parts[1];
					break;
			}

			string fileTypeMarker = MonoAndroidHelper.MANGLED_ASSEMBLY_REGULAR_ASSEMBLY_MARKER;
			var abis = new List<string> ();
			if (!String.IsNullOrEmpty (abi)) {
				abis.Add (abi);
			} else if (arch == AndroidTargetArch.None) {
				foreach (AndroidTargetArch targetArch in MonoAndroidHelper.SupportedTargetArchitectures) {
					abis.Add (MonoAndroidHelper.ArchToAbi (targetArch));
				}
			} else {
				abis.Add (MonoAndroidHelper.ArchToAbi (arch));
			}

			if (!String.IsNullOrEmpty (culture)) {
				// Android doesn't allow us to put satellite assemblies in lib/{CULTURE}/assembly.dll.so, we must instead
				// mangle the name.
				fileTypeMarker = MonoAndroidHelper.MANGLED_ASSEMBLY_SATELLITE_ASSEMBLY_MARKER;
				fileName = $"{culture}-{fileName}";
			}

			var ret = new List<string> ();
			var newParts = new List<string> {
				String.Empty, // ABI placeholder
				$"{fileTypeMarker}{fileName}.so",
			};

			foreach (string a in abis) {
				newParts[0] = a;
				ret.Add (MonoAndroidHelper.MakeZipArchivePath ("lib", newParts));
			}

			return ret;
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

			foreach (string wantedEntry in potentialEntries) {
				Console.WriteLine ($"Wanted entry: {wantedEntry}");
				foreach (string existingEntry in archiveContents) {
					if (String.Compare (existingEntry, wantedEntry, StringComparison.Ordinal) == 0) {
						return true;
					}
				}
			}

			return false;
		}

		/// <summary>
		/// Checks whether <paramref name="entryPath"/> exists in the archive or assembly store.  The path should use the
		/// "old style" `assemblies/{ABI}/assembly.dll` format.
		/// </summary>
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
				return fileNames.Where (x => {
					string? culture = null;
					string fileName = x;
					int slashIndex = x.IndexOf ('/');
					if (slashIndex > 0) {
						culture = x.Substring (0, slashIndex);
						fileName = x.Substring (slashIndex + 1);
					}

					return !zip.ContainsEntry (MonoAndroidHelper.MakeZipArchivePath (prefixAssemblies, x)) &&
					       !zip.ContainsEntry (MonoAndroidHelper.MakeZipArchivePath (prefixLib, x)) &&
					       !zip.ContainsEntry (MonoAndroidHelper.MakeZipArchivePath (prefixAssemblies, MonoAndroidHelper.MakeDiscreteAssembliesEntryName (fileName, culture))) &&
					       !zip.ContainsEntry (MonoAndroidHelper.MakeZipArchivePath (prefixLib, MonoAndroidHelper.MakeDiscreteAssembliesEntryName (fileName, culture)));
				});
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
