using System;
using System.Globalization;
using System.IO;
using System.Text;
using Microsoft.Android.Tasks;
using Microsoft.Build.Utilities;
using Xunit;

namespace Microsoft.Android.Sdk.TrimmableTypeMap.Tests;

public class ExtractTypeMapKeysFromLlvmIrArrayTests : IDisposable
{
	readonly string directory = Path.Combine (Path.GetTempPath (), "llvm-array-typemap-" + Guid.NewGuid ().ToString ("N"));
	readonly TypeMapTaskBuildEngine engine = new ();

	public ExtractTypeMapKeysFromLlvmIrArrayTests () => Directory.CreateDirectory (directory);

	[Theory]
	[InlineData ("java_type_names")]
	[InlineData ("type_map_java_type_names")]
	public void ArraysContributeOnlyReferenceElementClasses (string symbol)
	{
		var task = CreateTask (symbol, "[Ljava/lang/Object;", "[Z", "[B", "[C", "[S", "[I", "[J", "[F", "[D",
			"[[[I", "test/Live", "[[Ltest/Outer$Inner;", "test/Outer$Inner");
		Assert.True (task.Execute ());
		Assert.Empty (engine.Errors);
		Assert.Equal ("java/lang/Object\ntest/Live\ntest/Outer$Inner\n", File.ReadAllText (task.OutputFile));
	}

	[Fact]
	public void PrimitiveArraysAloneProduceAValidEmptyClassSet ()
	{
		var task = CreateTask ("java_type_names", "[B", "[[I", "[[[D");
		Assert.True (task.Execute ());
		Assert.Empty (File.ReadAllBytes (task.OutputFile));
	}

	[Theory]
	[InlineData ("[")]
	[InlineData ("[[")]
	[InlineData ("[V")]
	[InlineData ("[Q")]
	[InlineData ("[Iextra")]
	[InlineData ("[[I;")]
	[InlineData ("[L;")]
	[InlineData ("[Ljava.lang.Object;")]
	[InlineData ("[Ljava/lang/Object")]
	[InlineData ("[Ljava/lang/Object;;")]
	[InlineData ("[Ltest/*;")]
	[InlineData ("[Ltest/Foo[0];")]
	[InlineData ("Ltest/Foo;")]
	public void MalformedDescriptorsStillFail (string name)
	{
		var task = CreateTask ("java_type_names", name);
		Assert.False (task.Execute ());
		Assert.Contains (engine.Errors, error => error.Code == "XA4327");
		Assert.False (File.Exists (task.OutputFile));
	}

	ExtractTypeMapKeysFromLlvmIr CreateTask (string symbol, params string [] names)
	{
		var bytes = Encoding.UTF8.GetBytes (string.Join ('\0', names) + '\0');
		var literal = new StringBuilder ();
		foreach (var value in bytes) {
			literal.Append ('\\').Append (value.ToString ("X2", CultureInfo.InvariantCulture));
		}
		var source = Path.Combine (directory, "typemaps.ll");
		File.WriteAllText (source, $"@{symbol} = dso_local local_unnamed_addr constant [{bytes.Length} x i8] c\"{literal}\", align 1\n");
		return new ExtractTypeMapKeysFromLlvmIr {
			BuildEngine = engine,
			LlvmIrFiles = [new TaskItem (source)],
			OutputFile = Path.Combine (directory, "classes.keys"),
		};
	}

	public void Dispose () => Directory.Delete (directory, recursive: true);
}
