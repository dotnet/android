namespace ApplicationUtility;

/// <summary>
/// Generates a report for a NativeAOT shared library.
/// </summary>
[AspectReporter (typeof (NativeAotSharedLibrary))]
class NativeAotSharedLibraryReporter : SharedLibraryReporter
{
	protected override string AspectName => Library.AspectName;
	protected override string LibraryKind => "NativeAOT shared library";

	public NativeAotSharedLibraryReporter (NativeAotSharedLibrary library, MarkdownDocument doc)
		: base (library, doc)
	{}
}
