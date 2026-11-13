using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.Android.Build.BaseTasks.Tests.Utilities;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NUnit.Framework;
using Xamarin.Android.Tools.BootstrapTasks;

namespace Microsoft.Android.Build.BaseTasks.Tests
{
	[TestFixture]
	public class BootstrapProcessTests
	{
		class RecordingAdb : Adb
		{
			public bool MergeOutput { get; set; }
			public string OutputFile { get; set; }
			public int OutputTimeoutSeconds { get; set; } = 30;
			public List<string> ParsedOutput { get; } = new List<string> ();
			public List<string> ParsedError { get; } = new List<string> ();

			protected override int OutputTimeout => OutputTimeoutSeconds;

			protected override List<CommandInfo> GenerateCommandArguments () => [
				new CommandInfo {
					ArgumentsString = Arguments,
					MergeStdoutAndStderr = MergeOutput,
					StdoutFilePath = OutputFile,
					StderrFilePath = OutputFile + ".stderr",
				},
			];

			protected override void ProcessStdout (string line) => ParsedOutput.Add (line);
			protected override void ProcessStderr (string line) => ParsedError.Add (line);
		}

		[SetUp]
		public void SetUp ()
		{
			if (OperatingSystem.IsWindows ())
				Assert.Ignore ("These command fixtures require a POSIX shell.");
		}

		[TestCase (false)]
		[TestCase (true)]
		public void AdbPreservesLineRoutingAndFiles (bool merge)
		{
			string path = Path.GetTempFileName ();
			var task = new RecordingAdb {
				BuildEngine = new MockBuildEngine (TextWriter.Null),
				ToolPath = "/bin", ToolExe = "sh",
				Arguments = """-c "printf 'stdout\n\nlast'; printf 'stderr\n' >&2" """,
				MergeOutput = merge, OutputFile = path,
			};
			try {
				Assert.IsTrue (task.Execute ());
				Assert.AreEqual (4, task.Output.Length);
				if (merge) {
					CollectionAssert.AreEqual (task.Output, task.ParsedOutput);
					CollectionAssert.AreEqual (task.Output, File.ReadAllLines (path));
					Assert.IsEmpty (task.ParsedError);
				} else {
					CollectionAssert.AreEqual (new [] { "stdout", "", "last" }, task.ParsedOutput);
					CollectionAssert.AreEqual (new [] { "stderr" }, task.ParsedError);
					CollectionAssert.AreEqual (task.ParsedOutput, File.ReadAllLines (path));
					CollectionAssert.AreEqual (task.ParsedError, File.ReadAllLines (path + ".stderr"));
				}
			} finally {
				File.Delete (path);
				File.Delete (path + ".stderr");
			}
		}

		[TestCase (false)]
		[TestCase (true)]
		public void AdbPreservesIgnoredExitCodes (bool ignore)
		{
			var task = new Adb {
				BuildEngine = new MockBuildEngine (TextWriter.Null),
				ToolPath = "/bin", ToolExe = "sh",
				Arguments = """-c "printf diagnostic; exit 7" """,
				IgnoreExitCode = ignore,
			};
			Assert.AreEqual (ignore, task.Execute ());
			CollectionAssert.AreEqual (new [] { "diagnostic" }, task.Output);
		}

		[Test]
		public void AdbPreservesProcessTimeout ()
		{
			var task = new Adb {
				BuildEngine = new MockBuildEngine (TextWriter.Null),
				ToolPath = "/bin", ToolExe = "sh",
				Arguments = """-c "exec sleep 5" """,
				Timeout = 100,
			};
			Assert.IsFalse (task.Execute ());
		}

		[Test]
		public void AdbBoundsOutputDrainWhenExitedProcessLeavesInheritedPipesOpen ()
		{
			string directory = Path.Combine (TestContext.CurrentContext.WorkDirectory, "bootstrap-output-timeout-" + Guid.NewGuid ().ToString ("N"));
			Directory.CreateDirectory (directory);
			string pidFile = Path.Combine (directory, "pipe-holder.pid");
			var task = new RecordingAdb {
				BuildEngine = new MockBuildEngine (TextWriter.Null),
				ToolPath = "/bin", ToolExe = "sh",
				Arguments = $"""-c "sleep 30 & echo $! > '{pidFile}'; printf 'stdout-before-deadline\n'; printf 'stderr-before-deadline\n' >&2; exit 0" """,
				MergeOutput = false,
				OutputFile = Path.Combine (directory, "stdout.txt"),
				OutputTimeoutSeconds = 1,
			};
			var watch = Stopwatch.StartNew ();
			try {
				Assert.IsTrue (task.Execute ());
				Assert.GreaterOrEqual (watch.Elapsed, TimeSpan.FromMilliseconds (700), "Output draining must wait for the configured deadline while the descendant holds the pipes open.");
				Assert.Less (watch.Elapsed, TimeSpan.FromSeconds (5), "Output draining must stop at its deadline after the direct process exits.");
				Assert.IsTrue (File.Exists (pidFile), "The inherited-pipe fixture must start.");
				int pid = int.Parse (File.ReadAllText (pidFile), CultureInfo.InvariantCulture);
				using (var pipeHolder = Process.GetProcessById (pid))
					Assert.IsFalse (pipeHolder.HasExited, "The descendant must still hold the redirected pipes when output draining times out.");
				CollectionAssert.AreEquivalent (new [] { "stdout-before-deadline", "stderr-before-deadline" }, task.Output);
				CollectionAssert.AreEqual (new [] { "stdout-before-deadline" }, task.ParsedOutput);
				CollectionAssert.AreEqual (new [] { "stderr-before-deadline" }, task.ParsedError);
				CollectionAssert.AreEqual (task.ParsedOutput, File.ReadAllLines (task.OutputFile));
				CollectionAssert.AreEqual (task.ParsedError, File.ReadAllLines (task.OutputFile + ".stderr"));
			} finally {
				if (File.Exists (pidFile)) {
					int pid = int.Parse (File.ReadAllText (pidFile), CultureInfo.InvariantCulture);
					foreach (var process in Process.GetProcesses ()) {
						using (process) {
							if (process.Id == pid && !process.HasExited) {
								process.Kill ();
								Assert.IsTrue (process.WaitForExit (5000), "The test-owned pipe holder must be cleaned up.");
								break;
							}
						}
					}
				}
				Directory.Delete (directory, recursive: true);
			}
		}

		[TestCase (0)]
		[TestCase (7)]
		public void ParallelCommandsPreserveCaptureAndExitCode (int exitCode)
		{
			const int lineCount = 10000;
			var errors = new List<BuildErrorEventArgs> ();
			var messages = new List<BuildMessageEventArgs> ();
			var command = new TaskItem ("/bin/sh");
			command.SetMetadata ("Command", "/bin/sh");
			command.SetMetadata ("Arguments", $"""-c "i=0; while [ $i -lt {lineCount} ]; do echo stdout-$i; echo stderr-$i >&2; i=$((i + 1)); done; exit {exitCode}" """);
			var task = new RunParallelCmds {
				BuildEngine = new MockBuildEngine (TextWriter.Null, errors: errors, messages: messages),
				Commands = [command],
			};
			Assert.AreEqual (exitCode == 0, task.Execute ());
			string capturedOutput = messages.Single (e => e.Message.StartsWith ("Output: " + Environment.NewLine, StringComparison.Ordinal)).Message
				.Substring (("Output: " + Environment.NewLine).Length);
			CollectionAssert.AreEqual (
				Enumerable.Range (0, lineCount).Select (i => "stdout-" + i),
				capturedOutput.Split (new [] { Environment.NewLine }, StringSplitOptions.None));
			if (exitCode != 0) {
				string errorPrefix = "Error output: " + Environment.NewLine;
				string capturedError = errors.Single (e => e.Message.Contains (errorPrefix)).Message;
				capturedError = capturedError.Substring (capturedError.IndexOf (errorPrefix, StringComparison.Ordinal) + errorPrefix.Length);
				CollectionAssert.AreEqual (
					Enumerable.Range (0, lineCount).Select (i => "stderr-" + i),
					capturedError.Split (new [] { Environment.NewLine }, StringSplitOptions.None));
			}
		}
	}
}
