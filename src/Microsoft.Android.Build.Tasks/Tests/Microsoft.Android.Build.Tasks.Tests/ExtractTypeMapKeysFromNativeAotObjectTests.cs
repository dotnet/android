using System;
using System.Collections.Generic;
using System.Diagnostics;
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

	[TestCase (true, false)]
	[TestCase (false, false)]
	[TestCase (true, true)]
	[TestCase (false, true)]
	[Platform (Exclude = "Win")]
	public void NativeMetadataToolsBoundOutputAfterRootExit (bool readobj, bool cancel)
	{
		string directory = Path.Combine (Root, "temp", TestName);
		Directory.CreateDirectory (directory);
		string tool = Path.Combine (directory, readobj ? "llvm-readobj" : "llvm-objdump");
		try {
			File.WriteAllText (tool, $"""
				#!/bin/sh
				printf '[]\n'
				echo diagnostic >&2
				{(cancel ? "echo $$ > \"$0.pid\"\nexec sleep 60" : "sleep 60 &\necho $! > \"$0.pid\"")}
				""");
			if (!OperatingSystem.IsWindows ())
				File.SetUnixFileMode (tool, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
			string objectFile = Path.Combine (directory, "map.o");
			string outputFile = Path.Combine (directory, "keys.txt");
			File.WriteAllBytes (objectFile, []);
			File.WriteAllText (outputFile, "existing output");
			var errors = new List<BuildErrorEventArgs> ();
			var task = new NativeCaptureTask {
				BuildEngine = new MockBuildEngine (TestContext.Out, errors),
				LlvmReadObjPath = Path.Combine (directory, "llvm-readobj"),
				NativeObjectFiles = [new TaskItem (objectFile)],
				OutputFile = outputFile,
				CaptureOnly = cancel,
				Readobj = readobj,
			};
			var elapsed = Stopwatch.StartNew ();
			if (cancel) {
				var execution = Task.Run (() => task.Execute ());
				bool started = System.Threading.SpinWait.SpinUntil (() => File.Exists (tool + ".pid"), 5000);
				task.Cancel ();
				execution.WaitAsync (TimeSpan.FromSeconds (5)).GetAwaiter ().GetResult ();
				var capture = task.Operation ?? throw new InvalidOperationException ("The task did not start its asynchronous work.");
				Assert.CatchAsync<OperationCanceledException> (async () => await capture.WaitAsync (TimeSpan.FromSeconds (5)));
				Assert.IsTrue (started, "The tool must start before task cancellation.");
				Assert.Less (elapsed.Elapsed, TimeSpan.FromSeconds (10));
			} else if (readobj) {
				Assert.IsFalse (task.Execute ());
				Assert.That (errors, Has.Count.EqualTo (1));
				Assert.AreEqual ("XA4327", errors [0].Code);
				StringAssert.Contains (objectFile, errors [0].Message);
				StringAssert.Contains ("Timed out draining llvm-readobj output", errors [0].Message);
				Assert.AreEqual ("existing output", File.ReadAllText (outputFile));
			} else {
				Assert.ThrowsAsync<TimeoutException> (async () => await task.CaptureAsync (readobj));
			}
			Assert.Less (elapsed.Elapsed, TimeSpan.FromSeconds (35), "Root exit must start a bounded drain even without MSBuild cancellation.");
		} finally {
			if (File.Exists (tool + ".pid") && Process.TryGetProcessById (int.Parse (File.ReadAllText (tool + ".pid"), System.Globalization.CultureInfo.InvariantCulture), out var descendant)) {
				using (descendant) {
					descendant.Kill ();
					Assert.IsTrue (descendant.WaitForExit (5000), "The fixture descendant must terminate.");
				}
			}
			Directory.Delete (directory, recursive: true);
		}
	}

	sealed class NativeCaptureTask : ExtractTypeMapKeysFromNativeAotObject
	{
		public bool CaptureOnly { get; set; }
		public bool Readobj { get; set; }
		public Task? Operation { get; private set; }

		public override Task RunTaskAsync () => Operation = CaptureOnly ? CaptureAsync (Readobj) : base.RunTaskAsync ();

		public Task<string> CaptureAsync (bool readobj) => readobj ?
			base.ReadObjectMetadataAsync ("map.o") : base.ReadObjectRelocationsAsync ("map.o", ".rodata", 0, 4);
	}

	[TestCase ("Mono_Android_Android_Runtime_JavaDictionary")]
	[TestCase ("Mono_Android_Java_Lang_ObjectExtra")]
	[TestCase ("ThirdParty_TypeMap___TypeMapAnchor")]
	[TestCase ("_Other_TypeMap___TypeMapAnchorExtra")]
	[TestCase ("Mono_Android_0_Android_Runtime_JavaDictionary")]
	[TestCase ("Mono_Android_x_Java_Lang_Object")]
	[TestCase ("Mono_Android_01_Java_Lang_Object")]
	[TestCase ("_Owner_TypeMap_x___TypeMapAnchor")]
	[TestCase ("_Owner_TypeMap_0___TypeMapAnchorExtra")]
	public void MissingRecognizedJavaGroupIsNotAnEmptySuccess (string typeName)
	{
		var fixture = CreateFixture ();
		fixture.AssertSuccess ();
		File.Delete (fixture.OutputFile);

		var (task, errors) = fixture.CreateTask ();
		task.Relocations = fixture.Relocations.Replace (JavaGroup, $"_ZTV{typeName.Length}{typeName}", StringComparison.Ordinal);

		Assert.IsFalse (task.Execute ());
		AssertCodedError (errors, fixture.ObjectFile, "No Java type map group");
		Assert.IsFalse (File.Exists (fixture.OutputFile), "Non-Java groups must not produce an empty key file.");
	}

	[TestCase ("Mono_Android_0_Java_Lang_Object")]
	[TestCase ("Mono_Android_12_Java_Lang_Object")]
	[TestCase ("_Owner_TypeMap_0___TypeMapAnchor")]
	[TestCase ("_Owner_TypeMap_12___TypeMapAnchor")]
	[TestCase ("Mono_Android_0_Java_Lang_Object_1")]
	[TestCase ("_Owner_TypeMap_12___TypeMapAnchor_0")]
	public void DisambiguatedJavaGroupRetainsKeys (string typeName)
	{
		var fixture = CreateFixture ();
		var (task, errors) = fixture.CreateTask ();
		task.Relocations = fixture.Relocations.Replace (JavaGroup, $"_ZTV{typeName.Length}{typeName}", StringComparison.Ordinal);

		Assert.IsTrue (task.Execute ());
		Assert.IsEmpty (errors);
		Assert.AreEqual ("test/Live\n", File.ReadAllText (fixture.OutputFile));
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
