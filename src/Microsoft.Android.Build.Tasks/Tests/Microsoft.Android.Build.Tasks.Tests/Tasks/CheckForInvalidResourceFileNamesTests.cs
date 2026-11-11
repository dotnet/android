using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using NUnit.Framework;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Android.Tasks;
using TaskItem = Microsoft.Build.Utilities.TaskItem;

namespace Xamarin.Android.Build.Tests
{

	[TestFixture]
	public class CheckForInvalidResourceFileNamesTests : BaseTest {

#pragma warning disable 414
		static object [] InvalidResourceFileNamesChecks () => new object [] {
			new object[] {
				"myresource.xml",
				true,
				0,
			},
			new object[] {
				"_myresource.xml",
				true,
				0,
			},
			new object[] {
				"Myresource.xml",
				true,
				0,
			},
			new object[] {
				"@myresource.xml",
				false,
				2,
			},
			new object[] {
				"5myresource.xml",
				false,
				1,
			},
			new object[] {
				".myresource.xml",
				false,
				1,
			},
			new object[] {
				"-myresource.xml",
				false,
				2,
			},
			new object[] {
				"import.xml",
				false,
				1,
			},
			new object[] {
				"class.png",
				false,
				1,
			},
			new object[] {
				"class1.png",
				true,
				0,
			},
		};
#pragma warning restore 414
		[Test]
		[TestCaseSource(nameof(InvalidResourceFileNamesChecks))]
		public void InvalidResourceFileNames (string file, bool shouldPass, int expectedErrorCount)
		{
			var errors = new List<BuildErrorEventArgs> ();
			var task = new CheckForInvalidResourceFileNames {
				BuildEngine = new MockBuildEngine (TestContext.Out, errors),
				Resources = new TaskItem [] {
					new TaskItem (file),
				},
			};
			Assert.AreEqual (shouldPass, task.Execute (), $"task.Execute() should have {(shouldPass ? "succeeded" : "failed")}.");
			Assert.AreEqual (expectedErrorCount, errors.Count, "There should be one error.");
		}

		[TestCase ("Microsoft.Android.Tasks.CheckForInvalidResourceFileNames")]
		[TestCase ("CheckForInvalidResourceFileNames")]
		public void RegisteredTaskNamesExecute (string taskName)
		{
			string directory = Path.Combine (Root, "temp", TestName);
			Directory.CreateDirectory (directory);
			string project = Path.Combine (directory, "resources.proj");
			string marker = Path.Combine (directory, "executed.txt");
			string commonTargets = Path.Combine (NativeAotBootstrapTestTools.FindRepositoryRoot (),
				"src", "Xamarin.Android.Build.Tasks", "Xamarin.Android.Common.targets");
			var registrations = XDocument.Load (commonTargets).Descendants ()
				.Where (e => e.Name.LocalName == "UsingTask" && (string) e.Attribute ("TaskName") == "Microsoft.Android.Tasks.CheckForInvalidResourceFileNames")
				.Select (e => {
					var registration = new XElement (e);
					registration.Name = "UsingTask";
					return registration;
				})
				.ToArray ();
			Assert.AreEqual (2, registrations.Length);
			new XDocument (new XElement ("Project",
				new XElement ("PropertyGroup",
					new XElement ("_MicrosoftAndroidBuildTasksAssembly", typeof (CheckForInvalidResourceFileNames).Assembly.Location)),
				registrations,
				new XElement ("Target", new XAttribute ("Name", "Build"),
					new XElement (taskName,
						new XAttribute ("Resources", "Resources/layout/main.xml;Resources/values/strings-v2.xml")),
					new XElement ("WriteLinesToFile",
						new XAttribute ("File", marker),
						new XAttribute ("Lines", "executed"),
						new XAttribute ("Overwrite", "true")))))
				.Save (project);

			string dotnet = Environment.GetEnvironmentVariable ("DOTNET_HOST_PATH") ?? "dotnet";
			var result = NativeAotBootstrapTestTools.Run (dotnet, "msbuild", project, "-t:Build", "-v:minimal", "-nr:false");
			Assert.AreEqual (0, result.ExitCode, result.Output + result.Error);
			FileAssert.Exists (marker);
		}
	}
}
