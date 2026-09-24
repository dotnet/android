using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.Android.Tasks;
using Xunit;
using TaskItem = Microsoft.Build.Utilities.TaskItem;

namespace Microsoft.Android.Sdk.TrimmableTypeMap.Tests;

public class ExtractTypeMapKeysFromNativeAotObjectTests : IDisposable
{
	const int SectionOffset = 128;
	const int SymbolOffset = 17;
	readonly string directory = Path.Combine (AppContext.BaseDirectory, nameof (ExtractTypeMapKeysFromNativeAotObjectTests), Guid.NewGuid ().ToString ("N"));
	readonly Dictionary<string, JsonArray> metadata = new (StringComparer.Ordinal);

	public ExtractTypeMapKeysFromNativeAotObjectTests ()
	{
		Directory.CreateDirectory (directory);
		File.WriteAllBytes (Path.Combine (directory, "llvm-readobj-placeholder"), []);
	}

	public void Dispose () => Directory.Delete (directory, recursive: true);

	[Fact]
	public void TaskLivesInModernBuildTasksAssembly ()
	{
		Assert.Equal ("Microsoft.Android.Tasks.ExtractTypeMapKeysFromNativeAotObject", typeof (ExtractTypeMapKeysFromNativeAotObject).FullName);
		Assert.Equal ("Microsoft.Android.Build.Tasks", typeof (ExtractTypeMapKeysFromNativeAotObject).Assembly.GetName ().Name);
	}

	[Fact]
	public void CapturedIlcArm64BlobContainsOnlyRetainedCanonicalKeys ()
	{
		// Microsoft.DotNet.ILCompiler 11.0.0-rc.2.26461.115, Android ARM64, optimized with --scanreflection.
		byte [] blob = Convert.FromBase64String ("AAIFXbUBGnNhbXBsZS9BbHdheXMEFnNhbXBsZS9MaXZlAkJzYW1wbGUvTGl2ZVsxMjM0NTY3ODkwMTIzNDU2Nzg5MF0CNGNvbS/DqXhhbXBsZS9QZWVyzpQkTmVzdGVkBBxzYW1wbGUvTGl2ZVswXQIAAgACD06t/lxt/qPIpx3+zIY=");
		var (task, engine) = CreateTask (WriteObject ("captured-ilc-arm64", blob));

		Assert.True (task.Execute ());
		Assert.Empty (engine.Errors);
		byte [] expected = new UTF8Encoding (false).GetBytes ("com/\u00e9xample/Peer\u0394$Nested\nsample/Always\nsample/Live\n");
		Assert.Equal (expected, File.ReadAllBytes (task.OutputFile));
	}

	[Fact]
	public void ExtractsOnlySymbolPayloadAndUnionsEveryJavaGroupAndObject ()
	{
		string arm64 = WriteObject ("arm64", NativeAotObjectTestFixture.CreateGroups (
			["test/Zebra", "test/Outer$Inner", "test/Alias[0]", "test/\u00e9clair"],
			["test/FromOtherGroup", "test/Alias[1]", "test/\U00010400Peer"]));
		string x64 = WriteObject ("x64", NativeAotObjectTestFixture.CreateGroups (
			["test/Alpha", "test/Alias", "test/Outer$Inner"],
			["test/A\u0301", "test/a", "test/\u2160Peer"]));
		Summary (x64) ["Format"] = "elf64-x86-64";
		Summary (x64) ["Arch"] = "x86_64";
		Symbol (arm64) ["Name"] = new JsonObject { ["Name"] = "Compilation_123___external_type_map__", ["Value"] = 42 };
		var (task, engine) = CreateTask (arm64, x64, arm64);
		Directory.CreateDirectory (Path.GetDirectoryName (task.OutputFile) ?? throw new InvalidOperationException ());
		File.WriteAllText (task.OutputFile, "stale/sentinel\n");

		Assert.True (task.Execute ());
		Assert.Empty (engine.Errors);
		byte [] expected = new UTF8Encoding (false).GetBytes (
			"test/Alias\ntest/Alpha\ntest/A\u0301\ntest/FromOtherGroup\ntest/Outer$Inner\ntest/Zebra\ntest/a\ntest/\u00e9clair\ntest/\u2160Peer\ntest/\U00010400Peer\n");
		Assert.Equal (expected, File.ReadAllBytes (task.OutputFile));

		File.SetLastWriteTimeUtc (task.OutputFile, new DateTime (2000, 1, 1));
		var (reordered, reorderedEngine) = CreateTask (x64, arm64);
		Assert.True (reordered.Execute ());
		Assert.Empty (reorderedEngine.Errors);
		Assert.Equal (expected, File.ReadAllBytes (reordered.OutputFile));
		Assert.True (File.GetLastWriteTimeUtc (reordered.OutputFile).Year > 2000);
	}

	[Theory]
	[InlineData ("elf32-littlearm", "arm", "32bit")]
	[InlineData ("elf64-littleaarch64", "aarch64", "64bit")]
	[InlineData ("elf64-x86-64", "x86_64", "64bit")]
	[InlineData ("elf32-i386", "i386", "32bit")]
	public void AcceptsSupportedRelocatableElfArchitectures (string format, string arch, string addressSize)
	{
		string path = WriteObject ("map", NativeAotObjectTestFixture.CreateBlob ("test/Live"));
		Summary (path) ["Format"] = format;
		Summary (path) ["Arch"] = arch;
		Summary (path) ["AddressSize"] = addressSize;
		var (task, engine) = CreateTask (path);

		Assert.True (task.Execute ());
		Assert.Empty (engine.Errors);
		Assert.Equal ("test/Live\n", File.ReadAllText (task.OutputFile));
	}

	[Theory]
	[InlineData ("test/A[123]", "test/A")]
	[InlineData ("test/A[000]", "test/A")]
	[InlineData ("test/A[9999999999999999999999999999999]", "test/A")]
	[InlineData ("test/Outer$Inner[2]", "test/Outer$Inner")]
	[InlineData ("test/\u2160[1]", "test/\u2160")]
	public void RemovesOneTerminalNonemptyAsciiDecimalAlias (string key, string expected)
	{
		var (task, engine) = CreateTask (WriteObject ("map", NativeAotObjectTestFixture.CreateBlob (key)));

		Assert.True (task.Execute ());
		Assert.Empty (engine.Errors);
		Assert.Equal (new UTF8Encoding (false).GetBytes (expected + "\n"), File.ReadAllBytes (task.OutputFile));
	}

	[Fact]
	public void ObjectArraysContributeOnlyCanonicalElementClasses ()
	{
		byte [] blob = NativeAotObjectTestFixture.CreateBlob (
			"[Ljava/lang/Object;", "[[Ltest/Outer$Inner;", "[Ltest/Outer$Inner;[000]",
			"test/Outer$Inner", "[I[0]");
		var (task, engine) = CreateTask (WriteObject ("arrays", blob));

		Assert.True (task.Execute ());
		Assert.Empty (engine.Errors);
		Assert.Equal ("java/lang/Object\ntest/Outer$Inner\n", File.ReadAllText (task.OutputFile));
	}

	[Fact]
	public void PrimitiveArraysContributeNoClasses ()
	{
		byte [] blob = NativeAotObjectTestFixture.CreateBlob ("[Z", "[B", "[C", "[S", "[I", "[J", "[F", "[D", "[[I", "[[[D");
		var (task, engine) = CreateTask (WriteObject ("primitive-arrays", blob));

		Assert.True (task.Execute ());
		Assert.Empty (engine.Errors);
		Assert.True (File.Exists (task.OutputFile));
		Assert.Empty (File.ReadAllBytes (task.OutputFile));
	}

	[Fact]
	public void SelectsJavaGroupsByFixupsRatherThanKeyAppearance ()
	{
		string path = WriteObject ("mixed-universes", NativeAotObjectTestFixture.CreateGroups (
			["System.Collections.Generic.IDictionary`2[System.Char,System.Int32]", "foreign/LooksLikeJava"],
			["test/Shared", "test/Alias[0]"],
			["test/PerAssembly", "test/Alias[1]"]));
		var (task, engine) = CreateTask (path);
		task.UseGroupMetadata = true;
		task.RelocationOutput = AddGroupMetadata (path,
			"_ZTV43Mono_Android_Android_Runtime_JavaDictionary",
			"_ZTV29Mono_Android_Java_Lang_Object",
			"_ZTV37_Mono_Android_TypeMap___TypeMapAnchor");

		Assert.True (task.Execute ());
		Assert.Empty (engine.Errors);
		Assert.Equal ("test/Alias\ntest/PerAssembly\ntest/Shared\n", File.ReadAllText (task.OutputFile));
		Assert.Equal (".rela.rodata", task.RelocationSection);
		Assert.Equal (12, task.RelocationEnd - task.RelocationStart);
	}

	[Theory]
	[InlineData ("aarch64", "R_AARCH64_PREL32", "SHT_RELA", ".rela.rodata")]
	[InlineData ("arm", "R_ARM_REL32", "SHT_REL", ".rel.rodata")]
	[InlineData ("x86_64", "R_X86_64_PC32", "SHT_RELA", ".rela.rodata")]
	[InlineData ("i386", "R_386_PC32", "SHT_REL", ".rel.rodata")]
	public void ResolvesGroupSlotsUsingTargetRelocationKind (string arch, string kind, string sectionType, string sectionName)
	{
		string path = WriteObject ("abi-group", NativeAotObjectTestFixture.CreateBlob ("test/Live"));
		Summary (path) ["Arch"] = arch;
		var (task, engine) = CreateTask (path);
		task.UseGroupMetadata = true;
		task.RelocationOutput = AddGroupMetadata (path, "_ZTV29Mono_Android_Java_Lang_Object")
			.Replace ("R_AARCH64_PREL32", kind, StringComparison.Ordinal);
		var relocationSection = RelocationSection (path);
		relocationSection ["Name"] = new JsonObject { ["Name"] = sectionName, ["Value"] = 0 };
		relocationSection ["Type"] = new JsonObject { ["Name"] = sectionType, ["Value"] = 0 };

		Assert.True (task.Execute ());
		Assert.Empty (engine.Errors);
		Assert.Equal ("test/Live\n", File.ReadAllText (task.OutputFile));
		Assert.Equal (sectionName, task.RelocationSection);
	}

	[Fact]
	public void ResolvesCompilationPrefixedMapAndFixupSymbols ()
	{
		string path = WriteObject ("prefixed", NativeAotObjectTestFixture.CreateBlob ("test/Live"));
		Symbol (path) ["Name"] = new JsonObject { ["Name"] = "Compilation_123___external_type_map__", ["Value"] = 0 };
		var (task, engine) = CreateTask (path);
		task.UseGroupMetadata = true;
		task.RelocationOutput = AddGroupMetadata (path, "Compilation_123__ZTV29Mono_Android_Java_Lang_Object");

		Assert.True (task.Execute ());
		Assert.Empty (engine.Errors);
		Assert.Equal ("test/Live\n", File.ReadAllText (task.OutputFile));
	}

	[Fact]
	public void MalformedSelectedJavaKeyStillFails ()
	{
		string path = WriteObject ("invalid-java-group", NativeAotObjectTestFixture.CreateGroups (
			["System.Collections.Generic.IDictionary`2[System.Char,System.Int32]"],
			["test/Invalid[abc]"]));
		var (task, engine) = CreateTask (path);
		task.UseGroupMetadata = true;
		task.RelocationOutput = AddGroupMetadata (path,
			"_ZTV43Mono_Android_Android_Runtime_JavaDictionary", "_ZTV29Mono_Android_Java_Lang_Object");

		AssertFailure (task, engine);
	}

	[Theory]
	[InlineData ("_ZTV43Mono_Android_Android_Runtime_JavaDictionary")]
	[InlineData ("_ZTV29Mono_Android_Java_Lang_ObjectExtra")]
	[InlineData ("_ZTV30ThirdParty_TypeMap___TypeMapAnchor")]
	[InlineData ("_ZTV30_Other_TypeMap___TypeMapAnchorExtra")]
	public void MissingRecognizedJavaGroupIsNotAnEmptySuccess (string groupSymbol)
	{
		string path = WriteObject ("foreign-only", NativeAotObjectTestFixture.CreateBlob ("test/LooksLikeJava"));
		var (task, engine) = CreateTask (path);
		task.UseGroupMetadata = true;
		task.RelocationOutput = AddGroupMetadata (path, groupSymbol);

		AssertFailure (task, engine);
	}

	[Theory]
	[InlineData ("missing-fixups")]
	[InlineData ("duplicate-fixups")]
	[InlineData ("bad-fixup-size")]
	[InlineData ("missing-relocation-section")]
	[InlineData ("ambiguous-relocation-section")]
	[InlineData ("wrong-target-section")]
	[InlineData ("missing-relocation")]
	[InlineData ("duplicate-relocation")]
	[InlineData ("wrong-relocation-kind")]
	[InlineData ("nonzero-addend")]
	public void InvalidGroupMetadataFails (string defect)
	{
		string path = WriteObject ("invalid-group-metadata", NativeAotObjectTestFixture.CreateBlob ("test/Live"));
		var (task, engine) = CreateTask (path);
		task.UseGroupMetadata = true;
		task.RelocationOutput = AddGroupMetadata (path, "_ZTV29Mono_Android_Java_Lang_Object");
		var symbols = Array (Document (path) ["Symbols"]);
		var sections = Array (Document (path) ["Sections"]);
		switch (defect) {
		case "missing-fixups":
			symbols.RemoveAt (symbols.Count - 1);
			break;
		case "duplicate-fixups":
			symbols.Add (symbols [symbols.Count - 1]?.DeepClone ());
			break;
		case "bad-fixup-size":
			Object (Object (symbols [symbols.Count - 1]) ["Symbol"]) ["Size"] = 3;
			break;
		case "missing-relocation-section":
			sections.RemoveAt (sections.Count - 1);
			break;
		case "ambiguous-relocation-section":
			var duplicate = Object (sections [sections.Count - 1]?.DeepClone ());
			Object (duplicate ["Section"]) ["Index"] = 4;
			sections.Add (duplicate);
			break;
		case "wrong-target-section":
			RelocationSection (path) ["Info"] = 2;
			break;
		case "missing-relocation":
			task.RelocationOutput = "";
			break;
		case "duplicate-relocation":
			task.RelocationOutput += task.RelocationOutput;
			break;
		case "wrong-relocation-kind":
			task.RelocationOutput = task.RelocationOutput.Replace ("R_AARCH64_PREL32", "R_AARCH64_ABS64", StringComparison.Ordinal);
			break;
		case "nonzero-addend":
			task.RelocationOutput = task.RelocationOutput.TrimEnd () + "+0x4\n";
			break;
		}
		AssertFailure (task, engine);
	}

	[Fact]
	public void OutOfRangeGroupFixupFails ()
	{
		byte [] group = NativeAotObjectTestFixture.CreateGroup (
			NativeAotObjectTestFixture.CreateTable ([NativeAotObjectTestFixture.CreateKey ("test/Live")]), typeIndex: 2);
		string path = WriteObject ("bad-group-index", NativeAotObjectTestFixture.CreateTable ([group]));
		var (task, engine) = CreateTask (path);
		task.UseGroupMetadata = true;
		task.RelocationOutput = AddGroupMetadata (path, "_ZTV29Mono_Android_Java_Lang_Object");

		AssertFailure (task, engine);
		Assert.Null (task.RelocationSection);
	}

	[Fact]
	public void EmptyOuterTableDoesNotRequireGroupRelocations ()
	{
		var (task, engine) = CreateTask (WriteObject ("empty-groups", NativeAotObjectTestFixture.CreateGroups ()));
		task.UseGroupMetadata = true;

		Assert.True (task.Execute ());
		Assert.Empty (engine.Errors);
		Assert.Empty (File.ReadAllBytes (task.OutputFile));
		Assert.Null (task.RelocationSection);
	}

	[Theory]
	[InlineData (true)]
	[InlineData (false)]
	public void EmptyOuterOrInnerTableOverwritesWithZeroByteFile (bool emptyOuter)
	{
		byte [] blob = emptyOuter ? NativeAotObjectTestFixture.CreateGroups () : NativeAotObjectTestFixture.CreateBlob ();
		string path = WriteObject ("empty", blob);
		var (task, engine) = CreateTask (path);

		Assert.True (task.Execute ());
		Assert.Empty (engine.Errors);
		Assert.True (File.Exists (task.OutputFile));
		Assert.Empty (File.ReadAllBytes (task.OutputFile));

		File.WriteAllText (task.OutputFile, "stale/Key\n");
		for (int run = 0; run < 2; run++) {
			File.SetLastWriteTimeUtc (task.OutputFile, new DateTime (2000, 1, 1));
			var (repeat, repeatEngine) = CreateTask (path);
			Assert.True (repeat.Execute ());
			Assert.Empty (repeatEngine.Errors);
			Assert.Empty (File.ReadAllBytes (repeat.OutputFile));
			Assert.True (File.GetLastWriteTimeUtc (repeat.OutputFile).Year > 2000);
		}
	}

	[Theory]
	[InlineData (1, 0)]
	[InlineData (1, 2)]
	[InlineData (2, 1)]
	[InlineData (4, 3)]
	public void ReadsBucketIndexWidthsAndMultipleBuckets (int indexWidth, int bucketShift)
	{
		byte [] inner = NativeAotObjectTestFixture.CreateTable (
			new [] { "test/Z", "test/A", "test/M" }.Select (key => NativeAotObjectTestFixture.CreateKey (key)).ToArray (),
			indexWidth, bucketShift);
		byte [] group = NativeAotObjectTestFixture.CreateGroup (inner);
		byte [] blob = NativeAotObjectTestFixture.CreateTable ([group, group], indexWidth, bucketShift);
		var (task, engine) = CreateTask (WriteObject ("buckets", blob));

		Assert.True (task.Execute ());
		Assert.Empty (engine.Errors);
		Assert.Equal ("test/A\ntest/M\ntest/Z\n", File.ReadAllText (task.OutputFile));
	}

	[Theory]
	[InlineData (1, false)]
	[InlineData (2, false)]
	[InlineData (3, false)]
	[InlineData (4, false)]
	[InlineData (5, false)]
	[InlineData (1, true)]
	[InlineData (2, true)]
	[InlineData (3, true)]
	[InlineData (4, true)]
	[InlineData (5, true)]
	public void RelativeOffsetsUseTheIntegerAddressAndCanPointBackwards (int relativeWidth, bool backwards)
	{
		byte [] inner = NativeAotObjectTestFixture.CreateTable (
			[NativeAotObjectTestFixture.CreateKey ("test/Live")], relativeWidth: relativeWidth, backwards: backwards);
		byte [] blob = NativeAotObjectTestFixture.CreateTable (
			[NativeAotObjectTestFixture.CreateGroup (inner)], relativeWidth: relativeWidth, backwards: backwards);
		var (task, engine) = CreateTask (WriteObject ("relative", blob));

		Assert.True (task.Execute ());
		Assert.Empty (engine.Errors);
		Assert.Equal ("test/Live\n", File.ReadAllText (task.OutputFile));
	}

	[Theory]
	[InlineData (0u)]
	[InlineData (128u)]
	[InlineData (16384u)]
	[InlineData (2097152u)]
	[InlineData (268435456u)]
	[InlineData (uint.MaxValue)]
	public void ReadsAllUnsignedCompactIntegerWidths (uint typeIndex)
	{
		byte [] inner = NativeAotObjectTestFixture.CreateTable ([NativeAotObjectTestFixture.CreateKey ("test/Live", typeIndex)]);
		byte [] blob = NativeAotObjectTestFixture.CreateTable ([NativeAotObjectTestFixture.CreateGroup (inner, typeIndex: typeIndex)]);
		var (task, engine) = CreateTask (WriteObject ("indices", blob));

		Assert.True (task.Execute ());
		Assert.Empty (engine.Errors);
		Assert.Equal ("test/Live\n", File.ReadAllText (task.OutputFile));
	}

	[Theory]
	[InlineData (127)]
	[InlineData (128)]
	[InlineData (16384)]
	[InlineData (2097152)]
	public void ReadsMultibyteStringLengthsWithoutTruncatingKeys (int byteLength)
	{
		string key = "test/" + new string ('A', byteLength - 5);
		var (task, engine) = CreateTask (WriteObject ("length", NativeAotObjectTestFixture.CreateBlob (key)));

		Assert.True (task.Execute ());
		Assert.Empty (engine.Errors);
		Assert.Equal (new UTF8Encoding (false).GetBytes (key + "\n"), File.ReadAllBytes (task.OutputFile));
	}

	[Fact]
	public void MissingObjectInputsFail ()
	{
		var (task, engine) = CreateTask ();
		AssertFailure (task, engine);
	}

	[Theory]
	[InlineData ("missing")]
	[InlineData ("directory")]
	public void MissingOrUnreadableObjectFailsWithFileContext (string kind)
	{
		string path = Path.Combine (directory, "invalid.o");
		if (kind == "directory") {
			Directory.CreateDirectory (path);
		}
		var (task, engine) = CreateTask (path);
		AssertFailure (task, engine, path);
	}

	[Theory]
	[InlineData (0)]
	[InlineData (128)]
	public void TruncatedObjectFailsAgainstActualFileBounds (int length)
	{
		string path = WriteObject ("truncated", NativeAotObjectTestFixture.CreateBlob ("test/Live"));
		using (var stream = File.OpenWrite (path)) {
			stream.SetLength (length);
		}
		var (task, engine) = CreateTask (path);
		AssertFailure (task, engine, path);
	}

	[Theory]
	[InlineData ("")]
	[InlineData ("missing-llvm-readobj")]
	public void MissingToolFailsWithCodedError (string tool)
	{
		var engine = new TypeMapTaskBuildEngine ();
		var task = new ExtractTypeMapKeysFromNativeAotObject {
			BuildEngine = engine,
			NativeObjectFiles = [new TaskItem (WriteObject ("map", NativeAotObjectTestFixture.CreateBlob ("test/Live")))],
			LlvmReadObjPath = tool.Length == 0 ? "" : Path.Combine (directory, tool),
			OutputFile = Path.Combine (directory, "keys.txt"),
		};
		AssertFailure (task, engine, task.LlvmReadObjPath);
	}

	[Theory]
	[InlineData ("")]
	[InlineData ("not JSON")]
	[InlineData ("{}")]
	[InlineData ("[]")]
	[InlineData ("[{},{}]")]
	[InlineData ("[null]")]
	[InlineData ("[{}]")]
	public void MalformedToolJsonFailsWithFileContext (string json)
	{
		string path = WriteObject ("map", NativeAotObjectTestFixture.CreateBlob ("test/Live"));
		var (task, engine) = CreateTask (path);
		task.MetadataReader = _ => json;
		AssertFailure (task, engine, path);
	}

	[Theory]
	[InlineData ("FileSummary")]
	[InlineData ("ElfHeader")]
	[InlineData ("Sections")]
	[InlineData ("Symbols")]
	public void MissingRequiredMetadataFails (string property)
	{
		string path = WriteObject ("map", NativeAotObjectTestFixture.CreateBlob ("test/Live"));
		Document (path).Remove (property);
		var (task, engine) = CreateTask (path);
		AssertFailure (task, engine, path);
	}

	[Theory]
	[InlineData ("format")]
	[InlineData ("architecture")]
	[InlineData ("dynamic")]
	[InlineData ("executable")]
	[InlineData ("big-endian")]
	[InlineData ("missing-encoding")]
	[InlineData ("no-symbol")]
	[InlineData ("suffix-not-terminal")]
	[InlineData ("undefined-symbol")]
	[InlineData ("no-sections")]
	[InlineData ("nobits-section")]
	[InlineData ("duplicate-section")]
	[InlineData ("negative-offset")]
	[InlineData ("section-past-file")]
	[InlineData ("section-overruns-file")]
	[InlineData ("symbol-past-section")]
	[InlineData ("symbol-overruns-section")]
	[InlineData ("empty-symbol")]
	[InlineData ("huge-symbol")]
	[InlineData ("wrong-number-type")]
	public void InvalidObjectMetadataFails (string defect)
	{
		string path = WriteObject ("map", NativeAotObjectTestFixture.CreateBlob ("test/Live"));
		var header = Object (Document (path) ["ElfHeader"]);
		var symbols = Array (Document (path) ["Symbols"]);
		var sections = Array (Document (path) ["Sections"]);
		long fileLength = new FileInfo (path).Length;
		switch (defect) {
			case "format": Summary (path) ["Format"] = "Mach-O arm64"; break;
			case "architecture": Summary (path) ["Arch"] = "riscv64"; break;
			case "dynamic": header ["Type"] = "SharedObject (0x3)"; break;
			case "executable": header ["Type"] = "Executable (0x2)"; break;
			case "big-endian": Object (Object (header ["Ident"]) ["DataEncoding"]) ["Value"] = 2; break;
			case "missing-encoding": Object (header ["Ident"]).Remove ("DataEncoding"); break;
			case "no-symbol": symbols.RemoveAt (1); break;
			case "suffix-not-terminal": Object (Symbol (path) ["Name"]) ["Name"] = "__external_type_map__unrelated"; break;
			case "undefined-symbol": Object (Symbol (path) ["Section"]) ["Value"] = 0; break;
			case "no-sections": sections.Clear (); break;
			case "nobits-section": Object (Section (path) ["Type"]) ["Name"] = "SHT_NOBITS"; break;
			case "duplicate-section": sections.Add (sections [1]?.DeepClone ()); break;
			case "negative-offset": Section (path) ["Offset"] = -1; break;
			case "section-past-file": Section (path) ["Offset"] = fileLength + 1; break;
			case "section-overruns-file": Section (path) ["Size"] = fileLength; break;
			case "symbol-past-section": Symbol (path) ["Value"] = fileLength; break;
			case "symbol-overruns-section": Symbol (path) ["Size"] = fileLength; break;
			case "empty-symbol": Symbol (path) ["Size"] = 0; break;
			case "huge-symbol": Symbol (path) ["Size"] = long.MaxValue; break;
			case "wrong-number-type": Symbol (path) ["Value"] = "17"; break;
			default: throw new ArgumentException (defect, nameof (defect));
		}
		var (task, engine) = CreateTask (path);
		AssertFailure (task, engine, path);
	}

	[Theory]
	[MemberData (nameof (MalformedTables))]
	public void MalformedOuterOrInnerTableFails (string defect, byte [] table)
	{
		foreach (bool inner in new [] { false, true }) {
			byte [] blob = inner ? NativeAotObjectTestFixture.CreateTable ([NativeAotObjectTestFixture.CreateGroup (table)]) : table;
			string path = WriteObject (defect + (inner ? "-inner" : "-outer"), blob);
			var (task, engine) = CreateTask (path);
			AssertFailure (task, engine, path);
		}
	}

	public static IEnumerable<object []> MalformedTables ()
	{
		yield return ["missing-header", new byte [] { }];
		yield return ["invalid-index-width", new byte [] { 3 }];
		yield return ["invalid-bucket-count", new byte [] { 252 }];
		yield return ["truncated-indices", new byte [] { 0, 2 }];
		yield return ["indices-overlap-header", new byte [] { 0, 1, 1 }];
		yield return ["unordered-indices", new byte [] { 4, 3, 2, 3 }];
		yield return ["bucket-outside-blob", new byte [] { 0, 2, 250 }];
		yield return ["truncated-cell", new byte [] { 0, 2, 3, 42 }];
		yield return ["truncated-relative", new byte [] { 0, 2, 4, 42, 1 }];
		yield return ["cell-overruns-bucket", new byte [] { 0, 2, 4, 42, 15, 0, 0, 0, 0 }];
		yield return ["positive-relative-overflow", new byte [] { 0, 2, 8, 42, 15, 255, 255, 255, 127 }];
		yield return ["negative-relative-overflow", new byte [] { 0, 2, 8, 42, 15, 0, 0, 0, 128 }];
		yield return ["invalid-integer-tag", new byte [] { 0, 2, 4, 42, 31 }];
	}

	[Theory]
	[InlineData (0u)]
	[InlineData (2u)]
	[InlineData (uint.MaxValue)]
	public void InvalidGroupStateIsNotAnEmptyMap (uint state)
	{
		byte [] group = NativeAotObjectTestFixture.CreateGroup (NativeAotObjectTestFixture.CreateTable ([]), state);
		var (task, engine) = CreateTask (WriteObject ("invalid-state", NativeAotObjectTestFixture.CreateTable ([group])));
		AssertFailure (task, engine);
	}

	[Theory]
	[InlineData (new byte [] { })]
	[InlineData (new byte [] { 0 })]
	[InlineData (new byte [] { 0, 2 })]
	[InlineData (new byte [] { 31 })]
	[InlineData (new byte [] { 0, 31 })]
	public void TruncatedOrMalformedGroupTupleFails (byte [] group)
	{
		var (task, engine) = CreateTask (WriteObject ("invalid-group", NativeAotObjectTestFixture.CreateTable ([group])));
		AssertFailure (task, engine);
	}

	[Theory]
	[MemberData (nameof (MalformedKeyTuples))]
	public void MalformedKeyTupleFails (string defect, byte [] tuple)
	{
		byte [] inner = NativeAotObjectTestFixture.CreateTable ([tuple]);
		byte [] blob = NativeAotObjectTestFixture.CreateTable ([NativeAotObjectTestFixture.CreateGroup (inner)]);
		var (task, engine) = CreateTask (WriteObject (defect, blob));
		AssertFailure (task, engine);
	}

	public static IEnumerable<object []> MalformedKeyTuples ()
	{
		yield return ["overflow-length", NativeAotObjectTestFixture.EncodeUnsigned (uint.MaxValue)];
		yield return ["invalid-length-tag", new byte [] { 31 }];
		yield return ["truncated-two-byte-length", new byte [] { 1 }];
		yield return ["truncated-three-byte-length", new byte [] { 3, 0 }];
		yield return ["truncated-four-byte-length", new byte [] { 7, 0, 0 }];
		yield return ["truncated-five-byte-length", new byte [] { 15, 0, 0, 0 }];
		yield return ["length-overruns-blob", new byte [] { 126, 65, 0 }];
		yield return ["missing-target-index", new byte [] { 2, 65 }];
		yield return ["invalid-target-index", new byte [] { 2, 65, 31 }];
		yield return ["overlong-utf8", new byte [] { 4, 192, 175, 0 }];
		yield return ["surrogate-utf8", new byte [] { 6, 237, 160, 128, 0 }];
		yield return ["incomplete-utf8", new byte [] { 4, 240, 144, 0 }];
		yield return ["invalid-utf8-continuation", new byte [] { 2, 128, 0 }];
	}

	[Theory]
	[InlineData ("")]
	[InlineData ("test/Invalid\nName")]
	[InlineData ("test/Invalid\rName")]
	[InlineData ("test/Invalid\0Name")]
	[InlineData ("test/A[]")]
	[InlineData ("test/A[-1]")]
	[InlineData ("test/A[1x]")]
	[InlineData ("test/A[1]Extra")]
	[InlineData ("test/A[1")]
	[InlineData ("test/A[")]
	[InlineData ("[0]")]
	[InlineData ("test/A[\u0661]")]
	[InlineData ("test/A[1][2]")]
	[InlineData ("test.Invalid")]
	[InlineData ("test/*")]
	[InlineData ("test/")]
	[InlineData ("/test")]
	[InlineData ("test//Invalid")]
	[InlineData ("test/Invalid\u200bName")]
	[InlineData ("[L;")]
	[InlineData ("[V")]
	[InlineData ("[Q")]
	[InlineData ("[Iextra")]
	[InlineData ("[Ljava.lang.Object;")]
	[InlineData ("[Ljava/lang/Object")]
	[InlineData ("[Ljava/lang/Object;;")]
	[InlineData ("[Ltest/A[0];")]
	[InlineData ("[[")]
	public void InvalidCanonicalKeysOrAliasesFail (string key)
	{
		var (task, engine) = CreateTask (WriteObject ("invalid-key", NativeAotObjectTestFixture.CreateBlob (key)));
		AssertFailure (task, engine);
	}

	[Fact]
	public void InvalidSecondObjectDoesNotWritePartialOutput ()
	{
		string valid = WriteObject ("valid", NativeAotObjectTestFixture.CreateBlob ("test/Live"));
		string invalid = WriteObject ("invalid", [3]);
		var (task, engine) = CreateTask (valid, invalid);
		AssertFailure (task, engine, invalid);
	}

	[Fact]
	public void InvalidLaterGroupDoesNotWritePartialOutput ()
	{
		byte [] valid = NativeAotObjectTestFixture.CreateGroup (
			NativeAotObjectTestFixture.CreateTable ([NativeAotObjectTestFixture.CreateKey ("test/Live")]));
		byte [] invalid = NativeAotObjectTestFixture.CreateGroup (NativeAotObjectTestFixture.CreateTable ([]), state: 0);
		var (task, engine) = CreateTask (WriteObject ("mixed-groups", NativeAotObjectTestFixture.CreateTable ([valid, invalid])));
		AssertFailure (task, engine);
	}

	[Fact]
	public void MetadataReadFailureIsReportedWithoutPartialOutput ()
	{
		string path = WriteObject ("map", NativeAotObjectTestFixture.CreateBlob ("test/Live"));
		var (task, engine) = CreateTask (path);
		task.MetadataReader = _ => throw new IOException ("Could not read object metadata.");
		AssertFailure (task, engine, path);
	}

	[Fact]
	public void OutputWriteFailureIsReported ()
	{
		var (task, engine) = CreateTask (WriteObject ("map", NativeAotObjectTestFixture.CreateBlob ("test/Live")));
		task.OutputFile = directory;
		AssertFailure (task, engine, directory);
	}

	[Fact]
	public void OutputWriteFailurePreservesExistingOutputAndRemovesTemporaryFile ()
	{
		var (task, engine) = CreateTask (WriteObject ("map", NativeAotObjectTestFixture.CreateBlob ("test/Live")));
		Directory.CreateDirectory (Path.GetDirectoryName (task.OutputFile) ?? throw new InvalidOperationException ());
		File.WriteAllText (task.OutputFile, "stale/sentinel\n");
		task.OutputWriter = (outputFile, _) => {
			File.WriteAllText (outputFile, "partial");
			throw new IOException ("Could not finish writing output.");
		};

		Assert.False (task.Execute ());
		Assert.Contains (engine.Errors, error => error.Code == "XA4327");
		Assert.Equal ("stale/sentinel\n", File.ReadAllText (task.OutputFile));
		Assert.Empty (Directory.GetFiles (Path.GetDirectoryName (task.OutputFile) ?? throw new InvalidOperationException (), "*.tmp"));
	}

	static void AssertFailure (ExtractTypeMapKeysFromNativeAotObject task, TypeMapTaskBuildEngine engine, string? file = null)
	{
		file ??= task.NativeObjectFiles.Length == 0 ? nameof (task.NativeObjectFiles) : task.NativeObjectFiles [0].ItemSpec;
		Assert.False (task.Execute ());
		Assert.Contains (engine.Errors, error => error.Code == "XA4327" &&
			(file.Length == 0 || error.Message != null && error.Message.Contains (file, StringComparison.Ordinal)));
		Assert.False (File.Exists (task.OutputFile));
	}

	(MetadataTask task, TypeMapTaskBuildEngine engine) CreateTask (params string [] paths)
	{
		var engine = new TypeMapTaskBuildEngine ();
		return (new MetadataTask (path => metadata [path].ToJsonString ()) {
			BuildEngine = engine,
			NativeObjectFiles = paths.Select (path => new TaskItem (path)).ToArray (),
			LlvmReadObjPath = Path.Combine (directory, "llvm-readobj-placeholder"),
			OutputFile = Path.Combine (directory, "output", "keys.txt"),
		}, engine);
	}

	string WriteObject (string name, byte [] blob)
	{
		string path = Path.Combine (directory, name + ".o");
		byte [] bytes = new byte [SectionOffset + SymbolOffset + blob.Length + 64];
		Encoding.UTF8.GetBytes ("unrelated/Before\0__external_type_map__\0").CopyTo (bytes, 0);
		blob.CopyTo (bytes, SectionOffset + SymbolOffset);
		Encoding.UTF8.GetBytes ("unrelated/After\0").CopyTo (bytes, SectionOffset + SymbolOffset + blob.Length);
		File.WriteAllBytes (path, bytes);
		metadata [path] = Array (JsonNode.Parse ($$$$"""
			[{
			  "FileSummary": {"File":"fixture.o","Format":"elf64-littleaarch64","Arch":"aarch64","AddressSize":"64bit"},
			  "ElfHeader": {"Type":"Relocatable (0x1)","Ident":{"DataEncoding":{"Name":"LittleEndian","Value":1}}},
			  "Sections": [
			    {"Section":{"Index":2,"Name":{"Name":".rodata","Value":9},"Type":{"Name":"SHT_PROGBITS","Value":1},"Offset":0,"Size":64}},
			    {"Section":{"Index":1,"Name":{"Name":".rodata","Value":1},"Type":{"Name":"SHT_PROGBITS","Value":1},"Offset":{{{{SectionOffset}}}},"Size":{{{{SymbolOffset + blob.Length + 64}}}}}}
			  ],
			  "Symbols": [
			    {"Symbol":{"Name":{"Name":"unrelated/Removed","Value":0},"Value":0,"Size":64,"Section":{"Name":".rodata","Value":2}}},
			    {"Symbol":{"Name":{"Name":"__external_type_map__","Value":0},"Value":{{{{SymbolOffset}}}},"Size":{{{{blob.Length}}}},"Section":{"Name":".rodata","Value":1}}},
			    {"Symbol":{"Name":{"Name":"__external_type_map__not_a_map","Value":0},"Value":0,"Size":64,"Section":{"Name":".rodata","Value":2}}}
			  ]
			}]
			"""));
		return path;
	}

	JsonObject Document (string path) => Object (metadata [path] [0]);
	JsonObject Summary (string path) => Object (Document (path) ["FileSummary"]);
	JsonObject Section (string path) => Object (Object (Array (Document (path) ["Sections"]) [1]) ["Section"]);
	JsonObject Symbol (string path) => Object (Object (Array (Document (path) ["Symbols"]) [1]) ["Symbol"]);
	JsonObject RelocationSection (string path) => Object (Object (Array (Document (path) ["Sections"]) [2]) ["Section"]);

	string AddGroupMetadata (string path, params string [] groupSymbols)
	{
		var symbol = Symbol (path);
		long size = symbol ["Size"]?.GetValue<long> () ?? throw new InvalidOperationException ();
		string mapName = Object (symbol ["Name"]) ["Name"]?.GetValue<string> () ?? throw new InvalidOperationException ();
		string prefix = mapName.Substring (0, mapName.Length - "__external_type_map__".Length);
		long value = SymbolOffset + size + 16;
		Array (Document (path) ["Symbols"]).Add (new JsonObject {
			["Symbol"] = new JsonObject {
				["Name"] = new JsonObject { ["Name"] = prefix + "__external_CommonFixupsTable_references", ["Value"] = 0 },
				["Value"] = value,
				["Size"] = groupSymbols.Length * 4,
				["Section"] = new JsonObject { ["Name"] = ".rodata", ["Value"] = 1 },
			},
		});
		Array (Document (path) ["Sections"]).Add (new JsonObject {
			["Section"] = new JsonObject {
				["Index"] = 3,
				["Name"] = new JsonObject { ["Name"] = ".rela.rodata", ["Value"] = 0 },
				["Type"] = new JsonObject { ["Name"] = "SHT_RELA", ["Value"] = 4 },
				["Info"] = 1,
			},
		});
		var relocations = new StringBuilder ("RELOCATION RECORDS FOR [.rodata]:\nOFFSET TYPE VALUE\n");
		for (int i = 0; i < groupSymbols.Length; i++) {
			relocations.Append ($"{value + i * 4:x16} R_AARCH64_PREL32 {groupSymbols [i]}\n");
		}
		return relocations.ToString ();
	}

	static JsonObject Object (JsonNode? node) => node as JsonObject ?? throw new InvalidOperationException ("Expected an object fixture.");
	static JsonArray Array (JsonNode? node) => node as JsonArray ?? throw new InvalidOperationException ("Expected an array fixture.");

	sealed class MetadataTask (Func<string, string> reader) : ExtractTypeMapKeysFromNativeAotObject
	{
		public Func<string, string> MetadataReader { get; set; } = reader;
		public Action<string, IReadOnlyCollection<string>>? OutputWriter { get; set; }
		public bool UseGroupMetadata { get; set; }
		public string RelocationOutput { get; set; } = "";
		public string? RelocationSection { get; private set; }
		public long RelocationStart { get; private set; }
		public long RelocationEnd { get; private set; }

		protected override Task<string> ReadObjectMetadataAsync (string objectFile) => Task.FromResult (MetadataReader (objectFile));

		protected override void WriteOutputFile (string outputFile, IReadOnlyCollection<string> keys)
		{
			if (OutputWriter is Action<string, IReadOnlyCollection<string>> outputWriter) {
				outputWriter (outputFile, keys);
				return;
			}
			base.WriteOutputFile (outputFile, keys);
		}

		// Parser fixtures model Java groups; separate relocation tests exercise group selection.
		protected override Task<HashSet<uint>> GetJavaTypeMapGroupsAsync (
			string objectFile, JsonElement fileMetadata, string mapSymbol, IReadOnlyCollection<uint> groupIndices, long fileLength) =>
			UseGroupMetadata
				? base.GetJavaTypeMapGroupsAsync (objectFile, fileMetadata, mapSymbol, groupIndices, fileLength)
				: Task.FromResult (new HashSet<uint> (groupIndices));

		protected override Task<string> ReadObjectRelocationsAsync (string objectFile, string section, long start, long end)
		{
			RelocationSection = section;
			RelocationStart = start;
			RelocationEnd = end;
			return Task.FromResult (RelocationOutput);
		}
	}
}
