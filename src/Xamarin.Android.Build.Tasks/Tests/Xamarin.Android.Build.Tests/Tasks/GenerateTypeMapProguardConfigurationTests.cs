using System;
using System.IO;
using System.Text;
using Microsoft.Build.Utilities;
using NUnit.Framework;
using Xamarin.Android.Tasks;

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
		var expected = "-keep class android.app.Activity\n-keep class test.Caf\u00e9\n-keep class test.Outer$Inner\n-keep class test.\U00010428Peer\n";
		CollectionAssert.AreEqual (new UTF8Encoding (false).GetBytes (expected), File.ReadAllBytes (task.OutputFile));
	}

	[TestCase ("test/*")]
	[TestCase ("test/Foo { *; }")]
	[TestCase ("test/Foo[1]")]
	[TestCase ("test.Foo")]
	[TestCase ("test//Foo")]
	[TestCase ("/test/Foo")]
	[TestCase ("test/Foo/")]
	[TestCase ("test/1Foo")]
	[TestCase ("test/Foo;")]
	[TestCase ("test/Foo #comment")]
	[TestCase ("-dontshrink")]
	[TestCase ("test/Foo\n-keep class **")]
	[TestCase ("test/Foo\r-dontobfuscate")]
	[TestCase ("\ufefftest/Foo")]
	[TestCase ("test/Foo\u0000")]
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

	static GenerateTypeMapProguardConfiguration CreateTask (string input, string output) => new () {
		BuildEngine = new MockBuildEngine (TestContext.Out),
		TypeMapKeyFiles = [new TaskItem (input)],
		OutputFile = output,
	};
}
