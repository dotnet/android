using System.Collections.Generic;

using ELFSharp.ELF;

namespace tmt;

static class Relocations
{
	public static ulong GetValue (ELF<ulong> elf, List<ELF64RelocationAddend> rels, ulong symbolOffset)
	{
		return elf.Machine switch {
			Machine.AArch64 => GetValue_AArch64 (rels, symbolOffset),
			_               => 0
		};
	}

	static ulong GetValue_AArch64 (List<ELF64RelocationAddend> rels, ulong symbolOffset)
	{
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

		Log.Warning ($"[AArch64] Relocation of supported type for symbol at offset 0x{symbolOffset:x} not found");
		return 0;
	}

	static ulong GetRelType (ELF64RelocationAddend rel) => rel.Info & 0xffffffff;
	static ulong GetRelSym (ELF64RelocationAddend rel) => rel.Info >> 32;
}
