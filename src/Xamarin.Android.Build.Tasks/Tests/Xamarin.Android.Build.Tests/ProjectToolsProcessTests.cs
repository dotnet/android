using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using NUnit.Framework;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests;

[TestFixture, NonParallelizable]
public class ProjectToolsProcessTests : BaseTest
{
	string directory = "";

	[SetUp]
	public void CreateProcessDirectory ()
	{
		directory = Path.Combine (Path.GetTempPath (), $"project-process-{Guid.NewGuid ():N}");
		Directory.CreateDirectory (directory);
	}

	[TearDown]
	public void DeleteProcessDirectory () => FileSystemUtils.DeleteDirectoryWithRetry (directory);

	string Script (string name, string contents)
	{
		if (OperatingSystem.IsWindows ())
			throw new PlatformNotSupportedException ("Shell fixtures require Unix.");
		string path = Path.Combine (directory, name);
		using (var writer = new StreamWriter (path)) {
			writer.WriteLine ("#!/bin/sh");
			writer.WriteLine (contents);
		}
		File.SetUnixFileMode (path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
		return path;
	}

	[TestCase (false, false)]
	[TestCase (true, false)]
	[TestCase (false, true)]
	[TestCase (true, true)]
	public void BuildHarnessLogsBothStreamsOnce (bool succeed, bool useDotNetCLI)
	{
		var project = new XamarinAndroidLibraryProject {
			TargetFramework = "net11.0",
			Imports = {
				new Import ("ProcessOutput.targets") {
					TextContent = () => """
						<Project>
						  <UsingTask TaskName="WriteProcessOutput" TaskFactory="RoslynCodeTaskFactory" AssemblyFile="$(MSBuildToolsPath)/Microsoft.Build.Tasks.Core.dll">
						    <ParameterGroup>
						      <Succeed ParameterType="System.Boolean" Required="true" />
						    </ParameterGroup>
						    <Task>
						      <Code Type="Fragment" Language="cs"><![CDATA[
						        System.Threading.Tasks.Task.WaitAll (
						          System.Threading.Tasks.Task.Run (() => {
						            for (int i = 0; i < 1024; i++)
						              System.Console.WriteLine ("stdout-" + i + ":" + new string ('x', 128));
						          }),
						          System.Threading.Tasks.Task.Run (() => {
						            for (int i = 0; i < 1024; i++)
						              System.Console.Error.WriteLine ("stderr-" + i + ":" + new string ('x', 128));
						          }));
						        return Succeed;
						      ]]></Code>
						    </Task>
						  </UsingTask>
						  <Target Name="WriteOutput">
						    <WriteProcessOutput Succeed="$(Succeed)" />
						  </Target>
						</Project>
						""",
				},
			},
		};
		using var builder = new ProjectBuilder (directory) {
			AutomaticNuGetRestore = false,
			ThrowOnBuildFailure = false,
			MaxCpuCount = 1,
			Target = "WriteOutput",
		};
		string logPath;
		if (useDotNetCLI) {
			builder.Save (project);
			var cli = new DotNetCLI (Path.Combine (directory, project.ProjectFilePath));
			Assert.AreEqual (succeed, cli.Build (target: "WriteOutput", parameters: [$"Succeed={succeed}"]));
			logPath = cli.ProcessLogFile;
		} else {
			Assert.AreEqual (succeed, builder.Build (project, parameters: [$"Succeed={succeed}"]));
			logPath = Path.Combine (directory, "process.log");
		}
		string [] lines = File.ReadAllLines (logPath);
		foreach (string stream in new [] { "stdout", "stderr" }) {
			var expected = Enumerable.Range (0, 1024).Select (i => $"{stream}-{i}:" + new string ('x', 128));
			CollectionAssert.AreEqual (expected, lines.Where (line => line.StartsWith (stream + "-", StringComparison.Ordinal)));
		}
		Assert.AreEqual ($"{(useDotNetCLI ? "Exit Code" : "ExitCode")}: {(succeed ? 0 : 1)}", lines.Last ());
		using var log = File.Open (logPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
	}

	[Test]
	[Platform (Exclude = "Win")]
	public void SevenZipKeepsArgumentBoundariesAndBothDiagnostics ()
	{
		string argumentsFile = Path.Combine (directory, "arguments");
		Script ("7z", $"printf '%s\\n' \"$@\" > '{argumentsFile}'\necho stdout-line\necho stderr-line >&2\nexit 7");
		string archivePath = Path.Combine (directory, "archive with spaces.7z");
		string destination = Path.Combine (directory, "destination with spaces");
		string previousPath = Environment.GetEnvironmentVariable ("PATH");
		var previousOutput = Console.Out;
		using var output = new StringWriter ();
		try {
			Environment.SetEnvironmentVariable ("PATH", directory + Path.PathSeparator + previousPath);
			Console.SetOut (output);
			using var archive = new SevenZipHelper (archivePath);
			Assert.IsFalse (archive.ExtractAll (destination));
		} finally {
			Console.SetOut (previousOutput);
			Environment.SetEnvironmentVariable ("PATH", previousPath);
		}
		CollectionAssert.AreEqual (new [] { "x", archivePath, "-o" + destination }, File.ReadAllLines (argumentsFile));
		CollectionAssert.AreEqual (new [] { "stdout-line", "stderr-line" }.OrderBy (line => line, StringComparer.Ordinal),
			output.ToString ().Split (Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).OrderBy (line => line, StringComparer.Ordinal));
	}

	[TestCase (0)]
	[TestCase (7)]
	[Platform (Exclude = "Win")]
	public void GradleLogsBothStreamsOnceAndPreservesFailure (int exitCode)
	{
		var gradle = new GradleCLI {
			GradlePath = Script ("gradle", $$"""
				i=0
				while [ "$i" -lt 1024 ]; do
					echo stdout-$i
					echo stderr-$i >&2
					i=$((i + 1))
				done
				printf stdout-tail
				printf stderr-tail >&2
				exit {{exitCode}}
				"""),
			ProjectDirectory = directory,
			ProcessLogFile = Path.Combine (directory, "gradle.log"),
		};
		Assert.AreEqual (exitCode == 0, gradle.Execute ());
		string [] lines = File.ReadAllLines (gradle.ProcessLogFile);
		foreach (string stream in new [] { "stdout", "stderr" }) {
			var expected = Enumerable.Range (0, 1024).Select (i => $"{stream}-{i}").Append ($"{stream}-tail");
			CollectionAssert.AreEqual (expected, lines.Where (line => line.StartsWith (stream + "-", StringComparison.Ordinal)));
		}
		Assert.AreEqual ($"Exit Code: {exitCode}", lines.Last ());
	}

	[TestCase ("javac")]
	[TestCase ("jar")]
	[Platform (Exclude = "Win")]
	public void JarFailureRetainsRawDiagnostics (string tool)
	{
		var builder = new JarContentBuilder {
			BaseDirectory = directory,
			JavaSourceFileName = "Payload.java",
			JavaSourceText = "public class Payload {}",
			JarFileName = "payload.jar",
			JavacFullPath = Script ("javac", tool == "javac" ? "printf 'stdout\\r\\ntail'; printf 'stderr\\r\\ntail' >&2; exit 7" : ": > Payload.class"),
			JarFullPath = Script ("jar", "printf 'stdout\\r\\ntail'; printf 'stderr\\r\\ntail' >&2; exit 7"),
		};
		var error = Assert.Throws<InvalidOperationException> (() => builder.Build ());
		Assert.IsNotNull (error);
		StringAssert.Contains ("stdout\r\ntail", error.Message);
		StringAssert.Contains ("stderr\r\ntail", error.Message);
		StringAssert.StartsWith ($"`{(tool == "javac" ? "Javac" : "Jar")}` command line tool did not successfully finish:", error.Message);
	}

	[Test]
	[Platform (Exclude = "Win")]
	public void JarRunsOnceAndContainsCompiledClass ()
	{
		string jdk = AndroidSdkResolver.GetJavaSdkPath ();
		string invocations = Path.Combine (directory, "jar-invocations");
		var builder = new JarContentBuilder {
			BaseDirectory = directory,
			JavaSourceFileName = "Payload.java",
			JavaSourceText = "public class Payload { public int value() { return 42; } }",
			JarFileName = "payload.jar",
			JavacFullPath = Path.Combine (jdk, "bin", "javac"),
			JarFullPath = Script ("jar", $"echo jar >> '{invocations}'\nexec '{Path.Combine (jdk, "bin", "jar")}' \"$@\""),
		};
		using var stream = new MemoryStream (builder.Build ());
		using var archive = new ZipArchive (stream, ZipArchiveMode.Read);
		CollectionAssert.AreEqual (new [] { "jar" }, File.ReadAllLines (invocations));
		Assert.IsNotNull (archive.GetEntry ("Payload.class"));
		Assert.IsNotNull (archive.GetEntry ("META-INF/MANIFEST.MF"));
	}
}
