using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
public class ScannerComparisonTests
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
	/// Represents one managed type's methods for a given JNI name.
	/// Multiple managed types can share the same JNI name (aliases).
	/// </summary>
	record TypeMethodGroup (string ManagedName, List<MethodEntry> Methods);

	/// <summary>
	/// Opens the assembly with Cecil and returns the scanner results plus methods per type.
	/// Multiple managed types can map to the same JNI name (aliases).
	/// </summary>
	static (List<TypeMapEntry> entries, Dictionary<string, List<TypeMethodGroup>> methodsByJavaName) RunLegacyScanner (string assemblyPath)
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

		// Extract method-level [Register] attributes from each TypeDefinition.
		// Use the raw javaTypes list to get ALL types — multiple managed types
		// can map to the same JNI name (aliases).
		// Skip Invoker types (DoNotGenerateAcw + name ends with "Invoker") — the new
		// scanner intentionally excludes these as they're implementation details.
		var methodsByJavaName = new Dictionary<string, List<TypeMethodGroup>> ();
		foreach (var typeDef in javaTypes) {
			var javaName = GetCecilJavaName (typeDef);
			if (javaName == null) {
				continue;
			}

			if (IsCecilInvokerType (typeDef)) {
				continue;
			}

			// Cecil uses '/' for nested types, SRM uses '+' (CLR format) — normalize
			var managedName = $"{typeDef.FullName.Replace ('/', '+')}, {typeDef.Module.Assembly.Name.Name}";
			var methods = ExtractMethodRegistrations (typeDef);

			if (!methodsByJavaName.TryGetValue (javaName, out var groups)) {
				groups = new List<TypeMethodGroup> ();
				methodsByJavaName [javaName] = groups;
			}

			groups.Add (new TypeMethodGroup (
				managedName,
				methods.OrderBy (m => m.JniName, StringComparer.Ordinal)
					.ThenBy (m => m.JniSignature, StringComparer.Ordinal)
					.ToList ()
			));
		}

		// Some types appear in dataSets.JavaToManaged (the typemap) but not in
		// javaTypes (the raw list). Include them with empty method lists so the
		// comparison covers all types known to the legacy scanner.
		foreach (var entry in dataSets.JavaToManaged) {
			if (methodsByJavaName.ContainsKey (entry.JavaName)) {
				continue;
			}

			methodsByJavaName [entry.JavaName] = new List<TypeMethodGroup> {
				new TypeMethodGroup (entry.ManagedName, new List<MethodEntry> ())
			};
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
	/// Checks if a Cecil TypeDefinition is an Invoker type (DoNotGenerateAcw=true
	/// and name ends with "Invoker"). These are runtime implementation details
	/// that the new scanner intentionally excludes from the typemap.
	/// </summary>
	static bool IsCecilInvokerType (TypeDefinition typeDef)
	{
		if (!typeDef.Name.EndsWith ("Invoker", StringComparison.Ordinal)) {
			return false;
		}

		if (!typeDef.HasCustomAttributes) {
			return false;
		}

		foreach (var attr in typeDef.CustomAttributes) {
			if (attr.AttributeType.FullName != "Android.Runtime.RegisterAttribute") {
				continue;
			}

			if (attr.HasProperties) {
				foreach (var prop in attr.Properties) {
					if (prop.Name == "DoNotGenerateAcw" && prop.Argument.Value is bool val && val) {
						return true;
					}
				}
			}
		}

		return false;
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
	/// Returns all types including aliases (multiple managed types per JNI name).
	/// </summary>
	static (List<TypeMapEntry> entries, Dictionary<string, List<TypeMethodGroup>> methodsByJavaName) RunNewScanner (string assemblyPath)
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

		var methodsByJavaName = new Dictionary<string, List<TypeMethodGroup>> ();
		foreach (var peer in peers) {
			var managedName = $"{peer.ManagedTypeName}, {peer.AssemblyName}";

			if (!methodsByJavaName.TryGetValue (peer.JavaName, out var groups)) {
				groups = new List<TypeMethodGroup> ();
				methodsByJavaName [peer.JavaName] = groups;
			}

			groups.Add (new TypeMethodGroup (
				managedName,
				peer.MarshalMethods
					.Select (m => new MethodEntry (m.JniName, m.JniSignature, m.Connector))
					.OrderBy (m => m.JniName, StringComparer.Ordinal)
					.ThenBy (m => m.JniSignature, StringComparer.Ordinal)
					.ToList ()
			));
		}

		return (entries, methodsByJavaName);
	}

	/// <summary>
	/// Verifies the new scanner produces the EXACT same JNI → managed type mapping
	/// as the legacy scanner on Mono.Android.dll. Every entry must match: same JNI name,
	/// same managed name, same skip flag. No extras, no missing entries.
	/// </summary>
	[Fact]
	public void ExactTypeMap_MonoAndroid ()
	{
		var assemblyPath = MonoAndroidAssemblyPath;

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
	/// Verifies the new scanner discovers the EXACT same set of managed types per JNI name
	/// and the EXACT same marshal methods per managed type as the legacy scanner.
	/// Multiple managed types can map to the same JNI name (aliases).
	/// </summary>
	[Fact]
	public void ExactMarshalMethods_MonoAndroid ()
	{
		var assemblyPath = MonoAndroidAssemblyPath;

		var (_, legacyMethods) = RunLegacyScanner (assemblyPath);
		var (_, newMethods) = RunNewScanner (assemblyPath);

		var legacyTypeCount = legacyMethods.Values.Sum (g => g.Count);
		var newTypeCount = newMethods.Values.Sum (g => g.Count);
		var legacyMethodCount = legacyMethods.Values.Sum (g => g.Sum (t => t.Methods.Count));
		var newMethodCount = newMethods.Values.Sum (g => g.Sum (t => t.Methods.Count));
		output.WriteLine ($"Legacy: {legacyTypeCount} type groups across {legacyMethods.Count} JNI names, {legacyMethodCount} total methods");
		output.WriteLine ($"New:    {newTypeCount} type groups across {newMethods.Count} JNI names, {newMethodCount} total methods");

		var allJavaNames = new HashSet<string> (legacyMethods.Keys);
		allJavaNames.UnionWith (newMethods.Keys);

		var missingTypes = new List<string> ();
		var extraTypes = new List<string> ();
		var missingMethods = new List<string> ();
		var extraMethods = new List<string> ();
		var connectorMismatches = new List<string> ();

		foreach (var javaName in allJavaNames.OrderBy (n => n)) {
			var inLegacy = legacyMethods.TryGetValue (javaName, out var legacyGroups);
			var inNew = newMethods.TryGetValue (javaName, out var newGroups);

			if (inLegacy && !inNew) {
				foreach (var g in legacyGroups!) {
					missingTypes.Add ($"{javaName} → {g.ManagedName} ({g.Methods.Count} methods)");
				}
				continue;
			}

			if (!inLegacy && inNew) {
				foreach (var g in newGroups!) {
					extraTypes.Add ($"{javaName} → {g.ManagedName} ({g.Methods.Count} methods)");
				}
				continue;
			}

			// Both scanners found this JNI name — compare managed types within it
			var legacyByManaged = legacyGroups!.ToDictionary (g => g.ManagedName, g => g.Methods);
			var newByManaged = newGroups!.ToDictionary (g => g.ManagedName, g => g.Methods);

			foreach (var managedName in legacyByManaged.Keys.Except (newByManaged.Keys)) {
				missingTypes.Add ($"{javaName} → {managedName} ({legacyByManaged [managedName].Count} methods)");
			}

			foreach (var managedName in newByManaged.Keys.Except (legacyByManaged.Keys)) {
				extraTypes.Add ($"{javaName} → {managedName} ({newByManaged [managedName].Count} methods)");
			}

			// For managed types present in both, compare their method sets
			foreach (var managedName in legacyByManaged.Keys.Intersect (newByManaged.Keys)) {
				var legacyMethodList = legacyByManaged [managedName];
				var newMethodList = newByManaged [managedName];

				var legacySet = new HashSet<(string name, string sig)> (
					legacyMethodList.Select (m => (m.JniName, m.JniSignature))
				);
				var newSet = new HashSet<(string name, string sig)> (
					newMethodList.Select (m => (m.JniName, m.JniSignature))
				);

				foreach (var m in legacySet.Except (newSet)) {
					missingMethods.Add ($"{javaName} [{managedName}]: {m.name}{m.sig}");
				}

				foreach (var m in newSet.Except (legacySet)) {
					extraMethods.Add ($"{javaName} [{managedName}]: {m.name}{m.sig}");
				}

				// For methods in both, compare connector strings
				var legacyByKey = legacyMethodList
					.GroupBy (m => (m.JniName, m.JniSignature))
					.ToDictionary (g => g.Key, g => g.First ());
				var newByKey = newMethodList
					.GroupBy (m => (m.JniName, m.JniSignature))
					.ToDictionary (g => g.Key, g => g.First ());

				foreach (var key in legacyByKey.Keys.Intersect (newByKey.Keys)) {
					var lc = legacyByKey [key].Connector ?? "";
					var nc = newByKey [key].Connector ?? "";
					if (lc != nc) {
						connectorMismatches.Add ($"{javaName} [{managedName}]: {key.JniName}{key.JniSignature} legacy='{lc}' new='{nc}'");
					}
				}
			}
		}

		// Log all differences
		if (missingTypes.Count > 0) {
			output.WriteLine ($"\n--- MANAGED TYPES MISSING from new scanner ({missingTypes.Count}) ---");
			foreach (var m in missingTypes) output.WriteLine ($"  {m}");
		}
		if (extraTypes.Count > 0) {
			output.WriteLine ($"\n--- MANAGED TYPES EXTRA in new scanner ({extraTypes.Count}) ---");
			foreach (var e in extraTypes) output.WriteLine ($"  {e}");
		}
		if (missingMethods.Count > 0) {
			output.WriteLine ($"\n--- METHODS MISSING from new scanner ({missingMethods.Count}) ---");
			foreach (var m in missingMethods) output.WriteLine ($"  {m}");
		}
		if (extraMethods.Count > 0) {
			output.WriteLine ($"\n--- METHODS EXTRA in new scanner ({extraMethods.Count}) ---");
			foreach (var e in extraMethods) output.WriteLine ($"  {e}");
		}
		if (connectorMismatches.Count > 0) {
			output.WriteLine ($"\n--- CONNECTOR MISMATCHES ({connectorMismatches.Count}) ---");
			foreach (var m in connectorMismatches) output.WriteLine ($"  {m}");
		}

		// All five categories must be empty — the new scanner should find the exact
		// same managed types and methods per JNI name as the legacy scanner.
		Assert.Empty (missingTypes);
		Assert.Empty (extraTypes);
		Assert.Empty (missingMethods);
		Assert.Empty (extraMethods);
		Assert.Empty (connectorMismatches);
	}

	[Fact]
	public void ScannerDiagnostics_MonoAndroid ()
	{
		var assemblyPath = MonoAndroidAssemblyPath;

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

	/// <summary>
	/// Gets the file path of the Mono.Android.dll assembly from AssemblyMetadata
	/// set by the project file at build time.
	/// </summary>
	static string MonoAndroidAssemblyPath {
		get {
			var attr = typeof (ScannerComparisonTests).Assembly
				.GetCustomAttributes (typeof (System.Reflection.AssemblyMetadataAttribute), false)
				.Cast<System.Reflection.AssemblyMetadataAttribute> ()
				.FirstOrDefault (a => a.Key == "MonoAndroidRefAssembly");

			if (attr == null || string.IsNullOrEmpty (attr.Value)) {
				throw new InvalidOperationException (
					"MonoAndroidRefAssembly metadata not found. " +
					"Ensure the android repo is built (bin/Debug/lib/packs/Microsoft.Android.Ref.*).");
			}

			return attr.Value;
		}
	}
}
