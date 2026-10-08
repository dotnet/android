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
		  <replace-type from="a/B" to="b/X" />
		  <replace-type from="a/A" to="b/Y" />
		  <reverse-type from="b/X" to="a/B" />
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
			Assert.AreEqual ($"lib/{abi}/libbinary_blobs.so", library.GetMetadata ("ArchivePath"));
			byte [] elf = File.ReadAllBytes (library.ItemSpec);
			int page = abi is "arm64-v8a" or "x86_64" ? 16384 : 4096;
			int stored = BitConverter.ToInt32 (elf, page + 8);
			Assert.Greater (stored, 0);
			Assert.LessOrEqual (page + 16 + stored, elf.Length);
			byte [] blob = elf.AsSpan (page, 16 + stored).ToArray ();
			Assert.AreEqual (compress ? 1 : 0, BitConverter.ToUInt16 (blob, 6));
			JniRemappingBinaryBlob.Validate (blob);
			AssemblyStoreElfWriter.Validate (elf, MonoAndroidHelper.AbiToTargetArch (abi), "libbinary_blobs.so",
				new [] { (JniRemappingBinaryBlob.Symbol, blob) });
			byte [] raw = compress ? Decompress (blob) : blob.AsSpan (16).ToArray ();
			CollectionAssert.AreEqual (new uint [] { 2, 1, 1, 1 },
				Enumerable.Range (0, 4).Select (index => BitConverter.ToUInt32 (raw, index * 4)));
			Assert.AreEqual (2u, BitConverter.ToUInt32 (raw, 48));
			Assert.AreEqual (1u, BitConverter.ToUInt32 (raw, 52));
			int types = (int)BitConverter.ToUInt32 (raw, 16);
			Assert.AreEqual ("a/A", ReadString (raw, BitConverter.ToUInt32 (raw, types)));
			Assert.AreEqual ("a/B", ReadString (raw, BitConverter.ToUInt32 (raw, types + 12)));
			int methods = (int)BitConverter.ToUInt32 (raw, 32);
			Assert.AreEqual ("(I)V", ReadString (raw, BitConverter.ToUInt32 (raw, methods + 8)));
			Assert.AreEqual (0u, BitConverter.ToUInt32 (raw, methods + 32 + 8), "Wildcard follows the exact signature.");
			Assert.AreEqual (1u, BitConverter.ToUInt32 (raw, methods + 32 + 28));
		}
	}

	static byte [] Decompress (byte [] blob)
	{
		byte [] raw = new byte [BitConverter.ToInt32 (blob, 12)];
		Assert.IsTrue (ZstandardDecoder.TryDecompress (blob.AsSpan (16), raw, out int written));
		Assert.AreEqual (raw.Length, written);
		return raw;
	}

	static string ReadString (byte [] raw, uint offset)
	{
		int end = Array.IndexOf (raw, (byte)0, (int)offset);
		return Encoding.UTF8.GetString (raw, (int)offset, end - (int)offset);
	}

	[Test]
	public void MultipleSymbolsKeepIndependentCompressionEnvelopes ()
	{
		string xml = WriteXml (Xml);
		byte [] compressed = JniRemappingBinaryBlob.Create (xml, compress: true);
		byte [] uncompressed = JniRemappingBinaryBlob.Create (xml, compress: false);
		using var compressedStream = new MemoryStream (compressed);
		using var uncompressedStream = new MemoryStream (uncompressed);
		using var elf = new MemoryStream ();
		AssemblyStoreElfWriter.Write (
			new [] { ("remapping_data", (Stream)compressedStream), ("xa_second_blob", (Stream)uncompressedStream) },
			elf, AndroidTargetArch.Arm64, "libbinary_blobs.so");
		AssemblyStoreElfWriter.Validate (elf.ToArray (), AndroidTargetArch.Arm64, "libbinary_blobs.so",
			new [] { ("remapping_data", compressed), ("xa_second_blob", uncompressed) });
		Assert.AreEqual (1, BitConverter.ToUInt16 (compressed, 6));
		Assert.AreEqual (0, BitConverter.ToUInt16 (uncompressed, 6));
		JniRemappingBinaryBlob.Validate (compressed);
		JniRemappingBinaryBlob.Validate (uncompressed);
	}

	[Test]
	public void EmptyMappingOmitsLibrariesAndRemovesStaleOutputs ()
	{
		var task = new GenerateJniRemappingBinaryBlobs {
			BuildEngine = new MockBuildEngine (TestContext.Out),
			RemappingXmlFilePath = WriteXml (Xml),
			OutputDirectory = Path.Combine (DirectoryPath, "out"),
			SupportedAbis = ["arm64-v8a", "x86_64"],
		};
		Assert.IsTrue (task.Execute ());
		string [] libraries = task.BinaryBlobLibraries.Select (item => item.ItemSpec).ToArray ();
		Assert.AreEqual (2, libraries.Length);
		task.RemappingXmlFilePath = WriteXml ("<replacements />");
		Assert.IsTrue (task.Execute ());
		Assert.IsEmpty (task.BinaryBlobLibraries);
		foreach (string library in libraries)
			FileAssert.DoesNotExist (library);
		Assert.AreEqual ("false", File.ReadAllText (Path.Combine (task.OutputDirectory, "binary-blobs.stamp")));
		Assert.IsEmpty (JniRemappingBinaryBlob.Create (null, compress: true));
	}

	[TestCase (null)]
	[TestCase ("<replacements />")]
	public void FreshEmptyMappingWritesOnlyAbsenceStamp (string? xml)
	{
		var task = new GenerateJniRemappingBinaryBlobs {
			BuildEngine = new MockBuildEngine (TestContext.Out),
			RemappingXmlFilePath = xml == null ? "" : WriteXml (xml),
			OutputDirectory = Path.Combine (DirectoryPath, "out"),
			SupportedAbis = ["arm64-v8a"],
		};
		Assert.IsTrue (task.Execute ());
		Assert.IsEmpty (task.BinaryBlobLibraries);
		CollectionAssert.AreEqual (new [] { Path.Combine (task.OutputDirectory, "binary-blobs.stamp") },
			Directory.GetFiles (task.OutputDirectory, "*", SearchOption.AllDirectories));
		Assert.AreEqual ("false", File.ReadAllText (Path.Combine (task.OutputDirectory, "binary-blobs.stamp")));
	}

	[TestCase ("""<replacements><replace-type from="a" /></replacements>""")]
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
		byte [] compressed = JniRemappingBinaryBlob.Create (WriteXml (Xml), true);
		compressed [^1] ^= 0xff;
		Assert.Throws<InvalidDataException> (() => JniRemappingBinaryBlob.Validate (compressed));
	}
}
