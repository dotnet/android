namespace ApplicationUtility;

/// <summary>
/// Aspect state for <see cref="XamarinAppSharedLibrary"/>. Carries the format tag and loaded ELF instance.
/// </summary>
class XamarinAppLibraryAspectState : SharedLibraryAspectState
{
	public AnELF? LoadedELF { get; }
	public ulong FormatTag { get; }

	public XamarinAppLibraryAspectState (bool success, ulong formatTag, AnELF? elf)
		: base (success, elf?.AnyELF)
	{
		FormatTag = formatTag;
		LoadedELF = elf;
	}
}
