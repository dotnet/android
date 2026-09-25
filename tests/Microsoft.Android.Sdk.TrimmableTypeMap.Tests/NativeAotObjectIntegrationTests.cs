using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Android.Tasks;
using Xunit;
using TaskItem = Microsoft.Build.Utilities.TaskItem;

namespace Microsoft.Android.Sdk.TrimmableTypeMap.Tests;

static class NativeAotObjectIntegrationTools
{
	public static string LlvmReadObjPath =>
		typeof (NativeAotObjectIntegrationTools).Assembly.GetCustomAttributes<AssemblyMetadataAttribute> ()
			.Single (attribute => attribute.Key == "NativeAotLlvmReadObjPath").Value ?? "";

	public static string LlvmObjDumpPath => Path.Combine (Path.GetDirectoryName (LlvmReadObjPath) ?? "",
		OperatingSystem.IsWindows () ? "llvm-objdump.exe" : "llvm-objdump");

	public static string ClangPath => Path.Combine (Path.GetDirectoryName (LlvmReadObjPath) ?? "",
		OperatingSystem.IsWindows () ? "clang.exe" : "clang");

	public static string? SkipReason => File.Exists (LlvmReadObjPath) && File.Exists (LlvmObjDumpPath) && File.Exists (ClangPath)
		? null
		: "Set _NativeAotLlvmReadObjPath to the NDK llvm-readobj executable with adjacent llvm-objdump and clang to run native-object integration tests.";
}

sealed class NativeAotObjectFactAttribute : FactAttribute
{
	public NativeAotObjectFactAttribute () => Skip = NativeAotObjectIntegrationTools.SkipReason;
}

sealed class NativeAotObjectTheoryAttribute : TheoryAttribute
{
	public NativeAotObjectTheoryAttribute () => Skip = NativeAotObjectIntegrationTools.SkipReason;
}

public class NativeAotObjectIntegrationTests : IDisposable
{
	readonly string directory = Path.Combine (Path.GetTempPath (), nameof (NativeAotObjectIntegrationTests), Guid.NewGuid ().ToString ("N"));

	public NativeAotObjectIntegrationTests () => Directory.CreateDirectory (directory);

	public void Dispose () => Directory.Delete (directory, recursive: true);

	[NativeAotObjectTheory]
	[InlineData ("armv7a-linux-androideabi")]
	[InlineData ("aarch64-linux-android")]
	[InlineData ("i686-linux-android")]
	[InlineData ("x86_64-linux-android")]
	public void ExtractsJavaKeysFromRealLlvmObject (string targetTriple)
	{
		string objectFile = NativeAotObjectTestFixture.WriteObjectGroups (
			directory, targetTriple, NativeAotObjectIntegrationTools.LlvmReadObjPath, targetTriple,
			("_ZTV43Mono_Android_Android_Runtime_JavaDictionary", ["managed/NotAJavaPeer"]),
			("_ZTV29Mono_Android_Java_Lang_Object", ["test/Zebra", "test/Alias[0]"]),
			("_ZTV37_Mono_Android_TypeMap___TypeMapAnchor", ["test/Alpha", "test/Alias[1]"]));
		var engine = new TypeMapTaskBuildEngine ();
		string outputFile = Path.Combine (directory, targetTriple + ".txt");
		var task = new ExtractTypeMapKeysFromNativeAotObject {
			BuildEngine = engine,
			NativeObjectFiles = [new TaskItem (objectFile)],
			LlvmReadObjPath = NativeAotObjectIntegrationTools.LlvmReadObjPath,
			OutputFile = outputFile,
		};

		Assert.True (task.Execute ());
		Assert.Empty (engine.Errors);
		Assert.Equal ("test/Alias\ntest/Alpha\ntest/Zebra\n", File.ReadAllText (outputFile));
	}
}
