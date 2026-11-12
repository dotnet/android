using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
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

		static TaskLoggingHelper CreateLog ()
		{
			return new TaskLoggingHelper (new MockBuildEngine (TextWriter.Null), nameof (MonoAndroidHelperProcessTests));
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

		[TestCase (0, false)]
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
