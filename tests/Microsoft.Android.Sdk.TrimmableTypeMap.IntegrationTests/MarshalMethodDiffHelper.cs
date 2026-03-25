using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Android.Sdk.TrimmableTypeMap.IntegrationTests;

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

static class MarshalMethodDiffHelper
{
	public static MarshalMethodComparisonResult CompareMarshalMethods (
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

	public static UserTypesMethodComparisonResult CompareUserTypeMarshalMethods (
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

	public static List<string> CompareExportFlags (
		Dictionary<string, List<TypeMethodGroup>> legacyMethods,
		Dictionary<string, List<TypeMethodGroup>> newMethods)
	{
		var mismatches = new List<string> ();

		var allJavaNames = new HashSet<string> (legacyMethods.Keys);
		allJavaNames.IntersectWith (newMethods.Keys);

		foreach (var javaName in allJavaNames.OrderBy (n => n, StringComparer.Ordinal)) {
			var legacyGroups = legacyMethods [javaName];
			var newGroups = newMethods [javaName];

			var legacyByManaged = legacyGroups.ToDictionary (g => g.ManagedName, g => g.Methods);
			var newByManaged = newGroups.ToDictionary (g => g.ManagedName, g => g.Methods);

			foreach (var managedName in legacyByManaged.Keys.Intersect (newByManaged.Keys)) {
				var legacyMethodList = legacyByManaged [managedName];
				var newMethodList = newByManaged [managedName];

				var legacyByKey = legacyMethodList
					.GroupBy (m => (m.JniName, m.JniSignature))
					.ToDictionary (g => g.Key, g => g.First ());
				var newByKey = newMethodList
					.GroupBy (m => (m.JniName, m.JniSignature))
					.ToDictionary (g => g.Key, g => g.First ());

				foreach (var key in legacyByKey.Keys.Intersect (newByKey.Keys)) {
					var lm = legacyByKey [key];
					var nm = newByKey [key];

					if (lm.IsExport != nm.IsExport) {
						mismatches.Add ($"{javaName} [{managedName}]: {key.JniName}{key.JniSignature} legacy.IsExport={lm.IsExport} new.IsExport={nm.IsExport}");
					}
				}
			}
		}

		return mismatches;
	}

	public static List<string> CompareNativeCallbackNames (
		Dictionary<string, List<TypeMethodGroup>> legacyMethods,
		Dictionary<string, List<TypeMethodGroup>> newMethods)
	{
		var mismatches = new List<string> ();

		var allJavaNames = new HashSet<string> (legacyMethods.Keys);
		allJavaNames.IntersectWith (newMethods.Keys);

		foreach (var javaName in allJavaNames.OrderBy (n => n, StringComparer.Ordinal)) {
			var legacyGroups = legacyMethods [javaName];
			var newGroups = newMethods [javaName];

			var legacyByManaged = legacyGroups.ToDictionary (g => g.ManagedName, g => g.Methods);
			var newByManaged = newGroups.ToDictionary (g => g.ManagedName, g => g.Methods);

			foreach (var managedName in legacyByManaged.Keys.Intersect (newByManaged.Keys)) {
				var legacyMethodList = legacyByManaged [managedName];
				var newMethodList = newByManaged [managedName];

				var legacyByKey = legacyMethodList
					.GroupBy (m => (m.JniName, m.JniSignature))
					.ToDictionary (g => g.Key, g => g.First ());
				var newByKey = newMethodList
					.GroupBy (m => (m.JniName, m.JniSignature))
					.ToDictionary (g => g.Key, g => g.First ());

				foreach (var key in legacyByKey.Keys.Intersect (newByKey.Keys)) {
					var lm = legacyByKey [key];
					var nm = newByKey [key];

					// Skip constructors — legacy uses "n_ClassName" format while
					// new scanner uses "n_ctor", which are different by design
					if (key.JniName == ".ctor") {
						continue;
					}

					var lc = lm.NativeCallbackName ?? "";
					var nc = nm.NativeCallbackName ?? "";

					// DoNotGenerateAcw types and interfaces use direct [Register]
					// attribute extraction which doesn't produce NativeCallbackName.
					// Only compare when legacy has a value.
					if (lc == "") {
						continue;
					}

					// Legacy format: "n_jniMethodName"
					// New format: "n_ManagedMethodName_EncodedParams"
					// The naming conventions differ by design — legacy uses JNI names
					// while new scanner uses managed names. Just verify both have a
					// callback and start with "n_".
					if (nc == "") {
						mismatches.Add ($"{javaName} [{managedName}]: {key.JniName}{key.JniSignature} legacy='{lc}' new=(missing)");
					} else if (!nc.StartsWith ("n_", StringComparison.Ordinal)) {
						mismatches.Add ($"{javaName} [{managedName}]: {key.JniName}{key.JniSignature} new callback '{nc}' does not start with 'n_'");
					}
				}
			}
		}

		return mismatches;
	}

	public static List<string> CompareDeclaringTypes (
		Dictionary<string, List<TypeMethodGroup>> legacyMethods,
		Dictionary<string, List<TypeMethodGroup>> newMethods)
	{
		var mismatches = new List<string> ();

		var allJavaNames = new HashSet<string> (legacyMethods.Keys);
		allJavaNames.IntersectWith (newMethods.Keys);

		foreach (var javaName in allJavaNames.OrderBy (n => n, StringComparer.Ordinal)) {
			var legacyGroups = legacyMethods [javaName];
			var newGroups = newMethods [javaName];

			var legacyByManaged = legacyGroups.ToDictionary (g => g.ManagedName, g => g.Methods);
			var newByManaged = newGroups.ToDictionary (g => g.ManagedName, g => g.Methods);

			foreach (var managedName in legacyByManaged.Keys.Intersect (newByManaged.Keys)) {
				var legacyMethodList = legacyByManaged [managedName];
				var newMethodList = newByManaged [managedName];

				var legacyByKey = legacyMethodList
					.GroupBy (m => (m.JniName, m.JniSignature))
					.ToDictionary (g => g.Key, g => g.First ());
				var newByKey = newMethodList
					.GroupBy (m => (m.JniName, m.JniSignature))
					.ToDictionary (g => g.Key, g => g.First ());

				foreach (var key in legacyByKey.Keys.Intersect (newByKey.Keys)) {
					var lm = legacyByKey [key];
					var nm = newByKey [key];

					// The legacy pipeline doesn't track declaring types for
					// interface method implementations. Only compare when
					// legacy has a non-empty value.
					if (lm.DeclaringTypeName == "" && nm.DeclaringTypeName != "") {
						continue;
					}

					if (lm.DeclaringTypeName != nm.DeclaringTypeName) {
						mismatches.Add ($"{javaName} [{managedName}]: {key.JniName}{key.JniSignature} legacy='{lm.DeclaringTypeName}' new='{nm.DeclaringTypeName}'");
					}
				}
			}
		}

		return mismatches;
	}
}
