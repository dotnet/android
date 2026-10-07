#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

using Microsoft.Android.Tasks;
using Microsoft.Build.Framework;
using NUnit.Framework;
using Xamarin.Android.Tasks;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Build.Tests.Tasks;

[TestFixture]
public class GenerateJniRemappingBinaryBlobsTests : BaseTest
{
	const string Xml = """
		<replacements>
		  <replace-type from="a/😀" to="b/X" />
		  <replace-type from="a/A" to="b/Y" />
		  <reverse-type from="b/X" to="a/😀" />
		  <replace-method source-type="b/X" source-method-name="run" target-type="b/X" target-method-name="r" target-method-instance-to-static="true" />
		  <replace-method source-type="b/X" source-method-name="run" source-method-signature="(I)V" target-type="b/X" target-method-name="s" target-method-signature="(I)V" target-method-instance-to-static="false" />
		  <replace-field source-type="b/X" source-field-name="value" source-field-signature="I" target-type="b/Y" target-field-name="v" />
		</replacements>
		""";

	string DirectoryPath => Path.Combine (Root, "temp", TestName);

	string WriteXml (string text)
	{
		Directory.CreateDirectory (DirectoryPath);
		string path = Path.Combine (DirectoryPath, "merged.xml");
		File.WriteAllText (path, text);
		return path;
	}

	[TestCase (false)]
	[TestCase (true)]
	public void ProducesPerAbiElfAndValidatedBlob (bool compress)
	{
		string [] abis = ["armeabi-v7a", "arm64-v8a", "x86", "x86_64"];
		var task = new GenerateJniRemappingBinaryBlobs {
			BuildEngine = new MockBuildEngine (TestContext.Out),
			RemappingXmlFilePath = WriteXml (Xml),
			OutputDirectory = Path.Combine (DirectoryPath, "out"),
			SupportedAbis = abis,
			Compress = compress,
		};
		Assert.IsTrue (task.Execute ());
		CollectionAssert.AreEqual (abis, task.BinaryBlobLibraries.Select (item => item.GetMetadata ("Abi")));
		foreach (var library in task.BinaryBlobLibraries) {
			string abi = library.GetMetadata ("Abi");
			string archive = $"lib/{abi}/libbinary_blobs.so";
			Assert.AreEqual (archive, library.GetMetadata ("ArchivePath"));
			FileAssert.Exists (library.ItemSpec);
			byte [] elf = File.ReadAllBytes (library.ItemSpec);
			int page = abi == "arm64-v8a" || abi == "x86_64" ? 16384 : 4096;
			byte [] blob = elf.AsSpan (page, elf.Length - page).ToArray ();
			// The payload ends before the ELF section-name table.
			int stored = BitConverter.ToInt32 (blob, 8);
			Assert.LessOrEqual (page + 16 + stored, elf.Length);
			blob = blob.AsSpan (0, 16 + stored).ToArray ();
			Assert.AreEqual (compress ? 1 : 0, BitConverter.ToUInt16 (blob, 6));
			JniRemappingBinaryBlob.Validate (blob);
			AssemblyStoreElfWriter.Validate (elf, MonoAndroidHelper.AbiToTargetArch (abi), "libbinary_blobs.so",
				new [] { (JniRemappingBinaryBlob.Symbol, blob) });
			byte [] raw = compress ? Decompress (blob) : blob.AsSpan (16).ToArray ();
			Assert.AreEqual (56u, BitConverter.ToUInt32 (raw, 16));
			Assert.AreEqual (2u, BitConverter.ToUInt32 (raw, 0));
			Assert.AreEqual (1u, BitConverter.ToUInt32 (raw, 4));
			Assert.AreEqual (1u, BitConverter.ToUInt32 (raw, 8));
			Assert.AreEqual (1u, BitConverter.ToUInt32 (raw, 12));
			Assert.AreEqual (2u, BitConverter.ToUInt32 (raw, 48));
			Assert.AreEqual (1u, BitConverter.ToUInt32 (raw, 52));
			uint typeOffset = BitConverter.ToUInt32 (raw, 16);
			uint stringsOffset = BitConverter.ToUInt32 (raw, 40);
			uint firstNameOffset = BitConverter.ToUInt32 (raw, (int)typeOffset);
			uint secondNameOffset = BitConverter.ToUInt32 (raw, (int)typeOffset + 12);
			Assert.AreEqual ("a/A", ReadString (raw, firstNameOffset));
			Assert.AreEqual ("a/😀", ReadString (raw, secondNameOffset));
			Assert.Greater (firstNameOffset, stringsOffset);
			uint methodOffset = BitConverter.ToUInt32 (raw, 32);
			Assert.AreEqual ("(I)V", ReadString (raw, BitConverter.ToUInt32 (raw, (int)methodOffset + 8)));
			Assert.AreEqual (0u, BitConverter.ToUInt32 (raw, (int)methodOffset + 32 + 8), "Wildcard signature follows the exact signature.");
			Assert.AreEqual (1u, BitConverter.ToUInt32 (raw, (int)methodOffset + 32 + 28));
		}
	}

	static byte [] Decompress (byte [] blob)
	{
		byte [] raw = new byte [BitConverter.ToInt32 (blob, 12)];
		Assert.IsTrue (ZstandardDecoder.TryDecompress (blob.AsSpan (16), raw, out int written));
		Assert.AreEqual (raw.Length, written);
		return raw;
	}

	[Test]
	public void MissingMethodStaticFlagDefaultsToInstance ()
	{
		string xml = WriteXml ("""
			<replacements>
			  <replace-method source-type="a/B" source-method-name="run" target-type="b/C" target-method-name="go" />
			</replacements>
			""");
		byte [] blob = JniRemappingBinaryBlob.Create (xml, compress: false);
		JniRemappingBinaryBlob.Validate (blob);
		byte [] raw = blob.AsSpan (16).ToArray ();
		uint methodOffset = BitConverter.ToUInt32 (raw, 32);
		Assert.AreEqual (0u, BitConverter.ToUInt32 (raw, (int)methodOffset + 28));
	}

	static string ReadString (byte [] raw, uint offset)
	{
		int end = Array.IndexOf (raw, (byte)0, (int)offset);
		return Encoding.UTF8.GetString (raw, (int)offset, end - (int)offset);
	}

	[Test]
	public void MultipleSymbolsKeepIndependentCompressionEnvelopes ()
	{
		string xml = WriteXml ("<replacements />");
		byte [] compressed = JniRemappingBinaryBlob.Create (xml, compress: true);
		byte [] uncompressed = JniRemappingBinaryBlob.Create (xml, compress: false);
		using var compressedStream = new MemoryStream (compressed);
		using var uncompressedStream = new MemoryStream (uncompressed);
		using var elf = new MemoryStream ();
		AssemblyStoreElfWriter.Write (
			new [] { ("xa_jni_remapping", (Stream)compressedStream), ("xa_second_blob", (Stream)uncompressedStream) },
			elf, AndroidTargetArch.Arm64, "libbinary_blobs.so");
		AssemblyStoreElfWriter.Validate (elf.ToArray (), AndroidTargetArch.Arm64, "libbinary_blobs.so",
			new [] { ("xa_jni_remapping", compressed), ("xa_second_blob", uncompressed) });
		Assert.AreEqual (1, BitConverter.ToUInt16 (compressed, 6));
		Assert.AreEqual (0, BitConverter.ToUInt16 (uncompressed, 6));
		JniRemappingBinaryBlob.Validate (compressed);
		JniRemappingBinaryBlob.Validate (uncompressed);
	}

	[Test]
	public void SortsScalarTypeNamesAndOverloadedMethodDescriptors ()
	{
		string bmp = "\uE000";
		string supplementary = char.ConvertFromUtf32 (0x10000);
		string [] entries = [
			$"""<replace-type from="a/{supplementary}" to="b/S" />""",
			$"""<replace-method source-type="a/{supplementary}" source-method-name="m" source-method-signature="(J)V" target-type="b/S" target-method-name="j" target-method-instance-to-static="false" />""",
			$"""<replace-method source-type="a/{supplementary}" source-method-name="m" target-type="b/S" target-method-name="w" target-method-instance-to-static="false" />""",
			$"""<replace-type from="a/{bmp}" to="b/B" />""",
			$"""<replace-method source-type="a/{supplementary}" source-method-name="m" source-method-signature="(I)" target-type="b/S" target-method-name="p" target-method-instance-to-static="false" />""",
			$"""<replace-method source-type="a/{supplementary}" source-method-name="m" source-method-signature="(I)V" target-type="b/S" target-method-name="i" target-method-instance-to-static="false" />""",
		];
		string Wrap (IEnumerable<string> nodes) => "<replacements>" + string.Join ("", nodes) + "</replacements>";
		byte [] forward = JniRemappingBinaryBlob.Create (WriteXml (Wrap (entries)), false);
		byte [] reversed = JniRemappingBinaryBlob.Create (WriteXml (Wrap (entries.Reverse ())), false);
		CollectionAssert.AreEqual (forward, reversed, "Overloads must have a deterministic order independent of XML input order.");
		byte [] raw = forward.AsSpan (16).ToArray ();
		int typeOffset = (int)BitConverter.ToUInt32 (raw, 16);
		Assert.AreEqual ($"a/{bmp}", ReadString (raw, BitConverter.ToUInt32 (raw, typeOffset)));
		Assert.AreEqual ($"a/{supplementary}", ReadString (raw, BitConverter.ToUInt32 (raw, typeOffset + 12)));
		int methodOffset = (int)BitConverter.ToUInt32 (raw, 32);
		string [] expected = ["(I)V", "(J)V", "(I)", ""];
		for (int i = 0; i < expected.Length; i++) {
			uint signature = BitConverter.ToUInt32 (raw, methodOffset + i * 32 + 8);
			Assert.AreEqual (expected [i], signature == 0 ? "" : ReadString (raw, signature));
		}
	}

	[Test]
	public void RejectsRawTablesAboveRuntimeLimit ()
	{
		const int limit = 256 * 1024 * 1024;
		Assert.DoesNotThrow (() => JniRemappingBinaryBlob.EnsureRawBodySize (limit));
		var error = Assert.Throws<InvalidDataException> (() => JniRemappingBinaryBlob.EnsureRawBodySize ((long)limit + 1));
		StringAssert.Contains ("256 MiB", error?.Message);

		byte [] envelope = JniRemappingBinaryBlob.Create (WriteXml ("<replacements />"), false);
		BitConverter.GetBytes (limit + 1).CopyTo (envelope, 12);
		error = Assert.Throws<InvalidDataException> (() => JniRemappingBinaryBlob.Validate (envelope));
		StringAssert.Contains ("256 MiB", error?.Message);
	}

	[Test]
	public void EmptyRemappingIsValid ()
	{
		byte [] blob = JniRemappingBinaryBlob.Create (WriteXml ("<replacements />"), false);
		JniRemappingBinaryBlob.Validate (blob);
		Assert.AreEqual (73, blob.Length);
		CollectionAssert.AreEqual (new uint [] { 0, 0, 0, 0 }, Enumerable.Range (0, 4)
			.Select (index => BitConverter.ToUInt32 (blob, 16 + index * 4)));
	}

	[TestCase (false)]
	[TestCase (true)]
	public void EmptyMappingProducesLibrariesWithOrWithoutXml (bool provideXml)
	{
		string [] abis = ["armeabi-v7a", "arm64-v8a", "x86", "x86_64"];
		var task = new GenerateJniRemappingBinaryBlobs {
			BuildEngine = new MockBuildEngine (TestContext.Out),
			RemappingXmlFilePath = provideXml ? WriteXml ("<replacements />") : "",
			OutputDirectory = Path.Combine (DirectoryPath, "empty-out"),
			SupportedAbis = abis,
		};

		Assert.IsTrue (task.Execute ());
		Assert.AreEqual (abis.Length, task.BinaryBlobLibraries.Length);
		foreach (var library in task.BinaryBlobLibraries) {
			string abi = library.GetMetadata ("Abi");
			Assert.Contains (abi, abis);
			Assert.AreEqual ($"lib/{abi}/libbinary_blobs.so", library.GetMetadata ("ArchivePath"));
			Assert.AreEqual (Path.Combine (task.OutputDirectory, abi, "libbinary_blobs.so"), library.ItemSpec);
			FileAssert.Exists (library.ItemSpec);
			byte [] elf = File.ReadAllBytes (library.ItemSpec);
			int offset = abi is "arm64-v8a" or "x86_64" ? 16384 : 4096;
			byte [] blob = elf.AsSpan (offset, 73).ToArray ();
			JniRemappingBinaryBlob.Validate (blob);
			AssemblyStoreElfWriter.Validate (elf, MonoAndroidHelper.AbiToTargetArch (abi), "libbinary_blobs.so",
				new [] { (JniRemappingBinaryBlob.Symbol, blob) });
		}
	}

	[Test]
	public void NonblankMissingXmlFailsWithoutPublishing ()
	{
		var errors = new List<BuildErrorEventArgs> ();
		var task = new GenerateJniRemappingBinaryBlobs {
			BuildEngine = new MockBuildEngine (TestContext.Out, errors),
			RemappingXmlFilePath = Path.Combine (DirectoryPath, "missing.xml"),
			OutputDirectory = Path.Combine (DirectoryPath, "out"),
			SupportedAbis = ["arm64-v8a"],
		};

		Assert.IsFalse (task.Execute ());
		Assert.That (errors, Has.Some.Property ("Message").Contains ("missing.xml"));
		Assert.IsEmpty (task.BinaryBlobLibraries);
		Assert.IsFalse (Directory.Exists (task.OutputDirectory));
	}

	[TestCase ("""<replacements><replace-type from="a" /></replacements>""")]
	[TestCase ("""<replacements><replace-type from="a" to="b" /><replace-type from="a" to="c" /></replacements>""")]
	[TestCase ("""<replacements><replace-method source-type="a" source-method-name="m" target-type="b" target-method-name="n" target-method-instance-to-static="invalid" /></replacements>""")]
	[TestCase ("""<!DOCTYPE replacements [<!ENTITY bad SYSTEM "file:///etc/passwd">]><replacements>&bad;</replacements>""")]
	public void InvalidXmlDoesNotPublishOutput (string xml)
	{
		var errors = new List<BuildErrorEventArgs> ();
		var task = new GenerateJniRemappingBinaryBlobs {
			BuildEngine = new MockBuildEngine (TestContext.Out, errors),
			RemappingXmlFilePath = WriteXml (xml),
			OutputDirectory = Path.Combine (DirectoryPath, "out"),
			SupportedAbis = ["arm64-v8a"],
		};
		Assert.IsFalse (task.Execute ());
		Assert.That (errors, Has.Some.Property ("Code").EqualTo ("XA4325"));
		Assert.IsEmpty (task.BinaryBlobLibraries);
		Assert.IsFalse (Directory.Exists (task.OutputDirectory));
	}

	[Test]
	public void ValidatorRejectsCorruptEnvelopeAndTables ()
	{
		byte [] good = JniRemappingBinaryBlob.Create (WriteXml (Xml), false);
		foreach (int index in new [] { 0, 4, 6, 8, 12, 16 + 16, 16 + 40 }) {
			byte [] bad = (byte [])good.Clone ();
			bad [index] ^= 0x80;
			Assert.Throws<InvalidDataException> (() => JniRemappingBinaryBlob.Validate (bad), $"Byte {index}");
		}
		byte [] invalidUtf8 = (byte [])good.Clone ();
		int firstName = (int)BitConverter.ToUInt32 (good, 16 + 56);
		invalidUtf8 [16 + firstName] = 0xff;
		Assert.Throws<InvalidDataException> (() => JniRemappingBinaryBlob.Validate (invalidUtf8));
		byte [] missingNul = (byte [])good.Clone ();
		missingNul [16 + firstName + (int)BitConverter.ToUInt32 (good, 16 + 60)] = (byte)'X';
		Assert.Throws<InvalidDataException> (() => JniRemappingBinaryBlob.Validate (missingNul));
		byte [] invalidFlags = (byte [])good.Clone ();
		int methodOffset = (int)BitConverter.ToUInt32 (good, 16 + 32);
		invalidFlags [16 + methodOffset + 28] = 2;
		Assert.Throws<InvalidDataException> (() => JniRemappingBinaryBlob.Validate (invalidFlags));
		byte [] invalidRange = (byte [])good.Clone ();
		int methodTypeOffset = (int)BitConverter.ToUInt32 (good, 16 + 24);
		invalidRange [16 + methodTypeOffset + 8] = 1;
		Assert.Throws<InvalidDataException> (() => JniRemappingBinaryBlob.Validate (invalidRange));
		byte [] compressed = JniRemappingBinaryBlob.Create (WriteXml (Xml), true);
		compressed [^1] ^= 0xff;
		Assert.Throws<InvalidDataException> (() => JniRemappingBinaryBlob.Validate (compressed));
	}

	[Test]
	public void InvalidAbiDoesNotPublishEarlierLibraries ()
	{
		var task = new GenerateJniRemappingBinaryBlobs {
			BuildEngine = new MockBuildEngine (TestContext.Out),
			RemappingXmlFilePath = WriteXml (Xml),
			OutputDirectory = Path.Combine (DirectoryPath, "out"),
			SupportedAbis = ["arm64-v8a", "../invalid"],
		};
		Assert.IsFalse (task.Execute ());
		Assert.IsFalse (Directory.Exists (task.OutputDirectory));
	}
}
