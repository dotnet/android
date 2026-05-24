using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Xamarin.Android.Tasks;
using Xamarin.Android.Tools;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests {
	[TestFixture]
	[Category ("Node-2")]
	public class TrimmableTypeMapBuildTests : BaseTest {

		[Test]
		public void Build_WithTrimmableTypeMap_Succeeds ([Values] bool isRelease, [Values (AndroidRuntime.CoreCLR, AndroidRuntime.NativeAOT)] AndroidRuntime runtime)
		{
			if (IgnoreUnsupportedConfiguration (runtime, release: isRelease)) {
				return;
			}

			var proj = new XamarinAndroidApplicationProject {
				IsRelease = isRelease,
			};
			proj.SetRuntime (runtime);
			proj.SetProperty ("_AndroidTypeMapImplementation", "trimmable");

			using var builder = CreateApkBuilder ();
			Assert.IsTrue (builder.Build (proj), "Build should have succeeded.");

			var intermediateDir = builder.Output.GetIntermediaryPath ("typemap");
			DirectoryAssert.Exists (intermediateDir);
		}

		[Test]
		public void Build_WithTrimmableTypeMap_IncrementalBuild ([Values] bool isRelease, [Values (AndroidRuntime.CoreCLR, AndroidRuntime.NativeAOT)] AndroidRuntime runtime)
		{
			if (IgnoreUnsupportedConfiguration (runtime, release: isRelease)) {
				return;
			}
			AssertCommercialBuild (); // Incremental build assertions require Fast Deployment

			var proj = new XamarinAndroidApplicationProject {
				IsRelease = isRelease,
			};
			proj.SetRuntime (runtime);
			proj.SetProperty ("_AndroidTypeMapImplementation", "trimmable");

			using var builder = CreateApkBuilder ();
			Assert.IsTrue (builder.Build (proj), "First build should have succeeded.");

			var intermediateDir = builder.Output.GetIntermediaryPath ("typemap");
			DirectoryAssert.Exists (intermediateDir);

			Assert.IsTrue (builder.Build (proj), "Second build should have succeeded.");

			Assert.IsTrue (
				builder.Output.IsTargetSkipped ("_GenerateJavaStubs"),
				"_GenerateJavaStubs should be skipped on incremental build.");
		}

		[Test]
		public void Build_WithTrimmableTypeMap_ArrayRankChangeRegeneratesTypeMap ()
		{
			if (IgnoreUnsupportedConfiguration (AndroidRuntime.CoreCLR, release: true)) {
				return;
			}

			var proj = new XamarinAndroidApplicationProject {
				IsRelease = true,
			};
			proj.SetRuntime (AndroidRuntime.CoreCLR);
			proj.SetProperty ("_AndroidTypeMapImplementation", "trimmable");
			proj.SetProperty ("_AndroidTrimmableTypeMapMaxArrayRank", "0");

			using var builder = CreateApkBuilder ();
			Assert.IsTrue (builder.Build (proj), "First build should have succeeded.");
			builder.Output.AssertTargetIsNotSkipped ("_GenerateTrimmableTypeMap");

			Assert.IsTrue (builder.Build (proj, doNotCleanupOnUpdate: true), "Second build should have succeeded.");
			builder.Output.AssertTargetIsSkipped ("_GenerateTrimmableTypeMap");

			proj.SetProperty ("_AndroidTrimmableTypeMapMaxArrayRank", "3");
			Assert.IsTrue (builder.Build (proj, doNotCleanupOnUpdate: true), "Array rank change build should have succeeded.");
			builder.Output.AssertTargetIsNotSkipped ("_GenerateTrimmableTypeMap");
		}

		[Test]
		public void Build_WithTrimmableTypeMap_DoesNotHitCopyIfChangedMismatch ([Values (AndroidRuntime.CoreCLR, AndroidRuntime.NativeAOT)] AndroidRuntime runtime)
		{
			var proj = new XamarinAndroidApplicationProject {
				IsRelease = true,
			};
			proj.SetRuntime (runtime);
			proj.SetProperty ("_AndroidTypeMapImplementation", "trimmable");

			using var builder = CreateApkBuilder ();
			Assert.IsTrue (builder.Build (proj), "Build should have succeeded.");

			Assert.IsFalse (
				StringAssertEx.ContainsText (builder.LastBuildOutput, "source and destination count mismatch"),
				$"{builder.BuildLogFile} should not fail with XACIC7004.");
			Assert.IsFalse (
				StringAssertEx.ContainsText (builder.LastBuildOutput, "Internal error: architecture"),
				$"{builder.BuildLogFile} should keep trimmable typemap assemblies aligned across ABIs.");
		}

		[Test]
		public void Build_WithTrimmableTypeMap_AssemblyStoreMappingsStayInRange ()
		{
			var proj = new XamarinAndroidApplicationProject {
				IsRelease = true,
			};
			proj.SetRuntime (AndroidRuntime.CoreCLR);
			proj.SetProperty ("_AndroidTypeMapImplementation", "trimmable");

			using var builder = CreateApkBuilder ();
			Assert.IsTrue (builder.Build (proj), "Build should have succeeded.");

			var environmentFiles = Directory.GetFiles (builder.Output.GetIntermediaryPath ("android"), "environment.*.ll");
			Assert.IsNotEmpty (environmentFiles, "Expected generated environment.<abi>.ll files.");

			foreach (var environmentFile in environmentFiles) {
				var abi = Path.GetFileNameWithoutExtension (environmentFile).Substring ("environment.".Length);
				var manifestFile = builder.Output.GetIntermediaryPath (Path.Combine ("app_shared_libraries", abi, "assembly-store.so.manifest"));

				if (!File.Exists (manifestFile)) {
					continue;
				}

				var environmentText = File.ReadAllText (environmentFile);
				var runtimeDataMatch = Regex.Match (environmentText, @"assembly_store_bundled_assemblies.*\[(\d+)\s+x");
				Assert.IsTrue (runtimeDataMatch.Success, $"{environmentFile} should declare assembly_store_bundled_assemblies.");

				var runtimeDataCount = int.Parse (runtimeDataMatch.Groups [1].Value);
				var maxMappingIndex = File.ReadLines (manifestFile)
					.Select (line => Regex.Match (line, @"\bmi:(\d+)\b"))
					.Where (match => match.Success)
					.Select (match => int.Parse (match.Groups [1].Value))
					.Max ();

				Assert.That (
					runtimeDataCount,
					Is.GreaterThan (maxMappingIndex),
					$"{Path.GetFileName (environmentFile)} should allocate enough runtime slots for {Path.GetFileName (manifestFile)}.");
			}
		}

		[Test]
		public void NativeAotTrimmableTypeMap_DoesNotExportFrameworkTypeMaps ()
		{
			if (IgnoreUnsupportedConfiguration (AndroidRuntime.NativeAOT, release: true)) {
				return;
			}

			var proj = new XamarinAndroidApplicationProject {
				IsRelease = true,
			};
			proj.SetRuntime (AndroidRuntime.NativeAOT);
			proj.SetProperty ("_AndroidTypeMapImplementation", "trimmable");

			using var builder = CreateApkBuilder ();
			Assert.IsTrue (builder.Build (proj), "Build should have succeeded.");

			var ridIntermediateDir = builder.Output.GetIntermediaryPath ("android-arm64");
			var rspFiles = Directory.GetFiles (ridIntermediateDir, "*.ilc.rsp", SearchOption.AllDirectories);
			Assert.IsNotEmpty (rspFiles, $"{ridIntermediateDir} should contain an ILC response file.");

			var rspText = File.ReadAllText (rspFiles [0]);
			StringAssert.Contains ("_Java.Interop.TypeMap.dll", rspText);
			StringAssert.Contains ("_Mono.Android.TypeMap.dll", rspText);
			StringAssert.DoesNotContain ("--generateunmanagedentrypoints:_Java.Interop.TypeMap", rspText);
			StringAssert.DoesNotContain ("--generateunmanagedentrypoints:_Mono.Android.TypeMap", rspText);
			StringAssert.Contains ($"--generateunmanagedentrypoints:_{proj.ProjectName}.TypeMap", rspText);
		}

		[Test]
		public void CoreClrTrimmableTypeMap_PackagesJavaProxyThrowable ()
		{
			if (IgnoreUnsupportedConfiguration (AndroidRuntime.CoreCLR, release: true)) {
				return;
			}

			var proj = new XamarinAndroidApplicationProject {
				IsRelease = true,
			};
			proj.SetRuntime (AndroidRuntime.CoreCLR);
			proj.SetProperty ("_AndroidTypeMapImplementation", "trimmable");

			using var builder = CreateApkBuilder ();
			Assert.IsTrue (builder.Build (proj), "Build should have succeeded.");

			var dexFile = builder.Output.GetIntermediaryPath (Path.Combine ("android", "bin", "classes.dex"));
			FileAssert.Exists (dexFile);
			Assert.IsTrue (
				DexUtils.ContainsClassWithMethod ("Landroid/runtime/JavaProxyThrowable;", "<init>", "(Ljava/lang/String;)V", dexFile, AndroidSdkPath),
				$"`{dexFile}` should include `android.runtime.JavaProxyThrowable`.");
		}

		[Test]
		public void CoreClrTrimmableTypeMap_PackagesReadyToRunTypeMap ()
		{
			if (IgnoreUnsupportedConfiguration (AndroidRuntime.CoreCLR, release: true)) {
				return;
			}

			var proj = new XamarinAndroidApplicationProject {
				IsRelease = true,
			};
			proj.SetRuntime (AndroidRuntime.CoreCLR);
			proj.SetProperty ("_AndroidTypeMapImplementation", "trimmable");
			proj.SetProperty ("RuntimeIdentifier", "android-arm64");
			proj.SetProperty ("AndroidEnableAssemblyCompression", "false");

			using var builder = CreateApkBuilder ();
			Assert.IsTrue (builder.Build (proj), "Build should have succeeded.");

			var r2rTypeMap = builder.Output.GetIntermediaryPath (Path.Combine ("android-arm64", "R2R", "_Microsoft.Android.TypeMaps.dll"));
			FileAssert.Exists (r2rTypeMap, "ReadyToRun should compile the generated TypeMap entry assembly.");
			using (var r2rStream = File.OpenRead (r2rTypeMap)) {
				using var r2rReader = new System.Reflection.PortableExecutable.PEReader (r2rStream);
				Assert.IsTrue (
					r2rReader.PEHeaders.CorHeader.ManagedNativeHeaderDirectory.Size > 0,
					"ReadyToRun output for _Microsoft.Android.TypeMaps.dll should have a managed native header.");
			}

			var apk = Path.Combine (Root, builder.ProjectDirectory, proj.OutputPath, "android-arm64", $"{proj.PackageName}-Signed.apk");
			FileAssert.Exists (apk);

			var helper = new ArchiveAssemblyHelper (apk, useAssemblyStores: true);
			var packagedTypeMapEntries = helper.ListArchiveContents ("lib/", arch: AndroidTargetArch.Arm64)
				.Where (entry => entry.StartsWith ("lib/arm64-v8a/lib__", StringComparison.Ordinal) &&
					entry.EndsWith (".dll.so", StringComparison.Ordinal) &&
					!entry.EndsWith (".ni.dll.so", StringComparison.Ordinal) &&
					entry.Contains ("TypeMap", StringComparison.Ordinal))
				.ToArray ();
			Assert.AreEqual (
				packagedTypeMapEntries.Distinct ().Count (),
				packagedTypeMapEntries.Length,
				"TypeMap assemblies should be packaged only once; do not include both linked IL and ReadyToRun copies.");
			Assert.AreEqual (
				1,
				packagedTypeMapEntries.Count (entry => entry == "lib/arm64-v8a/lib__Microsoft.Android.TypeMaps.dll.so"),
				"_Microsoft.Android.TypeMaps.dll should be packaged only once.");

			Assert.IsTrue (helper.Exists ("assemblies/arm64-v8a/_Microsoft.Android.TypeMaps.dll"), "_Microsoft.Android.TypeMaps.dll should exist in the APK.");
			using (var packagedTypeMap = helper.ReadEntry ("assemblies/arm64-v8a/_Microsoft.Android.TypeMaps.dll", AndroidTargetArch.Arm64)) {
				Assert.IsNotNull (packagedTypeMap, "_Microsoft.Android.TypeMaps.dll should be readable from the APK.");
				using var packagedReader = new System.Reflection.PortableExecutable.PEReader (packagedTypeMap);
				Assert.IsTrue (
					packagedReader.PEHeaders.CorHeader.ManagedNativeHeaderDirectory.Size > 0,
					"Packaged _Microsoft.Android.TypeMaps.dll should be the ReadyToRun image, not the linked IL image.");
			}
		}

		[Test]
		public void TrimmableTypeMap_PreserveLists_ArePackagedInSdk ()
		{
			foreach (var file in new [] {
				"Trimmable.CoreCLR.xml",
				"System.Private.CoreLib.xml",
			}) {
				var path = Path.Combine (TestEnvironment.DotNetPreviewAndroidSdkDirectory, "PreserveLists", file);
				FileAssert.Exists (path, $"{path} should exist in the SDK pack.");
			}
		}

		[Test]
		public void TrimmableTypeMap_RuntimeArtifacts_ArePackagedInSdk ()
		{
			var toolsDir = TestEnvironment.AndroidMSBuildDirectory;

			foreach (var file in new [] {
				"java_runtime.jar",
				"java_runtime.dex",
				"java_runtime_fastdev.jar",
				"java_runtime_fastdev.dex",
				"java_runtime_trimmable.jar",
				"java_runtime_trimmable.dex",
				"java_runtime_clr.jar",
				"java_runtime_clr.dex",
				"java_runtime_fastdev_clr.jar",
				"java_runtime_fastdev_clr.dex",
			}) {
				FileAssert.Exists (Path.Combine (toolsDir, file), $"{file} should exist in the SDK pack.");
			}

		}

		// T1: end-to-end build coverage for [Export] and [ExportField] under trimmable.
		// The trimmable typemap path emits a per-assembly typemap DLL and JCW Java
		// sources for user peer types. This test confirms that, for a project that
		// uses both [Export] (instance method) and [ExportField] (static getter),
		// the JCW Java file the build generates contains the expected `native`
		// method declaration AND a static field declaration referencing the field
		// initializer method. If either side regresses, the runtime would silently
		// fail to wire up the user's exports.
		[Test]
		public void Build_WithExportAndExportField_GeneratesJcwAndTypeMap ()
		{
			const AndroidRuntime runtime = AndroidRuntime.CoreCLR;

			var proj = new XamarinAndroidApplicationProject {
				IsRelease = false,
				References = {
					new BuildItem.Reference ("Mono.Android.Export"),
				},
			};
			proj.SetRuntime (runtime);
			proj.SetProperty ("_AndroidTypeMapImplementation", "trimmable");
			proj.Sources.Add (new BuildItem.Source ("ExportShapes.cs") {
				TextContent = () => @"using System;
using Java.Interop;

namespace UnnamedProject {
	class ExportShapes : Java.Lang.Object {
		[Export]
		public string EchoString (string x) => ""<"" + x + "">"";

		[ExportField (""FOO"")]
		public static int InitialFoo () => 42;
	}
}"
			});

			using var builder = CreateApkBuilder ();
			Assert.IsTrue (builder.Build (proj), "Build should have succeeded.");

			var javaDir = Path.Combine (builder.Output.GetIntermediaryPath ("typemap"), "java");
			DirectoryAssert.Exists (javaDir, "Trimmable JCW Java output directory should exist.");

			var allJavaFiles = Directory.GetFiles (javaDir, "*.java", SearchOption.AllDirectories);
			Assert.IsNotEmpty (allJavaFiles, "At least one JCW Java source file should be generated.");

			// The JCW Java file for ExportShapes lives under a crc64<hash>/ECDH directory
			// matching the CRC64 hash of the type. Search by content (one of the method
			// names that must appear in the generated source) rather than by filename
			// to avoid coupling to the hash.
			string? exportShapesJava = null;
			string? exportShapesText = null;
			foreach (var f in allJavaFiles) {
				var text = File.ReadAllText (f);
				if (text.Contains ("EchoString") && text.Contains ("InitialFoo")) {
					exportShapesJava = f;
					exportShapesText = text;
					break;
				}
			}
			Assert.IsNotNull (exportShapesJava,
				$"Could not find a generated JCW Java file referencing both EchoString and InitialFoo under {javaDir}.");
			Assert.IsNotNull (exportShapesText,
				$"Could not find a generated JCW Java file referencing both EchoString and InitialFoo under {javaDir}.");
			var javaText = exportShapesText;

			// [Export] EchoString — Java side must declare a `native` method matching
			// the C# signature (String -> String). The trimmable emitter generates
			// `public native` for instance [Export] methods.
			StringAssert.Contains ("native", javaText,
				"Generated JCW should contain a native method declaration for [Export].");
			StringAssert.Contains ("EchoString", javaText,
				"Generated JCW should contain the [Export] method name.");

			// [ExportField] FOO — Java side must declare a static field initialized
			// by calling the C# initializer method (`InitialFoo`). Without this,
			// the [ExportField] is silently dropped and Java callers see no FOO.
			StringAssert.Contains ("FOO", javaText,
				"Generated JCW should contain the [ExportField] declaration.");
			StringAssert.Contains ("InitialFoo", javaText,
				"Generated JCW should reference the [ExportField] initializer method.");

			// A per-assembly typemap DLL should be present (named after the app
			// assembly + .TypeMap suffix). We only check that *some* user typemap
			// assembly was produced — the exact name varies based on app assembly.
			var typemapDir = builder.Output.GetIntermediaryPath ("typemap");
			var typemapDlls = Directory.GetFiles (typemapDir, "*.TypeMap.dll");
			Assert.IsNotEmpty (typemapDlls, "Trimmable typemap should produce at least one *.TypeMap.dll.");
		}

		// T6: trim-warning baseline for [Export] under trimmable.
		// The trimmable [Export] code generator emits IL that reaches into
		// Mono.Android via [IgnoresAccessChecksTo] and dispatches through
		// member references built from System.Reflection.Metadata. If the
		// emitter starts producing reflection-style patterns that the trim
		// analyzer cannot track (e.g. missing [DynamicallyAccessedMembers] on
		// helper signatures), IL2xxx / IL3xxx warnings will appear pointing
		// at the generated `_<App>.TypeMap.dll` or at the user's [Export]
		// source. The baseline is: zero such warnings reference either of
		// those locations. This is a targeted assertion (not a full no-IL-warnings
		// guarantee), so unrelated framework warnings don't fail the test.
		[Test]
		public void Build_WithExport_ProducesNoTrimWarningsTargetingExportCodegen ()
		{
			const AndroidRuntime runtime = AndroidRuntime.CoreCLR;

			var proj = new XamarinAndroidApplicationProject {
				IsRelease = true,
				References = {
					new BuildItem.Reference ("Mono.Android.Export"),
				},
			};
			proj.SetRuntime (runtime);
			proj.SetProperty ("_AndroidTypeMapImplementation", "trimmable");
			proj.SetProperty ("TrimMode", "full");
			proj.SetProperty ("TrimmerSingleWarn", "false");
			proj.Sources.Add (new BuildItem.Source ("ExportShapes.cs") {
				TextContent = () => @"using System;
using Java.Interop;

namespace UnnamedProject {
	class ExportShapes : Java.Lang.Object {
		[Export]
		public string EchoString (string x) => ""<"" + x + "">"";

		[ExportField (""FOO"")]
		public static int InitialFoo () => 42;
	}
}"
			});

			using var builder = CreateApkBuilder ();
			Assert.IsTrue (builder.Build (proj), "Build should have succeeded.");

			// Match actual IL2xxx and IL3xxx warning lines (trim + AOT analysis), then
			// keep only those whose message text references either the generated
			// trimmable typemap assembly or the [Export] source file.
			// The regex requires ": warning IL" to avoid matching CSC command lines
			// that mention IL codes in /nowarn switches.
			// Exclude IL2026 about ExportAttribute/ExportFieldAttribute constructors
			// themselves — those are expected (the attributes carry [RequiresUnreferencedCode]).
			var ilWarningRegex = new Regex (@":\s*warning\s+(IL[23]\d{3})\b", RegexOptions.Compiled);
			var offending = new List<string> ();
			foreach (var line in builder.LastBuildOutput) {
				if (!ilWarningRegex.IsMatch (line)) {
					continue;
				}
				if (line.Contains ("ExportAttribute", StringComparison.Ordinal)
						&& line.Contains ("RequiresUnreferencedCode", StringComparison.Ordinal)) {
					continue;
				}
				bool mentionsTypeMap = line.Contains (".TypeMap.dll", StringComparison.OrdinalIgnoreCase)
					|| line.Contains ("_Microsoft.Android.TypeMaps", StringComparison.OrdinalIgnoreCase);
				bool mentionsExportSource = line.Contains ("ExportShapes.cs", StringComparison.OrdinalIgnoreCase);
				if (mentionsTypeMap || mentionsExportSource) {
					offending.Add (line.Trim ());
				}
			}

			Assert.IsEmpty (offending,
				"Trimmable [Export] codegen should not introduce IL2xxx / IL3xxx warnings against the generated typemap " +
				"assembly or the user's [Export] source. Offending warning lines:\n  " +
				string.Join ("\n  ", offending));
		}

		[Test]
		public void Build_WithTrimmableTypeMap_AbstractTypeWithProtectedCtor_Succeeds ()
		{
			if (IgnoreUnsupportedConfiguration (AndroidRuntime.NativeAOT, release: true)) {
				return;
			}

			var proj = new XamarinAndroidApplicationProject {
				IsRelease = true,
			};
			proj.SetRuntime (AndroidRuntime.NativeAOT);
			proj.SetProperty ("_AndroidTypeMapImplementation", "trimmable");
			proj.Sources.Add (new BuildItem.Source ("AbstractProvider.cs") {
				TextContent = () => @"
namespace UnnamedProject {
	public abstract class AbstractProvider : Java.Lang.Object {
		protected AbstractProvider (Android.Content.Context context) { }
		public abstract string GetData ();
	}

	public class ConcreteProvider : AbstractProvider {
		public ConcreteProvider (Android.Content.Context context) : base (context) { }
		public override string GetData () => ""hello"";
	}
}"
			});

			using var builder = CreateApkBuilder ();
			Assert.IsTrue (builder.Build (proj), "Build should have succeeded — abstract types with protected ctors should not cause XAGTT7009.");
		}
	}
}
