using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Xml;
using System.Xml.Linq;
using NUnit.Framework;
using Xamarin.Android.Tasks;
using Xamarin.Android.Tools;
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

			// Set to true when we are marking a new Android API level as stable, but it has not
			// been added to the Xamarin manifest yet.
			var xamarin_manifest_needs_updating = false;

			var oldSdkPath = Environment.GetEnvironmentVariable ("TEST_ANDROID_SDK_PATH");
			var oldJdkPath = Environment.GetEnvironmentVariable ("TEST_ANDROID_JDK_PATH");
			var outdatedCommandLineToolsRevision = new Version (19, 0);
			GoogleV2Fixture googleV2Fixture = null;
			try {
				string sdkPath = Path.Combine (Root, "temp", TestName, "android-sdk");
				bool useGoogleV2Fixture = manifestType == "GoogleV2";
				string jdkPath = useGoogleV2Fixture
					? AndroidSdkResolver.GetJavaSdkPath ()
					: Path.Combine (Root, "temp", TestName, "android-jdk");
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
				// When using the default Xamarin manifest, this test should fail if we can't install any of the defaults in Xamarin.Installer.Common.props
				// When using the Google manifest, override the platform tools version to the one in their manifest as it only ever contains one version
				if (useGoogleV2Fixture) {
					googleV2Fixture = new GoogleV2Fixture ();
					buildArgs.Add ($"AndroidManifestSource={googleV2Fixture.ManifestUrl}");
					buildArgs.Add ($"_AndroidGoogleAddonsListSource={googleV2Fixture.AddonsListUrl}");
					buildArgs.Add ($"AndroidSdkPlatformToolsVersion={GetCurrentPlatformToolsVersion (googleV2Fixture.ManifestUrl)}");
					buildArgs.Add ("AndroidInstallJavaDependencies=false");
				} else {
					var manifestPath = Path.Combine (XABuildPaths.TopDirectory, "src", "Xamarin.Installer.AndroidSDK", "Feeds", "AndroidManifestFeed_d18.0.xml");
					Assert.IsTrue (File.Exists (manifestPath), $"Xamarin manifest does not exist at '{manifestPath}'.");
					buildArgs.Add ($"AndroidManifestSource={manifestPath}");
				}

				using (var b = CreateApkBuilder ()) {
					b.Verbosity = LoggerVerbosity.Detailed;
					b.CleanupAfterSuccessfulBuild = false;
					b.ThrowOnBuildFailure = false;
					string defaultTarget = b.Target;
					b.Target = "InstallAndroidDependencies";
					b.BuildLogFile = "install-deps.log";

					// The Xamarin variant downloads the Android SDK and JDK over the network, which
					// can fail intermittently in CI. Retry a few times before giving up, starting from a
					// clean SDK/JDK directory each attempt so a partial download does not affect the next.
					// See https://github.com/dotnet/android/issues/11973
					int maxInstallAttempts = useGoogleV2Fixture ? 1 : 3;
					bool installSucceeded = false;
					for (int attempt = 1; attempt <= maxInstallAttempts; attempt++) {
						var pathsToReset = useGoogleV2Fixture ? new [] { sdkPath } : new [] { sdkPath, jdkPath };
						foreach (var path in pathsToReset) {
							if (Directory.Exists (path))
								Directory.Delete (path, recursive: true);
							Directory.CreateDirectory (path);
						}

						if (!useGoogleV2Fixture) {
							var commandLineToolsPath = Path.Combine (sdkPath, "cmdline-tools", "latest");
							Directory.CreateDirectory (commandLineToolsPath);
							File.WriteAllText (Path.Combine (commandLineToolsPath, "source.properties"), $"Pkg.Revision={outdatedCommandLineToolsRevision}");
						}

						if (b.Build (proj, parameters: buildArgs.ToArray ())) {
							installSucceeded = true;
							break;
						}

						TestContext.WriteLine ($"InstallAndroidDependencies attempt {attempt} of {maxInstallAttempts} failed. Please check the task output in 'install-deps.log'.");
						if (attempt < maxInstallAttempts)
							Thread.Sleep (TimeSpan.FromSeconds (10));
					}
					googleV2Fixture?.AssertServerSucceeded ();
					Assert.IsTrue (installSucceeded, $"InstallAndroidDependencies should have succeeded within {maxInstallAttempts} attempts.");

					if (useGoogleV2Fixture) {
						googleV2Fixture.AssertInstallation (sdkPath, b.LastBuildOutput);
					} else {
						var sourceProperties = Path.Combine (sdkPath, "cmdline-tools", "latest", "source.properties");
						var revisionProperty = File.ReadLines (sourceProperties)
							.First (line => line.StartsWith ("Pkg.Revision", StringComparison.Ordinal));
						int separator = revisionProperty.IndexOf ('=');
						Assert.GreaterOrEqual (separator, 0, "The command-line tools revision property should contain a value.");
						var installedRevision = Version.Parse (revisionProperty.Substring (separator + 1).Trim ());
						Assert.Greater (installedRevision.CompareTo (outdatedCommandLineToolsRevision), 0, "The outdated command-line tools installation should have been updated.");
					}

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

					if (useGoogleV2Fixture)
						return;

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
				googleV2Fixture?.Dispose ();
				Environment.SetEnvironmentVariable ("TEST_ANDROID_SDK_PATH", oldSdkPath);
				Environment.SetEnvironmentVariable ("TEST_ANDROID_JDK_PATH", oldJdkPath);
			}
		}

		static string GetCurrentPlatformToolsVersion (Uri manifestUrl)
		{
			var s = new XmlReaderSettings {
				XmlResolver = null,
			};
			var r = XmlReader.Create (manifestUrl.ToString (), s);
			var d = XDocument.Load (r);

			var platformToolsPackage    = d.Root.Elements ("remotePackage")
				.Where (e => "platform-tools" == (string) e.Attribute("path") &&
					"android-sdk-preview-license" != (string) e.Element ("uses-license")?.Attribute ("ref"))
				.FirstOrDefault ();

			var revision    = platformToolsPackage.Element ("revision");

			return $"{revision.Element ("major")?.Value}.{revision.Element ("minor")?.Value}.{revision.Element ("micro")?.Value}";
		}

		sealed class GoogleV2Fixture : IDisposable
		{
			const string PlatformToolsVersion = "99.0.1";
			const string BuildToolsVersion = "37.0.0";
			const string CommandLineToolsVersion = "22.0";
			const string PlatformVersion = "37.0";
			const string FixtureDependencyPath = "extras;googlev2-fixture";

			readonly LocalHttpFixtureServer server = new LocalHttpFixtureServer ();
			readonly List<string> expectedRequests = new List<string> {
				"/repository2-3.xml",
				"/repository2-3.xml",
				"/addons_list-5.xml",
				"/addon.xml",
				"/platform-tools.zip",
				"/build-tools.zip",
				"/command-line-tools.zip",
				"/platform.zip",
				"/fixture-dependency.zip",
			};

			public Uri ManifestUrl => server.GetUri ("repository2-3.xml");
			public Uri AddonsListUrl => server.GetUri ("addons_list-5.xml");

			public GoogleV2Fixture ()
			{
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
					? "@echo off\r\necho Accepted\r\nexit /b 0\r\n"
					: "#!/bin/sh\necho Accepted\nexit 0\n";
				var commandLineToolsArchive = CreateCommandLineToolsArchive (
					("cmdline-tools/source.properties", $"Pkg.Revision={CommandLineToolsVersion}"),
					($"cmdline-tools/bin/{sdkManagerName}", sdkManagerContents));

				server.AddResponse ("platform-tools.zip", platformToolsArchive);
				server.AddResponse ("build-tools.zip", buildToolsArchive);
				server.AddResponse ("platform.zip", platformArchive);
				server.AddResponse ("fixture-dependency.zip", fixtureDependencyArchive);
				server.AddResponse ("command-line-tools.zip", commandLineToolsArchive);
				server.AddResponse ("repository2-3.xml", Encoding.UTF8.GetBytes (CreateRepositoryManifest (
					platformToolsArchive,
					buildToolsArchive,
					platformArchive,
					fixtureDependencyArchive,
					commandLineToolsArchive)));
				server.AddResponse ("addons_list-5.xml", Encoding.UTF8.GetBytes (CreateAddonsListManifest ()));
				server.AddResponse ("addon.xml", Encoding.UTF8.GetBytes ("<repository />"));
			}

			public void AssertInstallation (string sdkPath, IEnumerable<string> buildOutput)
			{
				AssertServerSucceeded ();
				AssertFixtureFile (sdkPath, "platform-tools", "fixture-marker.txt");
				AssertFixtureFile (sdkPath, "build-tools", BuildToolsVersion, "fixture-marker.txt");
				AssertFixtureFile (sdkPath, "cmdline-tools", CommandLineToolsVersion, "source.properties");
				AssertFixtureFile (sdkPath, "platforms", $"android-{PlatformVersion}", "android.jar");
				AssertFixtureFile (sdkPath, "extras", "googlev2-fixture", "nested", "fixture-marker.txt");

				var output = string.Join ("\n", buildOutput);
				StringAssert.Contains ("Skipping Java SDK installation.", output);
				StringAssert.DoesNotContain ("dl.google.com", output);
				StringAssert.DoesNotContain ("dl-ssl.google.com", output);
				CollectionAssert.AreEquivalent (expectedRequests, server.Requests,
					$"GoogleV2 should only request the expected loopback fixtures from {server.BaseUri}.");
			}

			public void AssertServerSucceeded ()
			{
				Assert.IsNull (server.ServerException, $"The local fixture server failed: {server.ServerException}");
			}

			public void Dispose ()
			{
				server.Dispose ();
			}

			static void AssertFixtureFile (string sdkPath, params string [] parts)
			{
				var path = parts.Aggregate (sdkPath, Path.Combine);
				Assert.IsTrue (File.Exists (path), $"Expected synthetic GoogleV2 fixture file '{path}' to be installed.");
				Assert.IsTrue (File.Exists (Path.Combine (Path.GetDirectoryName (path), "package.xml")) ||
					File.Exists (Path.Combine (Path.GetDirectoryName (Path.GetDirectoryName (path)), "package.xml")),
					$"Expected a package.xml next to the installed GoogleV2 fixture '{path}'.");
			}

			string CreateRepositoryManifest (
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
				).ToString (SaveOptions.DisableFormatting);
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
								new XElement ("checksum", new XAttribute ("type", "sha256"), GetSha256 (archive)),
								new XElement ("url", new Uri (server.BaseUri, archiveName))))));
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
							new XElement ("url", new Uri (server.BaseUri, "addon.xml"))))
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

			static string GetSha256 (byte [] contents)
			{
				using (var sha256 = SHA256.Create ())
					return string.Concat (sha256.ComputeHash (contents).Select (value => value.ToString ("x2")));
			}
		}

		sealed class LocalHttpFixtureServer : IDisposable
		{
			readonly TcpListener listener;
			readonly Dictionary<string, byte []> responses = new Dictionary<string, byte []> (StringComparer.Ordinal);
			readonly ConcurrentQueue<string> requests = new ConcurrentQueue<string> ();
			readonly ConcurrentBag<System.Threading.Tasks.Task> requestTasks = new ConcurrentBag<System.Threading.Tasks.Task> ();
			readonly System.Threading.Tasks.Task acceptLoop;
			Exception serverException;
			volatile bool disposed;

			public Uri BaseUri { get; }
			public IEnumerable<string> Requests => requests;
			public Exception ServerException => serverException;

			public LocalHttpFixtureServer ()
			{
				listener = new TcpListener (IPAddress.Loopback, 0);
				listener.Start ();
				var endpoint = (IPEndPoint) listener.LocalEndpoint;
				BaseUri = new Uri ($"http://127.0.0.1:{endpoint.Port}/");
				acceptLoop = System.Threading.Tasks.Task.Run (AcceptLoop);
			}

			public Uri GetUri (string path)
			{
				return new Uri (BaseUri, path);
			}

			public void AddResponse (string path, byte [] contents)
			{
				responses ["/" + path.TrimStart ('/')] = contents;
			}

			public void Dispose ()
			{
				disposed = true;
				listener.Stop ();
				acceptLoop.GetAwaiter ().GetResult ();
				System.Threading.Tasks.Task.WaitAll (requestTasks.ToArray ());
			}

			void AcceptLoop ()
			{
				while (!disposed) {
					try {
						var client = listener.AcceptTcpClient ();
						requestTasks.Add (System.Threading.Tasks.Task.Run (() => {
							using (client) {
								try {
									HandleRequest (client);
								} catch (Exception ex) {
									Interlocked.CompareExchange (ref serverException, ex, null);
								}
							}
						}));
					} catch (SocketException) when (disposed) {
						return;
					} catch (ObjectDisposedException) when (disposed) {
						return;
					} catch (Exception ex) {
						Interlocked.CompareExchange (ref serverException, ex, null);
						return;
					}
				}
			}

			void HandleRequest (TcpClient client)
			{
				client.ReceiveTimeout = 5000;
				client.SendTimeout = 5000;
				using (var stream = client.GetStream ())
				using (var reader = new StreamReader (stream, Encoding.ASCII, false, 1024, leaveOpen: true)) {
					stream.ReadTimeout = 5000;
					stream.WriteTimeout = 5000;
					var requestLine = reader.ReadLine ();
					if (string.IsNullOrEmpty (requestLine))
						throw new InvalidDataException ("The local fixture server received an empty HTTP request.");

					var requestParts = requestLine.Split (' ');
					if (requestParts.Length < 2)
						throw new InvalidDataException ($"The local fixture server received an invalid HTTP request line: '{requestLine}'.");

					string line;
					do {
						line = reader.ReadLine ();
					} while (!string.IsNullOrEmpty (line));

					var path = new Uri (BaseUri, requestParts [1]).AbsolutePath;
					requests.Enqueue (path);
					bool found = responses.TryGetValue (path, out byte [] response);
					response = response ?? [];
					var status = found ? "200 OK" : "404 Not Found";
					var header = Encoding.ASCII.GetBytes (
						$"HTTP/1.1 {status}\r\nContent-Length: {response.Length}\r\nConnection: close\r\n\r\n");
					stream.Write (header, 0, header.Length);
					stream.Write (response, 0, response.Length);
				}
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
