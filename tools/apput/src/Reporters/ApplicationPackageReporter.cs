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
		AddSection ("Generic Android application information");

		WriteNativeArch (package.Architectures);
		AddNativeArchDesc (package.Architectures);

		WriteYesNo ("Valid Android package", package.ValidAndroidPackage);
		AddYesNo ("Valid Android package", package.ValidAndroidPackage);

		WriteItem ("Package name", ValueOrNone (package.PackageName));
		AddLabeledItem ("Package name", ValueOrNone (package.PackageName));

		WriteItem ("Main activity", ValueOrNone (package.MainActivity));
		AddLabeledItem ("Main activity", ValueOrNone (package.MainActivity));

		WriteItem ("Minimum SDK version", ValueOrNone (package.MinSdkVersion));
		AddLabeledItem ("Minimum SDK version", ValueOrNone (package.MinSdkVersion));

		WriteItem ("Target SDK version", ValueOrNone (package.TargetSdkVersion));
		AddLabeledItem ("Target SDK version", ValueOrNone (package.TargetSdkVersion));

		WriteYesNo ("Signed", package.Signed);
		AddYesNo ("Signed", package.Signed);

		WriteYesNo ("Debuggable", package.Debuggable);
		AddYesNo ("Debuggable", package.Debuggable).AddNewline ();

		if (package.Permissions == null || package.Permissions.Count == 0) {
			AddText ("No permissions specified");
			WriteItem ("Permissions", "none");
		} else {
			AddText ($"Application requests the following {GetCountable (Countable.Permission, package.Permissions.Count)}:");
			WriteLine (LabelColor, "Permissions:");

			ReportDoc.BeginList ();
			foreach (string permission in package.Permissions) {
				ReportDoc.AddListItem ($"{permission}", MarkdownTextStyle.Monospace);

				Write (LabelColor, "  * ");
				WriteLine (ValidValueColor, permission);
			}
			ReportDoc.EndList ();
		}

		AddSection ("Shared libraries", 2);

		if (package.SharedLibraries == null || package.SharedLibraries.Count == 0) {
			// Very unlikely...
			WriteItem ("Shared libraries", "none");
			AddText ("No shared libraries found in the package");
		} else {
			AddText ($"Application contains the following {GetCountable (Countable.SharedLibrary, package.SharedLibraries.Count)}:");
			WriteLine (LabelColor, "Shared libraries:");

			ReportDoc.BeginList ();
			foreach (SharedLibrary lib in package.SharedLibraries) {
				ReportDoc.StartListItem ($"{lib.Name}", MarkdownTextStyle.Monospace);
				ReportDoc.BeginList ()
				         .AddListItem ($"Alignment: {lib.Alignment}")
				         .AddListItem ($"Debug info: {YesNo (lib.HasDebugInfo)}")
				         .AddListItem ($"Size: {lib.Size}", appendLine: false)
				         .EndList ()
				         .EndListItem ();

				Write (LabelColor, "  * ");
				WriteLine (ValidValueColor, $"{lib.Name}");
				WriteLine (LabelColor, $"    * Alignment: {lib.Alignment}");
				WriteLine (LabelColor, $"    * Debug info: {YesNo (lib.HasDebugInfo)}");
				WriteLine (LabelColor, $"    * Size: {lib.Size}");
			}
			ReportDoc.AddNewline ();
			ReportDoc.EndList ();
		}

		WriteSubsectionBanner (".NET for Android application information");
		AddSection (".NET for Android application information", 1);

		WriteItem ("Runtime", package.Runtime.ToString ());
		AddLabeledItem ("Runtime", package.Runtime.ToString ());

		AddSection ("Assembly stores", 2);

		if (package.AssemblyStores == null || package.AssemblyStores.Count == 0) {
			WriteItem ("Assembly stores", "none");
			AddText ("No assembly stores found");
		} else {
			AddText ($"Application contains the following {GetCountable (Countable.AssemblyStore, package.AssemblyStores.Count)}:");
			WriteLine (LabelColor, "Assembly stores");

			ReportDoc.BeginList ();
			foreach (AssemblyStore store in package.AssemblyStores) {
				ReportDoc.StartListItem ($"{store.Architecture}", MarkdownTextStyle.Monospace);
				AddListItemText ($" ({store.NumberOfAssemblies} {GetCountable (Countable.Assembly, store.NumberOfAssemblies)})");
				ReportDoc.EndListItem ();

				var color = store.Architecture == AndroidTargetArch.None ? InvalidValueColor : ValidValueColor;

				Write (LabelColor, "  * ");
				WriteLine (color, $"{store.Architecture} ({store.NumberOfAssemblies} {GetCountable (Countable.Assembly, store.NumberOfAssemblies)})");
			}
			ReportDoc.EndList ();
		}
	}
}
