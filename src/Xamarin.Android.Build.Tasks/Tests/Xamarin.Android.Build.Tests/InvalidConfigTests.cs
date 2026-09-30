using NUnit.Framework;
using System.IO;
using Xamarin.Android.Tasks;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests
{
	public class InvalidConfigTests : BaseTest
	{
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
			var proj = new XamarinAndroidApplicationProject ();
			proj.SetProperty (proj.DebugProperties, "AndroidLinkMode", "Full");
			proj.EmbedAssembliesIntoApk = false;
			using (var b = CreateApkBuilder ()) {
				b.Target = "Build"; // SignAndroidPackage would fail for OSS builds
				Assert.IsTrue (b.Build (proj), "Build should have succeeded.");
				Assert.IsTrue (StringAssertEx.ContainsText (b.LastBuildOutput, "XA0119"), "Output should contain XA0119 warnings");
			}
		}

		[Test]
		public void XA0119AAB ()
		{
			var proj = new XamarinAndroidApplicationProject ();
			proj.SetProperty ("AndroidPackageFormat", "aab");
			using (var builder = CreateApkBuilder ()) {
				builder.ThrowOnBuildFailure = false;
				Assert.IsTrue (builder.Build (proj), "Build should have succeeded.");
				Assert.IsTrue (StringAssertEx.ContainsText (builder.LastBuildOutput, "XA0119"), "Output should contain XA0119 warnings");
			}
		}

		[Test]
		public void UnsupportedJcwCodegenTargetIsRejected (
			[Values ("XamarinAndroid", "JavaInterop1")] string codegenTarget,
			[Values (AndroidRuntime.CoreCLR, AndroidRuntime.NativeAOT)] AndroidRuntime runtime)
		{
			var project = new XamarinAndroidApplicationProject {
				IsRelease = runtime == AndroidRuntime.NativeAOT,
			};
			project.SetRuntime (runtime);
			project.SetProperty ("_AndroidJcwCodegenTarget", codegenTarget);
			using (var builder = CreateApkBuilder ()) {
				builder.Target = "_CheckForInvalidConfigurationAndPlatform";
				builder.ThrowOnBuildFailure = false;
				Assert.IsFalse (builder.Build (project), "Build should have failed.");
				StringAssertEx.Contains ("error XA4240:", builder.LastBuildOutput, "Build should fail with XA4240.");
				StringAssertEx.Contains (codegenTarget, builder.LastBuildOutput, "Error should identify the unsupported code generation target.");
			}
		}

		[Test]
		[TestCase ("RunAOTCompilation")]
		[TestCase ("EnableLLVM")]
		public void UnsupportedMonoAotPropertyFailsBuild (string property)
		{
			var project = new XamarinAndroidApplicationProject {
				IsRelease = true,
			};
			project.SetRuntime (AndroidRuntime.CoreCLR);
			project.SetProperty (property, "true");

			using var builder = CreateApkBuilder ();
			builder.ThrowOnBuildFailure = false;
			Assert.IsFalse (builder.Build (project), "Build should have failed.");
			StringAssertEx.Contains ("error XA1044", builder.LastBuildOutput, "Build output should contain error XA1044");
			StringAssertEx.Contains (property, builder.LastBuildOutput, $"Build output should mention {property}");
			StringAssertEx.Contains ("CoreCLR", builder.LastBuildOutput, "Build output should mention CoreCLR");
		}

	}
}
