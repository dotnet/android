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
			// Clear AndroidNdkDirectory to simulate a machine without NDK installed.
			// This overrides the rsp-injected value (MSBuild last-value-wins).
			//
			// Currently NativeAOT linking requires NDK tools, so the build should fail.
			// When the workload pack ships its own linker, flip this to Assert.IsTrue.
			Assert.IsFalse (
				builder.Build (proj, parameters: new [] { "AndroidNdkDirectory=\"\"" }),
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
			// Use workload-provided linker and sysroot instead of NDK
			Assert.IsTrue (
				builder.Build (proj, parameters: new [] {
					"AndroidNdkDirectory=\"\"",
					"_AndroidUseWorkloadNativeLinker=true",
				}),
				"Build should succeed with workload linker and no NDK."
			);
		}
	}
}
