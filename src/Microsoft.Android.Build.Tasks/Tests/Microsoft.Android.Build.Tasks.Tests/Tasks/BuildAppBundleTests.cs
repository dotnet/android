#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Android.Tasks;
using NUnit.Framework;

namespace Xamarin.Android.Build.Tests.Tasks
{
	[TestFixture]
	public class BuildAppBundleTests : BaseTest
	{
		const string LegacyTaskName = "Xamarin.Android.Tasks.BuildAppBundle";
		const string ModernTaskName = "Microsoft.Android.Tasks.BuildAppBundle";

		[TestCase (false)]
		[TestCase (true)]
		public void BuildAppBundleCommand (bool legacyName)
		{
			BuildAppBundle task = legacyName ? new Xamarin.Android.Tasks.BuildAppBundle () : new BuildAppBundle ();
			task.BaseZip = "base.zip";
			task.Output = "foo.aab";
			task.JarPath = "bundletool.jar";
			Assert.AreEqual (legacyName ? LegacyTaskName : ModernTaskName, task.GetType ().FullName);
			Assert.AreSame (typeof (BuildAppBundle).Assembly, task.GetType ().Assembly);
			Assert.IsInstanceOf<Xamarin.Android.Tasks.BundleTool> (task);
			string cmd = task.GetCommandLineBuilder ().ToString ();
			Assert.AreEqual ("-jar bundletool.jar build-bundle --modules base.zip --output foo.aab", cmd);
		}

		[Test]
		public void TaskRegistrationsMatchForBothQualifiedNames ()
		{
			var registrations = LoadRegistrations ();
			foreach (var name in new [] { ModernTaskName, LegacyTaskName }) {
				var entries = registrations.Where (element => (string?) element.Attribute ("TaskName") == name).ToArray ();
				Assert.AreEqual (2, entries.Length, name);
				Assert.That (entries.Select (element => (string?) element.Attribute ("AssemblyFile")), Is.All.EqualTo ("$(_MicrosoftAndroidBuildTasksAssembly)"), name);
				Assert.That (entries.Select (element => (string?) element.Attribute ("Runtime")), Is.All.EqualTo ("NET"), name);
				var full = entries.Single (element => (string?) element.Attribute ("Condition") == " '$(MSBuildRuntimeType)' == 'Full' ");
				var core = entries.Single (element => (string?) element.Attribute ("Condition") == " '$(MSBuildRuntimeType)' == 'Core' ");
				Assert.AreEqual ("TaskHostFactory", (string?) full.Attribute ("TaskFactory"), name);
				Assert.IsNull (core.Attribute ("TaskFactory"), name);
			}
		}

		[TestCase (LegacyTaskName)]
		[TestCase (ModernTaskName)]
		[TestCase ("BuildAppBundle")]
		public void RegisteredTaskNamesExecute (string taskName)
		{
			string directory = Path.Combine (Root, "temp", TestName);
			Directory.CreateDirectory (directory);
			bool windows = OperatingSystem.IsWindows ();
			string toolPath = windows ? Environment.SystemDirectory : File.Exists ("/usr/bin/true") ? "/usr/bin" : "/bin";
			string toolExe = windows ? "cmd.exe" : "true";
			FileAssert.Exists (Path.Combine (toolPath, toolExe));
			string project = Path.Combine (directory, "bundle.proj");
			string marker = Path.Combine (directory, "executed.txt");
			var registrations = LoadRegistrations ();
			Assert.AreEqual (4, registrations.Length);
			new XDocument (new XElement ("Project",
				new XElement ("PropertyGroup",
					new XElement ("_MicrosoftAndroidBuildTasksAssembly", typeof (BuildAppBundle).Assembly.Location)),
				registrations,
				new XElement ("Target", new XAttribute ("Name", "Build"),
					new XElement (taskName,
						new XAttribute ("ToolPath", toolPath),
						new XAttribute ("ToolExe", toolExe),
						new XAttribute ("JavaOptions", windows ? "/c rem" : ""),
						new XAttribute ("BaseZip", "base.zip"),
						new XAttribute ("Output", Path.Combine (directory, "output.aab")),
						new XAttribute ("JarPath", "bundletool.jar")),
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

		static XElement [] LoadRegistrations ()
		{
			string commonTargets = Path.Combine (NativeAotBootstrapTestTools.FindRepositoryRoot (),
				"src", "Xamarin.Android.Build.Tasks", "Xamarin.Android.Common.targets");
			return XDocument.Load (commonTargets).Descendants ()
				.Where (element => element.Name.LocalName == "UsingTask" &&
					((string?) element.Attribute ("TaskName") == LegacyTaskName || (string?) element.Attribute ("TaskName") == ModernTaskName))
				.Select (element => {
					var registration = new XElement (element);
					registration.Name = "UsingTask";
					return registration;
				})
				.ToArray ();
		}
	}
}
