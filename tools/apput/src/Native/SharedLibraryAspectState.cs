using ELFSharp.ELF;

namespace ApplicationUtility;

class SharedLibraryAspectState : BasicAspectState
{
	public IELF? ElfImage { get; }

	public SharedLibraryAspectState (bool success, IELF? elf)
		: base (success)
	{
		ElfImage = elf;
	}
}
