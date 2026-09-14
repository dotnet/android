// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Mono.AndroidTools;
using Mono.AndroidTools.Util;
using Xamarin.AndroidTools.Properties;

namespace Xamarin.AndroidTools.Debugging
{
	// ActivityManager has one debug-app slot per device, not per package or user.
	// Keep the gate through cleanup, including when another AndroidDevice instance
	// targets the same serial. Unrelated adb clients do not participate in this lock.
	static class ManagedActivityLaunch
	{
		static readonly ConcurrentDictionary<string, SemaphoreSlim> gates = new ConcurrentDictionary<string, SemaphoreSlim> (StringComparer.Ordinal);
		const int CleanupTimeoutMilliseconds = 5000;
		const int PollMilliseconds = 100;
		const string DumpCommand = "dumpsys activity processes";
		const string ClearCommand = "am clear-debug-app";
		static readonly Regex packagePattern = new Regex (@"\A[A-Za-z][A-Za-z0-9_]*(?:\.[A-Za-z][A-Za-z0-9_]*)+\z");
		static readonly Regex markerPattern = new Regex (@"(?m)^  mDebugApp=(\S+)/orig=(\S+) mDebugTransient=(true|false) mOrigWaitForDebugger=(true|false)\r?$");

		internal static SemaphoreSlim GetGate (string serial) => gates.GetOrAdd (serial, _ => new SemaphoreSlim (1, 1));

		internal static async Task RunAsync (AndroidDevice device, ExecutionConfiguration configuration, AmStartCommand command, CancellationToken token)
		{
			// set-debug-app force-stops even an already-running package. Never turn
			// a warm launch into a cold one, or reinterpret caller-requested waits/repeats.
			if (!command.ForceStop || command.Wait || command.Repeat != 0 || device.BuildVersionSdk < 31) {
				await LaunchUnprotectedAsync (device, configuration, command, Resources.ManagedLaunchUnsupported, token).ConfigureAwait (false);
				return;
			}

			var package = configuration.PackageName;
			if (!packagePattern.IsMatch (package))
				throw new ArgumentException (Resources.ManagedLaunchPackageMismatch, nameof (configuration));
			if (string.IsNullOrEmpty (command.Component)) {
				await LaunchUnprotectedAsync (device, configuration, command, Resources.ManagedLaunchComponentUnsupported, token).ConfigureAwait (false);
				return;
			}
			if (!command.Component.StartsWith (package + "/", StringComparison.Ordinal) || command.Component.Length == package.Length + 1)
				throw new ArgumentException (Resources.ManagedLaunchPackageMismatch, nameof (configuration));
			if (configuration.Debugger.Timeout <= TimeSpan.Zero)
				throw new ArgumentOutOfRangeException (nameof (configuration.Debugger.Timeout));

			using (var timeout = CancellationTokenSource.CreateLinkedTokenSource (token)) {
				timeout.CancelAfter (configuration.Debugger.Timeout);
				bool armed = false;
				Exception primaryError = null;
				try {
					// AOSP's setDebugApp uses USER_ALL. Restrict this transaction to
					// single-user devices so an explicit --user never kills another profile.
					var users = await device.RunShellCommand ("pm list users", timeout.Token).ConfigureAwait (false);
					var userLines = users.Trim ().Split ('\n');
					var userIds = Regex.Matches (users, @"(?m)^[ \t]*UserInfo\{(\d+):[^{}\r\n]*:[0-9a-fA-F]+\}(?: running)?\r?$");
					if (userLines [0].TrimEnd ('\r') != "Users:" || userIds.Count == 0 || userIds.Count != userLines.Length - 1)
						throw new InvalidOperationException (Resources.ManagedLaunchStateUnavailable);
					if (userIds.Count != 1 || (!string.IsNullOrEmpty (command.User) && command.User != "current" && command.User != userIds [0].Groups [1].Value)) {
						await LaunchUnprotectedAsync (device, configuration, command, Resources.ManagedLaunchUnsupported, token).ConfigureAwait (false);
						return;
					}

					if (!await UsesPackageProcessAsync (device, command, package, userIds [0].Groups [1].Value, timeout.Token).ConfigureAwait (false)) {
						await LaunchUnprotectedAsync (device, configuration, command, Resources.ManagedLaunchProcessUnsupported, token).ConfigureAwait (false);
						return;
					}

					DebugAppState state;
					try {
						state = await ReadStateAsync (device, timeout.Token).ConfigureAwait (false);
					} catch (UnsupportedDumpLayoutException) {
						// Before any mutation, an unknown dump layout only means that
						// protection is unavailable. Never apply this fallback after arming.
						await LaunchUnprotectedAsync (device, configuration, command, Resources.ManagedLaunchLayoutUnsupported, token).ConfigureAwait (false);
						return;
					}
					if (!state.CanClear (package))
						throw new InvalidOperationException (Resources.ManagedLaunchStateConflict);
					if (state.HasMarker) {
						armed = true;
						using (var mutation = new CancellationTokenSource (CleanupTimeoutMilliseconds))
							await ExecuteEmptyCommandAsync (device, ClearCommand, mutation.Token).ConfigureAwait (false);
					}
					timeout.Token.ThrowIfCancellationRequested ();

					var builder = new ProcessArgumentBuilder ();
					builder.Add ("am", "set-debug-app");
					builder.AddQuoted (package);
					armed = true;
					// Do not abandon an in-flight mutation on caller cancellation:
					// drain its reply before cleanup can overtake it on another connection.
					using (var mutation = new CancellationTokenSource (CleanupTimeoutMilliseconds))
						await ExecuteEmptyCommandAsync (device, builder.ToString (), mutation.Token).ConfigureAwait (false);
					state = await ReadStateAsync (device, timeout.Token).ConfigureAwait (false);
					if (!state.IsPending (package))
						throw new InvalidOperationException (Resources.ManagedLaunchStateConflict);

					await device.ExecuteIntentCommandAsync (command, configuration.LogWiter, timeout.Token, waitForCompletion: true).ConfigureAwait (false);
					while (true) {
						state = await ReadStateAsync (device, timeout.Token).ConfigureAwait (false);
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
							: new TimeoutException (Resources.ManagedLaunchTimeout, ex);
						System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture (primaryError).Throw ();
					}
					throw;
				} finally {
					if (armed) {
						using (var cleanup = new CancellationTokenSource (CleanupTimeoutMilliseconds)) {
							try {
								var state = await ReadStateAsync (device, cleanup.Token).ConfigureAwait (false);
								if (!state.CanClear (package))
									throw new InvalidOperationException (Resources.ManagedLaunchStateConflict);
								if (state.HasMarker)
									await ExecuteEmptyCommandAsync (device, ClearCommand, cleanup.Token).ConfigureAwait (false);
								state = await ReadStateAsync (device, cleanup.Token).ConfigureAwait (false);
								if (state.HasMarker)
									throw new InvalidOperationException (Resources.ManagedLaunchStateConflict);
							} catch (Exception cleanupError) when (primaryError != null) {
								// Preserve launch/cancellation errors, but never hide failed cleanup.
								AndroidLogger.LogError (Resources.ManagedLaunchCleanupFailed, cleanupError);
							}
						}
					}
				}
			}
			token.ThrowIfCancellationRequested ();
		}

		static async Task LaunchUnprotectedAsync (AndroidDevice device, ExecutionConfiguration configuration, AmStartCommand command, string diagnostic, CancellationToken token)
		{
			AndroidLogger.LogInfo (diagnostic);
			configuration.LogWiter?.Invoke (diagnostic);
			// The legacy intent continuation can turn a canceled shell into empty
			// output. Preserve its launch behavior, but not cancellation-as-success.
			token.ThrowIfCancellationRequested ();
			await device.ExecuteIntentCommandAsync (command, configuration.LogWiter, token).ConfigureAwait (false);
			token.ThrowIfCancellationRequested ();
		}

		static async Task<bool> UsesPackageProcessAsync (AndroidDevice device, AmStartCommand command, string package, string user, CancellationToken token)
		{
			// No existing device metadata helper exposes ActivityInfo.processName.
			// PackageManager resolves both application inheritance and activity overrides.
			var builder = new ProcessArgumentBuilder ();
			builder.Add ("pm", "resolve-activity", "--user");
			builder.AddQuoted (user);
			builder.Add ("-n");
			builder.AddQuoted (command.Component);
			var output = await device.RunShellCommand (builder.ToString (), token).ConfigureAwait (false);
			token.ThrowIfCancellationRequested ();

			var activityName = command.Component.Substring (package.Length + 1);
			if (activityName.StartsWith (".", StringComparison.Ordinal))
				activityName = package + activityName;
			var activity = Regex.Match (output, @"(?m)^ActivityInfo:\r?\n(?<fields>(?:  [^\r\n]*\r?\n)+)");
			if (!activity.Success || activity.NextMatch ().Success)
				return false;
			var fields = activity.Groups ["fields"].Value;
			if (!Regex.IsMatch (fields, @"(?m)^  name=" + Regex.Escape (activityName) + @"\r?$") ||
					!Regex.IsMatch (fields, @"(?m)^  packageName=" + Regex.Escape (package) + @"\r?$") ||
					!Regex.IsMatch (fields, @"(?m)^  enabled=(true|false) exported=(true|false) directBootAware=(true|false)\r?$") ||
					!Regex.IsMatch (fields, @"(?m)^  ApplicationInfo:\r?$"))
				return false;

			// ComponentInfo omits processName when it equals the package. Only read
			// the two-space activity field, never ApplicationInfo's four-space value:
			// an activity can override a custom application process back to the package.
			var processes = Regex.Matches (fields, @"(?m)^  processName=(\S+)\r?$");
			return processes.Count == 0
				? !Regex.IsMatch (fields, @"(?m)^  processName=")
				: processes.Count == 1 && processes [0].Groups [1].Value == package;
		}

		static async Task ExecuteEmptyCommandAsync (AndroidDevice device, string command, CancellationToken token)
		{
			var output = await device.RunShellCommand (command, token).ConfigureAwait (false);
			token.ThrowIfCancellationRequested ();
			if (!string.IsNullOrWhiteSpace (output))
				throw new AdbException (output);
		}

		static async Task<DebugAppState> ReadStateAsync (AndroidDevice device, CancellationToken token)
		{
			var dump = await device.RunShellCommand (DumpCommand, token).ConfigureAwait (false);
			token.ThrowIfCancellationRequested ();
			return new DebugAppState (dump);
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
				if (!dump.StartsWith ("ACTIVITY MANAGER RUNNING PROCESSES (dumpsys activity processes)", StringComparison.Ordinal) ||
						!Regex.IsMatch (dump, @"(?m)^  mForceBackgroundCheck=(true|false)\s*\z"))
					throw new UnsupportedDumpLayoutException ();
				marker = markerPattern.Match (dump);
				if ((dump.Contains ("mDebugApp=") && !marker.Success) || marker.NextMatch ().Success)
					throw new InvalidOperationException (Resources.ManagedLaunchStateUnavailable);
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
