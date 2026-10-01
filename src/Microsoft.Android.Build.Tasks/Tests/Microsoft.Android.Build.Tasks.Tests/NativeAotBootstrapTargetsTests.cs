using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
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
	public void RetainsCryptoInitializerWithoutSchedulingLlvmBootstrapInputs ()
	{
		string contents = File.ReadAllText (TargetsPath);
		StringAssert.Contains ("System.Security.Cryptography.Native.Android!AndroidCryptoNative_InitLibraryOnLoad", contents);
		StringAssert.DoesNotContain ("_PrivateJniInit", contents);
		StringAssert.DoesNotContain ("_PrivateEnvironment", contents);
		StringAssert.DoesNotContain ("GenerateNativeAotLibraryLoadAssemblerSources", contents);
		StringAssert.DoesNotContain ("GenerateNativeAotEnvironmentAssemblerSources", contents);
		StringAssert.DoesNotContain ("CompileNativeAssembly", contents);
		StringAssert.Contains ("AdditionalObjectFiles=\"@(_NativeAotAdditionalObjects)\"", contents);

		var project = XDocument.Load (TargetsPath);
		var gate = project.Descendants ("Target").Single (target => (string?) target.Attribute ("Name") == "_AndroidValidateNativeAotJniInitFunctions");
		var error = gate.Element ("AndroidError") ?? throw new InvalidOperationException ("Missing NativeAOT JNI initializer error");
		Assert.AreEqual ("XA1051", (string?) error.Attribute ("Code"));
		Assert.AreEqual ("XA1051", (string?) error.Attribute ("ResourceName"));
		StringAssert.Contains ("AndroidStaticJniInitFunction", Xamarin.Android.Tasks.Properties.Resources.XA1051);
		StringAssert.Contains ("AndroidStaticJniInitFunction->Count()", (string?) error.Attribute ("Condition"));
		StringAssert.Contains ("_AndroidValidateNativeAotJniInitFunctions", project.Descendants ("_AndroidBeforeIlcCompileDependsOn").Single ().Value);
		StringAssert.Contains ("_AndroidValidateNativeAotJniInitFunctions",
			(string?) project.Descendants ("Target").Single (target => (string?) target.Attribute ("Name") == "_PrepareNativeAotAndroidAppInputs").Attribute ("DependsOnTargets"));
	}

	[Test]
	public void GeneratesIncrementallyAndRecordsCleanOutputs ()
	{
		string path = Path.Combine (Root, "temp", TestName);
		Directory.CreateDirectory (path);
		string environmentFile = Path.Combine (path, "environment.txt");
		File.WriteAllText (environmentFile, "DOTNET_VALUE=first\ndebug.dotnet.max_grefc=1234\n");
		string manifestFile = Path.Combine (path, "AndroidManifest.xml");
		XDocument.Parse ("""
			<manifest xmlns:android="http://schemas.android.com/apk/res/android">
			  <application>
			    <provider android:name="net.dot.jni.nativeaot.NativeAotRuntimeProvider_1" />
			    <provider android:name="net.dot.jni.nativeaot.NativeAotRuntimeProvider_2/../../../../../escaped" />
			    <provider android:name="net.dot.jni.nativeaot.NativeAotRuntimeProvider_3\..\..\..\..\..\escaped" />
			  </application>
			</manifest>
			""").Save (manifestFile);
		string projectFile = Path.Combine (path, "bootstrap.proj");
		var project = new XDocument (
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
				new XElement ("Target", new XAttribute ("Name", "_FindJavaStubFiles"))));
		project.Save (projectFile);

		string dotnet = Environment.GetEnvironmentVariable ("DOTNET_HOST_PATH") ?? "dotnet";
		var first = NativeAotBootstrapTestTools.Run (dotnet, "msbuild", projectFile, "-t:_FindJavaStubFiles", "-getItem:FileWrites", "-v:quiet", "-nr:false");
		Assert.AreEqual (0, first.ExitCode, first.Output + first.Error);
		string sourceDirectory = Path.Combine (path, "android", "src", "net", "dot", "jni", "nativeaot");
		string sourceFile = Path.Combine (sourceDirectory, "NativeAotEnvironmentVars.java");
		StringAssert.Contains ("\"first\"", File.ReadAllText (sourceFile));
		DateTime timestamp = File.GetLastWriteTimeUtc (sourceFile);
		using (var result = JsonDocument.Parse (first.Output)) {
			var files = result.RootElement.GetProperty ("Items").GetProperty ("FileWrites").EnumerateArray ()
				.Select (file => Path.GetFullPath (file.GetProperty ("Identity").GetString ()
					?? throw new InvalidOperationException ("FileWrites item has no identity"))).ToArray ();
			CollectionAssert.Contains (files, sourceFile);
			CollectionAssert.Contains (files, Path.Combine (sourceDirectory, "NativeAotRuntimeProvider_1.java"));
			Assert.AreEqual (3, files.Length, "Only the bootstrap files and legitimate provider belong in FileWrites.");
			foreach (string file in files) {
				Assert.AreEqual (Path.GetFullPath (sourceDirectory), Path.GetDirectoryName (file));
			}
		}

		var second = NativeAotBootstrapTestTools.Run (dotnet, "msbuild", projectFile, "-t:_FindJavaStubFiles", "-v:quiet", "-nr:false");
		Assert.AreEqual (0, second.ExitCode, second.Output + second.Error);
		Assert.AreEqual (timestamp, File.GetLastWriteTimeUtc (sourceFile), "A no-op build must skip the source generator.");

		string providerFile = Path.Combine (sourceDirectory, "NativeAotRuntimeProvider_1.java");
		File.Delete (providerFile);
		var missingProvider = NativeAotBootstrapTestTools.Run (dotnet, "msbuild", projectFile, "-t:_FindJavaStubFiles", "-v:quiet", "-nr:false");
		Assert.AreEqual (0, missingProvider.ExitCode, missingProvider.Output + missingProvider.Error);
		FileAssert.Exists (providerFile, "Provider names must be recovered even when the typemap generator is skipped.");

		Thread.Sleep (50);
		File.WriteAllText (environmentFile, "DOTNET_VALUE=second\ndebug.dotnet.max_grefc=4321\n");
		var changed = NativeAotBootstrapTestTools.Run (dotnet, "msbuild", projectFile, "-t:_FindJavaStubFiles", "-v:quiet", "-nr:false");
		Assert.AreEqual (0, changed.ExitCode, changed.Output + changed.Error);
		string changedSource = File.ReadAllText (sourceFile);
		StringAssert.Contains ("\"second\"", changedSource);
		StringAssert.Contains ("\"4321\"", changedSource);
		Assert.Greater (File.GetLastWriteTimeUtc (sourceFile), timestamp);

		File.Delete (sourceFile);
		var missing = NativeAotBootstrapTestTools.Run (dotnet, "msbuild", projectFile, "-t:_FindJavaStubFiles", "-v:quiet", "-nr:false");
		Assert.AreEqual (0, missing.ExitCode, missing.Output + missing.Error);
		FileAssert.Exists (sourceFile);
	}
}
