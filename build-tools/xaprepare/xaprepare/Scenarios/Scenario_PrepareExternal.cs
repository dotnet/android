using System;

namespace Xamarin.Android.Prepare
{
	[Scenario (isDefault: false)]
	partial class Scenario_PrepareExternal : ScenarioNoStandardEndSteps
	{
		public Scenario_PrepareExternal ()
			: base ("PrepareExternal", "Prepare external submodules", Context.Instance)
		{}

		protected override void AddSteps (Context context)
		{
			Steps.Add (new Step_PrepareExternal ());

			// disable installation of missing programs...
			context.SetCondition (KnownConditions.AllowProgramInstallation, false);

			// ...but do not signal an error when any are missing
			context.SetCondition (KnownConditions.IgnoreMissingPrograms, true);
		}
	}
}
