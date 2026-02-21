namespace ApplicationUtility;

class AssemblyStoreSharedLibraryAspectState : DotNetAndroidWrapperSharedLibraryAspectState
{
	public IAspectState? AssemblyStoreState { get; }

	public AssemblyStoreSharedLibraryAspectState (bool success, IAspectState? assemblyStoreAspectState, AnELF? elf)
		: base (success, elf)
	{
		AssemblyStoreState = assemblyStoreAspectState;
	}
}
