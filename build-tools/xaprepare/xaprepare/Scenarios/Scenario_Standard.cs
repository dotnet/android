using System;

namespace Xamarin.Android.Prepare
{
	[Scenario (isDefault: true)]
	partial class Scenario_Standard : Scenario
	{
		public Scenario_Standard ()
			: base ("Standard", "Standard init", Context.Instance)
		{}

		protected override void AddSteps (Context context)
		{
			if (context == null)
				throw new ArgumentNullException (nameof (context));

			Steps.Add (new Step_ShowEnabledRuntimes ());
			Steps.Add (new Step_InstallCorrettoOpenJDK ());
			Steps.Add (new Step_Android_SDK_NDK ());
			Steps.Add (new Step_CloneMonoIfNeeded ());
			Steps.Add (new Step_GenerateFiles (atBuildStart: true));
			Steps.Add (new Step_PrepareProps ());
			Steps.Add (new Step_PrepareExternal ());
			Steps.Add (new Step_PrepareLocal ());
			Steps.Add (new Step_DownloadMonoArchive ());
			AddRequiredOSSpecificSteps (true);
			Steps.Add (new Step_InstallMonoRuntimes ());
			Steps.Add (new Step_Get_Windows_Binutils ());

			AddRequiredOSSpecificSteps (false);
		}

		protected override void AddEndSteps (Context context)
		{
			Steps.Add (new Step_ThirdPartyNotices ());
			Steps.Add (new Step_GenerateFiles (atBuildStart: false));
		}

		partial void AddRequiredOSSpecificSteps (bool beforeBundle);
	}
}
