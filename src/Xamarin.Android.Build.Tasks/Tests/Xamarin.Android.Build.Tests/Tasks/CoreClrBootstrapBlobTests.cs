#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Utilities;
using NUnit.Framework;
using Xamarin.Android.Tasks;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests.Tasks;

[TestFixture]
[Platform ("MacOsX,Linux")]
public class CoreClrBootstrapBlobTests : BaseTest
{
	string javaHome = "";
	string nativeLibrary = "";
	string libraryDirectory = "";

	[OneTimeSetUp]
	public void CompileProductionReader ()
	{
		string? home = Environment.GetEnvironmentVariable ("JAVA_HOME") ?? Environment.GetEnvironmentVariable ("TEST_ANDROID_JDK_PATH");
		if (home.IsNullOrEmpty ()) {
			Assert.Ignore ("Host JNI bootstrap tests require JAVA_HOME or TEST_ANDROID_JDK_PATH");
		}
		javaHome = home ?? throw new InvalidOperationException ("Java home is required");
		libraryDirectory = Path.Combine (Root, "temp", nameof (CoreClrBootstrapBlobTests));
		Directory.CreateDirectory (libraryDirectory);
		string probe = WriteResource ("CoreClrBootstrapProbe.cc", libraryDirectory);
		nativeLibrary = Path.Combine (libraryDirectory, OperatingSystem.IsMacOS () ? "libbootstrap-blob.dylib" : "libbootstrap-blob.so");
		string root = XABuildPaths.TopDirectory;
		var result = Run (Environment.GetEnvironmentVariable ("CXX") ?? "clang++",
			"-std=c++23", "-Wall", "-Wextra", "-Werror", "-shared", "-fPIC",
			"-I" + Path.Combine (javaHome, "include"),
			"-I" + Path.Combine (javaHome, "include", OperatingSystem.IsMacOS () ? "darwin" : "linux"),
			"-I" + Path.Combine (root, "src", "native", "clr", "include"),
			"-I" + Path.Combine (root, "src", "native", "common", "include"),
			"-I" + Path.Combine (root, "external", "Java.Interop", "src", "java-interop"),
			Path.Combine (root, "src", "native", "clr", "runtime-base", "java-app-config.cc"),
			probe, "-o", nativeLibrary);
		Assert.That (result.ExitCode, Is.EqualTo (0), result.StandardOutput + result.StandardError);
	}

	[OneTimeTearDown]
	public void DeleteProductionReaderLibrary ()
	{
		if (libraryDirectory.Length > 0 && Directory.Exists (libraryDirectory)) {
			Directory.Delete (libraryDirectory, recursive: true);
		}
	}

	[TestCase (false, 0)]
	[TestCase (true, 0)]
	[TestCase (true, 20000)]
	public void ReadsGeneratedBlobAndRetainsOwnedPointers (bool populated, int padding)
	{
		var result = RunProbe (populated, padding, "valid");
		Assert.That (result.ExitCode, Is.EqualTo (0), result.StandardOutput + result.StandardError);
		Assert.That (result.StandardOutput, Is.EqualTo (Convert.ToBase64String (result.Data)));
		Assert.That (result.StandardOutput + result.StandardError, Does.Not.Contain ("WARNING in native method"));
	}

	[Test]
	public void PreservesJniAndPreloadFlags ()
	{
		var result = RunProbe (true, 0, "jni-flags");
		Assert.That (result.ExitCode, Is.EqualTo (0), result.StandardOutput + result.StandardError);
		Assert.That (result.StandardOutput, Is.EqualTo (Convert.ToBase64String (result.Data)));
	}

	[TestCase (false, false)]
	[TestCase (true, false)]
	[TestCase (false, true)]
	[TestCase (true, true)]
	public void DexUsesBulkArrayPayloadsInsteadOfIndividualByteStores (bool debug, bool useR8)
	{
		var probe = RunProbe (true, 20000, "valid");
		Assert.That (probe.ExitCode, Is.EqualTo (0), probe.StandardOutput + probe.StandardError);
		string directory = Path.Combine (Root, "temp", TestName);
		string dexDirectory = Path.Combine (directory, "dex");
		Directory.CreateDirectory (dexDirectory);
		string tools = Directory.GetDirectories (Path.Combine (AndroidSdkPath, "build-tools"))
			.OrderBy (path => path, StringComparer.Ordinal).Last ();
		string androidJar = Directory.GetFiles (Path.Combine (AndroidSdkPath, "platforms"), "android.jar", SearchOption.AllDirectories)
			.OrderBy (path => path, StringComparer.Ordinal).Last ();
		string runtimeJar = Path.Combine (TestEnvironment.DotNetPreviewAndroidSdkDirectory, "tools", "java_runtime_clr.jar");
		string executable = useR8 ? Path.Combine (javaHome, "bin", "java") : Path.Combine (tools, "d8");
		var arguments = new List<string> ();
		if (useR8) {
			arguments.AddRange (["-cp", Path.Combine (tools, "lib", "d8.jar"), "com.android.tools.r8.R8",
				"--pg-conf", Path.Combine (XABuildPaths.TopDirectory, "src", "Xamarin.Android.Build.Tasks", "Resources", "proguard_xamarin.cfg")]);
		}
		arguments.AddRange ([debug ? "--debug" : "--release", "--min-api", "24",
			"--lib", androidJar, useR8 ? "--lib" : "--classpath", runtimeJar, "--output", dexDirectory,
			Path.Combine (directory, "net", "dot", "android", "AppBootstrapConfig.class"),
			Path.Combine (directory, "net", "dot", "android", "ApplicationRegistration.class")]);
		var compile = Run (executable, arguments.ToArray ());
		Assert.That (compile.ExitCode, Is.EqualTo (0), compile.StandardOutput + compile.StandardError);
		var dump = Run (Path.Combine (tools, "dexdump"), "-d", Path.Combine (dexDirectory, "classes.dex"));
		Assert.That (dump.ExitCode, Is.EqualTo (0), dump.StandardError);
		Assert.That (dump.StandardOutput, Does.Contain ("fill-array-data"));
		Assert.That (dump.StandardOutput, Does.Not.Contain ("aput-byte"));
		Assert.That (dump.StandardOutput, Does.Contain ("'NativeConfig'"));
		Assert.That (dump.StandardOutput, Does.Contain ("'NativeConfigLayout'"));
		Assert.That (dump.StandardOutput, Does.Contain ("'NativeLibraryFlags'"));
	}

	[TestCase ("negative-count", "Invalid Java application bootstrap count")]
	[TestCase ("invalid-offset", "Invalid Java application bootstrap offsets")]
	[TestCase ("missing-terminator", "Invalid Java application bootstrap string bounds")]
	[TestCase ("invalid-flag", "Invalid Java native library preload flags")]
	[TestCase ("preload-without-jni", "Invalid Java native library preload flags")]
	public void RejectsMalformedGeneratedData (string scenario, string error)
	{
		var result = RunProbe (true, 0, scenario);
		Assert.That (result.ExitCode, Is.Not.EqualTo (0), result.StandardOutput + result.StandardError);
		Assert.That (result.StandardError, Does.Contain (error));
	}

	(int ExitCode, string StandardOutput, string StandardError, byte [] Data) RunProbe (bool populated, int padding, string scenario)
	{
		string directory = Path.Combine (Root, "temp", TestName);
		Directory.CreateDirectory (directory);
		string environment = Path.Combine (directory, "environment.txt");
		File.WriteAllText (environment, populated
			? "Z_ENV=first\nA_ENV=" + new string ('x', padding) + "\"\\\t\u00e9\u4e2d\ud83d\ude80\nEMPTY_ENV=\ncustom.setting=value\n"
			: "");
		string runtimeConfig = Path.Combine (directory, "app.runtimeconfig.json");
		File.WriteAllText (runtimeConfig, populated
			? """{"runtimeOptions":{"configProperties":{"Z.Switch":"last","A.Switch":"first","HOST_RUNTIME_CONTRACT":"untrusted"}}}"""
			: """{"runtimeOptions":{}}""");
		var task = new GenerateJavaApplicationConfig {
			BuildEngine = new MockBuildEngine (TestContext.Out),
			AndroidPackageName = "example.test",
			OutputFile = Path.Combine (directory, "AppBootstrapConfig.java"),
			Environments = [new TaskItem (environment)],
			ProjectRuntimeConfigFilePath = runtimeConfig,
			NativeLibraries = populated ? [new TaskItem ("libFirst.so"), new TaskItem ("libSecond.so")] : [],
		};
		Assert.IsTrue (task.Execute ());
		string probe = WriteResource ("CoreClrBootstrapProbe.java", directory);
		string androidJar = Directory.GetFiles (Path.Combine (AndroidSdkPath, "platforms"), "android.jar", SearchOption.AllDirectories)
			.OrderBy (path => path, StringComparer.Ordinal).Last ();
		string runtimeJar = Path.Combine (TestEnvironment.DotNetPreviewAndroidSdkDirectory, "tools", "java_runtime_clr.jar");
		var compile = Run (Path.Combine (javaHome, "bin", "javac"), "--release", "17", "-d", directory,
			"-classpath", androidJar + Path.PathSeparator + runtimeJar,
			Path.Combine (XABuildPaths.TopDirectory, "src", "java-runtime", "java", "net", "dot", "android", "ApplicationRegistration.java"),
			task.OutputFile, probe);
		Assert.That (compile.ExitCode, Is.EqualTo (0), compile.StandardOutput + compile.StandardError);
		var result = Run (Path.Combine (javaHome, "bin", "java"), "-Xcheck:jni", "-cp",
			directory + Path.PathSeparator + androidJar + Path.PathSeparator + runtimeJar,
			"net.dot.android.CoreClrBootstrapProbe", nativeLibrary, scenario);
		return (result.ExitCode, result.StandardOutput, result.StandardError, JavaAppConfigTestHelper.Read (File.ReadAllText (task.OutputFile)).Data);
	}

	static string WriteResource (string name, string directory)
	{
		using Stream? resource = typeof (CoreClrBootstrapBlobTests).Assembly.GetManifestResourceStream (name);
		if (resource == null) {
			throw new InvalidOperationException ($"Bootstrap test resource {name} was not found");
		}
		string path = Path.Combine (directory, name);
		using var output = File.Create (path);
		resource.CopyTo (output);
		return path;
	}

	static (int ExitCode, string StandardOutput, string StandardError) Run (string executable, params string [] arguments)
	{
		var startInfo = new ProcessStartInfo (executable) {
			UseShellExecute = false,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
		};
		foreach (string argument in arguments) {
			startInfo.ArgumentList.Add (argument);
		}
		return NativeToolTestHelper.Capture (startInfo, TimeSpan.FromMinutes (2));
	}
}
