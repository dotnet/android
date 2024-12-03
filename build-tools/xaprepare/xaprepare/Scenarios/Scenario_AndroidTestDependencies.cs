using System;

namespace Xamarin.Android.Prepare
{
	[Scenario (isDefault: false)]
	partial class Scenario_AndroidTestDependencies : ScenarioNoStandardEndSteps
	{
		protected virtual AndroidToolchainComponentType AndroidSdkNdkType => AndroidToolchainComponentType.CoreDependency;

		public Scenario_AndroidTestDependencies () 
			: base ("AndroidTestDependencies", "Install Android SDK, OpenJDK and .NET preview test dependencies.")
		{}

		protected Scenario_AndroidTestDependencies (string name, string description) 
			: base (name, description)
		{}

		protected override void AddSteps (Context context)
		{
			Steps.Add (new Step_InstallDotNetPreview ());
			Steps.Add (new Step_InstallMicrosoftOpenJDK (allowJIJavaHomeMatch: true));
			Steps.Add (new Step_Android_SDK_NDK (AndroidSdkNdkType));

			// disable installation of missing programs...
			context.SetCondition (KnownConditions.AllowProgramInstallation, false);

			// ...but do not signal an error when any are missing
			context.SetCondition (KnownConditions.IgnoreMissingPrograms, true);
		}
	}
}
