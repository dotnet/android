#nullable enable
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;

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

	static bool Register (MockBuildEngine engine, string project, ITaskItem [] assemblies, string [] abis, ITaskItem []? packagedAssemblies = null)
	{
		var task = new CollectCompressedAssemblyInfo {
			BuildEngine = engine,
			ResolvedAssemblies = assemblies,
			PackagedAssemblies = packagedAssemblies ?? [],
			SupportedAbis = abis,
			ProjectFullPath = project,
			EnableCompression = true,
		};
		return task.Execute ();
	}

	CollectAssemblyFilesToCompress Collect (MockBuildEngine engine, string project, ITaskItem [] assemblies, string [] abis)
	{
		var task = new CollectAssemblyFilesToCompress {
			BuildEngine = engine,
			AssemblyCompressionDirectory = Path.Combine (TestDirectory, "zstd"),
			EmbedAssemblies = true,
			EnableCompression = true,
			ProjectFullPath = project,
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
