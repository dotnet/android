using System;

namespace Xamarin.Android.Prepare
{
	[Scenario (isDefault: false)]
	partial class Scenario_AndroidTestDependencies : ScenarioNoStandardEndSteps
	{
		public Scenario_AndroidTestDependencies () 
			: base ("AndroidTestDependencies", "Install Android SDK, OpenJDK and .NET preview test dependencies.")
		{}

		protected override void AddSteps (Context context)
		{
			Steps.Add (new Step_InstallDotNetPreview ());
			Steps.Add (new Step_InstallJetBrainsOpenJDK8 ());
			Steps.Add (new Step_InstallMicrosoftOpenJDK11 ());
			Steps.Add (new Step_Android_SDK_NDK (AndroidToolchainComponentType.CoreDependency));

			// disable installation of missing programs...
			context.SetCondition (KnownConditions.AllowProgramInstallation, false);

			// ...but do not signal an error when any are missing
			context.SetCondition (KnownConditions.IgnoreMissingPrograms, true);
		}
	}
}
