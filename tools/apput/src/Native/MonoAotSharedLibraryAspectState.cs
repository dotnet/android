namespace ApplicationUtility;

/// <summary>
/// Aspect state for <see cref="MonoAotSharedLibrary"/>.
/// </summary>
class MonoAotSharedLibraryAspectState : SharedLibraryAspectState
{
	public AnELF? LoadedELF { get; }

	public MonoAotSharedLibraryAspectState (bool success, AnELF? elf)
		: base (success, elf?.AnyELF)
	{
		LoadedELF = elf;
	}
}
