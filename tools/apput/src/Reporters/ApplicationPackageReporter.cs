namespace ApplicationUtility;

[AspectReporter (typeof (PackageAPK))]
[AspectReporter (typeof (PackageAAB))]
[AspectReporter (typeof (PackageBase))]
class ApplicationPackageReporter : BaseReporter
{
	readonly ApplicationPackage package;

	protected override string AspectName => ApplicationPackage.AspectName;
	protected override string ShortDescription => package.PackageFormat;

	public ApplicationPackageReporter (ApplicationPackage package)
	{
		this.package = package;
	}

	protected override void DoReport ()
	{
		WriteAspectDesc (package.PackageFormat);

		WriteSubsectionBanner ("Generic Android application information");
		WriteNativeArch (package.Architectures);
		WriteItem ("Package name", ValueOrNone (package.PackageName));
		WriteItem ("Main activity", ValueOrNone (package.MainActivity));
		WriteYesNo ("Valid Android package", package.ValidAndroidPackage);
		WriteYesNo ("Signed", package.Signed);
		WriteYesNo ("Debuggable", package.Debuggable);

		WriteSubsectionBanner (".NET for Android application information");
		WriteItem ("Runtime", package.Runtime.ToString ());
	}
}
