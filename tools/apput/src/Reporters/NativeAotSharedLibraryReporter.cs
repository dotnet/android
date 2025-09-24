namespace ApplicationUtility;

[AspectReporter (typeof (NativeAotSharedLibrary))]
class NativeAotSharedLibraryReporter : SharedLibraryReporter
{
	readonly NativeAotSharedLibrary library;

	protected override string LibraryKind => "NativeAOT shared library";

	public NativeAotSharedLibraryReporter (NativeAotSharedLibrary library)
		: base (library)
	{
		this.library = library;
	}

	public override void Report ()
	{
		base.Report ();
	}
}
