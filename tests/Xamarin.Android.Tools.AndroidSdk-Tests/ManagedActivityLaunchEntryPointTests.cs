// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Android.Build.BaseTasks.Tests.Utilities;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Mono.AndroidTools;
using NUnit.Framework;
using Xamarin.Android.Tasks;
using Xamarin.AndroidTools.Debugging;
using LaunchAdbServer = Xamarin.Android.Tools.Tests.ManagedActivityLaunchTests.LaunchAdbServer;

namespace Xamarin.Android.Tools.Tests;

[TestFixture]
public class ManagedActivityLaunchEntryPointTests
{
	const string PackageName = "com.example.managed";

	[Test]
	public async Task RunProgramAcceptsExplicitDebugIntent ()
	{
		var (exitCode, output, error) = await RunProgramAsync ("--help");
		Assert.AreEqual (0, exitCode, error);
		StringAssert.Contains ("--attach-debugger", output);
	}

	[Test]
	public async Task RunProgramProtectsExplicitDebugLaunchWithoutActivityWait ([Values] bool noWait)
	{
		await using var server = new LaunchAdbServer ();
		var arguments = new List<string> { "--activity", ".MainActivity", "--attach-debugger", "--user", "0", "--no-wake-device" };
		if (noWait)
			arguments.Add ("--no-wait");

		var (exitCode, output, error) = await RunProgramAsync (server, arguments.ToArray ());

		Assert.AreEqual (0, exitCode, output + error);
		var commands = server.Commands.ToArray ();
		CollectionAssert.Contains (commands, "am set-debug-app 'com.example.managed'");
		var start = commands.Single (c => c.StartsWith ("am start ", StringComparison.Ordinal));
		Assert.AreEqual ("am start -S --user '0' -n 'com.example.managed/.MainActivity'", start);
		Assert.IsFalse (commands.Any (c => c.Contains (" -D") || c.Contains (" -W") || c.Contains (" -w") || c.Contains ("--persistent")));
		Assert.IsTrue (server.Attached);
		Assert.IsNull (server.DebugApp);
		Assert.Greater (Array.IndexOf (commands, "am clear-debug-app"), Array.IndexOf (commands, start));
		Assert.AreEqual (!noWait, commands.Any (c => c.StartsWith ("logcat ", StringComparison.Ordinal)));
		Assert.That (server.AdbCommands.Where (c => !c.EndsWith ("get-serialno", StringComparison.Ordinal)),
			Is.All.StartsWith ("-s " + server.Serial + " "), "Resolve automatic selection once, then pin the same device.");
	}

	[Test]
	public async Task RunProgramAllowsAdbDaemonStartupDiagnostics ()
	{
		await using var server = new LaunchAdbServer { LogDaemonStartup = true };
		var (exitCode, output, error) = await RunProgramAsync (server,
			"--activity", ".MainActivity", "--attach-debugger", "--no-wait", "--no-wake-device");
		Assert.AreEqual (0, exitCode, output + error);
		StringAssert.Contains ("daemon started successfully", error);
		Assert.IsTrue (server.Attached);
		Assert.IsNull (server.DebugApp);
	}

	[Test]
	public async Task RunProgramCtrlCDrainsMutationAndCleansBeforeStoppingApp ()
	{
		await using var server = new LaunchAdbServer ();
		var arguments = new [] {
			Assembly.Load ("Microsoft.Android.Run").Location,
			"--adb", server.CreateFakeAdb (), "--package", PackageName, "--activity", ".MainActivity",
			"--attach-debugger", "--no-wait", "--no-wake-device", "--user", "0",
		};
		var processId = new TaskCompletionSource<int> (TaskCreationOptions.RunContinuationsAsynchronously);
		var arming = new TaskCompletionSource (TaskCreationOptions.RunContinuationsAsynchronously);
		var release = new TaskCompletionSource (TaskCreationOptions.RunContinuationsAsynchronously);
		server.BeforeResponse = command => {
			if (command.StartsWith ("am set-debug-app ", StringComparison.Ordinal)) {
				arming.TrySetResult ();
				return release.Task;
			}
			return Task.CompletedTask;
		};
		var run = RunDotnetAsync (arguments, process => processId.TrySetResult (process.Id));
		try {
			await arming.Task.WaitAsync (TimeSpan.FromSeconds (5));
			var pid = await processId.Task;
			await SendCtrlCAsync (pid);
			await Task.WhenAny (run, Task.Delay (100));
			Assert.IsFalse (run.IsCompleted);
			Assert.IsFalse (server.Commands.Contains ("am clear-debug-app"));
			release.TrySetResult ();
			var (exitCode, output, stderr) = await run.WaitAsync (TimeSpan.FromSeconds (10));
			Assert.AreEqual (130, exitCode, output + stderr);
			StringAssert.Contains ("Stopping application...", output);
			Assert.IsNull (server.DebugApp);
			var commands = server.Commands.ToArray ();
			int clear = Array.IndexOf (commands, "am clear-debug-app");
			int stop = Array.FindIndex (commands, c => c.StartsWith ("am force-stop ", StringComparison.Ordinal));
			Assert.GreaterOrEqual (clear, 0);
			Assert.Greater (stop, clear);
			Assert.IsFalse (commands.Any (c => c.StartsWith ("am start ", StringComparison.Ordinal)));
		} finally {
			release.TrySetResult ();
			await run.WaitAsync (TimeSpan.FromSeconds (10));
		}
	}

	[TestCase ("arm")]
	[TestCase ("cleanup")]
	[Category ("ManagedLaunchBoundaryRegression")]
	public async Task RunProgramPrivateBudgetTimeoutIsFailure (string boundary)
	{
		await using var server = new LaunchAdbServer ();
		var pending = new TaskCompletionSource (TaskCreationOptions.RunContinuationsAsynchronously);
		var command = boundary == "arm" ? "am set-debug-app 'com.example.managed'" : "am clear-debug-app";
		server.BeforeResponse = value => value == command ? pending.Task : Task.CompletedTask;
		var elapsed = Stopwatch.StartNew ();

		var (exitCode, output, error) = await RunProgramAsync (server,
			"--activity", ".MainActivity", "--attach-debugger", "--no-wait", "--no-wake-device");

		Assert.AreEqual (1, exitCode, output + error);
		StringAssert.Contains ("Timed out", error);
		StringAssert.Contains ("debug-app", error);
		Assert.GreaterOrEqual (elapsed.Elapsed, TimeSpan.FromSeconds (5));
		Assert.Less (elapsed.Elapsed, TimeSpan.FromSeconds (15));
		Assert.IsFalse (output.Contains ("Stopping application..."), "No Ctrl+C was sent.");
		Assert.IsFalse (server.Commands.Any (c => c.StartsWith ("am force-stop ", StringComparison.Ordinal)));
		Assert.AreEqual (boundary == "cleanup", server.Attached);
	}

	[Test]
	public async Task RunProgramNoWaitAndPortsAreNotDebugIntent ([Values] bool noWait, [Values] bool ports)
	{
		await using var server = new LaunchAdbServer ();
		server.SetDebugAppState ("com.example.other", true);
		var arguments = new List<string> { "--activity", ".MainActivity", "--no-wake-device" };
		if (noWait)
			arguments.Add ("--no-wait");
		if (ports)
			arguments.AddRange (new [] { "--forward-port", "10000:10000", "--reverse-port", "8000:8001" });

		var (exitCode, output, error) = await RunProgramAsync (server, arguments.ToArray ());

		Assert.AreEqual (0, exitCode, output + error);
		Assert.IsFalse (server.Commands.Any (c => c.Contains ("debug-app") || c == "dumpsys activity processes"));
		Assert.AreEqual ("com.example.other", server.DebugApp);
		var start = server.Commands.Single (c => c.StartsWith ("am start ", StringComparison.Ordinal));
		Assert.AreEqual (!noWait, start.Contains (" -W"));
		Assert.AreEqual (!noWait, server.Commands.Any (c => c.StartsWith ("logcat ", StringComparison.Ordinal)));
		Assert.AreEqual (ports, server.Commands.Contains ("forward tcp:10000 tcp:10000"));
		Assert.AreEqual (ports, server.Commands.Contains ("reverse tcp:8000 tcp:8001"));
	}

	[TestCase ("older-api")]
	[TestCase ("multiple-users")]
	[TestCase ("unsupported-layout")]
	[TestCase ("custom-process")]
	[Category ("ManagedLaunchBoundaryRegression")]
	public async Task RunProgramDebugFallbackWaitsForPid (string reason)
	{
		await using var server = new LaunchAdbServer ();
		if (reason == "older-api")
			server.ApiLevel = 30;
		if (reason == "multiple-users")
			server.UserList += "\tUserInfo{10:Work:30} running\n";
		if (reason == "custom-process")
			server.EffectiveProcessName = PackageName + ":custom";
		int pidQueries = 0;
		server.TransformResponse = (command, output) => {
			if (command.StartsWith ("pidof ", StringComparison.Ordinal))
				return (command == "pidof " + server.EffectiveProcessName || command == "pidof '" + server.EffectiveProcessName + "'") &&
					Interlocked.Increment (ref pidQueries) > 3 ? "1234\n" : "";
			if (reason == "unsupported-layout" && command == "dumpsys activity processes")
				return output + "  vendor postamble\n";
			return output;
		};

		var (exitCode, output, error) = await RunProgramAsync (server,
			"--activity", ".MainActivity", "--attach-debugger", "--no-wake-device");

		Assert.AreEqual (0, exitCode, output + error);
		Assert.GreaterOrEqual (pidQueries, 4);
		StringAssert.Contains ("managed-launch logcat", output);
		Assert.IsTrue (server.Commands.Any (c => c.StartsWith ("logcat ", StringComparison.Ordinal)));
		Assert.IsFalse (server.Commands.Any (c => c.Contains ("debug-app") || c.Contains (" -W")));
	}

	[TestCase ("com.example.managed:custom")]
	[TestCase ("com.example.managed:custom$worker")]
	[Category ("ManagedLaunchPidRegression")]
	public async Task RunProgramCustomProcessTracksStartupAndExit (string processName)
	{
		await using var server = new LaunchAdbServer { EffectiveProcessName = processName };
		var pidCommand = "pidof '" + processName + "'";
		int matchingQueries = 0;
		var logcatPending = new TaskCompletionSource (TaskCreationOptions.RunContinuationsAsynchronously);
		// Keep logcat alive so success requires observing the process exit, not
		// merely the fake logcat executable finishing before the next PID query.
		server.BeforeResponse = command => command.StartsWith ("logcat ", StringComparison.Ordinal) ? logcatPending.Task : Task.CompletedTask;
		server.TransformResponse = (command, output) => {
			if (!command.StartsWith ("pidof ", StringComparison.Ordinal))
				return output;
			if (command != pidCommand)
				return "";
			int query = Interlocked.Increment (ref matchingQueries);
			return query is 3 or 4 ? "1234\n" : "";
		};

		var (exitCode, output, error) = await RunProgramAsync (server,
			"--activity", ".MainActivity", "--attach-debugger", "--no-wake-device", "--verbose");

		Assert.AreEqual (0, exitCode, output + error);
		Assert.AreEqual (5, matchingQueries, "Two no-matches, startup PID, running PID, then exit.");
		Assert.That (server.Commands.Where (c => c.StartsWith ("pidof ", StringComparison.Ordinal)), Is.All.EqualTo (pidCommand));
		CollectionAssert.Contains (server.Commands, "logcat --pid=1234");
		StringAssert.Contains ("App has exited.", output);
		Assert.AreEqual (1, server.Commands.Count (c => c.StartsWith ("pm resolve-activity ", StringComparison.Ordinal)),
			"Reuse the process identity already resolved for launch eligibility.");
		Assert.IsFalse (server.Commands.Any (c => c.Contains ("debug-app")));
		CollectionAssert.Contains (server.Commands, "am start -S -n 'com.example.managed/.MainActivity'");
	}

	[TestCase ("stderr", false)]
	[TestCase ("stdout", false)]
	[TestCase ("stderr", true)]
	[Category ("ManagedLaunchPidRegression")]
	public async Task RunProgramDebugPidDiagnosticsFailPromptly (string channel, bool afterStartup)
	{
		await using var server = new LaunchAdbServer { ApiLevel = 30 };
		const string diagnostic = "adb: device offline\n";
		int pidQueries = 0;
		int failOnQuery = afterStartup ? 2 : 1;
		var logcatPending = new TaskCompletionSource (TaskCreationOptions.RunContinuationsAsynchronously);
		server.BeforeResponse = command => command.StartsWith ("logcat ", StringComparison.Ordinal) ? logcatPending.Task : Task.CompletedTask;
		server.TransformResponse = (command, output) => {
			if (!command.StartsWith ("pidof ", StringComparison.Ordinal))
				return output;
			if (Interlocked.Increment (ref pidQueries) < failOnQuery)
				return "1234\n";
			return channel == "stdout" ? diagnostic : "";
		};
		server.CliResult = command => command.StartsWith ("pidof ", StringComparison.Ordinal) && pidQueries >= failOnQuery
			? (1, channel == "stderr" ? diagnostic : "")
			: (0, "");
		var elapsed = Stopwatch.StartNew ();

		var (exitCode, output, error) = await RunProgramAsync (server,
			"--activity", ".MainActivity", "--attach-debugger", "--no-wake-device", "--verbose");

		Assert.AreEqual (1, exitCode, output + error);
		StringAssert.Contains (diagnostic.Trim (), error);
		StringAssert.DoesNotContain ("Timed out", error);
		Assert.AreEqual (failOnQuery, pidQueries, "Do not retry an ADB failure as pidof no-match.");
		Assert.Less (elapsed.Elapsed, TimeSpan.FromSeconds (10));
		if (afterStartup)
			StringAssert.Contains ("App PID: 1234", output);
		else
			Assert.IsFalse (server.Commands.Any (c => c.StartsWith ("logcat ", StringComparison.Ordinal)));
	}

	[Test]
	[Category ("ManagedLaunchPidRegression")]
	public async Task RunProgramNonDebugPidDiagnosticsRetainExistingBehavior ()
	{
		await using var server = new LaunchAdbServer ();
		server.TransformResponse = (command, output) => command.StartsWith ("pidof ", StringComparison.Ordinal) ? "" : output;
		server.CliResult = command => command.StartsWith ("pidof ", StringComparison.Ordinal) ? (1, "adb: device offline\n") : (0, "");

		var (exitCode, output, error) = await RunProgramAsync (server, "--activity", ".MainActivity", "--no-wake-device");

		Assert.AreEqual (1, exitCode, output + error);
		StringAssert.Contains ("could not retrieve PID", error);
		Assert.AreEqual (1, server.Commands.Count (c => c.StartsWith ("pidof ", StringComparison.Ordinal)));
		Assert.IsFalse (server.Commands.Any (c => c.Contains ("debug-app")));
	}

	[Test]
	[Category ("ManagedLaunchBoundaryRegression")]
	public async Task RunProgramPidPollingTimeoutIsFailure ()
	{
		await using var server = new LaunchAdbServer { ApiLevel = 30 };
		server.TransformResponse = (command, output) => command.StartsWith ("pidof ", StringComparison.Ordinal) ? "" : output;
		var elapsed = Stopwatch.StartNew ();

		var (exitCode, output, error) = await RunProgramAsync (server,
			"--activity", ".MainActivity", "--attach-debugger", "--no-wake-device");

		Assert.AreEqual (1, exitCode, output + error);
		StringAssert.Contains ("Timed out", error);
		StringAssert.Contains ("process", error);
		Assert.Greater (server.Commands.Count (c => c.StartsWith ("pidof ", StringComparison.Ordinal)), 1);
		Assert.GreaterOrEqual (elapsed.Elapsed, TimeSpan.FromSeconds (30));
		Assert.Less (elapsed.Elapsed, TimeSpan.FromSeconds (40));
		Assert.IsFalse (server.Commands.Any (c => c.StartsWith ("logcat ", StringComparison.Ordinal)));
	}

	[Test]
	[Category ("ManagedLaunchBoundaryRegression")]
	public async Task RunProgramPidPollingHonorsCtrlC ()
	{
		await using var server = new LaunchAdbServer { ApiLevel = 30 };
		var arguments = new [] {
			Assembly.Load ("Microsoft.Android.Run").Location,
			"--adb", server.CreateFakeAdb (), "--package", PackageName, "--activity", ".MainActivity",
			"--attach-debugger", "--no-wake-device",
		};
		var processId = new TaskCompletionSource<int> (TaskCreationOptions.RunContinuationsAsynchronously);
		var querying = new TaskCompletionSource (TaskCreationOptions.RunContinuationsAsynchronously);
		var pending = new TaskCompletionSource (TaskCreationOptions.RunContinuationsAsynchronously);
		server.BeforeResponse = command => {
			if (command.StartsWith ("pidof ", StringComparison.Ordinal)) {
				querying.TrySetResult ();
				return pending.Task;
			}
			return Task.CompletedTask;
		};
		var run = RunDotnetAsync (arguments, process => processId.TrySetResult (process.Id));
		await querying.Task.WaitAsync (TimeSpan.FromSeconds (5));
		await SendCtrlCAsync (await processId.Task);

		var (exitCode, output, error) = await run.WaitAsync (TimeSpan.FromSeconds (10));
		Assert.AreEqual (130, exitCode, output + error);
		StringAssert.Contains ("Stopping application...", output);
		Assert.IsFalse (server.Commands.Any (c => c.Contains ("debug-app") || c.StartsWith ("logcat ", StringComparison.Ordinal)));
		Assert.IsTrue (server.Commands.Any (c => c.StartsWith ("am force-stop ", StringComparison.Ordinal)));
	}

	[TestCase (false, false)]
	[TestCase (false, true)]
	[TestCase (true, true)]
	[Category ("ManagedLaunchBoundaryRegression")]
	public async Task RunProgramPidPollingIsDebugWaitOnly (bool attachDebugger, bool noWait)
	{
		await using var server = new LaunchAdbServer { ApiLevel = 30 };
		server.TransformResponse = (command, output) => command.StartsWith ("pidof ", StringComparison.Ordinal) ? "" : output;
		var arguments = new List<string> { "--activity", ".MainActivity", "--no-wake-device" };
		if (attachDebugger)
			arguments.Add ("--attach-debugger");
		if (noWait)
			arguments.Add ("--no-wait");

		var (exitCode, output, error) = await RunProgramAsync (server, arguments.ToArray ());

		Assert.AreEqual (noWait ? 0 : 1, exitCode, output + error);
		Assert.AreEqual (noWait ? 0 : 1, server.Commands.Count (c => c.StartsWith ("pidof ", StringComparison.Ordinal)));
		Assert.IsFalse (server.Commands.Any (c => c.Contains ("debug-app") || c.StartsWith ("logcat ", StringComparison.Ordinal)));
	}

	[Test]
	public async Task RunProgramInstrumentationIgnoresActivityDebugIntent ()
	{
		await using var server = new LaunchAdbServer ();
		var (exitCode, output, error) = await RunProgramAsync (server,
			"--instrument", "runner", "--attach-debugger", "--no-wait", "--forward-port", "10000:10000");

		Assert.AreEqual (0, exitCode, output + error);
		Assert.IsTrue (server.Commands.Any (c => c.StartsWith ("am instrument ", StringComparison.Ordinal)));
		Assert.IsFalse (server.Commands.Any (c => c.Contains ("debug-app") || c.StartsWith ("am start ", StringComparison.Ordinal)));
	}

	[Test]
	public async Task RunProgramDotnetTestDispatchDoesNotBecomeAnActivityLaunch ()
	{
		await using var server = new LaunchAdbServer ();
		var (exitCode, output, error) = await RunProgramAsync (server,
			"--instrument", "runner", "--server", "dotnettestcli", "--attach-debugger", "--no-wait");

		// No test pipe is provided: stop at the real MTP entry point without
		// starting a test host or replacing it with an activity launch.
		Assert.AreEqual (1, exitCode, output + error);
		StringAssert.Contains ("--dotnet-test-pipe", error);
		Assert.IsEmpty (server.Commands);
	}

	[TestCase ("Error: Activity not started, primary failure\n")]
	[TestCase ("Starting: Intent { cmp=com.example.managed/.MainActivity }\njava.lang.SecurityException: Permission Denial\n")]
	public async Task RunProgramPropagatesLaunchErrorsAndCleans (string output)
	{
		await using var server = new LaunchAdbServer ();
		server.TransformResponse = (command, result) => command.StartsWith ("am start ", StringComparison.Ordinal) ? output : result;
		var (exitCode, _, error) = await RunProgramAsync (server, "--activity", ".MainActivity", "--attach-debugger", "--no-wait");

		Assert.AreEqual (1, exitCode);
		StringAssert.Contains (output.Trim (), error);
		Assert.IsNull (server.DebugApp);
		CollectionAssert.Contains (server.Commands, "am clear-debug-app");
	}

	[Test]
	[Category ("ManagedLaunchBoundaryRegression")]
	public async Task RunProgramStartWarningsOnStderrAreSuccessful (
		[Values ("Warning: Activity not started, intent has been delivered to currently running top-most instance.\n",
			"Warning: Activity not started, its current task has been brought to the front\n")] string warning,
		[Values] bool fallback)
	{
		await using var server = new LaunchAdbServer { ApiLevel = fallback ? 30 : 36 };
		server.CliResult = command => command.StartsWith ("am start ", StringComparison.Ordinal) ? (0, warning) : (0, "");

		var (exitCode, output, error) = await RunProgramAsync (server,
			"--activity", ".MainActivity", "--attach-debugger", "--no-wait", "--no-wake-device");

		Assert.AreEqual (0, exitCode, output + error);
		StringAssert.Contains ("Stopping: com.example.managed", output);
		StringAssert.Contains ("Starting: Intent {", output);
		StringAssert.Contains (warning.Trim (), output + error);
		Assert.AreEqual (!fallback, server.Attached);
		Assert.IsNull (server.DebugApp);
		Assert.AreEqual (!fallback, server.Commands.Contains ("am clear-debug-app"));
	}

	[Test]
	[Category ("ManagedLaunchBoundaryRegression")]
	public async Task RunProgramStartErrorsOnStderrRemainFailures (
		[Values ("Error: Permission denied\n", "Exception occurred while executing 'start':\njava.lang.SecurityException\n",
			"java.lang.SecurityException: Permission Denial\n")] string diagnostic,
		[Values] bool fallback)
	{
		await using var server = new LaunchAdbServer { ApiLevel = fallback ? 30 : 36 };
		server.CliResult = command => command.StartsWith ("am start ", StringComparison.Ordinal) ? (0, diagnostic) : (0, "");

		var (exitCode, output, error) = await RunProgramAsync (server,
			"--activity", ".MainActivity", "--attach-debugger", "--no-wait", "--no-wake-device");

		Assert.AreEqual (1, exitCode, output + error);
		StringAssert.Contains (diagnostic.Trim (), error);
		Assert.IsNull (server.DebugApp);
		Assert.AreEqual (!fallback, server.Commands.Contains ("am clear-debug-app"));
	}

	[Test]
	[Category ("ManagedLaunchBoundaryRegression")]
	public async Task RunProgramStartNonzeroExitIsFailure ()
	{
		await using var server = new LaunchAdbServer ();
		server.CliResult = command => command.StartsWith ("am start ", StringComparison.Ordinal)
			? (1, "Warning: Activity not started, its current task has been brought to the front\n")
			: (0, "");

		var (exitCode, output, error) = await RunProgramAsync (server,
			"--activity", ".MainActivity", "--attach-debugger", "--no-wait", "--no-wake-device");

		Assert.AreEqual (1, exitCode, output + error);
		StringAssert.Contains ("exit code 1", error);
		Assert.IsNull (server.DebugApp);
		CollectionAssert.Contains (server.Commands, "am clear-debug-app");
	}

	[TestCase ("am set-debug-app 'com.example.managed'")]
	[TestCase ("am clear-debug-app")]
	[Category ("ManagedLaunchBoundaryRegression")]
	public async Task RunProgramMutationStderrRemainsFailure (string command)
	{
		await using var server = new LaunchAdbServer ();
		const string diagnostic = "Warning: unexpected mutation output\n";
		server.CliResult = value => value == command ? (0, diagnostic) : (0, "");

		var (exitCode, output, error) = await RunProgramAsync (server,
			"--activity", ".MainActivity", "--attach-debugger", "--no-wait", "--no-wake-device");

		Assert.AreEqual (1, exitCode, output + error);
		StringAssert.Contains (diagnostic.Trim (), error);
		CollectionAssert.Contains (server.Commands, command);
	}

	[TestCase ("getprop ro.build.version.sdk")]
	[TestCase ("dumpsys activity processes")]
	[Category ("ManagedLaunchBoundaryRegression")]
	public async Task RunProgramQueryStderrDoesNotFallback (string command)
	{
		await using var server = new LaunchAdbServer ();
		const string diagnostic = "Error: Permission denied\n";
		server.CliResult = value => value == command ? (0, diagnostic) : (0, "");

		var (exitCode, output, error) = await RunProgramAsync (server,
			"--activity", ".MainActivity", "--attach-debugger", "--no-wait", "--no-wake-device");

		Assert.AreEqual (1, exitCode, output + error);
		StringAssert.Contains (diagnostic.Trim (), error);
		Assert.IsFalse (server.Commands.Any (c => c.StartsWith ("am ", StringComparison.Ordinal)));
	}

	[TestCase ("get-serialno")]
	[TestCase ("getprop ro.build.version.sdk")]
	[TestCase ("dumpsys activity processes")]
	public async Task RunProgramTransportFailureDoesNotFallback (string command)
	{
		await using var server = new LaunchAdbServer { FailTransportCommand = command };
		var (exitCode, _, error) = await RunProgramAsync (server, "--activity", ".MainActivity", "--attach-debugger", "--no-wait");

		Assert.AreEqual (1, exitCode);
		StringAssert.Contains ("simulated transport fail", error);
		Assert.IsFalse (server.Commands.Any (c => c.StartsWith ("am ", StringComparison.Ordinal)));
	}

	[TestCase (".Outer$Inner")]
	[TestCase (".Activity'Literal")]
	public async Task RunProgramQuotesTheDeviceComponent (string activity)
	{
		await using var server = new LaunchAdbServer ();
		// Force a metadata fallback: even that path must quote for the device
		// shell, not just ProcessStartInfo's host-side argument parser.
		server.TransformResponse = (command, output) => command.StartsWith ("pm resolve-activity ", StringComparison.Ordinal) ? "" : output;
		var (exitCode, output, error) = await RunProgramAsync (server,
			"--activity", activity, "--attach-debugger", "--no-wait", "--no-wake-device");

		Assert.AreEqual (0, exitCode, output + error);
		var literal = "'" + PackageName + "/" + activity.Replace ("'", "'\\''") + "'";
		CollectionAssert.Contains (server.Commands, $"pm resolve-activity --user '0' -n {literal}");
		CollectionAssert.Contains (server.Commands, $"am start -S -n {literal}");
		Assert.IsFalse (server.Commands.Any (c => c.Contains ("debug-app")));
	}

	[Test]
	public async Task RunArgumentsCarryOnlyExplicitActivityDebugIntent (
		[Values ("", "false", "TrUe")] string attachDebugger,
		[Values] bool debuggerServer,
		[Values] bool instrumentation)
	{
		var directory = Path.Combine (Path.GetTempPath (), $"managed-launch-msbuild-{Guid.NewGuid ():N}");
		Directory.CreateDirectory (directory);
		var project = Path.Combine (directory, "run.proj");
		try {
			// Import the shipping target, not a test-side copy of its conditions.
			new XDocument (new XElement ("Project",
				new XElement ("PropertyGroup",
					new XElement ("_XamarinAndroidBuildTasksAssembly", typeof (RunActivity).Assembly.Location),
					new XElement ("PrepTasksAssembly", typeof (RunActivity).Assembly.Location)),
				new XElement ("Import", new XAttribute ("Project", Path.Combine (TestContext.CurrentContext.TestDirectory, "Microsoft.Android.Sdk.Application.targets"))),
				new XElement ("PropertyGroup",
					new XElement ("_AndroidComputeRunArgumentsDependsOn", "TestNoOp"),
					new XElement ("_AndroidPackage", PackageName),
					new XElement ("AndroidLaunchActivity", ".MainActivity"),
					new XElement ("AndroidInstrumentation", instrumentation ? "runner" : ""),
					new XElement ("AndroidAttachDebugger", attachDebugger),
					new XElement ("AndroidDebuggerServer", debuggerServer),
					new XElement ("_AndroidRunAttachDebuggerArg", "--attach-debugger"),
					new XElement ("Configuration", "Debug"),
					new XElement ("WaitForExit", "false")),
				new XElement ("Target", new XAttribute ("Name", "TestNoOp")),
				new XElement ("Target", new XAttribute ("Name", "ComputeRunArguments")))).Save (project);
			var (exitCode, arguments, error) = await RunDotnetAsync (
				"msbuild", project, "-nologo", "-t:_AndroidComputeRunArguments", "-getProperty:RunArguments");

			Assert.AreEqual (0, exitCode, arguments + error);
			bool debug = string.Equals (attachDebugger, "true", StringComparison.OrdinalIgnoreCase);
			Assert.AreEqual (debug && !instrumentation, arguments.Contains ("--attach-debugger"), arguments);
			Assert.AreEqual (debug && debuggerServer, arguments.Contains ("--forward-port"), arguments);
			Assert.AreEqual (instrumentation, arguments.Contains ("--instrument"), arguments);
			Assert.AreEqual (!instrumentation, arguments.Contains ("--activity"), arguments);
			StringAssert.Contains ("--no-wait", arguments);
		} finally {
			File.Delete (project);
			Directory.Delete (directory);
		}
	}

	[TestCase (false, false)]
	[TestCase (false, true)]
	[TestCase (true, false)]
	[TestCase (true, true)]
	public async Task RunActivityProtectsOnlyManagedDebugLaunches (bool attachDebugger, bool allowJavaDebugging)
	{
		await using var server = new LaunchAdbServer ();
		var task = CreateRunActivity (server);
		task.AttachDebugger = attachDebugger;
		task.AllowJavaDebugging = allowJavaDebugging;

		Assert.IsTrue (task.Execute ());

		Assert.AreEqual (attachDebugger && !allowJavaDebugging,
			server.Commands.Any (c => c.StartsWith ("am set-debug-app ", StringComparison.Ordinal)));
		Assert.IsNull (server.DebugApp);
		Assert.IsTrue (server.Commands.Any (c => c.StartsWith ("am start ", StringComparison.Ordinal)));
		Assert.AreEqual (attachDebugger, server.Commands.Contains ("date +%s"),
			"Managed debugger properties must still be prepared on both debug paths.");
		if (attachDebugger && !allowJavaDebugging) {
			Assert.IsTrue (server.Attached);
			Assert.IsFalse (server.Commands.Any (c => c.Contains (" -D") || c.Contains (" -W") || c.StartsWith ("ps", StringComparison.Ordinal)));
			var start = server.Commands.Single (c => c.StartsWith ("am start ", StringComparison.Ordinal));
			StringAssert.Contains ("-a android.intent.action.MAIN -c android.intent.category.LAUNCHER", start);
		}
	}

	[Test]
	public async Task RunActivityExplicitJavaDebuggingStillUsesDAndJdwp ()
	{
		await using var server = new LaunchAdbServer ("emulator-5554");
		var task = CreateRunActivity (server);
		task.AttachDebugger = true;
		task.AllowJavaDebugging = true;
		var cancelled = new TaskCompletionSource (TaskCreationOptions.RunContinuationsAsynchronously);
		using var registration = task.CancellationToken.Register (() => cancelled.TrySetResult ());
		server.BeforeResponse = command => {
			if (command.StartsWith ("ps", StringComparison.Ordinal)) {
				// Confirm cancellation before allowing PID discovery to finish:
				// the legacy JDWP forwarder otherwise uses AdbServer.Default.
				((ICancelableTask) task).Cancel ();
				return cancelled.Task;
			}
			return Task.CompletedTask;
		};
		try {
			await Task.Run (() => task.Execute ()).WaitAsync (TimeSpan.FromSeconds (8));
			await task.Finished.Task.WaitAsync (TimeSpan.FromSeconds (5));
			Assert.IsTrue (server.Commands.Any (c => c.StartsWith ("am start ", StringComparison.Ordinal) && c.Contains (" -D")));
			Assert.IsTrue (server.Commands.Any (c => c.StartsWith ("ps", StringComparison.Ordinal)));
			Assert.IsFalse (server.Commands.Any (c => c.Contains ("debug-app")));
		} finally {
			((AsyncTask) task).Cancel ();
		}
	}

	[TestCase ("warm")]
	[TestCase ("older-api")]
	[TestCase ("multiple-users")]
	[TestCase ("different-user")]
	[TestCase ("custom-process")]
	[TestCase ("unsupported-layout")]
	public async Task RunActivityUnsupportedLaunchesRetainLegacyBehavior (string reason)
	{
		await using var server = new LaunchAdbServer ();
		var messages = new List<BuildMessageEventArgs> ();
		var task = CreateRunActivity (server, messages: messages);
		task.AttachDebugger = true;
		task.AllowJavaDebugging = false;
		task.ForceStop = reason != "warm";
		if (reason == "older-api")
			server.ApiLevel = 30;
		if (reason == "multiple-users")
			server.UserList += "\tUserInfo{10:Work:30} running\n";
		if (reason == "different-user")
			task.UserID = 10;
		if (reason == "custom-process")
			server.EffectiveProcessName = PackageName + ":custom";
		if (reason == "unsupported-layout")
			server.TransformResponse = (command, output) => command == "dumpsys activity processes" ? output + "  vendor postamble\n" : output;

		Assert.IsTrue (task.Execute ());
		Assert.IsFalse (server.Commands.Any (c => c.Contains ("debug-app")));
		var expected = new AmStartCommand (PackageName, ".MainActivity") {
			ForceStop = task.ForceStop,
			User = task.UserID.ToString (CultureInfo.InvariantCulture),
			Action = "android.intent.action.MAIN",
			Categories = new [] { "android.intent.category.LAUNCHER" },
		};
		CollectionAssert.Contains (server.Commands, expected.ToString ());
		Assert.IsTrue (messages.Any (m => m.Message.Contains ("Launching without")));
	}

	[TestCase ("Error: Activity class {com.example.managed/.MainActivity} does not exist.\n", true)]
	[TestCase ("Error: primary launch failure\n", false)]
	public async Task RunActivityPreservesTypedDiagnosticsAndCleans (string diagnostic, bool notFound)
	{
		await using var server = new LaunchAdbServer ();
		var errors = new List<BuildErrorEventArgs> ();
		var task = CreateRunActivity (server, errors: errors);
		task.AttachDebugger = true;
		task.AllowJavaDebugging = false;
		server.TransformResponse = (command, output) => command.StartsWith ("am start ", StringComparison.Ordinal) ? output + diagnostic : output;

		Assert.IsFalse (task.Execute ());
		Assert.IsTrue (errors.Any (e => e.Code.StartsWith ("XARUNA", StringComparison.Ordinal)));
		Assert.IsTrue (errors.Any (e => e.Message.Contains (notFound ? "ActivityNotFoundException" : "primary launch failure")));
		Assert.IsNull (server.DebugApp);
		CollectionAssert.Contains (server.Commands, "am clear-debug-app");
	}

	[TestCase ("arm")]
	[TestCase ("cleanup")]
	[Category ("ManagedLaunchBoundaryRegression")]
	public async Task RunActivityPrivateBudgetTimeoutIsFailure (string boundary)
	{
		await using var server = new LaunchAdbServer ();
		var errors = new List<BuildErrorEventArgs> ();
		var task = CreateRunActivity (server, errors: errors);
		task.AttachDebugger = true;
		task.AllowJavaDebugging = false;
		var pending = new TaskCompletionSource (TaskCreationOptions.RunContinuationsAsynchronously);
		var command = boundary == "arm" ? "am set-debug-app 'com.example.managed'" : "am clear-debug-app";
		server.BeforeResponse = value => value == command ? pending.Task : Task.CompletedTask;

		Assert.IsFalse (await Task.Run (() => task.Execute ()).WaitAsync (TimeSpan.FromSeconds (15)));
		Assert.IsTrue (errors.Any (e => e.Code == "XARUNA7017" && e.Message.Contains ("Timed out")));
		Assert.IsFalse (errors.Any (e => e.Code == "XARUNA7012" || e.Code == "XARUNA7013"));
		Assert.AreEqual (boundary == "cleanup", server.Attached);
	}

	[Test]
	public async Task RunActivityReportsBothPrimaryAndCleanupErrors ()
	{
		await using var server = new LaunchAdbServer ();
		var errors = new List<BuildErrorEventArgs> ();
		var task = CreateRunActivity (server, errors: errors);
		task.AttachDebugger = true;
		task.AllowJavaDebugging = false;
		server.TransformResponse = (command, output) => command.StartsWith ("am start ", StringComparison.Ordinal)
			? "Error: primary launch failure"
			: command == "am clear-debug-app" ? "cleanup failure" : output;

		Assert.IsFalse (task.Execute ());
		Assert.IsTrue (errors.Any (e => e.Message.Contains ("primary launch failure")));
		Assert.IsTrue (errors.Any (e => e.Message.Contains ("Failed to clean up") && e.Message.Contains ("cleanup failure")));
	}

	[Test]
	public async Task RunActivityCancellationDrainsMutationBeforeReturning ()
	{
		await using var server = new LaunchAdbServer ();
		var task = CreateRunActivity (server);
		task.AttachDebugger = true;
		task.AllowJavaDebugging = false;
		var arming = new TaskCompletionSource (TaskCreationOptions.RunContinuationsAsynchronously);
		var release = new TaskCompletionSource (TaskCreationOptions.RunContinuationsAsynchronously);
		server.BeforeResponse = command => {
			if (command.StartsWith ("am set-debug-app ", StringComparison.Ordinal)) {
				arming.TrySetResult ();
				return release.Task;
			}
			return Task.CompletedTask;
		};
		var launch = Task.Run (() => task.Execute ());
		try {
			await arming.Task.WaitAsync (TimeSpan.FromSeconds (5));
			((ICancelableTask) task).Cancel ();
			await Task.WhenAny (launch, Task.Delay (100));
			Assert.IsFalse (launch.IsCompleted, "MSBuild must not return success or abandon mutation cleanup on cancellation.");
			Assert.IsFalse (server.Commands.Contains ("am clear-debug-app"), "Cleanup cannot overtake the in-flight set-debug-app reply.");
			release.TrySetResult ();
			Assert.IsFalse (await launch.WaitAsync (TimeSpan.FromSeconds (8)));
			Assert.IsNull (server.DebugApp);
			Assert.IsFalse (server.Commands.Any (c => c.StartsWith ("am start ", StringComparison.Ordinal)));
		} finally {
			release.TrySetResult ();
			await task.Finished.Task.WaitAsync (TimeSpan.FromSeconds (8));
		}
	}

	[TestCase ("warm")]
	[TestCase ("custom-process")]
	[TestCase ("unsupported-layout")]
	public async Task RunActivityFallbackCancellationIsNotSuccess (string reason)
	{
		await using var server = new LaunchAdbServer ();
		var task = CreateRunActivity (server);
		task.AttachDebugger = true;
		task.AllowJavaDebugging = false;
		task.ForceStop = reason != "warm";
		if (reason == "custom-process")
			server.EffectiveProcessName = PackageName + ":custom";
		if (reason == "unsupported-layout")
			server.TransformResponse = (command, output) => command == "dumpsys activity processes" ? output + "  vendor postamble\n" : output;
		var starting = new TaskCompletionSource (TaskCreationOptions.RunContinuationsAsynchronously);
		var release = new TaskCompletionSource (TaskCreationOptions.RunContinuationsAsynchronously);
		server.BeforeResponse = command => {
			if (command.StartsWith ("am start ", StringComparison.Ordinal)) {
				starting.TrySetResult ();
				return release.Task;
			}
			return Task.CompletedTask;
		};
		var launch = Task.Run (() => task.Execute ());
		try {
			await starting.Task.WaitAsync (TimeSpan.FromSeconds (5));
			((ICancelableTask) task).Cancel ();
			Assert.IsFalse (await launch.WaitAsync (TimeSpan.FromSeconds (7)));
			Assert.IsFalse (server.Commands.Any (c => c.Contains ("debug-app")));
		} finally {
			release.TrySetResult ();
			await task.Finished.Task.WaitAsync (TimeSpan.FromSeconds (8));
		}
	}

	[Test]
	public async Task LegacyDebuggingLibraryDoesNotOwnManagedProtection ()
	{
		await using var server = new LaunchAdbServer ();
		var configuration = new ExecutionConfiguration (PackageName, new AmStartCommand (PackageName, ".MainActivity") {
			ForceStop = true,
		}) {
			AllowJavaDebugging = false,
		};

		await server.Device.StartWithDebuggingAsync (configuration, CancellationToken.None);

		Assert.IsFalse (server.Commands.Any (c => c.Contains ("debug-app")),
			"The deprecated library must retain its pre-PR behavior, not implement the new transaction.");
	}

	static ObservedRunActivity CreateRunActivity (LaunchAdbServer server, IList<BuildErrorEventArgs> errors = null, IList<BuildMessageEventArgs> messages = null)
	{
		var target = "-s " + server.Serial;
		IBuildEngine4 engine = new MockBuildEngine (TestContext.Out, errors: errors, messages: messages);
		// Use the task's existing per-build device cache, never AdbServer.Default.
		engine.RegisterTaskObjectAssemblyLocal (
			Tuple.Create ("AndroidHelper_AndroidDevice", target),
			server.Device, RegisteredTaskObjectLifetime.Build);
		return new ObservedRunActivity {
			BuildEngine = engine,
			AdbTarget = target,
			PackageName = PackageName,
			ActivityName = ".MainActivity",
			Server = true,
		};
	}

	sealed class ObservedRunActivity : RunActivity
	{
		internal TaskCompletionSource Finished { get; } = new TaskCompletionSource (TaskCreationOptions.RunContinuationsAsynchronously);

		public override async Task RunTaskAsync ()
		{
			try {
				await base.RunTaskAsync ();
			} finally {
				Finished.TrySetResult ();
			}
		}
	}

	static Task<(int ExitCode, string Output, string Error)> RunProgramAsync (LaunchAdbServer server, params string [] arguments) =>
		RunProgramAsync (new [] { "--adb", server.CreateFakeAdb (), "--adb-target", "-d", "--package", PackageName }.Concat (arguments).ToArray ());

	static Task<(int ExitCode, string Output, string Error)> RunProgramAsync (params string [] arguments)
	{
		var program = Assembly.Load ("Microsoft.Android.Run").Location;
		return RunDotnetAsync (new [] { program }.Concat (arguments).ToArray ());
	}

	static async Task SendCtrlCAsync (int processId)
	{
		var signal = ProcessUtils.CreateProcessStartInfo ("/bin/kill", "-INT", processId.ToString (CultureInfo.InvariantCulture));
		using var error = new StringWriter ();
		using var timeout = new CancellationTokenSource (TimeSpan.FromSeconds (5));
		Assert.AreEqual (0, await ProcessUtils.StartProcess (signal, TextWriter.Null, error, timeout.Token), error.ToString ());
	}

	static Task<(int ExitCode, string Output, string Error)> RunDotnetAsync (params string [] arguments) =>
		RunDotnetAsync (arguments, onStarted: null);

	static async Task<(int ExitCode, string Output, string Error)> RunDotnetAsync (string [] arguments, Action<Process> onStarted)
	{
		var dotnet = Environment.GetEnvironmentVariable ("DOTNET_HOST_PATH") ?? "dotnet";
		var psi = ProcessUtils.CreateProcessStartInfo (dotnet, arguments);
		using var output = new StringWriter ();
		using var error = new StringWriter ();
		using var timeout = new CancellationTokenSource (TimeSpan.FromSeconds (45));
		var exitCode = await ProcessUtils.StartProcess (psi, output, error, timeout.Token, onStarted: onStarted);
		return (exitCode, output.ToString (), error.ToString ());
	}
}
