namespace Xamarin.Android.Prepare
{
	/// <summary>
	///   Abstract base class for all scenarios which don't perform the full set of steps and thus should not generate
	///   the ProfileAssemblies.projitems file which step requires Mono runtime to be installed first.
	/// </summary>
	abstract class ScenarioNoStandardEndSteps : Scenario
	{
		protected ScenarioNoStandardEndSteps (string name, string description, Context context)
			: base (name, description, context)
		{}

		protected override void AddEndSteps (Context context)
		{
			// We don't want to call the end steps here because they require Mono runtime and assemblies to be installed
			// (they generate ProfileAssemblies.projitems which step verifies that the sets of assemblies on disk and in
			// xaprepare are identical)
		}
	}
}
