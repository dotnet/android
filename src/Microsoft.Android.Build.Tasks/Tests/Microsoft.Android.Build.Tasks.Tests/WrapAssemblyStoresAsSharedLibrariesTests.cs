#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Microsoft.Android.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NUnit.Framework;

namespace Xamarin.Android.Build.Tests.Tasks;

[TestFixture]
public class WrapAssemblyStoresAsSharedLibrariesTests : BaseTest
{
	[TestCase ("armeabi-v7a", "android-arm")]
	[TestCase ("arm64-v8a", "android-arm64")]
	[TestCase ("x86", "android-x86")]
	[TestCase ("x86_64", "android-x64")]
	[Category ("RequiresAndroidNdk")]
	public void WrapsStoreWithArchivePathAndCleanupDirectory (string abi, string rid)
	{
		string directory = Path.Combine (Root, "temp", TestName);
		var store = CreateStore (directory, abi);
		var task = new WrapAssemblyStoresAsSharedLibraries {
			BuildEngine = new MockBuildEngine (TestContext.Out),
			IntermediateOutputPath = directory,
			ResolvedAssemblies = [store],
		};

		Assert.IsTrue (task.Execute ());
		Assert.AreEqual (1, task.WrappedAssemblies.Length);
		var library = task.WrappedAssemblies [0];
		string outputDirectory = Path.Combine (directory, rid, "wrapped-assembly-store");
		Assert.AreEqual (Path.Combine (outputDirectory, "libassembly-store.so"), library.ItemSpec);
		Assert.AreEqual ($"lib/{abi}/libassembly-store.so", library.GetMetadata ("ArchivePath"));
		FileAssert.Exists (library.ItemSpec);
		Assert.AreEqual (1, task.DirectoriesToDelete.Length);
		Assert.AreEqual (outputDirectory, task.DirectoriesToDelete [0].ItemSpec);

		CollectionAssert.AreEqual (File.ReadAllBytes (store.ItemSpec), NativeToolTestHelper.ReadSection (library.ItemSpec, "payload"));
		Assert.IsEmpty (Directory.GetFiles (directory, "*.S", SearchOption.AllDirectories));
		Assert.IsEmpty (Directory.GetFiles (directory, "*.o", SearchOption.AllDirectories));
	}

	[Test]
	[Category ("RequiresAndroidNdk")]
	public void PerAbiStoresRemainSeparateForAppBundleSplitting ()
	{
		string directory = Path.Combine (Root, "temp", TestName);
		string [] abis = ["armeabi-v7a", "arm64-v8a", "x86_64"];
		ITaskItem [] stores = abis.Select (abi => CreateStore (directory, abi)).ToArray ();
		var task = new WrapAssemblyStoresAsSharedLibraries {
			BuildEngine = new MockBuildEngine (TestContext.Out),
			IntermediateOutputPath = directory,
			ResolvedAssemblies = stores,
		};

		Assert.IsTrue (task.Execute ());
		Assert.AreEqual (abis.Length, task.WrappedAssemblies.Select (item => item.ItemSpec).Distinct ().Count ());
		CollectionAssert.AreEquivalent (abis.Select (abi => $"lib/{abi}/libassembly-store.so"),
			task.WrappedAssemblies.Select (item => item.GetMetadata ("ArchivePath")));
		Assert.AreEqual (abis.Length, task.DirectoriesToDelete.Length);
		foreach (var library in task.WrappedAssemblies) {
			string abi = library.GetMetadata ("ArchivePath").Split ('/') [1];
			CollectionAssert.AreEqual (File.ReadAllBytes (stores.Single (item => item.GetMetadata ("Abi") == abi).ItemSpec),
				NativeToolTestHelper.ReadSection (library.ItemSpec, "payload"));
		}
	}

	[Test]
	public void MissingAbiLogsCodedError ()
	{
		string directory = Path.Combine (Root, "temp", TestName);
		var store = CreateStore (directory, "arm64-v8a");
		store.RemoveMetadata ("Abi");
		var errors = new List<BuildErrorEventArgs> ();
		var task = new WrapAssemblyStoresAsSharedLibraries {
			BuildEngine = new MockBuildEngine (TestContext.Out, errors),
			IntermediateOutputPath = directory,
			ResolvedAssemblies = [store],
		};

		Assert.IsFalse (task.Execute ());
		Assert.AreEqual (1, errors.Count);
		Assert.AreEqual ("XA4234", errors [0].Code);
		Assert.IsEmpty (task.WrappedAssemblies);
		Assert.IsEmpty (Directory.GetFiles (directory, "libassembly-store.so", SearchOption.AllDirectories));
	}

	[Test]
	public void MissingAbiPreservesEarlierWrapperAndCleanupOutputs ()
	{
		string directory = Path.Combine (Root, "temp", TestName);
		var first = CreateStore (directory, "arm64-v8a");
		var invalid = CreateStore (directory, "armeabi-v7a");
		invalid.RemoveMetadata ("Abi");
		var last = CreateStore (directory, "x86_64");
		var errors = new List<BuildErrorEventArgs> ();
		var task = new WrapAssemblyStoresAsSharedLibraries {
			BuildEngine = new MockBuildEngine (TestContext.Out, errors),
			IntermediateOutputPath = directory,
			ResolvedAssemblies = [first, invalid, last],
		};

		Assert.IsFalse (task.Execute ());
		Assert.AreEqual (1, errors.Count);
		Assert.AreEqual ("XA4234", errors [0].Code);
		StringAssert.Contains (invalid.ItemSpec, errors [0].Message);
		Assert.AreEqual (1, task.WrappedAssemblies.Length, "The wrapper written before the error must remain tracked.");
		var library = task.WrappedAssemblies [0];
		FileAssert.Exists (library.ItemSpec);
		Assert.AreEqual ("lib/arm64-v8a/libassembly-store.so", library.GetMetadata ("ArchivePath"));
		Assert.AreEqual (1, task.DirectoriesToDelete.Length, "The partial wrapper directory must remain available for cleanup.");
		Assert.AreEqual (Path.GetDirectoryName (library.ItemSpec), task.DirectoriesToDelete [0].ItemSpec);
		Assert.IsFalse (Directory.Exists (Path.Combine (directory, "android-arm", "wrapped-assembly-store")));
		Assert.IsFalse (Directory.Exists (Path.Combine (directory, "android-x64", "wrapped-assembly-store")),
			"The task should stop at the invalid item, not continue wrapping later stores.");
	}

	static TaskItem CreateStore (string directory, string abi)
	{
		string storeDirectory = Path.Combine (directory, abi);
		Directory.CreateDirectory (storeDirectory);
		string path = Path.Combine (storeDirectory, "assembly-store.so");
		byte [] data = new byte [4097];
		new Random (StringComparer.Ordinal.GetHashCode (abi)).NextBytes (data);
		File.WriteAllBytes (path, data);
		var store = new TaskItem (path);
		store.SetMetadata ("Abi", abi);
		return store;
	}
}
