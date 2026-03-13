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
			Assert.IsNotNull (task.GeneratedAssemblies);
			Assert.IsEmpty (task.GeneratedAssemblies);
			Assert.IsNotNull (task.GeneratedJavaFiles);
			Assert.IsEmpty (task.GeneratedJavaFiles);
		}

		[Test]
		public void Execute_WithMonoAndroid_ProducesOutputs ()
		{
			var path = Path.Combine ("temp", TestName);
			var outputDir = Path.Combine (Root, path, "typemap");
			var javaDir = Path.Combine (Root, path, "java");

			var monoAndroidPath = FindMonoAndroidDll ();
			if (monoAndroidPath is null) {
				Assert.Ignore ("Mono.Android.dll not found; skipping.");
				return;
			}

			var task = CreateTask (new [] { new TaskItem (monoAndroidPath) }, outputDir, javaDir);

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

			var monoAndroidPath = FindMonoAndroidDll ();
			if (monoAndroidPath is null) {
				Assert.Ignore ("Mono.Android.dll not found; skipping.");
				return;
			}

			var assemblies = new [] { new TaskItem (monoAndroidPath) };

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

			var monoAndroidPath = FindMonoAndroidDll ();
			if (monoAndroidPath is null) {
				Assert.Ignore ("Mono.Android.dll not found; skipping.");
				return;
			}

			// Copy Mono.Android.dll to a temp location so we can touch it
			var tempDir = Path.Combine (Root, path, "assemblies");
			Directory.CreateDirectory (tempDir);
			var tempAssemblyPath = Path.Combine (tempDir, "Mono.Android.dll");
			File.Copy (monoAndroidPath, tempAssemblyPath, true);

			var assemblies = new [] { new TaskItem (tempAssemblyPath) };

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
			var task2 = CreateTask (new [] { new TaskItem (tempAssemblyPath) }, outputDir, javaDir);
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
			Assert.IsNotNull (task.GeneratedAssemblies);
			Assert.IsEmpty (task.GeneratedAssemblies);
			Assert.IsNotNull (task.GeneratedJavaFiles);
			Assert.IsEmpty (task.GeneratedJavaFiles);
			Assert.IsTrue (messages.Any (m => m.Message.Contains ("No Java peer types found")),
				"Should log that no peers were found.");
		}

		GenerateTrimmableTypeMap CreateTask (ITaskItem [] assemblies, string outputDir, string javaDir,
			IList<BuildMessageEventArgs>? messages = null, string tfv = "v11.0")
		{
			return new GenerateTrimmableTypeMap {
				BuildEngine = new MockBuildEngine (TestContext.Out, messages: messages),
				ResolvedAssemblies = assemblies,
				OutputDirectory = outputDir,
				JavaSourceOutputDirectory = javaDir,
				TargetFrameworkVersion = tfv,
			};
		}

		static string? FindMonoAndroidDll ()
		{
			var candidates = new [] {
				Path.Combine (TestEnvironment.DotNetPreviewPacksDirectory, "Microsoft.Android.Ref.35"),
				Path.Combine (TestEnvironment.MonoAndroidFrameworkDirectory ?? ""),
			};

			foreach (var dir in candidates) {
				if (string.IsNullOrEmpty (dir) || !Directory.Exists (dir)) {
					continue;
				}
				var files = Directory.GetFiles (dir, "Mono.Android.dll", SearchOption.AllDirectories);
				if (files.Length > 0) {
					return files [0];
				}
			}
			return null;
		}
	}
}
