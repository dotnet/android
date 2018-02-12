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
			IBuildEngine engine = new MockBuildEngine (TestContext.Out);
			var task = new CalculateProjectDependencies {
				BuildEngine = engine
			};

			task.BuildToolsVersion = "26.0.1";
			task.TargetFrameworkVersion = "v8.0";
			task.ManifestFile = new TaskItem ("AndroidManifest.xml");
			task.Execute ();
			Assert.IsNotNull (task.Dependencies);
			Assert.AreEqual (4, task.Dependencies.Length);
			Assert.IsNotNull (task.Dependencies.FirstOrDefault (x => x.ItemSpec == "build-tool" && x.GetMetadata ("Version") == "26.0.1"),
				"Dependencies should contains a build-tool version 26.0.1");
			Assert.IsNotNull (task.Dependencies.FirstOrDefault (x => x.ItemSpec == "platform" && x.GetMetadata ("Version") == "26"),
				"Dependencies should contains a platform version 26");
		}

		[Test]
		public void ManifestFileExists ()
		{
			IBuildEngine engine = new MockBuildEngine (TestContext.Out);
			var task = new CalculateProjectDependencies {
				BuildEngine = engine
			};

			var path = Path.Combine (Root, "temp", TestName);
			Directory.CreateDirectory (path);
			var manifestFile = Path.Combine (path, "AndroidManifest.xml");
			File.WriteAllText (manifestFile, @"<?xml version='1.0' ?>
<manifest xmlns:android='http://schemas.android.com/apk/res/android' android:versionCode='1' android:versionName='1.0' package='Mono.Android_Tests'>
	<uses-sdk android:minSdkVersion='10' />
</manifest>");

			task.BuildToolsVersion = "26.0.1";
			task.TargetFrameworkVersion = "v8.0";
			task.ManifestFile = new TaskItem (manifestFile);
			Assert.IsTrue(task.Execute ());
			Assert.IsNotNull (task.Dependencies);
			Assert.AreEqual (5, task.Dependencies.Length);
			Assert.IsNotNull (task.Dependencies.FirstOrDefault (x => x.ItemSpec == "build-tool" && x.GetMetadata ("Version") == "26.0.1"),
				"Dependencies should contain a build-tool version 26.0.1");
			Assert.IsNotNull (task.Dependencies.FirstOrDefault (x => x.ItemSpec == "platform" && x.GetMetadata ("Version") == "26"),
				"Dependencies should contain a platform version 26");
			Assert.IsNotNull (task.Dependencies.FirstOrDefault (x => x.ItemSpec == "platform" && x.GetMetadata ("Version") == "10"),
				"Dependencies should contain a platform version 10");

			Directory.Delete (path, recursive: true);
		}
	}
}
