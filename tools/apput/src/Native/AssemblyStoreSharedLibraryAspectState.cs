namespace ApplicationUtility;

/// <summary>
/// Aspect state for <see cref="AssemblyStoreSharedLibrary"/>. Carries the probed assembly store aspect state.
/// </summary>
class AssemblyStoreSharedLibraryAspectState : DotNetAndroidWrapperSharedLibraryAspectState
{
	public IAspectState? AssemblyStoreState { get; }

	public AssemblyStoreSharedLibraryAspectState (bool success, IAspectState? assemblyStoreAspectState, AnELF? elf, ulong storeDataOffset)
		: base (success, elf, storeDataOffset)
	{
		AssemblyStoreState = assemblyStoreAspectState;
	}
}
