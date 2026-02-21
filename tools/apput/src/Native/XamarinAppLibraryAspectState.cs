namespace ApplicationUtility;

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
