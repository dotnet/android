using System;
using NUnit.Framework;
using Xamarin.ProjectTools;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Framework;
using System.Text;
using Xamarin.Android.Tasks;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Build.Tests {
	[TestFixture]
	[Category ("Node-2")]
	[Parallelizable (ParallelScope.Children)]
	public class GetManifestVersionsTests : BaseTest {

#pragma warning disable 414
static object [] ReadManifestVersionsTestCases () => new object [] {
	new object[] {
		/* versionCode */		"55555",
		/* versionName */		"33.4",
		/* expectedVersionCode */	"55555" ,
		/* expectedVersionName */	"33.4",
	},
	new object[] {
		/* versionCode */		"1",
		/* versionName */		"1.0",
		/* expectedVersionCode */	"1" ,
		/* expectedVersionName */	"1.0",
	},
};
#pragma warning restore 414

		[Test]
		[TestCaseSource (nameof(ReadManifestVersionsTestCases))]
		public void ReadManifestVersions (string versionCode, string versionName, string expectedVersionCode, string expectedVersionName)
		{
			var path = Path.Combine ("temp", TestName);
			Directory.CreateDirectory (Path.Combine (path, "manifest"));
			IBuildEngine engine = new MockBuildEngine (TestContext.Out);
			var task = new GetManifestVersions {
				BuildEngine = engine
			};
			string versionBlock = $"android:versionCode='{versionCode}' android:versionName='{versionName}'";
			File.WriteAllText (Path.Combine (path, "manifest", "AndroidManifest.xml"),
				$@"<?xml version='1.0' ?>
<manifest xmlns:android='http://schemas.android.com/apk/res/android' {versionBlock} android:package='Foo.Foo' />");
			task.ManifestPath = path;
			Assert.IsTrue (task.Execute ());
			Assert.AreEqual (1, task.ManifestVersions.Count());
			Assert.AreEqual (expectedVersionCode, task.ManifestVersions[0].GetMetadata ("VersionCode"));
			Assert.AreEqual (expectedVersionName, task.ManifestVersions[0].GetMetadata ("VersionName"));
		}
	}
}
