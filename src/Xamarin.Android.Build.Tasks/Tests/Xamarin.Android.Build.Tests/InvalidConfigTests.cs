using NUnit.Framework;
using System.IO;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests
{
	public class InvalidConfigTests : BaseTest
	{
		static readonly object [] SettingCombinationsSource = new object [] {
			// Interpreter + AOT
			new object [] {
				/* isRelease */      true,
				/* useInterpreter */ true,
				/* publishTrimmed */ true,
				/* aot */            true,
				/* expected */       true,
			},
			// Debug + AOT
			new object [] {
				/* isRelease */      false,
				/* useInterpreter */ false,
				/* publishTrimmed */ true,
				/* aot */            true,
				/* expected */       true,
			},
			// Debug + PublishTrimmed
			new object [] {
				/* isRelease */      false,
				/* useInterpreter */ false,
				/* publishTrimmed */ true,
				/* aot */            false,
				/* expected */       true,
			},
			// AOT + PublishTrimmed=false
			new object [] {
				/* isRelease */      true,
				/* useInterpreter */ false,
				/* publishTrimmed */ false,
				/* aot */            true,
				/* expected */       false,
			},
		};

		[Test]
		[TestCaseSource (nameof (SettingCombinationsSource))]
		public void SettingCombinations (bool isRelease, bool useInterpreter, bool publishTrimmed, bool aot, bool expected)
		{
			var proj = new XamarinAndroidApplicationProject {
				IsRelease = isRelease,
				EnableDefaultItems = true,
			};
			proj.SetProperty ("UseInterpreter", useInterpreter.ToString ());
			proj.SetProperty ("PublishTrimmed", publishTrimmed.ToString ());
			proj.SetProperty ("RunAOTCompilation", aot.ToString ());
			var builder = CreateApkBuilder ();
			builder.ThrowOnBuildFailure = false;
			Assert.AreEqual (expected, builder.Build (proj), $"{proj.ProjectName} should {(expected ? "succeed" : "fail")}");
		}

		[Test]
		public void EolFrameworks ([Values ("net6.0-android", "net7.0-android")] string targetFramework)
		{
			var library = new XamarinAndroidLibraryProject () {
				TargetFramework = targetFramework,
				EnableDefaultItems = true,
			};
			var builder = CreateApkBuilder ();
			builder.ThrowOnBuildFailure = false;
			Assert.IsFalse (builder.Restore (library), $"{library.ProjectName} restore should fail");
			Assert.IsTrue (StringAssertEx.ContainsText (builder.LastBuildOutput, $"NETSDK1202: The workload '{targetFramework}' is out of support"), $"{builder.BuildLogFile} should have NETSDK1202.");
		}

		[Test]
		public void XA0119 ()
		{
			AssertCommercialBuild ();

			var proj = new XamarinAndroidApplicationProject ();
			proj.SetProperty ("_XASupportsFastDev", "True");
			proj.SetProperty (proj.DebugProperties, "AndroidLinkMode", "Full");
			using (var b = CreateApkBuilder (Path.Combine ("temp", TestName))) {
				b.Target = "Build"; // SignAndroidPackage would fail for OSS builds
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
				Assert.IsTrue (StringAssertEx.ContainsText (b.LastBuildOutput, "XA0119"), "Output should contain XA0119 warnings");
			}
		}

		[Test]
		public void XA0119AAB ()
		{
			AssertCommercialBuild ();

			var proj = new XamarinAndroidApplicationProject ();
			proj.SetProperty ("_XASupportsFastDev", "True");
			proj.SetProperty ("AndroidPackageFormat", "aab");
			using (var builder = CreateApkBuilder ()) {
				builder.ThrowOnBuildFailure = false;
				Assert.IsTrue (builder.Build (proj), "Build should have succeeded.");
				Assert.IsTrue (StringAssertEx.ContainsText (builder.LastBuildOutput, "XA0119"), "Output should contain XA0119 warnings");
			}
		}

		[Test]
		public void XA0119Interpreter ()
		{
			var proj = new XamarinAndroidApplicationProject {
				IsRelease = true,
				AotAssemblies = true,
			};
			proj.SetProperty ("UseInterpreter", "true");
			using (var builder = CreateApkBuilder ()) {
				builder.ThrowOnBuildFailure = false;
				Assert.IsTrue (builder.Build (proj), "Build should have succeeded.");
				Assert.IsTrue (StringAssertEx.ContainsText (builder.LastBuildOutput, "XA0119"), "Output should contain XA0119 warnings");
			}
		}
	}
}
