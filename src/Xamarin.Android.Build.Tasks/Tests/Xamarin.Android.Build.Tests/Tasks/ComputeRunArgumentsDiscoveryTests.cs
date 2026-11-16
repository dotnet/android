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

namespace Xamarin.Android.Build.Tests;

[TestFixture]
public class ComputeRunArgumentsDiscoveryTests
{
	const string AndroidNamespace = "http://schemas.android.com/apk/res/android";
	const string Activity = "<activity android:name='.Activity'><intent-filter><category android:name='android.intent.category.LAUNCHER' /></intent-filter></activity>";
	static readonly XNamespace MSBuildNamespace = "http://schemas.microsoft.com/developer/msbuild/2003";
	static readonly string TasksDirectory = Path.Combine (XABuildPaths.TopDirectory, "src", "Xamarin.Android.Build.Tasks");
	static readonly string ApplicationTargets = Path.Combine (TasksDirectory, "Microsoft.Android.Sdk", "targets", "Microsoft.Android.Sdk.Application.targets");
	string directory = "";

	[SetUp]
	public void Setup ()
	{
		directory = Path.Combine (Path.GetTempPath (), "AndroidRunArguments-" + Guid.NewGuid ());
		Directory.CreateDirectory (Path.Combine (directory, "android"));
	}

	[TearDown]
	public void TearDown ()
	{
		Directory.Delete (directory, recursive: true);
	}

	[TestCase (false, "XA1043", 3)]
	[TestCase (true, "XA1048", 2)]
	public void MissingInstrumentationReportsLaunchModeError (bool useInstrumentation, string expectedCode, int expectedReads)
	{
		WriteManifest ("", "");
		var (logger, _) = Build (useInstrumentation, expectedSuccess: false);
		CollectionAssert.AreEqual (new [] { expectedCode }, logger.Errors.Select (error => error.Code));
		Assert.AreEqual (expectedReads, ManifestReadCount (logger));
	}

	[TestCase (false, "", 3)]
	[TestCase (false, "android:name=''", 3)]
	[TestCase (true, "", 2)]
	[TestCase (true, "android:name=''", 2)]
	public void MissingOrEmptyFirstInstrumentationNameIsXA1042 (bool useInstrumentation, string attributes, int expectedReads)
	{
		WriteManifest ("", $"<instrumentation {attributes} /><instrumentation android:name='.Second' />");
		var (logger, _) = Build (useInstrumentation, expectedSuccess: false);
		CollectionAssert.AreEqual (new [] { "XA1042" }, logger.Errors.Select (error => error.Code));
		Assert.AreEqual (expectedReads, ManifestReadCount (logger));
	}

	[TestCase ("")]
	[TestCase ("android:name=''")]
	public void ActivityWinsWithoutReadingMalformedInstrumentation (string attributes)
	{
		WriteManifest (Activity, $"<instrumentation {attributes} />");
		var (logger, instance) = Build ();
		Assert.IsEmpty (logger.Errors);
		Assert.AreEqual (".Activity", instance.GetPropertyValue ("AndroidLaunchActivity"));
		StringAssert.Contains ("--activity \".Activity\"", instance.GetPropertyValue ("RunArguments"));
		Assert.AreEqual (1, ManifestReadCount (logger));
	}

	[TestCase (false, 2)]
	[TestCase (true, 1)]
	public void NamedInstrumentationIsSelectedOnlyWhenNeeded (bool useInstrumentation, int expectedReads)
	{
		WriteManifest (useInstrumentation ? Activity : "", "<instrumentation android:name='.Instrumentation' />");
		var (logger, instance) = Build (useInstrumentation);
		Assert.AreEqual (".Instrumentation", instance.GetPropertyValue ("AndroidInstrumentation"));
		StringAssert.Contains ("--instrument \".Instrumentation\"", instance.GetPropertyValue ("RunArguments"));
		Assert.AreEqual (expectedReads, ManifestReadCount (logger));
		if (useInstrumentation)
			Assert.AreEqual (0, logger.Tasks.Count (task => task == "GetAndroidActivityName"));
	}

	[TestCase ("", "XA1048", 2)]
	[TestCase ("<instrumentation android:name='.Instrumentation' />", "", 1)]
	public void InstrumentationOptInIgnoresPreviouslyDiscoveredActivity (string instrumentation, string errorCode, int expectedReads)
	{
		WriteManifest (Activity, instrumentation);
		var (logger, instance) = Build (useInstrumentation: true, expectedSuccess: errorCode == "", properties: new Dictionary<string, string> {
			["_AndroidLaunchableActivity"] = ".PreviousActivity",
		});
		Assert.AreEqual (expectedReads, ManifestReadCount (logger));
		if (errorCode == "") {
			Assert.AreEqual (".Instrumentation", instance.GetPropertyValue ("AndroidInstrumentation"));
			StringAssert.Contains ("--instrument \".Instrumentation\"", instance.GetPropertyValue ("RunArguments"));
		} else {
			CollectionAssert.AreEqual (new [] { errorCode }, logger.Errors.Select (error => error.Code));
		}
	}

	[TestCase (".ExplicitActivity", "", "--activity \".ExplicitActivity\"")]
	[TestCase ("", ".ExplicitInstrumentation", "--instrument \".ExplicitInstrumentation\"")]
	[TestCase (".ExplicitActivity", ".ExplicitInstrumentation", "--instrument \".ExplicitInstrumentation\"")]
	public void ExplicitOverridesSkipDiscovery (string activity, string instrumentation, string expectedArgument)
	{
		File.WriteAllText (ManifestFile, "not XML");
		var (logger, instance) = Build (useInstrumentation: true, properties: new Dictionary<string, string> {
			["AndroidLaunchActivity"] = activity,
			["AndroidInstrumentation"] = instrumentation,
		});
		Assert.AreEqual (0, ManifestReadCount (logger));
		StringAssert.Contains (expectedArgument, instance.GetPropertyValue ("RunArguments"));
	}

	[TestCase (false)]
	[TestCase (true)]
	public void ParallelProjectsUseTheirOwnRelativeManifests (bool multithreaded)
	{
		var resources = new List<ProjectResource> ();
		foreach (string name in new [] { "first", "second" }) {
			string instrumentation = "." + name;
			resources.Add (new ProjectResource {
				Path = Path.Combine (name, "obj", "android", "AndroidManifest.xml"),
				Content = Manifest ("", $"<instrumentation android:name='{instrumentation}' />"),
			});
			var project = new XElement (MSBuildNamespace + "Project",
				new XElement (MSBuildNamespace + "PropertyGroup",
					new XElement (MSBuildNamespace + "IntermediateOutputPath", "obj/"),
					new XElement (MSBuildNamespace + "_AndroidComputeRunArgumentsDependsOn", "NoOp"),
					new XElement (MSBuildNamespace + "_XamarinAndroidBuildTasksAssembly", typeof (AndroidError).Assembly.Location),
					new XElement (MSBuildNamespace + "AndroidUseInstrumentation", name == "first" ? "true" : "false")));
			AddTargets (project);
			project.Add (new XElement (MSBuildNamespace + "Target",
				new XAttribute ("Name", "Build"),
				new XAttribute ("DependsOnTargets", "_AndroidComputeRunArguments"),
				new XElement (MSBuildNamespace + "Error",
					new XAttribute ("Condition", $"'$(AndroidInstrumentation)' != '{instrumentation}'"),
					new XAttribute ("Text", $"The {name} project selected the wrong instrumentation."))));
			resources.Add (new ProjectResource { Path = Path.Combine (name, "run.proj"), Content = project.ToString () });
		}
		resources.Add (new ProjectResource {
			Path = "driver.proj",
			Content = """
				<Project>
				  <Target Name="Build">
				    <MSBuild Projects="first\run.proj;second\run.proj" Targets="Build" BuildInParallel="true" />
				  </Target>
				</Project>
				""",
		});
		new XamarinAndroidApplicationProject ().UpdateProjectFiles (directory, resources, doNotCleanup: true);
		var dotnet = new DotNetCLI (Path.Combine (directory, "driver.proj")) { Verbosity = "diagnostic" };
		dotnet.EnvironmentVariables ["DOTNET_CLI_USE_MSBUILD_SERVER"] = "0";
		dotnet.EnvironmentVariables ["MSBUILDDISABLENODEREUSE"] = "1";
		Assert.IsTrue (dotnet.Build (msbuildArguments: multithreaded ?
			["--no-restore", "-m:2", "-mt", "-nr:false", "-warnaserror:MSB4286,MSB4287,MSB4288"] :
			["--no-restore", "-m:2", "-nr:false"]), File.ReadAllText (dotnet.ProcessLogFile));
	}

	string ManifestFile => Path.Combine (directory, "android", "AndroidManifest.xml");

	static string Manifest (string application, string instrumentation) =>
		$"<manifest xmlns:android='{AndroidNamespace}'><application>{application}</application>{instrumentation}</manifest>";

	void WriteManifest (string application, string instrumentation) =>
		File.WriteAllText (ManifestFile, Manifest (application, instrumentation));

	static int ManifestReadCount (RunLogger logger) =>
		logger.Tasks.Count (task => task == "XmlPeek" || task == "GetAndroidActivityName");

	static void AddTargets (XElement project)
	{
		var common = XDocument.Load (Path.Combine (TasksDirectory, "Xamarin.Android.Common.targets")).Root
			?? throw new InvalidOperationException ("The common targets have no root element.");
		var launcher = common.Elements (MSBuildNamespace + "Target")
			.SingleOrDefault (target => (string) target.Attribute ("Name") == "_GetAndroidLaunchableActivity");
		if (launcher != null)
			project.Add (new XElement (launcher));
		project.Add (
			new XElement (MSBuildNamespace + "UsingTask",
				new XAttribute ("TaskName", "Xamarin.Android.Tasks.AndroidError"),
				new XAttribute ("AssemblyFile", typeof (AndroidError).Assembly.Location)),
			new XElement (MSBuildNamespace + "Import", new XAttribute ("Project", ApplicationTargets)),
			new XElement (MSBuildNamespace + "Target", new XAttribute ("Name", "NoOp")));
	}

	(RunLogger Logger, ProjectInstance Instance) Build (bool useInstrumentation = false, bool expectedSuccess = true, Dictionary<string, string> properties = null)
	{
		var propertyGroup = new XElement (MSBuildNamespace + "PropertyGroup",
			new XElement (MSBuildNamespace + "IntermediateOutputPath", directory + Path.DirectorySeparatorChar),
			new XElement (MSBuildNamespace + "_AndroidComputeRunArgumentsDependsOn", "NoOp"),
			new XElement (MSBuildNamespace + "_XamarinAndroidBuildTasksAssembly", typeof (AndroidError).Assembly.Location),
			new XElement (MSBuildNamespace + "AndroidUseInstrumentation", useInstrumentation.ToString ()));
		foreach (var property in properties ?? [])
			propertyGroup.Add (new XElement (MSBuildNamespace + property.Key, property.Value));
		var project = new XElement (MSBuildNamespace + "Project", propertyGroup);
		AddTargets (project);
		using var reader = project.CreateReader ();
		using var collection = new ProjectCollection ();
		string toolsPath = Directory.EnumerateDirectories (Path.Combine (TestEnvironment.DotNetPreviewDirectory, "sdk"))
			.First (path => File.Exists (Path.Combine (path, "Microsoft.Common.tasks")));
		collection.AddToolset (new Toolset ("Current", toolsPath, collection, ""));
		var root = ProjectRootElement.Create (reader, collection);
		root.FullPath = Path.Combine (directory, "Run.proj");
		var instance = new ProjectInstance (root, globalProperties: null, toolsVersion: null, collection);
		var logger = new RunLogger ();
		bool success = instance.Build ("_AndroidComputeRunArguments", new [] { logger });
		Assert.AreEqual (expectedSuccess, success,
			string.Join (Environment.NewLine, logger.Errors.Select (error => $"{error.Code}: {error.Message}")));
		return (logger, instance);
	}

	sealed class RunLogger : Logger
	{
		public List<BuildErrorEventArgs> Errors { get; } = [];
		public List<string> Tasks { get; } = [];

		public override void Initialize (IEventSource eventSource)
		{
			eventSource.ErrorRaised += (_, error) => Errors.Add (error);
			eventSource.TaskStarted += (_, task) => Tasks.Add (task.TaskName);
		}
	}
}
