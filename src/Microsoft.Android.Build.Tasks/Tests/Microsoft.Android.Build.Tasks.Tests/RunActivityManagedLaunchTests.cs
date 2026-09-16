using Microsoft.Build.Framework;
using Microsoft.Android.Run.Tests;
using Mono.AndroidTools;
using Mono.AndroidTools.Adb;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xamarin.Android.Build.Tests;
using Xamarin.Android.Tasks;

namespace Microsoft.Android.Build.Tasks.Tests
{
	[TestFixture]
	public class RunActivityManagedLaunchTests
	{
		const string PackageName = "com.example.managed";

		[TestCase (false, false)]
		[TestCase (false, true)]
		[TestCase (true, false)]
		[TestCase (true, true)]
		public void ProtectsOnlyManagedDebugLaunches (bool attachDebugger, bool allowJavaDebugging)
		{
			using var server = CreateServer ();
			var task = CreateRunActivity (server);
			task.AttachDebugger = attachDebugger;
			task.AllowJavaDebugging = allowJavaDebugging;

			Assert.IsTrue (task.Execute ());
			Assert.AreEqual (attachDebugger && !allowJavaDebugging,
				server.Commands.Any (command => command.StartsWith ("am set-debug-app ", StringComparison.Ordinal)));
			Assert.AreEqual (attachDebugger, server.Commands.Contains ("date +%s"));
			if (attachDebugger && !allowJavaDebugging) {
				Assert.IsTrue (server.State.Attached);
				Assert.IsFalse (server.Commands.Any (command => command.Contains (" -D", StringComparison.Ordinal) || command.Contains (" -W", StringComparison.Ordinal)));
			}
		}

		[Test]
		public async Task ExplicitJavaDebuggingStillUsesDAndJdwp ()
		{
			await using var server = CreateServer ("emulator-5554");
			var task = CreateRunActivity (server);
			task.AttachDebugger = true;
			task.AllowJavaDebugging = true;
			var cancelled = new TaskCompletionSource (TaskCreationOptions.RunContinuationsAsynchronously);
			using var registration = task.CancellationToken.Register (() => cancelled.TrySetResult ());
			server.State.BeforeResponse = command => {
				if (command.StartsWith ("ps", StringComparison.Ordinal)) {
					((ICancelableTask) task).Cancel ();
					return cancelled.Task;
				}
				return Task.CompletedTask;
			};

			try {
				await Task.Run (() => task.Execute ()).WaitAsync (TimeSpan.FromSeconds (8));
				await task.Finished.Task.WaitAsync (TimeSpan.FromSeconds (5));
				Assert.IsTrue (server.Commands.Any (command => command.StartsWith ("am start ", StringComparison.Ordinal) && command.Contains (" -D", StringComparison.Ordinal)));
				Assert.IsTrue (server.Commands.Any (command => command.StartsWith ("ps", StringComparison.Ordinal)));
				Assert.IsFalse (server.Commands.Any (command => command.Contains ("debug-app", StringComparison.Ordinal)));
			} finally {
				((AsyncTask) task).Cancel ();
			}
		}

		[TestCase ("warm")]
		[TestCase ("custom-process")]
		[TestCase ("unsupported-layout")]
		public void UnsupportedLaunchesRetainLegacyBehavior (string reason)
		{
			using var server = CreateServer ();
			var messages = new List<BuildMessageEventArgs> ();
			var task = CreateRunActivity (server, messages: messages);
			task.AttachDebugger = true;
			task.AllowJavaDebugging = false;
			task.ForceStop = reason != "warm";
			if (reason == "custom-process")
				server.State.EffectiveProcessName = PackageName + ":custom";
			if (reason == "unsupported-layout")
				server.State.TransformResponse = (command, output) => command == "dumpsys activity processes" ? output + "  vendor postamble\n" : output;

			Assert.IsTrue (task.Execute ());
			Assert.IsFalse (server.Commands.Any (command => command.Contains ("debug-app", StringComparison.Ordinal)));
			Assert.IsTrue (messages.Any (message => message.Message.Contains ("Launching without", StringComparison.Ordinal)));
		}

		[Test]
		public async Task FallbackCancellationAfterLaunchReturnsFalseAndFinishes ()
		{
			await using var server = CreateServer ();
			var task = CreateRunActivity (server);
			task.AttachDebugger = true;
			task.AllowJavaDebugging = false;
			task.ForceStop = true;
			server.State.EffectiveProcessName = PackageName + ":custom";
			var starting = new TaskCompletionSource (TaskCreationOptions.RunContinuationsAsynchronously);
			var release = new TaskCompletionSource (TaskCreationOptions.RunContinuationsAsynchronously);
			server.State.BeforeResponse = command => {
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
				release.TrySetResult ();
				Assert.IsFalse (await launch.WaitAsync (TimeSpan.FromSeconds (8)));
				Assert.IsFalse (server.Commands.Any (command => command.Contains ("debug-app", StringComparison.Ordinal)));
			} finally {
				release.TrySetResult ();
				await task.Finished.Task.WaitAsync (TimeSpan.FromSeconds (8));
			}
		}

		[TestCase ("Error: Activity class {com.example.managed/.MainActivity} does not exist.\n", true)]
		[TestCase ("Error: primary launch failure\n", false)]
		public void PreservesTypedDiagnosticsAndCleans (string diagnostic, bool notFound)
		{
			using var server = CreateServer ();
			var errors = new List<BuildErrorEventArgs> ();
			var task = CreateRunActivity (server, errors: errors);
			task.AttachDebugger = true;
			task.AllowJavaDebugging = false;
			server.State.TransformResponse = (command, output) => command.StartsWith ("am start ", StringComparison.Ordinal) ? output + diagnostic : output;

			Assert.IsFalse (task.Execute ());
			Assert.IsTrue (errors.Any (error => error.Code.StartsWith ("XARUNA", StringComparison.Ordinal)));
			Assert.IsTrue (errors.Any (error => error.Message.Contains (notFound ? "ActivityNotFoundException" : "primary launch failure", StringComparison.Ordinal)));
			CollectionAssert.Contains (server.Commands, "am clear-debug-app");
		}

		[TestCase ("arm")]
		[TestCase ("cleanup")]
		public async Task PrivateBudgetTimeoutIsFailure (string boundary)
		{
			await using var server = CreateServer ();
			var errors = new List<BuildErrorEventArgs> ();
			var task = CreateRunActivity (server, errors: errors);
			task.AttachDebugger = true;
			task.AllowJavaDebugging = false;
			var pending = new TaskCompletionSource (TaskCreationOptions.RunContinuationsAsynchronously);
			string command = boundary == "arm" ? "am set-debug-app 'com.example.managed'" : "am clear-debug-app";
			server.State.BeforeResponse = value => value == command ? pending.Task : Task.CompletedTask;

			Assert.IsFalse (await Task.Run (() => task.Execute ()).WaitAsync (TimeSpan.FromSeconds (15)));
			Assert.IsTrue (errors.Any (error => error.Code == "XARUNA7017" && error.Message.Contains ("Timed out", StringComparison.Ordinal)));
			Assert.IsFalse (errors.Any (error => error.Code == "XARUNA7012" || error.Code == "XARUNA7013"));
		}

		[Test]
		public void ReportsBothPrimaryAndCleanupErrors ()
		{
			using var server = CreateServer ();
			var errors = new List<BuildErrorEventArgs> ();
			var task = CreateRunActivity (server, errors: errors);
			task.AttachDebugger = true;
			task.AllowJavaDebugging = false;
			server.State.TransformResponse = (command, output) => command switch {
				_ when command.StartsWith ("am start ", StringComparison.Ordinal) => "Error: primary launch failure",
				"am clear-debug-app" => "cleanup failure",
				_ => output,
			};

			Assert.IsFalse (task.Execute ());
			Assert.IsTrue (errors.Any (error => error.Message.Contains ("primary launch failure", StringComparison.Ordinal)));
			Assert.IsTrue (errors.Any (error => error.Message.Contains ("Failed to clean up", StringComparison.Ordinal) && error.Message.Contains ("cleanup failure", StringComparison.Ordinal)));
		}

		[Test]
		public async Task CancellationDrainsMutationBeforeReturning ()
		{
			await using var server = CreateServer ();
			var task = CreateRunActivity (server);
			task.AttachDebugger = true;
			task.AllowJavaDebugging = false;
			var arming = new TaskCompletionSource (TaskCreationOptions.RunContinuationsAsynchronously);
			var release = new TaskCompletionSource (TaskCreationOptions.RunContinuationsAsynchronously);
			server.State.BeforeResponse = command => {
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
				Assert.IsFalse (launch.IsCompleted);
				Assert.IsFalse (server.Commands.Contains ("am clear-debug-app"));
				release.TrySetResult ();
				Assert.IsFalse (await launch.WaitAsync (TimeSpan.FromSeconds (8)));
				Assert.IsNull (server.State.DebugApp);
			} finally {
				release.TrySetResult ();
				await task.Finished.Task.WaitAsync (TimeSpan.FromSeconds (8));
			}
		}

		[TestCase (".MainActivity", "true", "", true)]
		[TestCase (".MainActivity", "false", "", false)]
		[TestCase ("", "true", "com.example/runner", false)]
		public async Task RunArgumentsCarryOnlyExplicitActivityDebugIntent (string activity, string attachDebugger, string instrumentation, bool expectAttachFlag)
		{
			string directory = Path.Combine (Path.GetTempPath (), $"managed-launch-msbuild-{Guid.NewGuid ():N}");
			Directory.CreateDirectory (directory);
			string project = Path.Combine (directory, "run.proj");
			try {
				new System.Xml.Linq.XDocument (new System.Xml.Linq.XElement ("Project",
					new System.Xml.Linq.XElement ("PropertyGroup",
						new System.Xml.Linq.XElement ("_XamarinAndroidBuildTasksAssembly", typeof (RunActivity).Assembly.Location),
						new System.Xml.Linq.XElement ("PrepTasksAssembly", typeof (RunActivity).Assembly.Location)),
					new System.Xml.Linq.XElement ("Import", new System.Xml.Linq.XAttribute ("Project", Path.Combine (TestContext.CurrentContext.TestDirectory, "Microsoft.Android.Sdk.Application.targets"))),
					new System.Xml.Linq.XElement ("PropertyGroup",
						new System.Xml.Linq.XElement ("_AndroidComputeRunArgumentsDependsOn", "TestNoOp"),
						new System.Xml.Linq.XElement ("_AndroidPackage", PackageName),
						new System.Xml.Linq.XElement ("AndroidLaunchActivity", activity),
						new System.Xml.Linq.XElement ("AndroidInstrumentation", instrumentation),
						new System.Xml.Linq.XElement ("AndroidAttachDebugger", attachDebugger),
						new System.Xml.Linq.XElement ("AndroidDebuggerServer", "true"),
						new System.Xml.Linq.XElement ("Configuration", "Debug"),
						new System.Xml.Linq.XElement ("WaitForExit", "false")),
					new System.Xml.Linq.XElement ("Target", new System.Xml.Linq.XAttribute ("Name", "TestNoOp")),
					new System.Xml.Linq.XElement ("Target", new System.Xml.Linq.XAttribute ("Name", "ComputeRunArguments")))).Save (project);

				var result = await RunDotnetAsync ("msbuild", project, "-nologo", "-t:_AndroidComputeRunArguments", "-getProperty:RunArguments");

				Assert.AreEqual (0, result.ExitCode, result.Output + result.Error);
				Assert.AreEqual (expectAttachFlag, result.Output.Contains ("--attach-debugger", StringComparison.Ordinal), result.Output);
				Assert.AreEqual (instrumentation.Length != 0, result.Output.Contains ("--instrument", StringComparison.Ordinal), result.Output);
				Assert.AreEqual (instrumentation.Length == 0, result.Output.Contains ("--activity", StringComparison.Ordinal), result.Output);
			} finally {
				File.Delete (project);
				Directory.Delete (directory);
			}
		}

		static ManagedLaunchTestServer CreateServer (string serial = "managed-launch-test") => new ManagedLaunchTestServer (PackageName, serial);

		static ObservedRunActivity CreateRunActivity (ManagedLaunchTestServer server, IList<BuildErrorEventArgs>? errors = null, IList<BuildMessageEventArgs>? messages = null)
		{
			var target = "-s " + server.Serial;
			IBuildEngine4 engine = new MockBuildEngine (TestContext.Out, errors: errors, messages: messages);
			var adb = new AdbServer (System.Net.IPAddress.Loopback, server.Port);
			engine.RegisterTaskObjectAssemblyLocal (
				Tuple.Create ("AndroidHelper_AndroidDevice", target),
				new AndroidDevice (server.Serial, adb: adb), RegisteredTaskObjectLifetime.Build);
			return new ObservedRunActivity {
				BuildEngine = engine,
				AdbTarget = target,
				PackageName = PackageName,
				ActivityName = ".MainActivity",
				Server = true,
			};
		}

		static async Task<(int ExitCode, string Output, string Error)> RunDotnetAsync (params string [] arguments)
		{
			string dotnet = Environment.GetEnvironmentVariable ("DOTNET_HOST_PATH") ?? "dotnet";
			var psi = new System.Diagnostics.ProcessStartInfo (dotnet) {
				UseShellExecute = false,
			};
			foreach (string argument in arguments)
				psi.ArgumentList.Add (argument);
			return await ProcessTestUtilities.RunProcessAsync (psi, TimeSpan.FromSeconds (45));
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
	}
}
