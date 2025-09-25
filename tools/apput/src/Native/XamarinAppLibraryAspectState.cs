namespace ApplicationUtility;

class XamarinAppLibraryAspectState : BasicAspectState
{
	public ulong FormatTag { get; }

	public XamarinAppLibraryAspectState (bool success, ulong formatTag)
		: base (success)
	{
		FormatTag = formatTag;
	}
}
