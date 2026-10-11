#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using ELFSharp.ELF;
using ELFSharp.ELF.Sections;
using Microsoft.Android.Tasks;
using NUnit.Framework;

namespace Xamarin.Android.Build.Tests.Tasks;

[TestFixture]
public class GenerateJniRemappingBinaryBlobsTests : BaseTest
{
	string DirectoryPath => Path.Combine (Root, "temp", TestName);

	[Test]
	public void LibraryNameControlsFilenameSonameAndArchivePath (
		[Values ("libbootstrap_binary_blobs.so", "libtest_bootstrap.so")] string libraryName,
		[Values ("remapping", "bootstrap", "combined")] string contents,
		[Values (false, true)] bool compress)
	{
		Directory.CreateDirectory (DirectoryPath);
		string remapping = Path.Combine (DirectoryPath, "remapping.xml");
		File.WriteAllText (remapping, """<replacements><replace-type from="example/Original" to="example/Replacement" /></replacements>""");
		string bootstrap = Path.Combine (DirectoryPath, "bootstrap.bin");
		File.WriteAllBytes (bootstrap, CoreClrBootstrapBlob.Create (
			false, false, 0, 0, 0, 0, "com.example.test",
			new Dictionary<string, string> (), new Dictionary<string, string> (), new Dictionary<string, string> (),
			[], [], 1));
		var task = new GenerateJniRemappingBinaryBlobs {
			BuildEngine = new MockBuildEngine (TestContext.Out),
			LibraryName = libraryName,
			RemappingXmlFilePath = contents == "bootstrap" ? "" : remapping,
			BootstrapFilePath = contents == "remapping" ? "" : bootstrap,
			OutputDirectory = Path.Combine (DirectoryPath, "out"),
			SupportedAbis = ["armeabi-v7a", "arm64-v8a", "x86", "x86_64"],
			Compress = compress,
		};
		Assert.IsTrue (task.Execute ());
		Assert.AreEqual (4, task.BinaryBlobLibraries.Length);
		foreach (var item in task.BinaryBlobLibraries) {
			string abi = item.GetMetadata ("Abi");
			Assert.AreEqual (Path.Combine (task.OutputDirectory, abi, libraryName), item.ItemSpec);
			Assert.AreEqual ($"lib/{abi}/{libraryName}", item.GetMetadata ("ArchivePath"));
			using IELF elf = ELFReader.Load (item.ItemSpec);
			var entry = elf.GetSections<IDynamicSection> ().Single ().Entries.Single (entry => (int)entry.Tag == 14);
			long offset = entry switch {
				DynamicEntry<uint> value => value.Value,
				DynamicEntry<ulong> value => checked ((long)value.Value),
				_ => throw new InvalidDataException ("Unexpected ELF dynamic entry type."),
			};
			var strings = elf.GetSection (".dynstr") as IStringTable ?? throw new InvalidDataException ("Missing ELF string table.");
			Assert.AreEqual (libraryName, strings [offset]);
			var symbols = elf.GetSection (".dynsym") as ISymbolTable ?? throw new InvalidDataException ("Missing ELF symbols.");
			Assert.AreEqual (contents != "bootstrap", symbols.Entries.Any (symbol => symbol.Name == JniRemappingBinaryBlob.Symbol));
			Assert.AreEqual (contents != "remapping", symbols.Entries.Any (symbol => symbol.Name == CoreClrBootstrapBlob.Symbol));
			FileAssert.DoesNotExist (Path.Combine (task.OutputDirectory, abi, "libbinary_blobs.so"));
		}
		Assert.AreEqual ("true", File.ReadAllText (Path.Combine (task.OutputDirectory, "binary-blobs.stamp")));

		task.RemappingXmlFilePath = "";
		task.BootstrapFilePath = "";
		Assert.IsTrue (task.Execute ());
		Assert.IsEmpty (task.BinaryBlobLibraries);
		Assert.IsEmpty (Directory.GetFiles (task.OutputDirectory, "*.so", SearchOption.AllDirectories));
		Assert.AreEqual ("false", File.ReadAllText (Path.Combine (task.OutputDirectory, "binary-blobs.stamp")));
	}

	[TestCase ("")]
	[TestCase ("../libtest.so")]
	[TestCase ("nested\\libtest.so")]
	[TestCase ("libtest\0.so")]
	public void RejectsInvalidLibraryFilename (string libraryName)
	{
		var task = new GenerateJniRemappingBinaryBlobs {
			BuildEngine = new MockBuildEngine (TestContext.Out),
			LibraryName = libraryName,
			OutputDirectory = Path.Combine (DirectoryPath, "out"),
			SupportedAbis = ["arm64-v8a"],
		};
		Assert.IsFalse (task.Execute ());
		Assert.IsEmpty (task.BinaryBlobLibraries);
		Assert.IsFalse (Directory.Exists (task.OutputDirectory));
	}
}
