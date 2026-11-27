#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using ELFSharp.ELF;
using ELFSharp.ELF.Sections;
using ELFSharp.ELF.Segments;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tasks;

static partial class ELFHelper
{
	const string BinaryBlobLibraryName = "libbinary_blobs.so";
	const int DynamicNullTag = 0;
	const int DynamicStringTableTag = 5;
	const int DynamicStringTableSizeTag = 10;
	const int DynamicSonameTag = 14;

	public static bool StreamHasBinaryBlobSoname (TaskLoggingHelper log, Stream stream, string source)
	{
		try {
			using IELF elf = ELFReader.Load (stream, shouldOwnStream: false);
			return HasSoname (elf, BinaryBlobLibraryName);
		} catch (Exception ex) {
			log.LogDebugMessage ($"Attempt to read the SONAME from '{source}' failed with exception.");
			log.LogDebugMessage (ex.ToString ());
			return false;
		}
	}

	static bool HasSoname (IELF elf, string soname)
	{
		foreach (IDynamicSection dynamicSection in elf.GetSections<IDynamicSection> ()) {
			ulong? stringTableAddress = null;
			ulong? stringTableSize = null;
			var sonameOffsets = new List<ulong> ();
			foreach (IDynamicEntry entry in dynamicSection.Entries) {
				if ((int)entry.Tag == DynamicNullTag)
					break;
				var (hasValue, value) = GetDynamicValue (entry);
				if (!hasValue)
					continue;
				switch ((int)entry.Tag) {
					case DynamicStringTableTag:
						stringTableAddress = value;
						break;
					case DynamicStringTableSizeTag:
						stringTableSize = value;
						break;
					case DynamicSonameTag:
						sonameOffsets.Add (value);
						break;
				}
			}
			if (!stringTableAddress.HasValue || !stringTableSize.HasValue || sonameOffsets.Count == 0)
				continue;

			foreach (IStringTable strings in elf.GetSections<IStringTable> ()) {
				if (!TryGetSectionAddress (strings, out ulong sectionAddress, out ulong sectionSize) ||
						stringTableAddress.Value < sectionAddress)
					continue;
				ulong tableOffset = stringTableAddress.Value - sectionAddress;
				if (tableOffset > sectionSize || stringTableSize.Value > sectionSize - tableOffset)
					continue;
				foreach (ulong sonameOffset in sonameOffsets) {
					if (sonameOffset >= stringTableSize.Value || tableOffset > long.MaxValue ||
							sonameOffset > (ulong)long.MaxValue - tableOffset)
						continue;
					if (string.Equals (strings [(long)(tableOffset + sonameOffset)], soname, StringComparison.Ordinal))
						return true;
				}
			}
		}
		return false;
	}

	static (bool Success, ulong Value) GetDynamicValue (IDynamicEntry entry) => entry switch {
		DynamicEntry<ulong> { Value: var value } => (true, value),
		DynamicEntry<uint> { Value: var value } => (true, value),
		_ => (false, 0),
	};

	static bool TryGetSectionAddress (IStringTable section, out ulong address, out ulong size)
	{
		switch (section) {
			case Section<ulong> section64:
				address = section64.LoadAddress;
				size = section64.Size;
				return true;
			case Section<uint> section32:
				address = section32.LoadAddress;
				size = section32.Size;
				return true;
			default:
				address = 0;
				size = 0;
				return false;
		}
	}
}
