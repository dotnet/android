using System;

namespace Xamarin.Android.Prepare
{
	[Scenario (isDefault: false)]
	partial class Scenario_EmulatorTestDependencies : ScenarioNoStandardEndSteps
	{
		public Scenario_EmulatorTestDependencies () 
			: base ("EmulatorTestDependencies", "Install Android SDK emulator dependencies.")
		{}

		protected override void AddSteps (Context context)
		{
			Steps.Add (new Step_Android_SDK_NDK (AndroidToolchainComponentType.EmulatorDependency));

			// disable installation of missing programs...
			context.SetCondition (KnownConditions.AllowProgramInstallation, false);

			// ...but do not signal an error when any are missing
			context.SetCondition (KnownConditions.IgnoreMissingPrograms, true);
		}
	}
}
