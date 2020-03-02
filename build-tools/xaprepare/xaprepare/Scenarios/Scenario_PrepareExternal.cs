using System;

namespace Xamarin.Android.Prepare
{
	[Scenario (isDefault: false)]
	partial class Scenario_PrepareExternal : ScenarioNoStandardEndSteps
	{
		public Scenario_PrepareExternal ()
			: base ("PrepareExternal", "Prepare external submodules", Context.Instance)
		{
			NeedsGitSubmodules = true;
		}

		protected override void AddSteps (Context context)
		{
			Steps.Add (new Step_PrepareExternal ());
			Steps.Add (new Step_PrepareExternalJavaInterop ());
		}
	}
}
