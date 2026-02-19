using System;
using System.IO;

namespace ApplicationUtility;

class AssemblyStoreSharedLibrary : DotNetAndroidWrapperSharedLibrary
{
	AssemblyStore? store;
	readonly AssemblyStoreSharedLibraryAspectState state;

	public AssemblyStore AssemblyStore { get; }

	protected AssemblyStoreSharedLibrary (Stream stream, string libraryName, IAspectState state)
		: base (stream, libraryName, state)
	{
		this.state = (AssemblyStoreSharedLibraryAspectState)state;
		AssemblyStore = LoadStore (stream, libraryName);
	}

	public new static IAspect LoadAspect (Stream stream, IAspectState? state, string? description)
	{
		if (String.IsNullOrEmpty (description)) {
			throw new ArgumentException ("Must be a shared library name", nameof (description));
		}

		var libraryState = EnsureValidAspectState<AssemblyStoreSharedLibraryAspectState> (state);
		if (libraryState == null || libraryState.AssemblyStoreState == null) {
			throw new InvalidOperationException ("Internal error: unexpected aspect state. Was ProbeAspect unsuccessful?");
		}

		return new AssemblyStoreSharedLibrary (stream, description, libraryState);
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

		using Stream? storeStream = GetStoreStream (baseState, stream, description);
		if (storeStream == null) {
			return GetErrorState ();
		}

		IAspectState storeState = AssemblyStore.ProbeAspect (storeStream, description);
		if (!storeState.Success) {
			return GetErrorState ();
		}

		return new AssemblyStoreSharedLibraryAspectState (
			success: true,
			assemblyStoreAspectState: storeState,
			elf: baseState.LoadedELF
		);

		AssemblyStoreSharedLibraryAspectState GetErrorState ()
		{
			return new AssemblyStoreSharedLibraryAspectState (
				success: false,
				assemblyStoreAspectState: null,
				elf: null
			);
		}
	}

	static Stream? GetStoreStream (IAspectState state, Stream stream, string? description)
	{
		// If successful, then we know .NET for Android payload is there, we must load the library and check
		// for presence of the assembly store data.
		var library = DotNetAndroidWrapperSharedLibrary.LoadAspect (stream, state, description) as DotNetAndroidWrapperSharedLibrary;
		if (library == null) {
			throw new InvalidOperationException ($"Failed to load library '{description}'");
		}

		if (!library.HasAndroidPayload) {
			Log.Debug ($"AssemblyStore: stream ('{description}') is an ELF shared library, without payload");
			return null;
		}

		return library.OpenAndroidPayload ();
	}

	AssemblyStore LoadStore (Stream stream, string libraryName)
	{
		if (store != null) {
			return store;
		}

		using Stream? storeStream = GetStoreStream (state, stream, libraryName);
		if (storeStream == null) {
			throw new InvalidOperationException ($"Failed to load assembly store from '{libraryName}'");
		}

		return (AssemblyStore)AssemblyStore.LoadAspect (storeStream, state.AssemblyStoreState!, libraryName);
	}
}
