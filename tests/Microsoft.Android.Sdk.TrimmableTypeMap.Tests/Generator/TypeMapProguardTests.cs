using System;
using System.IO;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Android.Tasks;
using Xunit;

namespace Microsoft.Android.Sdk.TrimmableTypeMap.Tests;

public class TypeMapProguardTests : IDisposable
{
	readonly string directory = Path.Combine (Path.GetTempPath (), "typemap-proguard-" + Guid.NewGuid ().ToString ("N"));
	readonly TypeMapTaskBuildEngine engine = new ();

	public TypeMapProguardTests () => Directory.CreateDirectory (directory);

	[Fact]
	public void GeneratorUnionsCanonicalKeysAndWritesClassAndInterfaceRules ()
	{
		var first = Write ("first.keys", "test/Outer$Inner\r\nandroid/app/Activity\r\n\r\ntest/Caf\u00e9\n");
		var second = Write ("second.keys", "android/app/Activity\ntest/\U00010428Peer\n");
		var task = CreateGenerator (first, second);
		Assert.True (task.Execute ());
		var expected =
			"-keep class android.app.Activity\n-keep interface android.app.Activity\n" +
			"-keep class test.Caf\u00e9\n-keep interface test.Caf\u00e9\n" +
			"-keep class test.Outer$Inner\n-keep interface test.Outer$Inner\n" +
			"-keep class test.\U00010428Peer\n-keep interface test.\U00010428Peer\n";
		Assert.Equal (new UTF8Encoding (false).GetBytes (expected), File.ReadAllBytes (task.OutputFile));
	}

	[Fact]
	public void MemberGeneratorScopesRulesToCanonicalKeys ()
	{
		var task = new GenerateTypeMapMemberProguardConfiguration {
			BuildEngine = engine,
			TypeMapKeyFiles = [
				new TaskItem (Write ("first.keys", "test/Peer\ntest/Contract\n")),
				new TaskItem (Write ("second.keys", "test/Peer\ntest/Base\n")),
			],
			OutputFile = Path.Combine (directory, "members.cfg"),
		};
		Assert.True (task.Execute ());
		Assert.Equal (
			"-keepclassmembers class test.Base { *; }\n-keepclassmembers interface test.Base { *; }\n" +
			"-keepclassmembers class test.Contract { *; }\n-keepclassmembers interface test.Contract { *; }\n" +
			"-keepclassmembers class test.Peer { *; }\n-keepclassmembers interface test.Peer { *; }\n",
			File.ReadAllText (task.OutputFile));
	}

	[Fact]
	public void MemberGeneratorRejectsInvalidKeysWithoutOverwritingOutput ()
	{
		var output = Write ("members.cfg", "previous output");
		var task = new GenerateTypeMapMemberProguardConfiguration {
			BuildEngine = engine,
			TypeMapKeyFiles = [new TaskItem (Write ("invalid.keys", "test/Peer\ntest/*\n"))],
			OutputFile = output,
		};
		Assert.False (task.Execute ());
		Assert.Contains (engine.Errors, error => error.Code == "XA4328");
		Assert.Equal ("previous output", File.ReadAllText (output));
	}

	[Theory]
	[InlineData ("test/*")]
	[InlineData ("test/Foo { *; }")]
	[InlineData ("test/Foo[1]")]
	[InlineData ("test.Foo")]
	[InlineData ("test//Foo")]
	[InlineData ("/test/Foo")]
	[InlineData ("test/Foo/")]
	[InlineData ("test/Foo;")]
	[InlineData ("test/Foo\n-keep class **")]
	[InlineData ("\ufefftest/Foo")]
	public void GeneratorRejectsRuleInjectionAndNoncanonicalKeys (string input)
	{
		var task = CreateGenerator (Write ("invalid.keys", input));
		Assert.False (task.Execute ());
		Assert.Contains (engine.Errors, error => error.Code == "XA4328");
		Assert.False (File.Exists (task.OutputFile));
	}

	[Fact]
	public void EmptyMapOverwritesAndRefreshesRealOutput ()
	{
		var task = CreateGenerator (Write ("empty.keys", ""));
		Assert.True (task.Execute ());
		var old = DateTime.UtcNow.AddDays (-1);
		File.SetLastWriteTimeUtc (task.OutputFile, old);
		Assert.True (task.Execute ());
		Assert.Empty (File.ReadAllBytes (task.OutputFile));
		Assert.True (File.GetLastWriteTimeUtc (task.OutputFile) > old);
	}

	[Fact]
	public void MissingMapIsAnError ()
	{
		var task = CreateGenerator (Path.Combine (directory, "missing.keys"));
		Assert.False (task.Execute ());
		Assert.Contains (engine.Errors, error => error.Code == "XA4328");
	}

	GenerateTypeMapProguardConfiguration CreateGenerator (params string [] inputs)
	{
		var items = new ITaskItem [inputs.Length];
		for (int i = 0; i < inputs.Length; i++) {
			items [i] = new TaskItem (inputs [i]);
		}
		return new GenerateTypeMapProguardConfiguration {
			BuildEngine = engine,
			TypeMapKeyFiles = items,
			OutputFile = Path.Combine (directory, "rules", "classes.cfg"),
		};
	}

	string Write (string name, string content)
	{
		var path = Path.Combine (directory, name);
		File.WriteAllText (path, content, new UTF8Encoding (false));
		return path;
	}

	public void Dispose () => Directory.Delete (directory, recursive: true);
}
