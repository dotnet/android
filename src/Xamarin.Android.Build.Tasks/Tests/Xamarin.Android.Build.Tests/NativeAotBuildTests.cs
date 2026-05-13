using NUnit.Framework;
using Xamarin.Android.Tasks;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests
{
	/// <summary>
	/// Build tests specific to the NativeAOT runtime.
	/// </summary>
	[TestFixture]
	[Category ("Node-2")]
	public class NativeAotBuildTests : BaseTest
	{
		[Test]
		public void BuildNativeAot_WithoutNdk_Fails ()
		{
			var proj = new XamarinAndroidApplicationProject {
				IsRelease = true,
			};
			proj.SetRuntime (AndroidRuntime.NativeAOT);

			using var builder = CreateApkBuilder ();
			builder.ThrowOnBuildFailure = false;
			Assert.IsFalse (
				builder.Build (proj, parameters: new [] { "_SkipNdkResolution=true" }),
				"Build should have failed without NDK."
			);
		}

		[Test]
		public void BuildNativeAot_WithWorkloadLinker_WithoutNdk ()
		{
			var proj = new XamarinAndroidApplicationProject {
				IsRelease = true,
			};
			proj.SetRuntime (AndroidRuntime.NativeAOT);

			using var builder = CreateApkBuilder ();
			Assert.IsTrue (
				builder.Build (proj, parameters: new [] {
					"_SkipNdkResolution=true",
					"_AndroidUseWorkloadNativeLinker=true",
				}),
				"Build should succeed with workload linker and no NDK."
			);
		}
	}
}
