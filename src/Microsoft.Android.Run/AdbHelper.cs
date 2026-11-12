#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xamarin.Android.Tools;

static class AdbHelper
{
	public static ProcessStartInfo CreateStartInfo (string adbPath, string? adbTarget, string arguments)
	{
		var fullArguments = string.IsNullOrEmpty (adbTarget) ? arguments : $"{adbTarget} {arguments}";
		return new ProcessStartInfo {
			FileName = adbPath,
			Arguments = fullArguments,
			UseShellExecute = false,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			CreateNoWindow = true,
		};
	}

	/// <summary>
	/// Builds a <see cref="ProcessStartInfo"/> from a pre-split argument list.
	/// <see cref="ProcessStartInfo.ArgumentList"/> quotes each element for us, so
	/// values containing spaces or double quotes survive the trip through the
	/// operating system's command line parser untouched.
	/// </summary>
	public static ProcessStartInfo CreateStartInfo (string adbPath, string? adbTarget, IEnumerable<string> arguments)
	{
		var psi = new ProcessStartInfo {
			FileName = adbPath,
			UseShellExecute = false,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			CreateNoWindow = true,
		};
		if (!string.IsNullOrEmpty (adbTarget)) {
			// `adbTarget` is an already formatted switch such as `-s emulator-5554`.
			foreach (var part in adbTarget.Split (' ', StringSplitOptions.RemoveEmptyEntries))
				psi.ArgumentList.Add (part);
		}
		foreach (var argument in arguments)
			psi.ArgumentList.Add (argument);
		return psi;
	}

	public static async Task<(int ExitCode, string Output, string Error)> RunAsync (string adbPath, string? adbTarget, string arguments, CancellationToken cancellationToken, bool verbose = false)
	{
		var psi = CreateStartInfo (adbPath, adbTarget, arguments);

		if (verbose)
			Console.WriteLine ($"Running: adb {psi.Arguments}");

		cancellationToken.ThrowIfCancellationRequested ();
		using var process = new Process { StartInfo = psi };
		process.Start ();
		using var execution = CancellationTokenSource.CreateLinkedTokenSource (cancellationToken);
		using var reading = CancellationTokenSource.CreateLinkedTokenSource (cancellationToken);
		var exit = process.SafeHandle.WaitForExitOrKillOnCancellationAsync (execution.Token);
		var capture = process.ReadAllTextAsync (reading.Token);
		using var stdout = process.StandardOutput;
		using var stderr = process.StandardError;
		try {
			if (await Task.WhenAny (capture, exit).ConfigureAwait (false) == capture)
				await capture.ConfigureAwait (false);
			var status = await exit.WaitAsync (cancellationToken).ConfigureAwait (false);
			reading.CancelAfter (TimeSpan.FromSeconds (30));
			var result = await capture.ConfigureAwait (false);
			cancellationToken.ThrowIfCancellationRequested ();
			return (status.ExitCode, result.StandardOutput, result.StandardError);
		} catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) {
			throw new TimeoutException ("The adb client exited, but its redirected output did not close within 30 seconds.");
		} catch (OperationCanceledException ex) {
			throw new OperationCanceledException (ex.Message, ex, cancellationToken);
		} finally {
			reading.Cancel ();
			execution.Cancel ();
			await WaitForCommandCompletionAsync (capture, exit, reading.Token).ConfigureAwait (false);
		}
	}

	static async Task WaitForCommandCompletionAsync (Task capture, Task exit, CancellationToken readingToken)
	{
		try {
			try {
				await capture.ConfigureAwait (false);
			} catch (OperationCanceledException) when (readingToken.IsCancellationRequested) {
				// Stop client reads without terminating a persistent ADB server.
			}
		} finally {
			await exit.WaitAsync (TimeSpan.FromSeconds (5)).ConfigureAwait (false);
		}
	}
}
