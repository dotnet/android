using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Xamarin.Android.Prepare
{
    class ArchLinuxProgram : LinuxProgram
    {
        private static string aurHelper = null;
		// TODO: check if binary name is right.
        private static string[] aurHelpersToCheck = new string[] { "yay","aurman","pacaur","pakku","aura","pikaur","trizen","pbget","auracle","repofish","pb", "bb", "rua" };
        /// <summary>
        /// Available AUR helper which has to support "-Q", "-S".
        /// </summary>
        public static string AvailableAurHelper
        {
            get
            {
                if (!string.IsNullOrEmpty(aurHelper))
                {
                    return null;
                }
                return aurHelper = CheckForAvailability(aurHelpersToCheck);
            }
        }

        private static string CheckForAvailability(string[] aurHelpersToCheck)
        {
			foreach (var item in aurHelpersToCheck)
			{
				if(ExistsOnPath(item))
					return item;
			}
			return null;
        }

		#region Check for executable
		// code from https://stackoverflow.com/a/3856090
		private static bool ExistsOnPath(string fileName)
		{
			return GetFullPath(fileName) != null;
		}

		private static string GetFullPath(string fileName)
		{
			if (File.Exists(fileName))
				return Path.GetFullPath(fileName);

			var values = Environment.GetEnvironmentVariable("PATH");
			foreach (var path in values.Split(Path.PathSeparator))
			{
				var fullPath = Path.Combine(path, fileName);
				if (File.Exists(fullPath))
					return fullPath;
			}
			return null;
		}
		#endregion
		
        public ArchLinuxProgram(string packageName, string executableName = null)
            : base(packageName, executableName)
        { }

		// TODO: Add support for packages with different name but with package name in "provides" field.
        protected override bool CheckWhetherInstalled()
        {
            var pacmanOut = Utilities.GetStringFromStdout("pacman", "-Q", PackageName, "--noconfirm", "--color", "never");
            return !String.IsNullOrEmpty(pacmanOut) && !pacmanOut.ToLower().StartsWith("error:");
        }

#pragma warning disable CS1998
        public override async Task<bool> Install()
        {

            ProcessRunner runner = new ProcessRunner("sudo", AvailableAurHelper ?? "pacman", "-S", PackageName, "--noconfirm", "--color", "never")
			{
				EchoStandardOutput = true,
				EchoStandardError = true,
				ProcessTimeout = TimeSpan.FromMinutes(30),
				StandardOutputEchoWrapper = new PacmanStandardStreamWrapper()
			};

            bool failed = await Task.Run(() => !runner.Run());
            if (failed)
            {
                Log.Error($"Installation of {PackageName} timed out");
                failed = true;
            }

            if (runner.ExitCode != 0)
            {
                Log.Error($"Installation failed with error code {runner.ExitCode}");
                failed = true;
            }

            return !failed;
        }
#pragma warning restore CS1998

		// TODO: Add support for packages with different name but with package name in "provides" field.
        protected override bool DeterminePackageVersion()
        {
            var pacmanOut = Utilities.GetStringFromStdout("pacman", "-Q", PackageName, "--noconfirm", "--color", "never");
            if (String.IsNullOrEmpty(pacmanOut) || pacmanOut.ToLower().StartsWith("error"))
            {
                CurrentVersion = null;
                return false;
            }

            var parts = pacmanOut.Split(' ');
            CurrentVersion = parts.Length == 2 ? parts[1] : null;
            return !String.IsNullOrEmpty(CurrentVersion);
        }

        private class PacmanStandardStreamWrapper : ProcessStandardStreamWrapper
        {
			bool interactive = Context.Instance.InteractiveSession;

			public PacmanStandardStreamWrapper ()
			{
				LoggingLevel = ProcessStandardStreamWrapper.LogLevel.Message;
			}

			protected override string PreprocessMessage (string message, ref bool writeLine)
			{
				// apt-get calls `dpkg` which can't be persuaded to not show any progress and it shows the progress by
				// writing a line which ends with `0x0D` that is supposed to move the caret to the beginning of the line
				// which doesn't work with System.Diagnostics.Process because it strips 0x0D and 0x0A before passing the
				// line to us...
				// I think maybe pacman would do the same.
				// So in order to keep the display straight we need to reset the cursor position blindly
				// here.
				Console.CursorLeft = 1;
				return message?.TrimEnd ();
			}
        }
    }
}
