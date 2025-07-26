#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
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
		List<TypeDefinition> javaTypes = codeGenState.JavaTypesForJCW;
		TypeDefinitionCache cache = codeGenState.TypeCache;

		// We need to save a map of .NET type -> ACW type for resource file fixups
		var managed = new Dictionary<string, TypeDefinition> (javaTypes.Count, StringComparer.Ordinal);
		var java    = new Dictionary<string, TypeDefinition> (javaTypes.Count, StringComparer.Ordinal);

		var managedConflicts = new Dictionary<string, List<string>> (0, StringComparer.Ordinal);
		var javaConflicts    = new Dictionary<string, List<string>> (0, StringComparer.Ordinal);

		bool success = true;

		using var acw_map = MemoryStreamPool.Shared.CreateStreamWriter ();
		foreach (TypeDefinition type in javaTypes.OrderBy (t => t.FullName.Replace ('/', '.'))) {
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

		// If there's conflicts, the "new way" file never got written, and will show up as
		// "changed" in our comparison test, so skip it.
		if (javaConflicts.Count > 0) {
			return false;
		}

		if (Files.HasStreamChanged (acw_map.BaseStream, acwMapFile)) {
			log.LogError ($"ACW map file '{acwMapFile}' changed");
			Files.CopyIfStreamChanged (acw_map.BaseStream, acwMapFile + "2");
		} else {
			log.LogDebugMessage ($"ACW map file '{acwMapFile}' unchanged");
		}

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

	public void Generate (List<ACWMapEntry> javaTypes, string acwMapFile)
	{
		// We need to save a map of .NET type -> ACW type for resource file fixups
		var managed = new Dictionary<string, ACWMapEntry> (javaTypes.Count, StringComparer.Ordinal);
		var java = new Dictionary<string, ACWMapEntry> (javaTypes.Count, StringComparer.Ordinal);

		var managedConflicts = new Dictionary<string, List<string>> (0, StringComparer.Ordinal);
		var javaConflicts = new Dictionary<string, List<string>> (0, StringComparer.Ordinal);

		using var acw_map = MemoryStreamPool.Shared.CreateStreamWriter ();

		foreach (var type in javaTypes.OrderBy (t => t.ManagedKey)) {
			string managedKey = type.ManagedKey;
			string javaKey = type.JavaKey;

			acw_map.Write (type.PartialAssemblyQualifiedName);
			acw_map.Write (';');
			acw_map.Write (javaKey);
			acw_map.WriteLine ();

			ACWMapEntry conflict;
			bool hasConflict = false;

			if (managed.TryGetValue (managedKey, out conflict)) {
				if (!conflict.ModuleName.Equals (type.ModuleName)) {
					if (!managedConflicts.TryGetValue (managedKey, out var list))
						managedConflicts.Add (managedKey, list = new List<string> { conflict.PartialAssemblyName });
					list.Add (type.PartialAssemblyName);
				}
				hasConflict = true;
			}

			if (java.TryGetValue (javaKey, out conflict)) {
				if (!conflict.ModuleName.Equals(type.ModuleName)) {
					if (!javaConflicts.TryGetValue (javaKey, out var list))
						javaConflicts.Add (javaKey, list = new List<string> { conflict.AssemblyQualifiedName });
					list.Add (type.AssemblyQualifiedName);
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

				acw_map.Write (type.CompatJniName);
				acw_map.Write (';');
				acw_map.Write (javaKey);
				acw_map.WriteLine ();
			}
		}

		acw_map.Flush ();

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

		// Don't write the output file if there are any errors so that
		// future incremental builds will try again.
		if (javaConflicts.Count > 0)
			return;

		Files.CopyIfStreamChanged (acw_map.BaseStream, acwMapFile);
	}
}

class ACWMapEntry
{
	public string AssemblyQualifiedName { get; set; }
	public string CompatJniName { get; set; }
	public string JavaKey { get; set; }
	public string ManagedKey { get; set; }
	public string ModuleName { get; set; }
	public string PartialAssemblyName { get; set; }
	public string PartialAssemblyQualifiedName { get; set; }

	public ACWMapEntry (string assemblyQualifiedName, string compatJniName, string javaKey, string managedKey, string moduleName, string partialAssemblyName, string partialAssemblyQualifiedName)
	{
		AssemblyQualifiedName = assemblyQualifiedName;
		CompatJniName = compatJniName;
		JavaKey = javaKey;
		ManagedKey = managedKey;
		ModuleName = moduleName;
		PartialAssemblyName = partialAssemblyName;
		PartialAssemblyQualifiedName = partialAssemblyQualifiedName;
	}

	public static ACWMapEntry Create (TypeDefinition type, TypeDefinitionCache cache)
	{
		return new ACWMapEntry (
			assemblyQualifiedName: type.GetAssemblyQualifiedName (cache),
			compatJniName: JavaNativeTypeManager.ToCompatJniName (type, cache).Replace ('/', '.'),
			javaKey: JavaNativeTypeManager.ToJniName (type, cache).Replace ('/', '.'),
			managedKey: type.FullName.Replace ('/', '.'),
			moduleName: type.Module.Name,
			partialAssemblyName: type.GetPartialAssemblyName (cache),
			partialAssemblyQualifiedName: type.GetPartialAssemblyQualifiedName (cache)
		);
	}

	public static ACWMapEntry Create (XElement type, string partialAssemblyName, string moduleName)
	{
		return new ACWMapEntry (
			assemblyQualifiedName: type.GetAttributeOrDefault ("assembly-qualified-name", string.Empty),
			compatJniName: type.GetAttributeOrDefault ("compat-jni-name", string.Empty),
			javaKey: type.GetAttributeOrDefault ("java-key", string.Empty),
			managedKey: type.GetAttributeOrDefault ("managed-key", string.Empty),
			moduleName: moduleName,
			partialAssemblyName: partialAssemblyName,
			partialAssemblyQualifiedName: type.GetAttributeOrDefault ("partial-assembly-qualified-name", string.Empty)
		);
	}
}
