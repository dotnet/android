using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

using Java.Interop.Tools.Cecil;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Mono.Cecil;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks;

class JavaType
{
	public readonly TypeDefinition Type;
	public readonly IDictionary<AndroidTargetArch, TypeDefinition>? PerAbiTypes;
	public bool IsABiSpecific { get; }

	public JavaType (TypeDefinition type, IDictionary<AndroidTargetArch, TypeDefinition>? perAbiTypes)
	{
		Type = type;
		if (perAbiTypes != null) {
			PerAbiTypes = new ReadOnlyDictionary<AndroidTargetArch, TypeDefinition> (perAbiTypes);
			IsABiSpecific = perAbiTypes.Count > 1 || (perAbiTypes.Count == 1 && !perAbiTypes.ContainsKey (AndroidTargetArch.None));
		}
	}
}

class XAJavaTypeScanner
{
	public bool ErrorOnCustomJavaObject { get; set; }

	TaskLoggingHelper log;
	TypeDefinitionCache cache;

	public XAJavaTypeScanner (TaskLoggingHelper log, TypeDefinitionCache cache)
	{
		this.log = log;
		this.cache = cache;
	}

	public ICollection<TypeDefinition> GetJavaTypes (ICollection<ITaskItem> inputAssemblies, XAAssemblyResolver resolver)
	{
		var types = new Dictionary<string, TypeDefinition> (StringComparer.Ordinal);
		foreach (ITaskItem asmItem in inputAssemblies) {
			AndroidTargetArch arch = MonoAndroidHelper.GetTargetArch (asmItem);
			AssemblyDefinition asmdef = resolver.Load (arch, asmItem.ItemSpec);

			foreach (ModuleDefinition md in asmdef.Modules) {
				foreach (TypeDefinition td in md.Types) {
					AddJavaType (td, types);
				}
			}
		}

		return types.Values;
	}

	void AddJavaType (TypeDefinition type, Dictionary<string, TypeDefinition> types)
	{
		if (type.IsSubclassOf ("Java.Lang.Object", cache) || type.IsSubclassOf ("Java.Lang.Throwable", cache) || (type.IsInterface && type.ImplementsInterface ("Java.Interop.IJavaPeerable", cache))) {
			// For subclasses of e.g. Android.App.Activity.
			string typeName = type.GetPartialAssemblyQualifiedName (cache);
			if (types.ContainsKey (typeName)) {
				return;
			}

			types.Add (typeName, type);
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
