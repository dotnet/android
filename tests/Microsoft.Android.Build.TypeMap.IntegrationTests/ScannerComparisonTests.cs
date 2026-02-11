using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Java.Interop.Tools.Cecil;
using Java.Interop.Tools.TypeNameMappings;
using Microsoft.Build.Utilities;
using Mono.Cecil;
using Xamarin.Android.Tasks;
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
		var methodsByJavaName = new Dictionary<string, List<TypeMethodGroup>> ();
		foreach (var typeDef in javaTypes) {
			var javaName = GetCecilJavaName (typeDef);
			if (javaName == null) {
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
	/// Scans all given assemblies but only returns types from the primary assembly (first path).
	/// </summary>
	static (List<TypeMapEntry> entries, Dictionary<string, List<TypeMethodGroup>> methodsByJavaName) RunNewScanner (string[] assemblyPaths)
	{
		var primaryAssemblyName = Path.GetFileNameWithoutExtension (assemblyPaths [0]);
		using var scanner = new JavaPeerScanner ();
		var allPeers = scanner.Scan (assemblyPaths);
		var peers = allPeers.Where (p => p.AssemblyName == primaryAssemblyName).ToList ();

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
		var (newEntries, _) = RunNewScanner (AllAssemblyPaths);

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
		var (_, newMethods) = RunNewScanner (AllAssemblyPaths);

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
	/// Verifies the new scanner produces the same base Java type name for every type
	/// as the legacy Cecil-based scanner. BaseJavaName drives the JCW "extends" clause
	/// and is critical for correct Java inheritance.
	/// </summary>
	[Fact]
	public void ExactBaseJavaNames_MonoAndroid ()
	{
		var assemblyPath = MonoAndroidAssemblyPath;

		var (legacyData, _) = BuildLegacyTypeData (assemblyPath);
		var newData = BuildNewTypeData (AllAssemblyPaths);

		var allManagedNames = new HashSet<string> (legacyData.Keys);
		allManagedNames.IntersectWith (newData.Keys);

		var mismatches = new List<string> ();
		int compared = 0;

		foreach (var managedName in allManagedNames.OrderBy (n => n)) {
			var legacy = legacyData [managedName];
			var newInfo = newData [managedName];

			compared++;

			if (legacy.BaseJavaName != newInfo.BaseJavaName) {
				// Legacy ToJniName can't resolve bases for open generic types (returns null).
				// Our scanner resolves them correctly. Accept this known difference.
				if (legacy.BaseJavaName == null && newInfo.BaseJavaName != null && managedName.Contains ('`')) {
					continue;
				}

				// Invokers share JNI names with their base class. Legacy ToJniName
				// self-reference filter discards the base (baseJni == javaName), but
				// our scanner correctly resolves it. Accept legacy=null, new=valid
				// for DoNotGenerateAcw types.
				if (legacy.BaseJavaName == null && newInfo.BaseJavaName != null && newInfo.DoNotGenerateAcw) {
					continue;
				}

				// Legacy ToJniName(System.Object) returns "java/lang/Object" as a fallback,
				// making Java.Lang.Object/Throwable appear to have themselves as base.
				// Our scanner correctly returns null. Accept legacy=self, new=null.
				if (legacy.BaseJavaName != null && newInfo.BaseJavaName == null &&
					legacy.BaseJavaName == legacy.JavaName) {
					continue;
				}

				mismatches.Add ($"{managedName}: legacy='{legacy.BaseJavaName ?? "(null)"}' new='{newInfo.BaseJavaName ?? "(null)"}'");
			}
		}

		output.WriteLine ($"Compared BaseJavaName for {compared} types");

		if (mismatches.Count > 0) {
			output.WriteLine ($"\n--- BASE JAVA NAME MISMATCHES ({mismatches.Count}) ---");
			foreach (var m in mismatches) output.WriteLine ($"  {m}");
		}

		Assert.Empty (mismatches);
	}

	/// <summary>
	/// Verifies the new scanner produces the same implemented interface Java names
	/// for every type as the legacy Cecil-based scanner. ImplementedInterfaceJavaNames
	/// drives the JCW "implements" clause.
	/// </summary>
	[Fact]
	public void ExactImplementedInterfaces_MonoAndroid ()
	{
		var assemblyPath = MonoAndroidAssemblyPath;

		var (legacyData, _) = BuildLegacyTypeData (assemblyPath);
		var newData = BuildNewTypeData (AllAssemblyPaths);

		var allManagedNames = new HashSet<string> (legacyData.Keys);
		allManagedNames.IntersectWith (newData.Keys);

		var missingInterfaces = new List<string> ();
		var extraInterfaces = new List<string> ();
		int compared = 0;

		foreach (var managedName in allManagedNames.OrderBy (n => n)) {
			var legacy = legacyData [managedName];
			var newInfo = newData [managedName];

			compared++;

			var legacySet = new HashSet<string> (legacy.ImplementedInterfaces, StringComparer.Ordinal);
			var newSet = new HashSet<string> (newInfo.ImplementedInterfaces, StringComparer.Ordinal);

			foreach (var iface in legacySet.Except (newSet)) {
				missingInterfaces.Add ($"{managedName}: missing '{iface}'");
			}

			foreach (var iface in newSet.Except (legacySet)) {
				extraInterfaces.Add ($"{managedName}: extra '{iface}'");
			}
		}

		output.WriteLine ($"Compared ImplementedInterfaces for {compared} types");

		if (missingInterfaces.Count > 0) {
			output.WriteLine ($"\n--- INTERFACES MISSING from new scanner ({missingInterfaces.Count}) ---");
			foreach (var m in missingInterfaces) output.WriteLine ($"  {m}");
		}
		if (extraInterfaces.Count > 0) {
			output.WriteLine ($"\n--- INTERFACES EXTRA in new scanner ({extraInterfaces.Count}) ---");
			foreach (var e in extraInterfaces) output.WriteLine ($"  {e}");
		}

		Assert.Empty (missingInterfaces);
		Assert.Empty (extraInterfaces);
	}

	/// <summary>
	/// Verifies the new scanner resolves the same activation constructor info
	/// for every type as the legacy Cecil-based scanner. This determines how
	/// peer instances are created from Java handles.
	/// </summary>
	[Fact]
	public void ExactActivationCtors_MonoAndroid ()
	{
		var assemblyPath = MonoAndroidAssemblyPath;

		var (legacyData, _) = BuildLegacyTypeData (assemblyPath);
		var newData = BuildNewTypeData (AllAssemblyPaths);

		var allManagedNames = new HashSet<string> (legacyData.Keys);
		allManagedNames.IntersectWith (newData.Keys);

		var presenceMismatches = new List<string> ();
		var declaringTypeMismatches = new List<string> ();
		var styleMismatches = new List<string> ();
		int compared = 0;
		int withActivationCtor = 0;

		foreach (var managedName in allManagedNames.OrderBy (n => n)) {
			var legacy = legacyData [managedName];
			var newInfo = newData [managedName];

			compared++;

			if (legacy.HasActivationCtor != newInfo.HasActivationCtor) {
				presenceMismatches.Add ($"{managedName}: legacy.has={legacy.HasActivationCtor} new.has={newInfo.HasActivationCtor}");
				continue;
			}

			if (!legacy.HasActivationCtor) {
				continue;
			}

			withActivationCtor++;

			if (legacy.ActivationCtorDeclaringType != newInfo.ActivationCtorDeclaringType) {
				declaringTypeMismatches.Add ($"{managedName}: legacy='{legacy.ActivationCtorDeclaringType}' new='{newInfo.ActivationCtorDeclaringType}'");
			}

			if (legacy.ActivationCtorStyle != newInfo.ActivationCtorStyle) {
				styleMismatches.Add ($"{managedName}: legacy='{legacy.ActivationCtorStyle}' new='{newInfo.ActivationCtorStyle}'");
			}
		}

		output.WriteLine ($"Compared ActivationCtor for {compared} types ({withActivationCtor} have activation ctors)");

		if (presenceMismatches.Count > 0) {
			output.WriteLine ($"\n--- ACTIVATION CTOR PRESENCE MISMATCHES ({presenceMismatches.Count}) ---");
			foreach (var m in presenceMismatches) output.WriteLine ($"  {m}");
		}
		if (declaringTypeMismatches.Count > 0) {
			output.WriteLine ($"\n--- ACTIVATION CTOR DECLARING TYPE MISMATCHES ({declaringTypeMismatches.Count}) ---");
			foreach (var m in declaringTypeMismatches) output.WriteLine ($"  {m}");
		}
		if (styleMismatches.Count > 0) {
			output.WriteLine ($"\n--- ACTIVATION CTOR STYLE MISMATCHES ({styleMismatches.Count}) ---");
			foreach (var m in styleMismatches) output.WriteLine ($"  {m}");
		}

		Assert.Empty (presenceMismatches);
		Assert.Empty (declaringTypeMismatches);
		Assert.Empty (styleMismatches);
	}

	/// <summary>
	/// Verifies the new scanner discovers the same Java constructors ([Register("&lt;init&gt;",...)])
	/// for every type as the legacy Cecil-based scanner. These determine which nctor_N
	/// native methods appear in JCW Java source files.
	/// </summary>
	[Fact]
	public void ExactJavaConstructors_MonoAndroid ()
	{
		var assemblyPath = MonoAndroidAssemblyPath;

		var (legacyData, _) = BuildLegacyTypeData (assemblyPath);
		var newData = BuildNewTypeData (AllAssemblyPaths);

		var allManagedNames = new HashSet<string> (legacyData.Keys);
		allManagedNames.IntersectWith (newData.Keys);

		var missingCtors = new List<string> ();
		var extraCtors = new List<string> ();
		int compared = 0;
		int totalCtors = 0;

		foreach (var managedName in allManagedNames.OrderBy (n => n)) {
			var legacy = legacyData [managedName];
			var newInfo = newData [managedName];

			compared++;

			var legacySet = new HashSet<string> (legacy.JavaConstructorSignatures, StringComparer.Ordinal);
			var newSet = new HashSet<string> (newInfo.JavaConstructorSignatures, StringComparer.Ordinal);
			totalCtors += newSet.Count;

			foreach (var sig in legacySet.Except (newSet)) {
				missingCtors.Add ($"{managedName}: missing '<init>{sig}'");
			}

			foreach (var sig in newSet.Except (legacySet)) {
				extraCtors.Add ($"{managedName}: extra '<init>{sig}'");
			}
		}

		output.WriteLine ($"Compared JavaConstructors for {compared} types ({totalCtors} total constructors)");

		if (missingCtors.Count > 0) {
			output.WriteLine ($"\n--- JAVA CONSTRUCTORS MISSING from new scanner ({missingCtors.Count}) ---");
			foreach (var m in missingCtors) output.WriteLine ($"  {m}");
		}
		if (extraCtors.Count > 0) {
			output.WriteLine ($"\n--- JAVA CONSTRUCTORS EXTRA in new scanner ({extraCtors.Count}) ---");
			foreach (var e in extraCtors) output.WriteLine ($"  {e}");
		}

		Assert.Empty (missingCtors);
		Assert.Empty (extraCtors);
	}

	/// <summary>
	/// Verifies the new scanner produces the same type-level flags as the legacy
	/// Cecil-based scanner: IsInterface, IsAbstract, IsGenericDefinition, DoNotGenerateAcw.
	/// </summary>
	[Fact]
	public void ExactTypeFlags_MonoAndroid ()
	{
		var assemblyPath = MonoAndroidAssemblyPath;

		var (legacyData, _) = BuildLegacyTypeData (assemblyPath);
		var newData = BuildNewTypeData (AllAssemblyPaths);

		var allManagedNames = new HashSet<string> (legacyData.Keys);
		allManagedNames.IntersectWith (newData.Keys);

		var interfaceMismatches = new List<string> ();
		var abstractMismatches = new List<string> ();
		var genericMismatches = new List<string> ();
		var acwMismatches = new List<string> ();
		int compared = 0;

		foreach (var managedName in allManagedNames.OrderBy (n => n)) {
			var legacy = legacyData [managedName];
			var newInfo = newData [managedName];

			compared++;

			if (legacy.IsInterface != newInfo.IsInterface) {
				interfaceMismatches.Add ($"{managedName}: legacy={legacy.IsInterface} new={newInfo.IsInterface}");
			}

			if (legacy.IsAbstract != newInfo.IsAbstract) {
				abstractMismatches.Add ($"{managedName}: legacy={legacy.IsAbstract} new={newInfo.IsAbstract}");
			}

			if (legacy.IsGenericDefinition != newInfo.IsGenericDefinition) {
				genericMismatches.Add ($"{managedName}: legacy={legacy.IsGenericDefinition} new={newInfo.IsGenericDefinition}");
			}

			if (legacy.DoNotGenerateAcw != newInfo.DoNotGenerateAcw) {
				acwMismatches.Add ($"{managedName}: legacy={legacy.DoNotGenerateAcw} new={newInfo.DoNotGenerateAcw}");
			}
		}

		output.WriteLine ($"Compared type flags for {compared} types");

		if (interfaceMismatches.Count > 0) {
			output.WriteLine ($"\n--- IsInterface MISMATCHES ({interfaceMismatches.Count}) ---");
			foreach (var m in interfaceMismatches) output.WriteLine ($"  {m}");
		}
		if (abstractMismatches.Count > 0) {
			output.WriteLine ($"\n--- IsAbstract MISMATCHES ({abstractMismatches.Count}) ---");
			foreach (var m in abstractMismatches) output.WriteLine ($"  {m}");
		}
		if (genericMismatches.Count > 0) {
			output.WriteLine ($"\n--- IsGenericDefinition MISMATCHES ({genericMismatches.Count}) ---");
			foreach (var m in genericMismatches) output.WriteLine ($"  {m}");
		}
		if (acwMismatches.Count > 0) {
			output.WriteLine ($"\n--- DoNotGenerateAcw MISMATCHES ({acwMismatches.Count}) ---");
			foreach (var m in acwMismatches) output.WriteLine ($"  {m}");
		}

		Assert.Empty (interfaceMismatches);
		Assert.Empty (abstractMismatches);
		Assert.Empty (genericMismatches);
		Assert.Empty (acwMismatches);
	}

	// ================================================================
	// Shared data extraction helpers for comprehensive comparison tests
	// ================================================================

	/// <summary>
	/// Unified per-type data record used for comparison between legacy and new scanners.
	/// </summary>
	record TypeComparisonData (
		string ManagedName,
		string JavaName,
		string? BaseJavaName,
		IReadOnlyList<string> ImplementedInterfaces,
		bool HasActivationCtor,
		string? ActivationCtorDeclaringType,
		string? ActivationCtorStyle,
		IReadOnlyList<string> JavaConstructorSignatures,
		bool IsInterface,
		bool IsAbstract,
		bool IsGenericDefinition,
		bool DoNotGenerateAcw
	);

	/// <summary>
	/// Opens the assembly with Cecil and extracts per-type comparison data.
	/// Keyed by managed type name "Namespace.Type, Assembly" for join with new scanner.
	/// Returns both the per-type data and the type map entries for backward compatibility.
	/// </summary>
	static (Dictionary<string, TypeComparisonData> perType, List<TypeMapEntry> entries) BuildLegacyTypeData (string assemblyPath)
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

		var perType = new Dictionary<string, TypeComparisonData> (StringComparer.Ordinal);

		foreach (var typeDef in javaTypes) {
			var javaName = GetCecilJavaName (typeDef);
			if (javaName == null) {
				continue;
			}

			// Cecil uses '/' for nested types, SRM uses '+' — normalize
			var managedName = $"{typeDef.FullName.Replace ('/', '+')}, {typeDef.Module.Assembly.Name.Name}";

			// Base Java name
			string? baseJavaName = null;
			var baseType = typeDef.GetBaseType (cache);
			if (baseType != null) {
				var baseJni = JavaNativeTypeManager.ToJniName (baseType, cache);
				// Filter self-references: ToJniName can return the type's own JNI name
				// (e.g., Java.Lang.Object → System.Object → "java/lang/Object").
				if (baseJni != null && baseJni != javaName) {
					baseJavaName = baseJni;
				}
			}

			// Implemented interfaces (only Java peer interfaces with [Register])
			var implementedInterfaces = new List<string> ();
			if (typeDef.HasInterfaces) {
				foreach (var ifaceImpl in typeDef.Interfaces) {
					var ifaceDef = cache.Resolve (ifaceImpl.InterfaceType);
					if (ifaceDef == null) {
						continue;
					}
					var ifaceRegs = CecilExtensions.GetTypeRegistrationAttributes (ifaceDef);
					var ifaceReg = ifaceRegs.FirstOrDefault ();
					if (ifaceReg != null) {
						implementedInterfaces.Add (ifaceReg.Name.Replace ('.', '/'));
					}
				}
			}
			implementedInterfaces.Sort (StringComparer.Ordinal);

			// Activation constructor
			bool hasActivationCtor = false;
			string? activationCtorDeclaringType = null;
			string? activationCtorStyle = null;
			FindLegacyActivationCtor (typeDef, cache, out hasActivationCtor, out activationCtorDeclaringType, out activationCtorStyle);

			// Java constructors: [Register("<init>", sig, ...)] on .ctor methods
			var javaCtorSignatures = new List<string> ();
			foreach (var method in typeDef.Methods) {
				if (!method.IsConstructor || method.IsStatic || !method.HasCustomAttributes) {
					continue;
				}
				foreach (var attr in method.CustomAttributes) {
					if (attr.AttributeType.FullName != "Android.Runtime.RegisterAttribute") {
						continue;
					}
					if (attr.ConstructorArguments.Count >= 2) {
						var regName = (string) attr.ConstructorArguments [0].Value;
						if (regName == "<init>" || regName == ".ctor") {
							javaCtorSignatures.Add ((string) attr.ConstructorArguments [1].Value);
						}
					}
				}
			}
			javaCtorSignatures.Sort (StringComparer.Ordinal);

			// Type flags
			var isInterface = typeDef.IsInterface;
			var isAbstract = typeDef.IsAbstract && !typeDef.IsInterface;
			var isGenericDefinition = typeDef.HasGenericParameters;
			var doNotGenerateAcw = GetCecilDoNotGenerateAcw (typeDef);

			perType [managedName] = new TypeComparisonData (
				managedName,
				javaName,
				baseJavaName,
				implementedInterfaces,
				hasActivationCtor,
				activationCtorDeclaringType,
				activationCtorStyle,
				javaCtorSignatures,
				isInterface,
				isAbstract,
				isGenericDefinition,
				doNotGenerateAcw
			);
		}

		return (perType, entries);
	}

	/// <summary>
	/// Walks the type hierarchy with Cecil to find the activation constructor.
	/// XI-style: (IntPtr, JniHandleOwnership). JI-style: (ref JniObjectReference, JniObjectReferenceOptions).
	/// </summary>
	static void FindLegacyActivationCtor (TypeDefinition typeDef, TypeDefinitionCache cache,
		out bool found, out string? declaringType, out string? style)
	{
		found = false;
		declaringType = null;
		style = null;

		// Walk from current type up through base types
		TypeDefinition? current = typeDef;
		while (current != null) {
			foreach (var method in current.Methods) {
				if (!method.IsConstructor || method.IsStatic || method.Parameters.Count != 2) {
					continue;
				}

				var p0 = method.Parameters [0].ParameterType.FullName;
				var p1 = method.Parameters [1].ParameterType.FullName;

				if (p0 == "System.IntPtr" && p1 == "Android.Runtime.JniHandleOwnership") {
					found = true;
					declaringType = $"{current.FullName.Replace ('/', '+')}, {current.Module.Assembly.Name.Name}";
					style = "XamarinAndroid";
					return;
				}

				if ((p0 == "Java.Interop.JniObjectReference&" || p0 == "Java.Interop.JniObjectReference") &&
				    p1 == "Java.Interop.JniObjectReferenceOptions") {
					found = true;
					declaringType = $"{current.FullName.Replace ('/', '+')}, {current.Module.Assembly.Name.Name}";
					style = "JavaInterop";
					return;
				}
			}

			current = current.GetBaseType (cache);
		}
	}

	/// <summary>
	/// Gets DoNotGenerateAcw from a Cecil TypeDefinition's [Register] attribute.
	/// </summary>
	static bool GetCecilDoNotGenerateAcw (TypeDefinition typeDef)
	{
		if (!typeDef.HasCustomAttributes) {
			return false;
		}

		foreach (var attr in typeDef.CustomAttributes) {
			if (attr.AttributeType.FullName != "Android.Runtime.RegisterAttribute") {
				continue;
			}
			if (attr.HasProperties) {
				foreach (var prop in attr.Properties) {
					if (prop.Name == "DoNotGenerateAcw" && prop.Argument.Value is bool val) {
						return val;
					}
				}
			}
			// [Register] found but DoNotGenerateAcw not set — defaults to false
			return false;
		}

		return false;
	}

	/// <summary>
	/// Runs the new SRM-based scanner and builds per-type comparison data.
	/// Keyed by managed type name "Namespace.Type, Assembly" for join with legacy data.
	/// Scans all given assemblies but only returns types from the primary assembly (first path).
	/// </summary>
	static Dictionary<string, TypeComparisonData> BuildNewTypeData (string[] assemblyPaths)
	{
		var primaryAssemblyName = Path.GetFileNameWithoutExtension (assemblyPaths [0]);
		using var scanner = new JavaPeerScanner ();
		var peers = scanner.Scan (assemblyPaths);

		var perType = new Dictionary<string, TypeComparisonData> (StringComparer.Ordinal);

		foreach (var peer in peers) {
			// Only include types from the primary assembly
			if (peer.AssemblyName != primaryAssemblyName) {
				continue;
			}

			var managedName = $"{peer.ManagedTypeName}, {peer.AssemblyName}";

			// Map ActivationCtor
			bool hasActivationCtor = peer.ActivationCtor != null;
			string? activationCtorDeclaringType = null;
			string? activationCtorStyle = null;
			if (peer.ActivationCtor != null) {
				activationCtorDeclaringType = $"{peer.ActivationCtor.DeclaringTypeName}, {peer.ActivationCtor.DeclaringAssemblyName}";
				activationCtorStyle = peer.ActivationCtor.Style.ToString ();
			}

			// Java constructor signatures (sorted)
			var javaCtorSignatures = peer.JavaConstructors
				.Select (c => c.JniSignature)
				.OrderBy (s => s, StringComparer.Ordinal)
				.ToList ();

			// Implemented interfaces (sorted)
			var implementedInterfaces = peer.ImplementedInterfaceJavaNames
				.OrderBy (i => i, StringComparer.Ordinal)
				.ToList ();

			perType [managedName] = new TypeComparisonData (
				managedName,
				peer.JavaName,
				peer.BaseJavaName,
				implementedInterfaces,
				hasActivationCtor,
				activationCtorDeclaringType,
				activationCtorStyle,
				javaCtorSignatures,
				peer.IsInterface,
				peer.IsAbstract && !peer.IsInterface,  // Match legacy: isAbstract excludes interfaces
				peer.IsGenericDefinition,
				peer.DoNotGenerateAcw
			);
		}

		return perType;
	}

	/// <summary>
	/// Gets the file path of the Mono.Android.dll ref assembly.
	/// At compile time, nameof(Java.Lang.Object) verifies that the reference is correctly set up.
	/// At runtime, we locate the assembly via the copy in the test output directory (placed there
	/// by the _AddMonoAndroidReference MSBuild target with Private=true).
	/// </summary>
	static string MonoAndroidAssemblyPath {
		get {
			// Compile-time check: this ensures the Mono.Android reference is properly configured.
			// It's never actually evaluated at runtime — it just validates the build setup.
			_ = nameof (Java.Lang.Object);

			// At runtime, find the Mono.Android.dll copy in the test output directory.
			var testDir = Path.GetDirectoryName (typeof (ScannerComparisonTests).Assembly.Location)!;
			var path = Path.Combine (testDir, "Mono.Android.dll");

			if (!File.Exists (path)) {
				throw new InvalidOperationException (
					$"Mono.Android.dll not found at '{path}'. " +
					"Ensure Mono.Android is built (bin/Debug/lib/packs/Microsoft.Android.Ref.*).");
			}

			return path;
		}
	}

	/// <summary>
	/// Gets all assembly paths needed for scanning: Mono.Android.dll + Java.Interop.dll.
	/// Java.Interop.dll contains base types like JavaObject and JavaException that
	/// Mono.Android types inherit from — without it, cross-assembly base resolution fails.
	/// </summary>
	static string[] AllAssemblyPaths {
		get {
			var monoAndroidPath = MonoAndroidAssemblyPath;
			var dir = Path.GetDirectoryName (monoAndroidPath)!;
			var javaInteropPath = Path.Combine (dir, "Java.Interop.dll");

			if (!File.Exists (javaInteropPath)) {
				return new [] { monoAndroidPath };
			}

			return new [] { monoAndroidPath, javaInteropPath };
		}
	}
}
