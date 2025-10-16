using System;
using System.IO;
using ELFSharp.ELF;

namespace ApplicationUtility;

class MonoAotSharedLibrary : SharedLibrary
{
	const string MonoAotDataSymbol = "mono_aot_file_info";

	protected MonoAotSharedLibrary (Stream stream, string libraryName, MonoAotSharedLibraryAspectState state)
		: base (stream, libraryName, state)
	{}

	public new static IAspect LoadAspect (Stream stream, IAspectState? state, string? description)
	{
		if (String.IsNullOrEmpty (description)) {
			throw new ArgumentException ("Must be a shared library name", nameof (description));
		}

		var libState = EnsureValidAspectState<MonoAotSharedLibraryAspectState> (state);
		return new MonoAotSharedLibrary (stream, description, libState);
	}

	public static new IAspectState ProbeAspect (Stream stream, string? description) => IsMonoAotSharedLibrary (stream, description);

	static MonoAotSharedLibraryAspectState IsMonoAotSharedLibrary (Stream stream, string? description)
	{
		Log.Debug ($"Checking if '{description}' is a Mono AOT shared library");
		if (!IsSupportedELFSharedLibrary (stream, description, out IELF? elf) || elf == null) {
			return GetErrorState ();
		}

		if (!AnELF.TryLoad (stream, description ?? String.Empty, out AnELF? anElf) || anElf == null) {
			Log.Debug ($"Library '{description}' failed to load");
			return GetErrorState ();
		}

		if (!anElf.HasSymbol (MonoAotDataSymbol)) {
			Log.Debug ($"Symbol '{MonoAotDataSymbol}' missing, not a Mono AOT shared library");
			return GetErrorState ();
		}

		return new MonoAotSharedLibraryAspectState (true, anElf);

		MonoAotSharedLibraryAspectState GetErrorState () => new MonoAotSharedLibraryAspectState (false, null);
	}
}
