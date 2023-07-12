using System;
using System.Collections.Generic;

using Microsoft.Build.Framework;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks;

class RidSpecificInputAssemblySet : InputAssemblySet
{
	Dictionary<AndroidTargetArch, Dictionary<string, ITaskItem>> javaTypeAssemblies = new ();
	Dictionary<AndroidTargetArch, Dictionary<string, ITaskItem>> userAssemblies = new ();

	public Dictionary<AndroidTargetArch, Dictionary<string, ITaskItem>> JavaTypeAssemblies => javaTypeAssemblies;
	public Dictionary<AndroidTargetArch, Dictionary<string, ITaskItem>> UserAssemblies     => userAssemblies;

	public override void AddJavaTypeAssembly (ITaskItem assemblyItem)
	{
		Add (assemblyItem.ItemSpec, assemblyItem, javaTypeAssemblies);
	}

	public override void AddUserAssembly (ITaskItem assemblyItem)
	{
		Add (GetUserAssemblyKey (assemblyItem), assemblyItem, userAssemblies);
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

	void Add (string key, ITaskItem assemblyItem, Dictionary<AndroidTargetArch, Dictionary<string, ITaskItem>> assemblies)
	{
		AndroidTargetArch arch = MonoAndroidHelper.GetTargetArch (assemblyItem);
		if (arch == AndroidTargetArch.None) {
			throw new InvalidOperationException ($"`Abi` metadata item is required for assembly '{assemblyItem.ItemSpec}'");
		}

		if (!assemblies.TryGetValue (arch, out Dictionary<string, ITaskItem>? dict)) {
			dict = new (StringComparer.OrdinalIgnoreCase);
			assemblies.Add (arch, dict);
		}

		if (dict.ContainsKey (key)) {
			return;
		}

		dict.Add (key, assemblyItem);
	}
}
