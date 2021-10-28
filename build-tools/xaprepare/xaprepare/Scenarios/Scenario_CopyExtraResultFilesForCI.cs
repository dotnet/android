using System.IO;

namespace Xamarin.Android.Prepare
{
	[Scenario (isDefault: false)]
	partial class Scenario_CopyExtraResultFilesForCI : ScenarioNoStandardEndSteps
	{
		public Scenario_CopyExtraResultFilesForCI () 
			: base ("CopyExtraResultFilesForCI", "Copy extra result files to artifact directory")
		{
			// Do not create a log that we would attempt to copy during this scenario.
			Log.Instance.SetLogFile (Path.Combine (Context.Instance.LogDirectory, $"package-results-{Context.Instance.BuildTimeStamp}.txt"));
		}

		protected override void AddSteps (Context context)
		{
			Steps.Add (new Step_CopyExtraResultFilesForCI ());

			// disable installation of missing programs...
			context.SetCondition (KnownConditions.AllowProgramInstallation, false);

			// ...but do not signal an error when any are missing
			context.SetCondition (KnownConditions.IgnoreMissingPrograms, true);
		}
	}
}
