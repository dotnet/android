// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Microsoft.Android.Run.Tests
{
	[TestFixture]
	public class MicrosoftAndroidRunCliTests
	{
		const string PackageName = "com.example.managed";

		[Test]
		public async Task AcceptsExplicitDebugIntent ()
		{
			Assert.IsTrue (File.Exists (GetFakeAdbPath ()), GetFakeAdbPath ());
			var result = await RunProgramAsync ("--help");
			Assert.AreEqual (0, result.ExitCode, result.Error);
			StringAssert.Contains ("--attach-debugger", result.Output);
		}

		[Test]
		public async Task ProtectsExplicitDebugLaunchWithoutActivityWait ([Values] bool noWait)
		{
			await using var server = new ManagedLaunchTestServer (PackageName);
			var arguments = new List<string> { "--activity", ".MainActivity", "--attach-debugger", "--user", "0", "--no-wake-device" };
			if (noWait)
				arguments.Add ("--no-wait");

			var result = await RunProgramAsync (server, arguments.ToArray ());

			Assert.AreEqual (0, result.ExitCode, result.Output + result.Error);
			CollectionAssert.Contains (server.Commands, "am set-debug-app 'com.example.managed'");
			CollectionAssert.Contains (server.Commands, "am start -S --user '0' -n 'com.example.managed/.MainActivity'");
			Assert.IsFalse (server.Commands.Any (command => command.Contains (" -D", StringComparison.Ordinal) || command.Contains (" -W", StringComparison.Ordinal) || command.Contains ("--persistent", StringComparison.Ordinal)));
			Assert.AreEqual (!noWait, server.Commands.Any (command => command.StartsWith ("logcat ", StringComparison.Ordinal)));
			Assert.That (server.AdbCommands.Where (command => !command.EndsWith ("get-serialno", StringComparison.Ordinal)),
				Is.All.StartsWith ("-s managed-launch-test "), "Resolve automatic selection once, then pin the same device.");
		}

		[Test]
		public async Task AllowsAdbDaemonStartupDiagnostics ()
		{
			await using var server = new ManagedLaunchTestServer (PackageName);

			var result = await RunProgramAsync (server, new [] { "--activity", ".MainActivity", "--attach-debugger", "--no-wake-device", "--no-wait" }, daemonStartup: true);

			Assert.AreEqual (0, result.ExitCode, result.Output + result.Error);
			StringAssert.Contains ("daemon started successfully", result.Error);
		}

		[Test]
		public async Task NoWaitAndPortsAreNotDebugIntent ([Values] bool noWait, [Values] bool ports)
		{
			await using var server = new ManagedLaunchTestServer (PackageName);
			server.State.SetDebugAppState ("com.example.other", isTransient: true);
			var arguments = new List<string> { "--activity", ".MainActivity", "--no-wake-device" };
			if (noWait)
				arguments.Add ("--no-wait");
			if (ports)
				arguments.AddRange (new [] { "--forward-port", "10000:10000", "--reverse-port", "8000:8001" });

			var result = await RunProgramAsync (server, arguments.ToArray ());

			Assert.AreEqual (0, result.ExitCode, result.Output + result.Error);
			Assert.IsFalse (server.Commands.Any (command => command.Contains ("debug-app", StringComparison.Ordinal) || command == "dumpsys activity processes"));
			Assert.AreEqual ("com.example.other", server.State.DebugApp);
			Assert.AreEqual (ports, server.Commands.Contains ("forward tcp:10000 tcp:10000"));
			Assert.AreEqual (ports, server.Commands.Contains ("reverse tcp:8000 tcp:8001"));
		}

		[TestCase ("older-api")]
		[TestCase ("unsupported-layout")]
		[TestCase ("custom-process")]
		public async Task DebugFallbackWaitsForPid (string reason)
		{
			await using var server = new ManagedLaunchTestServer (PackageName);
			if (reason == "older-api")
				server.State.ApiLevel = 30;
			if (reason == "custom-process")
				server.State.EffectiveProcessName = PackageName + ":custom";
			server.State.TransformResponse = (command, output) => {
				if (reason == "unsupported-layout" && command == "dumpsys activity processes")
					return output + "  vendor postamble\n";
				if (command.StartsWith ("pidof ", StringComparison.Ordinal)) {
					bool matches = reason == "custom-process"
						? command == "pidof " + server.State.EffectiveProcessName || command == "pidof '" + server.State.EffectiveProcessName + "'"
						: command == "pidof " + PackageName || command == "pidof '" + PackageName + "'";
					return matches && server.Commands.Count (item => item.StartsWith ("pidof ", StringComparison.Ordinal)) > 3
						? "1234\n"
						: "";
				}
				return output;
			};

			var result = await RunProgramAsync (server, "--activity", ".MainActivity", "--attach-debugger", "--no-wake-device");

			Assert.AreEqual (0, result.ExitCode, result.Output + result.Error);
			Assert.IsTrue (server.Commands.Any (command => command.StartsWith ("logcat ", StringComparison.Ordinal)));
			Assert.IsFalse (server.Commands.Any (command => command.Contains ("debug-app", StringComparison.Ordinal) || command.Contains (" -W", StringComparison.Ordinal)));
		}

		[Test]
		public async Task CustomProcessTracksStartupAndExit ()
		{
			await using var server = new ManagedLaunchTestServer (PackageName);
			server.State.EffectiveProcessName = PackageName + ":custom$worker";
			string pidCommand = "pidof '" + server.State.EffectiveProcessName + "'";
			var releaseLogcat = new TaskCompletionSource (TaskCreationOptions.RunContinuationsAsynchronously);
			server.State.BeforeResponse = command => command.StartsWith ("logcat ", StringComparison.Ordinal) ? releaseLogcat.Task : Task.CompletedTask;
			server.State.TransformResponse = (command, output) => {
				if (!command.StartsWith ("pidof ", StringComparison.Ordinal))
					return output;
				if (command != pidCommand)
					return "";
				int query = server.Commands.Count (item => item == pidCommand);
				if (query >= 5)
					releaseLogcat.TrySetResult ();
				return query is 3 or 4 ? "1234\n" : "";
			};

			var result = await RunProgramAsync (server, "--activity", ".MainActivity", "--attach-debugger", "--no-wake-device", "--verbose");

			Assert.AreEqual (0, result.ExitCode, result.Output + result.Error);
			Assert.That (server.Commands.Where (command => command.StartsWith ("pidof ", StringComparison.Ordinal)), Is.All.EqualTo (pidCommand));
			CollectionAssert.Contains (server.Commands, "logcat --pid=1234");
			StringAssert.Contains ("App has exited.", result.Output);
		}

		[TestCase ("stderr", false)]
		[TestCase ("stdout", true)]
		public async Task DebugPidDiagnosticsFailPromptly (string channel, bool afterStartup)
		{
			await using var server = new ManagedLaunchTestServer (PackageName);
			server.State.ApiLevel = 30;
			const string diagnostic = "adb: device offline\n";
			int failOnQuery = afterStartup ? 2 : 1;
			server.State.TransformResponse = (command, output) => {
				if (!command.StartsWith ("pidof ", StringComparison.Ordinal))
					return output;
				return server.Commands.Count (item => item.StartsWith ("pidof ", StringComparison.Ordinal)) < failOnQuery ? "1234\n" : channel == "stdout" ? diagnostic : "";
			};
			server.State.CliResult = command => command.StartsWith ("pidof ", StringComparison.Ordinal) &&
				server.Commands.Count (item => item.StartsWith ("pidof ", StringComparison.Ordinal)) >= failOnQuery
				? (1, channel == "stderr" ? diagnostic : "")
				: (0, "");

			var result = await RunProgramAsync (server, "--activity", ".MainActivity", "--attach-debugger", "--no-wake-device", "--verbose");

			Assert.AreEqual (1, result.ExitCode, result.Output + result.Error);
			StringAssert.Contains (diagnostic.Trim (), result.Error);
			if (afterStartup)
				StringAssert.Contains ("App PID: 1234", result.Output);
		}

		[Test]
		public async Task NonDebugPidDiagnosticsRetainExistingBehavior ()
		{
			await using var server = new ManagedLaunchTestServer (PackageName);
			server.State.TransformResponse = (command, output) => command.StartsWith ("pidof ", StringComparison.Ordinal) ? "" : output;
			server.State.CliResult = command => command.StartsWith ("pidof ", StringComparison.Ordinal) ? (1, "adb: device offline\n") : (0, "");

			var result = await RunProgramAsync (server, "--activity", ".MainActivity", "--no-wake-device");

			Assert.AreEqual (1, result.ExitCode, result.Output + result.Error);
			StringAssert.Contains ("could not retrieve PID", result.Error);
			Assert.AreEqual (1, server.Commands.Count (command => command.StartsWith ("pidof ", StringComparison.Ordinal)));
		}

		[Test]
		public async Task PidPollingTimeoutIsFailure ()
		{
			await using var server = new ManagedLaunchTestServer (PackageName);
			server.State.ApiLevel = 30;
			server.State.TransformResponse = (command, output) => command.StartsWith ("pidof ", StringComparison.Ordinal) ? "" : output;

			var result = await RunProgramAsync (server, "--activity", ".MainActivity", "--attach-debugger", "--no-wake-device");

			Assert.AreEqual (1, result.ExitCode, result.Output + result.Error);
			StringAssert.Contains ("Timed out", result.Error);
			StringAssert.Contains ("process", result.Error);
			Assert.IsFalse (server.Commands.Any (command => command.StartsWith ("logcat ", StringComparison.Ordinal)));
		}

		[Test]
		public async Task InstrumentationIgnoresActivityDebugIntent ()
		{
			await using var server = new ManagedLaunchTestServer (PackageName);

			var result = await RunProgramAsync (server, "--instrument", "runner", "--attach-debugger", "--no-wait", "--forward-port", "10000:10000");

			Assert.AreEqual (0, result.ExitCode, result.Output + result.Error);
			Assert.IsTrue (server.Commands.Any (command => command.StartsWith ("am instrument ", StringComparison.Ordinal)));
			Assert.IsFalse (server.Commands.Any (command => command.Contains ("debug-app", StringComparison.Ordinal) || command.StartsWith ("am start ", StringComparison.Ordinal)));
		}

		[Test]
		public async Task DotnetTestDispatchDoesNotBecomeAnActivityLaunch ()
		{
			await using var server = new ManagedLaunchTestServer (PackageName);

			var result = await RunProgramAsync (server, "--instrument", "runner", "--server", "dotnettestcli", "--attach-debugger", "--no-wait");

			Assert.AreEqual (1, result.ExitCode, result.Output + result.Error);
			StringAssert.Contains ("--dotnet-test-pipe", result.Error);
			Assert.IsEmpty (server.Commands);
		}

		[TestCase (false)]
		[TestCase (true)]
		public async Task ClassifiesStartStderrForSuccessAndFailure (bool fallback)
		{
			await using var server = new ManagedLaunchTestServer (PackageName);
			server.State.ApiLevel = fallback ? 30 : 36;
			server.State.CliResult = command => command == "am start -S -n 'com.example.managed/.MainActivity'"
				? (0, "Warning: Activity not started, its current task has been brought to the front\n")
				: (0, "");

			var success = await RunProgramAsync (server, "--activity", ".MainActivity", "--attach-debugger", "--no-wait", "--no-wake-device");

			Assert.AreEqual (0, success.ExitCode, success.Output + success.Error);
			Assert.IsNull (server.State.DebugApp);

			server.State.CliResult = command => command == "am start -S -n 'com.example.managed/.MainActivity'"
				? (0, "Error: Permission denied\n")
				: (0, "");

			var failure = await RunProgramAsync (server, "--activity", ".MainActivity", "--attach-debugger", "--no-wait", "--no-wake-device");

			Assert.AreEqual (1, failure.ExitCode, failure.Output + failure.Error);
			StringAssert.Contains ("Error: Permission denied", failure.Error);
		}

		[Test]
		public async Task TransportFailuresDoNotFallback ()
		{
			await using var server = new ManagedLaunchTestServer (PackageName);
			server.State.FailTransportCommand = "get-serialno";

			var result = await RunProgramAsync (server, "--activity", ".MainActivity", "--attach-debugger", "--no-wait");

			Assert.AreEqual (1, result.ExitCode, result.Output + result.Error);
			StringAssert.Contains ("simulated transport fail", result.Error);
			Assert.IsFalse (server.Commands.Any (command => command.StartsWith ("am ", StringComparison.Ordinal)));
		}

		[Test]
		public async Task QuotesTheDeviceComponent ()
		{
			await using var server = new ManagedLaunchTestServer (PackageName);
			server.State.TransformResponse = (command, output) => command.StartsWith ("pm resolve-activity ", StringComparison.Ordinal) ? "" : output;

			var result = await RunProgramAsync (server, "--activity", ".Activity'Literal", "--attach-debugger", "--no-wait", "--no-wake-device");

			Assert.AreEqual (0, result.ExitCode, result.Output + result.Error);
			CollectionAssert.Contains (server.Commands, "am start -S -n 'com.example.managed/.Activity'\\''Literal'");
		}

		[Test]
		[Platform ("Linux,MacOsX")]
		public async Task CtrlCDrainsMutationAndCleansBeforeStoppingApp ()
		{
			await using var server = new ManagedLaunchTestServer (PackageName);
			var arming = new TaskCompletionSource (TaskCreationOptions.RunContinuationsAsynchronously);
			var release = new TaskCompletionSource (TaskCreationOptions.RunContinuationsAsynchronously);
			server.State.BeforeResponse = command => {
				if (command.StartsWith ("am set-debug-app ", StringComparison.Ordinal)) {
					arming.TrySetResult ();
					return release.Task;
				}
				return Task.CompletedTask;
			};

			var psi = CreateProgramStartInfo (server, daemonStartup: false, "--activity", ".MainActivity", "--attach-debugger", "--no-wake-device", "--no-wait", "--user", "0");
			using var process = ProcessTestUtilities.StartProcess (psi, out var output, out var error, out var stdoutClosed, out var stderrClosed, out var exitTask);
			try {
				await arming.Task.WaitAsync (TimeSpan.FromSeconds (5));
				SendSigInt (process.Id);
				await Task.WhenAny (exitTask, Task.Delay (100));
				Assert.IsFalse (process.HasExited);
				Assert.IsFalse (server.Commands.Contains ("am clear-debug-app"));

				release.TrySetResult ();
				await Task.WhenAll (stdoutClosed, stderrClosed, exitTask).WaitAsync (TimeSpan.FromSeconds (10));
				Assert.AreEqual (130, process.ExitCode, output.ToString () + error.ToString ());
				StringAssert.Contains ("Stopping application...", output.ToString ());
				Assert.IsNull (server.State.DebugApp);
				int clear = Array.IndexOf (server.Commands.ToArray (), "am clear-debug-app");
				int stop = Array.FindIndex (server.Commands.ToArray (), command => command.StartsWith ("am force-stop ", StringComparison.Ordinal));
				Assert.GreaterOrEqual (clear, 0);
				Assert.Greater (stop, clear);
			} finally {
				release.TrySetResult ();
				ProcessTestUtilities.TryKillProcessTree (process);
				try {
					await Task.WhenAll (stdoutClosed, stderrClosed, exitTask).WaitAsync (TimeSpan.FromSeconds (5));
				} catch (TimeoutException) {
				}
			}
		}

		static Task<(int ExitCode, string Output, string Error)> RunProgramAsync (params string [] arguments) =>
			RunProgramCoreAsync (server: null, arguments, daemonStartup: false);

		static Task<(int ExitCode, string Output, string Error)> RunProgramAsync (ManagedLaunchTestServer server, params string [] arguments) =>
			RunProgramCoreAsync (server, arguments, daemonStartup: false);

		static Task<(int ExitCode, string Output, string Error)> RunProgramAsync (ManagedLaunchTestServer server, string [] arguments, bool daemonStartup) =>
			RunProgramCoreAsync ((ManagedLaunchTestServer?) server, arguments, daemonStartup);

		static async Task<(int ExitCode, string Output, string Error)> RunProgramCoreAsync (ManagedLaunchTestServer? server, string [] arguments, bool daemonStartup)
		{
			var psi = server == null
				? CreateProgramStartInfo (null, daemonStartup, arguments)
				: CreateProgramStartInfo (server, daemonStartup, arguments);
			return await ProcessTestUtilities.RunProcessAsync (psi, TimeSpan.FromSeconds (45));
		}

		static ProcessStartInfo CreateProgramStartInfo (ManagedLaunchTestServer? server, bool daemonStartup, params string [] arguments)
		{
			string dotnet = Environment.GetEnvironmentVariable ("DOTNET_HOST_PATH") ?? "dotnet";
			string program = Assembly.Load ("Microsoft.Android.Run").Location;
			var psi = new ProcessStartInfo (dotnet) {
				UseShellExecute = false,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
			};
			psi.ArgumentList.Add (program);
			if (server != null) {
				psi.ArgumentList.Add ("--adb");
				psi.ArgumentList.Add (GetFakeAdbPath ());
				psi.ArgumentList.Add ("--adb-target");
				psi.ArgumentList.Add ("-d");
				psi.ArgumentList.Add ("--package");
				psi.ArgumentList.Add (PackageName);
				psi.Environment ["MANAGED_LAUNCH_TEST_ADB_PORT"] = server.Port.ToString (CultureInfo.InvariantCulture);
				psi.Environment ["MANAGED_LAUNCH_TEST_ADB_DAEMON_STARTUP"] = daemonStartup ? "1" : "0";
			}
			foreach (string argument in arguments)
				psi.ArgumentList.Add (argument);
			return psi;
		}

		static string GetFakeAdbPath ()
		{
			string fileName = OperatingSystem.IsWindows () ? "ManagedLaunchTestAdbClient.exe" : "ManagedLaunchTestAdbClient";
			return Path.Combine (AppContext.BaseDirectory, "ManagedLaunchTestAdbProxyTool", fileName);
		}

		static void SendSigInt (int processId)
		{
			if (kill (processId, 2) != 0)
				throw new InvalidOperationException ($"kill({processId}, SIGINT) failed.");
		}

		[DllImport ("libc", SetLastError = true)]
		static extern int kill (int pid, int sig);

		[Test]
		public void RunProgramCoreAsyncTimesOutWhileDrainingStderr ()
		{
			var psi = new ProcessStartInfo (GetFakeAdbPath ()) {
				UseShellExecute = false,
			};
			psi.Environment ["MANAGED_LAUNCH_TEST_CHILD_MODE"] = "stderr-spam-then-wait";

			Assert.ThrowsAsync<TimeoutException> (async () =>
				await ProcessTestUtilities.RunProcessAsync (psi, TimeSpan.FromMilliseconds (300)));
		}
	}
}
