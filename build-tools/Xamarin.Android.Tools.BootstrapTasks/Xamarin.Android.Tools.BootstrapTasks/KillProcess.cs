using System;
using System.Diagnostics;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tools.BootstrapTasks
{
	public class KillProcess : Task
	{
		[Required]
		public                  int             ProcessId       {get; set;}

		public override bool Execute ()
		{
			Log.LogMessage (MessageImportance.Low, $"Task {nameof (KillProcess)}");
			Log.LogMessage (MessageImportance.Low, $"  {nameof (ProcessId)}: {ProcessId}");
			if (ProcessId <= 0) {
				Log.LogError ($"Invalid process ID: {ProcessId}");
				return false;
			}

			Process process;
			try {
				process = Process.GetProcessById (ProcessId);
			} catch (ArgumentException) {
				Log.LogMessage (MessageImportance.Low, $"Process {ProcessId} has already exited.");
				return !Log.HasLoggedErrors;
			}
			using (process) {
				try {
					if (!process.HasExited)
						process.Kill ();
				} catch (InvalidOperationException) when (process.HasExited) {
				}
				// The recorded PID can belong to another task host, so don't request its exit status.
				if (!process.WaitForExit (5000)) {
					Log.LogError ($"Process {ProcessId} did not exit after termination.");
					return false;
				}
			}

			return !Log.HasLoggedErrors;
		}
	}
}
