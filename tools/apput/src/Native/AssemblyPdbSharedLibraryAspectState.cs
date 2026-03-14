namespace ApplicationUtility;

class AssemblyPdbSharedLibraryAspectState : DotNetAndroidWrapperSharedLibraryAspectState
{
	public IAspectState? AssemblyPdbState { get; }

	public AssemblyPdbSharedLibraryAspectState (bool success, IAspectState? assemblyPdbAspectState, AnELF? elf, ulong assemblyDataOffset)
		: base (success, elf, assemblyDataOffset)
	{
		AssemblyPdbState = assemblyPdbAspectState;
	}
}
