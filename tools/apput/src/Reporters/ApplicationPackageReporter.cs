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
		AddAspectDesc (package.PackageFormat);

		AddSection ("Generic Android application information");
		AddNativeArchDesc (package.Architectures);
		AddYesNo ("Valid Android package", package.ValidAndroidPackage);
		AddLabeledItem ("Package name", ValueOrNone (package.PackageName));
		AddLabeledItem ("Main activity", ValueOrNone (package.MainActivity));
		AddLabeledItem ("Minimum SDK version", ValueOrNone (package.MinSdkVersion));
		AddLabeledItem ("Target SDK version", ValueOrNone (package.TargetSdkVersion));
		AddYesNo ("Signed", package.Signed);
		AddYesNo ("Debuggable", package.Debuggable).AddNewline ();

		if (package.Permissions == null || package.Permissions.Count == 0) {
			AddText ("No permissions specified");
		} else {
			AddText ($"Application requests the following {GetCountable (Countable.Permission, package.Permissions.Count)}:");

			ReportDoc.BeginList ();
			foreach (string permission in package.Permissions) {
				ReportDoc.AddListItem ($"{permission}", MarkdownTextStyle.Monospace);
			}
			ReportDoc.EndList ();
		}

		AddSection ("Shared libraries", 2);
		if (package.SharedLibraries == null || package.SharedLibraries.Count == 0) {
			// Very unlikely...
			AddText ("No shared libraries found in the package");
		} else {
			AddText ($"Application contains the following {GetCountable (Countable.SharedLibrary, package.SharedLibraries.Count)}:");

			ReportDoc.BeginList ();
			foreach (SharedLibrary lib in package.SharedLibraries) {
				ReportDoc.StartListItem ($"{lib.Name}", MarkdownTextStyle.Monospace);

				// Markdown renderer has a bug where it won't render the first item of the sub-list
				// properly if the item line ends with a formatting character (or whitespace)
				AddText (":  ", addIndent: false);
				ReportDoc.BeginList ()
				         .AddLabeledListItem ("Alignment", $"{lib.Alignment}")
				         .AddLabeledListItem ("Debug info", $"{YesNo (lib.HasDebugInfo)}")
				         .AddLabeledListItem ("Size", $"{lib.Size}", appendLine: false)
				         .EndList ()
				         .EndListItem ();
			}
			ReportDoc.AddNewline ();
			ReportDoc.EndList ();
		}

		AddSection (".NET for Android application information", 1);
		AddLabeledItem ("Runtime", package.Runtime.ToString ());

		AddSection ("Assembly stores", 2);
		if (package.AssemblyStores == null || package.AssemblyStores.Count == 0) {
			AddText ("No assembly stores found");
		} else {
			AddText ($"Application contains the following {GetCountable (Countable.AssemblyStore, package.AssemblyStores.Count)}:");

			ReportDoc.BeginList ();
			foreach (AssemblyStore store in package.AssemblyStores) {
				ReportDoc.StartListItem ($"{store.Architecture}", MarkdownTextStyle.Monospace);
				AddListItemText ($" ({store.NumberOfAssemblies} {GetCountable (Countable.Assembly, store.NumberOfAssemblies)})");
				ReportDoc.EndListItem ();
			}
			ReportDoc.EndList ();
		}
	}
}
