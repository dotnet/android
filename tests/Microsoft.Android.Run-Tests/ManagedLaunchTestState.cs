// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Android.Run.Tests
{
	internal sealed class ManagedLaunchTestState
	{
		string debugProperty = "";
		bool started;
		bool transient;
		int pidRequests;

		public ManagedLaunchTestState (string packageName)
		{
			PackageName = packageName;
			EffectiveProcessName = packageName;
			ApplicationProcessName = packageName;
		}

		public ConcurrentQueue<string> Commands { get; } = new ConcurrentQueue<string> ();

		public string PackageName { get; }

		public bool AttachOnDump { get; set; } = true;

		public string? DebugApp { get; set; }

		public string? OriginalDebugApp { get; set; }

		public bool OriginalWaitForDebugger { get; set; }

		public string UserList { get; set; } = "Users:\n\tUserInfo{0:Owner:13} running\n";

		public int ApiLevel { get; set; } = 36;

		public string EffectiveProcessName { get; set; }

		public string ApplicationProcessName { get; set; }

		public Func<string, Task> BeforeResponse { get; set; } = _ => Task.CompletedTask;

		public Func<string, string, string> TransformResponse { get; set; } = (_, output) => output;

		public Func<string, (int ExitCode, string Error)> CliResult { get; set; } = _ => (0, "");

		public string? FailTransportCommand { get; set; }

		public bool Attached { get; private set; }

		public void SetDebugAppState (string package, bool isTransient)
		{
			DebugApp = package;
			transient = isTransient;
		}

		public async Task<(bool Success, string Output)> RespondToCommandAsync (string command, CancellationToken token)
		{
			Commands.Enqueue (command);
			await BeforeResponse (command).WaitAsync (token);
			if (command == FailTransportCommand)
				return (false, "simulated transport fail");
			return (true, TransformResponse (command, Respond (command)));
		}

		string Respond (string command)
		{
			if (command == "get-serialno")
				return "managed-launch-test\n";
			if (command.StartsWith ("forward ", StringComparison.Ordinal) || command.StartsWith ("reverse ", StringComparison.Ordinal))
				return "";
			if (command.StartsWith ("pidof ", StringComparison.Ordinal)) {
				string process = command.Substring ("pidof ".Length);
				if (process != EffectiveProcessName && process != "'" + EffectiveProcessName.Replace ("'", "'\\''") + "'")
					return "";
				return Interlocked.Increment (ref pidRequests) == 1 ? "1234\n" : "";
			}
			if (command.StartsWith ("logcat ", StringComparison.Ordinal))
				return "managed-launch logcat\n";
			if (command.StartsWith ("am force-stop ", StringComparison.Ordinal))
				return "";
			if (command.StartsWith ("input keyevent KEYCODE_WAKEUP; wm dismiss-keyguard", StringComparison.Ordinal)) {
				int start = command.IndexOf ("am start ", StringComparison.Ordinal);
				return start >= 0 ? Respond (command.Substring (start)) : "";
			}
			if (command == "date +%s")
				return "1000\n";
			if (command.StartsWith ("setprop ", StringComparison.Ordinal)) {
				int quote = command.IndexOf ("\" ", StringComparison.Ordinal);
				debugProperty = quote >= 0 ? command.Substring (quote + 3).Trim ('"') : "";
				return "";
			}
			if (command == "getprop")
				return $"[ro.build.version.sdk]: [{ApiLevel}]\n[debug.mono.extra]: [{debugProperty}]\n";
			if (command == "getprop ro.build.version.sdk")
				return ApiLevel.ToString (CultureInfo.InvariantCulture) + "\n";
			if (command == "pm list users")
				return UserList;
			if (command.StartsWith ("pm resolve-activity ", StringComparison.Ordinal)) {
				string component = command.Substring (command.IndexOf ("-n ", StringComparison.Ordinal) + 3).Trim ('\'', '"');
				string activity = component.Substring (component.IndexOf ('/') + 1);
				if (activity.StartsWith (".", StringComparison.Ordinal))
					activity = PackageName + activity;
				string process = EffectiveProcessName == PackageName ? "" : $"  processName={EffectiveProcessName}\n";
				return "priority=0 preferredOrder=0 match=0x0 specificIndex=-1 isDefault=false\n" +
					$"ActivityInfo:\n  name={activity}\n  packageName={PackageName}\n" +
					process + "  enabled=true exported=true directBootAware=false\n" +
					$"  ApplicationInfo:\n    packageName={PackageName}\n    processName={ApplicationProcessName}\n" +
					"    uid=10123 flags=0x0 privateFlags=0x0 theme=0x0\n";
			}
			if (command == "am set-debug-app 'com.example.managed'") {
				DebugApp = PackageName;
				transient = true;
				started = false;
				Attached = false;
				return "";
			}
			if (command == "am clear-debug-app") {
				DebugApp = null;
				transient = false;
				return "";
			}
			if (command.StartsWith ("am start ", StringComparison.Ordinal)) {
				started = true;
				return (command.Contains (" -S") ? $"Stopping: {PackageName}\n" : "") +
					$"Starting: Intent {{ cmp={PackageName}/.MainActivity }}\n";
			}
			if (command == "dumpsys activity processes") {
				if (AttachOnDump && started && DebugApp == EffectiveProcessName) {
					Attached = true;
					DebugApp = null;
				}
				string process = started
					? $"  *APP* UID 10123 ProcessRecord{{abc 1234:{EffectiveProcessName}/u0a123}}\n    pid=1234\n" +
						(Attached ? "    mDebugging=true\n" : "")
					: "";
				string marker = transient || DebugApp != null
					? $"  mDebugApp={DebugApp ?? "null"}/orig={OriginalDebugApp ?? "null"} mDebugTransient={transient.ToString ().ToLowerInvariant ()} mOrigWaitForDebugger={OriginalWaitForDebugger.ToString ().ToLowerInvariant ()}\n"
					: "";
				return "ACTIVITY MANAGER RUNNING PROCESSES (dumpsys activity processes)\n" + process + marker + "  mForceBackgroundCheck=false\n";
			}
			if (command.StartsWith ("am broadcast ", StringComparison.Ordinal) || command.StartsWith ("am instrument ", StringComparison.Ordinal) ||
					command.StartsWith ("\"run-as\" ", StringComparison.Ordinal))
				return "";
			if (command.StartsWith ("ps", StringComparison.Ordinal))
				return $"USER PID PPID VSIZE RSS WCHAN PC NAME\nu0_a123 1234 1 0 0 0 0 {PackageName}\n";

			throw new InvalidOperationException ("Unexpected ADB command: " + command);
		}
	}
}
