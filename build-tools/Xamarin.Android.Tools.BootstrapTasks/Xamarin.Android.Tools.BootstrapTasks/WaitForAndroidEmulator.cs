using System;
using System.Collections.Generic;
using System.Threading;

using Microsoft.Build.Framework;

namespace Xamarin.Android.Tools.BootstrapTasks
{
	public class WaitForAndroidEmulator : Adb
	{
		int bootCompleted = -1;
		bool pmReady;
		bool checkingPackageManager;

		protected override bool ExecuteCommands (CancellationToken cancellationToken)
		{
			// Include each ADB invocation and retry delay in the shared boot/PM deadline.
			using var deadline = CancellationTokenSource.CreateLinkedTokenSource (cancellationToken);
			if (Timeout > 0)
				deadline.CancelAfter (Timeout);
			try {
				while (bootCompleted != 1) {
					if (!base.ExecuteCommands (deadline.Token))
						return false;
					if (bootCompleted != 1)
						System.Threading.Tasks.Task.Delay (3000, deadline.Token).GetAwaiter ().GetResult ();
				}

				// sys.boot_completed can precede Package Manager readiness.
				Log.LogMessage (MessageImportance.Normal, "Boot completed, waiting for Package Manager...");
				while (!pmReady) {
					if (!base.ExecuteCommands (deadline.Token))
						return false;
					if (!pmReady)
						System.Threading.Tasks.Task.Delay (3000, deadline.Token).GetAwaiter ().GetResult ();
				}
			} catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && deadline.IsCancellationRequested) {
				if (bootCompleted != 1)
					Log.LogError ($"Emulator '{AdbTarget}' did not finish launching in {Timeout} ms.");
				else
					Log.LogError ($"Emulator '{AdbTarget}' Package Manager did not become ready in {Timeout} ms.");
				return false;
			}

			Log.LogMessage (MessageImportance.Normal, "Package Manager is ready.");

			return !Log.HasLoggedErrors;
		}

		protected override List <CommandInfo> GenerateCommandArguments ()
		{
			return new List <CommandInfo> {
				new CommandInfo {
					ArgumentsString = $"{AdbTarget} shell getprop sys.boot_completed",
					ShouldRun = () => bootCompleted != 1,
				},
				new CommandInfo {
					ArgumentsString = $"{AdbTarget} shell pm path com.android.shell",
					IgnoreExitCode = true,
					LogIgnoredExitCodeAsWarning = false,
					MergeStdoutAndStderr = false,
					ShouldRun = () => bootCompleted == 1,
				},
			};
		}

		protected override void BeforeCommand (int commandIndex, CommandInfo info)
			=> checkingPackageManager = commandIndex == 1;

		protected override void AfterCommand (int commandIndex, CommandInfo info)
		{
			if (checkingPackageManager && info.ExitCode != 0)
				pmReady = false;
		}

		protected override void ProcessStdout (string line)
		{
			if (string.IsNullOrEmpty (line))
				return;

			if (!checkingPackageManager) {
				if (int.TryParse (line, out int value))
					bootCompleted = value;
			} else {
				if (line.StartsWith ("package:", StringComparison.Ordinal))
					pmReady = true;
			}
		}

	}
}
