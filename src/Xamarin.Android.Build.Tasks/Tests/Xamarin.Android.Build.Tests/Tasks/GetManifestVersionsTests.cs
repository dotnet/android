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
	[Category ("Node-2")]
	[Parallelizable (ParallelScope.Children)]
	public class GetManifestVersionsTests : BaseTest {

#pragma warning disable 414
static object [] ReadManifestVersionsTestCases () => new object [] {
	new object[] {
		/* includeVersion */		true,
		/* versionCode */		"55555",
		/* versionName */		"33.4",
		/* expectedVersionCode */	"55555" ,
		/* expectedVersionName */	"33.4",
	},
	new object[] {
		/* includeVersion */		false,
		/* versionCode */		"1",
		/* versionName */		"1.0",
		/* expectedVersionCode */	"1" ,
		/* expectedVersionName */	"1.0",
	},
};
#pragma warning restore 414

		[Test]
		[TestCaseSource (nameof(ReadManifestVersionsTestCases))]
		public void ReadManifestVersions (bool includeVersion, string versionCode, string versionName, string expectedVersionCode, string expectedVersionName)
		{
			var path = Path.Combine ("temp", TestName);
			Directory.CreateDirectory (path);
			IBuildEngine engine = new MockBuildEngine (TestContext.Out);
			var task = new GetManifestVersions {
				BuildEngine = engine
			};
			string versionBlock = string.Empty;
			if (includeVersion)
				versionBlock = $"android:versionCode='{versionCode}' android:versionName='{versionName}'";
			File.WriteAllText (Path.Combine (path, "AndroidManifest.xml"),
				$@"<?xml version='1.0' ?>
<manifest xmlns:android='http://schemas.android.com/apk/res/android' {versionBlock} android:package='Foo.Foo' />");
			task.VersionCode = versionCode;
			task.VersionName = versionName;
			task.ManifestFile = new TaskItem (Path.Combine (path, "AndroidManifest.xml"));
			Assert.IsTrue (task.Execute ());
			Assert.IsNotNull (task.VersionCode);
			Assert.IsNotNull (task.VersionName);

		}
	}
}
