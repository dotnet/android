// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Mono.AndroidTools;
using NUnit.Framework;
using Xamarin.AndroidTools.Debugging;
using ManagedActivityLaunch = Microsoft.Android.Run.ManagedActivityLaunch;

namespace Xamarin.Android.Tools.Tests;

[TestFixture]
public class ManagedActivityLaunchTests
{
	const string PackageName = "com.example.managed";

	static ExecutionConfiguration Configuration (bool forceStop = true)
	{
		var configuration = new ExecutionConfiguration (PackageName, new AmStartCommand (PackageName, ".MainActivity") {
			ForceStop = forceStop,
			User = "0",
		}) {
			AllowJavaDebugging = false,
		};
		configuration.Debugger.Timeout = TimeSpan.FromSeconds (2);
		return configuration;
	}

	static Task<string> LaunchAsync (AndroidDevice device, ExecutionConfiguration configuration, CancellationToken token)
	{
		var command = configuration.RunCommand as AmStartCommand;
		Assert.IsNotNull (command);
		// Exercise the implementation compiled into the real run executable; only
		// transport/setup/fallback callbacks come from this private ADB fixture.
		return ManagedActivityLaunch.RunAsync (
			device.ID, configuration.PackageName, command.Component, command.User, command.ForceStop,
			command.ToString (), configuration.Debugger.Timeout,
			prepare: t => device.SetDebugPropertiesAsync (configuration.PackageName, configuration.Debugger, t),
			runShellCommand: device.RunShellCommand,
			launchUnprotected: t => device.ExecuteIntentCommandAsync (command, configuration.LogWiter, t),
			log: message => configuration.LogWiter?.Invoke (message),
			logCleanupError: AndroidLogger.LogError,
			token: token);
	}

	[Test]
	public async Task ManagedLaunchArmsWithoutJavaWaitAndCleansAfterAttach ()
	{
		await using var server = new LaunchAdbServer ();
		var processName = await LaunchAsync (server.Device, Configuration (), CancellationToken.None);

		Assert.AreEqual (PackageName, processName);
		var commands = server.Commands.ToArray ();
		CollectionAssert.Contains (commands, "am set-debug-app 'com.example.managed'");
		Assert.IsFalse (commands.Any (c => c.Contains ("-w") || c.Contains ("--persistent") || c.Contains (" -D")));
		Assert.Greater (Array.LastIndexOf (commands, "am clear-debug-app"), Array.FindIndex (commands, c => c.StartsWith ("am start ", StringComparison.Ordinal)));
		Assert.IsTrue (server.Attached);
		Assert.IsNull (server.DebugApp);
	}

	[Test]
	public async Task VisiblePidDoesNotAllowCleanupBeforeAttach ()
	{
		await using var server = new LaunchAdbServer { AttachOnDump = false };
		var observedPid = new TaskCompletionSource (TaskCreationOptions.RunContinuationsAsynchronously);
		var allowAttach = new TaskCompletionSource (TaskCreationOptions.RunContinuationsAsynchronously);
		server.TransformResponse = (command, output) => {
			if (command == "dumpsys activity processes" && output.Contains ("pid=1234")) {
				observedPid.TrySetResult ();
				server.AttachOnDump = allowAttach.Task.IsCompleted;
			}
			return output;
		};
		var launch = LaunchAsync (server.Device, Configuration (), CancellationToken.None);
		await observedPid.Task.WaitAsync (TimeSpan.FromSeconds (5));
		Assert.IsFalse (launch.IsCompleted);
		Assert.AreEqual (PackageName, server.DebugApp);
		Assert.IsFalse (server.Commands.Contains ("am clear-debug-app"));
		allowAttach.SetResult ();
		await launch;
		Assert.IsTrue (server.Attached);
		Assert.IsNull (server.DebugApp);
	}

	[TestCase ("Error: Activity not started, unable to resolve Intent")]
	[TestCase ("Starting: Intent { cmp=com.example.managed/.MainActivity }\nError type 3\nError: Activity class does not exist.")]
	[TestCase ("java.lang.SecurityException: Permission Denial")]
	[TestCase ("/system/bin/sh: am: not found")]
	[TestCase ("Starting: Intent { cmp=com.example.managed/.MainActivity }\nError: Permission denied")]
	[TestCase ("Starting: Intent { cmp=com.example.managed/.MainActivity }\nException occurred while executing 'start':\njava.lang.SecurityException")]
	public async Task LaunchTextErrorsArePropagatedAndCleaned (string error)
	{
		await using var server = new LaunchAdbServer ();
		server.TransformResponse = (command, output) => command.StartsWith ("am start ", StringComparison.Ordinal) ? error : output;
		Assert.CatchAsync (() => LaunchAsync (server.Device, Configuration (), CancellationToken.None));
		Assert.IsNull (server.DebugApp);
		CollectionAssert.Contains (server.Commands, "am clear-debug-app");
	}

	[TestCase ("ExceptionActivity", "https://example.com/")]
	[TestCase ("MainActivity", "https://example.com/Error:Details")]
	public async Task DiagnosticWordsInSuccessfulIntentAreNotLaunchErrors (string activity, string uri)
	{
		await using var server = new LaunchAdbServer ();
		var configuration = Configuration ();
		configuration.RunCommand.Component = PackageName + "/." + activity;
		configuration.RunCommand.DataUri = uri;
		server.TransformResponse = (command, output) => command.StartsWith ("am start ", StringComparison.Ordinal)
			? $"Starting: Intent {{ dat={uri} cmp={configuration.RunCommand.Component} }}\n"
			: output;
		await LaunchAsync (server.Device, configuration, CancellationToken.None);
		Assert.IsTrue (server.Attached);
		Assert.IsNull (server.DebugApp);
	}

	[TestCase ("")]
	[TestCase ("Warning: Activity not started because the  current activity is being kept for the user.\n")]
	[TestCase ("Warning: Activity not started, intent has been delivered to currently running top-most instance.\n")]
	[TestCase ("Warning: Activity not started because intent should be handled by the caller\n")]
	[TestCase ("Warning: Activity not started, its current task has been brought to the front\n")]
	public async Task ForceStopPreambleAndSuccessfulWarningsAreAccepted (string warning)
	{
		await using var server = new LaunchAdbServer ();
		server.TransformResponse = (command, output) => command.StartsWith ("am start ", StringComparison.Ordinal) ? output + warning : output;
		await LaunchAsync (server.Device, Configuration (), CancellationToken.None);
		Assert.IsTrue (server.Attached);
		Assert.IsNull (server.DebugApp);
	}

	[TestCase ("Error: Permission denied\n", false)]
	[TestCase ("Exception occurred while executing 'start':\njava.lang.SecurityException\n", false)]
	[TestCase ("Error type 3\nError: Activity class {com.example.managed/.MainActivity} does not exist.\n", true)]
	public async Task ForceStopPreambleDoesNotHideLaterErrors (string diagnostic, bool notFound)
	{
		await using var server = new LaunchAdbServer ();
		server.TransformResponse = (command, output) => command.StartsWith ("am start ", StringComparison.Ordinal) ? output + diagnostic : output;
		var error = Assert.CatchAsync<ManagedActivityLaunch.CommandFailedException> (() => LaunchAsync (server.Device, Configuration (), CancellationToken.None));
		if (notFound)
			Assert.IsTrue (error.ActivityNotFound);
		else
			StringAssert.Contains (diagnostic.Trim (), error.Message);
		Assert.IsNull (server.DebugApp);
	}

	[TestCase ("application :app", "com.example.managed:app", "com.example.managed:app")]
	[TestCase ("application absolute", "com.example.shared", "com.example.shared")]
	[TestCase ("activity :ui", "com.example.managed", "com.example.managed:ui")]
	[TestCase ("activity absolute", "com.example.managed", "com.example.ui")]
	[TestCase ("activity overrides application", "com.example.managed:app", "com.example.ui")]
	public async Task CustomProcessRetainsUnprotectedLaunch (string declaration, string applicationProcess, string effectiveProcess)
	{
		await using var server = new LaunchAdbServer {
			ApplicationProcessName = applicationProcess,
			EffectiveProcessName = effectiveProcess,
		};
		// These are PackageManager's resolved values for the named manifest declarations,
		// not a test-side reimplementation of manifest process-name resolution.
		var configuration = Configuration ();
		var messages = new List<string> ();
		configuration.LogWiter = messages.Add;
		configuration.Debugger.Timeout = TimeSpan.FromMilliseconds (200);
		// Isolate process eligibility from the separate force-stop preamble regression.
		server.TransformResponse = (command, output) => command.StartsWith ("am start ", StringComparison.Ordinal)
			? output.Replace ("Stopping: com.example.managed\n", "")
			: output;
		var processName = await LaunchAsync (server.Device, configuration, CancellationToken.None);
		Assert.AreEqual (effectiveProcess, processName);
		CollectionAssert.Contains (server.Commands, configuration.RunCommand.ToString (), declaration);
		Assert.IsFalse (server.Commands.Any (c => c.Contains ("debug-app")), declaration);
		Assert.IsTrue (messages.Any (m => m.Contains ("process")), declaration);
	}

	[Test]
	public async Task ActivityCanOverrideCustomApplicationProcessBackToPackage ()
	{
		await using var server = new LaunchAdbServer { ApplicationProcessName = "com.example.managed:app" };
		var processName = await LaunchAsync (server.Device, Configuration (), CancellationToken.None);
		Assert.AreEqual (PackageName, processName);
		CollectionAssert.Contains (server.Commands, "pm resolve-activity --user '0' -n 'com.example.managed/.MainActivity'");
		CollectionAssert.Contains (server.Commands, "am set-debug-app 'com.example.managed'");
		Assert.IsTrue (server.Attached);
	}

	[TestCase ("")]
	[TestCase ("No activity found\n")]
	[TestCase ("ActivityInfo:\n  name=com.example.managed.MainActivity\n  packageName=com.example.managed\n")]
	[TestCase ("ActivityInfo:\n  name=com.example.managed.MainActivity\n  packageName=com.example.managed\n  processName=\n  enabled=true exported=true directBootAware=false\n  ApplicationInfo:\n")]
	public async Task UnconfirmedProcessMetadataDoesNotArm (string metadata)
	{
		await using var server = new LaunchAdbServer ();
		var configuration = Configuration ();
		var messages = new List<string> ();
		configuration.LogWiter = messages.Add;
		server.TransformResponse = (command, output) => command.StartsWith ("pm resolve-activity ", StringComparison.Ordinal) ? metadata : output;
		var processName = await LaunchAsync (server.Device, configuration, CancellationToken.None);
		Assert.IsNull (processName, "Unconfirmed metadata must not produce a guessed process identity.");
		Assert.IsFalse (server.Commands.Any (c => c.Contains ("debug-app")));
		CollectionAssert.Contains (server.Commands, configuration.RunCommand.ToString ());
		Assert.IsTrue (messages.Any (m => m.Contains ("could not be confirmed")));
	}

	[Test]
	public async Task CancellationDuringProcessResolutionDoesNotLaunchOrArm ()
	{
		await using var server = new LaunchAdbServer ();
		using var cancellation = new CancellationTokenSource ();
		server.TransformResponse = (command, output) => {
			if (command.StartsWith ("pm resolve-activity ", StringComparison.Ordinal))
				cancellation.Cancel ();
			return output;
		};
		Assert.CatchAsync<OperationCanceledException> (() => LaunchAsync (server.Device, Configuration (), cancellation.Token));
		Assert.IsFalse (server.Commands.Any (c => c.StartsWith ("am ", StringComparison.Ordinal)));
	}

	[Test]
	public async Task ResolverTransportFailureIsPropagatedBeforeArming ()
	{
		await using var server = new LaunchAdbServer {
			FailTransportCommand = "pm resolve-activity --user '0' -n 'com.example.managed/.MainActivity'",
		};
		var error = Assert.CatchAsync (() => LaunchAsync (server.Device, Configuration (), CancellationToken.None));
		StringAssert.Contains ("simulated transport fail", error.ToString ());
		Assert.IsFalse (server.Commands.Any (c => c.StartsWith ("am ", StringComparison.Ordinal)));
	}

	[Test]
	public async Task LaunchTransportFailureIsNotSuccess ()
	{
		await using var server = new LaunchAdbServer ();
		var configuration = Configuration ();
		server.FailTransportCommand = configuration.RunCommand.ToString ();
		var error = Assert.CatchAsync (() => LaunchAsync (server.Device, configuration, CancellationToken.None));
		StringAssert.Contains ("simulated transport fail", error.ToString ());
		Assert.IsNull (server.DebugApp);
		CollectionAssert.Contains (server.Commands, "am clear-debug-app");
	}

	[Test]
	public async Task AttachTimeoutCleansAndAllowsRetry ()
	{
		await using var server = new LaunchAdbServer { AttachOnDump = false };
		var configuration = Configuration ();
		configuration.Debugger.Timeout = TimeSpan.FromMilliseconds (150);
		var error = Assert.ThrowsAsync<TimeoutException> (() => LaunchAsync (server.Device, configuration, CancellationToken.None));
		Assert.IsInstanceOf<TimeoutException> (error.GetBaseException (), "The task host must classify the deadline as a timeout.");
		Assert.IsNull (server.DebugApp);
		server.AttachOnDump = true;
		configuration.Debugger.Timeout = TimeSpan.FromSeconds (2);
		await LaunchAsync (server.Device, configuration, CancellationToken.None);
		Assert.IsNull (server.DebugApp);
		Assert.AreEqual (2, server.Commands.Count (c => c.StartsWith ("am set-debug-app", StringComparison.Ordinal)));
	}

	[TestCase ("flag-missing")]
	[TestCase ("package-prefix")]
	[TestCase ("wrong-user")]
	[TestCase ("marker-missing")]
	public async Task ConsumptionRequiresMatchingDebuggingProcessAndGlobalState (string mismatch)
	{
		await using var server = new LaunchAdbServer ();
		server.TransformResponse = (command, output) => {
			if (command != "dumpsys activity processes" || !server.Attached)
				return output;
			if (mismatch == "flag-missing")
				return output.Replace ("mDebugging=true", "mDebugging=false");
			if (mismatch == "package-prefix")
				return output.Replace (PackageName + "/u", PackageName + ".other/u");
			if (mismatch == "wrong-user")
				return output.Replace ("/u0a", "/u10a");
			return output.Replace ("  mDebugApp=null/orig=null mDebugTransient=true mOrigWaitForDebugger=false\n", "");
		};
		var configuration = Configuration ();
		configuration.Debugger.Timeout = TimeSpan.FromMilliseconds (150);
		Assert.ThrowsAsync<TimeoutException> (() => LaunchAsync (server.Device, configuration, CancellationToken.None));
	}

	[TestCase (0)]
	[TestCase (-1)]
	public async Task UnboundedOrZeroTimeoutCannotArm (int milliseconds)
	{
		await using var server = new LaunchAdbServer ();
		var configuration = Configuration ();
		configuration.Debugger.Timeout = TimeSpan.FromMilliseconds (milliseconds);
		Assert.ThrowsAsync<ArgumentOutOfRangeException> (() => LaunchAsync (server.Device, configuration, CancellationToken.None));
		Assert.IsFalse (server.Commands.Any (c => c.Contains ("debug-app")));
	}

	[TestCase ("pm list users")]
	[TestCase ("am set-debug-app 'com.example.managed'")]
	[TestCase ("armed-state")]
	[TestCase ("launch")]
	[TestCase ("attached-state")]
	[TestCase ("am clear-debug-app")]
	public async Task CancellationAtTransactionBoundariesUsesIndependentCleanup (string boundary)
	{
		await using var server = new LaunchAdbServer ();
		using var cancellation = new CancellationTokenSource ();
		server.TransformResponse = (command, output) => {
			if (command == boundary ||
					(boundary == "armed-state" && command == "dumpsys activity processes" && server.DebugApp == PackageName) ||
					(boundary == "launch" && command.StartsWith ("am start ", StringComparison.Ordinal)) ||
					(boundary == "attached-state" && server.Attached))
				cancellation.Cancel ();
			return output;
		};
		Assert.CatchAsync<OperationCanceledException> (() => LaunchAsync (server.Device, Configuration (), cancellation.Token));
		Assert.IsNull (server.DebugApp);
		if (boundary != "pm list users")
			CollectionAssert.Contains (server.Commands, "am clear-debug-app");
	}

	[Test]
	public async Task AlreadyCanceledLaunchDoesNotTouchDevice ()
	{
		await using var server = new LaunchAdbServer ();
		using var cancellation = new CancellationTokenSource ();
		cancellation.Cancel ();
		Assert.CatchAsync<OperationCanceledException> (() => LaunchAsync (server.Device, Configuration (), cancellation.Token));
		Assert.IsEmpty (server.Commands);
	}

	[TestCase (false)]
	[TestCase (true)]
	public async Task CleanupFailurePreservesPrimaryErrorAndIsReported (bool changeOwner)
	{
		await using var server = new LaunchAdbServer ();
		var errors = new ConcurrentQueue<string> ();
		MessageHandler log = (_, message) => errors.Enqueue (message);
		AndroidLogger.Error += log;
		try {
			server.TransformResponse = (command, output) => {
				if (command.StartsWith ("am start ", StringComparison.Ordinal)) {
					if (changeOwner)
						server.DebugApp = "com.example.other";
					return "Error: Activity not started, primary launch failure";
				}
				if (command == "am clear-debug-app")
					return "cleanup failure";
				return output;
			};
			var error = Assert.ThrowsAsync<ManagedActivityLaunch.CommandFailedException> (() => LaunchAsync (server.Device, Configuration (), CancellationToken.None));
			StringAssert.Contains ("primary launch failure", error.Message);
			Assert.IsTrue (errors.Any (e => e.Contains ("Failed to clean up")));
			if (changeOwner) {
				Assert.AreEqual ("com.example.other", server.DebugApp);
				Assert.IsFalse (server.Commands.Contains ("am clear-debug-app"));
			}
		} finally {
			AndroidLogger.Error -= log;
		}
	}

	[Test]
	public async Task CleanupFailureAfterSuccessFailsLaunch ()
	{
		await using var server = new LaunchAdbServer ();
		server.TransformResponse = (command, output) => command == "am clear-debug-app" ? "cleanup failure" : output;
		var error = Assert.ThrowsAsync<ManagedActivityLaunch.CommandFailedException> (() => LaunchAsync (server.Device, Configuration (), CancellationToken.None));
		Assert.AreEqual ("cleanup failure", error.Message);
	}

	[TestCase ("")]
	[TestCase ("Users:\n")]
	[TestCase ("Users:\n\tUserInfo{0:Owner:13} running\n\tUserInfo{broken}\n")]
	[TestCase ("Permission Denial")]
	public async Task MalformedUsersCannotMasqueradeAsSingleUser (string users)
	{
		await using var server = new LaunchAdbServer { UserList = users };
		Assert.ThrowsAsync<InvalidOperationException> (() => LaunchAsync (server.Device, Configuration (), CancellationToken.None));
		Assert.IsFalse (server.Commands.Any (c => c.StartsWith ("am ", StringComparison.Ordinal)));
	}

	[TestCase ("ACTIVITY MANAGER RUNNING PROCESSES (dumpsys activity processes)\n  mDebugApp=broken\n  mForceBackgroundCheck=false\n")]
	[TestCase ("ACTIVITY MANAGER RUNNING PROCESSES (dumpsys activity processes)\n  mDebugApp=com.example.managed/orig=null mDebugTransient=true mOrigWaitForDebugger=false\n  mDebugApp=broken\n  mForceBackgroundCheck=false\n")]
	[TestCase ("ACTIVITY MANAGER RUNNING PROCESSES (dumpsys activity processes)\n  mDebugApp=com.example.managed/orig=null mDebugTransient=true mOrigWaitForDebugger=false\n    mDebugApp=com.example.other/orig=null mDebugTransient=true mOrigWaitForDebugger=false\n  mForceBackgroundCheck=false\n")]
	public async Task MalformedDumpCannotMasqueradeAsUnowned (string dump)
	{
		await using var server = new LaunchAdbServer ();
		server.TransformResponse = (command, output) => command == "dumpsys activity processes" ? dump : output;
		Assert.ThrowsAsync<InvalidOperationException> (() => LaunchAsync (server.Device, Configuration (), CancellationToken.None));
		Assert.IsFalse (server.Commands.Any (c => c.StartsWith ("am ", StringComparison.Ordinal)));
	}

	[TestCase ("warm")]
	[TestCase ("multiple-users")]
	[TestCase ("different-user")]
	[TestCase ("older-api")]
	public async Task UnsupportedLaunchesPreserveCommandWithoutDebugAppMutation (string reason)
	{
		await using var server = new LaunchAdbServer ();
		var configuration = Configuration (reason != "warm");
		var command = configuration.RunCommand as AmStartCommand;
		Assert.IsNotNull (command);
		if (reason == "multiple-users")
			server.UserList += "\tUserInfo{10:Work:30} running\n";
		if (reason == "different-user")
			command.User = "10";
		if (reason == "older-api")
			server.ApiLevel = 30;
		var expected = command.ToString ();
		var messages = new List<string> ();
		configuration.LogWiter = messages.Add;
		await LaunchAsync (server.Device, configuration, CancellationToken.None);
		CollectionAssert.Contains (server.Commands, expected);
		Assert.IsFalse (server.Commands.Any (c => c.Contains ("debug-app")));
		Assert.IsTrue (messages.Any (m => m.Contains ("Launching without changing")));
	}

	[TestCase ("com.example.other", "com.example.other/.Activity")]
	[TestCase ("com.example.managed", "com.example.other/.Activity")]
	[TestCase ("com.example.managed", "com.example.managed/")]
	public async Task ComponentAndPackageMustMatchBeforeArming (string package, string component)
	{
		await using var server = new LaunchAdbServer ();
		var configuration = Configuration ();
		configuration.RunCommand.PackageName = package;
		configuration.RunCommand.Component = component;
		Assert.ThrowsAsync<ArgumentException> (() => LaunchAsync (server.Device, configuration, CancellationToken.None));
		Assert.IsFalse (server.Commands.Any (c => c.Contains ("debug-app")));
	}

	[TestCase (null)]
	[TestCase ("")]
	[TestCase ("com.example.other")]
	public async Task ExplicitComponentDoesNotRequireNonEmittedPackageBookkeeping (string bookkeepingPackage)
	{
		await using var server = new LaunchAdbServer ();
		var command = new AmStartCommand {
			Component = PackageName + "/.MainActivity",
			PackageName = bookkeepingPackage,
			ForceStop = true,
		};
		var configuration = new ExecutionConfiguration (PackageName, command) { AllowJavaDebugging = false };
		await LaunchAsync (server.Device, configuration, CancellationToken.None);
		CollectionAssert.Contains (server.Commands, command.ToString ());
		CollectionAssert.Contains (server.Commands, "am set-debug-app 'com.example.managed'");
	}

	[Test]
	public async Task FallbackPreservesLaunchAndCancellation (
		[Values ("implicit", "warm", "multiple-users", "custom-process", "unconfirmed-process", "unsupported-layout")] string reason,
		[Values ("none", "diagnostic", "pending")] string boundary)
	{
		await using var server = new LaunchAdbServer ();
		using var cancellation = new CancellationTokenSource ();
		var configuration = Configuration (reason != "warm");
		if (reason == "implicit") {
			configuration.RunCommand.Component = null;
			configuration.RunCommand.Intent = PackageName;
		}
		if (reason == "multiple-users")
			server.UserList += "\tUserInfo{10:Work:30} running\n";
		if (reason == "custom-process")
			server.EffectiveProcessName = PackageName + ":custom";
		server.TransformResponse = (command, output) => {
			if (reason == "unconfirmed-process" && command.StartsWith ("pm resolve-activity ", StringComparison.Ordinal))
				return "No activity found\n";
			if (reason == "unsupported-layout" && command == "dumpsys activity processes")
				return output + "  vendor postamble\n";
			return output;
		};
		var messages = new List<string> ();
		configuration.LogWiter = message => {
			messages.Add (message);
			if (boundary == "diagnostic" && message.Contains ("Launching without"))
				cancellation.Cancel ();
		};
		var pending = new TaskCompletionSource (TaskCreationOptions.RunContinuationsAsynchronously);
		var release = new TaskCompletionSource (TaskCreationOptions.RunContinuationsAsynchronously);
		server.BeforeResponse = command => {
			if (boundary == "pending" && command.StartsWith ("am start ", StringComparison.Ordinal)) {
				pending.TrySetResult ();
				return release.Task;
			}
			return Task.CompletedTask;
		};
		try {
			var launch = LaunchAsync (server.Device, configuration, cancellation.Token);
			if (boundary == "pending") {
				await pending.Task.WaitAsync (TimeSpan.FromSeconds (5));
				cancellation.Cancel ();
			}
			if (boundary == "none")
				await launch;
			else
				Assert.CatchAsync<OperationCanceledException> (async () => await launch.WaitAsync (TimeSpan.FromSeconds (7)));
			Assert.IsTrue (messages.Any (m => m.Contains ("Launching without")));
			Assert.AreEqual (boundary != "diagnostic", server.Commands.Any (c => c.StartsWith ("am start ", StringComparison.Ordinal)));
			Assert.IsFalse (server.Commands.Any (c => c.Contains ("debug-app")));
		} finally {
			release.TrySetResult ();
		}
	}

	[TestCase ("")]
	[TestCase ("ACTIVITY MANAGER RUNNING PROCESSES (dumpsys activity processes)\n")]
	public async Task UnrecognizedInitialLayoutLaunchesWithoutMutation (string dump)
	{
		await using var server = new LaunchAdbServer ();
		server.TransformResponse = (command, output) => command == "dumpsys activity processes" ? dump : output;
		await LaunchAsync (server.Device, Configuration (), CancellationToken.None);
		Assert.IsTrue (server.Commands.Any (c => c.StartsWith ("am start ", StringComparison.Ordinal)));
		Assert.IsFalse (server.Commands.Any (c => c.Contains ("debug-app")));
	}

	[Test]
	public async Task UnsupportedLayoutAfterMutationStillFailsClosed (
		[Values ("armed", "cleanup", "stale-clear")] string boundary,
		[Values ("truncated", "malformed-marker")] string layout)
	{
		await using var server = new LaunchAdbServer ();
		if (boundary == "stale-clear")
			server.SetDebugAppState (PackageName, true);
		server.TransformResponse = (command, output) => {
			if (command == "dumpsys activity processes" &&
					((boundary == "armed" && server.Commands.Any (c => c.StartsWith ("am set-debug-app", StringComparison.Ordinal))) ||
					(boundary != "armed" && server.Commands.Contains ("am clear-debug-app"))))
				return layout == "truncated"
					? output.Replace ("  mForceBackgroundCheck=false\n", "")
					: output.Replace ("  mForceBackgroundCheck=false", "  mDebugApp=broken\n  mForceBackgroundCheck=false");
			return output;
		};
		Assert.CatchAsync<InvalidOperationException> (() => LaunchAsync (server.Device, Configuration (), CancellationToken.None));
		if (boundary == "stale-clear")
			Assert.IsFalse (server.Commands.Any (c => c.StartsWith ("am set-debug-app ", StringComparison.Ordinal)),
				"After any clear, reject unsupported state before rearming, not only during final cleanup.");
	}

	[TestCase (false)]
	[TestCase (true)]
	public async Task InitialDumpTransportOrCancellationDoesNotFallback (bool cancel)
	{
		await using var server = new LaunchAdbServer ();
		using var cancellation = new CancellationTokenSource ();
		if (cancel) {
			server.TransformResponse = (command, output) => {
				if (command == "dumpsys activity processes")
					cancellation.Cancel ();
				return output;
			};
			Assert.CatchAsync<OperationCanceledException> (() => LaunchAsync (server.Device, Configuration (), cancellation.Token));
		} else {
			server.FailTransportCommand = "dumpsys activity processes";
			var error = Assert.CatchAsync (() => LaunchAsync (server.Device, Configuration (), CancellationToken.None));
			StringAssert.Contains ("simulated transport fail", error.ToString ());
		}
		Assert.IsFalse (server.Commands.Any (c => c.StartsWith ("am ", StringComparison.Ordinal)));
	}

	[Test]
	public async Task GateTimeoutDescribesAdmissionRatherThanProcessAttach ()
	{
		await using var server = new LaunchAdbServer ();
		using var cancellation = new CancellationTokenSource ();
		var pending = new TaskCompletionSource (TaskCreationOptions.RunContinuationsAsynchronously);
		var release = new TaskCompletionSource (TaskCreationOptions.RunContinuationsAsynchronously);
		server.BeforeResponse = command => {
			if (command == "date +%s") {
				pending.TrySetResult ();
				return release.Task;
			}
			return Task.CompletedTask;
		};
		var configuration = Configuration ();
		configuration.Debugger.Timeout = TimeSpan.FromSeconds (40);
		var first = LaunchAsync (server.Device, configuration, cancellation.Token);
		try {
			await pending.Task.WaitAsync (TimeSpan.FromSeconds (5));
			var error = Assert.ThrowsAsync<TimeoutException> (async () =>
				await LaunchAsync (server.CreateDevice (), Configuration (), CancellationToken.None).WaitAsync (TimeSpan.FromSeconds (35)));
			StringAssert.Contains ("another activity debug launch", error.Message);
			Assert.IsFalse (server.Commands.Any (c => c.StartsWith ("am ", StringComparison.Ordinal)));
		} finally {
			cancellation.Cancel ();
			release.TrySetResult ();
			Assert.CatchAsync (async () => await first.WaitAsync (TimeSpan.FromSeconds (5)));
		}
	}

	[TestCase ("com.example.app;echo bad")]
	[TestCase ("com.example.$(id)")]
	[TestCase ("com.example.`id`")]
	[TestCase ("com.example.\"bad")]
	[TestCase ("com.example.\napp")]
	public async Task InvalidPackageCannotReachDebugAppCommand (string package)
	{
		await using var server = new LaunchAdbServer ();
		var configuration = new ExecutionConfiguration (package, new AmStartCommand (package, ".Activity") { ForceStop = true }) {
			AllowJavaDebugging = false,
		};
		Assert.ThrowsAsync<ArgumentException> (() => LaunchAsync (server.Device, configuration, CancellationToken.None));
		Assert.IsFalse (server.Commands.Any (c => c.Contains ("debug-app")));
	}

	[TestCase ("java-allowed")]
	[TestCase ("without-debugging")]
	[TestCase ("broadcast")]
	[TestCase ("instrumentation")]
	[TestCase ("null")]
	public async Task OtherLaunchPathsDoNotUseManagedTransaction (string path)
	{
		await using var server = new LaunchAdbServer ();
		var configuration = Configuration ();
		if (path == "broadcast")
			configuration = new ExecutionConfiguration (PackageName, new AmBroadcastCommand { Action = "test" }) { AllowJavaDebugging = false };
		if (path == "instrumentation")
			configuration = new ExecutionConfiguration (PackageName, new InstrumentationCommand ()) { AllowJavaDebugging = false };
		if (path == "null")
			configuration = new ExecutionConfiguration (PackageName, null) { AllowJavaDebugging = false };
		if (path == "java-allowed")
			configuration.AllowJavaDebugging = true;
		if (path == "without-debugging")
			await server.Device.StartWithoutDebuggingAsync (configuration, CancellationToken.None);
		else
			await server.Device.StartWithDebuggingAsync (configuration, CancellationToken.None);
		Assert.IsFalse (server.Commands.Any (c => c.Contains ("debug-app")));
		if (path == "broadcast")
			Assert.IsTrue (server.Commands.Any (c => c.Contains ("--include-stopped-packages")));
		if (path == "null")
			Assert.IsFalse (server.Commands.Any (c => c.StartsWith ("am ", StringComparison.Ordinal)));
	}

	[Test]
	public async Task ExplicitJavaDebuggingStillStartsWithDAndRequestsJdwpPid ()
	{
		await using var server = new LaunchAdbServer ();
		using var cancellation = new CancellationTokenSource ();
		var configuration = Configuration ();
		configuration.AllowJavaDebugging = true;
		var command = configuration.RunCommand as AmStartCommand;
		Assert.IsNotNull (command);
		command.EnableDebugging = true;
		server.TransformResponse = (cmd, output) => {
			if (cmd.StartsWith ("ps", StringComparison.Ordinal)) {
				// Stop before port forwarding: this branch still uses AdbServer.Default.
				cancellation.Cancel ();
				return "USER PID PPID VSIZE RSS WCHAN PC NAME\n";
			}
			return output;
		};
		Assert.CatchAsync (() => server.Device.StartWithDebuggingAsync (configuration, cancellation.Token));
		Assert.IsTrue (server.Commands.Any (c => c.StartsWith ("am start ", StringComparison.Ordinal) && c.Contains (" -D")));
		Assert.IsTrue (server.Commands.Any (c => c.StartsWith ("ps", StringComparison.Ordinal)));
		Assert.IsFalse (server.Commands.Any (c => c.Contains ("debug-app")));
	}

	[TestCase (null)]
	[TestCase ("current")]
	[TestCase ("0")]
	public async Task SingleUserCommandRetainsRequestedUser (string user)
	{
		await using var server = new LaunchAdbServer ();
		var configuration = Configuration ();
		var command = configuration.RunCommand as AmStartCommand;
		Assert.IsNotNull (command);
		command.User = user;
		var expected = command.ToString ();
		await LaunchAsync (server.Device, configuration, CancellationToken.None);
		CollectionAssert.Contains (server.Commands, expected);
		CollectionAssert.Contains (server.Commands, "am set-debug-app 'com.example.managed'");
	}

	[TestCase ("com.example.other", false)]
	[TestCase ("com.example.managed", false)]
	[TestCase ("com.example.other", true)]
	public async Task PreexistingForeignOrPersistentDebugAppIsNotCleared (string package, bool transient)
	{
		await using var server = new LaunchAdbServer ();
		server.SetDebugAppState (package, transient);
		Assert.ThrowsAsync<InvalidOperationException> (() => LaunchAsync (server.Device, Configuration (), CancellationToken.None));
		Assert.IsFalse (server.Commands.Any (c => c.Contains ("debug-app")));
		Assert.AreEqual (package, server.DebugApp);
	}

	[TestCase ("com.example.managed", false)]
	[TestCase ("com.example.other", false)]
	[TestCase (null, true)]
	public async Task OriginalDebugAppOrWaitSettingIsNotCleared (string original, bool wait)
	{
		await using var server = new LaunchAdbServer { OriginalDebugApp = original };
		server.SetDebugAppState (PackageName, true);
		if (wait)
			server.TransformResponse = (command, output) => output.Replace ("mOrigWaitForDebugger=false", "mOrigWaitForDebugger=true");
		Assert.ThrowsAsync<InvalidOperationException> (() => LaunchAsync (server.Device, Configuration (), CancellationToken.None));
		Assert.IsFalse (server.Commands.Any (c => c.Contains ("debug-app")));
		Assert.AreEqual (PackageName, server.DebugApp);
		Assert.AreEqual (original, server.OriginalDebugApp);
	}

	[Test]
	public async Task MatchingStaleTransientStateIsClearedBeforeRearming ()
	{
		await using var server = new LaunchAdbServer ();
		server.SetDebugAppState (PackageName, true);
		await LaunchAsync (server.Device, Configuration (), CancellationToken.None);
		var commands = server.Commands.ToArray ();
		Assert.Less (Array.IndexOf (commands, "am clear-debug-app"), Array.IndexOf (commands, "am set-debug-app 'com.example.managed'"));
		Assert.AreEqual (2, commands.Count (c => c == "am clear-debug-app"));
	}

	[Test]
	public async Task CancellationDrainsArmingBeforeCleanup ()
	{
		await using var server = new LaunchAdbServer ();
		using var cancellation = new CancellationTokenSource ();
		var arming = new TaskCompletionSource (TaskCreationOptions.RunContinuationsAsynchronously);
		var release = new TaskCompletionSource (TaskCreationOptions.RunContinuationsAsynchronously);
		server.BeforeResponse = command => {
			if (command.StartsWith ("am set-debug-app", StringComparison.Ordinal)) {
				arming.TrySetResult ();
				return release.Task;
			}
			return Task.CompletedTask;
		};
		var launch = LaunchAsync (server.Device, Configuration (), cancellation.Token);
		await arming.Task.WaitAsync (TimeSpan.FromSeconds (5));
		cancellation.Cancel ();
		Assert.IsFalse (launch.IsCompleted);
		Assert.IsFalse (server.Commands.Contains ("am clear-debug-app"));
		release.SetResult ();
		Assert.CatchAsync<OperationCanceledException> (async () => await launch.WaitAsync (TimeSpan.FromSeconds (5)));
		Assert.IsNull (server.DebugApp);
		CollectionAssert.Contains (server.Commands, "am clear-debug-app");
	}

	[Test]
	public async Task GateIncludesCleanupAcrossDeviceInstances ()
	{
		await using var server = new LaunchAdbServer ();
		var cleaning = new TaskCompletionSource (TaskCreationOptions.RunContinuationsAsynchronously);
		var release = new TaskCompletionSource (TaskCreationOptions.RunContinuationsAsynchronously);
		server.BeforeResponse = command => {
			if (command == "am clear-debug-app") {
				cleaning.TrySetResult ();
				return release.Task;
			}
			return Task.CompletedTask;
		};
		var first = LaunchAsync (server.Device, Configuration (), CancellationToken.None);
		await cleaning.Task.WaitAsync (TimeSpan.FromSeconds (5));
		var commandCount = server.Commands.Count;
		using var cancellation = new CancellationTokenSource ();
		var second = LaunchAsync (server.CreateDevice (), Configuration (), cancellation.Token);
		Assert.IsFalse (second.IsCompleted);
		Assert.AreEqual (commandCount, server.Commands.Count);
		cancellation.Cancel ();
		Assert.CatchAsync<OperationCanceledException> (async () => await second);
		release.SetResult ();
		await first;
		Assert.IsNull (server.DebugApp);
	}

	[Test]
	public async Task TimedOutShellCompletionCannotRunCleanupAfterNextOwner ()
	{
		await using var server = new LaunchAdbServer ();
		var starting = new TaskCompletionSource (TaskCreationOptions.RunContinuationsAsynchronously);
		var release = new TaskCompletionSource (TaskCreationOptions.RunContinuationsAsynchronously);
		var completed = new TaskCompletionSource (TaskCreationOptions.RunContinuationsAsynchronously);
		server.BeforeResponse = command => {
			if (command.StartsWith ("am start ", StringComparison.Ordinal)) {
				starting.TrySetResult ();
				return release.Task;
			}
			return Task.CompletedTask;
		};
		server.TransformResponse = (command, output) => {
			if (command.StartsWith ("am start ", StringComparison.Ordinal))
				completed.TrySetResult ();
			return output;
		};
		var configuration = Configuration ();
		configuration.Debugger.Timeout = TimeSpan.FromMilliseconds (250);
		var launch = LaunchAsync (server.Device, configuration, CancellationToken.None);
		await starting.Task.WaitAsync (TimeSpan.FromSeconds (5));
		Assert.ThrowsAsync<TimeoutException> (async () => await launch.WaitAsync (TimeSpan.FromSeconds (5)));
		Assert.IsNull (server.DebugApp);
		var clears = server.Commands.Count (c => c == "am clear-debug-app");
		server.DebugApp = "com.example.next";
		release.SetResult ();
		await completed.Task.WaitAsync (TimeSpan.FromSeconds (5));
		Assert.AreEqual ("com.example.next", server.DebugApp);
		Assert.AreEqual (clears, server.Commands.Count (c => c == "am clear-debug-app"));
	}

	[Test]
	public async Task HungCleanupIsBoundedAndDoesNotReplaceLaunchError ()
	{
		await using var server = new LaunchAdbServer ();
		var release = new TaskCompletionSource (TaskCreationOptions.RunContinuationsAsynchronously);
		server.BeforeResponse = command => command == "am clear-debug-app" ? release.Task : Task.CompletedTask;
		server.TransformResponse = (command, output) => command.StartsWith ("am start ", StringComparison.Ordinal)
			? "Error: Activity not started, primary failure"
			: output;
		try {
			var launch = LaunchAsync (server.Device, Configuration (), CancellationToken.None);
			var error = Assert.ThrowsAsync<ManagedActivityLaunch.CommandFailedException> (async () => await launch.WaitAsync (TimeSpan.FromSeconds (8)));
			StringAssert.Contains ("primary failure", error.Message);
		} finally {
			release.TrySetResult ();
		}
	}

	sealed class InstrumentationCommand : AmIntentCommand
	{
		protected override void AppendTo (Mono.AndroidTools.Util.ProcessArgumentBuilder builder)
		{
			builder.Add ("am", "instrument", "com.example.managed/runner");
		}
	}

	// A private TCP endpoint exercises AndroidDevice's actual ADB transport without
	// replacing the launch algorithm or touching an installed adb server/device.
	internal sealed class LaunchAdbServer : IAsyncDisposable
	{
		readonly TcpListener listener = new TcpListener (IPAddress.Loopback, 0);
		readonly CancellationTokenSource stop = new CancellationTokenSource ();
		readonly List<Task> connections = [];
		readonly Task accepting;
		string debugProperty = "";
		string fakeAdbPath;
		bool started;
		bool transient;
		int pidRequests;

		public ConcurrentQueue<string> Commands { get; } = new ConcurrentQueue<string> ();
		public ConcurrentQueue<string> AdbCommands { get; } = new ConcurrentQueue<string> ();
		public bool LogDaemonStartup { get; set; }
		public string DebugApp { get; set; }
		public string OriginalDebugApp { get; set; }
		public bool AttachOnDump { get; set; } = true;
		public string UserList { get; set; } = "Users:\n\tUserInfo{0:Owner:13} running\n";
		public int ApiLevel { get; set; } = 36;
		public string EffectiveProcessName { get; set; } = PackageName;
		public string ApplicationProcessName { get; set; } = PackageName;
		public Func<string, Task> BeforeResponse { get; set; } = _ => Task.CompletedTask;
		public Func<string, string, string> TransformResponse { get; set; } = (_, output) => output;
		public Func<string, (int ExitCode, string Error)> CliResult { get; set; } = _ => (0, "");
		public string FailTransportCommand { get; set; }
		public bool Attached { get; private set; }
		public AndroidDevice Device { get; }
		public string Serial { get; }
		readonly AdbServer adb;

		public LaunchAdbServer (string serial = "managed-launch-test")
		{
			Serial = serial;
			listener.Start ();
			adb = new AdbServer (IPAddress.Loopback, ((IPEndPoint) listener.LocalEndpoint).Port);
			Device = CreateDevice ();
			accepting = AcceptAsync ();
		}

		public AndroidDevice CreateDevice () => new AndroidDevice (Serial, adb: adb);

		public string CreateFakeAdb ()
		{
			if (OS.IsWindows)
				Assert.Ignore ("Fake adb process tests require bash, like AdbRunnerTests.");
			Assert.IsNull (fakeAdbPath);
			var directory = Path.Combine (Path.GetTempPath (), $"managed-launch-adb-{Guid.NewGuid ():N}");
			Directory.CreateDirectory (directory);
			fakeAdbPath = Path.Combine (directory, "adb");
			// This is only a process-to-fixture bridge. All state transitions and
			// responses stay in the same private server used by the task tests.
			var daemonOutput = LogDaemonStartup
				? "if [[ \"$command\" == *get-serialno ]]; then\n    printf '%s\\n' '* daemon not running; starting now at tcp:5037' '* daemon started successfully' >&2\nfi"
				: "";
			File.WriteAllText (fakeAdbPath, $$"""
				#!/bin/bash
				set -e
				exec 3<>/dev/tcp/127.0.0.1/{{((IPEndPoint) listener.LocalEndpoint).Port}}
				LC_ALL=C
				command="$*"
				{{daemonOutput}}
				printf '%04x%s' "${#command}" "$command" >&3
				IFS= read -r status <&3
				while IFS= read -r record <&3; do
				    case "$record" in
				        O*) printf '%s\n' "${record:1}" ;;
				        o*) printf '%s' "${record:1}" ;;
				        E*) printf '%s\n' "${record:1}" >&2 ;;
				        e*) printf '%s' "${record:1}" >&2 ;;
				        *) printf '%s\n' 'Invalid fake ADB response record' >&2; exit 1 ;;
				    esac
				done
				exit "$status"

				""");
			FileUtil.Chmod (fakeAdbPath, 0x1ED); // 0755
			return fakeAdbPath;
		}

		public void SetDebugAppState (string package, bool isTransient)
		{
			DebugApp = package;
			transient = isTransient;
		}

		async Task AcceptAsync ()
		{
			try {
				while (!stop.IsCancellationRequested) {
					var client = await listener.AcceptTcpClientAsync (stop.Token);
					connections.Add (RespondAsync (client));
				}
			} catch (OperationCanceledException) when (stop.IsCancellationRequested) {
			}
		}

		async Task RespondAsync (TcpClient client)
		{
			try {
				using (client) {
					var stream = client.GetStream ();
					var request = await ReadCommandAsync (stream);
					if (!request.StartsWith ("host:transport:", StringComparison.Ordinal)) {
						AdbCommands.Enqueue (request);
						foreach (var target in new [] { $"-s {Serial} ", "-d ", "-e " }) {
							if (request.StartsWith (target, StringComparison.Ordinal)) {
								request = request.Substring (target.Length);
								break;
							}
						}
						if (request.StartsWith ("shell ", StringComparison.Ordinal))
							request = request.Substring (6);
						var reply = await RespondToCommandAsync (request);
						var result = reply.Success ? CliResult (request) : (ExitCode: 1, Error: reply.Output);
						if (reply.Success && request.StartsWith ("pidof ", StringComparison.Ordinal) &&
								string.IsNullOrWhiteSpace (reply.Output) && result.ExitCode == 0 && string.IsNullOrWhiteSpace (result.Error))
							result = (1, "");
						var cliResponse = new StringBuilder ().Append (result.ExitCode.ToString (CultureInfo.InvariantCulture)).Append ('\n');
						AppendCliOutput (cliResponse, 'O', reply.Success ? reply.Output : "");
						AppendCliOutput (cliResponse, 'E', result.Error);
						await stream.WriteAsync (Encoding.UTF8.GetBytes (cliResponse.ToString ()), stop.Token);
						return;
					}
					Assert.AreEqual ("host:transport:" + Serial, request);
					await stream.WriteAsync (Encoding.ASCII.GetBytes ("OKAY"), stop.Token);
					var command = await ReadCommandAsync (stream);
					Assert.IsTrue (command.StartsWith ("shell:", StringComparison.Ordinal), command);
					command = command.Substring (6);
					var response = await RespondToCommandAsync (command);
					var status = response.Success ? "OKAY" : "FAIL" + Encoding.UTF8.GetByteCount (response.Output).ToString ("X4", CultureInfo.InvariantCulture);
					await stream.WriteAsync (Encoding.UTF8.GetBytes (status + response.Output), stop.Token);
				}
			} catch (OperationCanceledException) when (stop.IsCancellationRequested) {
			}
		}

		async Task<(bool Success, string Output)> RespondToCommandAsync (string command)
		{
			Commands.Enqueue (command);
			await BeforeResponse (command).WaitAsync (stop.Token);
			if (command == FailTransportCommand)
				return (false, "simulated transport fail");
			return (true, TransformResponse (command, Respond (command)));
		}

		static void AppendCliOutput (StringBuilder response, char channel, string output)
		{
			// Preserve both channels and whether the last line was terminated.
			// The shell bridge emits the bytes, not a merged approximation of ADB.
			var lines = output.Split ('\n');
			for (int i = 0; i < lines.Length; i++) {
				bool last = i == lines.Length - 1;
				if (last && lines [i].Length == 0)
					break;
				response.Append (last ? char.ToLowerInvariant (channel) : channel).Append (lines [i]).Append ('\n');
			}
		}

		string Respond (string command)
		{
			if (command == "get-serialno")
				return Serial + "\n";
			if (command.StartsWith ("forward ", StringComparison.Ordinal) || command.StartsWith ("reverse ", StringComparison.Ordinal))
				return "";
			if (command.StartsWith ("pidof ", StringComparison.Ordinal)) {
				var process = command.Substring ("pidof ".Length);
				if (process != EffectiveProcessName && process != "'" + EffectiveProcessName.Replace ("'", "'\\''") + "'")
					return "";
				return Interlocked.Increment (ref pidRequests) == 1 ? "1234\n" : "";
			}
			if (command.StartsWith ("logcat ", StringComparison.Ordinal))
				return "managed-launch logcat\n";
			if (command.StartsWith ("am force-stop ", StringComparison.Ordinal))
				return "";
			if (command.StartsWith ("input keyevent KEYCODE_WAKEUP; wm dismiss-keyguard", StringComparison.Ordinal)) {
				var start = command.IndexOf ("am start ", StringComparison.Ordinal);
				return start >= 0 ? Respond (command.Substring (start)) : "";
			}
			if (command == "date +%s")
				return "1000\n";
			if (command.StartsWith ("setprop ", StringComparison.Ordinal)) {
				// SetFastDevPropertyFile skips file transfer when getprop reflects setprop.
				debugProperty = command.Substring (command.IndexOf ("\" ", StringComparison.Ordinal) + 3).Trim ('"');
				return "";
			}
			if (command == "getprop")
				return $"[ro.build.version.sdk]: [{ApiLevel}]\n[debug.mono.extra]: [{debugProperty}]\n";
			if (command == "getprop ro.build.version.sdk")
				return ApiLevel.ToString (CultureInfo.InvariantCulture) + "\n";
			if (command == "pm list users")
				return UserList;
			if (command.StartsWith ("pm resolve-activity ", StringComparison.Ordinal)) {
				var component = command.Substring (command.IndexOf ("-n ", StringComparison.Ordinal) + 3).Trim ('\'', '"');
				var activity = component.Substring (component.IndexOf ('/') + 1);
				if (activity.StartsWith (".", StringComparison.Ordinal))
					activity = PackageName + activity;
				var process = EffectiveProcessName == PackageName ? "" : $"  processName={EffectiveProcessName}\n";
				// ResolveInfo.dump -> ActivityInfo.dump -> ComponentInfo.dumpFront.
				// Component processName is OMITTED for the default package process,
				// whereas nested ApplicationInfo always prints its own processName.
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
				return (command.Contains (" -S") ? "Stopping: com.example.managed\n" : "") +
					"Starting: Intent { cmp=com.example.managed/.MainActivity }\n";
			}
			if (command == "dumpsys activity processes") {
				if (AttachOnDump && started && DebugApp == EffectiveProcessName) {
					Attached = true;
					DebugApp = null;
				}
				var process = started
					? $"  *APP* UID 10123 ProcessRecord{{abc 1234:{EffectiveProcessName}/u0a123}}\n    pid=1234\n" +
						(Attached ? "    mDebugging=true\n" : "")
					: "";
				var marker = transient || DebugApp != null
					? $"  mDebugApp={DebugApp ?? "null"}/orig={OriginalDebugApp ?? "null"} mDebugTransient={transient.ToString ().ToLowerInvariant ()} mOrigWaitForDebugger=false\n"
					: "";
				return "ACTIVITY MANAGER RUNNING PROCESSES (dumpsys activity processes)\n" + process + marker + "  mForceBackgroundCheck=false\n";
			}
			if (command.StartsWith ("am broadcast ", StringComparison.Ordinal) || command.StartsWith ("am instrument ", StringComparison.Ordinal) ||
					command.StartsWith ("\"run-as\" ", StringComparison.Ordinal))
				return "";
			if (command.StartsWith ("ps", StringComparison.Ordinal))
				return "USER PID PPID VSIZE RSS WCHAN PC NAME\nu0_a123 1234 1 0 0 0 0 com.example.managed\n";
			throw new InvalidOperationException ("Unexpected ADB command: " + command);
		}

		async Task<string> ReadCommandAsync (NetworkStream stream)
		{
			var header = new byte [4];
			await stream.ReadExactlyAsync (header, stop.Token);
			var length = int.Parse (Encoding.ASCII.GetString (header), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
			var data = new byte [length];
			await stream.ReadExactlyAsync (data, stop.Token);
			return Encoding.UTF8.GetString (data);
		}

		public async ValueTask DisposeAsync ()
		{
			stop.Cancel ();
			await accepting;
			listener.Stop ();
			await Task.WhenAll (connections);
			stop.Dispose ();
			if (fakeAdbPath != null) {
				File.Delete (fakeAdbPath);
				Directory.Delete (Path.GetDirectoryName (fakeAdbPath));
			}
		}
	}
}
