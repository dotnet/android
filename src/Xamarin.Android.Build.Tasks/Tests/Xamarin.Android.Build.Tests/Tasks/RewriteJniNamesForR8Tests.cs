using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using Microsoft.Build.Framework;
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

		static byte [] BuildAssemblyWithMalformedLdstrOperand ()
		{
			var fixture = new JniFixtureBuilder ();
			UserStringHandle value = fixture.String ("malformed");
			int fieldStart = fixture.NextFieldRid;
			int methodStart = fixture.NextMethodRid;
			fixture.AddVoidMethod ("Malformed", fixture.EmitLoadStringBody (value));
			fixture.AddType ("Acme", "Malformed", fieldStart, methodStart);

			byte [] image = fixture.Serialize ();
			uint token = (uint) MetadataTokens.GetToken (value);
			byte [] pattern = {
				(byte) ILOpCode.Ldstr,
				(byte) token,
				(byte) (token >> 8),
				(byte) (token >> 16),
				(byte) (token >> 24),
				(byte) ILOpCode.Pop,
				(byte) ILOpCode.Ret,
			};
			int match = -1;
			for (int i = 0; i <= image.Length - pattern.Length; i++) {
				bool matches = true;
				for (int j = 0; j < pattern.Length; j++) {
					if (image [i + j] != pattern [j]) {
						matches = false;
						break;
					}
				}
				if (!matches) {
					continue;
				}
				Assert.AreEqual (-1, match, "The fixture should contain exactly one matching ldstr sequence.");
				match = i;
			}
			Assert.AreNotEqual (-1, match, "The fixture's ldstr sequence was not found.");
			image [match + sizeof (uint)] = 0x71;
			return image;
		}

		static byte [] BuildAssemblyWithLoadedString (string value)
		{
			var fixture = new JniFixtureBuilder ();
			UserStringHandle loadedValue = fixture.String (value);
			int fieldStart = fixture.NextFieldRid;
			int methodStart = fixture.NextMethodRid;
			fixture.AddVoidMethod ("LoadString", fixture.EmitLoadStringBody (loadedValue));
			fixture.AddType ("Acme", "StringLoader", fieldStart, methodStart);
			return fixture.Serialize ();
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
			string rewriteManifest = Path.Combine (path, "destination", "rewrite-manifest.txt");

			var task = new RewriteJniNamesForR8 {
				BuildEngine = new MockBuildEngine (TestContext.Out),
				SourceFiles = new [] { new Microsoft.Build.Utilities.TaskItem (sourceDll) },
				DestinationFiles = new [] { new Microsoft.Build.Utilities.TaskItem (destinationDll) },
				MappingFile = mappingFile,
				RewriteManifestFile = rewriteManifest,
			};

			Assert.IsTrue (task.Execute (), "Task should succeed.");

			FileAssert.Exists (destinationDll);
			FileAssert.Exists (destinationPdb);
			FileAssert.Exists (rewriteManifest);
			Assert.AreEqual ("", File.ReadAllText (rewriteManifest));
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
		public void RemovesStaleDestinationPdbWhenSourceHasNoPdb ()
		{
			string path = Path.Combine (Root, "temp", TestName);
			Directory.CreateDirectory (path);

			string sourceDll = Path.Combine (path, "source", "Test.dll");
			Directory.CreateDirectory (Path.GetDirectoryName (sourceDll));
			File.WriteAllBytes (sourceDll, BuildTrivialAssembly ());

			string destinationDll = Path.Combine (path, "destination", "Test.dll");
			string destinationPdb = Path.ChangeExtension (destinationDll, "pdb");
			Directory.CreateDirectory (Path.GetDirectoryName (destinationDll));
			File.WriteAllBytes (destinationPdb, new byte [] { 1, 2, 3, 4 });

			string mappingFile = Path.Combine (path, "mapping.txt");
			File.WriteAllText (mappingFile, "");
			var task = new RewriteJniNamesForR8 {
				BuildEngine = new MockBuildEngine (TestContext.Out),
				SourceFiles = new [] { new Microsoft.Build.Utilities.TaskItem (sourceDll) },
				DestinationFiles = new [] { new Microsoft.Build.Utilities.TaskItem (destinationDll) },
				MappingFile = mappingFile,
			};

			Assert.IsTrue (task.Execute (), "Task should succeed.");
			FileAssert.Exists (destinationDll);
			FileAssert.DoesNotExist (destinationPdb, "A PDB from a previous copy must not survive when the source PDB is absent.");
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
		public void LeavesInPlaceAssemblyWithIdentityMappingUntouched ()
		{
			string path = Path.Combine (Root, "temp", TestName);
			Directory.CreateDirectory (path);

			string assembly = Path.Combine (path, "Test.dll");
			byte [] content = BuildAssemblyWithLoadedString ("acme/orig/Identity");
			File.WriteAllBytes (assembly, content);
			DateTime originalWriteTime = new DateTime (2020, 1, 2, 3, 4, 5, DateTimeKind.Utc);
			File.SetLastWriteTimeUtc (assembly, originalWriteTime);

			string mappingFile = Path.Combine (path, "mapping.txt");
			File.WriteAllText (mappingFile, "acme.orig.Identity -> acme.orig.Identity:\n");

			var task = new RewriteJniNamesForR8 {
				BuildEngine = new MockBuildEngine (TestContext.Out),
				SourceFiles = new [] { new Microsoft.Build.Utilities.TaskItem (assembly) },
				DestinationFiles = new [] { new Microsoft.Build.Utilities.TaskItem (assembly) },
				MappingFile = mappingFile,
			};

			Assert.IsTrue (task.Execute (), "Task should succeed.");
			CollectionAssert.AreEqual (content, File.ReadAllBytes (assembly));
			Assert.AreEqual (originalWriteTime, File.GetLastWriteTimeUtc (assembly), "An in-place identity mapping must not write the assembly.");
		}

		[Test]
		public void FailsWithACodedErrorWhenSourceAndDestinationCountsDiffer ()
		{
			string path = Path.Combine (Root, "temp", TestName);
			Directory.CreateDirectory (path);
			string mappingFile = Path.Combine (path, "mapping.txt");
			File.WriteAllText (mappingFile, "");

			var errors = new List<BuildErrorEventArgs> ();
			var task = new RewriteJniNamesForR8 {
				BuildEngine = new MockBuildEngine (TestContext.Out, errors),
				SourceFiles = new [] { new Microsoft.Build.Utilities.TaskItem ("a.dll"), new Microsoft.Build.Utilities.TaskItem ("b.dll") },
				DestinationFiles = new [] { new Microsoft.Build.Utilities.TaskItem ("a.dll") },
				MappingFile = mappingFile,
			};

			Assert.IsFalse (task.Execute (), "Task should fail when SourceFiles/DestinationFiles counts differ.");
			Assert.AreEqual (1, errors.Count, "Exactly one error should have been logged.");
			Assert.AreEqual ("XA4325", errors [0].Code, "The error should use the documented XA4325 code.");
			StringAssert.Contains ("SourceFiles", errors [0].Message, "The error should name the mismatched item groups.");
		}

		[Test]
		public void LeavesRewrittenFilesEmptyWhenAnAssemblyCannotBeRewritten ()
		{
			string path = Path.Combine (Root, "temp", TestName);
			Directory.CreateDirectory (path);
			string source = Path.Combine (path, "Malformed.dll");
			string destination = Path.Combine (path, "out", "Malformed.dll");
			File.WriteAllBytes (source, BuildAssemblyWithMalformedLdstrOperand ());

			string mappingFile = Path.Combine (path, "mapping.txt");
			File.WriteAllText (mappingFile, "");
			var errors = new List<BuildErrorEventArgs> ();
			var task = new RewriteJniNamesForR8 {
				BuildEngine = new MockBuildEngine (TestContext.Out, errors),
				SourceFiles = new [] { new Microsoft.Build.Utilities.TaskItem (source) },
				DestinationFiles = new [] { new Microsoft.Build.Utilities.TaskItem (destination) },
				MappingFile = mappingFile,
			};

			Assert.IsFalse (task.Execute (), "Task should fail for malformed IL.");
			Assert.AreEqual (1, errors.Count, "Exactly one error should have been logged.");
			Assert.AreEqual ("XA4325", errors [0].Code);
			StringAssert.Contains ("Malformed IL", errors [0].Message);
			CollectionAssert.IsEmpty (task.RewrittenFiles, "A failed invocation must not publish partial or null output items.");
			FileAssert.DoesNotExist (destination);
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
