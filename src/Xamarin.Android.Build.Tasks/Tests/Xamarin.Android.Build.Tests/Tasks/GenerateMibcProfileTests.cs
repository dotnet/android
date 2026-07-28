using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using NUnit.Framework;
using Xamarin.Android.Tasks;

namespace Xamarin.Android.Build.Tests {
	[TestFixture]
	[Parallelizable (ParallelScope.Children)]
	public class GenerateMibcProfileTests : BaseTest {

		static string InputAssembly => typeof (GenerateMibcProfile).Assembly.Location;

		GenerateMibcProfile CreateTask (string outputFile, string? mainAssembly = null) =>
			new GenerateMibcProfile {
				BuildEngine = new MockBuildEngine (TestContext.Out),
				MainAssembly = mainAssembly ?? InputAssembly,
				OutputFile = outputFile,
			};

		[Test]
		public void Execute_WritesZippedManagedPE ()
		{
			var path = Path.Combine (Root, "temp", TestName);
			Directory.CreateDirectory (path);
			var outputFile = Path.Combine (path, "test.mibc");

			var task = CreateTask (outputFile);
			Assert.IsTrue (task.Execute (), "Task should succeed.");

			FileAssert.Exists (outputFile);

			// A MIBC file is a zip archive with a single "<file name>.dll" entry.
			using var archive = ZipFile.OpenRead (outputFile);
			Assert.AreEqual (1, archive.Entries.Count, "Expected a single entry.");
			var entry = archive.Entries [0];
			Assert.AreEqual ("test.mibc.dll", entry.Name);

			var buffer = new MemoryStream ();
			using (var entryStream = entry.Open ())
				entryStream.CopyTo (buffer);
			buffer.Position = 0;

			using var peReader = new PEReader (buffer);
			Assert.IsTrue (peReader.HasMetadata, "The entry should be a managed PE.");
			var reader = peReader.GetMetadataReader ();

			// crossgen2 looks up a global method named "AssemblyDictionary".
			var globalMethods = reader.GetTypeDefinition (reader.TypeDefinitions.First ())
				.GetMethods ()
				.Select (h => reader.GetString (reader.GetMethodDefinition (h).Name))
				.ToArray ();
			CollectionAssert.Contains (globalMethods, "AssemblyDictionary");

			// One group method per assembly, named after the assembly it describes.
			var assemblyName = Path.GetFileNameWithoutExtension (InputAssembly);
			CollectionAssert.Contains (globalMethods, $"Assemblies_{assemblyName}_1");
		}

		[Test]
		public void Execute_IsDeterministic ()
		{
			var path = Path.Combine (Root, "temp", TestName);
			Directory.CreateDirectory (path);
			var outputFile = Path.Combine (path, "test.mibc");

			Assert.IsTrue (CreateTask (outputFile).Execute (), "First run should succeed.");
			var bytes = File.ReadAllBytes (outputFile);

			Assert.IsTrue (CreateTask (outputFile).Execute (), "Second run should succeed.");
			CollectionAssert.AreEqual (bytes, File.ReadAllBytes (outputFile), "Output should be byte-identical.");
		}

		[Test]
		public void Execute_MissingAssembly_WritesNothing ()
		{
			var path = Path.Combine (Root, "temp", TestName);
			Directory.CreateDirectory (path);
			var outputFile = Path.Combine (path, "test.mibc");

			var task = CreateTask (outputFile, mainAssembly: Path.Combine (path, "DoesNotExist.dll"));
			Assert.IsTrue (task.Execute (), "Task should succeed.");

			FileAssert.DoesNotExist (outputFile);
		}
	}
}
