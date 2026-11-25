#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Xml.Linq;

using Microsoft.Android.Tasks;
using Microsoft.Build.Utilities;
using NUnit.Framework;

namespace Xamarin.Android.Build.Tests.Tasks;

[TestFixture]
public class CreateAssemblyStoreTests : BaseTest
{
	[Test]
	[TestCase ("Example.dll.zst")]
	[TestCase ("Example.ni.dll.zst")]
	public void CoreCLRStoreUsesVersionThreeHeader (string assemblyFileName)
	{
		string testDirectory = Path.Combine (Root, "temp", TestName);
		Directory.CreateDirectory (testDirectory);

		string assemblyPath = Path.Combine (testDirectory, assemblyFileName);
		File.WriteAllBytes (assemblyPath, [1, 3, 3, 7, 9, 11, 17, 23]);

		var metadata = new Dictionary<string, string> {
			["Abi"] = "arm64-v8a",
		};
		var task = new CreateAssemblyStore {
			BuildEngine = new MockBuildEngine (TestContext.Out),
			AppSharedLibrariesDir = Path.Combine (testDirectory, "stores"),
			ResolvedFrameworkAssemblies = [],
			ResolvedUserAssemblies = [new TaskItem (assemblyPath, metadata)],
			SupportedAbis = ["arm64-v8a"],
		};

		Assert.IsTrue (task.Execute (), "CreateAssemblyStore should succeed.");

		string storePath = task.AssembliesToAddToArchive.Single ().ItemSpec;
		using var reader = new BinaryReader (File.OpenRead (storePath));
		Assert.AreEqual (0x41424158u, reader.ReadUInt32 (), "Unexpected assembly store magic.");
		Assert.AreEqual (0x80010003u, reader.ReadUInt32 (), "Unexpected arm64 assembly store version.");
		uint assemblyCount = reader.ReadUInt32 ();
		uint indexEntryCount = reader.ReadUInt32 ();
		Assert.AreEqual (1u, assemblyCount, "The store should contain only the real assembly.");
		Assert.AreEqual (assemblyCount * 2, indexEntryCount, "Unexpected index entry count.");
		uint indexSize = reader.ReadUInt32 ();
		Assert.AreEqual (5 * sizeof (uint), reader.BaseStream.Position, "Unexpected assembly store header size.");

		reader.BaseStream.Seek (indexSize, SeekOrigin.Current);
		Assert.AreEqual (0u, reader.ReadUInt32 (), "The first descriptor should immediately follow the index.");

		string manifest = File.ReadAllText ($"{storePath}.manifest");
		StringAssert.Contains ($" {Path.GetFileNameWithoutExtension (assemblyFileName)}", manifest, "The real assembly name should be indexed.");
	}

	[Test]
	public void AssemblyStoresAreRegisteredForCurrentAbis ()
	{
		string directory = Path.Combine (Root, "temp", TestName);
		Directory.CreateDirectory (directory);
		string intermediate = Path.Combine (directory, "obj");
		string stores = Path.Combine (intermediate, "app_shared_libraries");
		string targets = Path.Combine (NativeAotBootstrapTestTools.FindRepositoryRoot (), "src",
			"Xamarin.Android.Build.Tasks", "Xamarin.Android.Common.targets");
		var defineAbis = new XElement (XDocument.Load (targets).Descendants ()
			.Single (element => element.Name.LocalName == "Target" && (string?) element.Attribute ("Name") == "_DefineBuildTargetAbis"));
		foreach (var element in defineAbis.DescendantsAndSelf ()) {
			element.Name = element.Name.LocalName;
		}
		string project = Path.Combine (directory, "stores.proj");
		new XDocument (
			new XElement ("Project",
				new XElement ("PropertyGroup",
					new XElement ("_AndroidRuntime", "CoreCLR"),
					new XElement ("AndroidApplication", "true"),
					new XElement ("EmbedAssembliesIntoApk", "true"),
					new XElement ("AndroidSupportedAbis", "arm64-v8a;x86_64"),
					new XElement ("IntermediateOutputPath", intermediate + Path.DirectorySeparatorChar),
					new XElement ("OutDir", Path.Combine (directory, "out") + Path.DirectorySeparatorChar),
					new XElement ("CleanFile", "stores.FileListAbsolute.txt"),
					new XElement ("_AndroidApplicationSharedLibraryPath", stores + Path.DirectorySeparatorChar)),
				new XElement ("Import", new XAttribute ("Project", "$(MSBuildToolsPath)/Microsoft.Common.CurrentVersion.targets")),
				new XElement ("PropertyGroup", new XElement ("IncrementalCleanDependsOn", "_DefineBuildTargetAbis")),
				defineAbis))
			.Save (project);

		CollectionAssert.AreEquivalent (new [] {
			Path.Combine (stores, "arm64-v8a", "assembly-store.so"),
			Path.Combine (stores, "arm64-v8a", "assembly-store.so.manifest"),
			Path.Combine (stores, "x86_64", "assembly-store.so"),
			Path.Combine (stores, "x86_64", "assembly-store.so.manifest"),
		}, GetFileWrites ());
		CollectionAssert.AreEquivalent (new [] {
			Path.Combine (stores, "arm64-v8a", "assembly-store.so"),
			Path.Combine (stores, "arm64-v8a", "assembly-store.so.manifest"),
		}, GetFileWrites ("-p:AndroidSupportedAbis=arm64-v8a"));
		CollectionAssert.IsEmpty (GetFileWrites ("-p:_AndroidRuntime=NativeAOT"));
		CollectionAssert.IsEmpty (GetFileWrites ("-p:EmbedAssembliesIntoApk=false"));

		foreach (string file in GetFileWrites ()) {
			Directory.CreateDirectory (Path.GetDirectoryName (file) ?? throw new InvalidOperationException ("Store has no directory"));
			File.WriteAllText (file, "store");
		}
		RunIncrementalClean ();
		RunIncrementalClean ("-p:AndroidSupportedAbis=arm64-v8a");
		FileAssert.Exists (Path.Combine (stores, "arm64-v8a", "assembly-store.so"));
		FileAssert.DoesNotExist (Path.Combine (stores, "x86_64", "assembly-store.so"));
		FileAssert.DoesNotExist (Path.Combine (stores, "x86_64", "assembly-store.so.manifest"));

		string [] GetFileWrites (params string [] properties)
		{
			string dotnet = Environment.GetEnvironmentVariable ("DOTNET_HOST_PATH") ?? "dotnet";
			var arguments = new [] { "msbuild", project, "-t:_DefineBuildTargetAbis", "-getItem:FileWrites", "-v:quiet", "-nr:false" }
				.Concat (properties).ToArray ();
			var result = NativeAotBootstrapTestTools.Run (dotnet, arguments);
			Assert.AreEqual (0, result.ExitCode, result.Output + result.Error);
			using var document = JsonDocument.Parse (result.Output);
			return document.RootElement.GetProperty ("Items").GetProperty ("FileWrites").EnumerateArray ()
				.Select (item => Path.GetFullPath (item.GetProperty ("Identity").GetString ()
					?? throw new InvalidOperationException ("FileWrites item has no identity")))
				.ToArray ();
		}

		void RunIncrementalClean (params string [] properties)
		{
			string dotnet = Environment.GetEnvironmentVariable ("DOTNET_HOST_PATH") ?? "dotnet";
			var arguments = new [] { "msbuild", project, "-t:IncrementalClean", "-v:quiet", "-nr:false" }
				.Concat (properties).ToArray ();
			var result = NativeAotBootstrapTestTools.Run (dotnet, arguments);
			Assert.AreEqual (0, result.ExitCode, result.Output + result.Error);
		}
	}
}
