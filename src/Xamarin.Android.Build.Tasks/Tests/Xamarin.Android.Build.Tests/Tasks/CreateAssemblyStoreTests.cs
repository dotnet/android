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
	[TestCase ("Example.dll.zst", "armeabi-v7a", 0x00020003u)]
	[TestCase ("Example.ni.dll.zst", "arm64-v8a", 0x80010003u)]
	[TestCase ("Example.dll.zst", "x86", 0x00040003u)]
	[TestCase ("Example.ni.dll.zst", "x86_64", 0x80030003u)]
	public void StoreUsesCoreCLRFormat (string assemblyFileName, string abi, uint expectedVersion)
	{
		string testDirectory = Path.Combine (Root, "temp", $"{nameof (StoreUsesCoreCLRFormat)}-{abi}-{assemblyFileName}");
		Directory.CreateDirectory (testDirectory);

		string assemblyPath = Path.Combine (testDirectory, assemblyFileName);
		File.WriteAllBytes (assemblyPath, [1, 3, 3, 7, 9, 11, 17, 23]);

		var metadata = new Dictionary<string, string> {
			["Abi"] = abi,
		};
		var task = new CreateAssemblyStore {
			BuildEngine = new MockBuildEngine (TestContext.Out),
			AppSharedLibrariesDir = Path.Combine (testDirectory, "stores"),
			ResolvedFrameworkAssemblies = [],
			ResolvedUserAssemblies = [new TaskItem (assemblyPath, metadata)],
			SupportedAbis = [abi],
			UseAssemblyStore = true,
		};

		Assert.IsTrue (task.Execute (), "CreateAssemblyStore should succeed.");

		string storePath = task.AssembliesToAddToArchive.Single ().ItemSpec;
		using var reader = new BinaryReader (File.OpenRead (storePath));
		Assert.AreEqual (0x41424158u, reader.ReadUInt32 (), "Unexpected assembly store magic.");
		Assert.AreEqual (expectedVersion, reader.ReadUInt32 (), "Unexpected assembly store version.");
		uint assemblyCount = reader.ReadUInt32 ();
		uint indexEntryCount = reader.ReadUInt32 ();
		Assert.AreEqual (1u, assemblyCount, "The store should contain only the real assembly.");
		Assert.AreEqual (assemblyCount * 2, indexEntryCount, "Unexpected index entry count.");
		uint indexSize = reader.ReadUInt32 ();
		Assert.AreEqual (5 * sizeof (uint), reader.BaseStream.Position, "Unexpected assembly store header size.");
		Assert.AreEqual (indexEntryCount * (2 * sizeof (uint) + sizeof (byte)), indexSize, "Assembly store indexes should always use 32-bit CRC32 hashes.");

		var expectedHashes = new HashSet<uint> {
			TypeMapHelper.HashNameForCLR (Path.GetFileNameWithoutExtension (assemblyFileName)),
			TypeMapHelper.HashNameForCLR (Path.GetFileNameWithoutExtension (Path.GetFileNameWithoutExtension (assemblyFileName))),
		};
		var actualHashes = new HashSet<uint> ();
		for (int i = 0; i < indexEntryCount; i++) {
			actualHashes.Add (reader.ReadUInt32 ());
			reader.BaseStream.Seek (sizeof (uint) + sizeof (byte), SeekOrigin.Current);
		}
		CollectionAssert.AreEquivalent (expectedHashes, actualHashes, "Unexpected assembly name hashes.");

		Assert.AreEqual (0u, reader.ReadUInt32 (), "The first descriptor should immediately follow the index.");

		string manifest = File.ReadAllText ($"{storePath}.manifest");
		StringAssert.Contains ($" {Path.GetFileNameWithoutExtension (assemblyFileName)}", manifest, "The real assembly name should be indexed.");
	}
}
