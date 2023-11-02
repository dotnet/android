using System;
using System.Collections.Generic;

namespace Xamarin.Android.Prepare
{
	[Scenario (isDefault: false)]
	class Scenario_ThirdPartyNotices : Scenario
	{
		public Scenario_ThirdPartyNotices ()
			: base ("ThirdPartyNotices", "Generate the `THIRD-PARTY-NOTICES.TXT` files.")
		{
			NeedsGitSubmodules = true;
		}

		protected override void AddSteps (Context context)
		{
			if (context == null)
				throw new ArgumentNullException (nameof (context));

			Steps.Add (new Step_ThirdPartyNotices ());
		}

		protected override void AddStartSteps (Context context)
		{
		}

		protected override void AddEndSteps (Context context)
		{
		}
	}
}
