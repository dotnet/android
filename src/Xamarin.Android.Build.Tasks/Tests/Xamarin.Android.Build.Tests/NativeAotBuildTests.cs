using System.IO;

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
		public void RestoreNativeAot_AndroidArmRuntimePack ()
		{
			var proj = new XamarinAndroidApplicationProject {
				IsRelease = true,
			};
			proj.SetRuntime (AndroidRuntime.NativeAOT);
			proj.SetRuntimeIdentifiers (["armeabi-v7a"]);

			using var builder = CreateApkBuilder ();
			Assert.IsTrue (
				builder.RunTarget (proj, "Restore"),
				"Restore should succeed for android-arm."
			);

			var intermediate = Path.Combine (Root, builder.ProjectDirectory, proj.IntermediateOutputPath);
			var assets = File.ReadAllText (Path.Combine (intermediate, "..", "project.assets.json"));
			StringAssert.Contains (
				"\"Microsoft.NETCore.App.Runtime.NativeAOT.android-arm\"",
				assets,
				"Restore should select the android-arm NativeAOT runtime pack."
			);
			StringAssert.DoesNotContain (
				"\"Microsoft.NETCore.App.Runtime.NativeAOT.linux-bionic-arm\"",
				assets,
				"Restore should not fall back to the linux-bionic-arm NativeAOT runtime pack."
			);
		}

		[Test]
		public void BuildNativeAot_WithoutNdk ()
		{
			var proj = new XamarinAndroidApplicationProject {
				IsRelease = true,
			};
			proj.SetRuntime (AndroidRuntime.NativeAOT);

			using var builder = CreateApkBuilder ();
			Assert.IsTrue (
				builder.Build (proj),
				"Build should succeed without NDK (workload linker is the default)."
			);
			AssertUsesLibcxxWithoutLibunwind (builder, "android-arm64");
		}

		[Test]
		public void BuildNativeAot_WithNdkLinker ()
		{
			var proj = new XamarinAndroidApplicationProject {
				IsRelease = true,
			};
			proj.SetRuntime (AndroidRuntime.NativeAOT);
			proj.SetProperty ("_SkipNdkResolution", "false");

			using var builder = CreateApkBuilder ();
			Assert.IsTrue (
				builder.Build (proj, parameters: new [] {
					"_AndroidUseWorkloadNativeLinker=false",
				}),
				"Build should succeed with NDK linker."
			);
			AssertUsesLibcxxWithoutLibunwind (builder, "android-arm64");
		}

		[Test]
		public void BuildNativeAot_AndroidArm_WithNdkLinker ()
		{
			var proj = new XamarinAndroidApplicationProject {
				IsRelease = true,
			};
			proj.SetRuntime (AndroidRuntime.NativeAOT);
			proj.SetRuntimeIdentifiers (["armeabi-v7a"]);
			proj.SetProperty ("_SkipNdkResolution", "false");

			using var builder = CreateApkBuilder ();
			Assert.IsTrue (
				builder.Build (proj, parameters: [
					"_AndroidUseWorkloadNativeLinker=false",
				]),
				"android-arm build should succeed with NDK linker."
			);
			AssertUsesLibcxxWithoutLibunwind (builder, "android-arm");
		}

		[Test]
		public void BuildNativeAot_WithoutNdk_WorkloadLinkerDisabled_Fails ()
		{
			var proj = new XamarinAndroidApplicationProject {
				IsRelease = true,
			};
			proj.SetRuntime (AndroidRuntime.NativeAOT);

			using var builder = CreateApkBuilder ();
			builder.ThrowOnBuildFailure = false;
			Assert.IsFalse (
				builder.Build (proj, parameters: new [] {
					"_AndroidUseWorkloadNativeLinker=false",
				}),
				"Build should fail without NDK when workload linker is disabled."
			);
		}

		void AssertUsesLibcxxWithoutLibunwind (ProjectBuilder builder, string runtimeIdentifier)
		{
			var intermediate = builder.Output.GetIntermediaryPath (runtimeIdentifier);
			var responseFiles = Directory.GetFiles (intermediate, "ld.lib*.rsp", SearchOption.AllDirectories);
			Assert.IsNotEmpty (responseFiles, $"{intermediate} should contain a native linker response file.");

			foreach (var responseFile in responseFiles) {
				var response = File.ReadAllText (responseFile);
				StringAssert.Contains ("libc++abi.a", response, $"{responseFile} should link libc++abi.");
				StringAssert.DoesNotContain ("libunwind.a", response, $"{responseFile} should not link the NDK unwinder.");
			}
		}
	}
}
