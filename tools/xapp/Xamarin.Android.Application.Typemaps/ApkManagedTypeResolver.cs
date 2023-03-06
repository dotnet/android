using System;
using System.Collections.Generic;
using System.IO;

using K4os.Compression.LZ4;
using Mono.Cecil;
using Xamarin.Tools.Zip;

namespace tmt
{
	class ApkManagedTypeResolver : ManagedTypeResolver
	{
		const uint CompressedDataMagic = 0x5A4C4158; // 'XALZ', little-endian

		Dictionary<string, ZipEntry> assemblies;
		ZipArchive apk;

		public ApkManagedTypeResolver (ZipArchive apk, string assemblyEntryPrefix)
		{
			this.apk = apk;
			assemblies = new Dictionary<string, ZipEntry> (StringComparer.Ordinal);

			foreach (ZipEntry entry in apk) {
				if (!entry.FullName.StartsWith (assemblyEntryPrefix, StringComparison.Ordinal)) {
					continue;
				}

				if (!entry.FullName.EndsWith (".dll", StringComparison.Ordinal)) {
					continue;
				}

				assemblies.Add (Path.GetFileNameWithoutExtension (entry.FullName), entry);
				assemblies.Add (entry.FullName, entry);
			}
		}

		protected override string? FindAssembly (string assemblyName)
		{
			if (assemblies.Count == 0) {
				return null;
			}

			if (!assemblies.TryGetValue (assemblyName, out ZipEntry? entry) || entry == null) {
				return null;
			}

			return entry.FullName;
		}

		protected override AssemblyDefinition ReadAssembly (string assemblyPath)
		{
			if (!assemblies.TryGetValue (assemblyPath, out ZipEntry? entry) || entry == null) {
				// Should "never" happen - if the assembly wasn't there, FindAssembly should have returned `null`
				throw new InvalidOperationException ($"Should not happen: assembly {assemblyPath} not found in the APK archive.");
			}

			byte[]? assemblyBytes = null;
			var stream = new MemoryStream ();
			entry.Extract (stream);
			stream.Seek (0, SeekOrigin.Begin);

			//
			// LZ4 compressed assembly header format:
			//   uint magic;                 // 0x5A4C4158; 'XALZ', little-endian
			//   uint descriptor_index;      // Index into an internal assembly descriptor table
			//   uint uncompressed_length;   // Size of assembly, uncompressed
			//
			using (var reader = new BinaryReader (stream)) {
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
