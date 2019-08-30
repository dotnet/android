using System;
using System.Collections.Generic;

namespace Xamarin.Android.Prepare
{
	partial class Scenario_Standard
	{
		partial void AddRequiredMacOSSteps (bool beforeBundle)
		{
			if (beforeBundle)
				return;

			Steps.Add (new Step_ChangeLibMonoSgenDylibID ());
		}
	}
}
