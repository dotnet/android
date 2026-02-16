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

namespace Microsoft.Android.Sdk.TrimmableTypeMap.IntegrationTests;

public class ScannerComparisonTests
{
	readonly ITestOutputHelper output;

	public ScannerComparisonTests (ITestOutputHelper output)
	{
		this.output = output;
	}

	record TypeMapEntry (string JavaName, string ManagedName, bool SkipInJavaToManaged);

	record MethodEntry (string JniName, string JniSignature, string? Connector);

	record TypeMethodGroup (string ManagedName, List<MethodEntry> Methods);

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

	[Fact]
	public void ExactTypeMap_MonoAndroid ()
	{
		var (legacy, _) = RunLegacyScanner (MonoAndroidAssemblyPath);
		var (newEntries, _) = RunNewScanner (AllAssemblyPaths);
		output.WriteLine ($"Legacy: {legacy.Count} entries, New: {newEntries.Count} entries");
		AssertTypeMapMatch (legacy, newEntries);
	}

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

		foreach (var javaName in allJavaNames.OrderBy (n => n, StringComparer.Ordinal)) {
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

		LogDiffs ("MANAGED TYPES MISSING from new scanner", missingTypes);
		LogDiffs ("MANAGED TYPES EXTRA in new scanner", extraTypes);
		LogDiffs ("METHODS MISSING from new scanner", missingMethods);
		LogDiffs ("METHODS EXTRA in new scanner", extraMethods);
		LogDiffs ("CONNECTOR MISMATCHES", connectorMismatches);

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
		var withConstructors = peers.Count (p => p.MarshalMethods.Any (m => m.IsConstructor));
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

		foreach (var managedName in allManagedNames.OrderBy (n => n, StringComparer.Ordinal)) {
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

		LogDiffs ("BASE JAVA NAME MISMATCHES", mismatches);

		Assert.Empty (mismatches);
	}

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

		foreach (var managedName in allManagedNames.OrderBy (n => n, StringComparer.Ordinal)) {
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

		LogDiffs ("INTERFACES MISSING from new scanner", missingInterfaces);
		LogDiffs ("INTERFACES EXTRA in new scanner", extraInterfaces);

		Assert.Empty (missingInterfaces);
		Assert.Empty (extraInterfaces);
	}

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

		foreach (var managedName in allManagedNames.OrderBy (n => n, StringComparer.Ordinal)) {
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

		LogDiffs ("ACTIVATION CTOR PRESENCE MISMATCHES", presenceMismatches);
		LogDiffs ("ACTIVATION CTOR DECLARING TYPE MISMATCHES", declaringTypeMismatches);
		LogDiffs ("ACTIVATION CTOR STYLE MISMATCHES", styleMismatches);

		Assert.Empty (presenceMismatches);
		Assert.Empty (declaringTypeMismatches);
		Assert.Empty (styleMismatches);
	}

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

		foreach (var managedName in allManagedNames.OrderBy (n => n, StringComparer.Ordinal)) {
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

		LogDiffs ("JAVA CONSTRUCTORS MISSING from new scanner", missingCtors);
		LogDiffs ("JAVA CONSTRUCTORS EXTRA in new scanner", extraCtors);

		Assert.Empty (missingCtors);
		Assert.Empty (extraCtors);
	}

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

		foreach (var managedName in allManagedNames.OrderBy (n => n, StringComparer.Ordinal)) {
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

		LogDiffs ("IsInterface MISMATCHES", interfaceMismatches);
		LogDiffs ("IsAbstract MISMATCHES", abstractMismatches);
		LogDiffs ("IsGenericDefinition MISMATCHES", genericMismatches);
		LogDiffs ("DoNotGenerateAcw MISMATCHES", acwMismatches);

		Assert.Empty (interfaceMismatches);
		Assert.Empty (abstractMismatches);
		Assert.Empty (genericMismatches);
		Assert.Empty (acwMismatches);
	}


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
				foreach (var prop in attr.Properties.Where (p => p.Name == "DoNotGenerateAcw")) {
					if (prop.Argument.Value is bool val) {
						return val;
					}
				}
			}
			// [Register] found but DoNotGenerateAcw not set — defaults to false
			return false;
		}

		return false;
	}

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

			// Java constructor signatures (sorted) — derived from constructor marshal methods
			var javaCtorSignatures = peer.MarshalMethods
				.Where (m => m.IsConstructor)
				.Select (m => m.JniSignature)
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

	static string MonoAndroidAssemblyPath {
		get {
			// Compile-time check: this ensures the Mono.Android reference is properly configured.
			// It's never actually evaluated at runtime — it just validates the build setup.
			_ = nameof (Java.Lang.Object);

			// At runtime, find the Mono.Android.dll copy in the test output directory.
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

	[Fact]
	public void ExactTypeMap_UserTypesFixture ()
	{
		var paths = AllUserTypesAssemblyPaths;
		Assert.NotNull (paths);

		var fixturePath = paths! [0];
		var (legacy, _) = RunLegacyScanner (fixturePath);
		var (newEntries, _) = RunNewScanner (paths);

		output.WriteLine ($"UserTypesFixture: Legacy={legacy.Count} entries, New={newEntries.Count} entries");

		// Normalize CRC64 hashes — the two scanners use different polynomials
		var legacyNormalized = legacy.Select (e => e with { JavaName = NormalizeCrc64 (e.JavaName) }).ToList ();
		var newNormalized = newEntries.Select (e => e with { JavaName = NormalizeCrc64 (e.JavaName) }).ToList ();

		AssertTypeMapMatch (legacyNormalized, newNormalized);
	}

	[Fact]
	public void ExactMarshalMethods_UserTypesFixture ()
	{
		var paths = AllUserTypesAssemblyPaths;
		Assert.NotNull (paths);

		var fixturePath = paths! [0];
		var (_, legacyMethods) = RunLegacyScanner (fixturePath);
		var (_, newMethods) = RunNewScanner (paths);

		// Normalize CRC64 hashes in method group keys
		var legacyNormalized = legacyMethods
			.ToDictionary (kvp => NormalizeCrc64 (kvp.Key), kvp => kvp.Value);
		var newNormalized = newMethods
			.ToDictionary (kvp => NormalizeCrc64 (kvp.Key), kvp => kvp.Value);

		output.WriteLine ($"UserTypesFixture: Legacy={legacyNormalized.Count} types with methods, New={newNormalized.Count}");

		// Only compare types that the legacy scanner found (it skips user types without [Register])
		var missing = new List<string> ();
		var methodMismatches = new List<string> ();

		foreach (var javaName in legacyNormalized.Keys.OrderBy (n => n, StringComparer.Ordinal)) {
			if (!newNormalized.TryGetValue (javaName, out var newGroups)) {
				missing.Add (javaName);
				continue;
			}

			var legacyGroups = legacyNormalized [javaName];

			foreach (var legacyGroup in legacyGroups) {
				var newGroup = newGroups.FirstOrDefault (g => g.ManagedName == legacyGroup.ManagedName);
				if (newGroup == null) {
					missing.Add ($"{javaName} → {legacyGroup.ManagedName}");
					continue;
				}

				// Legacy test helper only extracts [Register] methods, not [Export] methods.
				// When legacy has 0 methods (from the typemap fallback path) but new has some,
				// the new scanner is correct — it handles [Export] too. Skip comparison.
				if (legacyGroup.Methods.Count == 0) {
					continue;
				}

				if (legacyGroup.Methods.Count != newGroup.Methods.Count) {
					methodMismatches.Add ($"{javaName}/{legacyGroup.ManagedName}: legacy={legacyGroup.Methods.Count} methods, new={newGroup.Methods.Count}");
					continue;
				}

				for (int i = 0; i < legacyGroup.Methods.Count; i++) {
					var lm = legacyGroup.Methods [i];
					var nm = newGroup.Methods [i];
					if (lm.JniName != nm.JniName || lm.JniSignature != nm.JniSignature) {
						methodMismatches.Add ($"{javaName}: [{i}] legacy=({lm.JniName}, {lm.JniSignature}) new=({nm.JniName}, {nm.JniSignature})");
					}
				}
			}
		}

		LogDiffs ("MISSING from new scanner", missing);
		LogDiffs ("METHOD MISMATCHES", methodMismatches);

		Assert.Empty (missing);
		Assert.Empty (methodMismatches);
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

		LogDiffs ("MISSING", missing);
		LogDiffs ("EXTRA", extra);
		LogDiffs ("MANAGED NAME MISMATCHES", managedNameMismatches);
		LogDiffs ("SKIP FLAG MISMATCHES", skipMismatches);

		Assert.Empty (missing);
		Assert.Empty (extra);
		Assert.Empty (managedNameMismatches);
		Assert.Empty (skipMismatches);
	}

	void LogDiffs (string label, List<string> items)
	{
		if (items.Count == 0) return;
		output.WriteLine ($"\n--- {label} ({items.Count}) ---");
		foreach (var item in items) output.WriteLine ($"  {item}");
	}
}
