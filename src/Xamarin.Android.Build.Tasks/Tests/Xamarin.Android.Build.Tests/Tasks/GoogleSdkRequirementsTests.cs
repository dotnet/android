using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
		WriteManifest ("29", "27");
		var (logger, _) = Build ("28", reuseRead: true);
		CollectionAssert.AreEqual (new [] { "XA1006", "XA1007" }, logger.Warnings.Select (warning => warning.Code),
			string.Join (Environment.NewLine, logger.Warnings.Select (warning => warning.Message)));
		Assert.AreEqual (1, logger.Tasks.Count (task => task == "ReadAndroidManifest"), "The check must not parse the manifest again.");
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

	(CheckLogger Logger, ProjectInstance Instance) Build (string compile, bool enabled = true, bool reuseRead = false, bool warningsAsErrors = false)
	{
		string tasksDirectory = Path.Combine (XABuildPaths.TopDirectory, "src", "Xamarin.Android.Build.Tasks");
		XNamespace ns = "http://schemas.microsoft.com/developer/msbuild/2003";
		var project = new XElement (ns + "Project");
		if (reuseRead) {
			var common = XDocument.Load (Path.Combine (tasksDirectory, "Xamarin.Android.Common.targets"));
			var read = new XElement (common.Descendants (ns + "Target").Single (element => (string) element.Attribute ("Name") == "_ReadAndroidManifest"));
			read.Attribute ("DependsOnTargets")?.Remove ();
			project.Add (read);
		}
		using var reader = project.CreateReader ();
		using var collection = new ProjectCollection ();
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
		root.AddProperty ("_XamarinAndroidBuildTasksAssembly", typeof (ReadAndroidManifest).Assembly.Location);
		foreach (string name in new [] { "ReadAndroidManifest", "AndroidWarning" }) {
			root.AddUsingTask ("Xamarin.Android.Tasks." + name, typeof (ReadAndroidManifest).Assembly.Location, null);
		}
		root.AddImport (Path.Combine (tasksDirectory, "Microsoft.Android.Sdk", "targets", "Microsoft.Android.Sdk.Tooling.targets"));
		var check = root.AddTarget ("Check");
		check.DependsOnTargets = reuseRead ? "_ReadAndroidManifest;_CheckGoogleSdkRequirements" : "_CheckGoogleSdkRequirements";
		check.AddPropertyGroup ().AddProperty ("CheckCompleted", "true");
		var instance = new ProjectInstance (root, globalProperties: null, toolsVersion: null, collection);
		var logger = new CheckLogger ();
		bool result = instance.Build ("Check", new [] { logger });
		Assert.AreEqual (!warningsAsErrors, result, string.Join (Environment.NewLine, logger.Errors.Select (error => error.Message)));
		return (logger, instance);
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
