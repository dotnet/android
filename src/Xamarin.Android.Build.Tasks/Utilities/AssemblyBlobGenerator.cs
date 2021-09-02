using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tasks
{
	class AssemblyBlobGenerator
	{
		const uint BlobMagic = 0x41424158; // 'XABA', little-endian

		sealed class BlobIndexEntry
		{
			public int Index;

			public ulong NameHash64;
			public uint NameHash32;

			public uint DataOffset;
			public uint DataSize;

			public uint DebugDataOffset;
			public uint DebugDataSize;

			public uint ConfigDataOffset;
			public uint ConfigDataSize;
		};

		const string BlobPrefix = "assemblies";
		const string BlobExtension = ".blob";

		readonly string archiveAssembliesPrefix;
		readonly TaskLoggingHelper log;

		readonly List<BlobAssemblyInfo> commonAssemblies;
		readonly Dictionary<string, List<BlobAssemblyInfo>> archAssemblies;

		public AssemblyBlobGenerator (string archiveAssembliesPrefix, TaskLoggingHelper log)
		{
			if (String.IsNullOrEmpty (archiveAssembliesPrefix)) {
				throw new ArgumentException ("must not be null or empty", nameof (archiveAssembliesPrefix));
			}

			this.archiveAssembliesPrefix = archiveAssembliesPrefix;
			this.log = log;

			commonAssemblies = new List <BlobAssemblyInfo> ();
			archAssemblies = new Dictionary<string, List<BlobAssemblyInfo>> (StringComparer.OrdinalIgnoreCase);
		}

		public void Add (BlobAssemblyInfo blobAssembly)
		{
			if (String.IsNullOrEmpty (blobAssembly.Abi)) {
				log.LogMessage (MessageImportance.Low, $"AssemblyBlobGenerator: adding common assembly {blobAssembly.FilesystemAssemblyPath}");
				commonAssemblies.Add (blobAssembly);
				return;
			}

			if (!archAssemblies.ContainsKey (blobAssembly.Abi)) {
				archAssemblies.Add (blobAssembly.Abi, new List<BlobAssemblyInfo> ());
			}

			log.LogMessage (MessageImportance.Low, $"AssemblyBlobGenerator: adding arch '{blobAssembly.Abi}' assembly {blobAssembly.FilesystemAssemblyPath}");
			archAssemblies[blobAssembly.Abi].Add (blobAssembly);
		}

		public void Generate (string outputDirectory)
		{
			if (commonAssemblies.Count > 0) {
				Generate (Path.Combine (outputDirectory, $"{BlobPrefix}{BlobExtension}"), commonAssemblies);
			}

			if (archAssemblies.Count == 0) {
				return;
			}

			foreach (var kvp in archAssemblies) {
				string abi = kvp.Key;
				List<BlobAssemblyInfo> assemblies = kvp.Value;

				if (assemblies.Count == 0) {
					continue;
				}

				Generate (Path.Combine (outputDirectory, $"{BlobPrefix}_{abi}{BlobExtension}"), assemblies);
			}
		}

		void Generate (string outputFilePath, List<BlobAssemblyInfo> assemblies)
		{
			log.LogMessage (MessageImportance.Low, $"AssemblyBlobGenerator: generating blob: {outputFilePath}");
			// TODO: test with satellite assemblies, their name must include the culture prefix

			using (var fs = File.Open (outputFilePath, FileMode.Create, FileAccess.Write, FileShare.Read)) {
				using (var writer = new BinaryWriter (fs, Encoding.UTF8)) {
					Generate (writer, assemblies);
					writer.Flush ();
				}
			}
		}

		void Generate (BinaryWriter writer, List<BlobAssemblyInfo> assemblies)
		{
			Encoding assemblyNameEncoding = Encoding.UTF8;
			int assemblyNameWidth = 0;
			var names = new List<string> ();

			Action<string> updateNameWidth = (string assemblyName) => {
				int nameBytes = assemblyNameEncoding.GetBytes (assemblyName).Length;
				if (nameBytes > assemblyNameWidth) {
					assemblyNameWidth = nameBytes;
				}
			};

			var index = new List<BlobIndexEntry> ();
			foreach (BlobAssemblyInfo assembly in assemblies) {
				log.LogMessage (MessageImportance.Low, $"AssemblyBlobGenerator: assembly fs path == '{assembly.FilesystemAssemblyPath}'; assembly archive path == '{assembly.ArchiveAssemblyPath}'");
				string assemblyName = Path.GetFileNameWithoutExtension (assembly.FilesystemAssemblyPath);
				if (assemblyName.EndsWith (".dll", StringComparison.OrdinalIgnoreCase)) {
					assemblyName = Path.GetFileNameWithoutExtension (assemblyName);
				}

				string archivePath = assembly.ArchiveAssemblyPath;
				if (archivePath.StartsWith (archiveAssembliesPrefix, StringComparison.OrdinalIgnoreCase)) {
					archivePath = archivePath.Substring (archiveAssembliesPrefix.Length);
				}

				if (!String.IsNullOrEmpty (assembly.Abi)) {
					string abiPath = $"{assembly.Abi}/";
					if (archivePath.StartsWith (abiPath, StringComparison.Ordinal)) {
						archivePath = archivePath.Substring (abiPath.Length);
					}
				}

				if (!String.IsNullOrEmpty (archivePath)) {
					assemblyName = $"{archivePath}/{assemblyName}";
				}

				log.LogMessage (MessageImportance.Low, $"   => assemblyName == '{assemblyName}'; archivePath == '{archivePath}'");
				updateNameWidth (assemblyName);

				names.Add (assemblyName);
				assembly.NameIndex = names.Count - 1;

				BlobIndexEntry entry = WriteAssembly (writer, assembly, assemblyName);
				index.Add (entry);
				entry.Index = index.Count - 1;
			}

			writer.Flush ();
			writer.Seek (0, SeekOrigin.Begin);

			// Header, must be identical to the BundledAssemblyBlobHeader structure in src/monodroid/jni/xamarin-app.hh
			writer.Write (BlobMagic);               // magic
			writer.Write ((uint)index.Count);       // entry_count
			writer.Write ((uint)assemblyNameWidth); // name_width

			var sortedIndex = new List<BlobIndexEntry> (index);
			sortedIndex.Sort ((BlobIndexEntry a, BlobIndexEntry b) => a.NameHash32.CompareTo (b.NameHash32));
			foreach (BlobIndexEntry entry in sortedIndex) {
				writer.Write (entry.NameHash32);
				writer.Write (entry.Index);
			}

			sortedIndex.Sort ((BlobIndexEntry a, BlobIndexEntry b) => a.NameHash64.CompareTo (b.NameHash64));
			foreach (BlobIndexEntry entry in sortedIndex) {
				writer.Write (entry.NameHash64);
				writer.Write (entry.Index);
			}

			foreach (BlobIndexEntry entry in index) {
				writer.Write (entry.DataOffset);
				writer.Write (entry.DataSize);
				writer.Write (entry.DebugDataOffset);
				writer.Write (entry.DebugDataSize);
				writer.Write (entry.ConfigDataOffset);
				writer.Write (entry.ConfigDataSize);
			}

			// TODO: write the names array
		}

		BlobIndexEntry WriteAssembly (BinaryWriter writer, BlobAssemblyInfo assembly, string assemblyName)
		{
			var ret = new BlobIndexEntry ();

			return ret;
		}
	}
}
