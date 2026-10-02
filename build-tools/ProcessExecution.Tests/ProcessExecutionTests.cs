using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using MonoDroid.Utils;
using NUnit.Framework;
using Xamarin.Android.BuildTools.PrepTasks;
using Xamarin.Android.Tools.BootstrapTasks;

namespace Xamarin.Android.BuildTools.Tests
{
	[TestFixture]
	public class ProcessExecutionTests
	{
		const int LineCount = 5000;
		string directory;
		string ciDirectory;
		string ciAssembly;
		string ChildAssembly => Path.Combine (TestContext.CurrentContext.TestDirectory, "ProcessTestChild.dll");
		string ChildExecutable => Path.Combine (TestContext.CurrentContext.TestDirectory, OperatingSystem.IsWindows () ? "ProcessTestChild.exe" : "ProcessTestChild");
		string HostOS => OperatingSystem.IsWindows () ? "Windows" : "Unix";

		[OneTimeSetUp]
		public void CompileCiApp ()
		{
			ciDirectory = Path.Combine (Path.GetTempPath (), "repo-ci-capture-" + Guid.NewGuid ().ToString ("N"));
			Directory.CreateDirectory (ciDirectory);
			var info = new ProcessStartInfo (Environment.GetEnvironmentVariable ("DOTNET_HOST_PATH") ?? "dotnet") {
				UseShellExecute = false,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
			};
			foreach (var arg in new [] {
				"run", "--file", Path.Combine (TestContext.CurrentContext.TestDirectory, "Sources", "ci_failures.cs"),
				"-p:TargetFramework=net10.0", "-p:OutputPath=" + ciDirectory + Path.DirectorySeparatorChar, "--",
			}) {
				info.ArgumentList.Add (arg);
			}
			using var process = new Process { StartInfo = info };
			var output = new List<string> ();
			int code = ProcessRunner.Run (process, (line, error) => output.Add (line), TimeSpan.FromMinutes (2), TimeSpan.FromSeconds (10));
			Assert.That (code, Is.EqualTo (1), string.Join ("\n", output));
			Assert.That (output, Has.Some.StartsWith ("usage: dotnet run ci_failures.cs"));
			ciAssembly = Path.Combine (ciDirectory, "ci_failures.dll");
			Assert.That (File.Exists (ciAssembly), Is.True, "The file-based CI app was not compiled.");
		}

		[OneTimeTearDown]
		public void RemoveCiApp ()
		{
			if (ciDirectory != null && Directory.Exists (ciDirectory)) {
				Directory.Delete (ciDirectory, recursive: true);
			}
		}

		[SetUp]
		public void SetUp ()
		{
			directory = Path.Combine (Path.GetTempPath (), "repo-process-tests-" + Guid.NewGuid ().ToString ("N"));
			Directory.CreateDirectory (directory);
		}

		[TearDown]
		public void TearDown ()
		{
			var marker = Path.Combine (directory, "holder.pid");
			if (File.Exists (marker)) {
				StopChild (int.Parse (File.ReadAllText (marker)));
			}
			Directory.Delete (directory, recursive: true);
		}

		[TestCase (0)]
		[TestCase (7)]
		public void CapturingBothPipesPreservesLinesExitCodeAndCallingThread (int exitCode)
		{
			var output = new List<(string Text, bool Error)> ();
			int thread = Environment.CurrentManagedThreadId;
			using var process = new Process { StartInfo = Child ("duplex", exitCode.ToString ()) };
			var result = ProcessRunner.Run (process, (line, error) => {
				Assert.That (Environment.CurrentManagedThreadId, Is.EqualTo (thread));
				output.Add ((line, error));
			}, TimeSpan.FromSeconds (10), TimeSpan.FromSeconds (1));
			Assert.That (result, Is.EqualTo (exitCode));
			Assert.That (output.Count (line => !line.Error), Is.EqualTo (LineCount + 1));
			Assert.That (output.Count (line => line.Error), Is.EqualTo (LineCount + 1));
			Assert.That (output.Last (line => !line.Error).Text, Is.EqualTo ("stdout tail"));
			Assert.That (output.Last (line => line.Error).Text, Is.EqualTo ("stderr tail"));
		}

		[Test]
		public void CallbackFailureIsPropagatedAfterReaderCompletion ()
		{
			using var process = new Process { StartInfo = Child ("duplex") };
			var error = Assert.Throws<InvalidOperationException> (() => ProcessRunner.Run (process, (line, standardError) => {
				throw new InvalidOperationException ("consumer failed");
			}, TimeSpan.FromSeconds (10), TimeSpan.FromSeconds (1)));
			Assert.That (error?.Message, Is.EqualTo ("consumer failed"));
			Assert.That (process.HasExited, Is.True);
		}

		[Test]
		public void ProcessDeadlineKillsTheChildAndRetainsPartialOutput ()
		{
			var output = new List<string> ();
			using var process = new Process { StartInfo = Child ("hang") };
			var watch = Stopwatch.StartNew ();
			Assert.Throws<TimeoutException> (() => ProcessRunner.Run (process, (line, error) => output.Add (line),
				TimeSpan.FromSeconds (1), TimeSpan.FromSeconds (1)));
			Assert.That (watch.Elapsed, Is.LessThan (TimeSpan.FromSeconds (5)));
			Assert.That (process.HasExited, Is.True);
			Assert.That (output.Single (), Does.StartWith ("pid:"));
		}

		[Test]
		public void ExitedChildWithInheritedPipesHasABoundedDrain ()
		{
			using var process = new Process { StartInfo = Child ("orphan", Path.Combine (directory, "holder.pid")) };
			var watch = Stopwatch.StartNew ();
			var error = Assert.Throws<TimeoutException> (() => ProcessRunner.Run (process, (line, standardError) => { },
				TimeSpan.FromSeconds (5), TimeSpan.FromMilliseconds (200)));
			Assert.That (error?.Message, Does.Contain ("after process exit"));
			Assert.That (watch.Elapsed, Is.LessThan (TimeSpan.FromSeconds (5)));
			Assert.That (process.HasExited, Is.True);
		}

		[TestCase (true, false)]
		[TestCase (false, true)]
		[TestCase (true, true)]
		public void GitCaptureHandlesIndependentlyRedirectedStreams (bool stdout, bool stderr)
		{
			var output = stdout ? new List<string> () : null;
			var error = stderr ? new List<string> () : null;
			var info = Child ("small");
			var task = new GitCommitInfo { WorkingDirectory = directory };
			int code = (int) Invoke (typeof (GitCommitInfo).GetMethod ("RunCommand", BindingFlags.Instance | BindingFlags.NonPublic),
				task, info.FileName, QuoteArguments (info), output, error);
			Assert.That (code, Is.Zero);
			if (output != null)
				Assert.That (output, Is.EqualTo (new [] { "stdout" }));
			if (error != null)
				Assert.That (error, Is.EqualTo (new [] { "stderr" }));
		}

		[TestCase ("hang", 1)]
		[TestCase ("orphan", 5)]
		public void GitTimeoutsAreFailuresNotSuccessfulPartialCaptures (string mode, int processTimeout)
		{
			var engine = new RecordingBuildEngine ();
			var info = mode == "orphan" ? Child (mode, Path.Combine (directory, "holder.pid")) : Child (mode);
			var task = new GitCommitInfo {
				BuildEngine = engine,
				WorkingDirectory = directory,
				GitPath = info.FileName,
				ProcessTimeout = processTimeout,
				OutputTimeout = 1,
			};
			var watch = Stopwatch.StartNew ();
			bool result = (bool) Invoke (typeof (GitCommitInfo).GetMethod ("RunGit", BindingFlags.Instance | BindingFlags.NonPublic),
				task, QuoteArguments (info), new List<string> (), new List<string> ());
			Assert.That (result, Is.False);
			Assert.That (engine.Errors, Has.Some.Contains ("exited with code -1"));
			Assert.That (engine.Warnings, Has.Count.EqualTo (1));
			Assert.That (watch.Elapsed, Is.LessThan (TimeSpan.FromSeconds (5)));
		}

		[Test]
		public void GitNonzeroExitAndStderrArePreserved ()
		{
			var engine = new RecordingBuildEngine ();
			var info = Child ("duplex", "7");
			var task = new GitCommitInfo { BuildEngine = engine, WorkingDirectory = directory, GitPath = info.FileName };
			var output = new List<string> ();
			var error = new List<string> ();
			bool result = (bool) Invoke (typeof (GitCommitInfo).GetMethod ("RunGit", BindingFlags.Instance | BindingFlags.NonPublic),
				task, QuoteArguments (info), output, error);
			Assert.That (result, Is.False);
			Assert.That (output, Has.Count.EqualTo (LineCount + 1));
			Assert.That (error, Has.Count.EqualTo (LineCount + 1));
			Assert.That (engine.Errors, Has.Some.Contains ("exited with code 7"));
			Assert.That (engine.Errors.Last (), Is.EqualTo ("stderr tail"));
		}

		[TestCase (0)]
		[TestCase (-1)]
		public void GitDisabledProcessDeadlineStillWaitsForExit (int processTimeout)
		{
			var info = Child ("delayed");
			var task = new GitCommitInfo { WorkingDirectory = directory, ProcessTimeout = processTimeout };
			var output = new List<string> ();
			var watch = Stopwatch.StartNew ();
			int code = (int) Invoke (typeof (GitCommitInfo).GetMethod ("RunCommand", BindingFlags.Instance | BindingFlags.NonPublic),
				task, info.FileName, QuoteArguments (info), output, new List<string> ());
			Assert.That (code, Is.Zero);
			Assert.That (output, Is.EqualTo (new [] { "stdout" }));
			Assert.That (watch.Elapsed, Is.GreaterThanOrEqualTo (TimeSpan.FromMilliseconds (200)));
		}

		[Test]
		public void VersionDetectionDrainsAfterTheFirstVersion ()
		{
			Assert.That (Which.GetProgramVersion (HostOS, ChildExecutable + " version"), Is.EqualTo (new Version (8, 2, 1)));
			Assert.That (Which.GetProgramVersion (HostOS, OperatingSystem.IsWindows () ? "rem no output" : "true"), Is.EqualTo (new Version ()));
			var error = Assert.Throws<InvalidOperationException> (() => Which.GetProgramVersion (HostOS, ChildExecutable + " version-failure"));
			Assert.That (error?.Message, Does.Contain ("exit code 7"));
		}

		[Test]
		public void VersionDetectionHasAnEffectiveThirtySecondDeadline ()
		{
			var watch = Stopwatch.StartNew ();
			Assert.Throws<TimeoutException> (() => Which.GetProgramVersion (HostOS, ChildExecutable + " hang " + Path.Combine (directory, "holder.pid")));
			Assert.That (watch.Elapsed, Is.GreaterThanOrEqualTo (TimeSpan.FromSeconds (29)));
			Assert.That (watch.Elapsed, Is.LessThan (TimeSpan.FromSeconds (40)));
		}

		[Test]
		[Repeat (3)]
		public void GenApiParsesBothStreamsSeriallyWithoutLosingMembers ()
		{
			var tools = InstallApiTools ();
			var contract = WriteData ("contract.dll", "members-contract");
			var implementation = WriteData ("implementation.dll", "members-implementation");
			var missing = CodeGenDiff.GenerateMissingItems (tools, contract, implementation, (level, text) => { });
			string padding = new string ('x', 128);
			Assert.That (missing, Is.EquivalentTo (new [] { $"-out-member-04999:{padding}", $"-err-member-04999:{padding}" }));
		}

		[Test]
		public void GenApiPreservesNestedTypesAndAttributeParsing ()
		{
			var missing = CodeGenDiff.GenerateMissingItems (InstallApiTools (), WriteData ("contract.dll", "syntax-contract"),
				WriteData ("implementation.dll", "syntax-implementation"), (level, text) => { });
			Assert.That (missing, Is.EqualTo (new [] { "namespace Tests", "{", "public partial class Example", "{", "-public void Missing ();", "}", "}" }));
		}

		[Test]
		public void GenApiPropagatesNonzeroExitAndParserLoggerFailure ()
		{
			var tools = InstallApiTools ();
			var contract = WriteData ("contract.dll", "failure");
			var implementation = WriteData ("implementation.dll", "");
			var exit = Assert.Throws<InvalidOperationException> (() => CodeGenDiff.GenerateMissingItems (tools, contract, implementation, (level, text) => { }));
			Assert.That (exit?.Message, Does.Contain ("exit code 7"));
			File.WriteAllText (contract, "malformed");
			int thread = Environment.CurrentManagedThreadId;
			var parser = Assert.Throws<InvalidOperationException> (() => CodeGenDiff.GenerateMissingItems (tools, contract, implementation, (level, text) => {
				Assert.That (Environment.CurrentManagedThreadId, Is.EqualTo (thread));
				throw new InvalidOperationException ("parser logger failed");
			}));
			Assert.That (parser?.Message, Is.EqualTo ("parser logger failed"));
		}

		[TestCase ("success", true)]
		[TestCase ("silent-failure", false)]
		[TestCase ("reported-failure", false)]
		public void ApiCompatHonorsExitStatusEvenWithNoIssuesReported (string scenario, bool success)
		{
			var task = ApiCompat (scenario);
			Assert.That (task.Execute (), Is.EqualTo (success));
			var engine = (RecordingBuildEngine) task.BuildEngine;
			Assert.That (engine.Errors.Count == 0, Is.EqualTo (success));
			if (scenario == "silent-failure")
				Assert.That (engine.Errors, Has.Some.Contains ("exit code 7"));
		}

		[Test]
		public void ApiCompatRetainsCrashRetriesAndMissingLines ()
		{
			var crash = ApiCompat ("crash");
			Assert.That (crash.Execute (), Is.False);
			Assert.That (File.ReadAllLines (Path.Combine (crash.ApiCompatibilityPath, "reference", "net10.0", "Mono.Android.dll.attempts")), Has.Length.EqualTo (3));
			var task = ApiCompat ("breakages");
			File.WriteAllText (Path.Combine (task.ApiCompatibilityPath, "acceptable-breakages-vReference-net10.0.txt"), "Total issues: 1\n");
			task.LinesToAdd = Path.Combine (directory, "missing.txt");
			Assert.That (task.Execute (), Is.False);
			Assert.That (File.ReadAllLines (task.LinesToAdd), Has.Length.EqualTo (LineCount * 2));
		}

		[TestCase (0)]
		[TestCase (7)]
		public void CiCapturePreservesExactTextAndExitStatus (int exitCode)
		{
			var capture = CiCapture ("Run", 0, "duplex", exitCode.ToString ());
			Assert.That (capture.Code, Is.Zero, capture.Error);
			using var result = JsonDocument.Parse (capture.Output);
			Assert.That (result.RootElement.GetProperty ("Code").GetInt32 (), Is.EqualTo (exitCode));
			Assert.That (result.RootElement.GetProperty ("Stdout").GetString (), Does.EndWith ("\nstdout tail"));
			Assert.That (result.RootElement.GetProperty ("Stderr").GetString (), Does.EndWith ("\nstderr tail"));
			Assert.That (result.RootElement.GetProperty ("Stdout").GetString ()?.Count (c => c == '\n'), Is.EqualTo (LineCount));
			Assert.That (result.RootElement.GetProperty ("Stderr").GetString ()?.Count (c => c == '\n'), Is.EqualTo (LineCount));
		}

		[Test]
		public void CiCaptureSupportsSixConcurrentCommands ()
		{
			var captures = new (int Code, string Output, string Error) [6];
			Parallel.For (0, captures.Length, i => captures [i] = CiCapture ("Run", 0, "duplex", "0"));
			foreach (var capture in captures) {
				Assert.That (capture.Code, Is.Zero, capture.Error);
				using var result = JsonDocument.Parse (capture.Output);
				Assert.That (result.RootElement.GetProperty ("Stdout").GetString ()?.Count (c => c == '\n'), Is.EqualTo (LineCount));
				Assert.That (result.RootElement.GetProperty ("Stderr").GetString ()?.Count (c => c == '\n'), Is.EqualTo (LineCount));
			}
		}

		[Test]
		public void CiCaptureDoesNotNormalizeText ()
		{
			var capture = CiCapture ("Run", 0, "exact");
			Assert.That (capture.Code, Is.Zero, capture.Error);
			using var result = JsonDocument.Parse (capture.Output);
			Assert.That (result.RootElement.GetProperty ("Stdout").GetString (), Is.EqualTo (" leading\r\n\ntrailing\u00e9\0"));
			Assert.That (result.RootElement.GetProperty ("Stderr").GetString (), Is.EqualTo (" error\t\r\nlast"));
		}

		[TestCase ("hang")]
		[TestCase ("orphan")]
		public void CiDeadlineCoversBothExitAndEof (string mode)
		{
			var watch = Stopwatch.StartNew ();
			var capture = CiCapture ("CaptureCommand", 1000, mode, Path.Combine (directory, "holder.pid"));
			Assert.That (capture.Code, Is.EqualTo (70));
			Assert.That (capture.Error, Does.Contain ("TimeoutException"));
			Assert.That (watch.Elapsed, Is.LessThan (TimeSpan.FromSeconds (5)));
		}

		[Test]
		public void GeneratorStreamingWorksOnModernDotNetAndPreservesErrors ()
		{
			var lines = ProcessRocks.ReadStandardOutput (Child ("duplex", "0"), false).ToArray ();
			Assert.That (lines, Has.Length.EqualTo (LineCount + 1));
			Assert.That (lines.Last (), Is.EqualTo ("stdout tail"));
			var error = Assert.Throws<CommandFailedException> (() => ProcessRocks.ReadStandardOutput (Child ("duplex", "7"), false).ToArray ());
			Assert.That (error?.ExitCode, Is.EqualTo (7));
			Assert.That (error?.ErrorLog, Does.EndWith ("stderr tail" + Environment.NewLine));
		}

		[Test]
		public void DisposingGeneratorEnumerationTerminatesTheChild ()
		{
			using var iterator = ProcessRocks.ReadStandardOutput (Child ("stream"), false).GetEnumerator ();
			Assert.That (iterator.MoveNext (), Is.True);
			int pid = int.Parse (iterator.Current.Substring ("pid:".Length));
			using var child = Process.GetProcessById (pid);
			iterator.Dispose ();
			Assert.That (child.WaitForExit (5000), Is.True);
		}

		ProcessStartInfo Child (params string [] args)
		{
			var info = new ProcessStartInfo (Environment.GetEnvironmentVariable ("DOTNET_HOST_PATH") ?? "dotnet") {
				UseShellExecute = false,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
			};
			info.ArgumentList.Add (ChildAssembly);
			foreach (var arg in args)
				info.ArgumentList.Add (arg);
			return info;
		}

		(int Code, string Output, string Error) CiCapture (string method, int timeout, params string [] args)
		{
			var cli = new [] { "invoke-ci", ciAssembly, method, timeout.ToString () }.Concat (args).ToArray ();
			using var process = new Process { StartInfo = Child (cli) };
			var output = new List<string> ();
			var error = new List<string> ();
			int code = ProcessRunner.Run (process, (line, standardError) => (standardError ? error : output).Add (line),
				TimeSpan.FromSeconds (10), TimeSpan.FromSeconds (1));
			return (code, string.Join ("\n", output), string.Join ("\n", error));
		}

		string InstallApiTools ()
		{
			var tools = Path.Combine (directory, "netcoreapp3.1");
			Directory.CreateDirectory (tools);
			foreach (var name in new [] { "Microsoft.DotNet.ApiCompat", "Microsoft.DotNet.GenAPI" }) {
				File.Copy (ChildAssembly, Path.Combine (tools, name + ".dll"), overwrite: true);
				File.Copy (Path.ChangeExtension (ChildAssembly, ".runtimeconfig.json"), Path.Combine (tools, name + ".runtimeconfig.json"), overwrite: true);
			}
			var framework = Path.Combine (directory, "net472");
			Directory.CreateDirectory (framework);
			return framework;
		}

		CheckApiCompatibility ApiCompat (string scenario)
		{
			var tools = InstallApiTools ();
			var compatibility = Path.Combine (directory, scenario, "compatibility");
			var reference = Path.Combine (compatibility, "reference", "net10.0");
			var implementation = Path.Combine (directory, scenario, "implementation");
			Directory.CreateDirectory (reference);
			Directory.CreateDirectory (implementation);
			File.WriteAllText (Path.Combine (reference, "Mono.Android.dll"), scenario);
			File.WriteAllText (Path.Combine (implementation, "Mono.Android.dll"), "");
			return new CheckApiCompatibility {
				BuildEngine = new RecordingBuildEngine (),
				ApiCompatPath = tools,
				CodeGenPath = tools,
				ApiLevel = "v17.0",
				LastStableApiLevel = "v17.0",
				TargetImplementationPath = implementation,
				ApiCompatibilityPath = compatibility,
				TargetFramework = "net10.0",
			};
		}

		string WriteData (string name, string content)
		{
			var path = Path.Combine (directory, name);
			File.WriteAllText (path, content);
			return path;
		}

		static string QuoteArguments (ProcessStartInfo info)
			=> string.Join (" ", info.ArgumentList.Select (arg => "\"" + arg.Replace ("\"", "\\\"") + "\""));

		static object Invoke (MethodInfo method, object instance, params object [] args)
		{
			if (method == null)
				throw new InvalidOperationException ("Missing process method.");
			try {
				return method.Invoke (instance, args) ?? throw new InvalidOperationException ("Missing process result.");
			} catch (TargetInvocationException ex) {
				if (ex.InnerException == null)
					throw;
				ExceptionDispatchInfo.Capture (ex.InnerException).Throw ();
				throw;
			}
		}

		static void StopChild (int pid)
		{
			Process child;
			try {
				child = Process.GetProcessById (pid);
			} catch (ArgumentException) {
				return;
			}
			using (child) {
				if (!child.HasExited)
					child.Kill (entireProcessTree: true);
				Assert.That (child.WaitForExit (5000), Is.True, "Pipe holder did not exit.");
			}
		}

		sealed class RecordingBuildEngine : IBuildEngine
		{
			internal readonly List<string> Errors = new List<string> ();
			internal readonly List<string> Warnings = new List<string> ();
			public bool ContinueOnError => false;
			public int LineNumberOfTaskNode => 0;
			public int ColumnNumberOfTaskNode => 0;
			public string ProjectFileOfTaskNode => "";
			public void LogErrorEvent (BuildErrorEventArgs e) => Errors.Add (e.Message);
			public void LogWarningEvent (BuildWarningEventArgs e) => Warnings.Add (e.Message);
			public void LogMessageEvent (BuildMessageEventArgs e) { }
			public void LogCustomEvent (CustomBuildEventArgs e) { }
			public bool BuildProjectFile (string projectFileName, string [] targetNames, IDictionary globalProperties, IDictionary targetOutputs)
				=> throw new NotSupportedException ();
		}
	}
}
