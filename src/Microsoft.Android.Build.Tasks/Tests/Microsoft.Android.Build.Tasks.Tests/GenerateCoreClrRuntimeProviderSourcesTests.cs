using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using NUnit.Framework;
using Microsoft.Android.Tasks;

namespace Xamarin.Android.Build.Tests;

[TestFixture]
public class GenerateCoreClrRuntimeProviderSourcesTests : BaseTest
{
	[Test]
	public void GeneratesAdditionalProcessProvider ()
	{
		var outputDirectory = Path.Combine (Root, "temp", TestName, "android");
		var task = new GenerateCoreClrRuntimeProviderSources {
			BuildEngine = new MockBuildEngine (TestContext.Out),
			AdditionalProviderSources = ["MonoRuntimeProvider_1"],
			OutputDirectory = outputDirectory,
		};

		Assert.IsTrue (task.Execute (), "The additional provider should be generated.");
		var provider = Path.Combine (outputDirectory, "src", "mono", "MonoRuntimeProvider_1.java");
		FileAssert.Exists (provider);
		StringAssert.Contains ("class MonoRuntimeProvider_1", File.ReadAllText (provider));
	}

	[Test]
	public void RemovesObsoleteNumberedProviders ()
	{
		var outputDirectory = Path.Combine (Root, "temp", TestName, "android");
		var task = new GenerateCoreClrRuntimeProviderSources {
			BuildEngine = new MockBuildEngine (TestContext.Out),
			AdditionalProviderSources = ["MonoRuntimeProvider_1", "MonoRuntimeProvider_2"],
			OutputDirectory = outputDirectory,
		};

		Assert.IsTrue (task.Execute ());
		var sourceDirectory = Path.Combine (outputDirectory, "src", "mono");
		var first = Path.Combine (sourceDirectory, "MonoRuntimeProvider_1.java");
		var second = Path.Combine (sourceDirectory, "MonoRuntimeProvider_2.java");
		var unrelated = Path.Combine (sourceDirectory, "MonoRuntimeProvider_custom.java");
		var similarName = Path.Combine (sourceDirectory, "MonoRuntimeProvider_1backup.java");
		File.WriteAllText (unrelated, "unrelated");
		File.WriteAllText (similarName, "unrelated");
		var firstTimestamp = File.GetLastWriteTimeUtc (first);

		task.AdditionalProviderSources = ["MonoRuntimeProvider_1"];
		Assert.IsTrue (task.Execute ());
		FileAssert.Exists (first);
		FileAssert.DoesNotExist (second);
		FileAssert.Exists (unrelated);
		FileAssert.Exists (similarName);
		CollectionAssert.AreEquivalent (new [] { second }, task.DeletedSources);
		Assert.AreEqual (firstTimestamp, File.GetLastWriteTimeUtc (first));

		task.AdditionalProviderSources = [];
		Assert.IsTrue (task.Execute ());
		FileAssert.DoesNotExist (first);
		FileAssert.Exists (unrelated);
		FileAssert.Exists (similarName);
		CollectionAssert.AreEquivalent (new [] { first }, task.DeletedSources);
		Assert.IsTrue (task.Execute ());
		Assert.IsEmpty (task.DeletedSources);
	}

	[Test]
	public void RemovedProvidersInvalidateJavaCompilation ()
	{
		string directory = Path.Combine (Root, "temp", TestName);
		Directory.CreateDirectory (directory);
		string input = Path.Combine (directory, "input");
		string targetStamp = Path.Combine (directory, "typemap.stamp");
		string javaStamp = Path.Combine (directory, "javac.stamp");
		string targets = Path.Combine (NativeAotBootstrapTestTools.FindRepositoryRoot (), "src", "Xamarin.Android.Build.Tasks",
			"Microsoft.Android.Sdk", "targets", "Microsoft.Android.Sdk.TypeMap.Trimmable.targets");
		var target = XDocument.Load (targets).Descendants ()
			.Single (element => element.Name.LocalName == "Target" && (string?) element.Attribute ("Name") == "_GenerateTrimmableTypeMap");
		var generate = new XElement (target.Elements ().Single (element => element.Name.LocalName == "GenerateCoreClrRuntimeProviderSources"));
		var invalidate = new XElement (target.Elements ().Single (element => element.Name.LocalName == "Delete" &&
			(string?) element.Attribute ("Files") == "$(_AndroidCompileJavaStampFile)"));
		foreach (var element in generate.DescendantsAndSelf ().Concat (invalidate.DescendantsAndSelf ())) {
			element.Name = element.Name.LocalName;
		}

		string project = Path.Combine (directory, "providers.proj");
		new XDocument (
			new XElement ("Project",
				new XElement ("PropertyGroup",
					new XElement ("_AndroidRuntime", "CoreCLR"),
					new XElement ("IntermediateOutputPath", directory + Path.DirectorySeparatorChar),
					new XElement ("_AndroidCompileJavaStampFile", javaStamp)),
				new XElement ("UsingTask",
					new XAttribute ("TaskName", "Microsoft.Android.Tasks.GenerateCoreClrRuntimeProviderSources"),
					new XAttribute ("AssemblyFile", typeof (GenerateCoreClrRuntimeProviderSources).Assembly.Location)),
				new XElement ("Target", new XAttribute ("Name", "Generate"), new XAttribute ("Inputs", input),
					new XAttribute ("Outputs", targetStamp),
					new XElement ("ItemGroup",
						new XElement ("_AdditionalProviderSources", new XAttribute ("Include", "$(Providers)"))),
					generate,
					invalidate,
					new XElement ("Touch", new XAttribute ("Files", targetStamp), new XAttribute ("AlwaysCreate", "true")))))
			.Save (project);
		File.WriteAllText (input, "input");
		File.WriteAllText (javaStamp, "compiled");
		string provider = Path.Combine (directory, "android", "src", "mono", "MonoRuntimeProvider_1.java");

		RunTarget ("-p:Providers=MonoRuntimeProvider_1");
		FileAssert.Exists (provider);
		FileAssert.Exists (javaStamp);
		var timestamp = File.GetLastWriteTimeUtc (provider);
		var targetTimestamp = File.GetLastWriteTimeUtc (targetStamp);
		RunTarget ("-p:Providers=MonoRuntimeProvider_1");
		FileAssert.Exists (javaStamp);
		Assert.AreEqual (timestamp, File.GetLastWriteTimeUtc (provider));
		Assert.AreEqual (targetTimestamp, File.GetLastWriteTimeUtc (targetStamp), "An unchanged target should be skipped.");

		File.SetLastWriteTimeUtc (targetStamp, DateTime.UtcNow.AddMinutes (-1));
		RunTarget ();
		FileAssert.DoesNotExist (provider);
		FileAssert.DoesNotExist (javaStamp);
		File.WriteAllText (javaStamp, "compiled again");
		targetTimestamp = File.GetLastWriteTimeUtc (targetStamp);
		RunTarget ();
		FileAssert.Exists (javaStamp);
		Assert.AreEqual (targetTimestamp, File.GetLastWriteTimeUtc (targetStamp), "An unchanged empty provider set should be skipped.");

		void RunTarget (params string [] properties)
		{
			string dotnet = Environment.GetEnvironmentVariable ("DOTNET_HOST_PATH") ?? "dotnet";
			var arguments = new [] { "msbuild", project, "-t:Generate", "-v:quiet", "-nr:false" }
				.Concat (properties).ToArray ();
			var result = NativeAotBootstrapTestTools.Run (dotnet, arguments);
			Assert.AreEqual (0, result.ExitCode, result.Output + result.Error);
		}
	}
}
