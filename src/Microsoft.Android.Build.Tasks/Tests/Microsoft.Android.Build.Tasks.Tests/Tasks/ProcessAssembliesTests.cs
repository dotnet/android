using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Mono.Cecil;
using NUnit.Framework;
using Microsoft.Android.Tasks;
using Xamarin.Android.Tasks;

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
			string taskAssembly = typeof (ProcessAssembliesTests).Assembly.GetCustomAttributes<AssemblyMetadataAttribute> ()
				.Single (attribute => attribute.Key == "AndroidTaskAssembly").Value
				?? throw new InvalidOperationException ("Missing task assembly path.");
			taskAssembly = SecurityElement.Escape (taskAssembly);
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
				File.WriteAllText (Path.Combine (root, "process.proj"), $"""
						<Project>
						  <UsingTask TaskName="Microsoft.Android.Tasks.ProcessAssemblies" AssemblyFile="{taskAssembly}" Runtime="NET" />
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
						""");
			}
			File.WriteAllText (Path.Combine (directory, "driver.proj"), """
					<Project>
					  <ItemGroup><Reader Include="first\process.proj;second\process.proj" /></ItemGroup>
					  <Target Name="Build">
					    <MSBuild Projects="@(Reader)" BuildInParallel="true" Targets="Build" />
					  </Target>
					</Project>
					""");
			var start = new ProcessStartInfo (typeof (ProcessAssembliesTests).Assembly.GetCustomAttributes<AssemblyMetadataAttribute> ()
				.Single (attribute => attribute.Key == "DotNetHostPath").Value
				?? throw new InvalidOperationException ("Missing .NET host path.")) {
				WorkingDirectory = directory,
				UseShellExecute = false,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
			};
			start.Environment ["DOTNET_CLI_USE_MSBUILD_SERVER"] = "0";
			start.Environment ["MSBUILDDISABLENODEREUSE"] = "1";
			var arguments = new List<string> { "msbuild", "driver.proj", "-t:Build", "-v:diagnostic", "-m:2", "-nr:false", "-warnaserror:MSB4286,MSB4287,MSB4288" };
			if (multithreaded)
				arguments.Add ("-mt");
			foreach (var argument in arguments)
				start.ArgumentList.Add (argument);
			var result = NativeToolTestHelper.Capture (start, TimeSpan.FromMinutes (2));
			var output = result.StandardOutput + result.StandardError;
			Assert.AreEqual (0, result.ExitCode, output);
			foreach (string name in new [] { "first", "second" }) {
				string root = Path.Combine (directory, name);
				bool android = name == "first";
				CollectionAssert.AreEqual (new [] {
					$"library.dll|{name}-first|{android}",
					$"library.dll|{name}-second|{android}",
				}, File.ReadAllLines (Path.Combine (root, "assemblies.txt")));
				CollectionAssert.AreEqual (android ? new [] { "extra.pdb", "library.pdb" } : [],
					File.ReadAllLines (Path.Combine (root, "symbols.txt")));
				Assert.IsTrue (output.Contains ("XAPRAS7028") && output.Contains (Path.Combine (root, "missing.dll")),
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
		public void DuplicateJavaLibrariesPreserveFirst ()
		{
			var first = new TaskItem ("library.jar");
			first.SetMetadata ("Owner", "first");
			first.SetMetadata ("NuGetPackageId", "Microsoft.Android.Runtime.android-arm64");
			var second = new TaskItem ("LIBRARY.jar");
			second.SetMetadata ("Owner", "second");
			second.SetMetadata ("NuGetPackageId", "Microsoft.Android.Runtime.android-arm64");
			var task = new ProcessAssemblies {
				BuildEngine = new MockBuildEngine (TestContext.Out),
				TaskEnvironment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment (directory, new Dictionary<string, string> ()),
				InputJavaLibraries = [first, second],
			};

			Assert.IsTrue (task.Execute ());
			CollectionAssert.AreEqual (new [] { first }, task.OutputJavaLibraries);
		}

		[Test]
		public void ReferenceMetadataDoesNotRequireFile ()
		{
			var item = new TaskItem ("missing.dll");
			item.SetMetadata ("HasMonoAndroidReference", "true");
			var environment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment (directory, new Dictionary<string, string> ());
			Assert.IsTrue (MonoAndroidHelper.HasMonoAndroidReference (item, environment));
		}

		[TestCase ("Mono.Android")]
		[TestCase ("Java.Interop")]
		[TestCase ("Mono.Android.dll")]
		[TestCase ("Java.Interop.dll")]
		public void RecognizesFrameworkAssemblyNamesAndFiles (string assembly)
		{
			Assert.IsTrue (assembly.EndsWith (".dll", StringComparison.OrdinalIgnoreCase)
				? MonoAndroidHelper.IsFrameworkAssembly (assembly)
				: MonoAndroidHelper.IsFrameworkAssemblyName (assembly));
		}
	}
}
