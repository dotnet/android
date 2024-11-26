using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using NUnit.Framework;
using Xamarin.Android.Tasks;
using Xamarin.Android.Tools;
using Xamarin.ProjectTools;
using Microsoft.Build.Framework;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	[Parallelizable (ParallelScope.Children)]
	public class AndroidDependenciesTests : BaseTest
	{
		[Test]
		[NonParallelizable] // Do not run environment modifying tests in parallel.
		public void InstallAndroidDependenciesTest ([Values ("GoogleV2", "Xamarin")] string manifestType)
		{
			// Set to true when we are marking a new Android API level as stable, but it has not
			// been added to the Xamarin manifest yet.
			var xamarin_manifest_needs_updating = false;

			AssertCommercialBuild ();
			var oldSdkPath = Environment.GetEnvironmentVariable ("TEST_ANDROID_SDK_PATH");
			var oldJdkPath = Environment.GetEnvironmentVariable ("TEST_ANDROID_JDK_PATH");
			try {
				string sdkPath = Path.Combine (Root, "temp", TestName, "android-sdk");
				string jdkPath = Path.Combine (Root, "temp", TestName, "android-jdk");
				Environment.SetEnvironmentVariable ("TEST_ANDROID_SDK_PATH", sdkPath);
				Environment.SetEnvironmentVariable ("TEST_ANDROID_JDK_PATH", jdkPath);
				foreach (var path in new [] { sdkPath, jdkPath }) {
					if (Directory.Exists (path))
						Directory.Delete (path, recursive: true);
					Directory.CreateDirectory (path);
				}

				var proj = new XamarinAndroidApplicationProject ();
				var buildArgs = new List<string> {
					"AcceptAndroidSDKLicenses=true",
					$"AndroidManifestType={manifestType}",
				};
				// When using the default Xamarin manifest, this test should fail if we can't install any of the defaults in Xamarin.Installer.Common.props
				// When using the Google manifest, override the platform tools version to the one in their manifest as it only ever contains one version
				if (manifestType == "GoogleV2") {
					buildArgs.Add ($"AndroidSdkPlatformToolsVersion={GetCurrentPlatformToolsVersion ()}");
				}

				using (var b = CreateApkBuilder ()) {
					b.Verbosity = LoggerVerbosity.Detailed;
					b.CleanupAfterSuccessfulBuild = false;
					string defaultTarget = b.Target;
					b.Target = "InstallAndroidDependencies";
					b.BuildLogFile = "install-deps.log";
					Assert.IsTrue (b.Build (proj, parameters: buildArgs.ToArray ()), "InstallAndroidDependencies should have succeeded.");

					// When dependencies can not be resolved/installed a warning will be present in build output:
					//    Dependency `platform-tools` should have been installed but could not be resolved.
					var depFailedMessage = "should have been installed but could not be resolved";
					bool failedToInstall = b.LastBuildOutput.ContainsText (depFailedMessage);

					// If we don't think the Xamarin manifest has been updated to contain the new API level:
					// - Don't error if we got the expected failure
					// - Error if didn't get a failure, because we need to update this test
					if (manifestType == "Xamarin" && xamarin_manifest_needs_updating) {
						if (!failedToInstall)
							Assert.Fail ("We didn't expect the Xamarin manifest to have the requested component. If the manifest has been updated, change 'InstallAndroidDependenciesTest.xamarin_manifest_needs_updating' to be 'false'. ");

						return;
					}

					if (failedToInstall) {
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

		static string GetCurrentPlatformToolsVersion ()
		{
			var s = new XmlReaderSettings {
				XmlResolver = null,
			};
			var r = XmlReader.Create ("https://dl-ssl.google.com/android/repository/repository2-3.xml", s);
			var d = XDocument.Load (r);

			var platformToolsPackage    = d.Root.Elements ("remotePackage")
				.Where (e => "platform-tools" == (string) e.Attribute("path"))
				.FirstOrDefault ();

			var revision    = platformToolsPackage.Element ("revision");

			return $"{revision.Element ("major")?.Value}.{revision.Element ("minor")?.Value}.{revision.Element ("micro")?.Value}";
		}

		[Test]
		[TestCase ("AotAssemblies", false)]
		[TestCase ("AndroidEnableProfiledAot", false)]
		[TestCase ("EnableLLVM", true)]
		public void GetDependencyNdkRequiredConditions (string property, bool ndkRequired)
		{
			var proj = new XamarinAndroidApplicationProject ();
			proj.AotAssemblies = true;
			proj.SetProperty (property, "true");
			using (var builder = CreateApkBuilder ()) {
				builder.Verbosity = LoggerVerbosity.Detailed;
				builder.Target = "GetAndroidDependencies";
				Assert.IsTrue (builder.Build (proj), "Build should have succeeded.");
				IEnumerable<string> taskOutput = builder.LastBuildOutput
					.Select (x => x.Trim ())
					.SkipWhile (x => !x.StartsWith ("Task \"CalculateProjectDependencies\"", StringComparison.Ordinal))
					.SkipWhile (x => !x.StartsWith ("Output Item(s):", StringComparison.Ordinal))
					.TakeWhile (x => !x.StartsWith ("Done executing task \"CalculateProjectDependencies\"", StringComparison.Ordinal));
				if (ndkRequired)
					StringAssertEx.Contains ("ndk-bundle", taskOutput, "ndk-bundle should be a dependency.");
				else
					StringAssertEx.DoesNotContain ("ndk-bundle", taskOutput, "ndk-bundle should not be a dependency.");
			}
		}

		[Test]
		public void GetDependencyWhenBuildToolsAreMissingTest ()
		{
			var apis = new ApiInfo [] {
			};
			var path = Path.Combine ("temp", TestName);
			var androidSdkPath = CreateFauxAndroidSdkDirectory (Path.Combine (path, "android-sdk"),
					null, apis);
			var referencesPath = CreateFauxReferencesDirectory (Path.Combine (path, "xbuild-frameworks"), apis);
			var proj = new XamarinAndroidApplicationProject () {
				IsRelease = true,
				TargetSdkVersion = "26",
			};
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
				int apiLevel = XABuildConfig.AndroidDefaultTargetDotnetApiLevel;
				StringAssertEx.Contains ($"platforms/android-{apiLevel}", builder.LastBuildOutput, $"platforms/android-{apiLevel} should be a dependency.");
				StringAssertEx.Contains ($"build-tools/{buildToolsVersion}", builder.LastBuildOutput, $"build-tools/{buildToolsVersion} should be a dependency.");
				StringAssertEx.Contains ("platform-tools", builder.LastBuildOutput, "platform-tools should be a dependency.");
			}
		}

		[Test]
		public void GetDependencyWhenSDKIsMissingTest ([Values (true, false)] bool createSdkDirectory, [Values (true, false)] bool installJavaDeps)
		{
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
				IsRelease = true,
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
				int apiLevel = XABuildConfig.AndroidDefaultTargetDotnetApiLevel;
				StringAssertEx.Contains ($"platforms/android-{apiLevel}", builder.LastBuildOutput, $"platforms/android-{apiLevel} should be a dependency.");
				StringAssertEx.Contains ($"build-tools/{buildToolsVersion}", builder.LastBuildOutput, $"build-tools/{buildToolsVersion} should be a dependency.");
				StringAssertEx.Contains ("platform-tools", builder.LastBuildOutput, "platform-tools should be a dependency.");
				if (installJavaDeps)
					StringAssertEx.ContainsRegex ($@"JavaDependency=\s*jdk\s*Version={requestedJdkVersion}", builder.LastBuildOutput, $"jdk {requestedJdkVersion} should be a dependency.");
				else
					StringAssertEx.DoesNotContainRegex ($@"JavaDependency=\s*jdk\s*Version={requestedJdkVersion}", builder.LastBuildOutput, $"jdk {requestedJdkVersion} should not be a dependency.");
			}
		}

		static readonly XNamespace MSBuildXmlns = "http://schemas.microsoft.com/developer/msbuild/2003";

		static string GetExpectedBuildToolsVersion ()
		{
			return XABuildConfig.AndroidSdkBuildToolsVersion;
		}
	}
}
