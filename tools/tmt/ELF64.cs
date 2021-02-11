using System;
using System.IO;

using ELFSharp;
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

		public override byte[] GetData (ulong symbolValue, ulong size = 0)
		{
			Log.Debug ($"ELF64.GetData: Looking for symbol value {symbolValue:X08}");

			SymbolEntry<ulong>? symbol = GetSymbol (DynamicSymbols, symbolValue);
			if (symbol == null && Symbols != null) {
				symbol = GetSymbol (Symbols, symbolValue);
			}

			if (symbol != null) {
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
