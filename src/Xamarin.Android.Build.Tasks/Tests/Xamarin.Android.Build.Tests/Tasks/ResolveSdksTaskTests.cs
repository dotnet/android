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
using System.Xml;
using System.Xml.Linq;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;

namespace Xamarin.Android.Build.Tests {

	[TestFixture]
	[NonParallelizable] // NOTE: This test was hanging without this
	public class ResolveSdksTaskTests : BaseTest {
		static ApiInfo [] apiInfoSelection = new ApiInfo [] {
			new ApiInfo () { Id = "35", Level = 35, Name = "Android15", FrameworkVersion = "v15.0", Stable = true },
			new ApiInfo () { Id = "36", Level = 36, Name = "Android16", FrameworkVersion = "v16.0", Stable = true },
			new ApiInfo () { Id = "Preview", Level = 37, Name = "Preview", FrameworkVersion = "v16.0.99", Stable = false },
		};

		// via Xamarin.Android.Common.props
		const   string  MinimumSupportedJavaVersion     = "1.6.0";
		const   string  LatestSupportedJavaVersion      = "11.0.99";

		[TestCase ("36.0.0", "35.0", "35", "35")]
		[TestCase ("36.0.0", "36.0", "36", "36")]
		[TestCase ("36.0.0", "36.1", "36.1", "36.1")]
		[TestCase ("37.0.0", "37.0", "37", "Preview")]
		[TestCase ("36.0.0", null, "36", "36")]
		[TestCase ("36.0.0", "", "36", "36")]
		[TestCase ("36.0.0", "invalid", "36", "36")]
		public void ResolveAndroidToolingTest (string buildtools, string targetPlatformVersion, string expectedApiLevel, string expectedApiLevelName)
		{
			var path = Path.Combine ("temp", "ResolveAndroidToolingTest_" + Guid.NewGuid ());
			var androidSdkPath = CreateFauxAndroidSdkDirectory (Path.Combine (path, "android-sdk"), buildtools, apiInfoSelection);
			var androidNdkPath = CreateFauxAndroidNdkDirectory (Path.Combine (path, "android-ndk"));
			string javaExe = string.Empty;
			string javacExe;
			var javaPath = CreateFauxJavaSdkDirectory (Path.Combine (path, "jdk"), "21.0.8", out javaExe, out javacExe);
			var referencePath = CreateFauxReferencesDirectory (Path.Combine (path, "references"), apiInfoSelection);
			var errors = new List<BuildErrorEventArgs> ();
			IBuildEngine engine = new MockBuildEngine (TestContext.Out, errors);
			var resolveSdks = new ResolveSdks {
				BuildEngine = engine,
				AndroidSdkPath = androidSdkPath,
				AndroidNdkPath = androidNdkPath,
				JavaSdkPath = javaPath,
				MinimumSupportedJavaVersion = "21.0",
				LatestSupportedJavaVersion = "25.0.99",
				ReferenceAssemblyPaths = new [] {
					Path.Combine (referencePath, "MonoAndroid"),
				},
			};
			var validateJavaVersion = new ValidateJavaVersion {
				BuildEngine = engine,
				JavaSdkPath = javaPath,
				JavaToolExe = javaExe,
				JavacToolExe = javacExe,
				LatestSupportedJavaVersion = "25.0.99",
				MinimumSupportedJavaVersion = "21.0",
			};
			var androidTooling = new ResolveAndroidTooling {
				BuildEngine = engine,
				ReferenceAssemblyPaths = resolveSdks.ReferenceAssemblyPaths,
				AndroidSdkPath = androidSdkPath,
				TargetPlatformVersion = targetPlatformVersion,
				AndroidSdkBuildToolsVersion = buildtools,
			};
			try {
				Assert.IsTrue (resolveSdks.Execute (), "ResolveSdks should succeed.");
				Assert.IsTrue (validateJavaVersion.Execute (), "ValidateJavaVersion should succeed.");
				Assert.IsTrue (androidTooling.Execute (), "ResolveAndroidTooling should succeed.");
				Assert.AreEqual (targetPlatformVersion, androidTooling.TargetPlatformVersion, "TargetPlatformVersion should not be modified.");
				Assert.AreEqual (expectedApiLevel, androidTooling.AndroidApiLevel);
				Assert.AreEqual (expectedApiLevelName, androidTooling.AndroidApiLevelName);
				Assert.AreEqual (Path.Combine (androidSdkPath, "build-tools", buildtools), androidTooling.AndroidSdkBuildToolsPath);
				Assert.IsNotEmpty (androidTooling.Aapt2Version);
				Assert.IsEmpty (errors);
			} finally {
				Directory.Delete (Path.Combine (Root, path), recursive: true);
			}
		}

		[Test, NonParallelizable]
		public void ResolveSdkOutputs ()
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
				ReferenceAssemblyPaths = resolveSdks.ReferenceAssemblyPaths,
				AndroidSdkPath = androidSdkPath,
				TargetPlatformVersion = "26.0",
				AndroidSdkBuildToolsVersion = "26.0.3",
			};
			Assert.IsTrue (resolveSdks.Execute (), "ResolveSdks should succeed!");
			Assert.IsTrue (validateJavaVersion.Execute (), "ValidateJavaVersion should succeed!");
			Assert.IsTrue (androidTooling.Execute (), "ResolveAndroidTooling should succeed!");
			Assert.AreEqual (androidTooling.AndroidApiLevel, "26", "AndroidApiLevel should be 26");
			Assert.AreEqual ("26.0", androidTooling.TargetPlatformVersion, "TargetPlatformVersion should be unchanged");
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
				m.Message.StartsWith ("  TargetFrameworkVersion:", StringComparison.Ordinal) ||
				m.Message.StartsWith ("  TargetPlatformVersion:", StringComparison.Ordinal)));
			Directory.Delete (Path.Combine (Root, path), recursive: true);
		}

		[Test]
		public void ResolveAndroidToolingCachesAapt2Version ()
		{
			var path = Path.Combine ("temp", TestName);
			var sdkPath = CreateFauxAndroidSdkDirectory (Path.Combine (path, "android-sdk"), "36.0.0");
			var messages = new List<BuildMessageEventArgs> ();
			var engine = new MockBuildEngine (TestContext.Out, messages: messages);
			try {
				string version = "";
				for (int i = 0; i < 2; i++) {
					messages.Clear ();
					var tooling = new ResolveAndroidTooling {
						BuildEngine = engine,
						AndroidSdkPath = sdkPath,
						TargetPlatformVersion = "36.0",
					};
					Assert.IsTrue (tooling.Execute ());
					Assert.IsNotEmpty (tooling.Aapt2Version);
					if (i == 0)
						version = tooling.Aapt2Version;
					Assert.AreEqual (version, tooling.Aapt2Version);
					Assert.AreEqual (i == 1, messages.Any (m => m.Message == $"Using cached value for Aapt2Version: {version}"));
				}
			} finally {
				Directory.Delete (Path.Combine (Root, path), recursive: true);
			}
		}

		[Test]
		public void ResolveAndroidToolingRejectsMissingAapt2Override ()
		{
			var path = Path.Combine ("temp", TestName);
			var sdkPath = CreateFauxAndroidSdkDirectory (Path.Combine (path, "android-sdk"), "36.0.0");
			var errors = new List<BuildErrorEventArgs> ();
			var aapt2Path = Path.Combine (sdkPath, "missing-aapt2");
			try {
				var tooling = new ResolveAndroidTooling {
					BuildEngine = new MockBuildEngine (TestContext.Out, errors),
					AndroidSdkPath = sdkPath,
					TargetPlatformVersion = "36.0",
					Aapt2ToolPath = aapt2Path,
				};
				Assert.IsFalse (tooling.Execute ());
				Assert.AreEqual ("XA0112", errors.Single ().Code);
				StringAssert.Contains (aapt2Path, errors [0].Message);
			} finally {
				Directory.Delete (Path.Combine (Root, path), recursive: true);
			}
		}

		[Test]
		[TestCase (false, false)]
		[TestCase (false, true)]
		[TestCase (true, false)]
		[TestCase (true, true)]
		public void ResolvedToolingInputsEscapeApostrophes (bool tooling, bool changed)
		{
			var file = tooling ? Path.Combine ("Microsoft.Android.Sdk", "targets", "Microsoft.Android.Sdk.Tooling.targets") : "Xamarin.Android.Tooling.targets";
			var document = XDocument.Load (Path.Combine (XABuildPaths.TopDirectory, "src", "Xamarin.Android.Build.Tasks", file));
			var itemName = tooling ? "_AndroidToolingInput" : "_AndroidSdkInput";
			var changedItemName = tooling ? "_AndroidChangedToolingInput" : "_AndroidChangedSdkInput";
			var inputGroups = document.Descendants ().Where (e => e.Name.LocalName == "ItemGroup" &&
				e.Elements ().Any (i => i.Name.LocalName == itemName || i.Name.LocalName == changedItemName));
			var input = tooling ? "LintToolPath" : "AndroidSdkDirectory";
			var outerInput = tooling ? "_AndroidOuterToolingInputLintToolPath" : "_AndroidOuterSdkInputAndroidSdkDirectory";
			const string value = "O'Brien/sdk;a=b";
			var globals = new Dictionary<string, string> {
				[input] = value,
				[outerInput] = changed ? value.ToUpperInvariant () : value,
				["_ComputeFilesToPublishForRuntimeIdentifiers"] = "true",
				["_AndroidUseResolvedSdk"] = "true",
			};
			var outerInputPrefix = tooling ? "_AndroidOuterToolingInput" : "_AndroidOuterSdkInput";
			foreach (var item in inputGroups.Elements ().Where (e => e.Name.LocalName == itemName)) {
				var include = (string?) item.Attribute ("Include");
				if (include == null)
					continue;
				foreach (var identity in include.Split (';')) {
					var propertyName = (string?) item.Attribute ("PropertyName") ?? identity;
					if (propertyName != "MSBuildProjectFullPath")
						globals.TryAdd (propertyName, "");
					globals.TryAdd (outerInputPrefix + identity, "");
				}
			}
			var ns = document.Root?.Name.Namespace ?? XNamespace.None;
			var projectXml = new XElement (ns + "Project",
				new XElement (ns + "Target", new XAttribute ("Name", "CaptureInputs"), inputGroups.Select (e => new XElement (e))));
			using var reader = XmlReader.Create (new StringReader (projectXml.ToString ()));
			using var collection = new ProjectCollection ();
			var root = ProjectRootElement.Create (reader, collection);
			root.FullPath = Path.Combine (Root, "EscapingProbe.proj");
			globals ["_AndroidOuterSdkInputMSBuildProjectFullPath"] = root.FullPath;
			var instance = new ProjectInstance (root, globals, toolsVersion: null, collection);
			Assert.IsTrue (instance.Build ("CaptureInputs", []), "Apostrophes must not break MSBuild property functions.");
			Assert.AreEqual (changed ? 1 : 0, instance.GetItems (changedItemName).Count,
				"Input matching must remain ordinal: " + string.Join (", ", instance.GetItems (changedItemName).Select (i =>
					$"{i.EvaluatedInclude}={i.GetMetadataValue ("EscapedValue")} vs {i.GetMetadataValue ("EscapedOuterValue")}")));
		}

		[Test]
		public void SupportedVersionsAreIsolatedAcrossConcurrentTasks ()
		{
			var path = Path.Combine ("temp", TestName);
			var sdkPaths = new string [2];
			var referencePaths = new string [2][];
			for (int i = 0; i < 2; i++) {
				var apis = new [] {
					new ApiInfo { Id = "Z", Level = 127 + i, Name = "Preview", FrameworkVersion = $"v108.{i}", Stable = true },
				};
				sdkPaths [i] = CreateFauxAndroidSdkDirectory (Path.Combine (path, $"sdk-{i}"), "26.0.3", apis);
				referencePaths [i] = [CreateFauxReferencesDirectory (Path.Combine (path, $"references-{i}"), apis)];
			}
			var engine = new MockBuildEngine (TextWriter.Synchronized (new StringWriter ()));
			try {
				var first = MonoAndroidHelper.GetSupportedVersions (engine, referencePaths [0]);
				var second = MonoAndroidHelper.GetSupportedVersions (engine, referencePaths [1]);
				Assert.AreNotSame (first, second);
				Assert.AreSame (first, MonoAndroidHelper.GetSupportedVersions (engine, referencePaths [0]));
				Assert.AreSame (first, MonoAndroidHelper.GetSupportedVersions (engine,
					referencePaths [0].Select (p => Path.GetRelativePath (Environment.CurrentDirectory, p)).ToArray ()));
				Assert.AreNotSame (first, MonoAndroidHelper.GetSupportedVersions (new MockBuildEngine (TextWriter.Null), referencePaths [0]));
				Assert.AreEqual (127, first.GetApiLevelFromId ("Z"));
				Assert.AreEqual (128, second.GetApiLevelFromId ("Z"));
				var firstManifest = new ManifestDocument (null, first);
				var secondManifest = new ManifestDocument (null, second);
				Assert.AreEqual (127, firstManifest.VersionResolver.GetApiLevelFromId ("Z"));
				Assert.AreEqual (128, secondManifest.VersionResolver.GetApiLevelFromId ("Z"));
				Assert.IsNull (new ManifestDocument (null).VersionResolver.GetApiLevelFromId ("Z"));
				var concurrentEngine = new MockBuildEngine (TextWriter.Synchronized (new StringWriter ()));
				System.Threading.Tasks.Parallel.For (0, 64, i => {
					var context = i % 2;
					var task = new GetJavaPlatformJar {
						BuildEngine = concurrentEngine,
						AndroidSdkDirectory = sdkPaths [context],
						ReferenceAssemblyPaths = referencePaths [context],
						AndroidApiLevel = "Z",
						TargetFramework = "net11.0-android",
						SupportedOSPlatformVersion = "24.0",
					};
					Assert.IsTrue (task.Execute ());
					Assert.AreEqual ((127 + context).ToString (), task.TargetSdkVersion);
					Assert.AreEqual (Path.Combine (sdkPaths [context], "platforms", "android-Z", "android.jar"), task.JavaPlatformJarPath);
				});
			} finally {
				Directory.Delete (Path.Combine (Root, path), recursive: true);
			}
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
				var androidTooling = new ResolveAndroidTooling {
					BuildEngine = engine,
					AndroidSdkPath = sdkPath == "empty" ? path : sdkPath == "missing" ? Path.Combine (path, "missing") : sdkPath,
					AndroidSdkBuildToolsVersion = "36.0.0",
					TargetPlatformVersion = "37.0",
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
				var tooling = new ResolveAndroidTooling {
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
