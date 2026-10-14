using System;
using System.IO;
using System.Text;

using Xamarin.Android.Tools;

namespace Microsoft.Android.Tasks;

// ET_REL data has section-relative symbols and no program headers, dynamic table or relocations.
internal static class NativeAotDataObjectWriter
{
	static readonly byte [] SectionNames = Encoding.ASCII.GetBytes ("\0.rodata\0.symtab\0.strtab\0.shstrtab\0.note.GNU-stack\0");

	public static void Write (byte [] payload, string symbol, Stream output, AndroidTargetArch arch)
	{
		ArgumentNullException.ThrowIfNull (payload);
		ArgumentNullException.ThrowIfNull (symbol);
		ArgumentNullException.ThrowIfNull (output);
		if (payload.Length == 0 || symbol.Length == 0 || symbol.IndexOf ('\0') >= 0 ||
			!output.CanWrite || !output.CanSeek) {
			throw new ArgumentException ("A nonempty payload and symbol, and a writable seekable output are required.");
		}
		var (is64Bit, machine, flags) = arch switch {
			AndroidTargetArch.Arm => (false, (ushort)40, 0x05000000u), // EABI5; floating-point calling-convention flags apply to ET_DYN, not data-only ET_REL.
			AndroidTargetArch.Arm64 => (true, (ushort)183, 0u),
			AndroidTargetArch.X86_64 => (true, (ushort)62, 0u),
			_ => throw new NotSupportedException ($"Unsupported NativeAOT data-object architecture: {arch}"),
		};
		uint wordSize = is64Bit ? 8u : 4u;
		ushort headerSize = (ushort)(is64Bit ? 64 : 52);
		ushort sectionSize = (ushort)(is64Bit ? 64 : 40);
		uint symbolSize = is64Bit ? 24u : 16u;
		var utf8 = new UTF8Encoding (false, true);
		byte [] strings = utf8.GetBytes ("\0" + symbol + "\0" + symbol + "_size\0");
		ulong dataOffset = Align (headerSize, 4);
		ulong sizeOffset = Align ((ulong)payload.Length, 4);
		ulong dataSize = checked (sizeOffset + 4);
		ulong symbolsOffset = Align (checked (dataOffset + dataSize), wordSize);
		ulong stringsOffset = checked (symbolsOffset + 3 * symbolSize);
		ulong sectionNamesOffset = checked (stringsOffset + (ulong)strings.Length);
		ulong sectionsOffset = Align (checked (sectionNamesOffset + (ulong)SectionNames.Length), wordSize);
		ulong fileSize = checked (sectionsOffset + 6u * sectionSize);
		if (!is64Bit && fileSize > uint.MaxValue) {
			throw new InvalidDataException ("NativeAOT data object exceeds the ELF32 size limit.");
		}

		output.SetLength (0);
		using var writer = new BinaryWriter (output, utf8, leaveOpen: true);
		output.Position = 0;
		writer.Write (new byte [] { 0x7f, (byte)'E', (byte)'L', (byte)'F', (byte)(is64Bit ? 2 : 1), 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0 });
		writer.Write ((ushort)1); // ET_REL
		writer.Write (machine);
		writer.Write (1u);
		Word (0); // e_entry
		Word (0); // e_phoff
		Word (sectionsOffset);
		writer.Write (flags);
		writer.Write (headerSize);
		writer.Write ((ushort)0); // e_phentsize
		writer.Write ((ushort)0); // e_phnum
		writer.Write (sectionSize);
		writer.Write ((ushort)6);
		writer.Write ((ushort)4); // e_shstrndx

		output.Position = checked ((long)dataOffset);
		writer.Write (payload);
		output.Position = checked ((long)(dataOffset + sizeOffset));
		writer.Write (checked ((uint)payload.Length));
		output.Position = checked ((long)symbolsOffset);
		Symbol (0, 0, 0, 0, 0);
		Symbol (1, 0x11, 1, 0, (ulong)payload.Length);
		Symbol (checked ((uint)utf8.GetByteCount (symbol) + 2), 0x11, 1, sizeOffset, 4);
		output.Position = checked ((long)stringsOffset);
		writer.Write (strings);
		output.Position = checked ((long)sectionNamesOffset);
		writer.Write (SectionNames);
		output.Position = checked ((long)sectionsOffset);
		Section (0, 0, 0, 0, 0, 0, 0, 0, 0);
		Section (1, 1, 2, dataOffset, dataSize, 0, 0, 4, 0); // SHT_PROGBITS, SHF_ALLOC
		Section (9, 2, 0, symbolsOffset, 3 * symbolSize, 3, 1, wordSize, symbolSize); // SHT_SYMTAB; first global = 1
		Section (17, 3, 0, stringsOffset, (ulong)strings.Length, 0, 0, 1, 0);
		Section (25, 3, 0, sectionNamesOffset, (ulong)SectionNames.Length, 0, 0, 1, 0);
		Section (35, 1, 0, sectionNamesOffset, 0, 0, 0, 1, 0); // non-executable .note.GNU-stack
		writer.Flush ();

		void Word (ulong value)
		{
			if (is64Bit) {
				writer.Write (value);
			} else {
				writer.Write (checked ((uint)value));
			}
		}

		void Symbol (uint name, byte info, ushort section, ulong value, ulong size)
		{
			writer.Write (name);
			if (!is64Bit) {
				Word (value);
				Word (size);
			}
			writer.Write (info);
			writer.Write ((byte)(name == 0 ? 0 : 2)); // STV_HIDDEN: static data is not an app export.
			writer.Write (section);
			if (is64Bit) {
				Word (value);
				Word (size);
			}
		}

		void Section (uint name, uint type, ulong sectionFlags, ulong offset, ulong size, uint link, uint info, ulong alignment, ulong entrySize)
		{
			writer.Write (name);
			writer.Write (type);
			Word (sectionFlags);
			Word (0); // sh_addr
			Word (offset);
			Word (size);
			writer.Write (link);
			writer.Write (info);
			Word (alignment);
			Word (entrySize);
		}
	}

	static ulong Align (ulong value, uint alignment) => checked (value + alignment - 1) & ~((ulong)alignment - 1);
}
