#nullable enable
using System;
using System.Collections.Generic;
using System.IO;

using Microsoft.Android.Build.Tasks;
using Microsoft.Android.Tasks;
using NUnit.Framework;
using Xamarin.Android.Tasks;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Build.Tests.Tasks;

[TestFixture]
public class CoreClrBootstrapBlobTests : BaseTest
{
	string DirectoryPath => Path.Combine (Root, "temp", TestName);

	[Test]
	public void PooledStreamCanBeReusedForSmallerPayloadAfterFailure ()
	{
		var pool = new MemoryStreamPool ();
		var output = pool.Rent ();
		try {
			var environment = new Dictionary<string, string> { ["LARGE_VALUE"] = new string ('x', 128 * 1024) };
			void Write () => CoreClrBootstrapBlob.Write (
				output, false, false, 0, 0, 0, 0, "com.example.test",
				environment, new Dictionary<string, string> (), new Dictionary<string, string> (), [], [], 1);

			Write ();
			Assert.Greater (output.Length, 128 * 1024);
			Assert.AreEqual (0, output.Position);
			environment ["INVALID"] = "embedded\0nul";
			Assert.Throws<InvalidDataException> (() => Write ());
			pool.Return (output);
			var previous = output;
			output = pool.Rent ();
			Assert.AreSame (previous, output);
			environment.Clear ();
			environment ["EMPTY"] = "";
			environment ["UTF8"] = "\u00e9\u65e5";
			Write ();

			byte [] expected = CoreClrBootstrapBlob.Create (
				false, false, 0, 0, 0, 0, "com.example.test",
				environment, new Dictionary<string, string> (), new Dictionary<string, string> (), [], [], 1);
			Assert.Less (output.Length, 1024, "A smaller payload must not retain bytes from the previous use.");
			Assert.Greater (output.Capacity, output.Length, "Validation must use the logical length, not pooled buffer capacity.");
			Assert.AreEqual (0, output.Position);
			CollectionAssert.AreEqual (expected, output.ToArray ());
			CoreClrBootstrapBlob.Validate (output.ToArray ());
			Assert.Throws<InvalidDataException> (() => CoreClrBootstrapBlob.Validate (output.GetBuffer ()),
				"The byte-array validator must still reject trailing capacity as extra payload bytes.");
		} finally {
			pool.Return (output);
		}
	}

	[Test]
	public void EmptyRemappingPublishesBootstrapOnly ()
	{
		byte [] raw = CoreClrBootstrapBlob.Create (
			false, false, 0, 0, 0, 0, "com.example.test",
			new Dictionary<string, string> (), new Dictionary<string, string> (), new Dictionary<string, string> (),
			[], [], 1);
		Directory.CreateDirectory (DirectoryPath);
		string bootstrapPath = Path.Combine (DirectoryPath, "bootstrap.bin");
		File.WriteAllBytes (bootstrapPath, raw);
		var task = new GenerateJniRemappingBinaryBlobs {
			BuildEngine = new MockBuildEngine (TestContext.Out),
			BootstrapFilePath = bootstrapPath,
			OutputDirectory = Path.Combine (DirectoryPath, "out"),
			SupportedAbis = ["arm64-v8a"],
		};
		Assert.IsTrue (task.Execute ());
		Assert.AreEqual (1, task.BinaryBlobLibraries.Length);
		FileAssert.Exists (task.BinaryBlobLibraries [0].ItemSpec);
		Assert.AreEqual ("true", File.ReadAllText (Path.Combine (task.OutputDirectory, "binary-blobs.stamp")));
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

	[Test]
	public void RejectsUnsortedDsoCache ()
	{
		Assert.Throws<InvalidDataException> (() => CoreClrBootstrapBlob.Create (
			false, false, 0, 0, 0, 0, "com.example.test",
			new Dictionary<string, string> (), new Dictionary<string, string> (), new Dictionary<string, string> (),
			[(2u, false, false, "libsecond.so"), (1u, false, false, "libfirst.so")], [], 1));
	}
}
