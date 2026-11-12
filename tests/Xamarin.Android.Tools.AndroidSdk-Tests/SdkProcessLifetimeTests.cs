#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Xamarin.Android.Tools.Tests;

[TestFixture]
[Platform ("Linux,MacOsX")]
[Parallelizable (ParallelScope.Children)]
public class SdkProcessLifetimeTests
{
	[TestCase ("avd-list")]
	[TestCase ("avd-delete")]
	[TestCase ("avd-profiles")]
	[TestCase ("avd-create")]
	[TestCase ("sdk-list")]
	[TestCase ("sdk-install")]
	[TestCase ("sdk-license")]
	[TestCase ("tar")]
	[TestCase ("jdk")]
	public async Task ExitedCommandBoundsInheritedOutput (string command)
	{
		using var child = new Command ("""
			printf 'out-tail'
			printf 'err-tail' >&2
			sleep 90 &
			echo $! > "$0.child"
			exit 0
			""", command);
		var timer = Stopwatch.StartNew ();
		Task operation = child.RunAsync (command, CancellationToken.None);
		try {
			var error = Assert.ThrowsAsync<TimeoutException> (async () => await operation.WaitAsync (TimeSpan.FromSeconds (40)));
			Assert.That (error?.Message, Does.Contain ("30 seconds"));
			Assert.That (timer.Elapsed, Is.GreaterThanOrEqualTo (TimeSpan.FromSeconds (29)));
			Assert.That (operation.IsCompleted, Is.True, "The owner's own deadline must finish, not the test watchdog.");
			Assert.That (child.IsRunning (".child"), Is.True, "Do not kill unrelated installer services or shared clients.");
		} finally {
			child.Stop ();
			try {
				await operation.WaitAsync (TimeSpan.FromSeconds (5));
			} catch (Exception) when (operation.IsCompleted) {
				// Observe the tested failure before cleaning up the owned fixture.
			}
		}
	}

	[TestCase ("avd-list")]
	[TestCase ("avd-delete")]
	[TestCase ("avd-profiles")]
	[TestCase ("sdk-list")]
	public async Task FiniteDiscoveryBoundsLiveRootWithoutCallerDeadline (string command)
	{
		using var child = new Command ("exec sleep 90", command);
		Task operation = child.RunAsync (command, CancellationToken.None);
		try {
			Assert.ThrowsAsync<TimeoutException> (async () => await operation.WaitAsync (TimeSpan.FromSeconds (40)));
			Assert.That (operation.IsCompleted, Is.True, "Default callers must receive the owner deadline.");
			Assert.That (child.IsRunning (".pid"), Is.False);
		} finally {
			child.Stop ();
			try {
				await operation.WaitAsync (TimeSpan.FromSeconds (5));
			} catch (Exception) when (operation.IsCompleted) {
				// Observe the tested failure before cleaning up the owned fixture.
			}
		}
	}

	[TestCase ("avd-create")]
	[TestCase ("sdk-install")]
	[TestCase ("sdk-license")]
	[TestCase ("tar")]
	[TestCase ("jdk")]
	public async Task CallerCancellationStopsAndJoinsTheOwnedClient (string command)
	{
		using var child = new Command ("exec sleep 90", command);
		var operation = child.RunAsync (command);
		await child.WaitForStartAsync ();
		child.Cancellation.Cancel ();
		var error = Assert.CatchAsync<OperationCanceledException> (async () => await operation.WaitAsync (TimeSpan.FromSeconds (5)));
		Assert.That (error?.CancellationToken, Is.EqualTo (child.Cancellation.Token));
		Assert.That (child.IsRunning (".pid"), Is.False);
	}

	[TestCase ("sdk-install")]
	[TestCase ("jdk")]
	public async Task InstallationDurationRemainsCallerControlled (string command)
	{
		using var child = new Command ("exec sleep 90", command);
		var operation = child.RunAsync (command);
		try {
			await child.WaitForStartAsync ();
			await Task.Delay (TimeSpan.FromSeconds (31));
			Assert.That (operation.IsCompleted, Is.False, "An active installer must not inherit the finite query timeout.");
		} finally {
			child.Cancellation.Cancel ();
			Assert.CatchAsync<OperationCanceledException> (async () => await operation.WaitAsync (TimeSpan.FromSeconds (5)));
		}
	}

	[TestCase ("jdk")]
	[TestCase ("tar")]
	public void InstallerFailurePreservesUnterminatedDiagnostics (string command)
	{
		using var child = new Command ("printf 'late error-tail' >&2\nexit 7", command);
		var error = Assert.CatchAsync (async () => await child.RunAsync (command).WaitAsync (TimeSpan.FromSeconds (5)));
		Assert.That (error?.Message, Does.Contain ("late error-tail"));
		Assert.That (error, command == "tar" ? Is.TypeOf<IOException> () : Is.TypeOf<InvalidOperationException> ());
	}

	[Test]
	public async Task SdkInputLoggerFailureStopsTheClientAndPreservesTheFailure ()
	{
		using var child = new Command ("""
			IFS= read -r answer
			exec 0<&-
			exec sleep 90
			""", "sdk-license");
		var failure = new InvalidOperationException ("The input logger failed.");
		using var sdk = new SdkManager (logger: (_, message) => {
			if (message.StartsWith ("sdkmanager standard input closed:", StringComparison.Ordinal))
				throw failure;
		});
		var operation = child.RunSdkAsync (sdk, ["--licenses"]);
		try {
			var error = Assert.ThrowsAsync<InvalidOperationException> (async () => await operation.WaitAsync (TimeSpan.FromSeconds (5)));
			Assert.That (error, Is.SameAs (failure));
			Assert.That (child.IsRunning (".pid"), Is.False);
		} finally {
			child.Cancellation.Cancel ();
			try {
				await operation.WaitAsync (TimeSpan.FromSeconds (5));
			} catch (Exception) when (operation.IsCompleted) {
				// Observe the tested failure before cleaning up the owned fixture.
			}
		}
	}

	[TestCase ("sdk-list")]
	[TestCase ("avd-list")]
	[TestCase ("jdk-properties")]
	[Platform ("MacOsX")]
	[NonParallelizable]
	public async Task SdkCapturesClosePipeDescriptorsWithoutWaitingForFinalizers (string command)
	{
		TestContext.WriteLine (typeof (object).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute> ()?.InformationalVersion);
		using var child = new Command ("exit 0", command);
		for (int i = 0; i < 5; i++)
			await child.RunAsync (command);
		GC.Collect ();
		GC.WaitForPendingFinalizers ();
		GC.Collect ();
		bool noGc = GC.TryStartNoGCRegion (64 * 1024 * 1024);
		Assert.That (noGc, Is.True, "The regression must not rely on finalizers running.");
		try {
			int before = CountDescriptors ();
			for (int i = 0; i < 25; i++)
				await child.RunAsync (command);
			Assert.That (CountDescriptors () - before, Is.LessThanOrEqualTo (4), "Native capture alone retains two descriptors per call until GC.");
		} finally {
			GC.EndNoGCRegion ();
		}
	}

	static int CountDescriptors ()
	{
		int count = 0;
		for (int fd = 0; fd < 8192; fd++)
			if (Fcntl (fd, 1) != -1)
				count++;
		return count;
	}

	[DllImport ("libc", EntryPoint = "fcntl")]
	static extern int Fcntl (int fd, int command);

	sealed class Command : IDisposable
	{
		readonly string directory = Path.Combine (Path.GetTempPath (), "sdk-process-" + Guid.NewGuid ().ToString ("N"));
		readonly string executable;
		readonly SdkManager sdk = new (logger: (_, _) => { });
		readonly JdkInstaller jdk = new (logger: (_, _) => { });
		public CancellationTokenSource Cancellation { get; } = new ();

		public Command (string body, string command)
		{
			Directory.CreateDirectory (directory);
			executable = Path.Combine (directory, "command");
			string preflight = command == "avd-create" ? "if [ \"$1\" = list ]; then exit 0; fi\n" : "";
			File.WriteAllText (executable, "#!/bin/sh\n" + preflight + "echo $$ > \"$0.pid\"\n" + body + "\n");
			if (!OperatingSystem.IsWindows ())
				File.SetUnixFileMode (executable, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
		}

		public Task RunAsync (string command, CancellationToken? cancellationToken = null)
		{
			var runner = new AvdManagerRunner (executable);
			var token = cancellationToken ?? Cancellation.Token;
			return command switch {
				"avd-list" => runner.ListAvdsAsync (token),
				"avd-delete" => runner.DeleteAvdAsync ("name", token),
				"avd-profiles" => runner.ListDeviceProfilesAsync (token),
				"avd-create" => runner.GetOrCreateAvdAsync ("name", "image", cancellationToken: token),
				"sdk-list" => RunSdkAsync (sdk, ["--list"], token),
				"sdk-install" => RunSdkAsync (sdk, ["platform-tools"], token),
				"sdk-license" => RunSdkAsync (sdk, ["--licenses"], token),
				"jdk-properties" => Task.FromResult ((typeof (JdkInfo).GetMethod ("GetJavaProperties", BindingFlags.NonPublic | BindingFlags.Static) ??
					throw new MissingMethodException ()).Invoke (null, [(Action<TraceLevel, string>) ((_, _) => { }), executable])),
				"jdk" => Invoke (typeof (JdkInstaller), jdk, "RunPlatformInstallerAsync",
					[typeof (ProcessStartInfo), typeof (CancellationToken)], [StartInfo (), token]),
				"tar" => Invoke (typeof (DownloadUtils), null, "ExtractTarGzAsync",
					[typeof (string), typeof (ProcessStartInfo), typeof (Action<TraceLevel, string>), typeof (CancellationToken)],
					["archive.tar.gz", StartInfo (), (Action<TraceLevel, string>) ((_, _) => { }), token]),
				_ => throw new ArgumentOutOfRangeException (nameof (command)),
			};
		}

		public Task RunSdkAsync (SdkManager manager, string [] arguments, CancellationToken? cancellationToken = null) =>
			Invoke (typeof (SdkManager), manager, "RunSdkManagerAsync",
				[typeof (string), typeof (string []), typeof (bool), typeof (CancellationToken)],
				[executable, arguments, false, cancellationToken ?? Cancellation.Token]);

		ProcessStartInfo StartInfo () => new (executable) {
			UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true,
		};

		static Task Invoke (Type type, object? owner, string method, Type [] types, object [] args)
		{
			var info = type.GetMethod (method, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static, types) ??
				throw new MissingMethodException (type.FullName, method);
			return info.Invoke (owner, args) as Task ?? throw new InvalidOperationException ("The command must return a task.");
		}

		public async Task WaitForStartAsync ()
		{
			using var deadline = new CancellationTokenSource (TimeSpan.FromSeconds (5));
			while (!File.Exists (executable + ".pid"))
				await Task.Delay (10, deadline.Token);
		}

		public bool IsRunning (string suffix)
		{
			if (!File.Exists (executable + suffix))
				return false;
			try {
				using var process = Process.GetProcessById (int.Parse (File.ReadAllText (executable + suffix).Trim (), CultureInfo.InvariantCulture));
				return !process.HasExited;
			} catch (ArgumentException) {
				return false;
			}
		}

		public void Stop ()
		{
			Cancellation.Cancel ();
			foreach (var suffix in new [] { ".pid", ".child" }) {
				if (!File.Exists (executable + suffix))
					continue;
				try {
					using var process = Process.GetProcessById (int.Parse (File.ReadAllText (executable + suffix).Trim (), CultureInfo.InvariantCulture));
					if (!process.HasExited)
						process.Kill (entireProcessTree: true);
					Assert.That (process.WaitForExit (5000), Is.True, "Reap the owned fixture before deleting its files.");
				} catch (ArgumentException) {
					// The owned fixture has already been reaped.
				}
			}
		}

		public void Dispose ()
		{
			Stop ();
			sdk.Dispose ();
			jdk.Dispose ();
			Cancellation.Dispose ();
			Directory.Delete (directory, recursive: true);
		}
	}
}
