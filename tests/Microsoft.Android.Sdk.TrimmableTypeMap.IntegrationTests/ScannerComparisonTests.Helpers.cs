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
		Assert.Fail ($"{label} ({items.Count}){Environment.NewLine}{details}");
	}

	[Fact]
	public void Scanner_NonPeerAssembly_ProducesEmptyResults ()
	{
		// Scan an assembly with no Java peers (the test assembly itself has no [Register] types)
		var testAssemblyPath = typeof (ScannerComparisonTests).Assembly.Location;
		using var scanner = new JavaPeerScanner ();
		var peers = scanner.Scan (new [] { testAssemblyPath });

		// The test assembly has no Java peer types — scan should succeed with empty results
		Assert.Empty (peers);
	}

	[Fact]
	public void ImplementorTypes_HaveCorrectMonoPrefix ()
	{
		using var scanner = new JavaPeerScanner ();
		var peers = scanner.Scan (AllAssemblyPaths);

		// Find implementor types — these follow the pattern *_*Implementor
		var implementors = peers.Where (p =>
			p.ManagedTypeName.Contains ("Implementor", StringComparison.Ordinal) &&
			!p.IsInterface).ToList ();

		Assert.True (implementors.Count > 10,
			$"Expected >10 implementor types in Mono.Android, got {implementors.Count}");

		// Verify all implementors have the mono/ package prefix in their JNI name
		var missingPrefix = implementors
			.Where (p => !p.JavaName.StartsWith ("mono/", StringComparison.Ordinal))
			.Select (p => $"{p.ManagedTypeName} → {p.JavaName}")
			.ToList ();

		AssertNoDiffs ("IMPLEMENTORS MISSING mono/ PREFIX", missingPrefix);
	}

	[Fact]
	public void ExactComponentAttributes_MonoAndroid ()
	{
		var legacyData = TypeDataBuilder.BuildLegacyComponentData (MonoAndroidAssemblyPath);
		var newData = TypeDataBuilder.BuildNewComponentData (AllAssemblyPaths);

		// Mono.Android is a binding assembly — most types have DoNotGenerateAcw=true
		// and no component attributes, so counts may be low. Just verify parity.
		var (missing, extra, kindMismatches, nameMismatches, propertyMismatches) = ComparisonDiffHelper.CompareComponentAttributes (legacyData, newData);

		AssertNoDiffs ("COMPONENTS MISSING from new scanner", missing);
		AssertNoDiffs ("COMPONENTS EXTRA in new scanner", extra);
		AssertNoDiffs ("COMPONENT KIND MISMATCHES", kindMismatches);
		AssertNoDiffs ("COMPONENT NAME MISMATCHES", nameMismatches);
		AssertNoDiffs ("COMPONENT PROPERTY MISMATCHES", propertyMismatches);
	}

	[Fact]
	public void ExactComponentAttributes_UserTypesFixture ()
	{
		var paths = AllUserTypesAssemblyPaths;
		Assert.NotNull (paths);

		var legacyData = TypeDataBuilder.BuildLegacyComponentData (paths! [0]);
		var newData = TypeDataBuilder.BuildNewComponentData (paths);
		var (missing, extra, kindMismatches, nameMismatches, propertyMismatches) = ComparisonDiffHelper.CompareComponentAttributes (legacyData, newData);

		AssertNoDiffs ("COMPONENTS MISSING from new scanner", missing);
		AssertNoDiffs ("COMPONENTS EXTRA in new scanner", extra);
		AssertNoDiffs ("COMPONENT KIND MISMATCHES", kindMismatches);
		AssertNoDiffs ("COMPONENT NAME MISMATCHES", nameMismatches);
		AssertNoDiffs ("COMPONENT PROPERTY MISMATCHES", propertyMismatches);
	}

	[Fact]
	public void ExactAssemblyManifestAttributes_MonoAndroid ()
	{
		var legacy = TypeDataBuilder.BuildLegacyManifestData (MonoAndroidAssemblyPath);
		var newData = TypeDataBuilder.BuildNewManifestData (AllAssemblyPaths);

		var (missingPerms, extraPerms, missingFeats, extraFeats) =
			ComparisonDiffHelper.CompareAssemblyManifestAttributes (legacy, newData);

		AssertNoDiffs ("USES-PERMISSION MISSING from new scanner", missingPerms);
		AssertNoDiffs ("USES-PERMISSION EXTRA in new scanner", extraPerms);
		AssertNoDiffs ("USES-FEATURE MISSING from new scanner", missingFeats);
		AssertNoDiffs ("USES-FEATURE EXTRA in new scanner", extraFeats);
	}

	[Fact]
	public void ExactJavaFields_UserTypesFixture ()
	{
		var paths = AllUserTypesAssemblyPaths;
		Assert.NotNull (paths);

		var primaryAssemblyName = Path.GetFileNameWithoutExtension (paths! [0]);
		using var scanner = new JavaPeerScanner ();
		var peers = scanner.Scan (paths);

		var peersWithFields = peers
			.Where (p => p.AssemblyName == primaryAssemblyName && p.JavaFields.Count > 0)
			.ToList ();

		// The FieldExporter fixture type should have at least one field
		Assert.True (peersWithFields.Count > 0,
			"Expected at least one user type with JavaFields (FieldExporter)");

		foreach (var peer in peersWithFields) {
			foreach (var field in peer.JavaFields) {
				// Every field should have required properties populated
				Assert.False (string.IsNullOrEmpty (field.FieldName),
					$"{peer.ManagedTypeName}: JavaField has empty FieldName");
				Assert.False (string.IsNullOrEmpty (field.JavaTypeName),
					$"{peer.ManagedTypeName}: JavaField '{field.FieldName}' has empty JavaTypeName");
				Assert.False (string.IsNullOrEmpty (field.InitializerMethodName),
					$"{peer.ManagedTypeName}: JavaField '{field.FieldName}' has empty InitializerMethodName");
				Assert.False (string.IsNullOrEmpty (field.Visibility),
					$"{peer.ManagedTypeName}: JavaField '{field.FieldName}' has empty Visibility");
			}
		}
	}

	[Fact]
	public void ExportMethod_UserTypesFixture_IsDiscovered ()
	{
		var paths = AllUserTypesAssemblyPaths;
		Assert.NotNull (paths);

		var primaryAssemblyName = Path.GetFileNameWithoutExtension (paths! [0]);
		using var scanner = new JavaPeerScanner ();
		var peers = scanner.Scan (paths);

		var exportPeer = peers.FirstOrDefault (p =>
			p.AssemblyName == primaryAssemblyName &&
			p.ManagedTypeName.Contains ("ExportWithThrows", StringComparison.Ordinal));
		Assert.NotNull (exportPeer);

		var exportMethod = exportPeer.MarshalMethods.FirstOrDefault (m => m.IsExport && m.JniName == "riskyOperation");
		Assert.NotNull (exportMethod);
		Assert.Equal ("()V", exportMethod.JniSignature);
	}

	[Fact]
	public void ExactCompatJniNames_MonoAndroid ()
	{
		var (legacyData, _) = TypeDataBuilder.BuildLegacy (MonoAndroidAssemblyPath);
		var newData = TypeDataBuilder.BuildNew (AllAssemblyPaths);

		Assert.True (legacyData.Count > 8000, $"Expected >8000 legacy type data entries, got {legacyData.Count}");
		Assert.True (newData.Count > 8000, $"Expected >8000 new type data entries, got {newData.Count}");

		var mismatches = ComparisonDiffHelper.CompareCompatJniNames (legacyData, newData);

		AssertNoDiffs ("COMPAT JNI NAME MISMATCHES", mismatches);
	}

	[Fact]
	public void ExactNativeCallbackNames_MonoAndroid ()
	{
		var (_, legacyMethods) = ScannerRunner.RunLegacy (MonoAndroidAssemblyPath);
		var (_, newMethods) = ScannerRunner.RunNew (AllAssemblyPaths);

		Assert.True (legacyMethods.Count > 500, $"Expected >500 legacy method groups, got {legacyMethods.Count}");
		Assert.True (newMethods.Count > 500, $"Expected >500 new method groups, got {newMethods.Count}");

		var mismatches = MarshalMethodDiffHelper.CompareNativeCallbackNames (legacyMethods, newMethods);

		AssertNoDiffs ("NATIVE CALLBACK NAME MISMATCHES", mismatches);
	}

	[Fact]
	public void ExactDeclaringTypes_MonoAndroid ()
	{
		var (_, legacyMethods) = ScannerRunner.RunLegacy (MonoAndroidAssemblyPath);
		var (_, newMethods) = ScannerRunner.RunNew (AllAssemblyPaths);

		Assert.True (legacyMethods.Count > 500, $"Expected >500 legacy method groups, got {legacyMethods.Count}");
		Assert.True (newMethods.Count > 500, $"Expected >500 new method groups, got {newMethods.Count}");

		var mismatches = MarshalMethodDiffHelper.CompareDeclaringTypes (legacyMethods, newMethods);

		AssertNoDiffs ("DECLARING TYPE MISMATCHES", mismatches);
	}

	[Fact]
	public void ExactConstructorSuperArgs_UserTypesFixture ()
	{
		var paths = AllUserTypesAssemblyPaths;
		Assert.NotNull (paths);

		var legacyData = TypeDataBuilder.BuildLegacyConstructorSuperArgs (paths! [0]);
		var newData = TypeDataBuilder.BuildNewConstructorSuperArgs (paths);

		var mismatches = ComparisonDiffHelper.CompareConstructorSuperArgs (legacyData, newData);

		AssertNoDiffs ("CONSTRUCTOR SUPER ARGS MISMATCHES", mismatches);
	}

	[Fact]
	public void ExactInvokerTypes_MonoAndroid ()
	{
		var (legacyData, _) = TypeDataBuilder.BuildLegacy (MonoAndroidAssemblyPath);
		var newData = TypeDataBuilder.BuildNew (AllAssemblyPaths);

		Assert.True (legacyData.Count > 8000, $"Expected >8000 legacy type data entries, got {legacyData.Count}");
		Assert.True (newData.Count > 8000, $"Expected >8000 new type data entries, got {newData.Count}");

		var mismatches = ComparisonDiffHelper.CompareInvokerTypes (legacyData, newData);

		AssertNoDiffs ("INVOKER TYPE MISMATCHES", mismatches);
	}

	[Fact]
	public void ExactCannotRegisterInStaticCtor_MonoAndroid ()
	{
		var (legacyData, _) = TypeDataBuilder.BuildLegacy (MonoAndroidAssemblyPath);
		var newData = TypeDataBuilder.BuildNew (AllAssemblyPaths);

		Assert.True (legacyData.Count > 8000, $"Expected >8000 legacy type data entries, got {legacyData.Count}");
		Assert.True (newData.Count > 8000, $"Expected >8000 new type data entries, got {newData.Count}");

		var mismatches = ComparisonDiffHelper.CompareCannotRegisterInStaticCtor (legacyData, newData);

		AssertNoDiffs ("CANNOT REGISTER IN STATIC CTOR MISMATCHES", mismatches);
	}
}
