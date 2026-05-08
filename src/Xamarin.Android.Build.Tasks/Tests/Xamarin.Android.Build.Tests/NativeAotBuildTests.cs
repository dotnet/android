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
			// Override _AndroidNdkDirectory (the internal resolved path) to simulate no NDK.
			// Setting AndroidNdkDirectory alone is not sufficient because ResolveSdks has a
			// fallback chain that discovers NDK from the SDK directory, environment variables,
			// and other standard locations.  /p: has highest MSBuild precedence and cannot be
			// overridden by task outputs.
			Assert.IsFalse (
				builder.Build (proj, parameters: new [] { "_AndroidNdkDirectory=" }),
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
					"_AndroidNdkDirectory=",
					"_AndroidUseWorkloadNativeLinker=true",
				}),
				"Build should succeed with workload linker and no NDK."
			);
		}
	}
}
