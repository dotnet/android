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

using LegacyTypeMapDebugEntry = Xamarin.Android.Tasks.TypeMapGenerator.TypeMapDebugEntry;
using LegacyTypeMapDebugDataSets = Xamarin.Android.Tasks.TypeMapGenerator.TypeMapDebugDataSets;

namespace Microsoft.Android.Build.TypeMap.IntegrationTests;

/// <summary>
/// Side-by-side comparison tests: runs both the legacy Cecil-based scanner
/// and the new SRM-based scanner on the same assembly and compares their outputs.
/// </summary>
public class ScannerComparisonTests : IDisposable
{
	readonly ITestOutputHelper output;

	// Matches "crc64" followed by hex digits, e.g. "crc64a1b2c3d4e5f67890"
	static readonly Regex Crc64Pattern = new Regex (@"crc64[0-9a-fA-F]+", RegexOptions.Compiled);

	public ScannerComparisonTests (ITestOutputHelper output)
	{
		this.output = output;
	}

	/// <summary>
	/// Normalizes a JNI name by replacing any "crc64{hex}" portion with "crc64HASH".
	/// This allows comparing names between legacy (crc-64-jones) and new (ECMA-182) scanners
	/// since the hash algorithm differs but the type name portion is the same.
	/// </summary>
	static string NormalizeCrc64Name (string name) => Crc64Pattern.Replace (name, "crc64HASH");

	/// <summary>
	/// Runs the legacy scanner (XAJavaTypeScanner + TypeMapCecilAdapter) on the given assembly.
	/// Returns a sorted set of (JavaName, ManagedName, SkipInJavaToManaged) tuples.
	/// </summary>
	static List<LegacyEntry> RunLegacyScanner (string assemblyPath)
	{
		var cache = new TypeDefinitionCache ();
		var resolver = new DefaultAssemblyResolver ();
		resolver.AddSearchDirectory (Path.GetDirectoryName (assemblyPath)!);

		// Also add the .NET runtime directory for System.Runtime etc.
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
			.Select (e => new LegacyEntry (e.JavaName, e.ManagedName, e.SkipInJavaToManaged, e.AssemblyName))
			.OrderBy (e => e.JavaName, StringComparer.Ordinal)
			.ThenBy (e => e.ManagedName, StringComparer.Ordinal)
			.ToList ();
	}

	/// <summary>
	/// Runs the new SRM-based scanner on the given assembly.
	/// Returns a sorted set of comparable entries.
	/// </summary>
	static List<NewEntry> RunNewScanner (string assemblyPath)
	{
		using var scanner = new JavaPeerScanner ();
		var peers = scanner.Scan (new [] { assemblyPath });

		return peers
			.Select (p => new NewEntry (
				p.JavaName,
				$"{p.ManagedTypeName}, {p.AssemblyName}",
				p.IsInterface || p.IsGenericDefinition,
				p.AssemblyName
			))
			.OrderBy (e => e.JavaName, StringComparer.Ordinal)
			.ThenBy (e => e.ManagedName, StringComparer.Ordinal)
			.ToList ();
	}

	[SkippableFact]
	public void SameJavaNames_MonoAndroid ()
	{
		var assemblyPath = FindMonoAndroidAssembly ();
		Skip.If (assemblyPath == null, "Mono.Android.dll not found");

		var legacy = RunLegacyScanner (assemblyPath);
		var newResults = RunNewScanner (assemblyPath);

		output.WriteLine ($"Legacy found {legacy.Count} types, New found {newResults.Count} types");

		var legacyNames = new HashSet<string> (legacy.Select (e => NormalizeCrc64Name (e.JavaName)));
		var newNames = new HashSet<string> (newResults.Select (e => NormalizeCrc64Name (e.JavaName)));

		var onlyInLegacy = legacyNames.Except (newNames).OrderBy (n => n).ToList ();
		var onlyInNew = newNames.Except (legacyNames).OrderBy (n => n).ToList ();

		foreach (var name in onlyInLegacy) {
			output.WriteLine ($"ONLY IN LEGACY: {name}");
		}
		foreach (var name in onlyInNew) {
			output.WriteLine ($"ONLY IN NEW: {name}");
		}

		Assert.Empty (onlyInLegacy);
		Assert.Empty (onlyInNew);
	}

	[SkippableFact]
	public void SameSkipInJavaToManaged_MonoAndroid ()
	{
		var assemblyPath = FindMonoAndroidAssembly ();
		Skip.If (assemblyPath == null, "Mono.Android.dll not found");

		var legacy = RunLegacyScanner (assemblyPath);
		var newResults = RunNewScanner (assemblyPath);

		var legacyByJavaName = legacy.GroupBy (e => NormalizeCrc64Name (e.JavaName)).ToDictionary (g => g.Key, g => g.First ());
		var newByJavaName = newResults.GroupBy (e => NormalizeCrc64Name (e.JavaName)).ToDictionary (g => g.Key, g => g.First ());

		var commonNames = legacyByJavaName.Keys.Intersect (newByJavaName.Keys).OrderBy (n => n);

		var mismatches = new List<string> ();
		foreach (var name in commonNames) {
			var legacyEntry = legacyByJavaName [name];
			var newEntry = newByJavaName [name];

			if (legacyEntry.SkipInJavaToManaged != newEntry.SkipInJavaToManaged) {
				mismatches.Add ($"{name}: legacy.Skip={legacyEntry.SkipInJavaToManaged}, new.Skip={newEntry.SkipInJavaToManaged}");
			}
		}

		foreach (var m in mismatches) {
			output.WriteLine ($"MISMATCH: {m}");
		}

		Assert.Empty (mismatches);
	}

	[SkippableFact]
	public void SameManagedNames_MonoAndroid ()
	{
		var assemblyPath = FindMonoAndroidAssembly ();
		Skip.If (assemblyPath == null, "Mono.Android.dll not found");

		var legacy = RunLegacyScanner (assemblyPath);
		var newResults = RunNewScanner (assemblyPath);

		var legacyByJavaName = legacy.GroupBy (e => NormalizeCrc64Name (e.JavaName)).ToDictionary (g => g.Key, g => g.First ());
		var newByJavaName = newResults.GroupBy (e => NormalizeCrc64Name (e.JavaName)).ToDictionary (g => g.Key, g => g.First ());

		var commonNames = legacyByJavaName.Keys.Intersect (newByJavaName.Keys).OrderBy (n => n);

		var mismatches = new List<string> ();
		foreach (var name in commonNames) {
			var legacyEntry = legacyByJavaName [name];
			var newEntry = newByJavaName [name];

			if (legacyEntry.ManagedName != newEntry.ManagedName) {
				mismatches.Add ($"{name}: legacy='{legacyEntry.ManagedName}', new='{newEntry.ManagedName}'");
			}
		}

		foreach (var m in mismatches) {
			output.WriteLine ($"MISMATCH: {m}");
		}

		Assert.Empty (mismatches);
	}

	static string? FindMonoAndroidAssembly ()
	{
		var thisDir = Path.GetDirectoryName (typeof (ScannerComparisonTests).Assembly.Location)!;
		var repoRoot = Path.GetFullPath (Path.Combine (thisDir, "..", "..", "..", "..", ".."));

		// Try same repo first, then sibling
		var repoDirs = new [] { repoRoot, Path.Combine (repoRoot, "..", "android") };

		foreach (var repo in repoDirs) {
			var packsDir = Path.Combine (repo, "bin", "Debug", "lib", "packs");
			if (!Directory.Exists (packsDir)) {
				continue;
			}

			// Find any Microsoft.Android.Ref.* directory
			foreach (var refDir in Directory.GetDirectories (packsDir, "Microsoft.Android.Ref.*")) {
				// Find version directories inside
				foreach (var versionDir in Directory.GetDirectories (refDir)) {
					var candidate = Path.Combine (versionDir, "ref");
					if (!Directory.Exists (candidate)) {
						continue;
					}

					// Find any net*.0 TFM directory
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

	[SkippableFact]
	public void ScannerDiagnostics_MonoAndroid ()
	{
		var assemblyPath = FindMonoAndroidAssembly ();
		Skip.If (assemblyPath == null, "Mono.Android.dll not found");

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

		output.WriteLine ($"Total types:       {peers.Count}");
		output.WriteLine ($"Interfaces:        {interfaces}");
		output.WriteLine ($"Abstract classes:   {abstracts}");
		output.WriteLine ($"Generic defs:       {generics}");
		output.WriteLine ($"With marshal methods: {withMethods} ({totalMethods} total methods)");
		output.WriteLine ($"With constructors:  {withConstructors}");
		output.WriteLine ($"With base Java:     {withBase}");
		output.WriteLine ($"With interfaces:    {withInterfaces}");

		// Mono.Android.dll should have thousands of types
		Assert.True (peers.Count > 3000, $"Expected >3000 types, got {peers.Count}");
		Assert.True (interfaces > 500, $"Expected >500 interfaces, got {interfaces}");
		Assert.True (totalMethods > 10000, $"Expected >10000 marshal methods, got {totalMethods}");
	}

	public void Dispose ()
	{
	}

	record LegacyEntry (string JavaName, string ManagedName, bool SkipInJavaToManaged, string AssemblyName);
	record NewEntry (string JavaName, string ManagedName, bool SkipInJavaToManaged, string AssemblyName);
}
