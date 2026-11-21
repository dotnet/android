using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Xamarin.Android.Tasks;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests
{
	[TestClass]
	[DoNotParallelize]
	public class CollectNonEmptyDirectoriesTests : BaseTest
	{
		[TestMethod]
		public void RelativeDirectoriesUseProjectDirectory ()
		{
			var path = Path.Combine (Root, "temp", TestName);
			TestOutputDirectories [TestId] = path;
			Directory.CreateDirectory (path);
			var assembly = SecurityElement.Escape (Path.Combine (TestEnvironment.AndroidMSBuildDirectory, "Xamarin.Android.Build.Tasks.dll"));
			var resources = new List<ProjectResource> ();
			foreach (var name in new [] { "first", "second" }) {
				resources.Add (new ProjectResource { Path = Path.Combine (name, "res", "values", $"{name}.xml"), Content = "" });
				resources.Add (new ProjectResource {
					Path = Path.Combine (name, "collect.proj"),
					Content = $"""
						<Project>
						  <UsingTask TaskName="Xamarin.Android.Tasks.CollectNonEmptyDirectories" AssemblyFile="{assembly}" />
						  <ItemGroup>
						    <Resource Include="res" />
						    <Resource Include="lp\0\res" AndroidSkipResourceProcessing="true" StampFile="archive.stamp" />
						  </ItemGroup>
						  <Target Name="Build">
						    <MakeDir Directories="lp\0\res;stamps" />
						    <CollectNonEmptyDirectories Directories="@(Resource)" LibraryProjectIntermediatePath="lp" StampDirectory="stamps">
						      <Output TaskParameter="Output" ItemName="_Directories" />
						      <Output TaskParameter="LibraryResourceFiles" ItemName="_Files" />
						    </CollectNonEmptyDirectories>
						    <WriteLinesToFile File="directories.txt" Overwrite="true" Lines="@(_Directories)" />
						    <WriteLinesToFile File="files.txt" Overwrite="true" Lines="@(_Files)" />
						  </Target>
						</Project>
						""",
				});
			}
			resources.Add (new ProjectResource {
				Path = "driver.proj",
				Content = """
					<Project>
					  <Target Name="Build">
					    <MSBuild Projects="first\collect.proj;second\collect.proj" Targets="Build" BuildInParallel="true" />
					  </Target>
					</Project>
					""",
			});
			new XamarinAndroidApplicationProject ().UpdateProjectFiles (path, resources, doNotCleanup: true);
			var dotnet = new DotNetCLI (Path.Combine (path, "driver.proj")) { Verbosity = "diagnostic" };
			dotnet.EnvironmentVariables ["DOTNET_CLI_USE_MSBUILD_SERVER"] = "0";
			Assert.IsTrue (dotnet.Build (msbuildArguments:
				["--no-restore", "-m:2", "-mt", "-nr:false", "-warnaserror:MSB4286,MSB4287,MSB4288"]), File.ReadAllText (dotnet.ProcessLogFile));
			var buildLog = File.ReadAllText (dotnet.BuildLogFile);
			Assert.Contains ("Task \"CollectNonEmptyDirectories\"", buildLog);
			Assert.DoesNotContain ("Task \"CollectNonEmptyDirectories\" ran in TaskHost", buildLog);
			foreach (var name in new [] { "first", "second" }) {
				var projectDirectory = Path.Combine (path, name);
				string cache = Path.Combine ("res", "..", $"{Files.HashString ("res")}-files.cache");
				string file = Path.Combine ("res", "values", $"{name}.xml");
				string archiveDirectory = Path.Combine ("lp", "0", "res");
				CollectionAssert.AreEqual (new [] { "res", archiveDirectory }, File.ReadAllLines (Path.Combine (projectDirectory, "directories.txt")));
				CollectionAssert.AreEqual (new [] { file, "archive.stamp" }, File.ReadAllLines (Path.Combine (projectDirectory, "files.txt")));
				CollectionAssert.AreEqual (new [] { file }, File.ReadAllLines (Path.Combine (projectDirectory, cache)));
				CollectionAssert.AreEqual (new [] { "archive.stamp" }, File.ReadAllLines (Path.Combine (projectDirectory, "lp", "0", "files.cache")));
			}
		}

		[TestMethod]
		[DataRow (false)]
		[DataRow (true)]
		public void RelativeCacheUsesStampTimestamp (bool stale)
		{
			var path = Path.Combine (Root, "temp", TestName);
			TestOutputDirectories [TestId] = path;
			Directory.CreateDirectory (Path.Combine (path, "res", "values"));
			string resourceDirectory = Path.Combine ("res", "..", "res") + Path.DirectorySeparatorChar + Path.DirectorySeparatorChar;
			string actual = Path.Combine (resourceDirectory, "values", "actual.xml");
			string cached = stale ? actual : Path.Combine ("res", "values", "cached.xml");
			File.WriteAllText (Path.Combine (path, actual), "");
			File.WriteAllLines (Path.Combine (path, "custom.cache"), new [] { cached }, System.Text.Encoding.UTF8);
			File.WriteAllText (Path.Combine (path, "custom.stamp"), "");
			var timestamp = DateTime.UtcNow.AddMinutes (-2);
			File.SetLastWriteTimeUtc (Path.Combine (path, "custom.stamp"), timestamp);
			File.SetLastWriteTimeUtc (Path.Combine (path, "custom.cache"), stale ? timestamp.AddMinutes (-1) : timestamp);
			var input = new TaskItem (resourceDirectory, new Dictionary<string, string> {
				{ "StampFile", "custom.stamp" },
				{ "FilesCache", "custom.cache" },
				{ "Custom", "preserved" },
			});
			var task = new CollectNonEmptyDirectories {
				BuildEngine = new MockBuildEngine (Console.Out),
				TaskEnvironment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment (path, new Dictionary<string, string> ()),
				Directories = [input],
				LibraryProjectIntermediatePath = "lp",
				StampDirectory = "stamps",
			};
			Assert.IsTrue (task.Execute ());
			var output = task.Output.Single ();
			var resource = task.LibraryResourceFiles.Single ();
			Assert.AreEqual (cached, resource.ItemSpec);
			Assert.AreEqual (resourceDirectory, output.ItemSpec);
			Assert.AreEqual (cached, output.GetMetadata ("FileFound"));
			Assert.AreEqual ("preserved", output.GetMetadata ("Custom"));
			Assert.AreEqual ("custom.stamp", output.GetMetadata ("StampFile"));
			Assert.AreEqual ("custom.cache", output.GetMetadata ("FilesCache"));
			Assert.AreEqual (resourceDirectory, resource.GetMetadata ("ResourceDirectory"));
			Assert.AreEqual (cached, resource.GetMetadata ("StampFile"));
			Assert.AreEqual ("custom.cache", resource.GetMetadata ("FilesCache"));
			Assert.AreEqual ("custom.stamp", resource.GetMetadata ("Hash"));
			Assert.AreEqual (Path.Combine (resourceDirectory, "..", "flat") + Path.DirectorySeparatorChar, resource.GetMetadata ("_ArchiveDirectory"));
			Assert.AreEqual (stale ? "values_actual.arsc.flat" : "values_cached.arsc.flat", resource.GetMetadata ("_FlatFile"));
			Assert.AreEqual ("", input.GetMetadata ("FileFound"), "Input metadata must not change.");
			var cacheTimestamp = File.GetLastWriteTimeUtc (Path.Combine (path, "custom.cache"));
			if (stale)
				Assert.IsGreaterThan (timestamp, cacheTimestamp, "A stale cache must be refreshed even if its contents did not change.");
			else
				Assert.AreEqual (File.GetLastWriteTimeUtc (Path.Combine (path, "custom.stamp")), cacheTimestamp, "A valid cache must not be rewritten.");
		}

		[TestMethod]
		public void CollectNonEmptyDirectoriesTest ()
		{
			var path = Path.Combine (Root, "temp", TestName);
			TestOutputDirectories [TestId] = path;
			var resPath = Path.Combine (path, "res");
			var resizerPath = Path.Combine (path, "resizer");
			Directory.CreateDirectory (Path.Combine (path, "stamps"));
			Directory.CreateDirectory (Path.Combine (resPath, "values"));
			Directory.CreateDirectory (Path.Combine (resPath, "layout"));
			Directory.CreateDirectory (Path.Combine (resizerPath, "drawable"));
			Directory.CreateDirectory (Path.Combine (path, "empty"));
			File.WriteAllText (Path.Combine (resPath, "values", "strings.xml"), "");
			File.WriteAllText (Path.Combine (resPath, "values", ".ignored"), "");
			File.WriteAllText (Path.Combine (resPath, "layout", "main.xml"), "");
			File.WriteAllText (Path.Combine (resizerPath, "drawable", "icon.xml"), "");
			var libPath = Path.Combine (path, "lp");
			Directory.CreateDirectory (Path.Combine (libPath, "0", "res", "values"));
			Directory.CreateDirectory (Path.Combine (libPath, "1", "res", "values"));
			File.WriteAllText (Path.Combine (libPath, "0", "res", "values", "strings.xml"), "");
			File.WriteAllText (Path.Combine (libPath, "1", "res", "values", "strings.xml"), "");
			var errors = new List<BuildErrorEventArgs> ();
			IBuildEngine engine = new MockBuildEngine (Console.Out, errors);
			var task = new CollectNonEmptyDirectories {
				BuildEngine = engine,
				Directories = new ITaskItem[] {
					new TaskItem (resPath, new Dictionary<string, string> {
						{ "FilesCache", Path.Combine(path, "files.cache") },
					}),
					new TaskItem (resizerPath),
					new TaskItem (Path.Combine (libPath, "0", "res"), new Dictionary<string, string> {
						{ "AndroidSkipResourceProcessing", "True" },
						{ "StampFile", "0.stamp" },
					}),
					new TaskItem (Path.Combine (libPath, "1", "res")),
					new TaskItem (Path.Combine (path, "empty")),
					new TaskItem (Path.Combine (path, "missing")),
				},
				LibraryProjectIntermediatePath = libPath,
				StampDirectory = Path.Combine(path, "stamps"),
			};
			Assert.IsTrue (task.Execute (), $"task should have succeeded. {string.Join (";", errors.Select (x => x.Message))}");
			Assert.AreEqual (4, task.Output.Length, "Output should have 4 items in it.");
			Assert.AreEqual (5, task.LibraryResourceFiles.Length, "Output should have 5 items in it.");
			Assert.AreEqual ("layout_main.xml.flat", task.LibraryResourceFiles[0].GetMetadata ("_FlatFile"));
			Assert.AreEqual ("values_strings.arsc.flat", task.LibraryResourceFiles[1].GetMetadata ("_FlatFile"));
			Assert.AreEqual ("0.flata", task.LibraryResourceFiles[3].GetMetadata ("_FlatFile"));
			Assert.AreEqual ("values_strings.arsc.flat", task.LibraryResourceFiles[4].GetMetadata ("_FlatFile"));
			var archive = task.LibraryResourceFiles [3];
			Assert.AreEqual ("0.stamp", archive.ItemSpec);
			Assert.AreEqual ("0.stamp", archive.GetMetadata ("StampFile"));
			Assert.AreEqual (Path.Combine (path, "stamps", $"{Files.HashString (resPath)}.stamp"), task.Output [0].GetMetadata ("StampFile"));
			foreach (var item in task.Output) {
				Assert.IsNotNull (item.GetMetadata ("FilesCache"), "FilesCache should have been set");
				var cacheFile = item.GetMetadata ("FilesCache");
				Assert.IsTrue (File.Exists (cacheFile), $"{cacheFile} should have been created.");
			}
		}
	}
}
