using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Microsoft.Android.Tasks;
using NUnit.Framework;

namespace Xamarin.Android.Build.Tests;

[TestFixture]
public class GenerateCoreClrRuntimeProviderSourcesTests : BaseTest
{
	[TestCase (false)]
	[TestCase (true)]
	public void GeneratesAdditionalProcessProvider (bool relativePath)
	{
		var outputDirectory = Path.Combine (Root, "temp", TestName, "android");
		var task = new GenerateCoreClrRuntimeProviderSources {
			BuildEngine = new MockBuildEngine (TestContext.Out),
			TaskEnvironment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment (
				Path.GetDirectoryName (outputDirectory) ?? throw new InvalidOperationException ("Output directory has no parent."),
				new Dictionary<string, string> ()),
			AdditionalProviderSources = ["MonoRuntimeProvider_1", "MonoRuntimeProvider_2"],
			OutputDirectory = relativePath ? "android" : outputDirectory,
		};

		Assert.IsTrue (task.Execute (), "The additional provider should be generated.");
		string template = File.ReadAllText (Path.Combine (NativeAotBootstrapTestTools.FindRepositoryRoot (),
			"src", "Xamarin.Android.Build.Tasks", "Resources", "MonoRuntimeProvider.Bundled.java"));
		foreach (string name in task.AdditionalProviderSources) {
			var provider = Path.Combine (outputDirectory, "src", "mono", name + ".java");
			FileAssert.Exists (provider);
			CollectionAssert.AreEqual (Encoding.UTF8.GetBytes (template.Replace ("MonoRuntimeProvider", name)),
				File.ReadAllBytes (provider), "The generated Java bytes must match the original template substitution.");
			var timestamp = new DateTime (2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
			File.SetLastWriteTimeUtc (provider, timestamp);
			Assert.IsTrue (task.Execute ());
			Assert.AreEqual (timestamp, File.GetLastWriteTimeUtc (provider), "Unchanged providers must not be rewritten.");
			File.WriteAllText (provider, "stale provider");
			Assert.IsTrue (task.Execute ());
			CollectionAssert.AreEqual (Encoding.UTF8.GetBytes (template.Replace ("MonoRuntimeProvider", name)), File.ReadAllBytes (provider));
		}
		Assert.AreEqual (relativePath ? "android" : outputDirectory, task.OutputDirectory);
	}

	[Test]
	public void EmptyProvidersDoNotCreateOutputDirectory ()
	{
		string directory = Path.Combine (Root, "temp", TestName, "android");
		var task = new GenerateCoreClrRuntimeProviderSources {
			BuildEngine = new MockBuildEngine (TestContext.Out),
			OutputDirectory = directory,
		};
		Assert.IsTrue (task.Execute ());
		Assert.IsFalse (Directory.Exists (directory));
	}

	[Test]
	public void ConcurrentProjectsResolveTheirOwnRelativeOutputPaths ()
	{
		string root = Path.Combine (Root, "temp", TestName);
		Parallel.For (0, 2, i => {
			string directory = Path.Combine (root, $"project{i}");
			var task = new GenerateCoreClrRuntimeProviderSources {
				BuildEngine = new MockBuildEngine (TextWriter.Null),
				TaskEnvironment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment (directory, new Dictionary<string, string> ()),
				AdditionalProviderSources = [$"MonoRuntimeProvider_{i}"],
				OutputDirectory = "android",
			};
			Assert.IsTrue (task.Execute ());
			FileAssert.Exists (Path.Combine (directory, "android", "src", "mono", $"MonoRuntimeProvider_{i}.java"));
		});
	}

	[Test]
	public void OutputFailureIsReportedWithExistingTaskPrefix ()
	{
		string directory = Path.Combine (Root, "temp", TestName);
		Directory.CreateDirectory (directory);
		string output = Path.Combine (directory, "android");
		File.WriteAllText (output, "not a directory");
		var errors = new List<BuildErrorEventArgs> ();
		var task = new GenerateCoreClrRuntimeProviderSources {
			BuildEngine = new MockBuildEngine (TestContext.Out, errors),
			OutputDirectory = output,
			AdditionalProviderSources = ["MonoRuntimeProvider_1"],
		};
		Assert.IsFalse (task.Execute ());
		Assert.AreEqual (1, errors.Count);
		StringAssert.StartsWith ("XAGCRP", errors [0].Code);
		Assert.AreEqual ("GCRP", task.TaskPrefix);
	}

	[TestCase (false)]
	[TestCase (true)]
	public void ShippedRegistrationExecutesTask (bool multithreaded)
	{
		string directory = Path.Combine (Root, "temp", TestName);
		Directory.CreateDirectory (directory);
		string targetsPath = Path.Combine (NativeAotBootstrapTestTools.FindRepositoryRoot (),
			"src", "Xamarin.Android.Build.Tasks", "Microsoft.Android.Sdk", "targets", "Microsoft.Android.Sdk.TypeMap.Trimmable.targets");
		string projectFile = Path.Combine (directory, "providers.proj");
		new XDocument (
			new XElement ("Project",
				new XElement ("PropertyGroup",
					new XElement ("_MicrosoftAndroidBuildTasksAssembly", typeof (GenerateCoreClrRuntimeProviderSources).Assembly.Location),
					new XElement ("_XamarinAndroidBuildTasksAssembly", typeof (GenerateCoreClrRuntimeProviderSources).Assembly.Location)),
				new XElement ("Import", new XAttribute ("Project", targetsPath)),
				new XElement ("Target", new XAttribute ("Name", "Generate"),
					new XElement ("GenerateCoreClrRuntimeProviderSources",
						new XAttribute ("OutputDirectory", "android"),
						new XAttribute ("AdditionalProviderSources", "MonoRuntimeProvider_1")))))
			.Save (projectFile);
		string dotnet = Environment.GetEnvironmentVariable ("DOTNET_HOST_PATH") ?? "dotnet";
		var arguments = new List<string> {
			"msbuild", projectFile, "-t:Generate", "-m", "-nr:false", "-warnaserror:MSB4286,MSB4287,MSB4288", "-v:quiet",
		};
		if (multithreaded) {
			arguments.Add ("-mt");
		}
		var result = NativeAotBootstrapTestTools.Run (dotnet, arguments.ToArray ());
		Assert.AreEqual (0, result.ExitCode, result.Output + result.Error);
		FileAssert.Exists (Path.Combine (directory, "android", "src", "mono", "MonoRuntimeProvider_1.java"));
	}

	[Test]
	public void RegistrationUsesModernAssemblyOnBothRuntimes ()
	{
		var targets = XDocument.Load (Path.Combine (NativeAotBootstrapTestTools.FindRepositoryRoot (),
			"src", "Xamarin.Android.Build.Tasks", "Microsoft.Android.Sdk", "targets", "Microsoft.Android.Sdk.TypeMap.Trimmable.targets"));
		var registrations = targets.Descendants ("UsingTask").Where (element =>
			(string?)element.Attribute ("TaskName") == "Microsoft.Android.Tasks.GenerateCoreClrRuntimeProviderSources").ToArray ();
		Assert.AreEqual (2, registrations.Length);
		Assert.That (registrations.Select (element => (string?)element.Attribute ("AssemblyFile")), Is.All.EqualTo ("$(_MicrosoftAndroidBuildTasksAssembly)"));
		Assert.That (registrations.Select (element => (string?)element.Attribute ("Runtime")), Is.All.EqualTo ("NET"));
		Assert.AreEqual ("TaskHostFactory", (string?)registrations.Single (element =>
			(string?)element.Attribute ("Condition") == " '$(MSBuildRuntimeType)' == 'Full' ").Attribute ("TaskFactory"));
		Assert.IsNull (registrations.Single (element =>
			(string?)element.Attribute ("Condition") == " '$(MSBuildRuntimeType)' == 'Core' ").Attribute ("TaskFactory"));
		Assert.IsFalse (targets.Descendants ("UsingTask").Any (element =>
			(string?)element.Attribute ("TaskName") == "Xamarin.Android.Tasks.GenerateCoreClrRuntimeProviderSources"));
	}
}
