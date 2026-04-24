namespace ApplicationUtility;

/// <summary>
/// Aspect state for <see cref="AssemblySharedLibrary"/>. Carries the probed assembly aspect state.
/// </summary>
class AssemblySharedLibraryAspectState : DotNetAndroidWrapperSharedLibraryAspectState
{
	public IAspectState? AssemblyState { get; }

	public AssemblySharedLibraryAspectState (bool success, IAspectState? assemblyAspectState, AnELF? elf, ulong assemblyDataOffset)
		: base (success, elf, assemblyDataOffset)
	{
		AssemblyState = assemblyAspectState;
	}
}
