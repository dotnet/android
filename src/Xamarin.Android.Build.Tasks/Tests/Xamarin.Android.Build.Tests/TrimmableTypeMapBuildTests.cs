using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Mono.Cecil;
using NUnit.Framework;
using Xamarin.Android.AssemblyStore;
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
			proj.SetProperty ("AndroidTypeMapImplementation", "trimmable");

			using var builder = CreateApkBuilder ();
			Assert.IsTrue (builder.Build (proj), "Build should have succeeded.");

			var intermediateDir = builder.Output.GetIntermediaryPath ("typemap");
			AssertTrimmableTypeMapOutputs (intermediateDir);
		}

		[TestCase ("llvm-ir", AndroidRuntime.CoreCLR, "parameters", "XA4205")]
		[TestCase ("trimmable", AndroidRuntime.CoreCLR, "parameters", "XA4205")]
		[TestCase ("trimmable", AndroidRuntime.NativeAOT, "parameters", "XA4205")]
		[TestCase ("llvm-ir", AndroidRuntime.CoreCLR, "void", "XA4208")]
		[TestCase ("trimmable", AndroidRuntime.CoreCLR, "void", "XA4208")]
		[TestCase ("trimmable", AndroidRuntime.NativeAOT, "void", "XA4208")]
		[TestCase ("llvm-ir", AndroidRuntime.CoreCLR, "generic", "XA4207")]
		[TestCase ("trimmable", AndroidRuntime.CoreCLR, "generic", "XA4207")]
		[TestCase ("trimmable", AndroidRuntime.NativeAOT, "generic", "XA4207")]
		[TestCase ("llvm-ir", AndroidRuntime.CoreCLR, "parameters-and-void", "XA4205")]
		[TestCase ("trimmable", AndroidRuntime.CoreCLR, "parameters-and-void", "XA4205")]
		[TestCase ("trimmable", AndroidRuntime.NativeAOT, "parameters-and-void", "XA4205")]
		[TestCase ("llvm-ir", AndroidRuntime.CoreCLR, "generic-parameters-and-void", "XA4207")]
		[TestCase ("trimmable", AndroidRuntime.CoreCLR, "generic-parameters-and-void", "XA4207")]
		[TestCase ("trimmable", AndroidRuntime.NativeAOT, "generic-parameters-and-void", "XA4207")]
		public void Build_InvalidExportField_ReportsLegacyDiagnostic (
			string typeMapImplementation,
			AndroidRuntime runtime,
			string invalidShape,
			string expectedCode)
		{
			bool isRelease = runtime == AndroidRuntime.NativeAOT;
			if (IgnoreUnsupportedConfiguration (runtime, release: isRelease)) {
				return;
			}

			var initializer = invalidShape switch {
				"parameters" => "public int InitialValue (int value) => value;",
				"void" => "public void InitialValue () { }",
				"generic" => "public int InitialValue () => 42;",
				"parameters-and-void" => "public void InitialValue (int value) { }",
				"generic-parameters-and-void" => "public void InitialValue (int value) { }",
				_ => throw new InvalidOperationException ($"Unknown invalid [ExportField] shape '{invalidShape}'."),
			};
			var proj = CreateExportFieldValidationProject (runtime, typeMapImplementation, $"""
						[ExportField ("VALUE")]
						{initializer}
				""", genericType: invalidShape.StartsWith ("generic", StringComparison.Ordinal));

			using var builder = CreateApkBuilder ();
			builder.ThrowOnBuildFailure = false;
			Assert.IsFalse (builder.Build (proj), $"{runtime}/{typeMapImplementation} should reject {invalidShape} [ExportField] initializers.");
			StringAssertEx.Contains ($"error {expectedCode}", builder.LastBuildOutput, $"The build should report {expectedCode}.");
			if (invalidShape == "parameters-and-void") {
				Assert.IsFalse (
					builder.LastBuildOutput.Any (line => line.Contains ("error XA4208", StringComparison.Ordinal)),
					"XA4205 should take precedence over XA4208, matching LLVM-IR."
				);
			} else if (invalidShape == "generic-parameters-and-void") {
				Assert.IsFalse (
					builder.LastBuildOutput.Any (line =>
						line.Contains ("error XA4205", StringComparison.Ordinal) ||
						line.Contains ("error XA4208", StringComparison.Ordinal)),
					"XA4207 should take precedence over initializer signature diagnostics, matching LLVM-IR."
				);
			}
		}

		static XamarinAndroidApplicationProject CreateExportFieldValidationProject (
			AndroidRuntime runtime,
			string typeMapImplementation,
			string members,
			bool genericType = false)
		{
			var typeParameters = genericType ? "<T>" : "";
			var proj = new XamarinAndroidApplicationProject {
				IsRelease = runtime == AndroidRuntime.NativeAOT,
				References = {
					new BuildItem.Reference ("Mono.Android.Export"),
				},
			};
			proj.SetRuntime (runtime);
			proj.SetProperty ("AndroidTypeMapImplementation", typeMapImplementation);
			proj.Sources.Add (new BuildItem.Source ("ExportFieldValidation.cs") {
				TextContent = () => $$"""
					using Android.Runtime;
					using Java.Interop;

					namespace ExportFieldValidation {
						[Register ("com/example/exportfields/ValidationPeer")]
						class ValidationPeer{{typeParameters}} : Java.Lang.Object {
							public ValidationPeer () {
							}

					{{members}}
						}
					}
					""",
			});
			return proj;
		}

		[Test]
		public void Build_PublishAotProject_UsesTrimmableTypeMapForCoreClrDebug ()
		{
			if (IgnoreUnsupportedConfiguration (AndroidRuntime.CoreCLR, release: false)) {
				return;
			}

			var proj = new XamarinAndroidApplicationProject {
				IsRelease = false,
			};
			proj.SetRuntime (AndroidRuntime.CoreCLR);
			proj.SetProperty (KnownProperties.PublishAot, "true");

			using var builder = CreateApkBuilder ();
			Assert.IsTrue (builder.Build (proj), "Build should have succeeded.");

			var intermediateDir = builder.Output.GetIntermediaryPath ("typemap");
			AssertTrimmableTypeMapOutputs (intermediateDir);
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
			proj.SetProperty ("AndroidTypeMapImplementation", "trimmable");
			bool trimNativeAotJavaCode = isRelease && runtime == AndroidRuntime.NativeAOT;

			using var builder = CreateApkBuilder ();
			Assert.IsTrue (builder.Build (proj), "First build should have succeeded.");

			var intermediateDir = builder.Output.GetIntermediaryPath ("typemap");
			AssertTrimmableTypeMapOutputs (intermediateDir);
			var typemapDlls = Directory.GetFiles (intermediateDir, "*.dll");
			Assert.IsNotEmpty (typemapDlls, "First build should have generated typemap DLL(s).");

			string scanDgml = "";
			DateTime scanDgmlTimestamp = default;
			if (trimNativeAotJavaCode) {
				var ridIntermediateDir = builder.Output.GetIntermediaryPath ("android-arm64");
				scanDgml = Path.Combine (ridIntermediateDir, "native", $"{proj.ProjectName}.scan.dgml.xml");
				var codegenDgml = Path.Combine (ridIntermediateDir, "native", $"{proj.ProjectName}.codegen.dgml.xml");
				FileAssert.Exists (scanDgml);
				FileAssert.DoesNotExist (codegenDgml, "Optimized builds should emit only the scan DGML needed for Java trimming.");
				scanDgmlTimestamp = File.GetLastWriteTimeUtc (scanDgml);
			}

			Assert.IsTrue (builder.Build (proj), "Second build should have succeeded.");

			Assert.IsTrue (
				builder.Output.IsTargetSkipped ("_GenerateJavaStubs"),
				"_GenerateJavaStubs should be skipped on incremental build.");
			if (trimNativeAotJavaCode) {
				builder.Output.AssertTargetIsSkipped ("_GenerateTrimmableTypeMapProguardConfiguration");
				Assert.AreEqual (scanDgmlTimestamp, File.GetLastWriteTimeUtc (scanDgml), "No-op builds should not rewrite the scan DGML.");
			}
			if (isRelease && runtime == AndroidRuntime.CoreCLR) {
				builder.Output.AssertTargetIsSkipped ("_RemoveRegisterAttributeCoreClr");
			}
			if (isRelease && runtime == AndroidRuntime.NativeAOT) {
				builder.Output.AssertTargetIsNotSkipped ("_RemoveRegisterAttributeNativeAot");
			}
			foreach (var typemapDll in typemapDlls) {
				FileAssert.Exists (typemapDll, $"No-op builds should preserve generated typemap assembly {typemapDll} when _GenerateTrimmableTypeMap is skipped.");
			}
		}

		[Test]
		public void Build_WithR8JniNameRewriting_IsIncremental ([Values (AndroidRuntime.CoreCLR, AndroidRuntime.NativeAOT)] AndroidRuntime runtime)
		{
			const string originalJavaName = "com/example/R8JniPeer";
			const string changedJavaName = "com/example/R8JniPeerChanged";
			const string userJavaName = "com.example.UserJavaType";
			if (IgnoreUnsupportedConfiguration (runtime, release: true)) {
				return;
			}

			string javaName = originalJavaName;
			var peerSource = new BuildItem.Source ("R8JniPeer.cs") {
				TextContent = () => $$"""
using Android.Runtime;

namespace UnnamedProject;

[Register ("{{javaName}}")]
public class R8JniPeer : Java.Lang.Object
{
	public R8JniPeer () { }
}
""",
			};
			var proj = new XamarinAndroidApplicationProject {
				IsRelease = true,
				Sources = {
					peerSource,
				},
			};
			if (runtime == AndroidRuntime.CoreCLR) {
				proj.LinkTool = "r8";
			}
			proj.AndroidJavaSources.Add (new BuildItem (AndroidBuildActions.AndroidJavaSource, "UserJavaType.java") {
				TextContent = () => "package com.example; public class UserJavaType { }",
				Encoding = Encoding.ASCII,
				Metadata = { { "Bind", "False" } },
			});
			proj.SetRuntime (runtime);
			proj.SetProperty (KnownProperties.RuntimeIdentifier, "android-arm64");
			proj.SetProperty ("AndroidPackageFormat", "aab");
			proj.SetProperty ("AndroidTypeMapImplementation", "trimmable");
			proj.SetProperty ("AndroidEnableR8JniNameObfuscation", "true");
			proj.SetProperty ("AndroidCreateProguardMappingFile", "false");
			proj.Imports.Add (CreateR8JniManifestMergerInputsAssertionImport ());

			using var builder = CreateApkBuilder (Path.Combine ("temp", $"R8JniNameRewriting_{runtime}_{Guid.NewGuid ():N}"));
			Assert.IsTrue (builder.Build (proj), "Clean R8 JNI name-rewriting build should have succeeded.");

			var projectDirectory = Path.Combine (Root, builder.ProjectDirectory);
			var seedMapping = FindSingleFile (projectDirectory, "mapping.txt", path => path.Contains ("r8-jni-seed", StringComparison.Ordinal));
			var finalMapping = FindSingleFile (projectDirectory, "r8-jni-final-mapping.txt");
			var rewriteManifest = FindSingleFile (projectDirectory, "r8-jni-rewrite-manifest.txt");
			var reachabilityManifest = FindSingleFile (projectDirectory, "r8-jni-reachability-manifest.txt");
			var rewrittenAssemblies = Directory.GetFiles (projectDirectory, "*.dll", SearchOption.AllDirectories)
				.Where (path => path.Contains ("r8-jni-rewritten", StringComparison.Ordinal))
				.ToArray ();

			Assert.IsNotEmpty (rewrittenAssemblies, "The clean build should rewrite managed assemblies before trimming or AOT.");
			foreach (var rewrittenAssembly in rewrittenAssemblies) {
				var hashDirectory = Path.GetFileName (Path.GetDirectoryName (rewrittenAssembly));
				Assert.That (hashDirectory, Does.Match ("^[0-9a-fA-F]{16}$"), $"Rewritten assembly staging should include a source-path hash: {rewrittenAssembly}");
			}
			AssertR8MappingRenamesClass (seedMapping, originalJavaName);
			AssertR8MappingKeepsClassName (seedMapping, $"{proj.PackageName}/MainActivity");
			AssertR8MappingKeepsClassName (finalMapping, $"{proj.PackageName}/MainActivity");
			StringAssert.DoesNotContain ($"{userJavaName} ->", File.ReadAllText (seedMapping), "User Java sources are kept by final R8 and should not receive seed-only names.");
			StringAssert.Contains ($"C\t{originalJavaName}", File.ReadAllText (rewriteManifest));
			Assert.That (new FileInfo (reachabilityManifest).Length, Is.GreaterThan (0), "The clean build should record post-link JNI reachability.");
			Assert.That (new FileInfo (finalMapping).Length, Is.GreaterThan (0), "The final R8 pass should emit its applied mapping.");
			AssertR8MappingRenamesClass (finalMapping, originalJavaName);
			Assert.That (Directory.GetFiles (Path.Combine (projectDirectory, proj.OutputPath), "mapping.txt", SearchOption.AllDirectories), Is.Empty,
				"Disabling public mapping output should not write mapping.txt under bin.");
			var appBundle = FindSingleFile (Path.Combine (projectDirectory, proj.OutputPath), $"{proj.PackageName}-Signed.aab");
			using (var archive = Xamarin.Tools.Zip.ZipArchive.Open (appBundle, FileMode.Open)) {
				Assert.IsFalse (archive.Any (entry => entry.FullName == "BUNDLE-METADATA/com.android.tools.build.obfuscation/proguard.map"),
					"Disabling public mapping output should omit the ProGuard mapping from app-bundle metadata.");
			}

			IEnumerable<string> outputFiles = rewrittenAssemblies
				.Append (seedMapping)
				.Append (rewriteManifest)
				.Append (reachabilityManifest)
				.Append (finalMapping);
			string? ilcRspFile = null;
			string? ilcRspContent = null;
			if (runtime == AndroidRuntime.NativeAOT) {
				ilcRspFile = FindSingleFile (projectDirectory, $"{proj.ProjectName}.ilc.rsp");
				ilcRspContent = File.ReadAllText (ilcRspFile);
				StringAssert.Contains ("r8-jni-rewritten", ilcRspContent, "ILC should compile the rewritten managed inputs.");
				outputFiles = outputFiles.Append (ilcRspFile);
			}
			var outputTimestamps = outputFiles.ToDictionary (path => path, File.GetLastWriteTimeUtc, StringComparer.Ordinal);

			Assert.IsTrue (builder.Build (proj, doNotCleanupOnUpdate: true, saveProject: false), "No-op R8 JNI name-rewriting build should have succeeded.");
			builder.Output.AssertTargetIsSkipped ("_AndroidCompileR8JniSeedJava");
			builder.Output.AssertTargetIsSkipped ("_AndroidGenerateR8JniSeedMapping");
			builder.Output.AssertTargetIsSkipped (runtime == AndroidRuntime.CoreCLR
				? "_AndroidRewriteJniNamesBeforeILLink"
				: "_AndroidRewriteJniNamesBeforeIlc");
			if (runtime == AndroidRuntime.NativeAOT) {
				builder.Output.AssertTargetIsNotSkipped ("WriteIlcRspFileForCompilation");
				FileAssert.Exists (ilcRspFile);
				string actualIlcRspContent = File.ReadAllText (ilcRspFile);
				StringAssert.Contains ("r8-jni-rewritten", actualIlcRspContent, "ILC response-file recomputation should retain rewritten managed inputs when the rewrite target is skipped.");
				Assert.AreEqual (ilcRspContent, actualIlcRspContent, "ILC response-file recomputation should preserve rewritten managed inputs when the rewrite target is skipped.");
				Assert.AreEqual (outputTimestamps [ilcRspFile], File.GetLastWriteTimeUtc (ilcRspFile), "An unchanged ILC response file should preserve its timestamp.");
			}
			builder.Output.AssertTargetIsSkipped ("_CompileToDalvik");
			foreach (var pair in outputTimestamps) {
				Assert.AreEqual (pair.Value, File.GetLastWriteTimeUtc (pair.Key), $"No-op build should preserve {pair.Key}.");
			}

			System.Threading.Thread.Sleep (1100);
			File.AppendAllText (reachabilityManifest, Environment.NewLine);
			Assert.IsTrue (builder.Build (proj, doNotCleanupOnUpdate: true, saveProject: false), "Reachability-manifest change rebuild should have succeeded.");
			builder.Output.AssertTargetIsNotSkipped ("_CompileToDalvik");

			System.Threading.Thread.Sleep (1100);
			File.AppendAllText (seedMapping, Environment.NewLine);
			Assert.IsTrue (builder.Build (proj, doNotCleanupOnUpdate: true, saveProject: false), "Seed-mapping change rebuild should have succeeded.");
			builder.Output.AssertTargetIsNotSkipped ("_CompileToDalvik");

			var missingRewrittenAssembly = rewrittenAssemblies [0];
			File.Delete (missingRewrittenAssembly);
			Assert.IsTrue (builder.Build (proj, doNotCleanupOnUpdate: true, saveProject: false), "Missing rewritten assembly rebuild should have succeeded.");
			builder.Output.AssertTargetIsNotSkipped (runtime == AndroidRuntime.CoreCLR
				? "_AndroidRewriteJniNamesBeforeILLink"
				: "_AndroidRewriteJniNamesBeforeIlc");
			FileAssert.Exists (missingRewrittenAssembly, "The managed rewrite target should recover a missing output.");

			javaName = changedJavaName;
			proj.Touch ("R8JniPeer.cs");
			Assert.IsTrue (builder.Build (proj, doNotCleanupOnUpdate: true, saveProject: false), "JNI-name change rebuild should have succeeded.");
			builder.Output.AssertTargetIsNotSkipped ("_AndroidCompileR8JniSeedJava");
			builder.Output.AssertTargetIsNotSkipped ("_AndroidGenerateR8JniSeedMapping");
			builder.Output.AssertTargetIsNotSkipped (runtime == AndroidRuntime.CoreCLR
				? "_AndroidRewriteJniNamesBeforeILLink"
				: "_AndroidRewriteJniNamesBeforeIlc");
			builder.Output.AssertTargetIsNotSkipped ("_CompileToDalvik");
			StringAssert.DoesNotContain ($"{originalJavaName.Replace ('/', '.')} ->", File.ReadAllText (seedMapping));
			StringAssert.Contains ($"{changedJavaName.Replace ('/', '.')} ->", File.ReadAllText (seedMapping));
			AssertR8MappingRenamesClass (seedMapping, changedJavaName);
			AssertR8MappingKeepsClassName (seedMapping, $"{proj.PackageName}/MainActivity");
			AssertR8MappingRenamesClass (finalMapping, changedJavaName);
			StringAssert.Contains ($"C\t{changedJavaName}", File.ReadAllText (rewriteManifest));

			proj.SetProperty ("AndroidEnableR8JniNameObfuscation", "false");
			Assert.IsTrue (builder.Build (proj, doNotCleanupOnUpdate: true), "Disabling R8 JNI name rewriting should invalidate the existing build outputs.");
			builder.Output.AssertTargetIsNotSkipped ("_CleanIntermediateIfNeeded");

			proj.SetProperty ("AndroidEnableR8JniNameObfuscation", "true");
			Assert.IsTrue (builder.Build (proj, doNotCleanupOnUpdate: true), "Re-enabling R8 JNI name rewriting should regenerate the pipeline outputs.");
			builder.Output.AssertTargetIsNotSkipped ("_AndroidGenerateR8JniSeedMapping");
			FileAssert.Exists (seedMapping);
		}

		[Test]
		public void Build_WithR8JniNameRewriting_SupportsMultipleRuntimeIdentifiers ([Values (AndroidRuntime.CoreCLR, AndroidRuntime.NativeAOT)] AndroidRuntime runtime)
		{
			const string javaName = "com/example/R8JniMultiAbiPeer";
			if (IgnoreUnsupportedConfiguration (runtime, release: true)) {
				return;
			}

			var proj = new XamarinAndroidApplicationProject {
				IsRelease = true,
				LinkTool = "r8",
				Sources = {
					new BuildItem.Source ("R8JniMultiAbiPeer.cs") {
						TextContent = () => $$"""
using Android.Runtime;

namespace UnnamedProject;

[Register ("{{javaName}}")]
public class R8JniMultiAbiPeer : Java.Lang.Object
{
	public R8JniMultiAbiPeer () { }
}
""",
					},
				},
			};
			proj.SetRuntime (runtime);
			proj.SetRuntimeIdentifiers (AndroidTargetArch.Arm64, AndroidTargetArch.X86_64);
			proj.SetProperty ("AndroidPackageFormat", "apk");
			proj.SetProperty ("AndroidTypeMapImplementation", "trimmable");
			proj.SetProperty ("AndroidEnableR8JniNameObfuscation", "true");
			proj.Imports.Add (CreateR8JniManifestMergerInputsAssertionImport ());

			using var builder = CreateApkBuilder (Path.Combine ("temp", $"R8JniNameRewritingMultiAbi_{runtime}_{Guid.NewGuid ():N}"));
			Assert.IsTrue (builder.Build (proj), "Multi-ABI R8 JNI name-rewriting build should have succeeded.");

			var projectDirectory = Path.Combine (Root, builder.ProjectDirectory);
			var seedMapping = FindSingleFile (projectDirectory, "mapping.txt", path => path.Contains ("r8-jni-seed", StringComparison.Ordinal));
			var rewriteManifests = Directory.GetFiles (projectDirectory, "r8-jni-rewrite-manifest.txt", SearchOption.AllDirectories);
			var reachabilityManifests = Directory.GetFiles (projectDirectory, "r8-jni-reachability-manifest.txt", SearchOption.AllDirectories);
			var rewrittenAssemblies = Directory.GetFiles (projectDirectory, "*.dll", SearchOption.AllDirectories)
				.Where (path => path.Contains ("r8-jni-rewritten", StringComparison.Ordinal))
				.ToArray ();

			StringAssert.Contains ($"{javaName.Replace ('/', '.')} ->", File.ReadAllText (seedMapping));
			AssertR8MappingKeepsClassName (seedMapping, $"{proj.PackageName}/MainActivity");
			foreach (var runtimeIdentifier in new [] { "android-arm64", "android-x64" }) {
				Assert.That (rewrittenAssemblies, Has.Some.Contains (runtimeIdentifier), $"The {runtimeIdentifier} inner build should have isolated rewritten assemblies.");
				Assert.That (rewriteManifests, Has.Some.Contains (runtimeIdentifier), $"The {runtimeIdentifier} inner build should have a rewrite manifest.");
			}
			Assert.That (reachabilityManifests, Has.Exactly (1).Contains ("android-arm64"), "Final R8 should consume the shared first-RID reachability manifest.");
			var firstRuntimeIdentifierReachabilityManifest = reachabilityManifests.Single (path => path.Contains ("android-arm64", StringComparison.Ordinal));
			Assert.That (new FileInfo (firstRuntimeIdentifierReachabilityManifest).Length, Is.GreaterThan (0), "The shared reachability manifest should contain retained JNI entries.");
			foreach (var manifest in rewriteManifests) {
				StringAssert.Contains ($"C\t{javaName}", File.ReadAllText (manifest));
			}
		}

		[Test]
		public void Build_WithR8JniNameRewriting_SupportsProjectReferencesAndCustomRules ([Values (AndroidRuntime.CoreCLR, AndroidRuntime.NativeAOT)] AndroidRuntime runtime)
		{
			const string libraryJavaName = "com/example/R8JniLibraryPeer";
			if (IgnoreUnsupportedConfiguration (runtime, release: true)) {
				return;
			}

			string proguardRule = "-dontwarn com.example.UnusedOne";
			var library = new XamarinAndroidLibraryProject {
				IsRelease = true,
				ProjectName = "R8JniLibrary",
				Sources = {
					new BuildItem.Source ("R8JniLibraryPeer.cs") {
						TextContent = () => $$"""
using Android.Runtime;

namespace R8JniLibrary;

[Register ("{{libraryJavaName}}")]
public class R8JniLibraryPeer : Java.Lang.Object
{
	public R8JniLibraryPeer () { }
}
""",
					},
				},
			};
			library.SetRuntime (runtime);
			library.OtherBuildItems.Add (new AndroidItem.ProguardConfiguration ("proguard.txt") {
				TextContent = () => "-dontwarn com.example.library.**",
			});

			var app = new XamarinAndroidApplicationProject {
				IsRelease = true,
				LinkTool = "r8",
			};
			app.SetRuntime (runtime);
			app.SetProperty (KnownProperties.RuntimeIdentifier, "android-arm64");
			app.SetProperty ("AndroidPackageFormat", "apk");
			app.SetProperty ("AndroidTypeMapImplementation", "trimmable");
			app.SetProperty ("AndroidEnableR8JniNameObfuscation", "true");
			app.References.Add (new BuildItem.ProjectReference (Path.Combine ("..", library.ProjectName, $"{library.ProjectName}.csproj"), library.ProjectName, library.ProjectGuid));
			app.OtherBuildItems.Add (new AndroidItem.ProguardConfiguration ("r8-jni-rules.pro") {
				TextContent = () => proguardRule,
			});
			app.OtherBuildItems.Add (new AndroidItem.ProguardConfiguration ("generated-acw-keep.cfg") {
				TextContent = () => $"-keep class {libraryJavaName.Replace ('/', '.')} {{ *; }}",
				Metadata = {
					{ "AndroidGeneratedProguardConfiguration", "true" },
				},
			});
			app.AndroidJavaSources.Add (new BuildItem (AndroidBuildActions.AndroidJavaSource, "R8JniLayoutView.java") {
				TextContent = () => """
					package com.example;
					public class R8JniLayoutView extends android.view.View {
						public R8JniLayoutView (android.content.Context context, android.util.AttributeSet attrs) {
							super (context, attrs);
						}
					}
					""",
				Encoding = Encoding.ASCII,
				Metadata = { { "Bind", "False" } },
			});
			app.AndroidResources.Add (new AndroidItem.AndroidResource ("Resources\\layout\\r8_jni_layout_view.axml") {
				TextContent = () => """
					<?xml version="1.0" encoding="utf-8"?>
					<com.example.R8JniLayoutView xmlns:android="http://schemas.android.com/apk/res/android"
						android:layout_width="match_parent"
						android:layout_height="match_parent" />
					""",
			});
			app.Imports.Add (new Import ("CaptureR8JniSeedConfiguration.targets") {
				TextContent = () => """
					<Project>
					  <Target Name="_CaptureR8JniSeedConfiguration"
					      AfterTargets="_AndroidGenerateR8JniSeedMappingInputs"
					      BeforeTargets="_AndroidGenerateR8JniSeedMapping">
					    <MakeDir Directories="$(_AndroidR8JniSeedDirectory)" />
					    <WriteLinesToFile
					        File="$(_AndroidR8JniSeedDirectory)configuration-items.txt"
					        Lines="@(_AndroidR8JniSeedProguardConfiguration->'%(Identity)|%(AndroidGeneratedProguardConfiguration)|%(AndroidAaptProguardConfiguration)|%(AndroidManifestProguardConfiguration)')"
					        Overwrite="true"
					        WriteOnlyWhenDifferent="true" />
					  </Target>
					  <Target Name="_CaptureR8JniFinalConfiguration"
					      AfterTargets="_CalculateProguardConfigurationFiles"
					      BeforeTargets="_CompileToDalvik">
					    <WriteLinesToFile
					        File="$(_AndroidR8JniSeedDirectory)final-configuration-items.txt"
					        Lines="@(_ProguardConfiguration->'%(Identity)|%(AndroidSdkBaselineProguardConfiguration)|%(AndroidGeneratedProguardConfiguration)|%(AndroidAaptProguardConfiguration)|%(AndroidR8JniMappedProguardConfiguration)')"
					        Overwrite="true"
					        WriteOnlyWhenDifferent="true" />
					  </Target>
					</Project>
					""",
			});

			string testDirectory = Path.Combine ("temp", $"R8JniNameRewritingReferences_{runtime}_{Guid.NewGuid ():N}");
			using var libraryBuilder = CreateDllBuilder (Path.Combine (testDirectory, library.ProjectName));
			using var appBuilder = CreateApkBuilder (Path.Combine (testDirectory, "App"));
			Assert.IsTrue (libraryBuilder.Build (library), "Referenced library build should have succeeded.");
			Assert.IsTrue (appBuilder.Build (app), "R8 JNI name-rewriting build with a project reference and custom rules should have succeeded.");

			var projectDirectory = Path.Combine (Root, appBuilder.ProjectDirectory);
			var seedMapping = FindSingleFile (projectDirectory, "mapping.txt", path => path.Contains ("r8-jni-seed", StringComparison.Ordinal));
			var finalMapping = FindSingleFile (projectDirectory, "mapping.txt", path => !path.Contains ("r8-jni-seed", StringComparison.Ordinal));
			var rewriteManifest = FindSingleFile (projectDirectory, "r8-jni-rewrite-manifest.txt");
			var seedConfigurationItems = File.ReadAllLines (FindSingleFile (projectDirectory, "configuration-items.txt"));
			var seedConfigurationPaths = seedConfigurationItems.Select (item => item.Split ('|') [0]).ToArray ();
			var finalConfigurationItems = File.ReadAllLines (FindSingleFile (projectDirectory, "final-configuration-items.txt"));
			var finalAaptConfiguration = finalConfigurationItems
				.Select (item => item.Split ('|'))
				.Single (metadata => metadata.Length == 5 && metadata [2] == "true" && metadata [3] == "true");
			var manifestRules = FindSingleFile (projectDirectory, "manifest_rules.txt");
			var finalAaptRules = Path.IsPathRooted (finalAaptConfiguration [0])
				? finalAaptConfiguration [0]
				: Path.Combine (projectDirectory, finalAaptConfiguration [0]);
			var mappedProjectRules = FindSingleFile (projectDirectory, "proguard_project_references.cfg");
			var primaryRules = FindSingleFile (projectDirectory, "proguard_project_primary.cfg");
			FileAssert.Exists (finalAaptRules, "The final configured AAPT rules should exist.");
			AssertR8MappingRenamesClass (seedMapping, libraryJavaName);
			AssertR8MappingRenamesClass (finalMapping, libraryJavaName);
			AssertR8MappingContainsMember (finalMapping, libraryJavaName, "nctor_0");
			AssertR8MappingContainsMember (finalMapping, $"{app.PackageName}/MainActivity", "n_OnCreate_Landroid_os_Bundle_");
			StringAssert.Contains ($"C\t{libraryJavaName}", File.ReadAllText (rewriteManifest));
			Assert.That (seedConfigurationPaths, Has.Some.EndsWith ("r8-jni-rules.pro"), "Seed R8 should receive user-authored rules.");
			Assert.That (seedConfigurationPaths, Has.Some.EndsWith ("proguard.txt"), "Seed R8 should receive AAR consumer rules.");
			Assert.That (seedConfigurationItems, Has.Some.EndsWith ("manifest_rules.txt|true||true"),
				"Seed R8 should receive manifest-only keep rules with explicit generated and manifest provenance.");
			StringAssert.Contains ($"-keep class {app.PackageName}.MainActivity", File.ReadAllText (manifestRules));
			StringAssert.DoesNotContain ("com.example.R8JniLayoutView", File.ReadAllText (manifestRules),
				"Seed manifest rules must not include resource custom views.");
			StringAssert.Contains ("-keep class com.example.R8JniLayoutView", File.ReadAllText (finalAaptRules),
				"Final AAPT rules should retain resource custom-view rules.");
			StringAssert.Contains ("#Auto Generated file", File.ReadAllText (finalAaptRules),
				"The final configured AAPT file should wrap the generated manifest and resource rules.");
			Assert.IsFalse (seedConfigurationPaths.Any (path =>
				new [] { "proguard-android.txt", "proguard_xamarin.cfg", "proguard_project_references.cfg", "proguard_project_primary.cfg", "generated-acw-keep.cfg" }.Contains (Path.GetFileName (path), StringComparer.Ordinal)),
				"Seed R8 should not receive generated or baseline configurations that pin managed peers.");
			Assert.That (finalConfigurationItems, Has.Some.EndsWith ("proguard-android.txt|true|||"),
				"Final R8 should identify only the SDK baseline by explicit provenance metadata.");
			Assert.That (finalConfigurationItems, Has.Some.EndsWith ("generated-acw-keep.cfg||true||"),
				"Generated ACW keep rules should carry semantic provenance so R8 can exclude them.");
			Assert.That (finalConfigurationItems, Has.Some.EndsWith ("aapt_rules.txt||true|true|"),
				"Final AAPT rules should carry specific provenance so R8 retains them.");
			Assert.That (finalConfigurationItems, Has.Some.EndsWith ("proguard_project_references.cfg||true||true"),
				"Mapped linked-assembly rules should carry specific provenance so R8 retains them.");
			StringAssert.Contains ($"-keep,allowobfuscation class {libraryJavaName.Replace ('/', '.')}", File.ReadAllText (mappedProjectRules));
			StringAssert.Contains ("-keepclassmembers,allowobfuscation", File.ReadAllText (mappedProjectRules));
			Assert.That (
				File.ReadAllLines (primaryRules).Where (line => line.StartsWith ("-keep class ", StringComparison.Ordinal)),
				Is.EqualTo (new [] { "-keep class com.example.R8JniLayoutView { *; }" }),
				"The final primary configuration must pin only user-authored Java without a managed peer.");

			proguardRule = "-dontwarn com.example.UnusedTwo";
			app.Touch ("r8-jni-rules.pro");
			Assert.IsTrue (appBuilder.Build (app, doNotCleanupOnUpdate: true, saveProject: false), "Custom-rule incremental build should have succeeded.");
			appBuilder.Output.AssertTargetIsNotSkipped ("_AndroidGenerateR8JniSeedMapping");
			appBuilder.Output.AssertTargetIsNotSkipped (runtime == AndroidRuntime.CoreCLR
				? "_AndroidRewriteJniNamesBeforeILLink"
				: "_AndroidRewriteJniNamesBeforeIlc");
			appBuilder.Output.AssertTargetIsNotSkipped ("_CompileToDalvik");
		}

		[Test]
		public void R8MappingMemberAssertion_AllowsSourceFileMetadata ()
		{
			var mappingFile = Path.GetTempFileName ();
			try {
				File.WriteAllText (mappingFile, """
					com.example.R8JniLibraryPeer -> f:
					# {"id":"sourceFile","fileName":"R8JniLibraryPeer.java"}
					    void nctor_0() -> a
					""");

				AssertR8MappingContainsMember (mappingFile, "com/example/R8JniLibraryPeer", "nctor_0");
			} finally {
				File.Delete (mappingFile);
			}
		}

		[Test]
		public void Build_WithTrimmableTypeMap_MissingJavaListPreservesGeneratedJava ()
		{
			if (IgnoreUnsupportedConfiguration (AndroidRuntime.CoreCLR, release: false)) {
				return;
			}

			var proj = new XamarinAndroidApplicationProject ();
			proj.SetRuntime (AndroidRuntime.CoreCLR);
			proj.SetProperty ("AndroidTypeMapImplementation", "trimmable");

			using var builder = CreateApkBuilder ();
			Assert.IsTrue (builder.Build (proj), "First build should have succeeded.");

			var typemapDirectory = builder.Output.GetIntermediaryPath ("typemap");
			var javaDirectory = Path.Combine (typemapDirectory, "java");
			var javaFiles = Directory.GetFiles (javaDirectory, "*.java", SearchOption.AllDirectories);
			var javaFilesList = Path.Combine (typemapDirectory, "java-files.txt");
			Assert.IsNotEmpty (javaFiles, "First build should have generated pre-trim Java sources.");
			FileAssert.Exists (javaFilesList, "First build should have persisted the pre-trim Java file list.");
			foreach (var path in File.ReadAllLines (javaFilesList)) {
				Assert.IsTrue (Path.IsPathFullyQualified (path), $"The persisted Java path should be fully qualified: {path}");
			}
			File.Delete (javaFilesList);

			Assert.IsTrue (builder.Build (proj, doNotCleanupOnUpdate: true, saveProject: false), "No-op build should have succeeded.");
			builder.Output.AssertTargetIsSkipped ("_GenerateTrimmableTypeMap");
			foreach (var javaFile in javaFiles) {
				FileAssert.Exists (javaFile, $"IncrementalClean should preserve {javaFile} when upgrading an obj directory without java-files.txt.");
			}
		}

		[Test]
		public void Build_WithTrimmableTypeMap_PublishTrimmed_MissingLinkedJavaListRegenerates ()
		{
			if (IgnoreUnsupportedConfiguration (AndroidRuntime.CoreCLR, release: true)) {
				return;
			}

			var proj = new XamarinAndroidApplicationProject {
				IsRelease = true,
			};
			proj.SetRuntime (AndroidRuntime.CoreCLR);
			proj.SetProperty ("AndroidTypeMapImplementation", "trimmable");

			using var builder = CreateApkBuilder ();
			Assert.IsTrue (builder.Build (proj), "First build should have succeeded.");

			var typemapDirectory = builder.Output.GetIntermediaryPath ("typemap");
			var linkedJavaDirectory = Path.Combine (typemapDirectory, "linked-java");
			var linkedJavaFiles = Directory.GetFiles (linkedJavaDirectory, "*.java", SearchOption.AllDirectories);
			var linkedJavaFilesList = Path.Combine (typemapDirectory, "linked-java-files.txt");
			Assert.IsNotEmpty (linkedJavaFiles, "First build should have generated post-trim Java sources.");
			FileAssert.Exists (linkedJavaFilesList, "First build should have persisted the post-trim Java file list.");
			foreach (var path in File.ReadAllLines (linkedJavaFilesList)) {
				Assert.IsTrue (Path.IsPathFullyQualified (path), $"The persisted linked Java path should be fully qualified: {path}");
			}
			File.Delete (linkedJavaFilesList);

			Assert.IsTrue (builder.Build (proj, doNotCleanupOnUpdate: true, saveProject: false), "Migration rebuild should have succeeded.");
			builder.Output.AssertTargetIsNotSkipped ("_GeneratePostTrimTrimmableTypeMapJavaSources");
			builder.Output.AssertTargetIsSkipped ("_CompileJava");
			FileAssert.Exists (linkedJavaFilesList, "Post-trim generation should recreate linked-java-files.txt.");
			foreach (var javaFile in linkedJavaFiles) {
				FileAssert.Exists (javaFile, $"IncrementalClean should preserve {javaFile} until post-trim generation recreates its list.");
			}
		}

		[Test]
		public void Build_WithTrimmableTypeMap_KeepsNativeAotRuntimeHostAcws ()
		{
			const bool isRelease = true;
			if (IgnoreUnsupportedConfiguration (AndroidRuntime.NativeAOT, release: isRelease)) {
				return;
			}

			var proj = new XamarinAndroidApplicationProject {
				IsRelease = isRelease,
				LinkTool = "r8",
			};
			proj.SetRuntime (AndroidRuntime.NativeAOT);
			proj.SetProperty ("AndroidTypeMapImplementation", "trimmable");

			using var builder = CreateApkBuilder ();
			Assert.IsTrue (builder.Build (proj), "Build should have succeeded.");

			var dexFile = builder.Output.GetIntermediaryPath (Path.Combine ("android", "bin", "classes.dex"));
			FileAssert.Exists (dexFile);

			// Regression test: the NativeAOT runtime host assembly (Microsoft.Android.Runtime.NativeAOT) is
			// resolved only in the per-RID inner build, so the RID-independent outer-build trimmable typemap
			// generator never scanned it. Its only Java Callable Wrapper type, UncaughtExceptionMarshaler,
			// therefore had no JCW and no typemap entry -> the runtime ACW is absent from classes.dex and the
			// app crashes at startup in JavaInteropRuntime.init (setDefaultUncaughtExceptionHandler). A
			// reference assembly for the host is now shipped in the SDK pack and fed to the generator. The JCW
			// name is CRC-hashed (e.g. `scrc64...UncaughtExceptionMarshaler`), so match on the type name suffix.
			Assert.IsTrue (DexUtils.ContainsClass ("UncaughtExceptionMarshaler;", dexFile, AndroidSdkPath),
				$"`{dexFile}` should include the UncaughtExceptionMarshaler runtime ACW.");
		}

		[Test]
		public void Build_WithTrimmableTypeMap_DeletesStaleGeneratedJavaSources ()
		{
			if (IgnoreUnsupportedConfiguration (AndroidRuntime.CoreCLR, release: false)) {
				return;
			}

			var proj = new XamarinAndroidApplicationProject ();
			proj.SetRuntime (AndroidRuntime.CoreCLR);
			proj.SetProperty ("AndroidTypeMapImplementation", "trimmable");

			using var builder = CreateApkBuilder ();
			Assert.IsTrue (builder.Build (proj), "First build should have succeeded.");

			var staleRelativePath = Path.Combine ("crc64stale", "Old.java");
			var staleClassPath = Path.Combine ("crc64stale", "Old.class");
			var staleGeneratedJava = builder.Output.GetIntermediaryPath (Path.Combine ("typemap", "java", staleRelativePath));
			var staleCompiledClass = builder.Output.GetIntermediaryPath (Path.Combine ("android", "bin", "classes", staleClassPath));
			var staleGeneratedJavaDirectory = Path.GetDirectoryName (staleGeneratedJava);
			var staleCompiledClassDirectory = Path.GetDirectoryName (staleCompiledClass);
			if (staleGeneratedJavaDirectory is null || staleCompiledClassDirectory is null) {
				throw new InvalidOperationException ("Could not determine stale Java output directories.");
			}
			Directory.CreateDirectory (staleGeneratedJavaDirectory);
			Directory.CreateDirectory (staleCompiledClassDirectory);
			File.WriteAllText (staleGeneratedJava, "package crc64stale; public class Old {}");
			File.WriteAllBytes (staleCompiledClass, []);

			proj.MainActivity += Environment.NewLine + "// Force trimmable typemap regeneration.";
			proj.Touch ("MainActivity.cs");
			Assert.IsTrue (builder.Build (proj, doNotCleanupOnUpdate: true, saveProject: false), "Second build should have succeeded.");
			builder.Output.AssertTargetIsNotSkipped ("_GenerateTrimmableTypeMap");
			builder.Output.AssertTargetIsNotSkipped ("_CompileJava");

			FileAssert.DoesNotExist (staleGeneratedJava, "Regenerated trimmable typemap should delete stale Java sources.");
			FileAssert.DoesNotExist (staleCompiledClass, "Deleting stale generated Java sources should force Java recompilation and remove stale class outputs.");
		}

		[Test]
		public void Build_WithTrimmableTypeMap_RecompilesUpdatedGeneratedJavaSources ()
		{
			if (IgnoreUnsupportedConfiguration (AndroidRuntime.CoreCLR, release: false)) {
				return;
			}

			var proj = new XamarinAndroidApplicationProject ();
			proj.SetRuntime (AndroidRuntime.CoreCLR);
			proj.SetProperty ("AndroidTypeMapImplementation", "trimmable");

			using var builder = CreateApkBuilder ();
			Assert.IsTrue (builder.Build (proj), "First build should have succeeded.");

			var generatedJavaDirectory = builder.Output.GetIntermediaryPath (Path.Combine ("typemap", "java"));
			var generatedJavaFiles = Directory.GetFiles (generatedJavaDirectory, "*.java", SearchOption.AllDirectories);
			Assert.IsNotEmpty (generatedJavaFiles, "Test setup should have generated trimmable typemap Java sources.");

			var generatedJava = generatedJavaFiles [0];
			var typeMapStamp = builder.Output.GetIntermediaryPath (Path.Combine ("typemap", "_GenerateTrimmableTypeMap.stamp"));
			var javaStubsStamp = builder.Output.GetIntermediaryPath (Path.Combine ("stamp", "_GenerateJavaStubs.stamp"));
			FileAssert.Exists (typeMapStamp, "First build should have written the trimmable typemap output stamp.");
			FileAssert.Exists (javaStubsStamp, "First build should have written the Java stubs output stamp.");

			var updatedJava = File.ReadAllText (generatedJava) + "\n// Force generated Java recompilation regression.\n";
			File.WriteAllText (generatedJava, updatedJava);
			var stampTime = DateTime.UtcNow;
			File.SetLastWriteTimeUtc (typeMapStamp, stampTime);
			File.SetLastWriteTimeUtc (javaStubsStamp, stampTime.AddSeconds (-5));

			Assert.IsTrue (builder.Build (proj, doNotCleanupOnUpdate: true), "Second build should have succeeded.");
			builder.Output.AssertTargetIsNotSkipped ("_GenerateJavaStubs");
			builder.Output.AssertTargetIsNotSkipped ("_CompileJava");
			Assert.AreEqual (updatedJava, File.ReadAllText (generatedJava), "Updated generated Java sources should be compiled in place from the generator output directory even when typemap assemblies do not change.");

			var relativePath = Path.GetRelativePath (generatedJavaDirectory, generatedJava);
			var compiledClass = builder.Output.GetIntermediaryPath (Path.Combine ("android", "bin", "classes", Path.ChangeExtension (relativePath, ".class")));
			FileAssert.Exists (compiledClass, "Updated generated Java sources should be compiled in place into android/bin/classes.");
		}

		// The JCWs that actually get compiled and packaged are the generated Java sources compiled
		// in place from their output directory (no longer copied into $(IntermediateOutputPath)android/src).
		// For CoreCLR + PublishTrimmed those come from the post-trim `typemap/linked-java` directory,
		// which `_GeneratePostTrimTrimmableTypeMapJavaSources` (re)generates from the linked assemblies
		// and `_CompileJava` compiles via the Javac AdditionalStubSourceDirectories parameter.
		static void AssertLinkedJavaCompiledInPlace (ProjectBuilder builder, string message)
		{
			var linkedJavaDirectory = builder.Output.GetIntermediaryPath (Path.Combine ("typemap", "linked-java"));
			DirectoryAssert.Exists (linkedJavaDirectory, $"{message}: post-trim linked-java directory should exist.");

			var linkedJavaFiles = Directory.GetFiles (linkedJavaDirectory, "*.java", SearchOption.AllDirectories);
			Assert.IsNotEmpty (linkedJavaFiles, $"{message}: post-trim build should have generated linked-java JCWs.");

			var androidSrcDirectory = builder.Output.GetIntermediaryPath (Path.Combine ("android", "src"));
			var classesDirectory = builder.Output.GetIntermediaryPath (Path.Combine ("android", "bin", "classes"));
			foreach (var linkedJava in linkedJavaFiles) {
				var relativePath = Path.GetRelativePath (linkedJavaDirectory, linkedJava);
				var copiedJava = Path.Combine (androidSrcDirectory, relativePath);
				FileAssert.DoesNotExist (copiedJava, $"{message}: linked-java JCW '{relativePath}' should be compiled in place, not copied to android/src.");

				var compiledClass = Path.Combine (classesDirectory, Path.ChangeExtension (relativePath, ".class"));
				FileAssert.Exists (compiledClass, $"{message}: linked-java JCW '{relativePath}' should be compiled in place into android/bin/classes.");
			}
		}

		[Test]
		public void Build_WithTrimmableTypeMap_PublishTrimmed_CompilesLinkedJavaInPlace ()
		{
			if (IgnoreUnsupportedConfiguration (AndroidRuntime.CoreCLR, release: true)) {
				return;
			}

			var proj = new XamarinAndroidApplicationProject {
				IsRelease = true,
			};
			proj.SetRuntime (AndroidRuntime.CoreCLR);
			proj.SetProperty ("AndroidTypeMapImplementation", "trimmable");

			using var builder = CreateApkBuilder ();
			Assert.IsTrue (builder.Build (proj), "First build should have succeeded.");
			AssertLinkedJavaCompiledInPlace (builder, "After first build");

			// A no-op rebuild must not start copying linked-java into android/src, even though
			// the post-trim Java generation may run again.
			Assert.IsTrue (builder.Build (proj, doNotCleanupOnUpdate: true, saveProject: false), "No-op rebuild should have succeeded.");
			AssertLinkedJavaCompiledInPlace (builder, "After no-op rebuild");
		}

		[Test]
		public void Build_WithTrimmableTypeMap_PublishTrimmed_DeletesStaleLinkedJavaWhenLinkedJavaShrinks ()
		{
			if (IgnoreUnsupportedConfiguration (AndroidRuntime.CoreCLR, release: true)) {
				return;
			}

			var proj = new XamarinAndroidApplicationProject {
				IsRelease = true,
			};
			proj.SetRuntime (AndroidRuntime.CoreCLR);
			proj.SetProperty ("AndroidTypeMapImplementation", "trimmable");

			using var builder = CreateApkBuilder ();
			Assert.IsTrue (builder.Build (proj), "First build should have succeeded.");

			// Simulate a JCW the post-trim pass produced on a previous build but no longer
			// produces (e.g. its type was trimmed away). It lives in the post-trim
			// `linked-java` source directory (compiled in place), with a compiled .class.
			var staleRelativePath = Path.Combine ("crc64stale", "Old.java");
			var staleClassPath = Path.Combine ("crc64stale", "Old.class");
			var staleLinkedJava = builder.Output.GetIntermediaryPath (Path.Combine ("typemap", "linked-java", staleRelativePath));
			var staleCompiledClass = builder.Output.GetIntermediaryPath (Path.Combine ("android", "bin", "classes", staleClassPath));
			var staleLinkedJavaDirectory = Path.GetDirectoryName (staleLinkedJava);
			var staleCompiledClassDirectory = Path.GetDirectoryName (staleCompiledClass);
			if (staleLinkedJavaDirectory is null || staleCompiledClassDirectory is null) {
				throw new InvalidOperationException ("Could not determine stale Java output directories.");
			}
			Directory.CreateDirectory (staleLinkedJavaDirectory);
			Directory.CreateDirectory (staleCompiledClassDirectory);
			File.WriteAllText (staleLinkedJava, "package crc64stale; public class Old {}");
			File.WriteAllBytes (staleCompiledClass, []);

			// Force the post-trim Java generation to re-run by removing its stamp (without a source
			// change, which would trigger an unrelated incremental CrossGen rebuild). It updates
			// linked-java in place (dropping the stale JCW); the JCWs are compiled in place, so
			// busting the compile stamp must drop the stale .class too.
			var postTrimStamp = builder.Output.GetIntermediaryPath (Path.Combine ("stamp", "_GeneratePostTrimTrimmableTypeMapJavaSources.stamp"));
			FileAssert.Exists (postTrimStamp, "First build should have written the post-trim Java stamp.");
			File.Delete (postTrimStamp);

			Assert.IsTrue (builder.Build (proj, doNotCleanupOnUpdate: true, saveProject: false), "Second build should have succeeded.");
			builder.Output.AssertTargetIsNotSkipped ("_GeneratePostTrimTrimmableTypeMapJavaSources");
			builder.Output.AssertTargetIsNotSkipped ("_CompileJava");

			FileAssert.DoesNotExist (staleLinkedJava, "Post-trim regeneration should drop the stale linked-java JCW.");
			FileAssert.DoesNotExist (staleCompiledClass, "Dropping the stale linked-java JCW should force Java recompilation and remove the stale class output.");
		}

		[Test]
		public void Build_WithTrimmableTypeMap_PublishTrimmed_PostTrimJavaGenerationIsIncremental ()
		{
			if (IgnoreUnsupportedConfiguration (AndroidRuntime.CoreCLR, release: true)) {
				return;
			}

			var proj = new XamarinAndroidApplicationProject {
				IsRelease = true,
			};
			proj.SetRuntime (AndroidRuntime.CoreCLR);
			proj.SetProperty ("AndroidTypeMapImplementation", "trimmable");

			using var builder = CreateApkBuilder ();
			Assert.IsTrue (builder.Build (proj), "First build should have succeeded.");

			// A no-op rebuild should not regenerate the post-trim JCWs or recompile them. If
			// _GeneratePostTrimTrimmableTypeMapJavaSources runs on every build, the JCWs that feed
			// _GenerateJavaStubs are rewritten each time, which both wastes work and means
			// _GenerateJavaStubs must re-run to stay consistent (otherwise the compiled JCWs go stale).
			Assert.IsTrue (builder.Build (proj, doNotCleanupOnUpdate: true, saveProject: false), "No-op rebuild should have succeeded.");
			builder.Output.AssertTargetIsSkipped ("_GeneratePostTrimTrimmableTypeMapJavaSources");
			builder.Output.AssertTargetIsSkipped ("_GenerateJavaStubs");

			var acwMap = builder.Output.GetIntermediaryPath ("acw-map.txt");
			FileAssert.Exists (acwMap, "First build should have generated the post-trim ACW map.");
			var linkedJava = Directory.GetFiles (
				builder.Output.GetIntermediaryPath (Path.Combine ("typemap", "linked-java")),
				"*.java",
				SearchOption.AllDirectories).First ();
			File.Delete (acwMap);
			File.Delete (linkedJava);

			Assert.IsTrue (builder.Build (proj, doNotCleanupOnUpdate: true, saveProject: false), "Missing-output recovery build should have succeeded.");
			builder.Output.AssertTargetIsNotSkipped ("_GeneratePostTrimTrimmableTypeMapJavaSources");
			FileAssert.Exists (acwMap, "Post-trim generation should restore a missing ACW map even when linked assemblies are unchanged.");
			FileAssert.Exists (linkedJava, "Post-trim generation should restore a missing linked Java source even when linked assemblies are unchanged.");
			Assert.That (new FileInfo (acwMap).Length, Is.GreaterThan (0), "The restored ACW map should contain the linked mappings.");
		}

		[Test]
		public void Build_WithTrimmableTypeMap_PublishTrimmed_IncrementalChangesAvoidUnnecessaryJavaWork ()
		{
			if (IgnoreUnsupportedConfiguration (AndroidRuntime.CoreCLR, release: true)) {
				return;
			}

			var proj = new XamarinAndroidApplicationProject {
				IsRelease = true,
			};
			proj.SetRuntime (AndroidRuntime.CoreCLR);
			proj.SetProperty ("AndroidTypeMapImplementation", "trimmable");

			using var builder = CreateApkBuilder ();
			Assert.IsTrue (builder.Build (proj), "First build should have succeeded.");

			var linkedJavaDirectory = builder.Output.GetIntermediaryPath (Path.Combine ("typemap", "linked-java"));
			var linkedJavaBefore = Directory.GetFiles (linkedJavaDirectory, "*.java", SearchOption.AllDirectories)
				.ToDictionary (
					path => Path.GetRelativePath (linkedJavaDirectory, path),
					path => (Hash: ComputeFileHash (path), WriteTime: File.GetLastWriteTimeUtc (path)),
					StringComparer.Ordinal);
			Assert.IsNotEmpty (linkedJavaBefore, "First build should have generated post-trim Java sources.");

			var updatedMainActivity = proj.DefaultMainActivity.Replace (
				"//${AFTER_ONCREATE}",
				"System.Console.WriteLine (\"Managed-only incremental change.\");");
			Assert.AreNotEqual (proj.DefaultMainActivity, updatedMainActivity, "Test setup should update the managed MainActivity body.");
			proj.MainActivity = updatedMainActivity;
			proj.Touch ("MainActivity.cs");

			Assert.IsTrue (builder.Build (proj, doNotCleanupOnUpdate: true, saveProject: false), "Managed-only rebuild should have succeeded.");
			builder.Output.AssertTargetIsNotSkipped ("_GeneratePostTrimTrimmableTypeMapJavaSources");
			builder.Output.AssertTargetIsNotSkipped ("_RemoveRegisterAttributeCoreClr");
			builder.Output.AssertTargetIsSkipped ("_CompileJava");
			builder.Output.AssertTargetIsSkipped ("_CompileToDalvik");

			var linkedJavaAfter = Directory.GetFiles (linkedJavaDirectory, "*.java", SearchOption.AllDirectories)
				.ToDictionary (
					path => Path.GetRelativePath (linkedJavaDirectory, path),
					path => (Hash: ComputeFileHash (path), WriteTime: File.GetLastWriteTimeUtc (path)),
					StringComparer.Ordinal);
			CollectionAssert.AreEquivalent (linkedJavaBefore.Keys, linkedJavaAfter.Keys, "A managed method-body change should not alter the JCW set.");
			foreach (var pair in linkedJavaBefore) {
				var current = linkedJavaAfter [pair.Key];
				Assert.IsTrue (pair.Value.Hash.SequenceEqual (current.Hash), $"{pair.Key} content should be unchanged.");
				Assert.AreEqual (pair.Value.WriteTime, current.WriteTime, $"{pair.Key} timestamp should remain stable.");
			}

			var acwMap = builder.Output.GetIntermediaryPath ("acw-map.txt");
			var applicationRegistration = builder.Output.GetIntermediaryPath (Path.Combine ("android", "src", "net", "dot", "android", "ApplicationRegistration.java"));
			var postTrimAcwMap = ComputeFileHash (acwMap);
			var postTrimApplicationRegistration = ComputeFileHash (applicationRegistration);

			var updatedManifest = proj.AndroidManifest.Replace (
				"</manifest>",
				"<uses-permission android:name=\"android.permission.CAMERA\" /></manifest>");
			Assert.AreNotEqual (proj.AndroidManifest, updatedManifest, "Test setup should update AndroidManifest.xml.");
			proj.AndroidManifest = updatedManifest;
			proj.Touch ("Properties\\AndroidManifest.xml");

			Assert.IsTrue (builder.Build (proj, doNotCleanupOnUpdate: true), "Manifest-only rebuild should have succeeded.");
			builder.Output.AssertTargetIsNotSkipped ("_GenerateTrimmableTypeMap");
			builder.Output.AssertTargetIsSkipped ("_GeneratePostTrimTrimmableTypeMapJavaSources");
			builder.Output.AssertTargetIsNotSkipped ("_GenerateJavaStubs");
			builder.Output.AssertTargetIsSkipped ("_RemoveRegisterAttributeCoreClr");
			builder.Output.AssertTargetIsSkipped ("_CompileJava");
			builder.Output.AssertTargetIsSkipped ("_CompileToDalvik");
			Assert.IsTrue (postTrimAcwMap.SequenceEqual (ComputeFileHash (acwMap)), "The pre-trim pass should not overwrite the linked acw-map.txt.");
			Assert.IsTrue (
				postTrimApplicationRegistration.SequenceEqual (ComputeFileHash (applicationRegistration)),
				"The pre-trim pass should not overwrite the linked ApplicationRegistration.java.");

			var mergedManifest = builder.Output.GetIntermediaryPath (Path.Combine ("android", "AndroidManifest.xml"));
			StringAssert.Contains ("android.permission.CAMERA", File.ReadAllText (mergedManifest), "The manifest-only change should flow into the packaged manifest.");
		}


		[Test]
		public void Build_WithTrimmableTypeMap_DoesNotHitCopyIfChangedMismatch ([Values (AndroidRuntime.CoreCLR, AndroidRuntime.NativeAOT)] AndroidRuntime runtime)
		{
			var proj = new XamarinAndroidApplicationProject {
				IsRelease = true,
			};
			proj.SetRuntime (runtime);
			proj.SetProperty ("AndroidTypeMapImplementation", "trimmable");

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
			proj.SetProperty ("AndroidTypeMapImplementation", "trimmable");

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
			proj.SetProperty ("AndroidTypeMapImplementation", "trimmable");

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
			proj.SetProperty ("AndroidTypeMapImplementation", "trimmable");

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
			proj.SetProperty ("AndroidTypeMapImplementation", "trimmable");
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
		public void ReleaseCoreClrTrimmableTypeMap_SupportsExplicitDynamicCodeSupportOff ()
		{
			if (IgnoreUnsupportedConfiguration (AndroidRuntime.CoreCLR, release: true)) {
				return;
			}

			var dynamicCodeDisabledTrimmable = BuildDynamicCodeSupportProfile ("trimmable", dynamicCodeSupport: false);

			using var runtimeConfigJson = JsonDocument.Parse (dynamicCodeDisabledTrimmable.RuntimeConfig);
			Assert.IsTrue (
				runtimeConfigJson.RootElement.TryGetProperty ("runtimeOptions", out var runtimeOptions),
				"runtimeconfig.json should include runtimeOptions.");
			Assert.IsTrue (
				runtimeOptions.TryGetProperty ("configProperties", out var configProperties),
				"runtimeconfig.json should include runtimeOptions.configProperties.");
			Assert.IsTrue (
				configProperties.TryGetProperty ("System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported", out var dynamicCodeSupportProperty),
				"runtimeconfig.json should include RuntimeFeature.IsDynamicCodeSupported.");
			Assert.IsFalse (
				dynamicCodeSupportProperty.GetBoolean (),
				"trimmable typemap builds should honor explicit DynamicCodeSupport=false.");
		}

		[Test]
		public void ReleaseCoreClrTrimmableTypeMap_SingleRuntimeIdentifier_PackagesLinkedOrReadyToRunTypeMapAssemblies ()
		{
			if (IgnoreUnsupportedConfiguration (AndroidRuntime.CoreCLR, release: true)) {
				return;
			}

			var proj = new XamarinAndroidApplicationProject {
				IsRelease = true,
				PackageName = "com.xamarin.typemapcomparison",
				ProjectName = "TypemapComparison",
			};
			proj.SetRuntime (AndroidRuntime.CoreCLR);
			proj.SetProperty (KnownProperties.RuntimeIdentifier, "android-arm64");
			proj.SetProperty ("AndroidPackageFormat", "apk");
			proj.SetProperty ("AndroidEnableAssemblyCompression", "false");
			proj.SetProperty (KnownProperties.AndroidLinkTool, "r8");
			proj.SetProperty ("TrimMode", "full");
			proj.SetProperty ("AndroidTypeMapImplementation", "trimmable");

			using var builder = CreateApkBuilder (Path.Combine ("temp", $"TypemapComparison_trimmable_single_rid_{Guid.NewGuid ():N}"));
			Assert.IsTrue (builder.Build (proj), "trimmable single-RID build should have succeeded.");

			var apkDirectory = Path.Combine (Root, builder.ProjectDirectory, proj.OutputPath);
			var apkPath = Directory.GetFiles (apkDirectory, "*-Signed.apk", SearchOption.AllDirectories).Single ();
			var typeMapDirectory = builder.Output.GetIntermediaryPath (Path.Combine ("android-arm64", "typemap"));
			var linkedAssemblyDirectory = builder.Output.GetIntermediaryPath (Path.Combine ("android-arm64", "linked"));
			var readyToRunAssemblyDirectory = builder.Output.GetIntermediaryPath (Path.Combine ("android-arm64", "R2R"));
			var javaSourceDirectory = builder.Output.GetIntermediaryPath (Path.Combine ("android-arm64", "typemap", "linked-java"));
			var dexFile = builder.Output.GetIntermediaryPath (Path.Combine ("android-arm64", "android", "bin", "classes.dex"));
			var acwMapPath = builder.Output.GetIntermediaryPath (Path.Combine ("android-arm64", "acw-map.txt"));
			var proguardPrimaryPath = builder.Output.GetIntermediaryPath (Path.Combine ("android-arm64", "proguard", "proguard_project_primary.cfg"));

			DirectoryAssert.Exists (typeMapDirectory, "trimmable build should generate typemap assemblies.");
			DirectoryAssert.Exists (linkedAssemblyDirectory, "Release trimmable build should run ILLink.");

			var generatedTypeMapAssemblies = Directory.GetFiles (typeMapDirectory, "*.dll")
				.Where (IsTypeMapAssemblyPath)
				.ToDictionary (Path.GetFileName, StringComparer.Ordinal);
			var linkedTypeMapAssemblies = Directory.GetFiles (linkedAssemblyDirectory, "*.dll")
				.Where (IsTypeMapAssemblyPath)
				.ToDictionary (Path.GetFileName, StringComparer.Ordinal);
			var expectedPackagedTypeMapAssemblies = linkedTypeMapAssemblies.ToDictionary (
				pair => pair.Key,
				pair => File.Exists (Path.Combine (readyToRunAssemblyDirectory, pair.Key))
					? Path.Combine (readyToRunAssemblyDirectory, pair.Key)
					: pair.Value,
				StringComparer.Ordinal);
			var postLinkModifiedTypeMapAssemblies = expectedPackagedTypeMapAssemblies
				.Where (pair => generatedTypeMapAssemblies.TryGetValue (pair.Key, out var generatedPath) && !FileContentsAreEqual (generatedPath, pair.Value))
				.OrderBy (pair => pair.Key, StringComparer.Ordinal)
				.ToArray ();
			Assert.IsNotEmpty (postLinkModifiedTypeMapAssemblies, "Test setup should include typemap assemblies changed by ILLink or ReadyToRun.");

			var packagedAssemblyNames = ReadPackagedManagedAssemblyNames (apkPath, AndroidTargetArch.Arm64);
			var packagedUnexpectedTypeMapAssemblies = packagedAssemblyNames
				.Where (IsTypeMapAssemblyName)
				.Except (expectedPackagedTypeMapAssemblies.Keys, StringComparer.Ordinal)
				.OrderBy (name => name, StringComparer.Ordinal)
				.ToArray ();
			Assert.IsEmpty (
				packagedUnexpectedTypeMapAssemblies,
				$"{apkPath} should package post-link typemap assemblies, not generated typemap assemblies absent from ILLink output.");
			var helper = new ArchiveAssemblyHelper (apkPath, useAssemblyStores: true);
			foreach (var pair in postLinkModifiedTypeMapAssemblies) {
				using var packagedAssembly = helper.ReadEntry ($"assemblies/arm64-v8a/{pair.Key}", AndroidTargetArch.Arm64);
				Assert.IsNotNull (packagedAssembly, $"{pair.Key} should be packaged in the APK.");
				var expectedHash = ComputeFileHash (pair.Value);
				var packagedHash = ComputeHash (packagedAssembly);
				Assert.IsTrue (
					expectedHash.SequenceEqual (packagedHash),
					$"{apkPath} should package post-link typemap assembly {pair.Key} from {pair.Value}, not the generated pre-link copy.");
			}

			AssertPostTrimR8InputsExcludeDeadFrameworkImplementor (dexFile, javaSourceDirectory, acwMapPath, proguardPrimaryPath);
		}

		[Test]
		public void ReleaseCoreClrTrimmableTypeMap_TrimsUnusedBindingListenerImplementors ()
		{
			if (IgnoreUnsupportedConfiguration (AndroidRuntime.CoreCLR, release: true)) {
				return;
			}

			var testRoot = Path.Combine ("temp", $"{TestName}_{Guid.NewGuid ():N}");
			var binding = new XamarinAndroidBindingProject {
				IsRelease = true,
				ProjectName = "ListenerBinding",
				AndroidClassParser = "class-parse",
			};
			binding.SetRuntime (AndroidRuntime.CoreCLR);

			var javaRoot = Path.Combine (Root, testRoot, "java");
			var javaSource = Path.Combine ("com", "example", "listener", "Widget.java");
			Directory.CreateDirectory (Path.Combine (javaRoot, Path.GetDirectoryName (javaSource) ?? ""));
			binding.Jars.Add (new AndroidItem.EmbeddedJar (Path.Combine ("java", "listener.jar")) {
				BinaryContent = new JarContentBuilder {
					BaseDirectory = javaRoot,
					JarFileName = "listener.jar",
					JavaSourceFileName = javaSource,
					JavaSourceText = """
						package com.example.listener;

						public class Widget {
							public interface OnChangedListener {
								void onChanged ();
							}

							public void setOnChangedListener (OnChangedListener listener) {
							}
						}
						""",
				}.Build,
			});

			using var bindingBuilder = CreateDllBuilder (Path.Combine (testRoot, binding.ProjectName));
			Assert.IsTrue (bindingBuilder.Build (binding), "Listener binding build should have succeeded.");

			foreach (bool useListener in new [] { false, true }) {
				var app = new XamarinAndroidApplicationProject {
					IsRelease = true,
					PackageName = useListener ? "com.xamarin.listenerused" : "com.xamarin.listenerunused",
					ProjectName = useListener ? "ListenerUsed" : "ListenerUnused",
				};
				app.SetRuntime (AndroidRuntime.CoreCLR);
				app.SetProperty (KnownProperties.RuntimeIdentifier, "android-arm64");
				app.SetProperty ("AndroidPackageFormat", "apk");
				app.SetProperty (KnownProperties.AndroidLinkTool, "r8");
				app.SetProperty ("TrimMode", "full");
				app.SetProperty ("PublishReadyToRun", "false");
				app.SetProperty ("AndroidTypeMapImplementation", "trimmable");
				app.References.Add (new BuildItem.ProjectReference ($"..\\{binding.ProjectName}\\{binding.ProjectName}.csproj", binding.ProjectName, binding.ProjectGuid));
				if (useListener) {
					app.MainActivity = app.DefaultMainActivity.Replace (
						"//${AFTER_ONCREATE}",
						"""
									var widget = new Com.Example.Listener.Widget ();
									widget.Changed += (sender, args) => { };
						""");
				}

				using var builder = CreateApkBuilder (Path.Combine (testRoot, app.ProjectName));
				Assert.IsTrue (builder.Build (app), $"{app.ProjectName} build should have succeeded.");

				var linkedDirectory = builder.Output.GetIntermediaryPath (Path.Combine ("android-arm64", "linked"));
				var linkedBinding = Path.Combine (linkedDirectory, $"{binding.ProjectName}.dll");
				var javaDirectory = builder.Output.GetIntermediaryPath (Path.Combine ("android-arm64", "typemap", "linked-java"));
				var implementorJava = Path.Combine (javaDirectory, "mono", "com", "example", "listener", "Widget_OnChangedListenerImplementor.java");
				var acwMapPath = builder.Output.GetIntermediaryPath (Path.Combine ("android-arm64", "acw-map.txt"));
				var proguardPath = builder.Output.GetIntermediaryPath (Path.Combine ("android-arm64", "proguard", "proguard_project_primary.cfg"));
				var dexPath = builder.Output.GetIntermediaryPath (Path.Combine ("android-arm64", "android", "bin", "classes.dex"));

				Assert.AreEqual (
					useListener,
					AssemblyContainsType (linkedBinding, "Com.Example.Listener.Widget/IOnChangedListenerImplementor"),
					$"{app.ProjectName} linked managed output should {(useListener ? "retain" : "trim")} the listener implementor.");
				Assert.AreEqual (
					useListener,
					File.Exists (implementorJava),
					$"{app.ProjectName} post-trim Java output should {(useListener ? "retain" : "trim")} the listener implementor.");
				AssertFileContains (
					acwMapPath,
					"IOnChangedListenerImplementor",
					useListener,
					$"{app.ProjectName} ACW map");
				AssertFileContains (
					proguardPath,
					"mono.com.example.listener.Widget_OnChangedListenerImplementor",
					useListener,
					$"{app.ProjectName} ProGuard configuration");
				Assert.AreEqual (
					useListener,
					DexUtils.ContainsClass ("Lmono/com/example/listener/Widget_OnChangedListenerImplementor;", dexPath, AndroidSdkPath),
					$"{app.ProjectName} DEX should {(useListener ? "retain" : "trim")} the listener implementor.");
			}
		}

		[Test]
		public void ReleaseCoreClrTrimmableTypeMap_UsesExternalJavaRoots ()
		{
			if (IgnoreUnsupportedConfiguration (AndroidRuntime.CoreCLR, release: true)) {
				return;
			}

			var app = new XamarinAndroidApplicationProject {
				IsRelease = true,
				PackageName = "com.xamarin.externaljavaroots",
				ProjectName = "ExternalJavaRoots",
			};
			app.SetRuntime (AndroidRuntime.CoreCLR);
			app.SetProperty (KnownProperties.RuntimeIdentifier, "android-arm64");
			app.SetProperty ("AndroidPackageFormat", "apk");
			app.SetProperty ("TrimMode", "full");
			app.SetProperty ("PublishReadyToRun", "false");
			app.SetProperty ("AndroidTypeMapImplementation", "trimmable");
			app.Sources.Add (new BuildItem.Source ("Views.cs") {
				TextContent = () => """
					using Android.Content;
					using Android.Runtime;
					using Android.Util;
					using Android.Views;

					namespace ExternalJavaRoots;

					public class LayoutOnlyView : View
					{
						public LayoutOnlyView (Context context, IAttributeSet attributes) : base (context, attributes)
						{
						}
					}

					[Register ("com.example.RegisteredLayoutOnlyView")]
					public class RegisteredLayoutOnlyView : View
					{
						public RegisteredLayoutOnlyView (Context context, IAttributeSet attributes) : base (context, attributes)
						{
						}
					}

					public class UnusedView : View
					{
						public UnusedView (Context context) : base (context)
						{
						}
					}
					""",
			});
			app.AndroidResources.Add (new AndroidItem.AndroidResource ("Resources\\layout\\layout_only.xml") {
				TextContent = () => """
					<?xml version="1.0" encoding="utf-8"?>
					<ExternalJavaRoots.LayoutOnlyView
						xmlns:android="http://schemas.android.com/apk/res/android"
						android:layout_width="match_parent"
						android:layout_height="match_parent" />
					""",
			});
			app.AndroidResources.Add (new AndroidItem.AndroidResource ("Resources\\layout\\registered_layout_only.xml") {
				TextContent = () => """
					<?xml version="1.0" encoding="utf-8"?>
					<com.example.RegisteredLayoutOnlyView
						xmlns:android="http://schemas.android.com/apk/res/android"
						android:layout_width="match_parent"
						android:layout_height="match_parent" />
					""",
			});

			using var builder = CreateApkBuilder (Path.Combine ("temp", $"{TestName}_{Guid.NewGuid ():N}"));
			Assert.IsTrue (builder.Build (app), "External Java roots build should have succeeded.");

			var linkedApp = builder.Output.GetIntermediaryPath (Path.Combine ("android-arm64", "linked", $"{app.ProjectName}.dll"));
			Assert.IsTrue (AssemblyContainsType (linkedApp, "ExternalJavaRoots.LayoutOnlyView"), "The XML-only custom view should survive linking.");
			Assert.IsTrue (AssemblyContainsType (linkedApp, "ExternalJavaRoots.RegisteredLayoutOnlyView"), "The XML-only custom view referenced by its explicit Java name should survive linking.");
			Assert.IsFalse (AssemblyContainsType (linkedApp, "ExternalJavaRoots.UnusedView"), "An unreferenced ACW should be trimmed.");

			var javaDirectory = builder.Output.GetIntermediaryPath (Path.Combine ("android-arm64", "typemap", "linked-java"));
			Assert.IsNotEmpty (Directory.GetFiles (javaDirectory, "LayoutOnlyView.java", SearchOption.AllDirectories));
			Assert.IsNotEmpty (Directory.GetFiles (javaDirectory, "RegisteredLayoutOnlyView.java", SearchOption.AllDirectories));
			Assert.IsEmpty (Directory.GetFiles (javaDirectory, "UnusedView.java", SearchOption.AllDirectories));
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
			proj.SetProperty ("AndroidTypeMapImplementation", "trimmable");
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
			proj.SetProperty ("AndroidTypeMapImplementation", "trimmable");
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
			proj.SetProperty ("AndroidTypeMapImplementation", "trimmable");
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

		static void AssertTrimmableTypeMapOutputs (string typemapDir)
		{
			DirectoryAssert.Exists (typemapDir);
			FileAssert.Exists (Path.Combine (typemapDir, "_Microsoft.Android.TypeMaps.dll"));
			FileAssert.Exists (Path.Combine (typemapDir, "_Mono.Android.TypeMap.dll"));

			var javaDir = Path.Combine (typemapDir, "java");
			DirectoryAssert.Exists (javaDir, "Trimmable JCW Java output directory should exist.");

			var javaFiles = Directory.GetFiles (javaDir, "*.java", SearchOption.AllDirectories);
			Assert.IsNotEmpty (javaFiles, "At least one trimmable JCW Java source file should be generated.");
		}

		static bool AssemblyContainsType (string assemblyPath, string typeFullName)
		{
			if (!File.Exists (assemblyPath)) {
				return false;
			}

			using var assembly = AssemblyDefinition.ReadAssembly (assemblyPath);
			return ContainsType (assembly.MainModule.Types, typeFullName);
		}

		static bool ContainsType (IEnumerable<TypeDefinition> types, string typeFullName)
		{
			foreach (var type in types) {
				if (type.FullName == typeFullName || ContainsType (type.NestedTypes, typeFullName)) {
					return true;
				}
			}

			return false;
		}

		static void AssertFileContains (string path, string value, bool expected, string description)
		{
			FileAssert.Exists (path, $"{description} should exist.");
			var contents = File.ReadAllText (path);
			Assert.AreEqual (
				expected,
				contents.Contains (value, StringComparison.Ordinal),
				$"{description} should {(expected ? "contain" : "exclude")} '{value}'.");
		}

		static string FindSingleFile (string directory, string fileName, Func<string, bool>? predicate = null)
		{
			var files = Directory.GetFiles (directory, fileName, SearchOption.AllDirectories)
				.Where (path => predicate?.Invoke (path) != false)
				.ToArray ();
			Assert.AreEqual (1, files.Length, $"Expected exactly one {fileName} under {directory}, but found:\n{string.Join ("\n", files)}");
			return files [0];
		}

		static void AssertR8MappingRenamesClass (string mappingFile, string originalJniName)
		{
			string originalName = originalJniName.Replace ('/', '.');
			var match = Regex.Match (
				File.ReadAllText (mappingFile),
				$"^{Regex.Escape (originalName)} -> (?<renamed>[^:]+):$",
				RegexOptions.Multiline);
			Assert.IsTrue (match.Success, $"Expected {mappingFile} to contain a mapping for {originalName}.");
			Assert.AreNotEqual (originalName, match.Groups ["renamed"].Value, $"Seed R8 should rename {originalName}.");
		}

		static void AssertR8MappingKeepsClassName (string mappingFile, string originalJniName)
		{
			string originalName = originalJniName.Replace ('/', '.');
			StringAssert.Contains (
				$"{originalName} -> {originalName}:",
				File.ReadAllText (mappingFile),
				$"R8 should preserve the binary Android resource entry point {originalName}.");
		}

		static void AssertR8MappingContainsMember (string mappingFile, string originalJniName, string memberName)
		{
			string originalName = originalJniName.Replace ('/', '.');
			var lines = File.ReadAllLines (mappingFile);
			string classHeader = $"{originalName} -> ";
			int classIndex = Array.FindIndex (lines, line =>
				line.StartsWith (classHeader, StringComparison.Ordinal) &&
				line.EndsWith (":", StringComparison.Ordinal));
			Assert.That (classIndex, Is.GreaterThanOrEqualTo (0), $"Expected {mappingFile} to contain a mapping for {originalName}.");

			var classMapping = lines
				.Skip (classIndex + 1)
				.TakeWhile (line => line.StartsWith ("    ", StringComparison.Ordinal) || line.StartsWith ("#", StringComparison.Ordinal))
				.ToArray ();
			Assert.IsTrue (
				classMapping.Any (line => Regex.IsMatch (line, $@"\b{Regex.Escape (memberName)}\([^)]*\) -> ")),
				$"Expected {mappingFile} to retain and map {originalName}.{memberName}. Matching class section:\n{string.Join ("\n", classMapping)}");
		}

		static Import CreateR8JniManifestMergerInputsAssertionImport ()
			=> new Import ("AssertR8JniManifestMergerDirectory.targets") {
				TextContent = () => """
					<Project>
					  <Target Name="_AssertR8JniManifestMergerDirectory"
					      BeforeTargets="_ManifestMerger"
					      Condition=" '$(_AndroidEnableR8JniNameRewriting)' == 'true' and '$(AndroidManifestMerger)' == 'manifestmerger.jar' ">
					    <Error
					        Condition=" !Exists('$(IntermediateOutputPath)android') "
					        Text="The R8 JNI manifest dependency chain must prepare the manifest merger output directory." />
					    <Error
					        Condition=" !Exists('$(IntermediateOutputPath)AndroidManifest.xml') "
					        Text="The R8 JNI manifest dependency chain must prepare the manifest merger input manifest." />
					  </Target>
					</Project>
					""",
			};

		DynamicCodeSupportProfile BuildDynamicCodeSupportProfile (string typemapImplementation, bool? dynamicCodeSupport)
		{
			var dynamicCodeSuffix = dynamicCodeSupport.HasValue ? $"_{dynamicCodeSupport.Value.ToString ().ToLowerInvariant ()}" : "";
			var projectName = $"DynamicCodeSupport_{typemapImplementation.Replace ("-", "_")}{dynamicCodeSuffix}";
			var proj = new XamarinAndroidApplicationProject {
				IsRelease = true,
				PackageName = "com.xamarin.dynamiccodesupport",
				ProjectName = projectName,
			};
			proj.SetRuntime (AndroidRuntime.CoreCLR);
			proj.SetProperty (KnownProperties.RuntimeIdentifier, "android-arm64");
			proj.SetProperty ("AndroidPackageFormat", "apk");
			proj.SetProperty (KnownProperties.AndroidLinkTool, "r8");
			proj.SetProperty ("TrimMode", "full");
			proj.SetProperty ("PublishReadyToRun", "false");
			proj.SetProperty ("AndroidTypeMapImplementation", typemapImplementation);
			if (dynamicCodeSupport.HasValue) {
				proj.SetProperty ("DynamicCodeSupport", dynamicCodeSupport.Value.ToString ().ToLowerInvariant ());
			}

			using var builder = CreateApkBuilder (Path.Combine ("temp", $"{projectName}_{Guid.NewGuid ():N}"));
			Assert.IsTrue (builder.Build (proj), $"{typemapImplementation} build should have succeeded.");

			var runtimeConfigPath = FindOutputFile (builder, proj, $"{proj.ProjectName}.runtimeconfig.json");
			return new DynamicCodeSupportProfile (File.ReadAllText (runtimeConfigPath));
		}

		ISet<string> ReadPackagedManagedAssemblyNames (string apkPath, AndroidTargetArch targetArch)
		{
			(var explorers, var errorMessage) = AssemblyStoreExplorer.Open (apkPath);
			Assert.IsNull (errorMessage, $"{apkPath} should contain readable assembly stores.");
			Assert.IsNotNull (explorers, $"{apkPath} should contain assembly stores.");

			var explorer = explorers.FirstOrDefault (e => e.TargetArch == targetArch);
			Assert.IsNotNull (explorer, $"{apkPath} should contain an {targetArch} assembly store.");

			return explorer.Assemblies
				.Where (a => !a.Ignore && a.Name.EndsWith (".dll", StringComparison.OrdinalIgnoreCase) && !a.Name.EndsWith (".ni.dll", StringComparison.OrdinalIgnoreCase))
				.Select (a => a.Name)
				.ToHashSet (StringComparer.Ordinal);
		}

		void AssertPostTrimR8InputsExcludeDeadFrameworkImplementor (string dexFile, string javaSourceDirectory, string acwMapPath, string proguardPrimaryPath)
		{
			const string deadManagedType = "Android.Animation.Animator+IAnimatorListenerImplementor";
			const string deadJavaName = "Lmono/android/animation/Animator_AnimatorListenerImplementor;";
			const string deadJavaDotName = "mono.android.animation.Animator_AnimatorListenerImplementor";

			Assert.IsTrue (
				Directory.EnumerateFiles (javaSourceDirectory, "MainActivity.java", SearchOption.AllDirectories).Any (),
				"Post-trim Java source generation should keep the app activity JCW.");
			FileAssert.DoesNotExist (
				Path.Combine (javaSourceDirectory, "mono", "android", "animation", "Animator_AnimatorListenerImplementor.java"),
				"Post-trim Java source generation should not generate framework listener implementors removed by ILLink.");

			FileAssert.Exists (acwMapPath, "Post-trim scan should rewrite acw-map.txt for R8.");
			var acwMap = File.ReadAllText (acwMapPath);
			Assert.IsFalse (acwMap.Contains (deadManagedType, StringComparison.Ordinal), $"{acwMapPath} should be based on linked assemblies.");
			Assert.IsFalse (acwMap.Contains (deadJavaDotName, StringComparison.Ordinal), $"{acwMapPath} should not keep removed framework listener implementors.");

			FileAssert.Exists (proguardPrimaryPath, "R8 should generate a primary proguard configuration from the post-trim acw-map.");
			Assert.IsFalse (
				File.ReadAllText (proguardPrimaryPath).Contains (deadJavaDotName, StringComparison.Ordinal),
				$"{proguardPrimaryPath} should not keep removed framework listener implementors.");

			FileAssert.Exists (dexFile, "R8 should produce classes.dex.");
			Assert.IsFalse (
				DexUtils.ContainsClass (deadJavaName, dexFile, AndroidSdkPath),
				$"{dexFile} should not contain the removed framework listener implementor.");
		}

		string FindOutputFile (ProjectBuilder builder, XamarinAndroidApplicationProject proj, string fileName)
		{
			var outputDirectory = Path.Combine (Root, builder.ProjectDirectory, proj.OutputPath);
			var files = Directory.GetFiles (outputDirectory, fileName, SearchOption.AllDirectories);
			Assert.AreEqual (1, files.Length, $"{outputDirectory} should contain one {fileName}.");
			return files [0];
		}

		bool IsTypeMapAssemblyPath (string file)
		{
			return IsTypeMapAssemblyName (Path.GetFileName (file));
		}

		bool IsTypeMapAssemblyName (string fileName)
		{
			return fileName.EndsWith (".TypeMap.dll", StringComparison.Ordinal) ||
				fileName.StartsWith ("_Microsoft.Android.TypeMap", StringComparison.Ordinal);
		}

		static bool FileContentsAreEqual (string first, string second)
		{
			return ComputeFileHash (first).SequenceEqual (ComputeFileHash (second));
		}

		static byte [] ComputeFileHash (string path)
		{
			using var stream = File.OpenRead (path);
			return ComputeHash (stream);
		}

		static byte [] ComputeHash (Stream stream)
		{
			return SHA256.HashData (stream);
		}

		sealed record DynamicCodeSupportProfile (string RuntimeConfig);
	}
}
