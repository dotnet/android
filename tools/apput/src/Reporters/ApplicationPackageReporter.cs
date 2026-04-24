using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;

namespace ApplicationUtility;

using ReadyToRunOperatingSystem = ILCompiler.Reflection.ReadyToRun.OperatingSystem;

/// <summary>
/// Generates a comprehensive report for an Android application package (APK, AAB, or base module),
/// including package metadata, manifest info, assembly stores, shared libraries, and architecture details.
/// </summary>
[AspectReporter (typeof (PackageAPK))]
[AspectReporter (typeof (PackageAAB))]
[AspectReporter (typeof (PackageBase))]
class ApplicationPackageReporter : BaseReporter
{
	readonly ApplicationPackage package;

	protected override string AspectName => package.AspectName;
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

		ReportStandaloneAssemblies ();
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

		AddLabeledItem ("Total number of native shared libraries", $"{package.SharedLibraries.Count}");
		AddLabeledItem ("Total size of all shared libraries", Utilities.SizeToString (package.SharedLibraries.Sum (l => (decimal)l.Size), reportBytes: false));

		var architectures = new HashSet<NativeArchitecture> ();
		var libraryCounts = new SortedDictionary<string, ulong> (StringComparer.Ordinal);
		var librarySizes = new SortedDictionary<string, decimal> (StringComparer.Ordinal);
		var libsByName = new SortedDictionary<string, List<SharedLibrary>> (StringComparer.Ordinal);
		foreach (SharedLibrary lib in package.SharedLibraries) {
			architectures.Add (lib.TargetArchitecture);
			string archName = lib.TargetArchitecture.ToString ();
			if (!libraryCounts.ContainsKey (archName)) {
				libraryCounts[archName] = 0;
			}
			if (!librarySizes.ContainsKey (archName)) {
				librarySizes[archName] = 0;
			}

			libraryCounts[archName]++;
			librarySizes[archName] += lib.Size;

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

		AddLabeledItem ("Library count per architecture", String.Join (", ", libraryCounts.Select (kvp => $"{kvp.Value} ({kvp.Key})")));
		AddLabeledItem ("Libraries size per architecture", String.Join (", ", librarySizes.Select (kvp => $"{Utilities.SizeToString (kvp.Value, reportBytes: false)} ({kvp.Key})")));

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

			ReportDoc.EndList ().EndListItem (appendLine: false);
		}
		ReportDoc.AddNewline ().EndList ();

		string GetDotNetWrapperValue (List<SharedLibrary> libs)
		{
			return GetAggregatedValue (
				libs,
				(SharedLibrary lib) => lib is DotNetAndroidWrapperSharedLibrary,
				(SharedLibrary lib, bool v) => $"{YesNo (v)}; Payload size: {Utilities.SizeToString (GetFormatTag (lib))}",
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
				(SharedLibrary lib, string v) => v,
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

	void ReportStandaloneAssemblies ()
	{
		const uint TopSectionLevel = 2;

		if (package.StandaloneAssemblies == null || package.StandaloneAssemblies.Count == 0) {
			return;
		}

		AddSection ("Individual assemblies", TopSectionLevel);
		AddText ($"Application contains {package.StandaloneAssemblies.Count} individually stored {GetCountable (Countable.Assembly, package.StandaloneAssemblies.Count)}");
		AddParagraph ();

		ReportAssemblies (package.StandaloneAssemblies, package.StandalonePdbs, TopSectionLevel);
	}

	void ReportAssemblyStores ()
	{
		const uint TopSectionLevel = 2;

		AddSection ("Assembly stores", TopSectionLevel);
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
			storeReporter.Report (ReportForm.Subsection, sectionLevel: TopSectionLevel);
			return;
		}

		var allAssemblies = new List<ApplicationAssembly> ();
		var allPdbs = new List<AssemblyPdb> ();
		foreach (AssemblyStore store in package.AssemblyStores) {
			allAssemblies.AddRange (store.Assemblies.Values);
			allPdbs.AddRange (store.PDBs.Values);
		}

		ReportAssemblies (allAssemblies, allPdbs, TopSectionLevel);
	}

	void ReportAssemblies (ICollection<ApplicationAssembly> assemblies, ICollection<AssemblyPdb>? pdbs, uint topSectionLevel)
	{
		var asmPdbs = new Dictionary<string, List<AssemblyPdb>> (StringComparer.Ordinal);
		if (pdbs != null && pdbs.Count > 0) {
			foreach (AssemblyPdb pdb in pdbs) {
				string dllName = Path.ChangeExtension (pdb.Name, ".dll");
				if (!asmPdbs.TryGetValue (dllName, out List<AssemblyPdb>? pdbList) || pdbList == null) {
					pdbList = new ();
				}
				pdbList.Add (pdb);
				asmPdbs.Add (dllName, pdbList);
			}
		}

		// To make output more compact, we group assemblies by name. For that reason we cannot use the designated
		// assembly reporter.
		var architectures = new HashSet<NativeArchitecture> ();
		var assembliesByName = new SortedDictionary<string, List<ApplicationAssembly>> (StringComparer.OrdinalIgnoreCase);
		var assemblyCounts = new SortedDictionary<NativeArchitecture, int> ();

		foreach (ApplicationAssembly asm in assemblies) {
			architectures.Add (asm.Architecture);
			if (!assemblyCounts.ContainsKey (asm.Architecture)) {
				assemblyCounts.Add (asm.Architecture, 0);
			}
			assemblyCounts[asm.Architecture]++;

			string name = GetModifiedSatelliteName (asm);
			if (!assembliesByName.TryGetValue (name, out List<ApplicationAssembly>? assemblyList) || assemblyList == null) {
				assemblyList = new List<ApplicationAssembly> ();
				assembliesByName[name] = assemblyList;
			}
			assemblyList.Add (asm);
		}

		AddLabeledItem ("Architectures", String.Join (", ", architectures));
		AddLabeledItem ("Assembly count", String.Join (", ", assemblyCounts.Select (kvp => $"{kvp.Value} ({kvp.Key})")));

		AddSection ("Assemblies", topSectionLevel + 1);
		ReportDoc.BeginList (appendLine: false);
		foreach (var kvp in assembliesByName) {
			List<ApplicationAssembly> assemblyList = kvp.Value;
			if (assemblyList.Count == 0) {
				continue;
			}

			ReportDoc.StartListItem (assemblyList[0].FullName).BeginList();

			ReportDoc.AddLabeledListItem ("Architectures", String.Join (", ", assemblyList.Select (asm => asm.Architecture.ToString ()).Distinct ()));

			ReportDoc.AddLabeledListItem ("Size", GetSizeValue (assemblyList));
			ReportDoc.AddLabeledListItem ("Compressed", GetCompressedValue (assemblyList));
			if (assemblyList.Any (asm => asm.IsCompressed)) {
				ReportDoc.AddLabeledListItem ("Compressed size", GetCompressedSizeValue (assemblyList));
			}

			ReportDoc.AddLabeledListItem ("Debug info (PDB) present", GetHasPdbValue (assemblyList));
			if (asmPdbs.ContainsKey (assemblyList[0].Name)) {
				ReportDoc.AddLabeledListItem ("PDB size", GetPdbSizeValue (assemblyList));
			}

			if (assemblyList[0].IsRTR) {
				ReportDoc.AddLabeledListItem ("ReadyToRun image", GetIsRtrValue (assemblyList));
				ReportDoc.AddLabeledListItem ("RTR target machine", GetRtrTargetMachineValue (assemblyList));
				ReportDoc.AddLabeledListItem ("RTR target operating system", GetRtrTargetOperatingSystemValue (assemblyList));
			}

			if (assemblyList[0].IsSatellite) {
				ReportDoc.AddLabeledListItem ("Satellite", "yes");
				ReportDoc.AddLabeledListItem ("Culture", GetCultureInfo (assemblyList));
			}

			ReportDoc.AddLabeledListItem ("Name hash", GetNameHashValue (assemblyList));
			ReportDoc.AddLabeledListItem ("Ignore on load", GetIgnoreOnLoadValue (assemblyList));

			ReportDoc.EndList ().EndListItem (appendLine: false);
		}
		ReportDoc.AddNewline ().EndList ();

		string GetCultureInfo (List<ApplicationAssembly> assemblyList)
		{
			var cultures = new HashSet<string> (StringComparer.Ordinal);
			foreach (ApplicationAssembly asm in assemblyList) {
				cultures.Add (Utilities.GetCultureInfo (asm.Culture));
			}

			var cultureList = cultures.ToList ();
			cultureList.Sort ();
			return String.Join (", ", cultureList);
		}

		string GetIsRtrValue (List<ApplicationAssembly> assemblyList)
		{
			return GetAggregatedValue (
				assemblyList,
				(ApplicationAssembly asm) => asm.IsRTR,
				(ApplicationAssembly asm, bool v) => YesNo (v),
				(ApplicationAssembly asm) => asm.Architecture.ToString ()
			);
		}

		string GetRtrTargetMachineValue (List<ApplicationAssembly> assemblyList)
		{
			return GetAggregatedValue (
				assemblyList,
				(ApplicationAssembly asm) => asm.RTRMachine,
				(ApplicationAssembly asm, Machine v) => v.ToString (),
				(ApplicationAssembly asm) => asm.Architecture.ToString ()
			);
		}

		string GetRtrTargetOperatingSystemValue (List<ApplicationAssembly> assemblyList)
		{
			return GetAggregatedValue (
				assemblyList,
				(ApplicationAssembly asm) => asm.RTROS,
				(ApplicationAssembly asm, ReadyToRunOperatingSystem v) => v.ToString (),
				(ApplicationAssembly asm) => asm.Architecture.ToString ()
			);
		}

		string GetHasPdbValue (List<ApplicationAssembly> assemblyList)
		{
			return GetAggregatedValue (
				assemblyList,
				(ApplicationAssembly asm) => asmPdbs.ContainsKey (asm.Name),
				(ApplicationAssembly asm, bool v) => YesNo (v),
				(ApplicationAssembly asm) => asm.Architecture.ToString ()
			);
		}

		string GetPdbSizeValue (List<ApplicationAssembly> assemblyList)
		{
			return GetAggregatedValue (
				assemblyList,
				(ApplicationAssembly asm) => GetPdbSize (asm),
				(ApplicationAssembly asm, ulong v) => Utilities.SizeToString (v),
				(ApplicationAssembly asm) => asm.Architecture.ToString ()
			);

			ulong GetPdbSize (ApplicationAssembly asm)
			{
				if (!asmPdbs.TryGetValue (asm.Name, out List<AssemblyPdb>? pdbList) || pdbList == null || pdbList.Count == 0) {
					return 0;
				}

				foreach (AssemblyPdb pdb in pdbList) {
					if (pdb.Architecture == asm.Architecture) {
						return pdb.Size;
					}
				}

				return 0;
			}
		}

		string GetIgnoreOnLoadValue (List<ApplicationAssembly> assemblyList)
		{
			return GetAggregatedValue (
				assemblyList,
				(ApplicationAssembly asm) => asm.IgnoreOnLoad,
				(ApplicationAssembly asm, bool v) => YesNo (v),
				(ApplicationAssembly asm) => asm.Architecture.ToString ()
			);
		}

		string GetNameHashValue (List<ApplicationAssembly> assemblyList)
		{
			return GetAggregatedValue (
				assemblyList,
				(ApplicationAssembly asm) => asm.NameHash,
				(ApplicationAssembly asm, ulong v) => $"0x{v:x}",
				(ApplicationAssembly asm) => asm.Architecture.ToString ()
			);
		}

		string GetSizeValue (List<ApplicationAssembly> assemblyList)
		{
			return GetAggregatedValue (
				assemblyList,
				(ApplicationAssembly asm) => asm.Size,
				(ApplicationAssembly asm, ulong v) => Utilities.SizeToString (v),
				(ApplicationAssembly asm) => asm.Architecture.ToString ()
			);
		}

		string GetCompressedSizeValue (List<ApplicationAssembly> assemblyList)
		{
			return GetAggregatedValue (
				assemblyList,
				(ApplicationAssembly asm) => asm.CompressedSize,
				(ApplicationAssembly asm, ulong v) => Utilities.SizeToString (v),
				(ApplicationAssembly asm) => asm.Architecture.ToString ()
			);
		}

		string GetCompressedValue (List<ApplicationAssembly> assemblyList)
		{
			return GetAggregatedValue (
				assemblyList,
				(ApplicationAssembly asm) => asm.IsCompressed,
				(ApplicationAssembly asm, bool v) => YesNo (v),
				(ApplicationAssembly asm) => asm.Architecture.ToString ()
			);
		}

		string GetModifiedSatelliteName (ApplicationAssembly asm)
		{
			if (!asm.IsSatellite) {
				return asm.Name;
			}

			return $"{asm.Name} ({asm.Culture})";
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
			return valToString (infos[0], getValue (infos[0]));
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

			sb.Append ($"{getArchName(info)}: {valToString (info, getValue (info))}");
		}

		return sb.ToString ();
	}
}
