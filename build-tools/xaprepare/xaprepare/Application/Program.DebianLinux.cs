using System;
using System.Threading.Tasks;

namespace Xamarin.Android.Prepare
{
	class DebianLinuxProgram : LinuxProgram
	{
		class AptGetStandardStreamWrapper : ProcessStandardStreamWrapper
		{
			bool interactive = Context.Instance.InteractiveSession;

			public AptGetStandardStreamWrapper ()
			{
				LoggingLevel = ProcessStandardStreamWrapper.LogLevel.Message;
			}

			protected override string PreprocessMessage (string message, ref bool writeLine)
			{
				// apt-get calls `dpkg` which can't be persuaded to not show any progress and it shows the progress by
				// writing a line which ends with `0x0D` that is supposed to move the caret to the beginning of the line
				// which doesn't work with System.Diagnostics.Process because it strips 0x0D and 0x0A before passing the
				// line to us... So in order to keep the display straight we need to reset the cursor position blindly
				// here.
				Console.CursorLeft = 1;
				return message?.TrimEnd ();
			}
		}

		public DebianLinuxProgram (string packageName, string executableName = null)
			: base (packageName, executableName)
		{}

		protected override bool CheckWhetherInstalled ()
		{
			string status = Utilities.GetStringFromStdout ("dpkg-query", "-f", "${db:Status-Abbrev}", "-W", PackageName);
			return !String.IsNullOrEmpty (status) && status.Length >= 2 && status[1] == 'i';
		}

		public override async Task<bool> Install ()
		{
			var runner = new ProcessRunner ("sudo", "apt-get", "-f", "-u", "install", PackageName) {
				EchoStandardOutput = true,
				EchoStandardError = true,
				ProcessTimeout = TimeSpan.FromMinutes (30),
				StandardOutputEchoWrapper = new AptGetStandardStreamWrapper (),
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

		protected override bool DeterminePackageVersion ()
		{
			CurrentVersion = Utilities.GetStringFromStdout ("dpkg-query", "-f", "${Version}", "-W", PackageName);
			return !String.IsNullOrEmpty (CurrentVersion);
		}
	}
}
