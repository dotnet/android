using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

using K4os.Compression.LZ4;
using NUnit.Framework;
using Xamarin.Android.Tools;
using ZstandardEncoder = System.IO.Compression.ZstandardEncoder;

namespace Xamarin.Android.AssemblyStore.Tests;

[TestFixture]
public class AssemblyStoreTests
{
	static readonly byte[] assemblyData = "Synthetic managed assembly"u8.ToArray ();

	[TestCase (AssemblyCompressionFormat.Lz4)]
	[TestCase (AssemblyCompressionFormat.Zstandard)]
	public void DecompressesBothCompressionFormats (AssemblyCompressionFormat format)
	{
		using Stream input = CreateCompressedAssembly (format, assemblyData);
		using var output = new MemoryStream ();

		Assert.IsTrue (AssemblyCompression.TryDecompress (input, output, out AssemblyCompressionFormat detectedFormat));
		Assert.AreEqual (format, detectedFormat);
		CollectionAssert.AreEqual (assemblyData, output.ToArray ());
	}

	[Test]
	public void LeavesUncompressedAssembliesUntouched ()
	{
		using var input = new MemoryStream (assemblyData);
		using var output = new MemoryStream ();

		Assert.IsFalse (AssemblyCompression.TryDecompress (input, output, out _));
		Assert.AreEqual (0, input.Position);
		Assert.AreEqual (0, output.Length);
	}

	[TestCase (AssemblyCompressionFormat.Lz4, "base_assemblies.blob")]
	[TestCase (AssemblyCompressionFormat.Zstandard, "base_assemblies.manifest")]
	[TestCase (AssemblyCompressionFormat.Lz4, "base")]
	public void ReadsLegacyStoreSets (AssemblyCompressionFormat format, string inputName)
	{
		string directory = CreateTemporaryDirectory ();
		try {
			using MemoryStream compressedStream = CreateCompressedAssembly (format, assemblyData);
			byte[] compressedAssembly = compressedStream.ToArray ();
			string indexStore = Path.Combine (directory, "base_assemblies.blob");
			CreateV1StoreSet (indexStore, compressedAssembly);

			(IList<AssemblyStoreExplorer>? explorers, string? errorMessage) = AssemblyStoreExplorer.Open (Path.Combine (directory, inputName));
			Assert.IsNull (errorMessage);
			Assert.IsNotNull (explorers);

			AssemblyStoreExplorer explorer = RequireSingle (explorers, "legacy store explorer");
			Assert.AreEqual (AndroidTargetArch.Arm64, explorer.TargetArch);

			IList<AssemblyStoreItem>? items = explorer.Find ("Test.dll", AndroidTargetArch.Arm64);
			Assert.IsNotNull (items);
			AssemblyStoreItem item = RequireSingle (items, "legacy store item");

			using Stream image = explorer.ReadImageData (item, uncompressIfNeeded: true) ??
				throw new InvalidOperationException ("Legacy store image was not returned");
			using var output = new MemoryStream ();
			image.CopyTo (output);
			CollectionAssert.AreEqual (assemblyData, output.ToArray ());
		} finally {
			Directory.Delete (directory, recursive: true);
		}
	}

	[Test]
	public void ReadsLegacyStoreApk ()
	{
		string directory = CreateTemporaryDirectory ();
		try {
			using MemoryStream compressedStream = CreateCompressedAssembly (AssemblyCompressionFormat.Lz4, assemblyData);
			string indexStore = Path.Combine (directory, "assemblies.blob");
			CreateV1StoreSet (indexStore, compressedStream.ToArray ());

			string apk = Path.Combine (directory, "legacy-v1.apk");
			using (FileStream file = File.Create (apk))
			using (var archive = new ZipArchive (file, ZipArchiveMode.Create)) {
				archive.CreateEntry ("AndroidManifest.xml");
				WriteEntry (archive, "assemblies/assemblies.blob", File.ReadAllBytes (indexStore));
				WriteEntry (archive, "assemblies/assemblies.arm64_v8a.blob", File.ReadAllBytes (Path.Combine (directory, "assemblies.arm64_v8a.blob")));
				WriteEntry (archive, "assemblies/assemblies.manifest", File.ReadAllBytes (Path.Combine (directory, "assemblies.manifest")));
			}

			(IList<AssemblyStoreExplorer>? explorers, string? errorMessage) = AssemblyStoreExplorer.Open (apk);
			Assert.IsNull (errorMessage);
			AssemblyStoreExplorer explorer = RequireSingle (explorers, "legacy APK store explorer");
			AssemblyStoreItem item = RequireSingle (explorer.Find ("Test.dll", AndroidTargetArch.Arm64), "legacy APK store item");
			using Stream image = explorer.ReadImageData (item, uncompressIfNeeded: true) ??
				throw new InvalidOperationException ("Legacy APK store image was not returned");
			using var output = new MemoryStream ();
			image.CopyTo (output);
			CollectionAssert.AreEqual (assemblyData, output.ToArray ());
		} finally {
			Directory.Delete (directory, recursive: true);
		}
	}

	[TestCase (2u, "lib/arm64-v8a/libassemblies.arm64-v8a.blob.so")]
	[TestCase (3u, "lib/arm64-v8a/libassembly-store.so")]
	public void ReadsV2ArchivePathsAndVersions (uint version, string storePath)
	{
		string directory = CreateTemporaryDirectory ();
		try {
			string apk = Path.Combine (directory, "legacy-v2.apk");
			using (FileStream file = File.Create (apk))
			using (var archive = new ZipArchive (file, ZipArchiveMode.Create)) {
				archive.CreateEntry ("AndroidManifest.xml");
				WriteEntry (
					archive,
					storePath,
					CreateV2Store (assemblyData, version)
				);
			}

			(IList<AssemblyStoreExplorer>? explorers, string? errorMessage) = AssemblyStoreExplorer.Open (apk);
			Assert.IsNull (errorMessage);
			Assert.IsNotNull (explorers);

			AssemblyStoreExplorer explorer = RequireSingle (explorers, "v2 store explorer");
			IList<AssemblyStoreItem>? items = explorer.Find ("Test.dll", AndroidTargetArch.Arm64);
			Assert.IsNotNull (items);
			AssemblyStoreItem item = RequireSingle (items, "v2 store item");
			using Stream image = explorer.ReadImageData (item) ??
				throw new InvalidOperationException ("V2 store image was not returned");
			using var output = new MemoryStream ();
			image.CopyTo (output);
			CollectionAssert.AreEqual (assemblyData, output.ToArray ());
		} finally {
			Directory.Delete (directory, recursive: true);
		}
	}

	[Test]
	public void IncludesCommonAssembliesInLegacyArchitectureViews ()
	{
		byte[] commonAssemblyData = "Common managed assembly"u8.ToArray ();
		string directory = CreateTemporaryDirectory ();
		try {
			string indexStore = Path.Combine (directory, "assemblies.blob");
			CreateV1StoreSet (indexStore, assemblyData, commonAssemblyData);

			(IList<AssemblyStoreExplorer>? explorers, string? errorMessage) = AssemblyStoreExplorer.Open (indexStore);
			Assert.IsNull (errorMessage);
			AssemblyStoreExplorer explorer = RequireSingle (explorers, "legacy store explorer");
			AssemblyStoreItem item = RequireSingle (explorer.Find ("Common.dll", AndroidTargetArch.Arm64), "common legacy store item");
			using Stream image = explorer.ReadImageData (item) ??
				throw new InvalidOperationException ("Common legacy store image was not returned");
			using var output = new MemoryStream ();
			image.CopyTo (output);
			CollectionAssert.AreEqual (commonAssemblyData, output.ToArray ());
		} finally {
			Directory.Delete (directory, recursive: true);
		}
	}

	static MemoryStream CreateCompressedAssembly (AssemblyCompressionFormat format, byte[] data)
	{
		byte[] compressed;
		switch (format) {
			case AssemblyCompressionFormat.Lz4: {
				compressed = new byte [LZ4Codec.MaximumOutputSize (data.Length)];
				int length = LZ4Codec.Encode (data, 0, data.Length, compressed, 0, compressed.Length);
				Array.Resize (ref compressed, length);
				break;
			}
			case AssemblyCompressionFormat.Zstandard: {
				long maximumLength = ZstandardEncoder.GetMaxCompressedLength (data.Length);
				compressed = new byte [checked ((int)maximumLength)];
				Assert.IsTrue (ZstandardEncoder.TryCompress (data, compressed, out int length, 3, 0));
				Array.Resize (ref compressed, length);
				break;
			}
			default:
				throw new NotSupportedException ($"Unsupported compression format '{format}'");
		}

		var output = new MemoryStream ();
		using (var writer = new BinaryWriter (output, System.Text.Encoding.UTF8, leaveOpen: true)) {
			writer.Write (format == AssemblyCompressionFormat.Lz4 ? 0x5A4C4158u : 0x535A4158u);
			writer.Write (0u);
			writer.Write ((uint)data.Length);
			writer.Write (compressed);
		}
		output.Seek (0, SeekOrigin.Begin);
		return output;
	}

	static void CreateV1StoreSet (string indexStorePath, byte[] image, byte[]? commonImage = null)
	{
		const ulong Hash64 = 0x123456789abcdef0;
		const uint Hash32 = 0x12345678;
		const ulong CommonHash64 = 0xfedcba9876543210;
		const uint CommonHash32 = 0x87654321;
		string directory = Path.GetDirectoryName (indexStorePath) ?? "";
		string baseName = Path.GetFileNameWithoutExtension (indexStorePath);
		uint globalEntryCount = commonImage == null ? 1u : 2u;
		uint localEntryCount = commonImage == null ? 0u : 1u;

		using (FileStream file = File.Create (indexStorePath))
		using (var writer = new BinaryWriter (file)) {
			WriteV1Header (writer, localEntryCount, globalEntryCount, storeId: 0);
			if (commonImage != null) {
				uint commonDataOffset = checked ((uint)(
					5 * sizeof (uint) +
					6 * sizeof (uint) +
					globalEntryCount * 2 * (sizeof (ulong) + 3 * sizeof (uint))
				));
				writer.Write (commonDataOffset);
				writer.Write ((uint)commonImage.Length);
				writer.Write (0u);
				writer.Write (0u);
				writer.Write (0u);
				writer.Write (0u);
			}
			WriteV1IndexEntry (writer, Hash32, storeId: 1);
			if (commonImage != null) {
				WriteV1IndexEntry (writer, CommonHash32, storeId: 0);
			}
			WriteV1IndexEntry (writer, Hash64, storeId: 1);
			if (commonImage != null) {
				WriteV1IndexEntry (writer, CommonHash64, storeId: 0);
				writer.Write (commonImage);
			}
		}

		string archStorePath = Path.Combine (directory, $"{baseName}.arm64_v8a.blob");
		using (FileStream file = File.Create (archStorePath))
		using (var writer = new BinaryWriter (file)) {
			const uint DataOffset = 5 * sizeof (uint) + 6 * sizeof (uint);
			WriteV1Header (writer, localEntryCount: 1, globalEntryCount: 0, storeId: 1);
			writer.Write (DataOffset);
			writer.Write ((uint)image.Length);
			writer.Write (0u);
			writer.Write (0u);
			writer.Write (0u);
			writer.Write (0u);
			writer.Write (image);
		}

		string manifestPath = Path.Combine (directory, $"{baseName}.manifest");
		File.WriteAllText (
			manifestPath,
			"Hash 32 Hash 64 Store ID Store idx Name\n" +
			$"0x{Hash32:x8} 0x{Hash64:x16} 1 0 Test\n" +
			(commonImage == null ? "" : $"0x{CommonHash32:x8} 0x{CommonHash64:x16} 0 0 Common\n")
		);
	}

	static void WriteV1Header (BinaryWriter writer, uint localEntryCount, uint globalEntryCount, uint storeId)
	{
		writer.Write (0x41424158u);
		writer.Write (1u);
		writer.Write (localEntryCount);
		writer.Write (globalEntryCount);
		writer.Write (storeId);
	}

	static void WriteV1IndexEntry (BinaryWriter writer, ulong hash, uint storeId)
	{
		writer.Write (hash);
		writer.Write (0u);
		writer.Write (0u);
		writer.Write (storeId);
	}

	static byte[] CreateV2Store (byte[] image, uint version)
	{
		byte[] name = "Test.dll"u8.ToArray ();
		const int HeaderSize = 5 * sizeof (uint);
		int indexSize = sizeof (ulong) + sizeof (uint) + (version >= 3 ? sizeof (byte) : 0);
		const int DescriptorSize = 7 * sizeof (uint);
		int dataOffset = HeaderSize + indexSize + DescriptorSize + sizeof (uint) + name.Length;

		using var output = new MemoryStream ();
		using (var writer = new BinaryWriter (output, System.Text.Encoding.UTF8, leaveOpen: true)) {
			writer.Write (0x41424158u);
			writer.Write (0x80010000u | version);
			writer.Write (1u);
			writer.Write (1u);
			writer.Write ((uint)indexSize);
			writer.Write (0x123456789abcdef0ul);
			writer.Write (0u);
			if (version >= 3) {
				writer.Write (false);
			}
			writer.Write (0u);
			writer.Write ((uint)dataOffset);
			writer.Write ((uint)image.Length);
			writer.Write (0u);
			writer.Write (0u);
			writer.Write (0u);
			writer.Write (0u);
			writer.Write ((uint)name.Length);
			writer.Write (name);
			writer.Write (image);
		}
		return output.ToArray ();
	}

	static void WriteEntry (ZipArchive archive, string path, byte[] data)
	{
		ZipArchiveEntry entry = archive.CreateEntry (path, CompressionLevel.NoCompression);
		using Stream output = entry.Open ();
		output.Write (data);
	}

	static string CreateTemporaryDirectory ()
	{
		string directory = Path.Combine (Path.GetTempPath (), $"assembly-store-tests-{Guid.NewGuid ():N}");
		Directory.CreateDirectory (directory);
		return directory;
	}

	static T RequireSingle<T> (IEnumerable<T>? values, string description)
	{
		if (values == null) {
			throw new InvalidOperationException ($"Missing {description}");
		}
		return values.Single ();
	}
}
