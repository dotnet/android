using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Microsoft.Android.Tasks;
using NUnit.Framework;

namespace Xamarin.Android.Build.Tests;

[TestFixture]
public class FixupAssetPackArchiveTests : BaseTest
{
	static readonly DateTimeOffset EntryTimestamp = new DateTimeOffset (2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
	const int EntryAttributes = 0x12340000;

	string TestDirectory => Path.Combine (Root, "temp", TestName);
	string ArchiveFile => Path.Combine (TestDirectory, "assetpack.zip");

	[SetUp]
	public void Setup ()
	{
		Directory.CreateDirectory (TestDirectory);
	}

	[TestCase (CompressionLevel.NoCompression, ZipCompressionMethod.Stored)]
	[TestCase (CompressionLevel.Optimal, ZipCompressionMethod.Deflate)]
	public void MovesManifestRemovesResourcesAndNormalizesPaths (CompressionLevel compression, ZipCompressionMethod method)
	{
		var contents = new string ('A', 4096);
		CreateArchive (compression,
			("AndroidManifest.xml", "manifest"),
			("resources.pb", "unused resources"),
			(@"assets\nested\contents.dat", contents),
			("assets.pb", "asset map"));

		Assert.IsTrue (CreateTask ().RunTask ());

		using var archive = ZipFile.OpenRead (ArchiveFile);
		CollectionAssert.AreEquivalent (
			new [] { "manifest/AndroidManifest.xml", "assets/nested/contents.dat", "assets.pb" },
			archive.Entries.Select (entry => entry.FullName));
		Assert.IsNull (archive.GetEntry ("AndroidManifest.xml"));
		Assert.IsNull (archive.GetEntry ("resources.pb"));
		Assert.IsNull (archive.GetEntry (@"assets\nested\contents.dat"));
		AssertEntry (archive, "manifest/AndroidManifest.xml", "manifest", method);
		AssertEntry (archive, "assets/nested/contents.dat", contents, method);
		AssertEntry (archive, "assets.pb", "asset map", method);
	}

	[Test]
	public void ExistingManifestIsReplaced ()
	{
		CreateArchive (CompressionLevel.NoCompression,
			("manifest/AndroidManifest.xml", "old manifest"),
			("AndroidManifest.xml", "current manifest"));

		Assert.IsTrue (CreateTask ().RunTask ());

		using var archive = ZipFile.OpenRead (ArchiveFile);
		Assert.AreEqual (1, archive.Entries.Count);
		AssertEntry (archive, "manifest/AndroidManifest.xml", "current manifest", ZipCompressionMethod.Stored);
	}

	[Test]
	public void MissingManifestStillRemovesResourcesAndNormalizesPaths ()
	{
		CreateArchive (CompressionLevel.NoCompression,
			("resources.pb", "unused resources"),
			(@"assets\contents.dat", "contents"));

		Assert.IsTrue (CreateTask ().RunTask ());

		using var archive = ZipFile.OpenRead (ArchiveFile);
		Assert.AreEqual (1, archive.Entries.Count);
		Assert.IsNull (archive.GetEntry ("resources.pb"));
		AssertEntry (archive, "assets/contents.dat", "contents", ZipCompressionMethod.Stored);
	}

	[Test]
	public void AlreadyFixedArchiveIsNotRewritten ()
	{
		CreateArchive (CompressionLevel.Optimal,
			("manifest/AndroidManifest.xml", "manifest"),
			("assets/contents.dat", "contents"),
			("assets.pb", "asset map"));
		var bytes = File.ReadAllBytes (ArchiveFile);
		var timestamp = new DateTime (2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
		File.SetLastWriteTimeUtc (ArchiveFile, timestamp);

		Assert.IsTrue (CreateTask ().RunTask ());

		CollectionAssert.AreEqual (bytes, File.ReadAllBytes (ArchiveFile));
		Assert.AreEqual (timestamp, File.GetLastWriteTimeUtc (ArchiveFile));
	}

	[Test]
	public void EmptyArchiveRemainsValid ()
	{
		CreateArchive (CompressionLevel.Optimal);

		Assert.IsTrue (CreateTask ().RunTask ());

		using var archive = ZipFile.OpenRead (ArchiveFile);
		Assert.IsEmpty (archive.Entries);
	}

	FixupAssetPackArchive CreateTask () => new FixupAssetPackArchive {
		BuildEngine = new MockBuildEngine (TestContext.Out),
		ArchiveFile = ArchiveFile,
	};

	void CreateArchive (CompressionLevel compression, params (string name, string contents) [] entries)
	{
		using var stream = File.Create (ArchiveFile);
		using var archive = new ZipArchive (stream, ZipArchiveMode.Create);
		foreach (var item in entries) {
			var entry = archive.CreateEntry (item.name, compression);
			entry.LastWriteTime = EntryTimestamp;
			entry.ExternalAttributes = EntryAttributes;
			using var writer = new StreamWriter (entry.Open (), new UTF8Encoding (false));
			writer.Write (item.contents);
		}
	}

	static void AssertEntry (ZipArchive archive, string name, string contents, ZipCompressionMethod method)
	{
		var entry = archive.GetEntry (name) ?? throw new InvalidOperationException ($"Missing '{name}'.");
		Assert.AreEqual (method, entry.CompressionMethod, name);
		Assert.AreEqual (EntryTimestamp.DateTime, entry.LastWriteTime.DateTime, name);
		Assert.AreEqual (EntryAttributes, entry.ExternalAttributes, name);
		using var reader = new StreamReader (entry.Open ());
		Assert.AreEqual (contents, reader.ReadToEnd (), name);
	}
}
