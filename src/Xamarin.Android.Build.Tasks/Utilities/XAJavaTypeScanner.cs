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
	sealed class TypeData
	{
		public readonly TypeDefinition FirstType;
		public readonly Dictionary<AndroidTargetArch, TypeDefinition> PerAbi;

		public bool IsAbiSpecific => !PerAbi.ContainsKey (AndroidTargetArch.None);

		public TypeData (TypeDefinition firstType)
		{
			FirstType = firstType;
			PerAbi = new Dictionary<AndroidTargetArch, TypeDefinition> ();
		}
	}

	public bool ErrorOnCustomJavaObject { get; set; }

	TaskLoggingHelper log;
	TypeDefinitionCache cache;

	public XAJavaTypeScanner (TaskLoggingHelper log, TypeDefinitionCache cache)
	{
		this.log = log;
		this.cache = cache;
	}

	public List<JavaType> GetJavaTypes (ICollection<ITaskItem> inputAssemblies, XAAssemblyResolver resolver)
	{
		var types = new Dictionary<string, TypeData> (StringComparer.Ordinal);
		foreach (ITaskItem asmItem in inputAssemblies) {
			AndroidTargetArch arch = MonoAndroidHelper.GetTargetArch (asmItem);
			AssemblyDefinition asmdef = resolver.Load (arch, asmItem.ItemSpec);

			foreach (ModuleDefinition md in asmdef.Modules) {
				foreach (TypeDefinition td in md.Types) {
					AddJavaType (td, types, arch);
				}
			}
		}

		var ret = new List<JavaType> ();
		foreach (var kvp in types) {
			ret.Add (new JavaType (kvp.Value.FirstType, kvp.Value.IsAbiSpecific ? kvp.Value.PerAbi : null));
		}

		return ret;
	}

	void AddJavaType (TypeDefinition type, Dictionary<string, TypeData> types, AndroidTargetArch arch)
	{
		if (type.ImplementsInterface ("Java.Interop.IJavaPeerable", cache)) {
			// For subclasses of e.g. Android.App.Activity.
			string typeName = type.GetPartialAssemblyQualifiedName (cache);
			if (!types.TryGetValue (typeName, out TypeData typeData)) {
				typeData = new TypeData (type);
				types.Add (typeName, typeData);
			}

			if (typeData.PerAbi.ContainsKey (AndroidTargetArch.None)) {
				if (arch == AndroidTargetArch.None) {
					throw new InvalidOperationException ($"Duplicate type '{type.FullName}' in assembly {type.Module.FileName}");
				}

				throw new InvalidOperationException ($"Previously added type '{type.FullName}' was in ABI-agnostic assembly, new one comes from ABI {arch} assembly");
			}

			if (typeData.PerAbi.ContainsKey (arch)) {
				throw new InvalidOperationException ($"Duplicate type '{type.FullName}' in assembly {type.Module.FileName}, for ABI {arch}");
			}

			typeData.PerAbi.Add (arch, type);
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
