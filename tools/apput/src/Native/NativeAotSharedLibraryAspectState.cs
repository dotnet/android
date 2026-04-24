using ELFSharp.ELF;

namespace ApplicationUtility;

/// <summary>
/// Aspect state for <see cref="NativeAotSharedLibrary"/>.
/// </summary>
class NativeAotSharedLibraryAspectState : SharedLibraryAspectState
{
	public NativeAotSharedLibraryAspectState (bool success, IELF? elf)
		: base (success, elf)
	{}
}
