#nullable enable
using System;
using System.Collections.Generic;
using System.IO;

using Java.Interop.Tools.Cecil;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Mono.Cecil;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks;

class XAJavaTypeScanner
{
	// Names of assemblies which don't have Mono.Android.dll references, or are framework assemblies, but which must
	// be scanned for Java types.
	static readonly HashSet<string> SpecialAssemblies = new HashSet<string> (StringComparer.OrdinalIgnoreCase) {
		"Mono.Android.dll",
		"Mono.Android.Runtime.dll",
	};

	public bool ErrorOnCustomJavaObject { get; set; }

	readonly TaskLoggingHelper log;
	readonly TypeDefinitionCache cache;
	readonly AndroidTargetArch targetArch;

	public XAJavaTypeScanner (AndroidTargetArch targetArch, TaskLoggingHelper log, TypeDefinitionCache cache)
	{
		this.targetArch = targetArch;
		this.log = log;
		this.cache = cache;
	}

	public List<TypeDefinition> GetJavaTypes (ICollection<ITaskItem> inputAssemblies, XAAssemblyResolver resolver)
	{
		var types = new List<TypeDefinition> ();
		foreach (ITaskItem asmItem in inputAssemblies) {
			if (!ShouldScan (asmItem)) {
				log.LogDebugMessage ($"[{targetArch}] Skipping Java type scanning in assembly '{asmItem.ItemSpec}'");
				continue;
			}
			log.LogDebugMessage ($"[{targetArch}] Scanning assembly '{asmItem.ItemSpec}' for Java types");

			AndroidTargetArch arch = MonoAndroidHelper.GetTargetArch (asmItem);
			if (arch != targetArch) {
				throw new InvalidOperationException ($"Internal error: assembly '{asmItem.ItemSpec}' should be in the '{targetArch}' architecture, but is in '{arch}' instead.");
			}

			AssemblyDefinition? asmdef = resolver.Load (asmItem.ItemSpec);
			if (asmdef == null) {
				log.LogDebugMessage ($"[{targetArch}] Unable to load assembly '{asmItem.ItemSpec}'");
				continue;
			}

			foreach (ModuleDefinition md in asmdef.Modules) {
				foreach (TypeDefinition td in md.Types) {
					AddJavaType (td, types);
				}
			}
		}

		return types;
	}

	bool ShouldScan (ITaskItem assembly)
	{
		string name = Path.GetFileName (assembly.ItemSpec);
		if (SpecialAssemblies.Contains (name)) {
			return true;
		}

		string? hasMonoAndroidReferenceMetadata = assembly.GetMetadata ("HasMonoAndroidReference");
		if (String.IsNullOrEmpty (hasMonoAndroidReferenceMetadata)) {
			return true; // Just in case - the metadata missing might be a false negative
		}

		if (Boolean.TryParse (hasMonoAndroidReferenceMetadata, out bool hasMonoAndroidReference)) {
			return hasMonoAndroidReference;
		}

		// A catch-all, it's better to do more work than to miss something important.
		return true;
	}

	void AddJavaType (TypeDefinition type, List<TypeDefinition> types)
	{
		if (type.HasJavaPeer (cache)) {
			// For subclasses of e.g. Android.App.Activity.
			types.Add (type);
		} else if (type.IsClass && !type.IsSubclassOf ("System.Exception", cache) && type.ImplementsInterface ("Android.Runtime.IJavaObject", cache)) {
			string message = $"XA4212: Type `{type.FullName}` implements `Android.Runtime.IJavaObject` but does not inherit `Java.Lang.Object` or `Java.Lang.Throwable`. This is not supported.";

			if (ErrorOnCustomJavaObject) {
				log.LogError (message);
			} else {
				log.LogWarning (message);
			}
			return;
		}

		if (!type.HasNestedTypes) {
			return;
		}

		foreach (TypeDefinition nested in type.NestedTypes) {
			AddJavaType (nested, types);
		}
	}
}
