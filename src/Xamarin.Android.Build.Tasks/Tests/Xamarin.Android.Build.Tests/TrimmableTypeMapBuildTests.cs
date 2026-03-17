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

			// TODO: perform full Build,SignAndroidPackage once manifest generation is implemented for the trimmable path
			using var builder = CreateApkBuilder ();
			Assert.IsTrue (builder.RunTarget (proj, "_GenerateJavaStubs"), "_GenerateJavaStubs with trimmable typemap should succeed.");

			// Verify typemap assemblies were generated
			var intermediateDir = builder.Output.GetIntermediaryPath ("typemap");
			if (intermediateDir != null) {
				DirectoryAssert.Exists (intermediateDir);
			}
		}

		[Test]
		public void Build_WithTrimmableTypeMap_IncrementalBuild ()
		{
			var proj = new XamarinAndroidApplicationProject ();
			proj.SetRuntime (AndroidRuntime.CoreCLR);
			proj.SetProperty ("_AndroidTypeMapImplementation", "trimmable");

			// TODO: perform full Build,SignAndroidPackage once manifest generation is implemented for the trimmable path
			using var builder = CreateApkBuilder ();
			Assert.IsTrue (builder.RunTarget (proj, "_GenerateJavaStubs"), "First build should succeed.");

			// Second build with no changes should be incremental (skip _GenerateJavaStubs)
			Assert.IsTrue (builder.RunTarget (proj, "_GenerateJavaStubs"), "Second build should succeed.");
			Assert.IsTrue (
				builder.Output.IsTargetSkipped ("_GenerateJavaStubs"),
				"_GenerateJavaStubs should be skipped on incremental build.");
		}
	}
}
