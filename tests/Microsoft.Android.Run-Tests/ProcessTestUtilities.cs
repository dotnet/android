// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Android.Run.Tests
{
	internal static class ProcessTestUtilities
	{
		public static async Task<(int ExitCode, string Output, string Error)> RunProcessAsync (ProcessStartInfo startInfo, TimeSpan timeout)
		{
			using var process = StartProcess (startInfo, out var output, out var error, out var stdoutClosed, out var stderrClosed, out var exitTask);

			try {
				await Task.WhenAll (stdoutClosed, stderrClosed, exitTask).WaitAsync (timeout);
				return (process.ExitCode, output.ToString (), error.ToString ());
			} catch (Exception) {
				TryKillProcessTree (process);
				throw;
			}
		}

		public static Process StartProcess (ProcessStartInfo startInfo, out StringBuilder output, out StringBuilder error, out Task stdoutClosed, out Task stderrClosed, out Task exitTask)
		{
			startInfo.UseShellExecute = false;
			startInfo.RedirectStandardOutput = true;
			startInfo.RedirectStandardError = true;

			var stdoutDone = new TaskCompletionSource (TaskCreationOptions.RunContinuationsAsynchronously);
			var stderrDone = new TaskCompletionSource (TaskCreationOptions.RunContinuationsAsynchronously);
			var exitDone = new TaskCompletionSource (TaskCreationOptions.RunContinuationsAsynchronously);
			var capturedOutput = new StringBuilder ();
			var capturedError = new StringBuilder ();

			var process = new Process {
				StartInfo = startInfo,
				EnableRaisingEvents = true,
			};
			process.OutputDataReceived += (_, e) => {
				if (e.Data == null) {
					stdoutDone.TrySetResult ();
					return;
				}
				capturedOutput.AppendLine (e.Data);
			};
			process.ErrorDataReceived += (_, e) => {
				if (e.Data == null) {
					stderrDone.TrySetResult ();
					return;
				}
				capturedError.AppendLine (e.Data);
			};
			process.Exited += (_, _) => exitDone.TrySetResult ();

			process.Start ();
			process.BeginOutputReadLine ();
			process.BeginErrorReadLine ();

			output = capturedOutput;
			error = capturedError;
			stdoutClosed = stdoutDone.Task;
			stderrClosed = stderrDone.Task;
			exitTask = exitDone.Task;
			return process;
		}

		public static void TryKillProcessTree (Process process)
		{
			try {
				if (!process.HasExited)
					process.Kill (entireProcessTree: true);
			} catch (InvalidOperationException) {
			}
		}
	}
}
