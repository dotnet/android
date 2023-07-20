using System.Collections.Generic;

using Microsoft.Build.Framework;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks;

abstract class RidSensitiveInputAssemblySet : InputAssemblySet
{
	// Both dictionaries must have the same keys present, even if the associated value is an empty dictionary
	Dictionary<AndroidTargetArch, Dictionary<string, ITaskItem>> javaTypeAssemblies = new ();
	Dictionary<AndroidTargetArch, Dictionary<string, ITaskItem>> userAssemblies = new ();

	public Dictionary<AndroidTargetArch, Dictionary<string, ITaskItem>> JavaTypeAssemblies => javaTypeAssemblies;
	public Dictionary<AndroidTargetArch, Dictionary<string, ITaskItem>> UserAssemblies     => userAssemblies;

	public override void AddJavaTypeAssembly (ITaskItem assemblyItem)
	{
		AndroidTargetArch arch = MonoAndroidHelper.GetTargetArch (assemblyItem);
		Add (arch, assemblyItem.ItemSpec, assemblyItem, JavaTypeAssemblies);
		EnsureArchKey (arch, userAssemblies);
	}

	public override void AddUserAssembly (ITaskItem assemblyItem)
	{
		AndroidTargetArch arch = MonoAndroidHelper.GetTargetArch (assemblyItem);
		Add (arch, GetUserAssemblyKey (assemblyItem), assemblyItem, UserAssemblies);
		EnsureArchKey (arch, javaTypeAssemblies);
	}

	public override bool IsUserAssembly (string name)
	{
		foreach (var kvp in userAssemblies) {
			if (kvp.Value.ContainsKey (name)) {
				return true;
			}
		}

		return false;
	}

	protected virtual void Add (AndroidTargetArch targetArch, string key, ITaskItem assemblyItem, Dictionary<AndroidTargetArch, Dictionary<string, ITaskItem>> assemblies)
	{
		Dictionary<string, ITaskItem> dict = EnsureArchKey (targetArch, assemblies);
		if (dict.ContainsKey (key)) {
			return;
		}

		dict.Add (key, assemblyItem);
	}

	Dictionary<string, ITaskItem> EnsureArchKey (AndroidTargetArch targetArch, Dictionary<AndroidTargetArch, Dictionary<string, ITaskItem>> assemblies)
	{
		if (assemblies.TryGetValue (targetArch, out Dictionary<string, ITaskItem> dict)) {
			return dict;
		}

		dict = new (AssemblyNameStringComparer);
		assemblies.Add (targetArch, dict);
		return dict;
	}
}
