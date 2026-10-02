// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using NUnit.Framework;

namespace Xamarin.Android.Tools.Tests
{
	[TestFixture]
	public class ProcessUtilsTests
	{
		static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds (10);
		static string DotNetHost => Environment.GetEnvironmentVariable ("DOTNET_HOST_PATH") ?? "dotnet";

		static ProcessStartInfo ChildStartInfo (string mode)
		{
			var runtimeConfig = Path.ChangeExtension (typeof (ProcessUtilsTests).Assembly.Location, ".runtimeconfig.json");
			var psi = ProcessUtils.CreateProcessStartInfo (
				DotNetHost, "exec", "--runtimeconfig", runtimeConfig, typeof (ProcessTestChild).Assembly.Location, mode);
			psi.Environment ["PROCESS_TEST_HOST"] = DotNetHost;
			psi.Environment ["PROCESS_TEST_RUNTIME_CONFIG"] = runtimeConfig;
			return psi;
		}

		static void KillOwnedChild (int pid)
		{
			if (pid == 0)
				return;
			try {
				using var child = Process.GetProcessById (pid);
				if (!child.HasExited) {
					child.Kill ();
					Assert.IsTrue (child.WaitForExit (5000), $"Owned test child {pid} did not stop.");
				}
			} catch (ArgumentException) {
				// The owned child has already been reaped.
			} catch (InvalidOperationException) {
				// The owned child exited between lookup and termination.
			}
		}

		static bool IsRunning (int pid)
		{
			Assert.Greater (pid, 0, "The fixture must report its owned process ID.");
			try {
				using var child = Process.GetProcessById (pid);
				return !child.HasExited;
			} catch (ArgumentException) {
				return false;
			}
		}

		static bool TryParseProcessId (string text, out int pid)
		{
			pid = 0;
			return text.EndsWith (Environment.NewLine, StringComparison.Ordinal) && int.TryParse (text.Trim (), out pid);
		}

		[Test]
		public async Task StartProcess_DrainsBothPipesConcurrently ()
		{
			using var stdout = new StringWriter ();
			using var stderr = new StringWriter ();
			using var deadline = new CancellationTokenSource (TestTimeout);
			var code = await ProcessUtils.StartProcess (ChildStartInfo ("flood"), stdout, stderr, deadline.Token).WaitAsync (TestTimeout);
			Assert.AreEqual (0, code);
			Assert.AreEqual (new string ('o', 256 * 1024), stdout.ToString ());
			Assert.AreEqual (new string ('e', 256 * 1024), stderr.ToString ());
		}

		[Test]
		public async Task StartProcess_PreservesTextExitCodeAndWriterOwnership ()
		{
			using var stdout = new CallbackWriter (_ => {});
			using var stderr = new CallbackWriter (_ => {});
			StreamReader outputReader = null, errorReader = null;
			var code = await ProcessUtils.StartProcess (ChildStartInfo ("text"), stdout, stderr, CancellationToken.None,
				onStarted: p => {
					outputReader = p.StandardOutput;
					errorReader = p.StandardError;
				}).WaitAsync (TestTimeout);
			Assert.AreEqual (7, code);
			Assert.AreEqual ("first\r\nsecond\nthird\rpartial", stdout.ToString ());
			Assert.AreEqual ("error\r\n\npartial-error", stderr.ToString ());
			Assert.IsFalse (stdout.Disposed);
			Assert.IsFalse (stderr.Disposed);
			stdout.Write ("more");
			stderr.Write ("more");
			Assert.IsNotNull (outputReader);
			Assert.IsNotNull (errorReader);
			Assert.Throws<ObjectDisposedException> (() => outputReader.Peek ());
			Assert.Throws<ObjectDisposedException> (() => errorReader.Peek ());
		}

		[Test]
		public async Task StartProcess_PreservesEncodingAndBomDetection ()
		{
			var psi = ChildStartInfo ("encoding");
			psi.StandardOutputEncoding = Encoding.ASCII;
			psi.StandardErrorEncoding = Encoding.ASCII;
			using var stdout = new StringWriter ();
			using var stderr = new StringWriter ();
			var code = await ProcessUtils.StartProcess (psi, stdout, stderr, CancellationToken.None).WaitAsync (TestTimeout);
			Assert.AreEqual (0, code);
			Assert.AreEqual ("caf\u00e9\r\npartial", stdout.ToString ());
			Assert.AreEqual ("error\u00e9\npartial", stderr.ToString ());
		}

		[Test]
		public async Task StartProcess_SynchronizesASharedWriter ()
		{
			using var writer = new ConcurrentWriter ();
			using var deadline = new CancellationTokenSource (TestTimeout);
			int pid = 0;
			try {
				var code = await ProcessUtils.StartProcess (ChildStartInfo ("flood"), writer, writer, deadline.Token,
					onStarted: p => pid = p.Id).WaitAsync (TestTimeout);
				Assert.AreEqual (0, code);
				Assert.AreEqual (1, writer.MaximumConcurrentWrites);
				Assert.AreEqual (512 * 1024, writer.ToString ().Length);
			} finally {
				KillOwnedChild (pid);
			}
		}

		[Test]
		public async Task StartProcess_PassesEnvironmentWithoutChangingTheHost ()
		{
			using var stdout = new StringWriter ();
			var before = Environment.GetEnvironmentVariable ("PROCESS_TEST_VALUE");
			var environment = new Dictionary<string, string> { ["PROCESS_TEST_VALUE"] = "value with spaces" };
			var code = await ProcessUtils.StartProcess (
				ChildStartInfo ("environment"), stdout, null, CancellationToken.None, environment).WaitAsync (TestTimeout);
			Assert.AreEqual (0, code);
			Assert.AreEqual ("value with spaces", stdout.ToString ());
			Assert.AreEqual (before, Environment.GetEnvironmentVariable ("PROCESS_TEST_VALUE"));
		}

		[Test]
		public async Task StartProcess_LeavesInputClosureToTheCallback ()
		{
			var psi = ChildStartInfo ("input");
			psi.RedirectStandardInput = true;
			using var stdout = new StringWriter ();
			using var stderr = new StringWriter ();
			StreamWriter input = null;
			var code = await ProcessUtils.StartProcess (psi, stdout, stderr, CancellationToken.None, onStarted: p => {
				input = p.StandardInput;
				input.Write ("answer");
				input.Close ();
			}).WaitAsync (TestTimeout);
			Assert.AreEqual (0, code);
			Assert.AreEqual ($"ready{Environment.NewLine}input:answer", stdout.ToString ());
			Assert.IsNotNull (input);
			Assert.Throws<ObjectDisposedException> (() => input.Write ("more"));
		}

		[Test]
		public async Task StartProcess_DrainsBeforeTheInputCallback ()
		{
			var psi = ChildStartInfo ("flood-input");
			psi.RedirectStandardInput = true;
			using var stdout = new StringWriter ();
			using var stderr = new StringWriter ();
			using var deadline = new CancellationTokenSource (TestTimeout);
			int pid = 0;
			try {
				var run = Task.Run (() => ProcessUtils.StartProcess (psi, stdout, stderr, deadline.Token, onStarted: p => {
					pid = p.Id;
					p.StandardInput.Write (new string ('i', 256 * 1024));
					p.StandardInput.Close ();
				}));
				Assert.AreEqual (0, await run.WaitAsync (TestTimeout));
				Assert.AreEqual (256 * 1024, stdout.ToString ().Length);
				Assert.AreEqual (256 * 1024, stderr.ToString ().Length);
			} finally {
				KillOwnedChild (pid);
			}
		}

		[Test]
		public void StartProcess_PreCanceledTokenDoesNotStartAChild ()
		{
			using var cancellation = new CancellationTokenSource ();
			cancellation.Cancel ();
			bool started = false;
			Assert.CatchAsync<OperationCanceledException> (async () => {
				await ProcessUtils.StartProcess (ChildStartInfo ("wait"), null, null, cancellation.Token,
					onStarted: _ => started = true).WaitAsync (TestTimeout);
			});
			Assert.IsFalse (started);
		}

		[Test]
		public void StartProcess_DeadlineStopsTheOwnedRoot ()
		{
			using var deadline = new CancellationTokenSource ();
			int pid = 0;
			try {
				Assert.CatchAsync<OperationCanceledException> (async () => {
					await ProcessUtils.StartProcess (ChildStartInfo ("wait"), null, null, deadline.Token, onStarted: p => {
						pid = p.Id;
						deadline.CancelAfter (TimeSpan.FromMilliseconds (200));
					}).WaitAsync (TestTimeout);
				});
				Assert.IsFalse (IsRunning (pid));
			} finally {
				KillOwnedChild (pid);
			}
		}

		[TestCase (false)]
		[TestCase (true)]
		public async Task StartProcess_CancellationDoesNotWaitForInheritedPipes (bool keepRootRunning)
		{
			using var cancellation = new CancellationTokenSource ();
			int rootPid = 0, descendantPid = 0;
			var descendant = new TaskCompletionSource<int> (TaskCreationOptions.RunContinuationsAsynchronously);
			var rootExited = new TaskCompletionSource<bool> (TaskCreationOptions.RunContinuationsAsynchronously);
			using var stdout = new CallbackWriter (text => {
				if (TryParseProcessId (text, out int pid)) {
					descendantPid = pid;
					descendant.TrySetResult (pid);
				}
			});
			using var stderr = new StringWriter ();
			StreamReader outputReader = null, errorReader = null;
			try {
				var run = ProcessUtils.StartProcess (
					ChildStartInfo (keepRootRunning ? "hold-output-running" : "hold-output"), stdout, stderr, cancellation.Token,
					onStarted: p => {
						rootPid = p.Id;
						outputReader = p.StandardOutput;
						errorReader = p.StandardError;
						p.Exited += (_, _) => rootExited.TrySetResult (true);
						if (p.HasExited)
							rootExited.TrySetResult (true);
					});
				await descendant.Task.WaitAsync (TestTimeout);
				if (!keepRootRunning)
					await rootExited.Task.WaitAsync (TestTimeout);
				Assert.IsFalse (run.IsCompleted, "The fixture descendant must retain the redirected pipes.");
				cancellation.Cancel ();
				Assert.CatchAsync<OperationCanceledException> (async () => await run.WaitAsync (TestTimeout));
				Assert.IsFalse (IsRunning (rootPid));
				Assert.IsTrue (IsRunning (descendantPid), "Shared or persistent descendants must not be tree-killed.");
				var length = stdout.ToString ().Length;
				await Task.Delay (50);
				Assert.AreEqual (length, stdout.ToString ().Length, "No sink writes may arrive after completion.");
				Assert.IsFalse (stdout.Disposed);
				Assert.IsNotNull (outputReader);
				Assert.IsNotNull (errorReader);
				Assert.Throws<ObjectDisposedException> (() => outputReader.Peek ());
				Assert.Throws<ObjectDisposedException> (() => errorReader.Peek ());
			} finally {
				cancellation.Cancel ();
				KillOwnedChild (rootPid);
				KillOwnedChild (descendantPid);
			}
		}

		[Test]
		public async Task StartProcess_BoundsPostExitDrainWithoutKillingDescendants ()
		{
			int rootPid = 0, descendantPid = 0;
			using var stdout = new CallbackWriter (text => TryParseProcessId (text, out descendantPid));
			using var stderr = new StringWriter ();
			var rootExited = new TaskCompletionSource<bool> (TaskCreationOptions.RunContinuationsAsynchronously);
			try {
				var run = ProcessUtils.StartProcess (ChildStartInfo ("hold-output"), stdout, stderr, CancellationToken.None, onStarted: p => {
					rootPid = p.Id;
					p.Exited += (_, _) => rootExited.TrySetResult (true);
					if (p.HasExited)
						rootExited.TrySetResult (true);
				});
				await rootExited.Task.WaitAsync (TestTimeout);
				var stopwatch = Stopwatch.StartNew ();
				var exception = Assert.ThrowsAsync<TimeoutException> (async () => await run.WaitAsync (TimeSpan.FromSeconds (40)));
				Assert.IsNotNull (exception);
				StringAssert.Contains ("redirected output", exception.Message);
				Assert.That (stopwatch.Elapsed, Is.InRange (TimeSpan.FromSeconds (25), TimeSpan.FromSeconds (35)));
				Assert.IsFalse (IsRunning (rootPid));
				Assert.IsTrue (IsRunning (descendantPid));
			} finally {
				KillOwnedChild (rootPid);
				KillOwnedChild (descendantPid);
			}
		}

		[Test]
		public void StartProcess_CallbackFailureStopsTheOwnedRoot ()
		{
			int pid = 0;
			var failure = new FormatException ("start callback failed");
			try {
				var exception = Assert.ThrowsAsync<FormatException> (async () => {
					await ProcessUtils.StartProcess (ChildStartInfo ("wait"), null, null, CancellationToken.None, onStarted: p => {
						pid = p.Id;
						throw failure;
					}).WaitAsync (TestTimeout);
				});
				Assert.AreSame (failure, exception);
				Assert.IsFalse (IsRunning (pid));
			} finally {
				KillOwnedChild (pid);
			}
		}

		[TestCase (false)]
		[TestCase (true)]
		public void StartProcess_WriterFailureStopsTheOwnedRootAndOtherDrain (bool failStderr)
		{
			int pid = 0;
			var failure = new FormatException ("writer failed");
			using var failing = new CallbackWriter (_ => throw failure);
			using var other = new StringWriter ();
			StreamReader outputReader = null, errorReader = null;
			try {
				var exception = Assert.ThrowsAsync<FormatException> (async () => {
					await ProcessUtils.StartProcess (ChildStartInfo ("flood-wait"),
						failStderr ? other : failing, failStderr ? failing : other, CancellationToken.None,
						onStarted: p => {
							pid = p.Id;
							outputReader = p.StandardOutput;
							errorReader = p.StandardError;
						}).WaitAsync (TestTimeout);
				});
				Assert.AreSame (failure, exception);
				Assert.IsFalse (IsRunning (pid));
				Assert.IsFalse (failing.Disposed);
				Assert.IsNotNull (outputReader);
				Assert.IsNotNull (errorReader);
				Assert.Throws<ObjectDisposedException> (() => outputReader.Peek ());
				Assert.Throws<ObjectDisposedException> (() => errorReader.Peek ());
			} finally {
				KillOwnedChild (pid);
			}
		}

		[Test]
		public void StartProcess_ReportsBothConsumerFailures ()
		{
			using var bothWriting = new CountdownEvent (2);
			var outputFailure = new FormatException ("stdout failed");
			var errorFailure = new IOException ("stderr failed");
			int pid = 0;
			void Fail (Exception failure)
			{
				bothWriting.Signal ();
				Assert.IsTrue (bothWriting.Wait (TestTimeout), "Both streams must reach their consumer.");
				throw failure;
			}
			using var stdout = new CallbackWriter (_ => Fail (outputFailure));
			using var stderr = new CallbackWriter (_ => Fail (errorFailure));
			try {
				var exception = Assert.ThrowsAsync<AggregateException> (async () => {
					await ProcessUtils.StartProcess (ChildStartInfo ("flood-wait"), stdout, stderr, CancellationToken.None,
						onStarted: p => pid = p.Id).WaitAsync (TestTimeout);
				});
				Assert.IsNotNull (exception);
				CollectionAssert.AreEquivalent (new Exception [] { outputFailure, errorFailure }, exception.InnerExceptions);
				Assert.IsFalse (IsRunning (pid));
			} finally {
				KillOwnedChild (pid);
			}
		}

		[TestCase (false)]
		[TestCase (true)]
		public void StartProcess_WriterFailureWinsOverConcurrentCancellation (bool failStderr)
		{
			using var cancellation = new CancellationTokenSource ();
			int pid = 0;
			var failure = new FormatException ("writer failed during cancellation");
			using var failing = new CallbackWriter (_ => {
				cancellation.Cancel ();
				throw failure;
			});
			using var other = new StringWriter ();
			try {
				var exception = Assert.ThrowsAsync<FormatException> (async () => {
					await ProcessUtils.StartProcess (ChildStartInfo ("flood-wait"),
						failStderr ? other : failing, failStderr ? failing : other, cancellation.Token,
						onStarted: p => pid = p.Id).WaitAsync (TestTimeout);
				});
				Assert.AreSame (failure, exception);
				Assert.IsFalse (IsRunning (pid));
			} finally {
				KillOwnedChild (pid);
			}
		}

		[Test]
		public void StartProcess_CallbackFailureWinsOverConcurrentCancellation ()
		{
			using var cancellation = new CancellationTokenSource ();
			int pid = 0;
			var failure = new FormatException ("callback failed during cancellation");
			try {
				var exception = Assert.ThrowsAsync<FormatException> (async () => {
					await ProcessUtils.StartProcess (ChildStartInfo ("wait"), null, null, cancellation.Token, onStarted: p => {
						pid = p.Id;
						cancellation.Cancel ();
						throw failure;
					}).WaitAsync (TestTimeout);
				});
				Assert.AreSame (failure, exception);
				Assert.IsFalse (IsRunning (pid));
			} finally {
				KillOwnedChild (pid);
			}
		}

		[TestCase (false)]
		[TestCase (true)]
		public void StartProcess_RedirectionRequiresAWriterBeforeStarting (bool redirectStderr)
		{
			var psi = ChildStartInfo ("wait");
			psi.RedirectStandardOutput = !redirectStderr;
			psi.RedirectStandardError = redirectStderr;
			bool started = false;
			int pid = 0;
			try {
				Assert.ThrowsAsync<ArgumentException> (async () => {
					await ProcessUtils.StartProcess (psi, null, null, CancellationToken.None, onStarted: p => {
						started = true;
						pid = p.Id;
					}).WaitAsync (TestTimeout);
				});
				Assert.IsFalse (started);
			} finally {
				KillOwnedChild (pid);
			}
		}

		[TestCase (true)]
		[TestCase (false)]
		public void Exec_PreservesLineEventsAndStderrSelection (bool includeStderr)
		{
			var lines = new List<string> ();
			object sender = null;
			ProcessUtils.Exec (ChildStartInfo ("text"), (s, e) => {
				sender = s;
				lines.Add (e.Data);
			}, includeStderr);
			Assert.That (sender, Is.InstanceOf<Process> ());
			Assert.That (lines, Does.Contain ("first").And.Contain ("second").And.Contain ("third").And.Contain ("partial"));
			if (includeStderr) {
				Assert.That (lines, Does.Contain ("error").And.Contain ("").And.Contain ("partial-error"));
				Assert.AreEqual (2, lines.FindAll (line => line == null).Count);
			} else {
				Assert.AreEqual (5, lines.Count);
				Assert.AreEqual (1, lines.FindAll (line => line == null).Count);
			}
		}

		[Test]
		public void Exec_CallbackFailureStopsTheOwnedRoot ()
		{
			int rootPid = 0, descendantPid = 0;
			var failure = new FormatException ("line callback failed");
			try {
				var exception = Assert.ThrowsAsync<FormatException> (async () => {
					await Task.Run (() => ProcessUtils.Exec (ChildStartInfo ("hold-output-running"), (s, e) => {
						rootPid = ((Process) s).Id;
						if (int.TryParse (e.Data, out int pid))
							descendantPid = pid;
						throw failure;
					})).WaitAsync (TestTimeout);
				});
				Assert.AreSame (failure, exception);
				Assert.IsFalse (IsRunning (rootPid));
				Assert.IsTrue (IsRunning (descendantPid));
			} finally {
				KillOwnedChild (rootPid);
				KillOwnedChild (descendantPid);
			}
		}

		[Test]
		public void Exec_EofCallbackFailureIsPropagated ()
		{
			var failure = new FormatException ("EOF callback failed");
			var exception = Assert.ThrowsAsync<FormatException> (async () => {
				await Task.Run (() => ProcessUtils.Exec (ChildStartInfo ("text"), (_, e) => {
					if (e.Data == null)
						throw failure;
				})).WaitAsync (TestTimeout);
			});
			Assert.AreSame (failure, exception);
		}

		[Test]
		public async Task ExecuteTool_SuccessfulParserCompletesTheReturnedTask ()
		{
			var exe = OS.IsWindows ? "whoami.exe" : "/usr/bin/true";
			bool parsed = false;
			var result = await ProcessUtils.ExecuteToolAsync (exe, output => {
				parsed = true;
				Assert.IsNotNull (output);
				return 42;
			}, CancellationToken.None).WaitAsync (TestTimeout);
			Assert.IsTrue (parsed);
			Assert.AreEqual (42, result);
		}

		[Test]
		public void ExecuteTool_ResultFailureFaultsReturnedTask ()
		{
			var exe = OS.IsWindows ? "whoami.exe" : "/usr/bin/true";
			var failure = new FormatException ("invalid result");
			var exception = Assert.ThrowsAsync<FormatException> (async () => {
				await ProcessUtils.ExecuteToolAsync<int> (exe, _ => throw failure, CancellationToken.None).WaitAsync (TestTimeout);
			});
			Assert.AreSame (failure, exception);
		}

		[Test]
		public void ExecuteTool_RequiresAParserBeforeStarting ()
		{
			bool started = false;
			Assert.ThrowsAsync<ArgumentNullException> (async () => {
				await ProcessUtils.ExecuteToolAsync<int> ("unused", null, CancellationToken.None,
					onStarted: _ => started = true).WaitAsync (TestTimeout);
			});
			Assert.IsFalse (started);
		}

		[Test]
		public void ExecuteTool_CancellationPreservesTheTokenAndStopsTheRoot ()
		{
			using var cancellation = new CancellationTokenSource ();
			var exe = OS.IsWindows ? Environment.GetEnvironmentVariable ("ComSpec") ?? "cmd.exe" : "/bin/sh";
			int pid = 0;
			bool parsed = false;
			try {
				var run = ProcessUtils.ExecuteToolAsync (exe, _ => {
					parsed = true;
					return 0;
				}, cancellation.Token, onStarted: p => {
					pid = p.Id;
					cancellation.CancelAfter (TimeSpan.FromMilliseconds (200));
				});
				var exception = Assert.CatchAsync<OperationCanceledException> (async () => await run.WaitAsync (TestTimeout));
				Assert.IsNotNull (exception);
				Assert.AreEqual (cancellation.Token, exception.CancellationToken);
				Assert.IsTrue (run.IsCanceled);
				Assert.IsFalse (parsed);
				Assert.IsFalse (IsRunning (pid));
			} finally {
				KillOwnedChild (pid);
			}
		}

		[Test]
		public void ExecuteTool_NonzeroExitPrefersStderr ()
		{
			var exe = OS.IsWindows ? Environment.GetEnvironmentVariable ("ComSpec") ?? "cmd.exe" : "/bin/sh";
			var exception = Assert.ThrowsAsync<InvalidOperationException> (async () => {
				await ProcessUtils.ExecuteToolAsync<int> (exe, _ => 0, CancellationToken.None, onStarted: p => {
					p.StandardInput.WriteLine (OS.IsWindows ? "echo diagnostic 1>&2" : "printf diagnostic >&2");
					p.StandardInput.WriteLine ("exit 7");
					p.StandardInput.Close ();
				}).WaitAsync (TestTimeout);
			});
			Assert.IsNotNull (exception);
			StringAssert.StartsWith ("7 : diagnostic", exception.Message);
		}

		[Test]
		public void ExecuteTool_NonzeroExitFallsBackToStdout ()
		{
			var exe = OS.IsWindows ? Environment.GetEnvironmentVariable ("ComSpec") ?? "cmd.exe" : "/bin/sh";
			var exception = Assert.ThrowsAsync<InvalidOperationException> (async () => {
				await ProcessUtils.ExecuteToolAsync<int> (exe, _ => 0, CancellationToken.None, onStarted: p => {
					p.StandardInput.WriteLine (OS.IsWindows ? "echo output diagnostic" : "printf 'output diagnostic'");
					p.StandardInput.WriteLine ("exit 7");
					p.StandardInput.Close ();
				}).WaitAsync (TestTimeout);
			});
			Assert.IsNotNull (exception);
			StringAssert.StartsWith ("7 : ", exception.Message);
			StringAssert.Contains ("output diagnostic", exception.Message);
		}

		[Test]
		public void CreateProcessStartInfo_SetsFileName ()
		{
			var psi = ProcessUtils.CreateProcessStartInfo ("myapp");
			Assert.AreEqual ("myapp", psi.FileName);
		}

		[Test]
		public void CreateProcessStartInfo_SetsShellAndWindow ()
		{
			var psi = ProcessUtils.CreateProcessStartInfo ("myapp");
			Assert.IsFalse (psi.UseShellExecute, "UseShellExecute should be false");
			Assert.IsTrue (psi.CreateNoWindow, "CreateNoWindow should be true");
		}

		[Test]
		public void CreateProcessStartInfo_NoArgs ()
		{
			var psi = ProcessUtils.CreateProcessStartInfo ("myapp");
			Assert.AreEqual (0, psi.ArgumentList.Count);
		}

		[Test]
		public void CreateProcessStartInfo_SingleArg ()
		{
			var psi = ProcessUtils.CreateProcessStartInfo ("myapp", "--version");
			Assert.AreEqual (1, psi.ArgumentList.Count);
			Assert.AreEqual ("--version", psi.ArgumentList [0]);
		}

		[Test]
		public void CreateProcessStartInfo_MultipleArgs ()
		{
			var psi = ProcessUtils.CreateProcessStartInfo ("tar", "-xzf", "archive.tar.gz", "-C", "/tmp/output");
			Assert.AreEqual (4, psi.ArgumentList.Count);
			Assert.AreEqual ("-xzf", psi.ArgumentList [0]);
			Assert.AreEqual ("archive.tar.gz", psi.ArgumentList [1]);
			Assert.AreEqual ("-C", psi.ArgumentList [2]);
			Assert.AreEqual ("/tmp/output", psi.ArgumentList [3]);
		}

		[Test]
		public void CreateProcessStartInfo_ArgWithSpaces ()
		{
			var psi = ProcessUtils.CreateProcessStartInfo ("cmd", "/c", "path with spaces");
			Assert.AreEqual (2, psi.ArgumentList.Count);
			Assert.AreEqual ("path with spaces", psi.ArgumentList [1]);
		}

		[Test]
		public void IsElevated_DoesNotThrow ()
		{
			// Smoke test: just verify it returns without crashing
			bool result = ProcessUtils.IsElevated ();
			Assert.That (result, Is.TypeOf<bool> ());
		}

		[Test]
		public void JoinArguments_WindowsPathIsNotDoubleEscaped ()
		{
			// Regression test: escaping every backslash produced `C:\\dir\\file.dll`, which
			// made `adb push` fail with "failed to read all of ...: Invalid argument".
			Assert.AreEqual (@"C:\Users\me\obj\a.dll", ProcessUtils.JoinArguments (@"C:\Users\me\obj\a.dll"));
		}

		[Test]
		public void JoinArguments_PathWithSpacesIsQuotedButNotDoubleEscaped ()
		{
			Assert.AreEqual ("\"C:\\path with spaces\\a.dll\"", ProcessUtils.JoinArguments (@"C:\path with spaces\a.dll"));
		}

		[Test]
		public void JoinArguments_EmbeddedQuoteIsEscaped ()
		{
			Assert.AreEqual ("\"he said \\\"hi\\\"\"", ProcessUtils.JoinArguments ("he said \"hi\""));
		}

		[Test]
		public void JoinArguments_BackslashBeforeQuoteIsDoubled ()
		{
			Assert.AreEqual ("\"a\\\\\\\"b\"", ProcessUtils.JoinArguments ("a\\\"b"));
		}

		[Test]
		public void JoinArguments_TrailingBackslashWithSpacesIsDoubled ()
		{
			// The trailing backslash precedes the closing quote, so it must be doubled.
			Assert.AreEqual ("\"C:\\a b\\\\\"", ProcessUtils.JoinArguments (@"C:\a b\"));
		}

		[Test]
		public void JoinArguments_TrailingBackslashWithoutSpacesNeedsNoQuoting ()
		{
			Assert.AreEqual (@"C:\dir\", ProcessUtils.JoinArguments (@"C:\dir\"));
		}

		[Test]
		public void JoinArguments_EmptyArgument ()
		{
			Assert.AreEqual ("\"\"", ProcessUtils.JoinArguments (""));
		}

		[Test]
		public void JoinArguments_NullArgument ()
		{
			Assert.AreEqual ("\"\"", ProcessUtils.JoinArguments (default (string)));
		}

		[Test]
		public void JoinArguments_NoArguments ()
		{
			Assert.AreEqual ("", ProcessUtils.JoinArguments ());
		}

		[Test]
		public void JoinArguments_MultipleArgumentsAreSpaceSeparated ()
		{
			Assert.AreEqual (
				"push -z any \"C:\\a b\\x.dll\" C:\\y.dll /data/local/tmp",
				ProcessUtils.JoinArguments ("push", "-z", "any", @"C:\a b\x.dll", @"C:\y.dll", "/data/local/tmp"));
		}

		[Test]
		public void JoinArguments_RoundTripsThroughArgumentParsing ()
		{
			var args = new [] {
				@"C:\Users\me\obj\Debug\net11.0-android\a.dll",
				@"C:\path with spaces\b.dll",
				"he said \"hi\"",
				@"C:\dir\",
				@"C:\a b\",
				"plain",
			};
			var parsed = SplitCommandLine (ProcessUtils.JoinArguments (args));
			CollectionAssert.AreEqual (args, parsed);
		}

		/// <summary>
		/// Arguments that contain no whitespace or quotes are now emitted bare rather than
		/// wrapped in quotes. That is transparent to the child process (the quotes were always
		/// stripped by argument parsing), but <see cref="AdbRunner"/> passes many such arguments,
		/// so assert the shapes it uses still arrive unchanged.
		/// </summary>
		[TestCase ("devices")]
		[TestCase ("-l")]
		[TestCase ("-s")]
		[TestCase ("58230DLCR0013R")]
		[TestCase ("emulator-5554")]
		[TestCase ("tcp:5555")]
		[TestCase ("localabstract:org.example_debug")]
		[TestCase ("--remove-all")]
		[TestCase ("ro.product.cpu.abilist")]
		[TestCase ("getprop")]
		public void JoinArguments_AdbArgumentShapesRoundTrip (string argument)
		{
			CollectionAssert.AreEqual (new [] { argument }, SplitCommandLine (ProcessUtils.JoinArguments (argument)));
		}

		[Test]
		public void JoinArguments_AdbShellCommandRoundTrips ()
		{
			// `AdbRunner.RunShellCommandAsync` passes an entire shell command as one argument.
			var args = new [] { "-s", "58230DLCR0013R", "shell", "echo \"remote=$(cat /data/local/tmp/x)\"" };
			CollectionAssert.AreEqual (args, SplitCommandLine (ProcessUtils.JoinArguments (args)));
		}

		/// <summary>
		/// Whitespace detection matches the BCL's <c>PasteArguments</c>, which uses
		/// <see cref="char.IsWhiteSpace(char)"/> rather than just space and tab. Quoting an
		/// argument is always safe, so erring towards quoting keeps the two implementations
		/// in agreement.
		/// </summary>
		[TestCase ("a\rb")]
		[TestCase ("a\fb")]
		[TestCase ("a\nb")]
		[TestCase ("a\vb")]
		[TestCase ("a\u00a0b")]
		public void JoinArguments_AllWhitespaceIsQuoted (string argument)
		{
			var joined = ProcessUtils.JoinArguments (argument);
			Assert.AreEqual ($"\"{argument}\"", joined);
		}

		/// <summary>
		/// Minimal implementation of the <c>CommandLineToArgvW</c> parsing rules, used to verify
		/// that <see cref="ProcessUtils.JoinArguments"/> round-trips.
		/// </summary>
		static List<string> SplitCommandLine (string commandLine)
		{
			var results = new List<string> ();
			var current = new StringBuilder ();
			bool inQuotes = false, hasArgument = false;

			for (int i = 0; i < commandLine.Length; i++) {
				char c = commandLine [i];
				if (c == '\\') {
					int backslashes = 0;
					while (i < commandLine.Length && commandLine [i] == '\\') {
						backslashes++;
						i++;
					}
					if (i < commandLine.Length && commandLine [i] == '"') {
						current.Append ('\\', backslashes / 2);
						if (backslashes % 2 == 0) {
							inQuotes = !inQuotes;
						} else {
							current.Append ('"');
						}
						hasArgument = true;
					} else {
						current.Append ('\\', backslashes);
						i--;
					}
					continue;
				}
				if (c == '"') {
					inQuotes = !inQuotes;
					hasArgument = true;
					continue;
				}
				if (!inQuotes && (c == ' ' || c == '\t')) {
					if (hasArgument || current.Length > 0) {
						results.Add (current.ToString ());
						current.Clear ();
						hasArgument = false;
					}
					continue;
				}
				current.Append (c);
				hasArgument = true;
			}

			if (hasArgument || current.Length > 0) {
				results.Add (current.ToString ());
			}
			return results;
		}

		sealed class CallbackWriter : StringWriter
		{
			readonly Action<string> onWrite;

			public bool Disposed { get; private set; }

			public CallbackWriter (Action<string> onWrite)
			{
				this.onWrite = onWrite;
			}

			public override void Write (char[] buffer, int index, int count)
			{
				base.Write (buffer, index, count);
				onWrite (ToString ());
			}

			protected override void Dispose (bool disposing)
			{
				Disposed = true;
				base.Dispose (disposing);
			}
		}

		sealed class ConcurrentWriter : StringWriter
		{
			int activeWrites;
			int maximumConcurrentWrites;
			public int MaximumConcurrentWrites => Volatile.Read (ref maximumConcurrentWrites);

			public override void Write (char[] buffer, int index, int count)
			{
				int active = Interlocked.Increment (ref activeWrites);
				int maximum;
				do {
					maximum = Volatile.Read (ref maximumConcurrentWrites);
				} while (active > maximum && Interlocked.CompareExchange (ref maximumConcurrentWrites, active, maximum) != maximum);
				Thread.Sleep (1);
				base.Write (buffer, index, count);
				Interlocked.Decrement (ref activeWrites);
			}
		}
	}
}
