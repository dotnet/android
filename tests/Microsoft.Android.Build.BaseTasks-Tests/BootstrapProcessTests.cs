using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Android.Build.BaseTasks.Tests.Utilities;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NUnit.Framework;
using Xamarin.Android.Tools.BootstrapTasks;

using Task = System.Threading.Tasks.Task;

namespace Microsoft.Android.Build.BaseTasks.Tests
{
	[TestFixture]
	public class BootstrapProcessTests
	{
		string directory;
		MockBuildEngine engine;
		List<BuildErrorEventArgs> errors;
		List<BuildWarningEventArgs> warnings;
		List<BuildMessageEventArgs> messages;

		sealed class RecordingAdb : Adb
		{
			public string OutputFile { get; set; }
			public string ErrorFile { get; set; }
			public bool MergeOutput { get; set; } = true;
			public bool ThrowOnOutput { get; set; }
			public Action<ProcessStartInfo> ConfigureStartInfo { get; set; }
			public List<string> Parsed { get; } = new List<string> ();
			public List<string> ParsedErrors { get; } = new List<string> ();
			public List<int> ParserThreads { get; } = new List<int> ();
			public int OwnerThread { get; private set; }

			protected override int OutputTimeout => 1;

			public override bool Execute ()
			{
				OwnerThread = Environment.CurrentManagedThreadId;
				return base.Execute ();
			}

			protected override List<CommandInfo> GenerateCommandArguments ()
			{
				var commands = base.GenerateCommandArguments ();
				commands [0].StdoutFilePath = OutputFile;
				commands [0].StderrFilePath = ErrorFile;
				commands [0].MergeStdoutAndStderr = MergeOutput;
				return commands;
			}

			protected override void CustomizeProcessStartInfo (ProcessStartInfo startInfo)
				=> ConfigureStartInfo?.Invoke (startInfo);

			protected override void ProcessStdout (string line)
			{
				ParserThreads.Add (Environment.CurrentManagedThreadId);
				Parsed.Add (line);
				if (ThrowOnOutput)
					throw new InvalidOperationException ("Output consumer failed.");
			}

			protected override void ProcessStderr (string line)
			{
				ParserThreads.Add (Environment.CurrentManagedThreadId);
				ParsedErrors.Add (line);
			}
		}

		sealed class ThrowingLogWriter : StringWriter
		{
			public override void WriteLine (string value)
			{
				if (value.Contains ("fault log consumer"))
					throw new InvalidOperationException ("Log consumer failed.");
			}
		}

		sealed class YieldingBuildEngine : MockBuildEngine, IBuildEngine3
		{
			public int YieldCount { get; private set; }
			public int ReacquireCount { get; private set; }

			public YieldingBuildEngine (TextWriter writer) : base (writer)
			{}

			void IBuildEngine3.Yield () => YieldCount++;
			void IBuildEngine3.Reacquire () => ReacquireCount++;
		}

		[SetUp]
		public void SetUp ()
		{
			if (OperatingSystem.IsWindows ())
				Assert.Ignore ("These subprocess fixtures require a POSIX shell.");
			directory = Path.Combine (TestContext.CurrentContext.WorkDirectory, "bootstrap-process-" + Guid.NewGuid ().ToString ("N"));
			Directory.CreateDirectory (directory);
			errors = new List<BuildErrorEventArgs> ();
			warnings = new List<BuildWarningEventArgs> ();
			messages = new List<BuildMessageEventArgs> ();
			engine = new MockBuildEngine (TextWriter.Null, errors, warnings, messages);
		}

		[TearDown]
		public void TearDown ()
		{
			if (directory == null)
				return;
			StopOwnedProcesses ();
			Directory.Delete (directory, recursive: true);
		}

		[TestCase (false)]
		[TestCase (true)]
		public async Task AdbProcessTimeoutPreservesDiagnosticsAndIgnoreExitCode (bool ignoreExitCode)
		{
			var script = CreateScript ("timeout", """
				echo $$ > root.pid
				printf 'before-timeout'
				exec sleep 60
				""");
			var task = CreateAdb (script);
			task.Timeout = 1000;
			task.IgnoreExitCode = ignoreExitCode;
			var watch = Stopwatch.StartNew ();

			Assert.AreEqual (ignoreExitCode, await ExecuteAsync (task));
			Assert.Less (watch.Elapsed, TimeSpan.FromSeconds (5));
			CollectionAssert.Contains (task.Output, "before-timeout");
			Assert.IsFalse (IsRunning (ReadPid ("root.pid")));
			Assert.IsTrue (warnings.Any (e => e.Message.Contains ("failed to exit within")));
		}

		[TestCase (-1, false, 11, true)]
		[TestCase (0, false, 11, true)]
		[TestCase (1000, false, 11, true)]
		[TestCase (1000, false, 11, false)]
		[TestCase (1000, true, 11, true)]
		[TestCase (1000, false, 4096, true)]
		[TestCase (1000, true, 4096, true)]
		public async Task AdbDeadlinesPreserveUnterminatedDiagnosticsWithoutKillingInheritedServer (int timeout, bool rootHangs, int tailLength, bool mergeOutput)
		{
			string stdoutTail = "stdout-tail" + (tailLength == 11 ? "" : new string ('x', tailLength - 13) + "\u00e9");
			string stderrTail = "stderr-tail" + (tailLength == 11 ? "" : new string ('y', tailLength - 13) + "\u00e9");
			File.WriteAllText (Path.Combine (directory, "stdout-tail"), stdoutTail);
			File.WriteAllText (Path.Combine (directory, "stderr-tail"), stderrTail);
			var script = CreateScript ("inherited-output", """
				echo $$ > root.pid
				sleep 60 &
				echo $! > server.pid
				printf 'before-eof\n'
				printf 'stderr-line\n' >&2
				sleep 0.1
				cat stdout-tail
				cat stderr-tail >&2
				""" + (rootHangs ? "\nwait" : "\nexit 0"));
			var task = CreateAdb (script);
			task.Timeout = timeout;
			task.MergeOutput = mergeOutput;
			task.OutputFile = Path.Combine (directory, "timeout-output.txt");
			task.ErrorFile = Path.Combine (directory, "timeout-error.txt");
			var watch = Stopwatch.StartNew ();

			Assert.IsFalse (await ExecuteAsync (task));
			Assert.Less (watch.Elapsed, TimeSpan.FromSeconds (5));
			Assert.GreaterOrEqual (watch.Elapsed, TimeSpan.FromMilliseconds (700));
			Assert.Multiple (() => {
				CollectionAssert.Contains (task.Output, "before-eof");
				CollectionAssert.Contains (task.Output, "stderr-line");
				CollectionAssert.Contains (task.Output, stdoutTail);
				CollectionAssert.Contains (task.Output, stderrTail);
				Assert.IsTrue (messages.Any (e => e.Message == stdoutTail));
				Assert.IsTrue (messages.Any (e => e.Message == stderrTail));
				StringAssert.Contains (stdoutTail, File.ReadAllText (task.OutputFile));
				StringAssert.Contains (stderrTail, File.ReadAllText (mergeOutput ? task.OutputFile : task.ErrorFile));
				CollectionAssert.AreEquivalent (mergeOutput ? new [] { "before-eof", "stderr-line" } : new [] { "before-eof" }, task.Parsed,
					"Cancellation fragments are diagnostic text, not complete parser lines.");
				CollectionAssert.AreEqual (mergeOutput ? [] : new [] { "stderr-line" }, task.ParsedErrors);
			});
			if (!mergeOutput) {
				Assert.AreEqual ("before-eof" + Environment.NewLine + stdoutTail, File.ReadAllText (task.OutputFile));
				Assert.AreEqual ("stderr-line" + Environment.NewLine + stderrTail, File.ReadAllText (task.ErrorFile));
			}
			Assert.IsTrue (warnings.Any (e => e.Message.Contains ("output did not close")));
			Assert.AreEqual (rootHangs, warnings.Any (e => e.Message.Contains ("failed to exit within")));
			Assert.IsFalse (IsRunning (ReadPid ("root.pid")));
			Assert.IsTrue (IsRunning (ReadPid ("server.pid")), "The inherited pipe holder is not an owned ADB client.");
		}

		[Test]
		public async Task AdbCancellationStopsOnlyItsClient ()
		{
			var script = CreateScript ("shared-server", """
				echo $$ > root.pid
				sleep 60 &
				echo $! > server.pid
				printf 'ready\n'
				wait
				""");
			var task = CreateAdb (script);
			var execution = ExecuteAsync (task);
			int root = ReadPid ("root.pid");
			int server = ReadPid ("server.pid");

			task.Cancel ();

			Assert.IsFalse (await execution);
			Assert.IsFalse (IsRunning (root));
			Assert.IsTrue (IsRunning (server));
		}

		[Test]
		public async Task AdbSerializesMergedFileParserAndLoggingConsumption ()
		{
			var script = CreateScript ("both-streams", """
				i=0
				while [ "$i" -lt 400 ]; do
					printf 'out-%s\n' "$i"
					printf 'err-%s\n' "$i" >&2
					i=$((i + 1))
				done
				printf '\nlast'
				""");
			var task = CreateAdb (script);
			task.OutputFile = Path.Combine (directory, "merged.txt");

			Assert.IsTrue (await ExecuteAsync (task));
			Assert.AreEqual (802, task.Output.Length);
			CollectionAssert.AreEqual (task.Output, task.Parsed);
			CollectionAssert.AreEqual (task.Output, File.ReadAllLines (task.OutputFile));
			Assert.IsTrue (task.ParserThreads.All (id => id == task.OwnerThread));
			CollectionAssert.Contains (task.Output, "");
			CollectionAssert.Contains (task.Output, "last");
		}

		[Test]
		public void AdbConsumerFailureStopsClientAndClosesOutputFile ()
		{
			var script = CreateScript ("consumer-failure", """
				echo $$ > root.pid
				printf 'fault\n'
				exec sleep 60
				""");
			var task = CreateAdb (script);
			task.OutputFile = Path.Combine (directory, "consumer.txt");
			task.ThrowOnOutput = true;

			var error = Assert.ThrowsAsync<InvalidOperationException> (async () => await ExecuteAsync (task));
			Assert.AreEqual ("Output consumer failed.", error.Message);
			Assert.IsFalse (IsRunning (ReadPid ("root.pid")));
			using var file = File.Open (task.OutputFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
		}

		[Test]
		public async Task AdbPreservesArgumentEnvironmentAndConfiguredEncoding ()
		{
			var script = CreateScript ("arguments with spaces", """
				printf '%s|%s|%s\n' "$1" "$2" "$BOOTSTRAP_PROCESS_VALUE"
				""");
			var task = CreateAdb (script);
			task.Arguments += " \"first argument\" \"second {argument}\"";
			task.EnvironmentVariables = ["BOOTSTRAP_PROCESS_VALUE=environment value"];
			Assert.IsTrue (await ExecuteAsync (task));
			CollectionAssert.AreEqual (new [] { "first argument|second {argument}|environment value" }, task.Output);

			script = CreateScript ("encoding", "printf '\\377\\376\\101\\000\\040\\000\\102\\000'");
			task = CreateAdb (script);
			task.ConfigureStartInfo = startInfo => startInfo.StandardOutputEncoding = Encoding.Unicode;
			Assert.IsTrue (await ExecuteAsync (task));
			CollectionAssert.AreEqual (new [] { "A B" }, task.Output);
		}

		[Test]
		public async Task AdbPreservesSplitByteOrderMarksAndLineTerminators ()
		{
			foreach (var encoding in new [] {
				new UTF8Encoding (encoderShouldEmitUTF8Identifier: true),
				Encoding.Unicode, Encoding.BigEndianUnicode, Encoding.UTF32,
				new UTF32Encoding (bigEndian: true, byteOrderMark: true),
			}) {
				string payload = Path.Combine (directory, "encoded-output");
				byte [] preamble = encoding.GetPreamble ();
				File.WriteAllBytes (payload, preamble.Concat (encoding.GetBytes ("first\r\nsecond\rthird\nlast-\u00e9")).ToArray ());
				int split = preamble.Length + encoding.GetByteCount ("first\r");
				string script = CreateScript ("split-encoding", $"""
					dd if="{payload}" bs=1 count=1 2>/dev/null
					sleep 0.05
					dd if="{payload}" bs=1 skip=1 count={split - 1} 2>/dev/null
					sleep 0.05
					dd if="{payload}" bs=1 skip={split} 2>/dev/null
					""");
				var task = CreateAdb (script);
				task.ConfigureStartInfo = startInfo => startInfo.RedirectStandardError = false;

				Assert.IsTrue (await ExecuteAsync (task), encoding.WebName);
				CollectionAssert.AreEqual (new [] { "first", "second", "third", "last-\u00e9" }, task.Output, encoding.WebName);
				CollectionAssert.AreEqual (task.Output, task.Parsed, encoding.WebName);
			}
		}

		[Test]
		public async Task CheckAdbTargetRejectsAnUnfinishedProbe ()
		{
			CreateScript ("adb", """
				echo $$ > root.pid
				printf '34\n'
				exec sleep 60
				""");
			var task = new CheckAdbTarget {
				BuildEngine = engine,
				WorkingDirectory = directory,
				ToolPath = directory,
				ToolExe = "adb",
				SdkVersion = "34",
				Timeout = 1000,
			};

			Assert.IsTrue (await ExecuteAsync (task), "An unavailable target remains an optional probe.");
			CollectionAssert.Contains (task.Output, "34");
			Assert.IsFalse (task.IsValidTarget);
			Assert.IsFalse (IsRunning (ReadPid ("root.pid")));
		}

		[TestCase (true)]
		[TestCase (false)]
		public async Task EmulatorDeadlineIncludesCommandsAndRetryDelays (bool commandHangs)
		{
			CreateScript ("adb", commandHangs ? """
				echo $$ > root.pid
				exec sleep 60
				""" : "printf '0\\n'");
			var task = new WaitForAndroidEmulator {
				BuildEngine = engine,
				WorkingDirectory = directory,
				ToolPath = directory,
				ToolExe = "adb",
				Timeout = 200,
			};
			var watch = Stopwatch.StartNew ();

			Assert.IsFalse (await ExecuteAsync (task));
			Assert.Less (watch.Elapsed, TimeSpan.FromSeconds (2));
			Assert.IsNotEmpty (errors);
		}

		[Test]
		public async Task EmulatorReadinessRequiresSuccessfulPackageManagerCommand ()
		{
			CreateScript ("adb", """
				case "$*" in
					*sys.boot_completed*) printf '1\n' ;;
					*) printf 'package:com.android.shell\n'; exit 1 ;;
				esac
				""");
			var task = new WaitForAndroidEmulator {
				BuildEngine = engine,
				WorkingDirectory = directory,
				ToolPath = directory,
				ToolExe = "adb",
				Timeout = 5000,
			};

			Assert.IsFalse (await ExecuteAsync (task));
			Assert.IsTrue (errors.Any (e => e.Message.Contains ("Package Manager did not become ready")),
				string.Join ("\n", errors.Select (e => e.Message).Concat (messages.Select (e => e.Message))) +
				"\nOutput: " + string.Join ("|", task.Output));
		}

		[Test]
		public async Task ParallelCommandsObserveEmptyAndMixedResults ()
		{
			Assert.IsTrue (await ExecuteAsync (new RunParallelCmds { BuildEngine = engine, Commands = [] }));
			string failure = CreateScript ("failure", "printf 'diagnostic {value}\\n' >&2\nprintf 'failure output'\nexit 7");
			string success = CreateScript ("success", "printf 'last command'\nprintf 'successful stderr' >&2");
			var task = new RunParallelCmds {
				BuildEngine = engine,
				Commands = [
					CreateCommand ("/bin/sh", $"\"{failure}\""),
					CreateCommand (Path.Combine (directory, "missing-command"), ""),
					CreateCommand ("/bin/sh", $"\"{success}\""),
				],
			};

			Assert.IsFalse (await ExecuteAsync (task));
			Assert.AreEqual (2, errors.Count);
			Assert.IsTrue (errors.Any (e => e.Message.Contains ("code: 7") && e.Message.Contains ("diagnostic {value}")));
			Assert.IsTrue (messages.Any (e => e.Message.Contains ("failure output")));
			Assert.IsTrue (messages.Any (e => e.Message.Contains ("last command")));
			Assert.IsTrue (messages.Any (e => e.Message.Contains ("successful stderr")));
			Assert.IsTrue (messages.Any (e => e.Message == "RunParallelCmds completed 3 commands"));
		}

		[Test]
		public async Task ParallelManagedRuntimePreservesCommandArgumentBoundary ()
		{
			string script = CreateScript ("managed command with spaces", "printf '%s|%s\\n' \"$1\" \"$2\"");
			var task = new RunParallelCmds {
				BuildEngine = engine,
				ManagedRuntime = "/bin/sh",
				Commands = [CreateCommand (script, "\"first argument\" \"second argument\"")],
			};

			Assert.IsTrue (await ExecuteAsync (task));
			Assert.IsTrue (messages.Any (e => e.Message.Contains ("first argument|second argument")));
		}

		[Test]
		public async Task ParallelCancellationJoinsActiveCommandsAndDoesNotLaunchQueuedCommands ()
		{
			string script = CreateScript ("parallel-sleeper", "echo $$ > \"$1\"\nexec sleep 60");
			int limit = Environment.ProcessorCount;
			var task = new RunParallelCmds {
				BuildEngine = engine,
				Commands = Enumerable.Range (0, limit + 2).Select (i => CreateCommand ("/bin/sh", $"\"{script}\" \"{i}.pid\"")).ToArray (),
			};
			var execution = ExecuteAsync (task);
			Assert.IsTrue (SpinWait.SpinUntil (() => Directory.GetFiles (directory, "*.pid").Length == limit, TimeSpan.FromSeconds (5)));
			await Task.Delay (100);
			Assert.AreEqual (limit, Directory.GetFiles (directory, "*.pid").Length);
			var pids = Directory.GetFiles (directory, "*.pid").Select (path => int.Parse (File.ReadAllText (path))).ToArray ();

			task.Cancel ();

			Assert.IsFalse (await execution);
			Assert.IsTrue (pids.All (pid => !IsRunning (pid)));
			Assert.AreEqual (limit, Directory.GetFiles (directory, "*.pid").Length);
			Assert.IsEmpty (errors);
		}

		[Test]
		public void ParallelLogConsumerFailureJoinsActiveCommandsBeforeReacquiring ()
		{
			if (Environment.ProcessorCount < 2)
				Assert.Ignore ("This scenario needs one active command while another completes.");
			string sleeper = CreateScript ("active-command", "echo $$ > active.pid\nexec sleep 60");
			string output = CreateScript ("output-command", """
				while [ ! -s active.pid ]; do sleep 0.01; done
				printf 'fault log consumer\n'
				""");
			using var writer = new ThrowingLogWriter ();
			var buildEngine = new YieldingBuildEngine (writer);
			var task = new RunParallelCmds {
				BuildEngine = buildEngine,
				YieldDuringToolExecution = true,
				Commands = [
					CreateCommand ("/bin/sh", $"\"{sleeper}\""),
					CreateCommand ("/bin/sh", $"\"{output}\""),
				],
			};

			var error = Assert.ThrowsAsync<InvalidOperationException> (async () => await ExecuteAsync (task));

			Assert.AreEqual ("Log consumer failed.", error.Message);
			Assert.IsFalse (IsRunning (ReadPid ("active.pid")));
			Assert.AreEqual (1, buildEngine.YieldCount);
			Assert.AreEqual (1, buildEngine.ReacquireCount);
		}

		[TestCase (false, 11)]
		[TestCase (true, 11)]
		[TestCase (true, 4096)]
		public async Task ParallelOutputCancellationPreservesUnterminatedDiagnostics (bool cancelCommand, int tailLength)
		{
			string stdoutTail = "stdout-tail" + (tailLength == 11 ? "" : new string ('x', tailLength - 13) + "\u00e9");
			string stderrTail = "stderr-tail" + (tailLength == 11 ? "" : new string ('y', tailLength - 13) + "\u00e9");
			File.WriteAllText (Path.Combine (directory, "stdout-tail"), stdoutTail);
			File.WriteAllText (Path.Combine (directory, "stderr-tail"), stderrTail);
			string script = CreateScript ("parallel-inherited-output", """
				echo $$ > root.pid
				sleep 60 &
				echo $! > inherited.pid
				printf 'partial stdout\n'
				printf 'partial stderr\n' >&2
				sleep 0.1
				cat stdout-tail
				cat stderr-tail >&2
				printf 'ready' > ready
				""" + (cancelCommand ? "\nwait" : "\nexit 0"));
			var task = new RunParallelCmds {
				BuildEngine = engine,
				Commands = [CreateCommand ("/bin/sh", $"\"{script}\"")],
			};
			var watch = Stopwatch.StartNew ();
			var execution = ExecuteAsync (task, TimeSpan.FromSeconds (40));
			if (cancelCommand) {
				Assert.IsTrue (SpinWait.SpinUntil (() => File.Exists (Path.Combine (directory, "ready")), TimeSpan.FromSeconds (5)));
				await Task.Delay (100);
				task.Cancel ();
			}

			Assert.IsFalse (await execution);
			Assert.Less (watch.Elapsed, TimeSpan.FromSeconds (35));
			if (cancelCommand)
				Assert.IsEmpty (errors);
			else {
				Assert.GreaterOrEqual (watch.Elapsed, TimeSpan.FromSeconds (25));
				Assert.IsTrue (errors.Any (e => e.Message.Contains ("output did not close")));
			}
			Assert.Multiple (() => {
				Assert.IsTrue (messages.Any (e => e.Message.Contains ("partial stdout")));
				Assert.IsTrue (messages.Any (e => e.Message.Contains (stdoutTail)));
				Assert.IsTrue (errors.Any (e => e.Message.Contains ("partial stderr") && e.Message.Contains (stderrTail)) ||
					messages.Any (e => e.Message.Contains ("partial stderr") && e.Message.Contains (stderrTail)));
			});
			Assert.IsFalse (IsRunning (ReadPid ("root.pid")));
		}

		[Test]
		public void KillProcessWaitsForOnlyTheRecordedPidAndAllowsAnExitedPid ()
		{
			using var recorded = Process.Start (new ProcessStartInfo ("/bin/sleep", "60") { UseShellExecute = false })
				?? throw new InvalidOperationException ("Could not start the recorded fixture process.");
			try {
				using var other = Process.Start (new ProcessStartInfo ("/bin/sleep", "60") { UseShellExecute = false })
					?? throw new InvalidOperationException ("Could not start the unrelated fixture process.");
				try {
					var task = new KillProcess { BuildEngine = engine, ProcessId = recorded.Id };
					Assert.IsTrue (task.Execute ());
					Assert.IsTrue (recorded.HasExited);
					Assert.IsFalse (other.HasExited);
					Assert.IsTrue (task.Execute ());
					Assert.IsFalse (new KillProcess { BuildEngine = engine, ProcessId = 0 }.Execute ());
				} finally {
					StopOwnedProcess (other);
				}
			} finally {
				StopOwnedProcess (recorded);
			}
		}

		[TestCase (false)]
		[TestCase (true)]
		public void ReleaseTargetOnlyStopsRecordedEmulatorAndKeepsExplicitServerTeardown (bool ownedEmulator)
		{
			string log = Path.Combine (directory, "release.log");
			CreateScript ("adb", $"printf '%s\\n' \"$*\" >> \"{log}\"");
			using var emulator = Process.Start (new ProcessStartInfo ("/bin/sleep", "60") { UseShellExecute = false });
			Assert.IsNotNull (emulator);
			try {
				var startInfo = new ProcessStartInfo ("dotnet") {
					UseShellExecute = false,
					RedirectStandardOutput = true,
					RedirectStandardError = true,
				};
				foreach (string argument in new [] {
					"msbuild", Path.Combine (TestContext.CurrentContext.TestDirectory, "TestApks.targets"),
					"-nologo", "-t:ReleaseAndroidTarget",
					"-p:BootstrapTasksAssembly=" + Path.Combine (TestContext.CurrentContext.TestDirectory, "Xamarin.Android.Tools.BootstrapTasks.dll"),
					"-p:PrepTasksAssembly=" + Path.Combine (TestContext.CurrentContext.TestDirectory, "xa-prep-tasks.dll"),
					"-p:AdbToolPath=" + directory, "-p:AdbToolExe=adb",
					"-p:_EmuTarget=-s emulator-5599",
				})
					startInfo.ArgumentList.Add (argument);
				if (ownedEmulator)
					startInfo.ArgumentList.Add ("-p:_EmuPid=" + emulator.Id);

				var result = Process.RunAndCaptureText (startInfo, TimeSpan.FromSeconds (15));

				Assert.IsFalse (result.ExitStatus.Canceled, result.StandardOutput + result.StandardError);
				Assert.AreEqual (0, result.ExitStatus.ExitCode, result.StandardOutput + result.StandardError);
				CollectionAssert.AreEqual (ownedEmulator
					? new [] { "-s emulator-5599 emu kill", "kill-server" }
					: new [] { "kill-server" }, File.ReadAllLines (log));
				Assert.AreEqual (ownedEmulator, emulator.HasExited);
			} finally {
				StopOwnedProcess (emulator);
			}
		}

		RecordingAdb CreateAdb (string script)
			=> new RecordingAdb {
				BuildEngine = engine,
				ToolPath = "/bin",
				ToolExe = "sh",
				Arguments = $"\"{script}\"",
				WorkingDirectory = directory,
				WriteOutputAsMessage = true,
			};

		ITaskItem CreateCommand (string command, string arguments)
		{
			var item = new TaskItem (command);
			item.SetMetadata ("Command", command);
			item.SetMetadata ("Arguments", arguments);
			item.SetMetadata ("WorkingDirectory", directory);
			return item;
		}

		string CreateScript (string name, string content)
		{
			string path = Path.Combine (directory, name);
			File.WriteAllText (path, "#!/bin/sh\n" + content + "\n");
			if (!OperatingSystem.IsWindows ())
				File.SetUnixFileMode (path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
			return path;
		}

		async Task<bool> ExecuteAsync (ITask task, TimeSpan? timeout = null)
		{
			var execution = Task.Run (task.Execute);
			try {
				return await execution.WaitAsync (timeout ?? TimeSpan.FromSeconds (10));
			} finally {
				(task as ICancelableTask)?.Cancel ();
				if (!execution.IsCompleted) {
					StopOwnedProcesses ();
					await execution.WaitAsync (TimeSpan.FromSeconds (5));
				}
			}
		}

		int ReadPid (string name)
		{
			int pid = 0;
			string path = Path.Combine (directory, name);
			Assert.IsTrue (SpinWait.SpinUntil (() => File.Exists (path) && int.TryParse (File.ReadAllText (path), out pid), TimeSpan.FromSeconds (5)),
				$"Missing fixture process ID: {name}\n{string.Join ("\n", messages.Select (e => e.Message))}\n{string.Join ("\n", warnings.Select (e => e.Message))}\n{string.Join ("\n", errors.Select (e => e.Message))}");
			return pid;
		}

		static bool IsRunning (int pid)
		{
			try {
				using var process = Process.GetProcessById (pid);
				return !process.HasExited;
			} catch (ArgumentException) {
				return false;
			}
		}

		static void StopOwnedProcess (Process process)
		{
			try {
				try {
					if (!process.HasExited)
						process.Kill (entireProcessTree: true);
				} catch (InvalidOperationException) when (process.HasExited) {
				}
			} finally {
				Assert.IsTrue (process.WaitForExit (5000), $"Fixture process {process.Id} did not exit.");
			}
		}

		void StopOwnedProcesses ()
		{
			foreach (string path in Directory.GetFiles (directory, "*.pid")) {
				if (!int.TryParse (File.ReadAllText (path), out int pid))
					continue;
				try {
					using var process = Process.GetProcessById (pid);
					StopOwnedProcess (process);
				} catch (ArgumentException) {
					// The recorded fixture process has already exited.
				}
			}
		}
	}
}
