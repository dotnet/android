using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

using Xamarin.Android.BuildTools.PrepTasks;

namespace Xamarin.Android.Tools.BootstrapTasks
{
	public class RunAndroidEmulatorCheckBootTimes : Task
	{
		public string CheckBootTimesPath { get; set; }
		public string DeviceName { get; set; } = "XamarinAndroidTestRunner";

		public override bool Execute ()
		{
			Log.LogMessage (MessageImportance.High, $"Task {nameof (RunAndroidEmulatorCheckBootTimes)}");
			Log.LogMessage (MessageImportance.High, $"     {nameof (CheckBootTimesPath)}: {CheckBootTimesPath}");
			Log.LogMessage (MessageImportance.High, $"     {nameof (DeviceName)}: {DeviceName}");

			var fileInfo = new FileInfo (CheckBootTimesPath);
			Log.LogMessage (MessageImportance.High, $"CheckBootTimesPath:  {fileInfo.FullName}");

			if (!fileInfo.Exists) {
				Log.LogError ($"Unable to find {fileInfo.FullName}.");
				return !Log.HasLoggedErrors;
			}

			bool finishAsExpected = false;
			var success = RunProcess (
				fileInfo.FullName,
				$"--devicename {DeviceName}",
				5 * 60000,
				(string data, ManualResetEvent mre) => {
					Log.LogMessage (MessageImportance.High, $"CheckBootTimes ({DateTime.UtcNow}): {data}");
					if (!string.IsNullOrWhiteSpace (data) && data.IndexOf ("Check-Boot-Times Done.", StringComparison.OrdinalIgnoreCase) != -1) {
						finishAsExpected = true;
						mre.Set ();
					}

					return true;
				});

			if (!success || !finishAsExpected) {
				Log.LogError ("check-boot-times did not run as expected, please see logs for more info.");
				CloseProcesses ();
				return false;
			}

			Log.LogMessage (MessageImportance.High, $"Done with RunAndroidEmulatorCheckBootTimes task.");
			return true;
		}


		static void CloseProcesses ()
		{
			// Try to close emulator
			for (int i = 0; i < 10; i++) {
				try {
					var proc = GetProcess("emulator");
					if (proc == null) {
						proc = GetProcess("qemu");

						if (proc == null) {
							break;
						}
					}

					proc.WaitForExit (5000);

					// Close process by sending a close message to its main window.
					proc.CloseMainWindow ();

					// Free resources associated with process.
					proc.Close ();
				} catch (Exception e) {
					Console.WriteLine ($"Unable to close emulator. {e.Message}");
				}
			}

			// Emulator was not close, try to kill process
			for (int i = 0; i < 10; i++) {
				try {
					var proc = GetProcess("emulator");
					if (proc == null) {
						proc = GetProcess("qemu");

						if (proc == null) {
							break;
						}
					}

					proc.WaitForExit (5000);

					proc.Kill ();
				} catch (Exception e) {
					Console.WriteLine ($"Unable to kill emulator. {e.Message}");
				}
			}

			// kill check-boot-times process if needed
			for (int i = 0; i < 10; i++) {
				try {
					var proc = GetProcess("check-boot-times");
					if (proc == null) {
						break;
					}

					proc.WaitForExit (5000);

					proc.Kill ();
				} catch (Exception e) {
					Console.WriteLine ($"Unable to kill check-boot-times. {e.Message}");
				}
			}
		}

		static bool RunProcess (string filename, string arguments, int timeout, Func<string, ManualResetEvent, bool> validation, Action processTimeout = null)
		{
			using (var process = new Process ()) {

				process.StartInfo.FileName = filename;
				process.StartInfo.Arguments = arguments;
				process.StartInfo.UseShellExecute = false;
				process.StartInfo.CreateNoWindow = true;
				process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
				process.StartInfo.RedirectStandardOutput = true;
				process.StartInfo.RedirectStandardOutput = true;
				process.StartInfo.RedirectStandardError = true;
				process.EnableRaisingEvents = true;

				var mre = new ManualResetEvent (false /* -> nonsignaled */);

				bool error = false;
				void dataReceived (object sender, DataReceivedEventArgs args)
				{
					if (!validation (args.Data, mre)) {
						error = true;
						mre.Set ();
					}
				}

				process.OutputDataReceived += dataReceived;
				process.ErrorDataReceived += dataReceived;
				process.Exited += (s, e) => {
					mre.WaitOne (1000);
					mre.Set ();
				};

				process.Start ();

				process.BeginOutputReadLine ();
				process.BeginErrorReadLine ();

				if (!mre.WaitOne (timeout) || !process.WaitForExit (timeout)) {
					processTimeout?.Invoke ();
					error = true;
				}

				process.CancelOutputRead ();
				process.CancelErrorRead ();

				if (error || !process.HasExited || process.ExitCode != 0) {
					return false;
				}
			}

			return true;
		}

		static Process GetProcess (string processName)
		{
			return Process.GetProcesses ().FirstOrDefault (p => p.ProcessName.IndexOf (processName, StringComparison.OrdinalIgnoreCase) != -1);
		}
	}
}
