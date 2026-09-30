using System;
using System.IO;
using System.Linq;
using System.Text.Json;

using ELFSharp.ELF;
using ELFSharp.ELF.Sections;
using NUnit.Framework;
using Xamarin.Android.Tasks;
using Xamarin.Android.Tools;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests
{
	/// <summary>
	/// Build tests for NativeAOT and native runtime packaging.
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
			"libunwind.a", // Android NDK archive, unrelated to the removed bundled libunwind.
		];

		[TestCase ("armeabi-v7a")]
		[TestCase ("arm64-v8a")]
		[TestCase ("x86_64")]
		public void BuildCoreClrWithPrebuiltRuntime (string abi)
		{
			var proj = new XamarinAndroidApplicationProject {
				IsRelease = true,
			};
			proj.SetRuntime (AndroidRuntime.CoreCLR);
			proj.SetRuntimeIdentifiers ([abi]);

			using var builder = CreateApkBuilder ();
			Assert.IsTrue (builder.Build (proj), $"CoreCLR app build should succeed for {abi} with the prebuilt runtime.");
			builder.Output.AssertTargetIsSkipped ("_LinkNativeRuntime", defaultIfNotUsed: true);
		}

		[TestCase ("apk")]
		[TestCase ("aab")]
		public void BuildCoreClrWithManagedAssemblyStoreWrappers (string packageFormat)
		{
			var proj = new XamarinAndroidApplicationProject {
				IsRelease = true,
			};
			proj.SetRuntime (AndroidRuntime.CoreCLR);
			proj.SetRuntimeIdentifiers (["armeabi-v7a", "arm64-v8a", "x86_64"]);
			proj.SetProperty ("AndroidUseAssemblyStore", "true");
			proj.SetProperty ("AndroidPackageFormat", packageFormat);

			using var builder = CreateApkBuilder ();
			Assert.IsTrue (builder.Build (proj), $"The three-RID {packageFormat} build should succeed.");
			string package = Path.Combine (Root, builder.ProjectDirectory, proj.OutputPath, $"{proj.PackageName}-Signed.{packageFormat}");
			FileAssert.Exists (package);
			string prefix = packageFormat == "aab" ? "base/" : "";
			foreach (var (abi, machine) in new [] {
				("armeabi-v7a", Machine.ARM),
				("arm64-v8a", Machine.AArch64),
				("x86_64", Machine.AMD64),
			}) {
				byte [] store = ZipHelper.ReadFileFromZip (package, $"{prefix}lib/{abi}/libassembly-store.so");
				Assert.IsNotNull (store, $"The {abi} store must be in the ABI-targeted library directory.");
				using var stream = new MemoryStream (store);
				using IELF elf = ELFReader.Load (stream, shouldOwnStream: false);
				Assert.AreEqual (machine, elf.Machine);
				Assert.AreEqual (FileType.SharedObject, elf.Type);
				var symbols = (ISymbolTable)elf.GetSection (".dynsym");
				CollectionAssert.AreEquivalent (new [] { "", "_assembly_store" }, symbols.Entries.Select (s => s.Name));
				byte [] payload = elf.GetSection ("payload").GetContents ();
				CollectionAssert.AreEqual (new byte [] { 0x58, 0x41, 0x42, 0x41 }, payload.Take (4));
			}
		}

		[TestCase ("armeabi-v7a")]
		[TestCase ("arm64-v8a")]
		[TestCase ("x86_64")]
		public void BuildNativeAotWithoutCPlusPlusArchives (string abi)
		{
			var proj = new XamarinAndroidApplicationProject {
				IsRelease = true,
			};
			proj.SetRuntime (AndroidRuntime.NativeAOT);
			proj.SetRuntimeIdentifiers ([abi]);

			using var builder = CreateApkBuilder ();
			Assert.IsTrue (builder.Build (proj), $"NativeAOT build should succeed for {abi} without libc++ or libunwind.");

			string intermediateDirectory = Path.Combine (Root, builder.ProjectDirectory, proj.IntermediateOutputPath);
			string [] responseFiles = Directory.GetFiles (intermediateDirectory, "ld.*.rsp", SearchOption.AllDirectories);
			Assert.IsNotEmpty (responseFiles, "Native linker response files should be generated.");
			Assert.IsEmpty (Directory.GetFiles (intermediateDirectory, "*.ll", SearchOption.AllDirectories),
				"NativeAOT app builds should not generate LLVM IR.");
			Assert.IsNotEmpty (Directory.GetFiles (intermediateDirectory, "AppBootstrapConfig.java", SearchOption.AllDirectories));
			string [] dexFiles = Directory.GetFiles (intermediateDirectory, "classes*.dex", SearchOption.AllDirectories);
			Assert.IsNotEmpty (dexFiles);
			Assert.IsTrue (dexFiles.Any (file => DexUtils.GetDexDump (file, AndroidSdkPath)
				.Any (line => line.Contains ("name          : 'SystemProperties'", StringComparison.Ordinal))),
				"R8 must preserve application bootstrap fields read by the NativeAOT host.");
			// The native host calls AppBootstrapConfig.readRemappingAsset(String) through JNI at
			// JNI_OnLoad to load the binary JNI remapping asset; if R8 strips this method the app
			// aborts before managed startup even runs. proguard_trimmable_nativeaot.cfg keeps the
			// whole class, but assert on the DEX directly so a regression there is caught here too.
			Assert.IsTrue (dexFiles.Any (file => DexUtils.ContainsClassWithMethod (
					"Lnet/dot/android/AppBootstrapConfig;", "readRemappingAsset", "(Ljava/lang/String;)[B",
					DexUtils.GetDexDump (file, AndroidSdkPath))),
				"R8 must preserve AppBootstrapConfig.readRemappingAsset(String), which the NativeAOT host calls through JNI at JNI_OnLoad.");
			foreach (string responseFile in responseFiles) {
				string response = File.ReadAllText (responseFile);
				StringAssert.Contains ("libnaot-android.release-static-release.a", response, responseFile);
				StringAssert.Contains ("libSystem.Security.Cryptography.Native.Android.a", response, responseFile);
				StringAssert.DoesNotContain ($"environment.{abi}.o", response, responseFile);
				StringAssert.DoesNotContain ($"jni_init_funcs.{abi}.o", response, responseFile);
				foreach (string archiveName in CPlusPlusArchiveNames) {
					StringAssert.DoesNotContain (archiveName, response, responseFile);
				}
			}

			string rid = AbiUtils.AbiToRuntimeIdentifier (abi);
			string apk = Path.Combine (Root, builder.ProjectDirectory, proj.OutputPath, $"{proj.PackageName}-Signed.apk");
			FileAssert.Exists (apk);
			byte [] remapAsset = ZipHelper.ReadFileFromZip (apk, $"assets/xa-internal/jni-remap.{rid}.bin");
			Assert.IsNotNull (remapAsset, $"The packaged apk should contain assets/xa-internal/jni-remap.{rid}.bin, or the native host aborts before managed startup.");
		}

		[Test]
		public void BuildNativeAotWithMultipleRuntimeIdentifiers ()
		{
			var proj = new XamarinAndroidApplicationProject {
				IsRelease = true,
			};
			proj.SetRuntime (AndroidRuntime.NativeAOT);
			proj.SetRuntimeIdentifiers (["arm64-v8a", "armeabi-v7a"]);

			using var builder = CreateApkBuilder ();
			Assert.IsTrue (builder.Build (proj), "The multi-RID NativeAOT build should succeed.");

			string intermediateDirectory = Path.Combine (Root, builder.ProjectDirectory, proj.IntermediateOutputPath);
			Assert.IsEmpty (Directory.GetFiles (intermediateDirectory, "*.ll", SearchOption.AllDirectories));
			string [] fileWrites = File.ReadAllLines (Path.Combine (intermediateDirectory, $"{proj.ProjectName}.csproj.FileListAbsolute.txt"));

			string apk = Path.Combine (Root, builder.ProjectDirectory, proj.OutputPath, $"{proj.PackageName}-Signed.apk");
			FileAssert.Exists (apk);
			foreach (string rid in new [] { "android-arm64", "android-arm" }) {
				byte [] asset = ZipHelper.ReadFileFromZip (apk, $"assets/xa-internal/jni-remap.{rid}.bin");
				Assert.IsNotNull (asset, $"NativeAOT must package a separate JNI remapping asset for {rid}.");
				Assert.GreaterOrEqual (asset.Length, 64, $"The {rid} asset must contain a valid binary header.");
				string assetFile = Path.Combine (intermediateDirectory, "android", "jni-remap", $"jni-remap.{rid}.bin");
				CollectionAssert.Contains (fileWrites, assetFile, $"The {rid} asset must be tracked for incremental clean.");
			}
		}

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
		public void RestoreNativeAot_UsesSdkRuntimePackVersion ()
		{
			var proj = new XamarinAndroidApplicationProject {
				IsRelease = true,
			};
			proj.SetRuntime (AndroidRuntime.NativeAOT);

			using var builder = CreateApkBuilder ();
			Assert.IsTrue (
				builder.RunTarget (proj, "Restore", parameters: [
					"MicrosoftNETCoreAppRefPackageVersion=0.0.0",
				]),
				"Restore should use the .NET SDK's NativeAOT runtime pack version."
			);

			var intermediate = Path.Combine (Root, builder.ProjectDirectory, proj.IntermediateOutputPath);
			using var assets = JsonDocument.Parse (File.ReadAllText (Path.Combine (intermediate, "..", "project.assets.json")));
			var runtimePacks = assets.RootElement
				.GetProperty ("project")
				.GetProperty ("frameworks")
				.EnumerateObject ()
				.SelectMany (framework => framework.Value.GetProperty ("downloadDependencies").EnumerateArray ())
				.Where (dependency => dependency.GetProperty ("name").GetString ()?.StartsWith ("Microsoft.NETCore.App.Runtime.NativeAOT.", StringComparison.Ordinal) == true)
				.ToArray ();
			Assert.IsNotEmpty (
				runtimePacks,
				"Restore should select a NativeAOT runtime pack."
			);
			foreach (var runtimePack in runtimePacks) {
				Assert.AreNotEqual (
					"[0.0.0, 0.0.0]",
					runtimePack.GetProperty ("version").GetString (),
					"Restore should ignore MicrosoftNETCoreAppRefPackageVersion and use the SDK-selected NativeAOT runtime pack version."
				);
			}
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

		[TestCase (AndroidRuntime.NativeAOT)]
		[TestCase (AndroidRuntime.CoreCLR)]
		public void RuntimePackDoesNotContainCPlusPlusArchives (AndroidRuntime runtime)
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
				package.AssertDoesNotContainEntry (packagePath, archivePath);
			}
		}

		[TestCase (AndroidRuntime.NativeAOT)]
		[TestCase (AndroidRuntime.CoreCLR)]
		public void CopyRuntimePackRemovesStaleCPlusPlusArchives (AndroidRuntime runtime)
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
				$"Microsoft.Android.Runtime.{runtime}.{apiLevelName}.android-arm64",
				packVersion,
				"runtimes",
				"android-arm64",
				"native"
			);
			Directory.CreateDirectory (nativeDirectory);
			foreach (string archiveName in CPlusPlusArchiveNames) {
				File.Create (Path.Combine (nativeDirectory, archiveName)).Dispose ();
			}

			string runtimeOutputPath = Path.Combine (outputDirectory, "runtime-output");

			string nativeProjectName = runtime == AndroidRuntime.NativeAOT ? "native-nativeaot.csproj" : "native-clr.csproj";
			string nativeProject = Path.Combine (XABuildPaths.TopDirectory, "src", "native", nativeProjectName);
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
						$"OutputPath={runtimeOutputPath}{Path.DirectorySeparatorChar}",
					]
				),
				$"Copying the {runtime} runtime pack should succeed. See {dotnet.ProcessLogFile}."
			);

			foreach (string archiveName in CPlusPlusArchiveNames) {
				FileAssert.DoesNotExist (Path.Combine (nativeDirectory, archiveName));
			}
			FileAssert.Exists (Path.Combine (nativeDirectory, "libc.so"));
			FileAssert.Exists (Path.Combine (nativeDirectory, "libclang_rt.builtins-aarch64-android.a"));
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
