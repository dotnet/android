using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;

using ELFSharp.ELF;
using ELFSharp.ELF.Sections;
using Microsoft.Android.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Xamarin.Android.Tasks;
using Xamarin.Android.Tools;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests
{
	/// <summary>
	/// Build tests for NativeAOT and native runtime packaging.
	/// </summary>
	[TestClass]
	[TestCategory ("Node-2")]
	[DoNotParallelize]
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

		[TestMethod]
		[DataRow ("armeabi-v7a")]
		[DataRow ("arm64-v8a")]
		[DataRow ("x86_64")]
		public void BuildCoreClrWithPrebuiltRuntime (string abi)
		{
			var proj = new XamarinAndroidApplicationProject {
				IsRelease = true,
			};
			proj.SetRuntime (AndroidRuntime.CoreCLR);
			proj.SetRuntimeIdentifiers ([abi]);
			using var builder = CreateApkBuilder ();
			string missingNdk = Path.Combine (Root, "missing-ndk-for-prebuilt-coreclr");
			Assert.IsFalse (Directory.Exists (missingNdk));
			Assert.IsTrue (builder.Build (proj, parameters: ["AndroidNdkDirectory=" + missingNdk]),
				$"CoreCLR app build should succeed for {abi} without the Android NDK.");
			string packagePath = Path.Combine (Root, builder.ProjectDirectory, proj.OutputPath, proj.PackageName + "-Signed.apk");
			Assert.IsTrue (File.Exists (packagePath), "An NDK-free CoreCLR build must produce a signed package.");
			using var package = ZipHelper.OpenZip (packagePath);
			package.AssertContainsEntry (packagePath, $"lib/{abi}/libbinary_blobs.so");
		}

		[TestMethod]
		[DataRow ("armeabi-v7a")]
		[DataRow ("arm64-v8a")]
		[DataRow ("x86_64")]
		public void BuildNativeAotWithoutCPlusPlusArchives (string abi)
		{
			var proj = new XamarinAndroidApplicationProject {
				IsRelease = true,
			};
			proj.SetRuntime (AndroidRuntime.NativeAOT);
			proj.SetRuntimeIdentifiers ([abi]);
			proj.SetProperty ("DebugSymbols", "false");

			using var builder = CreateApkBuilder ();
			Assert.IsTrue (builder.Build (proj), $"NativeAOT build should succeed for {abi} without libc++ or libunwind.");

			string intermediateDirectory = Path.Combine (Root, builder.ProjectDirectory, proj.IntermediateOutputPath);
			string [] responseFiles = Directory.GetFiles (intermediateDirectory, "ld.*.rsp", SearchOption.AllDirectories);
			Assert.IsNotEmpty (responseFiles, "Native linker response files should be generated.");
			foreach (string responseFile in responseFiles) {
				string response = File.ReadAllText (responseFile);
				Assert.Contains ("libnaot-android.release-static-release.a", response, responseFile);
				Assert.Contains ("nativeaot-bootstrap.o", response, responseFile);
				Assert.Contains ("--undefined=NativeAOT_InitializeEnvironment", response, responseFile);
				Assert.Contains ("-init=NativeAOT_InitializeEnvironment", response, responseFile);
				Assert.DoesNotContain ("jni_init_funcs.", response, responseFile);
				Assert.DoesNotContain ("environment.", response, responseFile);
				foreach (string archiveName in CPlusPlusArchiveNames) {
					Assert.DoesNotContain (archiveName, response, responseFile);
				}
			}
			if (abi == "armeabi-v7a") {
				AssertArmEhabiSymbolsPromoted (builder, proj);
			}
			string nativeObject = Path.Combine (intermediateDirectory, MonoAndroidHelper.AbiToRid (abi), "native", proj.ProjectName + ".o");
			string llvmNm = Path.Combine (GetNdkToolchainDirectory (), "bin", TestEnvironment.IsWindows ? "llvm-nm.exe" : "llvm-nm");
			var (exitCode, standardOutput, standardError) = RunProcessWithExitCode (llvmNm, $"--undefined-only \"{nativeObject}\"");
			Assert.AreEqual (0, exitCode, $"llvm-nm failed:{Environment.NewLine}{standardError}");
			CollectionAssert.Contains (standardOutput.Split ('\n').Select (line => line.Trim ()).ToArray (), "U AndroidCryptoNative_InitLibraryOnLoad",
				"The managed JNI_OnLoad must retain a direct crypto initializer reference even without application crypto calls.");
			string applicationLibrary = Directory.GetFiles (
				Path.Combine (Root, builder.ProjectDirectory, proj.OutputPath), $"lib{proj.ProjectName}.so", SearchOption.AllDirectories).Single ();
			string dynamic = NativeToolTestHelper.Run ("llvm-readelf", "--dynamic", applicationLibrary);
			Assert.Contains ("(INIT)", dynamic, "The fixed environment hook must survive as DT_INIT.");
			Assert.Contains ("(INIT_ARRAY)", dynamic, "NativeAOT's initialization array must remain intact.");
			Assert.DoesNotContain ("libbinary_blobs.so", dynamic, "NativeAOT data must not introduce a dynamic library dependency.");
			Assert.DoesNotContain ("libnaot-android", dynamic, "The NativeAOT host must be linked statically.");
			using var elf = NativeToolTestHelper.ReadElf (applicationLibrary);
			var exports = elf.RootElement [0].GetProperty ("DynamicSymbols").EnumerateArray ()
				.Select (entry => entry.GetProperty ("Symbol").GetProperty ("Name").GetProperty ("Name").GetString ()).ToArray ();
			CollectionAssert.DoesNotContain (exports, "dotnet_android_nativeaot_bootstrap");
			CollectionAssert.DoesNotContain (exports, "dotnet_android_nativeaot_bootstrap_size");
			CollectionAssert.DoesNotContain (exports, "dotnet_android_nativeaot_remapping_data");
			CollectionAssert.DoesNotContain (exports, "dotnet_android_nativeaot_remapping_data_size");
			CollectionAssert.DoesNotContain (exports, "NativeAOT_InitializeEnvironment");
			string definitions = NativeToolTestHelper.Run ("llvm-nm", "--demangle", "--defined-only",
				Path.ChangeExtension (applicationLibrary, ".dbg.so"));
			Assert.Contains ("BinaryBlobLoader::read_uncompressed(", definitions);
			Assert.DoesNotContain ("BinaryBlobLoader::decode(", definitions,
				"The NativeAOT host must not retain the CoreCLR blob decompression path.");
			Assert.DoesNotContain ("BinaryBlobLoader::load(", definitions);
			Assert.DoesNotContain ("BinaryBlobLoader::load_optional(", definitions);
			Assert.DoesNotContain ("OSBridge::", definitions);
			Assert.DoesNotContain ("RuntimeEnvironment::", definitions,
				"NativeAOT startup must use the supplied JNI environment without retaining a native JavaVM cache.");
		}

		[TestMethod]
		public void BootstrapUpdatesForCommandLineFlavorChanges ()
		{
			var proj = new XamarinAndroidApplicationProject {
				IsRelease = true,
				PackageName = "com.xamarin.bootstraprename",
				MainActivity = """
					[Activity (Name = "my.app.MainActivity", MainLauncher = true, Process = ":secondary")]
					public class MainActivity : Activity
					{
						protected override void OnCreate (Bundle? bundle)
						{
							base.OnCreate (bundle);
					#if BOOTSTRAP_FIRST
							Android.Util.Log.Info ("BootstrapFlavor", "first:" + System.Environment.GetEnvironmentVariable ("BOOTSTRAP_TEST"));
					#else
							Android.Util.Log.Info ("BootstrapFlavor", "second:" + System.Environment.GetEnvironmentVariable ("BOOTSTRAP_TEST"));
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
			builder.Save (proj);
			string projectDirectory = Path.Combine (Root, builder.ProjectDirectory);
			string intermediate = Path.Combine (projectDirectory, proj.IntermediateOutputPath);
			string legacySource = Path.Combine (intermediate, "android", "src", "net", "dot", "jni", "nativeaot", "NativeAotEnvironmentVars.java");
			Directory.CreateDirectory (Path.GetDirectoryName (legacySource));
			File.WriteAllText (legacySource, """
				package net.dot.jni.nativeaot;
				public class NativeAotEnvironmentVars {
					static String[] envNames = { "REMOVED_APP_SETTING" };
					static String[] envValues = { "obsolete-value" };
				}
				""");
			Assert.IsTrue (builder.Build (proj, doNotCleanupOnUpdate: true, parameters: firstParameters));
			builder.AutomaticNuGetRestore = false;

			string sourceFile = Path.Combine (intermediate, "android", "src", "net", "dot", "jni", "nativeaot", "JavaInteropRuntime.java");
			string environmentObject = Path.Combine (intermediate, "android-arm64", "native", "data", "nativeaot-bootstrap.o");
			Assert.IsFalse (File.Exists (legacySource), "Upgrading an existing intermediate directory must remove obsolete Java environment data.");
			Assert.IsFalse (DexUtils.ContainsClass ("Lnet/dot/jni/nativeaot/NativeAotEnvironmentVars;",
				Path.Combine (intermediate, "android", "bin", "classes.dex"), AndroidSdkPath),
				"The obsolete environment class must not remain in the application's DEX.");
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
			Assert.Contains ("NativeLibraryHelper.loadLibrary(\"BootstrapOriginal\", context);", File.ReadAllText (sourceFile));
			string nativeObject = Path.Combine (intermediate, "android-arm64", "native", "BootstrapOriginal.o");
			DateTime nativeObjectTimestamp = File.GetLastWriteTimeUtc (nativeObject);
			DateTime bootstrapSourceTimestamp = File.GetLastWriteTimeUtc (sourceFile);
			Assert.IsLessThan (File.GetLastWriteTimeUtc (environmentObject), File.GetLastWriteTimeUtc (inputs [3]),
				"The alternate environment file must predate the bootstrap outputs.");

			Assert.IsTrue (builder.Build (proj, doNotCleanupOnUpdate: true, saveProject: false, parameters: [
				"AssemblyName=BootstrapOriginal", "_BootstrapEnvironmentFlavor=second", "DefineConstants=BOOTSTRAP_SECOND",
			]));
			Assert.IsGreaterThan (nativeObjectTimestamp, File.GetLastWriteTimeUtc (nativeObject), "The managed build flavor must recompile.");
			CollectionAssert.AreEqual (inputTimestamps, inputs.Select (File.GetLastWriteTimeUtc).ToArray (),
				"Selecting a pre-existing environment file must not change the other bootstrap inputs.");
			Assert.AreEqual ("second", EnvironmentHelper.ReadNativeAotEnvironmentVariables (intermediate) ["BOOTSTRAP_TEST"]);
			Assert.AreEqual ("5678", EnvironmentHelper.ReadNativeAotEnvironmentVariables (intermediate, systemProperties: true) ["debug.dotnet.max_grefc"]);
			builder.Output.AssertTargetIsNotSkipped ("_AndroidGenerateNativeAotBootstrapObject");
			builder.Output.AssertTargetIsSkipped ("_AndroidGenerateNativeAotBootstrapSources", defaultIfNotUsed: true);
			Assert.AreEqual (bootstrapSourceTimestamp, File.GetLastWriteTimeUtc (sourceFile), "Unchanged library/provider selection must not rewrite Java.");
			nativeObjectTimestamp = File.GetLastWriteTimeUtc (nativeObject);

			foreach (var (flavor, expectedValue) in new [] { ("forward", "second"), ("reverse", "first") }) {
				Assert.IsTrue (builder.Build (proj, doNotCleanupOnUpdate: true, saveProject: false, parameters: [
					"AssemblyName=BootstrapOriginal", "_BootstrapEnvironmentFlavor=" + flavor, "DefineConstants=BOOTSTRAP_SECOND",
				]));
				Assert.AreEqual (expectedValue, EnvironmentHelper.ReadNativeAotEnvironmentVariables (intermediate) ["BOOTSTRAP_TEST"]);
				string expectedProperty = expectedValue == "first" ? "1234" : "5678";
				Assert.AreEqual (expectedProperty, EnvironmentHelper.ReadNativeAotEnvironmentVariables (intermediate, systemProperties: true) ["debug.dotnet.max_grefc"]);
				CollectionAssert.AreEqual (inputTimestamps, inputs.Select (File.GetLastWriteTimeUtc).ToArray (),
					"Reordering pre-existing files must update override precedence without changing their timestamps.");
				builder.Output.AssertTargetIsNotSkipped ("_AndroidGenerateNativeAotBootstrapObject");
				builder.Output.AssertTargetIsSkipped ("_AndroidGenerateNativeAotBootstrapSources", defaultIfNotUsed: true);
				Assert.AreEqual (bootstrapSourceTimestamp, File.GetLastWriteTimeUtc (sourceFile), "Environment order must not regenerate Java.");
				Assert.AreEqual (nativeObjectTimestamp, File.GetLastWriteTimeUtc (nativeObject), "Environment order must not recompile managed ILC output.");
				builder.Output.AssertTargetIsSkipped ("_CompileToDalvik", defaultIfNotUsed: true);
			}

			Assert.IsTrue (builder.Build (proj, doNotCleanupOnUpdate: true, parameters: renamedParameters, saveProject: false));
			Assert.Contains ("NativeLibraryHelper.loadLibrary(\"BootstrapRenamed\", context);", File.ReadAllText (sourceFile));
			builder.Output.AssertTargetIsNotSkipped ("_AndroidGenerateNativeAotBootstrapSources");
			CollectionAssert.AreEqual (inputTimestamps, inputs.Select (File.GetLastWriteTimeUtc).ToArray (),
				"The manifest, environment and project must stay unchanged during the command-line AssemblyName change.");

			string packagePath = Path.Combine (projectDirectory, proj.OutputPath, proj.PackageName + "-Signed.apk");
			using (var package = ZipHelper.OpenZip (packagePath)) {
				Assert.IsNotNull (package);
				package.AssertContainsEntry (packagePath, "lib/arm64-v8a/libBootstrapRenamed.so");
				package.AssertDoesNotContainEntry (packagePath, "lib/arm64-v8a/libBootstrapOriginal.so");
			}

			string fingerprint = Path.Combine (intermediate, "nativeaot-bootstrap.inputs");
			Assert.IsTrue (File.Exists (fingerprint));
			DateTime fingerprintTimestamp = File.GetLastWriteTimeUtc (fingerprint);
			DateTime sourceTimestamp = File.GetLastWriteTimeUtc (sourceFile);
			Assert.IsTrue (builder.Build (proj, doNotCleanupOnUpdate: true, parameters: renamedParameters, saveProject: false));
			builder.Output.AssertTargetIsSkipped ("_AndroidGenerateNativeAotBootstrapSources");
			Assert.AreEqual (fingerprintTimestamp, File.GetLastWriteTimeUtc (fingerprint));
			Assert.AreEqual (sourceTimestamp, File.GetLastWriteTimeUtc (sourceFile));

			string deviceTarget = Environment.GetEnvironmentVariable ("ADB_TARGET");
			if (!string.IsNullOrEmpty (deviceTarget)) {
				string sourceManifest = Path.Combine (projectDirectory, "Properties", "AndroidManifest.xml");
				string packageName = (string)XDocument.Load (sourceManifest).Root?.Attribute ("package");
				Assert.AreEqual (proj.PackageName, packageName);
				Console.Out.WriteLine ($"Installing fixture package '{packageName}' from '{sourceManifest}' to '{deviceTarget}'.");
				string [] deviceParameters = [.. renamedParameters, "AdbTarget=" + deviceTarget];
				Assert.IsTrue (builder.Install (proj, doNotCleanupOnUpdate: true, parameters: deviceParameters, saveProject: false));
				try {
					RunAdbCommand ("logcat -c");
					Assert.IsTrue (builder.RunTarget (proj, "Run", doNotCleanupOnUpdate: true, parameters: deviceParameters, saveProject: false));
					string startupLog = "";
					WaitFor (TimeSpan.FromSeconds (30), () => {
						startupLog = RunAdbCommand ("logcat -d -s BootstrapFlavor:I");
						return startupLog.Contains ("second:first", StringComparison.Ordinal);
					}, intervalInMS: 250);
					Assert.Contains ("second:first", startupLog,
						"The renamed app and secondary-process provider must read the selected bootstrap environment.");
				} finally {
					Assert.IsTrue (builder.RunTarget (proj, "Uninstall", doNotCleanupOnUpdate: true, parameters: deviceParameters, saveProject: false));
				}
			}

			Assert.IsTrue (builder.RunTarget (proj, "Clean", doNotCleanupOnUpdate: true, parameters: renamedParameters, saveProject: false));
			Assert.IsFalse (File.Exists (fingerprint));
		}

		[TestMethod]
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
			Assert.Contains (
				"\"Microsoft.NETCore.App.Runtime.NativeAOT.android-arm\"",
				assets,
				"Restore should select the android-arm NativeAOT runtime pack."
			);
			Assert.DoesNotContain (
				"\"Microsoft.NETCore.App.Runtime.NativeAOT.linux-bionic-arm\"",
				assets,
				"Restore should not fall back to the linux-bionic-arm NativeAOT runtime pack."
			);
		}

		[TestMethod]
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

		[TestMethod]
		public void BuildNativeAot_WithoutNdk ()
		{
			var proj = new XamarinAndroidApplicationProject {
				IsRelease = true,
			};
			proj.SetRuntime (AndroidRuntime.NativeAOT);
			proj.SetRuntimeIdentifiers (["arm64-v8a"]);
			proj.SetProperty ("_SkipNdkResolution", "true");

			using var builder = CreateApkBuilder ();
			builder.ThrowOnBuildFailure = false;
			Assert.IsFalse (builder.Build (proj), "NativeAOT must require an NDK.");
			StringAssertEx.Contains ("error XA5104:", builder.LastBuildOutput, "A missing NDK should produce XA5104.");
		}

		[TestMethod]
		[DataRow ("apk", false)]
		[DataRow ("aab", true)]
		public void BuildCoreClrWithNativeLibraryStripping (string packageFormat, bool isRelease)
		{
			string fixture = GetInstalledRuntimePackFile ("libnet-android.debug.so");
			byte [] original = File.ReadAllBytes (fixture);
			byte [] wrapper = Encoding.UTF8.GetBytes ("#!/system/bin/sh\nexec \"$@\"\n");
			const string nativeLibrary = "native/arm64-v8a/libstrip-input.so";
			var proj = new XamarinAndroidApplicationProject {
				IsRelease = isRelease,
				OtherBuildItems = {
					new AndroidItem.AndroidNativeLibrary (nativeLibrary) {
						BinaryContent = () => original,
					},
					new AndroidItem.AndroidNativeLibrary ("arm64-wrap.sh") {
						BinaryContent = () => wrapper,
						MetadataValues = "Link=lib\\arm64-v8a\\wrap.sh",
					},
				},
			};
			proj.SetRuntime (AndroidRuntime.CoreCLR);
			proj.SetRuntimeIdentifiers (["arm64-v8a"]);
			proj.SetProperty ("AndroidPackageFormat", packageFormat);
			proj.SetProperty ("AndroidStripNativeLibraries", "true");
			proj.SetProperty ("AndroidIncludeWrapSh", "true");

			using var builder = CreateApkBuilder ();
			Assert.IsTrue (builder.Build (proj), $"The {proj.Configuration} {packageFormat} build should strip native libraries using the NDK.");

			string package = Path.Combine (Root, builder.ProjectDirectory, proj.OutputPath, $"{proj.PackageName}-Signed.{packageFormat}");
			string archivePath = $"{(packageFormat == "aab" ? "base/" : "")}lib/arm64-v8a/libstrip-input.so";
			byte [] stripped = ZipHelper.ReadFileFromZip (package, archivePath);
			Assert.IsNotNull (stripped, "The stripped native input should be packaged.");
			Assert.IsLessThan (original.Length, stripped.Length, "The packaged native library should be smaller after stripping.");
			CollectionAssert.AreEqual (wrapper, ZipHelper.ReadFileFromZip (package, $"{(packageFormat == "aab" ? "base/" : "")}lib/arm64-v8a/wrap.sh"),
				"Linked wrapper scripts must be packaged unchanged.");
			using (var stream = new MemoryStream (stripped)) {
				using IELF elf = ELFReader.Load (stream, shouldOwnStream: false);
				Assert.AreEqual (FileType.SharedObject, elf.Type);
				Assert.AreEqual (Machine.AArch64, elf.Machine);
				Assert.IsFalse (elf.Sections.Any (section => section.Type == SectionType.SymbolTable || section.Name == ".debug_info"));
				var symbols = (ISymbolTable)elf.GetSection (".dynsym");
				CollectionAssert.Contains (symbols.Entries.Select (symbol => symbol.Name).ToArray (), "JNI_OnLoad", "Stripping must preserve dynamic exports.");
			}
			string intermediateDirectory = Path.Combine (Root, builder.ProjectDirectory, proj.IntermediateOutputPath);
			string strippedLibrary = Path.Combine (intermediateDirectory, "android-arm64", "stripped", "libstrip-input.so");
			Assert.IsTrue (File.Exists (strippedLibrary));

			builder.BuildLogFile = "stripping-disabled.log";
			Assert.IsTrue (builder.Build (proj, doNotCleanupOnUpdate: true, saveProject: false, parameters: [
				"AndroidStripNativeLibraries=false",
				"_SkipNdkResolution=true",
			]), "Disabling stripping should not require the NDK.");
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
			Assert.IsFalse (File.Exists (strippedLibrary));
		}

		[TestMethod]
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
			Assert.IsTrue (File.Exists (runtimeArchive));

			string [] linkerResponseFiles = Directory.GetFiles (nativeDirectory, "ld.*.rsp");
			Assert.AreEqual (1, linkerResponseFiles.Length, "One native linker response file should be generated.");
			string linkerResponse = File.ReadAllText (linkerResponseFiles [0]);
			Assert.Contains ("libRuntime.WorkstationGC.arm-ehabi.a", linkerResponse);
			foreach (string archiveName in CPlusPlusArchiveNames) {
				Assert.DoesNotContain (archiveName, linkerResponse);
			}

			string llvmNm = Path.Combine (GetNdkToolchainDirectory (), "bin", TestEnvironment.IsWindows ? "llvm-nm.exe" : "llvm-nm");
			var (exitCode, standardOutput, standardError) = RunProcessWithExitCode (llvmNm, $"--defined-only \"{runtimeArchive}\"");
			Assert.AreEqual (0, exitCode, $"llvm-nm failed:{Environment.NewLine}{standardError}");
			foreach (string symbol in ArmEhabiPersonalitySymbols) {
				Assert.Contains ($" W {symbol}", standardOutput, $"{symbol} should be a weak global symbol.");
			}
		}

		[TestMethod]
		[DataRow (AndroidRuntime.NativeAOT, "android-arm")]
		[DataRow (AndroidRuntime.NativeAOT, "android-arm64")]
		[DataRow (AndroidRuntime.NativeAOT, "android-x64")]
		[DataRow (AndroidRuntime.CoreCLR, "android-arm")]
		[DataRow (AndroidRuntime.CoreCLR, "android-arm64")]
		[DataRow (AndroidRuntime.CoreCLR, "android-x64")]
		public void RuntimePackDoesNotContainCPlusPlusArchives (AndroidRuntime runtime, string rid)
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
				$"Microsoft.Android.Runtime.NativeAOT.{apiLevelName}.{rid}"
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

			string nativeOutputRoot = Path.Combine (outputDirectory, "runtime-output");
			if (runtime == AndroidRuntime.NativeAOT) {
				// CI test jobs have installed packs, but not the native build's output tree.
				string nativeOutputDirectory = Path.Combine (nativeOutputRoot, "nativeaot", rid);
				Directory.CreateDirectory (nativeOutputDirectory);
				foreach (string configuration in new [] { "debug", "release" }) {
					string archiveName = $"libnaot-android.{configuration}-static-{configuration}.a";
					string archive = Directory.GetFiles (installedRuntimePack, archiveName, SearchOption.AllDirectories).Single ();
					File.Copy (archive, Path.Combine (nativeOutputDirectory, archiveName));
				}
			}

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
					$"AndroidRID={rid}",
					$"_MonoAndroidNETOutputRoot={managedOutputRoot}{Path.DirectorySeparatorChar}",
					$"NativeRuntimeOutputRootDir={nativeOutputRoot}{Path.DirectorySeparatorChar}",
					$"BaseIntermediateOutputPath={Path.Combine (outputDirectory, "obj")}{Path.DirectorySeparatorChar}",
					$"PackageOutputPath={outputDirectory}",
				]),
				$"Packing the {runtime} runtime pack should succeed. See {dotnet.ProcessLogFile}."
			);

			string packagePath = Directory.GetFiles (outputDirectory, "*.nupkg")
				.Single (path => !path.EndsWith (".symbols.nupkg", StringComparison.OrdinalIgnoreCase));
			using var package = ZipHelper.OpenZip (packagePath);
			foreach (string archiveName in CPlusPlusArchiveNames) {
				string archivePath = $"runtimes/{rid}/native/{archiveName}";
				package.AssertDoesNotContainEntry (packagePath, archivePath);
			}
			if (runtime == AndroidRuntime.NativeAOT) {
				foreach (string configuration in new [] { "debug", "release" }) {
					package.AssertContainsEntry (packagePath, $"runtimes/{rid}/native/libnaot-android.{configuration}-static-{configuration}.a");
					package.AssertDoesNotContainEntry (packagePath, $"runtimes/{rid}/native/libnaot-android.{configuration}.so");
				}
			}
		}

		[TestMethod]
		[DataRow (AndroidRuntime.NativeAOT)]
		[DataRow (AndroidRuntime.CoreCLR)]
		public void CopyRuntimePackRemovesStaleNativeLibraries (AndroidRuntime runtime)
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
			string runtimeOutputPath = Path.Combine (outputDirectory, "runtime-output");
			var runtimeDirectories = new [] {
				(Rid: "android-arm", ClangArch: "arm"),
				(Rid: "android-arm64", ClangArch: "aarch64"),
				(Rid: "android-x64", ClangArch: "x86_64"),
			}.Select (entry => (
				entry.Rid,
				entry.ClangArch,
				Native: Path.Combine (packsRoot, $"Microsoft.Android.Runtime.{runtime}.{apiLevelName}.{entry.Rid}",
					packVersion, "runtimes", entry.Rid, "native"),
				Output: Path.Combine (runtimeOutputPath, entry.Rid)
			)).ToArray ();
			string [] obsoleteSharedLibraries = runtime == AndroidRuntime.NativeAOT ? [
				"libnaot-android.debug.so",
				"libnaot-android.release.so",
			] : [];
			string [] retainedLibraries = runtime == AndroidRuntime.NativeAOT ? [
				"libnaot-android.debug-static-debug.a",
				"libnaot-android.release-static-release.a",
			] : ["libnet-android.release.so"];
			foreach (var directory in runtimeDirectories) {
				Directory.CreateDirectory (directory.Native);
				Directory.CreateDirectory (directory.Output);
				foreach (string archiveName in CPlusPlusArchiveNames) {
					File.Create (Path.Combine (directory.Native, archiveName)).Dispose ();
				}
				foreach (string libraryName in obsoleteSharedLibraries) {
					File.Create (Path.Combine (directory.Output, libraryName)).Dispose ();
					File.Create (Path.Combine (directory.Native, libraryName)).Dispose ();
				}
				foreach (string libraryName in retainedLibraries) {
					File.WriteAllText (Path.Combine (directory.Output, libraryName), $"{directory.Rid}/{libraryName}");
				}
			}

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
						"AndroidSupportedTargetJitAbis=armeabi-v7a:arm64-v8a:x86_64",
						$"MicrosoftAndroidPacksRootDir={packsRoot}{Path.DirectorySeparatorChar}",
						$"OutputPath={runtimeOutputPath}{Path.DirectorySeparatorChar}",
					]
				),
				$"Copying the {runtime} runtime pack should succeed. See {dotnet.ProcessLogFile}."
			);

			foreach (var directory in runtimeDirectories) {
				foreach (string archiveName in CPlusPlusArchiveNames) {
					Assert.IsFalse (File.Exists (Path.Combine (directory.Native, archiveName)));
				}
				foreach (string libraryName in obsoleteSharedLibraries) {
					Assert.IsFalse (File.Exists (Path.Combine (directory.Output, libraryName)));
					Assert.IsFalse (File.Exists (Path.Combine (directory.Native, libraryName)));
				}
				foreach (string libraryName in retainedLibraries) {
					Assert.IsTrue (File.Exists (Path.Combine (directory.Output, libraryName)));
					Assert.IsTrue (File.Exists (Path.Combine (directory.Native, libraryName)));
					Assert.AreEqual ($"{directory.Rid}/{libraryName}", File.ReadAllText (Path.Combine (directory.Native, libraryName)));
				}
				Assert.IsTrue (File.Exists (Path.Combine (directory.Native, "libc.so")));
				Assert.IsTrue (File.Exists (Path.Combine (directory.Native, $"libclang_rt.builtins-{directory.ClangArch}-android.a")));
			}
		}

	}
}
