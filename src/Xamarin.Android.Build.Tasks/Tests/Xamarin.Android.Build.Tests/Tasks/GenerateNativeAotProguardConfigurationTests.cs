using System.IO;
using Microsoft.Build.Utilities;
using NUnit.Framework;
using Xamarin.Android.Tasks;

namespace Xamarin.Android.Build.Tests;

[TestFixture]
[Parallelizable (ParallelScope.Children)]
public class GenerateNativeAotProguardConfigurationTests : BaseTest
{
	[Test]
	public void Execute_UsesDgmlTypeMetadata ()
	{
		var path = Path.Combine (Root, "temp", TestName);
		var dgmlFile = Path.Combine (path, "app.scan.dgml.xml");
		var acwMapFile = Path.Combine (path, "acw-map.txt");
		var outputFile = Path.Combine (path, "proguard", "proguard_project_references.cfg");
		Directory.CreateDirectory (path);
		File.WriteAllText (dgmlFile, """
			<?xml version="1.0" encoding="utf-8"?>
			<DirectedGraph xmlns="http://schemas.microsoft.com/vs/2009/dgml">
			  <Nodes>
			    <Node Id="1" Label="Type metadata: [UnnamedProject]UnnamedProject.MainActivity" />
			    <Node Id="2" Label="Type metadata: [Mono.Android]Android.App.Activity" />
			    <Node Id="3" Label="Type metadata: [My.Assembly]Duplicate.Type" />
			    <Node Id="4" Label="Type metadata: [Xamarin.AndroidX.Activity]AndroidX.Activity.Result.Contract.ActivityResultContracts+TakePicture" />
			    <Node Id="5" Label="Unrelated node" />
			  </Nodes>
			  <Links>
			    <Node Id="6" Label="Type metadata: [Other.Assembly]Other.Type" />
			  </Links>
			</DirectedGraph>
			""");
		File.WriteAllText (acwMapFile, """
			UnnamedProject.MainActivity, UnnamedProject;crc64a1.MainActivity
			Android.App.Activity, Mono.Android;android.app.Activity
			Duplicate.Type, My.Assembly;my.app.Duplicate
			AndroidX.Activity.Result.Contract.ActivityResultContracts+TakePicture, Xamarin.AndroidX.Activity;androidx.activity.result.contract.ActivityResultContracts$TakePicture
			Duplicate.Type;wrong.Duplicate
			Other.Type;other.Type
			""");

		var task = new GenerateNativeAotProguardConfiguration {
			BuildEngine = new MockBuildEngine (TestContext.Out),
			NativeAotDgmlFiles = new [] { new TaskItem (dgmlFile) },
			AcwMapFile = acwMapFile,
			OutputFile = outputFile,
			TrimJavaCallableWrappers = true,
		};

		Assert.IsTrue (task.Execute (), "Task should succeed.");
		var proguard = File.ReadAllText (outputFile);
		StringAssert.Contains ("-keep class crc64a1.MainActivity { *; }", proguard);
		StringAssert.Contains ("-keep class android.app.Activity { *; }", proguard);
		StringAssert.Contains ("-keep class my.app.Duplicate { *; }", proguard);
		StringAssert.Contains ("-keep class androidx.activity.result.contract.ActivityResultContracts$TakePicture { *; }", proguard);
		StringAssert.DoesNotContain ("wrong.Duplicate", proguard);
		StringAssert.DoesNotContain ("other.Type", proguard);
	}

	[Test]
	public void Execute_KeepsAllWhenTrimmingDisabled ()
	{
		var path = Path.Combine (Root, "temp", TestName);
		var acwMapFile = Path.Combine (path, "acw-map.txt");
		var outputFile = Path.Combine (path, "proguard", "proguard_project_references.cfg");
		Directory.CreateDirectory (path);
		File.WriteAllText (acwMapFile, """
			UnnamedProject.MainActivity, UnnamedProject;crc64a1.MainActivity
			Android.App.Activity, Mono.Android;android.app.Activity
			Duplicate.Type, My.Assembly;my.app.Duplicate
			Other.Type;other.Type
			""");

		var task = new GenerateNativeAotProguardConfiguration {
			BuildEngine = new MockBuildEngine (TestContext.Out),
			AcwMapFile = acwMapFile,
			OutputFile = outputFile,
			TrimJavaCallableWrappers = false,
		};

		Assert.IsTrue (task.Execute (), "Task should succeed without a DGML when trimming is disabled.");
		var proguard = File.ReadAllText (outputFile);
		StringAssert.Contains ("-keep class crc64a1.MainActivity { *; }", proguard);
		StringAssert.Contains ("-keep class android.app.Activity { *; }", proguard);
		StringAssert.Contains ("-keep class my.app.Duplicate { *; }", proguard);
		StringAssert.Contains ("-keep class other.Type { *; }", proguard);
	}

	[Test]
	public void Execute_IgnoresDgmlWhenTrimmingDisabled ()
	{
		var path = Path.Combine (Root, "temp", TestName);
		var dgmlFile = Path.Combine (path, "app.scan.dgml.xml");
		var acwMapFile = Path.Combine (path, "acw-map.txt");
		var outputFile = Path.Combine (path, "proguard", "proguard_project_references.cfg");
		Directory.CreateDirectory (path);
		File.WriteAllText (dgmlFile, """
			<?xml version="1.0" encoding="utf-8"?>
			<DirectedGraph xmlns="http://schemas.microsoft.com/vs/2009/dgml">
			  <Nodes>
			    <Node Id="1" Label="Type metadata: [UnnamedProject]UnnamedProject.MainActivity" />
			    <Node Id="2" Label="Type metadata: [Mono.Android]Android.App.Activity" />
			  </Nodes>
			</DirectedGraph>
			""");
		File.WriteAllText (acwMapFile, """
			UnnamedProject.MainActivity, UnnamedProject;crc64a1.MainActivity
			Android.App.Activity, Mono.Android;android.app.Activity
			Other.Type;other.Type
			""");

		var task = new GenerateNativeAotProguardConfiguration {
			BuildEngine = new MockBuildEngine (TestContext.Out),
			NativeAotDgmlFiles = new [] { new TaskItem (dgmlFile) },
			AcwMapFile = acwMapFile,
			OutputFile = outputFile,
			TrimJavaCallableWrappers = false,
		};

		Assert.IsTrue (task.Execute (), "Task should succeed and ignore the DGML when trimming is disabled.");
		var proguard = File.ReadAllText (outputFile);
		StringAssert.Contains ("-keep class crc64a1.MainActivity { *; }", proguard);
		StringAssert.Contains ("-keep class android.app.Activity { *; }", proguard);
		StringAssert.Contains ("-keep class other.Type { *; }", proguard);
	}
}
