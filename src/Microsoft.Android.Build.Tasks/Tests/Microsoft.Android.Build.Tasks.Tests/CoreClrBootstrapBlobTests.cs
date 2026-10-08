#nullable enable
using System;
using System.Collections.Generic;
using System.IO;

using Microsoft.Android.Tasks;
using NUnit.Framework;
using Xamarin.Android.Tasks;

namespace Xamarin.Android.Build.Tests.Tasks;

[TestFixture]
public class CoreClrBootstrapBlobTests : BaseTest
{
	string DirectoryPath => Path.Combine (Root, "temp", TestName);

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
