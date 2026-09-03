using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Android.Sdk.TrimmableTypeMap;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NUnit.Framework;
using Xamarin.Android.Tasks;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests {
	[TestFixture]
	[Parallelizable (ParallelScope.Children)]
	public class GenerateTrimmableTypeMapTests : BaseTest {

		[Test]
		public void Execute_EmptyAssemblyList_Succeeds ()
		{
			var path = Path.Combine ("temp", TestName);
			var outputDir = Path.Combine (Root, path, "typemap");
			var javaDir = Path.Combine (Root, path, "java");

			var task = CreateTask ([], outputDir, javaDir);

			Assert.IsTrue (task.Execute (), "Task should succeed with empty assembly list.");
			Assert.IsEmpty (task.GeneratedAssemblies);
			Assert.IsEmpty (task.GeneratedJavaFiles);
		}

		[Test]
		public void Execute_InvalidTargetFrameworkVersion_Fails ()
		{
			var path = Path.Combine ("temp", TestName);
			var outputDir = Path.Combine (Root, path, "typemap");
			var javaDir = Path.Combine (Root, path, "java");

			var errors = new List<BuildErrorEventArgs> ();
			var task = new GenerateTrimmableTypeMap {
				BuildEngine = new MockBuildEngine (TestContext.Out, errors),
				ResolvedAssemblies = [],
				OutputDirectory = outputDir,
				JavaSourceOutputDirectory = javaDir,
				TargetFrameworkVersion = "not-a-version",
			};

			Assert.IsFalse (task.Execute (), "Task should fail with invalid TargetFrameworkVersion.");
			Assert.IsNotEmpty (errors, "Should have logged an error.");
		}

		[Test]
		public void Execute_WithMonoAndroid_ProducesOutputs ()
		{
			var path = Path.Combine ("temp", TestName);
			var outputDir = Path.Combine (Root, path, "typemap");
			var javaDir = Path.Combine (Root, path, "java");

			var monoAndroidItem = FindMonoAndroidDll ();
			if (monoAndroidItem is null) {
				Assert.Ignore ("Mono.Android.dll not found; skipping.");
				return;
			}

			var task = CreateTask (new [] { monoAndroidItem }, outputDir, javaDir);

			Assert.IsTrue (task.Execute (), "Task should succeed.");
			Assert.IsNotNull (task.GeneratedAssemblies);
			Assert.IsNotEmpty (task.GeneratedAssemblies);

			var assemblyPaths = task.GeneratedAssemblies.Select (i => i.ItemSpec).ToList ();
			Assert.IsTrue (assemblyPaths.Any (p => p.Contains ("_Microsoft.Android.TypeMaps.dll")),
				"Should produce root _Microsoft.Android.TypeMaps.dll");
			Assert.IsTrue (assemblyPaths.Any (p => p.Contains ("_Mono.Android.TypeMap.dll")),
				"Should produce _Mono.Android.TypeMap.dll");

			foreach (var assembly in task.GeneratedAssemblies) {
				FileAssert.Exists (assembly.ItemSpec);
			}
		}

		[Test]
		public void Execute_SecondRun_OutputsAreUpToDate ()
		{
			var path = Path.Combine ("temp", TestName);
			var outputDir = Path.Combine (Root, path, "typemap");
			var javaDir = Path.Combine (Root, path, "java");

			var monoAndroidItem = FindMonoAndroidDll ();
			if (monoAndroidItem is null) {
				Assert.Ignore ("Mono.Android.dll not found; skipping.");
				return;
			}

			var assemblies = new [] { monoAndroidItem };

			// First run: generates everything
			var task1 = CreateTask (assemblies, outputDir, javaDir);
			Assert.IsTrue (task1.Execute (), "First run should succeed.");

			var typeMapPath = task1.GeneratedAssemblies
				.Select (i => i.ItemSpec)
				.First (p => p.Contains ("_Mono.Android.TypeMap.dll"));
			var firstWriteTime = File.GetLastWriteTimeUtc (typeMapPath);

			// Second run: same inputs — outputs should not be rewritten (CopyIfStreamChanged)
			var task2 = CreateTask (assemblies, outputDir, javaDir);
			Assert.IsTrue (task2.Execute (), "Second run should succeed.");

			var secondWriteTime = File.GetLastWriteTimeUtc (typeMapPath);
			Assert.AreEqual (firstWriteTime, secondWriteTime,
				"Typemap assembly should NOT be rewritten when content hasn't changed.");
		}

		[Test]
		public void Execute_MissingJavaSource_DoesNotPruneExistingOutput ()
		{
			var path = Path.Combine ("temp", TestName);
			var outputDir = Path.Combine (Root, path, "typemap");
			var javaInputDir = Path.Combine (Root, path, "java");
			var javaOutputDir = Path.Combine (Root, path, "linked-java");

			var monoAndroidItem = FindMonoAndroidDll ();
			if (monoAndroidItem is null) {
				Assert.Ignore ("Mono.Android.dll not found; skipping.");
				return;
			}

			var firstTask = CreateTask (new [] { monoAndroidItem }, outputDir, javaInputDir);
			Assert.IsTrue (firstTask.Execute (), "First run should generate Java inputs.");
			Assert.IsNotEmpty (firstTask.GeneratedJavaFiles, "Test setup should generate Java sources.");

			var missingInput = firstTask.GeneratedJavaFiles [0].ItemSpec;
			var relativePath = Path.GetRelativePath (javaInputDir, missingInput);
			var existingOutput = Path.Combine (javaOutputDir, relativePath);
			var existingOutputDirectory = Path.GetDirectoryName (existingOutput);
			if (existingOutputDirectory is null) {
				throw new InvalidOperationException ("Could not determine the linked Java output directory.");
			}
			Directory.CreateDirectory (existingOutputDirectory);
			File.Copy (missingInput, existingOutput);
			File.Delete (missingInput);

			var errors = new List<BuildErrorEventArgs> ();
			var secondTask = CreateTask (new [] { monoAndroidItem }, outputDir, javaOutputDir, errors: errors);
			secondTask.JavaSourceInputDirectory = javaInputDir;
			secondTask.GenerateTypeMapAssemblies = false;

			Assert.IsFalse (secondTask.Execute (), "The missing pre-trim Java source should fail with XA4255.");
			Assert.IsTrue (errors.Any (e => e.Code == "XA4255"), "The task should report the missing Java source.");
			FileAssert.Exists (existingOutput, "A failing in-place update should preserve the last known-good linked Java source.");
		}

		[Test]
		public void CopyJavaSources_ReverseMapsObfuscatedNestedClassPath ()
		{
			var path = Path.Combine (Root, "temp", TestName);
			var inputDir = Path.Combine (path, "java");
			var outputDir = Path.Combine (path, "linked-java");
			var originalRelativePath = Path.Combine ("com", "example", "Outer$Inner.java");
			var inputPath = Path.Combine (inputDir, originalRelativePath);
			var inputPathDirectory = Path.GetDirectoryName (inputPath);
			if (inputPathDirectory is null) {
				throw new InvalidOperationException ("Could not determine the Java input directory.");
			}
			Directory.CreateDirectory (inputPathDirectory);
			File.WriteAllText (inputPath, "original");
			var task = CreateJavaSourceCopyTask (
				inputDir,
				outputDir,
				"com.example.Outer$Inner -> g:\n",
				"C\tcom/example/Outer$Inner\n");

			Assert.IsTrue (task.LoadR8JavaSourcePathMapping ());
			var outputs = task.CopyJavaSourcesFromInputDirectory (new [] { new GeneratedJavaSource ("g.java", "") });

			Assert.That (outputs, Has.Exactly (1).Property ("ItemSpec").EqualTo (Path.Combine (outputDir, originalRelativePath)));
			Assert.AreEqual ("original", File.ReadAllText (Path.Combine (outputDir, originalRelativePath)));
			FileAssert.DoesNotExist (Path.Combine (outputDir, "g.java"));
		}

		[TestCase ('/', "com/example/Outer$Inner.java")]
		[TestCase ('\\', "com\\example\\Outer$Inner.java")]
		public void NormalizeJavaSourceRelativePath_UsesDirectorySeparator (char directorySeparator, string expected)
		{
			Assert.AreEqual (
				expected,
				GenerateTrimmableTypeMap.NormalizeJavaSourceRelativePath ("com/example/Outer$Inner.java", directorySeparator));
		}

		[TestCase ("C\tcom/example/First\nC\tcom/example/Second\n", TestName = "CopyJavaSources_MergedClassIsAmbiguous")]
		[TestCase ("C\tcom/example/Unrelated\n", TestName = "CopyJavaSources_MissingRequiredReverseEntry")]
		public void CopyJavaSources_InvalidReverseMappingUsesXA4327 (string manifest)
		{
			var path = Path.Combine (Root, "temp", TestName);
			var errors = new List<BuildErrorEventArgs> ();
			var task = CreateJavaSourceCopyTask (
				Path.Combine (path, "java"),
				Path.Combine (path, "linked-java"),
				"com.example.First -> g:\ncom.example.Second -> g:\n",
				manifest,
				errors);

			Assert.IsTrue (task.LoadR8JavaSourcePathMapping ());
			var outputs = task.CopyJavaSourcesFromInputDirectory (new [] { new GeneratedJavaSource ("g.java", "") });

			Assert.IsEmpty (outputs);
			Assert.That (errors, Has.Exactly (1).Property ("Code").EqualTo ("XA4327"));
		}

		[Test]
		public void CopyJavaSources_PreservesUnmappedPath ()
		{
			var path = Path.Combine (Root, "temp", TestName);
			var inputDir = Path.Combine (path, "java");
			var outputDir = Path.Combine (path, "linked-java");
			var relativePath = Path.Combine ("android", "runtime", "FrameworkPeer.java");
			var inputPath = Path.Combine (inputDir, relativePath);
			var inputPathDirectory = Path.GetDirectoryName (inputPath);
			if (inputPathDirectory is null) {
				throw new InvalidOperationException ("Could not determine the Java input directory.");
			}
			Directory.CreateDirectory (inputPathDirectory);
			File.WriteAllText (inputPath, "framework");
			var task = CreateJavaSourceCopyTask (
				inputDir,
				outputDir,
				"com.example.Other -> h:\n",
				"C\tcom/example/Other\n");

			Assert.IsTrue (task.LoadR8JavaSourcePathMapping ());
			var outputs = task.CopyJavaSourcesFromInputDirectory (new [] { new GeneratedJavaSource (relativePath, "") });

			Assert.That (outputs, Has.Exactly (1).Property ("ItemSpec").EqualTo (Path.Combine (outputDir, relativePath)));
			Assert.AreEqual ("framework", File.ReadAllText (Path.Combine (outputDir, relativePath)));
		}

		[Test]
		public void Execute_WritesGeneratedAssembliesListFile ()
		{
			var path = Path.Combine ("temp", TestName);
			var outputDir = Path.Combine (Root, path, "typemap");
			var javaDir = Path.Combine (Root, path, "java");
			var listFile = Path.Combine (outputDir, "typemap-assemblies.txt");
			var staleAssembly = Path.Combine (outputDir, "_Stale.TypeMap.dll");

			var monoAndroidItem = FindMonoAndroidDll ();
			if (monoAndroidItem is null) {
				Assert.Ignore ("Mono.Android.dll not found; skipping.");
				return;
			}

			Directory.CreateDirectory (outputDir);
			File.WriteAllText (staleAssembly, "stale");

			var task = CreateTask (new [] { monoAndroidItem }, outputDir, javaDir);
			task.GeneratedAssembliesListFile = listFile;

			Assert.IsTrue (task.Execute (), "Task should succeed.");

			var generatedAssemblies = task.GeneratedAssemblies.Select (i => i.ItemSpec).ToArray ();
			var listedAssemblies = File.ReadAllLines (listFile);
			CollectionAssert.AreEqual (generatedAssemblies, listedAssemblies);
			CollectionAssert.DoesNotContain (listedAssemblies, staleAssembly);
		}

		[Test]
		public void Execute_GeneratesFrameworkJcws ()
		{
			var path = Path.Combine ("temp", TestName);
			var outputDir = Path.Combine (Root, path, "typemap");
			var javaDir = Path.Combine (Root, path, "java");

			var monoAndroidItem = FindMonoAndroidDll ();
			if (monoAndroidItem is null) {
				Assert.Ignore ("Mono.Android.dll not found; skipping.");
				return;
			}

			var task = CreateTask (new [] { monoAndroidItem }, outputDir, javaDir);
			task.ResolvedFrameworkAssemblies = new [] { monoAndroidItem };

			Assert.IsTrue (task.Execute (), "Task should succeed.");

			var generatedJavaFiles = task.GeneratedJavaFiles.Select (i => i.ItemSpec).ToArray ();
			CollectionAssert.Contains (
				generatedJavaFiles,
				Path.Combine (javaDir, "android/runtime/JavaProxyThrowable.java"));
			CollectionAssert.Contains (
				generatedJavaFiles,
				Path.Combine (javaDir, "xamarin/android/net/ServerCertificateCustomValidator_TrustManager.java"));
			CollectionAssert.Contains (
				generatedJavaFiles,
				Path.Combine (javaDir, "xamarin/android/net/ServerCertificateCustomValidator_TrustManager_FakeSSLSession.java"));
			CollectionAssert.Contains (
				generatedJavaFiles,
				Path.Combine (javaDir, "xamarin/android/net/ServerCertificateCustomValidator_AlwaysAcceptingHostnameVerifier.java"));
			CollectionAssert.DoesNotContain (
				generatedJavaFiles,
				Path.Combine (javaDir, "android/app/Activity.java"));
		}

		[TestCase ("v11.0")]
		[TestCase ("v10.0")]
		[TestCase ("11.0")]
		public void Execute_ParsesTargetFrameworkVersion (string tfv)
		{
			var path = Path.Combine ("temp", TestName);
			var outputDir = Path.Combine (Root, path, "typemap");
			var javaDir = Path.Combine (Root, path, "java");

			var task = CreateTask ([], outputDir, javaDir, tfv: tfv);
			Assert.IsTrue (task.Execute (), $"Task should succeed with TargetFrameworkVersion='{tfv}'.");
		}

		[Test]
		public void Execute_ManifestPlaceholdersAreResolvedForRooting ()
		{
			var path = Path.Combine ("temp", TestName);
			var outputDir = Path.Combine (Root, path, "typemap");
			var javaDir = Path.Combine (Root, path, "java");
			var manifestTemplate = Path.Combine (Root, path, "AndroidManifest.xml");
			var mergedManifest = Path.Combine (Root, path, "obj", "android", "AndroidManifest.xml");
			var applicationRegistration = Path.Combine (Root, path, "src", "net", "dot", "android", "ApplicationRegistration.java");
			var warnings = new List<BuildWarningEventArgs> ();

			var monoAndroidItem = FindMonoAndroidDll ();
			if (monoAndroidItem is null) {
				Assert.Ignore ("Mono.Android.dll not found; skipping.");
				return;
			}

			var manifestDirectory = Path.GetDirectoryName (manifestTemplate);
			if (manifestDirectory is null) {
				Assert.Fail ("Could not determine manifest template directory.");
			}
			Directory.CreateDirectory (manifestDirectory);
			File.WriteAllText (manifestTemplate, """
				<?xml version="1.0" encoding="utf-8"?>
				<manifest xmlns:android="http://schemas.android.com/apk/res/android" package="${applicationId}">
				  <application android:name=".Application" />
				  <instrumentation android:name=".Instrumentation" />
				</manifest>
				""");

			var task = CreateTask (new [] { monoAndroidItem }, outputDir, javaDir, warnings: warnings);
			task.ManifestTemplate = manifestTemplate;
			task.MergedAndroidManifestOutput = mergedManifest;
			task.ApplicationRegistrationOutputFile = applicationRegistration;
			task.PackageName = "android.app";
			task.AndroidApiLevel = "35";
			task.SupportedOSPlatformVersion = "24";
			task.RuntimeProviderJavaName = "mono.MonoRuntimeProvider";
			task.ManifestPlaceholders = "applicationId=android.app";

			Assert.IsTrue (task.Execute (), "Task should succeed.");
			FileAssert.Exists (applicationRegistration);

			var registrationText = File.ReadAllText (applicationRegistration);
			StringAssert.Contains ("mono.android.Runtime.registerNatives (android.app.Application.class);", registrationText);
			StringAssert.Contains ("mono.android.Runtime.registerNatives (android.app.Instrumentation.class);", registrationText);
			StringAssert.DoesNotContain ("android.test.InstrumentationTestRunner.class", registrationText);
			StringAssert.DoesNotContain ("android.test.mock.MockApplication.class", registrationText);
			Assert.IsFalse (warnings.Any (w => w.Code == "XA4250"), "Resolved placeholder-based manifest references should not log XA4250.");
		}

		[Test]
		public void Execute_GenerateNativeAotProguardConfiguration_UsesDgmlTypeMetadata ()
		{
			var path = Path.Combine (Root, "temp", TestName);
			var dgmlFile = Path.Combine (path, "app.scan.dgml.xml");
			var acwMapFile = Path.Combine (path, "acw-map.txt");
			var mappingFile = Path.Combine (path, "mapping.txt");
			var rewriteManifestFile = Path.Combine (path, "r8-jni-rewrite-manifest.txt");
			var reachabilityManifestFile = Path.Combine (path, "r8-jni-reachability-manifest.txt");
			var outputFile = Path.Combine (path, "proguard", "proguard_project_references.cfg");
			Directory.CreateDirectory (path);
			File.WriteAllText (dgmlFile, """
				<?xml version="1.0" encoding="utf-8"?>
				<DirectedGraph xmlns="http://schemas.microsoft.com/vs/2009/dgml">
				  <Nodes>
				    <Node Id="1" Label="Type metadata: [UnnamedProject]UnnamedProject.MainActivity" />
				    <Node Id="2" Label="Type metadata: [Mono.Android]Android.App.Activity" />
				    <Node Id="3" Label="Type metadata: [My.Assembly]Duplicate.Type" />
				    <Node Id="4" Label="Type metadata: [Xamarin.AndroidX.Activity]AndroidX.Activity.Result.Contract.ActivityResultContracts+TakePicture" />
				    <Node Id="5" Label="Unrelated node" />
				  </Nodes>
				  <Links>
				    <Node Id="6" Label="Type metadata: [Other.Assembly]Other.Type" />
				  </Links>
				</DirectedGraph>
				""");
			File.WriteAllText (acwMapFile, """
				UnnamedProject.MainActivity, UnnamedProject;crc64a1.MainActivity
				Android.App.Activity, Mono.Android;android.app.Activity
				Duplicate.Type, My.Assembly;my.app.Duplicate
				AndroidX.Activity.Result.Contract.ActivityResultContracts+TakePicture, Xamarin.AndroidX.Activity;androidx.activity.result.contract.ActivityResultContracts$TakePicture
				Duplicate.Type;wrong.Duplicate
				Other.Type;other.Type
				""");
			File.WriteAllText (mappingFile, """
				crc64a1.MainActivity -> a.a:
				my.app.Duplicate -> a.b:
				androidx.activity.result.contract.ActivityResultContracts$TakePicture -> a.c:
				non.acw.Helper -> a.d:
				    int value -> e
				    void run() -> f
				other.Type -> a.g:
				    void removed() -> h:

				""");
			File.WriteAllText (rewriteManifestFile, """
				C	crc64a1/MainActivity
				C	non/acw/Helper
				C	other/Type
				F	non/acw/Helper	value
				M	non/acw/Helper	run():void
				M	other/Type	removed():void

				""".ReplaceLineEndings ("\n"));

			var task = new GenerateNativeAotProguardConfiguration {
				BuildEngine = new MockBuildEngine (TestContext.Out),
				NativeAotDgmlFiles = new [] { new TaskItem (dgmlFile) },
				AcwMapFile = acwMapFile,
				OutputFile = outputFile,
				R8MappingFile = mappingFile,
				R8RewriteManifestFile = rewriteManifestFile,
				R8ReachabilityManifestFile = reachabilityManifestFile,
				TrimJavaCallableWrappers = true,
			};

			Assert.IsTrue (task.Execute (), "Task should succeed.");
			var proguard = File.ReadAllText (outputFile);
			StringAssert.Contains ("-keep,allowobfuscation class crc64a1.MainActivity", proguard);
			StringAssert.Contains ("-keep,allowobfuscation class my.app.Duplicate", proguard);
			StringAssert.Contains ("-keep,allowobfuscation class androidx.activity.result.contract.ActivityResultContracts$TakePicture", proguard);
			StringAssert.Contains ("-keep,allowobfuscation class non.acw.Helper", proguard);
			StringAssert.Contains ("-keepclassmembers,allowobfuscation class non.acw.Helper", proguard);
			StringAssert.Contains ("*** value;", proguard);
			StringAssert.Contains ("*** run(...);", proguard);
			StringAssert.DoesNotContain ("-keep class crc64a1.MainActivity", proguard);
			StringAssert.DoesNotContain ("other.Type", proguard);
			StringAssert.DoesNotContain ("wrong.Duplicate", proguard);
			CollectionAssert.AreEqual (new [] {
				"C\tandroidx/activity/result/contract/ActivityResultContracts$TakePicture",
				"C\tcrc64a1/MainActivity",
				"C\tmy/app/Duplicate",
				"C\tnon/acw/Helper",
				"F\tnon/acw/Helper\tvalue",
				"M\tnon/acw/Helper\trun():void",
			}, File.ReadAllLines (reachabilityManifestFile));
			StringAssert.DoesNotContain ("\r", File.ReadAllText (reachabilityManifestFile), "R8 JNI manifests should use deterministic LF line endings.");
		}

		[TestCase ("missing-mapping")]
		[TestCase ("malformed-mapping")]
		[TestCase ("missing-rewrite-manifest")]
		[TestCase ("malformed-rewrite-manifest")]
		public void Execute_GenerateNativeAotProguardConfiguration_InvalidR8InputUsesXA4327 (string invalidInput)
		{
			var path = Path.Combine (Root, "temp", TestName);
			var acwMapFile = Path.Combine (path, "acw-map.txt");
			var mappingFile = Path.Combine (path, "mapping.txt");
			var rewriteManifestFile = Path.Combine (path, "r8-jni-rewrite-manifest.txt");
			Directory.CreateDirectory (path);
			File.WriteAllText (acwMapFile, "Managed.Type;managed.Type\n");
			if (invalidInput != "missing-mapping") {
				File.WriteAllText (mappingFile, invalidInput == "malformed-mapping"
					? "not a mapping\n"
					: "managed.Type -> a:\n");
			}
			if (invalidInput != "missing-rewrite-manifest") {
				File.WriteAllText (rewriteManifestFile, invalidInput == "malformed-rewrite-manifest"
					? "not a manifest entry\n"
					: "C\tmanaged/Type\n");
			}
			var errors = new List<BuildErrorEventArgs> ();
			var task = new GenerateNativeAotProguardConfiguration {
				BuildEngine = new MockBuildEngine (TestContext.Out, errors),
				AcwMapFile = acwMapFile,
				OutputFile = Path.Combine (path, "proguard.cfg"),
				R8MappingFile = mappingFile,
				R8RewriteManifestFile = rewriteManifestFile,
				TrimJavaCallableWrappers = false,
			};

			Assert.IsFalse (task.Execute (), "Invalid R8 input should fail the task.");
			Assert.That (errors, Has.Count.EqualTo (1));
			Assert.AreEqual ("XA4327", errors [0].Code);
		}

		[Test]
		public void Execute_GenerateNativeAotProguardConfiguration_KeepsAllWhenTrimmingDisabled ()
		{
			var path = Path.Combine (Root, "temp", TestName);
			var acwMapFile = Path.Combine (path, "acw-map.txt");
			var outputFile = Path.Combine (path, "proguard", "proguard_project_references.cfg");
			Directory.CreateDirectory (path);
			File.WriteAllText (acwMapFile, """
				UnnamedProject.MainActivity, UnnamedProject;crc64a1.MainActivity
				Android.App.Activity, Mono.Android;android.app.Activity
				Duplicate.Type, My.Assembly;my.app.Duplicate
				Other.Type;other.Type
				""");

			// No DGML is provided: with trimming disabled the task must keep every ACW from the map
			// rather than shrinking to the DGML-retained subset.
			var task = new GenerateNativeAotProguardConfiguration {
				BuildEngine = new MockBuildEngine (TestContext.Out),
				AcwMapFile = acwMapFile,
				OutputFile = outputFile,
				TrimJavaCallableWrappers = false,
			};

			Assert.IsTrue (task.Execute (), "Task should succeed without a DGML when trimming is disabled.");
			var proguard = File.ReadAllText (outputFile);
			StringAssert.Contains ("-keep class crc64a1.MainActivity { *; }", proguard);
			StringAssert.Contains ("-keep class android.app.Activity { *; }", proguard);
			StringAssert.Contains ("-keep class my.app.Duplicate { *; }", proguard);
			StringAssert.Contains ("-keep class other.Type { *; }", proguard);
		}

		[Test]
		public void Execute_GenerateNativeAotProguardConfiguration_IgnoresDgmlWhenTrimmingDisabled ()
		{
			var path = Path.Combine (Root, "temp", TestName);
			var dgmlFile = Path.Combine (path, "app.scan.dgml.xml");
			var acwMapFile = Path.Combine (path, "acw-map.txt");
			var outputFile = Path.Combine (path, "proguard", "proguard_project_references.cfg");
			Directory.CreateDirectory (path);
			File.WriteAllText (dgmlFile, """
				<?xml version="1.0" encoding="utf-8"?>
				<DirectedGraph xmlns="http://schemas.microsoft.com/vs/2009/dgml">
				  <Nodes>
				    <Node Id="1" Label="Type metadata: [UnnamedProject]UnnamedProject.MainActivity" />
				    <Node Id="2" Label="Type metadata: [Mono.Android]Android.App.Activity" />
				  </Nodes>
				</DirectedGraph>
				""");
			File.WriteAllText (acwMapFile, """
				UnnamedProject.MainActivity, UnnamedProject;crc64a1.MainActivity
				Android.App.Activity, Mono.Android;android.app.Activity
				Other.Type;other.Type
				""");

			var task = new GenerateNativeAotProguardConfiguration {
				BuildEngine = new MockBuildEngine (TestContext.Out),
				NativeAotDgmlFiles = new [] { new TaskItem (dgmlFile) },
				AcwMapFile = acwMapFile,
				OutputFile = outputFile,
				TrimJavaCallableWrappers = false,
			};

			Assert.IsTrue (task.Execute (), "Task should succeed and ignore the DGML when trimming is disabled.");
			var proguard = File.ReadAllText (outputFile);
			StringAssert.Contains ("-keep class crc64a1.MainActivity { *; }", proguard);
			StringAssert.Contains ("-keep class android.app.Activity { *; }", proguard);
			StringAssert.Contains ("-keep class other.Type { *; }", proguard);
		}

		GenerateTrimmableTypeMap CreateTask (ITaskItem [] assemblies, string outputDir, string javaDir,
			IList<BuildMessageEventArgs>? messages = null, IList<BuildWarningEventArgs>? warnings = null,
			IList<BuildErrorEventArgs>? errors = null, string tfv = "v11.0")
		{
			return new GenerateTrimmableTypeMap {
				BuildEngine = new MockBuildEngine (TestContext.Out, errors: errors, warnings: warnings, messages: messages),
				ResolvedAssemblies = assemblies,
				OutputDirectory = outputDir,
				JavaSourceOutputDirectory = javaDir,
				TargetFrameworkVersion = tfv,
			};
		}

		GenerateTrimmableTypeMap CreateJavaSourceCopyTask (
			string inputDir,
			string outputDir,
			string mapping,
			string manifest,
			IList<BuildErrorEventArgs>? errors = null)
		{
			string? root = Path.GetDirectoryName (inputDir);
			if (root is null) {
				throw new InvalidOperationException ("Could not determine the test root directory.");
			}
			string mappingFile = Path.Combine (root, "mapping.txt");
			string manifestFile = Path.Combine (root, "rewrite-manifest.txt");
			Directory.CreateDirectory (root);
			File.WriteAllText (mappingFile, mapping);
			File.WriteAllText (manifestFile, manifest);
			return new GenerateTrimmableTypeMap {
				BuildEngine = new MockBuildEngine (TestContext.Out, errors: errors),
				ResolvedAssemblies = [],
				OutputDirectory = Path.Combine (root, "typemap"),
				JavaSourceInputDirectory = inputDir,
				JavaSourceOutputDirectory = outputDir,
				R8MappingFile = mappingFile,
				R8RewriteManifestFile = manifestFile,
				TargetFrameworkVersion = "v11.0",
			};
		}

		static ITaskItem? FindMonoAndroidDll ()
		{
			var frameworkDir = TestEnvironment.MonoAndroidFrameworkDirectory;
			if (string.IsNullOrEmpty (frameworkDir) || !Directory.Exists (frameworkDir)) {
				return null;
			}
			var path = Path.Combine (frameworkDir, "Mono.Android.dll");
			if (!File.Exists (path)) {
				return null;
			}
			var item = new TaskItem (path);
			item.SetMetadata ("HasMonoAndroidReference", "True");
			return item;
		}
	}
}
