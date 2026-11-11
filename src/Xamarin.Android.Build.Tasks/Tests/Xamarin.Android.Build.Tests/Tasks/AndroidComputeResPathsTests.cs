using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Xamarin.Android.Tasks;
using Xamarin.ProjectTools;
using Microsoft.Android.Build.Tasks;

namespace Xamarin.Android.Build.Tests {
	[TestClass]
	[DoNotParallelize]
	public class AndroidComputeResPathsTests : BaseTest {

		[TestMethod]
		[DataRow (false)]
		[DataRow (true)]
		public void RelativeResourcePathsUseProjectDirectories (bool multithreaded)
		{
			var directory = Path.Combine (Root, "temp", TestName);
			TestOutputDirectories [TestId] = directory;
			Directory.CreateDirectory (directory);
			var assembly = SecurityElement.Escape (Path.Combine (TestEnvironment.AndroidMSBuildDirectory, "Xamarin.Android.Build.Tasks.dll"));
			var resources = new List<ProjectResource> ();
			foreach (var name in new [] { "first", "second" }) {
				resources.Add (new ProjectResource { Path = Path.Combine (name, "Resources", "Raw", "Source.xml"), Content = "" });
				resources.Add (new ProjectResource {
					Path = Path.Combine (name, "paths.proj"),
					Content = $"""
						<Project>
						  <UsingTask TaskName="Xamarin.Android.Tasks.AndroidComputeResPaths" AssemblyFile="{assembly}" />
						  <ItemGroup>
						    <Resource Include="Resources\Raw\Source.xml" />
						    <Resource Include="Resources\Raw\Source.xml" Link="{Path.Combine ("Resources", "Layout", $"{name}.axml")}" />
						    <Resource Include="Resources\Raw" />
						  </ItemGroup>
						  <Target Name="Build">
						    <AndroidComputeResPaths ResourceFiles="@(Resource)" ProjectDir="." IntermediateDir="{Path.Combine ("obj", "res")}"
						        Prefixes="Resources" PrefixProperty="MonoAndroidResourcePrefix" LowercaseFilenames="true">
						      <Output TaskParameter="IntermediateFiles" ItemName="_Intermediate" />
						      <Output TaskParameter="ResourceCaseMap" ItemName="_Map" />
						    </AndroidComputeResPaths>
						    <WriteLinesToFile File="paths.txt" Overwrite="true" Lines="@(_Intermediate)" />
						    <WriteLinesToFile File="map.txt" Overwrite="true" Lines="@(_Map->'%(Identity)|%(OriginalName)')" />
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
					    <MSBuild Projects="first\paths.proj;second\paths.proj" Targets="Build" BuildInParallel="true" />
					  </Target>
					</Project>
					""",
			});
			new XamarinAndroidApplicationProject ().UpdateProjectFiles (directory, resources, doNotCleanup: true);
			var dotnet = new DotNetCLI (Path.Combine (directory, "driver.proj")) { Verbosity = "diagnostic" };
			dotnet.EnvironmentVariables ["DOTNET_CLI_USE_MSBUILD_SERVER"] = "0";
			Assert.IsTrue (dotnet.Build (msbuildArguments: multithreaded ?
				["--no-restore", "-m:2", "-mt", "-nr:false", "-warnaserror:MSB4286,MSB4287,MSB4288"] :
				["--no-restore", "-m:2", "-nr:false"]), File.ReadAllText (dotnet.ProcessLogFile));
			foreach (var name in new [] { "first", "second" }) {
				var root = Path.Combine (directory, name);
				var output = Path.Combine (root, "obj", "res");
				var linked = Path.Combine ("Layout", $"{name}.axml");
				CollectionAssert.AreEqual (new [] {
					Path.Combine (output, "raw", "source.xml"),
					Path.Combine (output, "layout", $"{name}.xml"),
				}, File.ReadAllLines (Path.Combine (root, "paths.txt")));
				CollectionAssert.AreEqual (new [] {
					$"{Path.Combine ("raw", "source.xml")}|{Path.Combine ("Raw", "Source.xml")}",
					$"{Path.Combine ("layout", $"{name}.xml")}|{linked}",
				}, File.ReadAllLines (Path.Combine (root, "map.txt")));
			}
		}

		[TestMethod]
		public void RelativeAssetPackAndMissingResource ()
		{
			var directory = Path.Combine (Root, "temp", TestName);
			TestOutputDirectories [TestId] = directory;
			Directory.CreateDirectory (directory);
			File.WriteAllText (Path.Combine (directory, "source.txt"), "");
			var errors = new List<BuildErrorEventArgs> ();
			var source = new TaskItem ("source.txt", new Dictionary<string, string> {
				{ "LogicalName", Path.Combine ("Assets", "Scene.txt") },
				{ "AssetPack", "textures" },
			});
			var task = new AndroidComputeResPaths {
				BuildEngine = new MockBuildEngine (Console.Out, errors: errors),
				TaskEnvironment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment (directory, new Dictionary<string, string> ()),
				ResourceFiles = [source, new TaskItem ("missing.txt")],
				IntermediateDir = Path.Combine ("obj", "assets"),
				AssetPackIntermediateDir = Path.Combine ("obj", "packs"),
				Prefixes = "Assets",
				LowercaseFilenames = true,
			};
			Assert.IsFalse (task.Execute ());
			var output = task.IntermediateFiles.Single ();
			var error = errors.Single ();
			Assert.AreEqual (Path.Combine (directory, "obj", "packs", "textures", "assets", "scene.txt"),
				output.ItemSpec);
			Assert.AreSame (source, task.ResolvedResourceFiles.Single ());
			Assert.AreEqual ("XA2001", error.Code);
			Assert.AreEqual ("missing.txt", error.File);
			Assert.Contains ("missing.txt", error.Message);
		}

		[TestMethod]
		[DataRow (false)]
		[DataRow (true)]
		public void ResourceCaseMapCrossesProjects (bool emptyAssets)
		{
			var directory = Path.Combine (Root, "temp", TestName);
			TestOutputDirectories [TestId] = directory;
			Directory.CreateDirectory (directory);
			var tasksDirectory = TestEnvironment.AndroidMSBuildDirectory;
			var assembly = SecurityElement.Escape (Path.Combine (tasksDirectory, "Xamarin.Android.Build.Tasks.dll"));
			var assetTargets = SecurityElement.Escape (Path.Combine (tasksDirectory, "Xamarin.Android.Assets.targets"));
			var modernAssembly = SecurityElement.Escape (Path.Combine (tasksDirectory, "net", "Microsoft.Android.Build.Tasks.dll"));
			var resources = new [] {
				new ProjectResource { Path = Path.Combine ("producer", "source.xml"), Content = "" },
				new ProjectResource {
					Path = Path.Combine ("producer", "map.proj"),
					Content = $"""
						<Project>
						  <UsingTask TaskName="Xamarin.Android.Tasks.AndroidComputeResPaths" AssemblyFile="{assembly}" TaskFactory="TaskHostFactory" Runtime="NET" />
						  <ItemGroup>
						    <Resource Include="source.xml" LogicalName="Resources\Layout\Main.axml" />
						    <Resource Include="source.xml" Link="Resources\Layout\Percent%253B%3BName.axml" />
						  </ItemGroup>
						  <Target Name="Build" Returns="@(_Map)">
						    <AndroidComputeResPaths ResourceFiles="@(Resource)" IntermediateDir="res"
						        Prefixes="Resources" PrefixProperty="MonoAndroidResourcePrefix" LowercaseFilenames="true">
						      <Output TaskParameter="ResourceCaseMap" ItemName="_Map" />
						    </AndroidComputeResPaths>
						  </Target>
						</Project>
						""",
				},
				new ProjectResource {
					Path = "consumer.proj",
					Content = $"""
						<Project DefaultTargets="Build">
						  <PropertyGroup>
						    <_MicrosoftAndroidBuildTasksAssembly>{modernAssembly}</_MicrosoftAndroidBuildTasksAssembly>
						  </PropertyGroup>
						  <Import Project="{assetTargets}" />
						  <ItemGroup Condition="{!emptyAssets}">
						    <AndroidAsset Include="producer\source.xml" LogicalName="Assets\Scene.axml" />
						  </ItemGroup>
						  <Target Name="ReadMap">
						    <MSBuild Projects="producer\map.proj" Targets="Build">
						      <Output TaskParameter="TargetOutputs" ItemName="_AndroidResourceCaseMap" />
						    </MSBuild>
						    <WriteLinesToFile File="map.txt" Overwrite="true" Lines="@(_AndroidResourceCaseMap->'%(Identity)|%(OriginalName)')" />
						  </Target>
						  <Target Name="Build" DependsOnTargets="ReadMap;_ComputeAndroidAssetsPaths">
						    <Error Condition="'@(_AndroidResourceCaseMap->Count())' != '{(emptyAssets ? 0 : 1)}'" Text="Asset map was not replaced." />
						  </Target>
						</Project>
						""",
				},
			};
			new XamarinAndroidApplicationProject ().UpdateProjectFiles (directory, resources, doNotCleanup: true);
			var dotnet = new DotNetCLI (Path.Combine (directory, "consumer.proj"));
			Assert.IsTrue (dotnet.Build (msbuildArguments: ["--no-restore"]),
				string.Join (Environment.NewLine, dotnet.LastBuildOutput));
			CollectionAssert.AreEqual (new [] {
				$"{Path.Combine ("layout", "main.xml")}|{Path.Combine ("Layout", "Main.axml")}",
				$"{Path.Combine ("layout", "percent%3b;name.xml")}|{Path.Combine ("Layout", "Percent%3B;Name.axml")}",
			}, File.ReadAllLines (Path.Combine (directory, "map.txt")));
		}

		[TestMethod]
		[DataRow (false)]
		[DataRow (true)]
		public void ResourceCaseMapIsExplicit (bool lowercase)
		{
			var directory = Path.Combine (Root, "temp", TestName);
			TestOutputDirectories [TestId] = directory;
			Directory.CreateDirectory (directory);
			var source = Path.Combine (directory, "source.xml");
			File.WriteAllText (source, "");
			var names = new [] {
				Path.Combine ("Layout", "Main.axml"),
				Path.Combine ("Layout", "MAIN.axml"),
				Path.Combine ("layout", "main.xml"),
				Path.Combine ("Layout", "Percent%3B;Name.axml"),
				Path.Combine ("Layout", "Upper.AXML"),
			};
			var resources = names.Select ((name, index) => {
				var item = new TaskItem (source);
				((ITaskItem2) item).SetMetadataValueLiteral (index == 1 ? "Link" : "LogicalName", name);
				return (ITaskItem) item;
			}).ToArray ();
			var task = new AndroidComputeResPaths {
				BuildEngine = new NoRegistryBuildEngine (),
				ResourceFiles = resources,
				IntermediateDir = Path.Combine (directory, "res"),
				LowercaseFilenames = lowercase,
			};
			Assert.IsTrue (task.Execute ());
			var map = MonoAndroidHelper.ItemGroupToCaseMap (task.ResourceCaseMap);
			Assert.AreEqual (3, map.Count);
			var main = Path.Combine (lowercase ? "layout" : "Layout", lowercase ? "main.xml" : "Main.xml");
			Assert.AreEqual (names [lowercase ? 1 : 0], map [main]);
			var escapedName = Path.ChangeExtension (lowercase ? names [3].ToLowerInvariant () : names [3], ".xml");
			Assert.AreEqual (names [3], map [escapedName]);

			task.ResourceFiles = [];
			Assert.IsTrue (task.Execute ());
			Assert.IsEmpty (task.ResourceCaseMap);
		}

		[TestMethod]
		public void ResourceCaseMapUsesOrdinalLastWriter ()
		{
			var items = new [] {
				new TaskItem ("layout/main.xml", new Dictionary<string, string> { { "OriginalName", "Layout/First.axml" } }),
				new TaskItem ("layout/Main.xml", new Dictionary<string, string> { { "OriginalName", "Layout/Other.axml" } }),
				new TaskItem ("layout/main.xml", new Dictionary<string, string> { { "OriginalName", "Layout/Last.axml" } }),
			};
			var map = MonoAndroidHelper.ItemGroupToCaseMap (items);
			Assert.AreEqual (2, map.Count);
			Assert.AreEqual ("Layout/Last.axml", map ["layout/main.xml"]);
			Assert.AreEqual ("Layout/Other.axml", map ["layout/Main.xml"]);
		}

		class NoRegistryBuildEngine : MockBuildEngine, IBuildEngine4
		{
			public NoRegistryBuildEngine ()
				: base (Console.Out)
			{
			}

			void IBuildEngine4.RegisterTaskObject (object key, object value, RegisteredTaskObjectLifetime lifetime, bool allowEarlyCollection) =>
				throw new InvalidOperationException ("Resource case maps must use task items.");

			object IBuildEngine4.GetRegisteredTaskObject (object key, RegisteredTaskObjectLifetime lifetime) =>
				throw new InvalidOperationException ("Resource case maps must use task items.");

			object IBuildEngine4.UnregisterTaskObject (object key, RegisteredTaskObjectLifetime lifetime) =>
				throw new InvalidOperationException ("Resource case maps must use task items.");
		}

#pragma warning disable 414
		static object [] [] AndroidComputeResPathsChecks () => new object [] [] {
			new object[] {
				/* Identity */	                Path.Combine ("Assets", "asset1.txt"),
				/* LogicalName */               Path.Combine ("Assets", "subfolder", "asset1.txt"),
				/* expectedIntermediateFile */  Path.Combine ("subfolder", "asset1.txt"),
				/* expectedResultFile */        Path.Combine ("Assets", "asset1.txt"),
			},
			new object[] {
				/* Identity */	                Path.Combine ("Assets", "asset1.txt"),
				/* LogicalName */               null,
				/* expectedIntermediateFile */  Path.Combine ("asset1.txt"),
				/* expectedResultFile */        Path.Combine ("Assets", "asset1.txt"),
			},
			new object[] {
				/* Identity */	                Path.Combine ("Assets", "subfolder", "asset1.txt"),
				/* LogicalName */               null,
				/* expectedIntermediateFile */  Path.Combine ("subfolder", "asset1.txt"),
				/* expectedResultFile */        Path.Combine ("Assets", "subfolder", "asset1.txt"),
			},
			new object[] {
				/* Identity */	                Path.Combine ("Assets", "asset1.txt"),
				/* LogicalName */               Path.Combine ("%FOO%Assets", "subfolder", "asset1.txt"),
				/* expectedIntermediateFile */  Path.Combine ("subfolder", "asset1.txt"),
				/* expectedResultFile */        Path.Combine ("Assets", "asset1.txt"),
			},
			new object[] {
				/* Identity */	                Path.Combine ("Assets", "asset1.txt"),
				/* LogicalName */               Path.Combine ("%FOO%Assets", "subfolder", "..", "..", "asset1.txt"),
				/* expectedIntermediateFile */  Path.Combine ("asset1.txt"),
				/* expectedResultFile */        Path.Combine ("Assets", "asset1.txt"),
			},
		};
#pragma warning restore 414

		[TestMethod]
		[DynamicData (nameof (AndroidComputeResPathsChecks))]
		public void AndroidComputeResPathsTest (string identity, string logicalName, string expectedIntermediateFile, string expectedResultFile)
		{
			var hash = Files.HashString (identity + logicalName + expectedIntermediateFile + expectedResultFile);
			var projectDir = Path.Combine (Root, "temp", $"{nameof (AndroidComputeResPathsTest)}_{hash}");
			TestOutputDirectories [TestId] = projectDir;
			var intermdiateDir = Path.Combine (projectDir, "assets");
			var errors = new List<BuildErrorEventArgs> ();
			IBuildEngine engine = new MockBuildEngine (Console.Out, errors);

			var metaData = new Dictionary<string, string> ();
			if (!string.IsNullOrEmpty (logicalName))
				metaData.Add ("LogicalName", logicalName.Replace ("%FOO%", "\\"));

			// The file needs to exist on disk
			var path = Path.Combine (projectDir, identity);
			Directory.CreateDirectory (Path.GetDirectoryName (path));
			File.WriteAllText (path, contents: "");

			var assets = new List<ITaskItem> () {
				new TaskItem (identity, metaData),
			};

			var task = new AndroidComputeResPaths () {
				BuildEngine = engine,
				TaskEnvironment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment (projectDir, new Dictionary<string, string> ()),
				IntermediateDir = intermdiateDir,
				ProjectDir = projectDir,
				Prefixes = "Assets",
				ResourceFiles = assets.ToArray (),
			};
			Assert.IsTrue (task.Execute (), "task should have succeeded.");
			Assert.AreEqual (1, task.IntermediateFiles.Length, "Expected 1 entry in the IntermediateFiles Output");
			Assert.AreEqual (1, task.ResolvedResourceFiles.Length, "Expected 1 entry in the ResolvedResourceFiles Output");
			var expected = Path.GetFullPath (Path.Combine (intermdiateDir, expectedIntermediateFile));
			Assert.AreEqual (expected, task.IntermediateFiles [0].ItemSpec, ignoreCase: true,
				$"Expected {expected} in task.IntermediateFiles, but got {task.IntermediateFiles [0].ItemSpec}"
			);
			Assert.AreEqual (expectedResultFile, task.ResolvedResourceFiles [0].ItemSpec, ignoreCase: true,
				$"Expected {expectedResultFile} in task.ResolvedResourceFiles, but got {task.ResolvedResourceFiles [0].ItemSpec}"
			);
		}
	}
}
