using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

using NUnit.Framework;

namespace Xamarin.Android.Build.Tests;

[TestFixture]
public class JavaRuntimeTargetsTests : BaseTest
{
	[TestCase ("Build")]
	[TestCase ("Clean")]
	public void RetiredArtifactsAreRemovedWithoutRegeneratingRetainedOutputs (string target)
	{
		string directory = Path.Combine (Root, "temp", TestName);
		string output = Path.Combine (directory, "output");
		Directory.CreateDirectory (output);
		string targets = Path.Combine (NativeAotBootstrapTestTools.FindRepositoryRoot (), "src", "java-runtime", "java-runtime.targets");
		string project = Path.Combine (directory, "runtime.proj");
		new XDocument (
			new XElement ("Project",
				new XElement ("PropertyGroup",
					new XElement ("OutputPath", output + Path.DirectorySeparatorChar),
					new XElement ("IntermediateOutputPath", Path.Combine (directory, "obj") + Path.DirectorySeparatorChar),
					new XElement ("JavaCPath", "unexpected-javac-invocation"),
					new XElement ("JavaPath", "unexpected-d8-invocation")),
				new XElement ("Import", new XAttribute ("Project", targets)),
				new XElement ("Target", new XAttribute ("Name", "Build")),
				new XElement ("Target", new XAttribute ("Name", "Clean"))))
			.Save (project);

		string [] retired = [
			"java_runtime.jar", "java_runtime.dex",
			"java_runtime_fastdev.jar", "java_runtime_fastdev.dex",
			"java_runtime_clr.jar", "java_runtime_clr.dex",
			"java_runtime_fastdev_clr.jar", "java_runtime_fastdev_clr.dex",
		];
		string [] retained = ["java_runtime_trimmable.jar", "java_runtime_trimmable.dex", "mono_android_Runtime.h", "custom-runtime.jar"];
		DateTime timestamp = new [] { File.GetLastWriteTimeUtc (project), File.GetLastWriteTimeUtc (targets) }.Max ().AddSeconds (2);
		foreach (string name in retained) {
			string file = Path.Combine (output, name);
			File.WriteAllText (file, name);
			File.SetLastWriteTimeUtc (file, timestamp);
		}

		string dotnet = Environment.GetEnvironmentVariable ("DOTNET_HOST_PATH") ?? "dotnet";
		for (int run = 0; run < 2; run++) {
			foreach (string name in retired) {
				File.WriteAllText (Path.Combine (output, name), "stale");
			}
			var result = NativeAotBootstrapTestTools.Run (dotnet, "msbuild", project, "-t:" + target, "-v:normal", "-nr:false");
			Assert.AreEqual (0, result.ExitCode, result.Output + result.Error);
			foreach (string name in retired) {
				FileAssert.DoesNotExist (Path.Combine (output, name), $"{target} must remove retired artifact {name} on every run.");
			}
			foreach (string name in retained) {
				string file = Path.Combine (output, name);
				if (target == "Clean" && name == "java_runtime_trimmable.jar") {
					FileAssert.DoesNotExist (file, "Clean still removes the retained JAR through its existing cleanup target.");
					continue;
				}
				FileAssert.Exists (file, $"{target} must not remove {name} as a retired artifact.");
				Assert.AreEqual (name, File.ReadAllText (file));
				Assert.AreEqual (timestamp, File.GetLastWriteTimeUtc (file), "Cleanup must not rewrite retained or unrelated outputs.");
			}
			if (target == "Build") {
				StringAssert.Contains ("Skipping target \"_BuildJavaRuntimeJar\"", result.Output);
				StringAssert.Contains ("Skipping target \"_GenerateRuntimeDex16\"", result.Output);
			}
		}

		if (target == "Build" && !OperatingSystem.IsWindows ()) {
			File.WriteAllText (Path.Combine (output, retired [0]), "cannot delete");
			var permissions = File.GetUnixFileMode (output);
			try {
				File.SetUnixFileMode (output, permissions & ~(UnixFileMode.UserWrite | UnixFileMode.GroupWrite | UnixFileMode.OtherWrite));
				var result = NativeAotBootstrapTestTools.Run (dotnet, "msbuild", project, "-t:Build", "-v:normal", "-nr:false");
				Assert.AreNotEqual (0, result.ExitCode, "A failed retired-artifact deletion must fail the build.\n" + result.Output + result.Error);
			} finally {
				File.SetUnixFileMode (output, permissions);
			}
		}
	}
}
