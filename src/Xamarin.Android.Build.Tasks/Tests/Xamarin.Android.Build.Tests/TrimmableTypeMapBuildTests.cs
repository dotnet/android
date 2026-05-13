using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Xamarin.Android.Tasks;
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
		public void TrimmableTypeMap_PreserveList_IsPackagedInSdk ()
		{
			var path = Path.Combine (TestEnvironment.DotNetPreviewAndroidSdkDirectory, "PreserveLists", "Trimmable.CoreCLR.xml");

			FileAssert.Exists (path, $"{path} should exist in the SDK pack.");
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

		// Tripwire: the trimmable test lane in `NUnitInstrumentation.cs` excludes a
		// set of Java.Interop-Tests by exact name (those tests live in the external
		// Java.Interop-Tests assembly and cannot use [Category("TrimmableIgnore")]).
		// Any new exclusion added here means we are silently degrading the trimmable
		// CoreCLR test signal. Fixes for excluded tests should *remove* entries here,
		// not add to them. If you genuinely must add one (after exhausting alternatives),
		// bump the constant in this test with a comment that links the tracking issue.
		// See https://github.com/dotnet/android/issues/11170 for the live inventory.
		[Test]
		public void TrimmableTypeMap_ExcludedTestNames_DoesNotGrow ()
		{
			const int CurrentMaximumExclusionCount = 4;

			var path = Path.Combine (
				XABuildPaths.TopDirectory,
				"tests", "Mono.Android-Tests", "Mono.Android-Tests",
				"Xamarin.Android.RuntimeTests", "NUnitInstrumentation.cs");
			FileAssert.Exists (path, $"{path} should exist (this test reads the source verbatim).");

			var source = File.ReadAllText (path);

			// Locate the `ExcludedTestNames = ...` initializer block. The trailing
			// `};` ends the array initializer.
			var startIdx = source.IndexOf ("ExcludedTestNames", System.StringComparison.Ordinal);
			Assert.AreNotEqual (-1, startIdx, "ExcludedTestNames assignment not found in NUnitInstrumentation.cs.");
			var endIdx = source.IndexOf ("};", startIdx, System.StringComparison.Ordinal);
			Assert.AreNotEqual (-1, endIdx, "Could not find end of ExcludedTestNames initializer.");

			var block = source.Substring (startIdx, endIdx - startIdx);

			// Each excluded test is a quoted string literal containing a '.'-delimited
			// fully qualified type/method name. Comments may contain dots too, so we
			// count only quoted strings that include at least one '.'.
			var matches = Regex.Matches (block, "\"([^\"\\n]*\\.[^\"\\n]*)\"");

			Assert.LessOrEqual (
				matches.Count,
				CurrentMaximumExclusionCount,
				$"Trimmable lane exclusion count grew from {CurrentMaximumExclusionCount} to {matches.Count}. " +
				$"Excluding more tests degrades the trimmable CoreCLR signal. Fix the underlying issue and remove " +
				$"the exclusion, or update the constant in this test with a comment explaining the regression.");
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
	}
}
