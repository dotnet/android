using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;
using NUnit.Framework;

namespace Xamarin.ProjectTools
{
	public static class ProcessExtensions
	{
		const int SIGKILL = 9;
		const int EPERM = 1;
		const int ESRCH = 3;
		const string ProcessGroupLauncher = "/usr/bin/perl";
		// Moves the launcher into a new process group, then execs the target so it keeps the
		// started PID. Every descendant inherits that group, even after reparenting.
		const string ProcessGroupScript = "setpgrp(0, 0) or die $!; exec {$ARGV[0]} @ARGV or die $!";

		static readonly ConditionalWeakTable<Process, object> processGroups = new ();

		[DllImport ("libc", SetLastError = true)]
		static extern int kill (int pid, int sig);

		/// <summary>
		/// Sets environment variables on ProcessStartInfo with retries to work around a Mono Legacy bug.
		/// https://github.com/mono/mono/issues/16607
		/// </summary>
		public static void SetEnvironmentVariable (this ProcessStartInfo psi, string key, string value)
		{
			var retries = 3;

			while (retries-- > 0) {
				try {
					psi.EnvironmentVariables [key] = value;
					return;
				} catch (ArgumentNullException) {
					// Ignore exception
					// Hit thread safety issue, wait a tiny bit and then retry.
					Thread.Sleep (100);
				} catch (NullReferenceException) {
					// Ignore exception
					// Hit thread safety issue, wait a tiny bit and then retry.
					Thread.Sleep (100);
				}
			}

			Assert.Inconclusive ("Could not set ProcessStartInfo environment variable.");
		}

		public static void StopAndDrainOutput (this Process process, CancellationTokenSource cts, Task outputTask)
		{
			KillProcessGroup (process);
			try {
				process.Kill (entireProcessTree: true);
			} catch (InvalidOperationException) when (process.HasExited) {
			}
			bool exited = process.WaitForExit (30_000);
			if (!exited)
				cts.Cancel ();

			bool readerCancelled = false;
			try {
				outputTask.WaitAsync (TimeSpan.FromSeconds (30)).GetAwaiter ().GetResult ();
			} catch (OperationCanceledException) when (cts.IsCancellationRequested) {
				readerCancelled = true;
			} catch (TimeoutException) {
				cts.Cancel ();
				try {
					outputTask.WaitAsync (TimeSpan.FromSeconds (30)).GetAwaiter ().GetResult ();
				} catch (OperationCanceledException) when (cts.IsCancellationRequested) {
					readerCancelled = true;
				}
			}
			if (!exited)
				throw new TimeoutException ("The process did not exit after being stopped.");
			if (!readerCancelled)
				outputTask.GetAwaiter ().GetResult ();
		}

		/// <summary>
		/// Starts the process as the leader of a new process group on Unix, so that
		/// <see cref="StopAndDrainOutput"/> can kill descendants even after their parents exit.
		/// </summary>
		public static void StartInProcessGroup (this Process process)
		{
			if (OperatingSystem.IsWindows ()) {
				process.Start ();
				return;
			}
			if (!File.Exists (ProcessGroupLauncher))
				throw new InvalidOperationException ($"'{ProcessGroupLauncher}' is required to start a process in a new process group.");

			var psi = process.StartInfo;
			string fileName = psi.FileName;
			psi.FileName = ProcessGroupLauncher;
			if (psi.ArgumentList.Count > 0) {
				psi.ArgumentList.Insert (0, "-e");
				psi.ArgumentList.Insert (1, ProcessGroupScript);
				psi.ArgumentList.Insert (2, "--");
				psi.ArgumentList.Insert (3, fileName);
			} else {
				if (fileName.Contains ('"'))
					throw new ArgumentException ($"The process file name must not contain quotes: {fileName}", nameof (process));
				psi.Arguments = $"-e \"{ProcessGroupScript}\" -- \"{fileName}\" {psi.Arguments}";
			}
			process.Start ();
			processGroups.Add (process, process.Id);
		}

		static void KillProcessGroup (Process process)
		{
			if (!processGroups.TryGetValue (process, out var pgid))
				return;
			if (kill (-(int) pgid, SIGKILL) != 0) {
				int errno = Marshal.GetLastPInvokeError ();
				// macOS reports EPERM when every remaining group member is an unreaped zombie.
				if (errno != ESRCH && !(errno == EPERM && OperatingSystem.IsMacOS ()))
					throw new InvalidOperationException ($"Failed to kill process group {pgid}: errno {errno}.");
			}
		}

		public static bool WaitForExitAndOutput (this Process process, Task outputTask, CancellationTokenSource cts, TimeSpan timeout)
		{
			cts.CancelAfter (timeout);
			try {
				Task.WhenAll (process.WaitForExitAsync (cts.Token), outputTask).GetAwaiter ().GetResult ();
				return true;
			} catch (OperationCanceledException) when (cts.IsCancellationRequested) {
				process.StopAndDrainOutput (cts, outputTask);
				return false;
			} catch {
				process.StopAndDrainOutput (cts, outputTask);
				throw;
			}
		}

		/// <summary>
		/// Sends Ctrl+C (SIGINT) to the specified process and all its descendants.
		/// This simulates what a terminal does on Ctrl+C: send SIGINT to the entire
		/// foreground process group. Without this, child processes (e.g. Microsoft.Android.Run
		/// launched by dotnet run) would not receive the signal.
		/// Currently only supported on Unix/macOS; throws PlatformNotSupportedException on Windows.
		/// </summary>
		/// <remarks>
		/// See dotnet/sdk's NativeMethods.cs and GivenDotnetRunIsInterrupted.cs for the pattern used here.
		/// </remarks>
		public static void SendCtrlC (this Process process)
		{
			if (OperatingSystem.IsWindows ()) {
				throw new PlatformNotSupportedException ("SendCtrlC is not yet implemented on Windows.");
			}

			// Collect all descendant PIDs first, then send SIGINT to all of them.
			var pids = new List<int> ();
			GetDescendantPids (process.Id, pids);
			foreach (int pid in pids) {
				// The process may already have exited, which is expected in race conditions.
				if (SafeProcessHandle.TryOpen (pid, out var handle)) {
					using (handle)
						handle.Signal (PosixSignal.SIGINT);
				}
			}
			process.SafeHandle.Signal (PosixSignal.SIGINT);
		}

		static void GetDescendantPids (int parentPid, List<int> pids)
		{
			var result = Process.RunAndCaptureText ("pgrep", ["-P", parentPid.ToString (CultureInfo.InvariantCulture)]);
			foreach (string line in result.StandardOutput.Split ('\n', StringSplitOptions.RemoveEmptyEntries)) {
				if (int.TryParse (line.Trim (), out int childPid)) {
					GetDescendantPids (childPid, pids);
					pids.Add (childPid);
				}
			}
		}
	}
}
