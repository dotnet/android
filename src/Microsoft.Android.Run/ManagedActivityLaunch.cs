#nullable enable
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Resources = Microsoft.Android.Run.ManagedActivityLaunchResources;

namespace Microsoft.Android.Run
{
	// ActivityManager has one debug-app slot per device, not per package or user.
	// Source-linked into the RunActivity task so both surviving launch owners use
	// the same transaction without adding APIs to the deprecated tools libraries.
	// Unrelated host processes/adb clients do not participate in this lock.
	static class ManagedActivityLaunch
	{
		static readonly ConcurrentDictionary<string, SemaphoreSlim> gates = new ConcurrentDictionary<string, SemaphoreSlim> (StringComparer.Ordinal);
		const int MutationDrainTimeoutMilliseconds = 5000;
		const int CleanupTimeoutMilliseconds = 5000;
		const int PollMilliseconds = 100;
		const string DumpCommand = "dumpsys activity processes";
		const string ClearCommand = "am clear-debug-app";
		static readonly Regex packagePattern = new Regex (@"\A[A-Za-z][A-Za-z0-9_]*(?:\.[A-Za-z][A-Za-z0-9_]*)+\z");
		static readonly Regex markerPattern = new Regex (@"(?m)^  mDebugApp=(\S+)/orig=(\S+) mDebugTransient=(true|false) mOrigWaitForDebugger=(true|false)\r?$");

		// Return only the confirmed effective activity process, not a guess for
		// fallbacks that did not resolve metadata. Hosts can retain their legacy probe.
		internal static async Task<string?> RunAsync (
			string serial, string package, string? component, string? user,
			bool forceStop, string startCommand, TimeSpan startupTimeout,
			Func<CancellationToken, Task> prepare,
			Func<string, CancellationToken, Task<string>> runShellCommand,
			Func<CancellationToken, Task> launchUnprotected,
			Action<string> log, Action<string, Exception> logCleanupError,
			CancellationToken token)
		{
			token.ThrowIfCancellationRequested ();
			if (string.IsNullOrEmpty (serial))
				throw new ArgumentException (nameof (serial));
			if (string.IsNullOrEmpty (package) || !packagePattern.IsMatch (package))
				throw new ArgumentException (Resources.ManagedLaunchPackageMismatch, nameof (package));
			if (component != null && component.Length != 0 &&
					(!component.StartsWith (package + "/", StringComparison.Ordinal) || component.Length == package.Length + 1))
				throw new ArgumentException (Resources.ManagedLaunchPackageMismatch, nameof (component));
			if (startupTimeout <= TimeSpan.Zero)
				throw new ArgumentOutOfRangeException (nameof (startupTimeout));

			var gate = gates.GetOrAdd (serial, _ => new SemaphoreSlim (1, 1));
			if (!await gate.WaitAsync (TimeSpan.FromSeconds (30), token).ConfigureAwait (false))
				throw new TimeoutException (Resources.ManagedLaunchGateTimeout);
			string? processName = null;
			try {
				using var timeout = CancellationTokenSource.CreateLinkedTokenSource (token);
				timeout.CancelAfter (startupTimeout);
				bool armed = false;
				Exception? primaryError = null;
				try {
					// Hold the gate through managed debugger setup as well as cleanup.
					await prepare (timeout.Token).ConfigureAwait (false);
					timeout.Token.ThrowIfCancellationRequested ();
					// Both callers supply a no-wait, non-repeated activity command.
					// set-debug-app force-stops the package: never make a warm launch cold.
					if (!forceStop) {
						await LaunchUnprotectedAsync (Resources.ManagedLaunchUnsupported).ConfigureAwait (false);
						return null;
					}
					var sdk = await runShellCommand ("getprop ro.build.version.sdk", timeout.Token).ConfigureAwait (false);
					timeout.Token.ThrowIfCancellationRequested ();
					if (!int.TryParse (sdk.Trim (), NumberStyles.None, CultureInfo.InvariantCulture, out int apiLevel) || apiLevel <= 0)
						throw new InvalidOperationException (Resources.ManagedLaunchStateUnavailable);
					if (apiLevel < 31) {
						await LaunchUnprotectedAsync (Resources.ManagedLaunchUnsupported).ConfigureAwait (false);
						return null;
					}
					if (component == null || component.Length == 0) {
						await LaunchUnprotectedAsync (Resources.ManagedLaunchComponentUnsupported).ConfigureAwait (false);
						return null;
					}

					// AOSP's setDebugApp uses USER_ALL. Restrict this transaction to
					// single-user devices so an explicit --user never kills another profile.
					var users = await runShellCommand ("pm list users", timeout.Token).ConfigureAwait (false);
					timeout.Token.ThrowIfCancellationRequested ();
					var userLines = users.Trim ().Split ('\n');
					var userIds = Regex.Matches (users, @"(?m)^[ \t]*UserInfo\{(\d+):[^{}\r\n]*:[0-9a-fA-F]+\}(?: running)?\r?$");
					if (userLines [0].TrimEnd ('\r') != "Users:" || userIds.Count == 0 || userIds.Count != userLines.Length - 1)
						throw new InvalidOperationException (Resources.ManagedLaunchStateUnavailable);
					if (userIds.Count != 1 || (!string.IsNullOrEmpty (user) && user != "current" && user != userIds [0].Groups [1].Value)) {
						await LaunchUnprotectedAsync (Resources.ManagedLaunchUnsupported).ConfigureAwait (false);
						return null;
					}

					processName = await ResolveActivityProcessNameAsync (runShellCommand, component, package, userIds [0].Groups [1].Value, timeout.Token).ConfigureAwait (false);
					if (processName != package) {
						await LaunchUnprotectedAsync (Resources.ManagedLaunchProcessUnsupported).ConfigureAwait (false);
						return processName;
					}

					DebugAppState state;
					try {
						state = await ReadStateAsync (runShellCommand, timeout.Token).ConfigureAwait (false);
					} catch (UnsupportedDumpLayoutException) {
						// Before any mutation, an unknown dump layout only means that
						// protection is unavailable. Never apply this fallback after arming.
						await LaunchUnprotectedAsync (Resources.ManagedLaunchLayoutUnsupported).ConfigureAwait (false);
						return processName;
					}
					if (!state.CanClear (package))
						throw new InvalidOperationException (Resources.ManagedLaunchStateConflict);
					if (state.HasMarker) {
						armed = true;
						await ExecuteMutationAsync (runShellCommand, ClearCommand).ConfigureAwait (false);
						// A clear is already a mutation. Recheck strictly before rearming,
						// not just later when cleanup happens to read the state again.
						state = await ReadStateAsync (runShellCommand, timeout.Token).ConfigureAwait (false);
						if (state.HasMarker)
							throw new InvalidOperationException (Resources.ManagedLaunchStateConflict);
					}
					timeout.Token.ThrowIfCancellationRequested ();

					armed = true;
					// Do not abandon an in-flight mutation on caller cancellation:
					// drain its reply before cleanup can overtake it on another connection.
					await ExecuteMutationAsync (runShellCommand, "am set-debug-app " + QuoteForDeviceShell (package)).ConfigureAwait (false);
					state = await ReadStateAsync (runShellCommand, timeout.Token).ConfigureAwait (false);
					if (!state.IsPending (package))
						throw new InvalidOperationException (Resources.ManagedLaunchStateConflict);

					// The legacy intent executor returns after five seconds and can
					// hide transport failures. Await the host transport and check the
					// complete reply here, without a dependency on that executor.
					log (startCommand);
					var output = await runShellCommand (startCommand, timeout.Token).ConfigureAwait (false);
					timeout.Token.ThrowIfCancellationRequested ();
					log (output);
					CheckStartResult (output, component);
					while (true) {
						state = await ReadStateAsync (runShellCommand, timeout.Token).ConfigureAwait (false);
						if (!state.CanClear (package))
							throw new InvalidOperationException (Resources.ManagedLaunchStateConflict);
						// attachApplicationLocked sets mDebugging before restoring the
						// globals, under the same AMS lock used by dumpsys. A PID alone
						// is too early, and waiting for activity startup would deadlock
						// managed debugger attach. mDebugTransient remains true here.
						if (state.IsConsumed && state.HasDebuggingProcess (package, userIds [0].Groups [1].Value))
							break;
						await Task.Delay (PollMilliseconds, timeout.Token).ConfigureAwait (false);
					}
					token.ThrowIfCancellationRequested ();
				} catch (Exception ex) {
					primaryError = ex;
					if (timeout.IsCancellationRequested) {
						primaryError = token.IsCancellationRequested
							? (Exception) new OperationCanceledException (token)
							: new TimeoutException (Resources.ManagedLaunchTimeout);
						System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture (primaryError).Throw ();
					}
					throw;
				} finally {
					if (armed) {
						try {
							await CleanupAsync (runShellCommand, package).ConfigureAwait (false);
						} catch (Exception cleanupError) when (primaryError != null || token.IsCancellationRequested) {
							// Preserve launch/cancellation errors, but never hide failed cleanup.
							logCleanupError (Resources.ManagedLaunchCleanupFailed, cleanupError);
							if (primaryError == null)
								token.ThrowIfCancellationRequested ();
						}
					}
				}
				async Task LaunchUnprotectedAsync (string diagnostic)
				{
					log (diagnostic);
					// The legacy continuation can turn a canceled shell into empty
					// output. Preserve its command/behavior, not cancellation-as-success.
					timeout.Token.ThrowIfCancellationRequested ();
					await launchUnprotected (timeout.Token).ConfigureAwait (false);
					timeout.Token.ThrowIfCancellationRequested ();
				}
			} finally {
				gate.Release ();
			}
			token.ThrowIfCancellationRequested ();
			return processName;
		}

		static async Task<string?> ResolveActivityProcessNameAsync (Func<string, CancellationToken, Task<string>> runShellCommand, string component, string package, string user, CancellationToken token)
		{
			// No existing device metadata helper exposes ActivityInfo.processName.
			// PackageManager resolves both application inheritance and activity overrides.
			var command = $"pm resolve-activity --user {QuoteForDeviceShell (user)} -n {QuoteForDeviceShell (component)}";
			var output = await runShellCommand (command, token).ConfigureAwait (false);
			token.ThrowIfCancellationRequested ();

			var activityName = component.Substring (package.Length + 1);
			if (activityName.StartsWith (".", StringComparison.Ordinal))
				activityName = package + activityName;
			var activity = Regex.Match (output, @"(?m)^ActivityInfo:\r?\n(?<fields>(?:  [^\r\n]*\r?\n)+)");
			if (!activity.Success || activity.NextMatch ().Success)
				return null;
			var fields = activity.Groups ["fields"].Value;
			if (!Regex.IsMatch (fields, @"(?m)^  name=" + Regex.Escape (activityName) + @"\r?$") ||
					!Regex.IsMatch (fields, @"(?m)^  packageName=" + Regex.Escape (package) + @"\r?$") ||
					!Regex.IsMatch (fields, @"(?m)^  enabled=(true|false) exported=(true|false) directBootAware=(true|false)\r?$") ||
					!Regex.IsMatch (fields, @"(?m)^  ApplicationInfo:\r?$"))
				return null;

			// ComponentInfo omits processName when it equals the package. Only read
			// the two-space activity field, never ApplicationInfo's four-space value:
			// an activity can override a custom application process back to the package.
			var processes = Regex.Matches (fields, @"(?m)^  processName=(\S+)\r?$");
			if (processes.Count == 0)
				return Regex.IsMatch (fields, @"(?m)^  processName=") ? null : package;
			return processes.Count == 1 ? processes [0].Groups [1].Value : null;
		}

		static async Task ExecuteMutationAsync (Func<string, CancellationToken, Task<string>> runShellCommand, string command)
		{
			using var mutation = new CancellationTokenSource (MutationDrainTimeoutMilliseconds);
			try {
				await ExecuteEmptyCommandAsync (runShellCommand, command, mutation.Token).ConfigureAwait (false);
			} catch (OperationCanceledException) when (mutation.IsCancellationRequested) {
				// Deadline cancellation is an implementation detail, not the cause
				// reported to callers. MSBuild classifies GetBaseException(), so do
				// not nest that cancellation inside the timeout diagnostic.
				throw new TimeoutException (Resources.ManagedLaunchMutationTimeout);
			}
		}

		static async Task ExecuteEmptyCommandAsync (Func<string, CancellationToken, Task<string>> runShellCommand, string command, CancellationToken token)
		{
			var output = await runShellCommand (command, token).ConfigureAwait (false);
			token.ThrowIfCancellationRequested ();
			if (!string.IsNullOrWhiteSpace (output))
				throw new CommandFailedException (output);
		}

		static async Task CleanupAsync (Func<string, CancellationToken, Task<string>> runShellCommand, string package)
		{
			using var cleanup = new CancellationTokenSource (CleanupTimeoutMilliseconds);
			try {
				var state = await ReadStateAsync (runShellCommand, cleanup.Token).ConfigureAwait (false);
				if (!state.CanClear (package))
					throw new InvalidOperationException (Resources.ManagedLaunchStateConflict);
				if (state.HasMarker)
					await ExecuteEmptyCommandAsync (runShellCommand, ClearCommand, cleanup.Token).ConfigureAwait (false);
				state = await ReadStateAsync (runShellCommand, cleanup.Token).ConfigureAwait (false);
				if (state.HasMarker)
					throw new InvalidOperationException (Resources.ManagedLaunchStateConflict);
			} catch (OperationCanceledException) when (cleanup.IsCancellationRequested) {
				throw new TimeoutException (Resources.ManagedLaunchCleanupTimeout);
			}
		}

		static async Task<DebugAppState> ReadStateAsync (Func<string, CancellationToken, Task<string>> runShellCommand, CancellationToken token)
		{
			var dump = await runShellCommand (DumpCommand, token).ConfigureAwait (false);
			token.ThrowIfCancellationRequested ();
			return new DebugAppState (dump);
		}

		// adb shell joins its arguments; host argv quoting alone does not protect
		// values from expansion by the device shell. Also used for instrumentation.
		internal static string QuoteForDeviceShell (string value) =>
			"'" + value.Replace ("'", "'\\''") + "'";

		internal static void CheckStartResult (string output, string component)
		{
			bool starting = false, failed = false, notFound = false;
			foreach (var rawLine in output.Split ('\n')) {
				var line = rawLine.TrimEnd ('\r');
				// am start -S prints Stopping before Starting. Do not mistake
				// diagnostic-looking words in a component/URI for error records.
				starting |= line.StartsWith ("Starting: Intent {", StringComparison.Ordinal) && line.EndsWith ("}", StringComparison.Ordinal);
				bool error = line.StartsWith ("Error:", StringComparison.Ordinal);
				notFound |= error && (line.StartsWith ("Error: Bad component name", StringComparison.Ordinal) || line.EndsWith ("does not exist.", StringComparison.Ordinal));
				failed |= error || line.StartsWith ("Error type ", StringComparison.Ordinal) ||
					line.StartsWith ("Exception occurred while executing", StringComparison.Ordinal) ||
					Regex.IsMatch (line, @"^(?:[A-Za-z_$][A-Za-z0-9_$]*\.)*(?:[A-Za-z_$][A-Za-z0-9_$]*)?(?:Exception|Error)(?::|$)");
			}
			if (failed || !starting)
				throw new CommandFailedException (notFound
					? string.Format (CultureInfo.CurrentCulture, Resources.ManagedLaunchActivityNotFound, component)
					: string.IsNullOrWhiteSpace (output) ? Resources.ManagedLaunchStartFailed : output, notFound);
		}

		// The task maps this back to its existing typed ADB diagnostics. The shared
		// implementation and dotnet run must not reference Mono.AndroidTools.
		internal sealed class CommandFailedException : Exception
		{
			internal bool ActivityNotFound { get; }

			internal CommandFailedException (string message, bool activityNotFound = false) : base (message)
			{
				ActivityNotFound = activityNotFound;
			}
		}

		sealed class UnsupportedDumpLayoutException : InvalidOperationException
		{
			internal UnsupportedDumpLayoutException () : base (Resources.ManagedLaunchStateUnavailable)
			{
			}
		}

		sealed class DebugAppState
		{
			readonly string dump;
			readonly Match marker;

			internal DebugAppState (string dump)
			{
				this.dump = dump;
				marker = markerPattern.Match (dump);
				if (Regex.Matches (dump, "mDebugApp=").Count != (marker.Success ? 1 : 0))
					throw new InvalidOperationException (Resources.ManagedLaunchStateUnavailable);
				if (!dump.StartsWith ("ACTIVITY MANAGER RUNNING PROCESSES (dumpsys activity processes)", StringComparison.Ordinal) ||
						!Regex.IsMatch (dump, @"(?m)^  mForceBackgroundCheck=(true|false)\s*\z"))
					throw new UnsupportedDumpLayoutException ();
			}

			internal bool HasMarker => marker.Success;
			internal bool IsConsumed => HasMarker && marker.Groups [1].Value == "null" && CanClear ("null");
			internal bool IsPending (string package) => HasMarker && marker.Groups [1].Value == package && CanClear (package);
			internal bool CanClear (string package) => !HasMarker ||
				((marker.Groups [1].Value == package || marker.Groups [1].Value == "null") &&
				marker.Groups [2].Value == "null" && marker.Groups [3].Value == "true" && marker.Groups [4].Value == "false");

			internal bool HasDebuggingProcess (string package, string user)
			{
				// Restrict mDebugging to this process's full *APP* record, not a
				// substring match against another package or a later process record.
				var records = Regex.Split (dump, @"(?m)^  \*APP\* ");
				for (int i = 1; i < records.Length; i++) {
					var newline = records [i].IndexOf ('\n');
					if (newline < 0)
						continue;
					var header = records [i].Substring (0, newline);
					if (Regex.IsMatch (header, @"ProcessRecord\{\S+ [1-9][0-9]*:" + Regex.Escape (package) + "/u" + user + @"a[0-9]+\}") &&
							Regex.IsMatch (records [i], @"(?m)^    mDebugging=true\r?$"))
						return true;
				}
				return false;
			}
		}
	}
}
