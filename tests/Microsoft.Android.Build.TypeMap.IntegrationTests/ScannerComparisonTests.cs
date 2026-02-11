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

		// Look in standard locations for a built Mono.Android.dll
		var candidates = new [] {
			// Same repo after full build
			Path.Combine (thisDir, "..", "..", "..", "..", "..", "bin", "Debug", "lib", "packs", "Microsoft.Android.Ref.36", "36.1.99", "ref", "net11.0", "Mono.Android.dll"),
			Path.Combine (thisDir, "..", "..", "..", "..", "..", "bin", "Debug", "lib", "packs", "Microsoft.Android.Ref.36.1", "36.1.99", "ref", "net11.0", "Mono.Android.dll"),
			// Sibling android repo
			Path.Combine (thisDir, "..", "..", "..", "..", "..", "..", "android", "bin", "Debug", "lib", "packs", "Microsoft.Android.Ref.36", "36.1.99", "ref", "net11.0", "Mono.Android.dll"),
			Path.Combine (thisDir, "..", "..", "..", "..", "..", "..", "android", "bin", "Debug", "lib", "packs", "Microsoft.Android.Ref.36.1", "36.1.99", "ref", "net11.0", "Mono.Android.dll"),
		};

		foreach (var candidate in candidates) {
			var resolved = Path.GetFullPath (candidate);
			if (File.Exists (resolved)) {
				return resolved;
			}
		}

		return null;
	}

	public void Dispose ()
	{
	}

	record LegacyEntry (string JavaName, string ManagedName, bool SkipInJavaToManaged, string AssemblyName);
	record NewEntry (string JavaName, string ManagedName, bool SkipInJavaToManaged, string AssemblyName);
}
