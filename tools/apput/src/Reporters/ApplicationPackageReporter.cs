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

	public ApplicationPackageReporter (ApplicationPackage package, MarkdownDocument doc)
		: base (doc)
	{
		this.package = package;
	}

	protected override void DoReport ()
	{
		WriteAspectDesc (package.PackageFormat);
		AddAspectDesc (package.PackageFormat);

		WriteSubsectionBanner ("Generic Android application information");
		MarkdownHeading genericInfoSection = AddSection ("Generic Android application information");

		WriteNativeArch (package.Architectures);
		MarkdownParagraph para = genericInfoSection.AddParagraph ();

		AddNativeArchDesc (para, package.Architectures);

		WriteYesNo ("Valid Android package", package.ValidAndroidPackage);
		AddYesNo (para, "Valid Android package", package.ValidAndroidPackage);

		WriteItem ("Package name", ValueOrNone (package.PackageName));
		AddItem (para, "Package name", ValueOrNone (package.PackageName));

		WriteItem ("Main activity", ValueOrNone (package.MainActivity));
		AddItem (para, "Main activity", ValueOrNone (package.MainActivity));

		WriteItem ("Minimum SDK version", ValueOrNone (package.MinSdkVersion));
		AddItem (para, "Minimum SDK version", ValueOrNone (package.MinSdkVersion));

		WriteItem ("Target SDK version", ValueOrNone (package.TargetSdkVersion));
		AddItem (para, "Target SDK version", ValueOrNone (package.TargetSdkVersion));

		WriteYesNo ("Signed", package.Signed);
		AddYesNo (para, "Signed", package.Signed);

		WriteYesNo ("Debuggable", package.Debuggable);
		AddYesNo (para, "Debuggable", package.Debuggable);

		MarkdownHeading permissionsSection = genericInfoSection.AddSubSection ("System permissions");
		para = permissionsSection.AddParagraph ();

		if (package.Permissions == null || package.Permissions.Count == 0) {
			AddText (para, "No permissions specified");
			WriteItem ("Permissions", "none");
		} else {
			// TODO: markdown list here
			WriteLine (LabelColor, "Permissions:");

			foreach (string permission in package.Permissions) {
				Write (LabelColor, "  * ");
				WriteLine (ValidValueColor, permission);
			}
		}

		WriteSubsectionBanner (".NET for Android application information");
		MarkdownHeading nfaInfoSection = genericInfoSection.AddSubSection (".NET for Android application information");
		para = nfaInfoSection.AddParagraph ();

		WriteItem ("Runtime", package.Runtime.ToString ());
		AddItem (para, "Runtime", package.Runtime.ToString ());

		MarkdownHeading storesSection = nfaInfoSection.AddSubSection ("Assembly stores");
		para = storesSection.AddParagraph ();

		if (package.AssemblyStores == null || package.AssemblyStores.Count == 0) {
			WriteItem ("Assembly stores", "none");
			AddText (para, "No assembly stores found");
		} else {
			// TODO: markdown list here
			WriteLine (LabelColor, "Assembly stores");

			foreach (AssemblyStore store in package.AssemblyStores) {
				var color = store.Architecture == AndroidTargetArch.None ? InvalidValueColor : ValidValueColor;

				Write (LabelColor, "  * ");
				WriteLine (color, $"{store.Architecture} ({store.NumberOfAssemblies} {GetCountable (Countable.Assembly, store.NumberOfAssemblies)})");
			}
		}

		MarkdownHeading dsoSection = nfaInfoSection.AddSubSection ("Shared libraries");
		para = dsoSection.AddParagraph ();

		if (package.SharedLibraries == null || package.SharedLibraries.Count == 0) {
			// Very unlikely...
			WriteItem ("Shared libraries", "none");
			AddText (para, "No shared libraries found in the package");
		} else {
			// TODO: markdown list (or perhaps better, table?) here
			WriteLine (LabelColor, "Shared libraries:");

			foreach (SharedLibrary lib in package.SharedLibraries) {
				Write (LabelColor, "  * ");
				WriteLine (ValidValueColor, $"{lib.Name}");
				WriteLine (LabelColor, $"    * Alignment: {lib.Alignment}");
				WriteLine (LabelColor, $"    * Debug info: {YesNo (lib.HasDebugInfo)}");
				WriteLine (LabelColor, $"    * Size: {lib.Size}");
			}
		}
	}
}
