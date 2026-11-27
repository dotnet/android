using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NUnit.Framework;
using Xamarin.Android.Tasks;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	public class ReadImportedLibrariesCacheTests : BaseTest
	{
		string projectDirectory = "";

		[SetUp]
		public void SetUp ()
		{
			projectDirectory = Path.Combine (Root, "temp", TestName, Guid.NewGuid ().ToString ("N"));
			TestOutputDirectories [TestContext.CurrentContext.Test.ID] = projectDirectory;
			Directory.CreateDirectory (Path.Combine (projectDirectory, "obj"));
		}

		ReadImportedLibrariesCache CreateTask (List<BuildErrorEventArgs> errors, List<BuildMessageEventArgs> messages) =>
			new ReadImportedLibrariesCache {
				BuildEngine = new MockBuildEngine (TestContext.Out, errors: errors, messages: messages),
				TaskEnvironment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment (projectDirectory, new Dictionary<string, string> ()),
				CacheFile = Path.Combine ("obj", "imports.cache"),
			};

		[Test]
		public void MissingCachePreservesOutputs ()
		{
			var errors = new List<BuildErrorEventArgs> ();
			var messages = new List<BuildMessageEventArgs> ();
			var task = CreateTask (errors, messages);
			ITaskItem [] original = [new TaskItem ("existing")];
			task.Jars = task.NativeLibraries = task.ManifestDocuments = original;

			Assert.IsTrue (task.Execute ());
			Assert.AreSame (original, task.Jars);
			Assert.AreSame (original, task.NativeLibraries);
			Assert.AreSame (original, task.ManifestDocuments);
			Assert.IsEmpty (errors);
			Assert.AreEqual (1, messages.Count);
			Assert.AreEqual ($"{task.CacheFile} does not exist. No Imported Libraries found", messages [0].Message);
		}

		[Test]
		public void EmptyCache ()
		{
			new XDocument (new XElement ("Paths")).Save (Path.Combine (projectDirectory, "obj", "imports.cache"));
			var errors = new List<BuildErrorEventArgs> ();
			var messages = new List<BuildMessageEventArgs> ();
			var task = CreateTask (errors, messages);

			Assert.IsTrue (task.Execute ());
			Assert.IsNotNull (task.Jars);
			Assert.IsNotNull (task.NativeLibraries);
			Assert.IsNotNull (task.ManifestDocuments);
			Assert.IsEmpty (task.Jars);
			Assert.IsEmpty (task.NativeLibraries);
			Assert.IsEmpty (task.ManifestDocuments);
			Assert.IsEmpty (errors);
			Assert.IsEmpty (messages);
		}

		static XDocument Cache (string owner, string absoluteJar) =>
			new XDocument (
				new XElement ("Paths",
					new XElement ("Jars",
						new XElement ("Jar", new XAttribute ("Owner", owner), Path.Combine ("jars", "first.jar")),
						new XElement ("Jar", new XAttribute ("Owner", owner), absoluteJar),
						new XElement ("Jar", new XAttribute ("Owner", owner), Path.Combine ("jars", "first.jar"))),
					new XElement ("NativeLibraries",
						new XElement ("NativeLibrary", new XAttribute ("Abi", "arm64-v8a"), Path.Combine ("native", "libtest.so"))),
					new XElement ("ManifestDocuments",
						new XElement ("ManifestDocument", new XAttribute ("ImportedFrom", "library.aar"), Path.Combine ("manifests", "AndroidManifest.xml")))));

		[TestCase (false)]
		[TestCase (true)]
		public void ConcurrentProjectsPreserveRelativeFullPaths (bool multithreaded)
		{
			string assembly = Path.Combine (TestEnvironment.AndroidMSBuildDirectory, "Xamarin.Android.Build.Tasks.dll");
			var resources = new List<ProjectResource> ();
			foreach (string name in new [] { "first", "second" }) {
				string absoluteJar = Path.Combine (projectDirectory, name, "absolute.jar");
				resources.Add (new ProjectResource {
					Path = Path.Combine (name, "obj", "imports.cache"),
					Content = Cache (name, absoluteJar).ToString (),
				});
				var project = new XElement ("Project",
					new XElement ("UsingTask", new XAttribute ("TaskName", "Xamarin.Android.Tasks.ReadImportedLibrariesCache"), new XAttribute ("AssemblyFile", assembly)),
					new XElement ("Target", new XAttribute ("Name", "Build"),
						new XElement ("ReadImportedLibrariesCache", new XAttribute ("CacheFile", Path.Combine ("obj", "imports.cache")),
							new XElement ("Output", new XAttribute ("TaskParameter", "Jars"), new XAttribute ("ItemName", "_Jars")),
							new XElement ("Output", new XAttribute ("TaskParameter", "NativeLibraries"), new XAttribute ("ItemName", "_Native")),
							new XElement ("Output", new XAttribute ("TaskParameter", "ManifestDocuments"), new XAttribute ("ItemName", "_Manifests"))),
						new XElement ("WriteLinesToFile",
							new XAttribute ("File", "$(MSBuildProjectDirectory)/output.txt"),
							new XAttribute ("Overwrite", "true"),
							new XAttribute ("Lines", "@(_Jars->'Jar|%(Identity)|%(FullPath)|%(Owner)');@(_Native->'Native|%(Identity)|%(FullPath)|%(Abi)');@(_Manifests->'Manifest|%(Identity)|%(FullPath)|%(ImportedFrom)')"))));
				resources.Add (new ProjectResource { Path = Path.Combine (name, "reader.proj"), Content = project.ToString () });
			}
			var driver = new XElement ("Project",
				new XElement ("ItemGroup",
					new XElement ("Reader", new XAttribute ("Include", "first/reader.proj;second/reader.proj"))),
				new XElement ("Target", new XAttribute ("Name", "Build"),
					new XElement ("MSBuild", new XAttribute ("Projects", "@(Reader)"), new XAttribute ("BuildInParallel", "true"), new XAttribute ("Targets", "Build"))),
				new XElement ("Target", new XAttribute ("Name", "CheckMalformed"), new XAttribute ("DependsOnTargets", "Build")));
			resources.Add (new ProjectResource { Path = "driver.proj", Content = driver.ToString () });
			var projectFiles = new XamarinAndroidApplicationProject ();
			projectFiles.UpdateProjectFiles (projectDirectory, resources);
			var dotnet = new DotNetCLI (Path.Combine (projectDirectory, "driver.proj")) { Verbosity = "diagnostic" };
			dotnet.EnvironmentVariables ["DOTNET_CLI_USE_MSBUILD_SERVER"] = "0";
			dotnet.EnvironmentVariables ["MSBUILDDISABLENODEREUSE"] = "1";
			string [] arguments = multithreaded ?
				["--no-restore", "-m:2", "-mt", "-nr:false", "-warnaserror:MSB4286,MSB4287,MSB4288"] :
				["--no-restore", "-m:2", "-nr:false"];
			Assert.IsTrue (dotnet.Build (msbuildArguments: arguments),
				string.Join (Environment.NewLine, dotnet.LastBuildOutput) + File.ReadAllText (dotnet.ProcessLogFile));

			foreach (string name in new [] { "first", "second" }) {
				string root = Path.Combine (projectDirectory, name);
				string relativeJar = Path.Combine ("jars", "first.jar");
				string relativeNative = Path.Combine ("native", "libtest.so");
				string relativeManifest = Path.Combine ("manifests", "AndroidManifest.xml");
				string jar = $"Jar|{relativeJar}|{Path.Combine (root, relativeJar)}|{name}";
				CollectionAssert.AreEqual (new [] {
					jar,
					$"Jar|{Path.Combine (root, "absolute.jar")}|{Path.Combine (root, "absolute.jar")}|{name}",
					jar,
					$"Native|{relativeNative}|{Path.Combine (root, relativeNative)}|arm64-v8a",
					$"Manifest|{relativeManifest}|{Path.Combine (root, relativeManifest)}|library.aar",
				}, File.ReadAllLines (Path.Combine (root, "output.txt")));
			}

			projectFiles.UpdateProjectFiles (projectDirectory, new [] {
				new ProjectResource { Path = Path.Combine ("first", "obj", "imports.cache"), Content = "<Paths><Jars>" },
			}, doNotCleanup: true);
			dotnet.BuildLogFile = Path.Combine (projectDirectory, "malformed.log");
			dotnet.ProcessLogFile = Path.Combine (projectDirectory, "malformed-process.log");
			Assert.IsFalse (dotnet.Build (target: "CheckMalformed", msbuildArguments: arguments));
			string log = string.Join (Environment.NewLine, dotnet.LastBuildOutput);
			StringAssert.Contains ("XARIL7000", log);
			StringAssert.Contains (typeof (XmlException).FullName, log);
			StringAssert.DoesNotMatch (@"(?i)\b(?:warning|error) MSB428[678]\b", log);
		}
	}
}
