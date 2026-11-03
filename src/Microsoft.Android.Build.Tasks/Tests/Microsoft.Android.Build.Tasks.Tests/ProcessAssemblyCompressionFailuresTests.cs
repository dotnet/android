#nullable enable
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Android.Build.Tasks;
using Microsoft.Android.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NUnit.Framework;

namespace Xamarin.Android.Build.Tests.Tasks;

[TestFixture]
public class ProcessAssemblyCompressionFailuresTests : BaseTest
{
	[TestCase (false)]
	[TestCase (true)]
	public void OutputsPreserveInputArraysAndItems (bool empty)
	{
		ITaskItem [] framework = empty ? [] : [CreateAssembly ("Framework.dll")];
		ITaskItem [] user = empty ? [] : [CreateAssembly ("User.dll")];
		var task = CreateTask (framework, user);

		Assert.IsTrue (task.Execute ());
		Assert.AreSame (framework, task.ResolvedFrameworkAssembliesOutput);
		Assert.AreSame (user, task.ResolvedUserAssembliesOutput);
		if (!empty) {
			Assert.AreSame (framework [0], task.ResolvedFrameworkAssembliesOutput [0]);
			Assert.AreSame (user [0], task.ResolvedUserAssembliesOutput [0]);
			Assert.AreEqual ("true", framework [0].GetMetadata ("CompressedAssembly"));
			Assert.AreEqual ("true", user [0].GetMetadata ("CompressedAssembly"));
		}
	}

	[Test]
	public void RemovesOnlyFailedAssemblyMetadataFromOriginalItems ()
	{
		ITaskItem [] framework = [CreateAssembly ("Framework.dll"), CreateAssembly ("OtherFramework.dll")];
		ITaskItem [] user = [CreateAssembly ("User.dll"), CreateAssembly ("OtherUser.dll")];
		var task = CreateTask (framework, user);
		task.FailedToCompressAssembliesOutput = [new TaskItem ("Framework.dll"), new TaskItem ("User.dll")];

		Assert.IsTrue (task.Execute ());
		Assert.AreSame (framework, task.ResolvedFrameworkAssembliesOutput);
		Assert.AreSame (user, task.ResolvedUserAssembliesOutput);
		foreach (var items in new [] { framework, user }) {
			Assert.AreEqual ("", items [0].GetMetadata ("CompressedAssembly"));
			Assert.AreEqual ("true", items [1].GetMetadata ("CompressedAssembly"));
			foreach (var item in items) {
				Assert.AreEqual ("arm64-v8a", item.GetMetadata ("Abi"));
			}
		}
	}

	[Test]
	public void RepeatedFailureForSameAssemblyIsAllowed ()
	{
		var assembly = CreateAssembly ("User.dll");
		var task = CreateTask ([], [assembly]);
		task.FailedToCompressAssembliesOutput = [new TaskItem ("User.dll"), new TaskItem ("User.dll")];

		Assert.IsTrue (task.Execute ());
		Assert.AreEqual ("", assembly.GetMetadata ("CompressedAssembly"));
	}

	[Test]
	public void UnmatchedFailureDoesNotRollBackEarlierMetadataRemoval ()
	{
		var assembly = CreateAssembly ("User.dll");
		var task = CreateTask ([], [assembly]);
		task.FailedToCompressAssembliesOutput = [new TaskItem ("User.dll"), new TaskItem ("Missing.dll")];

		Assert.IsFalse (task.Execute ());
		Assert.AreSame (task.ResolvedUserAssemblies, task.ResolvedUserAssembliesOutput);
		Assert.AreEqual ("", assembly.GetMetadata ("CompressedAssembly"));
	}

	[TestCase ("Missing.dll")]
	[TestCase ("user.dll")]
	public void UnmatchedFailureStillFails (string itemSpec)
	{
		var assembly = CreateAssembly ("User.dll");
		var task = CreateTask ([], [assembly]);
		task.FailedToCompressAssembliesOutput = [new TaskItem (itemSpec)];

		Assert.Throws<InvalidOperationException> (() => task.RunTask ());
		Assert.AreSame (task.ResolvedUserAssemblies, task.ResolvedUserAssembliesOutput);
		Assert.AreEqual ("true", assembly.GetMetadata ("CompressedAssembly"));
		Assert.IsFalse (task.Execute ());
	}

	[TestCase (false)]
	[TestCase (true)]
	public void DuplicateAssemblyMatchesStillFail (bool acrossGroups)
	{
		var first = CreateAssembly ("Duplicate.dll");
		var second = CreateAssembly ("Duplicate.dll");
		var task = acrossGroups ? CreateTask ([first], [second]) : CreateTask ([], [first, second]);
		task.FailedToCompressAssembliesOutput = [new TaskItem ("Duplicate.dll")];

		Assert.Throws<InvalidOperationException> (() => task.RunTask ());
		Assert.AreEqual ("true", first.GetMetadata ("CompressedAssembly"));
		Assert.AreEqual ("true", second.GetMetadata ("CompressedAssembly"));
		Assert.IsFalse (task.Execute ());
	}

	[Test]
	public void SupportsMultithreadedExecution ()
	{
		Assert.IsTrue (Attribute.IsDefined (typeof (ProcessAssemblyCompressionFailures), typeof (MSBuildMultiThreadableTaskAttribute), inherit: false));
		Assert.IsInstanceOf<MultithreadedAndroidTask> (CreateTask ([], []));
	}

	[TestCase (false, false)]
	[TestCase (false, true)]
	[TestCase (true, false)]
	[TestCase (true, true)]
	public void ProductionTargetUsesModernTask (bool failures, bool multithreaded)
	{
		var repository = new DirectoryInfo (AppContext.BaseDirectory);
		while (repository != null && !File.Exists (Path.Combine (repository.FullName, "Configuration.props"))) {
			repository = repository.Parent;
		}
		var source = XDocument.Load (Path.Combine (
			repository?.FullName ?? throw new InvalidOperationException ("Could not locate the repository."),
			"src", "Xamarin.Android.Build.Tasks", "Xamarin.Android.Common.targets"));
		var registrations = source.Descendants ()
			.Where (element => element.Name.LocalName == "UsingTask" &&
				((string?)element.Attribute ("TaskName"))?.EndsWith (".ProcessAssemblyCompressionFailures", StringComparison.Ordinal) == true)
			.Select (element => new XElement (element))
			.ToArray ();
		Assert.AreEqual (2, registrations.Length);
		foreach (var registration in registrations) {
			Assert.AreEqual ("Microsoft.Android.Tasks.ProcessAssemblyCompressionFailures", (string?)registration.Attribute ("TaskName"));
			Assert.AreEqual ("$(_MicrosoftAndroidBuildTasksAssembly)", (string?)registration.Attribute ("AssemblyFile"));
			Assert.AreEqual ("NET", (string?)registration.Attribute ("Runtime"));
		}
		var full = registrations.Single (element => (string?)element.Attribute ("Condition") == " '$(MSBuildRuntimeType)' == 'Full' ");
		Assert.AreEqual ("TaskHostFactory", (string?)full.Attribute ("TaskFactory"));
		var core = registrations.Single (element => (string?)element.Attribute ("Condition") == " '$(MSBuildRuntimeType)' == 'Core' ");
		Assert.IsNull (core.Attribute ("TaskFactory"));
		var target = new XElement (source.Descendants ().Single (element =>
			element.Name.LocalName == "Target" && (string?)element.Attribute ("Name") == "_ProcessAssemblyCompressionFailures"));
		foreach (var element in registrations.SelectMany (element => element.DescendantsAndSelf ()).Concat (target.DescendantsAndSelf ())) {
			element.Name = element.Name.LocalName;
		}
		var directory = Path.Combine (Root, "temp", TestName);
		Directory.CreateDirectory (directory);
		var project = Path.Combine (directory, "compression.proj");
		new XDocument (new XElement ("Project",
			new XElement ("PropertyGroup",
				new XElement ("_MicrosoftAndroidBuildTasksAssembly", typeof (ProcessAssemblyCompressionFailures).Assembly.Location),
				new XElement ("EmbedAssembliesIntoApk", "True")),
			registrations,
			new XElement ("ItemGroup",
				new XElement ("_CompressedResolvedFrameworkAssemblies", new XAttribute ("Include", "Framework.dll"), new XElement ("CompressedAssembly", "true")),
				new XElement ("_CompressedBuildApkResolvedUserAssemblies", new XAttribute ("Include", "User.dll"), new XElement ("CompressedAssembly", "true")),
				failures ? new XElement ("_FailedToCompressAssemblies", new XAttribute ("Include", "User.dll")) : null),
			new XElement ("Target", new XAttribute ("Name", "_CompressAssemblies")),
			target,
			new XElement ("Target", new XAttribute ("Name", "Build"), new XAttribute ("DependsOnTargets", "_ProcessAssemblyCompressionFailures"),
				new XElement ("Error", new XAttribute ("Condition", "'@(_BuildApkResolvedFrameworkAssemblies)' != 'Framework.dll'"), new XAttribute ("Text", "Framework output was lost.")),
				new XElement ("Error", new XAttribute ("Condition", "'@(_BuildApkResolvedUserAssemblies)' != 'User.dll'"), new XAttribute ("Text", "User output was lost.")),
				new XElement ("Error", new XAttribute ("Condition", "'%(_BuildApkResolvedFrameworkAssemblies.CompressedAssembly)' != 'true'"), new XAttribute ("Text", "Framework metadata was changed.")),
				new XElement ("Error", new XAttribute ("Condition", $"'%(_BuildApkResolvedUserAssemblies.CompressedAssembly)' != '{(failures ? "" : "true")}'"), new XAttribute ("Text", "User metadata was not preserved.")))))
			.Save (project);
		var start = new ProcessStartInfo ("dotnet") {
			WorkingDirectory = directory,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
		};
		start.Environment ["DOTNET_CLI_USE_MSBUILD_SERVER"] = "0";
		foreach (var argument in new [] { "msbuild", project, "-t:Build", "-nologo", "-v:minimal", "-nr:false" }) {
			start.ArgumentList.Add (argument);
		}
		if (multithreaded) {
			foreach (var argument in new [] { "-m", "-mt", "-warnaserror:MSB4286,MSB4287,MSB4288" }) {
				start.ArgumentList.Add (argument);
			}
		}
		using var process = Process.Start (start) ?? throw new InvalidOperationException ("Could not start MSBuild.");
		var stdout = process.StandardOutput.ReadToEndAsync ();
		var stderr = process.StandardError.ReadToEndAsync ();
		bool completed = process.WaitForExit (120000);
		if (!completed) {
			process.Kill (entireProcessTree: true);
			process.WaitForExit ();
		}
		var output = stdout.GetAwaiter ().GetResult () + stderr.GetAwaiter ().GetResult ();
		Assert.IsTrue (completed, "MSBuild did not finish." + Environment.NewLine + output);
		Assert.AreEqual (0, process.ExitCode, output);
	}

	static ProcessAssemblyCompressionFailures CreateTask (ITaskItem [] framework, ITaskItem [] user) => new () {
		BuildEngine = new MockBuildEngine (TestContext.Out),
		ResolvedFrameworkAssemblies = framework,
		ResolvedUserAssemblies = user,
	};

	static TaskItem CreateAssembly (string itemSpec)
	{
		var item = new TaskItem (itemSpec);
		item.SetMetadata ("CompressedAssembly", "true");
		item.SetMetadata ("Abi", "arm64-v8a");
		return item;
	}
}
