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
					new XElement ("TargetName", "App")),
				new XElement ("Import", new XAttribute ("Project", TargetsPath)),
				new XElement ("Target", new XAttribute ("Name", "_GenerateJavaStubs")),
				new XElement ("Target", new XAttribute ("Name", "_GetGenerateJavaStubsInputs"),
					new XElement ("ItemGroup", new XElement ("_EnvironmentFiles", new XAttribute ("Include", environmentFile)))),
				new XElement ("Target", new XAttribute ("Name", "_FindJavaStubFiles"))))
			.Save (projectFile);

		string dotnet = Environment.GetEnvironmentVariable ("DOTNET_HOST_PATH") ?? "dotnet";
		var first = NativeAotBootstrapTestTools.Run (dotnet, "msbuild", projectFile, "-t:_FindJavaStubFiles", "-getItem:FileWrites", "-v:quiet", "-nr:false");
		Assert.AreEqual (0, first.ExitCode, first.Output + first.Error);
		string sourceDirectory = Path.Combine (path, "android", "src", "net", "dot", "jni", "nativeaot");
		string sourceFile = Path.Combine (sourceDirectory, "NativeAotEnvironmentVars.java");
		DateTime timestamp = File.GetLastWriteTimeUtc (sourceFile);
		using (var result = JsonDocument.Parse (first.Output)) {
			var files = result.RootElement.GetProperty ("Items").GetProperty ("FileWrites").EnumerateArray ()
				.Select (file => Path.GetFullPath (file.GetProperty ("Identity").GetString ()
					?? throw new InvalidOperationException ("FileWrites item has no identity"))).ToArray ();
			CollectionAssert.Contains (files, sourceFile);
			CollectionAssert.Contains (files, Path.Combine (sourceDirectory, "NativeAotRuntimeProvider_1.java"));
			CollectionAssert.Contains (files, Path.Combine (sourceDirectory, "NativeAotRuntimeProvider_12.java"));
			CollectionAssert.Contains (files, Path.Combine (path, "nativeaot-bootstrap.inputs"));
			Assert.AreEqual (5, files.Length, "Only the bootstrap files, legitimate providers and fingerprint belong in FileWrites.");
			foreach (string file in files.Where (file => file.EndsWith (".java", StringComparison.Ordinal))) {
				Assert.AreEqual (Path.GetFullPath (sourceDirectory), Path.GetDirectoryName (file));
			}
		}
		CollectionAssert.AreEquivalent (
			new [] { "JavaInteropRuntime.java", "NativeAotEnvironmentVars.java", "NativeAotRuntimeProvider_1.java", "NativeAotRuntimeProvider_12.java" },
			Directory.GetFiles (path, "*.java", SearchOption.AllDirectories).Select (Path.GetFileName));
		StringAssert.Contains ("class NativeAotRuntimeProvider_12", File.ReadAllText (Path.Combine (sourceDirectory, "NativeAotRuntimeProvider_12.java")));

		var second = NativeAotBootstrapTestTools.Run (dotnet, "msbuild", projectFile, "-t:_FindJavaStubFiles", "-v:quiet", "-nr:false");
		Assert.AreEqual (0, second.ExitCode, second.Output + second.Error);
		Assert.AreEqual (timestamp, File.GetLastWriteTimeUtc (sourceFile), "A no-op build must skip the source generator.");

		string providerFile = Path.Combine (sourceDirectory, "NativeAotRuntimeProvider_1.java");
		File.Delete (providerFile);
		var missingProvider = NativeAotBootstrapTestTools.Run (dotnet, "msbuild", projectFile, "-t:_FindJavaStubFiles", "-v:quiet", "-nr:false");
		Assert.AreEqual (0, missingProvider.ExitCode, missingProvider.Output + missingProvider.Error);
		FileAssert.Exists (providerFile, "Provider names must be recovered even when the typemap generator is skipped.");

	}
}
