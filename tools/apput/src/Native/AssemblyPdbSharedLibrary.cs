using System;
using System.IO;

namespace ApplicationUtility;

/// <summary>
/// Represents a shared library that wraps a Portable PDB file (e.g. <c>lib_Foo.pdb.so</c>).
/// </summary>
class AssemblyPdbSharedLibrary : DotNetAndroidWrapperSharedLibrary
{
	const string LogTag = "PDB";

	public AssemblyPdb PDB { get; }

	readonly AssemblyPdbSharedLibraryAspectState state;

	protected AssemblyPdbSharedLibrary (Stream stream, string libraryName, IAspectState state)
		: base (stream, libraryName, state)
	{
		this.state = (AssemblyPdbSharedLibraryAspectState)state;
		PDB = LoadPDB (stream, libraryName);
	}

	public new static IAspect LoadAspect (Stream stream, IAspectState? state, string? description)
	{
		if (String.IsNullOrEmpty (description)) {
			throw new ArgumentException ("Must be a shared library name", nameof (description));
		}

		var libraryState = EnsureValidAspectState<AssemblyPdbSharedLibraryAspectState> (state);
		if (libraryState == null) {
			throw new InvalidOperationException ("Internal error: unexpected aspect state. Was ProbeAspect unsuccessful?");
		}

		Stream? storeStream = GetPayloadStream (LogTag, libraryState, stream, description);
		if (storeStream == null) {
			throw new InvalidOperationException ("Internal error: failed to create assembly stream.");
		}

		return new AssemblyPdbSharedLibrary (storeStream, description, libraryState);
	}

	public new static IAspectState ProbeAspect (Stream stream, string? description)
	{
		var baseState = DotNetAndroidWrapperSharedLibrary.ProbeAspect (stream, description) as DotNetAndroidWrapperSharedLibraryAspectState;
		if (baseState == null) {
			throw new InvalidOperationException ("Unexpected base aspect state");
		}

		if (!baseState.Success) {
			return GetErrorState ();
		}

		using Stream? assemblyPdbStream = GetPayloadStream (LogTag, baseState, stream, description);
		if (assemblyPdbStream == null) {
			return GetErrorState ();
		}

		IAspectState assemblyPdbState = AssemblyPdb.ProbeAspect (assemblyPdbStream, description);
		if (!assemblyPdbState.Success) {
			return GetErrorState ();
		}

		return new AssemblyPdbSharedLibraryAspectState (
			success: true,
			assemblyPdbAspectState: assemblyPdbState,
			elf: baseState.LoadedELF,
			assemblyDataOffset: baseState.PayloadOffset
		);

		AssemblyPdbSharedLibraryAspectState GetErrorState ()
		{
			return new AssemblyPdbSharedLibraryAspectState (
				success: false,
				assemblyPdbAspectState: null,
				elf: null,
				assemblyDataOffset: 0
			);
		}
	}

	AssemblyPdb LoadPDB (Stream stream, string libraryName)
	{
		// Wrapped individual assemblies follow a naming pattern, we need to "unmangle" the actual assembly name
		// Names are of the form <PREFIX><NAME>.dll.so where <PREFIX> is either `lib_` or `lib-<CULTURE>_`
		string assemblyName = Path.GetFileName (libraryName);

		// Name must be at least <PREFIX> length + .dll.so + at least one character for assembly name
		if (assemblyName.Length > 11) {
			assemblyName = Utilities.DemangleSharedPdbLibraryName (assemblyName);
		} else {
			assemblyName = libraryName;
		}

		AssemblyPdb asm = (AssemblyPdb)AssemblyPdb.LoadAspect (stream, state.AssemblyPdbState!, assemblyName);
		asm.Architecture = TargetArchitecture;

		return asm;
	}
}
