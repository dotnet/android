using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using NUnit.Framework;
using Xamarin.Android.Tools;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	[Category ("Node-2")]
	[Parallelizable (ParallelScope.Children)]
	public class AndroidDependenciesTests : BaseTest
	{
		[Test]
		[NonParallelizable] // Do not run environment modifying tests in parallel.
		public void InstallAndroidDependenciesTest ()
		{
			AssertCommercialBuild ();
			// We need to grab the latest API level *before* changing env vars
			var apiLevel = AndroidSdkResolver.GetMaxInstalledPlatform ();
			var old = Environment.GetEnvironmentVariable ("ANDROID_SDK_PATH");
			try {
				string sdkPath = Path.Combine (Root, "temp", TestName, "android-sdk");
				Environment.SetEnvironmentVariable ("ANDROID_SDK_PATH", sdkPath);
				if (Directory.Exists (sdkPath))
					Directory.Delete (sdkPath, true);
				Directory.CreateDirectory (sdkPath);
				var proj = new XamarinAndroidApplicationProject {
					TargetSdkVersion = apiLevel.ToString (),
				};
				using (var b = CreateApkBuilder ()) {
					b.CleanupAfterSuccessfulBuild = false;
					string defaultTarget = b.Target;
					b.Target = "InstallAndroidDependencies";
					Assert.IsTrue (b.Build (proj, parameters: new string [] { "AcceptAndroidSDKLicenses=true" }), "InstallAndroidDependencies should have succeeded.");
					b.Target = defaultTarget;
					Assert.IsTrue (b.Build (proj, true), "build should have succeeded.");
					Assert.IsTrue (b.LastBuildOutput.ContainsText ($"Output Property: _AndroidSdkDirectory={sdkPath}"), "_AndroidSdkDirectory was not set to new SDK path.");
					Assert.IsTrue (b.LastBuildOutput.ContainsText ($"JavaPlatformJarPath={sdkPath}"), "JavaPlatformJarPath did not contain new SDK path.");
				}
			} finally {
				Environment.SetEnvironmentVariable ("ANDROID_SDK_PATH", old);
			}
		}

		[Test]
		[TestCase ("AotAssemblies", false)]
		[TestCase ("AndroidEnableProfiledAot", false)]
		[TestCase ("EnableLLVM", true)]
		[TestCase ("BundleAssemblies", true)]
		public void GetDependencyNdkRequiredConditions (string property, bool ndkRequired)
		{
			var proj = new XamarinAndroidApplicationProject ();
			proj.AotAssemblies = true;
			proj.SetProperty (property, "true");
			using (var builder = CreateApkBuilder ()) {
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
				TargetFrameworkVersion = "v8.0",
				TargetSdkVersion = "26",
				UseLatestPlatformSdk = false,
			};
			var parameters = new string [] {
				$"TargetFrameworkRootPath={referencesPath}",
				$"AndroidSdkDirectory={androidSdkPath}",
			};
			string buildToolsVersion = GetExpectedBuildToolsVersion ();
			using (var builder = CreateApkBuilder (Path.Combine (path, proj.ProjectName), cleanupAfterSuccessfulBuild: false, cleanupOnDispose: false)) {
				builder.ThrowOnBuildFailure = false;
				builder.Target = "GetAndroidDependencies";
				Assert.True (builder.Build (proj, parameters: parameters),
					string.Format ("First Build should have succeeded"));
				int apiLevel = Builder.UseDotNet ? XABuildConfig.AndroidDefaultTargetDotnetApiLevel : 26;
				StringAssertEx.Contains ($"platforms/android-{apiLevel}", builder.LastBuildOutput, $"platforms/android-{apiLevel} should be a dependency.");
				StringAssertEx.Contains ($"build-tools/{buildToolsVersion}", builder.LastBuildOutput, $"build-tools/{buildToolsVersion} should be a dependency.");
				StringAssertEx.Contains ("platform-tools", builder.LastBuildOutput, "platform-tools should be a dependency.");
			}
		}

		[Test]
		public void GetDependencyWhenSDKIsMissingTest ([Values (true, false)] bool createSdkDirectory)
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
				TargetFrameworkVersion = "v8.0",
				TargetSdkVersion = "26",
				UseLatestPlatformSdk = false,
			};
			var parameters = new string [] {
				$"TargetFrameworkRootPath={referencesPath}",
				$"AndroidSdkDirectory={androidSdkPath}",
			};

			string buildToolsVersion = GetExpectedBuildToolsVersion ();
			using (var builder = CreateApkBuilder (Path.Combine (path, proj.ProjectName), cleanupAfterSuccessfulBuild: false, cleanupOnDispose: false)) {
				builder.ThrowOnBuildFailure = false;
				builder.Target = "GetAndroidDependencies";
				Assert.True (builder.Build (proj, parameters: parameters),
					string.Format ("First Build should have succeeded"));
				int apiLevel = Builder.UseDotNet ? XABuildConfig.AndroidDefaultTargetDotnetApiLevel : 26;
				StringAssertEx.Contains ($"platforms/android-{apiLevel}", builder.LastBuildOutput, $"platforms/android-{apiLevel} should be a dependency.");
				StringAssertEx.Contains ($"build-tools/{buildToolsVersion}", builder.LastBuildOutput, $"build-tools/{buildToolsVersion} should be a dependency.");
				StringAssertEx.Contains ("platform-tools", builder.LastBuildOutput, "platform-tools should be a dependency.");
			}
		}

		static readonly XNamespace MSBuildXmlns = "http://schemas.microsoft.com/developer/msbuild/2003";

		static string GetExpectedBuildToolsVersion ()
		{
			var propsPath = Path.Combine (XABuildPaths.TopDirectory, "src", "Xamarin.Android.Build.Tasks", "Xamarin.Android.Common.props.in");
			var props = XElement.Load (propsPath);
			var AndroidSdkBuildToolsVersion = props.Elements (MSBuildXmlns + "PropertyGroup")
				.Elements (MSBuildXmlns + "AndroidSdkBuildToolsVersion")
				.FirstOrDefault ();
			return AndroidSdkBuildToolsVersion?.Value?.Trim ();
		}
	}
}
