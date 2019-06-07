using System;

namespace Xamarin.Android.Prepare
{
	partial class Scenario_Standard
	{
		partial void AddRequiredOSSpecificSteps (bool beforeBundle)
		{
			if (!beforeBundle)
				return;

			Steps.Add (new Step_InstallAnt ());
		}
	}
}
