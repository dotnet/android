using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NUnit.Framework;
using Xamarin.Android.Tasks;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests {

	[TestFixture]
	[Parallelizable (ParallelScope.Self)]
	public class CheckDuplicateJavaLibrariesTests : BaseTest {
		List<BuildErrorEventArgs> errors;
		List<BuildWarningEventArgs> warnings;
		List<BuildMessageEventArgs> messages;
		MockBuildEngine engine;
		string path;

		[SetUp]
		public void Setup ()
		{
			engine = new MockBuildEngine (TestContext.Out,
				errors: errors = new List<BuildErrorEventArgs> (),
				warnings: warnings = new List<BuildWarningEventArgs> (),
				messages: messages = new List<BuildMessageEventArgs> ());

			path = Path.Combine (Root, "temp", TestName);
			TestOutputDirectories [TestContext.CurrentContext.Test.ID] = path;
			Directory.CreateDirectory (path);
		}

		[Test]
		public void XA1014ConflictingJarNames ()
		{
			string jar1 = Path.Combine (path, "library.jar");
			File.WriteAllText (jar1, "jar1");

			Directory.CreateDirectory (Path.Combine (path, "jar2"));
			string jar2 = Path.Combine (path, "jar2", "library.jar");
			File.WriteAllText (jar2, "jar2");

			var task = new CheckDuplicateJavaLibraries {
				BuildEngine = engine,
				JavaSourceFiles = new ITaskItem [] { new TaskItem (jar1), new TaskItem (jar2) }
			};

			Assert.IsFalse (task.Execute (), "Task should fail!");
			BuildErrorEventArgs error = errors [0];
			Assert.AreEqual ("XA1014", error.Code);
			StringAssert.Contains ("library.jar", error.Message);
		}

		[TestCase ("same", true, null)]
		[TestCase ("diff", false, null)]
		[TestCase ("different size", false, null)]
		[TestCase ("diff", true, "LIBRARY.JAR")]
		public void RelativeJarContents (string content, bool expected, string? excludedFile)
		{
			Directory.CreateDirectory (Path.Combine (path, "imports"));
			File.WriteAllText (Path.Combine (path, "library.jar"), "same");
			File.WriteAllText (Path.Combine (path, "imports", "library.jar"), content);
			var task = new CheckDuplicateJavaLibraries {
				BuildEngine = engine,
				TaskEnvironment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment (path, new Dictionary<string, string> ()),
				JavaSourceFiles = [new TaskItem ("missing.java"), new TaskItem ("library.jar")],
				JavaLibraries = [new TaskItem (Path.Combine ("imports", "library.jar"))],
				LibraryProjectJars = [new TaskItem ("library.jar")],
				ExcludedFiles = excludedFile == null ? null : [excludedFile],
			};

			Assert.AreEqual (expected, task.Execute ());
			if (expected) {
				Assert.IsEmpty (errors);
			} else {
				Assert.AreEqual (1, errors.Count);
				Assert.AreEqual ("XA1014", errors [0].Code);
				StringAssert.Contains ("library.jar", errors [0].Message);
			}
		}

		[Test]
		[Platform ("Win")]
		public void LockedRelativeJarKeepsFileLockDiagnostic ()
		{
			Directory.CreateDirectory (Path.Combine (path, "imports"));
			File.WriteAllText (Path.Combine (path, "library.jar"), "same");
			string lockedJar = Path.Combine (path, "imports", "library.jar");
			File.WriteAllText (lockedJar, "same");
			using var stream = File.Open (lockedJar, FileMode.Open, FileAccess.Read, FileShare.None);
			var task = new CheckDuplicateJavaLibraries {
				BuildEngine = engine,
				TaskEnvironment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment (path, new Dictionary<string, string> ()),
				JavaLibraries = [new TaskItem ("library.jar"), new TaskItem (Path.Combine ("imports", "library.jar"))],
			};

			Assert.IsFalse (task.Execute ());
			Assert.AreEqual (1, errors.Count);
			Assert.AreEqual ("XACDJ7024", errors [0].Code);
			StringAssert.Contains (lockedJar, errors [0].Message);
		}

		[Test]
		public void ContentComparisonPreservesItemsAndOrder ()
		{
			Directory.CreateDirectory (Path.Combine (path, "imports"));
			File.WriteAllText (Path.Combine (path, "library.jar"), "same");
			File.WriteAllText (Path.Combine (path, "imports", "renamed.jar"), "same");
			File.WriteAllText (Path.Combine (path, "different.jar"), "diff");
			var first = new TaskItem ("library.jar");
			first.SetMetadata ("Owner", "first");
			var last = new TaskItem ("different.jar");
			ITaskItem [] items = [first, new TaskItem (Path.Combine ("imports", "renamed.jar")), last, first];
			var environment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment (path, new Dictionary<string, string> ());

			var distinct = MonoAndroidHelper.DistinctFilesByContent (items, environment).ToArray ();
			Assert.AreEqual (2, distinct.Length);
			Assert.AreSame (first, distinct [0]);
			Assert.AreSame (last, distinct [1]);
			Assert.AreEqual ("library.jar", first.ItemSpec);
			Assert.AreEqual ("first", first.GetMetadata ("Owner"));
			CollectionAssert.AreEqual (new [] { Path.Combine (path, "library.jar"), Path.Combine (path, "different.jar") },
				MonoAndroidHelper.DistinctFilesByContent (items.Select (i => i.ItemSpec), environment));
		}

		[Test]
		public void ConcurrentProjectsUseTheirOwnJarContents ()
		{
			string assembly = Path.Combine (TestEnvironment.AndroidMSBuildDirectory, "Xamarin.Android.Build.Tasks.dll");
			var resources = new List<ProjectResource> ();
			foreach (string name in new [] { "first", "second" }) {
				resources.Add (new ProjectResource { Path = Path.Combine (name, "library.jar"), Content = name });
				resources.Add (new ProjectResource {
					Path = Path.Combine (name, "imports", "library.jar"),
					Content = name == "first" ? name : "different",
				});
				var project = new XElement ("Project",
					new XElement ("UsingTask", new XAttribute ("TaskName", "Xamarin.Android.Tasks.CheckDuplicateJavaLibraries"), new XAttribute ("AssemblyFile", assembly)),
					new XElement ("Target", new XAttribute ("Name", "Build"),
						new XElement ("CheckDuplicateJavaLibraries",
							new XAttribute ("JavaLibraries", $"library.jar;{Path.Combine ("imports", "library.jar")}")),
						new XElement ("WriteLinesToFile",
							new XAttribute ("File", "$(MSBuildProjectDirectory)/checked.txt"),
							new XAttribute ("Lines", name), new XAttribute ("Overwrite", "true"))));
				resources.Add (new ProjectResource { Path = Path.Combine (name, "check.proj"), Content = project.ToString () });
			}
			var driver = new XElement ("Project",
				new XElement ("ItemGroup", new XElement ("Checks", new XAttribute ("Include", "first/check.proj;second/check.proj"))),
				new XElement ("Target", new XAttribute ("Name", "Build"),
					new XElement ("MSBuild", new XAttribute ("Projects", "@(Checks)"), new XAttribute ("BuildInParallel", "true"))));
			resources.Add (new ProjectResource { Path = "driver.proj", Content = driver.ToString () });
			var projectFiles = new XamarinAndroidApplicationProject ();
			projectFiles.UpdateProjectFiles (path, resources);
			var dotnet = new DotNetCLI (Path.Combine (path, "driver.proj")) { Verbosity = "diagnostic" };
			dotnet.EnvironmentVariables ["DOTNET_CLI_USE_MSBUILD_SERVER"] = "0";
			dotnet.EnvironmentVariables ["MSBUILDDISABLENODEREUSE"] = "1";
			Assert.IsFalse (dotnet.Build (msbuildArguments:
				["--no-restore", "-m:2", "-mt", "-nr:false", "-warnaserror:MSB4286,MSB4287,MSB4288"]),
				string.Join (Environment.NewLine, dotnet.LastBuildOutput) + File.ReadAllText (dotnet.ProcessLogFile));
			var errorLines = dotnet.LastBuildOutput.Where (line => line.Contains (": error ")).ToArray ();
			Assert.IsNotEmpty (errorLines);
			foreach (string line in errorLines) {
				StringAssert.Contains (": error XA1014:", line);
				StringAssert.Contains (Path.Combine (path, "second", "check.proj"), line);
			}
			Assert.AreEqual ("first", File.ReadAllText (Path.Combine (path, "first", "checked.txt")).Trim ());
			Assert.IsFalse (File.Exists (Path.Combine (path, "second", "checked.txt")));
		}
	}
}
