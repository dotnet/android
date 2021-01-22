using System;
using System.Collections.Generic;
using System.Threading;

namespace Xamarin.Android.Tools.BootstrapTasks
{
	public class WaitForAndroidEmulator : Adb
	{
		int bootCompleted = -1;

		public override bool Execute ()
		{
			var endTime = DateTime.UtcNow.AddMilliseconds (Timeout);
			while (DateTime.UtcNow < endTime && bootCompleted != 1) {
				base.Execute ();
				Thread.Sleep (3000);
			}

			if (bootCompleted != 1) {
				Log.LogError ($"Emulator '{AdbTarget}' did not finish launching in {Timeout} ms.");
				return false;
			} else {
				return true;
			}
		}

		protected override List <CommandInfo> GenerateCommandArguments ()
		{
			return new List <CommandInfo> {
				new CommandInfo {
					ArgumentsString = $"{AdbTarget} shell getprop sys.boot_completed",
				},
			};
		}

		protected override void ProcessStdout (string line)
		{
			if (string.IsNullOrEmpty (line))
				return;

			if (!int.TryParse (line, out int bootCompletedPropValue))
				return;

			bootCompleted = bootCompletedPropValue;
		}
	}
}
