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
			var project = new XamarinAndroidLibraryProject {
				TargetFramework = "net11.0",
				Imports = {
					new Import ("InheritedOutput.targets") {
						TextContent = () => """
							<Project>
							  <UsingTask TaskName="StartInheritedOutput" TaskFactory="RoslynCodeTaskFactory" AssemblyFile="$(MSBuildToolsPath)/Microsoft.Build.Tasks.Core.dll">
							    <ParameterGroup>
							      <SpawnChild ParameterType="System.Boolean" Required="true" />
							      <PidFile ParameterType="System.String" Required="true" />
							    </ParameterGroup>
							    <Task>
							      <Code Type="Fragment" Language="cs"><![CDATA[
							        if (SpawnChild) {
							          bool windows = System.Environment.OSVersion.Platform == System.PlatformID.Win32NT;
							          using (var child = System.Diagnostics.Process.Start (new System.Diagnostics.ProcessStartInfo (
							            windows ? "powershell" : "/bin/sh",
							            windows ? "-NoProfile -Command \"Start-Sleep -Seconds 20\"" : "-c \"sleep 20\"") { UseShellExecute = false })) {
							            System.IO.File.WriteAllText (PidFile, child.Id.ToString (System.Globalization.CultureInfo.InvariantCulture));
							          }
							        }
							        System.Console.WriteLine ("build-stdout");
							        System.Console.Error.WriteLine ("build-stderr");
							        return true;
							      ]]></Code>
							    </Task>
							  </UsingTask>
							  <Target Name="SpawnOutput" Inputs="$(MSBuildProjectFullPath)" Outputs="$(ChildPidFile)">
							    <StartInheritedOutput SpawnChild="$(SpawnChild)" PidFile="$(ChildPidFile)" />
							  </Target>
							</Project>
							""",
					},
				},
			};
			using var builder = CreateDllBuilder ();
			builder.AutomaticNuGetRestore = false;
			builder.MaxCpuCount = 1;
			string pidFile = Path.GetFullPath (Path.Combine (Root, builder.ProjectDirectory, "child.pid"));
			string processLog = Path.Combine (Path.GetDirectoryName (pidFile), "process.log");
			try {
				Assert.IsTrue (builder.RunTarget (project, "SpawnOutput", parameters: ["SpawnChild=false", $"ChildPidFile=\"{pidFile}\""]));
				var stopwatch = Stopwatch.StartNew ();
				Assert.IsTrue (builder.RunTarget (project, "SpawnOutput", parameters: ["SpawnChild=true", $"ChildPidFile=\"{pidFile}\""]));
				Assert.Less (stopwatch.Elapsed, TimeSpan.FromSeconds (10), "Inherited output must not delay the build until the child exits.");
				StringAssert.Contains ("build-stdout", File.ReadAllText (processLog));
				StringAssert.Contains ("build-stderr", File.ReadAllText (processLog));
				using var log = File.Open (processLog, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
			} finally {
				if (File.Exists (pidFile)) {
					int pid = int.Parse (File.ReadAllText (pidFile), System.Globalization.CultureInfo.InvariantCulture);
					if (Process.TryGetProcessById (pid, out var child)) {
						using (child) {
							child.Kill (entireProcessTree: true);
							Assert.IsTrue (child.WaitForExit (5000), "The fixture child did not exit after cleanup.");
						}
					}
				}
			}
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
			using var device = new FakeAdbDevice (0, $"{FakeAdbDevice.StagingRoot}/arm64-v8a/Foo.dll|3|1700000000");

			Assert.IsTrue (device.RunFastDeploy ());

			Assert.AreEqual (0, device.Errors.Count);
			Assert.That (device.Commands, Has.Some.Contain ($"cp -p {FakeAdbDevice.StagingRoot}/arm64-v8a/Foo.dll"));
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

		// Runs the real FastDeploy task in Copy mode against a fake `adb` script, so the tests depend
		// only on the task's public properties and on the adb command lines it issues. The script
		// answers `adb devices` and the staging file listing with canned output, records every command
		// line, and succeeds silently for everything else. It is a bash script, so the tests that use
		// it are not supported on Windows.
		sealed class FakeAdbDevice : IDisposable
		{
			public const string PackageName = "com.example.app";
			public const string StagingRoot = "/data/local/tmp/fastdeploy/" + PackageName + "/0";

			const string AdbScript = """
				#!/bin/bash
				dir="$(dirname "$0")"
				printf '%s\n' "$*" >> "$dir/adb.log"
				case "$*" in
					"devices -l")
						echo "List of devices attached"
						echo "emulator-5554          device product:sdk_gphone64_arm64 model:sdk_gphone64_arm64 device:emu64a transport_id:1"
						;;
					*"pwd; pidof"*)
						echo "/data/user/0/com.example.app"
						;;
					*"shell find "*"-exec stat"*)
						cat "$dir/staging-listing.out"
						exit "$(cat "$dir/staging-listing.exit")"
						;;
				esac
				exit 0
				""";

			readonly string directory = Path.Combine (Path.GetTempPath (), "fake-adb-" + Guid.NewGuid ().ToString ("N"));
			readonly List<BuildErrorEventArgs> errors = new List<BuildErrorEventArgs> ();

			public FakeAdbDevice (int stagingListingExitCode, string stagingListing)
			{
				if (TestEnvironment.IsWindows) {
					Assert.Ignore ("The fake adb is a bash script, which is not supported on Windows.");
				} else {
					Directory.CreateDirectory (Path.Combine (directory, "obj"));
					File.WriteAllText (Path.Combine (directory, "Foo.dll"), "foo");
					File.WriteAllText (Path.Combine (directory, "staging-listing.out"), stagingListing);
					File.WriteAllText (Path.Combine (directory, "staging-listing.exit"), stagingListingExitCode.ToString ());

					var adb = Path.Combine (directory, "adb");
					File.WriteAllText (adb, AdbScript);
					File.SetUnixFileMode (adb, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
				}
			}

			public IReadOnlyList<BuildErrorEventArgs> Errors => errors;

			// One entry per adb invocation, without the adb executable itself.
			public IReadOnlyList<string> Commands => File.ReadAllLines (Path.Combine (directory, "adb.log"));

			public bool RunFastDeploy ()
			{
				var foo = new TaskItem (Path.Combine (directory, "Foo.dll"));
				foo.SetMetadata ("TargetPath", "arm64-v8a/Foo.dll");
				var task = new FastDeploy {
					BuildEngine = new MockBuildEngine (TestContext.Out, errors),
					AdbToolPath = directory,
					AppFileTransferMode = "Copy",
					FastDevFiles = new ITaskItem [] { foo },
					IntermediateOutputPath = Path.Combine (directory, "obj") + Path.DirectorySeparatorChar,
					PackageName = PackageName,
					PrimaryCpuAbi = "arm64-v8a",
					UploadFlagFile = Path.Combine (directory, "obj", "upload.flag"),
				};
				return task.Execute ();
			}

			public void Dispose ()
			{
				Directory.Delete (directory, recursive: true);
			}
		}

	}

}
