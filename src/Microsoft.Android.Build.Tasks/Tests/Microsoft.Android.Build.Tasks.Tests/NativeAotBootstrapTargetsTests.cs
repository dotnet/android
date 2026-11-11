using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;

using ELFSharp.ELF;
using Microsoft.Android.Tasks;
using Microsoft.Build.Framework;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Xamarin.Android.Build.Tests;

[TestClass]
[DoNotParallelize]
public class NativeAotBootstrapTargetsTests : BaseTest
{
	static string TargetsPath => Path.Combine (NativeAotBootstrapTestTools.FindRepositoryRoot (),
		"src", "Xamarin.Android.Build.Tasks", "Microsoft.Android.Sdk", "targets", "Microsoft.Android.Sdk.NativeAOT.targets");

	[TestMethod]
	public void CoreClrBootstrapGenerationIsIndependentOfPackageManagerJava ()
	{
		string root = NativeAotBootstrapTestTools.FindRepositoryRoot ();
		var common = XDocument.Load (Path.Combine (root, "src", "Xamarin.Android.Build.Tasks", "Xamarin.Android.Common.targets"));
		var bootstrap = common.Descendants ().Single (element =>
			element.Name.LocalName == "Target" && (string?)element.Attribute ("Name") == "_AndroidGenerateCoreClrBootstrap");
		Assert.Contains ("'$(_AndroidRuntime)' == 'CoreCLR'", (string?)bootstrap.Attribute ("Condition"));
		var bootstrapTask = bootstrap.Elements ().Single (element => element.Name.LocalName == "GenerateNativeApplicationConfigSources");
		Assert.IsNull (bootstrapTask.Attribute ("MSBuildRuntime"));
		var usingTasks = common.Descendants ().Where (element =>
			element.Name.LocalName == "UsingTask" &&
			(string?)element.Attribute ("TaskName") == "Microsoft.Android.Tasks.GenerateNativeApplicationConfigSources").ToArray ();
		Assert.AreEqual (2, usingTasks.Length);
		Assert.IsTrue (usingTasks.Select (u => (string?)u.Attribute ("AssemblyFile")).All (v => v == "$(_MicrosoftAndroidBuildTasksAssembly)"));
		Assert.IsTrue (usingTasks.Select (u => (string?)u.Attribute ("Runtime")).All (v => v == "NET"));
		var fullUsingTask = usingTasks.Single (u => (string?)u.Attribute ("Condition") == " '$(MSBuildRuntimeType)' == 'Full' ");
		Assert.AreEqual ("TaskHostFactory", (string?)fullUsingTask.Attribute ("TaskFactory"));
		var coreUsingTask = usingTasks.Single (u => (string?)u.Attribute ("Condition") == " '$(MSBuildRuntimeType)' == 'Core' ");
		Assert.IsNull (coreUsingTask.Attribute ("TaskFactory"));
		var taskAssembly = common.Descendants ().Single (element => element.Name.LocalName == "_AndroidCoreClrBootstrapTaskAssembly");
		Assert.Contains ("$(_MicrosoftAndroidBuildTasksAssembly)", taskAssembly.Value);
		var java = common.Descendants ().Single (element =>
			element.Name.LocalName == "Target" && (string?)element.Attribute ("Name") == "_GeneratePackageManagerJava");
		Assert.IsFalse (java.Elements ().Any (element => element.Name.LocalName == "GenerateNativeApplicationConfigSources"));
		var remapping = XDocument.Load (Path.Combine (root, "src", "Xamarin.Android.Build.Tasks", "Microsoft.Android.Sdk", "targets", "Microsoft.Android.Sdk.R8JniRemapping.targets"));
		foreach (string target in new [] { "_AndroidGenerateBinaryBlobsInputs", "_AndroidGenerateR8BinaryBlobsInputs" }) {
			string dependencies = (string?)remapping.Descendants ().Single (element =>
				element.Name.LocalName == "Target" && (string?)element.Attribute ("Name") == target).Attribute ("DependsOnTargets") ?? "";
			Assert.Contains ("_AndroidGenerateCoreClrBootstrap", dependencies);
			Assert.DoesNotContain ("_GeneratePackageManagerJava", dependencies);
		}
	}

	[TestMethod]
	[DataRow ("NativeAOT", 0)]
	[DataRow ("CoreCLR", 1)]
	public void OnlyCoreClrConfigurationTracksEnvironmentInLegacyJavaTarget (string runtime, int expectedCount)
	{
		string path = Path.Combine (Root, "temp", TestName);
		Directory.CreateDirectory (path);
		string commonTargets = Path.Combine (NativeAotBootstrapTestTools.FindRepositoryRoot (),
			"src", "Xamarin.Android.Build.Tasks", "Xamarin.Android.Common.targets");
		var inputs = new XElement (XDocument.Load (commonTargets).Descendants ()
			.Single (element => element.Name.LocalName == "Target" && (string?)element.Attribute ("Name") == "_GetGenerateJavaStubsInputs"));
		foreach (var element in inputs.DescendantsAndSelf ()) {
			element.Name = element.Name.LocalName;
		}
		string projectFile = Path.Combine (path, "environment-inputs.proj");
		new XDocument (
			new XElement ("Project",
				new XElement ("PropertyGroup", new XElement ("_AndroidRuntime", runtime)),
				new XElement ("ItemGroup", new XElement ("AndroidEnvironment", new XAttribute ("Include", Path.Combine (path, "environment.txt")))),
				new XElement ("Target", new XAttribute ("Name", "_GenerateEnvironmentFiles")),
				inputs))
			.Save (projectFile);
		string dotnet = Environment.GetEnvironmentVariable ("DOTNET_HOST_PATH") ?? "dotnet";
		var result = NativeAotBootstrapTestTools.Run (dotnet, "msbuild", projectFile,
			"-t:_GetGenerateJavaStubsInputs", "-getItem:_AndroidCoreClrEnvironmentInputs,_EnvironmentFiles", "-v:quiet", "-nr:false");
		Assert.AreEqual (0, result.ExitCode, result.Output + result.Error);
		using var document = JsonDocument.Parse (result.Output);
		var items = document.RootElement.GetProperty ("Items");
		Assert.AreEqual (expectedCount, items.GetProperty ("_AndroidCoreClrEnvironmentInputs").GetArrayLength ());
		Assert.AreEqual (1, items.GetProperty ("_EnvironmentFiles").GetArrayLength (),
			"NativeAOT must still receive the environment input for its binary data-object generator.");
	}

	[TestMethod]
	public void OptionalDataObjectsInvalidateFailedWritesAndOmitEmptyMappings ()
	{
		string path = Path.Combine (Root, "temp", TestName);
		Directory.CreateDirectory (path);
		string xml = Path.Combine (path, "remap.xml");
		string output = Path.Combine (path, "remap.o");
		string stamp = Path.Combine (path, "remap.stamp");
		File.WriteAllText (xml, """<replacements><replace-type from="example/Original" to="example/Replacement" /></replacements>""");
		var task = new GenerateNativeAotDataObject {
			BuildEngine = new MockBuildEngine (Console.Out),
			Abi = "arm64-v8a",
			OutputFile = output,
			CompletionStamp = stamp,
			Remapping = true,
			RemappingXmlFilePath = xml,
		};
		Assert.IsTrue (task.Execute ());
		Assert.AreEqual ("true", File.ReadAllText (stamp));
		Assert.IsTrue (File.Exists (output));
		ReadOnlySpan<byte> symbols = File.ReadAllBytes (output);
		Assert.IsGreaterThanOrEqualTo (0, symbols.IndexOf (Encoding.ASCII.GetBytes ("\0" + GenerateNativeAotDataObject.RemappingSymbol + "\0")));
		Assert.IsGreaterThanOrEqualTo (0, symbols.IndexOf (Encoding.ASCII.GetBytes ("\0" + GenerateNativeAotDataObject.RemappingSymbol + "_size\0")));
		Assert.AreEqual (-1, symbols.IndexOf (Encoding.ASCII.GetBytes ("\0remapping_data\0")),
			"The static NativeAOT object must not use CoreCLR's dynamic lookup symbol.");
		using (IELF elf = ELFReader.Load (output)) {
			Assert.AreEqual (0, BinaryPrimitives.ReadUInt16LittleEndian (elf.GetSection (".rodata").GetContents ().AsSpan (6, 2)),
				"NativeAOT remapping must always remain mapped, uncompressed data.");
		}
		File.WriteAllText (xml, "<replacements />");
		Assert.IsTrue (task.Execute ());
		Assert.AreEqual ("false", File.ReadAllText (stamp));
		Assert.IsFalse (File.Exists (output), "Semantic empty data must not define optional symbols.");
		File.WriteAllText (output, "stale object");
		File.WriteAllText (xml, "<broken>");
		Assert.IsFalse (task.Execute ());
		Assert.IsFalse (File.Exists (stamp), "Malformed data cannot leave an up-to-date absence stamp.");
		Assert.IsFalse (File.Exists (output));
	}

	[TestMethod]
	[OSCondition (OperatingSystems.OSX | OperatingSystems.Linux)]
	public void FailedDataObjectCleanupUsesCodedErrors ()
	{
		if (OperatingSystem.IsWindows ()) {
			Assert.Inconclusive ("This regression requires Unix file permissions.");
			return;
		}
		string path = Path.Combine (Root, "temp", TestName);
		string outputDirectory = Path.Combine (path, "readonly");
		Directory.CreateDirectory (outputDirectory);
		string output = Path.Combine (outputDirectory, "bootstrap.o");
		string stamp = Path.Combine (path, "bootstrap.stamp");
		File.WriteAllText (output, "stale object");
		File.WriteAllText (stamp, "true");
		var errors = new List<BuildErrorEventArgs> ();
		GenerateNativeAotDataObject CreateTask () => new () {
			BuildEngine = new MockBuildEngine (Console.Out, errors),
			Abi = "arm64-v8a",
			OutputFile = output,
			CompletionStamp = stamp,
		};
		var directoryPermissions = File.GetUnixFileMode (outputDirectory);
		var filePermissions = File.GetUnixFileMode (output);
		const UnixFileMode WritePermissions = UnixFileMode.UserWrite | UnixFileMode.GroupWrite | UnixFileMode.OtherWrite;
		try {
			File.SetUnixFileMode (output, filePermissions & ~WritePermissions);
			File.SetUnixFileMode (outputDirectory, directoryPermissions & ~WritePermissions);
			Assert.IsFalse (CreateTask ().Execute ());
			Assert.AreEqual (2, errors.Count, "Both the write failure and cleanup failure must be reported.");
			Assert.IsTrue (errors.All (error => error.Code == "XA4325"),
				"Cleanup must not escape as a generic task exception.");
			Assert.IsFalse (File.Exists (stamp), "A failed write must invalidate the completion stamp even when output cleanup fails.");
			Assert.AreEqual ("stale object", File.ReadAllText (output));
		} finally {
			File.SetUnixFileMode (outputDirectory, directoryPermissions);
			File.SetUnixFileMode (output, filePermissions);
		}
		errors.Clear ();
		Assert.IsTrue (CreateTask ().Execute (), "Restoring permissions must allow regeneration.");
		Assert.IsEmpty (errors);
		Assert.AreEqual ("true", File.ReadAllText (stamp));
		using IELF elf = ELFReader.Load (output);
		Assert.AreEqual (FileType.Relocatable, elf.Type);
	}

	[TestMethod]
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

		string sourceDirectory = Path.Combine (path, "android", "src", "net", "dot", "jni", "nativeaot");
		Directory.CreateDirectory (sourceDirectory);
		string legacySource = Path.Combine (sourceDirectory, "NativeAotEnvironmentVars.java");
		File.WriteAllText (legacySource, """
			package net.dot.jni.nativeaot;
			public class NativeAotEnvironmentVars {
				static String[] envNames = { "REMOVED_APP_SETTING" };
				static String[] envValues = { "obsolete-value" };
			}
			""");
		string dotnet = Environment.GetEnvironmentVariable ("DOTNET_HOST_PATH") ?? "dotnet";
		var first = NativeAotBootstrapTestTools.Run (dotnet, "msbuild", projectFile, "-t:_FindJavaStubFiles;_AndroidGenerateNativeAotBootstrapObject", "-getItem:FileWrites", "-v:quiet", "-nr:false");
		Assert.AreEqual (0, first.ExitCode, first.Output + first.Error);
		Assert.IsFalse (File.Exists (legacySource), "An upgraded intermediate directory must not retain Java configuration for obsolete environment values.");
		string sourceFile = Path.Combine (sourceDirectory, "JavaInteropRuntime.java");
		string objectFile = Path.Combine (path, "native", "data", "nativeaot-bootstrap.o");
		Assert.IsTrue (File.Exists (objectFile));
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
			Directory.GetFiles (path, "*.java", SearchOption.AllDirectories).Select (Path.GetFileName).ToArray ());
		Assert.Contains ("class NativeAotRuntimeProvider_12", File.ReadAllText (Path.Combine (sourceDirectory, "NativeAotRuntimeProvider_12.java")));

		var second = NativeAotBootstrapTestTools.Run (dotnet, "msbuild", projectFile, "-t:_FindJavaStubFiles;_AndroidGenerateNativeAotBootstrapObject", "-v:quiet", "-nr:false");
		Assert.AreEqual (0, second.ExitCode, second.Output + second.Error);
		Assert.AreEqual (timestamp, File.GetLastWriteTimeUtc (sourceFile), "A no-op build must skip the source generator.");
		Assert.AreEqual (objectTimestamp, File.GetLastWriteTimeUtc (objectFile), "A no-op build must skip the data-object generator.");
		Assert.IsFalse (File.Exists (legacySource));

		var compressionOptIn = NativeAotBootstrapTestTools.Run (dotnet, "msbuild", projectFile,
			"-t:_FindJavaStubFiles;_AndroidGenerateNativeAotBootstrapObject", "-p:_AndroidR8CompressBinaryBlobs=true", "-v:quiet", "-nr:false");
		Assert.AreEqual (0, compressionOptIn.ExitCode, compressionOptIn.Output + compressionOptIn.Error);
		Assert.AreEqual (objectTimestamp, File.GetLastWriteTimeUtc (objectFile),
			"CoreCLR's private compression opt-in must not invalidate NativeAOT data.");
		using (IELF elf = ELFReader.Load (objectFile)) {
			Assert.AreEqual (0, BinaryPrimitives.ReadUInt16LittleEndian (elf.GetSection (".rodata").GetContents ().AsSpan (6, 2)));
		}

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
		Assert.IsTrue (File.Exists (objectFile));

		string providerFile = Path.Combine (sourceDirectory, "NativeAotRuntimeProvider_1.java");
		File.Delete (providerFile);
		var missingProvider = NativeAotBootstrapTestTools.Run (dotnet, "msbuild", projectFile, "-t:_FindJavaStubFiles", "-v:quiet", "-nr:false");
		Assert.AreEqual (0, missingProvider.ExitCode, missingProvider.Output + missingProvider.Error);
		Assert.IsTrue (File.Exists (providerFile), "Provider names must be recovered even when the typemap generator is skipped.");

	}
}
