using Xamarin.Android.Tools;

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
		WriteYesNo ("Valid Android package", package.ValidAndroidPackage);
		WriteItem ("Package name", ValueOrNone (package.PackageName));
		WriteItem ("Main activity", ValueOrNone (package.MainActivity));
		WriteItem ("Minimum SDK version", ValueOrNone (package.MinSdkVersion));
		WriteItem ("Target SDK version", ValueOrNone (package.TargetSdkVersion));
		WriteYesNo ("Signed", package.Signed);
		WriteYesNo ("Debuggable", package.Debuggable);

		if (package.Permissions == null || package.Permissions.Count == 0) {
			WriteItem ("Permissions", "none");
		} else {
			WriteLine (LabelColor, "Permissions:");

			foreach (string permission in package.Permissions) {
				Write (LabelColor, "  * ");
				WriteLine (ValidValueColor, permission);
			}
		}

		WriteSubsectionBanner (".NET for Android application information");
		WriteItem ("Runtime", package.Runtime.ToString ());

		if (package.AssemblyStores == null || package.AssemblyStores.Count == 0) {
			WriteItem ("Assembly stores", "none");
		} else {
			WriteLine (LabelColor, "Assembly stores");

			foreach (AssemblyStore store in package.AssemblyStores) {
				var color = store.Architecture == AndroidTargetArch.None ? InvalidValueColor : ValidValueColor;

				Write (LabelColor, "  * ");
				WriteLine (color, $"{store.Architecture} ({store.NumberOfAssemblies} {GetCountable (Countable.Assembly, store.NumberOfAssemblies)})");
			}
		}

		if (package.SharedLibraries == null || package.SharedLibraries.Count == 0) {
			// Very unlikely...
			WriteItem ("Shared libraries", "none");
		} else {
			WriteLine (LabelColor, "Shared libraries:");

			foreach (SharedLibrary lib in package.SharedLibraries) {
				Write (LabelColor, "  * ");
				WriteLine (ValidValueColor, $"{lib.Name}");
			}
		}
	}
}
