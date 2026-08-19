using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Microsoft.Build.Framework;
using NUnit.Framework;
using Xamarin.Android.Tasks;

namespace Xamarin.Android.Build.Tests {
	[TestFixture]
	[Parallelizable (ParallelScope.Children)]
	public class GenerateMissingTypeMapStubsTests : BaseTest {

		[Test]
		public void Execute_TrimmedTypeMap_GeneratesLoadableStub ()
		{
			var path = Path.Combine (Root, "temp", TestName);
			var typeMapDir = Path.Combine (path, "typemap");
			var linkedDir = Path.Combine (path, "linked");
			Directory.CreateDirectory (typeMapDir);
			Directory.CreateDirectory (linkedDir);

			// Pre-trim: two per-assembly typemaps plus the root were generated.
			File.WriteAllText (Path.Combine (typeMapDir, "_Kept.TypeMap.dll"), "not-a-real-assembly");
			File.WriteAllText (Path.Combine (typeMapDir, "_Trimmed.TypeMap.dll"), "not-a-real-assembly");
			File.WriteAllText (Path.Combine (typeMapDir, "_Microsoft.Android.TypeMaps.dll"), "not-a-real-assembly");

			// Post-trim (linked/): the root and one typemap survived; _Trimmed was trimmed away.
			File.WriteAllText (Path.Combine (linkedDir, "_Kept.TypeMap.dll"), "survivor");
			File.WriteAllText (Path.Combine (linkedDir, "_Microsoft.Android.TypeMaps.dll"), "survivor-root");

			var task = new GenerateMissingTypeMapStubs {
				BuildEngine = new MockBuildEngine (TestContext.Out),
				TypeMapDirectory = typeMapDir,
				LinkedAssembliesDirectory = linkedDir,
				RootTypeMapAssemblyName = "_Microsoft.Android.TypeMaps",
				TargetFrameworkVersion = "v11.0",
			};

			Assert.IsTrue (task.Execute (), "Task should succeed.");

			// Only the trimmed-away typemap should get a stub; the survivor and the root are left alone.
			var stubs = task.GeneratedStubs.Select (i => i.ItemSpec).ToArray ();
			Assert.AreEqual (1, stubs.Length, "Exactly one stub should be generated.");
			var stubPath = Path.Combine (linkedDir, "_Trimmed.TypeMap.dll");
			CollectionAssert.Contains (stubs, stubPath);
			FileAssert.Exists (stubPath);

			Assert.AreEqual ("survivor", File.ReadAllText (Path.Combine (linkedDir, "_Kept.TypeMap.dll")),
				"A surviving typemap must not be overwritten.");
			Assert.AreEqual ("survivor-root", File.ReadAllText (Path.Combine (linkedDir, "_Microsoft.Android.TypeMaps.dll")),
				"The root typemap must not be overwritten.");

			// The stub must be a valid managed PE assembly named after the trimmed typemap.
			using var stubStream = File.OpenRead (stubPath);
			using var peReader = new PEReader (stubStream);
			Assert.IsTrue (peReader.HasMetadata, "Stub should be a managed PE assembly.");
			var reader = peReader.GetMetadataReader ();
			var assemblyName = reader.GetString (reader.GetAssemblyDefinition ().Name);
			Assert.AreEqual ("_Trimmed.TypeMap", assemblyName, "Stub assembly name should match the trimmed typemap.");
		}

		[Test]
		public void Execute_NothingTrimmed_GeneratesNoStubs ()
		{
			var path = Path.Combine (Root, "temp", TestName);
			var typeMapDir = Path.Combine (path, "typemap");
			var linkedDir = Path.Combine (path, "linked");
			Directory.CreateDirectory (typeMapDir);
			Directory.CreateDirectory (linkedDir);

			File.WriteAllText (Path.Combine (typeMapDir, "_Kept.TypeMap.dll"), "not-a-real-assembly");
			File.WriteAllText (Path.Combine (linkedDir, "_Kept.TypeMap.dll"), "survivor");

			var task = new GenerateMissingTypeMapStubs {
				BuildEngine = new MockBuildEngine (TestContext.Out),
				TypeMapDirectory = typeMapDir,
				LinkedAssembliesDirectory = linkedDir,
				RootTypeMapAssemblyName = "_Microsoft.Android.TypeMaps",
				TargetFrameworkVersion = "v11.0",
			};

			Assert.IsTrue (task.Execute (), "Task should succeed.");
			Assert.IsEmpty (task.GeneratedStubs, "No stubs should be generated when nothing was trimmed.");
		}
	}
}
