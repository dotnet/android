using System;

namespace Xamarin.Android.Prepare
{
	[Scenario (isDefault: false)]
	partial class Scenario_EmulatorTestDependencies : Scenario_AndroidTestDependencies
	{
		public Scenario_EmulatorTestDependencies () 
			: base ("EmulatorTestDependencies", "Install Android SDK (with emulator) and .NET preview test dependencies.")
		{}
	}
}
