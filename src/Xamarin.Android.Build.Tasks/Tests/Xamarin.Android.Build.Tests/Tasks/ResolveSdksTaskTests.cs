using System;
using System.Collections.Generic;
using NUnit.Framework;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xamarin.Android.Tasks;
using Xamarin.Android.Tools;
using Xamarin.ProjectTools;
using ResolveAndroidTooling = Xamarin.Android.Tasks.Legacy.ResolveAndroidTooling;

namespace Xamarin.Android.Build.Tests {

	[TestFixture]
	[NonParallelizable] // NOTE: This test was hanging without this
	public class ResolveSdksTaskTests : BaseTest {
#pragma warning disable 414

		static ApiInfo [] apiInfoSelection = new ApiInfo [] {
			new ApiInfo () { Id = "25", Level = 25, Name = "Nougat", FrameworkVersion = "v7.1",  Stable = true },
			new ApiInfo () { Id = "26", Level = 26, Name = "Oreo", FrameworkVersion = "v8.0",  Stable = true  },
			new ApiInfo () { Id = "27", Level = 27, Name = "Oreo", FrameworkVersion = "v8.1",  Stable = true  },
			new ApiInfo () { Id = "28", Level = 28, Name = "Pie",    FrameworkVersion = "v9.0", Stable = false },
			new ApiInfo () { Id = "29", Level = 29, Name = "Android10", FrameworkVersion = "v10.0", Stable = false },
			new ApiInfo () { Id = "30", Level = 30, Name = "Android11", FrameworkVersion = "v11.0", Stable = false },
			new ApiInfo () { Id = "S",  Level = 31, Name = "S",         FrameworkVersion = "v11.0.99", Stable = false },
			new ApiInfo () { Id = "Z",  Level = 127, Name = "Z",    FrameworkVersion = "v108.1.99", Stable = false },
		};

		// via Xamarin.Android.Common.props
		const   string  MinimumSupportedJavaVersion     = "1.6.0";
		const   string  LatestSupportedJavaVersion      = "11.0.99";

		static object [] ResolveAndroidToolingTestCases = new object [] {
			new object[] {
				/* buildtools */   "26.0.3",
				/* jdk */ "1.8.0",
				/* apis*/ apiInfoSelection,
				/* targetFrameworkVersion */ "v9.0",
				/* expectedTaskResult */ true,
				/* expectedTargetFramework */ "v9.0",
				/* expectedError */ "",
				/* expectedErrorMessage */ "",
			},
			new object[] {
				/* buildtools */   "26.0.3",
				/* jdk */ "1.8.0",
				/* apis*/ apiInfoSelection,
				/* targetFrameworkVersion */ "v8.1",
				/* expectedTaskResult */ true,
				/* expectedTargetFramework */ "v8.1",
				/* expectedError */ "",
				/* expectedErrorMessage */ "",
			},
			new object[] {
				/* buildtools */   "26.0.3",
				/* jdk */ "1.8.0",
				/* apis*/ apiInfoSelection,
				/* targetFrameworkVersion */ "v8.0",
				/* expectedTaskResult */ true,
				/* expectedTargetFramework */ "v8.0",
				/* expectedError */ "",
				/* expectedErrorMessage */ "",
			},
			new object[] {
				/* buildtools */   "26.0.3",
				/* jdk */ "1.8.0",
				/* apis*/ apiInfoSelection,
				/* targetFrameworkVersion */ "v6.0",
				/* expectedTaskResult */ false,
				/* expectedTargetFramework */ "v6.0",
				/* expectedError */ "XA0001",
				/* expectedErrorMessage */ "Unsupported or invalid $(TargetFrameworkVersion) value of 'v6.0'. Please update your Project Options.",
			},
			new object[] {
				/* buildtools */   "30.0.0",
				/* jdk */ "11.0",
				/* apis*/ apiInfoSelection,
				/* targetFrameworkVersion */ "v11.0.99",
				/* expectedTaskResult */ true,
				/* expectedTargetFramework */ "v11.0.99",
				/* expectedError */ "",
				/* expectedErrorMessage */ "",
			},
		};
		#pragma warning restore 414
		[Test]
		[TestCaseSource(nameof(ResolveAndroidToolingTestCases))]
		public void ResolveAndroidToolingTest (string buildtools, string jdk, ApiInfo[] apis, string targetFrameworkVersion, bool expectedTaskResult, string expectedTargetFramework, string expectedError = "", string expectedErrorMessage = "")
		{
			var path = Path.Combine ("temp", "ResolveAndroidToolingTest_" + Guid.NewGuid ());
			var androidSdkPath = CreateFauxAndroidSdkDirectory (Path.Combine (path, "android-sdk"), buildtools, apis);
			var androidNdkPath = CreateFauxAndroidNdkDirectory (Path.Combine (path, "android-ndk"));
			string javaExe = string.Empty;
			string javacExe;
			var javaPath = CreateFauxJavaSdkDirectory (Path.Combine (path, "jdk"), jdk, out javaExe, out javacExe);
			var referencePath = CreateFauxReferencesDirectory (Path.Combine (path, "references"), apis);
			var errors = new List<BuildErrorEventArgs> ();
			IBuildEngine engine = new MockBuildEngine (TestContext.Out, errors);
			var resolveSdks = new ResolveSdks {
				BuildEngine = engine,
				AndroidSdkPath = androidSdkPath,
				AndroidNdkPath = androidNdkPath,
				JavaSdkPath = javaPath,
				MinimumSupportedJavaVersion = MinimumSupportedJavaVersion,
				LatestSupportedJavaVersion  = LatestSupportedJavaVersion,
				ReferenceAssemblyPaths = new [] {
					Path.Combine (referencePath, "MonoAndroid"),
				},
			};
			var validateJavaVersion = new ValidateJavaVersion {
				BuildEngine = engine,
				JavaSdkPath = javaPath,
				JavaToolExe = javaExe,
				JavacToolExe = javacExe,
				LatestSupportedJavaVersion = LatestSupportedJavaVersion,
				MinimumSupportedJavaVersion = "1.8.0",
			};
			var androidTooling = new ResolveAndroidTooling {
				BuildEngine = engine,
				AndroidSdkPath = androidSdkPath,
				TargetFrameworkVersion = targetFrameworkVersion,
				AndroidSdkBuildToolsVersion = buildtools,
				AndroidApplication = true,
			};
			Assert.AreEqual (expectedTaskResult, resolveSdks.Execute () && validateJavaVersion.Execute () && androidTooling.Execute (), $"Tasks should have {(expectedTaskResult ? "succeeded" : "failed" )}.");
			Assert.AreEqual (expectedTargetFramework, androidTooling.TargetFrameworkVersion, $"TargetFrameworkVersion should be {expectedTargetFramework} but was {androidTooling.TargetFrameworkVersion}");
			if (!string.IsNullOrWhiteSpace (expectedError)) {
				Assert.AreEqual (1, errors.Count (), "An error should have been raised.");
				Assert.AreEqual (expectedError, errors [0].Code, $"Expected error code {expectedError} but found {errors [0].Code}");
				Assert.AreEqual (expectedErrorMessage, errors [0].Message, $"Expected error code {expectedErrorMessage} but found {errors [0].Message}");
			}
			Directory.Delete (Path.Combine (Root, path), recursive: true);
		}

		[Test, NonParallelizable]
		public void ResolveSdkTiming ()
		{
			var path = Path.Combine ("temp", TestName);
			var androidSdkPath = CreateFauxAndroidSdkDirectory (Path.Combine (path, "android-sdk"), "26.0.3");
			var androidNdkPath = CreateFauxAndroidNdkDirectory (Path.Combine (path, "android-ndk"));
			string javaExe = string.Empty;
			string javacExe;
			var javaPath = CreateFauxJavaSdkDirectory (Path.Combine (path, "jdk"), "1.8.0", out javaExe, out javacExe);
			var referencePath = CreateFauxReferencesDirectory (Path.Combine (path, "references"), new ApiInfo [] {
				new ApiInfo () { Id = "26", Level = 26, Name = "Oreo", FrameworkVersion = "v8.0", Stable = true },
				new ApiInfo () { Id = "27", Level = 27, Name = "Oreo", FrameworkVersion = "v8.1", Stable = true },
			});
			var messages = new List<BuildMessageEventArgs> ();
			IBuildEngine engine = new MockBuildEngine (TestContext.Out, messages: messages);
			var resolveSdks = new ResolveSdks {
				BuildEngine = engine,
				AndroidSdkPath = androidSdkPath,
				AndroidNdkPath = androidNdkPath,
				JavaSdkPath = javaPath,
				MinimumSupportedJavaVersion = MinimumSupportedJavaVersion,
				LatestSupportedJavaVersion  = LatestSupportedJavaVersion,
				ReferenceAssemblyPaths = new [] {
					Path.Combine (referencePath, "MonoAndroid"),
				},
			};
			var validateJavaVersion = new ValidateJavaVersion {
				BuildEngine = engine,
				JavaSdkPath = javaPath,
				JavaToolExe = javaExe,
				JavacToolExe = javacExe,
				LatestSupportedJavaVersion = "1.8.0",
				MinimumSupportedJavaVersion = "1.8.0",
			};
			var androidTooling = new ResolveAndroidTooling {
				BuildEngine = engine,
				AndroidSdkPath = androidSdkPath,
				TargetFrameworkVersion = "v8.0",
				AndroidSdkBuildToolsVersion = "26.0.3",
				AndroidApplication = true,
			};
			var start = DateTime.UtcNow;
			Assert.IsTrue (resolveSdks.Execute (), "ResolveSdks should succeed!");
			Assert.IsTrue (validateJavaVersion.Execute (), "ValidateJavaVersion should succeed!");
			Assert.IsTrue (androidTooling.Execute (), "ResolveAndroidTooling should succeed!");
			var executionTime = DateTime.UtcNow - start;
			Assert.LessOrEqual (executionTime, TimeSpan.FromSeconds(2), "Task should not take more than 2 seconds to run.");
			Assert.AreEqual (androidTooling.AndroidApiLevel, "26", "AndroidApiLevel should be 26");
			Assert.AreEqual (androidTooling.TargetFrameworkVersion, "v8.0", "TargetFrameworkVersion should be v8.0");
			Assert.AreEqual (androidTooling.AndroidApiLevelName, "26", "AndroidApiLevelName should be 26");
			Assert.NotNull (resolveSdks.ReferenceAssemblyPaths, "ReferenceAssemblyPaths should not be null.");
			Assert.AreEqual (resolveSdks.ReferenceAssemblyPaths.Length, 1, "ReferenceAssemblyPaths should have 1 entry.");
			Assert.AreEqual (resolveSdks.ReferenceAssemblyPaths[0], Path.Combine (referencePath, "MonoAndroid"), $"ReferenceAssemblyPaths should be {Path.Combine (referencePath, "MonoAndroid")}.");
			var expected = Path.GetDirectoryName (GetType ().Assembly.Location);
			Assert.AreEqual (resolveSdks.MonoAndroidToolsPath, expected, $"MonoAndroidToolsPath should be {expected}");
			expected += Path.DirectorySeparatorChar;
			if (resolveSdks.MonoAndroidBinPath != expected) {
				//For non-Windows platforms, remove a directory such as "Darwin", MonoAndroidBinPath also has a trailing /
				var binPath = Path.GetDirectoryName (Path.GetDirectoryName (resolveSdks.MonoAndroidBinPath)) + Path.DirectorySeparatorChar;
				Assert.AreEqual (binPath, expected, $"MonoAndroidBinPath should be {expected}");
			}
			Assert.AreEqual (resolveSdks.AndroidSdkPath, androidSdkPath, $"AndroidSdkPath should be {androidSdkPath}");
			Assert.AreEqual (resolveSdks.AndroidNdkPath, androidNdkPath, $"AndroidNdkPath should be {androidNdkPath}");
			Assert.AreEqual (resolveSdks.JavaSdkPath, javaPath, $"JavaSdkPath should be {javaPath}");
			expected = Path.Combine (androidSdkPath, "build-tools", "26.0.3");
			Assert.AreEqual (androidTooling.AndroidSdkBuildToolsPath, expected, $"AndroidSdkBuildToolsPath should be {expected}");
			Assert.AreEqual (androidTooling.AndroidSdkBuildToolsBinPath, expected, "AndroidSdkBuildToolsBinPath should be {expected}");
			Assert.AreEqual (androidTooling.ZipAlignPath, expected, "ZipAlignPath should be {expected}");
			expected = Path.Combine (androidSdkPath, "tools");
			Assert.AreEqual (androidTooling.LintToolPath, expected, $"LintToolPath should be {expected}");
			expected = Path.Combine (androidSdkPath, "build-tools", "26.0.3", "lib", "apksigner.jar");
			Assert.AreEqual (androidTooling.ApkSignerJar, expected, $"ApkSignerJar should be {expected}");
			Assert.AreEqual (androidTooling.AndroidUseApkSigner, false, "AndroidUseApkSigner should be false");
			Assert.AreEqual (validateJavaVersion.JdkVersion, "1.8.0", "JdkVersion should be 1.8.0");
			Assert.AreEqual (validateJavaVersion.MinimumRequiredJdkVersion, "1.8.0", "MinimumRequiredJdkVersion should be 1.8.0");
			Assert.IsFalse (messages.Any (m => m.Message == "ResolveSdks Outputs:" ||
				m.Message == "ResolveAndroidTooling Outputs:" ||
				m.Message == "ValidateJavaVersion Outputs:" ||
				m.Message.StartsWith ("  AndroidSdkPath:", StringComparison.Ordinal) ||
				m.Message.StartsWith ("  AndroidApiLevel:", StringComparison.Ordinal) ||
				m.Message.StartsWith ("  TargetFrameworkVersion:", StringComparison.Ordinal)));
			Directory.Delete (Path.Combine (Root, path), recursive: true);
		}

		[TestCase (null, "XA5300")]
		[TestCase ("", "XA5300")]
		[TestCase ("missing", "XA5300")]
		[TestCase ("empty", "XA5205")]
		public void ResolveAndroidToolingWithMissingSdkDoesNotThrow (string sdkPath, string expectedError)
		{
			var path = Path.Combine (Root, "temp", TestName);
			Directory.CreateDirectory (path);
			var errors = new List<BuildErrorEventArgs> ();
			try {
				IBuildEngine engine = new MockBuildEngine (TestContext.Out, errors);
				var androidTooling = new Xamarin.Android.Tasks.ResolveAndroidTooling {
					BuildEngine = engine,
					AndroidSdkPath = sdkPath == "empty" ? path : sdkPath == "missing" ? Path.Combine (path, "missing") : sdkPath,
					AndroidSdkBuildToolsVersion = "36.0.0",
					TargetPlatformVersion = "37.0",
					AndroidApplication = true,
				};

				Assert.IsFalse (androidTooling.Execute (), "ResolveAndroidTooling should fail when the explicit SDK has no tooling.");
				Assert.AreEqual ("37", androidTooling.AndroidApiLevel, "AndroidApiLevel should still be calculated for dependency resolution.");
				Assert.AreEqual (expectedError, errors.Single ().Code);
			} finally {
				if (Directory.Exists (path))
					Directory.Delete (path, recursive: true);
			}
		}

		[Test]
		public void ResolveSdksReturnsAbsolutePaths ([Values (false, true)] bool discoverNdk, [Values (false, true)] bool commandLineTools)
		{
			var path = Path.Combine ("temp", TestName);
			var sdkPath = CreateFauxAndroidSdkDirectory (Path.Combine (path, "android-sdk"), "37.0.0");
			var ndkPath = CreateFauxAndroidNdkDirectory (discoverNdk ? Path.Combine (path, "android-sdk", "ndk-bundle") : Path.Combine (path, "android-ndk"));
			var javaPath = CreateFauxJavaSdkDirectory (Path.Combine (path, "jdk"), "1.8.0", out _, out _);
			try {
				File.WriteAllText (Path.Combine (ndkPath, "source.properties"), "Pkg.Revision = 21.0.6113669");
				var toolsPath = Path.Combine (sdkPath, commandLineTools ? Path.Combine ("cmdline-tools", "21.0") : "tools");
				Directory.CreateDirectory (toolsPath);
				var resolveSdks = new ResolveSdks {
					BuildEngine = new MockBuildEngine (TestContext.Out),
					AndroidSdkPath = Path.GetRelativePath (Environment.CurrentDirectory, sdkPath),
					AndroidNdkPath = discoverNdk ? null : Path.GetRelativePath (Environment.CurrentDirectory, ndkPath),
					JavaSdkPath = Path.GetRelativePath (Environment.CurrentDirectory, javaPath),
					MinimumSupportedJavaVersion = MinimumSupportedJavaVersion,
					LatestSupportedJavaVersion = LatestSupportedJavaVersion,
					CommandLineToolsVersion = "21.0",
				};
				Assert.IsTrue (resolveSdks.Execute ());
				Assert.AreEqual (sdkPath, resolveSdks.AndroidSdkPath);
				Assert.AreEqual (ndkPath, resolveSdks.AndroidNdkPath);
				Assert.AreEqual (javaPath, resolveSdks.JavaSdkPath);
				Assert.AreEqual (toolsPath, resolveSdks.CommandLineToolsPath);
			} finally {
				Directory.Delete (Path.Combine (Root, path), recursive: true);
			}
		}

		[Test]
		public void SdkConsumersUseExplicitPaths ()
		{
			var path = Path.Combine ("temp", TestName);
			var firstSdk = CreateFauxAndroidSdkDirectory (Path.Combine (path, "first-sdk"), "37.0.0", [new ApiInfo { Id = "37" }]);
			var secondSdk = CreateFauxAndroidSdkDirectory (Path.Combine (path, "second-sdk"), "36.0.0", [new ApiInfo { Id = "37" }]);
			var javaPath = CreateFauxJavaSdkDirectory (Path.Combine (path, "jdk"), "1.8.0", out _, out _);
			var engine = new MockBuildEngine (TestContext.Out);
			var resolveSdks = new ResolveSdks {
				BuildEngine = engine,
				AndroidSdkPath = firstSdk,
				JavaSdkPath = javaPath,
				MinimumSupportedJavaVersion = MinimumSupportedJavaVersion,
				LatestSupportedJavaVersion = LatestSupportedJavaVersion,
			};
			try {
				Assert.IsTrue (resolveSdks.Execute ());
				var tooling = new Xamarin.Android.Tasks.ResolveAndroidTooling {
					BuildEngine = engine,
					AndroidSdkPath = secondSdk,
					TargetPlatformVersion = "37.0",
				};
				Assert.IsTrue (tooling.Execute ());
				Assert.AreEqual (Path.Combine (secondSdk, "build-tools", "36.0.0"), tooling.AndroidSdkBuildToolsPath);
				Assert.AreEqual (Path.Combine (secondSdk, "tools"), tooling.LintToolPath);

				var proj = new XamarinAndroidApplicationProject {
					AndroidManifest = """
						<manifest xmlns:android="http://schemas.android.com/apk/res/android" package="com.example.isolation">
						  <application><uses-library android:name="example" /></application>
						</manifest>
						""",
				};
				using var builder = CreateApkBuilder (Path.Combine (path, "manifest"));
				builder.Save (proj);
				foreach (var sdk in new [] { secondSdk, firstSdk }) {
					var platformJar = Path.Combine (sdk, "platforms", "android-37", "android.jar");
					var optionalJar = Path.Combine (sdk, "platforms", "android-37", "optional", "example.jar");
					Directory.CreateDirectory (Path.GetDirectoryName (optionalJar));
					File.WriteAllText (optionalJar, "");
					var platform = new GetJavaPlatformJar {
						BuildEngine = engine,
						AndroidSdkDirectory = sdk,
						AndroidApiLevel = "37",
						SupportedOSPlatformVersion = XABuildConfig.AndroidMinimumDotNetApiLevel.ToString (),
					};
					Assert.IsTrue (platform.Execute ());
					Assert.AreEqual (platformJar, platform.JavaPlatformJarPath);
					var manifest = new ReadAndroidManifest {
						BuildEngine = engine,
						AndroidSdkDirectory = sdk,
						AndroidApiLevel = "37",
						ManifestFile = Path.Combine (Root, builder.ProjectDirectory, "Properties", "AndroidManifest.xml"),
					};
					Assert.IsTrue (manifest.Execute ());
					var libraries = manifest.UsesLibraries;
					Assert.IsNotNull (libraries);
					Assert.AreEqual (optionalJar, libraries.Single ().ItemSpec);
					var jarToXml = new JarToXmlCommandLine {
						BuildEngine = engine,
						AndroidSdkDirectory = sdk,
						AndroidApiLevel = "37",
						MonoAndroidToolsDirectory = resolveSdks.MonoAndroidToolsPath,
						OutputFile = Path.Combine (Root, path, "api.xml"),
					};
					StringAssert.Contains (platformJar, jarToXml.GetCommandLine ());
				}
			} finally {
				Directory.Delete (Path.Combine (Root, path), recursive: true);
			}
		}

		[TestCase (null, "XA5300")]
		[TestCase ("", "XA5300")]
		[TestCase ("missing", "XA5207")]
		public void PlatformJarWithMissingSdkDoesNotThrow (string sdkPath, string expectedError)
		{
			var errors = new List<BuildErrorEventArgs> ();
			var engine = new MockBuildEngine (TestContext.Out, errors);
			MonoAndroidHelper.RefreshSupportedVersions ([]);
			var jarToXml = new JarToXml {
				BuildEngine = engine,
				AndroidSdkDirectory = sdkPath == "missing" ? Path.Combine (Root, "temp", TestName, "missing-sdk") : sdkPath,
				AndroidApiLevel = "37",
				SourceJars = [new TaskItem ("source.jar")],
			};
			Assert.IsFalse (jarToXml.Execute ());
			Assert.AreEqual (expectedError, errors.Single ().Code);
		}

		class JarToXmlCommandLine : JarToXml
		{
			public string GetCommandLine () => GenerateCommandLineCommands ();
		}

	}
}
