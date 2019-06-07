using System;
using System.Collections.Generic;

namespace Xamarin.Android.Prepare
{
	[Scenario (isDefault: false)]
	class Scenario_UpdateMono : ScenarioNoStandardEndSteps
	{
		public const string MyName = "UpdateMono";

		public Scenario_UpdateMono ()
			: base (MyName, "Perform basic detection steps AND update Mono if necessary", Context.Instance)
		{}

		protected override void AddSteps (Context context)
		{
			// Allow automatic provisioning...
			context.AutoProvision = true;

			// ...and let it use sudo, because without it it's useless...
			context.AutoProvisionUsesSudo = true;

			// ...no new steps here, just enable Mono updates...
			context.SetCondition (KnownConditions.AllowMonoUpdate, true);

			// ...and disable installation of other programs...
			context.SetCondition (KnownConditions.AllowProgramInstallation, false);

			// ...but do not signal an error when any are missing
			context.SetCondition (KnownConditions.IgnoreMissingPrograms, true);
		}
	}
}
