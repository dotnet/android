// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Microsoft.Android.Run.Tests
{
	[TestFixture]
	public class ManagedActivityLaunchTests
	{
		const string DefaultPackageName = "com.example.managed";

		static T AssertThrowsAsync<T> (Func<Task> action) where T : Exception
		{
			var error = Assert.ThrowsAsync<T> (async () => await action ());
			return error ?? throw new InvalidOperationException ($"Expected {typeof (T).Name}.");
		}

		[Test]
		public async Task ArmsAndCleansAfterObservedAttach ()
		{
			var fixture = new ManagedLaunchFixture ();

			string? processName = await fixture.LaunchAsync ();

			Assert.AreEqual (DefaultPackageName, processName);
			CollectionAssert.Contains (fixture.State.Commands, "am set-debug-app 'com.example.managed'");
			CollectionAssert.Contains (fixture.State.Commands, fixture.StartCommand);
			CollectionAssert.Contains (fixture.State.Commands, "am clear-debug-app");
			Assert.IsTrue (fixture.State.Attached);
			Assert.IsNull (fixture.State.DebugApp);
		}

		[Test]
		public async Task WaitsForConsumedDebuggingStateNotPid ()
		{
			var fixture = new ManagedLaunchFixture ();
			fixture.State.AttachOnDump = false;
			var allowAttach = new TaskCompletionSource (TaskCreationOptions.RunContinuationsAsynchronously);
			var observedPid = new TaskCompletionSource (TaskCreationOptions.RunContinuationsAsynchronously);
			fixture.State.TransformResponse = (command, output) => {
				if (command == "dumpsys activity processes" && output.Contains ("pid=1234", StringComparison.Ordinal)) {
					observedPid.TrySetResult ();
					fixture.State.AttachOnDump = allowAttach.Task.IsCompleted;
				}
				return output;
			};

			Task<string?> launch = fixture.LaunchAsync ();
			await observedPid.Task.WaitAsync (TimeSpan.FromSeconds (5));
			Assert.IsFalse (launch.IsCompleted);
			Assert.AreEqual (DefaultPackageName, fixture.State.DebugApp);
			Assert.IsFalse (fixture.State.Commands.Contains ("am clear-debug-app"));

			allowAttach.TrySetResult ();
			await launch;
			Assert.IsTrue (fixture.State.Attached);
		}

		[TestCase ("Starting: Intent { cmp=com.example.managed/.MainActivity }\nError type 3\nError: Activity class does not exist.\n", true)]
		[TestCase ("Starting: Intent { cmp=com.example.managed/.MainActivity }\nError: Permission denied\n", false)]
		[TestCase ("Starting: Intent { cmp=com.example.managed/.MainActivity }\nException occurred while executing 'start':\njava.lang.SecurityException\n", false)]
		public void PropagatesStartErrorsAndCleans (string output, bool activityNotFound)
		{
			var fixture = new ManagedLaunchFixture ();
			fixture.State.TransformResponse = (command, response) => command == fixture.StartCommand ? output : response;

			var error = AssertThrowsAsync<ManagedActivityLaunch.CommandFailedException> (() => fixture.LaunchAsync ());
			Assert.AreEqual (activityNotFound, error.ActivityNotFound);
			Assert.IsNull (fixture.State.DebugApp);
			CollectionAssert.Contains (fixture.State.Commands, "am clear-debug-app");
		}

		[TestCase ("")]
		[TestCase ("Warning: Activity not started, intent has been delivered to currently running top-most instance.\n")]
		[TestCase ("Warning: Activity not started, its current task has been brought to the front\n")]
		public async Task AcceptsSuccessfulWarningsAfterForceStopPreamble (string warning)
		{
			var fixture = new ManagedLaunchFixture ();
			fixture.State.TransformResponse = (command, output) => command == fixture.StartCommand ? output + warning : output;

			await fixture.LaunchAsync ();

			Assert.IsTrue (fixture.State.Attached);
			Assert.IsNull (fixture.State.DebugApp);
		}

		[TestCase ("ExceptionActivity", "https://example.com/")]
		[TestCase ("MainActivity", "https://example.com/Error:Details")]
		public async Task DoesNotTreatDiagnosticWordsInsideIntentAsErrors (string activity, string uri)
		{
			var fixture = new ManagedLaunchFixture {
				Component = DefaultPackageName + "/." + activity,
			};
			fixture.State.TransformResponse = (command, output) => command == fixture.StartCommand
				? $"Starting: Intent {{ dat={uri} cmp={fixture.Component} }}\n"
				: output;

			await fixture.LaunchAsync ();

			Assert.IsTrue (fixture.State.Attached);
			Assert.IsNull (fixture.State.DebugApp);
		}

		[TestCase ("older-api", null)]
		[TestCase ("multiple-users", null)]
		[TestCase ("unconfirmed-process", null)]
		[TestCase ("unsupported-layout", DefaultPackageName)]
		[TestCase ("custom-process", "com.example.managed:ui")]
		[TestCase ("warm", null)]
		public async Task FallsBackWithoutMutatingDebugApp (string reason, string? expectedProcessName)
		{
			var fixture = new ManagedLaunchFixture ();
			if (reason == "older-api")
				fixture.State.ApiLevel = 30;
			if (reason == "multiple-users")
				fixture.State.UserList += "\tUserInfo{10:Work:30} running\n";
			if (reason == "custom-process")
				fixture.State.EffectiveProcessName = "com.example.managed:ui";
			if (reason == "warm")
				fixture.ForceStop = false;
			if (reason == "unconfirmed-process")
				fixture.State.TransformResponse = (command, output) => command.StartsWith ("pm resolve-activity ", StringComparison.Ordinal) ? "No activity found\n" : output;
			if (reason == "unsupported-layout")
				fixture.State.TransformResponse = (command, output) => command == "dumpsys activity processes" ? output + "  vendor postamble\n" : output;

			string? processName = await fixture.LaunchAsync ();

			Assert.AreEqual (1, fixture.UnprotectedLaunchCount);
			Assert.AreEqual (expectedProcessName, processName);
			Assert.IsFalse (fixture.State.Commands.Any (command => command.Contains ("debug-app", StringComparison.Ordinal)));
		}

		[Test]
		public async Task ActivityCanOverrideCustomApplicationProcessBackToPackage ()
		{
			var fixture = new ManagedLaunchFixture ();
			fixture.State.ApplicationProcessName = "com.example.managed:app";

			string? processName = await fixture.LaunchAsync ();

			Assert.AreEqual (DefaultPackageName, processName);
			CollectionAssert.Contains (fixture.State.Commands, "pm resolve-activity --user '0' -n 'com.example.managed/.MainActivity'");
			CollectionAssert.Contains (fixture.State.Commands, "am set-debug-app 'com.example.managed'");
			Assert.IsTrue (fixture.State.Attached);
		}

		[TestCase ("Users:\n")]
		[TestCase ("Users:\n\tUserInfo{0:Owner:13} running\n\tUserInfo{broken}\n")]
		[TestCase ("Permission Denial")]
		public void RejectsMalformedUsers (string users)
		{
			var fixture = new ManagedLaunchFixture ();
			fixture.State.UserList = users;

			Assert.ThrowsAsync<InvalidOperationException> (async () => await fixture.LaunchAsync ());
			Assert.IsFalse (fixture.State.Commands.Any (command => command.StartsWith ("am ", StringComparison.Ordinal)));
		}

		[TestCase ("ACTIVITY MANAGER RUNNING PROCESSES (dumpsys activity processes)\n  mDebugApp=broken\n  mForceBackgroundCheck=false\n")]
		[TestCase ("ACTIVITY MANAGER RUNNING PROCESSES (dumpsys activity processes)\n  mDebugApp=com.example.managed/orig=null mDebugTransient=true mOrigWaitForDebugger=false\n  mDebugApp=broken\n  mForceBackgroundCheck=false\n")]
		public void RejectsMalformedDumps (string dump)
		{
			var fixture = new ManagedLaunchFixture ();
			fixture.State.TransformResponse = (command, output) => command == "dumpsys activity processes" ? dump : output;

			Assert.ThrowsAsync<InvalidOperationException> (async () => await fixture.LaunchAsync ());
			Assert.IsFalse (fixture.State.Commands.Any (command => command.StartsWith ("am ", StringComparison.Ordinal)));
		}

		[TestCase ("foreign", false, null, false)]
		[TestCase ("matching", true, null, false)]
		[TestCase ("matching", false, null, true)]
		[TestCase ("matching", true, "com.example.other", false)]
		[TestCase ("matching", true, null, true)]
		public async Task RespectsForeignOrOriginalDebugAppState (string packageKind, bool isTransient, string? originalDebugApp, bool originalWait)
		{
			var fixture = new ManagedLaunchFixture ();
			fixture.State.OriginalDebugApp = originalDebugApp;
			fixture.State.OriginalWaitForDebugger = originalWait;
			fixture.State.SetDebugAppState (packageKind == "foreign" ? "com.example.other" : DefaultPackageName, isTransient);

			if (packageKind == "matching" && isTransient && originalDebugApp == null && !originalWait) {
				await fixture.LaunchAsync ();
				int clear = Array.IndexOf (fixture.State.Commands.ToArray (), "am clear-debug-app");
				int arm = Array.IndexOf (fixture.State.Commands.ToArray (), "am set-debug-app 'com.example.managed'");
				Assert.GreaterOrEqual (clear, 0);
				Assert.Greater (arm, clear);
				return;
			}

			Assert.ThrowsAsync<InvalidOperationException> (async () => await fixture.LaunchAsync ());
			Assert.IsFalse (fixture.State.Commands.Any (command => command == "am clear-debug-app"));
		}

		[TestCase ("flag-missing")]
		[TestCase ("package-prefix")]
		[TestCase ("wrong-user")]
		public void RequiresMatchingConsumedDebuggingProcessState (string mismatch)
		{
			var fixture = new ManagedLaunchFixture ();
			fixture.Timeout = TimeSpan.FromMilliseconds (150);
			fixture.State.TransformResponse = (command, output) => {
				if (command != "dumpsys activity processes" || !fixture.State.Attached)
					return output;
				return mismatch switch {
					"flag-missing" => output.Replace ("mDebugging=true", "mDebugging=false", StringComparison.Ordinal),
					"package-prefix" => output.Replace (DefaultPackageName + "/u", DefaultPackageName + ".other/u", StringComparison.Ordinal),
					_ => output.Replace ("/u0a", "/u10a", StringComparison.Ordinal),
				};
			};

			Assert.ThrowsAsync<TimeoutException> (async () => await fixture.LaunchAsync ());
		}

		[Test]
		public async Task StartupTimeoutCleansAndAllowsRetry ()
		{
			var fixture = new ManagedLaunchFixture ();
			fixture.Timeout = TimeSpan.FromMilliseconds (150);
			fixture.State.AttachOnDump = false;

			Assert.ThrowsAsync<TimeoutException> (async () => await fixture.LaunchAsync ());
			Assert.IsNull (fixture.State.DebugApp);

			fixture.Timeout = TimeSpan.FromSeconds (2);
			fixture.State.AttachOnDump = true;
			await fixture.LaunchAsync ();
			Assert.AreEqual (2, fixture.State.Commands.Count (command => command.StartsWith ("am set-debug-app", StringComparison.Ordinal)));
		}

		[Test]
		public async Task CancellationDrainsArmingBeforeCleanup ()
		{
			var fixture = new ManagedLaunchFixture ();
			using var cancellation = new CancellationTokenSource ();
			var arming = new TaskCompletionSource (TaskCreationOptions.RunContinuationsAsynchronously);
			var release = new TaskCompletionSource (TaskCreationOptions.RunContinuationsAsynchronously);
			fixture.State.BeforeResponse = command => {
				if (command.StartsWith ("am set-debug-app ", StringComparison.Ordinal)) {
					arming.TrySetResult ();
					return release.Task;
				}
				return Task.CompletedTask;
			};

			Task<string?> launch = fixture.LaunchAsync (cancellation.Token);
			await arming.Task.WaitAsync (TimeSpan.FromSeconds (5));
			cancellation.Cancel ();
			await Task.WhenAny (launch, Task.Delay (100));
			Assert.IsFalse (launch.IsCompleted);
			Assert.IsFalse (fixture.State.Commands.Contains ("am clear-debug-app"));

			release.TrySetResult ();
			Assert.ThrowsAsync<OperationCanceledException> (async () => await launch.WaitAsync (TimeSpan.FromSeconds (8)));
			Assert.IsNull (fixture.State.DebugApp);
		}

		[Test]
		public async Task SameSerialGateRemainsHeldThroughCleanup ()
		{
			var cleanupStarted = new TaskCompletionSource (TaskCreationOptions.RunContinuationsAsynchronously);
			var releaseCleanup = new TaskCompletionSource (TaskCreationOptions.RunContinuationsAsynchronously);
			var first = new ManagedLaunchFixture (serial: "shared-serial");
			first.State.BeforeResponse = command => {
				if (command == "am clear-debug-app") {
					cleanupStarted.TrySetResult ();
					return releaseCleanup.Task;
				}
				return Task.CompletedTask;
			};
			var second = new ManagedLaunchFixture (serial: "shared-serial");

			Task<string?> firstLaunch = first.LaunchAsync ();
			await cleanupStarted.Task.WaitAsync (TimeSpan.FromSeconds (5));

			Task<string?> secondLaunch = second.LaunchAsync ();
			await Task.WhenAny (secondLaunch, Task.Delay (100));
			Assert.IsFalse (secondLaunch.IsCompleted);
			Assert.IsEmpty (second.State.Commands);

			releaseCleanup.TrySetResult ();
			await firstLaunch.WaitAsync (TimeSpan.FromSeconds (8));
			await secondLaunch.WaitAsync (TimeSpan.FromSeconds (8));
		}

		[Test]
		public async Task SameSerialAdmissionTimeoutUsesDistinctDiagnostic ()
		{
			var release = new TaskCompletionSource (TaskCreationOptions.RunContinuationsAsynchronously);
			var first = new ManagedLaunchFixture (serial: "shared-serial");
			first.Timeout = TimeSpan.FromSeconds (40);
			first.State.BeforeResponse = command => command == "pm list users" ? release.Task : Task.CompletedTask;
			Task<string?> firstLaunch = first.LaunchAsync ();
			var second = new ManagedLaunchFixture (serial: "shared-serial");

			try {
				var error = AssertThrowsAsync<TimeoutException> (() => second.LaunchAsync ().WaitAsync (TimeSpan.FromSeconds (35)));
				Assert.AreEqual (ManagedActivityLaunchResources.ManagedLaunchGateTimeout, error.Message);
			} finally {
				release.TrySetResult ();
				await firstLaunch.WaitAsync (TimeSpan.FromSeconds (8));
			}
		}

		[Test]
		public void CleanupFailurePreservesPrimaryErrorAndIsReported ()
		{
			var fixture = new ManagedLaunchFixture ();
			fixture.State.TransformResponse = (command, output) => command switch {
				_ when command == fixture.StartCommand => "Error: Activity not started, primary launch failure",
				"am clear-debug-app" => "cleanup failure",
				_ => output,
			};

			var error = AssertThrowsAsync<ManagedActivityLaunch.CommandFailedException> (() => fixture.LaunchAsync ());
			StringAssert.Contains ("primary launch failure", error.Message);
			Assert.IsTrue (fixture.CleanupErrors.Any (entry => entry.Message.Contains ("Failed to clean up", StringComparison.Ordinal) &&
				entry.Exception.Message.Contains ("cleanup failure", StringComparison.Ordinal)));
		}

		[Test]
		public void CleanupFailureAfterSuccessfulLaunchFailsTheOperation ()
		{
			var fixture = new ManagedLaunchFixture ();
			fixture.State.TransformResponse = (command, output) => command == "am clear-debug-app" ? "cleanup failure" : output;

			var error = AssertThrowsAsync<ManagedActivityLaunch.CommandFailedException> (() => fixture.LaunchAsync ());
			Assert.AreEqual ("cleanup failure", error.Message);
		}

		[Test]
		public void UnsupportedLayoutAfterMutationFailsClosed ()
		{
			var fixture = new ManagedLaunchFixture ();
			fixture.State.SetDebugAppState (DefaultPackageName, isTransient: true);
			fixture.State.TransformResponse = (command, output) => command == "dumpsys activity processes" && fixture.State.Commands.Contains ("am clear-debug-app")
				? output.Replace ("  mForceBackgroundCheck=false", "  mDebugApp=broken\n  mForceBackgroundCheck=false", StringComparison.Ordinal)
				: output;

			Assert.ThrowsAsync<InvalidOperationException> (async () => await fixture.LaunchAsync ());
			Assert.IsFalse (fixture.State.Commands.Any (command => command.StartsWith ("am set-debug-app ", StringComparison.Ordinal)));
		}

		[Test]
		public void InitialDumpTransportFailureDoesNotFallback ()
		{
			var fixture = new ManagedLaunchFixture ();
			fixture.State.FailTransportCommand = "dumpsys activity processes";

			var error = AssertThrowsAsync<InvalidOperationException> (() => fixture.LaunchAsync ());
			StringAssert.Contains ("simulated transport fail", error.Message);
			Assert.IsFalse (fixture.State.Commands.Any (command => command.StartsWith ("am ", StringComparison.Ordinal)));
		}

		[TestCase ("com.example.managed", "com.example.other/.Activity")]
		[TestCase ("com.example.managed", "com.example.managed/")]
		public void RejectsPackageComponentMismatches (string package, string component)
		{
			var fixture = new ManagedLaunchFixture {
				PackageName = package,
				Component = component,
			};

			Assert.ThrowsAsync<ArgumentException> (async () => await fixture.LaunchAsync ());
			Assert.IsFalse (fixture.State.Commands.Any (command => command.Contains ("debug-app", StringComparison.Ordinal)));
		}

		sealed class ManagedLaunchFixture
		{
			public ManagedLaunchFixture (string? serial = null)
			{
				State = new ManagedLaunchTestState (DefaultPackageName);
				this.serial = serial ?? "managed-launch-" + Guid.NewGuid ().ToString ("N");
			}

			public ManagedLaunchTestState State { get; }

			readonly string serial;

			public string PackageName { get; set; } = DefaultPackageName;

			public string? Component { get; set; } = DefaultPackageName + "/.MainActivity";

			public string? User { get; set; } = "0";

			public bool ForceStop { get; set; } = true;

			public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds (2);

			public List<(string Message, Exception Exception)> CleanupErrors { get; } = [];

			public int UnprotectedLaunchCount { get; private set; }

			public string StartCommand => "am start" + (ForceStop ? " -S" : "") +
				(User == null ? "" : " --user " + ManagedActivityLaunch.QuoteForDeviceShell (User)) +
				" -n " + ManagedActivityLaunch.QuoteForDeviceShell (Component ?? PackageName);

			public Task<string?> LaunchAsync (CancellationToken token = default)
			{
				return ManagedActivityLaunch.RunAsync (
					serial, PackageName, Component, User, ForceStop,
					StartCommand, Timeout,
					prepare: _ => Task.CompletedTask,
					runShellCommand: RunShellCommandAsync,
					launchUnprotected: LaunchUnprotectedAsync,
					log: _ => { },
					logCleanupError: (message, error) => CleanupErrors.Add ((message, error)),
					token: token);
			}

			async Task<string> RunShellCommandAsync (string command, CancellationToken token)
			{
				var response = await State.RespondToCommandAsync (command, token);
				if (!response.Success)
					throw new InvalidOperationException (response.Output);
				return response.Output;
			}

			async Task LaunchUnprotectedAsync (CancellationToken token)
			{
				UnprotectedLaunchCount++;
				string output = await RunShellCommandAsync (StartCommand, token);
				ManagedActivityLaunch.CheckStartResult (output, Component ?? PackageName);
			}
		}
	}
}
