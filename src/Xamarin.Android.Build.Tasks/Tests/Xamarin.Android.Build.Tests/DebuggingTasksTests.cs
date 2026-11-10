using Microsoft.Build.Framework;
using NUnit.Framework;
using System.Collections.Generic;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Xamarin.Android.Build;
using Xamarin.Android.Tasks;
using Xamarin.ProjectTools;
using TaskItem = Microsoft.Build.Utilities.TaskItem;

namespace Xamarin.Android.Build.Tests
{
	[Category ("Node-2")]
	[TestFixture, NonParallelizable]
	public partial class DebuggingTasksTests : BaseTest
	{
		[OneTimeSetUp]
		public void SetUp ()
		{
		}

		[TestCase ("arm64-v8a", "arm64-v8a,armeabi-v7a,armeabi", "android-arm", "armeabi-v7a", "android-arm")]
		[TestCase ("arm64-v8a", "arm64-v8a,armeabi-v7a", "android-arm;android-arm64", "arm64-v8a", "android-arm64")]
		[TestCase ("arm64-v8a", "arm64-v8a,armeabi-v7a", "android-x64;android-arm", "armeabi-v7a", "android-arm")]
		[TestCase ("arm64-v8a", "arm64-v8a", "android-arm", "arm64-v8a", null)]
		[TestCase ("armeabi-v7a", "armeabi-v7a,armeabi", "android-arm", "armeabi-v7a", "android-arm")]
		[TestCase ("x86_64", "x86_64,x86", "android-x86", "x86", "android-x86")]
		[TestCase ("x86_64", "x86_64,x86", "android-x86;android-x64", "x86_64", "android-x64")]
		[TestCase ("x86_64", "x86_64,arm64-v8a,armeabi-v7a", "android-arm;android-arm64", "arm64-v8a", "android-arm64")]
		[TestCase ("arm64-v8a", "armeabi-v7a,arm64-v8a", "android-arm;android-arm64", "arm64-v8a", "android-arm64")]
		[TestCase ("arm64-v8a", "arm64-v8a, armeabi-v7a ", "android-arm", "armeabi-v7a", "android-arm")]
		[TestCase ("arm64-v8a", "", "android-arm64", "arm64-v8a", "android-arm64")]
		[TestCase ("arm64-v8a", "arm64-v8a,armeabi-v7a", null, "arm64-v8a", null)]
		[TestCase ("arm64-v8a", "arm64-v8a,armeabi-v7a", "", "arm64-v8a", null)]
		[TestCase (null, "", "invalid", null, null)]
		public void SelectRuntimeIdentifier (string deviceAbi, string supportedAbis, string runtimeIdentifiers, string expectedAbi, string expectedRid)
		{
			var task = new GetPrimaryCpuAbi {
				BuildEngine = new MockBuildEngine (TestContext.Out),
				ResultingAbi = deviceAbi,
				RuntimeIdentifiers = runtimeIdentifiers?.Split (';'),
			};

			task.SelectRuntimeIdentifier (supportedAbis.Split (','));

			Assert.AreEqual (expectedAbi, task.ResultingAbi);
			Assert.AreEqual (expectedRid, task.RuntimeIdentifier);
		}

		[TestCase (null, "arm64-v8a", "armeabi-v7a", "arm64-v8a,armeabi-v7a")]
		[TestCase ("", "arm64-v8a", "armeabi-v7a", "arm64-v8a,armeabi-v7a")]
		[TestCase (" , ", "arm64-v8a", "armeabi-v7a", "arm64-v8a,armeabi-v7a")]
		[TestCase ("", null, "armeabi-v7a", "armeabi-v7a")]
		[TestCase ("", "arm64-v8a", null, "arm64-v8a")]
		[TestCase ("", null, null, "")]
		[TestCase ("", " ", "", "")]
		[TestCase ("arm64-v8a", "arm64-v8a", "armeabi-v7a", "arm64-v8a")]
		public void GetSupportedAbis (string reportedAbis, string primaryAbi, string secondaryAbi, string expectedAbis)
		{
			var supportedAbis = GetPrimaryCpuAbi.GetSupportedAbis (reportedAbis?.Split (',') ?? [], primaryAbi, secondaryAbi);

			CollectionAssert.AreEqual (expectedAbis.Split (',', StringSplitOptions.RemoveEmptyEntries), supportedAbis);
		}

		[TestCase ("arm64-v8a", "android-arm", "armeabi-v7a")]
		[TestCase ("arm64-v8a", "android-arm64", "arm64-v8a")]
		[TestCase (null, "android-arm", "armeabi-v7a")]
		[TestCase ("", "android-arm64", "arm64-v8a")]
		public void SelectRuntimeIdentifierFromDeviceCache (string deviceAbi, string runtimeIdentifier, string expectedAbi)
		{
			var doc = DeviceCache.Update (null, "device", deviceAbi, 36, "model:TestDevice", ["arm64-v8a", "armeabi-v7a"]);
			doc = XDocument.Parse (doc.ToString ());
			Assert.IsTrue (DeviceCache.TryGet (doc, "device", "model:TestDevice", out var abi, out var sdkVersion, out var supportedAbis));
			Assert.AreEqual (deviceAbi, abi);
			Assert.AreEqual (36, sdkVersion);
			CollectionAssert.AreEqual (new [] { "arm64-v8a", "armeabi-v7a" }, supportedAbis);

			var task = new GetPrimaryCpuAbi {
				BuildEngine = new MockBuildEngine (TestContext.Out),
				ResultingAbi = abi,
				RuntimeIdentifiers = [runtimeIdentifier],
			};
			task.SelectRuntimeIdentifier (supportedAbis);

			Assert.AreEqual (expectedAbi, task.ResultingAbi);
			Assert.AreEqual (runtimeIdentifier, task.RuntimeIdentifier);
			Assert.AreEqual (deviceAbi, doc.Root?.Element ("Device")?.Element ("ResultingAbi")?.Value,
				"The cache must retain the device ABI, not the app ABI.");
		}

		[TestCase (null, "")]
		[TestCase ("", " , ")]
		public void DeviceCacheWithoutAnyAbiIsRefreshed (string deviceAbi, string supportedAbis)
		{
			var doc = DeviceCache.Update (null, "device", deviceAbi, 36, "model:TestDevice", supportedAbis.Split (','));

			Assert.IsFalse (DeviceCache.TryGet (doc, "device", "model:TestDevice", out _, out _, out _));
		}

		[Test]
		public void DeviceCacheWithoutSupportedAbisIsRefreshed ()
		{
			var doc = XDocument.Parse (
				"""
				<Devices>
				  <Device id="device">
				    <ResultingAbi>arm64-v8a</ResultingAbi>
				    <SdkVersion>36</SdkVersion>
				    <LongOutput>model:TestDevice</LongOutput>
				  </Device>
				</Devices>
				""");

			Assert.IsFalse (DeviceCache.TryGet (doc, "device", "model:TestDevice", out _, out _, out _));
		}

		[Test]
		public void DeviceCacheWithNoAdditionalAbisIsValid ()
		{
			var doc = DeviceCache.Update (null, "device", "armeabi-v7a", 19, "model:TestDevice", []);

			Assert.IsTrue (DeviceCache.TryGet (doc, "device", "model:TestDevice", out var abi, out _, out var supportedAbis));
			Assert.AreEqual ("armeabi-v7a", abi);
			Assert.IsEmpty (supportedAbis);
		}

		[Test]
		public void GetPrimaryCpuAbiHonorsAdbTargetArchitecture ()
		{
			var messages = new List<BuildMessageEventArgs> ();
			var task = new GetPrimaryCpuAbi {
				BuildEngine = new MockBuildEngine (TestContext.Out, messages: messages),
				AdbTargetArchitecture = "armeabi-v7a",
				RuntimeIdentifiers = ["android-arm64", "android-arm"],
			};

			Assert.IsTrue (task.Execute ());
			Assert.AreEqual ("armeabi-v7a", task.ResultingAbi);
			Assert.AreEqual ("android-arm", task.RuntimeIdentifier);
			Assert.IsTrue (messages.Any (m => m.Message == "Using $(AdbTargetArchitecture): armeabi-v7a"));
			Assert.IsFalse (messages.Any (m => m.Message.StartsWith ("  ResultingAbi:", StringComparison.Ordinal) ||
				m.Message.StartsWith ("  RuntimeIdentifier:", StringComparison.Ordinal) ||
				m.Message.StartsWith ("  SdkVersion:", StringComparison.Ordinal)));
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
		public void BuilderDoesNotWaitForInheritedRedirectedOutput ()
		{
			if (!IsWindows)
				Assert.Ignore ("This test reproduces a Windows child process inheriting redirected output.");

			var psi = new ProcessStartInfo (Environment.GetEnvironmentVariable ("ComSpec") ?? "cmd.exe",
				"/c start \"\" /b powershell -NoProfile -Command \"Start-Sleep -Seconds 8\"") {
				UseShellExecute = false,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				CreateNoWindow = true,
			};
			using var process = new Process { StartInfo = psi };
			var outputDone = new TaskCompletionSource<bool> (TaskCreationOptions.RunContinuationsAsynchronously);
			var errorDone = new TaskCompletionSource<bool> (TaskCreationOptions.RunContinuationsAsynchronously);
			process.OutputDataReceived += (_, e) => {
				if (e.Data == null)
					outputDone.TrySetResult (true);
			};
			process.ErrorDataReceived += (_, e) => {
				if (e.Data == null)
					errorDone.TrySetResult (true);
			};
			Assert.IsTrue (process.Start ());
			process.BeginOutputReadLine ();
			process.BeginErrorReadLine ();
			Assert.IsTrue (process.WaitForExit (5000), "The parent process should exit promptly.");

			var stopwatch = Stopwatch.StartNew ();
			Assert.IsFalse (Builder.WaitForRedirectedOutput (outputDone.Task, errorDone.Task));
			Assert.Less (stopwatch.Elapsed, TimeSpan.FromSeconds (6), "Inherited output handles must not hold the test runner indefinitely.");
			Assert.IsTrue (outputDone.Task.Wait (TimeSpan.FromSeconds (10)));
			Assert.IsTrue (errorDone.Task.Wait (TimeSpan.FromSeconds (10)));
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

		// https://github.com/dotnet/android/issues/12878
		[TestCase (137, "Killed")]
		[TestCase (1, "")]
		[TestCase (1, "error: closed")]
		[TestCase (1, "adb: error: device offline")]
		[TestCase (1, "find: '/data/local/tmp/fastdeploy/com.example.app/0/lib': Permission denied")]
		public void FastDeployRejectsFailedStagingFileListing (int exitCode, string output)
		{
			using var device = new FakeAdbDevice (exitCode, output);

			Assert.IsFalse (device.RunFastDeploy (), "A failed listing must fail the task.");

			Assert.AreEqual (1, device.Errors.Count);
			Assert.AreEqual ("XA0129", device.Errors [0].Code);
			Assert.That (device.Errors [0].Message, Does.Contain (FakeAdbDevice.StagingRoot));
			Assert.That (device.Commands.Last (), Does.Contain ($"find '{FakeAdbDevice.StagingRoot}'"),
				"Nothing may be changed on the device after the staging listing failed.");
		}

		[TestCase (1, "find: '/data/local/tmp/fastdeploy/com.example.app/0': No such file or directory")]
		[TestCase (0, "")]
		public void FastDeployTreatsMissingOrEmptyStagingDirectoryAsEmpty (int exitCode, string output)
		{
			using var device = new FakeAdbDevice (exitCode, output);

			Assert.IsTrue (device.RunFastDeploy ());

			Assert.AreEqual (0, device.Errors.Count);
		}

		[Test]
		public void FastDeployCopiesFilesFromSuccessfulStagingFileListing ()
		{
			using var device = new FakeAdbDevice (
				0, $"{FakeAdbDevice.StagingRoot}/arm64-v8a/Foo.dll|3|1700000000",
				$"find: '{FakeAdbDevice.OverrideRoot}': No such file or directory");

			Assert.IsTrue (device.RunFastDeploy ());

			Assert.AreEqual (0, device.Errors.Count);
			Assert.That (device.Commands, Has.Some.Contain ($"cp -p {FakeAdbDevice.StagingRoot}/arm64-v8a/Foo.dll"));
		}

		// https://github.com/dotnet/android/issues/13274
		[TestCase ("Copy", false)]
		[TestCase ("Symlink", false)]
		[TestCase ("Copy", true)]
		public void FastDeployRejectsPartialFileListing (string transferMode, bool overrideListing)
		{
			string root = overrideListing ? FakeAdbDevice.OverrideRoot : FakeAdbDevice.StagingRoot;
			string listing = $"{root}/arm64-v8a/Foo.dll|3|1700000000\nstat: '{root}/arm64-v8a/Bar.dll': No such file or directory";
			using var device = new FakeAdbDevice (
				overrideListing ? 0 : 1,
				overrideListing ? $"{FakeAdbDevice.StagingRoot}/arm64-v8a/Foo.dll|3|1700000000" : listing,
				overrideListing ? listing : $"{FakeAdbDevice.OverrideRoot}/.fastdeploy-manifest-hash|64|1700000000");
			if (transferMode == "Symlink") {
				device.FailCommand ("ln -sf", "ln: Operation not permitted");
			}

			Assert.IsFalse (device.RunFastDeploy (symlink: transferMode == "Symlink"), "Partial listings must fail deployment.");

			Assert.AreEqual (1, device.Errors.Count);
			Assert.AreEqual ("XA0129", device.Errors [0].Code);
			Assert.That (device.Errors [0].Message, Does.Contain (root));
			Assert.That (device.Commands.Last (), Does.Contain ("-exec stat"), "No commands after a failed listing.");
			Assert.That (device.Commands, Has.None.Contain ($"rm -rf {FakeAdbDevice.OverrideRoot}"),
				"The override directory must not be cleared.");
			Assert.That (device.Commands, Has.None.Contain ("printf %s"), "No success marker may be written.");
			Assert.IsFalse (device.HasManifest);
		}

		[Test]
		public void FastDeployRejectsMissingRootWithValidEntries ()
		{
			string root = FakeAdbDevice.StagingRoot;
			using var device = new FakeAdbDevice (1, $"{root}/arm64-v8a/Foo.dll|3|1700000000\nfind: '{root}': No such file or directory");

			Assert.IsFalse (device.RunFastDeploy ());
			Assert.AreEqual ("XA0129", device.Errors.Single ().Code);
			Assert.That (device.Commands, Has.None.Contain ("printf %s"));
			Assert.IsFalse (device.HasManifest);
		}

		[Test]
		public void FastDeployCopiesAfterClearingSymlinkState ()
		{
			using var device = new FakeAdbDevice (
				0, $"{FakeAdbDevice.StagingRoot}/arm64-v8a/Foo.dll|3|1700000000",
				$"{FakeAdbDevice.OverrideRoot}/arm64-v8a/Foo.dll|3|1700000000\n{FakeAdbDevice.OverrideRoot}/.fastdeploy-manifest-hash|64|1700000000");

			Assert.IsTrue (device.RunFastDeploy ());

			var commands = device.Commands.ToList ();
			int clearIndex = commands.FindIndex (c => c.Contains ($"rm -rf {FakeAdbDevice.OverrideRoot}"));
			Assert.Greater (clearIndex, commands.FindLastIndex (c => c.Contains ("-exec stat")),
				"Both listings must precede clearing the override directory.");
			Assert.That (commands.Skip (clearIndex + 1), Has.Some.Contain ($"cp -p {FakeAdbDevice.StagingRoot}/arm64-v8a/Foo.dll"),
				"Cleared files must be copied even when the previous listing matched staging.");
			Assert.IsTrue (device.HasManifest);
		}

		[Test]
		public void FastDeployPreservesManifestAfterPartialFileListing ()
		{
			using var device = new FakeAdbDevice (0, $"{FakeAdbDevice.StagingRoot}/arm64-v8a/Foo.dll|3|1700000000");
			Assert.IsTrue (device.RunFastDeploy ());
			string previousManifest = device.Manifest;
			int previousCommandCount = device.Commands.Count;
			device.TouchPayload ();
			device.SetStagingListing (1, $"{FakeAdbDevice.StagingRoot}/arm64-v8a/Foo.dll|3|1700000000\nstat: '{FakeAdbDevice.StagingRoot}/arm64-v8a/Bar.dll': No such file or directory");

			Assert.IsFalse (device.RunFastDeploy ());

			Assert.AreEqual ("XA0129", device.Errors.Single ().Code);
			Assert.AreEqual (previousManifest, device.Manifest, "A failed incremental deployment must not overwrite the successful host manifest.");
			Assert.That (device.Commands.Skip (previousCommandCount), Has.None.Contain ("printf %s"),
				"A failed incremental deployment must not update the remote success marker.");
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

		// Runs FastDeploy against a fake adb with configurable command results.
		sealed class FakeAdbDevice : IDisposable
		{
			public const string PackageName = "com.example.app";
			public const string StagingRoot = "/data/local/tmp/fastdeploy/" + PackageName + "/0";
			public const string OverrideRoot = "files/.__override__";

			const string AdbScript = """
				#!/bin/bash
				dir="$(dirname "$0")"
				printf '%s\n' "$*" >> "$dir/adb.log"
				for fault in fault second-fault; do
					if [ -f "$dir/$fault" ] && [[ "$*" == *"$(cat "$dir/$fault")"* ]]; then
						cat "$dir/$fault.out" >&2
						exit "$(cat "$dir/$fault.exit")"
					fi
				done
				case "$*" in
					"devices -l")
						echo "List of devices attached"
						echo "emulator-5554          device product:sdk_gphone64_arm64 model:sdk_gphone64_arm64 device:emu64a transport_id:1"
						;;
					*"pwd"*"pidof"*)
						cat "$dir/package.out"
						exit "$(cat "$dir/package.exit")"
						;;
					*"readlink -f ."*)
						echo "/data/user/0/com.example.app"
						;;
					*"shell find "*"-exec stat"*)
						cat "$dir/staging-listing.out"
						exit "$(cat "$dir/staging-listing.exit")"
						;;
					*"run-as"*"find "*)
						cat "$dir/override.out"
						exit "$(cat "$dir/override.exit")"
						;;
					*"remote="*"override="*)
						hash="$(cat "$dir/hash" 2>/dev/null)"
						echo "remote=$hash"
						echo "override=$hash"
						;;
					*"printf %s '"*)
						if [[ "$*" =~ printf\ %s\ \'([0-9a-fA-F]+)\' ]]; then
							printf '%s' "${BASH_REMATCH[1]}" > "$dir/hash"
						fi
						;;
				esac
				exit 0
				""";

			readonly string directory = Path.Combine (Path.GetTempPath (), "fake-adb-" + Guid.NewGuid ().ToString ("N"));
			readonly List<BuildErrorEventArgs> errors = new List<BuildErrorEventArgs> ();
			readonly List<ITaskItem> files = [];

			public FakeAdbDevice ()
				: this (0, $"{StagingRoot}/arm64-v8a/Foo.dll|3|1700000000")
			{
			}

			public FakeAdbDevice (int stagingListingExitCode, string stagingListing, string overrideListing = "files/.__override__/stale.dll|1|1700000000")
			{
				if (OperatingSystem.IsWindows ()) {
					Assert.Ignore ("The fake adb is a bash script, which is not supported on Windows.");
				} else {
					Directory.CreateDirectory (Path.Combine (directory, "obj"));
					AddFile ("Foo.dll");
					WriteResult ("staging-listing", stagingListingExitCode, stagingListing);
					WriteResult ("package", 0, "/data/user/0/com.example.app");
					WriteResult ("override", 0, overrideListing);

					var adb = Path.Combine (directory, "adb");
					File.WriteAllText (adb, AdbScript);
					File.SetUnixFileMode (adb, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
				}
			}

			public IReadOnlyList<BuildErrorEventArgs> Errors => errors;

			public bool HasManifest => Directory.GetFiles (Path.Combine (directory, "obj"), "manifest.json", SearchOption.AllDirectories).Length > 0;

			public string Manifest => File.ReadAllText (Directory.GetFiles (Path.Combine (directory, "obj"), "manifest.json", SearchOption.AllDirectories).Single ());

			public void SetStagingListing (int exitCode, string output)
			{
				WriteResult ("staging-listing", exitCode, output);
			}

			public void TouchPayload ()
			{
				string path = Path.Combine (directory, "Foo.dll");
				File.SetLastWriteTimeUtc (path, File.GetLastWriteTimeUtc (path).AddMinutes (1));
			}

			// One entry per adb invocation, without the adb executable itself.
			public IReadOnlyList<string> Commands => File.ReadAllLines (Path.Combine (directory, "adb.log"));

			public void AddFile (string name)
			{
				string path = Path.Combine (directory, name);
				File.WriteAllText (path, "foo");
				var item = new TaskItem (path);
				item.SetMetadata ("TargetPath", "arm64-v8a/" + name);
				files.Add (item);
				File.WriteAllLines (Path.Combine (directory, "staging-listing.out"),
					files.Select (file => $"{StagingRoot}/{file.GetMetadata ("TargetPath")}|3|1700000000"));
			}

			public void FailCommand (string command, string output, bool second = false, int exitCode = 1)
			{
				string fault = second ? "second-fault" : "fault";
				File.WriteAllText (Path.Combine (directory, fault), command);
				WriteResult (fault, exitCode, output);
			}

			public void WriteResult (string name, int exitCode, string output)
			{
				File.WriteAllText (Path.Combine (directory, name + ".exit"), exitCode.ToString ());
				File.WriteAllText (Path.Combine (directory, name + ".out"), output);
			}

			public bool RunFastDeploy (bool symlink = false, bool embedded = false, bool install = false)
			{
				string apk = Path.Combine (directory, "app.apk");
				if (install) {
					File.WriteAllText (apk, "");
				}
				var task = new FastDeploy {
					BuildEngine = new MockBuildEngine (TestContext.Out, errors),
					AdbToolPath = directory,
					AppFileTransferMode = symlink ? "Symlink" : "Copy",
					EmbedAssembliesIntoApk = embedded,
					FastDevFiles = files.ToArray (),
					IntermediateOutputPath = Path.Combine (directory, "obj") + Path.DirectorySeparatorChar,
					PackageName = PackageName,
					PackageFile = install ? apk : null,
					PrimaryCpuAbi = "arm64-v8a",
					UploadFlagFile = Path.Combine (directory, "obj", "upload.flag"),
				};
				return task.Execute ();
			}

			public int RunGlobScript (string failingCommand)
			{
				string staging = Path.Combine (directory, "staging");
				string target = Path.Combine (directory, "override");
				Directory.CreateDirectory (staging);
				Directory.CreateDirectory (target);
				foreach (string name in new [] { "Bar.dll", "Foo.dll", "Zoo.dll" }) {
					File.WriteAllText (Path.Combine (staging, name), "foo");
					File.WriteAllText (Path.Combine (target, name), "old");
				}
				string executable = Path.Combine (directory, failingCommand);
				File.WriteAllText (executable, $"""
					#!/bin/sh
					case "$*" in *Foo.dll*) exit 137;; esac
					exec /bin/{failingCommand} "$@"
					""");
				if (!OperatingSystem.IsWindows ()) {
					File.SetUnixFileMode (executable, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
				}

				string invocation = Commands.Single (c => c.Contains ("for f in"));
				const string shellPrefix = "'sh' '-c' ";
				string script = invocation.Substring (invocation.IndexOf (shellPrefix, StringComparison.Ordinal) + shellPrefix.Length)
					.Replace (StagingRoot + "/arm64-v8a", staging)
					.Replace ("files/.__override__/arm64-v8a", target);
				var startInfo = new ProcessStartInfo ("/bin/sh") { UseShellExecute = false };
				startInfo.ArgumentList.Add ("-c");
				startInfo.ArgumentList.Add ("/bin/sh -c " + script);
				startInfo.Environment ["PATH"] = directory + Path.PathSeparator + Environment.GetEnvironmentVariable ("PATH");
				using var process = Process.Start (startInfo);
				Assert.IsNotNull (process);
				process.WaitForExit ();
				return process.ExitCode;
			}

			public void Dispose ()
			{
				Directory.Delete (directory, recursive: true);
			}
		}

	}

}
