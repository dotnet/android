using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using NUnit.Framework;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests;

[TestFixture, NonParallelizable]
[Platform (Exclude = "Win")]
public class BaseTestProcessTests : BaseTest
{
	string directory = "";

	[SetUp]
	public void CreateProcessDirectory ()
	{
		directory = Path.Combine (Path.GetTempPath (), $"base-process-{Guid.NewGuid ():N}");
		Directory.CreateDirectory (directory);
	}

	[TearDown]
	public void DeleteProcessDirectory ()
	{
		foreach (var file in Directory.EnumerateFiles (directory, "*.pid")) {
			int pid = int.Parse (File.ReadAllText (file), CultureInfo.InvariantCulture);
			if (Process.TryGetProcessById (pid, out var child)) {
				using (child) {
					child.Kill (entireProcessTree: true);
					Assert.IsTrue (child.WaitForExit (5000), "The fixture child did not exit after cleanup.");
				}
			}
		}
		FileSystemUtils.DeleteDirectoryWithRetry (directory);
	}

	string Script (string contents)
	{
		if (OperatingSystem.IsWindows ())
			throw new PlatformNotSupportedException ("Shell fixtures require Unix.");
		string path = Path.Combine (directory, "command");
		using (var writer = new StreamWriter (path)) {
			writer.WriteLine ("#!/bin/sh");
			writer.WriteLine (contents);
		}
		File.SetUnixFileMode (path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
		return path;
	}

	[TestCase (0)]
	[TestCase (7)]
	public void ProcessCapturePreservesExitCodeAndTrailingDiagnostics (int exitCode)
	{
		string command = Script ($"printf 'stdout\\n\\nstdout-tail'; printf 'stderr\\n\\nstderr-tail' >&2; exit {exitCode}");
		var (code, stdout, stderr) = RunProcessWithExitCode (command, "", 5);
		Assert.AreEqual (exitCode, code);
		Assert.AreEqual ("stdout" + Environment.NewLine + "stdout-tail", stdout);
		Assert.AreEqual ("stderr" + Environment.NewLine + "stderr-tail", stderr);
	}

	[Test]
	public void ProcessTimeoutTerminatesOwnedTree ()
	{
		string pidFile = Path.Combine (directory, "child.pid");
		string command = Script ($"sleep 120 &\necho $! > '{pidFile}'\nwait");
		var stopwatch = Stopwatch.StartNew ();
		var (code, stdout, stderr) = RunProcessWithExitCode (command, "", 1);
		Assert.AreEqual (-1, code);
		Assert.IsNull (stdout);
		Assert.IsNull (stderr);
		Assert.Less (stopwatch.Elapsed, TimeSpan.FromSeconds (5));
		int pid = int.Parse (File.ReadAllText (pidFile), CultureInfo.InvariantCulture);
		Assert.IsFalse (Process.TryGetProcessById (pid, out var child), $"Fixture child {pid} survived the timeout.");
		child?.Dispose ();
	}

	[Test]
	public void InheritedOutputDoesNotExtendExecutionDeadline ()
	{
		string pidFile = Path.Combine (directory, "child.pid");
		string command = Script ($"sleep 20 &\necho $! > '{pidFile}'\necho stdout-tail\necho stderr-tail >&2");
		var stopwatch = Stopwatch.StartNew ();
		var (code, stdout, stderr) = RunProcessWithExitCode (command, "", 1);
		Assert.AreEqual (0, code, "The process exited successfully; only the separate EOF deadline should expire.");
		Assert.AreEqual ("stdout-tail", stdout);
		Assert.AreEqual ("stderr-tail", stderr);
		Assert.Less (stopwatch.Elapsed, TimeSpan.FromSeconds (5));
	}

	[Test]
	public void CommandStreamsDiagnosticsAndReturnsFailure ()
	{
		string command = Script ("echo stdout-line; echo stderr-line >&2; exit 7");
		using var output = new StringWriter ();
		var previous = Console.Out;
		try {
			Console.SetOut (output);
			Assert.IsFalse (RunCommand (command, ""));
		} finally {
			Console.SetOut (previous);
		}
		StringAssert.Contains ("stdout-line", output.ToString ());
		StringAssert.Contains ("stderr-line", output.ToString ());
	}

	[TestCase (0)]
	[TestCase (7)]
	public void ApkDiffPreservesRawDiagnosticsAndLogShape (int exitCode)
	{
		File.Move (Script ($"printf 'stdout\\r\\nraw-tail'; printf 'stderr\\r\\nraw-tail' >&2; exit {exitCode}"),
			Path.Combine (directory, "apkdiff"));
		string previous = Environment.GetEnvironmentVariable ("PATH");
		string logPath = Path.Combine (directory, "apkdiff.log");
		try {
			Environment.SetEnvironmentVariable ("PATH", directory + Path.PathSeparator + previous);
			var (code, stdout, stderr) = RunApkDiffCommand ("fixture", logPath);
			Assert.AreEqual (exitCode, code);
			Assert.AreEqual ("stdout\r\nraw-tail", stdout);
			Assert.AreEqual ("stderr\r\nraw-tail", stderr);
			string log = File.ReadAllText (logPath);
			StringAssert.Contains ($"apkdiff exited with code: {exitCode}", log);
			StringAssert.Contains ("\nstdOut:\n" + stdout + "\nstdErr:\n" + stderr, log);
		} finally {
			Environment.SetEnvironmentVariable ("PATH", previous);
		}
	}

	[Test]
	public void ApkDiffEofTimeoutKeepsRawDiagnosticsAndExitStatus ([Values (0, 7)] int exitCode)
	{
		string pidFile = Path.Combine (directory, "child.pid");
		string expectedOutput = "stdout\r\n\r\n" + new string ('o', 128 * 1024) + "raw-tail";
		string expectedError = "stderr\r\n\r\n" + new string ('e', 128 * 1024) + "raw-tail";
		File.Move (Script ($"printf '%s' '{expectedOutput}' &\nprintf '%s' '{expectedError}' >&2 &\nwait\n" +
			$"sleep 20 &\necho $! > '{pidFile}'\nexit {exitCode}"), Path.Combine (directory, "apkdiff"));
		string previous = Environment.GetEnvironmentVariable ("PATH");
		string logPath = Path.Combine (directory, "apkdiff.log");
		try {
			Environment.SetEnvironmentVariable ("PATH", directory + Path.PathSeparator + previous);
			var stopwatch = Stopwatch.StartNew ();
			var (code, stdout, stderr) = RunApkDiffCommand ("fixture", logPath);
			Assert.AreEqual (exitCode, code, "The output EOF deadline must not replace the completed process status.");
			Assert.AreEqual (expectedOutput, stdout);
			StringAssert.StartsWith (expectedError, stderr);
			StringAssert.Contains ("redirected output still open after 2 seconds", stderr);
			StringAssert.DoesNotContain ("apkdiff timed out after", stderr);
			Assert.Less (stopwatch.Elapsed, TimeSpan.FromSeconds (5));
			string log = File.ReadAllText (logPath);
			StringAssert.Contains ($"apkdiff exited with code: {exitCode}", log);
			StringAssert.Contains ("\nstdOut:\n" + stdout + "\nstdErr:\n" + stderr, log);
		} finally {
			Environment.SetEnvironmentVariable ("PATH", previous);
		}
	}

	[Test]
	public void ApkDiffTimeoutKeepsRawPartialDiagnosticsAndTerminatesTree ()
	{
		string pidFile = Path.Combine (directory, "child.pid");
		File.Move (Script ($"printf 'stdout\\r\\npartial'; printf 'stderr\\r\\npartial' >&2\nsleep 180 &\necho $! > '{pidFile}'\nwait"),
			Path.Combine (directory, "apkdiff"));
		string previous = Environment.GetEnvironmentVariable ("PATH");
		int timeout = TestEnvironment.IsRunningOnCI ? 120 : 30;
		try {
			Environment.SetEnvironmentVariable ("PATH", directory + Path.PathSeparator + previous);
			var stopwatch = Stopwatch.StartNew ();
			var (code, stdout, stderr) = RunApkDiffCommand ("fixture", Path.Combine (directory, "apkdiff.log"));
			Assert.AreEqual (-1, code);
			Assert.AreEqual ("stdout\r\npartial", stdout);
			StringAssert.Contains ("stderr\r\npartial", stderr);
			StringAssert.Contains ($"apkdiff timed out after {timeout} seconds", stderr);
			Assert.Less (stopwatch.Elapsed, TimeSpan.FromSeconds (timeout + 5));
			int pid = int.Parse (File.ReadAllText (pidFile), CultureInfo.InvariantCulture);
			Assert.IsFalse (Process.TryGetProcessById (pid, out var child), $"Fixture child {pid} survived the timeout.");
			child?.Dispose ();
		} finally {
			Environment.SetEnvironmentVariable ("PATH", previous);
		}
	}
}
