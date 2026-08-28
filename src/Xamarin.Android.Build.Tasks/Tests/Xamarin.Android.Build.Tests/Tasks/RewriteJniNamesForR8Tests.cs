using System;
using System.Collections.Immutable;
using System.IO;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using NUnit.Framework;
using Xamarin.Android.Tasks;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	[Parallelizable (ParallelScope.Children)]
	public class RewriteJniNamesForR8Tests : BaseTest
	{
		static byte [] BuildTrivialAssembly ()
		{
			var metadata = new MetadataBuilder ();
			var il = new BlobBuilder ();

			metadata.AddModule (0, metadata.GetOrAddString ("Fixture.dll"), metadata.GetOrAddGuid (Guid.NewGuid ()), default, default);
			metadata.AddAssembly (metadata.GetOrAddString ("Fixture"), new Version (1, 0, 0, 0), default, default, 0, AssemblyHashAlgorithm.None);
			metadata.AddTypeDefinition (default, default, metadata.GetOrAddString ("<Module>"), default,
				MetadataTokens.FieldDefinitionHandle (1), MetadataTokens.MethodDefinitionHandle (1));

			var peHeaderBuilder = new PEHeaderBuilder (imageCharacteristics: Characteristics.Dll);
			var peBuilder = new ManagedPEBuilder (peHeaderBuilder, new MetadataRootBuilder (metadata), il);
			var peBlob = new BlobBuilder ();
			peBuilder.Serialize (peBlob);

			using var stream = new MemoryStream ();
			peBlob.WriteContentTo (stream);
			return stream.ToArray ();
		}

		[Test]
		public void CopiesSourceToDestinationAndAdjacentPdbUnchanged ()
		{
			string path = Path.Combine (Root, "temp", TestName);
			Directory.CreateDirectory (path);

			string sourceDll = Path.Combine (path, "source", "Test.dll");
			Directory.CreateDirectory (Path.GetDirectoryName (sourceDll));
			File.WriteAllBytes (sourceDll, BuildTrivialAssembly ());
			string sourcePdb = Path.ChangeExtension (sourceDll, "pdb");
			byte [] pdbContent = { 1, 2, 3, 4, 5 };
			File.WriteAllBytes (sourcePdb, pdbContent);

			string mappingFile = Path.Combine (path, "mapping.txt");
			File.WriteAllText (mappingFile, "acme.orig.Unused -> a.b.C:\n");

			string destinationDll = Path.Combine (path, "destination", "nested", "Test.dll");
			string destinationPdb = Path.ChangeExtension (destinationDll, "pdb");

			var task = new RewriteJniNamesForR8 {
				BuildEngine = new MockBuildEngine (TestContext.Out),
				SourceFiles = new [] { new Microsoft.Build.Utilities.TaskItem (sourceDll) },
				DestinationFiles = new [] { new Microsoft.Build.Utilities.TaskItem (destinationDll) },
				MappingFile = mappingFile,
			};

			Assert.IsTrue (task.Execute (), "Task should succeed.");

			FileAssert.Exists (destinationDll);
			FileAssert.Exists (destinationPdb);
			CollectionAssert.AreEqual (File.ReadAllBytes (sourceDll), File.ReadAllBytes (destinationDll), "An assembly with no JNI replacements must remain byte-identical.");
			CollectionAssert.AreEqual (pdbContent, File.ReadAllBytes (destinationPdb), "The adjacent PDB must be copied unchanged.");

			using var sourceReader = new PEReader (ImmutableArray.Create (File.ReadAllBytes (sourceDll)));
			using var peReader = new PEReader (ImmutableArray.Create (File.ReadAllBytes (destinationDll)));
			Assert.IsTrue (peReader.HasMetadata, "The destination must still be a valid managed PE.");

			MetadataReader before = sourceReader.GetMetadataReader ();
			MetadataReader after = peReader.GetMetadataReader ();
			Assert.AreEqual (before.GetGuid (before.GetModuleDefinition ().Mvid), after.GetGuid (after.GetModuleDefinition ().Mvid));
			Assert.AreEqual ("Fixture", after.GetString (after.GetAssemblyDefinition ().Name));
		}

		[Test]
		public void LeavesInPlaceAssemblyWithNoReplacementsUntouched ()
		{
			string path = Path.Combine (Root, "temp", TestName);
			Directory.CreateDirectory (path);

			string assembly = Path.Combine (path, "Test.dll");
			byte [] content = BuildTrivialAssembly ();
			File.WriteAllBytes (assembly, content);
			DateTime originalWriteTime = new DateTime (2020, 1, 2, 3, 4, 5, DateTimeKind.Utc);
			File.SetLastWriteTimeUtc (assembly, originalWriteTime);

			string mappingFile = Path.Combine (path, "mapping.txt");
			File.WriteAllText (mappingFile, "acme.orig.Unused -> a.b.C:\n");

			var task = new RewriteJniNamesForR8 {
				BuildEngine = new MockBuildEngine (TestContext.Out),
				SourceFiles = new [] { new Microsoft.Build.Utilities.TaskItem (assembly) },
				DestinationFiles = new [] { new Microsoft.Build.Utilities.TaskItem (assembly) },
				MappingFile = mappingFile,
			};

			Assert.IsTrue (task.Execute (), "Task should succeed.");
			CollectionAssert.AreEqual (content, File.ReadAllBytes (assembly));
			Assert.AreEqual (originalWriteTime, File.GetLastWriteTimeUtc (assembly), "An in-place no-op must not write the assembly.");
		}

		[Test]
		public void FailsWithACodedErrorWhenSourceAndDestinationCountsDiffer ()
		{
			string path = Path.Combine (Root, "temp", TestName);
			Directory.CreateDirectory (path);
			string mappingFile = Path.Combine (path, "mapping.txt");
			File.WriteAllText (mappingFile, "");

			var task = new RewriteJniNamesForR8 {
				BuildEngine = new MockBuildEngine (TestContext.Out),
				SourceFiles = new [] { new Microsoft.Build.Utilities.TaskItem ("a.dll"), new Microsoft.Build.Utilities.TaskItem ("b.dll") },
				DestinationFiles = new [] { new Microsoft.Build.Utilities.TaskItem ("a.dll") },
				MappingFile = mappingFile,
			};

			Assert.IsFalse (task.Execute (), "Task should fail when SourceFiles/DestinationFiles counts differ.");
		}

		[Test]
		public void HandlesMultipleFilesInOneInvocation ()
		{
			string path = Path.Combine (Root, "temp", TestName);
			Directory.CreateDirectory (path);

			string source1 = Path.Combine (path, "One.dll");
			string source2 = Path.Combine (path, "Two.dll");
			File.WriteAllBytes (source1, BuildTrivialAssembly ());
			File.WriteAllBytes (source2, BuildTrivialAssembly ());

			string mappingFile = Path.Combine (path, "mapping.txt");
			File.WriteAllText (mappingFile, "");

			string destination1 = Path.Combine (path, "out", "One.dll");
			string destination2 = Path.Combine (path, "out", "Two.dll");

			var task = new RewriteJniNamesForR8 {
				BuildEngine = new MockBuildEngine (TestContext.Out),
				SourceFiles = new [] { new Microsoft.Build.Utilities.TaskItem (source1), new Microsoft.Build.Utilities.TaskItem (source2) },
				DestinationFiles = new [] { new Microsoft.Build.Utilities.TaskItem (destination1), new Microsoft.Build.Utilities.TaskItem (destination2) },
				MappingFile = mappingFile,
			};

			Assert.IsTrue (task.Execute ());
			FileAssert.Exists (destination1);
			FileAssert.Exists (destination2);
		}
	}
}
