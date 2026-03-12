using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NUnit.Framework;
using Xamarin.Android.Tasks;

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

			var task = new GenerateTrimmableTypeMap {
				BuildEngine = new MockBuildEngine (TestContext.Out),
				ResolvedAssemblies = [],
				OutputDirectory = outputDir,
				JavaSourceOutputDirectory = javaDir,
				TargetFrameworkVersion = "v11.0",
			};

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

			// Find a real assembly with [Register] types to scan
			var monoAndroidPath = FindMonoAndroidDll ();
			if (monoAndroidPath is null) {
				Assert.Ignore ("Mono.Android.dll not found; skipping integration-level task test.");
				return;
			}

			var task = new GenerateTrimmableTypeMap {
				BuildEngine = new MockBuildEngine (TestContext.Out),
				ResolvedAssemblies = new [] { new TaskItem (monoAndroidPath) },
				OutputDirectory = outputDir,
				JavaSourceOutputDirectory = javaDir,
				TargetFrameworkVersion = "v11.0",
			};

			Assert.IsTrue (task.Execute (), "Task should succeed.");
			Assert.IsNotNull (task.GeneratedAssemblies);
			Assert.IsNotEmpty (task.GeneratedAssemblies, "Should produce at least one typemap assembly.");

			// Should have per-assembly + root
			var assemblyPaths = task.GeneratedAssemblies.Select (i => i.ItemSpec).ToList ();
			Assert.IsTrue (assemblyPaths.Any (p => p.Contains ("_Microsoft.Android.TypeMaps.dll")),
				"Should produce root _Microsoft.Android.TypeMaps.dll");
			Assert.IsTrue (assemblyPaths.Any (p => p.Contains ("_Mono.Android.TypeMap.dll")),
				"Should produce _Mono.Android.TypeMap.dll");

			// All generated files should exist on disk
			foreach (var assembly in task.GeneratedAssemblies) {
				FileAssert.Exists (assembly.ItemSpec);
			}
		}

		[Test]
		public void Execute_ParsesTargetFrameworkVersion ()
		{
			var path = Path.Combine ("temp", TestName);
			var outputDir = Path.Combine (Root, path, "typemap");
			var javaDir = Path.Combine (Root, path, "java");

			// Test with different TFV formats — all should succeed
			foreach (var tfv in new [] { "v11.0", "v10.0", "11.0" }) {
				var task = new GenerateTrimmableTypeMap {
					BuildEngine = new MockBuildEngine (TestContext.Out),
					ResolvedAssemblies = [],
					OutputDirectory = outputDir,
					JavaSourceOutputDirectory = javaDir,
					TargetFrameworkVersion = tfv,
				};
				Assert.IsTrue (task.Execute (), $"Task should succeed with TargetFrameworkVersion='{tfv}'.");
			}
		}

		static string? FindMonoAndroidDll ()
		{
			// Look in standard locations relative to the test output
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
