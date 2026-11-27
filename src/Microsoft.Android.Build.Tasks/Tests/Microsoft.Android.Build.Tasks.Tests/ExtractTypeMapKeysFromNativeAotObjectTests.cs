using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
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

	[TestCase (false)]
	[TestCase (true)]
	public void RelativeObjectAndOutputPathsUseTaskDirectory (bool failWrite)
	{
		var fixture = CreateFixture ();
		var (task, errors) = fixture.CreateTask ();
		var directory = Path.GetDirectoryName (fixture.ObjectFile)
			?? throw new InvalidOperationException ("Missing fixture directory.");
		task.TaskEnvironment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment (directory, new Dictionary<string, string> ());
		task.NativeObjectFiles = [new TaskItem (Path.GetFileName (fixture.ObjectFile))];
		task.OutputFile = Path.Combine ("output", "keys.txt");
		task.FailAfterPartialWrite = failWrite;
		task.YieldDuringToolExecution = true;

		Assert.AreEqual (!failWrite, task.Execute ());
		Assert.AreEqual (failWrite ? 1 : 0, errors.Count);
		var engine = (MockBuildEngine) task.BuildEngine;
		Assert.AreEqual (1, engine.YieldCount);
		Assert.AreEqual (1, engine.ReacquireCount);
		if (!failWrite) {
			Assert.AreEqual ("test/Live\n", File.ReadAllText (fixture.OutputFile));
		}
		Assert.IsEmpty (Directory.GetFiles (fixture.OutputDirectory, "*.tmp"));
	}

	[Test]
	public void ConcurrentToolsUseProjectEnvironments ()
	{
		if (OperatingSystem.IsWindows ()) {
			Assert.Ignore ("The fake LLVM tools are shell scripts.");
			return;
		}

		var first = CreateProcessFixture ("first", "one");
		var second = CreateProcessFixture ("second", "two");
		var executions = Task.WhenAll (Task.Run (first.Task.Execute), Task.Run (second.Task.Execute));

		Assert.IsTrue (executions.Wait (TimeSpan.FromSeconds (10)), "The LLVM tasks did not finish.");
		Assert.That (executions.Result, Is.EqualTo (new [] { true, true }));
		AssertProcessResult (first, "one");
		AssertProcessResult (second, "two");
	}

	[TestCase (false)]
	[TestCase (true)]
	public void AsyncHooksDoNotCaptureCallingSynchronizationContext (bool fail)
	{
		var fixture = CreateFixture ();
		var errors = new List<BuildErrorEventArgs> ();
		var task = new AsyncMetadataTask {
			BuildEngine = new MockBuildEngine (TestContext.Out, errors),
			NativeObjectFiles = [new TaskItem (fixture.ObjectFile)],
			LlvmReadObjPath = Path.Combine (Path.GetDirectoryName (fixture.ObjectFile) ?? throw new InvalidOperationException (), "llvm-readobj-placeholder"),
			OutputFile = fixture.OutputFile,
			Metadata = fixture.Metadata,
			Relocations = fixture.Relocations,
			Fail = fail,
		};
		var previous = SynchronizationContext.Current;
		var context = new SynchronizationContext ();
		try {
			SynchronizationContext.SetSynchronizationContext (context);
			Assert.AreEqual (!fail, task.Execute ());
			Assert.That (errors, Has.Count.EqualTo (fail ? 1 : 0));
			Assert.IsNull (task.MetadataContext);
			Assert.AreSame (context, SynchronizationContext.Current);
		} finally {
			SynchronizationContext.SetSynchronizationContext (previous);
		}
	}

	[Test]
	public void CancellationStopsToolAndReacquires ()
	{
		if (OperatingSystem.IsWindows ()) {
			Assert.Ignore ("The fake LLVM tool is a shell script.");
			return;
		}

		var fixture = CreateFixture ();
		var directory = Path.GetDirectoryName (fixture.ObjectFile)
			?? throw new InvalidOperationException ("Missing fixture directory.");
		string tool = Path.Combine (directory, "llvm-readobj");
		File.WriteAllText (tool, "#!/bin/sh\nprintf started > tool-started\nexec /bin/sleep 30\n");
		File.SetUnixFileMode (tool, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
		var errors = new List<BuildErrorEventArgs> ();
		var engine = new MockBuildEngine (TestContext.Out, errors);
		var task = new ExtractTypeMapKeysFromNativeAotObject {
			BuildEngine = engine,
			TaskEnvironment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment (directory, new Dictionary<string, string> ()),
			NativeObjectFiles = [new TaskItem (fixture.ObjectFile)],
			LlvmReadObjPath = tool,
			OutputFile = fixture.OutputFile,
			YieldDuringToolExecution = true,
		};

		File.Delete (Path.Combine (directory, "tool-started"));
		var execution = Task.Run (task.Execute);
		bool started = SpinWait.SpinUntil (() => File.Exists (Path.Combine (directory, "tool-started")), TimeSpan.FromSeconds (10));
		task.Cancel ();

		Assert.IsTrue (execution.Wait (TimeSpan.FromSeconds (10)), "The cancelled task did not finish.");
		Assert.IsTrue (started, "The LLVM tool did not start.");
		Assert.IsFalse (execution.Result);
		Assert.That (errors, Has.Count.EqualTo (1));
		Assert.IsFalse (File.Exists (fixture.OutputFile));
		Assert.AreEqual (1, engine.YieldCount);
		Assert.AreEqual (1, engine.ReacquireCount);
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

	(Fixture Fixture, ExtractTypeMapKeysFromNativeAotObject Task, List<BuildErrorEventArgs> Errors, string Directory) CreateProcessFixture (
		string name, string value)
	{
		string directory = Path.Combine (Root, "temp", TestName, name);
		var fixture = new Fixture (directory);
		File.WriteAllText (Path.Combine (directory, "metadata.json"),
			fixture.Metadata.Replace ("\"Index\":1,", "\"Index\":1,\"Flags\":{\"Value\":0},", StringComparison.Ordinal));
		File.WriteAllText (Path.Combine (directory, "relocations.txt"), fixture.Relocations);
		using var metadata = JsonDocument.Parse (fixture.Metadata);
		var symbols = metadata.RootElement [0].GetProperty ("Symbols");
		File.WriteAllText (Path.Combine (directory, "symbols.txt"), SymbolLine (0) + "\n" + SymbolLine (1) + "\n");
		File.Delete (Path.Combine (directory, "readobj-environment"));
		File.Delete (Path.Combine (directory, "objdump-environment"));

		WriteTool ("llvm-readobj", """
			#!/bin/sh
			printf '%s\n%s\n' "$PWD" "$TASK_VALUE" > readobj-environment
			/bin/cat metadata.json
			""");
		WriteTool ("llvm-objdump", """
			#!/bin/sh
			printf '%s\n%s\n' "$PWD" "$TASK_VALUE" >> objdump-environment
			case "$1" in
				--syms) /bin/cat symbols.txt ;;
				--reloc) /bin/cat relocations.txt ;;
				*) exit 1 ;;
			esac
			""");

		var errors = new List<BuildErrorEventArgs> ();
		var task = new ExtractTypeMapKeysFromNativeAotObject {
			BuildEngine = new MockBuildEngine (TestContext.Out, errors),
			TaskEnvironment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment (directory, new Dictionary<string, string> { ["TASK_VALUE"] = value }),
			NativeObjectFiles = [new TaskItem ("map.o")],
			LlvmReadObjPath = Path.Combine (directory, "llvm-readobj"),
			OutputFile = Path.Combine ("output", "keys.txt"),
		};
		return (fixture, task, errors, directory);

		string SymbolLine (int index)
		{
			var symbol = symbols [index].GetProperty ("Symbol");
			return $"{symbol.GetProperty ("Value").GetInt64 ():x16} g O .rodata {symbol.GetProperty ("Size").GetInt64 ():x16} {symbol.GetProperty ("Name").GetProperty ("Name").GetString ()}";
		}

		void WriteTool (string tool, string script)
		{
			string path = Path.Combine (directory, tool);
			File.WriteAllText (path, script);
			if (OperatingSystem.IsWindows ()) {
				throw new PlatformNotSupportedException ("The fake LLVM tools are shell scripts.");
			}
			File.SetUnixFileMode (path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
		}
	}

	static void AssertProcessResult (
		(Fixture Fixture, ExtractTypeMapKeysFromNativeAotObject Task, List<BuildErrorEventArgs> Errors, string Directory) fixture, string value)
	{
		Assert.IsEmpty (fixture.Errors);
		Assert.AreEqual ("test/Live\n", File.ReadAllText (fixture.Fixture.OutputFile));
		Assert.That (File.ReadAllLines (Path.Combine (fixture.Directory, "readobj-environment")), Is.EqualTo (new [] { fixture.Directory, value }));
		Assert.That (File.ReadAllLines (Path.Combine (fixture.Directory, "objdump-environment")),
			Is.EqualTo (new [] { fixture.Directory, value, fixture.Directory, value }));
	}

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

	sealed class AsyncMetadataTask : ExtractTypeMapKeysFromNativeAotObject
	{
		public string Metadata { get; set; } = "";
		public string Relocations { get; set; } = "";
		public bool Fail { get; set; }
		public SynchronizationContext? MetadataContext { get; private set; }

		protected override async Task<string> ReadObjectMetadataAsync (string objectFile)
		{
			MetadataContext = SynchronizationContext.Current;
			await Task.Yield ();
			if (Fail) {
				throw new IOException ("Failed to read object metadata.");
			}
			return Metadata;
		}

		protected override Task<string> ReadObjectRelocationsAsync (string objectFile, string section, long start, long end) =>
			Task.FromResult (Relocations);
	}
}
