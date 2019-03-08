using System;
using System.Collections.Generic;

namespace Xamarin.Android.Prepare
{
	[Scenario (isDefault: false)]
	class Scenario_PrepareImageDependencies : ScenarioNoStandardEndSteps
	{
		public Scenario_PrepareImageDependencies () : base ("PrepareImageDependencies", "Prepare provisioning dependencies", Context.Instance)
		{}

		protected override void AddSteps (Context context)
		{
			Steps.Add (new Step_PrepareImageDependencies ());

			// disable installation of missing programs...
			context.SetCondition (KnownConditions.AllowProgramInstallation, false);

			// ...but do not signal an error when any are missing
			context.SetCondition (KnownConditions.IgnoreMissingPrograms, true);
		}
	}
}
