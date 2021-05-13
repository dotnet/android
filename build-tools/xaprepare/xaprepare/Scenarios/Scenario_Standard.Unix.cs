using System;
using System.Collections.Generic;

namespace Xamarin.Android.Prepare
{
	partial class Scenario_Standard
	{
		partial void AddRequiredOSSpecificSteps (bool beforeBundle)
		{
			AddRequiredMacOSSteps (beforeBundle);

			if (Context.Instance.WindowsJitAbisEnabled) {
				Log.DebugLine ("Windows JIT ABIs ENABLED, ADDING MinGW dependencies build step (BEFORE bundle)");
				Steps.Add (new Step_BuildMingwDependencies ());
			} else {
				Log.DebugLine ("Windows JIT ABis DISABLED, SKIPPING MinGW dependencies build step");
			}
		}

		partial void AddRequiredMacOSSteps (bool beforeBundle);
	}
}
