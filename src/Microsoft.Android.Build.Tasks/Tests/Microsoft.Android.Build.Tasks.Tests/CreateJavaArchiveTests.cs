using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Microsoft.Android.Tasks;
using NUnit.Framework;

namespace Xamarin.Android.Build.Tests;

[TestFixture]
public class CreateJavaArchiveTests : BaseTest
{
	string TestDirectory => Path.Combine (Root, "temp", TestName);
	string SourceDirectory => Path.Combine (TestDirectory, "classes");
	string OutputFile => Path.Combine (TestDirectory, "classes.jar");

	[SetUp]
	public void Setup ()
	{
		Directory.CreateDirectory (SourceDirectory);
	}

	[Test]
	public void CompiledClassesAreStored ()
	{
		var mainClass = new byte [] { 0xca, 0xfe, 0xba, 0xbe, 0, 0, 0, 61 };
		var nestedClass = new byte [] { 0xca, 0xfe, 0xba, 0xbe, 0, 0, 0, 62 };
		File.WriteAllBytes (Path.Combine (SourceDirectory, "Main.class"), mainClass);
		Directory.CreateDirectory (Path.Combine (SourceDirectory, "com", "example"));
		File.WriteAllBytes (Path.Combine (SourceDirectory, "com", "example", "Nested.class"), nestedClass);

		Assert.IsTrue (CreateTask ().RunTask ());

		using var archive = ZipFile.OpenRead (OutputFile);
		CollectionAssert.AreEquivalent (new [] { "Main.class", "com/example/Nested.class" }, archive.Entries.Select (entry => entry.FullName));
		foreach (var entry in archive.Entries) {
			Assert.AreEqual (ZipCompressionMethod.Stored, entry.CompressionMethod);
			Assert.AreEqual (entry.Length, entry.CompressedLength, $"{entry.FullName} should be STORED.");
		}
		CollectionAssert.AreEqual (mainClass, ReadClass (archive, "Main.class"));
		CollectionAssert.AreEqual (nestedClass, ReadClass (archive, "com/example/Nested.class"));
	}

	[Test]
	public void UnchangedClassesDoNotRewriteArchive ()
	{
		File.WriteAllBytes (Path.Combine (SourceDirectory, "Main.class"), [0xca, 0xfe, 0xba, 0xbe]);
		Assert.IsTrue (CreateTask ().RunTask ());
		var bytes = File.ReadAllBytes (OutputFile);
		var timestamp = new DateTime (2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
		File.SetLastWriteTimeUtc (OutputFile, timestamp);

		Assert.IsTrue (CreateTask ().RunTask ());

		CollectionAssert.AreEqual (bytes, File.ReadAllBytes (OutputFile));
		Assert.AreEqual (timestamp, File.GetLastWriteTimeUtc (OutputFile));
	}

	[Test]
	public void ChangedClassWithinDosTimestampIsUpdated ()
	{
		var file = Path.Combine (SourceDirectory, "Main.class");
		var timestamp = new DateTime (2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
		File.WriteAllBytes (file, [0xca, 0xfe, 0xba, 0xbe, 0]);
		File.SetLastWriteTimeUtc (file, timestamp);
		Assert.IsTrue (CreateTask ().RunTask ());

		var updated = new byte [] { 0xca, 0xfe, 0xba, 0xbe, 1 };
		File.WriteAllBytes (file, updated);
		File.SetLastWriteTimeUtc (file, timestamp.AddSeconds (1));
		Assert.IsTrue (CreateTask ().RunTask ());

		using var archive = ZipFile.OpenRead (OutputFile);
		CollectionAssert.AreEqual (updated, ReadClass (archive, "Main.class"));
		var entry = archive.GetEntry ("Main.class") ?? throw new InvalidOperationException ("Missing Main.class.");
		Assert.AreEqual (ZipCompressionMethod.Stored, entry.CompressionMethod);
		Assert.AreEqual (entry.Length, entry.CompressedLength);
	}

	[Test]
	public void EmptyClassDirectoryCreatesEmptyArchive ()
	{
		Assert.IsTrue (CreateTask ().RunTask ());
		using var archive = ZipFile.OpenRead (OutputFile);
		Assert.IsEmpty (archive.Entries);
	}

	CreateJavaArchive CreateTask () => new CreateJavaArchive {
		BuildEngine = new MockBuildEngine (TestContext.Out),
		SourceDirectory = SourceDirectory,
		OutputFile = OutputFile,
	};

	static byte [] ReadClass (ZipArchive archive, string name)
	{
		var entry = archive.GetEntry (name) ?? throw new InvalidOperationException ($"Missing {name}.");
		using var source = entry.Open ();
		using var destination = new MemoryStream ();
		source.CopyTo (destination);
		return destination.ToArray ();
	}
}
