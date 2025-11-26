using ELFSharp.ELF;

namespace ApplicationUtility;

class NativeAotSharedLibraryAspectState : SharedLibraryAspectState
{
	public NativeAotSharedLibraryAspectState (bool success, IELF? elf)
		: base (success, elf)
	{}
}
