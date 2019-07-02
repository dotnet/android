namespace Xamarin.Android.Prepare
{
	partial class Step_PrepareBundle
	{
		const string bundle404Message = null;

		void InitOS ()
		{
			osSupportsMonoBuild = true;

			AddFailureStep (new Step_BuildMonoRuntimes ());
			AddBuildLibZipFailureStep ();
			if (Context.Instance.WindowsJitAbisEnabled)
				AddFailureStep (new Step_BuildLibZipForWindows ());

			// We need it here (even though Scenario_Standard runs the step, because if we failed to download the
			// bundle, the Step_BuildMonoRuntimes above will clean the destination directory and the Windows GAS
			// executables with it.
			AddFailureStep (new Step_Get_Windows_GAS ());
			AddFailureStep (new Step_CreateBundle ());
		}

		partial void AddBuildLibZipFailureStep ();
	}
}
