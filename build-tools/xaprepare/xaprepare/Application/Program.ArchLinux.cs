using System;
using System.Threading.Tasks;

namespace Xamarin.Android.Prepare
{
	class ArchLinuxProgram : LinuxProgram
	{
		public ArchLinuxProgram (string packageName, string? executableName = null)
			: base (packageName, executableName)
		{}

		protected override bool CheckWhetherInstalled ()
		{
			return Utilities.RunCommand ("pacman", "-Q", PackageName);
		}

#pragma warning disable CS1998
		public override async Task<bool> Install ()
		{
			var runner = new ProcessRunner ("sudo", "pacman", "-S", "--noconfirm", PackageName) {
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
#pragma warning restore CS1998

		protected override bool DeterminePackageVersion()
		{
			var output = Utilities.GetStringFromStdout ("pacman", "-Q", PackageName).Split(' ');
			CurrentVersion = output.Length == 2 ? output[1] : null;
			return !String.IsNullOrEmpty (CurrentVersion);
		}
	}
}
