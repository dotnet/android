using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Android.Tasks;
using Microsoft.Build.Framework;
using NUnit.Framework;
using TaskItem = Microsoft.Build.Utilities.TaskItem;

namespace Xamarin.Android.Build.Tests;

[TestFixture]
[Parallelizable (ParallelScope.Children)]
public class ExtractTypeMapKeysFromNativeAotObjectTests : BaseTest
{
	const string JavaGroup = "_ZTV29Mono_Android_Java_Lang_Object";

	[TestCase ("_ZTV43Mono_Android_Android_Runtime_JavaDictionary")]
	[TestCase ("_ZTV29Mono_Android_Java_Lang_ObjectExtra")]
	[TestCase ("_ZTV30ThirdParty_TypeMap___TypeMapAnchor")]
	[TestCase ("_ZTV30_Other_TypeMap___TypeMapAnchorExtra")]
	public void MissingRecognizedJavaGroupIsNotAnEmptySuccess (string groupSymbol)
	{
		var fixture = CreateFixture ();
		fixture.AssertSuccess ();
		File.Delete (fixture.OutputFile);

		var (task, errors) = fixture.CreateTask ();
		task.Relocations = fixture.Relocations.Replace (JavaGroup, groupSymbol, StringComparison.Ordinal);

		Assert.IsFalse (task.Execute ());
		AssertCodedError (errors, fixture.ObjectFile, "No Java type map group");
		Assert.IsFalse (File.Exists (fixture.OutputFile), "Non-Java groups must not produce an empty key file.");
	}

	[TestCase ("", TestName = "MalformedToolJsonFailsWithFileContext_Empty")]
	[TestCase ("not JSON", TestName = "MalformedToolJsonFailsWithFileContext_InvalidJson")]
	[TestCase ("{}", TestName = "MalformedToolJsonFailsWithFileContext_Object")]
	[TestCase ("[]", TestName = "MalformedToolJsonFailsWithFileContext_EmptyArray")]
	[TestCase ("[{},{}]", TestName = "MalformedToolJsonFailsWithFileContext_MultipleObjects")]
	[TestCase ("[null]", TestName = "MalformedToolJsonFailsWithFileContext_Null")]
	[TestCase ("[{}]", TestName = "MalformedToolJsonFailsWithFileContext_MissingMetadata")]
	public void MalformedToolJsonFailsWithFileContext (string json)
	{
		var fixture = CreateFixture ();
		fixture.AssertSuccess ();
		File.Delete (fixture.OutputFile);

		var (task, errors) = fixture.CreateTask ();
		task.Metadata = json;

		Assert.IsFalse (task.Execute ());
		AssertCodedError (errors, fixture.ObjectFile);
		Assert.IsFalse (File.Exists (fixture.OutputFile), "Invalid tool output must not produce a partial key file.");
	}

	[Test]
	public void OutputWriteFailurePreservesExistingOutputAndRemovesTemporaryFile ()
	{
		var fixture = CreateFixture ();
		fixture.AssertSuccess ();

		var (task, errors) = fixture.CreateTask ();
		task.FailAfterPartialWrite = true;

		Assert.IsFalse (task.Execute ());
		AssertCodedError (errors, fixture.OutputFile, "Could not finish writing output.");
		Assert.AreEqual ("test/Live\n", File.ReadAllText (fixture.OutputFile));
		Assert.That (task.PartialOutputFile, Is.Not.Null.And.Not.EqualTo (fixture.OutputFile));
		Assert.IsFalse (File.Exists (task.PartialOutputFile));
		Assert.IsEmpty (Directory.GetFiles (fixture.OutputDirectory, "*.tmp"));
	}

	Fixture CreateFixture () => new (Path.Combine (Root, "temp", TestName));

	static void AssertCodedError (IList<BuildErrorEventArgs> errors, string file, string? reason = null)
	{
		Assert.That (errors, Has.Count.EqualTo (1));
		Assert.That (errors [0].Code, Is.EqualTo ("XA4327"));
		StringAssert.Contains (file, errors [0].Message);
		if (reason != null) {
			StringAssert.Contains (reason, errors [0].Message);
		}
	}

	sealed class Fixture
	{
		const int SectionOffset = 128;
		readonly string toolPath;

		public string ObjectFile { get; }
		public string OutputDirectory { get; }
		public string OutputFile { get; }
		public string Metadata { get; }
		public string Relocations { get; }

		public Fixture (string directory)
		{
			Directory.CreateDirectory (directory);
			ObjectFile = Path.Combine (directory, "map.o");
			toolPath = Path.Combine (directory, "llvm-readobj-placeholder");
			OutputDirectory = Path.Combine (directory, "output");
			OutputFile = Path.Combine (OutputDirectory, "keys.txt");

			// One NativeFormat group (fixup index 0) containing the key "test/Live".
			byte [] blob = Convert.FromHexString ("000208000f050000000002000208000f0500000012746573742f4c69766500");
			int sectionSize = blob.Length + 64;
			int fixupsOffset = blob.Length + 16;
			byte [] bytes = new byte [SectionOffset + sectionSize];
			blob.CopyTo (bytes, SectionOffset);
			File.WriteAllBytes (ObjectFile, bytes);
			File.WriteAllBytes (toolPath, []);

			Metadata = $$$$"""
				[{
				  "FileSummary":{"Format":"elf64-littleaarch64","Arch":"aarch64"},
				  "ElfHeader":{"Type":"Relocatable (0x1)","Ident":{"DataEncoding":{"Value":1}}},
				  "Sections":[
				    {"Section":{"Index":1,"Name":{"Name":".rodata"},"Type":{"Name":"SHT_PROGBITS"},"Offset":{{{{SectionOffset}}}},"Size":{{{{sectionSize}}}}}},
				    {"Section":{"Index":2,"Name":{"Name":".rela.rodata"},"Type":{"Name":"SHT_RELA"},"Info":1}}
				  ],
				  "Symbols":[
				    {"Symbol":{"Name":{"Name":"__external_type_map__"},"Value":0,"Size":{{{{blob.Length}}}},"Section":{"Value":1}}},
				    {"Symbol":{"Name":{"Name":"__external_CommonFixupsTable_references"},"Value":{{{{fixupsOffset}}}},"Size":4,"Section":{"Value":1}}}
				  ]
				}]
				""";
			Relocations = $"{fixupsOffset:x16} R_AARCH64_PREL32 {JavaGroup}\n";
		}

		public (MetadataTask Task, List<BuildErrorEventArgs> Errors) CreateTask ()
		{
			var errors = new List<BuildErrorEventArgs> ();
			return (new MetadataTask {
				BuildEngine = new MockBuildEngine (TestContext.Out, errors),
				NativeObjectFiles = [new TaskItem (ObjectFile)],
				LlvmReadObjPath = toolPath,
				OutputFile = OutputFile,
				Metadata = Metadata,
				Relocations = Relocations,
			}, errors);
		}

		public void AssertSuccess ()
		{
			var (task, errors) = CreateTask ();
			Assert.IsTrue (task.Execute (), "The valid fixture must extract a Java group.");
			Assert.IsEmpty (errors);
			Assert.AreEqual ("test/Live\n", File.ReadAllText (OutputFile));
		}
	}

	sealed class MetadataTask : ExtractTypeMapKeysFromNativeAotObject
	{
		public string Metadata { get; set; } = "";
		public string Relocations { get; set; } = "";
		public bool FailAfterPartialWrite { get; set; }
		public string? PartialOutputFile { get; private set; }

		protected override Task<string> ReadObjectMetadataAsync (string objectFile) => Task.FromResult (Metadata);

		protected override Task<string> ReadObjectRelocationsAsync (string objectFile, string section, long start, long end) =>
			Task.FromResult (Relocations);

		protected override void WriteOutputFile (string outputFile, IReadOnlyCollection<string> keys)
		{
			if (FailAfterPartialWrite) {
				PartialOutputFile = outputFile;
				File.WriteAllText (outputFile, "partial");
				throw new IOException ("Could not finish writing output.");
			}
			base.WriteOutputFile (outputFile, keys);
		}
	}
}
