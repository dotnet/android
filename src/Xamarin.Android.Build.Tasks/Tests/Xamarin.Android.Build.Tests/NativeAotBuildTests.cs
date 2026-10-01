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
			proj.SetProperty ("_SkipNdkResolution", "true");

			using var builder = CreateApkBuilder ();
			builder.Verbosity = LoggerVerbosity.Detailed;
			Assert.IsTrue (builder.Build (proj), $"CoreCLR app build should succeed for {abi} with the prebuilt runtime.");
			builder.Output.AssertTargetIsSkipped ("_LinkNativeRuntime", defaultIfNotUsed: true);
			StringAssertEx.DoesNotContain ("Task \"ResolveAndroidNdk\"", builder.LastBuildOutput, "Ordinary CoreCLR should not require an NDK.");
		}

		[TestCase ("armeabi-v7a")]
		[TestCase ("arm64-v8a")]
		[TestCase ("x86_64")]
		public void BuildNativeAotWithOfficialNdk (string abi)
		{
			var proj = new XamarinAndroidApplicationProject {
				IsRelease = true,
			};
			proj.SetRuntime (AndroidRuntime.NativeAOT);
			proj.SetRuntimeIdentifiers ([abi]);
			proj.SetProperty ("_SkipNdkResolution", "false");

			using var builder = CreateApkBuilder ();
			builder.Verbosity = LoggerVerbosity.Detailed;
			Assert.IsTrue (builder.Build (proj), $"NativeAOT build should succeed for {abi} without libc++ or libunwind.");
			string toolchain = GetNdkToolchainDirectory ();
			StringAssertEx.Contains (Path.Combine (toolchain, "bin", TestEnvironment.IsWindows ? "ld.lld.exe" : "ld.lld"), builder.LastBuildOutput,
				"The final linker must come from the official Android NDK.");

			string intermediateDirectory = Path.Combine (Root, builder.ProjectDirectory, proj.IntermediateOutputPath);
			string [] responseFiles = Directory.GetFiles (intermediateDirectory, "ld.*.rsp", SearchOption.AllDirectories);
			Assert.IsNotEmpty (responseFiles, "Native linker response files should be generated.");
			foreach (string responseFile in responseFiles) {
				string response = File.ReadAllText (responseFile);
				StringAssert.Contains ("libnaot-android.release-static-release.a", response, responseFile);
				StringAssert.Contains (Path.Combine (toolchain, "sysroot", "usr", "lib"), response, "CRT and system libraries must come from the NDK.");
				StringAssert.Contains (Path.Combine (toolchain, "lib", "clang"), response, "Compiler runtime must come from the NDK.");
				string projectDirectory = Path.Combine (Root, builder.ProjectDirectory);
				string [] linkedObjects = File.ReadAllLines (responseFile)
					.Select (line => line.Trim ('"'))
					.Where (line => line.EndsWith (".o", StringComparison.Ordinal))
					.Select (line => Path.GetFullPath (line, projectDirectory))
					.ToArray ();
				foreach (string objectName in new [] { $"jni_init_funcs.{abi}.o", $"environment.{abi}.o" }) {
					string objectFile = Directory.GetFiles (intermediateDirectory, objectName, SearchOption.AllDirectories).Single ();
					Assert.IsTrue (linkedObjects.Contains (objectFile), $"The generated object {objectName} must remain linked.");
				}
				foreach (string archiveName in CPlusPlusArchiveNames) {
					StringAssert.DoesNotContain (archiveName, response, responseFile);
				}
				if (abi == "armeabi-v7a") {
					AssertArmEhabiSymbolsPromoted (builder, proj);
				}
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
		[TestCase ("aab", false)]
		[TestCase ("aab", true)]
		public void BuildCoreClrWithNativeLibraryStripping (string packageFormat, bool isRelease)
		{
			string fixture = GetInstalledRuntimePackFile ("libnet-android.debug.so");
			byte [] original = File.ReadAllBytes (fixture);
			using (var stream = new MemoryStream (original)) {
				using IELF elf = ELFReader.Load (stream, shouldOwnStream: false);
				Assert.IsTrue (elf.Sections.Any (section => section.Type == SectionType.SymbolTable || section.Name == ".debug_info"),
					"The debug runtime fixture must contain symbols.");
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
			StringAssertEx.Contains (Path.Combine (GetNdkToolchainDirectory (), "bin", TestEnvironment.IsWindows ? "llvm-strip.exe" : "llvm-strip"),
				builder.LastBuildOutput, "The strip tool must come from the NDK.");

			string package = Path.Combine (Root, builder.ProjectDirectory, proj.OutputPath, $"{proj.PackageName}-Signed.{packageFormat}");
			string archivePath = $"{(packageFormat == "aab" ? "base/" : "")}lib/arm64-v8a/libstrip-input.so";
			byte [] stripped = ZipHelper.ReadFileFromZip (package, archivePath);
			Assert.IsNotNull (stripped, "The stripped native input should be packaged.");
			Assert.Less (stripped.Length, original.Length, "The packaged native library should be smaller after stripping.");
			using (var stream = new MemoryStream (stripped)) {
				using IELF elf = ELFReader.Load (stream, shouldOwnStream: false);
				Assert.AreEqual (FileType.SharedObject, elf.Type);
				Assert.AreEqual (Machine.AArch64, elf.Machine);
				Assert.IsFalse (elf.Sections.Any (section => section.Type == SectionType.SymbolTable || section.Name == ".debug_info"));
				var symbols = (ISymbolTable)elf.GetSection (".dynsym");
				CollectionAssert.Contains (symbols.Entries.Select (symbol => symbol.Name), "JNI_OnLoad", "Stripping must preserve dynamic exports.");
			}
			string intermediateDirectory = Path.Combine (Root, builder.ProjectDirectory, proj.IntermediateOutputPath);
			string strippedLibrary = Path.Combine (intermediateDirectory, "android-arm64", "stripped", "libstrip-input.so");
			FileAssert.Exists (strippedLibrary);
			CollectionAssert.AreEqual (stripped, File.ReadAllBytes (strippedLibrary), "The package should contain the intermediate stripped copy.");
			string [] fileWrites = File.ReadAllLines (Path.Combine (intermediateDirectory, $"{proj.ProjectName}.csproj.FileListAbsolute.txt"));
			Assert.IsTrue (fileWrites.Contains (strippedLibrary), "The stripped copy must be tracked for Clean.");

			DateTime stripTime = File.GetLastWriteTimeUtc (strippedLibrary);
			builder.BuildLogFile = "stripping-unchanged.log";
			Assert.IsTrue (builder.Build (proj, doNotCleanupOnUpdate: true, saveProject: false), "An unchanged build should reuse the stripped copy.");
			FileAssert.Exists (strippedLibrary, "IncrementalClean must preserve the stripped copy.");
			if (isRelease) {
				Assert.AreEqual (stripTime, File.GetLastWriteTimeUtc (strippedLibrary), "An unchanged Release build should not strip again.");
			}
			fileWrites = File.ReadAllLines (Path.Combine (intermediateDirectory, $"{proj.ProjectName}.csproj.FileListAbsolute.txt"));
			Assert.IsTrue (fileWrites.Contains (strippedLibrary), "The stripped copy must remain tracked after an unchanged build.");

			builder.BuildLogFile = "stripping-disabled.log";
			Assert.IsTrue (builder.Build (proj, doNotCleanupOnUpdate: true, saveProject: false, parameters: [
				"AndroidStripNativeLibraries=false",
				"_SkipNdkResolution=true",
			]), "Disabling stripping on an incremental build should not require an NDK.");
			StringAssertEx.DoesNotContain ("Task \"ResolveAndroidNdk\"", builder.LastBuildOutput, "Disabling stripping must remove the NDK requirement.");
			CollectionAssert.AreEqual (original, ZipHelper.ReadFileFromZip (package, archivePath), "Toggling stripping off must repackage the original.");

			builder.BuildLogFile = "stripping-enabled.log";
			Assert.IsTrue (builder.Build (proj, doNotCleanupOnUpdate: true, saveProject: false, parameters: [
				"AndroidStripNativeLibraries=true",
				"_SkipNdkResolution=false",
			]), "Re-enabling stripping should use the NDK on an incremental build.");
			CollectionAssert.AreEqual (stripped, ZipHelper.ReadFileFromZip (package, archivePath), "Toggling stripping on must repackage the stripped copy.");
			CollectionAssert.AreEqual (original, File.ReadAllBytes (Path.Combine (Root, builder.ProjectDirectory, nativeLibrary)), "Stripping must not modify project inputs.");
			CollectionAssert.AreEqual (original, File.ReadAllBytes (fixture), "Stripping must not modify the installed runtime pack.");

			Assert.IsTrue (builder.Clean (proj), "Clean should remove intermediate stripped libraries.");
			FileAssert.DoesNotExist (strippedLibrary);
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
			Assert.IsFalse (builder.Build (proj), "A failed strip must fail packaging rather than use the original file.");
			StringAssertEx.Contains ("error XA0142:", builder.LastBuildOutput, "The failed native tool should produce XA0142.");
			StringAssertEx.Contains ("llvm-strip", builder.LastBuildOutput, "The failure should identify the NDK strip command.");
			string strippedLibrary = Path.Combine (Root, builder.ProjectDirectory, proj.IntermediateOutputPath, "android-arm64", "stripped", "libinvalid.so");
			FileAssert.DoesNotExist (strippedLibrary, "A failed strip must remove any partial output.");
		}

		static string GetNdkToolchainDirectory ()
		{
			string hostTag = TestEnvironment.IsWindows ? "windows-x86_64" :
				TestEnvironment.IsMacOS ? "darwin-x86_64" : "linux-x86_64";
			return Path.Combine (AndroidNdkPath, "toolchains", "llvm", "prebuilt", hostTag);
		}

		static string GetInstalledRuntimePackFile (string fileName)
		{
			Version apiLevel = XABuildConfig.AndroidLatestStableApiLevel;
			string apiLevelName = apiLevel.Minor == 0 ? $"{apiLevel.Major}" : $"{apiLevel.Major}.{apiLevel.Minor}";
			string installedRuntimePack = Path.Combine (TestEnvironment.DotNetPreviewPacksDirectory, $"Microsoft.Android.Runtime.CoreCLR.{apiLevelName}.android-arm64");
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

	}
}
