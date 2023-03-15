using System;
using System.IO;

using ELFSharp.ELF;
using ELFSharp.ELF.Sections;

namespace Xamarin.Android.Application.Utilities;

class ELF64 : AnELF
{
	public override bool Is64Bit => true;
	public override string Bitness => "64";

	SymbolTable<ulong> DynamicSymbols => (SymbolTable<ulong>)DynSymSection;
	SymbolTable<ulong>? Symbols => (SymbolTable<ulong>?)SymSection;
	Section<ulong> Rodata => (Section<ulong>)RodataSection;
	ELF<ulong> ELF => (ELF<ulong>)AnyELF;

	public ELF64 (ILogger log, Stream stream, string filePath, IELF elf, ISymbolTable dynsymSection, ISection rodataSection, ISymbolTable? symSection)
		: base (log, stream, filePath, elf, dynsymSection, rodataSection, symSection)
	{}

	public override string GetStringFromPointerField (ISymbolEntry symbolEntry, ulong pointerFieldOffset)
	{
		var symbol = symbolEntry as SymbolEntry<ulong>;
		if (symbol == null) {
			throw new InvalidOperationException ($"Expected a 64-bit symbol entry, got {symbolEntry}");
		}

		switch (ELF.Machine) {
			case Machine.ARM:
				return GetStringFromPointerField_ARM (symbol, pointerFieldOffset);

			case Machine.AArch64:
				return GetStringFromPointerField_ARM64 (symbol, pointerFieldOffset);

			case Machine.Intel386:
				return GetStringFromPointerField_X86 (symbol, pointerFieldOffset);

			case Machine.AMD64:
				return GetStringFromPointerField_X64 (symbol, pointerFieldOffset);

			default:
				throw new InvalidOperationException ($"Unsupported ELF machine type '{ELF.Machine}'");
		}
	}

	string GetStringFromPointerField_ARM64 (SymbolEntry<ulong> symbolEntry, ulong pointerFieldOffset)
	{
		// Steps:
		//
		//  1. Calculate address of the field in the symbol data: [symbol section offset] + [symbol offset into section] + pointerFieldOffset
		//  2. Find the .rela.dyn section
		//  3. Find relocation entry with offset matching the address calculated in 1. Relocation entry should have code 0x403 (1027) - R_AARCH64_RELATIVE
		//  4. Read relocation entry (see elf(5) for Elf32_Rela and Elf64_Rela structures) and get the addendum value
		//  5. Find section the addendum from 4. falls within
		//  6. Read that section data
		//  7. Subtract section address from the addendum, this will give offset into the section
		//  8. Get section data
		//  9. Read ASCIIZ data from the offset obtained in 7.
		//
		throw new NotImplementedException();
	}

	string GetStringFromPointerField_ARM (SymbolEntry<ulong> symbolEntry, ulong pointerFieldOffset)
	{
		throw new NotImplementedException();
	}

	string GetStringFromPointerField_X64 (SymbolEntry<ulong> symbolEntry, ulong pointerFieldOffset)
	{
		throw new NotImplementedException();
	}

	string GetStringFromPointerField_X86 (SymbolEntry<ulong> symbolEntry, ulong pointerFieldOffset)
	{
		throw new NotImplementedException();
	}

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
