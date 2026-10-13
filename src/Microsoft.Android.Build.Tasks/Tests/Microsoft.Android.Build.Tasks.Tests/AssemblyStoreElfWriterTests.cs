#nullable enable
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

using NUnit.Framework;
using Microsoft.Android.Tasks;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Build.Tests.Tasks;

[TestFixture]
public class AssemblyStoreElfWriterTests : BaseTest
{
	const string LibraryName = "libassembly-store.so";

	[Test]
	[Category ("RequiresAndroidNdk")]
	public void WritesLoadableReadOnlyPayload (
		[Values (AndroidTargetArch.Arm, AndroidTargetArch.Arm64, AndroidTargetArch.X86, AndroidTargetArch.X86_64)] AndroidTargetArch arch,
		[Values (1, 4, 4095, 4096, 4097, 16383, 16384, 16385)] int payloadSize)
	{
		byte [] payload = Enumerable.Range (0, payloadSize).Select (i => (byte)(i % 251)).ToArray ();
		using var source = new MemoryStream (payload);
		using var output = new MemoryStream ();
		AssemblyStoreElfWriter.Write (source, output, arch, LibraryName);

		bool is64Bit = arch == AndroidTargetArch.Arm64 || arch == AndroidTargetArch.X86_64;
		uint pageSize = is64Bit ? 16384u : 4096u;
		Assert.Greater (output.Length, pageSize + payloadSize, "Non-allocated section metadata follows the payload.");
		Assert.Less (output.Length, pageSize + payloadSize + 1024, "Section metadata overhead is bounded.");
		string directory = Path.Combine (Root, "temp", TestName);
		Directory.CreateDirectory (directory);
		string library = Path.Combine (directory, LibraryName);
		File.WriteAllBytes (library, output.ToArray ());
		using var document = NativeToolTestHelper.ReadElf (library);
		var elf = document.RootElement [0];
		var header = elf.GetProperty ("ElfHeader");
		Assert.AreEqual ("SharedObject (0x3)", header.GetProperty ("Type").GetString ());
		Assert.AreEqual (1, header.GetProperty ("Ident").GetProperty ("DataEncoding").GetProperty ("Value").GetInt32 ());
		Assert.AreEqual (is64Bit ? 2 : 1, header.GetProperty ("Ident").GetProperty ("Class").GetProperty ("Value").GetInt32 ());
		Assert.AreEqual (arch switch {
			AndroidTargetArch.Arm => 40,
			AndroidTargetArch.Arm64 => 183,
			AndroidTargetArch.X86 => 3,
			AndroidTargetArch.X86_64 => 62,
			_ => throw new ArgumentOutOfRangeException (nameof (arch)),
		}, header.GetProperty ("Machine").GetProperty ("Value").GetInt32 ());
		Assert.AreEqual (4, header.GetProperty ("ProgramHeaderCount").GetInt32 ());
		Assert.AreEqual ("7", header.GetProperty ("SectionHeaderCount").GetString ());
		Assert.AreEqual ("6", header.GetProperty ("StringTableSectionIndex").GetString ());
		Assert.AreEqual (arch == AndroidTargetArch.Arm ? 0x05000200u : 0u, header.GetProperty ("Flags").GetProperty ("Value").GetUInt32 (),
			"Architecture-specific ELF flags.");
		AssertLayout (elf, library, payload, pageSize, is64Bit);

		CollectionAssert.AreEqual (payload, output.ToArray ().Skip ((int)pageSize).Take (payloadSize));
	}

	static void AssertLayout (JsonElement elf, string library, byte [] data, uint pageSize, bool is64Bit)
	{
		var sections = elf.GetProperty ("Sections").EnumerateArray ()
			.Select (item => item.GetProperty ("Section")).ToDictionary (section =>
				section.GetProperty ("Name").GetProperty ("Name").GetString () ?? throw new InvalidDataException ("The native section name is missing."));
		CollectionAssert.AreEqual (new [] { "", ".dynsym", ".dynstr", ".hash", ".dynamic", "payload", ".shstrtab" }, sections.Keys);
		var payload = sections ["payload"];
		Assert.AreEqual (2u, payload.GetProperty ("Flags").GetProperty ("Value").GetUInt32 (), "Payload must be allocated, read-only and non-executable.");
		Assert.AreEqual (pageSize, payload.GetProperty ("AddressAlignment").GetUInt64 ());
		Assert.AreEqual (pageSize, payload.GetProperty ("Offset").GetUInt64 ());
		Assert.AreEqual (payload.GetProperty ("Offset").GetUInt64 (), payload.GetProperty ("Address").GetUInt64 ());
		CollectionAssert.AreEqual (data, NativeToolTestHelper.ReadSection (library, "payload"));

		var symbols = elf.GetProperty ("DynamicSymbols").EnumerateArray ().Select (item => item.GetProperty ("Symbol")).ToArray ();
		Assert.AreEqual (2, symbols.Length, "Only the undefined entry and payload symbol are needed.");
		var symbol = symbols.Single (item => item.GetProperty ("Name").GetProperty ("Name").GetString () == "_assembly_store");
		Assert.AreEqual (1, symbol.GetProperty ("Binding").GetProperty ("Value").GetInt32 (), "The payload symbol must be global.");
		Assert.AreEqual (0, symbol.GetProperty ("Other").GetProperty ("Value").GetInt32 (), "The payload symbol must have default visibility.");
		Assert.AreEqual (1, symbol.GetProperty ("Type").GetProperty ("Value").GetInt32 (), "The payload symbol must be an object.");
		Assert.AreEqual (payload.GetProperty ("Index").GetInt32 (), symbol.GetProperty ("Section").GetProperty ("Value").GetInt32 ());
		Assert.AreEqual (pageSize, symbol.GetProperty ("Value").GetUInt64 ());
		Assert.AreEqual (data.Length, symbol.GetProperty ("Size").GetUInt64 ());
		Assert.IsFalse (symbols.Any (item => item.GetProperty ("Name").GetProperty ("Name").GetString () == "_assembly_store_end"));

		var segments = elf.GetProperty ("ProgramHeaders").EnumerateArray ().Select (item => item.GetProperty ("ProgramHeader")).ToArray ();
		Assert.AreEqual (4, segments.Length);
		var load = segments.Single (item => item.GetProperty ("Type").GetProperty ("Value").GetUInt32 () == 1);
		Assert.AreEqual (4u, load.GetProperty ("Flags").GetProperty ("Value").GetUInt32 (), "No writable or executable load segments.");
		Assert.AreEqual (0, load.GetProperty ("Offset").GetUInt64 ());
		Assert.AreEqual (0, load.GetProperty ("VirtualAddress").GetUInt64 ());
		Assert.AreEqual (pageSize, load.GetProperty ("Alignment").GetUInt64 ());
		ulong loadSize = load.GetProperty ("FileSize").GetUInt64 ();
		Assert.AreEqual (pageSize + data.Length, loadSize);
		Assert.AreEqual (loadSize, load.GetProperty ("MemSize").GetUInt64 ());
		foreach (uint systemPageSize in pageSize == 16384 ? new uint [] { 4096, 16384 } : new uint [] { 4096 }) {
			Assert.AreEqual (load.GetProperty ("Offset").GetUInt64 () % systemPageSize, load.GetProperty ("VirtualAddress").GetUInt64 () % systemPageSize);
		}

		var sectionNames = sections [".shstrtab"];
		Assert.AreEqual (0, sectionNames.GetProperty ("Flags").GetProperty ("Value").GetUInt32 ());
		Assert.AreEqual (0, sectionNames.GetProperty ("Address").GetUInt64 ());
		Assert.GreaterOrEqual (sectionNames.GetProperty ("Offset").GetUInt64 (), loadSize, "Section names must be outside PT_LOAD for stripping.");
		Assert.GreaterOrEqual (elf.GetProperty ("ElfHeader").GetProperty ("SectionHeaderOffset").GetUInt64 (),
			sectionNames.GetProperty ("Offset").GetUInt64 () + sectionNames.GetProperty ("Size").GetUInt64 ());

		var programHeaders = segments.Single (item => item.GetProperty ("Type").GetProperty ("Value").GetUInt32 () == 6);
		Assert.Greater (programHeaders.GetProperty ("Offset").GetUInt64 (), 0);
		Assert.LessOrEqual (programHeaders.GetProperty ("Offset").GetUInt64 () + programHeaders.GetProperty ("FileSize").GetUInt64 (), loadSize);
		var stack = segments.Single (item => item.GetProperty ("Type").GetProperty ("Value").GetUInt32 () == 0x6474e551);
		Assert.AreEqual (6u, stack.GetProperty ("Flags").GetProperty ("Value").GetUInt32 (), "The stack must not be executable.");

		var dynamicSection = sections [".dynamic"];
		var dynamicSegment = segments.Single (item => item.GetProperty ("Type").GetProperty ("Value").GetUInt32 () == 2);
		Assert.AreEqual (2u, dynamicSection.GetProperty ("Flags").GetProperty ("Value").GetUInt32 ());
		Assert.AreEqual (4u, dynamicSegment.GetProperty ("Flags").GetProperty ("Value").GetUInt32 ());
		Assert.AreEqual (dynamicSection.GetProperty ("Offset").GetUInt64 (), dynamicSegment.GetProperty ("Offset").GetUInt64 ());
		Assert.AreEqual (dynamicSection.GetProperty ("Size").GetUInt64 (), dynamicSegment.GetProperty ("FileSize").GetUInt64 ());
		Assert.AreEqual (dynamicSection.GetProperty ("Address").GetUInt64 (), dynamicSegment.GetProperty ("VirtualAddress").GetUInt64 ());
		Assert.LessOrEqual (dynamicSegment.GetProperty ("Offset").GetUInt64 () + dynamicSegment.GetProperty ("FileSize").GetUInt64 (), loadSize);

		var tags = new Dictionary<ulong, ulong> ();
		using (var reader = new BinaryReader (new MemoryStream (NativeToolTestHelper.ReadSection (library, ".dynamic")))) {
			while (reader.BaseStream.Position < reader.BaseStream.Length) {
				ulong tag = is64Bit ? reader.ReadUInt64 () : reader.ReadUInt32 ();
				ulong value = is64Bit ? reader.ReadUInt64 () : reader.ReadUInt32 ();
				tags.Add (tag, value);
			}
		}
		CollectionAssert.AreEquivalent (new ulong [] { 0, 4, 5, 6, 10, 11, 14 }, tags.Keys,
			"No dependencies, relocations, constructors or other dynamic linker work.");
		Assert.AreEqual (0, tags [0]);
		Assert.AreEqual (is64Bit ? 24 : 16, tags [11]);
		Assert.AreEqual (sections [".hash"].GetProperty ("Address").GetUInt64 (), tags [4]);
		Assert.AreEqual (sections [".dynstr"].GetProperty ("Address").GetUInt64 (), tags [5]);
		Assert.AreEqual (sections [".dynsym"].GetProperty ("Address").GetUInt64 (), tags [6]);
		byte [] strings = NativeToolTestHelper.ReadSection (library, ".dynstr");
		Assert.AreEqual (strings.Length, tags [10]);
		Assert.AreEqual (LibraryName + "\0", Encoding.UTF8.GetString (strings, (int)tags [14], strings.Length - (int)tags [14]));

		using var hashReader = new BinaryReader (new MemoryStream (NativeToolTestHelper.ReadSection (library, ".hash")));
		CollectionAssert.AreEqual (new uint [] { 1, 2, 1, 0, 0 }, Enumerable.Range (0, 5).Select (_ => hashReader.ReadUInt32 ()));
		Assert.AreEqual (hashReader.BaseStream.Length, hashReader.BaseStream.Position);
	}

	[Test]
	public void WritesMultipleNamedPayloads (
		[Values (AndroidTargetArch.Arm, AndroidTargetArch.Arm64, AndroidTargetArch.X86, AndroidTargetArch.X86_64)] AndroidTargetArch arch)
	{
		byte [] first = [1, 2, 3];
		byte [] second = [4, 5, 6, 7];
		using var a = new MemoryStream (first);
		using var b = new MemoryStream (second);
		using var elf = new MemoryStream ();
		AssemblyStoreElfWriter.Write (new [] { ("xa_first", (Stream)a), ("xa_second", (Stream)b) }, elf, arch, "libbinary_blobs.so");
		byte [] image = elf.ToArray ();
		bool is64Bit = arch == AndroidTargetArch.Arm64 || arch == AndroidTargetArch.X86_64;
		int wordSize = is64Bit ? 8 : 4;
		int headerSize = is64Bit ? 64 : 52;
		int programHeaderSize = is64Bit ? 56 : 32;
		int symbolSize = is64Bit ? 24 : 16;
		ulong symbolsOffset = checked ((ulong)((headerSize + 4 * programHeaderSize + wordSize - 1) & -wordSize));
		ulong stringsOffset = symbolsOffset + (ulong)(3 * symbolSize);
		ulong payloadOffset = is64Bit ? 16384u : 4096u;
		AssertPayloadSymbol (image, is64Bit, symbolSize, symbolsOffset, stringsOffset, payloadOffset, 1, 1, "xa_first", first);
		AssertPayloadSymbol (image, is64Bit, symbolSize, symbolsOffset, stringsOffset, payloadOffset + (ulong)first.Length,
			2, "xa_first".Length + 2, "xa_second", second);
		Assert.AreEqual (a.Length, a.Position);
		Assert.AreEqual (b.Length, b.Position);
	}

	static void AssertPayloadSymbol (byte [] image, bool is64Bit, int symbolSize, ulong symbolsOffset, ulong stringsOffset,
		ulong payloadOffset, int symbolIndex, int nameOffset, string name, byte [] payload)
	{
		int entryOffset = checked ((int)(symbolsOffset + (ulong)(symbolIndex * symbolSize)));
		Assert.AreEqual ((uint)nameOffset, BinaryPrimitives.ReadUInt32LittleEndian (image.AsSpan (entryOffset, 4)));
		int infoOffset = entryOffset + (is64Bit ? 4 : 12);
		Assert.AreEqual (0x11, image [infoOffset], "The exported symbol must be a global object.");
		Assert.AreEqual (0, image [infoOffset + 1], "The symbol must have default visibility.");
		Assert.AreEqual (5, BinaryPrimitives.ReadUInt16LittleEndian (image.AsSpan (infoOffset + 2, 2)));
		ulong value = is64Bit
			? BinaryPrimitives.ReadUInt64LittleEndian (image.AsSpan (entryOffset + 8, 8))
			: BinaryPrimitives.ReadUInt32LittleEndian (image.AsSpan (entryOffset + 4, 4));
		ulong size = is64Bit
			? BinaryPrimitives.ReadUInt64LittleEndian (image.AsSpan (entryOffset + 16, 8))
			: BinaryPrimitives.ReadUInt32LittleEndian (image.AsSpan (entryOffset + 8, 4));
		Assert.AreEqual (payloadOffset, value);
		Assert.AreEqual ((ulong)payload.Length, size);
		CollectionAssert.AreEqual (Encoding.UTF8.GetBytes (name + "\0"),
			image.AsSpan (checked ((int)(stringsOffset + (ulong)nameOffset)), name.Length + 1).ToArray ());
		CollectionAssert.AreEqual (payload, image.AsSpan (checked ((int)payloadOffset), payload.Length).ToArray ());
	}

	[Test]
	public void MultiSymbolRejectsInvalidInputsBeforeWriting ()
	{
		using var first = new MemoryStream (new byte [] { 1 });
		using var output = new MemoryStream ();
		Assert.Throws<ArgumentException> (() => AssemblyStoreElfWriter.Write (
			new [] { ("xa_same", (Stream)first), ("xa_same", (Stream)first) }, output, AndroidTargetArch.Arm64, "libbinary_blobs.so"));
		Assert.Throws<ArgumentException> (() => AssemblyStoreElfWriter.Write (
			new [] { ("bad\0name", (Stream)first) }, output, AndroidTargetArch.Arm64, "libbinary_blobs.so"));
		Assert.Zero (output.Length);
	}

	[Test]
	public void OutputIsDeterministicAndTruncatesPreviousContents ()
	{
		byte [] data = [1, 2, 3, 4, 5];
		using var payload = new MemoryStream (data);
		using var first = new MemoryStream ();
		AssemblyStoreElfWriter.Write (payload, first, AndroidTargetArch.Arm64, LibraryName);

		payload.Position = 0;
		using var second = new MemoryStream ();
		second.SetLength (100000);
		AssemblyStoreElfWriter.Write (payload, second, AndroidTargetArch.Arm64, LibraryName);
		CollectionAssert.AreEqual (first.ToArray (), second.ToArray ());
	}

	[Test]
	public void CopiesFromCurrentPositionAndLeavesStreamsOpen ()
	{
		using var payload = new MemoryStream (new byte [] { 1, 2, 3, 4, 5 });
		payload.Position = 2;
		using var output = new MemoryStream ();
		AssemblyStoreElfWriter.Write (payload, output, AndroidTargetArch.Arm64, LibraryName);

		Assert.AreEqual (payload.Length, payload.Position);
		Assert.IsTrue (payload.CanRead);
		Assert.IsTrue (output.CanWrite);
		CollectionAssert.AreEqual (new byte [] { 3, 4, 5 }, output.ToArray ().Skip (16384).Take (3));
	}

	[Test]
	public void EmptyPayloadIsRejected ()
	{
		using var source = new MemoryStream ();
		using var output = new MemoryStream ();
		Assert.Throws<InvalidDataException> (() => AssemblyStoreElfWriter.Write (source, output, AndroidTargetArch.Arm64, LibraryName));
		Assert.AreEqual (0, output.Length);
	}

	[TestCase (AndroidTargetArch.Arm, (long)uint.MaxValue)]
	[TestCase (AndroidTargetArch.X86, (long)uint.MaxValue)]
	[TestCase (AndroidTargetArch.Arm64, (long)uint.MaxValue + 1)]
	[TestCase (AndroidTargetArch.X86_64, (long)uint.MaxValue + 1)]
	public void OversizedPayloadIsRejectedBeforeWriting (AndroidTargetArch arch, long length)
	{
		using var source = new OversizedPayloadStream (length);
		using var output = new MemoryStream ();
		Assert.Throws<NotSupportedException> (() => AssemblyStoreElfWriter.Write (source, output, arch, LibraryName));
		Assert.AreEqual (0, output.Length);
	}

	[Test]
	public void UnsupportedArchitectureIsRejected ()
	{
		using var source = new MemoryStream (new byte [] { 1 });
		using var output = new MemoryStream ();
		Assert.Throws<NotSupportedException> (() => AssemblyStoreElfWriter.Write (source, output, (AndroidTargetArch)int.MaxValue, LibraryName));
		Assert.AreEqual (0, output.Length);
	}

	[TestCase ("")]
	[TestCase ("lib\0store.so")]
	public void InvalidLibraryNameIsRejected (string libraryName)
	{
		using var source = new MemoryStream (new byte [] { 1 });
		using var output = new MemoryStream ();
		Assert.Throws<ArgumentException> (() => AssemblyStoreElfWriter.Write (source, output, AndroidTargetArch.Arm64, libraryName));
		Assert.AreEqual (0, output.Length);
	}

	[Test]
	public void SameStreamIsRejectedBeforeWriting ()
	{
		using var stream = new MemoryStream (new byte [] { 1, 2, 3 });
		Assert.Throws<ArgumentException> (() => AssemblyStoreElfWriter.Write (stream, stream, AndroidTargetArch.Arm64, LibraryName));
		CollectionAssert.AreEqual (new byte [] { 1, 2, 3 }, stream.ToArray ());
	}

	[Test]
	public void ChangingPayloadLengthIsRejected ()
	{
		using var source = new OversizedPayloadStream (10);
		using var output = new MemoryStream ();
		Assert.Throws<IOException> (() => AssemblyStoreElfWriter.Write (source, output, AndroidTargetArch.Arm64, LibraryName));
	}

	sealed class OversizedPayloadStream : MemoryStream
	{
		readonly long length;
		public override long Length => length;

		public OversizedPayloadStream (long length)
		{
			this.length = length;
		}
	}
}
