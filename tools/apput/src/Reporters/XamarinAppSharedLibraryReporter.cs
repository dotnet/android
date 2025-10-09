namespace ApplicationUtility;

[AspectReporter (typeof (XamarinAppSharedLibrary))]
class XamarinAppSharedLibraryReporter : SharedLibraryReporter
{
	readonly XamarinAppSharedLibrary library;

	protected override string AspectName => XamarinAppSharedLibrary.AspectName;
	protected override string LibraryKind => "Xamarin.Android App shared library";

	public XamarinAppSharedLibraryReporter (XamarinAppSharedLibrary library, MarkdownDocument doc)
		: base (library, doc)
	{
		this.library = library;
	}

	protected override void DoReport ()
	{
		base.DoReport ();
		WriteSubsectionBanner ("Xamarin.Android app library info");
		WriteItem ("Format tag", $"0x{library.FormatTag:x}");
	}
}
