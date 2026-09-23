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
using AT = Xamarin.AndroidTools;

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

		// https://github.com/xamarin/monodroid/blob/63bbeb076d809c74811a8001d38bf2e9e8672627/tests/msbuild/nunit/Xamarin.Android.Build.Tests/Xamarin.Android.Build.Tests/ResolveXamarinAndroidToolsTests.cs
		[Test]
		[Repeat (10)]
		public void TestResolveToolsExists ()
		{
			List<BuildErrorEventArgs> errors = new List<BuildErrorEventArgs>();
			List<BuildMessageEventArgs> messages = new List<BuildMessageEventArgs>();

			var path = Path.Combine ("temp", TestName);
			if (Directory.Exists (Path.Combine (Root, path)))
				Directory.Delete (Path.Combine (Root, path), recursive: true);

			var engine = new MockBuildEngine (TestContext.Out, errors: errors, messages: messages);
			var frameworksRoot = Path.Combine (TestEnvironment.DotNetPreviewDirectory, "packs", "Microsoft.NETCore.App.Ref");
			var mscorlibDll = Directory.GetFiles (frameworksRoot, "mscorlib.dll", SearchOption.AllDirectories).LastOrDefault ();
			var frameworksPath = Path.GetDirectoryName (mscorlibDll);
			var androidSdk = CreateFauxAndroidSdkDirectory (Path.Combine (path, "Sdk"), "24.0.1", new[]
			{
				new ApiInfo { Id = "23", Level = 23, Name = "Marshmallow", FrameworkVersion = "v6.0", Stable = true },
				new ApiInfo { Id = "26", Level = 26, Name = "Oreo", FrameworkVersion = "v8.0", Stable = true },
				new ApiInfo { Id = "27", Level = 27, Name = "Oreo", FrameworkVersion = "v8.1", Stable = true },
				new ApiInfo { Id = "28", Level = 28, Name = "Pie", FrameworkVersion = "v9.0", Stable = true },
			});
			//var androidNdk = CreateFauxAndroidNdkDirectory (Path.Combine (path, "Ndk"));
			var javaSdk = CreateFauxJavaSdkDirectory (Path.Combine(path, "Java"), "1.8.0", out string javaExe, out string javacExe);
			var task = new ResolveXamarinAndroidTools () {
				BuildEngine = engine,
				AndroidNdkPath = null,
				AndroidSdkPath = androidSdk,
				JavaSdkPath = javaSdk,
				MonoAndroidToolsPath = TestEnvironment.AndroidMSBuildDirectory,
				ReferenceAssemblyPaths = new string[] {
					frameworksPath,
					TestEnvironment.MonoAndroidFrameworkDirectory,
				},
			};
			// ResolveXamarinAndroidTools replaces process-wide AndroidSdk state and updates JAVA_HOME/PATH on Windows.
			var javaHome = Environment.GetEnvironmentVariable ("JAVA_HOME");
			var environmentPath = Environment.GetEnvironmentVariable ("PATH");
			var actualAndroidSdk = AndroidSdkPath;
			var actualAndroidNdk = AndroidNdkPath;
			var actualJavaSdk = AndroidSdkResolver.GetJavaSdkPath ();
			List<string> firstTaskExecMessages;

			try {
				Assert.True (task.Execute (), "Task should have completed successfully.");
				Assert.AreEqual (0, errors.Count, "No Errors should have been raised");
				firstTaskExecMessages = messages.Select (x => x.Message)?.ToList ();
				Assert.True (task.Execute (), "Task should have completed successfully.");
			} finally {
				AT.AndroidSdk.Refresh (actualAndroidSdk, actualAndroidNdk, actualJavaSdk);
				Environment.SetEnvironmentVariable ("JAVA_HOME", javaHome);
				Environment.SetEnvironmentVariable ("PATH", environmentPath);
			}

			var expected = $"  Found FrameworkPath at {Path.GetFullPath (frameworksPath)}";
			Assert.IsNotNull (firstTaskExecMessages, "First execution did not contain any messages!");
			CollectionAssert.Contains (firstTaskExecMessages, expected);
			CollectionAssert.DoesNotContain (firstTaskExecMessages, "  Using cached AndroidSdk values");
			CollectionAssert.DoesNotContain (firstTaskExecMessages, "  Using cached MonoDroidSdk values");

			Assert.AreEqual (0, errors.Count, "No Errors should have been raised");
			var secondTaskExecMessages = messages.Select (x => x.Message)?.ToList ();
			Assert.IsNotNull (secondTaskExecMessages, "Second execution did not contain any messages!");
			CollectionAssert.Contains (secondTaskExecMessages, expected);
			CollectionAssert.Contains (secondTaskExecMessages, "  Using cached AndroidSdk values");
			CollectionAssert.Contains (secondTaskExecMessages, "  Using cached MonoDroidSdk values");
		}

		[Test]
		public void FastDeploy2ParsesWarmStateProbe ()
		{
			var state = FastDeploy2.ParseWarmStateProbeOutput (
				"""
				__XA_FD2_REDIRECT__=
				__XA_FD2_RUN_AS_DISABLED__=
				__XA_FD2_REMOTE_HASH__=remote-hash
				__XA_FD2_PID__=123 456
				__XA_FD2_PATH__=/data/user/0/com.example
				__XA_FD2_OVERRIDE_HASH__=override-hash
				__XA_FD2_RUN_AS_STATUS__=0
				__XA_FD2_FORCE_STOP_STATUS__=0
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
		public void FastDeploy2RejectsIncompleteWarmStateProbe ()
		{
			var state = FastDeploy2.ParseWarmStateProbeOutput (
				"""
				__XA_FD2_REDIRECT__=
				__XA_FD2_RUN_AS_DISABLED__=true
				run-as: package not debuggable
				""");

			Assert.IsFalse (state.HasRequiredState);
			Assert.IsTrue (state.HasRunAsDisabled);
			Assert.AreEqual ("true", state.RunAsDisabled);
		}

		[Test]
		public void FastDeploy2PlansOnlyNewStagingDirectories ()
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

			HashSet<string> files = FastDeploy2.GetFilesRequiringStagingDirectories (currentFiles, previousFiles);

			CollectionAssert.AreEquivalent (
				new [] { "arm64-v8a/fr/App.resources.dll" },
				files);
		}

		[Test]
		public void FastDeploy2PlansAllStagingDirectoriesAfterReset ()
		{
			var currentFiles = new [] {
				"App.dll",
				"arm64-v8a/App.dll",
				"arm64-v8a/fr/App.resources.dll",
			};

			HashSet<string> files = FastDeploy2.GetFilesRequiringStagingDirectories (currentFiles, previousFiles: null);

			CollectionAssert.AreEquivalent (currentFiles, files);
		}

		[TestCase ("adb: error: failed to copy: No such file or directory")]
		[TestCase ("adb: error: target '/data/local/tmp/app/arm64-v8a' is not a directory")]
		[TestCase ("remote couldn't create file: Is a directory")]
		public void FastDeploy2DetectsInvalidRemoteFilesystem (string output)
		{
			Assert.IsTrue (FastDeploy2.IsUnexpectedRemoteFilesystemError (output));
		}

		[Test]
		public void FastDeploy2DoesNotResetForUnrelatedPushFailure ()
		{
			Assert.IsFalse (FastDeploy2.IsUnexpectedRemoteFilesystemError ("adb: error: device offline"));
		}

		[TestCase ("adb: error: device offline", false)]
		[TestCase ("adb: failed to install app.apk: cmd: Failure calling service package: Broken pipe (32)", true)]
		[TestCase ("adb: failed to install app.apk: Broken pipe (32)", false)]
		[TestCase ("cmd: Failure calling service package: Security exception", false)]
		[TestCase ("Failure [INSTALL_FAILED_INSUFFICIENT_STORAGE]", false)]
		public void FastDeploy2ClassifiesOnlyKnownTransientInstallFailures (string output, bool expected)
		{
			Assert.AreEqual (expected, FastDeploy2.IsTransientInstallFailure (output));
		}

		[Test]
		public async Task FastDeploy2RetriesTransientInstallOnce ()
		{
			var task = new TestFastDeploy2 (
				CreateAdbResult (1, "adb: failed to install app.apk: cmd: Failure calling service package: Broken pipe (32)"),
				CreateAdbResult (0, "Success"));

			await task.InstallApkWithRetry ("app.apk", reinstall: false, testOnly: false, user: "");

			Assert.AreEqual (2, task.InstallAttempts);
			Assert.AreEqual (1, task.RecoveryAttempts);
		}

		[Test]
		public async Task FastDeploy2RetriesTransientInstallAfterUninstall ()
		{
			var task = new TestFastDeploy2 (
				CreateAdbResult (1, "Failure [INSTALL_FAILED_ALREADY_EXISTS]"),
				CreateAdbResult (1, "adb: failed to install app.apk: cmd: Failure calling service package: Broken pipe (32)"),
				CreateAdbResult (0, "Success"));

			await task.InstallApkWithRetry ("app.apk", reinstall: false, testOnly: false, user: "");

			Assert.AreEqual (3, task.InstallAttempts);
			Assert.AreEqual (1, task.UninstallAttempts);
			Assert.AreEqual (1, task.RecoveryAttempts);
		}

		[Test]
		public void FastDeploy2DoesNotRetryTransientInstallTwiceAcrossUninstall ()
		{
			var task = new TestFastDeploy2 (
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
		public void FastDeploy2DoesNotRetrySemanticInstallFailure ()
		{
			var task = new TestFastDeploy2 (
				CreateAdbResult (1, "Failure [INSTALL_FAILED_INSUFFICIENT_STORAGE]"));

			var exception = Assert.ThrowsAsync<FastDeployInstallException> (
				async () => await task.InstallApkWithRetry ("app.apk", reinstall: false, testOnly: false, user: ""));

			Assert.IsNotNull (exception);
			Assert.AreEqual ("ADB0060", exception.ErrorCode);
			Assert.AreEqual (1, task.InstallAttempts);
			Assert.AreEqual (0, task.RecoveryAttempts);
		}

		[Test]
		public void FastDeploy2PreservesBothTransientInstallAttempts ()
		{
			var task = new TestFastDeploy2 (
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
		public void FastDeploy2PreservesOriginalFailureWhenRecoveryFails ()
		{
			var task = new TestFastDeploy2 (
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

		static FastDeploy2.AdbCommandResult CreateAdbResult (int exitCode, string output)
		{
			return new FastDeploy2.AdbCommandResult {
				ExitCode = exitCode,
				StandardOutput = output,
				StandardError = "",
			};
		}

		sealed class TestFastDeploy2 : FastDeploy2
		{
			readonly Queue<AdbCommandResult> results;
			readonly Exception recoveryException;

			public int InstallAttempts { get; private set; }
			public int UninstallAttempts { get; private set; }
			public int RecoveryAttempts { get; private set; }

			public TestFastDeploy2 (params AdbCommandResult [] results)
				: this (recoveryException: null, results)
			{
			}

			public TestFastDeploy2 (Exception recoveryException, params AdbCommandResult [] results)
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

	/// <summary>
	/// Unit tests for <see cref="FastDeploy"/> helper methods that do not require a device.
	/// </summary>
	[TestFixture]
	public class FastDeployTests
	{
		// Canonical transient race output produced by the Android run-as tool when
		// the per-user data directory has not yet materialized after pm install.
		static readonly string [] TransientRaceOutputs = {
			"run-as: couldn't stat /data/user/0/com.example.app: No such file or directory",
			"run-as: couldn't stat /data/user/10/com.example.app: No such file or directory",
			// Verify case-insensitivity of the detection.
			"run-as: Couldn't Stat /data/user/0/com.example.app: No Such File Or Directory",
			// Extra surrounding whitespace / newlines as they may appear in raw adb output.
			"  run-as: couldn't stat /data/user/0/com.example.app: No such file or directory\n",
		};

		// Genuine run-as failures that must NOT be swallowed by the retry loop.
		static readonly string [] NonTransientOutputs = {
			// Null / empty — first guard in the implementation.
			null,
			"",
			// Successful pwd output — the data directory already exists.
			"/data/user/0/com.example.app",
			// Package not debuggable.
			"run-as: package 'com.example.app' is not debuggable",
			// Package not installed.
			"run-as: package 'com.example.app' is unknown",
			// Permission denied (SELinux / policy).
			"run-as: couldn't stat /data/user/0/com.example.app: Permission denied",
			// Only one of the two required substrings — must not match.
			"run-as: couldn't stat /data/user/0/com.example.app",
			"No such file or directory",
		};

		[TestCaseSource (nameof (TransientRaceOutputs))]
		public void IsTransientRunAsStatRace_ReturnsTrueForRaceSignature (string output)
		{
			Assert.IsTrue (FastDeploy.IsTransientRunAsStatRace (output),
				$"Expected transient-race detection for: {output}");
		}

		[TestCaseSource (nameof (NonTransientOutputs))]
		public void IsTransientRunAsStatRace_ReturnsFalseForNonTransientOutput (string output)
		{
			Assert.IsFalse (FastDeploy.IsTransientRunAsStatRace (output),
				$"Expected no transient-race detection for: {output}");
		}

		[TestCase ("package:/data/app/~~hash/com.example.app-base/base.apk")]
		[TestCase ("package:/data/app/~~hash/com.example.app-base/base.apk\npackage:/data/app/~~hash/com.example.app-split/split_config.en.apk")]
		public void IsPackageInstalledOutput_ReturnsTrueForPackagePaths (string output)
		{
			Assert.IsTrue (FastDeploy.IsPackageInstalledOutput (output));
		}

		[TestCase (null)]
		[TestCase ("")]
		[TestCase ("Error: package com.example.app was not found")]
		public void IsPackageInstalledOutput_ReturnsFalseWithoutPackagePath (string output)
		{
			Assert.IsFalse (FastDeploy.IsPackageInstalledOutput (output));
		}
	}
}
