namespace ApplicationUtility;

/// <summary>
/// Aspect state for <see cref="DotNetAndroidWrapperSharedLibrary"/>. Carries the loaded <see cref="AnELF"/>
/// and the offset to the .NET payload section.
/// </summary>
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
