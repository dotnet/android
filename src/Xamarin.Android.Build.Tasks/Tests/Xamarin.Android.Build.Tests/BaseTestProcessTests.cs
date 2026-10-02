using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using NUnit.Framework;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	[NonParallelizable]
	[Platform (Exclude = "Win")]
	public class BaseTestProcessTests : HostProcessFixture
	{
		sealed class Harness : BaseTest
		{
			public void VerifySignature (string apk) => AssertApkIsSigned (apk);
			public (int code, string stdout, string stderr) Capture (string command) => RunProcessWithExitCode (command, "", 10);
		}

		[TestCase (0)]
		[TestCase (7)]
		public void CommandDrainsBothStreams (int exitCode)
		{
			string script = CreateScript ("command", LargeOutput + $"\nexit {exitCode}");
			using var output = new StringWriter ();
			var previous = Console.Out;
			try {
				Console.SetOut (output);
				Assert.AreEqual (exitCode == 0, WithDeadline (() => Invoke<bool> (typeof (BaseTest), "RunCommand", null,
					[typeof (string), typeof (string), typeof (int)], script, "", 10000)));
			} finally {
				Console.SetOut (previous);
			}
			AssertOutput (output.ToString ());
		}

		[Test]
		public void NativeToolCapturesBothStreams ()
		{
			string script = CreateScript ("native", LargeOutput);
			var (stdout, stderr) = WithDeadline (() => Invoke<(List<string>, List<string>)> (typeof (EnvironmentHelper), "RunCommand", null,
				[typeof (string), typeof (string), typeof (int)], script, "", 10000));
			AssertOutput (string.Join (Environment.NewLine, stdout) + Environment.NewLine + string.Join (Environment.NewLine, stderr));
		}

		[Test]
		public void NativeToolPreservesNonzeroAssertion ()
		{
			string script = CreateScript ("native-error", "echo nonzero-stdout\necho nonzero-stderr >&2\nexit 7");
			var failure = Assert.Throws<AssertionException> (() => WithDeadline (() => Invoke<(List<string>, List<string>)> (
				typeof (EnvironmentHelper), "RunCommand", null, [typeof (string), typeof (string), typeof (int)], script, "", 10000)));
			Assert.IsNotNull (failure);
			StringAssert.Contains (script, failure.Message);
		}

		[TestCase (false)]
		[TestCase (true)]
		public void CommandTimeoutTerminatesProcessTree (bool nativeTool)
		{
			string childPidFile = Path.Combine (directory, "child.pid");
			processIds.Add (childPidFile);
			string script = CreateScript ("timeout", $$"""
				sleep 120 &
				echo $! > {{Quote (childPidFile)}}
				echo timeout-stdout
				echo timeout-stderr >&2
				wait
				""");
			var timer = Stopwatch.StartNew ();
			if (nativeTool) {
				Assert.Throws<AssertionException> (() => WithDeadline (() => Invoke<(List<string>, List<string>)> (
					typeof (EnvironmentHelper), "RunCommand", null, [typeof (string), typeof (string), typeof (int)], script, "", 500)));
			} else {
				Assert.IsFalse (WithDeadline (() => Invoke<bool> (typeof (BaseTest), "RunCommand", null,
					[typeof (string), typeof (string), typeof (int)], script, "", 500)));
			}
			Assert.Less (timer.Elapsed, TimeSpan.FromSeconds (10));
			Assert.IsTrue (File.Exists (childPidFile));
			AssertProcessExited (childPidFile);
		}

		[TestCase (0)]
		[TestCase (7)]
		public void ApkSignerDrainsBothStreams (int exitCode)
		{
			CreateScript ("sdk/build-tools/99.0.0/apksigner", LargeOutput + $"\nexit {exitCode}");
			var previous = Environment.GetEnvironmentVariable ("TEST_ANDROID_SDK_PATH");
			try {
				Environment.SetEnvironmentVariable ("TEST_ANDROID_SDK_PATH", Path.Combine (directory, "sdk"));
				var harness = new Harness ();
				if (exitCode == 0) {
					WithDeadline (() => { harness.VerifySignature ("test.apk"); return true; });
				} else {
					var failure = Assert.Throws<AssertionException> (() => WithDeadline (() => { harness.VerifySignature ("test.apk"); return true; }));
					Assert.IsNotNull (failure);
					StringAssert.Contains ("stdout-tail", failure.Message);
					StringAssert.Contains ("stderr-tail", failure.Message);
				}
			} finally {
				Environment.SetEnvironmentVariable ("TEST_ANDROID_SDK_PATH", previous);
			}
		}

		[Test]
		public void ExistingProcessCaptureRetainsTrailingOutput ()
		{
			string script = CreateScript ("capture", LargeOutput);
			var (code, stdout, stderr) = WithDeadline (() => new Harness ().Capture (script));
			Assert.AreEqual (0, code);
			AssertOutput (stdout + Environment.NewLine + stderr);
		}

		[TestCase (0)]
		[TestCase (7)]
		public void ChmodFixtureDrainsBothStreams (int exitCode)
		{
			CreateScript ("chmod", LargeOutput + (exitCode == 0 ? "\nexec /bin/chmod \"$@\"" : "\nexit 7"));
			var harness = new Harness ();
			string path = Path.Combine (directory, "fixture-script");
			if (exitCode == 0) {
				WithDeadline (() => {
					InvokeRaw (typeof (BaseTest), "CreateShellScript", harness, [typeof (string), typeof (string)], path, "echo fixture");
					return true;
				});
				Assert.IsTrue (File.Exists (path));
			} else {
				var failure = Assert.Throws<AssertionException> (() => WithDeadline (() => {
					InvokeRaw (typeof (BaseTest), "CreateShellScript", harness, [typeof (string), typeof (string)], path, "echo fixture");
					return true;
				}));
				Assert.IsNotNull (failure);
				StringAssert.Contains ("chmod failed", failure.Message);
				StringAssert.Contains ("stdout-tail", failure.Message);
				StringAssert.Contains ("stderr-tail", failure.Message);
			}
		}
	}
}
