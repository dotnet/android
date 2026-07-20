#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Hashing;
using System.Linq;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NUnit.Framework;
using Xamarin.Android.Tasks;

namespace Xamarin.Android.Build.Tests.Tasks;

[TestFixture]
public class CreateAssemblyStoreTests : BaseTest
{
	[Test]
	public void ContentIdMatchesStoreContents ()
	{
		string testDirectory = Path.Combine (Root, "temp", nameof (ContentIdMatchesStoreContents));
		Directory.CreateDirectory (testDirectory);

		string assemblyPath = Path.Combine (testDirectory, "Example.dll.zst");
		byte [] assemblyData = [1, 3, 3, 7, 9, 11, 17, 23];
		File.WriteAllBytes (assemblyPath, assemblyData);

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
		byte [] store = File.ReadAllBytes (storePath);
		using var reader = new BinaryReader (new MemoryStream (store));
		Assert.AreEqual (0x41424158u, reader.ReadUInt32 (), "Unexpected assembly store magic.");
		Assert.AreEqual (0x80010004u, reader.ReadUInt32 (), "Unexpected arm64 assembly store version.");
		reader.BaseStream.Seek (3 * sizeof (uint), SeekOrigin.Current);
		ulong contentId = reader.ReadUInt64 ();

		Assert.AreEqual (
			XxHash3.HashToUInt64 (store.AsSpan (5 * sizeof (uint) + sizeof (ulong))),
			contentId,
			"The content ID should hash everything after the assembly store header."
		);
	}
}
