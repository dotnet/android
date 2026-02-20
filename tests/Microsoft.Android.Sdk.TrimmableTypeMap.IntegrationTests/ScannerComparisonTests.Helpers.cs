using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Microsoft.Android.Sdk.TrimmableTypeMap.IntegrationTests;

public partial class ScannerComparisonTests
{
	static string MonoAndroidAssemblyPath {
		get {
			_ = nameof (Java.Lang.Object);

			var testDir = Path.GetDirectoryName (typeof (ScannerComparisonTests).Assembly.Location)
				?? throw new InvalidOperationException ("Could not determine test assembly directory.");
			var path = Path.Combine (testDir, "Mono.Android.dll");

			if (!File.Exists (path)) {
				throw new InvalidOperationException (
					$"Mono.Android.dll not found at '{path}'. " +
					"Ensure Mono.Android is built (bin/Debug/lib/packs/Microsoft.Android.Ref.*).");
			}

			return path;
		}
	}

	static string[] AllAssemblyPaths {
		get {
			var monoAndroidPath = MonoAndroidAssemblyPath;
			var dir = Path.GetDirectoryName (monoAndroidPath)
				?? throw new InvalidOperationException ("Could not determine Mono.Android directory.");
			var javaInteropPath = Path.Combine (dir, "Java.Interop.dll");

			if (!File.Exists (javaInteropPath)) {
				return new [] { monoAndroidPath };
			}

			return new [] { monoAndroidPath, javaInteropPath };
		}
	}

	static string? UserTypesFixturePath {
		get {
			var testDir = Path.GetDirectoryName (typeof (ScannerComparisonTests).Assembly.Location)
				?? throw new InvalidOperationException ("Could not determine test assembly directory.");
			var path = Path.Combine (testDir, "UserTypesFixture.dll");
			return File.Exists (path) ? path : null;
		}
	}

	static string[]? AllUserTypesAssemblyPaths {
		get {
			var fixturePath = UserTypesFixturePath;
			if (fixturePath == null) {
				return null;
			}

			var dir = Path.GetDirectoryName (fixturePath)!;
			var monoAndroidPath = Path.Combine (dir, "Mono.Android.dll");
			var javaInteropPath = Path.Combine (dir, "Java.Interop.dll");

			var paths = new List<string> { fixturePath };
			if (File.Exists (monoAndroidPath)) {
				paths.Add (monoAndroidPath);
			}
			if (File.Exists (javaInteropPath)) {
				paths.Add (javaInteropPath);
			}
			return paths.ToArray ();
		}
	}

	static string NormalizeCrc64 (string javaName)
	{
		if (javaName.StartsWith ("crc64", StringComparison.Ordinal)) {
			int slash = javaName.IndexOf ('/');
			if (slash > 0) {
				return "crc64.../" + javaName.Substring (slash + 1);
			}
		}
		return javaName;
	}

	void AssertTypeMapMatch (List<TypeMapEntry> legacy, List<TypeMapEntry> newEntries)
	{
		var legacyMap = legacy.GroupBy (e => e.JavaName).ToDictionary (g => g.Key, g => g.ToList ());
		var newMap = newEntries.GroupBy (e => e.JavaName).ToDictionary (g => g.Key, g => g.ToList ());

		var allJavaNames = new HashSet<string> (legacyMap.Keys);
		allJavaNames.UnionWith (newMap.Keys);

		var missing = new List<string> ();
		var extra = new List<string> ();
		var managedNameMismatches = new List<string> ();
		var skipMismatches = new List<string> ();

		foreach (var javaName in allJavaNames.OrderBy (n => n, StringComparer.Ordinal)) {
			var inLegacy = legacyMap.TryGetValue (javaName, out var legacyEntries);
			var inNew = newMap.TryGetValue (javaName, out var newEntriesForName);

			if (inLegacy && !inNew) {
				foreach (var e in legacyEntries!)
					missing.Add ($"{e.JavaName} → {e.ManagedName} (skip={e.SkipInJavaToManaged})");
				continue;
			}

			if (!inLegacy && inNew) {
				foreach (var e in newEntriesForName!)
					extra.Add ($"{e.JavaName} → {e.ManagedName} (skip={e.SkipInJavaToManaged})");
				continue;
			}

			var le = legacyEntries!.OrderBy (e => e.ManagedName).First ();
			var ne = newEntriesForName!.OrderBy (e => e.ManagedName).First ();

			if (le.ManagedName != ne.ManagedName)
				managedNameMismatches.Add ($"{javaName}: legacy='{le.ManagedName}' new='{ne.ManagedName}'");

			if (le.SkipInJavaToManaged != ne.SkipInJavaToManaged)
				skipMismatches.Add ($"{javaName}: legacy.skip={le.SkipInJavaToManaged} new.skip={ne.SkipInJavaToManaged}");
		}

		AssertNoDiffs ("MISSING", missing);
		AssertNoDiffs ("EXTRA", extra);
		AssertNoDiffs ("MANAGED NAME MISMATCHES", managedNameMismatches);
		AssertNoDiffs ("SKIP FLAG MISMATCHES", skipMismatches);
	}

	static void AssertNoDiffs (string label, List<string> items)
	{
		if (items.Count == 0) {
			return;
		}

		var details = string.Join (Environment.NewLine, items.Take (20).Select (item => $"  {item}"));
		Assert.True (false, $"{label} ({items.Count}){Environment.NewLine}{details}");
	}
}
