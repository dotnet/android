using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;

using ELFSharp.ELF;
using ELFSharp.ELF.Sections;
using Microsoft.Android.Tasks;
using Microsoft.Build.Utilities;
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
			string missingNdk = Path.Combine (Root, "missing-ndk-for-prebuilt-coreclr");
			DirectoryAssert.DoesNotExist (missingNdk);
			Assert.IsTrue (builder.Build (proj, parameters: ["AndroidNdkDirectory=" + missingNdk]),
				$"CoreCLR app build should succeed for {abi} without the Android NDK.");
			string packagePath = Path.Combine (Root, builder.ProjectDirectory, proj.OutputPath, proj.PackageName + "-Signed.apk");
			FileAssert.Exists (packagePath, "An NDK-free CoreCLR build must produce a signed package.");
			using var package = ZipHelper.OpenZip (packagePath);
			package.AssertContainsEntry (packagePath, $"lib/{abi}/libbinary_blobs.so");
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
			proj.SetProperty ("DebugSymbols", "false");

			using var builder = CreateApkBuilder ();
			Assert.IsTrue (builder.Build (proj), $"NativeAOT build should succeed for {abi} without libc++ or libunwind.");

			string intermediateDirectory = Path.Combine (Root, builder.ProjectDirectory, proj.IntermediateOutputPath);
			string [] responseFiles = Directory.GetFiles (intermediateDirectory, "ld.*.rsp", SearchOption.AllDirectories);
			Assert.IsNotEmpty (responseFiles, "Native linker response files should be generated.");
			foreach (string responseFile in responseFiles) {
				string response = File.ReadAllText (responseFile);
				StringAssert.Contains ("libnaot-android.release-static-release.a", response, responseFile);
				StringAssert.Contains ("nativeaot-bootstrap.o", response, responseFile);
				StringAssert.Contains ("-init=Host_NativeAOT_InitializeEnvironment", response, responseFile);
				StringAssert.DoesNotContain ("jni_init_funcs.", response, responseFile);
				StringAssert.DoesNotContain ("environment.", response, responseFile);
				foreach (string archiveName in CPlusPlusArchiveNames) {
					StringAssert.DoesNotContain (archiveName, response, responseFile);
				}
			}
			if (abi == "armeabi-v7a") {
				AssertArmEhabiSymbolsPromoted (builder, proj);
			}
			string nativeObject = Path.Combine (intermediateDirectory, MonoAndroidHelper.AbiToRid (abi), "native", proj.ProjectName + ".o");
			string llvmNm = Path.Combine (GetNdkToolchainDirectory (), "bin", TestEnvironment.IsWindows ? "llvm-nm.exe" : "llvm-nm");
			var (exitCode, standardOutput, standardError) = RunProcessWithExitCode (llvmNm, $"--undefined-only \"{nativeObject}\"");
			Assert.AreEqual (0, exitCode, $"llvm-nm failed:{Environment.NewLine}{standardError}");
			CollectionAssert.Contains (standardOutput.Split ('\n').Select (line => line.Trim ()), "U AndroidCryptoNative_InitLibraryOnLoad",
				"The managed JNI_OnLoad must retain a direct crypto initializer reference even without application crypto calls.");
			string applicationLibrary = Directory.GetFiles (
				Path.Combine (Root, builder.ProjectDirectory, proj.OutputPath), $"lib{proj.ProjectName}.so", SearchOption.AllDirectories).Single ();
			string dynamic = NativeToolTestHelper.Run ("llvm-readelf", "--dynamic", applicationLibrary);
			StringAssert.Contains ("(INIT)", dynamic, "The fixed environment hook must survive as DT_INIT.");
			StringAssert.Contains ("(INIT_ARRAY)", dynamic, "NativeAOT's initialization array must remain intact.");
			StringAssert.DoesNotContain ("libbinary_blobs.so", dynamic, "NativeAOT data must not introduce a dynamic library dependency.");
			using var elf = NativeToolTestHelper.ReadElf (applicationLibrary);
			var exports = elf.RootElement [0].GetProperty ("DynamicSymbols").EnumerateArray ()
				.Select (entry => entry.GetProperty ("Symbol").GetProperty ("Name").GetProperty ("Name").GetString ()).ToArray ();
			CollectionAssert.DoesNotContain (exports, "dotnet_android_nativeaot_bootstrap");
			CollectionAssert.DoesNotContain (exports, "dotnet_android_nativeaot_bootstrap_size");
			CollectionAssert.DoesNotContain (exports, "dotnet_android_nativeaot_remapping_data");
			CollectionAssert.DoesNotContain (exports, "dotnet_android_nativeaot_remapping_data_size");
			CollectionAssert.DoesNotContain (exports, "Host_NativeAOT_InitializeEnvironment");
			string definitions = NativeToolTestHelper.Run ("llvm-nm", "--demangle", "--defined-only",
				Path.ChangeExtension (applicationLibrary, ".dbg.so"));
			StringAssert.Contains ("BinaryBlobLoader::read_uncompressed(", definitions);
			StringAssert.DoesNotContain ("BinaryBlobLoader::decode(", definitions,
				"The NativeAOT host must not retain the CoreCLR blob decompression path.");
			StringAssert.DoesNotContain ("BinaryBlobLoader::load(", definitions);
			StringAssert.DoesNotContain ("BinaryBlobLoader::load_optional(", definitions);
		}

		[Test]
		public void BuildNativeAotAppBundleIncludesDebugSymbols ()
		{
			var proj = new XamarinAndroidApplicationProject {
				IsRelease = true,
			};
			proj.SetRuntime (AndroidRuntime.NativeAOT);
			string [] abis = ["arm64-v8a", "x86_64"];
			proj.SetRuntimeIdentifiers (abis);
			proj.SetProperty ("AndroidPackageFormat", "aab");
			proj.SetProperty ("DebugSymbols", "false");

			using var builder = CreateApkBuilder ();
			Assert.IsTrue (builder.Build (proj), "NativeAOT AAB build should succeed.");
			string bundle = Path.Combine (Root, builder.ProjectDirectory, proj.OutputPath, $"{proj.PackageName}-Signed.aab");
			FileAssert.Exists (bundle);
			string extractedDirectory = builder.Output.GetIntermediaryPath ("nativeaot-debug-symbols");
			Directory.CreateDirectory (extractedDirectory);
			using var archive = ZipHelper.OpenZip (bundle);
			foreach (string abi in abis) {
				string libraryEntryName = $"base/lib/{abi}/lib{proj.ProjectName}.so";
				string symbolsEntryName = $"BUNDLE-METADATA/com.android.tools.build.debugsymbols/{abi}/lib{proj.ProjectName}.so.dbg";
				string library = Path.Combine (extractedDirectory, $"{abi}.so");
				string symbols = Path.Combine (extractedDirectory, $"{abi}.dbg.so");
				ExtractEntry (libraryEntryName, library);
				ExtractEntry (symbolsEntryName, symbols);
				Assert.AreEqual (GetBuildId (library), GetBuildId (symbols),
					$"NativeAOT debug symbols for {abi} must match the bundled application library.");
			}

			void ExtractEntry (string entryName, string path)
			{
				var entry = archive.GetEntry (entryName);
				Assert.IsNotNull (entry, $"The bundle must contain '{entryName}'.");
				using var source = entry.Open ();
				using var destination = File.Create (path);
				source.CopyTo (destination);
			}

			static string GetBuildId (string path)
			{
				string output = NativeToolTestHelper.Run ("llvm-readelf", "--notes", path);
				string buildIdLine = output.Split ('\n').Single (line => line.Contains ("Build ID:", StringComparison.Ordinal));
				return buildIdLine.Substring (buildIdLine.IndexOf ("Build ID:", StringComparison.Ordinal) + "Build ID:".Length).Trim ();
			}
		}

		[Test]
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
			FileAssert.DoesNotExist (legacySource, "Upgrading an existing intermediate directory must remove obsolete Java environment data.");
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
			StringAssert.Contains ("NativeLibraryHelper.loadLibrary(\"BootstrapOriginal\", context);", File.ReadAllText (sourceFile));
			string nativeObject = Path.Combine (intermediate, "android-arm64", "native", "BootstrapOriginal.o");
			DateTime nativeObjectTimestamp = File.GetLastWriteTimeUtc (nativeObject);
			DateTime bootstrapSourceTimestamp = File.GetLastWriteTimeUtc (sourceFile);
			Assert.Less (File.GetLastWriteTimeUtc (inputs [3]), File.GetLastWriteTimeUtc (environmentObject),
				"The alternate environment file must predate the bootstrap outputs.");

			Assert.IsTrue (builder.Build (proj, doNotCleanupOnUpdate: true, saveProject: false, parameters: [
				"AssemblyName=BootstrapOriginal", "_BootstrapEnvironmentFlavor=second", "DefineConstants=BOOTSTRAP_SECOND",
			]));
			Assert.Greater (File.GetLastWriteTimeUtc (nativeObject), nativeObjectTimestamp, "The managed build flavor must recompile.");
			CollectionAssert.AreEqual (inputTimestamps, inputs.Select (File.GetLastWriteTimeUtc),
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
				CollectionAssert.AreEqual (inputTimestamps, inputs.Select (File.GetLastWriteTimeUtc),
					"Reordering pre-existing files must update override precedence without changing their timestamps.");
				builder.Output.AssertTargetIsNotSkipped ("_AndroidGenerateNativeAotBootstrapObject");
				builder.Output.AssertTargetIsSkipped ("_AndroidGenerateNativeAotBootstrapSources", defaultIfNotUsed: true);
				Assert.AreEqual (bootstrapSourceTimestamp, File.GetLastWriteTimeUtc (sourceFile), "Environment order must not regenerate Java.");
				Assert.AreEqual (nativeObjectTimestamp, File.GetLastWriteTimeUtc (nativeObject), "Environment order must not recompile managed ILC output.");
				builder.Output.AssertTargetIsSkipped ("_CompileToDalvik", defaultIfNotUsed: true);
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

			string deviceTarget = Environment.GetEnvironmentVariable ("ADB_TARGET");
			if (!string.IsNullOrEmpty (deviceTarget)) {
				string sourceManifest = Path.Combine (projectDirectory, "Properties", "AndroidManifest.xml");
				string packageName = (string)XDocument.Load (sourceManifest).Root?.Attribute ("package");
				Assert.AreEqual (proj.PackageName, packageName);
				TestContext.Out.WriteLine ($"Installing fixture package '{packageName}' from '{sourceManifest}' to '{deviceTarget}'.");
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
					StringAssert.Contains ("second:first", startupLog,
						"The renamed app and secondary-process provider must read the selected bootstrap environment.");
				} finally {
					Assert.IsTrue (builder.RunTarget (proj, "Uninstall", doNotCleanupOnUpdate: true, parameters: deviceParameters, saveProject: false));
				}
			}

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
			proj.SetRuntimeIdentifiers (["arm64-v8a"]);
			proj.SetProperty ("_SkipNdkResolution", "true");

			using var builder = CreateApkBuilder ();
			builder.ThrowOnBuildFailure = false;
			Assert.IsFalse (builder.Build (proj), "NativeAOT must require an NDK.");
			StringAssertEx.Contains ("error XA5104:", builder.LastBuildOutput, "A missing NDK should produce XA5104.");
		}

		[TestCase ("apk", false, "")]
		[TestCase ("aab", true, "")]
		[TestCase ("apk", false, "asan")]
		[TestCase ("aab", true, "ubsan")]
		public void BuildCoreClrWithNativeLibraryStripping (string packageFormat, bool isRelease, string checkedBuild)
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
			proj.SetProperty ("_AndroidCheckedBuild", checkedBuild);

			using var builder = CreateApkBuilder ();
			Assert.IsTrue (builder.Build (proj), $"The {proj.Configuration} {packageFormat} build should strip native libraries using the NDK.");

			string package = Path.Combine (Root, builder.ProjectDirectory, proj.OutputPath, $"{proj.PackageName}-Signed.{packageFormat}");
			string archivePath = $"{(packageFormat == "aab" ? "base/" : "")}lib/arm64-v8a/libstrip-input.so";
			byte [] stripped = ZipHelper.ReadFileFromZip (package, archivePath);
			Assert.IsNotNull (stripped, "The stripped native input should be packaged.");
			Assert.Less (stripped.Length, original.Length, "The packaged native library should be smaller after stripping.");
			CollectionAssert.AreEqual (wrapper, ZipHelper.ReadFileFromZip (package, $"{(packageFormat == "aab" ? "base/" : "")}lib/arm64-v8a/wrap.sh"),
				"Linked wrapper scripts must be packaged unchanged.");
			string sanitizerArchivePath = "";
			string sanitizerRuntimePath = "";
			if (checkedBuild != "") {
				var ndk = AndroidNdkTools.Create (AndroidNdkPath, new TaskLoggingHelper (new MockBuildEngine (TestContext.Out), TestName)) ??
					throw new InvalidOperationException ("The configured NDK must be available.");
				string runtimeDirectory = ndk.GetClangDeviceLibraryPath () ??
					throw new InvalidOperationException ("Clang runtime libraries must be available.");
				string sanitizerLibrary = $"libclang_rt.{(checkedBuild == "asan" ? "asan" : "ubsan_standalone")}-aarch64-android.so";
				sanitizerArchivePath = Path.Combine (packageFormat == "aab" ? "base" : "", "lib", "arm64-v8a", sanitizerLibrary)
					.Replace (Path.DirectorySeparatorChar, '/');
				sanitizerRuntimePath = Path.Combine (runtimeDirectory, sanitizerLibrary);
				Assert.IsNotNull (ZipHelper.ReadFileFromZip (package, sanitizerArchivePath), "Checked builds must package the official NDK sanitizer runtime.");
			}
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

			builder.BuildLogFile = "stripping-disabled.log";
			Assert.IsTrue (builder.Build (proj, doNotCleanupOnUpdate: true, saveProject: false, parameters: [
				"AndroidStripNativeLibraries=false",
				"_SkipNdkResolution=" + (checkedBuild == "" ? "true" : "false"),
			]), "Disabling stripping should only retain the NDK requirement for checked builds.");
			CollectionAssert.AreEqual (original, ZipHelper.ReadFileFromZip (package, archivePath), "Toggling stripping off must repackage the original.");
			if (checkedBuild != "") {
				CollectionAssert.AreEqual (File.ReadAllBytes (sanitizerRuntimePath), ZipHelper.ReadFileFromZip (package, sanitizerArchivePath),
					"Checked builds must package the resolved runtime unchanged when stripping is disabled.");
			}

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
