#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

using Microsoft.Android.Build.Tasks;
using Microsoft.Android.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NUnit.Framework;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Build.Tests.Tasks;

[TestFixture]
public class CompressionMetadataTests : BaseTest
{
	string TestDirectory => Path.Combine (Root, "temp", TestName);

	[TestCase (true, true)]
	[TestCase (false, false)]
	[TestCase (false, true)]
	public void RegistersMetadataWithoutNativeSources (bool debug, bool enableCompression)
	{
		var assembly = CreateAssembly ("Sample.dll", "arm64-v8a");
		var engine = new MockBuildEngine (TestContext.Out);
		string project = Path.Combine (TestDirectory, "Sample.csproj");
		Assert.IsTrue (Register (engine, project, [assembly], ["arm64-v8a"], debug, enableCompression));

		var info = ((IBuildEngine4)engine).GetRegisteredTaskObjectAssemblyLocal<
			Dictionary<AndroidTargetArch, Dictionary<string, CompressedAssemblyInfo>>> (
				CompressedAssemblyInfo.GetKey (project), RegisteredTaskObjectLifetime.Build);
		if (!debug && enableCompression) {
			Assert.IsNotNull (info);
			if (info == null) {
				throw new InvalidOperationException ("Expected registered compression metadata");
			}
			Assert.AreEqual (0u, info [AndroidTargetArch.Arm64] [assembly.GetMetadata ("DestinationSubPath")].DescriptorIndex);
		} else {
			Assert.IsNull (info);
		}
		Assert.IsEmpty (Directory.GetFiles (TestDirectory, "*.ll", SearchOption.AllDirectories));
	}

	[TestCase ("armeabi-v7a")]
	[TestCase ("arm64-v8a")]
	[TestCase ("x86_64")]
	public void TrimmingRetainsSparseIndicesAndCompressedFiles (string abi)
	{
		var assemblies = new [] {
			CreateAssembly ("First.dll", abi),
			CreateAssembly ("Kept.dll", abi),
			CreateAssembly ("Removed.dll", abi),
			CreateAssembly ("Last.dll", abi),
		};
		string project = Path.Combine (TestDirectory, "Sample.csproj");

		var engine = new MockBuildEngine (TestContext.Out);
		Assert.IsTrue (Register (engine, project, assemblies, [abi]));
		var collect = Collect (engine, project, [assemblies [1], assemblies [3]], [abi]);
		CollectionAssert.AreEqual (new [] { "1", "3" }, collect.AssembliesToCompressOutput.Select (item => item.GetMetadata ("DescriptorIndex")));

		var compress = new CompressAssemblies {
			BuildEngine = engine,
			AssembliesToCompress = collect.AssembliesToCompressOutput,
		};
		Assert.IsTrue (compress.Execute ());
		Assert.IsEmpty (compress.FailedToCompressAssembliesOutput);
		AssertCompressedAssembly (collect.AssembliesToCompressOutput [0], 1);
		AssertCompressedAssembly (collect.AssembliesToCompressOutput [1], 3);

		string lastOutput = collect.AssembliesToCompressOutput [1].GetMetadata ("DestinationPath");
		byte [] previousOutput = File.ReadAllBytes (lastOutput);
		DateTime previousTimestamp = File.GetLastWriteTimeUtc (lastOutput);

		// A later build retains the same pre-trimming list, but a different subset survives.
		// Last.dll must keep index 3 rather than being renumbered to 1 in the two-entry store.
		engine = new MockBuildEngine (TestContext.Out);
		Assert.IsTrue (Register (engine, project, assemblies, [abi]));
		collect = Collect (engine, project, [assemblies [0], assemblies [3]], [abi]);
		CollectionAssert.AreEqual (new [] { "0", "3" }, collect.AssembliesToCompressOutput.Select (item => item.GetMetadata ("DescriptorIndex")));
		Assert.AreEqual (lastOutput, collect.AssembliesToCompressOutput [1].GetMetadata ("DestinationPath"));
		CollectionAssert.AreEqual (previousOutput, File.ReadAllBytes (lastOutput));
		Assert.AreEqual (previousTimestamp, File.GetLastWriteTimeUtc (lastOutput));

		compress = new CompressAssemblies {
			BuildEngine = engine,
			AssembliesToCompress = [collect.AssembliesToCompressOutput [0]],
		};
		Assert.IsTrue (compress.Execute ());
		AssertCompressedAssembly (collect.AssembliesToCompressOutput [0], 0);
		AssertCompressedAssembly (collect.AssembliesToCompressOutput [1], 3);
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

	[Test]
	public void MultiAbiMetadataKeepsCultureAndSkipSemantics ()
	{
		string [] abis = ["armeabi-v7a", "arm64-v8a", "x86_64"];
		var inputs = new List<ITaskItem> ();
		var survivors = new List<ITaskItem> ();
		foreach (string abi in abis) {
			var excluded = CreateAssembly ("Excluded.dll", abi);
			excluded.SetMetadata ("AndroidSkipAddToPackage", "true");
			var uncompressed = CreateAssembly ("Uncompressed.dll", abi);
			uncompressed.SetMetadata ("AndroidSkipCompression", "true");
			var german = CreateAssembly ("Sample.resources.dll", abi, "de");
			var japanese = CreateAssembly ("Sample.resources.dll", abi, "ja");
			// Exercise the DestinationSubDirectory fallback in the registry key.
			japanese.RemoveMetadata ("DestinationSubPath");
			inputs.AddRange ([excluded, uncompressed, german, japanese]);
			survivors.AddRange ([excluded, uncompressed, german, japanese]);
		}

		string project = Path.Combine (TestDirectory, "Sample.csproj");
		var engine = new MockBuildEngine (TestContext.Out);
		Assert.IsTrue (Register (engine, project, inputs.ToArray (), abis));
		var collect = Collect (engine, project, survivors.ToArray (), abis);
		Assert.AreEqual (6, collect.AssembliesToCompressOutput.Length);
		CollectionAssert.AreEqual (new [] { "1", "2", "1", "2", "1", "2" },
			collect.AssembliesToCompressOutput.Select (item => item.GetMetadata ("DescriptorIndex")));
		foreach (var item in collect.AssembliesToCompressOutput) {
			string destination = item.GetMetadata ("DestinationPath");
			Assert.IsTrue (destination.Contains (Path.DirectorySeparatorChar + "de" + Path.DirectorySeparatorChar, StringComparison.Ordinal) ||
				destination.Contains (Path.DirectorySeparatorChar + "ja" + Path.DirectorySeparatorChar, StringComparison.Ordinal));
		}
	}

	[Test]
	public void ConsumerRequiresThePairedRegistry ()
	{
		var assembly = CreateAssembly ("Sample.dll", "arm64-v8a");
		var errors = new List<BuildErrorEventArgs> ();
		var task = CreateCollector (new MockBuildEngine (TestContext.Out, errors), "Missing.csproj", [assembly], ["arm64-v8a"]);
		Assert.IsFalse (task.Execute ());
		StringAssert.Contains ("Assembly compression info not found", errors.Single ().Message);
	}

	[TestCase (true, true, true)]
	[TestCase (false, false, true)]
	[TestCase (false, true, false)]
	public void DisabledCompressionDoesNotRequireRegistry (bool includeDebugSymbols, bool enableCompression, bool embedAssemblies)
	{
		var assembly = CreateAssembly ("Sample.dll", "arm64-v8a");
		var task = CreateCollector (new MockBuildEngine (TestContext.Out), "Disabled.csproj", [assembly], ["arm64-v8a"]);
		task.IncludeDebugSymbols = includeDebugSymbols;
		task.EnableCompression = enableCompression;
		task.EmbedAssemblies = embedAssemblies;
		Assert.IsTrue (task.Execute ());
		Assert.IsEmpty (task.AssembliesToCompressOutput);
		CollectionAssert.AreEqual (new [] { assembly }, task.ResolvedUserAssembliesOutput);
	}

	[Test]
	public void EmptyStoreHasAnEmptyRegistry ()
	{
		string project = Path.Combine (TestDirectory, "Empty.csproj");
		var engine = new MockBuildEngine (TestContext.Out);
		Assert.IsTrue (Register (engine, project, [], ["arm64-v8a"]));
		Assert.IsEmpty (Collect (engine, project, [], ["arm64-v8a"]).AssembliesToCompressOutput);
	}

	[Test]
	public void MissingAssemblyReportsAnError ()
	{
		var errors = new List<BuildErrorEventArgs> ();
		var engine = new MockBuildEngine (TestContext.Out, errors);
		var assembly = new TaskItem (Path.Combine (TestDirectory, "Missing.dll"));
		assembly.SetMetadata ("Abi", "arm64-v8a");
		Assert.IsFalse (Register (engine, "Missing.csproj", [assembly], ["arm64-v8a"]));
		Assert.AreEqual ("XA2025", errors.Single ().Code);
	}

	TaskItem CreateAssembly (string fileName, string abi, string culture = "")
	{
		string subDirectory = culture.Length == 0 ? abi : Path.Combine (abi, culture);
		string path = Path.Combine (TestDirectory, subDirectory, fileName);
		Directory.CreateDirectory (Path.GetDirectoryName (path) ?? throw new InvalidOperationException ("Missing assembly directory"));
		File.Copy (typeof (CompressionMetadataTests).Assembly.Location, path);
		var item = new TaskItem (path);
		item.SetMetadata ("Abi", abi);
		item.SetMetadata ("Culture", culture);
		item.SetMetadata ("DestinationSubDirectory", culture);
		item.SetMetadata ("DestinationSubPath", Path.Combine (culture, fileName));
		return item;
	}

	static bool Register (MockBuildEngine engine, string project, ITaskItem [] assemblies, string [] abis, bool debug = false, bool enableCompression = true)
	{
		var task = new CollectCompressedAssemblyInfo {
			BuildEngine = engine,
			ResolvedAssemblies = assemblies,
			SupportedAbis = abis,
			ProjectFullPath = project,
			Debug = debug,
			EnableCompression = enableCompression,
		};
		return task.Execute ();
	}

	CollectAssemblyFilesToCompress CreateCollector (MockBuildEngine engine, string project, ITaskItem [] assemblies, string [] abis) => new () {
		BuildEngine = engine,
		AssemblyCompressionDirectory = Path.Combine (TestDirectory, "zstd"),
		EmbedAssemblies = true,
		EnableCompression = true,
		ProjectFullPath = project,
		ResolvedUserAssemblies = assemblies,
		SupportedAbis = abis,
	};

	CollectAssemblyFilesToCompress Collect (MockBuildEngine engine, string project, ITaskItem [] assemblies, string [] abis)
	{
		var task = CreateCollector (engine, project, assemblies, abis);
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
