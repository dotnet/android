using System;
using System.Collections.Concurrent;
using System.IO;

using ELFSharp;
using ELFSharp.ELF;
using ELFSharp.ELF.Sections;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Utilities;

using ELFSymbolType = global::ELFSharp.ELF.Sections.SymbolType;
using ELFSectionType = global::ELFSharp.ELF.Sections.SectionType;

namespace Xamarin.Android.Tasks
{
	static class ELFHelper
	{
		public static bool IsEmptyAOTLibrary (TaskLoggingHelper log, string path)
		{
			if (String.IsNullOrEmpty (path) || !File.Exists (path)) {
				return false;
			}

			try {
				return IsEmptyAOTLibrary (log, path, ELFReader.Load (path));
			} catch (Exception ex) {
				log.LogWarning ($"Attempt to check whether '{path}' is a valid ELF file failed with exception, ignoring AOT check for the file.");
				log.LogWarningFromException (ex, showStackTrace: true);
				return false;
			}

		}

		static bool IsEmptyAOTLibrary (TaskLoggingHelper log, string path, IELF elf)
		{
			ISymbolTable? symtab = GetSymbolTable (elf, ".dynsym");
			if (symtab == null) {
				// We can't be sure what the DSO is, play safe
				return false;
			}

			bool mono_aot_file_info_found = false;
			foreach (var entry in symtab.Entries) {
				if (String.Compare ("mono_aot_file_info", entry.Name, StringComparison.Ordinal) == 0 && entry.Type == ELFSymbolType.Object) {
					mono_aot_file_info_found = true;
					break;
				}
			}

			if (!mono_aot_file_info_found) {
				// Not a MonoVM AOT assembly
				return false;
			}

			symtab = GetSymbolTable (elf, ".symtab");
			if (symtab == null) {
				// The DSO is stripped, we can't tell if there are any functions defined (.text will be present anyway)
				// We perhaps **can** take a look at the .text section size, but it's not a solid check...
				log.LogDebugMessage ($"{path} is an AOT assembly but without symbol table (stripped?). Including it in the archive.");
				return false;
			}

			bool isElf64 = elf.Class == Class.Bit64;
			foreach (var entry in symtab.Entries) {
				if (entry.Type == ELFSymbolType.Function) {
					return false;
				}

				if (!(isElf64 ? IsNonEmptyCodeSymbol (entry as SymbolEntry<ulong>) : IsNonEmptyCodeSymbol (entry as SymbolEntry<uint>))) {
					continue;
				}

				// We have an entry that's in (some) executable section and has some code in it.
				// Mono creates symbols which are essentially jump tables into executable code
				// inside the DSO that is not accessible via any other symbol, merely a blob of
				// executable code. The jump table symbols are named with the `_plt` prefix.
				if (entry.Name.EndsWith ("_plt")) {
					return false;
				}
			}
			return true;

			bool IsNonEmptyCodeSymbol<T> (SymbolEntry<T>? symbolEntry) where T : struct
			{
				if (symbolEntry == null) {
					return true; // Err on the side of caution
				}

				Type t = typeof(T);
				ulong size = 0;
				if (t == typeof(System.UInt64)) {
					size = (ulong)(object)symbolEntry.Size;
				} else if (t == typeof(System.UInt32)) {
					size = (uint)(object)symbolEntry.Size;
				}

				return size != 0 && symbolEntry.PointedSection.Type == ELFSectionType.ProgBits;
			}
		}

		static ISymbolTable? GetSymbolTable (IELF elf, string sectionName)
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

		static ISection? GetSection (IELF elf, string sectionName)
		{
			if (!elf.TryGetSection (sectionName, out ISection section)) {
				return null;
			}

			return section;
		}
	}
}
