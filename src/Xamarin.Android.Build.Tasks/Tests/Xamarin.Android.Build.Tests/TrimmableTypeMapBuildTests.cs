using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
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
			proj.SetProperty ("_AndroidTypeMapImplementation", "trimmable");

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
			proj.SetProperty ("_AndroidTypeMapImplementation", "trimmable");

			using var builder = CreateApkBuilder ();
			Assert.IsTrue (builder.Build (proj), "First build should have succeeded.");

			var intermediateDir = builder.Output.GetIntermediaryPath ("typemap");
			AssertTrimmableTypeMapOutputs (intermediateDir);
			var typemapDlls = Directory.GetFiles (intermediateDir, "*.dll");
			Assert.IsNotEmpty (typemapDlls, "First build should have generated typemap DLL(s).");

			Assert.IsTrue (builder.Build (proj), "Second build should have succeeded.");

			Assert.IsTrue (
				builder.Output.IsTargetSkipped ("_GenerateJavaStubs"),
				"_GenerateJavaStubs should be skipped on incremental build.");
			foreach (var typemapDll in typemapDlls) {
				FileAssert.Exists (typemapDll, $"No-op builds should preserve generated typemap assembly {typemapDll} when _GenerateTrimmableTypeMap is skipped.");
			}
		}

		[Test]
		public void Build_WithTrimmableTypeMap_DeletesStaleGeneratedJavaSourcesAndCopies ()
		{
			if (IgnoreUnsupportedConfiguration (AndroidRuntime.CoreCLR, release: false)) {
				return;
			}

			var proj = new XamarinAndroidApplicationProject ();
			proj.SetRuntime (AndroidRuntime.CoreCLR);
			proj.SetProperty ("_AndroidTypeMapImplementation", "trimmable");

			using var builder = CreateApkBuilder ();
			Assert.IsTrue (builder.Build (proj), "First build should have succeeded.");

			var staleRelativePath = Path.Combine ("crc64stale", "Old.java");
			var staleClassPath = Path.Combine ("crc64stale", "Old.class");
			var staleGeneratedJava = builder.Output.GetIntermediaryPath (Path.Combine ("typemap", "java", staleRelativePath));
			var staleCopiedJava = builder.Output.GetIntermediaryPath (Path.Combine ("android", "src", staleRelativePath));
			var staleCompiledClass = builder.Output.GetIntermediaryPath (Path.Combine ("android", "bin", "classes", staleClassPath));
			var staleGeneratedJavaDirectory = Path.GetDirectoryName (staleGeneratedJava);
			var staleCopiedJavaDirectory = Path.GetDirectoryName (staleCopiedJava);
			var staleCompiledClassDirectory = Path.GetDirectoryName (staleCompiledClass);
			if (staleGeneratedJavaDirectory is null || staleCopiedJavaDirectory is null || staleCompiledClassDirectory is null) {
				throw new InvalidOperationException ("Could not determine stale Java output directories.");
			}
			Directory.CreateDirectory (staleGeneratedJavaDirectory);
			Directory.CreateDirectory (staleCopiedJavaDirectory);
			Directory.CreateDirectory (staleCompiledClassDirectory);
			File.WriteAllText (staleGeneratedJava, "package crc64stale; public class Old {}");
			File.WriteAllText (staleCopiedJava, "package crc64stale; public class Old {}");
			File.WriteAllBytes (staleCompiledClass, []);

			proj.MainActivity += Environment.NewLine + "// Force trimmable typemap regeneration.";
			proj.Touch ("MainActivity.cs");
			Assert.IsTrue (builder.Build (proj, doNotCleanupOnUpdate: true, saveProject: false), "Second build should have succeeded.");
			builder.Output.AssertTargetIsNotSkipped ("_GenerateTrimmableTypeMap");
			builder.Output.AssertTargetIsNotSkipped ("_CompileJava");

			FileAssert.DoesNotExist (staleGeneratedJava, "Regenerated trimmable typemap should delete stale Java sources.");
			FileAssert.DoesNotExist (staleCopiedJava, "Regenerated trimmable typemap should delete stale android/src Java copies.");
			FileAssert.DoesNotExist (staleCompiledClass, "Deleting stale copied Java sources should force Java recompilation and remove stale class outputs.");
		}

		[Test]
		public void Build_WithTrimmableTypeMap_CopiesUpdatedGeneratedJavaSources ()
		{
			if (IgnoreUnsupportedConfiguration (AndroidRuntime.CoreCLR, release: false)) {
				return;
			}

			var proj = new XamarinAndroidApplicationProject ();
			proj.SetRuntime (AndroidRuntime.CoreCLR);
			proj.SetProperty ("_AndroidTypeMapImplementation", "trimmable");

			using var builder = CreateApkBuilder ();
			Assert.IsTrue (builder.Build (proj), "First build should have succeeded.");

			var generatedJavaDirectory = builder.Output.GetIntermediaryPath (Path.Combine ("typemap", "java"));
			var generatedJavaFiles = Directory.GetFiles (generatedJavaDirectory, "*.java", SearchOption.AllDirectories);
			Assert.IsNotEmpty (generatedJavaFiles, "Test setup should have generated trimmable typemap Java sources.");

			var generatedJava = generatedJavaFiles [0];
			var relativePath = Path.GetRelativePath (generatedJavaDirectory, generatedJava);
			var copiedJava = builder.Output.GetIntermediaryPath (Path.Combine ("android", "src", relativePath));
			var typeMapStamp = builder.Output.GetIntermediaryPath (Path.Combine ("typemap", "_GenerateTrimmableTypeMap.stamp"));
			var javaStubsStamp = builder.Output.GetIntermediaryPath (Path.Combine ("stamp", "_GenerateJavaStubs.stamp"));
			FileAssert.Exists (copiedJava, "First build should have copied generated Java sources to android/src.");
			FileAssert.Exists (typeMapStamp, "First build should have written the trimmable typemap output stamp.");
			FileAssert.Exists (javaStubsStamp, "First build should have written the Java stubs output stamp.");

			var updatedJava = File.ReadAllText (generatedJava) + "\n// Force generated Java copy regression.\n";
			File.WriteAllText (generatedJava, updatedJava);
			var stampTime = DateTime.UtcNow;
			File.SetLastWriteTimeUtc (typeMapStamp, stampTime);
			File.SetLastWriteTimeUtc (javaStubsStamp, stampTime.AddSeconds (-5));

			Assert.IsTrue (builder.Build (proj, doNotCleanupOnUpdate: true), "Second build should have succeeded.");
			builder.Output.AssertTargetIsNotSkipped ("_GenerateJavaStubs");
			builder.Output.AssertTargetIsNotSkipped ("_CompileJava");
			Assert.AreEqual (updatedJava, File.ReadAllText (copiedJava), "Updated generated Java sources should be copied to android/src even when typemap assemblies do not change.");
		}

		// The JCWs that actually get compiled and packaged are the ones copied into
		// $(IntermediateOutputPath)android/src. For CoreCLR + PublishTrimmed those come from
		// the post-trim `typemap/linked-java` directory, which `_GeneratePostTrimTrimmableTypeMapJavaSources`
		// (re)generates from the linked assemblies. The incrementality contract is that
		// android/src must always stay consistent with linked-java; if `_GenerateJavaStubs` is
		// skipped while linked-java changed, android/src would be left stale.
		static void AssertAndroidSrcMatchesLinkedJava (ProjectBuilder builder, string message)
		{
			var intermediate = builder.Output.GetIntermediaryPath ("");
			var linkedJavaDirectory = Directory.GetDirectories (intermediate, "linked-java", SearchOption.AllDirectories).FirstOrDefault ();
			Assert.IsNotNull (linkedJavaDirectory, $"{message}: post-trim linked-java directory should exist under '{intermediate}'.");
			// The JCWs that get compiled live in the same intermediate tree under android/src.
			var androidSrcDirectory = Path.Combine (Path.GetDirectoryName (Path.GetDirectoryName (linkedJavaDirectory)), "android", "src");
			DirectoryAssert.Exists (androidSrcDirectory, $"{message}: android/src directory should exist.");

			var linkedJavaFiles = Directory.GetFiles (linkedJavaDirectory, "*.java", SearchOption.AllDirectories);
			Assert.IsNotEmpty (linkedJavaFiles, $"{message}: post-trim build should have generated linked-java JCWs.");

			foreach (var linkedJava in linkedJavaFiles) {
				var relativePath = Path.GetRelativePath (linkedJavaDirectory, linkedJava);
				var copiedJava = Path.Combine (androidSrcDirectory, relativePath);
				FileAssert.Exists (copiedJava, $"{message}: linked-java JCW '{relativePath}' should be copied to android/src.");
				Assert.AreEqual (
					File.ReadAllText (linkedJava),
					File.ReadAllText (copiedJava),
					$"{message}: android/src copy of '{relativePath}' should match the post-trim linked-java source.");
			}
		}

		[Test]
		public void Build_WithTrimmableTypeMap_PublishTrimmed_KeepsAndroidSrcConsistentWithLinkedJava ()
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
			Assert.IsTrue (builder.Build (proj), "First build should have succeeded.");
			AssertAndroidSrcMatchesLinkedJava (builder, "After first build");

			// A no-op rebuild must not leave android/src out of sync with linked-java, even though
			// the post-trim Java generation may run again.
			Assert.IsTrue (builder.Build (proj, doNotCleanupOnUpdate: true, saveProject: false), "No-op rebuild should have succeeded.");
			AssertAndroidSrcMatchesLinkedJava (builder, "After no-op rebuild");
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
			proj.SetProperty ("_AndroidTypeMapImplementation", "trimmable");

			using var builder = CreateApkBuilder ();
			Assert.IsTrue (builder.Build (proj), "First build should have succeeded.");

			// A no-op rebuild should not regenerate the post-trim JCWs or recopy them. If
			// _GeneratePostTrimTrimmableTypeMapJavaSources runs on every build, the JCWs that feed
			// _GenerateJavaStubs are rewritten each time, which both wastes work and means
			// _GenerateJavaStubs must re-run to stay consistent (otherwise android/src goes stale).
			Assert.IsTrue (builder.Build (proj, doNotCleanupOnUpdate: true, saveProject: false), "No-op rebuild should have succeeded.");
			builder.Output.AssertTargetIsSkipped ("_GeneratePostTrimTrimmableTypeMapJavaSources");
			builder.Output.AssertTargetIsSkipped ("_GenerateJavaStubs");
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
			Assert.IsTrue (
				dynamicCodeDisabledTrimmable.LinkedTypeMapAssembliesContainArrayRankSentinels,
				"trimmable typemap builds should emit array typemap sentinels when dynamic code is disabled.");
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
			proj.SetProperty ("_AndroidTypeMapImplementation", "trimmable");

			using var builder = CreateApkBuilder (Path.Combine ("temp", $"TypemapComparison_trimmable_single_rid_{Guid.NewGuid ():N}"));
			Assert.IsTrue (builder.Build (proj), "trimmable single-RID build should have succeeded.");

			var apkDirectory = Path.Combine (Root, builder.ProjectDirectory, proj.OutputPath);
			var apkPath = Directory.GetFiles (apkDirectory, "*-Signed.apk", SearchOption.AllDirectories).Single ();
			var typeMapDirectory = builder.Output.GetIntermediaryPath (Path.Combine ("android-arm64", "typemap"));
			var linkedAssemblyDirectory = builder.Output.GetIntermediaryPath (Path.Combine ("android-arm64", "linked"));
			var readyToRunAssemblyDirectory = builder.Output.GetIntermediaryPath (Path.Combine ("android-arm64", "R2R"));
			var javaSourceDirectory = builder.Output.GetIntermediaryPath (Path.Combine ("android-arm64", "android", "src"));
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
			proj.SetProperty ("_AndroidTypeMapImplementation", typemapImplementation);
			if (dynamicCodeSupport.HasValue) {
				proj.SetProperty ("DynamicCodeSupport", dynamicCodeSupport.Value.ToString ().ToLowerInvariant ());
			}

			using var builder = CreateApkBuilder (Path.Combine ("temp", $"{projectName}_{Guid.NewGuid ():N}"));
			Assert.IsTrue (builder.Build (proj), $"{typemapImplementation} build should have succeeded.");

			var runtimeConfigPath = FindOutputFile (builder, proj, $"{proj.ProjectName}.runtimeconfig.json");
			var linkedAssemblyDirectory = builder.Output.GetIntermediaryPath (Path.Combine ("android-arm64", "linked"));
			return new DynamicCodeSupportProfile (
				File.ReadAllText (runtimeConfigPath),
				TypeMapAssembliesContainType (linkedAssemblyDirectory, "__ArrayMapRank1"));
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
				"Post-trim Java source generation should not copy framework listener implementors removed by ILLink.");

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

		bool TypeMapAssembliesContainType (string directory, string typeName)
		{
			if (!Directory.Exists (directory)) {
				return false;
			}

			foreach (var file in Directory.EnumerateFiles (directory, "*.dll", SearchOption.TopDirectoryOnly).Where (IsTypeMapAssemblyPath)) {
				using var assembly = AssemblyDefinition.ReadAssembly (file);
				if (assembly.Modules.SelectMany (m => m.Types).Any (type => type.Name == typeName)) {
					return true;
				}
			}

			return false;
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

		sealed record DynamicCodeSupportProfile (
			string RuntimeConfig,
			bool LinkedTypeMapAssembliesContainArrayRankSentinels);
	}
}
