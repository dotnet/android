using System;
using System.Collections.Generic;
using System.IO;
using ELFSharp.ELF;
using ELFSharp.ELF.Sections;

namespace ApplicationUtility;

public class NativeAotSharedLibrary : SharedLibrary
{
	readonly static List<(string sectionName, SectionType type)> NativeAotSections = new () {
		("__managedcode", SectionType.ProgBits),
		(".dotnet_eh_table", SectionType.ProgBits),
		("__unbox", SectionType.ProgBits),
		("__modules", SectionType.ProgBits),
		(".hydrated", SectionType.NoBits),
	};

	protected NativeAotSharedLibrary (Stream stream, string libraryName, IAspectState state)
		: base (stream, libraryName, state)
	{}

	public new static IAspect LoadAspect (Stream stream, IAspectState? state, string? description)
	{
		if (String.IsNullOrEmpty (description)) {
			throw new ArgumentException ("Must be a shared library name", nameof (description));
		}

		var libState = EnsureValidAspectState<NativeAotSharedLibraryAspectState> (state);
		return new NativeAotSharedLibrary (stream, description, libState);
	}

	public new static IAspectState ProbeAspect (Stream stream, string? description) => IsNativeAotSharedLibrary (stream, description);

	static NativeAotSharedLibraryAspectState IsNativeAotSharedLibrary (Stream stream, string? description)
	{
		if (!IsSupportedELFSharedLibrary (stream, description, out IELF? elf) || elf == null) {
			return GetErrorState ();
		}

		// Just one match should be enough
		foreach (var naotSection in NativeAotSections) {
			if (HasSection (elf, description, naotSection.sectionName, naotSection.type)) {
				return new NativeAotSharedLibraryAspectState (true, elf);
			}
		}

		return GetErrorState ();

		NativeAotSharedLibraryAspectState GetErrorState () => new NativeAotSharedLibraryAspectState (false, null);
	}
}
