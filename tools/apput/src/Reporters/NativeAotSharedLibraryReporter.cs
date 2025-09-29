namespace ApplicationUtility;

[AspectReporter (typeof (NativeAotSharedLibrary))]
class NativeAotSharedLibraryReporter : SharedLibraryReporter
{
	protected override string AspectName => NativeAotSharedLibrary.AspectName;
	protected override string LibraryKind => "NativeAOT shared library";

	public NativeAotSharedLibraryReporter (NativeAotSharedLibrary library)
		: base (library)
	{}
}
