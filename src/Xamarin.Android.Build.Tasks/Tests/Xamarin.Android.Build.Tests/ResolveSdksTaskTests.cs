﻿using System;
using System.Collections.Generic;
using NUnit.Framework;
using Xamarin.ProjectTools;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using System.Text;
using Xamarin.Android.Tasks;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Build.Tests {

	[TestFixture]
	[Parallelizable (ParallelScope.Self)]
	public class ResolveSdksTaskTests : BaseTest {
#pragma warning disable 414

		static ApiInfo [] apiInfoSelection = new ApiInfo [] {
			new ApiInfo () { Id = "25", Level = 25, Name = "Nougat", FrameworkVersion = "v7.1",  Stable = true },
			new ApiInfo () { Id = "26", Level = 26, Name = "Oreo", FrameworkVersion = "v8.0",  Stable = true  },
			new ApiInfo () { Id = "27", Level = 27, Name = "Oreo", FrameworkVersion = "v8.1",  Stable = true  },
			new ApiInfo () { Id = "P",  Level = 28, Name = "P",    FrameworkVersion = "v8.99", Stable = false },
			new ApiInfo () { Id = "Z",  Level = 127, Name = "Z",    FrameworkVersion = "v108.1.99", Stable = false },
		};

		static object [] UseLatestAndroidSdkTestCases = new object [] {
			new object[] {
				/* buildtools */   "26.0.3",
				/* jdk */ "1.8.0",
				/* apis*/ apiInfoSelection,
				/* useLatestAndroidSdk */ true,
				/* targetFrameworkVersion */ "v8.99",
				/* expectedTaskResult */ true,
				/* expectedTargetFramework */ "v8.99",
				/* expectedError */ "",
				/* expectedErrorMessage */ "",
			},
			new object[] {
				/* buildtools */   "26.0.3",
				/* jdk */ "1.8.0",
				/* apis*/ apiInfoSelection,
				/* useLatestAndroidSdk */ true,
				/* targetFrameworkVersion */ "v8.0",
				/* expectedTaskResult */ true,
				/* expectedTargetFramework */ "v8.1",
				/* expectedError */ "",
				/* expectedErrorMessage */ "",
			},
			new object[] {
				/* buildtools */   "26.0.3",
				/* jdk */ "1.8.0",
				/* apis*/ apiInfoSelection,
				/* useLatestAndroidSdk */ true,
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
				/* useLatestAndroidSdk */ true,
				/* targetFrameworkVersion */ "v6.0",
				/* expectedTaskResult */ true,
				/* expectedTargetFramework */ "v8.1",
				/* expectedError */ "",
				/* expectedErrorMessage */ "",
			},
			new object[] {
				/* buildtools */   "26.0.3",
				/* jdk */ "1.8.0",
				/* apis*/ apiInfoSelection,
				/* useLatestAndroidSdk */ true,
				/* targetFrameworkVersion */ null,
				/* expectedTaskResult */ true,
				/* expectedTargetFramework */ "v8.1",
				/* expectedError */ "",
				/* expectedErrorMessage */ "",
			},
			new object[] {
				/* buildtools */   "26.0.3",
				/* jdk */ "1.8.0",
				/* apis*/ apiInfoSelection,
				/* useLatestAndroidSdk */ true,
				/* targetFrameworkVersion */ "v7.1",
				/* expectedTaskResult */ true,
				/* expectedTargetFramework */ "v8.1",
				/* expectedError */ "",
				/* expectedErrorMessage */ "",
			},
			new object[] {
				/* buildtools */   "26.0.3",
				/* jdk */ "1.8.0",
				/* apis*/ apiInfoSelection,
				/* useLatestAndroidSdk */ false,
				/* targetFrameworkVersion */ "v8.99",
				/* expectedTaskResult */ true,
				/* expectedTargetFramework */ "v8.99",
				/* expectedError */ "",
				/* expectedErrorMessage */ "",
			},
			new object[] {
				/* buildtools */   "26.0.3",
				/* jdk */ "1.8.0",
				/* apis*/ apiInfoSelection,
				/* useLatestAndroidSdk */ false,
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
				/* useLatestAndroidSdk */ false,
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
				/* useLatestAndroidSdk */ false,
				/* targetFrameworkVersion */ null,
				/* expectedTaskResult */ true,
				/* expectedTargetFramework */ "v8.1",
				/* expectedError */ "",
				/* expectedErrorMessage */ "",
			},
			new object[] {
				/* buildtools */   "26.0.3",
				/* jdk */ "1.8.0",
				/* apis*/ apiInfoSelection,
				/* useLatestAndroidSdk */ false,
				/* targetFrameworkVersion */ "v6.0",
				/* expectedTaskResult */ false,
				/* expectedTargetFramework */ "v6.0",
				/* expectedError */ "XA0001",
				/* expectedErrorMessage */ "Unsupported or invalid $(TargetFrameworkVersion) value of 'v6.0'. Please update your Project Options.",
			},
		};
		#pragma warning restore 414
		[Test]
		[TestCaseSource(nameof(UseLatestAndroidSdkTestCases))]
		public void UseLatestAndroidSdk (string buildtools, string jdk, ApiInfo[] apis, bool useLatestAndroidSdk, string targetFrameworkVersion, bool expectedTaskResult, string expectedTargetFramework, string expectedError = "", string expectedErrorMessage = "")
		{
			var path = Path.Combine ("temp", "UseLatestAndroidSdk_" + Guid.NewGuid ());
			var androidSdkPath = CreateFauxAndroidSdkDirectory (Path.Combine (path, "android-sdk"), buildtools, apis);
			string javaExe = string.Empty;
			string javacExe;
			var javaPath = CreateFauxJavaSdkDirectory (Path.Combine (path, "jdk"), jdk, out javaExe, out javacExe);
			var referencePath = CreateFauxReferencesDirectory (Path.Combine (path, "references"), apis);
			var errors = new List<BuildErrorEventArgs> ();
			IBuildEngine engine = new MockBuildEngine (TestContext.Out, errors);
			var task = new ResolveSdks {
				BuildEngine = engine
			};
			task.AndroidSdkPath = androidSdkPath;
			task.AndroidNdkPath = androidSdkPath;
			task.JavaSdkPath = javaPath;
			task.TargetFrameworkVersion = targetFrameworkVersion;
			task.AndroidSdkBuildToolsVersion = buildtools;
			task.BuildingInsideVisualStudio = "true";
			task.UseLatestAndroidPlatformSdk = useLatestAndroidSdk;
			task.AotAssemblies = false;
			task.LatestSupportedJavaVersion = "1.8.0";
			task.MinimumSupportedJavaVersion = "1.7.0";
			task.ReferenceAssemblyPaths = new string [] {
				Path.Combine (referencePath, "MonoAndroid"),
			};
			task.CacheFile = Path.Combine (Root, path, "sdk.xml");
			task.SequencePointsMode = "None";
			task.JavaToolExe = javaExe;
			task.JavacToolExe = javacExe;
			Assert.AreEqual (expectedTaskResult, task.Execute (), $"Task should have {(expectedTaskResult ? "succeeded" : "failed" )}.");
			Assert.AreEqual (expectedTargetFramework, task.TargetFrameworkVersion, $"TargetFrameworkVersion should be {expectedTargetFramework} but was {task.TargetFrameworkVersion}");
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
			string javaExe = string.Empty;
			string javacExe;
			var javaPath = CreateFauxJavaSdkDirectory (Path.Combine (path, "jdk"), "1.8.0", out javaExe, out javacExe);
			var referencePath = CreateFauxReferencesDirectory (Path.Combine (path, "references"), new ApiInfo [] {
				new ApiInfo () { Id = "26", Level = 26, Name = "Oreo", FrameworkVersion = "v8.0", Stable = true },
				new ApiInfo () { Id = "27", Level = 27, Name = "Oreo", FrameworkVersion = "v8.1", Stable = true },
			});
			IBuildEngine engine = new MockBuildEngine (TestContext.Out);
			var task = new ResolveSdks {
				BuildEngine = engine
			};
			task.AndroidSdkPath = androidSdkPath;
			task.AndroidNdkPath = androidSdkPath;
			task.JavaSdkPath = javaPath;
			task.TargetFrameworkVersion = "v8.0";
			task.AndroidSdkBuildToolsVersion = "26.0.3";
			task.BuildingInsideVisualStudio = "true";
			task.UseLatestAndroidPlatformSdk = false;
			task.AotAssemblies = false;
			task.LatestSupportedJavaVersion = "1.8.0";
			task.MinimumSupportedJavaVersion = "1.7.0";
			task.ReferenceAssemblyPaths = new string [] {
				Path.Combine (referencePath, "MonoAndroid"),
			};
			task.CacheFile = Path.Combine (Root, path, "sdk.xml");
			task.SequencePointsMode = "None";
			task.JavaToolExe = javaExe;
			task.JavacToolExe = javacExe;
			var start = DateTime.UtcNow;
			Assert.IsTrue (task.Execute ());
			var executionTime = DateTime.UtcNow - start;
			Assert.LessOrEqual (executionTime, TimeSpan.FromSeconds(2), "Task should not take more than 2 seconds to run.");
			Assert.AreEqual (task.AndroidApiLevel, "26", "AndroidApiLevel should be 26");
			Assert.AreEqual (task.TargetFrameworkVersion, "v8.0", "TargetFrameworkVersion should be v8.0");
			Assert.AreEqual (task.AndroidApiLevelName, "26", "AndroidApiLevelName should be 26");
			Assert.NotNull (task.ReferenceAssemblyPaths, "ReferenceAssemblyPaths should not be null.");
			Assert.AreEqual (task.ReferenceAssemblyPaths.Length, 1, "ReferenceAssemblyPaths should have 1 entry.");
			Assert.AreEqual (task.ReferenceAssemblyPaths[0], Path.Combine (referencePath, "MonoAndroid"), $"ReferenceAssemblyPaths should be {Path.Combine (referencePath, "MonoAndroid")}.");
			var expected = Path.Combine (Root);
			Assert.AreEqual (task.MonoAndroidToolsPath, expected, $"MonoAndroidToolsPath should be {expected}");
			expected += Path.DirectorySeparatorChar;
			if (task.MonoAndroidBinPath != expected) {
				//For non-Windows platforms, remove a directory such as "Darwin", MonoAndroidBinPath also has a trailing /
				var binPath = Path.GetDirectoryName (Path.GetDirectoryName (task.MonoAndroidBinPath)) + Path.DirectorySeparatorChar;
				Assert.AreEqual (binPath, expected, $"MonoAndroidBinPath should be {expected}");
			}
			Assert.AreEqual (task.MonoAndroidIncludePath, null, "MonoAndroidIncludePath should be null");
			Assert.AreEqual (task.AndroidSdkPath, androidSdkPath, $"AndroidSdkPath should be {androidSdkPath}");
			Assert.AreEqual (task.JavaSdkPath, javaPath, $"JavaSdkPath should be {javaPath}");
			expected = Path.Combine (androidSdkPath, "build-tools", "26.0.3");
			Assert.AreEqual (task.AndroidSdkBuildToolsPath, expected, $"AndroidSdkBuildToolsPath should be {expected}");
			Assert.AreEqual (task.AndroidSdkBuildToolsBinPath, expected, "AndroidSdkBuildToolsBinPath should be {expected}");
			Assert.AreEqual (task.ZipAlignPath, expected, "ZipAlignPath should be {expected}");
			Assert.AreEqual (task.AndroidSequencePointsMode, "None", "AndroidSequencePointsMode should be None");
			expected = Path.Combine (androidSdkPath, "tools");
			Assert.AreEqual (task.LintToolPath, expected, $"LintToolPath should be {expected}");
			expected = Path.Combine (androidSdkPath, "build-tools", "26.0.3", "lib", "apksigner.jar");
			Assert.AreEqual (task.ApkSignerJar, expected, $"ApkSignerJar should be {expected}");
			Assert.AreEqual (task.AndroidUseApkSigner, false, "AndroidUseApkSigner should be false");
			Assert.AreEqual (task.JdkVersion, "1.8.0", "JdkVersion should be 1.8.0");
			Assert.AreEqual (task.MinimumRequiredJdkVersion, "1.8", "MinimumRequiredJdkVersion should be 1.8");
			Directory.Delete (Path.Combine (Root, path), recursive: true);
		}

		static object [] TargetFrameworkPairingParameters = new [] {
			//We support 28, but only 27 is installed
			new object [] {
				"Older API Installed", //description
				// androidSdks
				new [] {
					new ApiInfo () { Id = "27", Level = 27, Name = "Oreo", FrameworkVersion = "v8.1", Stable = true },
				},
				// targetFrameworks
				new ApiInfo [] {
					new ApiInfo () { Id = "27", Level = 27, Name = "Oreo", FrameworkVersion = "v8.1", Stable = true },
					new ApiInfo () { Id = "28", Level = 28, Name = "P",    FrameworkVersion = "v9.0", Stable = true },
				},
				null,   //userSelected
				"27",   //androidApiLevel
				"27",   //androidApiLevelName
				"v8.1", //targetFrameworkVersion
			},
			//28 is installed but we only support 27
			new object [] {
				"Newer API Installed", //description
				// androidSdks
				new [] {
					new ApiInfo () { Id = "27", Level = 27, Name = "Oreo", FrameworkVersion = "v8.1", Stable = true },
					new ApiInfo () { Id = "28", Level = 28, Name = "P",    FrameworkVersion = "v9.0", Stable = true },
				},
				// targetFrameworks
				new ApiInfo [] {
					new ApiInfo () { Id = "27", Level = 27, Name = "Oreo", FrameworkVersion = "v8.1", Stable = true },
				},
				null,   //userSelected
				"27",   //androidApiLevel
				"27",   //androidApiLevelName
				"v8.1", //targetFrameworkVersion
			},
			//A paired downgrade to API 26
			new object [] {
				"Paired Downgrade", //description
				// androidSdks
				new [] {
					new ApiInfo () { Id = "26", Level = 26, Name = "Oreo", FrameworkVersion = "v8.0", Stable = true },
					new ApiInfo () { Id = "27", Level = 27, Name = "Oreo", FrameworkVersion = "v8.1", Stable = true },
				},
				// targetFrameworks
				new ApiInfo [] {
					new ApiInfo () { Id = "26", Level = 26, Name = "Oreo", FrameworkVersion = "v8.0", Stable = true },
					new ApiInfo () { Id = "28", Level = 28, Name = "P",    FrameworkVersion = "v9.0", Stable = true },
				},
				null,   //userSelected
				"26",   //androidApiLevel
				"26",   //androidApiLevelName
				"v8.0", //targetFrameworkVersion
			},
			//A new API level 28 is not stable yet
			new object [] {
				"New Unstable API", //description
				// androidSdks
				new [] {
					new ApiInfo () { Id = "26", Level = 26, Name = "Oreo", FrameworkVersion = "v8.0", Stable = true },
					new ApiInfo () { Id = "27", Level = 27, Name = "Oreo", FrameworkVersion = "v8.1", Stable = true },
					new ApiInfo () { Id = "28", Level = 28, Name = "P",    FrameworkVersion = "v9.0", Stable = false },
				},
				// targetFrameworks
				new ApiInfo [] {
					new ApiInfo () { Id = "26", Level = 26, Name = "Oreo", FrameworkVersion = "v8.0", Stable = true },
					new ApiInfo () { Id = "27", Level = 27, Name = "Oreo", FrameworkVersion = "v8.1", Stable = true },
					new ApiInfo () { Id = "28", Level = 28, Name = "P",    FrameworkVersion = "v9.0", Stable = false },
				},
				null,   //userSelected
				"27",   //androidApiLevel
				"27",   //androidApiLevelName
				"v8.1", //targetFrameworkVersion
			},
			//User selected a new API level 28 is not stable yet
			new object [] {
				"User Selected Unstable API", //description
				// androidSdks
				new [] {
					new ApiInfo () { Id = "26", Level = 26, Name = "Oreo", FrameworkVersion = "v8.0", Stable = true },
					new ApiInfo () { Id = "27", Level = 27, Name = "Oreo", FrameworkVersion = "v8.1", Stable = true },
					new ApiInfo () { Id = "28", Level = 28, Name = "P",    FrameworkVersion = "v9.0", Stable = false },
				},
				// targetFrameworks
				new ApiInfo [] {
					new ApiInfo () { Id = "26", Level = 26, Name = "Oreo", FrameworkVersion = "v8.0", Stable = true },
					new ApiInfo () { Id = "27", Level = 27, Name = "Oreo", FrameworkVersion = "v8.1", Stable = true },
					new ApiInfo () { Id = "28", Level = 28, Name = "P",    FrameworkVersion = "v9.0", Stable = false },
				},
				"v9.0", //userSelected
				"28",   //androidApiLevel
				"28",   //androidApiLevelName
				"v9.0", //targetFrameworkVersion
			},
		};

		[Test]
		[TestCaseSource (nameof (TargetFrameworkPairingParameters))]
		public void TargetFrameworkPairing (string description, ApiInfo[] androidSdk, ApiInfo[] targetFrameworks, string userSelected, string androidApiLevel, string androidApiLevelName, string targetFrameworkVersion)
		{
			var path = Path.Combine ("temp", $"{nameof (TargetFrameworkPairing)}_{description.Replace (' ', '_')}");
			var androidSdkPath = CreateFauxAndroidSdkDirectory (Path.Combine (path, "android-sdk"), "26.0.3", androidSdk);
			string javaExe = string.Empty;
			string javacExe;
			var javaPath = CreateFauxJavaSdkDirectory (Path.Combine (path, "jdk"), "1.8.0", out javaExe, out javacExe);
			var referencePath = CreateFauxReferencesDirectory (Path.Combine (path, "references"), targetFrameworks);
			var log = new StringBuilder ();
			IBuildEngine engine = new MockBuildEngine (new StringWriter (log));
			var resolveSdks = new ResolveSdks {
				BuildEngine = engine,
				AndroidSdkPath = androidSdkPath,
				AndroidNdkPath = androidSdkPath,
				JavaSdkPath = javaPath,
				JavaToolExe = javaExe,
				JavacToolExe = javacExe,
				ReferenceAssemblyPaths = new [] {
					Path.Combine (referencePath, "MonoAndroid"),
				},
				UseLatestAndroidPlatformSdk = true,
				TargetFrameworkVersion = userSelected,
				LatestSupportedJavaVersion = "1.8.0",
				MinimumSupportedJavaVersion = "1.7.0",
			};
			Assert.IsTrue (resolveSdks.Execute (), $"ResolveSdks should succeed! Failed with: {log}");
			Assert.AreEqual (androidApiLevel, resolveSdks.AndroidApiLevel, $"AndroidApiLevel should be {androidApiLevel}");
			Assert.AreEqual (androidApiLevelName, resolveSdks.AndroidApiLevelName, $"AndroidApiLevelName should be {androidApiLevelName}");
			Assert.AreEqual (targetFrameworkVersion, resolveSdks.TargetFrameworkVersion, $"TargetFrameworkVersion should be {targetFrameworkVersion}");
			Directory.Delete (Path.Combine (Root, path), recursive: true);
		}
	}
}
