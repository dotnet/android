using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
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
			Assert.IsNull (task.GeneratedAssemblies);
			Assert.IsNull (task.GeneratedJavaFiles);
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
		public void Execute_SecondRun_SkipsUpToDateAssemblies ()
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

			// Wait to ensure timestamp difference is detectable
			Thread.Sleep (100);

			// Second run: same inputs, outputs should be skipped (not rewritten)
			var messages = new List<BuildMessageEventArgs> ();
			var task2 = CreateTask (assemblies, outputDir, javaDir, messages);
			Assert.IsTrue (task2.Execute (), "Second run should succeed.");

			var secondWriteTime = File.GetLastWriteTimeUtc (typeMapPath);
			Assert.AreEqual (firstWriteTime, secondWriteTime,
				"Typemap assembly should NOT be rewritten when source hasn't changed.");

			Assert.IsTrue (messages.Any (m => m.Message.Contains ("up to date")),
				"Should log 'up to date' for skipped assemblies.");
		}

		[Test]
		public void Execute_SourceTouched_RegeneratesOnlyChangedAssembly ()
		{
			var path = Path.Combine ("temp", TestName);
			var outputDir = Path.Combine (Root, path, "typemap");
			var javaDir = Path.Combine (Root, path, "java");

			var monoAndroidItem = FindMonoAndroidDll ();
			if (monoAndroidItem is null) {
				Assert.Ignore ("Mono.Android.dll not found; skipping.");
				return;
			}

			// Copy Mono.Android.dll to a temp location so we can touch it
			var tempDir = Path.Combine (Root, path, "assemblies");
			Directory.CreateDirectory (tempDir);
			var tempAssemblyPath = Path.Combine (tempDir, "Mono.Android.dll");
			File.Copy (monoAndroidItem.ItemSpec, tempAssemblyPath, true);

			var tempItem = new TaskItem (tempAssemblyPath);
			tempItem.SetMetadata ("HasMonoAndroidReference", "True");
			var assemblies = new [] { tempItem };

			// First run
			var task1 = CreateTask (assemblies, outputDir, javaDir);
			Assert.IsTrue (task1.Execute (), "First run should succeed.");

			var typeMapPath = task1.GeneratedAssemblies
				.Select (i => i.ItemSpec)
				.First (p => p.Contains ("_Mono.Android.TypeMap.dll"));
			var firstWriteTime = File.GetLastWriteTimeUtc (typeMapPath);

			// Touch the source assembly to simulate a change
			Thread.Sleep (100);
			File.SetLastWriteTimeUtc (tempAssemblyPath, DateTime.UtcNow);

			// Second run: source is newer → should regenerate
			var tempItem2 = new TaskItem (tempAssemblyPath);
			tempItem2.SetMetadata ("HasMonoAndroidReference", "True");
			var task2 = CreateTask (new [] { tempItem2 }, outputDir, javaDir);
			Assert.IsTrue (task2.Execute (), "Second run should succeed.");

			var secondWriteTime = File.GetLastWriteTimeUtc (typeMapPath);
			Assert.Greater (secondWriteTime, firstWriteTime,
				"Typemap assembly should be regenerated when source is touched.");
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
				AcwMapDirectory = Path.Combine (Root, path, "acw-maps"),
				TargetFrameworkVersion = "not-a-version",
			};

			Assert.IsFalse (task.Execute (), "Task should fail with invalid TargetFrameworkVersion.");
			Assert.IsNotEmpty (errors, "Should have logged an error.");
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
		public void Execute_NoPeersFound_ReturnsEmpty ()
		{
			var path = Path.Combine ("temp", TestName);
			var outputDir = Path.Combine (Root, path, "typemap");
			var javaDir = Path.Combine (Root, path, "java");

			// Use a real assembly that has no [Register] types
			var testAssemblyDir = Path.GetDirectoryName (GetType ().Assembly.Location)!;
			var nunitDll = Path.Combine (testAssemblyDir, "nunit.framework.dll");
			if (!File.Exists (nunitDll)) {
				Assert.Ignore ("nunit.framework.dll not found; skipping.");
				return;
			}

			var messages = new List<BuildMessageEventArgs> ();
			var task = CreateTask (new [] { new TaskItem (nunitDll) }, outputDir, javaDir, messages);

			Assert.IsTrue (task.Execute (), "Task should succeed with no peer types.");
			Assert.IsNull (task.GeneratedAssemblies);
			Assert.IsNull (task.GeneratedJavaFiles);
			Assert.IsTrue (messages.Any (m => m.Message.Contains ("No Java peer types found")),
				"Should log that no peers were found.");
		}

		[Test]
		public void Execute_WithMonoAndroid_PopulatesAcwMap ()
		{
			var path = Path.Combine ("temp", TestName);
			var outputDir = Path.Combine (Root, path, "typemap");
			var javaDir = Path.Combine (Root, path, "java");
			var acwMapFile = Path.Combine (Root, path, "acw-map.txt");

			var monoAndroidItem = FindMonoAndroidDll ();
			if (monoAndroidItem is null) {
				Assert.Ignore ("Mono.Android.dll not found; skipping.");
				return;
			}

			var task = CreateTask (new [] { monoAndroidItem }, outputDir, javaDir);
			task.AcwMapOutputFile = acwMapFile;

			Assert.IsTrue (task.Execute (), "Task should succeed.");
			FileAssert.Exists (acwMapFile);

			var lines = File.ReadAllLines (acwMapFile);
			Assert.IsNotEmpty (lines, "acw-map.txt should not be empty when types are found.");

			// Each type produces 3 lines, so the line count should be a multiple of 3
			Assert.AreEqual (0, lines.Length % 3, "acw-map.txt should have 3 lines per type.");

			// Check that Activity mapping exists (Mono.Android contains Android.App.Activity)
			Assert.IsTrue (lines.Any (l => l.Contains ("Android.App.Activity") && l.Contains ("android.app.Activity")),
				"Should contain Activity mapping.");

			// Verify format: each line should be "key;value"
			foreach (var line in lines) {
				Assert.IsTrue (line.Contains (';'), $"Line should contain ';' separator: {line}");
				var parts = line.Split (';');
				Assert.AreEqual (2, parts.Length, $"Line should have exactly 2 parts: {line}");
				Assert.IsNotEmpty (parts [0], $"Key should not be empty: {line}");
				Assert.IsNotEmpty (parts [1], $"Value should not be empty: {line}");
			}
		}

		[Test]
		public void Execute_EmptyAssemblyList_WritesEmptyAcwMap ()
		{
			var path = Path.Combine ("temp", TestName);
			var outputDir = Path.Combine (Root, path, "typemap");
			var javaDir = Path.Combine (Root, path, "java");
			var acwMapFile = Path.Combine (Root, path, "acw-map.txt");

			var task = CreateTask ([], outputDir, javaDir);
			task.AcwMapOutputFile = acwMapFile;

			Assert.IsTrue (task.Execute (), "Task should succeed.");
			FileAssert.Exists (acwMapFile);
			Assert.IsEmpty (File.ReadAllText (acwMapFile),
				"acw-map.txt should be empty when no peers are found.");
		}

		[Test]
		public void Execute_ManifestReferencedType_IsRootedAsUnconditional ()
		{
			var path = Path.Combine ("temp", TestName);
			var outputDir = Path.Combine (Root, path, "typemap");
			var javaDir = Path.Combine (Root, path, "java");

			var monoAndroidItem = FindMonoAndroidDll ();
			if (monoAndroidItem is null) {
				Assert.Ignore ("Mono.Android.dll not found; skipping.");
				return;
			}

			// Create a manifest template that references a known MCW binding type.
			// android.app.Activity has DoNotGenerateAcw=true so it is normally conditional.
			var manifestDir = Path.Combine (Root, path, "manifest");
			Directory.CreateDirectory (manifestDir);
			var manifestPath = Path.Combine (manifestDir, "AndroidManifest.xml");
			File.WriteAllText (manifestPath, """
				<?xml version="1.0" encoding="utf-8"?>
				<manifest xmlns:android="http://schemas.android.com/apk/res/android" package="com.example.test">
				  <application>
				    <activity android:name="android.app.Activity" />
				  </application>
				</manifest>
				""");

			var messages = new List<BuildMessageEventArgs> ();
			var task = CreateTask (new [] { monoAndroidItem }, outputDir, javaDir, messages);
			task.ManifestTemplate = manifestPath;

			Assert.IsTrue (task.Execute (), "Task should succeed.");
			Assert.IsTrue (messages.Any (m => m.Message.Contains ("Rooting manifest-referenced type")),
				"Should log that a manifest-referenced type was rooted.");
		}

		[Test]
		public void Execute_ManifestReferencedType_NotFound_LogsWarning ()
		{
			var path = Path.Combine ("temp", TestName);
			var outputDir = Path.Combine (Root, path, "typemap");
			var javaDir = Path.Combine (Root, path, "java");

			var monoAndroidItem = FindMonoAndroidDll ();
			if (monoAndroidItem is null) {
				Assert.Ignore ("Mono.Android.dll not found; skipping.");
				return;
			}

			var manifestDir = Path.Combine (Root, path, "manifest");
			Directory.CreateDirectory (manifestDir);
			var manifestPath = Path.Combine (manifestDir, "AndroidManifest.xml");
			File.WriteAllText (manifestPath, """
				<?xml version="1.0" encoding="utf-8"?>
				<manifest xmlns:android="http://schemas.android.com/apk/res/android" package="com.example.test">
				  <application>
				    <activity android:name="com.example.NonExistentActivity" />
				  </application>
				</manifest>
				""");

			var warnings = new List<BuildWarningEventArgs> ();
			var task = new GenerateTrimmableTypeMap {
				BuildEngine = new MockBuildEngine (TestContext.Out, warnings: warnings),
				ResolvedAssemblies = new [] { monoAndroidItem },
				OutputDirectory = outputDir,
				JavaSourceOutputDirectory = javaDir,
				AcwMapDirectory = Path.Combine (outputDir, "..", "acw-maps"),
				TargetFrameworkVersion = "v11.0",
				ManifestTemplate = manifestPath,
			};

			Assert.IsTrue (task.Execute (), "Task should succeed even with unresolved manifest types.");
			Assert.IsTrue (warnings.Any (w => w.Message.Contains ("com.example.NonExistentActivity")),
				"Should warn about unresolved manifest-referenced type.");
		}

		GenerateTrimmableTypeMap CreateTask (ITaskItem [] assemblies, string outputDir, string javaDir,
			IList<BuildMessageEventArgs>? messages = null, string tfv = "v11.0")
		{
			return new GenerateTrimmableTypeMap {
				BuildEngine = new MockBuildEngine (TestContext.Out, messages: messages),
				ResolvedAssemblies = assemblies,
				OutputDirectory = outputDir,
				JavaSourceOutputDirectory = javaDir,
				AcwMapDirectory = Path.Combine (outputDir, "..", "acw-maps"),
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
	}
}
