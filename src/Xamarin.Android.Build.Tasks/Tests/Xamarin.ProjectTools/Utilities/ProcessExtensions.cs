using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
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
		static readonly ConditionalWeakTable<Process, ProcessTreeTracker> processTreeTrackers = new ();

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
			try {
				process.Kill (entireProcessTree: true);
			} catch (InvalidOperationException) when (process.HasExited) {
			}
			if (processTreeTrackers.TryGetValue (process, out var tracker))
				tracker.StopAndKillDescendants ();
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

		public static void TrackDescendants (this Process process)
		{
			if (!OperatingSystem.IsWindows ())
				processTreeTrackers.Add (process, new ProcessTreeTracker (process));
		}

		public static bool WaitForExitAndOutput (this Process process, Task outputTask, CancellationTokenSource cts, TimeSpan timeout)
		{
			cts.CancelAfter (timeout);
			try {
				Task.WhenAll (process.WaitForExitAsync (cts.Token), outputTask).GetAwaiter ().GetResult ();
				if (processTreeTrackers.TryGetValue (process, out var tracker))
					tracker.StopTracking ();
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

		sealed class ProcessTreeTracker
		{
			readonly Process process;
			readonly CancellationTokenSource cts = new ();
			readonly Dictionary<int, SafeProcessHandle> handles = new ();
			readonly Task trackingTask;
			bool stopped;

			public ProcessTreeTracker (Process process)
			{
				this.process = process;
				trackingTask = Task.Run (TrackDescendantsAsync);
			}

			async Task TrackDescendantsAsync ()
			{
				while (!cts.IsCancellationRequested && !process.HasExited) {
					var pids = new List<int> ();
					GetDescendantPids (process.Id, pids);
					foreach (int pid in pids) {
						if (!handles.ContainsKey (pid) && SafeProcessHandle.TryOpen (pid, out var handle))
							handles.Add (pid, handle);
					}
					await Task.Delay (50);
				}
			}

			public void StopAndKillDescendants ()
			{
				Stop (killDescendants: true);
			}

			public void StopTracking ()
			{
				Stop (killDescendants: false);
			}

			void Stop (bool killDescendants)
			{
				if (stopped)
					return;
				stopped = true;
				cts.Cancel ();
				try {
					try {
						trackingTask.GetAwaiter ().GetResult ();
					} finally {
						foreach (var handle in handles.Values) {
							try {
								if (killDescendants && !OperatingSystem.IsWindows ())
									handle.Signal (PosixSignal.SIGKILL);
							} catch (InvalidOperationException) {
							}
						}
					}
				} finally {
					foreach (var handle in handles.Values)
						handle.Dispose ();
					handles.Clear ();
					cts.Dispose ();
				}
			}
		}
	}
}
