#nullable enable
using System.Collections.Generic;
using System.IO;

using NUnit.Framework;
using Xamarin.Android.Tasks;

namespace Xamarin.Android.Build.Tests.Tasks;

[TestFixture]
public class RuntimePropertiesParserTests : BaseTest
{
	const string RuntimeConfigJson = """
		{
		  "runtimeOptions": {
		    "tfm": "net11.0",
		    "configProperties": {
		      "System.Reflection.Metadata.MetadataUpdater.IsSupported": false,
		      "System.StartupHookProvider.IsSupported": false,
		      "Microsoft.Android.Runtime.RuntimeFeature.IsAssignableFromCheck": true
		    }
		  }
		}
		""";

	string directory = "";
	string runtimeConfigPath = "";
	string runtimeConfigDevPath = "";

	[SetUp]
	public void SetUp ()
	{
		directory = Path.Combine (Root, "temp", TestName);
		Directory.CreateDirectory (directory);
		runtimeConfigPath = Path.Combine (directory, "App.runtimeconfig.json");
		runtimeConfigDevPath = Path.Combine (directory, "App.runtimeconfig.dev.json");
		File.WriteAllText (runtimeConfigPath, RuntimeConfigJson);
	}

	static Dictionary<string, string> Parse (string runtimeConfigPath, string? runtimeConfigDevPath = null) =>
		RuntimePropertiesParser.ParseConfig (runtimeConfigPath, runtimeConfigDevPath) ??
			throw new AssertionException ($"Could not parse '{runtimeConfigPath}'.");

	[Test]
	public void MissingConfigReturnsNull ()
	{
		Assert.IsNull (RuntimePropertiesParser.ParseConfig (Path.Combine (directory, "does-not-exist.json")));
	}

	[Test]
	public void ConfigPropertiesAreParsed ()
	{
		var properties = Parse (runtimeConfigPath);

		Assert.AreEqual ("false", properties ["System.Reflection.Metadata.MetadataUpdater.IsSupported"]);
		Assert.AreEqual ("true", properties ["Microsoft.Android.Runtime.RuntimeFeature.IsAssignableFromCheck"]);
	}

	[Test]
	public void DevConfigPropertiesWin ()
	{
		// This is what the .NET SDK emits for `Debug` builds
		File.WriteAllText (runtimeConfigDevPath, """
			{
			  "runtimeOptions": {
			    "configProperties": {
			      "System.Reflection.Metadata.MetadataUpdater.IsSupported": true,
			      "System.StartupHookProvider.IsSupported": true
			    }
			  }
			}
			""");

		var properties = Parse (runtimeConfigPath, runtimeConfigDevPath);

		Assert.AreEqual ("true", properties ["System.Reflection.Metadata.MetadataUpdater.IsSupported"], "Hot Reload should be enabled by the dev file.");
		Assert.AreEqual ("true", properties ["System.StartupHookProvider.IsSupported"], "Startup hooks should be enabled by the dev file.");
		Assert.AreEqual ("true", properties ["Microsoft.Android.Runtime.RuntimeFeature.IsAssignableFromCheck"], "Properties absent from the dev file should be preserved.");
	}

	[Test]
	public void DevConfigWithoutConfigPropertiesIsIgnored ()
	{
		// Pre-net6.0 style dev file, which only carries probing paths
		File.WriteAllText (runtimeConfigDevPath, """
			{
			  "runtimeOptions": {
			    "additionalProbingPaths": [ "/home/user/.nuget/packages" ]
			  }
			}
			""");

		var properties = Parse (runtimeConfigPath, runtimeConfigDevPath);

		Assert.AreEqual ("false", properties ["System.Reflection.Metadata.MetadataUpdater.IsSupported"]);
	}

	[Test]
	public void MissingDevConfigIsIgnored ()
	{
		var properties = Parse (runtimeConfigPath, runtimeConfigDevPath);

		Assert.AreEqual ("false", properties ["System.StartupHookProvider.IsSupported"]);
	}

	[Test]
	public void ConfigWithoutConfigPropertiesStillGetsDevProperties ()
	{
		// A project that sets no feature switches gets a `*.runtimeconfig.json` without `configProperties`
		File.WriteAllText (runtimeConfigPath, """
			{
			  "runtimeOptions": {
			    "tfm": "net11.0"
			  }
			}
			""");
		File.WriteAllText (runtimeConfigDevPath, """
			{
			  "runtimeOptions": {
			    "configProperties": {
			      "System.Reflection.Metadata.MetadataUpdater.IsSupported": true,
			      "System.StartupHookProvider.IsSupported": true
			    }
			  }
			}
			""");

		var properties = Parse (runtimeConfigPath, runtimeConfigDevPath);

		Assert.AreEqual ("true", properties ["System.Reflection.Metadata.MetadataUpdater.IsSupported"], "Hot Reload should be enabled by the dev file.");
		Assert.AreEqual ("true", properties ["System.StartupHookProvider.IsSupported"], "Startup hooks should be enabled by the dev file.");
	}
}
