using System;
using System.Collections.Generic;
using System.IO;

using K4os.Compression.LZ4;
using Mono.Cecil;
using Xamarin.Android.AssemblyStore;
using Xamarin.Tools.Zip;

namespace tmt
{
	class ApkManagedTypeResolver : ManagedTypeResolver
	{
		const uint CompressedDataMagic = 0x5A4C4158; // 'XALZ', little-endian

		readonly Dictionary<string, ZipEntry>? individualAssemblies;
		readonly Dictionary<string, AssemblyStoreAssembly>? blobAssemblies;
		readonly ZipArchive apk;
		readonly AssemblyStoreExplorer? assemblyStoreExplorer;

		public ApkManagedTypeResolver (ZipArchive apk, string assemblyEntryPrefix)
		{
			this.apk = apk;

			if (apk.ContainsEntry ($"{assemblyEntryPrefix}assemblies.blob")) {
				blobAssemblies = new Dictionary<string, AssemblyStoreAssembly> (StringComparer.Ordinal);
				assemblyStoreExplorer = new AssemblyStoreExplorer (apk, assemblyEntryPrefix, keepStoreInMemory: true);
				LoadAssemblyBlobs (apk, assemblyEntryPrefix, assemblyStoreExplorer);
			} else {
				individualAssemblies = new Dictionary<string, ZipEntry> (StringComparer.Ordinal);
				LoadIndividualAssemblies (apk, assemblyEntryPrefix);
			}
		}

		void LoadAssemblyBlobs (ZipArchive apkArchive, string assemblyEntryPrefix, AssemblyStoreExplorer explorer)
		{
			foreach (AssemblyStoreAssembly assembly in explorer.Assemblies) {
				string assemblyName = assembly.Name;
				string dllName = assembly.DllName;

				if (!String.IsNullOrEmpty (assembly.Store.Arch)) {
					assemblyName = $"{assembly.Store.Arch}/{assemblyName}";
					dllName = $"{assembly.Store.Arch}/{dllName}";
				}

				blobAssemblies!.Add (assemblyName, assembly);
				blobAssemblies!.Add (dllName, assembly);
			}
		}

		void LoadIndividualAssemblies (ZipArchive apkArchive, string assemblyEntryPrefix)
		{
			foreach (ZipEntry entry in apkArchive) {
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

				individualAssemblies!.Add (name, entry);
				individualAssemblies.Add (entry.FullName, entry);
			}
		}

		protected override string? FindAssembly (string assemblyName)
		{
			if (individualAssemblies != null) {
				if (individualAssemblies.Count == 0) {
					return null;
				}

				if (!individualAssemblies.TryGetValue (assemblyName, out ZipEntry? entry) || entry == null) {
					return null;
				}

				return entry.FullName;
			}

			if (blobAssemblies == null || !blobAssemblies.TryGetValue (assemblyName, out AssemblyStoreAssembly? assembly) || assembly == null) {
				return null;
			}

			return assembly.Name;
		}

		Stream GetAssemblyStream (string assemblyPath)
		{
			MemoryStream? stream = null;
			if (individualAssemblies != null) {
				if (!individualAssemblies.TryGetValue (assemblyPath, out ZipEntry? entry) || entry == null) {
					// Should "never" happen - if the assembly wasn't there, FindAssembly should have returned `null`
					throw new InvalidOperationException ($"Should not happen: assembly '{assemblyPath}' not found in the APK archive.");
				}

				stream = new MemoryStream ();
				entry.Extract (stream);
				return PrepStream (stream);
			}

			if (blobAssemblies == null) {
				throw new InvalidOperationException ("Internal error: blobAssemblies shouldn't be null");
			}

			if (blobAssemblies == null || !blobAssemblies.TryGetValue (assemblyPath, out AssemblyStoreAssembly? assembly) || assembly == null) {
				// Should "never" happen - if the assembly wasn't there, FindAssembly should have returned `null`
				throw new InvalidOperationException ($"Should not happen: assembly '{assemblyPath}' not found in the assembly blob.");
			}

			stream = new MemoryStream ();
                        assembly.ExtractImage (stream);

			return PrepStream (stream);

			Stream PrepStream (Stream stream)
			{
				stream.Seek (0, SeekOrigin.Begin);
				return stream;
			}
		}

		protected override AssemblyDefinition ReadAssembly (string assemblyPath)
		{
			byte[]? assemblyBytes = null;
			Stream stream = GetAssemblyStream (assemblyPath);

			//
			// LZ4 compressed assembly header format:
			//   uint magic;                 // 0x5A4C4158; 'XALZ', little-endian
			//   uint descriptor_index;      // Index into an internal assembly descriptor table
			//   uint uncompressed_length;   // Size of assembly, uncompressed
			//
			using var reader = new BinaryReader (stream);
			uint magic = reader.ReadUInt32 ();
			if (magic == CompressedDataMagic) {
				reader.ReadUInt32 (); // descriptor index, ignore
				uint decompressedLength = reader.ReadUInt32 ();

				int inputLength = (int)(stream.Length - 12);
				byte[] sourceBytes = Utilities.BytePool.Rent (inputLength);
				reader.Read (sourceBytes, 0, inputLength);

				assemblyBytes = Utilities.BytePool.Rent ((int)decompressedLength);
				int decoded = LZ4Codec.Decode (sourceBytes, 0, inputLength, assemblyBytes, 0, (int)decompressedLength);
				if (decoded != (int)decompressedLength) {
					throw new InvalidOperationException ($"Failed to decompress LZ4 data of {assemblyPath} (decoded: {decoded})");
				}
				Utilities.BytePool.Return (sourceBytes);
			}


			if (assemblyBytes != null) {
				stream.Close ();
				stream.Dispose ();
				stream = new MemoryStream ();
				stream.Write (assemblyBytes, 0, assemblyBytes.Length);
				Utilities.BytePool.Return (assemblyBytes);
				stream.Seek (0, SeekOrigin.Begin);
			}

			return AssemblyDefinition.ReadAssembly (stream);
		}
	}
}
