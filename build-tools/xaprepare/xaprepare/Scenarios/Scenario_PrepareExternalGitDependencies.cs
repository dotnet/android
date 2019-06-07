using System;
using System.Collections.Generic;

namespace Xamarin.Android.Prepare
{
	[Scenario (isDefault: false)]
	class Scenario_PrepareExternalGitDependencies : ScenarioNoStandardEndSteps
	{
		public Scenario_PrepareExternalGitDependencies ()
			: base ("PrepareExternalGitDependencies", "Prepare external GIT dependencies", Context.Instance)
		{
			LogFilePath = Context.Instance.GetLogFilePath ("external-git-deps");
		}

		protected override void AddSteps (Context context)
		{
			Steps.Add (new Step_PrepareExternalGitDependencies ());

			// disable installation of missing programs...
			context.SetCondition (KnownConditions.AllowProgramInstallation, false);

			// ...but do not signal an error when any are missing
			context.SetCondition (KnownConditions.IgnoreMissingPrograms, true);
		}
	}
}
