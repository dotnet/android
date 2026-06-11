using System;

namespace Xamarin.Android.Prepare
{
	[Scenario (isDefault: false)]
	partial class Scenario_AndroidTestDependencies : ScenarioNoStandardEndSteps
	{
		public Scenario_AndroidTestDependencies () 
			: base ("AndroidTestDependencies", "Install Android SDK and .NET preview test dependencies.")
		{}

		protected Scenario_AndroidTestDependencies (string name, string description) 
			: base (name, description)
		{}

		protected override void AddSteps (Context context)
		{
			Steps.Add (new Step_PrepareDotNetWorkloads ());

			// disable installation of missing programs...
			context.SetCondition (KnownConditions.AllowProgramInstallation, false);

			// ...but do not signal an error when any are missing
			context.SetCondition (KnownConditions.IgnoreMissingPrograms, true);
		}
	}
}
