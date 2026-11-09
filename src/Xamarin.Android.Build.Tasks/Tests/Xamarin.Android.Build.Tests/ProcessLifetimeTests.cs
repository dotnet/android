using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Xamarin.Android.Tasks;

namespace Xamarin.Android.Build.Tests;

[TestFixture]
[Platform ("Linux,MacOsX")]
public class ProcessLifetimeTests : BaseTest
{
	[Test]
	public async Task ApkDiffDrainsConcurrentStreamsWithoutNormalizingText ()
	{
		var output = new string ('o', 1024) + "\r\n\n";
		var error = new string ('e', 1024) + "\r\n\n";
		using var tool = new OwnedTool ($$"""
			for ((i=0; i<128; i++)); do
			    printf '{{output.Replace ("\r", "\\r").Replace ("\n", "\\n")}}'
			    printf '{{error.Replace ("\r", "\\r").Replace ("\n", "\\n")}}' >&2
			done
			printf 'out-tail'
			printf 'err-tail' >&2
			""");
		var result = await RunApkDiffCommandAsync ("", tool.LogPath, tool.Path, 10).WaitAsync (TimeSpan.FromSeconds (15));
		Assert.AreEqual (0, result.code);
		Assert.AreEqual (string.Concat (System.Linq.Enumerable.Repeat (output, 128)) + "out-tail", result.stdOutput);
		Assert.AreEqual (string.Concat (System.Linq.Enumerable.Repeat (error, 128)) + "err-tail", result.stdError);
		StringAssert.Contains (result.stdOutput, File.ReadAllText (tool.LogPath));
	}

	[TestCase (0)]
	[TestCase (7)]
	public async Task ApkDiffPreservesCompletedStatusAtInheritedEofDeadline (int exitCode)
	{
		using var tool = new OwnedTool ($$"""
			sleep 60 &
			echo $! > "$0.child"
			printf 'before\r\n\nout-tail'
			printf 'diagnostic\r\n\nerr-tail' >&2
			exit {{exitCode}}
			""");
		var timer = Stopwatch.StartNew ();
		var result = await RunApkDiffCommandAsync ("", tool.LogPath, tool.Path, 10).WaitAsync (TimeSpan.FromSeconds (15));
		Assert.AreEqual (exitCode, result.code);
		Assert.AreEqual ("before\r\n\nout-tail", result.stdOutput);
		StringAssert.StartsWith ("diagnostic\r\n\nerr-tail", result.stdError);
		StringAssert.Contains ("redirected output still open after 2 seconds", result.stdError);
		Assert.GreaterOrEqual (timer.Elapsed, TimeSpan.FromSeconds (1.5));
		tool.AssertChildAlive ();
		StringAssert.Contains ($"apkdiff exited with code: {exitCode}", File.ReadAllText (tool.LogPath));
	}

	[Test]
	public async Task ApkDiffExecutionDeadlinePreservesPartialOutputAndKillsOwnedChild ()
	{
		using var tool = new OwnedTool ("""
			sleep 60 &
			child=$!
			echo "$child" > "$0.child"
			printf 'partial\r\n\nout-tail'
			printf 'partial-error\r\n\nerr-tail' >&2
			wait "$child"
			""");
		var result = await RunApkDiffCommandAsync ("", tool.LogPath, tool.Path, 1).WaitAsync (TimeSpan.FromSeconds (10));
		Assert.AreEqual (-1, result.code);
		Assert.AreEqual ("partial\r\n\nout-tail", result.stdOutput);
		StringAssert.StartsWith ("partial-error\r\n\nerr-tail", result.stdError);
		StringAssert.Contains ("timed out after 1 seconds", result.stdError);
		tool.AssertChildExited ();
	}

	[TestCase (false)]
	[TestCase (true)]
	public void AdbOwnersBoundPostExitDrainWithoutKillingServer (bool fastDeploy)
	{
		using var tool = new OwnedTool ("""
			sleep 60 &
			echo $! > "$0.child"
			printf 'ready'
			exit 0
			""");
		using var caller = new CancellationTokenSource ();
		Task run;
		if (fastDeploy) {
			run = RunFastDeployCommand (tool.Path);
		} else {
			run = AdbHelper.RunAsync (tool.Path, null, "devices", caller.Token);
		}
		var timer = Stopwatch.StartNew ();
		var exception = Assert.ThrowsAsync<TimeoutException> (async () => await run.WaitAsync (TimeSpan.FromSeconds (40)));
		Assert.IsNotNull (exception);
		StringAssert.Contains ("redirected output", exception.Message);
		Assert.GreaterOrEqual (timer.Elapsed, TimeSpan.FromSeconds (29));
		Assert.IsFalse (caller.IsCancellationRequested);
		tool.AssertChildAlive ();
	}

	[TestCase (false, 0)]
	[TestCase (false, 7)]
	[TestCase (true, 0)]
	[TestCase (true, 7)]
	public async Task AdbOwnersPreserveOutputAndCompletedStatus (bool fastDeploy, int exitCode)
	{
		using var tool = new OwnedTool ($$"""
			printf ' output\r\n\nout-tail '
			printf ' diagnostic\r\n\nerr-tail ' >&2
			exit {{exitCode}}
			""");
		if (fastDeploy) {
			var result = await RunFastDeployCommand (tool.Path).WaitAsync (TimeSpan.FromSeconds (5));
			Assert.AreEqual (exitCode, result.ExitCode);
			Assert.AreEqual ("output\r\n\nout-tail", result.StandardOutput);
			Assert.AreEqual ("diagnostic\r\n\nerr-tail", result.StandardError);
		} else {
			var result = await AdbHelper.RunAsync (tool.Path, null, "devices", CancellationToken.None).WaitAsync (TimeSpan.FromSeconds (5));
			Assert.AreEqual (exitCode, result.ExitCode);
			Assert.AreEqual (" output\r\n\nout-tail ", result.Output);
			Assert.AreEqual (" diagnostic\r\n\nerr-tail ", result.Error);
		}
	}

	static Task<FastDeploy.AdbCommandResult> RunFastDeployCommand (string path)
	{
		var task = new FastDeploy { AdbToolPath = System.IO.Path.GetDirectoryName (path), AdbToolExe = System.IO.Path.GetFileName (path) };
		var method = typeof (FastDeploy).GetMethod ("RunAdbCommand", BindingFlags.Instance | BindingFlags.NonPublic,
			null, [typeof (string []), typeof (System.Collections.Generic.Dictionary<string, string>)], null);
		Assert.IsNotNull (method);
		return method.Invoke (task, [new [] { "devices" }, null]) as Task<FastDeploy.AdbCommandResult> ??
			throw new InvalidOperationException ("Expected an asynchronous FastDeploy command.");
	}

	sealed class OwnedTool : IDisposable
	{
		readonly string directory = System.IO.Path.Combine (System.IO.Path.GetTempPath (), "process-owner-" + Guid.NewGuid ().ToString ("N"));

		public string Path => System.IO.Path.Combine (directory, "tool");
		public string LogPath => System.IO.Path.Combine (directory, "tool.log");

		public OwnedTool (string body)
		{
			Directory.CreateDirectory (directory);
			File.WriteAllText (Path, "#!/bin/bash\necho $$ > \"$0.pid\"\n" + body + "\n");
			if (!OperatingSystem.IsWindows ())
				File.SetUnixFileMode (Path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
		}

		public void AssertChildAlive ()
		{
			using var process = Process.GetProcessById (int.Parse (File.ReadAllText (Path + ".child").Trim ()));
			Assert.IsFalse (process.HasExited, "The persistent server must survive the client's drain deadline.");
		}

		public void AssertChildExited ()
		{
			try {
				using var process = Process.GetProcessById (int.Parse (File.ReadAllText (Path + ".child").Trim ()));
				Assert.IsTrue (process.WaitForExit (5000), "Execution timeout must terminate the owned child.");
			} catch (ArgumentException) {
				// The owned child has already been reaped.
			}
		}

		public void Dispose ()
		{
			foreach (var suffix in new [] { ".pid", ".child" }) {
				if (!File.Exists (Path + suffix))
					continue;
				try {
					using var process = Process.GetProcessById (int.Parse (File.ReadAllText (Path + suffix).Trim ()));
					if (!process.HasExited)
						process.Kill (entireProcessTree: true);
					Assert.IsTrue (process.WaitForExit (5000), "Fixture process must exit before deleting its files.");
				} catch (ArgumentException) {
					// The fixture process has already been reaped.
				}
			}
			Directory.Delete (directory, recursive: true);
		}
	}
}
