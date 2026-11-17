using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Android.Tasks;
using TaskItem = Microsoft.Build.Utilities.TaskItem;

namespace Xamarin.Android.Build.Tests
{

	[TestClass]
	[DoNotParallelize]
	public class CheckForInvalidResourceFileNamesTests : BaseTest {

#pragma warning disable 414
		static object [] [] InvalidResourceFileNamesChecks () => new object [] [] {
			new object[] {
				"myresource.xml",
				true,
				new string [0],
			},
			new object[] {
				"_myresource.xml",
				true,
				new string [0],
			},
			new object[] {
				"Myresource.xml",
				true,
				new string [0],
			},
			new object[] {
				"@myresource.xml",
				false,
				new [] { "APT0004", "APT0003" },
			},
			new object[] {
				"5myresource.xml",
				false,
				new [] { "APT0004" },
			},
			new object[] {
				".myresource.xml",
				false,
				new [] { "APT0004" },
			},
			new object[] {
				"-myresource.xml",
				false,
				new [] { "APT0004", "APT0003" },
			},
			new object[] {
				"import.xml",
				false,
				new [] { "APT0005" },
			},
			new object[] {
				"class.png",
				false,
				new [] { "APT0005" },
			},
			new object[] {
				"class1.png",
				true,
				new string [0],
			},
			new object[] {
				"Resources/values/strings@v2.xml",
				false,
				new [] { "APT0002" },
			},
			new object[] {
				"Resources/drawable/icon-v2.png",
				false,
				new [] { "APT0003" },
			},
		};
#pragma warning restore 414
		[TestMethod]
		[DynamicData (nameof(InvalidResourceFileNamesChecks))]
		public void InvalidResourceFileNames (string file, bool shouldPass, string [] expectedErrorCodes)
		{
			var errors = new List<BuildErrorEventArgs> ();
			var task = new CheckForInvalidResourceFileNames {
				BuildEngine = new MockBuildEngine (Console.Out, errors),
				Resources = new TaskItem [] {
					new TaskItem (file),
				},
			};
			Assert.AreEqual (shouldPass, task.Execute (), $"task.Execute() should have {(shouldPass ? "succeeded" : "failed")}.");
			CollectionAssert.AreEqual (expectedErrorCodes, errors.Select (e => e.Code).ToArray (), "Unexpected error codes.");
		}

		[TestMethod]
		[DataRow ("Microsoft.Android.Tasks.CheckForInvalidResourceFileNames")]
		[DataRow ("CheckForInvalidResourceFileNames")]
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
			Assert.IsTrue (File.Exists (marker), marker);
		}
	}
}
