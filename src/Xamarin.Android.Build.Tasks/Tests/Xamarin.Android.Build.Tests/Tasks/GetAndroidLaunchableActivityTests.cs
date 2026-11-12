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
public class GetAndroidLaunchableActivityTests
{
	const string AndroidNamespace = "http://schemas.android.com/apk/res/android";
	static readonly XNamespace MSBuildNamespace = "http://schemas.microsoft.com/developer/msbuild/2003";
	static readonly string TasksDirectory = Path.Combine (XABuildPaths.TopDirectory, "src", "Xamarin.Android.Build.Tasks");
	static readonly string ApplicationTargets = Path.Combine (TasksDirectory, "Microsoft.Android.Sdk", "targets", "Microsoft.Android.Sdk.Application.targets");
	static readonly string DebuggingTargets = Path.Combine (XABuildPaths.TopDirectory, "src", "Xamarin.Android.Build.Debugging.Tasks", "Xamarin.Android.Common.Debugging.targets");
	string directory = "";

	[SetUp]
	public void Setup ()
	{
		directory = Path.Combine (Path.GetTempPath (), "AndroidLauncher-" + Guid.NewGuid ());
		Directory.CreateDirectory (Path.Combine (directory, "android"));
	}

	[TearDown]
	public void TearDown ()
	{
		Directory.Delete (directory, recursive: true);
	}

	static string Entry (string type, string attributes) =>
		$"<{type} {attributes}><intent-filter><category android:name='android.intent.category.LAUNCHER' /></intent-filter></{type}>";

	static string Manifest (string application, string instrumentation = "") =>
		$"<manifest xmlns:android='{AndroidNamespace}'><application>{application}</application>{instrumentation}</manifest>";

	static IEnumerable<TestCaseData> LauncherCases ()
	{
		string firstActivity = Entry ("activity", "android:name='.FirstActivity'");
		string secondActivity = Entry ("activity", "android:name='.SecondActivity'");
		string firstAlias = Entry ("activity-alias", "android:name='.FirstAlias'");
		string secondAlias = Entry ("activity-alias", "android:name='.SecondAlias'");
		string invalidActivities = Entry ("activity", "") + Entry ("activity", "android:name=''") +
			Entry ("activity", "android:name='mono.android.__FastDevLauncher'");
		string invalidAliases = Entry ("activity-alias", "") + Entry ("activity-alias", "android:name=''") +
			Entry ("activity-alias", "android:name='mono.android.__FastDevLauncher'");
		yield return new TestCaseData (firstAlias + firstActivity + secondActivity, ".FirstActivity").SetName ("ActivityTakesPrecedenceOverEarlierAlias");
		yield return new TestCaseData (invalidActivities + firstActivity, ".FirstActivity").SetName ("InvalidActivityNamesAreSkipped");
		yield return new TestCaseData (invalidActivities + invalidAliases + firstAlias + secondAlias, ".FirstAlias").SetName ("FallsBackToFirstValidAlias");
		yield return new TestCaseData ("<activity android:name='.NotLaunchable' />" + firstAlias, ".FirstAlias").SetName ("ActivityWithoutLauncherDoesNotHideAlias");
		yield return new TestCaseData (invalidActivities + invalidAliases, "").SetName ("NoLaunchableActivity");
	}

	[TestCaseSource (nameof (LauncherCases))]
	public void SelectsLauncherWithOneManifestRead (string application, string expected)
	{
		File.WriteAllText (ManifestFile, Manifest (application));
		var (logger, instance) = Build ("_GetAndroidLaunchableActivity");
		Assert.AreEqual (expected, instance.GetPropertyValue ("_AndroidLaunchableActivity"));
		Assert.AreEqual (1, logger.Tasks.Count (task => task == "XmlPeek"));
	}

	[Test]
	public void OnlyFirstApplicationIsConsidered ()
	{
		File.WriteAllText (ManifestFile,
			$"<manifest xmlns:android='{AndroidNamespace}'>" +
			$"<application>{Entry ("activity-alias", "android:name='.FirstAlias'")}</application>" +
			$"<application>{Entry ("activity", "android:name='.SecondActivity'")}</application></manifest>");
		var (logger, instance) = Build ("_GetAndroidLaunchableActivity");
		Assert.AreEqual (".FirstAlias", instance.GetPropertyValue ("_AndroidLaunchableActivity"));
		Assert.AreEqual (1, logger.Tasks.Count (task => task == "XmlPeek"));
	}

	[Test]
	public void MissingManifestDoesNotRead ()
	{
		var (logger, instance) = Build ("_GetAndroidLaunchableActivity");
		Assert.AreEqual ("", instance.GetPropertyValue ("_AndroidLaunchableActivity"));
		Assert.IsEmpty (logger.Tasks);
	}

	[TestCase (".ExplicitActivity", "", "--activity \".ExplicitActivity\"")]
	[TestCase ("", ".ExplicitInstrumentation", "--instrument \".ExplicitInstrumentation\"")]
	[TestCase (".ExplicitActivity", ".ExplicitInstrumentation", "--instrument \".ExplicitInstrumentation\"")]
	public void ExplicitRunOverridesSkipManifest (string activity, string instrumentation, string expectedArgument)
	{
		File.WriteAllText (ManifestFile, "not XML");
		var (logger, instance) = Build ("_AndroidComputeRunArguments", new Dictionary<string, string> {
			["AndroidLaunchActivity"] = activity,
			["AndroidInstrumentation"] = instrumentation,
			["AndroidUseInstrumentation"] = "true",
		});
		Assert.AreEqual (0, ManifestReadCount (logger));
		StringAssert.Contains (expectedArgument, instance.GetPropertyValue ("RunArguments"));
	}

	[Test]
	public void InstrumentationOptInSkipsActivityDiscovery ()
	{
		File.WriteAllText (ManifestFile, Manifest (Entry ("activity", "android:name='.Activity'"),
			"<instrumentation android:name='.Instrumentation' />"));
		var (logger, instance) = Build ("_AndroidComputeRunArguments", new Dictionary<string, string> {
			["AndroidUseInstrumentation"] = "true",
		});
		Assert.AreEqual (".Instrumentation", instance.GetPropertyValue ("AndroidInstrumentation"));
		Assert.AreEqual (1, ManifestReadCount (logger));
	}

	[Test]
	public void InstrumentationFallbackReadsOnlyWhenActivityIsMissing ()
	{
		File.WriteAllText (ManifestFile, Manifest ("", "<instrumentation android:name='.Instrumentation' />"));
		var (logger, instance) = Build ("_AndroidComputeRunArguments");
		Assert.AreEqual (".Instrumentation", instance.GetPropertyValue ("AndroidInstrumentation"));
		Assert.AreEqual (2, ManifestReadCount (logger));
	}

	[TestCase ("RunWithLogging", "AndroidLaunchActivity", "AndroidLaunchActivity")]
	[TestCase ("RunTests", "RunActivity", "TargetTestActivity")]
	public void ExplicitConsumerOverridesSkipManifest (string target, string overrideProperty, string resultProperty)
	{
		File.WriteAllText (ManifestFile, "not XML");
		var (logger, instance) = Build (target, new Dictionary<string, string> {
			["AndroidApplication"] = "true",
			[overrideProperty] = ".Explicit",
			["TargetTestActivity"] = ".Explicit",
		});
		Assert.AreEqual (".Explicit", instance.GetPropertyValue (resultProperty));
		Assert.AreEqual (0, ManifestReadCount (logger));
	}

	[TestCase ("RunWithLogging", "AndroidLaunchActivity")]
	[TestCase ("RunTests", "TargetTestActivity")]
	public void ConsumersStillDiscoverActivities (string target, string resultProperty)
	{
		File.WriteAllText (ManifestFile, Manifest (Entry ("activity-alias", "android:name='.Alias'")));
		var (logger, instance) = Build (target, new Dictionary<string, string> {
			["AndroidApplication"] = "true",
			["AndroidUseInstrumentation"] = "true",
			["AndroidInstrumentation"] = ".Instrumentation",
		});
		Assert.AreEqual (".Alias", instance.GetPropertyValue (resultProperty));
		Assert.AreEqual (1, ManifestReadCount (logger));
	}

	[TestCase (false)]
	[TestCase (true)]
	public void ParallelProjectsUseTheirOwnRelativeManifests (bool multithreaded)
	{
		var resources = new List<ProjectResource> ();
		foreach (string name in new [] { "first", "second" }) {
			string activity = "." + name;
			string type = name == "first" ? "activity" : "activity-alias";
			resources.Add (new ProjectResource {
				Path = Path.Combine (name, "obj", "android", "AndroidManifest.xml"),
				Content = Manifest (Entry (type, $"android:name='{activity}'")),
			});
			var project = new XElement (MSBuildNamespace + "Project",
				new XElement (MSBuildNamespace + "PropertyGroup",
					new XElement (MSBuildNamespace + "IntermediateOutputPath", "obj/")),
				LauncherTarget (),
				new XElement (MSBuildNamespace + "Target",
					new XAttribute ("Name", "Build"),
					new XAttribute ("DependsOnTargets", "_GetAndroidLaunchableActivity"),
					new XElement (MSBuildNamespace + "Error",
						new XAttribute ("Condition", $"'$(_AndroidLaunchableActivity)' != '{activity}'"),
						new XAttribute ("Text", $"The {name} project selected the wrong activity."))));
			resources.Add (new ProjectResource { Path = Path.Combine (name, "launch.proj"), Content = project.ToString () });
		}
		resources.Add (new ProjectResource {
			Path = "driver.proj",
			Content = """
				<Project>
				  <Target Name="Build">
				    <MSBuild Projects="first\launch.proj;second\launch.proj" Targets="Build" BuildInParallel="true" />
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

	static int ManifestReadCount (LaunchLogger logger) =>
		logger.Tasks.Count (task => task == "XmlPeek" || task == "GetAndroidInstrumentationName");

	static XElement LauncherTarget ()
	{
		var root = XDocument.Load (Path.Combine (TasksDirectory, "Xamarin.Android.Common.targets")).Root
			?? throw new InvalidOperationException ("The common targets have no root element.");
		return new XElement (root.Elements (MSBuildNamespace + "Target")
			.Single (target => (string) target.Attribute ("Name") == "_GetAndroidLaunchableActivity"));
	}

	static XElement WithoutDeviceTasks (string path, string name)
	{
		var root = XDocument.Load (path).Root ?? throw new InvalidOperationException ($"{path} has no root element.");
		var target = new XElement (root.Elements ().Single (element =>
			element.Name.LocalName == "Target" && (string) element.Attribute ("Name") == name));
		target.Elements ().Where (element => element.Name.LocalName != "PropertyGroup").Remove ();
		foreach (var element in target.DescendantsAndSelf ())
			element.Name = MSBuildNamespace + element.Name.LocalName;
		return target;
	}

	(LaunchLogger Logger, ProjectInstance Instance) Build (string target, Dictionary<string, string> properties = null)
	{
		var propertyGroup = new XElement (MSBuildNamespace + "PropertyGroup",
			new XElement (MSBuildNamespace + "IntermediateOutputPath", directory + Path.DirectorySeparatorChar),
			new XElement (MSBuildNamespace + "_AndroidComputeRunArgumentsDependsOn", "NoOp"),
			new XElement (MSBuildNamespace + "_XamarinAndroidBuildTasksAssembly", typeof (AndroidError).Assembly.Location),
			new XElement (MSBuildNamespace + "_MicrosoftAndroidBuildTasksAssembly", Path.Combine (TestEnvironment.AndroidMSBuildDirectory, "net", "Microsoft.Android.Build.Tasks.dll")));
		foreach (var property in properties ?? [])
			propertyGroup.Add (new XElement (MSBuildNamespace + property.Key, property.Value));
		var project = new XElement (MSBuildNamespace + "Project",
			propertyGroup,
			new XElement (MSBuildNamespace + "UsingTask",
				new XAttribute ("TaskName", "Xamarin.Android.Tasks.AndroidError"),
				new XAttribute ("AssemblyFile", typeof (AndroidError).Assembly.Location)),
			LauncherTarget (),
			new XElement (MSBuildNamespace + "Import", new XAttribute ("Project", ApplicationTargets)),
			new XElement (MSBuildNamespace + "Import", new XAttribute ("Project", DebuggingTargets)),
			new XElement (MSBuildNamespace + "Target", new XAttribute ("Name", "NoOp")),
			new XElement (MSBuildNamespace + "Target", new XAttribute ("Name", "Install")),
			new XElement (MSBuildNamespace + "Target", new XAttribute ("Name", "AndroidPrepareForBuild")),
			WithoutDeviceTasks (ApplicationTargets, "RunWithLogging"),
			WithoutDeviceTasks (DebuggingTargets, "RunTests"));
		using var reader = project.CreateReader ();
		using var collection = new ProjectCollection ();
		string toolsPath = Directory.EnumerateDirectories (Path.Combine (TestEnvironment.DotNetPreviewDirectory, "sdk"))
			.First (path => File.Exists (Path.Combine (path, "Microsoft.Common.tasks")));
		collection.AddToolset (new Toolset ("Current", toolsPath, collection, ""));
		var root = ProjectRootElement.Create (reader, collection);
		root.FullPath = Path.Combine (directory, "Launch.proj");
		var instance = new ProjectInstance (root, globalProperties: null, toolsVersion: null, collection);
		var logger = new LaunchLogger ();
		Assert.IsTrue (instance.Build (target, new [] { logger }),
			string.Join (Environment.NewLine, logger.Errors.Select (error => $"{error.Code}: {error.Message}")));
		return (logger, instance);
	}

	sealed class LaunchLogger : Logger
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
