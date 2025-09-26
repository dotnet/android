namespace ApplicationUtility;

class MonoAotSharedLibraryAspectState : SharedLibraryAspectState
{
	public AnELF? LoadedELF { get; }

	public MonoAotSharedLibraryAspectState (bool success, AnELF? elf)
		: base (success, elf?.AnyELF)
	{
		LoadedELF = elf;
	}
}
