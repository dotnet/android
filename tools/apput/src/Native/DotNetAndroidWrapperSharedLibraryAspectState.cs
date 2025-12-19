namespace ApplicationUtility;

class DotNetAndroidWrapperSharedLibraryAspectState : SharedLibraryAspectState
{
	public AnELF? LoadedELF { get; }

	public DotNetAndroidWrapperSharedLibraryAspectState (bool success, AnELF? elf)
		: base (success, elf?.AnyELF)
	{
		LoadedELF = elf;
	}
}
