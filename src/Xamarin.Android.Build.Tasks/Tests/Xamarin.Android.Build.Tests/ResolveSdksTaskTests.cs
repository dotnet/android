using System;
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
	[Parallelizable (ParallelScope.Children)]
	public class ResolveSdksTaskTests : BaseTest {
		[Test]
		public void ResolveSdkTiming ()
		{
			var path = Path.Combine ("temp", TestName);
			var androidSdkPath = CreateFauxAndroidSdkDirectory (Path.Combine (path, "android-sdk"), "26.0.3");
			string javaExe = string.Empty;
			var javaPath = CreateFauxJavaSdkDirectory (Path.Combine (path, "jdk"), "1.8.0", out javaExe);
			var referencePath = CreateFauxReferencesDirectory (Path.Combine (path, "references"), new ApiInfo [] {
				new ApiInfo () { Id = 26, Level = 26, Name = "Oreo", FrameworkVersion = "v8.0", Stable = true },
				new ApiInfo () { Id = 27, Level = 27, Name = "Oreo", FrameworkVersion = "v8.1", Stable = true },
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
			var start = DateTime.UtcNow;
			Assert.IsTrue (task.Execute ());
			var executionTime = DateTime.UtcNow - start;
			Assert.LessOrEqual (executionTime, TimeSpan.FromSeconds(1), "Task should not take more than 1 second to run.");
			Assert.AreEqual (task.AndroidApiLevel, "26", "AndroidApiLevel should be 26");
			Assert.AreEqual (task.TargetFrameworkVersion, "v8.0", "TargetFrameworkVersion should be v8.0");
			Assert.AreEqual (task.AndroidApiLevelName, "26", "AndroidApiLevelName should be 26");
			Assert.AreEqual (task.SupportedApiLevel, "26", "SupportedApiLevel should be 26");
			Assert.NotNull (task.ReferenceAssemblyPaths, "ReferenceAssemblyPaths should not be null.");
			Assert.AreEqual (task.ReferenceAssemblyPaths.Length, 1, "ReferenceAssemblyPaths should have 1 entry.");
			Assert.AreEqual (task.ReferenceAssemblyPaths[0], Path.Combine (referencePath, "MonoAndroid"), $"ReferenceAssemblyPaths should be {Path.Combine (referencePath, "MonoAndroid")}.");
			var expected = Path.Combine (Root);
			Assert.AreEqual (task.MonoAndroidToolsPath, expected, $"MonoAndroidToolsPath should be {expected}");
			expected = Path.Combine (Root, "Darwin" + Path.DirectorySeparatorChar);
			Assert.AreEqual (task.MonoAndroidBinPath, expected, $"MonoAndroidBinPath should be {expected}");
			Assert.AreEqual (task.MonoAndroidIncludePath, null, "MonoAndroidIncludePath should be null");
			//Assert.AreEqual (task.AndroidNdkPath, "26", "AndroidNdkPath should be 26");
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
		}
	}
}
