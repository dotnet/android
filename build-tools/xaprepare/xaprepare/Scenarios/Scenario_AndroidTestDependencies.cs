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
			Steps.Add (new Step_InstallAdoptOpenJDK8 ());
			Steps.Add (new Step_InstallMicrosoftOpenJDK11 ());
			Steps.Add (new Step_Android_SDK_NDK (AndroidToolchainComponentType.CoreDependency));
			Steps.Add (new Step_GenerateMonoAndroidProfileItems ());

			// disable installation of missing programs...
			context.SetCondition (KnownConditions.AllowProgramInstallation, false);

			// ...but do not signal an error when any are missing
			context.SetCondition (KnownConditions.IgnoreMissingPrograms, true);
		}
	}
}
