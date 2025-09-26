using System;
using System.IO;

using ELFSharp.ELF;

namespace ApplicationUtility;

// TODO: make it an abstract class, we need to support different formats
class XamarinAppSharedLibrary : SharedLibrary
{
	const string FormatTagSymbol = "format_tag";

	public ulong FormatTag { get; }

	XamarinAppSharedLibrary (Stream stream, string description, XamarinAppLibraryAspectState state)
		: base (stream, description, state)
	{
		FormatTag = state.FormatTag;
	}

	public static new IAspect LoadAspect (Stream stream, IAspectState state, string? description)
	{
		if (String.IsNullOrEmpty (description)) {
			throw new ArgumentException ("Must be a shared library name", nameof (description));
		}

		var libState = EnsureValidAspectState<XamarinAppLibraryAspectState> (state);

		// TODO: this needs to be versioned
		return new XamarinAppSharedLibrary (stream, description, libState);
	}

	public static new IAspectState ProbeAspect (Stream stream, string? description) => IsXamarinAppSharedLibrary (stream, description);

	static XamarinAppLibraryAspectState IsXamarinAppSharedLibrary (Stream stream, string? description)
	{
		if (!IsSupportedELFSharedLibrary (stream, description, out IELF? elf) || elf == null) {
			return GetErrorState ();
		}

		if (!AnELF.TryLoad (stream, description ?? String.Empty, out AnELF? anElf) || anElf == null) {
			Log.Debug ($"Failed to load '{description}' as a Xamarin.Android application shared library");
			return GetErrorState ();
		}

		if (!anElf.HasSymbol (FormatTagSymbol)) {
			return LogMissingSymbolAndReturn (FormatTagSymbol);
		}
		ulong formatTag = anElf.GetUInt64 (FormatTagSymbol);

		// TODO: check for presence of a handful of fields more
		return GetState (success: true, formatTag, anElf);

		XamarinAppLibraryAspectState LogMissingSymbolAndReturn (string name)
		{
			Log.Debug ($"{description} is not a Xamarin.Android application shared library, it doesn't have the '{name}' symbol.");
			return GetErrorState ();
		}

		XamarinAppLibraryAspectState GetState (bool success, ulong formatTag, AnELF? elf) => new XamarinAppLibraryAspectState (success, formatTag, elf);
		XamarinAppLibraryAspectState GetErrorState () => GetState (success: false, formatTag: 0, elf: null);
	}
}
