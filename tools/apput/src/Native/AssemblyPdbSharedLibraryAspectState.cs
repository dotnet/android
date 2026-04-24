namespace ApplicationUtility;

/// <summary>
/// Aspect state for <see cref="AssemblyPdbSharedLibrary"/>. Carries the probed PDB aspect state
/// alongside the ELF and payload offset information.
/// </summary>
class AssemblyPdbSharedLibraryAspectState : DotNetAndroidWrapperSharedLibraryAspectState
{
	public IAspectState? AssemblyPdbState { get; }

	public AssemblyPdbSharedLibraryAspectState (bool success, IAspectState? assemblyPdbAspectState, AnELF? elf, ulong assemblyDataOffset)
		: base (success, elf, assemblyDataOffset)
	{
		AssemblyPdbState = assemblyPdbAspectState;
	}
}
