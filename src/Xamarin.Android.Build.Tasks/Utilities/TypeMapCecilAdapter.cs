#nullable enable
using System;
using System.Collections.Generic;
using Java.Interop.Tools.Cecil;
using Mono.Cecil;

using ModuleReleaseData = Xamarin.Android.Tasks.TypeMapGenerator.ModuleReleaseData;
using ReleaseGenerationState = Xamarin.Android.Tasks.TypeMapGenerator.ReleaseGenerationState;
using TypeMapDebugEntry = Xamarin.Android.Tasks.TypeMapGenerator.TypeMapDebugEntry;
using TypeMapReleaseEntry = Xamarin.Android.Tasks.TypeMapGenerator.TypeMapReleaseEntry;

namespace Xamarin.Android.Tasks;

// Converts types from Mono.Cecil to the format used by the typemap generator.
class TypeMapCecilAdapter
{
	public static (List<TypeMapDebugEntry> javaToManaged, List<TypeMapDebugEntry> managedToJava) GetDebugNativeEntries (NativeCodeGenState state)
	{
		var javaToManaged = new List<TypeMapDebugEntry> ();
		var managedToJava = new List<TypeMapDebugEntry> ();

		var javaDuplicates = new Dictionary<string, List<TypeMapDebugEntry>> (StringComparer.Ordinal);
		foreach (TypeDefinition td in state.AllJavaTypes) {
			UpdateApplicationConfig (state, td);

			TypeMapDebugEntry entry = GetDebugEntry (td, state.TypeCache);
			HandleDebugDuplicates (javaDuplicates, entry, td, state.TypeCache);

			javaToManaged.Add (entry);
			managedToJava.Add (entry);
		}

		SyncDebugDuplicates (javaDuplicates);

		return (javaToManaged, managedToJava);
	}

	public static ReleaseGenerationState GetReleaseGenerationState (NativeCodeGenState state)
	{
		var genState = new ReleaseGenerationState ();

		foreach (TypeDefinition td in state.AllJavaTypes) {
			ProcessReleaseType (state, genState, td);
		}

		return genState;
	}

	static void ProcessReleaseType (NativeCodeGenState state, ReleaseGenerationState genState, TypeDefinition td)
	{
		UpdateApplicationConfig (state, td);
		genState.AddKnownAssembly (GetAssemblyName (td));

		// We must NOT use Guid here! The reason is that Guid sort order is different than its corresponding
		// byte array representation and on the runtime we need the latter in order to be able to binary search
		// through the module array.
		byte [] moduleUUID;
		if (!genState.MvidCache.TryGetValue (td.Module.Mvid, out moduleUUID)) {
			moduleUUID = td.Module.Mvid.ToByteArray ();
			genState.MvidCache.Add (td.Module.Mvid, moduleUUID);
		}

		Dictionary<byte [], ModuleReleaseData> tempModules = genState.TempModules;
		if (!tempModules.TryGetValue (moduleUUID, out ModuleReleaseData moduleData)) {
			moduleData = new ModuleReleaseData {
				Mvid = td.Module.Mvid,
				MvidBytes = moduleUUID,
				//Assembly = td.Module.Assembly,
				AssemblyName = td.Module.Assembly.Name.Name,
				TypesScratch = new Dictionary<string, TypeMapReleaseEntry> (StringComparer.Ordinal),
				DuplicateTypes = new List<TypeMapReleaseEntry> (),
			};

			tempModules.Add (moduleUUID, moduleData);
		}

		string javaName = Java.Interop.Tools.TypeNameMappings.JavaNativeTypeManager.ToJniName (td, state.TypeCache);
		// We will ignore generic types and interfaces when generating the Java to Managed map, but we must not
		// omit them from the table we output - we need the same number of entries in both java-to-managed and
		// managed-to-java tables.  `SkipInJavaToManaged` set to `true` will cause the native assembly generator
		// to output `0` as the token id for the type, thus effectively causing the runtime unable to match such
		// a Java type name to a managed type. This fixes https://github.com/xamarin/xamarin-android/issues/4660
		var entry = new TypeMapReleaseEntry {
			JavaName = javaName,
			ManagedTypeName = td.FullName,
			Token = td.MetadataToken.ToUInt32 (),
			SkipInJavaToManaged = ShouldSkipInJavaToManaged (td),
		};

		if (moduleData.TypesScratch.ContainsKey (entry.JavaName)) {
			// This is disabled because it costs a lot of time (around 150ms per standard XF Integration app
			// build) and has no value for the end user. The message is left here because it may be useful to us
			// in our devloop at some point.
			//log.LogDebugMessage ($"Warning: duplicate Java type name '{entry.JavaName}' in assembly '{moduleData.AssemblyName}' (new token: {entry.Token}).");
			moduleData.DuplicateTypes.Add (entry);
		} else {
			moduleData.TypesScratch.Add (entry.JavaName, entry);
		}
	}

	static string GetAssemblyName (TypeDefinition td) => td.Module.Assembly.FullName;

	static TypeMapDebugEntry GetDebugEntry (TypeDefinition td, TypeDefinitionCache cache)
	{
		return new TypeMapDebugEntry {
			JavaName = Java.Interop.Tools.TypeNameMappings.JavaNativeTypeManager.ToJniName (td, cache),
			ManagedName = GetManagedTypeName (td),
			TypeDefinition = td,
			SkipInJavaToManaged = ShouldSkipInJavaToManaged (td),
		};
	}

	static string GetManagedTypeName (TypeDefinition td)
	{
		// This is necessary because Mono runtime will return to us type name with a `.` for nested types (not a
		// `/` or a `+`. So, for instance, a type named `DefaultRenderer` found in the
		// `Xamarin.Forms.Platform.Android.Platform` class in the `Xamarin.Forms.Platform.Android` assembly will
		// be seen here as
		//
		//   Xamarin.Forms.Platform.Android.Platform/DefaultRenderer
		//
		// The managed land name for the type will be rendered as
		//
		//   Xamarin.Forms.Platform.Android.Platform+DefaultRenderer
		//
		// And this is the form that we need in the map file
		//
		string managedTypeName = td.FullName.Replace ('/', '+');

		return $"{managedTypeName}, {td.Module.Assembly.Name.Name}";
	}


	static void HandleDebugDuplicates (Dictionary<string, List<TypeMapDebugEntry>> javaDuplicates, TypeMapDebugEntry entry, TypeDefinition td, TypeDefinitionCache cache)
	{
		List<TypeMapDebugEntry> duplicates;

		if (!javaDuplicates.TryGetValue (entry.JavaName, out duplicates)) {
			javaDuplicates.Add (entry.JavaName, new List<TypeMapDebugEntry> { entry });
		} else {
			TypeMapDebugEntry oldEntry = duplicates [0];
			if ((td.IsAbstract || td.IsInterface) &&
					!oldEntry.TypeDefinition.IsAbstract &&
					!oldEntry.TypeDefinition.IsInterface &&
					td.IsAssignableFrom (oldEntry.TypeDefinition, cache)) {
				// We found the `Invoker` type *before* the declared type
				// Fix things up so the abstract type is first, and the `Invoker` is considered a duplicate.
				duplicates.Insert (0, entry);
				oldEntry.SkipInJavaToManaged = false;
			} else {
				// ¯\_(ツ)_/¯
				duplicates.Add (entry);
			}
		}
	}

	static bool ShouldSkipInJavaToManaged (TypeDefinition td)
	{
		return td.IsInterface || td.HasGenericParameters;
	}

	static void SyncDebugDuplicates (Dictionary<string, List<TypeMapDebugEntry>> javaDuplicates)
	{
		foreach (List<TypeMapDebugEntry> duplicates in javaDuplicates.Values) {
			if (duplicates.Count < 2) {
				continue;
			}

			// Java duplicates must all point to the same managed type
			// Managed types, however, must point back to the original Java type instead
			// File/assembly generator use the `DuplicateForJavaToManaged` field to know to which managed type the
			// duplicate Java type must be mapped.
			TypeMapDebugEntry template = duplicates [0];
			for (int i = 1; i < duplicates.Count; i++) {
				duplicates [i].DuplicateForJavaToManaged = template;
			}
		}
	}

	static void UpdateApplicationConfig (NativeCodeGenState state, TypeDefinition javaType)
	{
		if (state.JniAddNativeMethodRegistrationAttributePresent || !javaType.HasCustomAttributes) {
			return;
		}

		foreach (CustomAttribute ca in javaType.CustomAttributes) {
			if (!state.JniAddNativeMethodRegistrationAttributePresent && String.Compare ("JniAddNativeMethodRegistrationAttribute", ca.AttributeType.Name, StringComparison.Ordinal) == 0) {
				state.JniAddNativeMethodRegistrationAttributePresent = true;
				break;
			}
		}
	}
}
