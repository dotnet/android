using System;

using ELFSharp.ELF;
using ELFSharp.ELF.Sections;

namespace Xamarin.Android.Tasks;

static partial class ELFHelper
{
	public static ISymbolTable? GetSymbolTable (IELF elf, string sectionName)
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

	public static ISection? GetSection (IELF elf, string sectionName)
	{
		if (!elf.TryGetSection (sectionName, out ISection section)) {
			return null;
		}

		return section;
	}

	public static SymbolEntry<T>? FindSymbol<T> (ISymbolTable? symbolTable, string symbolName) where T: struct
	{
		if (symbolTable == null) {
			return null;
		}

		ISymbolEntry? symbol = null;
		foreach (ISymbolEntry entry in symbolTable.Entries) {
			if (String.Compare (entry.Name, symbolName, StringComparison.Ordinal) != 0) {
				continue;
			}

			symbol = entry;
			break;
		}

		if (symbol == null) {
			return null;
		}

		Type t = typeof(T);
		if (t == typeof(ulong) || t == typeof(uint)) {
			return (SymbolEntry<T>)symbol;
		}

		throw new InvalidOperationException ($"Only `ulong` and `uint` types are accepted");
	}
}
