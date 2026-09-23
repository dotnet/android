using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NUnit.Framework;
using Microsoft.Android.Tasks;

namespace Xamarin.Android.Build.Tests;

[TestFixture]
[Parallelizable (ParallelScope.Children)]
public class GenerateTypeMapProguardConfigurationTests : BaseTest
{
	[Test]
	public void UnionsClassNamesWithoutMemberOrGlobalRules ()
	{
		var path = Path.Combine (Root, "temp", TestName);
		Directory.CreateDirectory (path);
		var first = Path.Combine (path, "first.keys");
		var second = Path.Combine (path, "second.keys");
		File.WriteAllText (first, "test/Outer$Inner\r\nandroid/app/Activity\r\n\r\ntest/Caf\u00e9\n", new UTF8Encoding (false));
		File.WriteAllText (second, "test/Outer$Inner\nandroid/app/Activity\ntest/\U00010428Peer\n", new UTF8Encoding (false));
		var task = new GenerateTypeMapProguardConfiguration {
			BuildEngine = new MockBuildEngine (TestContext.Out),
			TypeMapKeyFiles = [new TaskItem (second), new TaskItem (first)],
			OutputFile = Path.Combine (path, "proguard", "classes.cfg"),
		};

		Assert.IsTrue (task.Execute ());
		var expected =
			"-keep class android.app.Activity\n-keep interface android.app.Activity\n" +
			"-keep class test.Caf\u00e9\n-keep interface test.Caf\u00e9\n" +
			"-keep class test.Outer$Inner\n-keep interface test.Outer$Inner\n" +
			"-keep class test.\U00010428Peer\n-keep interface test.\U00010428Peer\n";
		CollectionAssert.AreEqual (new UTF8Encoding (false).GetBytes (expected), File.ReadAllBytes (task.OutputFile));
	}

	[TestCase ("test/*", TestName = "RejectsInvalidRecord_Wildcard")]
	[TestCase ("test/Foo { *; }", TestName = "RejectsInvalidRecord_ProguardBlock")]
	[TestCase ("test/Foo[1]", TestName = "RejectsInvalidRecord_AliasSuffix")]
	[TestCase ("test.Foo", TestName = "RejectsInvalidRecord_DottedName")]
	[TestCase ("test//Foo", TestName = "RejectsInvalidRecord_DoubleSlash")]
	[TestCase ("/test/Foo", TestName = "RejectsInvalidRecord_LeadingSlash")]
	[TestCase ("test/Foo/", TestName = "RejectsInvalidRecord_TrailingSlash")]
	[TestCase ("test/1Foo", TestName = "RejectsInvalidRecord_LeadingDigit")]
	[TestCase ("test/Foo;", TestName = "RejectsInvalidRecord_DescriptorTerminator")]
	[TestCase ("test/Foo #comment", TestName = "RejectsInvalidRecord_InlineComment")]
	[TestCase ("-dontshrink", TestName = "RejectsInvalidRecord_Directive")]
	[TestCase ("test/Foo\n-keep class **", TestName = "RejectsInvalidRecord_LineFeedInjection")]
	[TestCase ("test/Foo\r-dontobfuscate", TestName = "RejectsInvalidRecord_CarriageReturnInjection")]
	[TestCase ("\ufefftest/Foo", TestName = "RejectsInvalidRecord_Bom")]
	[TestCase ("test/Foo\u0000", TestName = "RejectsInvalidRecord_NullCharacter")]
	public void RejectsInvalidRecordsWithoutOverwritingOutput (string content)
	{
		var path = Path.Combine (Root, "temp", TestName);
		Directory.CreateDirectory (path);
		var input = Path.Combine (path, "input.keys");
		var output = Path.Combine (path, "classes.cfg");
		File.WriteAllText (input, content, new UTF8Encoding (false));
		File.WriteAllText (output, "previous output");
		var task = CreateTask (input, output);

		Assert.IsFalse (task.Execute ());
		Assert.AreEqual ("previous output", File.ReadAllText (output));
	}

	[Test]
	public void EmptyInputOverwritesOutputAndRefreshesTimestamp ()
	{
		var path = Path.Combine (Root, "temp", TestName);
		Directory.CreateDirectory (path);
		var input = Path.Combine (path, "input.keys");
		var output = Path.Combine (path, "classes.cfg");
		File.WriteAllText (input, "");
		File.WriteAllText (output, "stale");
		var old = DateTime.UtcNow.AddDays (-1);
		File.SetLastWriteTimeUtc (output, old);
		var task = CreateTask (input, output);

		Assert.IsTrue (task.Execute ());
		Assert.AreEqual (0, new FileInfo (output).Length);
		Assert.Greater (File.GetLastWriteTimeUtc (output), old);
		File.SetLastWriteTimeUtc (output, old);
		Assert.IsTrue (task.Execute ());
		Assert.Greater (File.GetLastWriteTimeUtc (output), old, "Unchanged output must still be a real incremental output.");
		File.Delete (output);
		Assert.IsTrue (task.Execute ());
		FileAssert.Exists (output);
	}

	[Test]
	public void MissingInputIsNotAnEmptyMap ()
	{
		var path = Path.Combine (Root, "temp", TestName);
		Directory.CreateDirectory (path);
		var task = CreateTask (Path.Combine (path, "missing.keys"), Path.Combine (path, "classes.cfg"));
		Assert.IsFalse (task.Execute ());
		Assert.IsFalse (File.Exists (task.OutputFile));
	}

	[Test]
	public void RejectsInvalidUtf8 ()
	{
		var path = Path.Combine (Root, "temp", TestName);
		Directory.CreateDirectory (path);
		var input = Path.Combine (path, "input.keys");
		File.WriteAllBytes (input, [0xff, 0xfe]);
		var task = CreateTask (input, Path.Combine (path, "classes.cfg"));
		Assert.IsFalse (task.Execute ());
		Assert.IsFalse (File.Exists (task.OutputFile));
	}

	[Test]
	public void NoInputsReportsOutputPath ()
	{
		var path = Path.Combine (Root, "temp", TestName);
		Directory.CreateDirectory (path);
		var output = Path.Combine (path, "classes.cfg");
		var errors = new List<BuildErrorEventArgs> ();
		var engine = new MockBuildEngine (TestContext.Out, errors);
		var task = new GenerateTypeMapProguardConfiguration {
			BuildEngine = engine,
			OutputFile = output,
		};

		Assert.IsFalse (task.Execute ());
		Assert.AreEqual (1, errors.Count);
		Assert.AreEqual ("XA4328", errors [0].Code);
		StringAssert.Contains (output, errors [0].Message);
	}

	[Test]
	public void InvalidOutputPathReportsGenerationError ()
	{
		var path = Path.Combine (Root, "temp", TestName);
		Directory.CreateDirectory (path);
		var input = Path.Combine (path, "input.keys");
		File.WriteAllText (input, "test/Foo");
		var errors = new List<BuildErrorEventArgs> ();
		var engine = new MockBuildEngine (TestContext.Out, errors);
		var task = new GenerateTypeMapProguardConfiguration {
			BuildEngine = engine,
			TypeMapKeyFiles = [new TaskItem (input)],
			OutputFile = "\0",
		};

		Assert.IsFalse (task.Execute ());
		Assert.AreEqual (1, errors.Count);
		Assert.AreEqual ("XA4328", errors [0].Code);
	}

	[Test]
	public void MemberGeneratorScopesRulesToCanonicalKeys ()
	{
		var path = Path.Combine (Root, "temp", TestName);
		Directory.CreateDirectory (path);
		var first = Path.Combine (path, "first.keys");
		var second = Path.Combine (path, "second.keys");
		File.WriteAllText (first, "test/Peer\ntest/Contract\n");
		File.WriteAllText (second, "test/Peer\ntest/Base\n");
		var task = new GenerateTypeMapMemberProguardConfiguration {
			BuildEngine = new MockBuildEngine (TestContext.Out),
			TypeMapKeyFiles = [new TaskItem (first), new TaskItem (second)],
			OutputFile = Path.Combine (path, "members.cfg"),
		};

		Assert.IsTrue (task.Execute ());
		Assert.AreEqual (
			"-keepclassmembers class test.Base { *; }\n-keepclassmembers interface test.Base { *; }\n" +
			"-keepclassmembers class test.Contract { *; }\n-keepclassmembers interface test.Contract { *; }\n" +
			"-keepclassmembers class test.Peer { *; }\n-keepclassmembers interface test.Peer { *; }\n",
			File.ReadAllText (task.OutputFile));
	}

	[Test]
	public void MemberGeneratorRejectsInvalidKeysWithoutOverwritingOutput ()
	{
		var path = Path.Combine (Root, "temp", TestName);
		Directory.CreateDirectory (path);
		var input = Path.Combine (path, "invalid.keys");
		var output = Path.Combine (path, "members.cfg");
		File.WriteAllText (input, "test/Peer\ntest/*\n");
		File.WriteAllText (output, "previous output");
		var errors = new List<BuildErrorEventArgs> ();
		var task = new GenerateTypeMapMemberProguardConfiguration {
			BuildEngine = new MockBuildEngine (TestContext.Out, errors),
			TypeMapKeyFiles = [new TaskItem (input)],
			OutputFile = output,
		};

		Assert.IsFalse (task.Execute ());
		Assert.AreEqual (1, errors.Count);
		Assert.AreEqual ("XA4328", errors [0].Code);
		Assert.AreEqual ("previous output", File.ReadAllText (output));
	}

	static GenerateTypeMapProguardConfiguration CreateTask (string input, string output) => new () {
		BuildEngine = new MockBuildEngine (TestContext.Out),
		TypeMapKeyFiles = [new TaskItem (input)],
		OutputFile = output,
	};
}
