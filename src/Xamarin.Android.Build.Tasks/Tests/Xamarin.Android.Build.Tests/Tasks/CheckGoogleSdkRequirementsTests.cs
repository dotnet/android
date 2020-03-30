using Microsoft.Build.Framework;
using NUnit.Framework;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Xamarin.Android.Tasks;
using Xamarin.Android.Tools;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests {
	[TestFixture]
	[Category ("Node-2")]
	public class CheckGoogleSdkRequirementsTests : BaseTest {
		List<BuildErrorEventArgs> errors;
		List<BuildWarningEventArgs> warnings;
		MockBuildEngine engine;

		[SetUp]
		public void Setup ()
		{
			var path = Path.Combine ("temp", TestName);
			engine = new MockBuildEngine (TestContext.Out, errors = new List<BuildErrorEventArgs> (), warnings = new List<BuildWarningEventArgs> ());
			var referencePath = CreateFauxReferencesDirectory (Path.Combine (path, "references"), new [] {
				new ApiInfo { Id = "27", Level = 27, Name = "Oreo", FrameworkVersion = "v8.1",  Stable = true },
				new ApiInfo { Id = "28", Level = 28, Name = "Pie", FrameworkVersion = "v9.0",  Stable = true },
			});
			MonoAndroidHelper.RefreshSupportedVersions (new [] {
				Path.Combine (referencePath, "MonoAndroid"),
			});
		}

		[TearDown]
		public void TearDown ()
		{
			var path = Path.Combine ("temp", TestName);
			Directory.Delete (Path.Combine (Root, path), recursive: true);
		}

		string CreateManiestFile (int minSDk, int targetSdk) {
			var manifest = Path.Combine (Root, "temp", TestName, "AndroidManifest.xml");
			File.WriteAllText (manifest, string.Format (@"<manifest xmlns:android='http://schemas.android.com/apk/res/android' android:versionCode='1' android:versionName='1.0' package='Foo.Foo'>
	<uses-sdk android:minSdkVersion = '{0}' android:targetSdkVersion = '{1}' />
</manifest>
", minSDk, targetSdk));
			return manifest;
		}

		[Test]
		public void CheckManifestIsOK ()
		{
			var task = new CheckGoogleSdkRequirements () {
				BuildEngine = engine,
				TargetFrameworkVersion = "v9.0",
				ManifestFile = CreateManiestFile (10, 28),
			};
			Assert.True (task.Execute (), "Task should have succeeded.");
			Assert.AreEqual (0, errors.Count, "There should be 0 errors reported.");
			Assert.AreEqual (0, warnings.Count, "There should be 0 warnings reported.");
		}

		[Test]
		public void CheckManifestTargetSdkLowerThanCompileSdk ()
		{
			var task = new CheckGoogleSdkRequirements () {
				BuildEngine = engine,
				TargetFrameworkVersion = "v9.0",
				ManifestFile = CreateManiestFile (10, 27),
			};
			Assert.True (task.Execute (), "Task should have succeeded.");
			Assert.AreEqual (0, errors.Count, "There should be 0 errors reported.");
			Assert.AreEqual (1, warnings.Count, "There should be 1 warning reported.");
		}

		[Test]
		public void CheckManifestCompileSdkLowerThanTargetSdk ()
		{
			var task = new CheckGoogleSdkRequirements () {
				BuildEngine = engine,
				TargetFrameworkVersion = "v8.1",
				ManifestFile = CreateManiestFile (10, 28),
			};
			Assert.True (task.Execute (), "Task should have succeeded.");
			Assert.AreEqual (0, errors.Count, "There should be 1 error reported.");
			Assert.AreEqual (1, warnings.Count, "There should be 0 warnings reported.");
		}

		[Test]
		public void CheckManifestMinSdkLowerThanTargetSdk ()
		{
			var task = new CheckGoogleSdkRequirements () {
				BuildEngine = engine,
				TargetFrameworkVersion = "v8.1",
				ManifestFile = CreateManiestFile (28, 27),
			};
			Assert.True (task.Execute (), "Task should have succeeded.");
			Assert.AreEqual (0, errors.Count, "There should be 0 error reported.");
			Assert.AreEqual (1, warnings.Count, "There should be 1 warnings reported.");
		}
	}
}
