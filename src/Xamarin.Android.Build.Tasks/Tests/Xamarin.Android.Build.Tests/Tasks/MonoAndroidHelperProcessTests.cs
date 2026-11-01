using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NUnit.Framework;
using Xamarin.Android.Tasks;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Build.Tests.Tasks
{
	[TestFixture]
	public class MonoAndroidHelperProcessTests
	{
		static (string Command, string Arguments) Shell (string windows, string unix)
		{
			return OS.IsWindows ? ("cmd.exe", $"/d /c \"{windows}\"") : ("/bin/sh", $"-c \"{unix}\"");
		}

		static TaskLoggingHelper CreateLog (List<BuildErrorEventArgs>? errors = null)
		{
			return new TaskLoggingHelper (new MockBuildEngine (TextWriter.Null, errors: errors), nameof (MonoAndroidHelperProcessTests));
		}

		[TestCase (0, true)]
		[TestCase (7, true)]
		[TestCase (7, false)]
		public void RunProcessDrainsOutputAndPreservesDiagnostics (int exitCode, bool logWarningOnFailure)
		{
			var shell = Shell (
				$"for /L %i in (1,1,100) do @(echo output-%i&echo error-%i>&2) & exit /b {exitCode}",
				$"i=1; while [ $i -le 100 ]; do echo output-$i; echo error-$i >&2; i=$((i+1)); done; exit {exitCode}");
			var output = new List<string> ();
			var error = new List<string> ();
			var errors = new List<BuildErrorEventArgs> ();
			int cancellations = 0;

			int result = MonoAndroidHelper.RunProcess (shell.Command, shell.Arguments, CreateLog (errors),
				onOutput: (sender, e) => {
					Thread.Sleep (1);
					output.Add (e.Data);
				},
				onError: (sender, e) => {
					Thread.Sleep (1);
					error.Add (e.Data);
				},
				cancelTask: () => cancellations++,
				logWarningOnFailure: logWarningOnFailure);

			Assert.AreEqual (exitCode, result);
			CollectionAssert.AreEqual (Enumerable.Range (1, 100).Select (i => $"output-{i}"), output);
			CollectionAssert.AreEqual (Enumerable.Range (1, 100).Select (i => $"error-{i}"), error);
			Assert.AreEqual (exitCode == 0 ? 0 : 1, cancellations);
			Assert.AreEqual (exitCode != 0 && logWarningOnFailure ? 1 : 0, errors.Count);
			if (errors.Count != 0) {
				Assert.AreEqual ("XA0142", errors [0].Code);
				StringAssert.Contains ("stdout | output-100", errors [0].Message);
				StringAssert.Contains ("stderr | error-100", errors [0].Message);
			}
		}

		[Test]
		[Timeout (30000)]
		public void RunProcessCancellationDrainsOutput ()
		{
			var shell = Shell (
				"echo ready&echo error>&2&for /L %i in (1,0,2) do @rem",
				"echo ready; echo error >&2; while :; do :; done");
			using var cancellation = new CancellationTokenSource ();
			var output = new List<string> ();
			var error = new List<string> ();
			int cancellations = 0;
			int received = 0;

			void CancelAfterBothStreams ()
			{
				if (Interlocked.Increment (ref received) == 2)
					cancellation.Cancel ();
				Thread.Sleep (50);
			}

			int result = MonoAndroidHelper.RunProcess (shell.Command, shell.Arguments, CreateLog (),
				onOutput: (sender, e) => {
					output.Add (e.Data);
					CancelAfterBothStreams ();
				},
				onError: (sender, e) => {
					error.Add (e.Data);
					CancelAfterBothStreams ();
				},
				cancellationToken: cancellation.Token,
				cancelTask: () => cancellations++,
				logWarningOnFailure: false);

			Assert.AreNotEqual (0, result);
			CollectionAssert.AreEqual (new [] { "ready" }, output);
			CollectionAssert.AreEqual (new [] { "error" }, error);
			Assert.AreEqual (1, cancellations);
		}

		[Test]
		[Timeout (30000)]
		public void RunProcessAlreadyCancelledToken ()
		{
			var shell = Shell ("for /L %i in (1,0,2) do @rem", "while :; do :; done");
			using var cancellation = new CancellationTokenSource ();
			cancellation.Cancel ();

			Assert.AreNotEqual (0, MonoAndroidHelper.RunProcess (shell.Command, shell.Arguments, CreateLog (),
				cancellationToken: cancellation.Token, logWarningOnFailure: false));
		}

		[Test]
		public void RunProcessStartFailure ()
		{
			using var cancellation = new CancellationTokenSource ();
			Assert.Throws<Win32Exception> (() => MonoAndroidHelper.RunProcess (
				Path.Combine (Path.GetTempPath (), Guid.NewGuid ().ToString ("N"), "missing"), "", CreateLog (),
				cancellationToken: cancellation.Token));
			Assert.DoesNotThrow (() => cancellation.Cancel ());
		}

		[TestCase (0, false)]
		[TestCase (7, false)]
		[TestCase (7, true)]
		public void RunProcessReleasesCancellationRegistration (int exitCode, bool throwOnFailure)
		{
			using var cancellation = new CancellationTokenSource ();
			var processes = new List<WeakReference> ();
			for (int i = 0; i < 10; i++)
				processes.Add (RunAndCaptureProcess (cancellation.Token, exitCode, throwOnFailure));

			GC.Collect ();
			GC.WaitForPendingFinalizers ();
			GC.Collect ();

			Assert.IsTrue (processes.All (p => !p.IsAlive), "The live cancellation token must not retain completed processes.");
			Assert.DoesNotThrow (() => cancellation.Cancel ());
		}

		[MethodImpl (MethodImplOptions.NoInlining)]
		static WeakReference RunAndCaptureProcess (CancellationToken token, int exitCode, bool throwOnFailure)
		{
			var shell = Shell ($"echo output&exit /b {exitCode}", $"echo output; exit {exitCode}");
			WeakReference? process = null;
			void Run ()
			{
				MonoAndroidHelper.RunProcess (shell.Command, shell.Arguments, CreateLog (),
					onOutput: (sender, e) => process = new WeakReference (sender),
					cancellationToken: token,
					cancelTask: () => {
						if (throwOnFailure)
							throw new InvalidOperationException ("Failure callback");
					},
					logWarningOnFailure: false);
			}
			if (throwOnFailure)
				Assert.Throws<InvalidOperationException> (Run);
			else
				Run ();
			return process ?? throw new InvalidOperationException ("No output callback received.");
		}
	}
}
