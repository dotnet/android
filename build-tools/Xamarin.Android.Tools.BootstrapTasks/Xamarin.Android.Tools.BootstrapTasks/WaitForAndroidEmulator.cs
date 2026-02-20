using System;
using System.Collections.Generic;
using System.Threading;

using Microsoft.Build.Framework;

namespace Xamarin.Android.Tools.BootstrapTasks
{
	public class WaitForAndroidEmulator : Adb
	{
		const int StateCheckBoot = 0;
		const int StateCheckPM = 1;

		int bootCompleted = -1;
		bool pmReady;
		int currentState = -1;
		bool waitingForPM;

		public override bool Execute ()
		{
			var endTime = DateTime.UtcNow.AddMilliseconds (Timeout);

			// Phase 1: wait for sys.boot_completed
			while (DateTime.UtcNow < endTime && bootCompleted != 1) {
				base.Execute ();
				Thread.Sleep (3000);
			}

			if (bootCompleted != 1) {
				Log.LogError ($"Emulator '{AdbTarget}' did not finish launching in {Timeout} ms.");
				return !Log.HasLoggedErrors;
			}

			// Phase 2: wait for Package Manager to be ready.
			// sys.boot_completed fires before PM is fully initialized, and
			// subsequent adb checks that query PM can fail if we return too
			// early, causing a second emulator to be launched on the same
			// port (see CheckAdbTarget).
			waitingForPM = true;
			Log.LogMessage (MessageImportance.Normal, "Boot completed, waiting for Package Manager...");
			while (DateTime.UtcNow < endTime && !pmReady) {
				pmReady = false;
				base.Execute ();
				if (pmReady)
					break;
				Thread.Sleep (3000);
			}

			if (!pmReady) {
				Log.LogError ($"Emulator '{AdbTarget}' Package Manager did not become ready in {Timeout} ms.");
			} else {
				Log.LogMessage (MessageImportance.Normal, "Package Manager is ready.");
			}

			return !Log.HasLoggedErrors;
		}

		protected override List <CommandInfo> GenerateCommandArguments ()
		{
			return new List <CommandInfo> {
				new CommandInfo {
					ArgumentsString = $"{AdbTarget} shell getprop sys.boot_completed",
					ShouldRun = () => !waitingForPM,
				},
				new CommandInfo {
					ArgumentsString = $"{AdbTarget} shell pm path com.android.shell",
					IgnoreExitCode = true,
					LogIgnoredExitCodeAsWarning = false,
					MergeStdoutAndStderr = false,
					ShouldRun = () => waitingForPM,
				},
			};
		}

		protected override void BeforeCommand (int commandIndex, CommandInfo info)
		{
			currentState = commandIndex;
		}

		protected override void ProcessStdout (string line)
		{
			if (string.IsNullOrEmpty (line))
				return;

			switch (currentState) {
				case StateCheckBoot:
					if (int.TryParse (line, out int bootCompletedPropValue))
						bootCompleted = bootCompletedPropValue;
					break;

				case StateCheckPM:
					if (line.StartsWith ("package:", StringComparison.OrdinalIgnoreCase))
						pmReady = true;
					break;
			}
		}

		protected override void ProcessStderr (string line)
		{
			// PM errors on stderr should not mark the target as ready
		}
	}
}
