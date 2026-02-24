using System;
using System.Collections.Generic;

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

	protected override void DoReport (ReportForm form, uint sectionLevel)
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
				ReportDoc.AddListItem ($"{permission}", MarkdownTextStyle.Monospace, styledWorkaroundTail: ";");
			}
			ReportDoc.AddNewline ().EndList ();
		}

		AddSection ("Shared libraries", 2);
		if (package.SharedLibraries == null || package.SharedLibraries.Count == 0) {
			// Very unlikely...
			AddText ("No shared libraries found in the package");
		} else {
			AddText ($"Application contains the following {package.SharedLibraries.Count} native shared {GetCountable (Countable.SharedLibrary, package.SharedLibraries.Count)}:");

			// Group and sort libraries by name, then architecture
			var libs = new SortedDictionary<string, List<SharedLibrary>> (StringComparer.Ordinal);
			foreach (SharedLibrary lib in package.SharedLibraries) {
				string name = Utilities.GetZipEntryFileName (lib.Name);
				if (!libs.TryGetValue (name, out List<SharedLibrary>? libsByName) || libsByName == null) {
					libsByName = new List<SharedLibrary> ();
					libs[name] = libsByName;
				}
				libsByName.Add (lib);
			}

			foreach (var kvp in libs) {
				kvp.Value.Sort ((SharedLibrary a, SharedLibrary b) => a.Name.CompareTo (b.Name));
			}

			ReportDoc.BeginList ();
			foreach (var kvp in libs) {
				foreach (SharedLibrary lib in kvp.Value) {
					ReportDoc.StartListItem ($"{lib.Name}", MarkdownTextStyle.Monospace);

					// Markdown renderer has a bug where it won't render the first item of the sub-list
					// properly if the item line ends with a formatting character (or whitespace without
					// preceding unformatted character)
					AddText (":  ", addIndent: false);
					var libReporter = new SharedLibraryReporter (lib, ReportDoc);
					libReporter.Report (ReportForm.SimpleList);
				}
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
			AddText ($"Application contains the following {package.AssemblyStores.Count} {GetCountable (Countable.AssemblyStore, package.AssemblyStores.Count)}.");

			foreach (AssemblyStore store in package.AssemblyStores) {
				AddSection ($"Architecture: {store.Architecture}");

				var storeReporter = new AssemblyStoreReporter (store, ReportDoc);
				storeReporter.Report (ReportForm.Subsection, sectionLevel: 2);
			}
		}
	}
}
