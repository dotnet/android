#nullable enable
using System;
using System.IO;
using System.Text;

using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks;

// A data-only ET_DYN image: loadable file offsets are also virtual addresses, with one
// read-only PT_LOAD covering the headers, dynamic metadata and assembly store.
static class AssemblyStoreElfWriter
{
	public const string PayloadSymbol = "_assembly_store";

	const ushort ProgramHeaderCount = 4;
	const ushort SectionCount = 7;
	const ushort PayloadSectionIndex = 5;
	const ushort SectionNamesIndex = 6;
	const uint SymbolCount = 2;
	const uint DynamicEntryCount = 7;
	const uint ReadOnly = 4; // PF_R
	const uint Allocated = 2; // SHF_ALLOC

	static readonly byte [] SectionNames = Encoding.ASCII.GetBytes ("\0.dynsym\0.dynstr\0.hash\0.dynamic\0payload\0.shstrtab\0");

	sealed class Layout
	{
		public readonly bool Is64Bit;
		public readonly ushort Machine;
		public readonly uint Flags;
		public readonly uint PageSize;
		public readonly uint WordSize;
		public readonly ushort HeaderSize;
		public readonly ushort ProgramHeaderSize;
		public readonly ushort SectionHeaderSize;
		public readonly uint SymbolSize;
		public readonly ulong DynamicSize;
		public readonly byte [] Strings;
		public readonly uint SonameIndex;
		public readonly ulong SymbolsOffset;
		public readonly ulong StringsOffset;
		public readonly ulong HashOffset;
		public readonly ulong DynamicOffset;
		public readonly ulong SectionNamesOffset;
		public readonly ulong SectionHeadersOffset;
		public readonly ulong PayloadOffset;
		public readonly ulong PayloadSize;
		public readonly ulong LoadSize;
		public readonly ulong FileSize;

		public Layout (AndroidTargetArch arch, ulong payloadSize, string libraryName)
		{
			(Is64Bit, Machine, Flags, PageSize) = arch switch {
				AndroidTargetArch.Arm => (false, (ushort)40, 0x05000200u, 4096u), // EABI5, soft-float
				AndroidTargetArch.Arm64 => (true, (ushort)183, 0u, 16384u),
				AndroidTargetArch.X86 => (false, (ushort)3, 0u, 4096u),
				AndroidTargetArch.X86_64 => (true, (ushort)62, 0u, 16384u),
				_ => throw new NotSupportedException ($"Unsupported assembly-store architecture: {arch}"),
			};
			if (payloadSize == 0) {
				throw new InvalidDataException ("The assembly-store payload must not be empty.");
			}
			if (payloadSize > uint.MaxValue) {
				throw new NotSupportedException ("Assembly-store payloads cannot exceed 4 GiB.");
			}

			WordSize = Is64Bit ? 8u : 4u;
			HeaderSize = (ushort)(Is64Bit ? 64 : 52);
			ProgramHeaderSize = (ushort)(Is64Bit ? 56 : 32);
			SectionHeaderSize = (ushort)(Is64Bit ? 64 : 40);
			SymbolSize = Is64Bit ? 24u : 16u;
			DynamicSize = DynamicEntryCount * WordSize * 2;
			Strings = new UTF8Encoding (false, true).GetBytes ("\0" + PayloadSymbol + "\0" + libraryName + "\0");
			SonameIndex = (uint)PayloadSymbol.Length + 2;

			SymbolsOffset = Align ((ulong)HeaderSize + (ulong)ProgramHeaderCount * ProgramHeaderSize, WordSize);
			StringsOffset = SymbolsOffset + SymbolCount * SymbolSize;
			HashOffset = Align (StringsOffset + (ulong)Strings.Length, sizeof (uint));
			DynamicOffset = Align (HashOffset + 5 * sizeof (uint), WordSize);
			PayloadOffset = Align (DynamicOffset + DynamicSize, PageSize);
			PayloadSize = payloadSize;
			LoadSize = checked (PayloadOffset + PayloadSize);
			// Keep non-allocated metadata outside PT_LOAD so strip/objcopy can rebuild it.
			SectionNamesOffset = LoadSize;
			SectionHeadersOffset = Align (SectionNamesOffset + (ulong)SectionNames.Length, WordSize);
			FileSize = checked (SectionHeadersOffset + (ulong)SectionCount * SectionHeaderSize);
			if (!Is64Bit && FileSize > uint.MaxValue) {
				throw new NotSupportedException ("The assembly store and ELF headers exceed the ELF32 size limit.");
			}
		}
	}

	public static void Write (Stream payload, Stream output, AndroidTargetArch arch, string libraryName)
	{
		if (payload == null) {
			throw new ArgumentNullException (nameof (payload));
		}
		if (output == null) {
			throw new ArgumentNullException (nameof (output));
		}
		if (libraryName == null) {
			throw new ArgumentNullException (nameof (libraryName));
		}
		if (libraryName.Length == 0 || libraryName.IndexOf ('\0') >= 0) {
			throw new ArgumentException ("The shared-library name must be nonempty and contain no NUL characters.", nameof (libraryName));
		}
		if (!payload.CanRead || !payload.CanSeek) {
			throw new ArgumentException ("The payload stream must be readable and seekable.", nameof (payload));
		}
		if (ReferenceEquals (payload, output) || !output.CanWrite || !output.CanSeek) {
			throw new ArgumentException ("The output must be a separate writable, seekable stream.", nameof (output));
		}

		var layout = new Layout (arch, checked ((ulong)(payload.Length - payload.Position)), libraryName);
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
		WriteSymbol (writer, layout, 1, 0x11, PayloadSectionIndex, layout.PayloadOffset, layout.PayloadSize); // STB_GLOBAL | STT_OBJECT
		output.Position = (long)layout.StringsOffset;
		writer.Write (layout.Strings);

		output.Position = (long)layout.HashOffset;
		// SysV hash: one bucket points to the only exported symbol; both chains terminate.
		writer.Write (1u);
		writer.Write (SymbolCount);
		writer.Write (1u);
		writer.Write (0u);
		writer.Write (0u);

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
		WriteSectionHeader (writer, layout, 1, 11, Allocated, layout.SymbolsOffset, SymbolCount * layout.SymbolSize, 2, 1, layout.WordSize, layout.SymbolSize);
		WriteSectionHeader (writer, layout, 9, 3, Allocated, layout.StringsOffset, (ulong)layout.Strings.Length, 0, 0, 1, 0);
		WriteSectionHeader (writer, layout, 17, 5, Allocated, layout.HashOffset, 5 * sizeof (uint), 1, 0, sizeof (uint), sizeof (uint));
		WriteSectionHeader (writer, layout, 23, 6, Allocated, layout.DynamicOffset, layout.DynamicSize, 2, 0, layout.WordSize, layout.WordSize * 2);
		WriteSectionHeader (writer, layout, 32, 1, Allocated, layout.PayloadOffset, layout.PayloadSize, 0, 0, layout.PageSize, 0);
		WriteSectionHeader (writer, layout, 40, 3, 0, layout.SectionNamesOffset, (ulong)SectionNames.Length, 0, 0, 1, 0);

		writer.Flush ();
		output.Position = (long)layout.PayloadOffset;
		payload.CopyTo (output);
		if ((ulong)output.Position != layout.LoadSize) {
			throw new IOException ("The assembly-store payload length changed while writing its ELF wrapper.");
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
