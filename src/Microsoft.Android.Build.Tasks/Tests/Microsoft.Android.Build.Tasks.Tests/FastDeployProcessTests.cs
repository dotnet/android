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

		public void Dispose ()
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
			Directory.Delete (directory, recursive: true);
		}
	}
}
