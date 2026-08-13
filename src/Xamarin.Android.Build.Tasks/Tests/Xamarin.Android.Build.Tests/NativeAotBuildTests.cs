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
		}

		[Test]
		public void NativeAotSharedLibraryIsPublishedWithoutNativeCompile ()
		{
			var proj = new XamarinAndroidApplicationProject {
				IsRelease = true,
			};
			proj.SetRuntime (AndroidRuntime.NativeAOT);
			proj.Imports.Add (new Import ("nativeaot-publish.targets") {
				TextContent = () =>
					"""
					<?xml version="1.0" encoding="utf-8"?>
					<Project>
					  <Target Name="_CreateExistingNativeAotSharedLibrary" BeforeTargets="_AndroidFixNativeLibraryFileName">
					    <PropertyGroup>
					      <NativeOutputPath />
					      <_ExpectedNativeAotSharedLibrary>$(OutputPath)$(RuntimeIdentifier)/native/lib$(TargetName).so</_ExpectedNativeAotSharedLibrary>
					    </PropertyGroup>
					    <MakeDir Directories="$([System.IO.Path]::GetDirectoryName('$(_ExpectedNativeAotSharedLibrary)'))" />
					    <Touch Files="$(_ExpectedNativeAotSharedLibrary)" AlwaysCreate="true" />
					  </Target>
					  <Target Name="_ValidateNativeAotSharedLibrary" AfterTargets="_AndroidFixNativeLibraryFileName">
					    <ItemGroup>
					      <_NativeAotAppLibrary Include="@(ResolvedFileToPublish)" Condition=" '%(Filename)%(Extension)' == 'lib$(TargetName).so' " />
					    </ItemGroup>
					    <Error Condition=" '@(_NativeAotAppLibrary->Count())' != '1' " Text="Expected one NativeAOT application library, but found @(_NativeAotAppLibrary->Count())." />
					    <Error Condition=" '%(_NativeAotAppLibrary.RelativePath)' != 'lib$(TargetName).so' " Text="NativeAOT application library has incorrect RelativePath metadata: %(_NativeAotAppLibrary.RelativePath)" />
					  </Target>
					</Project>
					""",
			});

			using var builder = CreateApkBuilder ();
			Assert.IsTrue (
				builder.RunTarget (proj, "_AndroidFixNativeLibraryFileName", parameters: [
					"RuntimeIdentifier=android-arm64",
				]),
				"Existing NativeAOT shared library should be registered for publishing."
			);
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
	}
}
