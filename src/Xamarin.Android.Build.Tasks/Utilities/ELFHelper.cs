using System;
using System.IO;

using ELFSharp;
using ELFSharp.ELF;
using ELFSharp.ELF.Sections;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Utilities;

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
				if (String.Compare ("mono_aot_file_info", entry.Name, StringComparison.Ordinal) == 0 && entry.Type == SymbolType.Object) {
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

			foreach (var entry in symtab.Entries) {
				if (entry.Type == SymbolType.Function) {
					return false;
				}
			}

			return true;
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
