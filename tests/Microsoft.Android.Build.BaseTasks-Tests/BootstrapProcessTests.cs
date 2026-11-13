using System;
using System.Collections.Generic;
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
			public List<string> ParsedOutput { get; } = new List<string> ();
			public List<string> ParsedError { get; } = new List<string> ();

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

		[TestCase (0)]
		[TestCase (7)]
		public void ParallelCommandsPreserveCaptureAndExitCode (int exitCode)
		{
			var errors = new List<BuildErrorEventArgs> ();
			var messages = new List<BuildMessageEventArgs> ();
			var command = new TaskItem ("/bin/sh");
			command.SetMetadata ("Command", "/bin/sh");
			command.SetMetadata ("Arguments", $"""-c "i=0; while [ $i -lt 10000 ]; do echo stdout; echo stderr >&2; i=$((i + 1)); done; exit {exitCode}" """);
			var task = new RunParallelCmds {
				BuildEngine = new MockBuildEngine (TextWriter.Null, errors: errors, messages: messages),
				Commands = [command],
			};
			Assert.AreEqual (exitCode == 0, task.Execute ());
			Assert.IsTrue (messages.Any (e => e.Message.Contains ("stdout")));
			if (exitCode != 0)
				Assert.IsTrue (errors.Any (e => e.Message.Contains ("code: 7") && e.Message.Contains ("stderr")));
		}
	}
}
