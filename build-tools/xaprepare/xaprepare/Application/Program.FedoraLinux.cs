using System;
using System.Threading.Tasks;

namespace Xamarin.Android.Prepare
{
	class FedoraLinuxProgram : LinuxProgram
	{
		public FedoraLinuxProgram (string packageName, string? executableName = null)
			: base (packageName, executableName)
		{}

		protected override bool CheckWhetherInstalled ()
		{
			return Utilities.RunCommand ("rpm", "-q", PackageName);
		}

		public override async Task<bool> Install ()
		{
			var runner = new ProcessRunner ("sudo", "dnf", "-y", "install", PackageName) {
				EchoStandardOutput = true,
				EchoStandardError = true,
				ProcessTimeout = TimeSpan.FromMinutes (30),
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

		protected override bool DeterminePackageVersion()
		{
			CurrentVersion = Utilities.GetStringFromStdout ("rpm", "-q", PackageName, "--qf", "%{version}");
			return !String.IsNullOrEmpty (CurrentVersion);
		}
	}
}
