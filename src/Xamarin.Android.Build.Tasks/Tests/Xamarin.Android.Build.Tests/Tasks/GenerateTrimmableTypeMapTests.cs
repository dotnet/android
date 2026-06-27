using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Android.Sdk.TrimmableTypeMap;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Mono.Cecil;
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
		public void Execute_SameInputs_ProducesByteStableAssemblies ()
		{
			var path = Path.Combine ("temp", TestName);
			var firstOutputDir = Path.Combine (Root, path, "first", "typemap");
			var firstJavaDir = Path.Combine (Root, path, "first", "java");
			var secondOutputDir = Path.Combine (Root, path, "second", "typemap");
			var secondJavaDir = Path.Combine (Root, path, "second", "java");

			var monoAndroidItem = FindMonoAndroidDll ();
			if (monoAndroidItem is null) {
				Assert.Ignore ("Mono.Android.dll not found; skipping.");
				return;
			}

			var assemblies = new [] { monoAndroidItem };
			var task1 = CreateTask (assemblies, firstOutputDir, firstJavaDir);
			Assert.IsTrue (task1.Execute (), "First run should succeed.");
			var task2 = CreateTask (assemblies, secondOutputDir, secondJavaDir);
			Assert.IsTrue (task2.Execute (), "Second run should succeed.");

			var firstAssemblies = ReadGeneratedAssemblyBytes (task1.GeneratedAssemblies);
			var secondAssemblies = ReadGeneratedAssemblyBytes (task2.GeneratedAssemblies);

			CollectionAssert.AreEquivalent (firstAssemblies.Keys, secondAssemblies.Keys, "Generated assembly set should be stable.");
			foreach (var name in firstAssemblies.Keys) {
				CollectionAssert.AreEqual (firstAssemblies [name], secondAssemblies [name], $"{name} should be byte-stable for identical inputs.");
			}
		}

		[Test]
		public void RootTypeMapAssembly_SystemRuntimeVersion_ChangesMvid ()
		{
			var first = GenerateRootTypeMapAssembly (new Version (11, 0));
			var second = GenerateRootTypeMapAssembly (new Version (11, 1));

			Assert.AreNotEqual (ReadMvid (first), ReadMvid (second),
				"Root typemap assembly MVID should change when emitted System.Runtime references change.");
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
		public void Execute_MaxArrayRankChange_RewritesGeneratedAssemblies ()
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

			var task1 = CreateTask (assemblies, outputDir, javaDir);
			task1.MaxArrayRank = 0;
			Assert.IsTrue (task1.Execute (), "First run should succeed.");
			Assert.IsFalse (GeneratedAssembliesContainType (task1.GeneratedAssemblies, "__ArrayMapRank1"),
				"MaxArrayRank=0 should not emit array-rank sentinel types.");

			var task2 = CreateTask (assemblies, outputDir, javaDir);
			task2.MaxArrayRank = 1;
			Assert.IsTrue (task2.Execute (), "Second run should succeed.");
			Assert.IsTrue (GeneratedAssembliesContainType (task2.GeneratedAssemblies, "__ArrayMapRank1"),
				"Changing MaxArrayRank should rewrite generated typemap assemblies even when source assemblies did not change.");
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
		public void Execute_DeletesStaleGeneratedJavaSources ()
		{
			var path = Path.Combine ("temp", TestName);
			var outputDir = Path.Combine (Root, path, "typemap");
			var javaDir = Path.Combine (Root, path, "java");
			var staleJavaFile = Path.Combine (javaDir, "stale", "Old.java");
			var staleJavaDirectory = Path.GetDirectoryName (staleJavaFile);

			if (staleJavaDirectory is null) {
				throw new InvalidOperationException ("Could not determine stale Java directory.");
			}
			Directory.CreateDirectory (staleJavaDirectory);
			File.WriteAllText (staleJavaFile, "class Old {}");

			var task = CreateTask ([], outputDir, javaDir);

			Assert.IsTrue (task.Execute (), "Task should succeed.");
			FileAssert.DoesNotExist (staleJavaFile);

			var deletedFile = task.DeletedJavaFiles.SingleOrDefault ();
			Assert.IsNotNull (deletedFile);
			Assert.AreEqual (staleJavaFile, deletedFile.ItemSpec);
			Assert.AreEqual (Path.Combine ("stale", "Old.java"), deletedFile.GetMetadata ("RelativePath"));
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
			task.ManifestPlaceholders = new [] { "applicationId=android.app" };

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
			var outputFile = Path.Combine (path, "proguard", "proguard_project_references.cfg");
			Directory.CreateDirectory (path);
			File.WriteAllText (dgmlFile, """
				<?xml version="1.0" encoding="utf-8"?>
				<DirectedGraph xmlns="http://schemas.microsoft.com/vs/2009/dgml">
				  <Nodes>
				    <Node Id="1" Label="Type metadata: [UnnamedProject]UnnamedProject.MainActivity" />
				    <Node Id="2" Label="Type metadata: [Mono.Android]Android.App.Activity" />
				    <Node Id="3" Label="Type metadata: [My.Assembly]Duplicate.Type" />
				    <Node Id="4" Label="Unrelated node" />
				  </Nodes>
				</DirectedGraph>
				""");
			File.WriteAllText (acwMapFile, """
				UnnamedProject.MainActivity, UnnamedProject;crc64a1.MainActivity
				Android.App.Activity, Mono.Android;android.app.Activity
				Duplicate.Type, My.Assembly;my.app.Duplicate
				Duplicate.Type;wrong.Duplicate
				Other.Type;other.Type
				""");

			var task = new GenerateNativeAotProguardConfiguration {
				BuildEngine = new MockBuildEngine (TestContext.Out),
				NativeAotDgmlFiles = new [] { new TaskItem (dgmlFile) },
				AcwMapFile = acwMapFile,
				OutputFile = outputFile,
			};

			Assert.IsTrue (task.Execute (), "Task should succeed.");
			var proguard = File.ReadAllText (outputFile);
			StringAssert.Contains ("-keep class crc64a1.MainActivity { *; }", proguard);
			StringAssert.Contains ("-keep class android.app.Activity { *; }", proguard);
			StringAssert.Contains ("-keep class my.app.Duplicate { *; }", proguard);
			StringAssert.DoesNotContain ("wrong.Duplicate", proguard);
			StringAssert.DoesNotContain ("other.Type", proguard);
		}

		GenerateTrimmableTypeMap CreateTask (ITaskItem [] assemblies, string outputDir, string javaDir,
			IList<BuildMessageEventArgs>? messages = null, IList<BuildWarningEventArgs>? warnings = null, string tfv = "v11.0")
		{
			return new GenerateTrimmableTypeMap {
				BuildEngine = new MockBuildEngine (TestContext.Out, warnings: warnings, messages: messages),
				ResolvedAssemblies = assemblies,
				OutputDirectory = outputDir,
				JavaSourceOutputDirectory = javaDir,
				TargetFrameworkVersion = tfv,
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

		static bool GeneratedAssembliesContainType (IEnumerable<ITaskItem> assemblies, string typeName)
		{
			foreach (var assemblyPath in assemblies.Select (a => a.ItemSpec)) {
				using var assembly = AssemblyDefinition.ReadAssembly (assemblyPath);
				if (assembly.Modules.SelectMany (m => m.Types).Any (t => t.Name == typeName)) {
					return true;
				}
			}
			return false;
		}

		static Dictionary<string, byte []> ReadGeneratedAssemblyBytes (IEnumerable<ITaskItem> assemblies)
		{
			return assemblies.ToDictionary (
				a => Path.GetFileName (a.ItemSpec),
				a => File.ReadAllBytes (a.ItemSpec),
				StringComparer.Ordinal);
		}

		static byte [] GenerateRootTypeMapAssembly (Version systemRuntimeVersion)
		{
			using var stream = new MemoryStream ();
			var generator = new RootTypeMapAssemblyGenerator (systemRuntimeVersion);
			generator.Generate (new [] { "_Mono.Android.TypeMap" }, useSharedTypemapUniverse: false, stream, maxArrayRank: 3);
			return stream.ToArray ();
		}

		static Guid ReadMvid (byte [] assemblyBytes)
		{
			using var stream = new MemoryStream (assemblyBytes);
			using var assembly = AssemblyDefinition.ReadAssembly (stream);
			return assembly.MainModule.Mvid;
		}
	}
}
