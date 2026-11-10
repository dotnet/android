#nullable enable

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Android.Tasks;
using Microsoft.Build.Framework;
using NUnit.Framework;

namespace Xamarin.Android.Build.Tests.Tasks
{
	[TestFixture]
	public class BuildAppBundleTests : BaseTest
	{
		const string BundleConfigPrefix = "BundleConfig.json: ";

		[Test]
		public void BuildAppBundleCommand ()
		{
			var task = new BuildAppBundle {
				BaseZip = "base.zip",
				Output = "foo.aab",
				JarPath = "bundletool.jar",
			};
			string cmd = task.GetCommandLineBuilder ().ToString ();
			Assert.AreEqual ("-jar bundletool.jar build-bundle --modules base.zip --output foo.aab", cmd);
		}

		[Test]
		public void BundleConfigMergesCustomConfiguration ()
		{
			var directory = Path.Combine (Root, "temp", TestName);
			Directory.CreateDirectory (directory);
			var customConfig = Path.Combine (directory, "BundleConfig.json");
			File.WriteAllText (customConfig, """
				{
					"optimizations": { "splitsConfig": { "splitDimension": [ { "value": "ABI", "negate": false } ] } },
					"compression": { "uncompressedGlob": [ "custom/**" ], "level": 1.50 },
				}
				""");
			var output = Path.Combine (directory, "foo.aab");
			File.WriteAllText (output, "stale");

			var messages = new List<BuildMessageEventArgs> ();
			var errors = new List<BuildErrorEventArgs> ();
			var task = new BuildAppBundle {
				BuildEngine = new MockBuildEngine (TestContext.Out, errors, messages: messages),
				BaseZip = "base.zip",
				Output = output,
				JarPath = "bundletool.jar",
				CustomBuildConfigFile = customConfig,
				UncompressedFileExtensions = ".foo;.bar,.baz",
				// Fail before launching java; the merged configuration is logged first.
				ToolPath = Path.Combine (directory, "does-not-exist"),
			};

			task.Execute ();
			Assert.IsNotEmpty (errors, "Execute should log an error without a java executable.");
			Assert.IsFalse (File.Exists (output), "A stale output should be deleted before bundletool runs.");

			var config = messages.Select (m => m.Message ?? "").Single (m => m.StartsWith (BundleConfigPrefix, System.StringComparison.Ordinal));
			Assert.AreEqual (ExpectedMergedConfig.ReplaceLineEndings (), config.Substring (BundleConfigPrefix.Length).ReplaceLineEndings ());
		}

		const string ExpectedMergedConfig = """
			{
			  "optimizations": {
			    "splitsConfig": {
			      "splitDimension": [
			        {
			          "value": "ABI",
			          "negate": false
			        }
			      ]
			    }
			  },
			  "compression": {
			    "uncompressedGlob": [
			      "custom/**",
			      "assemblies/**",
			      "**/*.3g2",
			      "**/*.3gp",
			      "**/*.3gpp",
			      "**/*.3gpp2",
			      "**/*.aac",
			      "**/*.amr",
			      "**/*.awb",
			      "**/*.gif",
			      "**/*.imy",
			      "**/*.jet",
			      "**/*.jpeg",
			      "**/*.jpg",
			      "**/*.m4a",
			      "**/*.m4v",
			      "**/*.mid",
			      "**/*.midi",
			      "**/*.mkv",
			      "**/*.mp2",
			      "**/*.mp3",
			      "**/*.mp4",
			      "**/*.mpeg",
			      "**/*.mpg",
			      "**/*.ogg",
			      "**/*.png",
			      "**/*.rtttl",
			      "**/*.smf",
			      "**/*.wav",
			      "**/*.webm",
			      "**/*.wma",
			      "**/*.wmv",
			      "**/*.xmf",
			      "**/*.foo",
			      "**/*.bar",
			      "**/*.baz"
			    ],
			    "level": 1.50
			  }
			}
			""";
	}
}
