using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Xml.Linq;

using Microsoft.Android.Tasks;
using NUnit.Framework;

namespace Xamarin.Android.Build.Tests;

[TestFixture]
public class NativeAotBootstrapTargetsTests : BaseTest
{
	static string TargetsPath => Path.Combine (NativeAotBootstrapTestTools.FindRepositoryRoot (),
		"src", "Xamarin.Android.Build.Tasks", "Microsoft.Android.Sdk", "targets", "Microsoft.Android.Sdk.NativeAOT.targets");

	[TestCase (false)]
	[TestCase (true)]
	public void OptionalDataObjectsInvalidateFailedWritesAndOmitEmptyMappings (bool compress)
	{
		string path = Path.Combine (Root, "temp", TestName);
		Directory.CreateDirectory (path);
		string xml = Path.Combine (path, "remap.xml");
		string output = Path.Combine (path, "remap.o");
		string stamp = Path.Combine (path, "remap.stamp");
		File.WriteAllText (xml, """<replacements><replace-type from="example/Original" to="example/Replacement" /></replacements>""");
		var task = new GenerateNativeAotDataObject {
			BuildEngine = new MockBuildEngine (TestContext.Out),
			Abi = "arm64-v8a",
			OutputFile = output,
			CompletionStamp = stamp,
			Remapping = true,
			RemappingXmlFilePath = xml,
			Compress = compress,
		};
		Assert.IsTrue (task.Execute ());
		Assert.AreEqual ("true", File.ReadAllText (stamp));
		FileAssert.Exists (output);
		File.WriteAllText (xml, "<replacements />");
		Assert.IsTrue (task.Execute ());
		Assert.AreEqual ("false", File.ReadAllText (stamp));
		FileAssert.DoesNotExist (output, "Semantic empty data must not define optional symbols.");
		File.WriteAllText (xml, "<broken>");
		Assert.IsFalse (task.Execute ());
		FileAssert.DoesNotExist (stamp, "Malformed data cannot leave an up-to-date absence stamp.");
		FileAssert.DoesNotExist (output);
	}

	[Test]
	public void GeneratedProvidersStayContainedAndIncremental ()
	{
		string path = Path.Combine (Root, "temp", TestName);
		Directory.CreateDirectory (path);
		string environmentFile = Path.Combine (path, "environment.txt");
		File.WriteAllText (environmentFile, "DOTNET_VALUE=first\ndebug.dotnet.max_grefc=1234\n");
		string manifestFile = Path.Combine (path, "AndroidManifest.xml");
		XDocument.Parse ("""
			<manifest xmlns:android="http://schemas.android.com/apk/res/android" xmlns:other="urn:other">
			  <application>
			    <provider android:name="net.dot.jni.nativeaot.NativeAotRuntimeProvider" />
			    <provider android:name="mono.MonoRuntimeProvider_1" />
			    <provider android:name="net.dot.jni.nativeaot.NativeAotRuntimeProvider_1" />
			    <provider android:name="net.dot.jni.nativeaot.NativeAotRuntimeProvider_12" />
			    <provider android:name="net.dot.jni.nativeaot.NativeAotRuntimeProvider_12" />
			    <provider android:name="net.dot.jni.nativeaot.NativeAotRuntimeProvider_" />
			    <provider android:name="net.dot.jni.nativeaot.NativeAotRuntimeProvider_-1" />
			    <provider android:name="net.dot.jni.nativeaot.NativeAotRuntimeProvider_1 " />
			    <provider android:name="net.dot.jni.nativeaot.NativeAotRuntimeProvider_&#x0661;" />
			    <provider android:name="net.dot.jni.nativeaot.NativeAotRuntimeProvider_2/../../../../../escaped" />
			    <provider android:name="net.dot.jni.nativeaot.NativeAotRuntimeProvider_3\..\..\..\..\..\escaped" />
			    <provider other:name="net.dot.jni.nativeaot.NativeAotRuntimeProvider_4" />
			  </application>
			</manifest>
			""").Save (manifestFile);
		string projectFile = Path.Combine (path, "bootstrap.proj");
		new XDocument (
			new XElement ("Project",
				new XElement ("PropertyGroup",
					new XElement ("_MicrosoftAndroidBuildTasksAssembly", typeof (GenerateNativeAotBootstrapSources).Assembly.Location),
					new XElement ("_XamarinAndroidBuildTasksAssembly", typeof (GenerateNativeAotBootstrapSources).Assembly.Location),
					new XElement ("IntermediateOutputPath", path + Path.DirectorySeparatorChar),
					new XElement ("NativeIntermediateOutputPath", Path.Combine (path, "native") + Path.DirectorySeparatorChar),
					new XElement ("RuntimeIdentifier", "android-arm64"),
					new XElement ("TargetName", "App")),
				new XElement ("Import", new XAttribute ("Project", TargetsPath)),
				new XElement ("Target", new XAttribute ("Name", "_GenerateJavaStubs")),
				new XElement ("Target", new XAttribute ("Name", "_GetGenerateJavaStubsInputs"),
					new XElement ("ItemGroup", new XElement ("_EnvironmentFiles", new XAttribute ("Include", environmentFile)))),
				new XElement ("Target", new XAttribute ("Name", "_FindJavaStubFiles"))))
			.Save (projectFile);

		string dotnet = Environment.GetEnvironmentVariable ("DOTNET_HOST_PATH") ?? "dotnet";
		var first = NativeAotBootstrapTestTools.Run (dotnet, "msbuild", projectFile, "-t:_FindJavaStubFiles;_AndroidGenerateNativeAotBootstrapObject", "-getItem:FileWrites", "-v:quiet", "-nr:false");
		Assert.AreEqual (0, first.ExitCode, first.Output + first.Error);
		string sourceDirectory = Path.Combine (path, "android", "src", "net", "dot", "jni", "nativeaot");
		string sourceFile = Path.Combine (sourceDirectory, "JavaInteropRuntime.java");
		string objectFile = Path.Combine (path, "native", "data", "nativeaot-bootstrap.o");
		FileAssert.Exists (objectFile);
		DateTime objectTimestamp = File.GetLastWriteTimeUtc (objectFile);
		DateTime timestamp = File.GetLastWriteTimeUtc (sourceFile);
		using (var result = JsonDocument.Parse (first.Output)) {
			var files = result.RootElement.GetProperty ("Items").GetProperty ("FileWrites").EnumerateArray ()
				.Select (file => Path.GetFullPath (file.GetProperty ("Identity").GetString ()
					?? throw new InvalidOperationException ("FileWrites item has no identity"))).ToArray ();
			CollectionAssert.Contains (files, sourceFile);
			CollectionAssert.Contains (files, Path.Combine (sourceDirectory, "NativeAotRuntimeProvider_1.java"));
			CollectionAssert.Contains (files, Path.Combine (sourceDirectory, "NativeAotRuntimeProvider_12.java"));
			CollectionAssert.Contains (files, Path.Combine (path, "nativeaot-bootstrap.inputs"));
			CollectionAssert.Contains (files, objectFile);
			Assert.AreEqual (6, files.Length, "Only minimal Java providers, static bootstrap data and fingerprints belong in FileWrites.");
			foreach (string file in files.Where (file => file.EndsWith (".java", StringComparison.Ordinal))) {
				Assert.AreEqual (Path.GetFullPath (sourceDirectory), Path.GetDirectoryName (file));
			}
		}
		CollectionAssert.AreEquivalent (
			new [] { "JavaInteropRuntime.java", "NativeAotRuntimeProvider_1.java", "NativeAotRuntimeProvider_12.java" },
			Directory.GetFiles (path, "*.java", SearchOption.AllDirectories).Select (Path.GetFileName));
		StringAssert.Contains ("class NativeAotRuntimeProvider_12", File.ReadAllText (Path.Combine (sourceDirectory, "NativeAotRuntimeProvider_12.java")));

		var second = NativeAotBootstrapTestTools.Run (dotnet, "msbuild", projectFile, "-t:_FindJavaStubFiles;_AndroidGenerateNativeAotBootstrapObject", "-v:quiet", "-nr:false");
		Assert.AreEqual (0, second.ExitCode, second.Output + second.Error);
		Assert.AreEqual (timestamp, File.GetLastWriteTimeUtc (sourceFile), "A no-op build must skip the source generator.");
		Assert.AreEqual (objectTimestamp, File.GetLastWriteTimeUtc (objectFile), "A no-op build must skip the data-object generator.");

		File.WriteAllText (environmentFile, "DOTNET_VALUE=second\ndebug.dotnet.max_grefc=5678\n");
		File.SetLastWriteTimeUtc (environmentFile, objectTimestamp.AddSeconds (1));
		var changed = NativeAotBootstrapTestTools.Run (dotnet, "msbuild", projectFile, "-t:_FindJavaStubFiles;_AndroidGenerateNativeAotBootstrapObject", "-v:quiet", "-nr:false");
		Assert.AreEqual (0, changed.ExitCode, changed.Output + changed.Error);
		Assert.AreEqual (timestamp, File.GetLastWriteTimeUtc (sourceFile), "Environment data must not regenerate Java.");
		Assert.AreNotEqual (objectTimestamp, File.GetLastWriteTimeUtc (objectFile), "Environment data must regenerate the object.");
		File.SetLastWriteTimeUtc (environmentFile, timestamp.AddSeconds (-1));
		File.Delete (objectFile);
		var missingObject = NativeAotBootstrapTestTools.Run (dotnet, "msbuild", projectFile, "-t:_AndroidGenerateNativeAotBootstrapObject", "-v:quiet", "-nr:false");
		Assert.AreEqual (0, missingObject.ExitCode, missingObject.Output + missingObject.Error);
		FileAssert.Exists (objectFile);

		string providerFile = Path.Combine (sourceDirectory, "NativeAotRuntimeProvider_1.java");
		File.Delete (providerFile);
		var missingProvider = NativeAotBootstrapTestTools.Run (dotnet, "msbuild", projectFile, "-t:_FindJavaStubFiles", "-v:quiet", "-nr:false");
		Assert.AreEqual (0, missingProvider.ExitCode, missingProvider.Output + missingProvider.Error);
		FileAssert.Exists (providerFile, "Provider names must be recovered even when the typemap generator is skipped.");

	}
}
