#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

using ELFSharp;
using ELFSharp.ELF;
using ELFSharp.ELF.Sections;
using ELFSharp.ELF.Segments;
using NUnit.Framework;
using Microsoft.Android.Tasks;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Build.Tests.Tasks;

[TestFixture]
public class AssemblyStoreElfWriterTests
{
	const string LibraryName = "libassembly-store.so";

	[Test]
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
		output.Position = 0;
		using (IELF elf = ELFReader.Load (output, shouldOwnStream: false)) {
			Assert.AreEqual (FileType.SharedObject, elf.Type);
			Assert.AreEqual (Endianess.LittleEndian, elf.Endianess);
			Assert.AreEqual (is64Bit ? Class.Bit64 : Class.Bit32, elf.Class);
			Assert.AreEqual (arch switch {
				AndroidTargetArch.Arm => Machine.ARM,
				AndroidTargetArch.Arm64 => Machine.AArch64,
				AndroidTargetArch.X86 => Machine.Intel386,
				AndroidTargetArch.X86_64 => Machine.AMD64,
				_ => throw new ArgumentOutOfRangeException (nameof (arch)),
			}, elf.Machine);
			Assert.IsTrue (elf.HasSectionHeader);
			Assert.IsTrue (elf.HasSectionsStringTable);
			Assert.AreEqual (4, elf.Segments.Count ());
			CollectionAssert.AreEqual (new [] { "", ".dynsym", ".dynstr", ".hash", ".dynamic", "payload", ".shstrtab" },
				elf.Sections.Select (section => section.Name));

			if (elf is ELF<ulong> elf64) {
				AssertLayout (elf64, payload, pageSize);
			} else if (elf is ELF<uint> elf32) {
				AssertLayout (elf32, payload, pageSize);
			} else {
				Assert.Fail ("Unexpected ELF class.");
			}
		}

		CollectionAssert.AreEqual (payload, output.ToArray ().Skip ((int)pageSize).Take (payloadSize));
		output.Position = is64Bit ? 48 : 36;
		using var reader = new BinaryReader (output, Encoding.UTF8, leaveOpen: true);
		Assert.AreEqual (arch == AndroidTargetArch.Arm ? 0x05000200u : 0u, reader.ReadUInt32 (), "Architecture-specific ELF flags.");
	}

	static void AssertLayout<T> (ELF<T> elf, byte [] data, uint pageSize) where T : struct
	{
		var payload = (Section<T>)elf.GetSection ("payload");
		Assert.AreEqual (2u, (ulong)payload.Flags, "Payload must be allocated, read-only and non-executable.");
		Assert.AreEqual (pageSize, AsUInt64 (payload.Alignment));
		Assert.AreEqual (pageSize, AsUInt64 (payload.Offset));
		Assert.AreEqual (AsUInt64 (payload.Offset), AsUInt64 (payload.LoadAddress));
		CollectionAssert.AreEqual (data, payload.GetContents ());

		var symbols = (ISymbolTable)elf.GetSection (".dynsym");
		Assert.AreEqual (2, symbols.Entries.Count (), "Only the undefined entry and payload symbol are needed.");
		var symbol = (SymbolEntry<T>)symbols.Entries.Single (s => s.Name == "_assembly_store");
		Assert.AreEqual (SymbolBinding.Global, symbol.Binding);
		Assert.AreEqual (SymbolVisibility.Default, symbol.Visibility);
		Assert.AreEqual (SymbolType.Object, symbol.Type);
		Assert.AreSame (payload, symbol.PointedSection);
		Assert.AreEqual (pageSize, AsUInt64 (symbol.Value));
		Assert.AreEqual (data.Length, AsUInt64 (symbol.Size));
		Assert.IsFalse (symbols.Entries.Any (s => s.Name == "_assembly_store_end"));

		var load = (Segment<T>)elf.Segments.Single (s => s.Type == SegmentType.Load);
		Assert.AreEqual (4u, (uint)load.Flags, "No writable or executable load segments.");
		Assert.AreEqual (0, load.Offset);
		Assert.AreEqual (0, AsUInt64 (load.Address));
		Assert.AreEqual (pageSize, AsUInt64 (load.Alignment));
		Assert.AreEqual (pageSize + data.Length, load.FileSize);
		Assert.AreEqual (load.FileSize, AsUInt64 (load.Size));
		foreach (uint systemPageSize in pageSize == 16384 ? new uint [] { 4096, 16384 } : new uint [] { 4096 }) {
			Assert.AreEqual ((ulong)load.Offset % systemPageSize, AsUInt64 (load.Address) % systemPageSize);
		}

		var sectionNames = (Section<T>)elf.GetSection (".shstrtab");
		Assert.AreEqual (0, (ulong)sectionNames.Flags);
		Assert.AreEqual (0, AsUInt64 (sectionNames.LoadAddress));
		Assert.GreaterOrEqual (AsUInt64 (sectionNames.Offset), (ulong)load.FileSize, "Section names must be outside PT_LOAD for stripping.");

		var programHeaders = (Segment<T>)elf.Segments.Single (s => s.Type == SegmentType.ProgramHeader);
		Assert.Greater (programHeaders.Offset, 0);
		Assert.LessOrEqual (programHeaders.Offset + programHeaders.FileSize, load.FileSize);
		var stack = elf.Segments.Single (s => (uint)s.Type == 0x6474e551);
		Assert.AreEqual (6u, (uint)stack.Flags, "The stack must not be executable.");

		var dynamicSection = (Section<T>)elf.GetSection (".dynamic");
		var dynamicSegment = (Segment<T>)elf.Segments.Single (s => s.Type == SegmentType.Dynamic);
		Assert.AreEqual (2u, (ulong)dynamicSection.Flags);
		Assert.AreEqual (4u, (uint)dynamicSegment.Flags);
		Assert.AreEqual (AsUInt64 (dynamicSection.Offset), (ulong)dynamicSegment.Offset);
		Assert.AreEqual (AsUInt64 (dynamicSection.Size), (ulong)dynamicSegment.FileSize);
		Assert.AreEqual (AsUInt64 (dynamicSection.LoadAddress), AsUInt64 (dynamicSegment.Address));
		Assert.LessOrEqual (dynamicSegment.Offset + dynamicSegment.FileSize, load.FileSize);

		var tags = new Dictionary<ulong, ulong> ();
		using (var reader = new BinaryReader (new MemoryStream (dynamicSection.GetContents ()))) {
			while (reader.BaseStream.Position < reader.BaseStream.Length) {
				ulong tag = elf.Class == Class.Bit64 ? reader.ReadUInt64 () : reader.ReadUInt32 ();
				ulong value = elf.Class == Class.Bit64 ? reader.ReadUInt64 () : reader.ReadUInt32 ();
				tags.Add (tag, value);
			}
		}
		CollectionAssert.AreEquivalent (new ulong [] { 0, 4, 5, 6, 10, 11, 14 }, tags.Keys,
			"No dependencies, relocations, constructors or other dynamic linker work.");
		Assert.AreEqual (0, tags [0]);
		Assert.AreEqual (elf.Class == Class.Bit64 ? 24 : 16, tags [11]);
		Assert.AreEqual (AsUInt64 (((Section<T>)elf.GetSection (".hash")).LoadAddress), tags [4]);
		Assert.AreEqual (AsUInt64 (((Section<T>)elf.GetSection (".dynstr")).LoadAddress), tags [5]);
		Assert.AreEqual (AsUInt64 (((Section<T>)elf.GetSection (".dynsym")).LoadAddress), tags [6]);
		byte [] strings = elf.GetSection (".dynstr").GetContents ();
		Assert.AreEqual (strings.Length, tags [10]);
		Assert.AreEqual (LibraryName + "\0", Encoding.UTF8.GetString (strings, (int)tags [14], strings.Length - (int)tags [14]));

		using var hashReader = new BinaryReader (new MemoryStream (elf.GetSection (".hash").GetContents ()));
		CollectionAssert.AreEqual (new uint [] { 1, 2, 1, 0, 0 }, Enumerable.Range (0, 5).Select (_ => hashReader.ReadUInt32 ()));
		Assert.AreEqual (hashReader.BaseStream.Length, hashReader.BaseStream.Position);
	}

	static ulong AsUInt64<T> (T value) where T : struct => Convert.ToUInt64 (value, CultureInfo.InvariantCulture);

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
		output.Position = 0;
		using var elf = ELFReader.Load (output, shouldOwnStream: false);
		CollectionAssert.AreEqual (new byte [] { 3, 4, 5 }, elf.GetSection ("payload").GetContents ());
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
