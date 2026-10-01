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
using Xamarin.Android.Tasks;

namespace Xamarin.Android.Build.Tests;

[TestFixture]
public class GenerateNativeAotBootstrapSourcesTests : BaseTest
{
	[TestCase ("plain", "plain")]
	[TestCase ("\"\\", "\\\"\\\\")]
	[TestCase ("\b\t\n\f\r", "\\b\\t\\n\\f\\r")]
	[TestCase ("\u0001\u007f", "\\u0001\\u007f")]
	[TestCase ("\u00e9\u4e2d\ud83d\ude80", "\\u00e9\\u4e2d\\ud83d\\ude80")]
	[TestCase ("\\u000a", "\\\\u000a")]
	public void EscapeJavaStrings (string value, string expected)
	{
		Assert.AreEqual (expected, GenerateNativeAotBootstrapSources.EscapeJavaString (value));
	}

	[Test]
	public void EnvironmentOrderingAndClassificationRemainUnchanged ()
	{
		var environment = new EnvironmentBuilder (escapeValues: false);
		environment.AddEnvironmentVariableLine (" # comment ");
		environment.AddEnvironmentVariableLine ("");
		environment.AddEnvironmentVariableLine ("DOTNET_FIRST=initial");
		environment.AddEnvironmentVariableLine ("DOTNET_SECOND=two=three");
		environment.AddEnvironmentVariableLine ("debug.dotnet.log=initial");
		environment.AddEnvironmentVariableLine ("DOTNET_FIRST=overridden");
		environment.AddEnvironmentVariableLine ("debug.dotnet.log=all");
		environment.AddEnvironmentVariableLine ("_OVERRIDE=\"C:\\test\"");
		environment.AddEnvironmentVariableLine ("debug.empty");

		CollectionAssert.AreEqual (new [] { "DOTNET_FIRST", "DOTNET_SECOND", "_OVERRIDE" }, environment.EnvironmentVariables.Keys);
		Assert.AreEqual ("overridden", environment.EnvironmentVariables ["DOTNET_FIRST"]);
		Assert.AreEqual ("two=three", environment.EnvironmentVariables ["DOTNET_SECOND"]);
		Assert.AreEqual ("\"C:\\test\"", environment.EnvironmentVariables ["_OVERRIDE"]);
		Assert.AreEqual ("all", environment.SystemProperties ["debug.dotnet.log"]);
		Assert.AreEqual ("", environment.SystemProperties ["debug.empty"]);

		var legacy = new EnvironmentBuilder ();
		legacy.AddEnvironmentVariable ("_OVERRIDE", "\"C:\\test\"");
		Assert.AreEqual ("\\\"C:\\\\test\\\"", legacy.EnvironmentVariables ["_OVERRIDE"]);
	}

	[Test]
	public void GeneratesBothKindsOfDataAndAdditionalProviders ()
	{
		string path = Path.Combine (Root, "temp", TestName);
		Directory.CreateDirectory (path);
		string environmentFile = Path.Combine (path, "environment.txt");
		string libraryFile = Path.Combine (path, "library.txt");
		File.WriteAllText (environmentFile, """
			DOTNET_VALUE="C:\test"
			debug.dotnet.log=initial
			DOTNET_TEMPLATE=@ENVIRONMENT_VAR_VALUES@
			""");
		File.WriteAllText (libraryFile, """
			DOTNET_VALUE=library override
			debug.dotnet.log=all
			debug.dotnet.max_grefc=1234
			""");
		var task = new GenerateNativeAotBootstrapSources {
			BuildEngine = new MockBuildEngine (TestContext.Out),
			OutputDirectory = path,
			TargetName = "My\"App\\Name",
			Environments = [new TaskItem (environmentFile), new TaskItem (libraryFile)],
			AdditionalProviderSources = ["NativeAotRuntimeProvider_1", "NativeAotRuntimeProvider_2"],
		};
		Assert.IsTrue (task.Execute ());
		Assert.AreEqual (4, task.GeneratedSources.Length);
		Assert.IsTrue (task.GeneratedSources.All (File.Exists));

		string source = File.ReadAllText (task.GeneratedSources [1]);
		StringAssert.Contains ("\"DOTNET_VALUE\"", source);
		StringAssert.Contains ("\"library override\"", source);
		StringAssert.Contains ("\"debug.dotnet.log\",\n\t\t\"all\"", source);
		StringAssert.Contains ("\"debug.dotnet.max_grefc\",\n\t\t\"1234\"", source);
		StringAssert.Contains ("\"@ENVIRONMENT_VAR_VALUES@\"", source, "Input text must not be interpreted as a template token.");
		StringAssert.DoesNotContain ("System.setProperty", source);
		StringAssert.DoesNotContain ("\"initial\"", source);
		StringAssert.Contains ("My\\\"App\\\\Name", File.ReadAllText (task.GeneratedSources [0]));
		for (int i = 1; i <= 2; i++) {
			string provider = File.ReadAllText (task.GeneratedSources [i + 1]);
			StringAssert.Contains ($"class NativeAotRuntimeProvider_{i}", provider);
			StringAssert.Contains ("NativeAotEnvironmentVars.Initialize ();", provider);
			StringAssert.Contains ("JavaInteropRuntime.loadLibrary(context);", provider);
		}
	}

	[Test]
	public void EmptyEnvironmentStillGeneratesBootstrapSources ()
	{
		var task = new GenerateNativeAotBootstrapSources {
			BuildEngine = new MockBuildEngine (TestContext.Out),
			OutputDirectory = Path.Combine (Root, "temp", TestName),
			TargetName = "App",
		};
		Assert.IsTrue (task.Execute ());
		Assert.AreEqual (2, task.GeneratedSources.Length);
		string source = File.ReadAllText (task.GeneratedSources [1]);
		StringAssert.Contains ("static final String[] systemProperties = new String[] {\n\n\t};", source);
		StringAssert.DoesNotContain ("@SYSTEM_PROPERTIES@", source);
	}

	[Test]
	public void JavaQuotingIsCultureInvariant ()
	{
		CultureInfo previous = CultureInfo.CurrentCulture;
		try {
			CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo ("tr-TR");
			Assert.AreEqual ("\\u0130\\ud83d\\ude80", GenerateNativeAotBootstrapSources.EscapeJavaString ("\u0130\ud83d\ude80"));
		} finally {
			CultureInfo.CurrentCulture = previous;
		}
	}

	[TestCase ("=value")]
	[TestCase ("DOTNET_VALUE=bad\0value")]
	[TestCase ("debug.property=bad\0value")]
	public void InvalidEnvironmentFailsBeforeWritingSources (string contents)
	{
		string path = Path.Combine (Root, "temp", TestName);
		Directory.CreateDirectory (path);
		string environmentFile = Path.Combine (path, "environment.txt");
		File.WriteAllText (environmentFile, contents);
		var errors = new List<BuildErrorEventArgs> ();
		var task = new GenerateNativeAotBootstrapSources {
			BuildEngine = new MockBuildEngine (TestContext.Out, errors),
			OutputDirectory = path,
			TargetName = "App",
			Environments = [new TaskItem (environmentFile)],
		};
		Assert.IsFalse (task.Execute ());
		Assert.IsNotEmpty (errors);
		Assert.IsEmpty (task.GeneratedSources);
	}

	[Test]
	public void MissingEnvironmentFileIsAnError ()
	{
		var errors = new List<BuildErrorEventArgs> ();
		var task = new GenerateNativeAotBootstrapSources {
			BuildEngine = new MockBuildEngine (TestContext.Out, errors),
			OutputDirectory = Path.Combine (Root, "temp", TestName),
			TargetName = "App",
			Environments = [new TaskItem (Path.Combine (Root, "temp", TestName, "missing.txt"))],
		};
		Assert.IsFalse (task.Execute ());
		Assert.IsNotEmpty (errors);
	}
}
