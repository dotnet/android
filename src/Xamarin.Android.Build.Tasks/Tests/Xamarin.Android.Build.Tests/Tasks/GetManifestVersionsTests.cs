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
		/* abi */                       "x86;arm64-v8a",
		/* versionCode */		"55555;66666",
		/* versionName */		"33.4;33.4",
		/* expectedVersionCode */	"55555;66666" ,
		/* expectedVersionName */	"33.4;33.4",
	},
	new object[] {
		/* abi */                       "manifest;x86;arm64-v8a",
		/* versionCode */		"1;55555;66666",
		/* versionName */		"1.0;33.4;33.4",
		/* expectedVersionCode */	"1;55555;66666" ,
		/* expectedVersionName */	"1.0;33.4;33.4",
	},
	new object[] {
		/* abi */                       "manifest",
		/* versionCode */		"1",
		/* versionName */		"1.0",
		/* expectedVersionCode */	"1" ,
		/* expectedVersionName */	"1.0",
	},
	new object[] {
		/* abi */                       "manifest;fake",
		/* versionCode */		"1;0",
		/* versionName */		"1.0;0",
		/* expectedVersionCode */	"1" ,
		/* expectedVersionName */	"1.0",
	},
};
#pragma warning restore 414

		[Test]
		[TestCaseSource (nameof(ReadManifestVersionsTestCases))]
		public void ReadManifestVersions (string abi, string versionCode, string versionName, string expectedVersionCode, string expectedVersionName)
		{
			char[] splitChars = new char[] {';'};
			var path = Path.Combine ("temp", TestName);
			Directory.CreateDirectory (Path.Combine (path));
			IBuildEngine engine = new MockBuildEngine (TestContext.Out);
			var task = new GetManifestVersions {
				BuildEngine = engine
			};
			string[] abis = abi.Split (splitChars);
			string[] versionCodes = versionCode.Split (splitChars);
			string[] versionNames = versionName.Split (splitChars);
			Assert.AreEqual (versionCodes.Length, versionNames.Length);
			string[] expectedVersionCodes = expectedVersionCode.Split (splitChars, StringSplitOptions.RemoveEmptyEntries);
			string[] expectedVersionNames = expectedVersionName.Split (splitChars, StringSplitOptions.RemoveEmptyEntries);
			for (int i=0; i < abis.Length; i ++) {
				string versionBlock = $"android:versionCode='{versionCodes[i]}' android:versionName='{versionNames[i]}'";
				Directory.CreateDirectory (Path.Combine (path, abis[i]));
				File.WriteAllText (Path.Combine (path, abis[i], "AndroidManifest.xml"),
					$@"<?xml version='1.0' ?>
	<manifest xmlns:android='http://schemas.android.com/apk/res/android' {versionBlock} android:package='Foo.Foo' />");
			}
			task.ManifestPath = path;
			Assert.IsTrue (task.Execute ());
			Assert.AreEqual (expectedVersionCodes.Length, task.ManifestVersions.Count());
			for (int i=0; i < expectedVersionCodes.Length; i ++) {
				Assert.AreEqual (expectedVersionCodes[i], task.ManifestVersions[i].GetMetadata ("VersionCode"));
				Assert.AreEqual (expectedVersionNames[i], task.ManifestVersions[i].GetMetadata ("VersionName"));
			}
		}
	}
}
