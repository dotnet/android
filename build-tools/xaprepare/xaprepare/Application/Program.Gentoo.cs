using System;
using System.Threading.Tasks;

namespace Xamarin.Android.Prepare
{
	class GentooLinuxProgram : LinuxProgram
	{
		public GentooLinuxProgram (string packageName, string? executableName = null)
			: base (packageName, executableName)
		{}

		protected override bool CheckWhetherInstalled ()
		{
			return Utilities.RunCommand ("equery", "--quiet", "list", PackageName);
		}

#pragma warning disable CS1998
		public override async Task<bool> Install ()
		{
			var runner = new ProcessRunner ("sudo", "emerge", "--oneshot", PackageName) {
				EchoStandardOutput = true,
				EchoStandardError = true,
				ProcessTimeout = TimeSpan.FromMinutes (60),	// gcc most probably will not compile in 60 minutes...
			};

			bool failed = await Task.Run (() => !runner.Run ());
			if (failed) {
				Log.Error ($"Installation of {PackageName} timed out");
				failed = true;
			}

			if (runner.ExitCode != 0) {
				Log.Error ($"Installation failed with error code {runner.ExitCode}");
				failed = true;
			}

			return !failed;
		}
#pragma warning restore CS1998

		protected override bool DeterminePackageVersion()
		{
			var output = Utilities.GetStringFromStdout ("equery", "--quiet", "list", PackageName).Replace($"{PackageName}-", "");
			if (!String.IsNullOrEmpty(output)) {
				CurrentVersion = output;
				return true;
			}

			return false;
		}
	}
}
