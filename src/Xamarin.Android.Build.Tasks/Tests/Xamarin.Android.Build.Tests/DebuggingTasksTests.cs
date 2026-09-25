using Microsoft.Build.Framework;
using NUnit.Framework;
using System.Collections.Generic;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xamarin.Android.Build;
using Xamarin.Android.Tasks;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests
{
	[Category ("Node-2")]
	[TestFixture, NonParallelizable]
	public class DebuggingTasksTests : BaseTest
	{
		[OneTimeSetUp]
		public void SetUp ()
		{
		}

		[TestCase (null, "physical")]
		[TestCase ("-s physical", "physical")]
		[TestCase ("-d", "physical")]
		[TestCase ("-e", "emulator-5554")]
		public void AndroidHelperSelectsDevice (string target, string expected)
		{
			var devices = new [] {
				new Xamarin.Android.Tools.AdbDeviceInfo { Serial = "physical", Type = Xamarin.Android.Tools.AdbDeviceType.Device },
				new Xamarin.Android.Tools.AdbDeviceInfo { Serial = "emulator-5554", Type = Xamarin.Android.Tools.AdbDeviceType.Emulator },
			};
			Assert.AreEqual (expected, AndroidHelper.SelectDevice (devices, target)?.Serial);
			Assert.IsNull (AndroidHelper.SelectDevice (devices, "-s missing"));
			Assert.IsNull (AndroidHelper.SelectDevice (devices, "invalid"));
		}

		[Test]
		public void FastDeployParsesWarmStateProbe ()
		{
			var state = FastDeploy.ParseWarmStateProbeOutput (
				"""
				__XA_FD_REDIRECT__=
				__XA_FD_RUN_AS_DISABLED__=
				__XA_FD_REMOTE_HASH__=remote-hash
				__XA_FD_PID__=123 456
				__XA_FD_PATH__=/data/user/0/com.example
				__XA_FD_OVERRIDE_HASH__=override-hash
				__XA_FD_RUN_AS_STATUS__=0
				__XA_FD_FORCE_STOP_STATUS__=0
				""");

			Assert.IsTrue (state.HasRequiredState);
			Assert.AreEqual ("remote-hash", state.RemoteHash);
			Assert.AreEqual ("override-hash", state.OverrideHash);
			Assert.AreEqual ("/data/user/0/com.example", state.InternalPath);
			Assert.AreEqual (123, state.ProcessId);
			Assert.AreEqual (0, state.RunAsStatus);
			Assert.AreEqual (0, state.ForceStopStatus);
		}

		[Test]
		public void FastDeployRejectsIncompleteWarmStateProbe ()
		{
			var state = FastDeploy.ParseWarmStateProbeOutput (
				"""
				__XA_FD_REDIRECT__=
				__XA_FD_RUN_AS_DISABLED__=true
				run-as: package not debuggable
				""");

			Assert.IsFalse (state.HasRequiredState);
			Assert.IsTrue (state.HasRunAsDisabled);
			Assert.AreEqual ("true", state.RunAsDisabled);
		}

		[Test]
		public void FastDeployPlansOnlyNewStagingDirectories ()
		{
			var previousFiles = new [] {
				"arm64-v8a/App.dll",
				"arm64-v8a/en-US/App.resources.dll",
			};
			var currentFiles = new [] {
				"arm64-v8a/App.dll",
				"arm64-v8a/New.dll",
				"arm64-v8a/en-US/App.resources.dll",
				"arm64-v8a/fr/App.resources.dll",
			};

			HashSet<string> files = FastDeploy.GetFilesRequiringStagingDirectories (currentFiles, previousFiles);

			CollectionAssert.AreEquivalent (
				new [] { "arm64-v8a/fr/App.resources.dll" },
				files);
		}

		[Test]
		public void FastDeployPlansAllStagingDirectoriesAfterReset ()
		{
			var currentFiles = new [] {
				"App.dll",
				"arm64-v8a/App.dll",
				"arm64-v8a/fr/App.resources.dll",
			};

			HashSet<string> files = FastDeploy.GetFilesRequiringStagingDirectories (currentFiles, previousFiles: null);

			CollectionAssert.AreEquivalent (currentFiles, files);
		}

		[TestCase ("adb: error: failed to copy: No such file or directory")]
		[TestCase ("adb: error: target '/data/local/tmp/app/arm64-v8a' is not a directory")]
		[TestCase ("remote couldn't create file: Is a directory")]
		public void FastDeployDetectsInvalidRemoteFilesystem (string output)
		{
			Assert.IsTrue (FastDeploy.IsUnexpectedRemoteFilesystemError (output));
		}

		[Test]
		public void FastDeployDoesNotResetForUnrelatedPushFailure ()
		{
			Assert.IsFalse (FastDeploy.IsUnexpectedRemoteFilesystemError ("adb: error: device offline"));
		}

		[TestCase ("adb: error: device offline", false)]
		[TestCase ("adb: failed to install app.apk: cmd: Failure calling service package: Broken pipe (32)", true)]
		[TestCase ("adb: failed to install app.apk: Broken pipe (32)", false)]
		[TestCase ("cmd: Failure calling service package: Security exception", false)]
		[TestCase ("Failure [INSTALL_FAILED_INSUFFICIENT_STORAGE]", false)]
		public void FastDeployClassifiesOnlyKnownTransientInstallFailures (string output, bool expected)
		{
			Assert.AreEqual (expected, FastDeploy.IsTransientInstallFailure (output));
		}

		[Test]
		public async Task FastDeployRetriesTransientInstallOnce ()
		{
			var task = new TestFastDeploy (
				CreateAdbResult (1, "adb: failed to install app.apk: cmd: Failure calling service package: Broken pipe (32)"),
				CreateAdbResult (0, "Success"));

			await task.InstallApkWithRetry ("app.apk", reinstall: false, testOnly: false, user: "");

			Assert.AreEqual (2, task.InstallAttempts);
			Assert.AreEqual (1, task.RecoveryAttempts);
		}

		[Test]
		public async Task FastDeployRetriesTransientInstallAfterUninstall ()
		{
			var task = new TestFastDeploy (
				CreateAdbResult (1, "Failure [INSTALL_FAILED_ALREADY_EXISTS]"),
				CreateAdbResult (1, "adb: failed to install app.apk: cmd: Failure calling service package: Broken pipe (32)"),
				CreateAdbResult (0, "Success"));

			await task.InstallApkWithRetry ("app.apk", reinstall: false, testOnly: false, user: "");

			Assert.AreEqual (3, task.InstallAttempts);
			Assert.AreEqual (1, task.UninstallAttempts);
			Assert.AreEqual (1, task.RecoveryAttempts);
		}

		[Test]
		public void FastDeployDoesNotRetryTransientInstallTwiceAcrossUninstall ()
		{
			var task = new TestFastDeploy (
				CreateAdbResult (1, "first failure: cmd: Failure calling service package: Broken pipe (32)"),
				CreateAdbResult (1, "Failure [INSTALL_FAILED_ALREADY_EXISTS]"),
				CreateAdbResult (1, "third failure: cmd: Failure calling service package: Broken pipe (32)"));

			var exception = Assert.ThrowsAsync<FastDeployInstallException> (
				async () => await task.InstallApkWithRetry ("app.apk", reinstall: false, testOnly: false, user: ""));

			Assert.IsNotNull (exception);
			Assert.AreEqual ("ADB0010", exception.ErrorCode);
			Assert.That (exception.Message, Does.Contain ("Install attempt 1"));
			Assert.That (exception.Message, Does.Contain ("first failure"));
			Assert.That (exception.Message, Does.Contain ("Install attempt 2"));
			Assert.That (exception.Message, Does.Contain ("INSTALL_FAILED_ALREADY_EXISTS"));
			Assert.That (exception.Message, Does.Contain ("Install attempt 3"));
			Assert.That (exception.Message, Does.Contain ("third failure"));
			Assert.That (exception.Message, Does.Not.Contain ("Install attempt 4"));
			Assert.AreEqual (3, task.InstallAttempts);
			Assert.AreEqual (1, task.UninstallAttempts);
			Assert.AreEqual (1, task.RecoveryAttempts);
		}

		[Test]
		public void FastDeployDoesNotRetrySemanticInstallFailure ()
		{
			var task = new TestFastDeploy (
				CreateAdbResult (1, "Failure [INSTALL_FAILED_INSUFFICIENT_STORAGE]"));

			var exception = Assert.ThrowsAsync<FastDeployInstallException> (
				async () => await task.InstallApkWithRetry ("app.apk", reinstall: false, testOnly: false, user: ""));

			Assert.IsNotNull (exception);
			Assert.AreEqual ("ADB0060", exception.ErrorCode);
			Assert.AreEqual (1, task.InstallAttempts);
			Assert.AreEqual (0, task.RecoveryAttempts);
		}

		[Test]
		public void FastDeployPreservesBothTransientInstallAttempts ()
		{
			var task = new TestFastDeploy (
				CreateAdbResult (1, "first failure: cmd: Failure calling service package: Broken pipe (32)"),
				CreateAdbResult (1, "second failure: cmd: Failure calling service package: Broken pipe (32)"));

			var exception = Assert.ThrowsAsync<FastDeployInstallException> (
				async () => await task.InstallApkWithRetry ("app.apk", reinstall: false, testOnly: false, user: ""));

			Assert.IsNotNull (exception);
			Assert.AreEqual ("ADB0010", exception.ErrorCode);
			Assert.That (exception.Message, Does.Contain ("Install attempt 1"));
			Assert.That (exception.Message, Does.Contain ("first failure"));
			Assert.That (exception.Message, Does.Contain ("ADB transport recovery"));
			Assert.That (exception.Message, Does.Contain ("Install attempt 2"));
			Assert.That (exception.Message, Does.Contain ("second failure"));
			Assert.AreEqual (2, task.InstallAttempts);
			Assert.AreEqual (1, task.RecoveryAttempts);
		}

		[Test]
		public void FastDeployPreservesOriginalFailureWhenRecoveryFails ()
		{
			var task = new TestFastDeploy (
				new InvalidOperationException ("package manager still unavailable"),
				CreateAdbResult (1, "cmd: Failure calling service package: Broken pipe (32)"));

			var exception = Assert.ThrowsAsync<FastDeployInstallException> (
				async () => await task.InstallApkWithRetry ("app.apk", reinstall: false, testOnly: false, user: ""));

			Assert.IsNotNull (exception);
			Assert.That (exception.Message, Does.Contain ("Failure calling service package: Broken pipe (32)"));
			Assert.That (exception.Message, Does.Contain ("package manager still unavailable"));
			Assert.AreEqual (1, task.InstallAttempts);
			Assert.AreEqual (1, task.RecoveryAttempts);
		}

		static FastDeploy.AdbCommandResult CreateAdbResult (int exitCode, string output)
		{
			return new FastDeploy.AdbCommandResult {
				ExitCode = exitCode,
				StandardOutput = output,
				StandardError = "",
			};
		}

		sealed class TestFastDeploy : FastDeploy
		{
			readonly Queue<AdbCommandResult> results;
			readonly Exception recoveryException;

			public int InstallAttempts { get; private set; }
			public int UninstallAttempts { get; private set; }
			public int RecoveryAttempts { get; private set; }

			public TestFastDeploy (params AdbCommandResult [] results)
				: this (recoveryException: null, results)
			{
			}

			public TestFastDeploy (Exception recoveryException, params AdbCommandResult [] results)
			{
				this.results = new Queue<AdbCommandResult> (results);
				this.recoveryException = recoveryException;
				BuildEngine = new MockBuildEngine (TestContext.Out);
				PackageName = "com.example.app";
			}

			internal override Task<AdbCommandResult> RunInstallCommand (string apkFile, bool reinstall, bool testOnly, string user)
			{
				InstallAttempts++;
				return Task.FromResult (results.Dequeue ());
			}

			internal override Task UninstallPackage (string packageName, bool preserveData, string user)
			{
				UninstallAttempts++;
				return Task.CompletedTask;
			}

			internal override Task WaitForInstallTransportRecovery ()
			{
				RecoveryAttempts++;
				return recoveryException == null ? Task.CompletedTask : Task.FromException (recoveryException);
			}
		}

	}

}
