using System;
using System.IO;
using System.Linq;
using System.Text.Json;

using ELFSharp.ELF;
using ELFSharp.ELF.Sections;
using Microsoft.Build.Framework;
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

		static string [] GetRuntimePackNativeFiles (AndroidRuntime runtime) => runtime == AndroidRuntime.CoreCLR ?
			["libnet-android.debug.so", "libnet-android.release.so"] :
			["libnaot-android.release-static-release.a"];

		static string [] GetObsoleteRuntimePackNativeFiles (AndroidRuntime runtime)
		{
			string [] common = [
				.. CPlusPlusArchiveNames,
				"libarchive-dso-stub.so",
				"libc.so",
				"libdl.so",
				"liblog.so",
				"libm.so",
				"libz.so",
				"crtbegin_so.o",
				"crtend_so.o",
				"libclang_rt.builtins-aarch64-android.a",
				"libruntime-base-debug.a",
				"libruntime-base-release.a",
				"libruntime-base-common-debug.a",
				"libruntime-base-common-release.a",
				"libxa-java-interop-debug.a",
				"libxa-java-interop-release.a",
				"libxa-lz4-debug.a",
				"libxa-lz4-release.a",
				"libxa-shared-bits-debug.a",
				"libxa-shared-bits-release.a",
				"libxamarin-startup-debug.a",
				"libxamarin-startup-release.a",
			];
			return runtime == AndroidRuntime.CoreCLR ?
				[.. common, "libnet-android.debug-static-debug.a", "libnet-android.release-static-release.a"] :
				[.. common, "libnaot-android.debug-static-debug.a", "libnaot-android.debug.so", "libnaot-android.release.so"];
		}

		static void CreateNativeRuntimePackInputs (string nativeDirectory, AndroidRuntime runtime)
		{
			Directory.CreateDirectory (nativeDirectory);
			foreach (string fileName in GetRuntimePackNativeFiles (runtime).Concat (GetObsoleteRuntimePackNativeFiles (runtime))) {
				File.WriteAllBytes (Path.Combine (nativeDirectory, fileName), [1, 2, 3]);
			}
		}

		[TestCase ("armeabi-v7a")]
		[TestCase ("arm64-v8a")]
		[TestCase ("x86_64")]
		public void BuildCoreClrWithPrebuiltRuntime (string abi)
		{
			var proj = new XamarinAndroidApplicationProject {
				IsRelease = false,
			};
			proj.SetRuntime (AndroidRuntime.CoreCLR);
			proj.SetRuntimeIdentifiers ([abi]);
			proj.SetProperty ("_SkipNdkResolution", "true");

			using var builder = CreateApkBuilder ();
			builder.Verbosity = LoggerVerbosity.Detailed;
			Assert.IsTrue (builder.Build (proj), $"The CoreCLR Debug build should succeed for {abi} without an NDK.");
			builder.Output.AssertTargetIsSkipped ("_LinkNativeRuntime", defaultIfNotUsed: true);
			StringAssertEx.DoesNotContain ("Task \"ResolveAndroidNdk\"", builder.LastBuildOutput, "Ordinary CoreCLR must not resolve NDK tools.");
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
			proj.SetProperty ("_SkipNdkResolution", "true");

			using var builder = CreateApkBuilder ();
			builder.Verbosity = LoggerVerbosity.Detailed;
			Assert.IsTrue (builder.Build (proj), $"The three-RID CoreCLR Release {packageFormat} build should succeed without an NDK.");
			builder.Output.AssertTargetIsSkipped ("_LinkNativeRuntime", defaultIfNotUsed: true);
			StringAssertEx.DoesNotContain ("Task \"ResolveAndroidNdk\"", builder.LastBuildOutput, "Assembly-store packaging must not resolve NDK tools.");
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
			builder.Verbosity = LoggerVerbosity.Detailed;
			Assert.IsTrue (builder.Build (proj), $"NativeAOT build should succeed for {abi} using the NDK by default, without libc++ or libunwind.");
			StringAssertEx.Contains ("Task \"ResolveAndroidNdk\"", builder.LastBuildOutput, "NativeAOT must resolve its NDK tools.");

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
				StringAssert.Contains (GetNdkToolchainDirectory ().Replace ('\\', '/'), response.Replace ('\\', '/'),
					$"The default NativeAOT linker inputs should come from the configured NDK: {responseFile}");
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
			if (abi == "armeabi-v7a") {
				AssertArmEhabiSymbolsPromoted (builder, proj);
			}
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

		[TestCase ("armeabi-v7a")]
		[TestCase ("arm64-v8a")]
		[TestCase ("x86_64")]
		public void BuildNativeAot_WithoutNdk (string abi)
		{
			var proj = new XamarinAndroidApplicationProject {
				IsRelease = true,
			};
			proj.SetRuntime (AndroidRuntime.NativeAOT);
			proj.SetRuntimeIdentifiers ([abi]);
			proj.SetProperty ("_SkipNdkResolution", "true");

			using var builder = CreateApkBuilder ();
			builder.ThrowOnBuildFailure = false;
			Assert.IsFalse (builder.Build (proj), $"NativeAOT must require an NDK for {abi}.");
			StringAssertEx.Contains ("error XA5104:", builder.LastBuildOutput, "A missing NDK should produce XA5104.");
		}

		[TestCase (false)]
		[TestCase (true)]
		public void BuildCoreClrWithNativeLibraryStripping_WithoutNdk (bool isRelease)
		{
			var proj = new XamarinAndroidApplicationProject {
				IsRelease = isRelease,
			};
			proj.SetRuntime (AndroidRuntime.CoreCLR);
			proj.SetRuntimeIdentifiers (["arm64-v8a"]);
			proj.SetProperty ("AndroidStripNativeLibraries", "true");
			proj.SetProperty ("_SkipNdkResolution", "true");

			using var builder = CreateApkBuilder ();
			builder.ThrowOnBuildFailure = false;
			Assert.IsFalse (builder.Build (proj), "Optional native library stripping must require an NDK.");
			StringAssertEx.Contains ("error XA5104:", builder.LastBuildOutput, "A missing NDK should produce XA5104.");
		}

		[TestCase ("apk", false)]
		[TestCase ("apk", true)]
		[TestCase ("aab", true)]
		public void BuildCoreClrWithNativeLibraryStripping (string packageFormat, bool isRelease)
		{
			string fixture = GetInstalledRuntimePackFile (AndroidRuntime.CoreCLR, "libnet-android.debug.so");
			byte [] original = File.ReadAllBytes (fixture);
			using (var stream = new MemoryStream (original)) {
				using IELF elf = ELFReader.Load (stream, shouldOwnStream: false);
				Assert.IsTrue (elf.Sections.Any (section => section.Type == SectionType.SymbolTable || section.Name == ".debug_info"),
					"The existing debug runtime library must contain symbols for the stripping regression test.");
			}

			const string nativeLibrary = "native/arm64-v8a/libstrip-input.so";
			var proj = new XamarinAndroidApplicationProject {
				IsRelease = isRelease,
				OtherBuildItems = {
					new AndroidItem.AndroidNativeLibrary (nativeLibrary) {
						BinaryContent = () => original,
					},
				},
			};
			proj.SetRuntime (AndroidRuntime.CoreCLR);
			proj.SetRuntimeIdentifiers (["arm64-v8a"]);
			proj.SetProperty ("AndroidPackageFormat", packageFormat);
			proj.SetProperty ("AndroidStripNativeLibraries", "true");

			using var builder = CreateApkBuilder ();
			builder.Verbosity = LoggerVerbosity.Detailed;
			Assert.IsTrue (builder.Build (proj), $"The {proj.Configuration} {packageFormat} build should strip native libraries using the NDK.");
			StringAssertEx.Contains ("Task \"ResolveAndroidNdk\"", builder.LastBuildOutput, "Native stripping must resolve the NDK tool.");

			string package = Path.Combine (Root, builder.ProjectDirectory, proj.OutputPath, $"{proj.PackageName}-Signed.{packageFormat}");
			string prefix = packageFormat == "aab" ? "base/" : "";
			byte [] stripped = ZipHelper.ReadFileFromZip (package, $"{prefix}lib/arm64-v8a/libstrip-input.so");
			Assert.IsNotNull (stripped, "The stripped native input should be packaged.");
			Assert.Less (stripped.Length, original.Length, "The packaged native library should be smaller after stripping.");
			using (var stream = new MemoryStream (stripped)) {
				using IELF elf = ELFReader.Load (stream, shouldOwnStream: false);
				Assert.AreEqual (FileType.SharedObject, elf.Type);
				Assert.AreEqual (Machine.AArch64, elf.Machine);
				Assert.IsFalse (elf.Sections.Any (section => section.Type == SectionType.SymbolTable || section.Name == ".debug_info"),
					"The packaged library should no longer contain debug symbols.");
				var symbols = (ISymbolTable)elf.GetSection (".dynsym");
				CollectionAssert.Contains (symbols.Entries.Select (symbol => symbol.Name), "JNI_OnLoad",
					"Stripping must preserve dynamic exports.");
			}
			string strippedLibrary = Path.Combine (Root, builder.ProjectDirectory, proj.IntermediateOutputPath, "android-arm64", "stripped", "libstrip-input.so");
			FileAssert.Exists (strippedLibrary);
			CollectionAssert.AreEqual (stripped, File.ReadAllBytes (strippedLibrary), "The package should contain the intermediate stripped copy.");

			builder.BuildLogFile = "stripping-disabled.log";
			Assert.IsTrue (builder.Build (proj, doNotCleanupOnUpdate: true, saveProject: false, parameters: [
				"AndroidStripNativeLibraries=false",
				"_SkipNdkResolution=true",
			]), "Disabling stripping on an incremental build should not require an NDK.");
			StringAssertEx.DoesNotContain ("Task \"ResolveAndroidNdk\"", builder.LastBuildOutput, "Disabling native stripping must remove the NDK requirement.");
			CollectionAssert.AreEqual (original, ZipHelper.ReadFileFromZip (package, $"{prefix}lib/arm64-v8a/libstrip-input.so"),
				"Toggling AndroidStripNativeLibraries off must repackage the original library.");

			builder.BuildLogFile = "stripping-enabled.log";
			Assert.IsTrue (builder.Build (proj, doNotCleanupOnUpdate: true, saveProject: false, parameters: [
				"AndroidStripNativeLibraries=true",
				"_SkipNdkResolution=false",
			]), "Re-enabling stripping should use the NDK on an incremental build.");
			CollectionAssert.AreEqual (stripped, ZipHelper.ReadFileFromZip (package, $"{prefix}lib/arm64-v8a/libstrip-input.so"),
				"Toggling AndroidStripNativeLibraries on must repackage the stripped library.");
			CollectionAssert.AreEqual (original, File.ReadAllBytes (Path.Combine (Root, builder.ProjectDirectory, nativeLibrary)),
				"Stripping must not modify the application's native library input.");
			CollectionAssert.AreEqual (original, File.ReadAllBytes (fixture), "Stripping must not modify the installed runtime pack.");
		}

		[Test]
		public void BuildCoreClrWithNativeLibraryStripping_FailsForInvalidLibrary ()
		{
			var proj = new XamarinAndroidApplicationProject {
				OtherBuildItems = {
					new AndroidItem.AndroidNativeLibrary ("native/arm64-v8a/libinvalid.so") {
						BinaryContent = () => [1, 2, 3, 4],
					},
				},
			};
			proj.SetRuntime (AndroidRuntime.CoreCLR);
			proj.SetRuntimeIdentifiers (["arm64-v8a"]);
			proj.SetProperty ("AndroidStripNativeLibraries", "true");

			using var builder = CreateApkBuilder ();
			builder.ThrowOnBuildFailure = false;
			Assert.IsFalse (builder.Build (proj), "A failed strip operation must fail packaging rather than use the original file.");
			StringAssertEx.Contains ("error XA0142:", builder.LastBuildOutput, "The failed native tool should produce XA0142.");
			StringAssertEx.Contains ("llvm-strip", builder.LastBuildOutput, "The failure should identify the NDK strip command.");
		}

		static string GetNdkToolchainDirectory ()
		{
			string hostTag = TestEnvironment.IsWindows ? "windows-x86_64" :
				TestEnvironment.IsMacOS ? "darwin-x86_64" : "linux-x86_64";
			return Path.Combine (AndroidNdkPath, "toolchains", "llvm", "prebuilt", hostTag);
		}

		static string GetInstalledRuntimePackFile (AndroidRuntime runtime, string fileName)
		{
			Version apiLevel = XABuildConfig.AndroidLatestStableApiLevel;
			string apiLevelName = apiLevel.Minor == 0 ? $"{apiLevel.Major}" : $"{apiLevel.Major}.{apiLevel.Minor}";
			string installedRuntimePack = Path.Combine (
				TestEnvironment.DotNetPreviewPacksDirectory,
				$"Microsoft.Android.Runtime.{runtime}.{apiLevelName}.android-arm64"
			);
			return Directory.GetFiles (installedRuntimePack, fileName, SearchOption.AllDirectories).Single ();
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

			string llvmNm = Path.Combine (GetNdkToolchainDirectory (), "bin", TestEnvironment.IsWindows ? "llvm-nm.exe" : "llvm-nm");
			FileAssert.Exists (llvmNm, "ARM EHABI verification requires llvm-nm from the configured Android NDK.");
			var (exitCode, standardOutput, standardError) = RunProcessWithExitCode (llvmNm, $"--defined-only \"{runtimeArchive}\"");
			Assert.AreEqual (0, exitCode, $"llvm-nm failed:{Environment.NewLine}{standardError}");
			foreach (string symbol in ArmEhabiPersonalitySymbols) {
				StringAssert.Contains ($" W {symbol}", standardOutput, $"{symbol} should be a weak global symbol.");
			}
		}

		[TestCase (AndroidRuntime.NativeAOT)]
		[TestCase (AndroidRuntime.CoreCLR)]
		public void RuntimePackDoesNotContainObsoleteNativeAssets (AndroidRuntime runtime)
		{
			string outputDirectory = Path.Combine (Root, TestName);
			if (Directory.Exists (outputDirectory)) {
				Directory.Delete (outputDirectory, recursive: true);
			}
			Directory.CreateDirectory (outputDirectory);
			Version apiLevel = XABuildConfig.AndroidLatestStableApiLevel;
			string apiLevelName = apiLevel.Minor == 0 ? $"{apiLevel.Major}" : $"{apiLevel.Major}.{apiLevel.Minor}";
			string runtimeAssembly = GetInstalledRuntimePackFile (AndroidRuntime.NativeAOT, "Microsoft.Android.Runtime.NativeAOT.dll");
			string managedOutputRoot = Path.Combine (outputDirectory, "xbuild-frameworks", "Microsoft.Android");
			string managedOutputDirectory = Path.Combine (managedOutputRoot, apiLevelName);
			Directory.CreateDirectory (managedOutputDirectory);
			File.Copy (runtimeAssembly, Path.Combine (managedOutputDirectory, Path.GetFileName (runtimeAssembly)));
			// The pack target requires a PDB, but its contents are unrelated to native asset composition.
			File.Create (Path.Combine (managedOutputDirectory, "Microsoft.Android.Runtime.NativeAOT.pdb")).Dispose ();

			string runtimeOutputRoot = Path.Combine (outputDirectory, "runtime-output");
			string runtimeFlavor = runtime == AndroidRuntime.CoreCLR ? "clr" : "nativeaot";
			CreateNativeRuntimePackInputs (Path.Combine (runtimeOutputRoot, runtimeFlavor, "android-arm64"), runtime);

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
					$"NativeRuntimeOutputRootDir={runtimeOutputRoot}{Path.DirectorySeparatorChar}",
					$"_MonoAndroidNETOutputRoot={managedOutputRoot}{Path.DirectorySeparatorChar}",
					$"BaseIntermediateOutputPath={Path.Combine (outputDirectory, "obj")}{Path.DirectorySeparatorChar}",
					$"PackageOutputPath={outputDirectory}",
				]),
				$"Packing the {runtime} runtime pack should succeed. See {dotnet.ProcessLogFile}."
			);

			string packagePath = Directory.GetFiles (outputDirectory, "*.nupkg")
				.Single (path => !path.EndsWith (".symbols.nupkg", StringComparison.OrdinalIgnoreCase));
			using var package = ZipHelper.OpenZip (packagePath);
			const string nativePrefix = "runtimes/android-arm64/native/";
			foreach (string fileName in GetObsoleteRuntimePackNativeFiles (runtime)) {
				string archivePath = nativePrefix + fileName;
				package.AssertDoesNotContainEntry (packagePath, archivePath);
			}
			CollectionAssert.AreEquivalent (
				GetRuntimePackNativeFiles (runtime).Select (fileName => nativePrefix + fileName),
				package.Select (entry => entry.FullName)
					.Where (path => path.StartsWith (nativePrefix, StringComparison.Ordinal) && !path.EndsWith ("/", StringComparison.Ordinal)),
				"The runtime pack should contain only the native host assets consumed by app builds.");
		}

		[TestCase (AndroidRuntime.NativeAOT)]
		[TestCase (AndroidRuntime.CoreCLR)]
		public void CopyRuntimePackRemovesStaleNativeAssets (AndroidRuntime runtime)
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
			string unrelatedFile = Path.Combine (nativeDirectory, "libunrelated.so");
			File.WriteAllBytes (unrelatedFile, [4, 5, 6]);
			string symbolFile = Path.Combine (nativeDirectory, runtime == AndroidRuntime.CoreCLR ?
				"libnet-android.release.so.debug" : "libnaot-android.release.so.debug");
			File.WriteAllBytes (symbolFile, [7, 8, 9]);

			string runtimeOutputPath = Path.Combine (outputDirectory, "runtime-output");
			string nativeSourceDirectory = Path.Combine (runtimeOutputPath, "android-arm64");
			CreateNativeRuntimePackInputs (nativeSourceDirectory, runtime);

			string nativeProjectName = runtime == AndroidRuntime.NativeAOT ? "native-nativeaot.csproj" : "native-clr.csproj";
			string nativeProject = Path.Combine (XABuildPaths.TopDirectory, "src", "native", nativeProjectName);
			var dotnet = new DotNetCLI (nativeProject) {
				ProjectDirectory = outputDirectory,
				BuildLogFile = Path.Combine (outputDirectory, "build.log"),
				ProcessLogFile = Path.Combine (outputDirectory, "process.log"),
			};
			for (int attempt = 0; attempt < 2; attempt++) {
				foreach (string fileName in GetObsoleteRuntimePackNativeFiles (runtime)) {
					File.Create (Path.Combine (nativeDirectory, fileName)).Dispose ();
				}
				dotnet.BuildLogFile = Path.Combine (outputDirectory, $"build-{attempt}.log");
				dotnet.ProcessLogFile = Path.Combine (outputDirectory, $"process-{attempt}.log");
				Assert.IsTrue (
					dotnet.Build (
						target: "_CopyToPackDirs",
						parameters: [
							$"Configuration={XABuildPaths.Configuration}",
							$"AndroidApiLevel={apiLevelName}",
							$"AndroidPackVersion={packVersion}",
							"AndroidSupportedTargetJitAbis=arm64-v8a",
							$"MicrosoftAndroidPacksRootDir={packsRoot}{Path.DirectorySeparatorChar}",
							$"OutputPath={runtimeOutputPath}{Path.DirectorySeparatorChar}",
						]
					),
					$"Copying the {runtime} runtime pack should succeed. See {dotnet.ProcessLogFile}."
				);

				foreach (string fileName in GetObsoleteRuntimePackNativeFiles (runtime)) {
					FileAssert.DoesNotExist (Path.Combine (nativeDirectory, fileName));
				}
				foreach (string fileName in GetRuntimePackNativeFiles (runtime)) {
					string destination = Path.Combine (nativeDirectory, fileName);
					FileAssert.Exists (destination);
					CollectionAssert.AreEqual (File.ReadAllBytes (Path.Combine (nativeSourceDirectory, fileName)), File.ReadAllBytes (destination));
				}
				CollectionAssert.AreEqual (new byte [] { 4, 5, 6 }, File.ReadAllBytes (unrelatedFile),
					"Removing obsolete runtime assets must preserve unrelated files.");
				CollectionAssert.AreEqual (new byte [] { 7, 8, 9 }, File.ReadAllBytes (symbolFile),
					"Removing obsolete runtime assets must preserve existing optional symbol sidecars.");
				if (attempt == 1) {
					dotnet.AssertTargetIsSkipped ("_CopyToPackDirs");
				}
			}
		}
	}
}
