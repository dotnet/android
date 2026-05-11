using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using NUnit.Framework;

namespace Xamarin.ProjectTools
{
	public static class ProcessExtensions
	{
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
			pids.Add (process.Id);

			foreach (int pid in pids) {
				if (kill (pid, SIGINT) != 0) {
					int errno = Marshal.GetLastPInvokeError ();
					// ESRCH (3) = process already exited, expected in race conditions
					if (errno != 3) {
						Console.Error.WriteLine ($"kill({pid}, SIGINT) failed with errno {errno}");
					}
				}
			}
		}

		static void GetDescendantPids (int parentPid, List<int> pids)
		{
			var psi = new ProcessStartInfo ("pgrep", $"-P {parentPid}") {
				RedirectStandardOutput = true,
				UseShellExecute = false,
				CreateNoWindow = true,
			};
			using var p = Process.Start (psi);
			if (p == null) {
				return;
			}
			string output = p.StandardOutput.ReadToEnd ();
			p.WaitForExit ();

			foreach (string line in output.Split ('\n', StringSplitOptions.RemoveEmptyEntries)) {
				if (int.TryParse (line.Trim (), out int childPid)) {
					GetDescendantPids (childPid, pids);
					pids.Add (childPid);
				}
			}
		}

		[DllImport ("libc", SetLastError = true)]
		static extern int kill (int pid, int sig);

		const int SIGINT = 2;
	}
}
