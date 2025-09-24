using System;
using System.Collections.Generic;
using System.IO;
using ELFSharp.ELF;
using ELFSharp.ELF.Sections;

namespace ApplicationUtility;

class NativeAotSharedLibrary : SharedLibrary
{
	readonly static List<(string sectionName, SectionType type)> NativeAotSections = new () {
		("__managedcode", SectionType.ProgBits),
		(".dotnet_eh_table", SectionType.ProgBits),
		("__unbox", SectionType.ProgBits),
		("__modules", SectionType.ProgBits),
		(".hydrated", SectionType.NoBits),
	};

	protected NativeAotSharedLibrary (Stream stream, string libraryName)
		: base (stream, libraryName)
	{}

	public new static IAspect LoadAspect (Stream stream, IAspectState? state, string? description)
	{
		if (String.IsNullOrEmpty (description)) {
			throw new ArgumentException ("Must be a shared library name", nameof (description));
		}

		if (!IsNativeAotSharedLibrary (stream, description)) {
			throw new InvalidOperationException ("Stream is not a supported NativeAOT shared library");
		}

		return new NativeAotSharedLibrary (stream, description);
	}

	public new static IAspectState ProbeAspect (Stream stream, string? description) => new BasicAspectState (IsNativeAotSharedLibrary (stream, description));

	static bool IsNativeAotSharedLibrary (Stream stream, string description)
	{
		if (!IsSupportedELFSharedLibrary (stream, description, out IELF? elf) || elf == null) {
			return false;
		}

		// Just one match should be enough
		foreach (var naotSection in NativeAotSections) {
			if (HasSection (elf, description, naotSection.sectionName, naotSection.type)) {
				return true;
			}
		}

		return false;
	}
}
