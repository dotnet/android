using System;
using System.Collections.Generic;

using Java.Interop.Tools.Cecil;
using Java.Interop.Tools.TypeNameMappings;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Utilities;
using Mono.Cecil;

namespace Xamarin.Android.Tasks;

class ACWMapGenerator
{
	readonly TaskLoggingHelper log;

	public ACWMapGenerator (TaskLoggingHelper log)
	{
		this.log = log;
	}

	public bool Generate (NativeCodeGenState codeGenState, string acwMapFile)
	{
		List<JavaType> javaTypes = codeGenState.JavaTypesForJCW;
		TypeDefinitionCache cache = codeGenState.TypeCache;

		// We need to save a map of .NET type -> ACW type for resource file fixups
		var managed = new Dictionary<string, TypeDefinition> (javaTypes.Count, StringComparer.Ordinal);
		var java    = new Dictionary<string, TypeDefinition> (javaTypes.Count, StringComparer.Ordinal);

		var managedConflicts = new Dictionary<string, List<string>> (0, StringComparer.Ordinal);
		var javaConflicts    = new Dictionary<string, List<string>> (0, StringComparer.Ordinal);

		bool success = true;

		using var acw_map = MemoryStreamPool.Shared.CreateStreamWriter ();
		foreach (JavaType jt in javaTypes) {
			TypeDefinition type = jt.Type;
			string managedKey = type.FullName.Replace ('/', '.');
			string javaKey = JavaNativeTypeManager.ToJniName (type, cache).Replace ('/', '.');

			acw_map.Write (type.GetPartialAssemblyQualifiedName (cache));
			acw_map.Write (';');
			acw_map.Write (javaKey);
			acw_map.WriteLine ();

			TypeDefinition conflict;
			bool hasConflict = false;
			if (managed.TryGetValue (managedKey, out conflict)) {
				if (!conflict.Module.Name.Equals (type.Module.Name)) {
					if (!managedConflicts.TryGetValue (managedKey, out var list))
					managedConflicts.Add (managedKey, list = new List<string> { conflict.GetPartialAssemblyName (cache) });
					list.Add (type.GetPartialAssemblyName (cache));
					success = false;
				}
				hasConflict = true;
			}
			if (java.TryGetValue (javaKey, out conflict)) {
				if (!conflict.Module.Name.Equals (type.Module.Name)) {
					if (!javaConflicts.TryGetValue (javaKey, out var list))
					javaConflicts.Add (javaKey, list = new List<string> { conflict.GetAssemblyQualifiedName (cache) });
					list.Add (type.GetAssemblyQualifiedName (cache));
					success = false;
				}
				hasConflict = true;
			}
			if (!hasConflict) {
				managed.Add (managedKey, type);
				java.Add (javaKey, type);

				acw_map.Write (managedKey);
				acw_map.Write (';');
				acw_map.Write (javaKey);
				acw_map.WriteLine ();

				acw_map.Write (JavaNativeTypeManager.ToCompatJniName (type, cache).Replace ('/', '.'));
				acw_map.Write (';');
				acw_map.Write (javaKey);
				acw_map.WriteLine ();
			}
		}

		acw_map.Flush ();
		Files.CopyIfStreamChanged (acw_map.BaseStream, acwMapFile);

		foreach (var kvp in managedConflicts) {
			log.LogCodedWarning ("XA4214", Properties.Resources.XA4214, kvp.Key, string.Join (", ", kvp.Value));
			log.LogCodedWarning ("XA4214", Properties.Resources.XA4214_Result, kvp.Key, kvp.Value [0]);
		}

		foreach (var kvp in javaConflicts) {
			log.LogCodedError ("XA4215", Properties.Resources.XA4215, kvp.Key);
			foreach (var typeName in kvp.Value) {
				log.LogCodedError ("XA4215", Properties.Resources.XA4215_Details, kvp.Key, typeName);
			}
		}

		if (javaConflicts.Count > 0) {
			return false;
		}

		return success;
	}
}
