using System;
using System.IO;
using System.Linq;
using System.Text.Json;

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
			foreach (string responseFile in responseFiles) {
				string response = File.ReadAllText (responseFile);
				StringAssert.Contains ("libnaot-android.release-static-release.a", response, responseFile);
				StringAssert.DoesNotContain ("jni_init_funcs.", response, responseFile);
				StringAssert.DoesNotContain ("environment.", response, responseFile);
				foreach (string archiveName in CPlusPlusArchiveNames) {
					StringAssert.DoesNotContain (archiveName, response, responseFile);
				}
			}
			string nativeObject = Path.Combine (intermediateDirectory, MonoAndroidHelper.AbiToRid (abi), "native", proj.ProjectName + ".o");
			NdkTools ndk = NdkTools.Create (AndroidNdkPath);
			ndk.OSBinPath = TestEnvironment.OSBinDirectory;
			string llvmNm = ndk.GetToolPath ("llvm-nm", MonoAndroidHelper.AbiToTargetArch (abi), 0);
			var (exitCode, standardOutput, standardError) = RunProcessWithExitCode (llvmNm, $"--undefined-only \"{nativeObject}\"");
			Assert.AreEqual (0, exitCode, $"llvm-nm failed:{Environment.NewLine}{standardError}");
			CollectionAssert.Contains (standardOutput.Split ('\n').Select (line => line.Trim ()), "U AndroidCryptoNative_InitLibraryOnLoad",
				"The managed JNI_OnLoad must retain a direct crypto initializer reference even without application crypto calls.");
		}

		[Test]
		public void BootstrapUpdatesForCommandLineFlavorChanges ()
		{
			var proj = new XamarinAndroidApplicationProject {
				IsRelease = true,
				PackageName = "com.xamarin.bootstraprename",
				MainActivity = """
					[Activity (Name = "my.app.MainActivity", MainLauncher = true)]
					public class MainActivity : Activity
					{
						protected override void OnCreate (Bundle? bundle)
						{
							base.OnCreate (bundle);
					#if BOOTSTRAP_FIRST
							Android.Util.Log.Info ("BootstrapFlavor", "first");
					#else
							Android.Util.Log.Info ("BootstrapFlavor", "second");
					#endif
						}
					}
					""",
				OtherBuildItems = {
					new BuildItem ("None", "first-environment.txt") {
						TextContent = () => "BOOTSTRAP_TEST=first\ndebug.dotnet.max_grefc=1234",
					},
					new BuildItem ("None", "second-environment.txt") {
						TextContent = () => "BOOTSTRAP_TEST=second\ndebug.dotnet.max_grefc=5678",
					},
				},
			};
			proj.Imports.Add (new Import ("BootstrapEnvironment.targets") {
				TextContent = () => """
					<Project>
					  <ItemGroup>
					    <AndroidEnvironment Include="first-environment.txt" Condition=" '$(_BootstrapEnvironmentFlavor)' == 'first' or '$(_BootstrapEnvironmentFlavor)' == 'forward' " />
					    <AndroidEnvironment Include="second-environment.txt"
					        Condition=" '$(_BootstrapEnvironmentFlavor)' == 'second' or '$(_BootstrapEnvironmentFlavor)' == 'forward' or '$(_BootstrapEnvironmentFlavor)' == 'reverse' " />
					    <AndroidEnvironment Include="first-environment.txt" Condition=" '$(_BootstrapEnvironmentFlavor)' == 'reverse' " />
					  </ItemGroup>
					</Project>
					""",
			});
			proj.SetRuntime (AndroidRuntime.NativeAOT);
			proj.SetRuntimeIdentifiers (["arm64-v8a"]);
			proj.SetProperty ("AndroidPackageFormat", "apk");

			using var builder = CreateApkBuilder ();
			string [] firstParameters = ["AssemblyName=BootstrapOriginal", "_BootstrapEnvironmentFlavor=first", "DefineConstants=BOOTSTRAP_FIRST"];
			string [] renamedParameters = ["AssemblyName=BootstrapRenamed", "_BootstrapEnvironmentFlavor=reverse", "DefineConstants=BOOTSTRAP_SECOND"];
			Assert.IsTrue (builder.Build (proj, parameters: firstParameters));
			builder.AutomaticNuGetRestore = false;

			string projectDirectory = Path.Combine (Root, builder.ProjectDirectory);
			string intermediate = Path.Combine (projectDirectory, proj.IntermediateOutputPath);
			string sourceFile = Path.Combine (intermediate, "android", "src", "net", "dot", "jni", "nativeaot", "JavaInteropRuntime.java");
			string environmentSource = Path.Combine (Path.GetDirectoryName (sourceFile), "NativeAotEnvironmentVars.java");
			string [] inputs = [
				Path.Combine (projectDirectory, proj.ProjectFilePath),
				Path.Combine (projectDirectory, "BootstrapEnvironment.targets"),
				Path.Combine (projectDirectory, "first-environment.txt"),
				Path.Combine (projectDirectory, "second-environment.txt"),
				Path.Combine (intermediate, "AndroidManifest.xml"),
				Path.Combine (intermediate, "build.props"),
				Path.GetFullPath (Path.Combine (intermediate, "..", "project.assets.json")),
			];
			var inputTimestamps = inputs.Select (File.GetLastWriteTimeUtc).ToArray ();
			StringAssert.Contains ("NativeLibraryHelper.loadLibrary(\"BootstrapOriginal\", context);", File.ReadAllText (sourceFile));
			string nativeObject = Path.Combine (intermediate, "android-arm64", "native", "BootstrapOriginal.o");
			DateTime nativeObjectTimestamp = File.GetLastWriteTimeUtc (nativeObject);
			Assert.Less (File.GetLastWriteTimeUtc (inputs [3]), File.GetLastWriteTimeUtc (environmentSource),
				"The alternate environment file must predate the bootstrap outputs.");

			Assert.IsTrue (builder.Build (proj, doNotCleanupOnUpdate: true, saveProject: false, parameters: [
				"AssemblyName=BootstrapOriginal", "_BootstrapEnvironmentFlavor=second", "DefineConstants=BOOTSTRAP_SECOND",
			]));
			Assert.Greater (File.GetLastWriteTimeUtc (nativeObject), nativeObjectTimestamp, "The managed build flavor must recompile.");
			CollectionAssert.AreEqual (inputTimestamps, inputs.Select (File.GetLastWriteTimeUtc),
				"Selecting a pre-existing environment file must not change the other bootstrap inputs.");
			Assert.AreEqual ("second", EnvironmentHelper.ReadNativeAotEnvironmentVariables (intermediate) ["BOOTSTRAP_TEST"]);
			StringAssert.Contains ("\"debug.dotnet.max_grefc\",\n\t\t\"5678\"", File.ReadAllText (environmentSource));
			builder.Output.AssertTargetIsNotSkipped ("_AndroidGenerateNativeAotBootstrapSources");

			foreach (var (flavor, expectedValue) in new [] { ("forward", "second"), ("reverse", "first") }) {
				Assert.IsTrue (builder.Build (proj, doNotCleanupOnUpdate: true, saveProject: false, parameters: [
					"AssemblyName=BootstrapOriginal", "_BootstrapEnvironmentFlavor=" + flavor, "DefineConstants=BOOTSTRAP_SECOND",
				]));
				Assert.AreEqual (expectedValue, EnvironmentHelper.ReadNativeAotEnvironmentVariables (intermediate) ["BOOTSTRAP_TEST"]);
				string expectedProperty = expectedValue == "first" ? "1234" : "5678";
				StringAssert.Contains ($"\"debug.dotnet.max_grefc\",\n\t\t\"{expectedProperty}\"", File.ReadAllText (environmentSource));
				CollectionAssert.AreEqual (inputTimestamps, inputs.Select (File.GetLastWriteTimeUtc),
					"Reordering pre-existing files must update override precedence without changing their timestamps.");
				builder.Output.AssertTargetIsNotSkipped ("_AndroidGenerateNativeAotBootstrapSources");
			}

			Assert.IsTrue (builder.Build (proj, doNotCleanupOnUpdate: true, parameters: renamedParameters, saveProject: false));
			StringAssert.Contains ("NativeLibraryHelper.loadLibrary(\"BootstrapRenamed\", context);", File.ReadAllText (sourceFile));
			builder.Output.AssertTargetIsNotSkipped ("_AndroidGenerateNativeAotBootstrapSources");
			CollectionAssert.AreEqual (inputTimestamps, inputs.Select (File.GetLastWriteTimeUtc),
				"The manifest, environment and project must stay unchanged during the command-line AssemblyName change.");

			string packagePath = Path.Combine (projectDirectory, proj.OutputPath, proj.PackageName + "-Signed.apk");
			using (var package = ZipHelper.OpenZip (packagePath)) {
				Assert.IsNotNull (package);
				package.AssertContainsEntry (packagePath, "lib/arm64-v8a/libBootstrapRenamed.so");
				package.AssertDoesNotContainEntry (packagePath, "lib/arm64-v8a/libBootstrapOriginal.so");
			}

			string fingerprint = Path.Combine (intermediate, "nativeaot-bootstrap.inputs");
			FileAssert.Exists (fingerprint);
			DateTime fingerprintTimestamp = File.GetLastWriteTimeUtc (fingerprint);
			DateTime sourceTimestamp = File.GetLastWriteTimeUtc (sourceFile);
			Assert.IsTrue (builder.Build (proj, doNotCleanupOnUpdate: true, parameters: renamedParameters, saveProject: false));
			builder.Output.AssertTargetIsSkipped ("_AndroidGenerateNativeAotBootstrapSources");
			Assert.AreEqual (fingerprintTimestamp, File.GetLastWriteTimeUtc (fingerprint));
			Assert.AreEqual (sourceTimestamp, File.GetLastWriteTimeUtc (sourceFile));

			Assert.IsTrue (builder.RunTarget (proj, "Clean", doNotCleanupOnUpdate: true, parameters: renamedParameters, saveProject: false));
			FileAssert.DoesNotExist (fingerprint);
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
