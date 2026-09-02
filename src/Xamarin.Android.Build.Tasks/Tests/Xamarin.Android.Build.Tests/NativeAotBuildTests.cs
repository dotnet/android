using System;
using System.IO;
using System.Linq;

using NUnit.Framework;
using Xamarin.Android.Tasks;
using Xamarin.Android.Tools;
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
		static readonly string [] ArmEhabiPersonalitySymbols = [
			"__aeabi_unwind_cpp_pr0",
			"__aeabi_unwind_cpp_pr1",
			"__aeabi_unwind_cpp_pr2",
		];

		static readonly string [] CPlusPlusArchiveNames = [
			"libc++_static.a",
			"libc++abi.a",
			"libunwind.a",
		];

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
		public void BuildNativeAot_AndroidArm_WithoutNdk ()
		{
			var proj = new XamarinAndroidApplicationProject {
				IsRelease = true,
			};
			proj.SetRuntime (AndroidRuntime.NativeAOT);
			proj.SetRuntimeIdentifiers (["armeabi-v7a"]);

			using var builder = CreateApkBuilder ();
			Assert.IsTrue (
				builder.Build (proj),
				"android-arm build should succeed without NDK."
			);
			AssertArmEhabiSymbolsPromoted (builder, proj);
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
			AssertArmEhabiSymbolsPromoted (builder, proj);
		}

		void AssertArmEhabiSymbolsPromoted (ProjectBuilder builder, XamarinAndroidApplicationProject proj)
		{
			string nativeDirectory = Path.Combine (Root, builder.ProjectDirectory, proj.IntermediateOutputPath, "android-arm", "native");
			string runtimeArchive = Path.Combine (nativeDirectory, "libRuntime.WorkstationGC.arm-ehabi.a");
			FileAssert.Exists (runtimeArchive);

			string [] linkerResponseFiles = Directory.GetFiles (nativeDirectory, "ld.*.rsp");
			Assert.AreEqual (1, linkerResponseFiles.Length, "One native linker response file should be generated.");
			string linkerResponse = File.ReadAllText (linkerResponseFiles [0]);
			StringAssert.Contains ("libRuntime.WorkstationGC.arm-ehabi.a", linkerResponse);
			foreach (string archiveName in CPlusPlusArchiveNames) {
				StringAssert.DoesNotContain (archiveName, linkerResponse);
			}

			NdkTools ndk = NdkTools.Create (AndroidNdkPath);
			ndk.OSBinPath = TestEnvironment.OSBinDirectory;
			string llvmNm = ndk.GetToolPath ("llvm-nm", AndroidTargetArch.Arm, 0);
			var (exitCode, standardOutput, standardError) = RunProcessWithExitCode (llvmNm, $"--defined-only \"{runtimeArchive}\"");
			Assert.AreEqual (0, exitCode, $"llvm-nm failed:{Environment.NewLine}{standardError}");
			foreach (string symbol in ArmEhabiPersonalitySymbols) {
				StringAssert.Contains ($" W {symbol}", standardOutput, $"{symbol} should be a weak global symbol.");
			}
		}

		[TestCase (AndroidRuntime.NativeAOT, false)]
		[TestCase (AndroidRuntime.CoreCLR, true)]
		public void RuntimePackCPlusPlusArchives (AndroidRuntime runtime, bool shouldContain)
		{
			string outputDirectory = Path.Combine (Root, TestName);
			if (Directory.Exists (outputDirectory)) {
				Directory.Delete (outputDirectory, recursive: true);
			}
			Directory.CreateDirectory (outputDirectory);
			Version apiLevel = XABuildConfig.AndroidLatestStableApiLevel;
			string apiLevelName = apiLevel.Minor == 0 ? $"{apiLevel.Major}" : $"{apiLevel.Major}.{apiLevel.Minor}";
			string installedRuntimePack = Path.Combine (
				TestEnvironment.DotNetPreviewPacksDirectory,
				$"Microsoft.Android.Runtime.NativeAOT.{apiLevelName}.android-arm64"
			);
			string runtimeAssembly = Directory.GetFiles (
				installedRuntimePack,
				"Microsoft.Android.Runtime.NativeAOT.dll",
				SearchOption.AllDirectories
			).Single ();
			string managedOutputRoot = Path.Combine (outputDirectory, "xbuild-frameworks", "Microsoft.Android");
			string managedOutputDirectory = Path.Combine (managedOutputRoot, apiLevelName);
			Directory.CreateDirectory (managedOutputDirectory);
			File.Copy (runtimeAssembly, Path.Combine (managedOutputDirectory, Path.GetFileName (runtimeAssembly)));
			// The pack target requires a PDB, but its contents are unrelated to native asset composition.
			File.Create (Path.Combine (managedOutputDirectory, "Microsoft.Android.Runtime.NativeAOT.pdb")).Dispose ();

			string runtimePackProject = Path.Combine (XABuildPaths.TopDirectory, "build-tools", "create-packs", "Microsoft.Android.Runtime.proj");
			var dotnet = new DotNetCLI (runtimePackProject) {
				ProjectDirectory = outputDirectory,
				BuildLogFile = Path.Combine (outputDirectory, "build.log"),
				ProcessLogFile = Path.Combine (outputDirectory, "process.log"),
			};
			Assert.IsTrue (
				dotnet.Pack (parameters: [
					$"Configuration={XABuildPaths.Configuration}",
					$"AndroidApiLevel={apiLevelName}",
					$"AndroidRuntime={runtime}",
					"AndroidRID=android-arm64",
					$"_MonoAndroidNETOutputRoot={managedOutputRoot}{Path.DirectorySeparatorChar}",
					$"BaseIntermediateOutputPath={Path.Combine (outputDirectory, "obj")}{Path.DirectorySeparatorChar}",
					$"PackageOutputPath={outputDirectory}",
				]),
				$"Packing the {runtime} runtime pack should succeed. See {dotnet.ProcessLogFile}."
			);

			string packagePath = Directory.GetFiles (outputDirectory, "*.nupkg")
				.Single (path => !path.EndsWith (".symbols.nupkg", StringComparison.OrdinalIgnoreCase));
			using var package = ZipHelper.OpenZip (packagePath);
			foreach (string archiveName in CPlusPlusArchiveNames) {
				string archivePath = $"runtimes/android-arm64/native/{archiveName}";
				if (shouldContain) {
					package.AssertContainsEntry (packagePath, archivePath);
				} else {
					package.AssertDoesNotContainEntry (packagePath, archivePath);
				}
			}
		}

		[Test]
		public void CopyNativeAotRuntimePackRemovesStaleCPlusPlusArchives ()
		{
			string outputDirectory = Path.Combine (Root, TestName);
			if (Directory.Exists (outputDirectory)) {
				Directory.Delete (outputDirectory, recursive: true);
			}
			Directory.CreateDirectory (outputDirectory);

			Version apiLevel = XABuildConfig.AndroidLatestStableApiLevel;
			string apiLevelName = apiLevel.Minor == 0 ? $"{apiLevel.Major}" : $"{apiLevel.Major}.{apiLevel.Minor}";
			string packVersion = "1.0.0-test";
			string packsRoot = Path.Combine (outputDirectory, "packs");
			string nativeDirectory = Path.Combine (
				packsRoot,
				$"Microsoft.Android.Runtime.NativeAOT.{apiLevelName}.android-arm64",
				packVersion,
				"runtimes",
				"android-arm64",
				"native"
			);
			Directory.CreateDirectory (nativeDirectory);
			foreach (string archiveName in CPlusPlusArchiveNames) {
				File.Create (Path.Combine (nativeDirectory, archiveName)).Dispose ();
			}

			string nativeProject = Path.Combine (XABuildPaths.TopDirectory, "src", "native", "native-nativeaot.csproj");
			var dotnet = new DotNetCLI (nativeProject) {
				ProjectDirectory = outputDirectory,
				BuildLogFile = Path.Combine (outputDirectory, "build.log"),
				ProcessLogFile = Path.Combine (outputDirectory, "process.log"),
			};
			Assert.IsTrue (
				dotnet.Build (
					target: "_CopyToPackDirs",
					parameters: [
						$"Configuration={XABuildPaths.Configuration}",
						$"AndroidApiLevel={apiLevelName}",
						$"AndroidPackVersion={packVersion}",
						$"MicrosoftAndroidPacksRootDir={packsRoot}{Path.DirectorySeparatorChar}",
					]
				),
				$"Copying the NativeAOT runtime pack should succeed. See {dotnet.ProcessLogFile}."
			);

			foreach (string archiveName in CPlusPlusArchiveNames) {
				FileAssert.DoesNotExist (Path.Combine (nativeDirectory, archiveName));
			}
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
