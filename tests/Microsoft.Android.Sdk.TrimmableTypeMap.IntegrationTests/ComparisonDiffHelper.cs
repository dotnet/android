using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Android.Sdk.TrimmableTypeMap.IntegrationTests;

static class ComparisonDiffHelper
{
	public static List<string> CompareBaseJavaNames (
		Dictionary<string, TypeComparisonData> legacyData,
		Dictionary<string, TypeComparisonData> newData)
	{
		var allManagedNames = new HashSet<string> (legacyData.Keys);
		allManagedNames.IntersectWith (newData.Keys);

		var mismatches = new List<string> ();

		foreach (var managedName in allManagedNames.OrderBy (n => n, System.StringComparer.Ordinal)) {
			var legacy = legacyData [managedName];
			var newInfo = newData [managedName];

			if (legacy.BaseJavaName == newInfo.BaseJavaName) {
				continue;
			}

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

		foreach (var managedName in allManagedNames.OrderBy (n => n, System.StringComparer.Ordinal)) {
			var legacy = legacyData [managedName];
			var newInfo = newData [managedName];

			var legacySet = new HashSet<string> (legacy.ImplementedInterfaces, System.StringComparer.Ordinal);
			var newSet = new HashSet<string> (newInfo.ImplementedInterfaces, System.StringComparer.Ordinal);

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

		foreach (var managedName in allManagedNames.OrderBy (n => n, System.StringComparer.Ordinal)) {
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

		foreach (var managedName in allManagedNames.OrderBy (n => n, System.StringComparer.Ordinal)) {
			var legacy = legacyData [managedName];
			var newInfo = newData [managedName];

			var legacySet = new HashSet<string> (legacy.JavaConstructorSignatures, System.StringComparer.Ordinal);
			var newSet = new HashSet<string> (newInfo.JavaConstructorSignatures, System.StringComparer.Ordinal);

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

		foreach (var managedName in allManagedNames.OrderBy (n => n, System.StringComparer.Ordinal)) {
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

	public static (List<string> missingComponents, List<string> extraComponents, List<string> kindMismatches, List<string> nameMismatches, List<string> propertyMismatches) CompareComponentAttributes (
		Dictionary<string, ComponentComparisonData> legacyData,
		Dictionary<string, ComponentComparisonData> newData)
	{
		var allManagedNames = new HashSet<string> (legacyData.Keys);
		allManagedNames.UnionWith (newData.Keys);

		var missingComponents = new List<string> ();
		var extraComponents = new List<string> ();
		var kindMismatches = new List<string> ();
		var nameMismatches = new List<string> ();
		var propertyMismatches = new List<string> ();

		foreach (var managedName in allManagedNames.OrderBy (n => n, System.StringComparer.Ordinal)) {
			var inLegacy = legacyData.TryGetValue (managedName, out var legacy);
			var inNew = newData.TryGetValue (managedName, out var newInfo);

			if (inLegacy && !inNew) {
				missingComponents.Add ($"{managedName}: {(legacy?.ComponentKind ?? "(null)")}");
				continue;
			}

			if (!inLegacy && inNew) {
				extraComponents.Add ($"{managedName}: {(newInfo?.ComponentKind ?? "(null)")}");
				continue;
			}

			if (legacy == null || newInfo == null) {
				continue;
			}

			if (legacy.ComponentKind != newInfo.ComponentKind) {
				kindMismatches.Add ($"{managedName}: legacy='{legacy.ComponentKind ?? "(null)"}' new='{newInfo.ComponentKind ?? "(null)"}'");
			}

			if (legacy.ComponentName != newInfo.ComponentName) {
				nameMismatches.Add ($"{managedName}: legacy='{legacy.ComponentName ?? "(null)"}' new='{newInfo.ComponentName ?? "(null)"}'");
			}

			// Compare component properties
			var legacyPropSet = new HashSet<string> (legacy.ComponentProperties, System.StringComparer.Ordinal);
			var newPropSet = new HashSet<string> (newInfo.ComponentProperties, System.StringComparer.Ordinal);

			foreach (var prop in legacyPropSet.Except (newPropSet)) {
				propertyMismatches.Add ($"{managedName}: missing property '{prop}'");
			}

			foreach (var prop in newPropSet.Except (legacyPropSet)) {
				propertyMismatches.Add ($"{managedName}: extra property '{prop}'");
			}
		}

		return (missingComponents, extraComponents, kindMismatches, nameMismatches, propertyMismatches);
	}

	public static (List<string> missingPermissions, List<string> extraPermissions, List<string> missingFeatures, List<string> extraFeatures) CompareAssemblyManifestAttributes (
		ManifestAttributeComparisonData legacy,
		ManifestAttributeComparisonData newData)
	{
		var legacyPermSet = new HashSet<string> (legacy.UsesPermissions, System.StringComparer.Ordinal);
		var newPermSet = new HashSet<string> (newData.UsesPermissions, System.StringComparer.Ordinal);

		var missingPermissions = legacyPermSet.Except (newPermSet)
			.Select (p => $"uses-permission: {p}")
			.OrderBy (s => s, System.StringComparer.Ordinal)
			.ToList ();

		var extraPermissions = newPermSet.Except (legacyPermSet)
			.Select (p => $"uses-permission: {p}")
			.OrderBy (s => s, System.StringComparer.Ordinal)
			.ToList ();

		var legacyFeatSet = new HashSet<string> (legacy.UsesFeatures, System.StringComparer.Ordinal);
		var newFeatSet = new HashSet<string> (newData.UsesFeatures, System.StringComparer.Ordinal);

		var missingFeatures = legacyFeatSet.Except (newFeatSet)
			.Select (f => $"uses-feature: {f}")
			.OrderBy (s => s, System.StringComparer.Ordinal)
			.ToList ();

		var extraFeatures = newFeatSet.Except (legacyFeatSet)
			.Select (f => $"uses-feature: {f}")
			.OrderBy (s => s, System.StringComparer.Ordinal)
			.ToList ();

		return (missingPermissions, extraPermissions, missingFeatures, extraFeatures);
	}
}
