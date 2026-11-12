#nullable enable
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;

using Microsoft.Android.Build.Tasks;
using Microsoft.Android.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NUnit.Framework;

namespace Xamarin.Android.Build.Tests.Tasks;

[TestFixture]
public class CompressionMetadataTests : BaseTest
{
	string TestDirectory => Path.Combine (Root, "temp", TestName);

	[TestCase (false)]
	[TestCase (true)]
	public void ProductionTargetsPassCompressionConfiguration (bool multithreaded)
	{
		const string abi = "arm64-v8a";
		var assembly = CreateAssembly ("Kept.dll", abi);
		var source = XDocument.Load (Path.Combine (NativeAotBootstrapTestTools.FindRepositoryRoot (),
			"src", "Xamarin.Android.Build.Tasks", "Xamarin.Android.Common.targets"));
		var targets = source.Descendants ().Where (element => element.Name.LocalName == "Target" &&
			((string?) element.Attribute ("Name") == "_AndroidCollectCompressedAssemblyInfo" ||
			 (string?) element.Attribute ("Name") == "_CollectAssembliesToCompress"))
			.Select (element => new XElement (element)).ToArray ();
		foreach (var target in targets) {
			foreach (var element in target.DescendantsAndSelf ()) {
				element.Name = element.Name.LocalName;
			}
		}
		string project = Path.Combine (TestDirectory, "compression.proj");
		new XDocument (new XElement ("Project",
			new XElement ("PropertyGroup",
				new XElement ("IntermediateOutputPath", "obj/net12.0/android-arm64/"),
				new XElement ("_AndroidRuntime", "MonoVM"),
				new XElement ("AndroidIncludeDebugSymbols", "false"),
				new XElement ("AndroidEnableAssemblyCompression", "true"),
				new XElement ("EmbedAssembliesIntoApk", "true")),
			new XElement ("UsingTask", new XAttribute ("TaskName", typeof (CollectCompressedAssemblyInfo).FullName ?? throw new InvalidOperationException ("Missing compression metadata task type name")),
				new XAttribute ("AssemblyFile", typeof (CollectCompressedAssemblyInfo).Assembly.Location)),
			new XElement ("UsingTask", new XAttribute ("TaskName", typeof (CollectAssemblyFilesToCompress).FullName ?? throw new InvalidOperationException ("Missing assembly collection task type name")),
				new XAttribute ("AssemblyFile", typeof (CollectAssemblyFilesToCompress).Assembly.Location)),
			new XElement ("ItemGroup",
				new XElement ("_BuildTargetAbis", new XAttribute ("Include", abi)),
				new XElement ("_AndroidAssemblyCompressionInputs", new XAttribute ("Include", assembly.ItemSpec),
					new XElement ("Abi", abi), new XElement ("DestinationSubPath", "Kept.dll")),
				new XElement ("_ShrunkUserAssemblies", new XAttribute ("Include", "@(_AndroidAssemblyCompressionInputs)"))),
			new XElement ("Target", new XAttribute ("Name", "_AndroidResolveAssemblyCompressionInputs")),
			new XElement ("Target", new XAttribute ("Name", "_PrepareBuildApk")),
			targets,
			new XElement ("Target", new XAttribute ("Name", "Build"),
				new XAttribute ("DependsOnTargets", "_CollectAssembliesToCompress"),
				new XElement ("Error", new XAttribute ("Condition", "'@(_AssembliesToCompress->Count())' != '1'"),
					new XAttribute ("Text", "Expected one assembly to compress.")),
				new XElement ("Error", new XAttribute ("Condition", "'%(_AssembliesToCompress.DescriptorIndex)' != '0'"),
					new XAttribute ("Text", "Expected the original descriptor index.")))))
			.Save (project);

		string dotnet = Environment.GetEnvironmentVariable ("DOTNET_HOST_PATH") ?? "dotnet";
		var arguments = new [] { "msbuild", project, "-t:Build", "-v:minimal", "-m", "-nr:false" };
		if (multithreaded) {
			arguments = arguments.Concat (new [] { "-mt", "-warnaserror:MSB4286,MSB4287,MSB4288" }).ToArray ();
		}
		var result = NativeAotBootstrapTestTools.Run (dotnet, arguments);
		Assert.AreEqual (0, result.ExitCode, result.Output + result.Error);
	}

	[TestCase ("net12.0", "x86_64")]
	[TestCase ("net13.0", "arm64-v8a")]
	public void InterleavedConfigurationsKeepTheirOwnDescriptorIndices (string secondFramework, string secondAbi)
	{
		const string firstAbi = "arm64-v8a";
		string project = Path.Combine (TestDirectory, "Sample.csproj");
		string firstIntermediate = Path.Combine (TestDirectory, "obj", "net12.0", firstAbi);
		string secondIntermediate = Path.Combine (TestDirectory, "obj", secondFramework, secondAbi);
		var removed = CreateAssembly ("Removed.dll", firstAbi);
		var first = CreateAssembly ("Kept.dll", firstAbi);
		var second = secondAbi == firstAbi ? first : CreateAssembly ("Kept.dll", secondAbi);
		var engine = new MockBuildEngine (TestContext.Out);

		Assert.IsTrue (Register (engine, project, [removed, first], [firstAbi],
			intermediateOutputPath: Path.Combine (firstIntermediate, ".")));
		Assert.IsTrue (Register (engine, project, [second], [secondAbi],
			intermediateOutputPath: secondIntermediate));

		var secondCollection = Collect (engine, project, [second], [secondAbi], Path.Combine (secondIntermediate, "."));
		var firstCollection = Collect (engine, project, [first], [firstAbi], firstIntermediate);
		Assert.AreEqual ("0", secondCollection.AssembliesToCompressOutput.Single ().GetMetadata ("DescriptorIndex"));
		Assert.AreEqual ("1", firstCollection.AssembliesToCompressOutput.Single ().GetMetadata ("DescriptorIndex"));
	}

	[Test]
	public void TrimmingRetainsSparseIndicesAndCompressedFiles ()
	{
		const string abi = "arm64-v8a";
		var assemblies = Enumerable.Range (0, 18)
			.Select (index => CreateAssembly ($"Assembly{index}.dll", abi))
			.ToArray ();
		string project = Path.Combine (TestDirectory, "Sample.csproj");

		var engine = new MockBuildEngine (TestContext.Out);
		Assert.IsTrue (Register (engine, project, assemblies, [abi]));
		var collect = Collect (engine, project, [assemblies [0], assemblies [17]], [abi]);

		var compress = new CompressAssemblies {
			BuildEngine = engine,
			AssembliesToCompress = collect.AssembliesToCompressOutput,
		};
		Assert.IsTrue (compress.Execute ());
		Assert.IsEmpty (compress.FailedToCompressAssembliesOutput);
		AssertCompressedAssembly (collect.AssembliesToCompressOutput [0], 0);
		AssertCompressedAssembly (collect.AssembliesToCompressOutput [1], 17);

		string lastOutput = collect.AssembliesToCompressOutput [1].GetMetadata ("DestinationPath");
		byte [] previousOutput = File.ReadAllBytes (lastOutput);
		DateTime previousTimestamp = File.GetLastWriteTimeUtc (lastOutput);

		// A later build retains the same pre-trimming list, but a different subset survives.
		// Assembly17.dll must keep index 17 rather than being renumbered in the two-entry store.
		engine = new MockBuildEngine (TestContext.Out);
		Assert.IsTrue (Register (engine, project, assemblies, [abi]));
		collect = Collect (engine, project, [assemblies [1], assemblies [17]], [abi]);
		Assert.AreEqual (lastOutput, collect.AssembliesToCompressOutput [1].GetMetadata ("DestinationPath"));

		compress = new CompressAssemblies {
			BuildEngine = engine,
			AssembliesToCompress = collect.AssembliesToCompressOutput,
		};
		Assert.IsTrue (compress.Execute ());
		AssertCompressedAssembly (collect.AssembliesToCompressOutput [0], 1);
		AssertCompressedAssembly (collect.AssembliesToCompressOutput [1], 17);
		CollectionAssert.AreEqual (previousOutput, File.ReadAllBytes (lastOutput));
		Assert.AreEqual (previousTimestamp, File.GetLastWriteTimeUtc (lastOutput));

		// A composite ReadyToRun image is generated after trimming, not in the input closure.
		var composite = CreateAssembly ("Sample.r2r.dll", abi);
		engine = new MockBuildEngine (TestContext.Out);
		Assert.IsTrue (Register (engine, project, assemblies, [abi], [assemblies [1], assemblies [17], composite]));
		collect = Collect (engine, project, [assemblies [1], assemblies [17], composite], [abi]);
		compress.BuildEngine = engine;
		compress.AssembliesToCompress = collect.AssembliesToCompressOutput;
		Assert.IsTrue (compress.Execute ());
		Assert.AreEqual (3, collect.AssembliesToCompressOutput.Length, "The generated composite image must not be silently packaged uncompressed.");
		AssertCompressedAssembly (collect.AssembliesToCompressOutput [2], 18);
		CollectionAssert.AreEqual (previousOutput, File.ReadAllBytes (lastOutput));
		Assert.AreEqual (previousTimestamp, File.GetLastWriteTimeUtc (lastOutput));
	}

	[Test]
	public void ChangedTrimmedAssemblyUsesItsOriginalIndexAndActualLength ()
	{
		const string abi = "arm64-v8a";
		var removed = CreateAssembly ("Removed.dll", abi);
		var original = CreateAssembly ("Kept.dll", abi);
		string project = Path.Combine (TestDirectory, "Sample.csproj");
		var engine = new MockBuildEngine (TestContext.Out);
		Assert.IsTrue (Register (engine, project, [removed, original], [abi]));

		var trimmed = new TaskItem (original) {
			ItemSpec = Path.Combine (TestDirectory, "shrunk", "Kept.dll"),
		};
		Directory.CreateDirectory (Path.GetDirectoryName (trimmed.ItemSpec) ?? throw new InvalidOperationException ("Missing assembly directory"));
		File.Copy (typeof (AndroidTask).Assembly.Location, trimmed.ItemSpec);
		Assert.AreNotEqual (new FileInfo (original.ItemSpec).Length, new FileInfo (trimmed.ItemSpec).Length,
			"The post-trimming payload must differ in length from the registered assembly.");
		var collect = Collect (engine, project, [trimmed], [abi]);
		var compress = new CompressAssemblies {
			BuildEngine = engine,
			AssembliesToCompress = collect.AssembliesToCompressOutput,
		};
		Assert.IsTrue (compress.Execute ());
		AssertCompressedAssembly (collect.AssembliesToCompressOutput.Single (), 1);

		// Changing the post-trimming bytes does not change the descriptor's identity.
		File.Copy (typeof (CompressionMetadataTests).Assembly.Location, trimmed.ItemSpec, overwrite: true);
		Assert.IsTrue (compress.Execute ());
		AssertCompressedAssembly (collect.AssembliesToCompressOutput.Single (), 1);
	}

	TaskItem CreateAssembly (string fileName, string abi)
	{
		string directory = Path.Combine (TestDirectory, abi);
		Directory.CreateDirectory (directory);
		string path = Path.Combine (directory, fileName);
		File.Copy (typeof (CompressionMetadataTests).Assembly.Location, path);
		var item = new TaskItem (path);
		item.SetMetadata ("Abi", abi);
		item.SetMetadata ("DestinationSubPath", fileName);
		return item;
	}

	bool Register (MockBuildEngine engine, string project, ITaskItem [] assemblies, string [] abis, ITaskItem []? packagedAssemblies = null, string? intermediateOutputPath = null)
	{
		var task = new CollectCompressedAssemblyInfo {
			BuildEngine = engine,
			ResolvedAssemblies = assemblies,
			PackagedAssemblies = packagedAssemblies ?? [],
			SupportedAbis = abis,
			ProjectFullPath = project,
			IntermediateOutputPath = intermediateOutputPath ?? Path.Combine (TestDirectory, "obj"),
			EnableCompression = true,
		};
		return task.Execute ();
	}

	CollectAssemblyFilesToCompress Collect (MockBuildEngine engine, string project, ITaskItem [] assemblies, string [] abis, string? intermediateOutputPath = null)
	{
		var task = new CollectAssemblyFilesToCompress {
			BuildEngine = engine,
			AssemblyCompressionDirectory = Path.Combine (TestDirectory, "zstd"),
			EmbedAssemblies = true,
			EnableCompression = true,
			ProjectFullPath = project,
			IntermediateOutputPath = intermediateOutputPath ?? Path.Combine (TestDirectory, "obj"),
			ResolvedUserAssemblies = assemblies,
			SupportedAbis = abis,
		};
		Assert.IsTrue (task.Execute ());
		return task;
	}

	static void AssertCompressedAssembly (ITaskItem item, uint expectedIndex)
	{
		using var reader = new BinaryReader (File.OpenRead (item.GetMetadata ("DestinationPath")));
		Assert.AreEqual (0x535A4158u, reader.ReadUInt32 (), "The XAZS format must remain unchanged.");
		Assert.AreEqual (expectedIndex, reader.ReadUInt32 ());
		byte [] source = File.ReadAllBytes (item.ItemSpec);
		Assert.AreEqual ((uint)source.Length, reader.ReadUInt32 ());
		byte [] compressed = reader.ReadBytes (checked ((int)(reader.BaseStream.Length - reader.BaseStream.Position)));
		byte [] decoded = new byte [source.Length];
		Assert.IsTrue (ZstandardDecoder.TryDecompress (compressed, decoded, out int written));
		Assert.AreEqual (source.Length, written);
		CollectionAssert.AreEqual (source, decoded);
	}
}
