#nullable enable
using System;
using System.IO;

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
	static partial class ELFHelper
	{
		public static ELFInfo? GetInfo (TaskLoggingHelper log, string path)
		{
			try {
				using IELF elf = ELFReader.Load (path);
				return new ELFInfo (elf);
			} catch (Exception ex) {
				log.LogDebugMessage ($"Attempt to check whether '{path}' has debug symbols failed with exception.");
				log.LogDebugMessage (ex.ToString ());
				return null;
			}
		}

		public static bool AssertValidLibraryAlignment (TaskLoggingHelper log, int alignmentInPages, string path, ITaskItem? item, string? reservedSoname = null)
		{
			if (path.IsNullOrEmpty () || !File.Exists (path)) {
				return false;
			}

			bool hasReservedSoname = false;
			log.LogDebugMessage ($"Checking alignment to {alignmentInPages}k page boundary in shared library {path}");
			try {
				using IELF elf = ELFReader.Load (path);
				if (!reservedSoname.IsNullOrEmpty ())
					hasReservedSoname = HasSoname (elf, reservedSoname);
				AssertValidLibraryAlignment (log, MonoAndroidHelper.ZipAlignmentToPageSize (alignmentInPages), path, elf, item);
			} catch (Exception ex) {
				log.LogCodedWarning ("XA0146", Properties.Resources.XA0146, path);
				log.LogWarningFromException (ex, showStackTrace: true);
			}
			return hasReservedSoname;
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

				// Android allows alignment higher than the page size, as long as it's a power of two. See:
				//
				// https://android.googlesource.com/platform//system/extras/+/3c784e9fd9f4e3f8e363a023939567c41c19d634%5E%21/#F0
				//
				if (segment64.Alignment >= pageSize && (segment64.Alignment % 2 == 0)) {
					continue;
				}
				log.LogDebugMessage ($"    expected segment alignment of at least 0x{pageSize:x} and a power of two, found 0x{segment64.Alignment:x}");

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
				if (metaValue.IsNullOrEmpty ()) {
					metaValue = item.GetMetadata ("OriginalFile");
					if (metaValue.IsNullOrEmpty ()) {
						metaValue = item.ItemSpec;
					}
				}
				string originalFile = metaValue;
				metaValue = item.GetMetadata ("NuGetPackageId");
				if (metaValue.IsNullOrEmpty ()) {
					return (Unknown, Unknown, originalFile);
				}

				string id = metaValue;
				string version;
				metaValue = item.GetMetadata ("NuGetPackageVersion");
				if (!metaValue.IsNullOrEmpty ()) {
					version = metaValue;
				} else {
					version = Unknown;
				}

				return (id, version, originalFile);
			}
		}
	}
}
