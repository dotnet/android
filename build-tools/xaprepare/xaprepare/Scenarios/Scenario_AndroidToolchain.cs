using System;

namespace Xamarin.Android.Prepare
{
	[Scenario (isDefault: false)]
	partial class Scenario_AndroidToolchain : ScenarioNoStandardEndSteps
	{
		public Scenario_AndroidToolchain () 
			: base ("AndroidToolchain", "Install Android SDK, NDK and Corretto JDK.", Context.Instance)
		{}

		protected override void AddSteps (Context context)
		{
			Steps.Add (new Step_InstallCorrettoOpenJDK ());
			Steps.Add (new Step_Android_SDK_NDK ());

			// disable installation of missing programs...
			context.SetCondition (KnownConditions.AllowProgramInstallation, false);

			// ...but do not signal an error when any are missing
			context.SetCondition (KnownConditions.IgnoreMissingPrograms, true);
		}
	}
}
