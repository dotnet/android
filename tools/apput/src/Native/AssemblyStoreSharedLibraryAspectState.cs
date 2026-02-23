namespace ApplicationUtility;

class AssemblyStoreSharedLibraryAspectState : DotNetAndroidWrapperSharedLibraryAspectState
{
	public IAspectState? AssemblyStoreState { get; }

	public AssemblyStoreSharedLibraryAspectState (bool success, IAspectState? assemblyStoreAspectState, AnELF? elf, ulong storeDataOffset)
		: base (success, elf, storeDataOffset)
	{
		AssemblyStoreState = assemblyStoreAspectState;
	}
}
