#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using Xamarin.Android.Tools;

namespace Microsoft.Android.Tasks;

// A data-only ET_DYN image: loadable file offsets are also virtual addresses, with one
// read-only PT_LOAD covering the headers, dynamic metadata and assembly store.
static class AssemblyStoreElfWriter
{
	public const string PayloadSymbol = "_assembly_store";

	const ushort ProgramHeaderCount = 4;
	const ushort SectionCount = 7;
	const ushort PayloadSectionIndex = 5;
	const ushort SectionNamesIndex = 6;
	const uint DynamicEntryCount = 7;
	const uint ReadOnly = 4; // PF_R
	const uint Allocated = 2; // SHF_ALLOC

	static readonly byte [] SectionNames = Encoding.ASCII.GetBytes ("\0.dynsym\0.dynstr\0.hash\0.dynamic\0payload\0.shstrtab\0");
	static readonly UTF8Encoding Utf8 = new (false, true);

	sealed class Layout
	{
		public bool Is64Bit { get; }
		public ushort Machine { get; }
		public uint Flags { get; }
		public uint PageSize { get; }
		public byte [] Strings { get; }
		public IReadOnlyList<(string Name, Stream Data, ulong Size)> Payloads { get; }
		public ulong PayloadSize { get; }
		public uint SymbolCount => checked ((uint)Payloads.Count + 1);

		public uint WordSize => Is64Bit ? 8u : 4u;
		public ushort HeaderSize => (ushort)(Is64Bit ? 64 : 52);
		public ushort ProgramHeaderSize => (ushort)(Is64Bit ? 56 : 32);
		public ushort SectionHeaderSize => (ushort)(Is64Bit ? 64 : 40);
		public uint SymbolSize => Is64Bit ? 24u : 16u;
		public ulong DynamicSize => DynamicEntryCount * WordSize * 2;
		public uint SonameIndex { get; }
		public ulong SymbolsOffset => Align ((ulong)HeaderSize + (ulong)ProgramHeaderCount * ProgramHeaderSize, WordSize);
		public ulong StringsOffset => SymbolsOffset + SymbolCount * SymbolSize;
		public ulong HashOffset => Align (StringsOffset + (ulong)Strings.Length, sizeof (uint));
		public ulong HashSize => checked ((ulong)(3 + SymbolCount) * sizeof (uint));
		public ulong DynamicOffset => Align (HashOffset + HashSize, WordSize);
		public ulong PayloadOffset => Align (DynamicOffset + DynamicSize, PageSize);
		public ulong LoadSize => checked (PayloadOffset + PayloadSize);

		// Keep non-allocated metadata outside PT_LOAD so strip/objcopy can rebuild it.
		public ulong SectionNamesOffset => LoadSize;
		public ulong SectionHeadersOffset => Align (SectionNamesOffset + (ulong)SectionNames.Length, WordSize);
		public ulong FileSize => checked (SectionHeadersOffset + (ulong)SectionCount * SectionHeaderSize);

		public Layout (AndroidTargetArch arch, IReadOnlyList<(string Name, Stream Data, ulong Size)> payloads, string libraryName)
		{
			(Is64Bit, Machine, Flags, PageSize) = arch switch {
				AndroidTargetArch.Arm => (false, (ushort)40, 0x05000200u, 4096u), // armeabi-v7a: EABI5, base (softfp) calling convention
				AndroidTargetArch.Arm64 => (true, (ushort)183, 0u, 16384u),
				AndroidTargetArch.X86 => (false, (ushort)3, 0u, 4096u),
				AndroidTargetArch.X86_64 => (true, (ushort)62, 0u, 16384u),
				_ => throw new NotSupportedException ($"Unsupported assembly-store architecture: {arch}"),
			};
			Payloads = payloads;
			using var strings = new MemoryStream ();
			strings.WriteByte (0);
			foreach (var payload in payloads) {
				var name = Utf8.GetBytes (payload.Name);
				strings.Write (name);
				strings.WriteByte (0);
				PayloadSize = checked (PayloadSize + payload.Size);
			}
			SonameIndex = checked ((uint)strings.Position);
			var soname = Utf8.GetBytes (libraryName);
			strings.Write (soname);
			strings.WriteByte (0);
			Strings = strings.ToArray ();
			if (PayloadSize > uint.MaxValue) {
				throw new NotSupportedException ("ELF payloads cannot exceed 4 GiB.");
			}
			if (!Is64Bit && FileSize > uint.MaxValue) {
				throw new NotSupportedException ("The assembly store and ELF headers exceed the ELF32 size limit.");
			}
		}
	}

	public static void Write (Stream payload, Stream output, AndroidTargetArch arch, string libraryName)
	{
		ArgumentNullException.ThrowIfNull (payload);
		Write (new [] { (Name: PayloadSymbol, Data: payload) }, output, arch, libraryName);
	}

	// Each stream is already encoded independently; the ELF layer never changes a symbol's payload.
	public static void Write (IReadOnlyList<(string Name, Stream Data)> payloads, Stream output, AndroidTargetArch arch, string libraryName)
	{
		ArgumentNullException.ThrowIfNull (payloads);
		ArgumentNullException.ThrowIfNull (output);
		ArgumentNullException.ThrowIfNull (libraryName);
		if (libraryName.Length == 0 || libraryName.IndexOf ('\0') >= 0) {
			throw new ArgumentException ("The shared-library name must be nonempty and contain no NUL characters.", nameof (libraryName));
		}
		if (!output.CanWrite || !output.CanSeek) {
			throw new ArgumentException ("The output must be a separate writable, seekable stream.", nameof (output));
		}

		if (payloads.Count == 0) {
			throw new InvalidDataException ("At least one payload symbol is required.");
		}
		var names = new HashSet<string> (StringComparer.Ordinal);
		var streams = new HashSet<Stream> (ReferenceEqualityComparer.Instance);
		var entries = new List<(string Name, Stream Data, ulong Size)> (payloads.Count);
		foreach (var (name, data) in payloads) {
			if (string.IsNullOrEmpty (name) || name.IndexOf ('\0') >= 0 || !names.Add (name)) {
				throw new ArgumentException ("Payload symbols must be distinct, nonempty and contain no NUL.", nameof (payloads));
			}
			Utf8.GetByteCount (name);
			if (data == null || !data.CanRead || !data.CanSeek || ReferenceEquals (data, output) || !streams.Add (data)) {
				throw new ArgumentException ("Each payload must have a separate readable, seekable stream.", nameof (payloads));
			}
			long size = checked (data.Length - data.Position);
			if (size <= 0) {
				throw new InvalidDataException ("Payload symbols must not be empty.");
			}
			entries.Add ((name, data, checked ((ulong)size)));
		}
		var layout = new Layout (arch, entries, libraryName);
		output.SetLength (0);
		output.Position = 0;
		using var writer = new BinaryWriter (output, Encoding.UTF8, leaveOpen: true);
		WriteHeader (writer, layout);
		WriteProgramHeader (writer, layout, 6, ReadOnly, layout.HeaderSize,
			(ulong)ProgramHeaderCount * layout.ProgramHeaderSize, layout.WordSize); // PT_PHDR
		WriteProgramHeader (writer, layout, 1, ReadOnly, 0, layout.LoadSize, layout.PageSize); // PT_LOAD
		WriteProgramHeader (writer, layout, 2, ReadOnly, layout.DynamicOffset, layout.DynamicSize, layout.WordSize); // PT_DYNAMIC
		WriteProgramHeader (writer, layout, 0x6474e551, 6, 0, 0, layout.WordSize); // PT_GNU_STACK: RW, not executable

		output.Position = (long)layout.SymbolsOffset;
		WriteSymbol (writer, layout, 0, 0, 0, 0, 0);
		uint nameOffset = 1;
		ulong payloadOffset = layout.PayloadOffset;
		foreach (var (name, _, size) in entries) {
			WriteSymbol (writer, layout, nameOffset, 0x11, PayloadSectionIndex, payloadOffset, size); // STB_GLOBAL | STT_OBJECT
			nameOffset = checked (nameOffset + (uint)Utf8.GetByteCount (name) + 1);
			payloadOffset = checked (payloadOffset + size);
		}
		output.Position = (long)layout.StringsOffset;
		writer.Write (layout.Strings);

		output.Position = (long)layout.HashOffset;
		// One bucket links every exported symbol, regardless of its name's hash.
		writer.Write (1u);
		writer.Write (layout.SymbolCount);
		writer.Write (1u);
		writer.Write (0u);
		for (uint i = 1; i < layout.SymbolCount; i++) {
			writer.Write (i + 1 < layout.SymbolCount ? i + 1 : 0u);
		}

		output.Position = (long)layout.DynamicOffset;
		WriteDynamicEntry (writer, layout, 14, layout.SonameIndex); // DT_SONAME
		WriteDynamicEntry (writer, layout, 6, layout.SymbolsOffset); // DT_SYMTAB
		WriteDynamicEntry (writer, layout, 11, layout.SymbolSize); // DT_SYMENT
		WriteDynamicEntry (writer, layout, 5, layout.StringsOffset); // DT_STRTAB
		WriteDynamicEntry (writer, layout, 10, (ulong)layout.Strings.Length); // DT_STRSZ
		WriteDynamicEntry (writer, layout, 4, layout.HashOffset); // DT_HASH
		WriteDynamicEntry (writer, layout, 0, 0); // DT_NULL

		output.Position = (long)layout.SectionNamesOffset;
		writer.Write (SectionNames);
		output.Position = (long)layout.SectionHeadersOffset;
		WriteSectionHeader (writer, layout, 0, 0, 0, 0, 0, 0, 0, 0, 0);
		WriteSectionHeader (writer, layout, 1, 11, Allocated, layout.SymbolsOffset, layout.SymbolCount * layout.SymbolSize, 2, 1, layout.WordSize, layout.SymbolSize);
		WriteSectionHeader (writer, layout, 9, 3, Allocated, layout.StringsOffset, (ulong)layout.Strings.Length, 0, 0, 1, 0);
		WriteSectionHeader (writer, layout, 17, 5, Allocated, layout.HashOffset, layout.HashSize, 1, 0, sizeof (uint), sizeof (uint));
		WriteSectionHeader (writer, layout, 23, 6, Allocated, layout.DynamicOffset, layout.DynamicSize, 2, 0, layout.WordSize, layout.WordSize * 2);
		WriteSectionHeader (writer, layout, 32, 1, Allocated, layout.PayloadOffset, layout.PayloadSize, 0, 0, layout.PageSize, 0);
		WriteSectionHeader (writer, layout, 40, 3, 0, layout.SectionNamesOffset, (ulong)SectionNames.Length, 0, 0, 1, 0);

		writer.Flush ();
		output.Position = (long)layout.PayloadOffset;
		foreach (var (_, data, size) in entries) {
			ulong end = checked ((ulong)output.Position + size);
			byte [] buffer = new byte [81920];
			ulong remaining = size;
			while (remaining > 0) {
				int read = data.Read (buffer, 0, (int)Math.Min ((ulong)buffer.Length, remaining));
				if (read == 0) {
					throw new IOException ("An ELF payload ended before its declared length.");
				}
				output.Write (buffer, 0, read);
				remaining -= (uint)read;
			}
			if ((ulong)output.Position != end || data.Position != data.Length) {
				throw new IOException ("An ELF payload length changed while writing its wrapper.");
			}
		}
	}

	public static void Validate (byte [] elf, AndroidTargetArch arch, string libraryName, IReadOnlyList<(string Name, byte [] Data)> expected)
	{
		ArgumentNullException.ThrowIfNull (elf);
		ArgumentNullException.ThrowIfNull (expected);
		var streams = new List<MemoryStream> ();
		try {
			var payloads = new List<(string Name, Stream Data)> ();
			foreach (var (name, data) in expected) {
				var stream = new MemoryStream (data, writable: false);
				streams.Add (stream);
				payloads.Add ((name, stream));
			}
			var layout = new Layout (arch, payloads.ConvertAll (entry => (entry.Name, entry.Data, (ulong)entry.Data.Length)), libraryName);
			if ((ulong)elf.Length != layout.FileSize || elf [0] != 0x7f || elf [1] != 'E' || elf [2] != 'L' || elf [3] != 'F' ||
				elf [4] != (layout.Is64Bit ? 2 : 1) || elf [5] != 1) {
				throw new InvalidDataException ("Invalid ELF header or size.");
			}
			using var reader = new BinaryReader (new MemoryStream (elf));
			reader.BaseStream.Position = 16;
			if (reader.ReadUInt16 () != 3 || reader.ReadUInt16 () != layout.Machine || reader.ReadUInt32 () != 1) {
				throw new InvalidDataException ("Invalid ELF machine or type.");
			}
			reader.BaseStream.Position = layout.HeaderSize;
			for (int i = 0; i < ProgramHeaderCount; i++) {
				uint type = reader.ReadUInt32 ();
				uint flags = layout.Is64Bit ? reader.ReadUInt32 () : 0;
				ulong offset = layout.Is64Bit ? reader.ReadUInt64 () : reader.ReadUInt32 ();
				reader.BaseStream.Position += layout.Is64Bit ? 16 : 8;
				ulong size = layout.Is64Bit ? reader.ReadUInt64 () : reader.ReadUInt32 ();
				ulong memSize = layout.Is64Bit ? reader.ReadUInt64 () : reader.ReadUInt32 ();
				if (!layout.Is64Bit) flags = reader.ReadUInt32 ();
				reader.BaseStream.Position += layout.WordSize;
				if (type == 1 && (flags != ReadOnly || offset != 0 || size != layout.LoadSize || memSize != size) ||
					type == 2 && (flags != ReadOnly || offset != layout.DynamicOffset || size != layout.DynamicSize) ||
					type == 0x6474e551 && flags != 6) {
					throw new InvalidDataException ("ELF load or dynamic segment is not read-only and bounded.");
				}
			}
			reader.BaseStream.Position = (long)layout.SectionHeadersOffset + PayloadSectionIndex * layout.SectionHeaderSize;
			reader.BaseStream.Position += 8;
			ulong sectionFlags = layout.Is64Bit ? reader.ReadUInt64 () : reader.ReadUInt32 ();
			if (sectionFlags != Allocated) throw new InvalidDataException ("ELF payload section is not read-only.");
			reader.BaseStream.Position = (long)layout.SymbolsOffset + layout.SymbolSize;
			ulong position = layout.PayloadOffset;
			uint nameOffset = 1;
			foreach (var (name, data) in expected) {
				uint symbolName = reader.ReadUInt32 ();
				ulong value = layout.Is64Bit ? 0 : reader.ReadUInt32 ();
				ulong length = layout.Is64Bit ? 0 : reader.ReadUInt32 ();
				byte info = reader.ReadByte ();
				byte visibility = reader.ReadByte ();
				ushort section = reader.ReadUInt16 ();
				if (layout.Is64Bit) {
					value = reader.ReadUInt64 ();
					length = reader.ReadUInt64 ();
				}
				if (symbolName != nameOffset || info != 0x11 || visibility != 0 || section != PayloadSectionIndex ||
					value != position || length != (ulong)data.Length ||
					!elf.AsSpan ((int)position, data.Length).SequenceEqual (data)) {
					throw new InvalidDataException ("Invalid ELF payload symbol or contents.");
				}
				nameOffset = checked (nameOffset + (uint)Utf8.GetByteCount (name) + 1);
				position += (ulong)data.Length;
			}
			reader.BaseStream.Position = (long)layout.HashOffset;
			if (reader.ReadUInt32 () != 1 || reader.ReadUInt32 () != layout.SymbolCount || reader.ReadUInt32 () != 1 ||
				reader.ReadUInt32 () != 0) throw new InvalidDataException ("Invalid ELF symbol hash table.");
			for (uint i = 1; i < layout.SymbolCount; i++) {
				if (reader.ReadUInt32 () != (i + 1 < layout.SymbolCount ? i + 1 : 0)) throw new InvalidDataException ("Broken ELF symbol hash chain.");
			}
			using var canonical = new MemoryStream ();
			Write (payloads, canonical, arch, libraryName);
			if (!canonical.ToArray ().AsSpan ().SequenceEqual (elf)) {
				throw new InvalidDataException ("ELF metadata does not match the payload layout.");
			}
		} finally {
			foreach (var stream in streams) stream.Dispose ();
		}
	}

	static ulong Align (ulong value, uint alignment) => checked (value + alignment - 1) & ~((ulong)alignment - 1);

	static void WriteWord (BinaryWriter writer, Layout layout, ulong value)
	{
		if (layout.Is64Bit) {
			writer.Write (value);
		} else {
			writer.Write (checked ((uint)value));
		}
	}

	static void WriteHeader (BinaryWriter writer, Layout layout)
	{
		writer.Write (new byte [] { 0x7f, (byte)'E', (byte)'L', (byte)'F', (byte)(layout.Is64Bit ? 2 : 1), 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0 });
		writer.Write ((ushort)3); // ET_DYN
		writer.Write (layout.Machine);
		writer.Write (1u); // EV_CURRENT
		WriteWord (writer, layout, 0); // No entry point.
		WriteWord (writer, layout, layout.HeaderSize);
		WriteWord (writer, layout, layout.SectionHeadersOffset);
		writer.Write (layout.Flags);
		writer.Write (layout.HeaderSize);
		writer.Write (layout.ProgramHeaderSize);
		writer.Write (ProgramHeaderCount);
		writer.Write (layout.SectionHeaderSize);
		writer.Write (SectionCount);
		writer.Write (SectionNamesIndex);
	}

	static void WriteProgramHeader (BinaryWriter writer, Layout layout, uint type, uint flags, ulong offset, ulong size, ulong alignment)
	{
		writer.Write (type);
		if (layout.Is64Bit) {
			writer.Write (flags);
		}
		WriteWord (writer, layout, offset); // p_offset
		WriteWord (writer, layout, offset); // p_vaddr
		WriteWord (writer, layout, offset); // p_paddr
		WriteWord (writer, layout, size); // p_filesz
		WriteWord (writer, layout, size); // p_memsz
		if (!layout.Is64Bit) {
			writer.Write (flags);
		}
		WriteWord (writer, layout, alignment);
	}

	static void WriteSymbol (BinaryWriter writer, Layout layout, uint name, byte info, ushort sectionIndex, ulong value, ulong size)
	{
		writer.Write (name);
		if (!layout.Is64Bit) {
			WriteWord (writer, layout, value);
			WriteWord (writer, layout, size);
		}
		writer.Write (info);
		writer.Write ((byte)0); // STV_DEFAULT
		writer.Write (sectionIndex);
		if (layout.Is64Bit) {
			WriteWord (writer, layout, value);
			WriteWord (writer, layout, size);
		}
	}

	static void WriteDynamicEntry (BinaryWriter writer, Layout layout, uint tag, ulong value)
	{
		WriteWord (writer, layout, tag);
		WriteWord (writer, layout, value);
	}

	static void WriteSectionHeader (BinaryWriter writer, Layout layout, uint name, uint type, uint flags,
		ulong offset, ulong size, uint link, uint info, ulong alignment, ulong entrySize)
	{
		writer.Write (name);
		writer.Write (type);
		WriteWord (writer, layout, flags);
		WriteWord (writer, layout, (flags & Allocated) != 0 ? offset : 0);
		WriteWord (writer, layout, offset);
		WriteWord (writer, layout, size);
		writer.Write (link);
		writer.Write (info);
		WriteWord (writer, layout, alignment);
		WriteWord (writer, layout, entrySize);
	}
}
