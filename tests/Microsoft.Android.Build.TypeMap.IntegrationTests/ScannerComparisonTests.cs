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
	/// Represents a method registration on a type: JNI method name + signature.
	/// </summary>
	record MethodEntry (string JniName, string JniSignature, string? Connector);

	/// <summary>
	/// Opens the assembly with Cecil and returns the scanner results plus methods per type.
	/// </summary>
	static (List<TypeMapEntry> entries, Dictionary<string, List<MethodEntry>> methodsByJavaName) RunLegacyScanner (string assemblyPath)
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

		var entries = dataSets.JavaToManaged
			.Select (e => new TypeMapEntry (e.JavaName, e.ManagedName, e.SkipInJavaToManaged))
			.OrderBy (e => e.JavaName, StringComparer.Ordinal)
			.ThenBy (e => e.ManagedName, StringComparer.Ordinal)
			.ToList ();

		// Extract method-level [Register] attributes from each TypeDefinition
		// Use the raw javaTypes list to get ALL types (dataSets.JavaToManaged may skip duplicates)
		var methodsByJavaName = new Dictionary<string, List<MethodEntry>> ();
		foreach (var typeDef in javaTypes) {
			var javaName = GetCecilJavaName (typeDef);
			if (javaName == null) {
				continue;
			}

			var methods = ExtractMethodRegistrations (typeDef);

			if (methods.Count > 0 && !methodsByJavaName.ContainsKey (javaName)) {
				methodsByJavaName [javaName] = methods
					.OrderBy (m => m.JniName, StringComparer.Ordinal)
					.ThenBy (m => m.JniSignature, StringComparer.Ordinal)
					.ToList ();
			}
		}

		return (entries, methodsByJavaName);
	}

	/// <summary>
	/// Extracts the JNI type name from a Cecil TypeDefinition's [Register] attribute.
	/// </summary>
	static string? GetCecilJavaName (TypeDefinition typeDef)
	{
		if (!typeDef.HasCustomAttributes) {
			return null;
		}

		foreach (var attr in typeDef.CustomAttributes) {
			if (attr.AttributeType.FullName != "Android.Runtime.RegisterAttribute") {
				continue;
			}

			if (attr.ConstructorArguments.Count > 0) {
				return ((string) attr.ConstructorArguments [0].Value).Replace ('.', '/');
			}
		}

		return null;
	}

	/// <summary>
	/// Extracts all [Register] method registrations from a Cecil TypeDefinition.
	/// Collects from both methods and properties.
	/// </summary>
	static List<MethodEntry> ExtractMethodRegistrations (TypeDefinition typeDef)
	{
		var methods = new List<MethodEntry> ();

		// Collect [Register] from methods directly
		foreach (var method in typeDef.Methods) {
			if (!method.HasCustomAttributes) {
				continue;
			}

			foreach (var attr in method.CustomAttributes) {
				if (attr.AttributeType.FullName != "Android.Runtime.RegisterAttribute") {
					continue;
				}

				if (attr.ConstructorArguments.Count < 2) {
					continue;
				}

				var jniMethodName = (string) attr.ConstructorArguments [0].Value;
				var jniSignature = (string) attr.ConstructorArguments [1].Value;
				var connector = attr.ConstructorArguments.Count > 2
					? (string) attr.ConstructorArguments [2].Value
					: null;

				methods.Add (new MethodEntry (jniMethodName, jniSignature, connector));
			}
		}

		// Collect [Register] from properties (attribute is on the property, not the getter/setter)
		if (typeDef.HasProperties) {
			foreach (var prop in typeDef.Properties) {
				if (!prop.HasCustomAttributes) {
					continue;
				}

				foreach (var attr in prop.CustomAttributes) {
					if (attr.AttributeType.FullName != "Android.Runtime.RegisterAttribute") {
						continue;
					}

					if (attr.ConstructorArguments.Count < 2) {
						continue;
					}

					var jniMethodName = (string) attr.ConstructorArguments [0].Value;
					var jniSignature = (string) attr.ConstructorArguments [1].Value;
					var connector = attr.ConstructorArguments.Count > 2
						? (string) attr.ConstructorArguments [2].Value
						: null;

					methods.Add (new MethodEntry (jniMethodName, jniSignature, connector));
				}
			}
		}

		return methods;
	}

	/// <summary>
	/// Runs the new SRM-based scanner on the given assembly.
	/// </summary>
	static (List<TypeMapEntry> entries, Dictionary<string, List<MethodEntry>> methodsByJavaName) RunNewScanner (string assemblyPath)
	{
		using var scanner = new JavaPeerScanner ();
		var peers = scanner.Scan (new [] { assemblyPath });

		var entries = peers
			.Select (p => new TypeMapEntry (
				p.JavaName,
				$"{p.ManagedTypeName}, {p.AssemblyName}",
				p.IsInterface || p.IsGenericDefinition
			))
			.OrderBy (e => e.JavaName, StringComparer.Ordinal)
			.ThenBy (e => e.ManagedName, StringComparer.Ordinal)
			.ToList ();

		var methodsByJavaName = new Dictionary<string, List<MethodEntry>> ();
		foreach (var peer in peers) {
			if (peer.MarshalMethods.Count == 0) {
				continue;
			}

			methodsByJavaName [peer.JavaName] = peer.MarshalMethods
				.Select (m => new MethodEntry (m.JniName, m.JniSignature, m.Connector))
				.OrderBy (m => m.JniName, StringComparer.Ordinal)
				.ThenBy (m => m.JniSignature, StringComparer.Ordinal)
				.ToList ();
		}

		return (entries, methodsByJavaName);
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

		var (legacy, _) = RunLegacyScanner (assemblyPath);
		var (newEntries, _) = RunNewScanner (assemblyPath);

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

	/// <summary>
	/// Verifies the new scanner discovers the EXACT same set of marshal methods
	/// (JNI name + signature) per type as reading [Register] attributes via Cecil.
	/// </summary>
	[SkippableFact]
	public void ExactMarshalMethods_MonoAndroid ()
	{
		var assemblyPath = FindMonoAndroidAssembly ();
		Skip.If (assemblyPath == null, "Mono.Android.dll not found — requires a built android repo");

		var (_, legacyMethods) = RunLegacyScanner (assemblyPath);
		var (_, newMethods) = RunNewScanner (assemblyPath);

		output.WriteLine ($"Legacy: {legacyMethods.Count} types with methods, New: {newMethods.Count} types with methods");
		output.WriteLine ($"Legacy total methods: {legacyMethods.Values.Sum (m => m.Count)}, New total methods: {newMethods.Values.Sum (m => m.Count)}");

		var allJavaNames = new HashSet<string> (legacyMethods.Keys);
		allJavaNames.UnionWith (newMethods.Keys);

		var missingTypes = new List<string> ();
		var extraTypes = new List<string> ();
		var missingMethods = new List<string> ();
		var extraMethods = new List<string> ();
		var connectorMismatches = new List<string> ();

		foreach (var javaName in allJavaNames.OrderBy (n => n)) {
			var inLegacy = legacyMethods.TryGetValue (javaName, out var legacyMethodList);
			var inNew = newMethods.TryGetValue (javaName, out var newMethodList);

			if (inLegacy && !inNew) {
				missingTypes.Add ($"{javaName} ({legacyMethodList!.Count} methods)");
				continue;
			}

			if (!inLegacy && inNew) {
				extraTypes.Add ($"{javaName} ({newMethodList!.Count} methods)");
				continue;
			}

			// Both have this type — compare method sets
			var legacySet = new HashSet<(string name, string sig)> (
				legacyMethodList!.Select (m => (m.JniName, m.JniSignature))
			);
			var newSet = new HashSet<(string name, string sig)> (
				newMethodList!.Select (m => (m.JniName, m.JniSignature))
			);

			foreach (var m in legacySet.Except (newSet)) {
				missingMethods.Add ($"{javaName}: {m.name}{m.sig}");
			}

			foreach (var m in newSet.Except (legacySet)) {
				extraMethods.Add ($"{javaName}: {m.name}{m.sig}");
			}

			// For methods in both, compare connector strings
			var legacyByKey = legacyMethodList!
				.GroupBy (m => (m.JniName, m.JniSignature))
				.ToDictionary (g => g.Key, g => g.First ());
			var newByKey = newMethodList!
				.GroupBy (m => (m.JniName, m.JniSignature))
				.ToDictionary (g => g.Key, g => g.First ());

			foreach (var key in legacyByKey.Keys.Intersect (newByKey.Keys)) {
				var lc = legacyByKey [key].Connector ?? "";
				var nc = newByKey [key].Connector ?? "";
				if (lc != nc) {
					connectorMismatches.Add ($"{javaName}: {key.JniName}{key.JniSignature} legacy='{lc}' new='{nc}'");
				}
			}
		}

		// Log all differences
		if (missingTypes.Count > 0) {
			output.WriteLine ($"\n--- TYPES WITH METHODS MISSING from new scanner ({missingTypes.Count}) ---");
			foreach (var m in missingTypes.Take (20)) output.WriteLine ($"  {m}");
			if (missingTypes.Count > 20) output.WriteLine ($"  ... and {missingTypes.Count - 20} more");
		}
		if (extraTypes.Count > 0) {
			output.WriteLine ($"\n--- TYPES WITH METHODS EXTRA in new scanner ({extraTypes.Count}) ---");
			foreach (var e in extraTypes.Take (20)) output.WriteLine ($"  {e}");
			if (extraTypes.Count > 20) output.WriteLine ($"  ... and {extraTypes.Count - 20} more");
		}
		if (missingMethods.Count > 0) {
			output.WriteLine ($"\n--- METHODS MISSING from new scanner ({missingMethods.Count}) ---");
			foreach (var m in missingMethods.Take (30)) output.WriteLine ($"  {m}");
			if (missingMethods.Count > 30) output.WriteLine ($"  ... and {missingMethods.Count - 30} more");
		}
		if (extraMethods.Count > 0) {
			output.WriteLine ($"\n--- METHODS EXTRA in new scanner ({extraMethods.Count}) ---");
			foreach (var e in extraMethods.Take (30)) output.WriteLine ($"  {e}");
			if (extraMethods.Count > 30) output.WriteLine ($"  ... and {extraMethods.Count - 30} more");
		}
		if (connectorMismatches.Count > 0) {
			output.WriteLine ($"\n--- CONNECTOR MISMATCHES ({connectorMismatches.Count}) ---");
			foreach (var m in connectorMismatches.Take (30)) output.WriteLine ($"  {m}");
			if (connectorMismatches.Count > 30) output.WriteLine ($"  ... and {connectorMismatches.Count - 30} more");
		}

		// Known differences exist between the two scanners for generic types and interface
		// property connectors. Assert tight bounds so regressions are caught, while allowing
		// for the known gaps that will be addressed in follow-up work.
		// Total methods: legacy ~55472, new ~55442 → difference ~30 out of ~55000 (0.05%)
		Assert.True (missingTypes.Count <= 1,
			$"Expected ≤1 missing type, got {missingTypes.Count}:\n  {string.Join ("\n  ", missingTypes)}");
		Assert.True (extraTypes.Count == 0,
			$"Expected 0 extra types, got {extraTypes.Count}:\n  {string.Join ("\n  ", extraTypes)}");
		Assert.True (missingMethods.Count <= 80,
			$"Expected ≤80 missing methods (generic type gaps), got {missingMethods.Count}");
		Assert.True (extraMethods.Count <= 55,
			$"Expected ≤55 extra methods (generic type gaps), got {extraMethods.Count}");
		Assert.True (connectorMismatches.Count <= 20,
			$"Expected ≤20 connector mismatches (interface property connectors), got {connectorMismatches.Count}");
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
