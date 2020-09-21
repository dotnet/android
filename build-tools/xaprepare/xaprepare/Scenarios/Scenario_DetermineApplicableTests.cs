using System;

namespace Xamarin.Android.Prepare
{
	[Scenario (isDefault: false)]
	partial class Scenario_DetermineApplicableTests : ScenarioNoStandardEndSteps
	{
		public Scenario_DetermineApplicableTests () 
			: base ("DetermineApplicableTests", "Determine which Azure Pipelines test jobs to run based on code change")
		{}

		protected override void AddSteps (Context context)
		{
			Steps.Add (new Step_DetermineAzurePipelinesTestJobs ());

			// disable installation of missing programs...
			context.SetCondition (KnownConditions.AllowProgramInstallation, false);

			// ...but do not signal an error when any are missing
			context.SetCondition (KnownConditions.IgnoreMissingPrograms, true);
		}
	}
}
