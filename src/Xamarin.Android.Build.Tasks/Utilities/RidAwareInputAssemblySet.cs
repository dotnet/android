using System.Collections.Generic;

using Microsoft.Build.Framework;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks;

/// <summary>
/// Assembly input set where **some** assemblies are expected to be RID-specific but **all** of
/// them need to be placed in RID-specific sets.  Any assembly that is not RID-specific will be
/// copied to all the RID-specific sets.  This is to be used in `Release` builds when linking is
/// **disabled**, as these builds use assembly MVIDs and type+method metadata token IDs.
/// </summary>
class RidAwareInputAssemblySet : RidSensitiveInputAssemblySet
{
	List<AndroidTargetArch> targetArches;

	public RidAwareInputAssemblySet (ICollection<AndroidTargetArch> targetArches)
	{
		this.targetArches = new List<AndroidTargetArch> (targetArches);
	}

	protected override void Add (AndroidTargetArch arch, string key, ITaskItem assemblyItem, Dictionary<AndroidTargetArch, Dictionary<string, ITaskItem>> assemblies)
	{
		if (arch != AndroidTargetArch.None) {
			base.Add (arch, key, assemblyItem, assemblies);
			return;
		}

		foreach (AndroidTargetArch targetArch in targetArches) {
			base.Add (targetArch, key, assemblyItem, assemblies);
		}
	}
}
