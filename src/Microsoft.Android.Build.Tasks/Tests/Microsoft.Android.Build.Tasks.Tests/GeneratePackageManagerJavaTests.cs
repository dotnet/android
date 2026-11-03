using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Android.Tasks;
using Microsoft.Build.Framework;
using NUnit.Framework;

namespace Xamarin.Android.Build.Tests;

[TestFixture]
public class GeneratePackageManagerJavaTests
{
	const string OutputFileName = "MonoPackageManager_Resources.java";

	[TestCase ("linked/HelloAndroid.dll", false)]
	[TestCase ("HelloAndroid.dll", true)]
	[TestCase ("linked/H\u00e9lloAndroid.dll", false)]
	[TestCase ("", true)]
	public void GeneratesExactUtf8WithoutBomAndPreservesUnchangedFiles (string mainAssembly, bool absoluteOutput)
	{
		string directory = CreateProjectDirectory ();
		try {
			string output = Path.Combine ("android", "src", "mono");
			var task = CreateTask (directory, mainAssembly, absoluteOutput ? Path.Combine (directory, output) : output);
			string file = Path.Combine (directory, output, OutputFileName);

			Assert.IsTrue (task.Execute ());
			CollectionAssert.AreEqual (ExpectedBytes (Path.GetFileName (mainAssembly)), File.ReadAllBytes (file));
			Assert.AreEqual (mainAssembly, task.MainAssembly);
			Assert.AreEqual (absoluteOutput ? Path.Combine (directory, output) : output, task.OutputDirectory);

			File.SetLastWriteTimeUtc (file, new DateTime (2020, 1, 2, 3, 4, 5, DateTimeKind.Utc));
			DateTime timestamp = File.GetLastWriteTimeUtc (file);
			Assert.IsTrue (task.Execute ());
			Assert.AreEqual (timestamp, File.GetLastWriteTimeUtc (file), "Identical output must not be rewritten.");

			task.MainAssembly = "linked/Changed.dll";
			Assert.IsTrue (task.Execute ());
			CollectionAssert.AreEqual (ExpectedBytes ("Changed.dll"), File.ReadAllBytes (file));
			Assert.AreNotEqual (timestamp, File.GetLastWriteTimeUtc (file));

			File.Delete (file);
			Assert.IsTrue (task.Execute ());
			CollectionAssert.AreEqual (ExpectedBytes ("Changed.dll"), File.ReadAllBytes (file));
		} finally {
			Directory.Delete (directory, recursive: true);
		}
	}

	[Test]
	public void DirectInstantiationUsesFallbackEnvironment ()
	{
		string directory = CreateProjectDirectory ();
		try {
			var task = new GeneratePackageManagerJava {
				BuildEngine = new MockBuildEngine (TestContext.Out),
				MainAssembly = "linked/Fallback.dll",
				OutputDirectory = directory,
			};
			Assert.IsTrue (task.Execute ());
			CollectionAssert.AreEqual (ExpectedBytes ("Fallback.dll"), File.ReadAllBytes (Path.Combine (directory, OutputFileName)));
		} finally {
			Directory.Delete (directory, recursive: true);
		}
	}

	[Test]
	public void ConcurrentProjectsResolveRelativeOutputsIndependently ()
	{
		string root = CreateProjectDirectory ();
		try {
			Parallel.For (0, 8, index => {
				string directory = Path.Combine (root, $"project{index}");
				Directory.CreateDirectory (directory);
				string assembly = $"App{index}.dll";
				var task = CreateTask (directory, Path.Combine ("linked", assembly), Path.Combine ("android", "src", "mono"));
				Assert.IsTrue (task.Execute ());
				CollectionAssert.AreEqual (ExpectedBytes (assembly),
					File.ReadAllBytes (Path.Combine (directory, "android", "src", "mono", OutputFileName)));
			});
		} finally {
			Directory.Delete (root, recursive: true);
		}
	}

	[Test]
	public void OutputFailureIsReported ()
	{
		string directory = CreateProjectDirectory ();
		try {
			Directory.CreateDirectory (Path.Combine (directory, OutputFileName));
			var errors = new List<BuildErrorEventArgs> ();
			var task = CreateTask (directory, "App.dll", directory);
			task.BuildEngine = new MockBuildEngine (TestContext.Out, errors);
			Assert.IsFalse (task.Execute ());
			Assert.IsNotEmpty (errors);
		} finally {
			Directory.Delete (directory, recursive: true);
		}
	}

	[Test]
	public void ProductionRegistrationSelectsTheDotNetTaskHost ()
	{
		var common = LoadCommonTargets ();
		var registrations = common.Descendants ().Where (element =>
			element.Name.LocalName == "UsingTask" &&
			(string?)element.Attribute ("TaskName") == typeof (GeneratePackageManagerJava).FullName).ToArray ();
		Assert.AreEqual (2, registrations.Length);
		Assert.That (registrations.Select (element => (string?)element.Attribute ("AssemblyFile")), Is.All.EqualTo ("$(_MicrosoftAndroidBuildTasksAssembly)"));
		Assert.That (registrations.Select (element => (string?)element.Attribute ("Runtime")), Is.All.EqualTo ("NET"));
		Assert.AreEqual ("TaskHostFactory", (string?)registrations.Single (element =>
			(string?)element.Attribute ("Condition") == " '$(MSBuildRuntimeType)' == 'Full' ").Attribute ("TaskFactory"));
		Assert.IsNull (registrations.Single (element =>
			(string?)element.Attribute ("Condition") == " '$(MSBuildRuntimeType)' == 'Core' ").Attribute ("TaskFactory"));
		var target = FindTarget (common, "_GeneratePackageManagerJava");
		Assert.IsTrue (target.Elements ().Any (element => element.Name.LocalName == typeof (GeneratePackageManagerJava).FullName));
		StringAssert.Contains ("$(_MicrosoftAndroidBuildTasksAssembly)", common.Descendants ()
			.Single (element => element.Name.LocalName == "_AndroidPackageManagerJavaTaskAssembly").Value);
	}

	[TestCase (false)]
	[TestCase (true)]
	public void ProductionTargetIsIncrementalAndRecoversMissingOutput (bool multithreaded)
	{
		string directory = CreateProjectDirectory ();
		try {
			var common = LoadCommonTargets ();
			string project = Path.Combine (directory, "package-manager.proj");
			var inputs = new XElement (FindTarget (common, "_GetGeneratePackageManagerJavaInputs"));
			var generation = new XElement (FindTarget (common, "_GeneratePackageManagerJava"));
			var registration = new XElement (common.Descendants ().Single (element =>
				element.Name.LocalName == "UsingTask" &&
				(string?)element.Attribute ("TaskName") == typeof (GeneratePackageManagerJava).FullName &&
				(string?)element.Attribute ("Condition") == " '$(MSBuildRuntimeType)' == 'Core' "));
			var document = new XDocument (new XElement ("Project",
				new XElement ("PropertyGroup",
					new XElement ("_MicrosoftAndroidBuildTasksAssembly", typeof (GeneratePackageManagerJava).Assembly.Location),
					new XElement ("_AndroidRuntime", "CoreCLR"),
					new XElement ("_GeneratePackageManagerJavaDependsOn", "_GetGeneratePackageManagerJavaInputs"),
					new XElement ("IntermediateOutputPath", directory + Path.DirectorySeparatorChar),
					new XElement ("_AndroidStampDirectory", directory + Path.DirectorySeparatorChar),
					new XElement ("_AndroidIntermediateJavaSourceDirectory", Path.Combine (directory, "android", "src") + Path.DirectorySeparatorChar),
					new XElement ("TargetPath", "linked/App.dll")),
				registration, inputs, generation));
			foreach (var element in document.Descendants ()) {
				element.Name = element.Name.LocalName;
			}
			document.Save (project);

			void Build (bool skipped, string assembly = "App.dll", bool succeeds = true)
			{
				var arguments = new List<string> {
					"msbuild", project, "-t:_GeneratePackageManagerJava", "-nr:false", "-v:normal",
					$"-p:TargetPath=linked/{assembly}",
				};
				if (multithreaded) {
					arguments.AddRange (["-m", "-mt", "-warnaserror:MSB4286,MSB4287,MSB4288"]);
				}
				var result = NativeAotBootstrapTestTools.Run (
					Environment.GetEnvironmentVariable ("DOTNET_HOST_PATH") ?? "dotnet", arguments.ToArray ());
				Assert.AreEqual (succeeds, result.ExitCode == 0, result.Output + result.Error);
				Assert.AreEqual (skipped, result.Output.Contains ("Skipping target \"_GeneratePackageManagerJava\"", StringComparison.Ordinal),
					result.Output + result.Error);
			}

			string file = Path.Combine (directory, "android", "src", "mono", OutputFileName);
			Build (skipped: false);
			CollectionAssert.AreEqual (ExpectedBytes ("App.dll"), File.ReadAllBytes (file));
			DateTime timestamp = File.GetLastWriteTimeUtc (file);
			Build (skipped: true);
			Assert.AreEqual (timestamp, File.GetLastWriteTimeUtc (file));
			File.Delete (file);
			Build (skipped: false);
			CollectionAssert.AreEqual (ExpectedBytes ("App.dll"), File.ReadAllBytes (file));
			Build (skipped: false, "Renamed.dll");
			CollectionAssert.AreEqual (ExpectedBytes ("Renamed.dll"), File.ReadAllBytes (file));
			Build (skipped: true, "Renamed.dll");
			File.Delete (file);
			Directory.CreateDirectory (file);
			Build (skipped: false, "Renamed.dll", succeeds: false);
			FileAssert.DoesNotExist (Path.Combine (directory, "_GeneratePackageManagerJava.stamp"));
			Directory.Delete (file);
			Build (skipped: false, "Renamed.dll");
			CollectionAssert.AreEqual (ExpectedBytes ("Renamed.dll"), File.ReadAllBytes (file));
		} finally {
			Directory.Delete (directory, recursive: true);
		}
	}

	static GeneratePackageManagerJava CreateTask (string directory, string mainAssembly, string outputDirectory) => new () {
		BuildEngine = new MockBuildEngine (TextWriter.Null),
		TaskEnvironment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment (directory, new Dictionary<string, string> ()),
		MainAssembly = mainAssembly,
		OutputDirectory = outputDirectory,
	};

	static string CreateProjectDirectory ()
	{
		string directory = Path.Combine (TestContext.CurrentContext.WorkDirectory, "GeneratePackageManagerJava", Guid.NewGuid ().ToString ("N"));
		Directory.CreateDirectory (directory);
		return directory;
	}

	static XDocument LoadCommonTargets () => XDocument.Load (Path.Combine (
		NativeAotBootstrapTestTools.FindRepositoryRoot (), "src", "Xamarin.Android.Build.Tasks", "Xamarin.Android.Common.targets"));

	static XElement FindTarget (XDocument document, string name) => document.Descendants ()
		.Single (element => element.Name.LocalName == "Target" && (string?)element.Attribute ("Name") == name);

	static byte [] ExpectedBytes (string mainAssembly) => new UTF8Encoding (encoderShouldEmitUTF8Identifier: false).GetBytes (
		string.Join (Environment.NewLine, new [] {
			"package mono;",
			"public class MonoPackageManager_Resources {",
			"\tpublic static String[] Assemblies = new String[]{",
			$"\t\t/* \"{mainAssembly}\" should be the only entry in this list. */",
			$"\t\t\"{mainAssembly}\",",
			"\t};",
			"}",
			"",
		}));
}
