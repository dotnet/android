using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Xamarin.Android.Tools;

namespace ApplicationUtility;

[AspectReporter (typeof (PackageAPK))]
[AspectReporter (typeof (PackageAAB))]
[AspectReporter (typeof (PackageBase))]
class ApplicationPackageReporter : BaseReporter
{
	sealed class AssemblyInfo
	{
		public ApplicationAssembly Assembly;
		public AndroidTargetArch Architecture;
		public bool IsSatellite;
	}

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
			return;
		}

		AddText ($"Application contains {package.AssemblyStores.Count} {GetCountable (Countable.AssemblyStore, package.AssemblyStores.Count)}.");
		AddParagraph ();

		if (package.AssemblyStores.Count == 1) {
			AddLabeledItem ("Architecture", package.AssemblyStores[0].Architecture.ToString ());

			// Take a shortcut here, there's just one store so we can use store reporter directly
			var storeReporter = new AssemblyStoreReporter (package.AssemblyStores[0], ReportDoc);
			storeReporter.Report (ReportForm.Subsection, sectionLevel: 2);
			return;
		}

		// To make output more compact, we group assemblies by name. For that reason we cannot use the designated
		// assembly reporter.
		var architectures = new HashSet<AndroidTargetArch> ();
		var assembliesByName = new SortedDictionary<string, List<AssemblyInfo>> (StringComparer.Ordinal);
		var assemblyCounts = new SortedDictionary<string, int> (StringComparer.Ordinal);

		foreach (AssemblyStore store in package.AssemblyStores) {
			architectures.Add (store.Architecture);
			assemblyCounts[store.Architecture.ToString ()] = store.Assemblies.Count;

			foreach (var kvp in store.Assemblies) {
				ApplicationAssembly asm = kvp.Value;
				bool isSatellite = asm.Name.Contains ('/');
				string name = isSatellite switch {
					true => GetModifiedSatelliteName (asm.Name),
					false => asm.Name,
				};

				if (!assembliesByName.TryGetValue (name, out List<AssemblyInfo>? assemblyInfos) || assemblyInfos == null) {
					assemblyInfos = new List<AssemblyInfo> ();
					assembliesByName[name] = assemblyInfos;
				}
				assemblyInfos.Add (
					new AssemblyInfo {
						Architecture = store.Architecture,
						Assembly = asm,
						IsSatellite = isSatellite,
					}
				);
			}
		}
		AddLabeledItem ("Architectures", String.Join (", ", architectures));
		AddLabeledItem ("Assembly count", String.Join (", ", assemblyCounts.Select (kvp => $"{kvp.Value} ({kvp.Key})")));

		ReportDoc.BeginList ();
		foreach (var kvp in assembliesByName) {
			List<AssemblyInfo> infos = kvp.Value;
			if (infos.Count == 0) {
				continue;
			}

			ReportDoc.StartListItem ($"{infos[0].Assembly.Name}").BeginList();

			ReportDoc.AddLabeledListItem ("Architectures", String.Join (", ", infos.Select (info => info.Architecture.ToString ()).Distinct ()));
			if (infos[0].IsSatellite) {
				ReportDoc.AddLabeledListItem ("Satellite", "yes");
				ReportDoc.AddLabeledListItem ("Culture", GetCultureInfo (infos));
			}

			ReportDoc.AddLabeledListItem ("Compressed", GetCompressedValue (infos));
			if (infos.Any (info => info.Assembly.IsCompressed)) {
				ReportDoc.AddLabeledListItem ("Compressed size", GetCompressedSizeValue (infos));
			}
			ReportDoc.AddLabeledListItem ("Size", GetSizeValue (infos));
			ReportDoc.AddLabeledListItem ("Name hash", GetNameHashValue (infos));
			ReportDoc.AddLabeledListItem ("Ignore on load", GetIgnoreOnLoadValue (infos));

			ReportDoc.EndList ().EndListItem ();
		}
		ReportDoc.AddNewline ();
		ReportDoc.EndList ();

		string GetCultureInfo (List<AssemblyInfo> infos)
		{
			var cultures = new HashSet<string> (StringComparer.Ordinal);
			var sb = new StringBuilder ();
			foreach (AssemblyInfo info in infos) {
				string cultureName = GetSatelliteCultureName (info.Assembly.Name);
				sb.Clear ();
				sb.Append (cultureName);

				var ci = !String.IsNullOrEmpty (cultureName) ? CultureInfo.GetCultureInfo (cultureName): null;
				if (ci != null) {
					sb.Append (" (");
					sb.Append (ci.NativeName);
					sb.Append ("; ");
					sb.Append (ci.EnglishName);
					sb.Append (')');
				}

				cultures.Add (sb.ToString ());
			}

			var cultureList = cultures.ToList ();
			cultureList.Sort ();
			return String.Join (", ", cultureList);
		}

		string GetIgnoreOnLoadValue (List<AssemblyInfo> infos)
		{
			return GetAggregatedValue<bool> (
				infos,
				(AssemblyInfo info) => info.Assembly.IgnoreOnLoad,
				(bool v) => YesNo (v)
			);
		}

		string GetNameHashValue (List<AssemblyInfo> infos)
		{
			return GetAggregatedValue<ulong> (
				infos,
				(AssemblyInfo info) => info.Assembly.NameHash,
				(ulong v) => $"0x{v:x}"
			);
		}

		string GetSizeValue (List<AssemblyInfo> infos)
		{
			return GetAggregatedValue<ulong> (
				infos,
				(AssemblyInfo info) => info.Assembly.Size,
				(ulong v) => Utilities.SizeToString (v)
			);
		}

		string GetCompressedSizeValue (List<AssemblyInfo> infos)
		{
			return GetAggregatedValue<ulong> (
				infos,
				(AssemblyInfo info) => info.Assembly.CompressedSize,
				(ulong v) => Utilities.SizeToString (v)
			);
		}

		string GetCompressedValue (List<AssemblyInfo> infos)
		{
			return GetAggregatedValue<bool> (
				infos,
				(AssemblyInfo info) => info.Assembly.IsCompressed,
				(bool v) => YesNo (v)
			);
		}

		bool AllIdentical<T> (List<AssemblyInfo> infos, T expected, Func<AssemblyInfo, T> getValue) where T: notnull
		{
			for (int i = 1; i < infos.Count; i++) {
				T val = getValue (infos[i]);
				if (!val.Equals (expected)) {
					return false;
				}
			}

			return true;
		}

		string GetAggregatedValue<T> (List<AssemblyInfo> infos, Func<AssemblyInfo, T> getValue, Func<T, string> valToString) where T: notnull
		{
			if (infos.Count == 1) {
				return getValue (infos[0]).ToString () ?? String.Empty;
			}

			T expected = getValue (infos[0]);
			if (AllIdentical (infos, expected, getValue)) {
				return valToString (expected);
			}

			var sb = new StringBuilder ();
			foreach (AssemblyInfo info in infos) {
				if (sb.Length > 0) {
					sb.Append ("; ");
				}

				sb.Append ($"{info.Architecture.ToString ()}: {getValue (info)}");
			}

			return sb.ToString ();
		}

		string GetSatelliteCultureName (string fullName)
		{
			if (fullName.Length == 0) {
				return fullName;
			}

			int idx = fullName.IndexOf ('/');
			if (idx < 0 || idx == fullName.Length - 1) {
				return fullName;
			}

			return fullName.Substring (0, idx);
		}

		string GetModifiedSatelliteName (string fullName)
		{
			if (fullName.Length == 0) {
				return fullName;
			}

			int idx = fullName.IndexOf ('/');
			if (idx < 0 || idx == fullName.Length - 1) {
				return fullName;
			}

			string name = fullName.Substring (idx + 1);
			string culture = fullName.Substring (0, idx);

			return $"{name} ({culture})";
		}

	}
}
