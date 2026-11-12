using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Android.Tasks;
using NUnit.Framework;

namespace Xamarin.Android.Build.Tests;

[TestFixture]
public class FastDeployProcessTests
{
	[Test]
	public void CleanupObservesExitWhenCaptureFaults ()
	{
		var fault = new IOException ("Injected output capture failure.");
		var exit = new TaskCompletionSource<bool> (TaskCreationOptions.RunContinuationsAsynchronously);
		var method = typeof (FastDeploy).GetMethod ("WaitForCommandCompletionAsync", BindingFlags.Static | BindingFlags.NonPublic);
		Assert.IsNotNull (method);
		var cleanup = method.Invoke (null, new object [] { Task.FromException (fault), exit.Task, CancellationToken.None }) as Task ??
			throw new InvalidOperationException ("Expected asynchronous command cleanup.");
		try {
			Assert.IsFalse (cleanup.IsCompleted, "Capture failure must not bypass the owned exit wait.");
		} finally {
			exit.SetResult (true);
		}
		var error = Assert.ThrowsAsync<IOException> (async () => await cleanup.WaitAsync (TimeSpan.FromSeconds (5)));
		Assert.AreSame (fault, error, "Successful exit cleanup must preserve the original capture failure.");
	}

	[Test]
	[Platform ("Linux,MacOsX")]
	public void BoundsPostExitDrainWithoutKillingServer ()
	{
		using var tool = new OwnedAdb ("""
			sleep 60 &
			echo $! > "$0.child"
			printf 'ready'
			exit 0
			""");
		var timer = Stopwatch.StartNew ();
		var run = RunCommand (tool.Path);
		var exception = Assert.ThrowsAsync<TimeoutException> (async () => await run.WaitAsync (TimeSpan.FromSeconds (40)));
		Assert.IsNotNull (exception);
		StringAssert.Contains ("redirected output", exception.Message);
		Assert.GreaterOrEqual (timer.Elapsed, TimeSpan.FromSeconds (29));
		using var server = Process.GetProcessById (int.Parse (File.ReadAllText (tool.Path + ".child").Trim (), CultureInfo.InvariantCulture));
		Assert.IsFalse (server.HasExited, "The server must survive the client's drain deadline.");
	}

	[TestCase (0)]
	[TestCase (7)]
	[Platform ("Linux,MacOsX")]
	public async Task PreservesOutputAndCompletedStatus (int exitCode)
	{
		using var tool = new OwnedAdb ($$"""
			printf ' output\r\n\nout-tail '
			printf ' diagnostic\r\n\nerr-tail ' >&2
			exit {{exitCode}}
			""");
		var result = await RunCommand (tool.Path).WaitAsync (TimeSpan.FromSeconds (5));
		Assert.AreEqual (exitCode, result.ExitCode);
		Assert.AreEqual ("output\r\n\nout-tail", result.StandardOutput);
		Assert.AreEqual ("diagnostic\r\n\nerr-tail", result.StandardError);
	}

	[TestCase ("mkdir", false)]
	[TestCase ("mkdir", true)]
	[TestCase ("cp", false)]
	[TestCase ("cp", true)]
	[TestCase ("rm", false)]
	[TestCase ("rm", true)]
	[Platform ("Linux,MacOsX")]
	public async Task RemoteArgumentsSurviveAdbShellReparsing (string command, bool runAs)
	{
		using var tool = new OwnedAdb ("""
			[ "$1" = shell ] || exit 9
			shift
			cd "$(dirname "$0")" || exit 10
			HOME="$PWD"
			PATH="$(dirname "$0"):$PATH"
			export HOME PATH
			exec /bin/sh -c "$*"
			""");
		var root = Path.GetDirectoryName (tool.Path) ?? throw new InvalidOperationException ();
		var runAsPath = Path.Combine (root, "run-as");
		File.WriteAllText (runAsPath, "#!/bin/sh\nshift\nexport XA_TEST_RUN_AS=inside\nexec \"$@\"\n");
		if (!OperatingSystem.IsWindows ())
			File.SetUnixFileMode (runAsPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
		var task = new FastDeploy { AdbToolPath = root, AdbToolExe = "adb" };
		var packageInfo = typeof (FastDeploy).GetField ("packageInfo", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue (task) ??
			throw new InvalidOperationException ();
		packageInfo.GetType ().GetProperty ("PackageName")?.SetValue (packageInfo, "com.example.app");
		var path = Path.Combine (root, "payload 'quoted' $HOME [1];#.dll");
		var destination = Path.Combine (root, "destination with spaces.dll");
		if (command != "mkdir")
			File.WriteAllText (path, "payload");
		string [] args = command switch {
			"mkdir" => ["mkdir", "-p", path],
			"cp" => ["cp", "-p", path, destination],
			_ => ["rm", "-f", path],
		};
		var result = await InvokeShellCommand (task, runAs ? "RunAs" : "RunAdbShellCommand", args);
		Assert.AreEqual (0, result.ExitCode, result.Output);
		if (command == "mkdir")
			Assert.IsTrue (Directory.Exists (path));
		else if (command == "cp")
			Assert.AreEqual ("payload", File.ReadAllText (destination));
		else
			Assert.IsFalse (File.Exists (path));

		if (runAs) {
			var nested = await InvokeShellCommand (task, "RunAs", ["sh", "-c", "printf '%s' \"$XA_TEST_RUN_AS\""]);
			Assert.AreEqual (0, nested.ExitCode, nested.Output);
			Assert.AreEqual ("inside", nested.Output, "The script must execute inside run-as, not be re-parsed by its parent.");
		}
	}

	[Test]
	[Platform ("Linux,MacOsX")]
	public async Task CancellationReachesPreflightDeviceDiscovery ()
	{
		using var tool = new OwnedAdb ("exec sleep 90");
		var task = new FastDeploy {
			BuildEngine = new MockBuildEngine (TestContext.Out),
			AdbToolPath = Path.GetDirectoryName (tool.Path), AdbToolExe = "adb",
		};
		var run = Task.Run (() => task.Execute ());
		try {
			using var deadline = new CancellationTokenSource (TimeSpan.FromSeconds (5));
			while (!File.Exists (tool.Path + ".pid"))
				await Task.Delay (10, deadline.Token);
			int pid = int.Parse (File.ReadAllText (tool.Path + ".pid"), CultureInfo.InvariantCulture);
			using var client = Process.GetProcessById (pid);
			task.Cancel ();
			await run.WaitAsync (TimeSpan.FromSeconds (5));
			Assert.IsTrue (task.CancellationToken.IsCancellationRequested);
			Assert.IsTrue (client.WaitForExit (5000), "Cancel must reach the initial live adb client, before deployment starts.");
		} finally {
			task.Cancel ();
			tool.Stop ();
			await run.WaitAsync (TimeSpan.FromSeconds (5));
		}
	}

	static Task<FastDeploy.AdbCommandResult> InvokeShellCommand (FastDeploy task, string name, string [] arguments)
	{
		var method = typeof (FastDeploy).GetMethod (name, BindingFlags.Instance | BindingFlags.NonPublic) ??
			throw new MissingMethodException (name);
		return method.Invoke (task, [arguments]) as Task<FastDeploy.AdbCommandResult> ??
			throw new InvalidOperationException ();
	}

	static Task<FastDeploy.AdbCommandResult> RunCommand (string path)
	{
		var task = new FastDeploy { AdbToolPath = Path.GetDirectoryName (path), AdbToolExe = Path.GetFileName (path) };
		var method = typeof (FastDeploy).GetMethod ("RunAdbCommand", BindingFlags.Instance | BindingFlags.NonPublic,
			null, [typeof (string []), typeof (System.Collections.Generic.Dictionary<string, string>)], null);
		Assert.IsNotNull (method);
		return method.Invoke (task, [new [] { "devices" }, null]) as Task<FastDeploy.AdbCommandResult> ??
			throw new InvalidOperationException ("Expected an asynchronous FastDeploy command.");
	}

	sealed class OwnedAdb : IDisposable
	{
		readonly string directory = System.IO.Path.Combine (System.IO.Path.GetTempPath (), "fastdeploy-process-" + Guid.NewGuid ().ToString ("N"));
		public string Path => System.IO.Path.Combine (directory, "adb");

		public OwnedAdb (string body)
		{
			Directory.CreateDirectory (directory);
			File.WriteAllText (Path, "#!/bin/bash\necho $$ > \"$0.pid\"\n" + body + "\n");
			if (!OperatingSystem.IsWindows ())
				File.SetUnixFileMode (Path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
		}

		public void Stop ()
		{
			foreach (var suffix in new [] { ".pid", ".child" }) {
				if (!File.Exists (Path + suffix))
					continue;
				try {
					using var process = Process.GetProcessById (int.Parse (File.ReadAllText (Path + suffix).Trim (), CultureInfo.InvariantCulture));
					if (!process.HasExited)
						process.Kill (entireProcessTree: true);
					Assert.IsTrue (process.WaitForExit (5000));
				} catch (ArgumentException) {
					// The owned process has already been reaped.
				}
			}
		}

		public void Dispose ()
		{
			Stop ();
			Directory.Delete (directory, recursive: true);
		}
	}
}
