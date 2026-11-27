using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Xml.Linq;
using NUnit.Framework;
using Microsoft.Android.Build.Tasks;
using Xamarin.Android.Tasks;
using Xamarin.Android.Tools;
using Xamarin.ProjectTools;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xamarin.Installer.Build.Tasks;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	[Parallelizable (ParallelScope.Children)]
	public class AndroidDependenciesTests : BaseTest
	{
		[Test]
		[NonParallelizable] // Do not run environment modifying tests in parallel.
		public void InstallAndroidDependenciesTest ([Values (AndroidRuntime.CoreCLR, AndroidRuntime.NativeAOT)] AndroidRuntime runtime)
		{
			bool isRelease = runtime == AndroidRuntime.NativeAOT;
			if (IgnoreUnsupportedConfiguration (runtime, release: isRelease)) {
				return;
			}

			var oldSdkPath = Environment.GetEnvironmentVariable ("TEST_ANDROID_SDK_PATH");
			var oldJdkPath = Environment.GetEnvironmentVariable ("TEST_ANDROID_JDK_PATH");

			try {
				string sdkPath = Path.Combine (Root, "temp", TestName, "android-sdk");
				string jdkPath = Path.Combine (Root, "temp", TestName, "android-jdk");
				Environment.SetEnvironmentVariable ("TEST_ANDROID_SDK_PATH", sdkPath);
				Environment.SetEnvironmentVariable ("TEST_ANDROID_JDK_PATH", jdkPath);

				var proj = new XamarinAndroidApplicationProject {
					IsRelease = isRelease,
				};
				proj.SetRuntime (runtime);
				var buildArgs = new List<string> {
					"AcceptAndroidSDKLicenses=true",

					// This integration test downloads the SDK, JDK and (for NativeAOT) NDK from scratch.
					"AndroidDependencyInstallationTimeout=30",
				};

				using (var b = CreateApkBuilder ()) {
					b.Verbosity = LoggerVerbosity.Detailed;
					b.CleanupAfterSuccessfulBuild = false;
					b.ThrowOnBuildFailure = false;
					string defaultTarget = b.Target;
					b.Target = "InstallAndroidDependencies";
					b.BuildLogFile = "install-deps.log";

					if (runtime == AndroidRuntime.NativeAOT) {
						Directory.CreateDirectory (sdkPath);
						Directory.CreateDirectory (jdkPath);
						var timeoutArgs = buildArgs
							.Where (arg => !arg.StartsWith ("AndroidDependencyInstallationTimeout=", StringComparison.Ordinal))
							.Concat (["AndroidDependencyInstallationTimeout=0"])
							.ToArray ();
						Assert.IsFalse (b.Build (proj, parameters: timeoutArgs), "A timed-out dependency installation must not report success.");
						Assert.IsTrue (b.LastBuildOutput.ContainsText ("timed out or was cancelled"),
							"Timeout must be diagnosed before callers try to use missing installed files.");
					}

					// InstallAndroidDependencies downloads the Android SDK and JDK over the network, which
					// can fail intermittently in CI. Retry a few times before giving up, starting from a
					// clean SDK/JDK directory each attempt so a partial download does not affect the next.
					// See https://github.com/dotnet/android/issues/11973
					const int maxInstallAttempts = 3;
					bool installSucceeded = false;
					for (int attempt = 1; attempt <= maxInstallAttempts; attempt++) {
						foreach (var path in new [] { sdkPath, jdkPath }) {
							if (Directory.Exists (path))
								Directory.Delete (path, recursive: true);
							Directory.CreateDirectory (path);
						}


						if (b.Build (proj, parameters: buildArgs.ToArray ())) {
							installSucceeded = true;
							break;
						}

						TestContext.WriteLine ($"InstallAndroidDependencies attempt {attempt} of {maxInstallAttempts} failed. Please check the task output in 'install-deps.log'.");
						if (attempt < maxInstallAttempts)
							Thread.Sleep (TimeSpan.FromSeconds (10));
					}
					Assert.IsTrue (installSucceeded, $"InstallAndroidDependencies should have succeeded within {maxInstallAttempts} attempts.");

					FileAssert.Exists (Path.Combine (sdkPath, "cmdline-tools", "21.0", "source.properties"));

					// When a package is not installed, a warning is present in the build output:
					//    The Android CLI did not install Android SDK package `platform-tools` into `...`.
					var depFailedMessage = "The Android CLI did not install Android SDK package";
					if (b.LastBuildOutput.ContainsText (depFailedMessage)) {
						var sb = new StringBuilder ();
						foreach (var line in b.LastBuildOutput) {
							if (line.Contains (depFailedMessage)) {
								sb.AppendLine (line);
							}
						}
						Assert.Fail ($"A required dependency was not installed, warnings are listed below. Please check the task output in 'install-deps.log'.\n{sb.ToString ()}");
					}

					b.Target = defaultTarget;
					b.BuildLogFile = "build.log";
					Assert.IsTrue (b.Build (proj, true), "build should have succeeded.");
					Assert.IsTrue ( b.LastBuildOutput.ContainsText ($"Output Property: _AndroidSdkDirectory={sdkPath}"),
						$"_AndroidSdkDirectory was not set to new SDK path `{sdkPath}`. Please check the task output in 'install-deps.log'");
					Assert.IsTrue (b.LastBuildOutput.ContainsText ($"Output Property: _JavaSdkDirectory={jdkPath}"),
						$"_JavaSdkDirectory was not set to new JDK path `{jdkPath}`. Please check the task output in 'install-deps.log'");
					Assert.IsTrue (b.LastBuildOutput.ContainsText ($"JavaPlatformJarPath={sdkPath}"),
						$"JavaPlatformJarPath did not contain new SDK path `{sdkPath}`. Please check the task output in 'install-deps.log'");
				}
			} finally {
				Environment.SetEnvironmentVariable ("TEST_ANDROID_SDK_PATH", oldSdkPath);
				Environment.SetEnvironmentVariable ("TEST_ANDROID_JDK_PATH", oldJdkPath);
			}
		}

		[TestCase (AndroidRuntime.CoreCLR, false, false, false)]
		[TestCase (AndroidRuntime.CoreCLR, false, true, true)]
		[TestCase (AndroidRuntime.NativeAOT, true, false, true)]
		[TestCase (AndroidRuntime.NativeAOT, false, false, false)]
		[TestCase (AndroidRuntime.NativeAOT, false, true, true)]
		public void NdkDependencyUsesEffectiveRuntime (AndroidRuntime runtime, bool isRelease, bool stripNativeLibraries, bool ndkRequired)
		{
			var proj = new XamarinAndroidApplicationProject ();
			proj.SetRuntime (runtime);
			// PublishAot remains true in Debug, but the effective runtime is CoreCLR.
			proj.IsRelease = isRelease;
			proj.SetProperty ("AndroidStripNativeLibraries", stripNativeLibraries.ToString ());
			proj.SetProperty ("_SkipNdkResolution", "true");
			using (var builder = CreateApkBuilder ()) {
				builder.Verbosity = LoggerVerbosity.Detailed;
				builder.Target = "GetAndroidDependencies";
				Assert.IsTrue (builder.Build (proj), "Dependency discovery should succeed without an installed NDK.");
				IEnumerable<string> taskOutput = builder.LastBuildOutput
					.Select (x => x.Trim ())
					.SkipWhile (x => !x.StartsWith ("Task \"CalculateProjectDependencies\"", StringComparison.Ordinal))
					.SkipWhile (x => !x.StartsWith ("Output Item(s):", StringComparison.Ordinal))
					.TakeWhile (x => !x.StartsWith ("Done executing task \"CalculateProjectDependencies\"", StringComparison.Ordinal))
					.ToArray ();
				Assert.IsNotEmpty (taskOutput, "CalculateProjectDependencies should log its output items.");
				if (ndkRequired) {
					StringAssertEx.Contains ("ndk-bundle", taskOutput, "NativeAOT and optional native library stripping require the NDK.");
				} else {
					StringAssertEx.DoesNotContain ("ndk-bundle", taskOutput, "Ordinary CoreCLR builds should not require the NDK.");
				}
			}
		}

		[Test]
		public void GetDependencyWhenBuildToolsAreMissingTest ([Values (AndroidRuntime.CoreCLR, AndroidRuntime.NativeAOT)] AndroidRuntime runtime)
		{
			const bool isRelease = true;
			if (IgnoreUnsupportedConfiguration (runtime, release: isRelease)) {
				return;
			}

			var apis = new ApiInfo [] {
			};
			var path = Path.Combine ("temp", TestName);
			var androidSdkPath = CreateFauxAndroidSdkDirectory (Path.Combine (path, "android-sdk"),
					null, apis);
			var referencesPath = CreateFauxReferencesDirectory (Path.Combine (path, "xbuild-frameworks"), apis);
			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = isRelease,
				TargetSdkVersion = "26",
			};
			proj.SetRuntime (runtime);
			var parameters = new string [] {
				$"TargetFrameworkRootPath={referencesPath}",
				$"AndroidSdkDirectory={androidSdkPath}",
			};
			string buildToolsVersion = GetExpectedBuildToolsVersion ();
			using (var builder = CreateApkBuilder (Path.Combine (path, proj.ProjectName), cleanupAfterSuccessfulBuild: false, cleanupOnDispose: false)) {
				builder.Verbosity = LoggerVerbosity.Detailed;
				builder.ThrowOnBuildFailure = false;
				builder.Target = "GetAndroidDependencies";
				Assert.True (builder.Build (proj, parameters: parameters),
					string.Format ("First Build should have succeeded"));
				var apiLevel = XABuildConfig.AndroidDefaultTargetDotnetApiLevel;
				StringAssertEx.Contains (
						anyOf: new [] { $"platforms/android-{apiLevel}", $"platforms/android-{apiLevel.Major}" },
						collection: builder.LastBuildOutput,
						message: $"platforms/android-{apiLevel} should be a dependency.");
				StringAssertEx.Contains ($"build-tools/{buildToolsVersion}", builder.LastBuildOutput, $"build-tools/{buildToolsVersion} should be a dependency.");
				StringAssertEx.Contains ("platform-tools", builder.LastBuildOutput, "platform-tools should be a dependency.");
			}
		}

		[Test]
		public void GetDependencyWhenSDKIsMissingTest ([Values] bool createSdkDirectory, [Values] bool installJavaDeps, [Values (AndroidRuntime.CoreCLR, AndroidRuntime.NativeAOT)] AndroidRuntime runtime)
		{
			const bool isRelease = true;
			if (IgnoreUnsupportedConfiguration (runtime, release: isRelease)) {
				return;
			}

			var apis = new ApiInfo [] {
			};
			var path = Path.Combine ("temp", TestName);
			var androidSdkPath = Path.Combine (path, "android-sdk");
			if (createSdkDirectory)
				Directory.CreateDirectory (androidSdkPath);
			else if (Directory.Exists (androidSdkPath))
				Directory.Delete (androidSdkPath, recursive: true);
			var referencesPath = CreateFauxReferencesDirectory (Path.Combine (path, "xbuild-frameworks"), apis);
			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = isRelease,
				TargetSdkVersion = "26",
			};
			var requestedJdkVersion = "17.0.8.1";
			var parameters = new string [] {
				$"TargetFrameworkRootPath={referencesPath}",
				$"AndroidSdkDirectory={androidSdkPath}",
				$"JavaSdkVersion={requestedJdkVersion}",
				$"AndroidInstallJavaDependencies={installJavaDeps}",
			};

			string buildToolsVersion = GetExpectedBuildToolsVersion ();
			using (var builder = CreateApkBuilder (Path.Combine (path, proj.ProjectName), cleanupAfterSuccessfulBuild: false, cleanupOnDispose: false)) {
				builder.Verbosity = LoggerVerbosity.Detailed;
				builder.ThrowOnBuildFailure = false;
				builder.Target = "GetAndroidDependencies";
				Assert.True (builder.Build (proj, parameters: parameters),
					string.Format ("First Build should have succeeded"));
				var apiLevel = XABuildConfig.AndroidDefaultTargetDotnetApiLevel;
				StringAssertEx.Contains (
						anyOf: new [] { $"platforms/android-{apiLevel}", $"platforms/android-{apiLevel.Major}" },
						collection: builder.LastBuildOutput,
						message: $"platforms/android-{apiLevel} should be a dependency.");
				StringAssertEx.Contains ($"build-tools/{buildToolsVersion}", builder.LastBuildOutput, $"build-tools/{buildToolsVersion} should be a dependency.");
				StringAssertEx.Contains ("platform-tools", builder.LastBuildOutput, "platform-tools should be a dependency.");
				if (installJavaDeps)
					StringAssertEx.ContainsRegex ($@"JavaDependency=\s*jdk\s*Version={requestedJdkVersion}", builder.LastBuildOutput, $"jdk {requestedJdkVersion} should be a dependency.");
				else
					StringAssertEx.DoesNotContainRegex ($@"JavaDependency=\s*jdk\s*Version={requestedJdkVersion}", builder.LastBuildOutput, $"jdk {requestedJdkVersion} should not be a dependency.");
			}
		}

		[Test]
		public void InstallAndroidDependenciesPassesPackagesToAndroidCli ()
		{
			var root = Path.Combine (Root, "temp", TestName);
			if (Directory.Exists (root))
				Directory.Delete (root, recursive: true);
			Directory.CreateDirectory (root);

			var cli = CreateCatalogCli (root, """
				Available packages:
				  platform-tools  38.0.0  Android SDK Platform-Tools
				  platform-tools  37.0.10  Android SDK Platform-Tools
				  platform-tools  37.0.3  Android SDK Platform-Tools
				""");

			var errors = new List<BuildErrorEventArgs> ();
			var task = new InstallAndroidDependencies {
				BuildEngine = new MockBuildEngine (TestContext.Out, errors, [], []),
				AndroidSdkPath = Path.Combine (root, "sdk"),
				AcceptAndroidSDKLicenses = true,
				InstallJavaDependencies = false,
				AndroidCliPath = cli,
				JavaDependencies = [],
				Dependencies = [
					new TaskItem ("build-tools/37.0.0", new Dictionary<string, string> { { "Version", "37.0.0" } }),
					new TaskItem ("platform-tools", new Dictionary<string, string> { { "Version", "37.0.3" } }),
					new TaskItem ("ndk-bundle", new Dictionary<string, string> { { "Version", "27.2.12479018" } }),
					new TaskItem ("platforms/android-37.0"),
					new TaskItem ("cmdline-tools/latest", new Dictionary<string, string> { { "Version", "latest" } }),
				],
			};
			Assert.IsTrue (task.Execute (), string.Join (Environment.NewLine, errors.Select (e => e.Message)));

			var args = File.ReadAllText (Path.Combine (root, "args.txt")).Trim ();
			StringAssert.Contains ($"--no-metrics --sdk={Path.Combine (root, "sdk")} sdk install", args);
			StringAssert.EndsWith ("build-tools/37.0.0 platform-tools@37.0.10 ndk/27.2.12479018 platforms/android-37.0 cmdline-tools/latest", args);
			StringAssert.EndsWith ("sdk list --all --all-versions platform-tools",
				File.ReadAllText (Path.Combine (root, "list-args.txt")).Trim ());
		}

		[TestCase ("37.0", "37.0.3", "37.0.3", null, null, TestName = "PlatformToolsLatestAlreadyInstalled")]
		[TestCase ("37.0", "37.0.1", "37.0.3", "platform-tools@37.0.3", null, TestName = "PlatformToolsRollsForwardWithinMajor")]
		[TestCase ("37.0", "38.0.0", "37.0.3", null, null, TestName = "PlatformToolsNewerMajorInstalled")]
		[TestCase ("37.0", "37.0.3-rc1", "37.0.3", "platform-tools@37.0.3", null, TestName = "PlatformToolsPreviewInstalled")]
		[TestCase ("37.0", null, null, null, "XA5310", TestName = "PlatformToolsNoCompatibleVersion")]
		[TestCase ("37.0", "37.0.3", null, null, "XA5310", TestName = "PlatformToolsInstalledButNotInCatalog")]
		[TestCase ("37.5", null, "37.0.3", null, "XA5310", TestName = "PlatformToolsNoSufficientVersion")]
		[TestCase ("38.0.0", null, "38.0.2", "platform-tools@38.0.2", null, TestName = "PlatformToolsRequestedMajor")]
		[TestCase ("", null, "37.0.3", "platform-tools", null, TestName = "PlatformToolsUnversionedNotInstalled")]
		[TestCase ("", "36.0.0", "37.0.3", null, null, TestName = "PlatformToolsUnversionedInstalled")]
		[TestCase ("latest", null, "37.0.3", null, "XA5311", TestName = "PlatformToolsMalformedVersion")]
		[TestCase ("37.0.3oops", null, "37.0.3", null, "XA5311", TestName = "PlatformToolsMalformedVersionSuffix")]
		[TestCase ("37.0.3...", null, "37.0.3", null, "XA5311", TestName = "PlatformToolsMalformedVersionTrailingDots")]
		public void PlatformToolsRollForward (string requested, string installed, string available, string expectedInstall, string expectedError)
		{
			var root = Path.Combine (Root, "temp", TestName);
			if (Directory.Exists (root))
				Directory.Delete (root, recursive: true);
			Directory.CreateDirectory (root);
			var sdkPath = Path.Combine (root, "sdk");
			if (installed != null) {
				var directory = Path.Combine (sdkPath, "platform-tools");
				Directory.CreateDirectory (directory);
				File.WriteAllText (Path.Combine (directory, "source.properties"), $"Pkg.Revision={installed}\n");
			}
			// The installed section is ignored: only versions available in the catalog can be selected.
			var catalog = $"""
				Installed packages:
				  platform-tools  {installed ?? "unknown"}  Android SDK Platform-Tools
				Available packages:
				  platform-tools  38.0.0  Android SDK Platform-Tools
				  platform-tools  {available ?? "37.1.0-rc1"}  Android SDK Platform-Tools
				""";
			var errors = new List<BuildErrorEventArgs> ();
			var task = new InstallAndroidDependencies {
				BuildEngine = new MockBuildEngine (TestContext.Out, errors, [], []),
				AndroidSdkPath = sdkPath,
				AcceptAndroidSDKLicenses = true,
				InstallJavaDependencies = false,
				AndroidCliPath = CreateCatalogCli (root, catalog),
				JavaDependencies = [],
				Dependencies = [new TaskItem ("platform-tools", new Dictionary<string, string> { { "Version", requested } })],
			};
			Assert.AreEqual (expectedError == null, task.Execute (), string.Join (Environment.NewLine, errors.Select (e => e.Message)));
			if (expectedError != null) {
				var error = errors.Single ();
				Assert.AreEqual (expectedError, error.Code);
				if (expectedError == "XA5310")
					StringAssert.Contains ($"version {requested} or later in the supported {requested.Split ('.') [0]}.x", error.Message);
				else
					StringAssert.Contains ($"'{requested}'", error.Message);
			}
			var argsPath = Path.Combine (root, "args.txt");
			Assert.AreEqual (expectedInstall != null, File.Exists (argsPath));
			if (expectedInstall != null)
				StringAssert.EndsWith ($"sdk install {expectedInstall}", File.ReadAllText (argsPath).Trim ());
			// Unversioned or invalid requests never query the catalog.
			Assert.AreEqual (requested.Length > 0 && expectedError != "XA5311", File.Exists (Path.Combine (root, "list-args.txt")));
		}

		[TestCase (false)]
		[TestCase (true)]
		public void PlatformToolsCatalogSelectsLatestStableVersion (bool pipes)
		{
			var catalog = """
				Installed packages:
				  platform-tools | 37.9.0 | Android SDK Platform-Tools
				Available packages:
				  platform-tools | 38.0.0 | Android SDK Platform-Tools
				  platform-tools | 37.0.3 | Android SDK Platform-Tools
				  platform-tools | 37.2.0 | Android SDK Platform-Tools
				  platform-tools | 37.0.10 | Android SDK Platform-Tools
				  platform-tools | 37.3.0-rc1 | Android SDK Platform-Tools
				  platform-tools | invalid | Android SDK Platform-Tools
				  build-tools/37.4.0 | 37.4.0 | Android SDK Build-Tools
				""";
			if (!pipes)
				catalog = catalog.Replace (" | ", "  ");
			Assert.AreEqual ("37.2.0", InstallAndroidDependencies.SelectPlatformToolsVersion (catalog, new Version (37, 0)));
			Assert.AreEqual ("37.2.0", InstallAndroidDependencies.SelectPlatformToolsVersion (catalog, new Version (37, 2, 0)));
			Assert.IsNull (InstallAndroidDependencies.SelectPlatformToolsVersion (catalog, new Version (37, 2, 1)));
		}

		[Test]
		public void PlatformToolsCatalogFailureStopsInstallation ()
		{
			var root = Path.Combine (Root, "temp", TestName);
			if (Directory.Exists (root))
				Directory.Delete (root, recursive: true);
			Directory.CreateDirectory (root);
			var errors = new List<BuildErrorEventArgs> ();
			var task = new InstallAndroidDependencies {
				BuildEngine = new MockBuildEngine (TestContext.Out, errors, [], []),
				AndroidSdkPath = Path.Combine (root, "sdk"),
				AcceptAndroidSDKLicenses = true,
				InstallJavaDependencies = false,
				AndroidCliPath = CreateCatalogCli (root, "Available packages:\n  platform-tools  37.0.3  Android SDK Platform-Tools", listExitCode: 7),
				JavaDependencies = [],
				Dependencies = [new TaskItem ("platform-tools", new Dictionary<string, string> { { "Version", "37.0" } })],
			};
			Assert.IsFalse (task.Execute ());
			Assert.AreEqual ("XA5309", errors.Single ().Code);
			StringAssert.Contains ("7", errors.Single ().Message);
			StringAssert.Contains ("catalog failed", errors.Single ().Message);
			StringAssert.Contains ("Available packages:", errors.Single ().Message);
			Assert.IsFalse (File.Exists (Path.Combine (root, "args.txt")));
		}

		[TestCase ("37.0.3", true, TestName = "PlatformToolsInstalledWithoutLicenses")]
		[TestCase ("37.0.1", false, TestName = "PlatformToolsUpgradeRequiresLicenses")]
		public void PlatformToolsLicenses (string installed, bool expectedSuccess)
		{
			var root = Path.Combine (Root, "temp", TestName);
			if (Directory.Exists (root))
				Directory.Delete (root, recursive: true);
			var sdkPath = Path.Combine (root, "sdk");
			var directory = Path.Combine (sdkPath, "platform-tools");
			Directory.CreateDirectory (directory);
			File.WriteAllText (Path.Combine (directory, "source.properties"), $"Pkg.Revision={installed}\n");
			var errors = new List<BuildErrorEventArgs> ();
			var task = new InstallAndroidDependencies {
				BuildEngine = new MockBuildEngine (TestContext.Out, errors, [], []),
				AndroidSdkPath = sdkPath,
				AcceptAndroidSDKLicenses = false,
				InstallJavaDependencies = false,
				AndroidCliPath = CreateCatalogCli (root, "Available packages:\n  platform-tools  37.0.3  Android SDK Platform-Tools"),
				JavaDependencies = [],
				Dependencies = [new TaskItem ("platform-tools", new Dictionary<string, string> { { "Version", "37.0" } })],
			};
			Assert.AreEqual (expectedSuccess, task.Execute (), string.Join (Environment.NewLine, errors.Select (e => e.Message)));
			if (!expectedSuccess)
				StringAssert.Contains ("AcceptAndroidSDKLicenses", errors.Single ().Message);
			Assert.IsFalse (File.Exists (Path.Combine (root, "args.txt")));
		}

		static string CreateCatalogCli (string root, string catalog, int listExitCode = 0)
		{
			File.WriteAllText (Path.Combine (root, "catalog.txt"), catalog);
			string cli;
			if (OperatingSystem.IsWindows ()) {
				cli = Path.Combine (root, "android.cmd");
				File.WriteAllText (cli, $"""
					@echo off
					rem cmd.exe splits --sdk=PATH at '=', so search for the "list" token instead of using a fixed position.
					echo %* | findstr /c:" list " >nul
					if not errorlevel 1 (
					  echo %* > "%~dp0list-args.txt"
					  type "%~dp0catalog.txt"
					  echo catalog failed 1>&2
					  exit /b {listExitCode}
					)
					echo %* > "%~dp0args.txt"
					""" + "\r\n");
			} else {
				cli = Path.Combine (root, "android");
				File.WriteAllText (cli, $$"""
					#!/bin/sh
					case " $* " in
					*" list "*)
					  echo "$@" > "$(dirname "$0")/list-args.txt"
					  cat "$(dirname "$0")/catalog.txt"
					  echo "catalog failed" >&2
					  exit {{listExitCode}}
					  ;;
					esac
					echo "$@" > "$(dirname "$0")/args.txt"
					""" + "\n");
				File.SetUnixFileMode (cli, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
			}
			return cli;
		}

		[Test]
		public async System.Threading.Tasks.Task UnknownJdkVersionIsNotValid ()
		{
			// aka.ms sends an unknown link to an HTML page on bing.com with status 200.
			using (var client = new HttpClient (new HtmlHandler ())) {
				var archive = await new JavaDependencyInstaller ().GetFirstValidJdkArchiveWithVersion (client, "21.0.99", CancellationToken.None);
				Assert.IsNull (archive);
			}
		}

		sealed class HtmlHandler : HttpMessageHandler
		{
			protected override System.Threading.Tasks.Task<HttpResponseMessage> SendAsync (HttpRequestMessage request, CancellationToken cancellationToken) =>
				System.Threading.Tasks.Task.FromResult (new HttpResponseMessage (HttpStatusCode.OK) {
					Content = new StringContent ("<html></html>", Encoding.UTF8, "text/html"),
				});
		}

		static readonly XNamespace MSBuildXmlns = "http://schemas.microsoft.com/developer/msbuild/2003";

		static string GetExpectedBuildToolsVersion ()
		{
			return XABuildConfig.AndroidSdkBuildToolsVersion;
		}
	}
}
