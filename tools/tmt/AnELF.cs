using System;
using System.IO;
using System.Linq;
using System.Text;

using ELFSharp;
using ELFSharp.ELF;
using ELFSharp.ELF.Sections;

namespace tmt
{
	abstract class AnELF
	{
		protected static readonly byte[] EmptyArray = Array.Empty<byte> ();

		const string DynsymSectionName  = ".dynsym";
		const string SymtabSectionName  = ".symtab";
		const string RodataSectionName  = ".rodata";

		ISymbolTable dynamicSymbolsSection;
		ISection rodataSection;
		ISymbolTable? symbolsSection;
		string filePath;
		IELF elf;
		Stream elfStream;

		protected ISymbolTable DynSymSection => dynamicSymbolsSection;
		protected ISymbolTable? SymSection => symbolsSection;
		protected ISection RodataSection => rodataSection;
		protected IELF AnyELF => elf;
		protected Stream ELFStream => elfStream;

		public string FilePath => filePath;
		public MapArchitecture MapArchitecture => GetMapArchitecture ();

		public abstract bool Is64Bit { get; }
		public abstract string Bitness { get; }

		protected AnELF (Stream stream, string filePath, IELF elf, ISymbolTable dynsymSection, ISection rodataSection, ISymbolTable? symSection)
		{
			this.filePath = filePath;
			this.elf = elf;
			elfStream = stream;
			dynamicSymbolsSection = dynsymSection;
			this.rodataSection = rodataSection;
			symbolsSection = symSection;
		}

		protected ISymbolEntry? GetSymbol (string symbolName)
		{
			ISymbolEntry? symbol = null;

			if (symbolsSection != null)
				symbol = GetSymbol (symbolsSection, symbolName);

			if (symbol == null)
				symbol = GetSymbol (dynamicSymbolsSection, symbolName);

			return symbol;
		}

		protected static ISymbolEntry? GetSymbol (ISymbolTable symtab, string symbolName)
		{
			return symtab.Entries.Where (entry => String.Compare (entry.Name, symbolName, StringComparison.Ordinal) == 0).FirstOrDefault ();
		}

		protected static SymbolEntry<T>? GetSymbol<T> (SymbolTable<T> symtab, T symbolValue) where T: struct
		{
			return symtab.Entries.Where (entry => entry.Value.Equals (symbolValue)).FirstOrDefault ();
		}

		public bool HasSymbol (string symbolName)
		{
			return GetSymbol (symbolName) != null;
		}

		public (byte[] data, ISymbolEntry? symbol) GetData (string symbolName)
		{
			Log.Debug ($"Looking for symbol: {symbolName}");
			ISymbolEntry? symbol = GetSymbol (symbolName);
			if (symbol == null) {
				return (EmptyArray, null);
			}

			if (Is64Bit) {
				var symbol64 = symbol as SymbolEntry<ulong>;
				if (symbol64 == null)
					throw new InvalidOperationException ($"Symbol '{symbolName}' is not a valid 64-bit symbol");
				return (GetData (symbol64), symbol);
			}

			var symbol32 = symbol as SymbolEntry<uint>;
			if (symbol32 == null)
				throw new InvalidOperationException ($"Symbol '{symbolName}' is not a valid 32-bit symbol");

			return (GetData (symbol32), symbol);
		}

		public abstract byte[] GetData (ulong symbolValue, ulong size);

		public string GetASCIIZ (ulong symbolValue)
		{
			return GetASCIIZ (GetData (symbolValue, 0), 0);
		}

		public string GetASCIIZ (byte[] data, ulong offset)
		{
			if (offset >= (ulong)data.LongLength)
				throw new InvalidOperationException ("Not enough data to retrieve an ASCIIZ string");

			int count = data.Length;

			for (ulong i = offset; i < (ulong)data.LongLength; i++) {
				if (data[i] == 0) {
					count = (int)(i - offset);
					break;
				}
			}

			return Encoding.ASCII.GetString (data, (int)offset, count);
		}

		protected virtual byte[] GetData (SymbolEntry<ulong> symbol)
		{
			throw new NotSupportedException ();
		}

		protected virtual byte[] GetData (SymbolEntry<uint> symbol)
		{
			throw new NotSupportedException ();
		}

		protected byte[] GetData (ISymbolEntry symbol, ulong size, ulong offset)
		{
			return GetData (symbol.PointedSection, size, offset);
		}

		protected byte[] GetData (ISection section, ulong size, ulong offset)
		{
			Log.Debug ($"AnELF.GetData: section == {section.Name}; requested data size == {size}; offset in section == {offset:X}");
			byte[] data = section.GetContents ();
			var s = (Section<ulong>)section;

			Log.Debug ($"  section offset in file: {s.Offset}; section size: {s.Size}; alignment: {s.Alignment}");
			Log.Debug ($"  section data length: {data.Length} (long: {data.LongLength})");
			if ((ulong)data.LongLength < (offset + size)) {
				Log.Debug ($"  not enough data in section");
				return EmptyArray;
			}

			if (size == 0)
				size = (ulong)data.Length - offset;

			var ret = new byte[size];
			checked {
				Array.Copy (data, (int)offset, ret, 0, (int)size);
			}

			return ret;
		}

		/// <summary>
		/// Find a relocation corresponding to a pointer at offset <paramref name="pointerOffset"/> into
		/// the specified <paramref name="symbol"/>.  Returns an `ulong`, which needs to be cast to `uint`
		/// for 32-pointers (it can be done safely as the upper 32-bits will be 0 in such cases)
		/// </summary>
		public abstract ulong DeterminePointerAddress (ISymbolEntry symbol, ulong pointerOffset);

		public uint GetUInt32 (string symbolName)
		{
			(byte[] data, _) = GetData (symbolName);
			return GetUInt32 (data, 0, symbolName);
		}

		public uint GetUInt32 (ulong symbolValue)
		{
			return GetUInt32 (GetData (symbolValue, 4), 0, symbolValue.ToString ());
		}

		protected uint GetUInt32 (byte[] data, ulong offset, string symbolName)
		{
			if (data.Length < 4) {
				throw new InvalidOperationException ($"Data not big enough to retrieve a 32-bit integer from it (need 4, got {data.Length})");
			}

			return BitConverter.ToUInt32 (GetIntegerData (4, data, offset, symbolName), 0);
		}

		public ulong GetUInt64 (string symbolName)
		{
			(byte[] data, _) = GetData (symbolName);
			return GetUInt64 (data, 0, symbolName);
		}

		public ulong GetUInt64 (ulong symbolValue)
		{
			return GetUInt64 (GetData (symbolValue, 8), 0, symbolValue.ToString ());
		}

		protected ulong GetUInt64 (byte[] data, ulong offset, string symbolName)
		{
			if (data.Length < 8)
				throw new InvalidOperationException ("Data not big enough to retrieve a 64-bit integer from it");
			return BitConverter.ToUInt64 (GetIntegerData (8, data, offset, symbolName), 0);
		}

		byte[] GetIntegerData (uint size, byte[] data, ulong offset, string symbolName)
		{
			if ((ulong)data.LongLength < (offset + size)) {
				string bits = size == 4 ? "32" : "64";
				throw new InvalidOperationException ($"Unable to read UInt{bits} value for symbol '{symbolName}': data not long enough");
			}

			byte[] ret = new byte[size];
			Array.Copy (data, (int)offset, ret, 0, ret.Length);
			Endianess myEndianness = BitConverter.IsLittleEndian ? Endianess.LittleEndian : Endianess.BigEndian;
			if (AnyELF.Endianess != myEndianness) {
				Array.Reverse (ret);
			}

			return ret;
		}

		public static bool TryLoad (Stream stream, string filePath, out AnELF? anElf)
		{
			anElf = null;
			Class elfClass = ELFReader.CheckELFType (stream);
			if (elfClass == Class.NotELF) {
				Log.Warning ($"AnELF.TryLoad: {filePath} is not an ELF binary");
				return false;
			}

			IELF elf = ELFReader.Load (stream, shouldOwnStream: false);

			if (elf.Type != FileType.SharedObject) {
				Log.Warning ($"AnELF.TryLoad: {filePath} is not a shared library");
				return false;
			}

			if (elf.Endianess != Endianess.LittleEndian) {
				Log.Warning ($"AnELF.TryLoad: {filePath} is not a little-endian binary");
				return false;
			}

			bool is64;
			switch (elf.Machine) {
				case Machine.ARM:
				case Machine.Intel386:
					is64 = false;

					break;

				case Machine.AArch64:
				case Machine.AMD64:
					is64 = true;

					break;

				default:
					Log.Warning ($"{filePath} is for an unsupported machine type {elf.Machine}");
					return false;
			}

			ISymbolTable? symtab = GetSymbolTable (elf, DynsymSectionName);
			if (symtab == null) {
				Log.Warning ($"{filePath} does not contain dynamic symbol section '{DynsymSectionName}'");
				return false;
			}
			ISymbolTable dynsym = symtab;

			ISection? sec = GetSection (elf, RodataSectionName);
			if (sec == null) {
				Log.Warning ("${filePath} does not contain read-only data section ('{RodataSectionName}')");
				return false;
			}
			ISection rodata = sec;

			ISymbolTable? sym = GetSymbolTable (elf, SymtabSectionName);

			if (is64) {
				anElf = new ELF64 (stream, filePath, elf, dynsym, rodata, sym);
			} else {
				anElf = new ELF32 (stream, filePath, elf, dynsym, rodata, sym);
			}

			Log.Debug ($"AnELF.TryLoad: {filePath} is a {anElf.Bitness}-bit ELF binary ({elf.Machine})");
			return true;
		}

		protected static ISymbolTable? GetSymbolTable (IELF elf, string sectionName)
		{
			ISection? section = GetSection (elf, sectionName);
			if (section == null) {
				return null;
			}

			var symtab = section as ISymbolTable;
			if (symtab == null) {
				return null;
			}

			return symtab;
		}

		protected static ISection? GetSection (IELF elf, string sectionName)
		{
			if (!elf.TryGetSection (sectionName, out ISection section)) {
				return null;
			}

			return section;
		}

		MapArchitecture GetMapArchitecture ()
		{
			switch (AnyELF.Machine) {
				case Machine.ARM:
					return MapArchitecture.ARM;

				case Machine.Intel386:
					return MapArchitecture.X86;

				case Machine.AArch64:
					return MapArchitecture.ARM64;

				case Machine.AMD64:
					return MapArchitecture.X86_64;

				default:
					throw new InvalidOperationException ($"Unsupported ELF machine type {AnyELF.Machine}");
			}
		}
	}
}
