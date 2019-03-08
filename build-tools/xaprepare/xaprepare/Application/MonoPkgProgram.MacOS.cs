using System;
using System.IO;
using System.Threading.Tasks;

namespace Xamarin.Android.Prepare
{
	class MonoPkgProgram : PkgProgram
	{
		public MonoPkgProgram (string name, string packageId, Uri packageUrl = null)
			: base (name, packageId, packageUrl)
		{
			ExecutableName = "mono";
		}

		public override bool CanInstall ()
		{
			// We do not want to return `false` here if Mono updates are disallowed - we want Install to be called to
			// show the error message as returning `false` here might prevent other programs from updating, and there's
			// no good reason for this.
			if (!Context.Instance.CheckCondition (KnownConditions.AllowMonoUpdate))
				return base.CanInstall ();
			return true;
		}

		public override async Task<bool> Install ()
		{
			if (Context.Instance.CheckCondition (KnownConditions.AllowMonoUpdate))
				return await base.Install ();

			Log.ErrorLine ($"Mono needs to be updated but updates are disallowed in this scenario. Please run prepare with the '/s:{Scenario_UpdateMono.MyName}' parameter to update Mono.");
			return false;
		}

		protected override bool CheckWhetherInstalled ()
		{
			IgnoreMaximumVersion = Context.Instance.IgnoreMaxMonoVersion;
			return base.CheckWhetherInstalled ();
		}

		protected override async Task<bool> DetermineCurrentVersion ()
		{
			// Mono is special in that its package does not contain the full version, so we have to get it from the
			// --version output instead.
			SkipPkgUtilVersionCheck = true;
			return await base.DetermineCurrentVersion ();
		}
	}
}
