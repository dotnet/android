using System;
using System.IO;

namespace ApplicationUtility;

class AssemblySharedLibrary : DotNetAndroidWrapperSharedLibrary
{
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

		using Stream? storeStream = GetAssemblyStream (libraryState, stream, description);
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

		using Stream? assemblyStream = GetAssemblyStream (baseState, stream, description);
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

	static Stream? GetAssemblyStream (IAspectState state, Stream stream, string? description)
	{
		// If successful, then we know .NET for Android payload is there, we must load the library and check
		// for presence of the assembly store data.
		var library = DotNetAndroidWrapperSharedLibrary.LoadAspect (stream, state, description) as DotNetAndroidWrapperSharedLibrary;
		if (library == null) {
			throw new InvalidOperationException ($"Failed to load library '{description}'");
		}

		if (!library.HasAndroidPayload) {
			Log.Debug ($"Assembly: stream ('{description}') is an ELF shared library, without payload");
			return null;
		}

		return library.OpenAndroidPayload ();
	}

	ApplicationAssembly LoadAssembly (Stream stream, string libraryName)
	{
		// Wrapped individual assemblies follow a naming pattern, we need to "unmangle" the actual assembly name
		// Names are of the form <PREFIX><NAME>.dll.so where <PREFIX> is either `lib_` or `lib-<CULTURE>_`
		string assemblyName = Path.GetFileName (libraryName);

		// Name must be at least <PREFIX> length + .dll.so + at least one character for assembly name
		if (assemblyName.Length > 11) {
			if (assemblyName.StartsWith ("lib_", StringComparison.Ordinal)) {
				assemblyName = assemblyName.Substring (4);
			} else if (assemblyName.StartsWith ("lib-", StringComparison.Ordinal)) {
				int cultureEnd = assemblyName.IndexOf ('_');
				if (cultureEnd == -1 || cultureEnd == 4) {
					// No culture, odd
					assemblyName = assemblyName.Substring (4);
				} else {
					string cultureName = assemblyName.Substring (4, cultureEnd - 4);
					assemblyName = $"{cultureName}/{assemblyName.Substring (cultureEnd + 1)}";
				}
			}

			if (assemblyName.EndsWith (".dll.so", StringComparison.Ordinal)) {
				assemblyName = Path.GetFileNameWithoutExtension (assemblyName);
			}

			Log.Debug ($"Demangled assembly name: '{assemblyName}'");
		} else {
			assemblyName = libraryName;
		}

		ApplicationAssembly asm = (ApplicationAssembly)ApplicationAssembly.LoadAspect (stream, state.AssemblyState!, assemblyName);
		asm.Architecture = TargetArchitecture;

		return asm;
	}
}
