namespace ApplicationUtility;

class AssemblySharedLibraryAspectState : DotNetAndroidWrapperSharedLibraryAspectState
{
	public IAspectState? AssemblyState { get; }

	public AssemblySharedLibraryAspectState (bool success, IAspectState? assemblyAspectState, AnELF? elf, ulong assemblyDataOffset)
		: base (success, elf, assemblyDataOffset)
	{
		AssemblyState = assemblyAspectState;
	}
}
