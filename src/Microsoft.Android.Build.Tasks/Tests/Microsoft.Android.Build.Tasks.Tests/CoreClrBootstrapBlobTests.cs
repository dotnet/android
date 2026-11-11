#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using Microsoft.Android.Build.Tasks;
using Microsoft.Android.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Xamarin.Android.Tasks;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Build.Tests.Tasks;

[TestClass]
[DoNotParallelize]
public class CoreClrBootstrapBlobTests : BaseTest
{
	string DirectoryPath => Path.Combine (Root, "temp", TestName);

	[TestMethod]
	public void LocalDateTimeOffsetIsReservedForTheDevice ()
	{
		const string name = "System.TimeZoneInfo.LocalDateTimeOffset";
		byte [] Create (string offset) => CoreClrBootstrapBlob.Create (
			false, false, 0, 0, 0, 0, "com.example.test",
			new Dictionary<string, string> (), new Dictionary<string, string> (),
			new Dictionary<string, string> { [name] = offset, ["other"] = "value" }, [], [], 1);

		byte [] raw = Create ("3600");
		CollectionAssert.AreEqual (raw, Create ("-18000"), "The build-time value must not be stored in the blob.");
		using var reader = new BinaryReader (new MemoryStream (raw));
		reader.BaseStream.Position = 20;
		Assert.AreEqual (5u, reader.ReadUInt32 ());
		reader.BaseStream.Position = 48;
		uint propertiesOffset = reader.ReadUInt32 ();
		reader.BaseStream.Position = propertiesOffset + 3 * 8;
		uint nameOffset = reader.ReadUInt32 ();
		Assert.AreEqual (0u, reader.ReadUInt32 (), "The host must fill the offset value at runtime.");
		int end = Array.IndexOf (raw, (byte)0, (int)nameOffset);
		Assert.IsGreaterThan ((int)nameOffset, end);
		Assert.AreEqual (name, Encoding.UTF8.GetString (raw, (int)nameOffset, end - (int)nameOffset));

		using var writer = new BinaryWriter (new MemoryStream (raw));
		writer.BaseStream.Position = propertiesOffset + 3 * 8 + 4;
		writer.Write (nameOffset);
		writer.Flush ();
		Assert.ThrowsExactly<InvalidDataException> (() => CoreClrBootstrapBlob.Validate (raw));
	}

	[TestMethod]
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
			Assert.IsGreaterThan (128 * 1024, output.Length);
			Assert.AreEqual (0, output.Position);
			environment ["INVALID"] = "embedded\0nul";
			Assert.ThrowsExactly<InvalidDataException> (() => Write ());
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
			Assert.IsLessThan (1024, output.Length, "A smaller payload must not retain bytes from the previous use.");
			Assert.IsGreaterThan (output.Length, output.Capacity, "Validation must use the logical length, not pooled buffer capacity.");
			Assert.AreEqual (0, output.Position);
			CollectionAssert.AreEqual (expected, output.ToArray ());
			CoreClrBootstrapBlob.Validate (output.ToArray ());
			Assert.ThrowsExactly<InvalidDataException> (() => CoreClrBootstrapBlob.Validate (output.GetBuffer ()),
				"The byte-array validator must still reject trailing capacity as extra payload bytes.");
		} finally {
			pool.Return (output);
		}
	}

	[TestMethod]
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
			BuildEngine = new MockBuildEngine (Console.Out),
			BootstrapFilePath = bootstrapPath,
			OutputDirectory = Path.Combine (DirectoryPath, "out"),
			SupportedAbis = ["arm64-v8a"],
		};
		Assert.IsTrue (task.Execute ());
		Assert.AreEqual (1, task.BinaryBlobLibraries.Length);
		Assert.IsTrue (File.Exists (task.BinaryBlobLibraries [0].ItemSpec));
		Assert.AreEqual ("true", File.ReadAllText (Path.Combine (task.OutputDirectory, "binary-blobs.stamp")));
	}

	[TestMethod]
	public void RejectsCorruptBootstrapWithoutPublishing ()
	{
		byte [] raw = CoreClrBootstrapBlob.Create (
			false, false, 0, 0, 0, 0, "com.example.test",
			new Dictionary<string, string> (), new Dictionary<string, string> (), new Dictionary<string, string> (),
			[], [], 1);
		BitConverter.GetBytes (uint.MaxValue).CopyTo (raw, 60); // String pool offset.
		Assert.ThrowsExactly<InvalidDataException> (() => CoreClrBootstrapBlob.Validate (raw));
		Directory.CreateDirectory (DirectoryPath);
		string bootstrapPath = Path.Combine (DirectoryPath, "invalid.bin");
		File.WriteAllBytes (bootstrapPath, raw);
		var task = new GenerateJniRemappingBinaryBlobs {
			BuildEngine = new MockBuildEngine (Console.Out),
			BootstrapFilePath = bootstrapPath,
			OutputDirectory = Path.Combine (DirectoryPath, "out"),
			SupportedAbis = ["arm64-v8a"],
		};
		Assert.IsFalse (task.Execute ());
		Assert.IsFalse (File.Exists (Path.Combine (task.OutputDirectory, "arm64-v8a", "libbinary_blobs.so")));
	}

	[TestMethod]
	public void RejectsUnsortedDsoCache ()
	{
		Assert.ThrowsExactly<InvalidDataException> (() => CoreClrBootstrapBlob.Create (
			false, false, 0, 0, 0, 0, "com.example.test",
			new Dictionary<string, string> (), new Dictionary<string, string> (), new Dictionary<string, string> (),
			[(2u, false, false, "libsecond.so", "second"), (1u, false, false, "libfirst.so", "first")], [], 1));
	}
}
