using System;

namespace Xamarin.Android.Prepare
{
	[Scenario (isDefault: false)]
	partial class Scenario_EmulatorTestDependencies : Scenario_AndroidTestDependencies
	{
		protected override AndroidToolchainComponentType AndroidSdkNdkType => AndroidToolchainComponentType.CoreDependency | AndroidToolchainComponentType.EmulatorDependency;

		public Scenario_EmulatorTestDependencies () 
			: base ("EmulatorTestDependencies", "Install Android SDK (with emulator), OpenJDK, and .NET preview test dependencies.")
		{}
	}
}
