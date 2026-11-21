using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Android.Tasks;

namespace Xamarin.Android.Build.Tests;

[TestClass]
public class GenerateTypeMapProguardConfigurationTests : BaseTest
{
	[TestMethod]
	public void UnionsClassNamesWithoutMemberOrGlobalRules ()
	{
		var path = Path.Combine (Root, "temp", TestName);
		Directory.CreateDirectory (path);
		var first = Path.Combine (path, "first.keys");
		var second = Path.Combine (path, "second.keys");
		File.WriteAllText (first, "test/Outer$Inner\r\nandroid/app/Activity\r\n\r\ntest/Caf\u00e9\n", new UTF8Encoding (false));
		File.WriteAllText (second, "test/Outer$Inner\nandroid/app/Activity\ntest/\U00010428Peer\n", new UTF8Encoding (false));
		var task = new GenerateTypeMapProguardConfiguration {
			BuildEngine = new MockBuildEngine (Console.Out),
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

	[TestMethod]
	[DataRow ("test/*", DisplayName = "RejectsInvalidRecord_Wildcard")]
	[DataRow ("test/Foo { *; }", DisplayName = "RejectsInvalidRecord_ProguardBlock")]
	[DataRow ("test/Foo[1]", DisplayName = "RejectsInvalidRecord_AliasSuffix")]
	[DataRow ("test.Foo", DisplayName = "RejectsInvalidRecord_DottedName")]
	[DataRow ("test//Foo", DisplayName = "RejectsInvalidRecord_DoubleSlash")]
	[DataRow ("/test/Foo", DisplayName = "RejectsInvalidRecord_LeadingSlash")]
	[DataRow ("test/Foo/", DisplayName = "RejectsInvalidRecord_TrailingSlash")]
	[DataRow ("test/1Foo", DisplayName = "RejectsInvalidRecord_LeadingDigit")]
	[DataRow ("test/Foo;", DisplayName = "RejectsInvalidRecord_DescriptorTerminator")]
	[DataRow ("test/Foo #comment", DisplayName = "RejectsInvalidRecord_InlineComment")]
	[DataRow ("-dontshrink", DisplayName = "RejectsInvalidRecord_Directive")]
	[DataRow ("test/Foo\n-keep class **", DisplayName = "RejectsInvalidRecord_LineFeedInjection")]
	[DataRow ("test/Foo\r-dontobfuscate", DisplayName = "RejectsInvalidRecord_CarriageReturnInjection")]
	[DataRow ("\ufefftest/Foo", DisplayName = "RejectsInvalidRecord_Bom")]
	[DataRow ("test/Foo\u0000", DisplayName = "RejectsInvalidRecord_NullCharacter")]
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

	[TestMethod]
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
		Assert.IsGreaterThan (old, File.GetLastWriteTimeUtc (output));
		File.SetLastWriteTimeUtc (output, old);
		Assert.IsTrue (task.Execute ());
		Assert.IsGreaterThan (old, File.GetLastWriteTimeUtc (output), "Unchanged output must still be a real incremental output.");
		File.Delete (output);
		Assert.IsTrue (task.Execute ());
		Assert.IsTrue (File.Exists (output));
	}

	[TestMethod]
	public void MissingInputIsNotAnEmptyMap ()
	{
		var path = Path.Combine (Root, "temp", TestName);
		Directory.CreateDirectory (path);
		var task = CreateTask (Path.Combine (path, "missing.keys"), Path.Combine (path, "classes.cfg"));
		Assert.IsFalse (task.Execute ());
		Assert.IsFalse (File.Exists (task.OutputFile));
	}

	[TestMethod]
	[DataRow (false, false)]
	[DataRow (false, true)]
	[DataRow (true, false)]
	[DataRow (true, true)]
	public void FailedWriteCannotPublishPartialConfiguration (bool members, bool existingOutput)
	{
		var path = Path.Combine (Root, "temp", TestName);
		Directory.CreateDirectory (path);
		var input = Path.Combine (path, "input.keys");
		var output = Path.Combine (path, "rules.cfg");
		File.WriteAllText (input, "test/Live\n");
		var old = DateTime.UtcNow.AddDays (-1);
		if (existingOutput) {
			File.WriteAllText (output, "previous output");
			File.SetLastWriteTimeUtc (output, old);
			old = File.GetLastWriteTimeUtc (output);
		}
		var errors = new List<BuildErrorEventArgs> ();
		GenerateTypeMapProguardConfiguration failing = members ? new FailingMemberWriter () : new FailingClassWriter ();
		failing.BuildEngine = new MockBuildEngine (Console.Out, errors);
		failing.TypeMapKeyFiles = [new TaskItem (input)];
		failing.OutputFile = output;

		Assert.IsFalse (failing.Execute ());
		Assert.AreEqual (1, errors.Count);
		Assert.AreEqual ("XA4328", errors [0].Code);
		if (existingOutput) {
			Assert.AreEqual ("previous output", File.ReadAllText (output));
			Assert.AreEqual (old, File.GetLastWriteTimeUtc (output));
		} else {
			Assert.IsFalse (File.Exists (output));
		}
		Assert.IsEmpty (Directory.GetFiles (path, "*.tmp"));

		GenerateTypeMapProguardConfiguration retry = members ? new GenerateTypeMapMemberProguardConfiguration () : new GenerateTypeMapProguardConfiguration ();
		retry.BuildEngine = new MockBuildEngine (Console.Out);
		retry.TypeMapKeyFiles = failing.TypeMapKeyFiles;
		retry.OutputFile = output;
		Assert.IsTrue (retry.Execute ());
		Assert.AreEqual (members
			? "-keepclassmembers class test.Live { *; }\n-keepclassmembers interface test.Live { *; }\n"
			: "-keep class test.Live\n-keep interface test.Live\n", File.ReadAllText (output));
		Assert.IsGreaterThan (old, File.GetLastWriteTimeUtc (output));
	}

	sealed class FailingClassWriter : GenerateTypeMapProguardConfiguration
	{
		protected override void WriteClassRule (TextWriter writer, string name)
		{
			base.WriteClassRule (writer, name);
			throw new IOException ("Injected write failure.");
		}
	}

	sealed class FailingMemberWriter : GenerateTypeMapMemberProguardConfiguration
	{
		protected override void WriteClassRule (TextWriter writer, string name)
		{
			base.WriteClassRule (writer, name);
			throw new IOException ("Injected write failure.");
		}
	}

	[TestMethod]
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

	[TestMethod]
	public void NoInputsReportsOutputPath ()
	{
		var path = Path.Combine (Root, "temp", TestName);
		Directory.CreateDirectory (path);
		var output = Path.Combine (path, "classes.cfg");
		var errors = new List<BuildErrorEventArgs> ();
		var engine = new MockBuildEngine (Console.Out, errors);
		var task = new GenerateTypeMapProguardConfiguration {
			BuildEngine = engine,
			OutputFile = output,
		};

		Assert.IsFalse (task.Execute ());
		Assert.AreEqual (1, errors.Count);
		Assert.AreEqual ("XA4328", errors [0].Code);
		Assert.Contains (output, errors [0].Message);
	}

	[TestMethod]
	public void InvalidOutputPathReportsGenerationError ()
	{
		var path = Path.Combine (Root, "temp", TestName);
		Directory.CreateDirectory (path);
		var input = Path.Combine (path, "input.keys");
		File.WriteAllText (input, "test/Foo");
		var errors = new List<BuildErrorEventArgs> ();
		var engine = new MockBuildEngine (Console.Out, errors);
		var task = new GenerateTypeMapProguardConfiguration {
			BuildEngine = engine,
			TypeMapKeyFiles = [new TaskItem (input)],
			OutputFile = "\0",
		};

		Assert.IsFalse (task.Execute ());
		Assert.AreEqual (1, errors.Count);
		Assert.AreEqual ("XA4328", errors [0].Code);
	}

	[TestMethod]
	public void MemberGeneratorScopesRulesToCanonicalKeys ()
	{
		var path = Path.Combine (Root, "temp", TestName);
		Directory.CreateDirectory (path);
		var first = Path.Combine (path, "first.keys");
		var second = Path.Combine (path, "second.keys");
		File.WriteAllText (first, "test/Peer\ntest/Contract\n");
		File.WriteAllText (second, "test/Peer\ntest/Base\n");
		var task = new GenerateTypeMapMemberProguardConfiguration {
			BuildEngine = new MockBuildEngine (Console.Out),
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

	[TestMethod]
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
			BuildEngine = new MockBuildEngine (Console.Out, errors),
			TypeMapKeyFiles = [new TaskItem (input)],
			OutputFile = output,
		};

		Assert.IsFalse (task.Execute ());
		Assert.AreEqual (1, errors.Count);
		Assert.AreEqual ("XA4328", errors [0].Code);
		Assert.AreEqual ("previous output", File.ReadAllText (output));
	}

	static GenerateTypeMapProguardConfiguration CreateTask (string input, string output) => new () {
		BuildEngine = new MockBuildEngine (Console.Out),
		TypeMapKeyFiles = [new TaskItem (input)],
		OutputFile = output,
	};
}
