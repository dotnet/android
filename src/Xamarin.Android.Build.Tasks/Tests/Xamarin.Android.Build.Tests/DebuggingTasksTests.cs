using Microsoft.Build.Framework;
using NUnit.Framework;
using System.Collections.Generic;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
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
			var task = new GetPrimaryCpuAbi {
				BuildEngine = new MockBuildEngine (TestContext.Out),
				AdbTargetArchitecture = "armeabi-v7a",
				RuntimeIdentifiers = ["android-arm64", "android-arm"],
			};

			Assert.IsTrue (task.Execute ());
			Assert.AreEqual ("armeabi-v7a", task.ResultingAbi);
			Assert.AreEqual ("android-arm", task.RuntimeIdentifier);
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
