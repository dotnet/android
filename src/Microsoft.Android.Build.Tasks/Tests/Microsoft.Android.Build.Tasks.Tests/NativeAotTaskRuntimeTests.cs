using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Android.Tasks;
using NUnit.Framework;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Build.Tests;

[TestFixture]
public class NativeAotTaskRuntimeTests : BaseTest
{
	string DirectoryPath => Path.Combine (Root, "temp", TestName);
	string NativeTargets => TargetPath ("Microsoft.Android.Sdk.NativeAOT.targets");
	string CommonTargets => TargetPath ("Xamarin.Android.Common.targets");
	string DotNetTool => typeof (NativeAotTaskRuntimeTests).Assembly.GetCustomAttributes<AssemblyMetadataAttribute> ()
		.Single (attribute => attribute.Key == "DotNetToolPath").Value ?? throw new InvalidOperationException ("Missing dotnet tool path.");

	[TestCase (false)]
	[TestCase (true)]
	public async Task IlcToolchainIsScopedWithoutChangingMsBuildPath (bool fullMsBuild)
	{
		string buildTool = await GetBuildToolAsync (fullMsBuild);
		var (ndk, toolchain) = CreateNdk ();
		string childPath = Path.Combine (DirectoryPath, "child-path.txt");
		string childMarker = Path.Combine (DirectoryPath, "child-marker.txt");
		string parentPath = Path.Combine (DirectoryPath, "parent-path.txt");
		string toolPaths = Path.Combine (DirectoryPath, "tool-paths.txt");
		string probeProject = Path.Combine (DirectoryPath, "probe.proj");
		new XDocument (new XElement ("Project",
			new XElement ("Target", new XAttribute ("Name", "Probe"),
				WriteFile (childPath, "$([MSBuild]::Escape($([System.Environment]::GetEnvironmentVariable('PATH'))))"),
				WriteFile (childMarker, "$([System.Environment]::GetEnvironmentVariable('ANDROID_NDK_TEST_MARKER'))"))))
			.Save (probeProject);

		var project = CreateProject (ndk);
		project.Add (new XElement ("PropertyGroup",
			new XElement ("RuntimeIdentifier", "android-arm64"),
			new XElement ("_AndroidRuntime", "NativeAOT"),
			new XElement ("AndroidNdkApiLevel_Arm64", "24"),
			new XElement ("_IlcEnvironmentVariables", "ANDROID_NDK_TEST_MARKER=preserved")));
		project.Add (new XElement ("Target", new XAttribute ("Name", "Check"),
			new XAttribute ("DependsOnTargets", "_AndroidBeforeIlcCompile"),
			WriteFile (parentPath, "$([MSBuild]::Escape($([System.Environment]::GetEnvironmentVariable('PATH'))))"),
			WriteFile (toolPaths, "compiler=$(CppCompilerAndLinker);linker=$(CppLinker);archive=$(CppLibCreator);objcopy=$(ObjCopyName)"),
			new XElement ("Exec",
				new XAttribute ("Command", $"\"{DotNetTool}\" msbuild \"{probeProject}\" -nologo -v:quiet -nr:false"),
				new XAttribute ("EnvironmentVariables", "$(_IlcEnvironmentVariables)"))));

		string originalPath = (Environment.GetEnvironmentVariable ("PATH") ?? "") + Path.PathSeparator + "existing;path with spaces";
		var (exitCode, output) = await RunBuildAsync (project, buildTool, fullMsBuild, originalPath);
		Assert.AreEqual (0, exitCode, output);
		Assert.AreEqual (originalPath, ReadValue (parentPath), "NDK preparation must not mutate the MSBuild process's PATH.");
		Assert.AreEqual (Path.Combine (toolchain, "bin") + Path.DirectorySeparatorChar + Path.PathSeparator + originalPath,
			ReadValue (childPath), "The ILC Exec environment must prepend the NDK while retaining escaped PATH separators.");
		Assert.AreEqual ("preserved", ReadValue (childMarker), "Existing ILC environment settings must survive.");
		string executableExt = OperatingSystem.IsWindows () ? ".exe" : "";
		string clang = Path.Combine (toolchain, "bin", "aarch64-linux-android24-clang" + (OperatingSystem.IsWindows () ? ".cmd" : ""));
		CollectionAssert.AreEqual (new [] {
			"compiler=" + clang, "linker=" + clang,
			"archive=" + Path.Combine (toolchain, "bin", "llvm-ar" + executableExt),
			"objcopy=" + Path.Combine (toolchain, "bin", "llvm-objcopy" + executableExt),
		}, File.ReadAllLines (toolPaths));
	}

	[TestCase (false)]
	[TestCase (true)]
	public async Task ResolverReturnsStripAndCheckedBuildPathsAcrossRuntimeBoundary (bool fullMsBuild)
	{
		string buildTool = await GetBuildToolAsync (fullMsBuild);
		var (ndk, toolchain) = CreateNdk ();
		string outputs = Path.Combine (DirectoryPath, "resolver-paths.txt");
		var project = CreateProject (ndk);
		project.Add (new XElement ("PropertyGroup",
			new XElement ("_AndroidRuntime", "CoreCLR"),
			new XElement ("_AndroidStripNativeLibraries", "true"),
			new XElement ("_AndroidCheckedBuild", "asan")));
		project.Add (new XElement ("Target", new XAttribute ("Name", "Check"),
			new XAttribute ("DependsOnTargets", "_AndroidResolveNdk"),
			WriteFile (outputs, "strip=$(_AndroidNdkStripToolPath);clang=$(_AndroidNdkClangRuntimeDirectory);linker=$(_AndroidNdkLinkerToolPath)")));

		var (exitCode, output) = await RunBuildAsync (project, buildTool, fullMsBuild);
		Assert.AreEqual (0, exitCode, output);
		CollectionAssert.AreEqual (new [] {
			"strip=" + Path.Combine (toolchain, "bin", OperatingSystem.IsWindows () ? "llvm-strip.exe" : "llvm-strip"),
			"clang=" + Path.Combine (toolchain, "lib", "clang", "20", "lib", "linux"),
			"linker=",
		}, File.ReadAllLines (outputs));
	}

	[TestCase (false)]
	[TestCase (true)]
	public async Task LinkerFailureAndCleanupCrossRuntimeBoundary (bool fullMsBuild)
	{
		string buildTool = await GetBuildToolAsync (fullMsBuild);
		var (ndk, _) = CreateNdk ();
		string outputLibrary = Path.Combine (DirectoryPath, "libapp.so");
		string debugLibrary = Path.ChangeExtension (outputLibrary, ".dbg.so");
		File.WriteAllText (outputLibrary, "previous library");
		File.WriteAllText (debugLibrary, "previous debug symbols");
		var project = CreateProject (ndk);
		project.Add (new XElement ("PropertyGroup",
			new XElement ("_AndroidRuntime", "NativeAOT"),
			new XElement ("_AndroidNativeAotSharedLibrary", outputLibrary),
			new XElement ("_AndroidNativeAotAbi", "arm64-v8a"),
			new XElement ("NativeObject", Path.Combine (DirectoryPath, "missing.o")),
			new XElement ("NativeIntermediateOutputPath", DirectoryPath + Path.DirectorySeparatorChar)));
		project.Add (new XElement ("ItemGroup",
			new XElement ("_NativeAotLinkLibraries", new XAttribute ("Include", "missing.a"))));
		project.Add (new XElement ("Target", new XAttribute ("Name", "Check"),
			new XAttribute ("DependsOnTargets", "_AndroidResolveNdk"),
			FindElement (NativeTargets, "LinkNativeAotSharedLibrary")));

		var (exitCode, output) = await RunBuildAsync (project, buildTool, fullMsBuild);
		Assert.AreNotEqual (0, exitCode, "An invalid native tool must fail the build.");
		StringAssert.Contains ("XA3007", output, "The task must load and return its native-tool diagnostic, not a task-loader failure.");
		FileAssert.DoesNotExist (outputLibrary);
		FileAssert.DoesNotExist (debugLibrary);
	}

	XElement CreateProject (string ndk)
	{
		var declaration = XDocument.Load (CommonTargets).Descendants ()
			.Single (element => element.Name.LocalName == "UsingTask" &&
				(string?) element.Attribute ("TaskName") == "Microsoft.Android.Tasks.ResolveAndroidNdk");
		return new XElement ("Project", new XAttribute ("Sdk", "Microsoft.NET.Sdk"),
			new XElement ("PropertyGroup",
				new XElement ("TargetFramework", "net11.0"),
				new XElement ("_MicrosoftAndroidBuildTasksAssembly", typeof (ResolveAndroidNdk).Assembly.Location),
				new XElement ("_XamarinAndroidBuildTasksAssembly", typeof (ResolveAndroidNdk).Assembly.Location),
				new XElement ("_AndroidNdkDirectory", ndk)),
			CopyElement (declaration),
			new XElement ("Import", new XAttribute ("Project", NativeTargets)),
			FindTarget (CommonTargets, "_AndroidResolveNdk"),
			new XElement ("Target", new XAttribute ("Name", "_CreatePropertiesCache")),
			new XElement ("Target", new XAttribute ("Name", "_ResolveSdks")));
	}

	(string Ndk, string Toolchain) CreateNdk ()
	{
		string ndk = Path.Combine (DirectoryPath, "ndk tools");
		string toolchain = Path.Combine (ndk, "toolchains", "llvm", "prebuilt", AndroidNdkTools.HostTag);
		Directory.CreateDirectory (Path.Combine (toolchain, "bin"));
		Directory.CreateDirectory (Path.Combine (toolchain, "sysroot", "usr", "lib"));
		Directory.CreateDirectory (Path.Combine (toolchain, "lib", "clang", "20", "lib", "linux"));
		File.WriteAllText (Path.Combine (ndk, "source.properties"), "Pkg.Revision = 29.0.14206865");
		foreach (string tool in new [] { "ld.lld", "llvm-objcopy", "llvm-strip" }) {
			File.WriteAllBytes (Path.Combine (toolchain, "bin", tool + (OperatingSystem.IsWindows () ? ".exe" : "")), []);
		}
		return (ndk, toolchain);
	}

	async Task<string> GetBuildToolAsync (bool fullMsBuild)
	{
		if (!fullMsBuild) {
			return DotNetTool;
		}
		if (!OperatingSystem.IsWindows ()) {
			Assert.Ignore ("Full MSBuild execution requires Windows and Visual Studio 2026 or later.");
		}
		string vswhere = Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.ProgramFilesX86),
			"Microsoft Visual Studio", "Installer", "vswhere.exe");
		if (!File.Exists (vswhere)) {
			Assert.Ignore ("Visual Studio's Full MSBuild is not installed.");
		}
		using var stdout = new StringWriter (CultureInfo.InvariantCulture);
		using var stderr = new StringWriter (CultureInfo.InvariantCulture);
		using var cancellation = new CancellationTokenSource (TimeSpan.FromSeconds (30));
		int exitCode = await ProcessUtils.StartProcess (ProcessUtils.CreateProcessStartInfo (vswhere,
			"-latest", "-prerelease", "-products", "*", "-version", "[18.0,)", "-requires", "Microsoft.Component.MSBuild",
			"-find", "MSBuild/Current/Bin/MSBuild.exe"), stdout, stderr, cancellation.Token);
		Assert.AreEqual (0, exitCode, stderr.ToString ());
		string tool = stdout.ToString ().Trim ();
		if (!File.Exists (tool)) {
			Assert.Ignore ("Full MSBuild 18 or later is required for the .NET task host.");
		}
		return tool;
	}

	async Task<(int ExitCode, string Output)> RunBuildAsync (XElement project, string buildTool, bool fullMsBuild, string? path = null)
	{
		string projectFile = Path.Combine (DirectoryPath, "runtime-test.proj");
		new XDocument (project).Save (projectFile);
		string [] arguments = [projectFile, "-target:Check", "-nologo", "-v:minimal", "-nr:false"];
		if (!fullMsBuild) {
			arguments = ["msbuild", .. arguments];
		}
		var startInfo = ProcessUtils.CreateProcessStartInfo (buildTool, arguments);
		startInfo.WorkingDirectory = DirectoryPath;
		if (path != null) {
			startInfo.Environment ["PATH"] = path;
		}
		if (Path.IsPathFullyQualified (DotNetTool)) {
			startInfo.Environment ["DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR"] =
				Path.GetDirectoryName (DotNetTool) ?? throw new InvalidOperationException ("Missing dotnet directory.");
		}
		using var stdout = new StringWriter (CultureInfo.InvariantCulture);
		using var stderr = new StringWriter (CultureInfo.InvariantCulture);
		using var cancellation = new CancellationTokenSource (TimeSpan.FromMinutes (2));
		int exitCode = await ProcessUtils.StartProcess (startInfo, stdout, stderr, cancellation.Token);
		return (exitCode, stdout.ToString () + stderr.ToString ());
	}

	static string ReadValue (string path) => File.ReadAllText (path).TrimEnd ('\r', '\n');

	static XElement WriteFile (string path, string lines) => new ("WriteLinesToFile",
		new XAttribute ("File", path), new XAttribute ("Lines", lines), new XAttribute ("Overwrite", "true"));

	static string TargetPath (string name) => Path.Combine (
		Path.GetDirectoryName (typeof (NativeAotTaskRuntimeTests).Assembly.Location) ?? throw new InvalidOperationException ("Missing test assembly directory."),
		"TargetInputs", name);

	static XElement FindElement (string path, string name) => CopyElement (XDocument.Load (path).Descendants ()
		.Single (element => element.Name.LocalName == name));

	static XElement FindTarget (string path, string name) => CopyElement (XDocument.Load (path).Descendants ()
		.Single (element => element.Name.LocalName == "Target" && (string?) element.Attribute ("Name") == name));

	static XElement CopyElement (XElement source)
	{
		var copy = new XElement (source);
		foreach (var element in copy.DescendantsAndSelf ()) {
			element.Name = element.Name.LocalName;
		}
		return copy;
	}
}
