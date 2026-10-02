using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

#if !NET5_0_OR_GREATER
using System.Runtime.InteropServices;
#endif

namespace Xamarin.Android.Tools
{
	public static class ProcessUtils
	{
		static string[] ExecutableFileExtensions;
		static readonly TimeSpan OutputDrainTimeout = TimeSpan.FromSeconds (30);
		static readonly TimeSpan ShutdownTimeout = TimeSpan.FromSeconds (5);

		static ProcessUtils ()
		{
			var pathExt     = Environment.GetEnvironmentVariable (EnvironmentVariableNames.PathExt);
			var pathExts    = pathExt?.Split (new char [] { Path.PathSeparator }, StringSplitOptions.RemoveEmptyEntries) ?? new string [0];

			ExecutableFileExtensions    = pathExts;
		}

		/// Backward-compatible overload matching the original shipped API (without environmentVariables).
#pragma warning disable RS0027 // Public API with optional parameter(s) should have the most parameters amongst its public overloads
		public static Task<int> StartProcess (ProcessStartInfo psi, TextWriter? stdout, TextWriter? stderr, CancellationToken cancellationToken, Action<Process>? onStarted = null)
#pragma warning restore RS0027
		{
			return StartProcess (psi, stdout, stderr, cancellationToken, null, onStarted);
		}

		/// Convenience overload accepting environmentVariables without requiring onStarted.
		public static Task<int> StartProcess (ProcessStartInfo psi, TextWriter? stdout, TextWriter? stderr, CancellationToken cancellationToken, IDictionary<string, string>? environmentVariables)
		{
			return StartProcess (psi, stdout, stderr, cancellationToken, environmentVariables, null);
		}

		public static async Task<int> StartProcess (ProcessStartInfo psi, TextWriter? stdout, TextWriter? stderr, CancellationToken cancellationToken, IDictionary<string, string>? environmentVariables, Action<Process>? onStarted)
		{
			if (psi == null)
				throw new ArgumentNullException (nameof (psi));
			cancellationToken.ThrowIfCancellationRequested ();
			if (psi.RedirectStandardOutput && stdout == null)
				throw new ArgumentException ("A writer is required for redirected standard output.", nameof (stdout));
			if (psi.RedirectStandardError && stderr == null)
				throw new ArgumentException ("A writer is required for redirected standard error.", nameof (stderr));

			psi.UseShellExecute = false;
			psi.RedirectStandardOutput |= stdout != null;
			psi.RedirectStandardError |= stderr != null;

			if (environmentVariables != null) {
				foreach (var kvp in environmentVariables)
					psi.EnvironmentVariables [kvp.Key] = kvp.Value;
			}

			using var process = new Process {
				StartInfo = psi,
				EnableRaisingEvents = true,
			};
			Task exit = WaitForExitAsync (process);
			process.Start ();
			using var input = psi.RedirectStandardInput ? process.StandardInput : null;
			using var outputReader = psi.RedirectStandardOutput ? process.StandardOutput : null;
			using var errorReader = psi.RedirectStandardError ? process.StandardError : null;
			using var readCancellation = CancellationTokenSource.CreateLinkedTokenSource (cancellationToken);

			var outputWriter = stdout == null ? null : TextWriter.Synchronized (stdout);
			var errorWriter = ReferenceEquals (stdout, stderr)
				? outputWriter
				: stderr == null ? null : TextWriter.Synchronized (stderr);
			Task output = Task.CompletedTask;
			Task error = Task.CompletedTask;
			if (outputReader != null && outputWriter != null)
				output = Task.Run (() => ReadStreamAsync (outputReader, outputWriter, readCancellation.Token));
			if (errorReader != null && errorWriter != null)
				error = Task.Run (() => ReadStreamAsync (errorReader, errorWriter, readCancellation.Token));

			var started = new TaskCompletionSource<bool> (TaskCreationOptions.RunContinuationsAsynchronously);
			var completion = CompleteProcessAsync (process, exit, output, error, started.Task, readCancellation, cancellationToken);
			try {
				onStarted?.Invoke (process);
				started.TrySetResult (true);
			} catch (Exception ex) {
				started.TrySetException (ex);
			}
			return await completion.ConfigureAwait (false);
		}

		static void KillProcess (Process p)
		{
			if (p.HasExited)
				return;
			try {
				p.Kill ();
			} catch (InvalidOperationException) when (p.HasExited) {
				// The owned root exited between checking and terminating it.
			}
		}

		static Task WaitForExitAsync (Process process)
		{
			var exitDone = new TaskCompletionSource<bool> (TaskCreationOptions.RunContinuationsAsynchronously);
			process.Exited += (o, e) => exitDone.TrySetResult (true);
			return exitDone.Task;
		}

		static async Task<int> CompleteProcessAsync (Process process, Task exit, Task output, Task error, Task started,
			CancellationTokenSource readCancellation, CancellationToken cancellationToken)
		{
			var failures = new List<Exception> ();
			using var timerCancellation = new CancellationTokenSource ();
			var canceled = new TaskCompletionSource<bool> (TaskCreationOptions.RunContinuationsAsynchronously);
			using (cancellationToken.Register (() => canceled.TrySetResult (true))) {
				try {
					var pending = new List<Task> { output, error, started, exit };
					Task? drainTimeout = null;
					while (pending.Count > 0) {
						var waits = new List<Task> (pending) { canceled.Task };
						if (drainTimeout != null && (!output.IsCompleted || !error.IsCompleted))
							waits.Add (drainTimeout);
						var completed = await Task.WhenAny (waits).ConfigureAwait (false);
						if (completed == canceled.Task || (completed.IsCanceled && cancellationToken.IsCancellationRequested))
							cancellationToken.ThrowIfCancellationRequested ();
						if (completed == drainTimeout)
							throw new TimeoutException ($"Process '{process.StartInfo.FileName}' exited, but its redirected output did not close within {OutputDrainTimeout.TotalSeconds} seconds.");
						await completed.ConfigureAwait (false);
						pending.Remove (completed);
						if (completed == exit && (!output.IsCompleted || !error.IsCompleted))
							drainTimeout = Task.Delay (OutputDrainTimeout, timerCancellation.Token);
					}
					cancellationToken.ThrowIfCancellationRequested ();
				} catch (Exception ex) {
					failures.Add (ex);
				}
			}

			timerCancellation.Cancel ();
			readCancellation.Cancel ();
			if (failures.Count > 0) {
				try {
					KillProcess (process);
				} catch (Exception ex) {
					failures.Add (ex);
				}
			}

			var shutdown = Task.WhenAll (output, error, started, exit);
			if (!shutdown.IsCompleted) {
				using var shutdownTimer = new CancellationTokenSource ();
				if (await Task.WhenAny (shutdown, Task.Delay (ShutdownTimeout, shutdownTimer.Token)).ConfigureAwait (false) != shutdown)
					failures.Add (new TimeoutException ($"Process '{process.StartInfo.FileName}' or its output consumers did not stop within {ShutdownTimeout.TotalSeconds} seconds."));
				shutdownTimer.Cancel ();
			}
			foreach (var task in new [] { output, error, started, exit }) {
				if (task.Exception is { } exceptions) {
					foreach (var ex in exceptions.InnerExceptions) {
						// A real consumer failure must not be hidden by concurrent execution cancellation.
						if (task != exit && failures.Count > 0 && failures [0] is OperationCanceledException && cancellationToken.IsCancellationRequested)
							failures [0] = ex;
						if (!failures.Contains (ex))
							failures.Add (ex);
					}
				}
				ObserveFailure (task);
			}
			ObserveFailure (shutdown);
			if (failures.Count == 1)
				ExceptionDispatchInfo.Capture (failures [0]).Throw ();
			if (failures.Count > 1)
				throw new AggregateException (failures);
			return process.ExitCode;
		}

		static void ObserveFailure (Task task)
		{
			if (task.IsCompleted) {
				_ = task.Exception;
				return;
			}
			task.ContinueWith (t => { _ = t.Exception; }, CancellationToken.None,
				TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
		}

		static async Task ReadStreamAsync (StreamReader stream, TextWriter destination, CancellationToken cancellationToken)
		{
			// The netstandard2.0 StreamReader overload cannot pass cancellation to its byte stream.
			using var source = new CancellableReadStream (stream.BaseStream, cancellationToken);
			using var reader = new StreamReader (source, stream.CurrentEncoding, detectEncodingFromByteOrderMarks: true, bufferSize: 4096, leaveOpen: true);
			var buffer = new char [4096];
			int read;
			while ((read = await reader.ReadAsync (buffer, 0, buffer.Length).ConfigureAwait (false)) > 0) {
				cancellationToken.ThrowIfCancellationRequested ();
				destination.Write (buffer, 0, read);
			}
		}

		sealed class CancellableReadStream : Stream
		{
			readonly Stream source;
			readonly CancellationToken cancellationToken;
			readonly TaskCompletionSource<bool> canceled = new TaskCompletionSource<bool> (TaskCreationOptions.RunContinuationsAsynchronously);
			readonly CancellationTokenRegistration registration;

			public CancellableReadStream (Stream source, CancellationToken cancellationToken)
			{
				this.source = source;
				this.cancellationToken = cancellationToken;
				registration = cancellationToken.Register (() => canceled.TrySetResult (true));
			}

			public override bool CanRead => source.CanRead;
			public override bool CanSeek => false;
			public override bool CanWrite => false;
			public override long Length => throw new NotSupportedException ();
			public override long Position {
				get => throw new NotSupportedException ();
				set => throw new NotSupportedException ();
			}

			public override int Read (byte[] buffer, int offset, int count)
			{
				cancellationToken.ThrowIfCancellationRequested ();
				return source.Read (buffer, offset, count);
			}

			public override async Task<int> ReadAsync (byte[] buffer, int offset, int count, CancellationToken token)
			{
				cancellationToken.ThrowIfCancellationRequested ();
				var read = source.ReadAsync (buffer, offset, count, cancellationToken);
				if (await Task.WhenAny (read, canceled.Task).ConfigureAwait (false) != read) {
					// Older streams may not interrupt an in-flight read. Never deliver its late data.
					ObserveFailure (read);
					cancellationToken.ThrowIfCancellationRequested ();
				}
				if (cancellationToken.IsCancellationRequested) {
					ObserveFailure (read);
					cancellationToken.ThrowIfCancellationRequested ();
				}
				return await read.ConfigureAwait (false);
			}

			public override void Flush () => throw new NotSupportedException ();
			public override long Seek (long offset, SeekOrigin origin) => throw new NotSupportedException ();
			public override void SetLength (long value) => throw new NotSupportedException ();
			public override void Write (byte[] buffer, int offset, int count) => throw new NotSupportedException ();

			protected override void Dispose (bool disposing)
			{
				if (disposing) {
					registration.Dispose ();
					source.Dispose ();
				}
				base.Dispose (disposing);
			}
		}

		/// <summary>
		/// Executes an Android Sdk tool and returns a result. The result is based on a function of the command output.
		/// </summary>
		public static async Task<TResult> ExecuteToolAsync<TResult> (string exe, Func<string, TResult> result, CancellationToken token, Action<Process>? onStarted = null)
		{
			if (result == null)
				throw new ArgumentNullException (nameof (result));
			using var log = new StringWriter ();
			using var error = new StringWriter ();

			var psi = new ProcessStartInfo (exe);
			psi.CreateNoWindow = true;
			psi.RedirectStandardInput = onStarted != null;

			var exitCode = await StartProcess (psi, log, error, token, null, onStarted).ConfigureAwait (false);
			var exeName = Path.GetFileName (exe);
			if (exitCode == 0)
				return result (log.ToString ());
			var errorMessage = error.ToString ();
			if (errorMessage.Length == 0)
				errorMessage = log.ToString ();
			throw new InvalidOperationException (errorMessage.Length == 0
				? $"`{exeName}` returned non-zero exit code"
				: $"{exitCode} : {errorMessage}");
		}

		internal static void Exec (ProcessStartInfo processStartInfo, DataReceivedEventHandler output, bool includeStderr = true)
		{
			if (output == null)
				throw new ArgumentNullException (nameof (output));
			processStartInfo.UseShellExecute         = false;
			processStartInfo.RedirectStandardInput   = false;
			processStartInfo.RedirectStandardOutput  = true;
			processStartInfo.RedirectStandardError   = true;
			processStartInfo.CreateNoWindow          = true;
			processStartInfo.WindowStyle             = ProcessWindowStyle.Hidden;

			using var p = new Process () {
				StartInfo   = processStartInfo,
				EnableRaisingEvents = true,
			};
			using var readCancellation = new CancellationTokenSource ();
			var outputDone = new TaskCompletionSource<bool> (TaskCreationOptions.RunContinuationsAsynchronously);
			var errorDone = new TaskCompletionSource<bool> (TaskCreationOptions.RunContinuationsAsynchronously);
			var started = new TaskCompletionSource<bool> (TaskCreationOptions.RunContinuationsAsynchronously);
			var callbackLock = new object ();
			p.OutputDataReceived += (sender, e) => Receive (sender, e, outputDone, include: true);
			p.ErrorDataReceived += (sender, e) => Receive (sender, e, errorDone, includeStderr);

			using var stopReading = readCancellation.Token.Register (() => {
				outputDone.TrySetCanceled ();
				errorDone.TrySetCanceled ();
			});
			var exit = WaitForExitAsync (p);
			p.Start ();
			var completion = CompleteProcessAsync (p, exit, outputDone.Task, errorDone.Task, started.Task, readCancellation, CancellationToken.None);
			try {
				p.BeginOutputReadLine ();
				p.BeginErrorReadLine ();
				started.TrySetResult (true);
			} catch (Exception ex) {
				started.TrySetException (ex);
			}
			completion.GetAwaiter ().GetResult ();

			void Receive (object sender, DataReceivedEventArgs e, TaskCompletionSource<bool> done, bool include)
			{
				lock (callbackLock) {
					if (readCancellation.IsCancellationRequested || outputDone.Task.IsFaulted || errorDone.Task.IsFaulted)
						return;
					try {
						if (include)
							output (sender, e);
						if (e.Data == null)
							done.TrySetResult (true);
					} catch (Exception ex) {
						done.TrySetException (ex);
					}
				}
			}
		}

		/// <summary>
		/// Creates a <see cref="ProcessStartInfo"/> with the given filename and arguments.
		/// On .NET 5+ uses <see cref="ProcessStartInfo.ArgumentList"/> to avoid shell-escaping issues;
		/// on older frameworks falls back to a single <see cref="ProcessStartInfo.Arguments"/> string
		/// built by <see cref="JoinArguments"/>.
		/// </summary>
		public static ProcessStartInfo CreateProcessStartInfo (string fileName, params string[] args)
		{
			var psi = new ProcessStartInfo {
				FileName = fileName,
				UseShellExecute = false,
				CreateNoWindow = true,
			};
#if NET5_0_OR_GREATER
			foreach (var arg in args)
				psi.ArgumentList.Add (arg);
#else
			psi.Arguments = JoinArguments (args);
#endif
			return psi;
		}

		/// <summary>
		/// Joins <paramref name="args"/> into a single command line suitable for
		/// <see cref="ProcessStartInfo.Arguments"/>.
		/// </summary>
		/// <remarks>
		/// Implements the quoting rules understood by <c>CommandLineToArgvW</c>, which are also
		/// the rules .NET uses when it parses <see cref="ProcessStartInfo.Arguments"/> on Unix.
		/// A run of backslashes is only an escape sequence when it is immediately followed by a
		/// quote, so backslashes must *not* be doubled unconditionally: doing so turns
		/// <c>C:\dir\file.dll</c> into <c>C:\\dir\\file.dll</c>, which some tools (notably
		/// <c>adb push</c>) reject.
		/// </remarks>
		internal static string JoinArguments (params string?[] args)
		{
			var sb = new StringBuilder ();
			for (int i = 0; i < args.Length; i++) {
				if (i > 0)
					sb.Append (' ');
				AppendArgument (sb, args [i]);
			}
			return sb.ToString ();
		}

		static void AppendArgument (StringBuilder sb, string? argument)
		{
			if (argument is null || argument.Length == 0) {
				sb.Append ("\"\"");
				return;
			}

			if (ContainsNoWhitespaceOrQuotes (argument)) {
				sb.Append (argument);
				return;
			}

			sb.Append ('"');
			for (int i = 0; i < argument.Length; i++) {
				int backslashes = 0;
				while (i < argument.Length && argument [i] == '\\') {
					backslashes++;
					i++;
				}

				if (i == argument.Length) {
					// Trailing backslashes precede the closing quote, so they must be doubled
					// to avoid escaping it.
					sb.Append ('\\', backslashes * 2);
					break;
				}

				if (argument [i] == '"') {
					sb.Append ('\\', backslashes * 2 + 1).Append ('"');
				} else {
					sb.Append ('\\', backslashes).Append (argument [i]);
				}
			}
			sb.Append ('"');
		}

		static bool ContainsNoWhitespaceOrQuotes (string s)
		{
			for (int i = 0; i < s.Length; i++) {
				char c = s [i];
				if (char.IsWhiteSpace (c) || c == '"')
					return false;
			}
			return true;
		}

		/// <summary>
		/// Throws <see cref="InvalidOperationException"/> when <paramref name="exitCode"/> is non-zero.
		/// Includes stderr/stdout context in the message when available.
		/// </summary>
		internal static void ThrowIfFailed (int exitCode, string command, string? stderr = null, string? stdout = null)
		{
			if (exitCode == 0)
				return;

			var message = $"'{command}' failed with exit code {exitCode}.";

			if (stderr is { Length: > 0 })
				message += $" stderr:{Environment.NewLine}{stderr.Trim ()}";
			if (stdout is { Length: > 0 })
				message += $" stdout:{Environment.NewLine}{stdout.Trim ()}";

			throw new InvalidOperationException (message);
		}

		/// <summary>
		/// Overload that accepts <see cref="StringWriter"/> directly so callers don't need to call ToString().
		/// </summary>
		internal static void ThrowIfFailed (int exitCode, string command, StringWriter? stderr = null, StringWriter? stdout = null)
		{
			ThrowIfFailed (exitCode, command, stderr?.ToString (), stdout?.ToString ());
		}

		/// <summary>
		/// Searches for a cmdline-tools binary in the SDK.
		/// Selects cmdline-tools/latest first, then the highest installed revision reported
		/// by source.properties, with deterministic directory-name fallback when metadata is unavailable.
		/// Legacy tools/bin installations are not supported.
		/// </summary>
		/// <param name="sdkPath">Root path to the Android SDK.</param>
		/// <param name="toolName">Tool binary name without extension (e.g., "avdmanager").</param>
		/// <param name="extension">File extension including the dot (e.g., ".bat") or empty string for no extension.</param>
		/// <param name="logger">Optional logger for diagnostic messages.</param>
		internal static string? FindCmdlineTool (string sdkPath, string toolName, string extension, Action<TraceLevel, string>? logger = null)
		{
			return CommandLineToolsResolver.Find (
				sdkPath,
				toolName,
				extension,
				logger: logger)?.Path;
		}

		internal static IEnumerable<string> FindExecutablesInPath (string executable)
		{
			var path        = Environment.GetEnvironmentVariable (EnvironmentVariableNames.Path) ?? "";
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

		/// <summary>Checks if running as Administrator (Windows) or root (macOS/Linux).</summary>
		public static bool IsElevated ()
		{
#if NET5_0_OR_GREATER
			return Environment.IsPrivilegedProcess;
#else
			if (OS.IsWindows)
				return IsUserAnAdmin ();
			return geteuid () == 0;
#endif
		}

#if !NET5_0_OR_GREATER
		[DllImport ("shell32.dll")]
		[return: MarshalAs (UnmanagedType.Bool)]
		static extern bool IsUserAnAdmin ();

		[DllImport ("libc", SetLastError = true)]
		static extern uint geteuid ();
#endif
	}
}
