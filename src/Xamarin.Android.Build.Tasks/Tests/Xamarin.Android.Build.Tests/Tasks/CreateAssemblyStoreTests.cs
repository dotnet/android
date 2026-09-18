#nullable enable
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Microsoft.Build.Utilities;
using NUnit.Framework;
using Xamarin.Android.Tasks;

namespace Xamarin.Android.Build.Tests.Tasks;

[TestFixture]
public class CreateAssemblyStoreTests : BaseTest
{
	[Test]
	[TestCase ("Example.dll.zst")]
	[TestCase ("Example.ni.dll.zst")]
	public void CoreCLRStoreUsesVersionThreeHeader (string assemblyFileName)
	{
		string testDirectory = Path.Combine (Root, "temp", $"{nameof (CoreCLRStoreUsesVersionThreeHeader)}-{assemblyFileName}");
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
			TargetRuntime = "CoreCLR",
			UseAssemblyStore = true,
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
}
