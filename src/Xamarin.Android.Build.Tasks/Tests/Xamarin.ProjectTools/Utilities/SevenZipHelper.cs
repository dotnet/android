using System;
using System.IO;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Xamarin.ProjectTools {
	public class SevenZipHelper : IDisposable {
		public string ArchivePath { get; private set; }

		public SevenZipHelper (string archivePath)
		{
			ArchivePath = archivePath;
		}

		public bool ExtractAll (string destinationDir)
		{
			return ExtractAllAsync (destinationDir).GetAwaiter ().GetResult ();
		}

		async Task<bool> ExtractAllAsync (string destinationDir)
		{
			var psi = new ProcessStartInfo ("7z", ["x", ArchivePath, $"-o{destinationDir}"]) {
				CreateNoWindow = true,
				UseShellExecute = false,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				InheritedHandles = [],
			};
			using var executionDeadline = new CancellationTokenSource (TimeSpan.FromMinutes (15));
			using var outputDeadline = CancellationTokenSource.CreateLinkedTokenSource (executionDeadline.Token);
			using var process = Process.Start (psi) ?? throw new InvalidOperationException ("Failed to start '7z'.");
			async Task LimitOutputDrainAsync ()
			{
				await process.WaitForExitAsync (executionDeadline.Token).ConfigureAwait (false);
				executionDeadline.CancelAfter (Timeout.InfiniteTimeSpan);
				outputDeadline.CancelAfter (TimeSpan.FromSeconds (2));
			}
			var exited = LimitOutputDrainAsync ();
			try {
				await foreach (var line in process.ReadAllLinesAsync (outputDeadline.Token).ConfigureAwait (false))
					Console.WriteLine (line.Content);
				await exited.ConfigureAwait (false);
			} catch (OperationCanceledException) when (executionDeadline.IsCancellationRequested) {
				Console.Error.WriteLine ("7z timed out after 15 minutes.");
				return false;
			} catch (OperationCanceledException) when (outputDeadline.IsCancellationRequested) {
				Console.Error.WriteLine ("7z exited with redirected output still open after 2 seconds.");
			} finally {
				using var stdoutReader = process.StandardOutput;
				using var stderrReader = process.StandardError;
				executionDeadline.Cancel ();
				try {
					await exited.ConfigureAwait (false);
				} catch (OperationCanceledException) when (executionDeadline.IsCancellationRequested) {
					// The execution deadline owns this exit observer.
				}
				if (!process.HasExited) {
					try {
						process.Kill (entireProcessTree: true);
					} catch (InvalidOperationException) when (process.HasExited) {
						// The process exited before the kill request.
					}
					if (!process.WaitForExit (30000))
						throw new TimeoutException ($"7z process {process.Id} did not exit after termination.");
				}
			}
			return process.ExitCode == 0;
		}

		public void Dispose ()
		{
		}

		public static SevenZipHelper Open (string path, FileMode fileMode)
		{
			return new SevenZipHelper (path);
		}
	}
}
