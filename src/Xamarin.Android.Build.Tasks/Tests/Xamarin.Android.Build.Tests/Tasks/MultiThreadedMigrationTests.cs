#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Mono.Cecil;
using NUnit.Framework;
using Xamarin.Android.Tasks;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	public class MultiThreadedMigrationTests
	{
		[TestCase (0)]
		[TestCase (1)]
		[TestCase (2)]
		public void PrepareSatelliteAssembliesPreservesMetadataAndOrder (int abiCount)
		{
			var reference = new TaskItem (Path.Combine ("references", "fr", "Library.resources.dll"));
			reference.SetMetadata ("Culture", "de");
			reference.SetMetadata ("Owner", "reference");
			reference.SetMetadata ("Abi", "original");
			reference.SetMetadata ("DestinationSubPath", "original");
			reference.SetMetadata ("DestinationSubDirectory", "original");
			var intermediate = new TaskItem (Path.Combine ("obj", "fr-CA", "App.resources.dll"));
			intermediate.SetMetadata ("Owner", "intermediate");
			string [] abis = ["arm64-v8a", "x86_64"];
			var task = new PrepareSatelliteAssemblies {
				BuildEngine = new MockBuildEngine (TextWriter.Null),
				BuildTargetAbis = abis.Take (abiCount).ToArray (),
				ReferenceSatellitePaths = [reference, reference],
				IntermediateSatelliteAssemblies = [intermediate],
			};

			Assert.IsTrue (task.Execute ());
			Assert.AreEqual ("de", reference.GetMetadata ("Culture"));
			Assert.AreEqual ("fr-CA", intermediate.GetMetadata ("Culture"));
			Assert.AreEqual ("original", reference.GetMetadata ("Abi"));
			Assert.AreEqual ("original", reference.GetMetadata ("DestinationSubPath"));
			Assert.AreEqual ("original", reference.GetMetadata ("DestinationSubDirectory"));
			Assert.AreEqual ("", intermediate.GetMetadata ("Abi"));
			var output = task.ProcessedSatelliteAssemblies ?? throw new InvalidOperationException ("Missing satellite outputs.");
			Assert.AreEqual (3 * abiCount, output.Length);
			ITaskItem [] inputs = [reference, reference, intermediate];
			for (int i = 0; i < inputs.Length; i++) {
				for (int a = 0; a < abiCount; a++) {
					var item = output [i * abiCount + a];
					var input = inputs [i];
					var culture = input.GetMetadata ("Culture");
					Assert.AreNotSame (input, item);
					Assert.AreEqual (input.ItemSpec, item.ItemSpec);
					Assert.AreEqual (input.GetMetadata ("Owner"), item.GetMetadata ("Owner"));
					Assert.AreEqual (culture, item.GetMetadata ("Culture"));
					Assert.AreEqual (abis [a], item.GetMetadata ("Abi"));
					Assert.AreEqual ($"{abis [a]}/{culture}/{Path.GetFileName (input.ItemSpec)}", item.GetMetadata ("DestinationSubPath"));
					Assert.AreEqual (Path.Combine (abis [a], culture) + Path.DirectorySeparatorChar, item.GetMetadata ("DestinationSubDirectory"));
				}
			}
		}

		[Test]
		public void PrepareSatelliteAssembliesConcurrentProjects ()
		{
			Parallel.For (0, 8, i => {
				var directory = Path.Combine (Path.GetTempPath (), "satellites", i.ToString ());
				var input = new TaskItem (Path.Combine ("obj", "es-MX", $"App{i}.resources.dll"));
				var task = new PrepareSatelliteAssemblies {
					BuildEngine = new MockBuildEngine (TextWriter.Null),
					TaskEnvironment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment (directory, new Dictionary<string, string> ()),
					BuildTargetAbis = ["arm64-v8a"],
					IntermediateSatelliteAssemblies = [input],
				};
				Assert.IsTrue (task.Execute ());
				var output = task.ProcessedSatelliteAssemblies ?? throw new InvalidOperationException ("Missing satellite outputs.");
				Assert.AreEqual (1, output.Length);
				Assert.AreEqual (input.ItemSpec, output [0].ItemSpec);
				Assert.AreEqual ("es-MX", input.GetMetadata ("Culture"));
				Assert.AreEqual ($"arm64-v8a/es-MX/App{i}.resources.dll", output [0].GetMetadata ("DestinationSubPath"));
			});
		}

		[Test]
		public void PrepareSatelliteAssembliesMissingCulture ()
		{
			var errors = new List<BuildErrorEventArgs> ();
			var task = new PrepareSatelliteAssemblies {
				BuildEngine = new MockBuildEngine (TextWriter.Null, errors: errors),
				BuildTargetAbis = ["arm64-v8a"],
				ReferenceSatellitePaths = [new TaskItem ("Library.resources.dll")],
			};
			Assert.IsFalse (task.Execute ());
			Assert.IsNull (task.ProcessedSatelliteAssemblies);
			Assert.AreEqual (1, errors.Count);
			Assert.AreEqual ("XAPSA7009", errors [0].Code);
			StringAssert.Contains ("missing the 'Culture' metadata", errors [0].Message);
		}

		[Test]
		public void ReadLibraryProjectImportsCacheUsesProjectDirectory ()
		{
			var directory = Path.Combine (Path.GetTempPath (), Path.GetRandomFileName ());
			Directory.CreateDirectory (directory);
			try {
				var cacheFile = Path.GetRandomFileName ();
				File.WriteAllText (Path.Combine (directory, cacheFile), "<Paths><Jars><Jar>a.jar</Jar></Jars></Paths>");
				var task = new ReadLibraryProjectImportsCache {
					BuildEngine = new MockBuildEngine (TextWriter.Null),
					TaskEnvironment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment (directory, new Dictionary<string, string> ()),
					CacheFile = cacheFile,
				};
				Assert.IsTrue (task.Execute ());
				Assert.AreEqual ("a.jar", task.Jars?.Single ().ItemSpec);
			} finally {
				Directory.Delete (directory, recursive: true);
			}
		}

		[TestCase ("")]
		[TestCase ("missing.xml")]
		public void ReadLibraryProjectImportsCacheMissingFile (string cacheFile)
		{
			var task = new ReadLibraryProjectImportsCache {
				BuildEngine = new MockBuildEngine (TextWriter.Null),
				TaskEnvironment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment (
					Path.Combine (Path.GetTempPath (), Path.GetRandomFileName ()), new Dictionary<string, string> ()),
				CacheFile = cacheFile,
			};
			Assert.IsTrue (task.Execute ());
			Assert.IsNull (task.Jars);
		}

		[Test]
		public void MigratedTasksHaveDirectAttribute ()
		{
			string [] assemblies = [
				"Microsoft.Android.Build.BaseTasks.dll",
				"Xamarin.Android.Build.Tasks.dll",
				"Xamarin.Android.Build.Debugging.Tasks.dll",
				"Xamarin.Installer.Build.Tasks.dll",
				Path.Combine ("net", "Microsoft.Android.Build.Tasks.dll"),
			];
			using var resolver = new DefaultAssemblyResolver ();
			resolver.AddSearchDirectory (TestEnvironment.AndroidMSBuildDirectory);
			resolver.AddSearchDirectory (Path.Combine (TestEnvironment.AndroidMSBuildDirectory, "net"));
			resolver.AddSearchDirectory (TestContext.CurrentContext.TestDirectory);
			resolver.AddSearchDirectory (RuntimeEnvironment.GetRuntimeDirectory ());
			int migratedTasks = 0;
			foreach (var name in assemblies) {
				using var assembly = AssemblyDefinition.ReadAssembly (
					Path.Combine (TestEnvironment.AndroidMSBuildDirectory, name),
					new ReaderParameters { AssemblyResolver = resolver });
				foreach (var type in assembly.MainModule.GetTypes ()) {
					bool marked = type.CustomAttributes.Any (a => a.AttributeType.FullName == typeof (MSBuildMultiThreadableTaskAttribute).FullName);
					if (type.IsAbstract) {
						Assert.IsFalse (marked, $"{type.FullName} is abstract and must not declare MSBuildMultiThreadableTaskAttribute.");
						continue;
					}
					var baseTypes = GetBaseTypes (type).ToArray ();
					if (baseTypes.Contains (typeof (MultithreadedAndroidTask).FullName)) {
						migratedTasks++;
						Assert.IsTrue (marked, $"{type.FullName} must declare MSBuildMultiThreadableTaskAttribute directly.");
					}
					if (marked) {
						Assert.IsFalse (baseTypes.Contains (typeof (AndroidTask).FullName) ||
							baseTypes.Contains (typeof (AsyncTask).FullName) ||
							baseTypes.Contains (typeof (AndroidToolTask).FullName),
							$"{type.FullName} must not use a base that captures the process working directory.");
					}
				}
			}
			Assert.Greater (migratedTasks, 0, "No migrated tasks were found in the SDK task assemblies.");
		}

		static IEnumerable<string> GetBaseTypes (TypeDefinition type)
		{
			for (TypeReference? baseType = type.BaseType; baseType != null;) {
				yield return baseType.FullName;
				if (baseType.FullName == "System.Object")
					yield break;
				var definition = baseType.Resolve () ?? throw new InvalidOperationException ($"Unable to resolve {baseType.FullName}.");
				baseType = definition.BaseType;
			}
		}
	}
}
