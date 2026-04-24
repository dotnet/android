namespace ApplicationUtility;

/// <summary>
/// Generates a report for a Xamarin/MAUI application shared library (<c>libxamarin-app.so</c>).
/// </summary>
[AspectReporter (typeof (XamarinAppSharedLibrary))]
class XamarinAppSharedLibraryReporter : SharedLibraryReporter
{
	readonly XamarinAppSharedLibrary library;

	protected override string AspectName => library.AspectName;
	protected override string LibraryKind => "Xamarin.Android App shared library";

	public XamarinAppSharedLibraryReporter (XamarinAppSharedLibrary library, MarkdownDocument doc)
		: base (library, doc)
	{
		this.library = library;
	}

	protected override void DoReport (ReportForm form, uint sectionLevel)
	{
		base.DoReport (form, sectionLevel);
		AddSection ("Xamarin.Android app library info", sectionLevel);
		AddLabeledItem ("Format tag", $"0x{library.FormatTag:x}");
	}
}
