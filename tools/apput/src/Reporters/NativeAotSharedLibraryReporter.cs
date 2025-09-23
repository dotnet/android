namespace ApplicationUtility;

[AspectReporter (typeof (NativeAotSharedLibrary))]
class NativeAotSharedLibraryReporter : BaseReporter
{
	readonly NativeAotSharedLibrary library;

	public NativeAotSharedLibraryReporter (NativeAotSharedLibrary library)
	{
		this.library = library;
	}

	public override void Report ()
	{
		WriteAspectDesc ("NativeAOT shared library");
		WriteNativeArch (library.TargetArchitecture);
	}
}
