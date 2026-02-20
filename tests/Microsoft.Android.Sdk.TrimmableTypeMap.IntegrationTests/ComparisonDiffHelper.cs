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
}
