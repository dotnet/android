using System;
using System.Collections.Generic;

using Microsoft.Build.Framework;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks;

/// <summary>
/// Assembly input set where **all** assemblies are expected to be RID-specific, any assembly which is
/// not will cause an exception.  This is meant to be used whenever linking is enabled.
/// </summary>
class RidSpecificInputAssemblySet : RidSensitiveInputAssemblySet
{
	protected override void Add (AndroidTargetArch arch, string key, ITaskItem assemblyItem, Dictionary<AndroidTargetArch, Dictionary<string, ITaskItem>> assemblies)
	{
		if (arch == AndroidTargetArch.None) {
			throw new InvalidOperationException ($"`Abi` metadata item is required for assembly '{assemblyItem.ItemSpec}'");
		}

		base.Add (arch, key, assemblyItem, assemblies);
	}
}
