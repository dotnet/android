using System;
using System.Collections.Generic;

using Java.Interop.Tools.Cecil;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Mono.Cecil;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks;

class XAJavaTypeScanner
{
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
			AndroidTargetArch arch = MonoAndroidHelper.GetTargetArch (asmItem);
			if (arch != targetArch) {
				throw new InvalidOperationException ($"Internal error: assembly '{asmItem.ItemSpec}' should be in the '{targetArch}' architecture, but is in '{arch}' instead.");
			}

			AssemblyDefinition asmdef = resolver.Load (asmItem.ItemSpec);

			foreach (ModuleDefinition md in asmdef.Modules) {
				foreach (TypeDefinition td in md.Types) {
					AddJavaType (td, types, arch);
				}
			}
		}

		return types;
	}

	void AddJavaType (TypeDefinition type, List<TypeDefinition> types, AndroidTargetArch arch)
	{
		if (type.IsSubclassOf ("Java.Lang.Object", cache) || type.IsSubclassOf ("Java.Lang.Throwable", cache) || (type.IsInterface && type.ImplementsInterface ("Java.Interop.IJavaPeerable", cache))) {
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
			AddJavaType (nested, types, arch);
		}
	}
}
