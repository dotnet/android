using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Android.Tasks;
using Microsoft.Build.Framework;
using NUnit.Framework;

namespace Xamarin.Android.Build.Tests;

[TestFixture]
[Parallelizable (ParallelScope.Children)]
public class LinkNativeAotSharedLibraryTests : BaseTest
{
	const string LinkerScript = """
		#!/bin/sh
		printf '%s\n%s\n' "$PWD" "$TASK_VALUE" > tool-environment
		while [ "$#" -gt 0 ]; do
			if [ "$1" = "-o" ]; then
				shift
				printf linked > "$1"
				break
			fi
			shift
		done
		""";

	[Test]
	public void ConcurrentToolsUseProjectEnvironments ()
	{
		if (OperatingSystem.IsWindows ()) {
			Assert.Ignore ("The fake linker is a shell script.");
		}

		var first = CreateTask ("first", "one", LinkerScript);
		var second = CreateTask ("second", "two", LinkerScript);
		foreach (var fixture in new [] { first, second }) {
			File.Delete (Path.Combine (fixture.Directory, "tool-environment"));
			File.Delete (Path.Combine (fixture.Directory, "out", "libapp.so"));
		}

		var executions = Task.WhenAll (Task.Run (first.Task.Execute), Task.Run (second.Task.Execute));
		Assert.IsTrue (executions.Wait (TimeSpan.FromSeconds (10)), "The linker tasks did not finish.");
		Assert.That (executions.Result, Is.EqualTo (new [] { true, true }));
		AssertProjectResult (first, "one");
		AssertProjectResult (second, "two");
	}

	[TestCase (false)]
	[TestCase (true)]
	public void ToolRunsUnderMsbuild (bool multithreaded)
	{
		if (OperatingSystem.IsWindows ()) {
			Assert.Ignore ("The fake linker is a shell script.");
		}

		var fixture = CreateTask ("msbuild", "unused", LinkerScript);
		string output = Path.Combine (fixture.Directory, "out", "libapp.so");
		File.Delete (output);
		string project = Path.Combine (fixture.Directory, "link.proj");
		new XDocument (new XElement ("Project",
			new XElement ("UsingTask",
				new XAttribute ("TaskName", typeof (LinkNativeAotSharedLibrary).FullName ?? throw new InvalidOperationException ("Missing task type name.")),
				new XAttribute ("AssemblyFile", typeof (LinkNativeAotSharedLibrary).Assembly.Location)),
			new XElement ("ItemGroup", new XElement ("NativeLibrary", new XAttribute ("Include", "input.a"))),
			new XElement ("Target", new XAttribute ("Name", "Build"),
				new XElement ("LinkNativeAotSharedLibrary",
					new XAttribute ("LinkerToolPath", fixture.Task.LinkerToolPath),
					new XAttribute ("ObjcopyToolPath", fixture.Task.ObjcopyToolPath),
					new XAttribute ("IntermediateOutputPath", "obj"),
					new XAttribute ("NativeObject", "input.o"),
					new XAttribute ("NativeLibraries", "@(NativeLibrary)"),
					new XAttribute ("OutputSharedLibrary", Path.Combine ("out", "libapp.so")),
					new XAttribute ("Abi", "arm64-v8a"),
					new XAttribute ("DebugBuild", "true"),
					new XAttribute ("YieldDuringToolExecution", "true")))))
			.Save (project);

		string dotnet = Environment.GetEnvironmentVariable ("DOTNET_HOST_PATH") ?? "dotnet";
		var arguments = new [] { "msbuild", project, "-t:Build", "-v:minimal", "-m", "-nr:false" };
		if (multithreaded) {
			arguments = arguments.Concat (new [] { "-mt", "-warnaserror:MSB4286,MSB4287,MSB4288" }).ToArray ();
		}
		var result = NativeAotBootstrapTestTools.Run (dotnet, arguments);
		Assert.AreEqual (0, result.ExitCode, result.Output + result.Error);
		Assert.AreEqual ("linked", File.ReadAllText (output));
	}

	[Test]
	public void CancellationStopsToolAndCleansOutput ()
	{
		if (OperatingSystem.IsWindows ()) {
			Assert.Ignore ("The fake linker is a shell script.");
		}

		var fixture = CreateTask ("cancel", "cancelled", """
			#!/bin/sh
			printf started > tool-started
			exec /bin/sleep 30
			""");
		string output = Path.Combine (fixture.Directory, "out", "libapp.so");
		Directory.CreateDirectory (Path.GetDirectoryName (output) ?? throw new InvalidOperationException ("Missing output directory."));
		File.WriteAllText (output, "partial");

		File.Delete (Path.Combine (fixture.Directory, "tool-started"));
		var execution = Task.Run (fixture.Task.Execute);
		bool started = SpinWait.SpinUntil (() => File.Exists (Path.Combine (fixture.Directory, "tool-started")), TimeSpan.FromSeconds (10));
		fixture.Task.Cancel ();

		Assert.IsTrue (execution.Wait (TimeSpan.FromSeconds (10)), "The cancelled task did not finish.");
		Assert.IsTrue (started, "The linker did not start.");
		Assert.IsFalse (execution.Result);
		Assert.That (fixture.Errors, Has.Count.EqualTo (1));
		Assert.IsFalse (File.Exists (output));
		Assert.AreEqual (1, fixture.Engine.YieldCount);
		Assert.AreEqual (1, fixture.Engine.ReacquireCount);
	}

	(LinkNativeAotSharedLibrary Task, MockBuildEngine Engine, List<BuildErrorEventArgs> Errors, string Directory) CreateTask (
		string name, string value, string script)
	{
		string directory = Path.Combine (Root, "temp", TestName, name);
		Directory.CreateDirectory (directory);
		Directory.CreateDirectory (Path.Combine (directory, "out"));
		string tool = Path.Combine (directory, "linker");
		File.WriteAllText (tool, script);
		if (OperatingSystem.IsWindows ()) {
			throw new PlatformNotSupportedException ("The fake linker is a shell script.");
		}
		File.SetUnixFileMode (tool, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
		var errors = new List<BuildErrorEventArgs> ();
		var engine = new MockBuildEngine (TextWriter.Null, errors);
		var task = new LinkNativeAotSharedLibrary {
			BuildEngine = engine,
			TaskEnvironment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment (directory, new Dictionary<string, string> { ["TASK_VALUE"] = value }),
			LinkerToolPath = tool,
			ObjcopyToolPath = tool,
			IntermediateOutputPath = "obj",
			NativeObject = "input.o",
			OutputSharedLibrary = Path.Combine ("out", "libapp.so"),
			Abi = "arm64-v8a",
			DebugBuild = true,
			YieldDuringToolExecution = true,
		};
		return (task, engine, errors, directory);
	}

	static void AssertProjectResult (
		(LinkNativeAotSharedLibrary Task, MockBuildEngine Engine, List<BuildErrorEventArgs> Errors, string Directory) fixture, string value)
	{
		Assert.IsEmpty (fixture.Errors);
		Assert.That (File.ReadAllLines (Path.Combine (fixture.Directory, "tool-environment")), Is.EqualTo (new [] { fixture.Directory, value }));
		Assert.AreEqual ("linked", File.ReadAllText (Path.Combine (fixture.Directory, "out", "libapp.so")));
		Assert.AreEqual (1, fixture.Engine.YieldCount);
		Assert.AreEqual (1, fixture.Engine.ReacquireCount);
	}
}
