using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Xunit;

namespace Microsoft.Android.Sdk.TrimmableTypeMap.Tests;

static class NativeAotObjectIntegrationTools
{
	public static string LlvmReadObjPath =>
		typeof (NativeAotObjectIntegrationTools).Assembly.GetCustomAttributes<AssemblyMetadataAttribute> ()
			.Single (attribute => attribute.Key == "NativeAotLlvmReadObjPath").Value ?? "";

	public static string LlvmObjDumpPath => Path.Combine (Path.GetDirectoryName (LlvmReadObjPath) ?? "",
		OperatingSystem.IsWindows () ? "llvm-objdump.exe" : "llvm-objdump");

	public static string? SkipReason => File.Exists (LlvmReadObjPath) && File.Exists (LlvmObjDumpPath)
		? null
		: "Set _NativeAotLlvmReadObjPath to the NDK llvm-readobj executable with adjacent llvm-objdump to run native-object integration tests.";
}

sealed class NativeAotObjectFactAttribute : FactAttribute
{
	public NativeAotObjectFactAttribute () => Skip = NativeAotObjectIntegrationTools.SkipReason;
}

sealed class NativeAotObjectTheoryAttribute : TheoryAttribute
{
	public NativeAotObjectTheoryAttribute () => Skip = NativeAotObjectIntegrationTools.SkipReason;
}
