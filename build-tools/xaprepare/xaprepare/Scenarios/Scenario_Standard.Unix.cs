using System;
using System.Collections.Generic;

namespace Xamarin.Android.Prepare
{
	partial class Scenario_Standard
	{
		partial void AddRequiredOSSpecificSteps (bool beforeBundle)
		{
			if (!beforeBundle) {
				// It has to go after the bundle step because bundle unpacking or creation *always* cleans its
				// destination directory and this is where we download the GAS binaries. They are not part of the bundle
				// (because they're not useful for every day work with XA) so they must be downloaded after the bundle
				// is unpacked.
				Log.DebugLine ("Adding Windows GAS download step (AFTER bundle)");
				Steps.Add (new Step_Get_Windows_GAS ());
				return;
			}

			if (Context.Instance.WindowsJitAbisEnabled) {
				Log.DebugLine ("Windows JIT ABIs ENABLED, ADDING MinGW dependencies build step (BEFORE bundle)");
				Steps.Add (new Step_BuildMingwDependencies ());
			} else {
				Log.DebugLine ("Windows JIT ABis DISABLED, SKIPPING MinGW dependencies build step");
			}
		}
	}
}
