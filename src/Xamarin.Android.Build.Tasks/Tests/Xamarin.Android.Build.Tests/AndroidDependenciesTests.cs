using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Xml.Linq;
using NUnit.Framework;
using Microsoft.Android.Build.Tasks;
using Xamarin.Android.Tasks;
using Xamarin.Android.Tools;
using Xamarin.Installer.AndroidSDK;
using Xamarin.Installer.AndroidSDK.Common;
using Xamarin.Installer.AndroidSDK.Manager;
using Xamarin.Installer.Common;
using Xamarin.ProjectTools;
using Microsoft.Build.Framework;
using Xamarin.Tools.Zip;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	[Parallelizable (ParallelScope.Children)]
	public class AndroidDependenciesTests : BaseTest
	{
		[Test]
		[NonParallelizable] // Do not run environment modifying tests in parallel.
		public void InstallAndroidDependenciesTest ([Values ("GoogleV2", "Xamarin")] string manifestType, [Values (AndroidRuntime.CoreCLR, AndroidRuntime.NativeAOT)] AndroidRuntime runtime)
		{
			bool isRelease = runtime == AndroidRuntime.NativeAOT;
			if (IgnoreUnsupportedConfiguration (runtime, release: isRelease)) {
				return;
			}

			// The GoogleV2 manifest from Google doesn't have an ndk-bundle component,
			// so the NDK dependency can't be resolved for runtimes that require it.
			if (manifestType == "GoogleV2" && runtime == AndroidRuntime.NativeAOT) {
				Assert.Ignore ("GoogleV2 manifest does not have an ndk-bundle component for NativeAOT");
			}
			if (manifestType == "GoogleV2") {
				InstallGoogleV2Dependencies ();
				return;
			}

			// Set to true when we are marking a new Android API level as stable, but it has not
			// been added to the Xamarin manifest yet.
			var xamarin_manifest_needs_updating = false;

			var oldSdkPath = Environment.GetEnvironmentVariable ("TEST_ANDROID_SDK_PATH");
			var oldJdkPath = Environment.GetEnvironmentVariable ("TEST_ANDROID_JDK_PATH");
			var outdatedCommandLineToolsRevision = new Version (19, 0);
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
					$"AndroidManifestType={manifestType}",
				};
				var manifestPath = Path.Combine (XABuildPaths.TopDirectory, "src", "Xamarin.Installer.AndroidSDK", "Feeds", "AndroidManifestFeed_d18.0.xml");
				Assert.IsTrue (File.Exists (manifestPath), $"Xamarin manifest does not exist at '{manifestPath}'.");
				buildArgs.Add ($"AndroidManifestSource={manifestPath}");

				using (var b = CreateApkBuilder ()) {
					b.Verbosity = LoggerVerbosity.Detailed;
					b.CleanupAfterSuccessfulBuild = false;
					b.ThrowOnBuildFailure = false;
					string defaultTarget = b.Target;
					b.Target = "InstallAndroidDependencies";
					b.BuildLogFile = "install-deps.log";

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

						var commandLineToolsPath = Path.Combine (sdkPath, "cmdline-tools", "latest");
						Directory.CreateDirectory (commandLineToolsPath);
						File.WriteAllText (Path.Combine (commandLineToolsPath, "source.properties"), $"Pkg.Revision={outdatedCommandLineToolsRevision}");

						if (b.Build (proj, parameters: buildArgs.ToArray ())) {
							installSucceeded = true;
							break;
						}

						TestContext.WriteLine ($"InstallAndroidDependencies attempt {attempt} of {maxInstallAttempts} failed. Please check the task output in 'install-deps.log'.");
						if (attempt < maxInstallAttempts)
							Thread.Sleep (TimeSpan.FromSeconds (10));
					}
					Assert.IsTrue (installSucceeded, $"InstallAndroidDependencies should have succeeded within {maxInstallAttempts} attempts.");

					var sourceProperties = Path.Combine (sdkPath, "cmdline-tools", "latest", "source.properties");
					var revisionProperty = File.ReadLines (sourceProperties)
						.First (line => line.StartsWith ("Pkg.Revision", StringComparison.Ordinal));
					int separator = revisionProperty.IndexOf ('=');
					Assert.GreaterOrEqual (separator, 0, "The command-line tools revision property should contain a value.");
					var installedRevision = Version.Parse (revisionProperty.Substring (separator + 1).Trim ());
					Assert.Greater (installedRevision.CompareTo (outdatedCommandLineToolsRevision), 0, "The outdated command-line tools installation should have been updated.");

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

		void InstallGoogleV2Dependencies ()
		{
			string sdkPath = Path.Combine (Root, "temp", TestName, "android-sdk");
			if (Directory.Exists (sdkPath))
				Directory.Delete (sdkPath, recursive: true);
			Directory.CreateDirectory (sdkPath);

			var fixture = new GoogleV2Fixture ();
			string platformToolsVersion = GetCurrentPlatformToolsVersion (fixture.RepositoryManifest);
			var installer = new AndroidSDKInstaller (
				fixture.Helpers,
				AndroidManifestType.GoogleV2,
				fixture.ManifestUrl,
				fixture.AddonsListUrl,
				fixture.RepositoryBaseUrl);
			installer.Discover (new List<string> { sdkPath });

			var sdk = installer.FindInstance (sdkPath);
			Assert.IsNotNull (sdk, $"The synthetic Android SDK should be discovered at '{sdkPath}'.");

			var requested = new [] {
				(path: $"platforms;android-{GoogleV2Fixture.PlatformVersion}", version: (string) null),
				(path: $"build-tools;{GoogleV2Fixture.BuildToolsVersion}", version: GoogleV2Fixture.BuildToolsVersion),
				(path: "platform-tools", version: platformToolsVersion),
				(path: $"cmdline-tools;{GoogleV2Fixture.CommandLineToolsVersion}", version: GoogleV2Fixture.CommandLineToolsVersion),
			};
			var components = new List<IAndroidComponent> ();
			foreach (var dependency in requested) {
				var version = dependency.version == null ? null : new AndroidRevision (dependency.version);
				var component = sdk.Components.FirstOrDefault (c => c.Path == dependency.path && (version == null || c.Revision == version));
				Assert.IsNotNull (component, $"The synthetic repository should contain '{dependency.path}/{dependency.version}'.");
				components.Add (component);
			}

			var installationSet = installer.GetInstallationSet (sdk, components);
			CollectionAssert.AreEquivalent (
				requested.Select (dependency => dependency.path).Append (GoogleV2Fixture.FixtureDependencyPath),
				installationSet.Select (component => component.Path),
				"The installation set should contain direct and transitive GoogleV2 dependencies.");

			var downloads = installer.GetDownloadItems (installationSet);
			fixture.PrepareDownloads (downloads, Path.Combine (Root, "temp", TestName, "downloads"));
			foreach (var download in downloads)
				Assert.IsTrue (download.IsDownloadValid (), $"Checksum validation should succeed for '{download.Url}'.");

			var licenses = installationSet
				.Select (component => component.License)
				.Where (license => license != null)
				.Distinct ()
				.ToList ();
			Assert.AreEqual (1, licenses.Count, "The synthetic GoogleV2 license should be parsed.");
			Assert.AreEqual ("android-sdk-license", licenses [0].ID);
			installer.Install (sdk, installationSet);

			string javaSdkPath = AndroidSdkResolver.GetJavaSdkPath ();
			Assert.IsTrue (Directory.Exists (javaSdkPath), $"The configured local JDK should exist at '{javaSdkPath}'.");
			installer.AcceptLicensesAsync (sdk, licenses, CancellationToken.None, javaSdkPath, throwsErrorIfValidationFailed: true)
				.GetAwaiter ().GetResult ();
			Assert.IsTrue (File.Exists (Path.Combine (sdkPath, "cmdline-tools", GoogleV2Fixture.CommandLineToolsVersion, "bin", "sdkmanager-invoked.txt")),
				"The synthetic sdkmanager should be invoked to accept licenses.");
			Assert.IsTrue (installer.IsLicenseAccepted (sdk, licenses [0]), "The synthetic GoogleV2 license should be accepted.");
			fixture.AssertInstallation (sdkPath);
		}

		static string GetCurrentPlatformToolsVersion (XDocument manifest)
		{
			var platformToolsPackage = manifest.Root.Elements ("remotePackage")
				.Where (e => "platform-tools" == (string) e.Attribute("path") &&
					"android-sdk-preview-license" != (string) e.Element ("uses-license")?.Attribute ("ref"))
				.FirstOrDefault ();
			Assert.IsNotNull (platformToolsPackage, "The GoogleV2 manifest should contain a stable platform-tools package.");

			var revision    = platformToolsPackage.Element ("revision");
			Assert.IsNotNull (revision, "The stable platform-tools package should contain a revision.");

			return $"{revision.Element ("major")?.Value}.{revision.Element ("minor")?.Value}.{revision.Element ("micro")?.Value}";
		}

		sealed class GoogleV2Fixture
		{
			public const string PlatformToolsVersion = "99.0.1";
			public const string BuildToolsVersion = "37.0.0";
			public const string CommandLineToolsVersion = "22.0";
			public const string PlatformVersion = "37.0";
			public const string FixtureDependencyPath = "extras;googlev2-fixture";

			readonly Dictionary<Uri, byte []> archives = new Dictionary<Uri, byte []> ();
			readonly List<string> requests = new List<string> ();
			readonly List<string> expectedRequests = new List<string> {
				"/repository2-3.xml",
				"/addons_list-5.xml",
				"/addon.xml",
				"/platform-tools.zip",
				"/build-tools.zip",
				"/command-line-tools.zip",
				"/platform.zip",
				"/fixture-dependency.zip",
			};

			public FixtureHelpers Helpers { get; }
			public Uri RepositoryBaseUrl { get; } = new Uri ("https://googlev2-fixture.test/");
			public Uri ManifestUrl => new Uri (RepositoryBaseUrl, "repository2-3.xml");
			public Uri AddonsListUrl => new Uri (RepositoryBaseUrl, "addons_list-5.xml");
			public XDocument RepositoryManifest { get; }

			public GoogleV2Fixture ()
			{
				Helpers = new FixtureHelpers (requests);
				var platformToolsArchive = CreateArchive (
					("platform-tools/source.properties", $"Pkg.Revision={PlatformToolsVersion}"),
					("platform-tools/fixture-marker.txt", "synthetic platform-tools"));
				var buildToolsArchive = CreateArchive (
					($"android-{BuildToolsVersion}/source.properties", $"Pkg.Revision={BuildToolsVersion}"),
					($"android-{BuildToolsVersion}/fixture-marker.txt", "synthetic build-tools"));
				var platformArchive = CreateArchive (
					($"android-{PlatformVersion}/source.properties", "Pkg.Revision=1"),
					($"android-{PlatformVersion}/android.jar", "synthetic android.jar"));
				var fixtureDependencyArchive = CreateArchive (
					("googlev2-fixture/source.properties", "Pkg.Revision=1"),
					("googlev2-fixture/nested/fixture-marker.txt", "synthetic transitive dependency"));
				var sdkManagerName = TestEnvironment.IsWindows ? "sdkmanager.bat" : "sdkmanager";
				var sdkManagerContents = TestEnvironment.IsWindows
					? "@echo off\r\necho invoked>sdkmanager-invoked.txt\r\necho Accepted\r\nexit /b 0\r\n"
					: "#!/bin/sh\necho invoked > sdkmanager-invoked.txt\necho Accepted\nexit 0\n";
				var commandLineToolsArchive = CreateCommandLineToolsArchive (
					("cmdline-tools/source.properties", $"Pkg.Revision={CommandLineToolsVersion}"),
					($"cmdline-tools/bin/{sdkManagerName}", sdkManagerContents));

				AddArchive ("platform-tools.zip", platformToolsArchive);
				AddArchive ("build-tools.zip", buildToolsArchive);
				AddArchive ("platform.zip", platformArchive);
				AddArchive ("fixture-dependency.zip", fixtureDependencyArchive);
				AddArchive ("command-line-tools.zip", commandLineToolsArchive);
				RepositoryManifest = CreateRepositoryManifest (
					platformToolsArchive,
					buildToolsArchive,
					platformArchive,
					fixtureDependencyArchive,
					commandLineToolsArchive);
				Helpers.AddResponse (ManifestUrl, RepositoryManifest.ToString (SaveOptions.DisableFormatting));
				Helpers.AddResponse (AddonsListUrl, CreateAddonsListManifest ());
				Helpers.AddResponse (new Uri (RepositoryBaseUrl, "addon.xml"), "<repository />");
			}

			public void PrepareDownloads (IEnumerable<Archive> downloads, string directory)
			{
				if (Directory.Exists (directory))
					Directory.Delete (directory, recursive: true);
				Directory.CreateDirectory (directory);

				foreach (var download in downloads) {
					requests.Add (download.Url.AbsolutePath);
					Assert.IsTrue (archives.TryGetValue (download.Url, out byte [] contents),
						$"Unexpected GoogleV2 fixture archive '{download.Url}'.");
					string path = Path.Combine (directory, Path.GetFileName (download.Url.LocalPath));
					File.WriteAllBytes (path, contents);
					download.DownloadedFilePath = path;
				}
			}

			public void AssertInstallation (string sdkPath)
			{
				AssertFixtureFile (sdkPath, "platform-tools", "fixture-marker.txt");
				AssertFixtureFile (sdkPath, "build-tools", BuildToolsVersion, "fixture-marker.txt");
				AssertFixtureFile (sdkPath, "cmdline-tools", CommandLineToolsVersion, "source.properties");
				AssertFixtureFile (sdkPath, "platforms", $"android-{PlatformVersion}", "android.jar");
				AssertFixtureFile (sdkPath, "extras", "googlev2-fixture", "nested", "fixture-marker.txt");

				CollectionAssert.AreEquivalent (expectedRequests, requests,
					"GoogleV2 should only access the expected in-memory fixture resources.");
			}

			void AddArchive (string name, byte [] contents)
			{
				archives.Add (new Uri (RepositoryBaseUrl, name), contents);
			}

			static void AssertFixtureFile (string sdkPath, params string [] parts)
			{
				var path = parts.Aggregate (sdkPath, Path.Combine);
				Assert.IsTrue (File.Exists (path), $"Expected synthetic GoogleV2 fixture file '{path}' to be installed.");
				Assert.IsTrue (File.Exists (Path.Combine (Path.GetDirectoryName (path), "package.xml")) ||
					File.Exists (Path.Combine (Path.GetDirectoryName (Path.GetDirectoryName (path)), "package.xml")),
					$"Expected a package.xml next to the installed GoogleV2 fixture '{path}'.");
			}

			XDocument CreateRepositoryManifest (
				byte [] platformToolsArchive,
				byte [] buildToolsArchive,
				byte [] platformArchive,
				byte [] fixtureDependencyArchive,
				byte [] commandLineToolsArchive)
			{
				XNamespace xsi = "http://www.w3.org/2001/XMLSchema-instance";
				return new XDocument (
					new XElement ("repository",
						new XAttribute (XNamespace.Xmlns + "xsi", xsi),
						new XElement ("license", new XAttribute ("id", "android-sdk-license"), "Synthetic test license"),
						new XElement ("channel", new XAttribute ("id", "channel-0"), "stable"),
						CreatePackage ("platform-tools", PlatformToolsVersion, "platform-tools.zip", platformToolsArchive),
						CreatePackage ($"build-tools;{BuildToolsVersion}", BuildToolsVersion, "build-tools.zip", buildToolsArchive),
						CreatePackage ($"cmdline-tools;{CommandLineToolsVersion}", CommandLineToolsVersion, "command-line-tools.zip", commandLineToolsArchive),
						CreatePackage (
							$"platforms;android-{PlatformVersion}",
							"1.0.0",
							"platform.zip",
							platformArchive,
							new XElement ("dependencies",
								new XElement ("dependency",
									new XAttribute ("path", FixtureDependencyPath)))),
						CreatePackage (FixtureDependencyPath, "1.0.0", "fixture-dependency.zip", fixtureDependencyArchive)
					)
				);
			}

			XElement CreatePackage (string path, string version, string archiveName, byte [] archive, XElement dependencies = null)
			{
				XNamespace xsi = "http://www.w3.org/2001/XMLSchema-instance";
				return new XElement ("remotePackage",
					new XAttribute ("path", path),
					new XElement ("type-details", new XAttribute (xsi + "type", "genericDetailsType")),
					CreateRevision (version),
					new XElement ("display-name", $"Synthetic {path}"),
					new XElement ("uses-license", new XAttribute ("ref", "android-sdk-license")),
					new XElement ("channelRef", new XAttribute ("ref", "channel-0")),
					dependencies,
					new XElement ("archives",
						new XElement ("archive",
							new XElement ("complete",
								new XElement ("size", archive.Length),
								new XElement ("checksum", new XAttribute ("type", "sha256"), Files.ToHexString (SHA256.HashData (archive)).ToLowerInvariant ()),
								new XElement ("url", new Uri (RepositoryBaseUrl, archiveName))))));
			}

			static XElement CreateRevision (string version)
			{
				var fields = version.Split ('.').Select (int.Parse).ToArray ();
				return new XElement ("revision",
					new XElement ("major", fields [0]),
					fields.Length > 1 ? new XElement ("minor", fields [1]) : null,
					fields.Length > 2 ? new XElement ("micro", fields [2]) : null);
			}

			string CreateAddonsListManifest ()
			{
				XNamespace xsi = "http://www.w3.org/2001/XMLSchema-instance";
				return new XDocument (
					new XElement ("sdk-addons-list",
						new XAttribute (XNamespace.Xmlns + "xsi", xsi),
						new XAttribute (XNamespace.Xmlns + "sdk", "http://schemas.android.com/sdk/android/repo/addons-list/3"),
						new XElement ("site",
							new XAttribute (xsi + "type", "sdk:addonSiteType"),
							new XElement ("displayName", "Synthetic add-on repository"),
							new XElement ("url", new Uri (RepositoryBaseUrl, "addon.xml"))))
				).ToString (SaveOptions.DisableFormatting);
			}

			static byte [] CreateArchive (params (string path, string contents) [] entries)
			{
				using (var stream = new MemoryStream ()) {
					using (var archive = ZipArchive.Create (stream)) {
						foreach (var entry in entries)
							archive.AddEntry (entry.path, entry.contents, Encoding.UTF8);
					}
					return stream.ToArray ();
				}
			}

			static byte [] CreateCommandLineToolsArchive (
				(string path, string contents) sourceProperties,
				(string path, string contents) sdkManager)
			{
				using (var stream = new MemoryStream ()) {
					using (var archive = ZipArchive.Create (stream)) {
						archive.AddEntry (sourceProperties.path, sourceProperties.contents, Encoding.UTF8);
						archive.AddEntry (
							Encoding.UTF8.GetBytes (sdkManager.contents),
							sdkManager.path,
							EntryPermissions.OwnerRead | EntryPermissions.OwnerWrite | EntryPermissions.OwnerExecute |
								EntryPermissions.GroupRead | EntryPermissions.GroupExecute |
								EntryPermissions.WorldRead | EntryPermissions.WorldExecute);
					}
					return stream.ToArray ();
				}
			}

		}

		sealed class FixtureHelpers : Helper, IHelpers
		{
			readonly Dictionary<Uri, string> responses = new Dictionary<Uri, string> ();
			readonly List<string> requests;

			public FixtureHelpers (List<string> requests)
			{
				this.requests = requests;
			}

			public void AddResponse (Uri uri, string contents)
			{
				responses.Add (uri, contents);
			}

			bool IHelpers.DownloadToString (Uri url, out string output)
			{
				requests.Add (url.AbsolutePath);
				if (!responses.TryGetValue (url, out output))
					throw new InvalidOperationException ($"Unexpected GoogleV2 fixture manifest '{url}'.");
				return true;
			}
		}

		static IEnumerable<object[]> Get_GetDependencyNdkRequiredConditionsData ()
		{
			var ret = new List<object[]> ();

			foreach (AndroidRuntime runtime in new[] { AndroidRuntime.CoreCLR, AndroidRuntime.NativeAOT }) {
				AddTestData ("AotAssemblies", false, runtime);
				AddTestData ("AndroidEnableProfiledAot", false, runtime);
				AddTestData ("EnableLLVM", true, runtime);
			}

			return ret;

			void AddTestData (string property, bool ndkRequired, AndroidRuntime runtime)
			{
				ret.Add (new object[] {
					property,
					ndkRequired,
					runtime,
				});
			}
		}

		[Test]
		[TestCaseSource (nameof (Get_GetDependencyNdkRequiredConditionsData))]
		public void GetDependencyNdkRequiredConditions (string property, bool ndkRequired, AndroidRuntime runtime)
		{
			bool isRelease = runtime == AndroidRuntime.NativeAOT;
			if (IgnoreUnsupportedConfiguration (runtime, release: isRelease)) {
				return;
			}

			// CoreCLR doesn't support AOT so it doesn't ever need the NDK and it doesn't support profiled AOT
			if (runtime == AndroidRuntime.CoreCLR && (ndkRequired || property == "AndroidEnableProfiledAot")) {
				Assert.Ignore ("CoreCLR doesn't support AOT, it doesn't ever require the NDK");
			}

			// NativeAOT doesn't support profiled AOT or EnableLLVM (Mono concepts)
			if (runtime == AndroidRuntime.NativeAOT && property == "AndroidEnableProfiledAot") {
				Assert.Ignore ("NativeAOT doesn't support profiled AOT");
			}

			if (runtime == AndroidRuntime.NativeAOT && property == "EnableLLVM") {
				Assert.Ignore ("EnableLLVM is not applicable to NativeAOT");
			}

			var proj = new XamarinAndroidApplicationProject {
				IsRelease = isRelease,
			};
			proj.SetRuntime (runtime);
			proj.AotAssemblies = runtime == AndroidRuntime.MonoVM;
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
		public void NativeAotRequiresNdk_WhenWorkloadLinkerDisabled ()
		{
			var proj = new XamarinAndroidApplicationProject {
				IsRelease = true,
			};
			proj.SetRuntime (AndroidRuntime.NativeAOT);
			proj.SetProperty ("_AndroidUseWorkloadNativeLinker", "false");
			proj.SetProperty ("_SkipNdkResolution", "false");
			using (var builder = CreateApkBuilder ()) {
				builder.Verbosity = LoggerVerbosity.Detailed;
				builder.Target = "GetAndroidDependencies";
				Assert.IsTrue (builder.Build (proj), "Build should have succeeded.");
				IEnumerable<string> taskOutput = builder.LastBuildOutput
					.Select (x => x.Trim ())
					.SkipWhile (x => !x.StartsWith ("Task \"CalculateProjectDependencies\"", StringComparison.Ordinal))
					.SkipWhile (x => !x.StartsWith ("Output Item(s):", StringComparison.Ordinal))
					.TakeWhile (x => !x.StartsWith ("Done executing task \"CalculateProjectDependencies\"", StringComparison.Ordinal));
				StringAssertEx.Contains ("ndk-bundle", taskOutput, "ndk-bundle should be a dependency for NativeAOT without workload linker.");
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

		static readonly XNamespace MSBuildXmlns = "http://schemas.microsoft.com/developer/msbuild/2003";

		static string GetExpectedBuildToolsVersion ()
		{
			return XABuildConfig.AndroidSdkBuildToolsVersion;
		}
	}
}
