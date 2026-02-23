namespace ApplicationUtility;

class DotNetAndroidWrapperSharedLibraryAspectState : SharedLibraryAspectState
{
	public AnELF? LoadedELF { get; }
	public ulong PayloadOffset { get; }

	public DotNetAndroidWrapperSharedLibraryAspectState (bool success, AnELF? elf, ulong payloadOffset)
		: base (success, elf?.AnyELF)
	{
		LoadedELF = elf;
		PayloadOffset = payloadOffset;
	}
}
