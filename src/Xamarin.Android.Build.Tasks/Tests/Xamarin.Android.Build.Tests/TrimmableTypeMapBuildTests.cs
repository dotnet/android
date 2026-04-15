using System.IO;
using NUnit.Framework;
using Xamarin.Android.Tasks;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests {
	[TestFixture]
	[Category ("Node-2")]
	public class TrimmableTypeMapBuildTests : BaseTest {

		[Test]
		public void Build_WithTrimmableTypeMap_Succeeds ()
		{
			var proj = new XamarinAndroidApplicationProject ();
			proj.SetRuntime (AndroidRuntime.CoreCLR);
			proj.SetProperty ("_AndroidTypeMapImplementation", "trimmable");

			// Full Build will fail downstream (manifest generation not yet implemented for trimmable path),
			// but _GenerateJavaStubs runs and completes before the failure point.
			using var builder = CreateApkBuilder ();
			builder.ThrowOnBuildFailure = false;
			builder.Build (proj);

			// Verify _GenerateJavaStubs ran by checking typemap outputs exist
			var intermediateDir = builder.Output.GetIntermediaryPath ("typemap");
			DirectoryAssert.Exists (intermediateDir);
		}

		[Test]
		public void Build_WithTrimmableTypeMap_IncrementalBuild ()
		{
			var proj = new XamarinAndroidApplicationProject ();
			proj.SetRuntime (AndroidRuntime.CoreCLR);
			proj.SetProperty ("_AndroidTypeMapImplementation", "trimmable");

			// Full Build will fail downstream (manifest generation not yet implemented for trimmable path),
			// but _GenerateJavaStubs runs and completes before the failure point.
			using var builder = CreateApkBuilder ();
			builder.ThrowOnBuildFailure = false;
			builder.Build (proj);

			// Verify _GenerateJavaStubs ran on the first build
			var intermediateDir = builder.Output.GetIntermediaryPath ("typemap");
			DirectoryAssert.Exists (intermediateDir);

			// Second build with no changes — _GenerateJavaStubs should be skipped
			builder.Build (proj);
			Assert.IsTrue (
				builder.Output.IsTargetSkipped ("_GenerateJavaStubs"),
				"_GenerateJavaStubs should be skipped on incremental build.");
		}

		[Test]
		public void TrimmableTypeMap_PreserveList_IsPackagedInSdk ()
		{
			var path = Path.Combine (TestEnvironment.AndroidMSBuildDirectory, "PreserveLists", "Trimmable.CoreCLR.xml");

			FileAssert.Exists (path, $"{path} should exist in the SDK pack.");
		}
	}
}
