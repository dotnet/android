using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;

namespace Xamarin.Android.Tasks;

public class FixUpMonoAndroidRuntime : AndroidTask
{
	public override string TaskPrefix => "FUMAR";

	[Required]
	public string IntermediateOutputDirectory { get; set; } = String.Empty;

	[Required]
	public ITaskItem[] ResolvedAssemblies { get; set; } = [];

	public override bool RunTask ()
	{
		List<ITaskItem> monoAndroidRuntimeItems = new ();
		foreach (ITaskItem item in ResolvedAssemblies) {

			if (!MonoAndroidHelper.StringEquals (Path.GetFileName (item.ItemSpec), "Mono.Android.Runtime.dll", StringComparison.OrdinalIgnoreCase)) {
				continue;
			}
			monoAndroidRuntimeItems.Add (item);
		}

		if (monoAndroidRuntimeItems.Count == 0) {
			Log.LogDebugMessage ("No 'Mono.Android.Runtime.dll' items found");
			return !Log.HasLoggedErrors;
		}

		return MonoAndroidRuntimeMarshalMethodsFixUp.Run (Log, monoAndroidRuntimeItems) && !Log.HasLoggedErrors;
	}
}
