using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Mono.Cecil;
using NUnit.Framework;
using Xamarin.Android.Tasks;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	public class ProcessAssembliesTests
	{
		string directory = "";

		[SetUp]
		public void SetUp ()
		{
			directory = Path.Combine (TestContext.CurrentContext.WorkDirectory, "ProcessAssemblies-" + Guid.NewGuid ());
			Directory.CreateDirectory (directory);
		}

		[TearDown]
		public void TearDown ()
		{
			Directory.Delete (directory, recursive: true);
		}

		[TestCase (false)]
		[TestCase (true)]
		public void RelativeReadsPreserveItems (bool multithreaded)
		{
			string taskAssembly = SecurityElement.Escape (Path.Combine (TestEnvironment.AndroidMSBuildDirectory, "Xamarin.Android.Build.Tasks.dll"));
			var resources = new List<ProjectResource> ();
			foreach (string name in new [] { "first", "second" }) {
				bool android = name == "first";
				string root = Path.Combine (directory, name);
				Directory.CreateDirectory (root);
				using var assembly = AssemblyDefinition.CreateAssembly (
					new AssemblyNameDefinition ("Library", new Version (1, 0)), "Library", ModuleKind.Dll);
				if (android) {
					assembly.MainModule.AssemblyReferences.Add (new AssemblyNameReference ("Mono.Android", new Version (1, 0)));
					File.WriteAllText (Path.Combine (root, "library.pdb"), "symbols");
					File.WriteAllText (Path.Combine (root, "extra.pdb"), "extra");
				}
				assembly.Write (Path.Combine (root, "library.dll"));
				resources.Add (new ProjectResource {
					Path = Path.Combine (name, "process.proj"),
					Content = $"""
						<Project>
						  <UsingTask TaskName="Xamarin.Android.Tasks.ProcessAssemblies" AssemblyFile="{taskAssembly}" />
						  <ItemGroup>
						    <Input Include="library.dll" RuntimeIdentifier="android-arm64" Owner="{name}-first" />
						    <Input Include="library.dll" RuntimeIdentifier="android-arm64" Owner="{name}-second" />
						    <Symbol Include="extra.pdb" />
						    <Symbol Include="missing.pdb" />
						    <Missing Include="missing.dll" RuntimeIdentifier="android-arm64" />
						  </ItemGroup>
						  <Target Name="Build">
						    <ProcessAssemblies RuntimeIdentifiers="android-arm64" InputAssemblies="@(Input)" ResolvedSymbols="@(Symbol)">
						      <Output TaskParameter="OutputAssemblies" ItemName="_Assemblies" />
						      <Output TaskParameter="ResolvedSymbols" ItemName="_Symbols" />
						    </ProcessAssemblies>
						    <WriteLinesToFile File="assemblies.txt" Overwrite="true"
						        Lines="@(_Assemblies->'%(Identity)|%(Owner)|%(HasMonoAndroidReference)')" />
						    <WriteLinesToFile File="symbols.txt" Overwrite="true" Lines="@(_Symbols)" />
						    <ProcessAssemblies RuntimeIdentifiers="android-arm64" InputAssemblies="@(Missing)" ContinueOnError="WarnAndContinue" />
						  </Target>
						</Project>
						""",
				});
			}
			resources.Add (new ProjectResource {
				Path = "driver.proj",
				Content = """
					<Project>
					  <ItemGroup><Reader Include="first\process.proj;second\process.proj" /></ItemGroup>
					  <Target Name="Build">
					    <MSBuild Projects="@(Reader)" BuildInParallel="true" Targets="Build" />
					  </Target>
					</Project>
					""",
			});
			new XamarinAndroidApplicationProject ().UpdateProjectFiles (directory, resources, doNotCleanup: true);
			var dotnet = new DotNetCLI (Path.Combine (directory, "driver.proj")) { Verbosity = "diagnostic" };
			dotnet.EnvironmentVariables ["DOTNET_CLI_USE_MSBUILD_SERVER"] = "0";
			dotnet.EnvironmentVariables ["MSBUILDDISABLENODEREUSE"] = "1";
			var arguments = new List<string> { "--no-restore", "-m:2", "-nr:false", "-warnaserror:MSB4286,MSB4287,MSB4288" };
			if (multithreaded)
				arguments.Add ("-mt");
			Assert.IsTrue (dotnet.Build (msbuildArguments: arguments.ToArray ()), File.ReadAllText (dotnet.ProcessLogFile));
			foreach (string name in new [] { "first", "second" }) {
				string root = Path.Combine (directory, name);
				bool android = name == "first";
				CollectionAssert.AreEqual (new [] {
					$"library.dll|{name}-first|{android}",
					$"library.dll|{name}-second|{android}",
				}, File.ReadAllLines (Path.Combine (root, "assemblies.txt")));
				CollectionAssert.AreEqual (android ? new [] { "extra.pdb", "library.pdb" } : [],
					File.ReadAllLines (Path.Combine (root, "symbols.txt")));
				Assert.IsTrue (dotnet.LastBuildOutput.Any (line =>
					line.Contains ("XAPRAS7028") && line.Contains (Path.Combine (root, "missing.dll"))),
					$"Missing file diagnostic for {name}");
			}
		}

		[Test]
		public void DesignTimeSkipsMissingAssembly ()
		{
			File.WriteAllText (Path.Combine (directory, "library.dll"), "not an assembly");
			var present = new TaskItem ("library.dll");
			present.SetMetadata ("RuntimeIdentifier", "android-arm64");
			var task = new ProcessAssemblies {
				BuildEngine = new MockBuildEngine (TestContext.Out),
				TaskEnvironment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment (directory, new Dictionary<string, string> ()),
				DesignTimeBuild = true,
				InputAssemblies = [new TaskItem ("missing.dll"), present],
			};
			Assert.IsTrue (task.Execute ());
			CollectionAssert.AreEqual (new [] { present }, task.OutputAssemblies);
			Assert.AreEqual ("", present.GetMetadata ("HasMonoAndroidReference"));
		}

		[Test]
		public void ReferenceMetadataDoesNotRequireFile ()
		{
			var item = new TaskItem ("missing.dll");
			item.SetMetadata ("HasMonoAndroidReference", "true");
			var environment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment (directory, new Dictionary<string, string> ());
			Assert.IsTrue (MonoAndroidHelper.HasMonoAndroidReference (item, environment));
		}
	}
}
