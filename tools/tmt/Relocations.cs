using System;
using System.Collections.Generic;

using ELFSharp.ELF;

namespace tmt;

static class Relocations
{
	public static ulong GetValue (ELF<uint> elf, List<ELF32Relocation> rels, uint symbolOffset)
	{
		return elf.Machine switch {
			Machine.ARM      => GetValue_Arm32 (rels, symbolOffset),
			Machine.Intel386 => GetValue_x86 (rels, symbolOffset),
			_                => throw new NotSupportedException ($"ELF machine {elf.Machine} is not supported")
		};
	}

	public static ulong GetValue (ELF<ulong> elf, List<ELF64RelocationAddend> rels, ulong symbolOffset)
	{
		return elf.Machine switch {
			Machine.AArch64 => GetValue_AArch64 (rels, symbolOffset),
			Machine.AMD64   => GetValue_x64 (rels, symbolOffset),
			_               => throw new NotSupportedException ($"ELF machine {elf.Machine} is not supported")
		};
	}

	static ulong GetValue_AArch64 (List<ELF64RelocationAddend> rels, ulong symbolOffset)
	{
		// Documentation: https://github.com/ARM-software/abi-aa/releases/download/2023Q3/aaelf64.pdf page 36, Elf64 Code 1027
		const ulong R_AARCH64_RELATIVE = 0x403;

		foreach (ELF64RelocationAddend rel in rels) {
			if (rel.Offset != symbolOffset) {
				continue;
			}

			ulong relType = GetRelType (rel);
			if (relType != R_AARCH64_RELATIVE) {
				Log.Warning ($"Found relocation for symbol at offset 0x{symbolOffset:x}, but it has an usupported type 0x{relType:x}");
				return 0;
			}

			// In this case, this is offset into the file where pointed-to data begins
			return (ulong)rel.Addend;
		}

		Log.Debug ($"[AArch64] Relocation of supported type for symbol at offset 0x{symbolOffset:x} not found");
		return 0;
	}

	static ulong GetValue_x64 (List<ELF64RelocationAddend> rels, ulong symbolOffset)
	{
		// Documentation: https://docs.oracle.com/en/operating-systems/solaris/oracle-solaris/11.4/linkers-libraries/x64-relocation-types.html#GUID-369D19D8-60F0-4D3D-B761-4F02BA0BC023
		//                Value 8
		const ulong R_AMD64_RELATIVE = 0x08;

		foreach (ELF64RelocationAddend rel in rels) {
			if (rel.Offset != symbolOffset) {
				continue;
			}

			ulong relType = GetRelType (rel);
			if (relType != R_AMD64_RELATIVE) {
				Log.Warning ($"Found relocation for symbol at offset 0x{symbolOffset:x}, but it has an usupported type 0x{relType:x}");
				return 0;
			}

			// In this case, this is offset into the file where pointed-to data begins
			return (ulong)rel.Addend;
		}

		Log.Debug ($"[AArch64] Relocation of supported type for symbol at offset 0x{symbolOffset:x} not found");
		return 0;
	}

	static ulong GetValue_Arm32 (List<ELF32Relocation> rels, uint symbolOffset)
	{
		// Documentation: https://github.com/ARM-software/abi-aa/releases/download/2023Q3/aaelf32.pdf, page 40, Code 23
		const uint R_ARM_RELATIVE = 0x17;

		foreach (ELF32Relocation rel in rels) {
			if (rel.Offset != symbolOffset) {
				continue;
			}

			uint relType = GetRelType (rel);
			if (relType != R_ARM_RELATIVE) {
				Log.Warning ($"Found relocation for symbol at offset 0x{symbolOffset:x}, but it has an usupported type 0x{relType:x}");
				return 0;
			}

			// In this case, offset is the value we need to calculate the location
			return rel.Offset;
		}

		Log.Debug ($"[Arm32] Relocation of supported type for symbol at offset 0x{symbolOffset:x} not found");
		return 0;
	}

	static ulong GetValue_x86 (List<ELF32Relocation> rels, uint symbolOffset)
	{
		// Documentation: https://docs.oracle.com/en/operating-systems/solaris/oracle-solaris/11.4/linkers-libraries/32-bit-x86-relocation-types.html#GUID-2CE5C854-5AD8-4CC5-AABA-C03BED1C3FC0
		//                Value 8
		const uint R_386_RELATIVE = 0x08;

		foreach (ELF32Relocation rel in rels) {
			if (rel.Offset != symbolOffset) {
				continue;
			}

			uint relType = GetRelType (rel);
			if (relType != R_386_RELATIVE) {
				Log.Warning ($"Found relocation for symbol at offset 0x{symbolOffset:x}, but it has an usupported type 0x{relType:x}");
				return 0;
			}

			// In this case, offset is the value we need to calculate the location
			return rel.Offset;
		}

		Log.Debug ($"[x86] Relocation of supported type for symbol at offset 0x{symbolOffset:x} not found");
		return 0;
	}

	// the `r_info` (`.Info` for us) field of relocation recold encodes both the relocation type and symbol table index

	static ulong GetRelType (ELF64RelocationAddend rel) => rel.Info & 0xffffffff;
	static ulong GetRelSym (ELF64RelocationAddend rel) => rel.Info >> 32;

	static uint GetRelType (ELF32Relocation rel) => rel.Info & 0xff;
	static uint GetRelSym (ELF32Relocation rel)  => rel.Info >> 8;
}
