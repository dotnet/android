extern alias xamarinbuildtasks;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Android.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NUnit.Framework;
using xamarinbuildtasks::Xamarin.Android.Tasks.LLVMIR;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Build.Tests;

[TestFixture]
public class ExtractTypeMapKeysFromLlvmIrTests
{
	const string ReleaseSymbol = "java_type_names";
	const string DebugSymbol = "type_map_java_type_names";
	static readonly UTF8Encoding Utf8 = new UTF8Encoding (false, true);
	string directory = "";
	List<BuildErrorEventArgs> errors = new List<BuildErrorEventArgs> ();

	[SetUp]
	public void SetUp ()
	{
		directory = Path.Combine (Path.GetTempPath (), nameof (ExtractTypeMapKeysFromLlvmIrTests), Guid.NewGuid ().ToString ("N"));
		Directory.CreateDirectory (directory);
		errors.Clear ();
	}

	[TearDown]
	public void TearDown ()
	{
		Directory.Delete (directory, recursive: true);
	}

	[Test]
	public void ReadsEmittedBlobs (
		[Values (ReleaseSymbol, DebugSymbol)] string symbol,
		[Values (false, true)] bool comments,
		[Values (AndroidTargetArch.Arm64, AndroidTargetArch.X86_64)] AndroidTargetArch arch,
		[Values (false, true)] bool empty)
	{
		string [] names = empty ? [] : ["test/Outer$Inner", "test/\u017Dlu\u0165ou\u010Dk\u00FD", "android/app/Activity", "test/\U00010400Type", "test/Outer$Inner"];
		string input = WriteEmittedBlob ("typemaps.ll", symbol, names, comments, arch);
		var task = CreateTask (input);

		Assert.IsTrue (task.Execute ());
		Assert.That (errors, Is.Empty);
		AssertOutput (task.OutputFile, names);
	}

	[Test]
	public void UnionsAllAbiFilesAndOverwritesOutput ()
	{
		string first = WriteEmittedBlob ("typemaps.arm64-v8a.ll", ReleaseSymbol, ["z/Last", "test/Outer$Inner"], false, AndroidTargetArch.Arm64);
		string second = WriteEmittedBlob ("typemaps.x86_64.ll", ReleaseSymbol, ["a/First", "test/Outer$Inner"], true, AndroidTargetArch.X86_64);
		var task = CreateTask (first, second, first);
		Assert.IsTrue (task.Execute ());
		AssertOutput (task.OutputFile, ["a/First", "test/Outer$Inner", "z/Last"]);

		DateTime oldTime = new DateTime (2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
		File.SetLastWriteTimeUtc (task.OutputFile, oldTime);
		Assert.IsTrue (task.Execute ());
		Assert.That (File.GetLastWriteTimeUtc (task.OutputFile), Is.GreaterThan (oldTime));
		AssertOutput (task.OutputFile, ["a/First", "test/Outer$Inner", "z/Last"]);

		task.LlvmIrFiles = [new TaskItem (WriteEmittedBlob ("empty.ll", ReleaseSymbol, [], false, AndroidTargetArch.Arm64))];
		Assert.IsTrue (task.Execute ());
		AssertOutput (task.OutputFile, []);
	}

	[Test]
	public void ReadsBlobsAcrossWriterAndReaderBufferBoundaries ()
	{
		string [] names = Enumerable.Range (0, 1024)
			.Select (i => "test/Type" + i.ToString (CultureInfo.InvariantCulture) + "$\u03A9")
			.Concat (["test/" + new string ('A', 4094) + "$\U00010400"])
			.ToArray ();
		var task = CreateTask (WriteEmittedBlob ("large.ll", DebugSymbol, names, false, AndroidTargetArch.Arm64));

		Assert.IsTrue (task.Execute ());
		AssertOutput (task.OutputFile, names);
	}

	[Test]
	public void ReadsLegacyMultilineByteArrays (
		[Values (ReleaseSymbol, DebugSymbol)] string symbol,
		[Values (false, true)] bool comments,
		[Values (false, true)] bool empty,
		[Values (false, true)] bool hexadecimal)
	{
		string [] names = empty ? [] : ["test/Outer$Inner", "test/\u03A9", "a/First"];
		byte [] bytes = Utf8.GetBytes (String.Join ("\0", names) + (empty ? "" : "\0"));
		var ir = new StringBuilder ($"@{symbol} = dso_local local_unnamed_addr constant [{bytes.Length} x i8] [\n");
		for (int i = 0; i < bytes.Length; i++) {
			if (i > 0) {
				ir.Append (",\n");
			}
			if (comments) {
				ir.Append ("; 'not/a/Class' @ 123 ; ] c\"bogus\\00\"\n");
			}
			ir.Append ("i8 ");
			ir.Append (hexadecimal ? "u0x" + bytes [i].ToString ("x2", CultureInfo.InvariantCulture) : bytes [i].ToString (CultureInfo.InvariantCulture));
		}
		ir.Append ("\n], align 16 ; optional trailing comment\n");
		var task = CreateTask (WriteInput (ir.ToString ()));

		Assert.IsTrue (task.Execute ());
		AssertOutput (task.OutputFile, names);
	}

	[Test]
	public void IgnoresUnrelatedGlobalsAndMisleadingComments ()
	{
		string source = """
			; @java_type_names = constant [14 x i8] c"not/a/Class\00", align 1
			@managed_type_names = constant [14 x i8] c"not/a/Class\00", align 1
			@java_type_names_size = constant i64 2, align 8
			@java_type_names_suffix = constant [14 x i8] c"not/a/Class\00", align 1
			@java_type_names = dso_local local_unnamed_addr constant [7 x i8]
			c"a\2FB\24\CE\A9\00", align 8
			""";
		var task = CreateTask (WriteInput (source));
		Assert.IsTrue (task.Execute ());
		AssertOutput (task.OutputFile, ["a/B$\u03A9"]);
	}

	[TestCase ("")]
	[TestCase ("; an LLVM IR file without a supported type map\n")]
	[TestCase ("@type_map = constant [0 x i8] c\"\", align 1\n")]
	[TestCase ("@java_type_names = external constant [0 x i8]\n")]
	[TestCase ("@java_type_names = constant [0 x i16] [], align 1\n")]
	[TestCase ("@java_type_names = constant [-1 x i8] c\"\", align 1\n")]
	[TestCase ("@java_type_names = constant [9223372036854775808 x i8] c\"\", align 1\n")]
	[TestCase ("@java_type_names = constant [0 x i8] zeroinitializer, align 1\n")]
	[TestCase ("@java_type_names = constant [0 x i8] c\"\", align 3\n")]
	[TestCase ("@java_type_names = constant [0 x i8] c\"\", align 1 garbage\n")]
	[TestCase ("@java_type_names = constant [0 x i8] c\"\"")]
	[TestCase ("@java_type_names = constant [0 x i8] c\"\", align 1\n@java_type_names = constant [0 x i8] c\"\", align 1\n")]
	[TestCase ("@java_type_names = constant [0 x i8] c\"\", align 1\n@type_map_java_type_names = constant [0 x i8] c\"\", align 1\n")]
	[TestCase ("@java_type_names = constant [2 x i8] c\"A\\00\", align 1\n@type_map_java_type_names = external constant [0 x i8]\n")]
	[TestCase ("@java_type_names = constant [2 x i8] c\"A\\0")]
	[TestCase ("@java_type_names = constant [2 x i8] c\"A\\zz\", align 1\n")]
	[TestCase ("@java_type_names = constant [2 x i8] c\"A\\0g\", align 1\n")]
	[TestCase ("@java_type_names = constant [2 x i8] c\"AB\", align 1\n")]
	[TestCase ("@java_type_names = constant [3 x i8] c\"A\\00\", align 1\n")]
	[TestCase ("@java_type_names = constant [3 x i8] c\"A\\00\\00\", align 1\n")]
	[TestCase ("@java_type_names = constant [1 x i8] c\"A\\00\", align 1\n")]
	[TestCase ("@java_type_names = constant [2 x i8] c\"\\FF\\00\", align 1\n")]
	[TestCase ("@java_type_names = constant [3 x i8] c\"\\C0\\AF\\00\", align 1\n")]
	[TestCase ("@java_type_names = constant [4 x i8] c\"\\ED\\A0\\80\\00\", align 1\n")]
	[TestCase ("@java_type_names = constant [2 x i8] [i8 u0x41 i8 u0x00], align 1\n")]
	[TestCase ("@java_type_names = constant [2 x i8] [i8 u0x41, i8 u0x00,], align 1\n")]
	[TestCase ("@java_type_names = constant [2 x i8] [i8 u0x41, i8 u0x100], align 1\n")]
	[TestCase ("@java_type_names = constant [2 x i8] [i8 u0x41, i8 256], align 1\n")]
	[TestCase ("@java_type_names = constant [2 x i8] [i8 u0x41, i16 u0x00], align 1\n")]
	[TestCase ("@java_type_names = constant [2 x i8] [i8 u0x41,")]
	public void RejectsUnsupportedOrMalformedInput (string source)
	{
		AssertFails (source);
	}

	[TestCase ("")]
	[TestCase ("test/Type[1]")]
	[TestCase ("[Ltest/Type;")]
	[TestCase ("test.Type")]
	[TestCase ("test//Type")]
	[TestCase ("/Type")]
	[TestCase ("test/")]
	[TestCase ("test/Type\n-keep class **")]
	[TestCase ("test/Type\r")]
	[TestCase ("test/Type\t")]
	[TestCase ("test/*")]
	[TestCase ("test/?")]
	[TestCase ("test/!Type")]
	[TestCase ("test/Type;")]
	[TestCase ("test/Type\"")]
	[TestCase ("test/Type\\")]
	[TestCase ("test/<1>")]
	[TestCase ("test/%")]
	public void RejectsInvalidClassNames (string name)
	{
		var task = CreateTask (WriteEmittedBlob ("invalid.ll", ReleaseSymbol, [name], false, AndroidTargetArch.Arm64));
		Assert.IsFalse (task.Execute ());
		Assert.That (errors.Single ().Code, Is.EqualTo ("XA4327"));
		Assert.That (File.Exists (task.OutputFile), Is.False);
	}

	[Test]
	public void RequiresEverySourceFile ()
	{
		string valid = WriteEmittedBlob ("valid.ll", DebugSymbol, [], false, AndroidTargetArch.Arm64);
		var task = CreateTask (valid, Path.Combine (directory, "missing.ll"));
		Assert.IsFalse (task.Execute ());
		Assert.That (errors.Single ().Code, Is.EqualTo ("XA4327"));
		Assert.That (File.Exists (task.OutputFile), Is.False);
	}

	[Test]
	public void RejectsOneUnsupportedAbiAmongValidInputs ()
	{
		string valid = WriteEmittedBlob ("valid.ll", DebugSymbol, [], false, AndroidTargetArch.Arm64);
		var task = CreateTask (valid, WriteInput ("@unrelated = constant i32 1, align 4\n"));
		Assert.IsFalse (task.Execute ());
		Assert.That (errors.Single ().Code, Is.EqualTo ("XA4327"));
		Assert.That (File.Exists (task.OutputFile), Is.False);
	}

	[Test]
	public void RequiresInputs ()
	{
		var task = CreateTask ();
		Assert.IsFalse (task.Execute ());
		Assert.That (errors.Single ().Code, Is.EqualTo ("XA4327"));
		Assert.That (File.Exists (task.OutputFile), Is.False);
	}

	[TestCase ("")]
	[TestCase (" ")]
	public void RequiresOutput (string output)
	{
		var task = CreateTask (WriteEmittedBlob ("valid.ll", ReleaseSymbol, [], false, AndroidTargetArch.Arm64));
		task.OutputFile = output;
		Assert.IsFalse (task.Execute ());
		Assert.That (errors.Single ().Code, Is.EqualTo ("XA4327"));
	}

	[Test]
	public void ReportsOutputIoErrors ()
	{
		var task = CreateTask (WriteEmittedBlob ("valid.ll", ReleaseSymbol, [], false, AndroidTargetArch.Arm64));
		task.OutputFile = directory;
		Assert.IsFalse (task.Execute ());
		Assert.That (errors.Single ().Code, Is.EqualTo ("XA4327"));
	}

	[Test]
	public void RejectsInvalidSourceEncoding ()
	{
		string input = Path.Combine (directory, "invalid-encoding.ll");
		File.WriteAllBytes (input, [0xff, 0xff]);
		var task = CreateTask (input);
		Assert.IsFalse (task.Execute ());
		Assert.That (errors.Single ().Code, Is.EqualTo ("XA4327"));
	}

	void AssertFails (string source)
	{
		var task = CreateTask (WriteInput (source));
		Assert.IsFalse (task.Execute ());
		Assert.That (errors.Single ().Code, Is.EqualTo ("XA4327"));
		Assert.That (File.Exists (task.OutputFile), Is.False);
	}

	ExtractTypeMapKeysFromLlvmIr CreateTask (params string [] inputs) => new ExtractTypeMapKeysFromLlvmIr {
		BuildEngine = new MockBuildEngine (TestContext.Out, errors),
		LlvmIrFiles = inputs.Select (path => (ITaskItem)new TaskItem (path)).ToArray (),
		OutputFile = Path.Combine (directory, "output", "keys.txt"),
	};

	string WriteInput (string text)
	{
		string path = Path.Combine (directory, "input.ll");
		File.WriteAllText (path, text, Utf8);
		return path;
	}

	string WriteEmittedBlob (string fileName, string symbol, string [] names, bool comments, AndroidTargetArch arch)
	{
		var log = new TaskLoggingHelper (new MockBuildEngine (TestContext.Out), "test");
		var module = new LlvmIrModule (new LlvmIrTypeCache (), log);
		var blob = new LlvmIrStringBlob ();
		foreach (string name in names) {
			blob.Add (name);
		}
		module.AddGlobalVariable (symbol, blob, LlvmIrVariableOptions.GlobalConstant, "Misleading comment: not/a/RetainedClass");
		module.AddGlobalVariable ("managed_type_names", "not/a/RetainedClass");
		module.AfterConstruction ();
		string path = Path.Combine (directory, fileName);
		var generator = LlvmIrGenerator.Create (arch, path);
		generator.EmitComments = comments;
		using var writer = new StreamWriter (path, append: false, Utf8);
		generator.Generate (writer, module);
		return path;
	}

	static void AssertOutput (string path, string [] names)
	{
		string expected = String.Join ("\n", names.Distinct (StringComparer.Ordinal).OrderBy (name => name, StringComparer.Ordinal));
		if (expected.Length > 0) {
			expected += "\n";
		}
		Assert.That (File.Exists (path), Is.True);
		Assert.That (File.ReadAllBytes (path), Is.EqualTo (Utf8.GetBytes (expected)), "Exact UTF-8 without BOM and LF output");
	}
}
