using System;
using System.Collections.Generic;

using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks;

static class AssemblyPackagingHelper
{
	public static void AddAssembliesFromCollection (TaskLoggingHelper Log, ICollection<string> SupportedAbis, ICollection<ITaskItem> assemblies, Action<AndroidTargetArch, Dictionary<string, ITaskItem>> doAddAssemblies)
	{
		Dictionary<AndroidTargetArch, Dictionary<string, ITaskItem>> perArchAssemblies = MonoAndroidHelper.GetPerArchAssemblies (
			assemblies,
			SupportedAbis,
			validate: true,
			shouldSkip: (ITaskItem asm) => {
				if (bool.TryParse (asm.GetMetadata ("AndroidSkipAddToPackage"), out bool value) && value) {
					Log.LogDebugMessage ($"Skipping {asm.ItemSpec} due to 'AndroidSkipAddToPackage' == 'true' ");
					return true;
				}

				return false;
			}
		);

		foreach (var kvp in perArchAssemblies) {
			Log.LogDebugMessage ($"Adding assemblies for architecture '{kvp.Key}'");
			doAddAssemblies (kvp.Key, kvp.Value);
		}
	}
}
