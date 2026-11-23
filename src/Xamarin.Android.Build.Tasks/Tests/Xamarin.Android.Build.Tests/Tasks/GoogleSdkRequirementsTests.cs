using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NUnit.Framework;
using Xamarin.Android.Tasks;
using Xamarin.ProjectTools;
using Resources = Xamarin.Android.Tasks.Properties.Resources;

namespace Xamarin.Android.Build.Tests;

[TestFixture]
public class GoogleSdkRequirementsTests
{
	string directory = "";

	[SetUp]
	public void Setup ()
	{
		directory = Path.Combine (Path.GetTempPath (), "GoogleSdkRequirements-" + Guid.NewGuid ());
		Directory.CreateDirectory (Path.Combine (directory, "android"));
	}

	[TearDown]
	public void TearDown ()
	{
		Directory.Delete (directory, recursive: true);
	}

	[TestCase ("28", "10", "28", "")]
	[TestCase ("28", "29", "27", "XA1006;XA1007")]
	[TestCase ("27", "29", "28", "XA1008;XA1007")]
	[TestCase ("36.1", "24", "36", "")]
	[TestCase ("28", "invalid", "invalid", "")]
	[TestCase ("Preview", "invalid", "23", "")]
	[TestCase ("Preview", null, null, "XA1007")]
	public void CheckMergedManifest (string compile, string minimum, string target, string expectedCodes)
	{
		WriteManifest (minimum, target);
		var (logger, instance) = Build (compile);
		CollectionAssert.AreEqual (expectedCodes.Split (';', StringSplitOptions.RemoveEmptyEntries), logger.Warnings.Select (warning => warning.Code),
			string.Join (Environment.NewLine, logger.Warnings.Select (warning => warning.Message)));
		Assert.AreEqual (1, logger.Tasks.Count (task => task == "ReadAndroidManifest"));
		foreach (var warning in logger.Warnings) {
			string first = instance.GetPropertyValue (warning.Code == "XA1007" ? "_AndroidManifestMinimumSdkVersion" : "_AndroidManifestCompileSdkVersion");
			string second = instance.GetPropertyValue ("_AndroidManifestTargetSdkVersion");
			Assert.AreEqual (string.Format (Resources.ResourceManager.GetString (warning.Code), first, second), warning.Message);
		}
	}

	[TestCase (false)]
	[TestCase (true)]
	public void SkippedChecksDoNotReadManifest (bool missingManifest)
	{
		if (!missingManifest)
			File.WriteAllText (ManifestFile, "not XML");
		var (logger, _) = Build ("28", enabled: missingManifest);
		Assert.IsEmpty (logger.Warnings);
		Assert.IsEmpty (logger.Tasks);
	}

	[Test]
	public void ReusesExistingManifestRead ()
	{
		string extension = OperatingSystem.IsWindows () ? ".exe" : "";
		string jvm = OperatingSystem.IsWindows () ? Path.Combine ("bin", "server", "jvm.dll")
			: Path.Combine ("lib", OperatingSystem.IsMacOS () ? "libjli.dylib" : "libjvm.so");
		foreach (string file in new [] {
			Path.Combine ("platform-tools", "adb" + extension),
			Path.Combine ("bin", "jar" + extension),
			Path.Combine ("bin", "java" + extension),
			Path.Combine ("bin", "javac" + extension),
			Path.Combine ("bin", "jarsigner" + extension),
			"ndk-stack" + extension,
			jvm,
		}) {
			string path = Path.Combine (directory, file);
			Directory.CreateDirectory (Path.GetDirectoryName (path));
			File.WriteAllText (path, "");
		}

		WriteManifest ("29", "27");
		string output = BuildReusingManifest ("28");
		var codes = Regex.Matches (output, @"\bXA\d{4}\b").Select (match => match.Value);
		CollectionAssert.AreEqual (new [] { "XA1006", "XA1007" }, codes, output);
	}

	[Test]
	public void WarningsAsErrorsStillLogsBothWarningsAndContinues ()
	{
		WriteManifest ("29", "27");
		var (logger, instance) = Build ("28", warningsAsErrors: true);
		CollectionAssert.AreEqual (new [] { "XA1006", "XA1007" }, logger.Errors.Select (error => error.Code));
		Assert.AreEqual ("true", instance.GetPropertyValue ("CheckCompleted"));
	}

	string ManifestFile => Path.Combine (directory, "android", "AndroidManifest.xml");

	void WriteManifest (string minimum, string target)
	{
		XNamespace android = "http://schemas.android.com/apk/res/android";
		new XDocument (new XElement ("manifest",
			new XAttribute (XNamespace.Xmlns + "android", android),
			new XElement ("uses-sdk",
				minimum == null ? null : new XAttribute (android + "minSdkVersion", minimum),
				target == null ? null : new XAttribute (android + "targetSdkVersion", target))))
			.Save (ManifestFile);
	}

	ProjectRootElement CreateProject (ProjectCollection collection, string compile, bool enabled = true, bool reuseRead = false, bool warningsAsErrors = false)
	{
		string tasksDirectory = Path.Combine (XABuildPaths.TopDirectory, "src", "Xamarin.Android.Build.Tasks");
		string taskAssembly = typeof (ReadAndroidManifest).Assembly.Location;
		XNamespace ns = "http://schemas.microsoft.com/developer/msbuild/2003";
		var project = new XElement (ns + "Project");
		if (reuseRead) {
			var common = XDocument.Load (Path.Combine (tasksDirectory, "Xamarin.Android.Common.targets"));
			var read = new XElement (common.Descendants (ns + "Target").Single (element => (string) element.Attribute ("Name") == "_ReadAndroidManifest"));
			read.Attribute ("DependsOnTargets")?.Remove ();
			project.Add (read);
		}
		using var reader = project.CreateReader ();
		string toolsPath = Directory.EnumerateDirectories (Path.Combine (TestEnvironment.DotNetPreviewDirectory, "sdk"))
			.First (path => File.Exists (Path.Combine (path, "Microsoft.Common.tasks")));
		collection.AddToolset (new Toolset ("Current", toolsPath, collection, ""));
		var root = ProjectRootElement.Create (reader, collection);
		root.FullPath = Path.Combine (directory, "Check.proj");
		root.AddProperty ("IntermediateOutputPath", directory + Path.DirectorySeparatorChar);
		root.AddProperty ("AndroidEnableGooglePlayStoreChecks", enabled.ToString ());
		root.AddProperty ("_AndroidApiLevel", compile);
		root.AddProperty ("_AndroidSdkDirectory", directory);
		root.AddProperty ("_AndroidSdkRequirementsManifest", Path.Combine (directory, "previous.xml"));
		root.AddProperty ("_AndroidSdkRequirementsApiLevel", compile);
		root.AddProperty ("_AndroidManifestCompileSdkVersion", "99");
		root.AddProperty ("_AndroidManifestMinimumSdkVersion", "99");
		root.AddProperty ("_AndroidManifestTargetSdkVersion", "1");
		root.AddProperty ("SupportedOSPlatformVersion", "1");
		root.AddProperty ("TargetPlatformVersion", "99");
		root.AddProperty ("MSBuildWarningsAsErrors", warningsAsErrors ? "XA1006;XA1007" : "");
		root.AddProperty ("_XamarinAndroidBuildTasksAssembly", taskAssembly);
		foreach (string name in new [] { "ReadAndroidManifest", "AndroidWarning" }) {
			root.AddUsingTask ("Xamarin.Android.Tasks." + name, taskAssembly, null);
		}
		if (reuseRead) {
			root.AddUsingTask ("Xamarin.Android.Tasks.ResolveSdks", taskAssembly, null);
			var resolve = root.AddTarget ("InitializeSdk").AddTask ("ResolveSdks");
			resolve.SetParameter ("AndroidSdkPath", directory);
			resolve.SetParameter ("AndroidNdkPath", directory);
			resolve.SetParameter ("JavaSdkPath", directory);
			resolve.SetParameter ("MinimumSupportedJavaVersion", "1.0");
			resolve.SetParameter ("LatestSupportedJavaVersion", "99.0");
			// A second manifest read must fail after the first read has completed.
			var invalidate = root.AddTarget ("InvalidateManifest").AddTask ("WriteLinesToFile");
			invalidate.SetParameter ("File", ManifestFile);
			invalidate.SetParameter ("Lines", "not XML");
			invalidate.SetParameter ("Overwrite", "true");
		}
		root.AddImport (Path.Combine (tasksDirectory, "Microsoft.Android.Sdk", "targets", "Microsoft.Android.Sdk.Tooling.targets"));
		var check = root.AddTarget ("Check");
		check.DependsOnTargets = reuseRead ? "InitializeSdk;_ReadAndroidManifest;InvalidateManifest;_CheckGoogleSdkRequirements" : "_CheckGoogleSdkRequirements";
		check.AddPropertyGroup ().AddProperty ("CheckCompleted", "true");
		return root;
	}

	(CheckLogger Logger, ProjectInstance Instance) Build (string compile, bool enabled = true, bool warningsAsErrors = false)
	{
		using var collection = new ProjectCollection ();
		var root = CreateProject (collection, compile, enabled, warningsAsErrors: warningsAsErrors);
		var instance = new ProjectInstance (root, globalProperties: null, toolsVersion: null, collection);
		var logger = new CheckLogger ();
		bool result = instance.Build ("Check", new [] { logger });
		Assert.AreEqual (!warningsAsErrors, result, string.Join (Environment.NewLine, logger.Errors.Select (error => error.Message)));
		return (logger, instance);
	}

	string BuildReusingManifest (string compile)
	{
		using var collection = new ProjectCollection ();
		var root = CreateProject (collection, compile, reuseRead: true);
		// ResolveSdks changes process-wide state, so isolate it from the test runner.
		root.Save ();
		var startInfo = new ProcessStartInfo (Path.Combine (TestEnvironment.DotNetPreviewDirectory, "dotnet")) {
			WorkingDirectory = directory,
			UseShellExecute = false,
			CreateNoWindow = true,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
		};
		startInfo.ArgumentList.Add ("msbuild");
		startInfo.ArgumentList.Add (root.FullPath);
		startInfo.ArgumentList.Add ("-target:Check");
		startInfo.ArgumentList.Add ("-nologo");
		startInfo.ArgumentList.Add ("-verbosity:minimal");
		startInfo.ArgumentList.Add ("-nodeReuse:false");
		using var process = Process.Start (startInfo) ?? throw new InvalidOperationException ("Could not start MSBuild.");
		var standardOutput = process.StandardOutput.ReadToEndAsync ();
		var standardError = process.StandardError.ReadToEndAsync ();
		process.WaitForExit ();
		string output = standardOutput.GetAwaiter ().GetResult () + standardError.GetAwaiter ().GetResult ();
		Assert.AreEqual (0, process.ExitCode, output);
		return output;
	}

	sealed class CheckLogger : Logger
	{
		public List<BuildWarningEventArgs> Warnings { get; } = [];
		public List<BuildErrorEventArgs> Errors { get; } = [];
		public List<string> Tasks { get; } = [];

		public override void Initialize (IEventSource eventSource)
		{
			eventSource.WarningRaised += (_, warning) => Warnings.Add (warning);
			eventSource.ErrorRaised += (_, error) => Errors.Add (error);
			eventSource.TaskStarted += (_, task) => Tasks.Add (task.TaskName);
		}
	}
}
