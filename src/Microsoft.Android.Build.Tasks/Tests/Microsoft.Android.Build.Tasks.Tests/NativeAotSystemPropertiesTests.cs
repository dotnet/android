using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using Microsoft.Android.Tasks;
using Microsoft.Build.Utilities;
using NUnit.Framework;

namespace Xamarin.Android.Build.Tests;

[TestFixture]
[Platform ("MacOsX,Linux")]
public class NativeAotSystemPropertiesTests : BaseTest
{
	string nativeLibrary = "";
	string libraryDirectory = "";

	static string ResourcesDirectory => Path.Combine (TestContext.CurrentContext.TestDirectory, "Resources", "NativeAotBootstrap");

	[OneTimeSetUp]
	public void CompileProductionReader ()
	{
		string javaTool = NativeAotBootstrapTestTools.JavaTool ("java");
		string javaHome = Path.GetDirectoryName (Path.GetDirectoryName (javaTool))
			?? throw new InvalidOperationException ("Unable to locate JNI headers");
		string root = NativeAotBootstrapTestTools.FindRepositoryRoot ();
		libraryDirectory = Path.Combine (Root, "temp", nameof (NativeAotSystemPropertiesTests));
		Directory.CreateDirectory (libraryDirectory);
		nativeLibrary = Path.Combine (libraryDirectory, OperatingSystem.IsMacOS () ? "libbootstrap-probe.dylib" : "libbootstrap-probe.so");
		var compile = NativeAotBootstrapTestTools.Run (Environment.GetEnvironmentVariable ("CXX") ?? "clang++",
			"-std=c++23", "-shared", "-fPIC",
			"-I" + Path.Combine (javaHome, "include"),
			"-I" + Path.Combine (javaHome, "include", OperatingSystem.IsMacOS () ? "darwin" : "linux"),
			"-I" + Path.Combine (root, "src", "native", "nativeaot", "include"),
			"-I" + Path.Combine (root, "src", "native", "common", "include"),
			"-I" + Path.Combine (root, "external", "Java.Interop", "src", "java-interop"),
			Path.Combine (root, "src", "native", "nativeaot", "runtime-base", "app-system-properties.cc"),
			Path.Combine (ResourcesDirectory, "bootstrap-probe.cc"),
			"-o", nativeLibrary);
		Assert.AreEqual (0, compile.ExitCode, compile.Output + compile.Error);
	}

	[OneTimeTearDown]
	public void DeleteProductionReaderLibrary ()
	{
		if (libraryDirectory.Length > 0 && Directory.Exists (libraryDirectory)) {
			Directory.Delete (libraryDirectory, recursive: true);
		}
	}

	[Test]
	public void ReadsGeneratedPropertiesAsTrueUtf8BeforeOtherNativeInitialization ()
	{
		string value = "\"C:\\quoted\"\t\u00e9\u4e2d\ud83d\ude80";
		string key = "debug.bootstrap.\ud83d\ude80";
		var result = RunProbe ($"{key}={value}\n", key);
		Assert.AreEqual (0, result.ExitCode, result.Output + result.Error);
		Assert.AreEqual (value, Encoding.UTF8.GetString (Convert.FromBase64String (result.Output)));
		StringAssert.DoesNotContain ("WARNING in native method", result.Error);
	}

	[Test]
	public void EmptyConfigurationAndMissingPropertiesResetLength ()
	{
		var result = RunProbe ("", "debug.missing");
		Assert.AreEqual (0, result.ExitCode, result.Output + result.Error);
		Assert.AreEqual ("missing", result.Output);
	}

	[Test]
	public void EmptyPropertyValueIsNotAJavaSystemProperty ()
	{
		var result = RunProbe ("debug.empty=\n", "debug.empty");
		Assert.AreEqual (0, result.ExitCode, result.Output + result.Error);
		Assert.AreEqual ("", result.Output);
	}

	[Test]
	public void KeepsNativePropertySnapshotForProcessLifetime ()
	{
		var result = RunProbe ("debug.property=initial\n", "debug.property", mode: "mutate");
		Assert.AreEqual (0, result.ExitCode, result.Output + result.Error);
		Assert.AreEqual ("initial", Encoding.UTF8.GetString (Convert.FromBase64String (result.Output)));
	}

	[Test]
	public void ReleasesLocalReferencesForLargeConfigurations ()
	{
		string contents = string.Join ("\n", Enumerable.Range (0, 1000).Select (i => $"debug.property.{i}=value-{i}"));
		var result = RunProbe (contents, "debug.property.999");
		Assert.AreEqual (0, result.ExitCode, result.Output + result.Error);
		Assert.AreEqual ("value-999", Encoding.UTF8.GetString (Convert.FromBase64String (result.Output)));
		StringAssert.DoesNotContain ("WARNING in native method", result.Output + result.Error);
	}

	[TestCase ("null", "Null NativeAOT system property array", TestName = "NativeAotNullPropertyArray")]
	[TestCase ("new String[] { \"name\" }", "Invalid NativeAOT system property count", TestName = "NativeAotOddPropertyCount")]
	[TestCase ("new String[] { null, \"value\" }", "Null NativeAOT system property name", TestName = "NativeAotNullPropertyName")]
	[TestCase ("new String[] { \"name\", null }", "Null NativeAOT system property value", TestName = "NativeAotNullPropertyValue")]
	[TestCase ("new String[] { \"\", \"value\" }", "Empty NativeAOT system property name", TestName = "NativeAotEmptyPropertyName")]
	[TestCase ("new String[] { \"name\", \"\\u0000\" }", "NUL character in NativeAOT system property string", TestName = "NativeAotNulPropertyValue")]
	public void InvalidConfigurationFailsExplicitly (string initializer, string message)
	{
		var result = RunProbe ("", "name", configuration: $"static final String[] systemProperties = {initializer};");
		Assert.AreEqual (73, result.ExitCode, result.Output + result.Error);
		StringAssert.Contains (message, result.Error);
	}

	[Test]
	public void MissingJniFieldFailsExplicitly ()
	{
		var result = RunProbe ("", "name", configuration: "static final String[] wrongField = new String[0];");
		Assert.AreEqual (73, result.ExitCode, result.Output + result.Error);
		StringAssert.Contains ("Unable to find NativeAOT system properties", result.Error);
		StringAssert.Contains ("NoSuchFieldError", result.Error);
	}

	[TestCase ("\\ud800", TestName = "NativeAotUnpairedHighSurrogate")]
	[TestCase ("\\udc00", TestName = "NativeAotUnpairedLowSurrogate")]
	public void ReplacesUnpairedSurrogatesWithUtf8ReplacementCharacter (string value)
	{
		var result = RunProbe ("", "name", configuration: $"static final String[] systemProperties = new String[] {{ \"name\", \"{value}\" }};");
		Assert.AreEqual (0, result.ExitCode, result.Output + result.Error);
		Assert.AreEqual ("\ufffd", Encoding.UTF8.GetString (Convert.FromBase64String (result.Output)));
	}

	(int ExitCode, string Output, string Error) RunProbe (string contents, string key, string? mode = null, string? configuration = null)
	{
		string path = Path.Combine (Root, "temp", TestName);
		Directory.CreateDirectory (path);
		string environmentFile = Path.Combine (path, "environment.txt");
		File.WriteAllText (environmentFile, contents);
		var task = new GenerateNativeAotBootstrapSources {
			BuildEngine = new MockBuildEngine (TestContext.Out),
			OutputDirectory = path,
			TargetName = "App",
			Environments = [new TaskItem (environmentFile)],
		};
		Assert.IsTrue (task.Execute ());
		var sources = task.GeneratedSources.ToArray ();
		if (configuration != null) {
			string invalidSourceDirectory = Path.Combine (path, "invalid-configuration");
			Directory.CreateDirectory (invalidSourceDirectory);
			sources [1] = Path.Combine (invalidSourceDirectory, "NativeAotEnvironmentVars.java");
			File.WriteAllText (sources [1], $"package net.dot.jni.nativeaot; public class NativeAotEnvironmentVars {{ {configuration} }}");
		}

		string classesDirectory = Path.Combine (path, "classes");
		Directory.CreateDirectory (classesDirectory);
		var arguments = new List<string> { "-d", classesDirectory };
		arguments.AddRange (sources);
		arguments.AddRange (Directory.GetFiles (ResourcesDirectory, "*.java", SearchOption.AllDirectories));
		var compile = NativeAotBootstrapTestTools.Run (NativeAotBootstrapTestTools.JavaTool ("javac"), arguments.ToArray ());
		Assert.AreEqual (0, compile.ExitCode, compile.Output + compile.Error);
		return NativeAotBootstrapTestTools.Run (NativeAotBootstrapTestTools.JavaTool ("java"),
			"-Xcheck:jni", "-cp", classesDirectory, "BootstrapProbe", nativeLibrary, key, mode ?? "");
	}
}
