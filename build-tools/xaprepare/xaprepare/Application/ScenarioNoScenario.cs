using System;

namespace Xamarin.Android.Prepare
{
	class ScenarioNoScenario : Scenario
	{
		public ScenarioNoScenario () : base ("ScenarioNoScenario", "No scenario selected")
		{}

		protected override void AddEndSteps (Context context)
		{
			throw new NotImplementedException ();
		}

		protected override void AddStartSteps (Context context)
		{
			throw new NotImplementedException ();
		}

		protected override void AddSteps (Context context)
		{
			throw new NotImplementedException ();
		}
	}
}
