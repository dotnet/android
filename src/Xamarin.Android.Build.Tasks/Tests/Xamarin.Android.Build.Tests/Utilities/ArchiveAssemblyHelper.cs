using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Xamarin.Android.AssemblyStore;
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
			".mdb",
		};

		static readonly Dictionary<string, string> ArchToAbi = new Dictionary<string, string> (StringComparer.OrdinalIgnoreCase) {
			{"x86", "x86"},
			{"x86_64", "x86_64"},
			{"armeabi_v7a", "armeabi-v7a"},
			{"arm64_v8a", "arm64-v8a"},
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

		public Stream ReadEntry (string path)
		{
			if (useAssemblyStores) {
				return ReadStoreEntry (path);
			}

			return ReadZipEntry (path);
		}

		Stream ReadZipEntry (string path)
		{
			using (var zip = ZipHelper.OpenZip (archivePath)) {
				ZipEntry entry = zip.ReadEntry (path);
				var ret = new MemoryStream ();
				entry.Extract (ret);
				ret.Flush ();
				return ret;
			}
		}

		Stream ReadStoreEntry (string path)
		{
			AssemblyStoreReader storeReader = null;
			AssemblyStoreAssembly assembly = null;
			string name = Path.GetFileNameWithoutExtension (path);
			var explorer = new AssemblyStoreExplorer (archivePath);

			foreach (var asm in explorer.Assemblies) {
				if (String.Compare (name, asm.Name, StringComparison.Ordinal) != 0) {
					continue;
				}
				assembly = asm;
				storeReader = asm.Store;
				break;
			}

			if (storeReader == null) {
				Console.WriteLine ($"Store for entry {path} not found, will try a standard Zip read");
				return ReadZipEntry (path);
			}

			string storeEntryName;
			if (String.IsNullOrEmpty (storeReader.Arch)) {
				storeEntryName = $"{assembliesRootDir}assemblies.blob";
			} else {
				storeEntryName = $"{assembliesRootDir}assemblies_{storeReader.Arch}.blob";
			}

			Stream store = ReadZipEntry (storeEntryName);
			if (store == null) {
				Console.WriteLine ($"Store zip entry {storeEntryName} does not exist");
				return null;
			}

			store.Seek (assembly.DataOffset, SeekOrigin.Begin);
			var ret = new MemoryStream ();
			byte[] buffer = buffers.Rent (AssemblyStoreReadBufferSize);
			int toRead = (int)assembly.DataSize;
			while (toRead > 0) {
				int nread = store.Read (buffer, 0, AssemblyStoreReadBufferSize);
				if (nread <= 0) {
					break;
				}

				ret.Write (buffer, 0, nread);
				toRead -= nread;
			}
			ret.Flush ();
			store.Dispose ();
			buffers.Return (buffer);

			return ret;
		}

		public List<string> ListArchiveContents (string storeEntryPrefix = DefaultAssemblyStoreEntryPrefix, bool forceRefresh = false)
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
			var explorer = new AssemblyStoreExplorer (archivePath);
			Console.WriteLine ($"Explorer found {explorer.Assemblies.Count} assemblies");
			foreach (var asm in explorer.Assemblies) {
				string prefix = storeEntryPrefix;

				if (haveMultipleRids && !String.IsNullOrEmpty (asm.Store.Arch)) {
					string arch = ArchToAbi[asm.Store.Arch];
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

			Console.WriteLine ("Archive entries with synthetised assembly storeReader entries:");
			foreach (string e in entries) {
				Console.WriteLine ($"  {e}");
			}

			return entries;
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

		public void Contains (string[] fileNames, out List<string> existingFiles, out List<string> missingFiles, out List<string> additionalFiles)
		{
			if (fileNames == null) {
				throw new ArgumentNullException (nameof (fileNames));
			}

			if (fileNames.Length == 0) {
				throw new ArgumentException ("must not be empty", nameof (fileNames));
			}

			if (useAssemblyStores) {
				StoreContains (fileNames, out existingFiles, out missingFiles, out additionalFiles);
			} else {
				ArchiveContains (fileNames, out existingFiles, out missingFiles, out additionalFiles);
			}
		}

		void ArchiveContains (string[] fileNames, out List<string> existingFiles, out List<string> missingFiles, out List<string> additionalFiles)
		{
			using (var zip = ZipHelper.OpenZip (archivePath)) {
				existingFiles = zip.Where (a => a.FullName.StartsWith (assembliesRootDir, StringComparison.InvariantCultureIgnoreCase)).Select (a => a.FullName).ToList ();
				missingFiles = fileNames.Where (x => !zip.ContainsEntry (assembliesRootDir + x)).ToList ();
				additionalFiles = existingFiles.Where (x => !fileNames.Contains (x.Replace (assembliesRootDir, string.Empty))).ToList ();
			}
		}

		void StoreContains (string[] fileNames, out List<string> existingFiles, out List<string> missingFiles, out List<string> additionalFiles)
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
						string fullPath = assembliesRootDir + file;
						if (zip.ContainsEntry (fullPath)) {
							existingFiles.Add (file);
						}
					}
				}
			}

			var explorer = new AssemblyStoreExplorer (archivePath, customLogger: (a, s) => {
				Console.WriteLine ($"DEBUG! {s}");
			});

			foreach (var f in explorer.AssembliesByName) {
				Console.WriteLine ($"DEBUG!\tKey:{f.Key}");
			}

			// Assembly stores don't store the assembly extension
			var storeAssemblies = explorer.AssembliesByName.Keys.Select (x => $"{x}.dll");
			if (explorer.AssembliesByName.Count != 0) {
				existingFiles.AddRange (storeAssemblies);

				// We need to fake config and debug files since they have no named entries in the storeReader
				foreach (string file in configFiles) {
					AssemblyStoreAssembly asm = GetStoreAssembly (file);
					if (asm == null) {
						continue;
					}

					if (asm.ConfigDataOffset > 0) {
						existingFiles.Add (file);
					}
				}

				foreach (string file in debugFiles) {
					AssemblyStoreAssembly asm = GetStoreAssembly (file);
					if (asm == null) {
						continue;
					}

					if (asm.DebugDataOffset > 0) {
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

			AssemblyStoreAssembly GetStoreAssembly (string file)
			{
				string assemblyName = Path.GetFileNameWithoutExtension (file);
				if (assemblyName.EndsWith (".dll", StringComparison.OrdinalIgnoreCase)) {
					assemblyName = Path.GetFileNameWithoutExtension (assemblyName);
				}

				if (!explorer.AssembliesByName.TryGetValue (assemblyName, out AssemblyStoreAssembly asm) || asm == null) {
					return null;
				}

				return asm;
			}
		}
	}
}
