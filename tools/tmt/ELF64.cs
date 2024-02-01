using System;
using System.Collections.Generic;
using System.IO;

using ELFSharp.ELF;
using ELFSharp.ELF.Sections;

namespace tmt
{
	class ELF64 : AnELF
	{
		public override bool Is64Bit => true;
		public override string Bitness => "64";

		SymbolTable<ulong> DynamicSymbols => (SymbolTable<ulong>)DynSymSection;
		SymbolTable<ulong>? Symbols => (SymbolTable<ulong>?)SymSection;
		Section<ulong> Rodata => (Section<ulong>)RodataSection;
		ELF<ulong> ELF => (ELF<ulong>)AnyELF;

		public ELF64 (Stream stream, string filePath, IELF elf, ISymbolTable dynsymSection, ISection rodataSection, ISymbolTable? symSection)
			: base (stream, filePath, elf, dynsymSection, rodataSection, symSection)
		{}

		public override ulong DeterminePointerAddress (ISymbolEntry symbol, ulong pointerOffset)
		{
			var sym64 = symbol as SymbolEntry<ulong>;
			if (sym64 == null) {
				throw new ArgumentException ("must be of type SymbolEntry<ulong>, was {symbol.GetType ()}", nameof (symbol));
			}

			if (sym64.PointedSection == null) {
				throw new ArgumentException ("does not belong to any section", nameof (symbol));
			}

			const string RelaDynSectionName = ".rela.dyn";
			ISection? sec = GetSection (ELF, RelaDynSectionName);
			if (sec == null) {
				Log.Warning ("${filePath} does not contain dynamic relocation section ('{RelaDynSectionName}')");
				return 0;
			}

			var relaDyn = sec as Section<ulong>;
			if (relaDyn == null) {
				Log.Warning ($"Invalid section type, expected 'Section<ulong>', got '{sec.GetType ()}'");
				return 0;
			}

			List<ELF64RelocationAddend> rels = LoadRelocationsAddend (relaDyn);
			if (rels.Count == 0) {
				Log.Warning ($"Relocation section '{RelaDynSectionName}' is empty");
				return 0;
			}

			ulong symRelocAddress = sym64.Value + pointerOffset;
			Log.Debug ($"Pointer relocation address == 0x{symRelocAddress:x}");

			ulong fileOffset = Relocations.GetValue (ELF, rels, symRelocAddress);
			Log.Debug ($"File offset == 0x{fileOffset:x}");

			return fileOffset;
		}

		public override byte[] GetData (ulong symbolValue, ulong size = 0)
		{
			Log.Debug ($"ELF64.GetData: Looking for symbol value {symbolValue:X08}");

			SymbolEntry<ulong>? symbol = GetSymbol (DynamicSymbols, symbolValue);
			if (symbol == null && Symbols != null) {
				symbol = GetSymbol (Symbols, symbolValue);
			}

			if (symbol != null && symbol.PointedSection != null) {
				Log.Debug ($"ELF64.GetData: found in section {symbol.PointedSection.Name}");
				return GetData (symbol);
			}

			Section<ulong> section = FindSectionForValue (symbolValue);

			Log.Debug ($"ELF64.GetData: found in section {section} {section.Name}");
			return GetData (section, size, OffsetInSection (section, symbolValue));
		}

		protected override byte[] GetData (SymbolEntry<ulong> symbol)
		{
			return GetData (symbol, symbol.Size, OffsetInSection (symbol.PointedSection, symbol.Value));
		}

		List<ELF64RelocationAddend> LoadRelocationsAddend (Section<ulong> section)
		{
			var ret = new List<ELF64RelocationAddend> ();
			byte[] data = section.GetContents ();
			ulong offset = 0;

			Log.Debug ($"Relocation section '{section.Name}' data length == {data.Length}");
			ulong counter = 0;
			while (offset < (ulong)data.Length) {
				ulong relOffset = Helpers.ReadUInt64 (data, ref offset, is64Bit: true);
				ulong relInfo = Helpers.ReadUInt64 (data, ref offset, is64Bit: true);
				long relAddend = Helpers.ReadInt64 (data, ref offset, is64Bit: true);

				Console.WriteLine ($"[{counter}] 0x{relOffset:x} 0x{relInfo:x} 0x{relAddend:x}");
				counter++;

				ret.Add (new ELF64RelocationAddend (relOffset, relInfo, relAddend));
			}

			return ret;
		}

		Section<ulong> FindSectionForValue (ulong symbolValue)
		{
			Log.Debug ($"FindSectionForValue ({symbolValue:X08})");
			int nsections = ELF.Sections.Count;

			for (int i = nsections - 1; i >= 0; i--) {
				Section<ulong> section = ELF.GetSection (i);
				if (section.Type != SectionType.ProgBits)
					continue;

				if (SectionInRange (section, symbolValue))
					return section;
			}

			throw new InvalidOperationException ($"Section matching symbol value {symbolValue:X08} cannot be found");
		}

		bool SectionInRange (Section<ulong> section, ulong symbolValue)
		{
			Log.Debug ($"SectionInRange ({section.Name}, {symbolValue:X08})");
			Log.Debug ($"  address == {section.LoadAddress:X08}; size == {section.Size}; last address = {section.LoadAddress + section.Size:X08}");
			Log.Debug ($"  symbolValue >= section.LoadAddress? {symbolValue >= section.LoadAddress}");
			Log.Debug ($"  (section.LoadAddress + section.Size) >= symbolValue? {(section.LoadAddress + section.Size) >= symbolValue}");
			return symbolValue >= section.LoadAddress && (section.LoadAddress + section.Size) >= symbolValue;
		}

		ulong OffsetInSection (Section<ulong> section, ulong symbolValue)
		{
			return symbolValue - section.LoadAddress;
		}
	}
}
