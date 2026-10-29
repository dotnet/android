using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using Xamarin.Android.Tasks;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests.Tasks
{
	[TestFixture]
	public class MonoAndroidHelperTests : BaseTest
	{
		[TestCase (false)]
		[TestCase (true)]
		public void DetermineJavaLibrariesInSeparateProjects (bool multithreaded)
		{
			string directory = Path.Combine (Root, "temp", TestName);
			Directory.CreateDirectory (directory);
			string assembly = Path.Combine (TestEnvironment.AndroidMSBuildDirectory, "Xamarin.Android.Build.Tasks.dll");
			string nestedJar = Path.Combine ("libs", "__reference__nested.jar");
			var resources = new List<ProjectResource> ();
			foreach (string name in new [] { "first", "second" }) {
				foreach (var file in new [] {
					("runtime.jar", "aaaa"),
					("copy.jar", name == "first" ? "aaaa" : "bbbb"),
					("source.jar", "cccc"),
					("library.jar", "dddd"),
					("prefix-block.jar", "eeee"),
					(nestedJar, "ffff"),
				}) {
					resources.Add (new ProjectResource { Path = Path.Combine (name, file.Item1), Content = file.Item2 });
				}
				resources.Add (new ProjectResource {
					Path = Path.Combine (name, "jars.proj"),
					Content = $"""
						<Project>
						  <UsingTask TaskName="Xamarin.Android.Tasks.DetermineJavaLibrariesToCompile" AssemblyFile="{SecurityElement.Escape (assembly)}" />
						  <ItemGroup>
						    <Runtime Include="runtime.jar" Owner="{name}" />
						    <Source Include="copy.jar;source.jar;missing.JAR;missing.java" Owner="{name}" />
						    <Library Include="library.jar;prefix-block.jar" Owner="{name}" />
						    <ProjectJar Include="__reference__missing.jar;{nestedJar}" Owner="{name}" />
						    <External Include="external.jar;external.jar" Owner="{name}" />
						  </ItemGroup>
						  <Target Name="Build">
						    <DetermineJavaLibrariesToCompile MonoPlatformJarPaths="@(Runtime)" JavaSourceFiles="@(Source)"
						        JavaLibraries="@(Library)" LibraryProjectJars="@(ProjectJar)"
						        ExternalJavaLibraries="@(External)" DoNotPackageJavaLibraries="BLOCK.JAR">
						      <Output TaskParameter="JavaLibrariesToCompile" ItemName="_Compile" />
						      <Output TaskParameter="ReferenceJavaLibraries" ItemName="_Reference" />
						    </DetermineJavaLibrariesToCompile>
						    <WriteLinesToFile File="compile.txt" Overwrite="true" Lines="@(_Compile->'%(Identity)|%(Owner)')" />
						    <WriteLinesToFile File="reference.txt" Overwrite="true" Lines="@(_Reference->'%(Identity)|%(Owner)')" />
						  </Target>
						</Project>
						""",
				});
			}
			resources.Add (new ProjectResource {
				Path = "driver.proj",
				Content = """
					<Project>
					  <ItemGroup><Reader Include="first\jars.proj;second\jars.proj" /></ItemGroup>
					  <Target Name="Build">
					    <MSBuild Projects="@(Reader)" BuildInParallel="true" Targets="Build" />
					  </Target>
					</Project>
					""",
			});
			new XamarinAndroidApplicationProject ().UpdateProjectFiles (directory, resources, doNotCleanup: true);
			var dotnet = new DotNetCLI (Path.Combine (directory, "driver.proj")) { Verbosity = "diagnostic" };
			dotnet.EnvironmentVariables ["DOTNET_CLI_USE_MSBUILD_SERVER"] = "0";
			string [] arguments = multithreaded ?
				["--no-restore", "-m:2", "-mt", "-nr:false", "-warnaserror:MSB4286,MSB4287,MSB4288"] :
				["--no-restore", "-m:2", "-nr:false"];
			Assert.IsTrue (dotnet.Build (msbuildArguments: arguments),
				string.Join (Environment.NewLine, dotnet.LastBuildOutput) + File.ReadAllText (dotnet.ProcessLogFile));
			foreach (string name in new [] { "first", "second" }) {
				var compile = new List<string> { "runtime.jar" };
				if (name == "second")
					compile.Add ("copy.jar");
				compile.AddRange (new [] { "source.jar", "library.jar", nestedJar });
				CollectionAssert.AreEqual (compile.Select (file => $"{file}|{name}"),
					File.ReadAllLines (Path.Combine (directory, name, "compile.txt")));
				CollectionAssert.AreEqual (new [] { $"external.jar|{name}", $"external.jar|{name}", $"prefix-block.jar|{name}" },
					File.ReadAllLines (Path.Combine (directory, name, "reference.txt")));
			}
		}

		[Test]
		public void TestStringEquals_DefaultComparison ()
		{
			// Test default Ordinal comparison
			Assert.IsTrue (MonoAndroidHelper.StringEquals ("Hello", "Hello"));
			Assert.IsFalse (MonoAndroidHelper.StringEquals ("Hello", "hello"));
			Assert.IsFalse (MonoAndroidHelper.StringEquals ("WORLD", "world"));
			Assert.IsTrue (MonoAndroidHelper.StringEquals ("", ""));
		}

		[Test]
		public void TestStringEquals_OrdinalIgnoreCaseComparison ()
		{
			// Test explicit OrdinalIgnoreCase comparison
			Assert.IsTrue (MonoAndroidHelper.StringEquals ("Hello", "hello", StringComparison.OrdinalIgnoreCase));
			Assert.IsTrue (MonoAndroidHelper.StringEquals ("WORLD", "world", StringComparison.OrdinalIgnoreCase));
			Assert.IsFalse (MonoAndroidHelper.StringEquals ("Hello", "World", StringComparison.OrdinalIgnoreCase));
			Assert.IsTrue (MonoAndroidHelper.StringEquals ("", "", StringComparison.OrdinalIgnoreCase));
		}

		[Test]
		public void TestStringEquals_NullHandling ()
		{
			// Test null handling
			Assert.IsTrue (MonoAndroidHelper.StringEquals (null, null));
			Assert.IsFalse (MonoAndroidHelper.StringEquals ("test", null));
			Assert.IsFalse (MonoAndroidHelper.StringEquals (null, "test"));
			Assert.IsFalse (MonoAndroidHelper.StringEquals ("", null));
			Assert.IsFalse (MonoAndroidHelper.StringEquals (null, ""));
		}

		[Test]
		public void TestStringEquals_ReplaceStringCompareUseCases ()
		{
			// Test cases that replicate the original String.Compare use cases

			// Replaces: String.Compare("typemap", Mode, StringComparison.OrdinalIgnoreCase) == 0
			string mode = "TYPEMAP";
			Assert.IsTrue (MonoAndroidHelper.StringEquals ("typemap", mode, StringComparison.OrdinalIgnoreCase));

			// Replaces: String.Compare("environment", Mode, StringComparison.OrdinalIgnoreCase) == 0  
			mode = "Environment";
			Assert.IsTrue (MonoAndroidHelper.StringEquals ("environment", mode, StringComparison.OrdinalIgnoreCase));

			// Replaces: String.Compare(abi, item.GetMetadata(), StringComparison.Ordinal) != 0
			string abi = "arm64-v8a";
			string metadata = "x86_64";
			Assert.IsFalse (MonoAndroidHelper.StringEquals (abi, metadata));

			// Replaces: String.Compare("merge", current.LocalName, StringComparison.Ordinal) == 0
			string localName = "merge";
			Assert.IsTrue (MonoAndroidHelper.StringEquals ("merge", localName));
		}
	}
}