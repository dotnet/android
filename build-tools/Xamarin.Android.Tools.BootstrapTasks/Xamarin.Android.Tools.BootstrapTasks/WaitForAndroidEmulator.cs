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

		public override bool Execute ()
		{
			// The timeout is shared between both phases (boot_completed and
			// Package Manager readiness), so the total wall-clock time for
			// the entire method is bounded by a single Timeout period.
			var endTime = DateTime.UtcNow.AddMilliseconds (Timeout);

			// Phase 1: wait for sys.boot_completed
			while (DateTime.UtcNow < endTime && bootCompleted != 1) {
				base.Execute ();
				if (bootCompleted == 1)
					break;
				Thread.Sleep (3000);
			}

			if (bootCompleted != 1) {
				Log.LogError ($"Emulator '{AdbTarget}' did not finish launching in {Timeout} ms.");
				return false;
			}

			// Phase 2: wait for Package Manager to be ready.
			// sys.boot_completed fires before PM is fully initialized, and
			// subsequent adb checks that query PM can fail if we return too
			// early, causing a second emulator to be launched on the same
			// port (see CheckAdbTarget).
			Log.LogMessage (MessageImportance.Normal, "Boot completed, waiting for Package Manager...");
			while (DateTime.UtcNow < endTime && !pmReady) {
				base.Execute ();
				if (pmReady)
					break;
				Thread.Sleep (3000);
			}

			if (!pmReady) {
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

		protected override void ProcessStdout (string line)
		{
			if (string.IsNullOrEmpty (line))
				return;

			if (bootCompleted != 1) {
				if (int.TryParse (line, out int value))
					bootCompleted = value;
			} else {
				if (line.StartsWith ("package:", StringComparison.Ordinal))
					pmReady = true;
			}
		}

	}
}
