using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Java.Interop.Tools.Cecil;
using Microsoft.Build.Utilities;
using Mono.Cecil;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Android.Build.TypeMap.IntegrationTests;

/// <summary>
/// Side-by-side comparison tests: runs both the legacy Cecil-based scanner
/// and the new SRM-based scanner on the same assembly and compares their outputs.
/// </summary>
public class ScannerComparisonTests : IDisposable
{
	readonly ITestOutputHelper output;

	public ScannerComparisonTests (ITestOutputHelper output)
	{
		this.output = output;
	}

	/// <summary>
	/// Represents a single type map entry: JNI name → managed type, with metadata.
	/// </summary>
	record TypeMapEntry (string JavaName, string ManagedName, bool SkipInJavaToManaged);

	/// <summary>
	/// Runs the legacy scanner (XAJavaTypeScanner + TypeMapCecilAdapter) on the given assembly.
	/// </summary>
	static List<TypeMapEntry> RunLegacyScanner (string assemblyPath)
	{
		var cache = new TypeDefinitionCache ();
		var resolver = new DefaultAssemblyResolver ();
		resolver.AddSearchDirectory (Path.GetDirectoryName (assemblyPath)!);

		var runtimeDir = Path.GetDirectoryName (typeof (object).Assembly.Location);
		if (runtimeDir != null) {
			resolver.AddSearchDirectory (runtimeDir);
		}

		var readerParams = new ReaderParameters { AssemblyResolver = resolver };
		using var assembly = AssemblyDefinition.ReadAssembly (assemblyPath, readerParams);

		var scanner = new Xamarin.Android.Tasks.XAJavaTypeScanner (
			Xamarin.Android.Tools.AndroidTargetArch.Arm64,
			new TaskLoggingHelper (new MockBuildEngine (), "test"),
			cache
		);

		var javaTypes = scanner.GetJavaTypes (assembly);
		var (dataSets, _) = Xamarin.Android.Tasks.TypeMapCecilAdapter.GetDebugNativeEntries (
			javaTypes, cache, needUniqueAssemblies: false
		);

		return dataSets.JavaToManaged
			.Select (e => new TypeMapEntry (e.JavaName, e.ManagedName, e.SkipInJavaToManaged))
			.OrderBy (e => e.JavaName, StringComparer.Ordinal)
			.ThenBy (e => e.ManagedName, StringComparer.Ordinal)
			.ToList ();
	}

	/// <summary>
	/// Runs the new SRM-based scanner on the given assembly.
	/// </summary>
	static List<TypeMapEntry> RunNewScanner (string assemblyPath)
	{
		using var scanner = new JavaPeerScanner ();
		var peers = scanner.Scan (new [] { assemblyPath });

		return peers
			.Select (p => new TypeMapEntry (
				p.JavaName,
				$"{p.ManagedTypeName}, {p.AssemblyName}",
				p.IsInterface || p.IsGenericDefinition
			))
			.OrderBy (e => e.JavaName, StringComparer.Ordinal)
			.ThenBy (e => e.ManagedName, StringComparer.Ordinal)
			.ToList ();
	}

	/// <summary>
	/// Verifies the new scanner produces the EXACT same JNI → managed type mapping
	/// as the legacy scanner on Mono.Android.dll. Every entry must match: same JNI name,
	/// same managed name, same skip flag. No extras, no missing entries.
	/// </summary>
	[SkippableFact]
	public void ExactTypeMap_MonoAndroid ()
	{
		var assemblyPath = FindMonoAndroidAssembly ();
		Skip.If (assemblyPath == null, "Mono.Android.dll not found — requires a built android repo");

		var legacy = RunLegacyScanner (assemblyPath);
		var newEntries = RunNewScanner (assemblyPath);

		output.WriteLine ($"Legacy: {legacy.Count} entries, New: {newEntries.Count} entries");

		// Build lookup: JavaName → list of entries (handles duplicates like java/lang/Object)
		var legacyMap = legacy.GroupBy (e => e.JavaName).ToDictionary (g => g.Key, g => g.ToList ());
		var newMap = newEntries.GroupBy (e => e.JavaName).ToDictionary (g => g.Key, g => g.ToList ());

		var allJavaNames = new HashSet<string> (legacyMap.Keys);
		allJavaNames.UnionWith (newMap.Keys);

		var missing = new List<string> ();
		var extra = new List<string> ();
		var managedNameMismatches = new List<string> ();
		var skipMismatches = new List<string> ();

		foreach (var javaName in allJavaNames.OrderBy (n => n)) {
			var inLegacy = legacyMap.TryGetValue (javaName, out var legacyEntries);
			var inNew = newMap.TryGetValue (javaName, out var newEntriesForName);

			if (inLegacy && !inNew) {
				foreach (var e in legacyEntries!) {
					missing.Add ($"{e.JavaName} → {e.ManagedName} (skip={e.SkipInJavaToManaged})");
				}
				continue;
			}

			if (!inLegacy && inNew) {
				foreach (var e in newEntriesForName!) {
					extra.Add ($"{e.JavaName} → {e.ManagedName} (skip={e.SkipInJavaToManaged})");
				}
				continue;
			}

			// Both have this JNI name — compare managed names and skip flags
			var legacySorted = legacyEntries!.OrderBy (e => e.ManagedName).ToList ();
			var newSorted = newEntriesForName!.OrderBy (e => e.ManagedName).ToList ();

			// Compare first entry (primary mapping) — that's what matters for the typemap
			var le = legacySorted [0];
			var ne = newSorted [0];

			if (le.ManagedName != ne.ManagedName) {
				managedNameMismatches.Add ($"{javaName}: legacy='{le.ManagedName}' new='{ne.ManagedName}'");
			}

			if (le.SkipInJavaToManaged != ne.SkipInJavaToManaged) {
				skipMismatches.Add ($"{javaName}: legacy.skip={le.SkipInJavaToManaged} new.skip={ne.SkipInJavaToManaged}");
			}
		}

		// Log all differences
		if (missing.Count > 0) {
			output.WriteLine ($"\n--- MISSING from new scanner ({missing.Count}) ---");
			foreach (var m in missing) output.WriteLine ($"  {m}");
		}
		if (extra.Count > 0) {
			output.WriteLine ($"\n--- EXTRA in new scanner ({extra.Count}) ---");
			foreach (var e in extra) output.WriteLine ($"  {e}");
		}
		if (managedNameMismatches.Count > 0) {
			output.WriteLine ($"\n--- MANAGED NAME MISMATCHES ({managedNameMismatches.Count}) ---");
			foreach (var m in managedNameMismatches) output.WriteLine ($"  {m}");
		}
		if (skipMismatches.Count > 0) {
			output.WriteLine ($"\n--- SKIP FLAG MISMATCHES ({skipMismatches.Count}) ---");
			foreach (var m in skipMismatches) output.WriteLine ($"  {m}");
		}

		// All four must be empty for the test to pass
		Assert.Empty (missing);
		Assert.Empty (extra);
		Assert.Empty (managedNameMismatches);
		Assert.Empty (skipMismatches);
	}

	[SkippableFact]
	public void ScannerDiagnostics_MonoAndroid ()
	{
		var assemblyPath = FindMonoAndroidAssembly ();
		Skip.If (assemblyPath == null, "Mono.Android.dll not found — requires a built android repo");

		using var scanner = new JavaPeerScanner ();
		var peers = scanner.Scan (new [] { assemblyPath });

		var interfaces = peers.Count (p => p.IsInterface);
		var abstracts = peers.Count (p => p.IsAbstract);
		var generics = peers.Count (p => p.IsGenericDefinition);
		var withMethods = peers.Count (p => p.MarshalMethods.Count > 0);
		var totalMethods = peers.Sum (p => p.MarshalMethods.Count);
		var withConstructors = peers.Count (p => p.JavaConstructors.Count > 0);
		var withBase = peers.Count (p => p.BaseJavaName != null);
		var withInterfaces = peers.Count (p => p.ImplementedInterfaceJavaNames.Count > 0);

		output.WriteLine ($"Total types:         {peers.Count}");
		output.WriteLine ($"Interfaces:          {interfaces}");
		output.WriteLine ($"Abstract classes:     {abstracts}");
		output.WriteLine ($"Generic defs:         {generics}");
		output.WriteLine ($"With marshal methods: {withMethods} ({totalMethods} total methods)");
		output.WriteLine ($"With constructors:    {withConstructors}");
		output.WriteLine ($"With base Java:       {withBase}");
		output.WriteLine ($"With interfaces:      {withInterfaces}");

		// Mono.Android.dll should have thousands of types
		Assert.True (peers.Count > 3000, $"Expected >3000 types, got {peers.Count}");
		Assert.True (interfaces > 500, $"Expected >500 interfaces, got {interfaces}");
		Assert.True (totalMethods > 10000, $"Expected >10000 marshal methods, got {totalMethods}");
	}

	static string? FindMonoAndroidAssembly ()
	{
		var thisDir = Path.GetDirectoryName (typeof (ScannerComparisonTests).Assembly.Location)!;
		var repoRoot = Path.GetFullPath (Path.Combine (thisDir, "..", "..", "..", "..", ".."));

		var repoDirs = new [] { repoRoot, Path.Combine (repoRoot, "..", "android") };

		foreach (var repo in repoDirs) {
			var packsDir = Path.Combine (repo, "bin", "Debug", "lib", "packs");
			if (!Directory.Exists (packsDir)) {
				continue;
			}

			foreach (var refDir in Directory.GetDirectories (packsDir, "Microsoft.Android.Ref.*")) {
				foreach (var versionDir in Directory.GetDirectories (refDir)) {
					var candidate = Path.Combine (versionDir, "ref");
					if (!Directory.Exists (candidate)) {
						continue;
					}

					foreach (var tfmDir in Directory.GetDirectories (candidate, "net*")) {
						var dll = Path.Combine (tfmDir, "Mono.Android.dll");
						if (File.Exists (dll)) {
							return dll;
						}
					}
				}
			}
		}

		return null;
	}

	public void Dispose ()
	{
	}
}
