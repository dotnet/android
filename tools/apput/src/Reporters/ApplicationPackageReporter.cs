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
		ReportSharedLibraries ();

		AddSection (".NET for Android application information", 1);
		AddLabeledItem ("Runtime", package.Runtime.ToString ());

		ReportAssemblyStores ();
	}

	void ReportSharedLibraries ()
	{
		AddSection ("Shared libraries", 2);
		if (package.SharedLibraries == null || package.SharedLibraries.Count == 0) {
			// Very unlikely...
			AddText ("No shared libraries found in the package");
			return;
		}

		AddText ($"Application contains a total of {package.SharedLibraries.Count} native shared {GetCountable (Countable.SharedLibrary, package.SharedLibraries.Count)}.");
		AddParagraph ();

		var architectures = new HashSet<NativeArchitecture> ();
		var libraryCounts = new SortedDictionary<string, int> (StringComparer.Ordinal);
		var libsByName = new SortedDictionary<string, List<SharedLibrary>> (StringComparer.Ordinal);
		foreach (SharedLibrary lib in package.SharedLibraries) {
			architectures.Add (lib.TargetArchitecture);
			string archName = lib.TargetArchitecture.ToString ();
			if (!libraryCounts.ContainsKey (archName)) {
				libraryCounts[archName] = 0;
			}
			libraryCounts[archName]++;

			string name = Utilities.GetZipEntryFileName (lib.Name);
			if (!libsByName.TryGetValue (name, out List<SharedLibrary>? libs) || libs == null) {
				libs = new List<SharedLibrary> ();
				libsByName[name] = libs;
			}
			libs.Add (lib);
		}

		AddLabeledItem ("Architectures", String.Join (", ", architectures));

		if (architectures.Count == 1) {
			// With just one architecture we can use SharedLibraryReporter
			ReportDoc.BeginList ();
			foreach (var kvp in libsByName) {
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
			ReportDoc.AddNewline ().EndList ();
			return;
		}

		AddLabeledItem ("Library count", String.Join (", ", libraryCounts.Select (kvp => $"{kvp.Value} ({kvp.Key})")));

		ReportDoc.BeginList ();
		foreach (var kvp in libsByName) {
			ReportDoc.StartListItem ($"{kvp.Key}", MarkdownTextStyle.Monospace);
			AddText (":  ", addIndent: false);

			List<SharedLibrary> libs = kvp.Value;
			ReportDoc.BeginList ();
			ReportDoc.AddLabeledListItem ("Architectures", String.Join (", ", libs.Select (info => info.TargetArchitecture.ToString ()).Distinct ()));
			ReportDoc.AddLabeledListItem (SharedLibraryReporter.SizeLabel, GetSizeValue (libs));

			if (libs.Any (lib => lib.HasSoname)) {
				ReportDoc.AddLabeledListItem (SharedLibraryReporter.SonameLabel, GetSonameValue (libs));
			}

			if (libs.Any (lib => lib.HasBuildID)) {
				ReportDoc.AddLabeledListItem (SharedLibraryReporter.BuildIdLabel, GetBuildIdValue (libs));
			}

			ReportDoc.AddLabeledListItem (SharedLibraryReporter.AlignmentLabel, GetAlignmentValue (libs));
			ReportDoc.AddLabeledListItem (SharedLibraryReporter.DebugInfoLabel, GetDebugInfoValue (libs));

			if (libs.Any (lib => lib.HasDebugLink)) {
				ReportDoc.AddLabeledListItem (SharedLibraryReporter.DebugLinkLabel, GetDebugLinkValue (libs));
			}

			if (libs.Any (lib => lib.HasAndroidIdent)) {
				ReportDoc.AddLabeledListItem (SharedLibraryReporter.AndroidIdentLabel, GetAndroidIdentValue (libs));
			}

			if (libs.Any (lib => lib is MonoAotSharedLibrary)) {
				ReportDoc.AddLabeledListItem (SharedLibraryReporter.MonoAotLabel, GetMonoAotValue (libs));
			}

			if (libs.Any (lib => lib is NativeAotSharedLibrary)) {
				ReportDoc.AddLabeledListItem (SharedLibraryReporter.NativeAotLabel, GetNativeAotValue (libs));
			}

			if (libs.Any (lib => lib is XamarinAppSharedLibrary)) {
				ReportDoc.AddLabeledListItem (SharedLibraryReporter.XamarinAppLabel, GetXamarinAppValue (libs));
			}

			if (libs.Any (lib => lib is DotNetAndroidWrapperSharedLibrary)) {
				ReportDoc.AddLabeledListItem (SharedLibraryReporter.DotNetWrapperLabel, GetDotNetWrapperValue (libs));
			}

			ReportDoc.EndList ().EndListItem ();
		}
		ReportDoc.AddNewline ().EndList ();

		string GetDotNetWrapperValue (List<SharedLibrary> libs)
		{
			return GetAggregatedValue (
				libs,
				(SharedLibrary lib) => lib is DotNetAndroidWrapperSharedLibrary,
				(SharedLibrary lib, bool v) => $"{YesNo (v)}; Payload size: {GetFormatTag (lib)}",
				(SharedLibrary lib) => lib.TargetArchitecture.ToString ()
			);

			ulong GetFormatTag (SharedLibrary lib)
			{
				if (lib is DotNetAndroidWrapperSharedLibrary wrapper) {
					return wrapper.PayloadSize;
				}

				return 0;
			}
		}

		string GetXamarinAppValue (List<SharedLibrary> libs)
		{
			return GetAggregatedValue (
				libs,
				(SharedLibrary lib) => lib is XamarinAppSharedLibrary,
				(SharedLibrary lib, bool v) => $"{YesNo (v)}; Format tag: 0x{GetFormatTag (lib):x}",
				(SharedLibrary lib) => lib.TargetArchitecture.ToString ()
			);

			ulong GetFormatTag (SharedLibrary lib)
			{
				if (lib is XamarinAppSharedLibrary xapp) {
					return xapp.FormatTag;
				}

				return 0;
			}
		}

		string GetNativeAotValue (List<SharedLibrary> libs)
		{
			return GetAggregatedValue (
				libs,
				(SharedLibrary lib) => lib is NativeAotSharedLibrary,
				(SharedLibrary lib, bool v) => YesNo (v),
				(SharedLibrary lib) => lib.TargetArchitecture.ToString ()
			);
		}

		string GetMonoAotValue (List<SharedLibrary> libs)
		{
			return GetAggregatedValue (
				libs,
				(SharedLibrary lib) => lib is MonoAotSharedLibrary,
				(SharedLibrary lib, bool v) => YesNo (v),
				(SharedLibrary lib) => lib.TargetArchitecture.ToString ()
			);
		}

		string GetSizeValue (List<SharedLibrary> libs)
		{
			return GetAggregatedValue (
				libs,
				(SharedLibrary lib) => lib.Size,
				(SharedLibrary lib, ulong v) => Utilities.SizeToString (v),
				(SharedLibrary lib) => lib.TargetArchitecture.ToString ()
			);
		}

		string GetAndroidIdentValue (List<SharedLibrary> libs)
		{
			return GetAggregatedValue (
				libs,
				(SharedLibrary lib) => lib.AndroidIdent ?? String.Empty,
				(SharedLibrary lib, string v) => /* ValueOrNone (v) */ "FIXME",
				(SharedLibrary lib) => lib.TargetArchitecture.ToString ()
			);
		}

		string GetDebugLinkValue (List<SharedLibrary> libs)
		{
			return GetAggregatedValue (
				libs,
				(SharedLibrary lib) => lib.DebugLink ?? String.Empty,
				(SharedLibrary lib, string v) => ValueOrNone (v),
				(SharedLibrary lib) => lib.TargetArchitecture.ToString ()
			);
		}

		string GetDebugInfoValue (List<SharedLibrary> libs)
		{
			return GetAggregatedValue (
				libs,
				(SharedLibrary lib) => lib.HasDebugInfo,
				(SharedLibrary lib, bool v) => YesNo (v),
				(SharedLibrary lib) => lib.TargetArchitecture.ToString ()
			);
		}

		string GetAlignmentValue (List<SharedLibrary> libs)
		{
			return GetAggregatedValue (
				libs,
				(SharedLibrary lib) => lib.Alignment,
				(SharedLibrary lib, ulong v) => Utilities.SizeToString (v),
				(SharedLibrary lib) => lib.TargetArchitecture.ToString ()
			);
		}

		string GetBuildIdValue (List<SharedLibrary> libs)
		{
			return GetAggregatedValue (
				libs,
				(SharedLibrary lib) => lib.BuildID ?? "<no build-id>",
				(SharedLibrary lib, string v) => v,
				(SharedLibrary lib) => lib.TargetArchitecture.ToString ()
			);
		}

		string GetSonameValue (List<SharedLibrary> libs)
		{
			return GetAggregatedValue (
				libs,
				(SharedLibrary lib) => lib.Soname ?? "<no soname>",
				(SharedLibrary lib, string v) => v,
				(SharedLibrary lib) => lib.TargetArchitecture.ToString ()
			);
		}
	}

	void ReportAssemblyStores ()
	{
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
			return GetAggregatedValue (
				infos,
				(AssemblyInfo info) => info.Assembly.IgnoreOnLoad,
				(AssemblyInfo info, bool v) => YesNo (v),
				(AssemblyInfo info) => info.Architecture.ToString ()
			);
		}

		string GetNameHashValue (List<AssemblyInfo> infos)
		{
			return GetAggregatedValue (
				infos,
				(AssemblyInfo info) => info.Assembly.NameHash,
				(AssemblyInfo info, ulong v) => $"0x{v:x}",
				(AssemblyInfo info) => info.Architecture.ToString ()
			);
		}

		string GetSizeValue (List<AssemblyInfo> infos)
		{
			return GetAggregatedValue (
				infos,
				(AssemblyInfo info) => info.Assembly.Size,
				(AssemblyInfo info, ulong v) => Utilities.SizeToString (v),
				(AssemblyInfo info) => info.Architecture.ToString ()
			);
		}

		string GetCompressedSizeValue (List<AssemblyInfo> infos)
		{
			return GetAggregatedValue (
				infos,
				(AssemblyInfo info) => info.Assembly.CompressedSize,
				(AssemblyInfo info, ulong v) => Utilities.SizeToString (v),
				(AssemblyInfo info) => info.Architecture.ToString ()
			);
		}

		string GetCompressedValue (List<AssemblyInfo> infos)
		{
			return GetAggregatedValue (
				infos,
				(AssemblyInfo info) => info.Assembly.IsCompressed,
				(AssemblyInfo info, bool v) => YesNo (v),
				(AssemblyInfo info) => info.Architecture.ToString ()
			);
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

	bool AllIdentical<V, T> (List<T> infos, V expected, Func<T, V> getValue) where V: notnull where T: class
	{
		for (int i = 1; i < infos.Count; i++) {
			V val = getValue (infos[i]);
			if (!val.Equals (expected)) {
				return false;
			}
		}

		return true;
	}

	string GetAggregatedValue<V, T> (List<T> infos, Func<T, V> getValue, Func<T, V, string> valToString, Func<T, string> getArchName) where V: notnull where T: class
	{
		if (infos.Count == 1) {
			return getValue (infos[0]).ToString () ?? String.Empty;
		}

		V expected = getValue (infos[0]);
		if (AllIdentical (infos, expected, getValue)) {
			return valToString (infos[0], expected);
		}

		var sb = new StringBuilder ();
		foreach (T info in infos) {
			if (sb.Length > 0) {
				sb.Append ("; ");
			}

			sb.Append ($"{getArchName(info)}: {getValue (info)}");
		}

		return sb.ToString ();
	}
}
