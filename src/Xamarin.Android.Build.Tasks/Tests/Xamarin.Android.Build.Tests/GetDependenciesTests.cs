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
	public class GetDependenciesTest : BaseTest {
		
		[Test]
		public void ManifestFileDoesNotExist ()
		{
			var path = Path.Combine ("temp", TestName);
			var referencePath = CreateFauxReferencesDirectory (Path.Combine (path, "references"), new ApiInfo[] {
				new ApiInfo () { Id = 26, Level = 26, Name = "Oreo", FrameworkVersion = "v8.0", Stable = true },
			} );
			MonoAndroidHelper.RefreshSupportedVersions (new string [] { referencePath });
			IBuildEngine engine = new MockBuildEngine (TestContext.Out);
			var task = new CalculateProjectDependencies {
				BuildEngine = engine
			};

			task.PlatformToolsVersion = "26.0.3";
			task.ToolsVersion = "26.0.1";
			task.NdkVersion = "r12d";
			task.BuildToolsVersion = "26.0.1";
			task.TargetFrameworkVersion = "v8.0";
			task.ManifestFile = new TaskItem (Path.Combine (path, "AndroidManifest.xml"));
			Assert.IsTrue (task.Execute ());
			Assert.IsNotNull (task.Dependencies);
			Assert.AreEqual (5, task.Dependencies.Length);
			Assert.IsNotNull (task.Dependencies.FirstOrDefault (x => x.ItemSpec == "build-tools;26.0.1" && x.GetMetadata ("Version") == "26.0.1"),
				"Dependencies should contains a build-tools version 26.0.1");
			Assert.IsNotNull (task.Dependencies.FirstOrDefault (x => x.ItemSpec == "tools" && x.GetMetadata ("Version") == "26.0.1"),
				"Dependencies should contains a tools version 26.0.1");
			Assert.IsNotNull (task.Dependencies.FirstOrDefault (x => x.ItemSpec == "platforms;android-26" && x.GetMetadata ("Version") == ""),
				"Dependencies should contains a platform version android-26");
			Assert.IsNotNull (task.Dependencies.FirstOrDefault (x => x.ItemSpec == "platform-tools" && x.GetMetadata ("Version") == "26.0.3"),
				"Dependencies should contains a platform-tools version 26.0.3");
			Assert.IsNotNull (task.Dependencies.FirstOrDefault (x => x.ItemSpec == "ndk-bundle" && x.GetMetadata ("Version") == "r12d"),
				"Dependencies should contains a ndk-bundle version r12d");
		}

		[Test]
		public void ManifestFileExists ()
		{
			var path = Path.Combine (Root, "temp", TestName);
			var referencePath = CreateFauxReferencesDirectory (Path.Combine (path, "references"), new ApiInfo[] {
				new ApiInfo () { Id = 26, Level = 26, Name = "Oreo", FrameworkVersion = "v8.0", Stable = true },
			} );
			MonoAndroidHelper.RefreshSupportedVersions (new string [] { referencePath });
			IBuildEngine engine = new MockBuildEngine (TestContext.Out);
			var task = new CalculateProjectDependencies {
				BuildEngine = engine
			};


			Directory.CreateDirectory (path);
			var manifestFile = Path.Combine (path, "AndroidManifest.xml");
			File.WriteAllText (manifestFile, @"<?xml version='1.0' ?>
<manifest xmlns:android='http://schemas.android.com/apk/res/android' android:versionCode='1' android:versionName='1.0' package='Mono.Android_Tests'>
	<uses-sdk android:minSdkVersion='10' />
</manifest>");

			task.PlatformToolsVersion = "26.0.3";
			task.ToolsVersion = "26.0.1";
			task.NdkVersion = "r12d";
			task.BuildToolsVersion = "26.0.1";
			task.TargetFrameworkVersion = "v8.0";
			task.ManifestFile = new TaskItem (manifestFile);
			Assert.IsTrue(task.Execute ());
			Assert.IsNotNull (task.Dependencies);
			Assert.AreEqual (5, task.Dependencies.Length);
			Assert.IsNotNull (task.Dependencies.FirstOrDefault (x => x.ItemSpec == "build-tools;26.0.1" && x.GetMetadata ("Version") == "26.0.1"),
				"Dependencies should contains a build-tools version 26.0.1");
			Assert.IsNotNull (task.Dependencies.FirstOrDefault (x => x.ItemSpec == "tools" && x.GetMetadata ("Version") == "26.0.1"),
				"Dependencies should contains a tools version 26.0.1");
			Assert.IsNotNull (task.Dependencies.FirstOrDefault (x => x.ItemSpec == "platforms;android-26" && x.GetMetadata ("Version") == ""),
				"Dependencies should contains a platform version android-26");
			Assert.IsNotNull (task.Dependencies.FirstOrDefault (x => x.ItemSpec == "platform-tools" && x.GetMetadata ("Version") == "26.0.3"),
				"Dependencies should contains a platform-tools version 26.0.3");
			Assert.IsNotNull (task.Dependencies.FirstOrDefault (x => x.ItemSpec == "ndk-bundle" && x.GetMetadata ("Version") == "r12d"),
				"Dependencies should contains a ndk-bundle version r12d");

			Directory.Delete (path, recursive: true);
		}
	}
}
