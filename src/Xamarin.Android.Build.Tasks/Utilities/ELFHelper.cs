using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;

using ELFSharp;
using ELFSharp.ELF;
using ELFSharp.ELF.Sections;
using ELFSharp.ELF.Segments;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

using ELFSymbolType = global::ELFSharp.ELF.Sections.SymbolType;
using ELFSectionType = global::ELFSharp.ELF.Sections.SectionType;

namespace Xamarin.Android.Tasks
{
	static class ELFHelper
	{
		public static void AssertValidLibraryAlignment (TaskLoggingHelper log, int alignmentInPages, string path, ITaskItem? item)
		{
			if (String.IsNullOrEmpty (path) || !File.Exists (path)) {
				return;
			}

			log.LogDebugMessage ($"Checking alignment to {alignmentInPages}k page boundary in shared library {path}");
			try {
				AssertValidLibraryAlignment (log, MonoAndroidHelper.ZipAlignmentToPageSize (alignmentInPages), path, ELFReader.Load (path), item);
			} catch (Exception ex) {
				log.LogWarning ($"Attempt to check whether '{path}' is a correctly aligned ELF file failed with exception, ignoring alignment check for the file.");
				log.LogWarningFromException (ex, showStackTrace: true);
			}
		}

		static void AssertValidLibraryAlignment (TaskLoggingHelper log, uint pageSize, string path, IELF elf, ITaskItem? item)
		{
			if (elf.Class == Class.Bit32 || elf.Class == Class.NotELF) {
				log.LogDebugMessage ($"  Not a 64-bit ELF image.  Ignored.");
				return;
			}

			var elf64 = elf as ELF<ulong>;
			if (elf64 == null) {
				throw new InvalidOperationException ($"Internal error: {elf} is not ELF<ulong>");
			}

			// We need to find all segments of Load type and make sure their alignment is as expected.
			foreach (ISegment segment in elf64.Segments) {
				if (segment.Type != SegmentType.Load) {
					continue;
				}

				var segment64 = segment as Segment<ulong>;
				if (segment64 == null) {
					throw new InvalidOperationException ($"Internal error: {segment} is not Segment<ulong>");
				}

				// TODO: what happens if the library is aligned at, say, 64k while 16k is required? Should we erorr out?
				//       We will need more info about that, have to wait till Google formally announce the requirement.
				//       At this moment the script https://developer.android.com/guide/practices/page-sizes#test they
				//       provide suggests it's a strict requirement, so we test for equality below.
				if (segment64.Alignment == pageSize) {
					continue;
				}
				log.LogDebugMessage ($"    expected segment alignment of 0x{pageSize:x}, found 0x{segment64.Alignment:x}");

				(string packageId, string packageVersion, string originalFile) = GetNugetPackageInfo ();
				log.LogCodedWarning ("XA0141", Properties.Resources.XA0141, packageId, packageVersion, originalFile, Path.GetFileName (path));
				break;
			}

			(string packageId, string packageVersion, string originalFile) GetNugetPackageInfo ()
			{
				const string Unknown = "<unknown>";

				if (item == null) {
					return (Unknown, Unknown, Unknown);
				}

				string? metaValue = item.GetMetadata ("PathInPackage");
				if (String.IsNullOrEmpty (metaValue)) {
					metaValue = item.GetMetadata ("OriginalFile");
					if (String.IsNullOrEmpty (metaValue)) {
						metaValue = item.ItemSpec;
					}
				}
				string originalFile = metaValue;
				metaValue = item.GetMetadata ("NuGetPackageId");
				if (String.IsNullOrEmpty (metaValue)) {
					return (Unknown, Unknown, originalFile);
				}

				string id = metaValue;
				string version;
				metaValue = item.GetMetadata ("NuGetPackageVersion");
				if (!String.IsNullOrEmpty (metaValue)) {
					version = metaValue;
				} else {
					version = Unknown;
				}

				return (id, version, originalFile);
			}
		}

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

		public static bool ReferencesLibrary (string libraryPath, string referencedLibraryName)
		{
			if (String.IsNullOrEmpty (libraryPath) || !File.Exists (libraryPath)) {
				return false;
			}

			IELF elf = ELFReader.Load (libraryPath);
			var dynstr = GetSection (elf, ".dynstr") as IStringTable;
			if (dynstr == null) {
				return false;
			}

			foreach (IDynamicSection section in elf.GetSections<IDynamicSection> ()) {
				foreach (IDynamicEntry entry in section.Entries) {
					if (IsLibraryReference (dynstr, entry, referencedLibraryName)) {
						return true;
					}
				}
			}

			return false;
		}

		static bool IsLibraryReference (IStringTable stringTable, IDynamicEntry dynEntry, string referencedLibraryName)
		{
			if (dynEntry.Tag != DynamicTag.Needed) {
				return false;
			}

			ulong index;
			if (dynEntry is DynamicEntry<ulong> entry64) {
				index = entry64.Value;
			} else if (dynEntry is DynamicEntry<uint> entry32) {
				index = (ulong)entry32.Value;
			} else {
				return false;
			}

			return String.Compare (referencedLibraryName, stringTable[(long)index], StringComparison.Ordinal) == 0;
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
