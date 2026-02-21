using ELFSharp.ELF;

namespace ApplicationUtility;

class SharedLibraryAspectState : BasicAspectState
{
	public IELF? ElfImage { get; }
	public bool Is64Bit { get; }

	public SharedLibraryAspectState (bool success, IELF? elf)
		: base (success)
	{
		ElfImage = elf;
		Is64Bit = elf != null ? elf.Class == Class.Bit64 : false;
	}
}
