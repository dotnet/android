using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Xamarin.Android.Tools
{
	public static class ProcessUtils
	{
		static string[] ExecutableFileExtensions;

		static ProcessUtils ()
		{
			var pathExt     = Environment.GetEnvironmentVariable ("PATHEXT");
			var pathExts    = pathExt?.Split (new char [] { Path.PathSeparator }, StringSplitOptions.RemoveEmptyEntries) ?? new string [0];

			ExecutableFileExtensions    = pathExts;
		}

		public static async Task<int> StartProcess (ProcessStartInfo psi, TextWriter? stdout, TextWriter? stderr, CancellationToken cancellationToken, Action<Process>? onStarted = null)
		{
			cancellationToken.ThrowIfCancellationRequested ();
			psi.UseShellExecute = false;
			psi.RedirectStandardOutput |= stdout != null;
			psi.RedirectStandardError |= stderr != null;

			var process = new Process {
				StartInfo = psi,
				EnableRaisingEvents = true,
			};

			Task output = Task.FromResult (true);
			Task error = Task.FromResult (true);
			Task exit = WaitForExitAsync (process);
			using (process) {
				process.Start ();
				if (onStarted != null)
					onStarted (process);

				// If the token is cancelled while we're running, kill the process.
				// Otherwise once we finish the Task.WhenAll we can remove this registration
				// as there is no longer any need to Kill the process.
				//
				// We wrap `stdout` and `stderr` in syncronized wrappers for safety in case they
				// end up writing to the same buffer, or they are the same object.
				using (cancellationToken.Register (() => KillProcess (process))) {
					if (psi.RedirectStandardOutput)
						output = ReadStreamAsync (process.StandardOutput, TextWriter.Synchronized (stdout!));

					if (psi.RedirectStandardError)
						error = ReadStreamAsync (process.StandardError, TextWriter.Synchronized (stderr!));

					await Task.WhenAll (new [] { output, error, exit }).ConfigureAwait (false);
				}
				// If we invoke 'KillProcess' our output, error and exit tasks will all complete normally.
				// To protected against passing the user incomplete data we have to call
				// `cancellationToken.ThrowIfCancellationRequested ()` here.
				cancellationToken.ThrowIfCancellationRequested ();
				return process.ExitCode;
			}
		}

		static void KillProcess (Process p)
		{
			try {
				p.Kill ();
			} catch (InvalidOperationException) {
				// If the process has already exited this could happen
			}
		}

		static Task WaitForExitAsync (Process process)
		{
			var exitDone = new TaskCompletionSource<bool> ();
			process.Exited += (o, e) => exitDone.TrySetResult (true);
			return exitDone.Task;
		}

		static async Task ReadStreamAsync (StreamReader stream, TextWriter destination)
		{
			int read;
			var buffer = new char [4096];
			while ((read = await stream.ReadAsync (buffer, 0, buffer.Length).ConfigureAwait (false)) > 0)
				destination.Write (buffer, 0, read);
		}

		/// <summary>
		/// Executes an Android Sdk tool and returns a result. The result is based on a function of the command output.
		/// </summary>
		public static Task<TResult> ExecuteToolAsync<TResult> (string exe, Func<string, TResult> result, CancellationToken token, Action<Process>? onStarted = null)
		{
			var tcs = new TaskCompletionSource<TResult> ();

			var log = new StringWriter ();
			var error = new StringWriter ();

			var psi = new ProcessStartInfo (exe);
			psi.CreateNoWindow = true;
			psi.RedirectStandardInput = onStarted != null;

			var processTask = ProcessUtils.StartProcess (psi, log, error, token, onStarted);
			var exeName = Path.GetFileName (exe);

			processTask.ContinueWith (t => {
				var output = log.ToString ();
				var errorOutput = error.ToString ();
				log.Dispose ();
				error.Dispose ();

				if (t.IsCanceled) {
					tcs.TrySetCanceled ();
					return;
				}

				if (t.IsFaulted) {
					tcs.TrySetException (t.Exception?.Flatten ()?.InnerException ?? t.Exception!);
					return;
				}

				if (t.Result == 0) {
					tcs.TrySetResult (result != null ? result (output) : default (TResult)!);
				} else {
					var errorMessage = !string.IsNullOrEmpty (errorOutput) ? errorOutput : output;
					errorMessage = string.IsNullOrEmpty (errorMessage)
						? $"`{exeName}` returned non-zero exit code"
						: $"{t.Result} : {errorMessage}";

					tcs.TrySetException (new InvalidOperationException (errorMessage));
				}
			}, TaskContinuationOptions.ExecuteSynchronously);

			return tcs.Task;
		}

		internal static void Exec (ProcessStartInfo processStartInfo, DataReceivedEventHandler output, bool includeStderr = true)
		{
			processStartInfo.UseShellExecute         = false;
			processStartInfo.RedirectStandardInput   = false;
			processStartInfo.RedirectStandardOutput  = true;
			processStartInfo.RedirectStandardError   = true;
			processStartInfo.CreateNoWindow          = true;
			processStartInfo.WindowStyle             = ProcessWindowStyle.Hidden;

			var p = new Process () {
				StartInfo   = processStartInfo,
			};
			p.OutputDataReceived    += output;
			if (includeStderr) {
				p.ErrorDataReceived   += output;
			}

			using (p) {
				p.Start ();
				p.BeginOutputReadLine ();
				p.BeginErrorReadLine ();
				p.WaitForExit ();
			}
		}

		internal static IEnumerable<string> FindExecutablesInPath (string executable)
		{
			var path        = Environment.GetEnvironmentVariable ("PATH") ?? "";
			var pathDirs    = path.Split (new char[] { Path.PathSeparator }, StringSplitOptions.RemoveEmptyEntries);

			foreach (var dir in pathDirs) {
				foreach (var exe in FindExecutablesInDirectory (dir, executable)) {
					yield return exe;
				}
			}
		}

		internal static IEnumerable<string> FindExecutablesInDirectory (string dir, string executable)
		{
			if (!Directory.Exists (dir))
				yield break;
			foreach (var exe in ExecutableFiles (executable)) {
				string exePath;
				try {
					exePath = Path.Combine (dir, exe);
				} catch (ArgumentException) {
					continue;
				}
				if (File.Exists (exePath))
					yield return exePath;
			}
		}

		internal static IEnumerable<string> ExecutableFiles (string executable)
		{
			if (ExecutableFileExtensions == null || ExecutableFileExtensions.Length == 0) {
				yield return executable;
				yield break;
			}

			foreach (var ext in ExecutableFileExtensions)
				yield return Path.ChangeExtension (executable, ext);
			yield return executable;
		}
	}
}

