using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Xamarin.Android.BuildTools
{
	internal static class ProcessRunner
	{
		internal static int Run (Process process, Action<string, bool> consume, TimeSpan? processTimeout, TimeSpan outputTimeout)
		{
			var output = new ConcurrentQueue<(string Text, bool StandardError)> ();
			try {
				return RunAsync (process, output, processTimeout, outputTimeout).GetAwaiter ().GetResult ();
			} finally {
				// Parsers and MSBuild loggers must run serially on the calling thread, not in pipe readers.
				foreach (var line in output) {
					consume (line.Text, line.StandardError);
				}
			}
		}

		static async Task<int> RunAsync (Process process, ConcurrentQueue<(string Text, bool StandardError)> output, TimeSpan? processTimeout, TimeSpan outputTimeout)
		{
			using var readCancellation = new CancellationTokenSource ();
			using var exitCancellation = new CancellationTokenSource ();
			if (processTimeout.HasValue) {
				exitCancellation.CancelAfter (processTimeout.Value);
			}

			if (!process.Start ()) {
				throw new InvalidOperationException ($"Could not start '{process.StartInfo.FileName}'.");
			}

			using var stdout = process.StartInfo.RedirectStandardOutput ? process.StandardOutput : null;
			using var stderr = process.StartInfo.RedirectStandardError ? process.StandardError : null;
			var readers = Task.WhenAll (
				stdout == null ? Task.CompletedTask : ReadLinesAsync (stdout, false, output, readCancellation.Token),
				stderr == null ? Task.CompletedTask : ReadLinesAsync (stderr, true, output, readCancellation.Token));
			var exit = process.WaitForExitAsync (exitCancellation.Token);
			try {
				try {
					await Task.WhenAny (exit, readers).ConfigureAwait (false);
					if (readers.IsCompleted) {
						await readers.ConfigureAwait (false);
					}
					await exit.ConfigureAwait (false);
				} catch (OperationCanceledException ex) when (exitCancellation.IsCancellationRequested) {
					throw new TimeoutException ($"Process '{process.StartInfo.FileName} {process.StartInfo.Arguments}' failed to exit within {processTimeout}.", ex);
				}

				try {
					await readers.WaitAsync (outputTimeout).ConfigureAwait (false);
				} catch (TimeoutException ex) {
					throw new TimeoutException ($"Output from '{process.StartInfo.FileName} {process.StartInfo.Arguments}' did not close within {outputTimeout} after process exit.", ex);
				}

				return process.ExitCode;
			} finally {
				exitCancellation.Cancel ();
				readCancellation.Cancel ();
				try {
					if (!process.HasExited) {
						try {
							process.Kill (entireProcessTree: true);
						} catch (InvalidOperationException) when (process.HasExited) {
						}
						if (!process.WaitForExit (5000)) {
							throw new TimeoutException ($"Process '{process.StartInfo.FileName}' did not exit after being killed.");
						}
					}
				} finally {
					try {
						await Task.WhenAll (exit, readers).WaitAsync (TimeSpan.FromSeconds (5)).ConfigureAwait (false);
					} catch (OperationCanceledException) when (readCancellation.IsCancellationRequested) {
					}
				}
			}
		}

		static async Task ReadLinesAsync (StreamReader reader, bool standardError, ConcurrentQueue<(string Text, bool StandardError)> output, CancellationToken cancellationToken)
		{
			string line;
			while ((line = await reader.ReadLineAsync (cancellationToken).ConfigureAwait (false)) != null) {
				output.Enqueue ((line, standardError));
			}
		}
	}
}
