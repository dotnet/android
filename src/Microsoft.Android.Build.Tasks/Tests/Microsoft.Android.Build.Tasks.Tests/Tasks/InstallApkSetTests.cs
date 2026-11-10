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
	public class InstallApkSetTests : BaseTest
	{
		const string QualifiedTaskName = "Microsoft.Android.Tasks.InstallApkSet";

		[Test]
		public void InstallApkSetCommand ()
		{
			var task = new InstallApkSet {
				ApkSet = "foo.apks",
				AdbToolPath = Path.Combine ("path", "with spaces"),
				AdbToolExe = "adb",
				AdbTarget = "-s emulator-5554",
				JarPath = "bundletool.jar",
			};
			string adb = Path.Combine (task.AdbToolPath, task.AdbToolExe);
			string cmd = task.GetCommandLineBuilder ().ToString ();
			Assert.AreEqual ($"-jar bundletool.jar install-apks --apks foo.apks --adb \"{adb}\" --device-id emulator-5554 --allow-downgrade --modules _ALL_", cmd);
		}

		[TestCase (QualifiedTaskName)]
		[TestCase ("InstallApkSet")]
		public void RegisteredTaskNamesExecute (string taskName)
		{
			string directory = Path.Combine (Root, "temp", TestName);
			Directory.CreateDirectory (directory);
			bool windows = OperatingSystem.IsWindows ();
			string toolPath = windows ? Environment.SystemDirectory : File.Exists ("/usr/bin/true") ? "/usr/bin" : "/bin";
			string toolExe = windows ? "cmd.exe" : "true";
			string project = Path.Combine (directory, "install.proj");
			string marker = Path.Combine (directory, "executed.txt");
			var registrations = XDocument.Load (Path.Combine (NativeAotBootstrapTestTools.FindRepositoryRoot (), "src", "Xamarin.Android.Build.Tasks", "Xamarin.Android.Common.targets"))
				.Descendants ()
				.Where (e => e.Name.LocalName == "UsingTask" && (string?) e.Attribute ("TaskName") == QualifiedTaskName)
				.Select (e => new XElement ("UsingTask", e.Attributes ()))
				.ToArray ();
			Assert.AreEqual (2, registrations.Length);
			new XDocument (new XElement ("Project",
				new XElement ("PropertyGroup",
					new XElement ("_MicrosoftAndroidBuildTasksAssembly", typeof (InstallApkSet).Assembly.Location)),
				registrations,
				new XElement ("Target", new XAttribute ("Name", "Build"),
					new XElement (taskName,
						new XAttribute ("ToolPath", toolPath),
						new XAttribute ("ToolExe", toolExe),
						new XAttribute ("JavaOptions", windows ? "/c rem" : ""),
						new XAttribute ("JarPath", "bundletool.jar"),
						new XAttribute ("AdbToolPath", directory),
						new XAttribute ("ApkSet", "foo.apks")),
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
