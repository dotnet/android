using System;
using System.IO;

namespace ApplicationUtility;

/// <summary>
/// Represents a shared library that wraps a single .NET assembly (e.g. <c>lib_Foo.dll.so</c>).
/// </summary>
class AssemblySharedLibrary : DotNetAndroidWrapperSharedLibrary
{
	const string LogTag = "Assembly";

	readonly AssemblySharedLibraryAspectState state;

	public ApplicationAssembly Assembly { get; }

	public override string AspectName => $"{base.AspectName} (Application Assembly)";

	protected AssemblySharedLibrary (Stream stream, string libraryName, IAspectState state)
		: base (stream, libraryName, state)
	{
		this.state = (AssemblySharedLibraryAspectState)state;
		Assembly = LoadAssembly (stream, libraryName);
	}

	public new static IAspect LoadAspect (Stream stream, IAspectState? state, string? description)
	{
		if (String.IsNullOrEmpty (description)) {
			throw new ArgumentException ("Must be a shared library name", nameof (description));
		}

		var libraryState = EnsureValidAspectState<AssemblySharedLibraryAspectState> (state);
		if (libraryState == null) {
			throw new InvalidOperationException ("Internal error: unexpected aspect state. Was ProbeAspect unsuccessful?");
		}

		Stream? storeStream = GetPayloadStream (LogTag, libraryState, stream, description);
		if (storeStream == null) {
			throw new InvalidOperationException ("Internal error: failed to create assembly stream.");
		}

		return new AssemblySharedLibrary (storeStream, description, libraryState);
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

		using Stream? assemblyStream = GetPayloadStream (LogTag, baseState, stream, description);
		if (assemblyStream == null) {
			return GetErrorState ();
		}

		IAspectState assemblyState = ApplicationAssembly.ProbeAspect (assemblyStream, description);
		if (!assemblyState.Success) {
			return GetErrorState ();
		}

		return new AssemblySharedLibraryAspectState (
			success: true,
			assemblyAspectState: assemblyState,
			elf: baseState.LoadedELF,
			assemblyDataOffset: baseState.PayloadOffset
		);

		AssemblySharedLibraryAspectState GetErrorState ()
		{
			return new AssemblySharedLibraryAspectState (
				success: false,
				assemblyAspectState: null,
				elf: null,
				assemblyDataOffset: 0
			);
		}
	}

	ApplicationAssembly LoadAssembly (Stream stream, string libraryName)
	{
		// Wrapped individual assemblies follow a naming pattern, we need to "unmangle" the actual assembly name
		// Names are of the form <PREFIX><NAME>.dll.so where <PREFIX> is either `lib_` or `lib-<CULTURE>_`
		string assemblyName = Path.GetFileName (libraryName);

		// Name must be at least <PREFIX> length + .dll.so + at least one character for assembly name
		if (assemblyName.Length > 11) {
			assemblyName = Utilities.DemangleSharedAssemblyLibraryName (assemblyName);
		} else {
			assemblyName = libraryName;
		}

		ApplicationAssembly asm = (ApplicationAssembly)ApplicationAssembly.LoadAspect (stream, state.AssemblyState!, assemblyName);
		asm.Architecture = TargetArchitecture;

		return asm;
	}
}
