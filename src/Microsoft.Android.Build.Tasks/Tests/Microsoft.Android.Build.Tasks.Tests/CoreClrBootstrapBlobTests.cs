#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

using Microsoft.Android.Tasks;
using NUnit.Framework;
using Xamarin.Android.Tasks;

namespace Xamarin.Android.Build.Tests.Tasks;

[TestFixture]
public class CoreClrBootstrapBlobTests : BaseTest
{
	string DirectoryPath => Path.Combine (Root, "temp", TestName);

	[TestCase (false)]
	[TestCase (true)]
	public void SharesElfWithRemappingForEveryAbi (bool compress)
	{
		var runtime = Enumerable.Range (0, 5000).ToDictionary (
			i => $"Test.Property.{i:D4}", i => new string ('x', 16) + i, StringComparer.Ordinal);
		var libraries = new (uint Hash, bool Ignore, bool IsJniLibrary, string Name) [] {
			(1, false, true, "libexample.so"),
			(2, false, true, "example"),
		};
		byte [] raw = CoreClrBootstrapBlob.Create (
			true, true, 2, 24, 0, 1, "com.example.😀",
			new SortedDictionary<string, string> { ["TEST_ENV"] = "hello" },
			new SortedDictionary<string, string> { ["test.property"] = "world" },
			runtime, libraries, [0, 1], 2);
		Assert.Greater (raw.Length, 64 * 1024);
		CoreClrBootstrapBlob.Validate (raw);
		Directory.CreateDirectory (DirectoryPath);
		string bootstrapPath = Path.Combine (DirectoryPath, "bootstrap.bin");
		File.WriteAllBytes (bootstrapPath, raw);
		string [] abis = ["armeabi-v7a", "arm64-v8a", "x86", "x86_64"];
		var task = new GenerateJniRemappingBinaryBlobs {
			BuildEngine = new MockBuildEngine (TestContext.Out),
			BootstrapFilePath = bootstrapPath,
			OutputDirectory = Path.Combine (DirectoryPath, "out"),
			SupportedAbis = abis,
			Compress = compress,
		};
		Assert.IsTrue (task.Execute ());
		byte [] remap = JniRemappingBinaryBlob.Create ("", compress);
		byte [] bootstrap = JniRemappingBinaryBlob.Wrap (raw, compress);
		foreach (var library in task.BinaryBlobLibraries) {
			byte [] elf = File.ReadAllBytes (library.ItemSpec);
			AssemblyStoreElfWriter.Validate (elf, MonoAndroidHelper.AbiToTargetArch (library.GetMetadata ("Abi")),
				"libbinary_blobs.so", new [] { (JniRemappingBinaryBlob.Symbol, remap), (CoreClrBootstrapBlob.Symbol, bootstrap) });
			Assert.AreEqual (compress ? 1 : 0, BitConverter.ToUInt16 (bootstrap, 6));
			Assert.AreEqual (raw.Length, BitConverter.ToInt32 (bootstrap, 12));
			byte [] decoded = compress ? Decompress (bootstrap) : bootstrap.AsSpan (16).ToArray ();
			CollectionAssert.AreEqual (raw, decoded);
		}
	}

	[Test]
	public void RejectsCorruptBootstrapWithoutPublishing ()
	{
		byte [] raw = CoreClrBootstrapBlob.Create (
			false, false, 0, 0, 0, 0, "com.example.test",
			new Dictionary<string, string> (), new Dictionary<string, string> (), new Dictionary<string, string> (),
			[], [], 1);
		BitConverter.GetBytes (uint.MaxValue).CopyTo (raw, 60); // String pool offset.
		Assert.Throws<InvalidDataException> (() => CoreClrBootstrapBlob.Validate (raw));
		Directory.CreateDirectory (DirectoryPath);
		string bootstrapPath = Path.Combine (DirectoryPath, "invalid.bin");
		File.WriteAllBytes (bootstrapPath, raw);
		var task = new GenerateJniRemappingBinaryBlobs {
			BuildEngine = new MockBuildEngine (TestContext.Out),
			BootstrapFilePath = bootstrapPath,
			OutputDirectory = Path.Combine (DirectoryPath, "out"),
			SupportedAbis = ["arm64-v8a"],
		};
		Assert.IsFalse (task.Execute ());
		FileAssert.DoesNotExist (Path.Combine (task.OutputDirectory, "arm64-v8a", "libbinary_blobs.so"));
	}

	static byte [] Decompress (byte [] blob)
	{
		byte [] raw = new byte [BitConverter.ToInt32 (blob, 12)];
		Assert.IsTrue (ZstandardDecoder.TryDecompress (blob.AsSpan (16), raw, out int written));
		Assert.AreEqual (raw.Length, written);
		return raw;
	}
}
