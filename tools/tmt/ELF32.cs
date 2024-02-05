using System;
using System.Collections.Generic;
using System.IO;

using ELFSharp.ELF;
using ELFSharp.ELF.Sections;

namespace tmt
{
	class ELF32 : AnELF
	{
		public override bool Is64Bit => false;
		public override string Bitness => "32";

		SymbolTable<uint> DynamicSymbols => (SymbolTable<uint>)DynSymSection;
		SymbolTable<uint>? Symbols => (SymbolTable<uint>?)SymSection;
		Section<uint> Rodata => (Section<uint>)RodataSection;
		ELF<uint> ELF => (ELF<uint>)AnyELF;

		public ELF32 (Stream stream, string filePath, IELF elf, ISymbolTable dynsymSection, ISection rodataSection, ISymbolTable? symSection)
			: base (stream, filePath, elf, dynsymSection, rodataSection, symSection)
		{}

		ulong DeterminePointerAddress (SymbolEntry<uint> symbol, uint pointerOffset)
		{
			if (symbol.PointedSection == null) {
				throw new ArgumentException ("does not belong to any section", nameof (symbol));
			}

			const string RelDynSectionName = ".rel.dyn";
			ISection? sec = GetSection (ELF, RelDynSectionName);
			if (sec == null) {
				Log.Warning ($"{FilePath} does not contain dynamic relocation section ('{RelDynSectionName}')");
				return 0;
			}

			var relDyn = sec as Section<uint>;
			if (relDyn == null) {
				Log.Warning ($"Invalid section type, expected 'Section<uint>', got '{sec.GetType ()}'");
				return 0;
			}

			List<ELF32Relocation> rels = LoadRelocations (relDyn);
			if (rels.Count == 0) {
				Log.Warning ($"Relocation section '{RelDynSectionName}' is empty");
				return 0;
			}

			uint symRelocAddress = symbol.Value + pointerOffset;
			Log.Debug ($"Pointer relocation address == 0x{symRelocAddress:x}");

			ulong fileOffset = Relocations.GetValue (ELF, rels, symRelocAddress);
			Log.Debug ($"File offset == 0x{fileOffset:x}");

			return fileOffset;
		}

		public override ulong DeterminePointerAddress (ISymbolEntry symbol, ulong pointerOffset)
		{
			var sym32 = symbol as SymbolEntry<uint>;
			if (sym32 == null) {
				throw new ArgumentException ("must be of type SymbolEntry<uint>, was {symbol.GetType ()}", nameof (symbol));
			}

			return DeterminePointerAddress (sym32, (uint)pointerOffset);
		}

		byte[] GetDataFromPointer (uint pointerValue, uint size)
		{
			Log.Debug ($"Looking for section containing pointer 0x{pointerValue:x}");
			uint dataOffset = 0;
			Section<uint>? section = null;

			foreach (Section<uint> s in ELF.Sections) {
				if (s.Type != SectionType.ProgBits) {
					continue;
				}

				if (s.LoadAddress > pointerValue || (s.LoadAddress + s.Size) < pointerValue) {
					continue;
				}

				Log.Debug ($"  Section '{s.Name}' matches");

				// Pointer is a load address, we convert it to the in-section offset by subtracting section load address from
				// the pointer
				dataOffset = pointerValue - s.LoadAddress;
				Log.Debug ($"  Pointer data section offset: 0x{dataOffset:x}");
				section = s;
				break;
			}

			if (section == null) {
				throw new InvalidOperationException ($"Data for pointer 0x{pointerValue:x} not located");
			}

			return GetData (section, size, dataOffset);
		}

		public override byte[] GetDataFromPointer (ulong pointerValue, ulong size)
		{
			return GetDataFromPointer ((uint)pointerValue, (uint)size);
		}

		public override byte[] GetData (ulong symbolValue, ulong size = 0)
		{
			checked {
				return GetData ((uint)symbolValue, size);
			}
		}

		byte[] GetData (uint symbolValue, ulong size)
		{
			Log.Debug ($"ELF64.GetData: Looking for symbol value {symbolValue:X08}");

			SymbolEntry<uint>? symbol = GetSymbol (DynamicSymbols, symbolValue);
			if (symbol == null && Symbols != null) {
				symbol = GetSymbol (Symbols, symbolValue);
			}

			if (symbol != null) {
				Log.Debug ($"ELF64.GetData: found in section {symbol.PointedSection.Name}");
				return GetData (symbol);
			}

			Section<uint> section = FindSectionForValue (symbolValue);

			Log.Debug ($"ELF64.GetData: found in section {section} {section.Name}");
			return GetData (section, size, OffsetInSection (section, symbolValue));
		}

		protected override byte[] GetData (SymbolEntry<uint> symbol)
		{
			ulong offset = symbol.Value - symbol.PointedSection.LoadAddress;
			return GetData (symbol, symbol.Size, offset);
		}

		List<ELF32Relocation> LoadRelocations (Section<uint> section)
		{
			var ret = new List<ELF32Relocation> ();

			byte[] data = section.GetContents ();
			ulong offset = 0;

			Log.Debug ($"Relocation section '{section.Name}' data length == {data.Length}");
			while (offset < (ulong)data.Length) {
				uint relOffset = Helpers.ReadUInt32 (data, ref offset, Is64Bit);
				uint relInfo = Helpers.ReadUInt32 (data, ref offset, Is64Bit);

				ret.Add (new ELF32Relocation (relOffset, relInfo));
			}

			return ret;
		}

		Section<uint> FindSectionForValue (uint symbolValue)
		{
			Log.Debug ($"FindSectionForValue ({symbolValue:X08})");
			int nsections = ELF.Sections.Count;

			for (int i = nsections - 1; i >= 0; i--) {
				Section<uint> section = ELF.GetSection (i);
				if (section.Type != SectionType.ProgBits)
					continue;

				if (SectionInRange (section, symbolValue))
					return section;
			}

			throw new InvalidOperationException ($"Section matching symbol value {symbolValue:X08} cannot be found");
		}

		bool SectionInRange (Section<uint> section, uint symbolValue)
		{
			Log.Debug ($"SectionInRange ({section.Name}, {symbolValue:X08})");
			Log.Debug ($"  address == {section.LoadAddress:X08}; size == {section.Size}; last address = {section.LoadAddress + section.Size:X08}");
			Log.Debug ($"  symbolValue >= section.LoadAddress? {symbolValue >= section.LoadAddress}");
			Log.Debug ($"  (section.LoadAddress + section.Size) >= symbolValue? {(section.LoadAddress + section.Size) >= symbolValue}");
			return symbolValue >= section.LoadAddress && (section.LoadAddress + section.Size) >= symbolValue;
		}

		ulong OffsetInSection (Section<uint> section, uint symbolValue)
		{
			return symbolValue - section.LoadAddress;
		}
	}
}
