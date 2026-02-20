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

namespace Microsoft.Android.Sdk.TrimmableTypeMap.IntegrationTests;

public class ScannerComparisonTests
{
	record TypeMapEntry (string JavaName, string ManagedName, bool SkipInJavaToManaged);

	record MethodEntry (string JniName, string JniSignature, string? Connector);

	record TypeMethodGroup (string ManagedName, List<MethodEntry> Methods);

	record MarshalMethodComparisonResult (
		List<string> MissingTypes,
		List<string> ExtraTypes,
		List<string> MissingMethods,
		List<string> ExtraMethods,
		List<string> ConnectorMismatches
	);

	record UserTypesMethodComparisonResult (
		List<string> Missing,
		List<string> MethodMismatches
	);

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

		var methodsByJavaName = new Dictionary<string, List<TypeMethodGroup>> ();
		foreach (var typeDef in javaTypes) {
			var javaName = GetCecilJavaName (typeDef);
			if (javaName == null) {
				continue;
			}

			var managedName = GetManagedName (typeDef);
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

		foreach (var method in typeDef.Methods) {
			if (!method.HasCustomAttributes) {
				continue;
			}

			AddRegisterMethods (method.CustomAttributes, methods);
		}

		if (typeDef.HasProperties) {
			foreach (var prop in typeDef.Properties) {
				if (!prop.HasCustomAttributes) {
					continue;
				}

				AddRegisterMethods (prop.CustomAttributes, methods);
			}
		}

		return methods;
	}

	static string GetManagedName (TypeDefinition typeDef)
	{
		return $"{typeDef.FullName.Replace ('/', '+')}, {typeDef.Module.Assembly.Name.Name}";
	}

	static void AddRegisterMethods (IEnumerable<CustomAttribute> attributes, List<MethodEntry> methods)
	{
		foreach (var attr in attributes) {
			if (attr.AttributeType.FullName != "Android.Runtime.RegisterAttribute" || attr.ConstructorArguments.Count < 2) {
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
		AssertTypeMapMatch (legacy, newEntries);
	}

	[Fact]
	public void ExactMarshalMethods_MonoAndroid ()
	{
		var (_, legacyMethods) = RunLegacyScanner (MonoAndroidAssemblyPath);
		var (_, newMethods) = RunNewScanner (AllAssemblyPaths);
		var result = CompareMarshalMethods (legacyMethods, newMethods);

		AssertNoDiffs ("MANAGED TYPES MISSING from new scanner", result.MissingTypes);
		AssertNoDiffs ("MANAGED TYPES EXTRA in new scanner", result.ExtraTypes);
		AssertNoDiffs ("METHODS MISSING from new scanner", result.MissingMethods);
		AssertNoDiffs ("METHODS EXTRA in new scanner", result.ExtraMethods);
		AssertNoDiffs ("CONNECTOR MISMATCHES", result.ConnectorMismatches);
	}

	static MarshalMethodComparisonResult CompareMarshalMethods (
		Dictionary<string, List<TypeMethodGroup>> legacyMethods,
		Dictionary<string, List<TypeMethodGroup>> newMethods)
	{
		var allJavaNames = new HashSet<string> (legacyMethods.Keys);
		allJavaNames.UnionWith (newMethods.Keys);

		var result = new MarshalMethodComparisonResult (
			new List<string> (),
			new List<string> (),
			new List<string> (),
			new List<string> (),
			new List<string> ()
		);

		foreach (var javaName in allJavaNames.OrderBy (n => n, StringComparer.Ordinal)) {
			var inLegacy = legacyMethods.TryGetValue (javaName, out var legacyGroups);
			var inNew = newMethods.TryGetValue (javaName, out var newGroups);

			if (inLegacy && !inNew) {
				foreach (var g in legacyGroups!) {
					result.MissingTypes.Add ($"{javaName} → {g.ManagedName} ({g.Methods.Count} methods)");
				}
				continue;
			}

			if (!inLegacy && inNew) {
				foreach (var g in newGroups!) {
					result.ExtraTypes.Add ($"{javaName} → {g.ManagedName} ({g.Methods.Count} methods)");
				}
				continue;
			}

			var legacyByManaged = legacyGroups!.ToDictionary (g => g.ManagedName, g => g.Methods);
			var newByManaged = newGroups!.ToDictionary (g => g.ManagedName, g => g.Methods);

			foreach (var managedName in legacyByManaged.Keys.Except (newByManaged.Keys)) {
				result.MissingTypes.Add ($"{javaName} → {managedName} ({legacyByManaged [managedName].Count} methods)");
			}

			foreach (var managedName in newByManaged.Keys.Except (legacyByManaged.Keys)) {
				result.ExtraTypes.Add ($"{javaName} → {managedName} ({newByManaged [managedName].Count} methods)");
			}

			foreach (var managedName in legacyByManaged.Keys.Intersect (newByManaged.Keys)) {
				CompareMethodGroups (javaName, managedName, legacyByManaged [managedName], newByManaged [managedName], result);
			}
		}

		return result;
	}

	static void CompareMethodGroups (
		string javaName,
		string managedName,
		List<MethodEntry> legacyMethodList,
		List<MethodEntry> newMethodList,
		MarshalMethodComparisonResult result)
	{
		var legacySet = new HashSet<(string name, string sig)> (
			legacyMethodList.Select (m => (m.JniName, m.JniSignature))
		);
		var newSet = new HashSet<(string name, string sig)> (
			newMethodList.Select (m => (m.JniName, m.JniSignature))
		);

		foreach (var m in legacySet.Except (newSet)) {
			result.MissingMethods.Add ($"{javaName} [{managedName}]: {m.name}{m.sig}");
		}

		foreach (var m in newSet.Except (legacySet)) {
			result.ExtraMethods.Add ($"{javaName} [{managedName}]: {m.name}{m.sig}");
		}

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
				result.ConnectorMismatches.Add ($"{javaName} [{managedName}]: {key.JniName}{key.JniSignature} legacy='{lc}' new='{nc}'");
			}
		}
	}

	[Fact]
	public void ScannerDiagnostics_MonoAndroid ()
	{
		using var scanner = new JavaPeerScanner ();
		var peers = scanner.Scan (new [] { MonoAndroidAssemblyPath });

		var interfaces = peers.Count (p => p.IsInterface);
		var totalMethods = peers.Sum (p => p.MarshalMethods.Count);
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
		var mismatches = ComparisonDiffHelper.CompareBaseJavaNames (legacyData, newData);

		AssertNoDiffs ("BASE JAVA NAME MISMATCHES", mismatches);
	}

	[Fact]
	public void ExactImplementedInterfaces_MonoAndroid ()
	{
		var assemblyPath = MonoAndroidAssemblyPath;

		var (legacyData, _) = BuildLegacyTypeData (assemblyPath);
		var newData = BuildNewTypeData (AllAssemblyPaths);
		var (missingInterfaces, extraInterfaces) = ComparisonDiffHelper.CompareImplementedInterfaces (legacyData, newData);

		AssertNoDiffs ("INTERFACES MISSING from new scanner", missingInterfaces);
		AssertNoDiffs ("INTERFACES EXTRA in new scanner", extraInterfaces);
	}

	[Fact]
	public void ExactActivationCtors_MonoAndroid ()
	{
		var assemblyPath = MonoAndroidAssemblyPath;

		var (legacyData, _) = BuildLegacyTypeData (assemblyPath);
		var newData = BuildNewTypeData (AllAssemblyPaths);
		var (presenceMismatches, declaringTypeMismatches, styleMismatches) = ComparisonDiffHelper.CompareActivationCtors (legacyData, newData);

		AssertNoDiffs ("ACTIVATION CTOR PRESENCE MISMATCHES", presenceMismatches);
		AssertNoDiffs ("ACTIVATION CTOR DECLARING TYPE MISMATCHES", declaringTypeMismatches);
		AssertNoDiffs ("ACTIVATION CTOR STYLE MISMATCHES", styleMismatches);
	}

	[Fact]
	public void ExactJavaConstructors_MonoAndroid ()
	{
		var assemblyPath = MonoAndroidAssemblyPath;

		var (legacyData, _) = BuildLegacyTypeData (assemblyPath);
		var newData = BuildNewTypeData (AllAssemblyPaths);
		var (missingCtors, extraCtors) = ComparisonDiffHelper.CompareJavaConstructors (legacyData, newData);

		AssertNoDiffs ("JAVA CONSTRUCTORS MISSING from new scanner", missingCtors);
		AssertNoDiffs ("JAVA CONSTRUCTORS EXTRA in new scanner", extraCtors);
	}

	[Fact]
	public void ExactTypeFlags_MonoAndroid ()
	{
		var assemblyPath = MonoAndroidAssemblyPath;

		var (legacyData, _) = BuildLegacyTypeData (assemblyPath);
		var newData = BuildNewTypeData (AllAssemblyPaths);
		var (interfaceMismatches, abstractMismatches, genericMismatches, acwMismatches) = ComparisonDiffHelper.CompareTypeFlags (legacyData, newData);

		AssertNoDiffs ("IsInterface MISMATCHES", interfaceMismatches);
		AssertNoDiffs ("IsAbstract MISMATCHES", abstractMismatches);
		AssertNoDiffs ("IsGenericDefinition MISMATCHES", genericMismatches);
		AssertNoDiffs ("DoNotGenerateAcw MISMATCHES", acwMismatches);
	}

	static class ComparisonDiffHelper
	{
		public static List<string> CompareBaseJavaNames (
			Dictionary<string, TypeComparisonData> legacyData,
			Dictionary<string, TypeComparisonData> newData)
		{
			var allManagedNames = new HashSet<string> (legacyData.Keys);
			allManagedNames.IntersectWith (newData.Keys);

			var mismatches = new List<string> ();

			foreach (var managedName in allManagedNames.OrderBy (n => n, StringComparer.Ordinal)) {
				var legacy = legacyData [managedName];
				var newInfo = newData [managedName];

				if (legacy.BaseJavaName != newInfo.BaseJavaName) {
					if (legacy.BaseJavaName == null && newInfo.BaseJavaName != null && managedName.Contains ('`')) {
						continue;
					}

					if (legacy.BaseJavaName == null && newInfo.BaseJavaName != null && newInfo.DoNotGenerateAcw) {
						continue;
					}

					if (legacy.BaseJavaName != null && newInfo.BaseJavaName == null &&
						legacy.BaseJavaName == legacy.JavaName) {
						continue;
					}

					mismatches.Add ($"{managedName}: legacy='{legacy.BaseJavaName ?? "(null)"}' new='{newInfo.BaseJavaName ?? "(null)"}'");
				}
			}

			return mismatches;
		}

		public static (List<string> missingInterfaces, List<string> extraInterfaces) CompareImplementedInterfaces (
			Dictionary<string, TypeComparisonData> legacyData,
			Dictionary<string, TypeComparisonData> newData)
		{
			var allManagedNames = new HashSet<string> (legacyData.Keys);
			allManagedNames.IntersectWith (newData.Keys);

			var missingInterfaces = new List<string> ();
			var extraInterfaces = new List<string> ();

			foreach (var managedName in allManagedNames.OrderBy (n => n, StringComparer.Ordinal)) {
				var legacy = legacyData [managedName];
				var newInfo = newData [managedName];

				var legacySet = new HashSet<string> (legacy.ImplementedInterfaces, StringComparer.Ordinal);
				var newSet = new HashSet<string> (newInfo.ImplementedInterfaces, StringComparer.Ordinal);

				foreach (var iface in legacySet.Except (newSet)) {
					missingInterfaces.Add ($"{managedName}: missing '{iface}'");
				}

				foreach (var iface in newSet.Except (legacySet)) {
					extraInterfaces.Add ($"{managedName}: extra '{iface}'");
				}
			}

			return (missingInterfaces, extraInterfaces);
		}

		public static (List<string> presenceMismatches, List<string> declaringTypeMismatches, List<string> styleMismatches) CompareActivationCtors (
			Dictionary<string, TypeComparisonData> legacyData,
			Dictionary<string, TypeComparisonData> newData)
		{
			var allManagedNames = new HashSet<string> (legacyData.Keys);
			allManagedNames.IntersectWith (newData.Keys);

			var presenceMismatches = new List<string> ();
			var declaringTypeMismatches = new List<string> ();
			var styleMismatches = new List<string> ();

			foreach (var managedName in allManagedNames.OrderBy (n => n, StringComparer.Ordinal)) {
				var legacy = legacyData [managedName];
				var newInfo = newData [managedName];

				if (legacy.HasActivationCtor != newInfo.HasActivationCtor) {
					presenceMismatches.Add ($"{managedName}: legacy.has={legacy.HasActivationCtor} new.has={newInfo.HasActivationCtor}");
					continue;
				}

				if (!legacy.HasActivationCtor) {
					continue;
				}

				if (legacy.ActivationCtorDeclaringType != newInfo.ActivationCtorDeclaringType) {
					declaringTypeMismatches.Add ($"{managedName}: legacy='{legacy.ActivationCtorDeclaringType}' new='{newInfo.ActivationCtorDeclaringType}'");
				}

				if (legacy.ActivationCtorStyle != newInfo.ActivationCtorStyle) {
					styleMismatches.Add ($"{managedName}: legacy='{legacy.ActivationCtorStyle}' new='{newInfo.ActivationCtorStyle}'");
				}
			}

			return (presenceMismatches, declaringTypeMismatches, styleMismatches);
		}

		public static (List<string> missingCtors, List<string> extraCtors) CompareJavaConstructors (
			Dictionary<string, TypeComparisonData> legacyData,
			Dictionary<string, TypeComparisonData> newData)
		{
			var allManagedNames = new HashSet<string> (legacyData.Keys);
			allManagedNames.IntersectWith (newData.Keys);

			var missingCtors = new List<string> ();
			var extraCtors = new List<string> ();

			foreach (var managedName in allManagedNames.OrderBy (n => n, StringComparer.Ordinal)) {
				var legacy = legacyData [managedName];
				var newInfo = newData [managedName];

				var legacySet = new HashSet<string> (legacy.JavaConstructorSignatures, StringComparer.Ordinal);
				var newSet = new HashSet<string> (newInfo.JavaConstructorSignatures, StringComparer.Ordinal);

				foreach (var sig in legacySet.Except (newSet)) {
					missingCtors.Add ($"{managedName}: missing '<init>{sig}'");
				}

				foreach (var sig in newSet.Except (legacySet)) {
					extraCtors.Add ($"{managedName}: extra '<init>{sig}'");
				}
			}

			return (missingCtors, extraCtors);
		}

		public static (List<string> interfaceMismatches, List<string> abstractMismatches, List<string> genericMismatches, List<string> acwMismatches) CompareTypeFlags (
			Dictionary<string, TypeComparisonData> legacyData,
			Dictionary<string, TypeComparisonData> newData)
		{
			var allManagedNames = new HashSet<string> (legacyData.Keys);
			allManagedNames.IntersectWith (newData.Keys);

			var interfaceMismatches = new List<string> ();
			var abstractMismatches = new List<string> ();
			var genericMismatches = new List<string> ();
			var acwMismatches = new List<string> ();

			foreach (var managedName in allManagedNames.OrderBy (n => n, StringComparer.Ordinal)) {
				var legacy = legacyData [managedName];
				var newInfo = newData [managedName];

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

			return (interfaceMismatches, abstractMismatches, genericMismatches, acwMismatches);
		}
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

			var managedName = GetManagedName (typeDef);

			string? baseJavaName = null;
			var baseType = typeDef.GetBaseType (cache);
			if (baseType != null) {
				var baseJni = JavaNativeTypeManager.ToJniName (baseType, cache);
				if (baseJni != null && baseJni != javaName) {
					baseJavaName = baseJni;
				}
			}

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

			bool hasActivationCtor = false;
			string? activationCtorDeclaringType = null;
			string? activationCtorStyle = null;
			FindLegacyActivationCtor (typeDef, cache, out hasActivationCtor, out activationCtorDeclaringType, out activationCtorStyle);

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
					declaringType = GetManagedName (current);
					style = "XamarinAndroid";
					return;
				}

				if ((p0 == "Java.Interop.JniObjectReference&" || p0 == "Java.Interop.JniObjectReference") &&
				    p1 == "Java.Interop.JniObjectReferenceOptions") {
					found = true;
					declaringType = GetManagedName (current);
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
			if (peer.AssemblyName != primaryAssemblyName) {
				continue;
			}

			var managedName = $"{peer.ManagedTypeName}, {peer.AssemblyName}";

			bool hasActivationCtor = peer.ActivationCtor != null;
			string? activationCtorDeclaringType = null;
			string? activationCtorStyle = null;
			if (peer.ActivationCtor != null) {
				activationCtorDeclaringType = $"{peer.ActivationCtor.DeclaringTypeName}, {peer.ActivationCtor.DeclaringAssemblyName}";
				activationCtorStyle = peer.ActivationCtor.Style.ToString ();
			}

			var javaCtorSignatures = peer.MarshalMethods
				.Where (m => m.IsConstructor)
				.Select (m => m.JniSignature)
				.OrderBy (s => s, StringComparer.Ordinal)
				.ToList ();

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
				peer.IsAbstract && !peer.IsInterface,
				peer.IsGenericDefinition,
				peer.DoNotGenerateAcw
			);
		}

		return perType;
	}

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

		var legacyNormalized = legacyMethods
			.ToDictionary (kvp => NormalizeCrc64 (kvp.Key), kvp => kvp.Value);
		var newNormalized = newMethods
			.ToDictionary (kvp => NormalizeCrc64 (kvp.Key), kvp => kvp.Value);

		var result = CompareUserTypeMarshalMethods (legacyNormalized, newNormalized);
		AssertNoDiffs ("MISSING from new scanner", result.Missing);
		AssertNoDiffs ("METHOD MISMATCHES", result.MethodMismatches);
	}

	static UserTypesMethodComparisonResult CompareUserTypeMarshalMethods (
		Dictionary<string, List<TypeMethodGroup>> legacyNormalized,
		Dictionary<string, List<TypeMethodGroup>> newNormalized)
	{
		var missing = new List<string> ();
		var methodMismatches = new List<string> ();

		foreach (var javaName in legacyNormalized.Keys.OrderBy (n => n, StringComparer.Ordinal)) {
			if (!newNormalized.TryGetValue (javaName, out var newGroups)) {
				missing.Add (javaName);
				continue;
			}

			var legacyGroups = legacyNormalized [javaName];

			foreach (var legacyGroup in legacyGroups) {
				CompareUserTypeMethodGroup (javaName, legacyGroup, newGroups, missing, methodMismatches);
			}
		}

		return new UserTypesMethodComparisonResult (missing, methodMismatches);
	}

	static void CompareUserTypeMethodGroup (
		string javaName,
		TypeMethodGroup legacyGroup,
		List<TypeMethodGroup> newGroups,
		List<string> missing,
		List<string> methodMismatches)
	{
		var newGroup = newGroups.FirstOrDefault (g => g.ManagedName == legacyGroup.ManagedName);
		if (newGroup == null) {
			missing.Add ($"{javaName} → {legacyGroup.ManagedName}");
			return;
		}

		if (legacyGroup.Methods.Count == 0) {
			return;
		}

		if (legacyGroup.Methods.Count != newGroup.Methods.Count) {
			methodMismatches.Add ($"{javaName}/{legacyGroup.ManagedName}: legacy={legacyGroup.Methods.Count} methods, new={newGroup.Methods.Count}");
			return;
		}

		for (int i = 0; i < legacyGroup.Methods.Count; i++) {
			var lm = legacyGroup.Methods [i];
			var nm = newGroup.Methods [i];
			if (lm.JniName != nm.JniName || lm.JniSignature != nm.JniSignature) {
				methodMismatches.Add ($"{javaName}: [{i}] legacy=({lm.JniName}, {lm.JniSignature}) new=({nm.JniName}, {nm.JniSignature})");
			}
		}
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
