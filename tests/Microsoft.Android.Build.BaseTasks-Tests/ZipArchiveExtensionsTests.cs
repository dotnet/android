using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using NUnit.Framework;
using Microsoft.Android.Build.Tasks;

namespace Microsoft.Android.Build.BaseTasks.Tests
{
	[TestFixture]
	public class ZipArchiveExtensionsTests
	{
		string tempDirectory = "";

		string TempDirectory => tempDirectory.Length > 0 ? tempDirectory : throw new InvalidOperationException ("Setup has not run.");

		[SetUp]
		public void Setup ()
		{
			tempDirectory = Path.Combine (Path.GetTempPath (), Path.GetRandomFileName ());
			Directory.CreateDirectory (tempDirectory);
		}

		[TearDown]
		public void TearDown ()
		{
			if (!string.IsNullOrEmpty (tempDirectory) && Directory.Exists (tempDirectory))
				Directory.Delete (tempDirectory, recursive: true);
		}

		[Test]
		public void FixupWindowsPathSeparators_PreservesStoredCompression ()
		{
			var archivePath = Path.Combine (TempDirectory, "archive.zip");

			using (var archive = ZipArchiveExtensions.CreateZip (archivePath)) {
				archive.AddEntry ("assets\\foo.txt", "foo", Encoding.UTF8, CompressionLevel.NoCompression);
			}

			using (var archive = ZipArchiveExtensions.OpenZipUpdate (archivePath, FileMode.Open)) {
				archive.FixupWindowsPathSeparators (_ => CompressionLevel.NoCompression);
			}

			var updatedMetadata = ZipArchiveMetadataReader.Read (archivePath);
			Assert.IsFalse (updatedMetadata.ContainsKey ("assets\\foo.txt"), "Malformed entry should be removed.");
			Assert.AreEqual (ZipEntryCompressionMethod.Store, updatedMetadata ["assets/foo.txt"].CompressionMethod, "Normalized entry should stay stored.");
		}

		[Test]
		public void Extract_MalformedEmptyStoredEntry ()
		{
			var archivePath = Path.Combine (TempDirectory, "archive.zip");
			CreateMalformedEmptyStoredEntryArchive (archivePath);

			using var archive = ZipFile.OpenRead (archivePath);
			var entry = archive.Entries [0];
			using var destination = new MemoryStream ();

			entry.Extract (destination);

			Assert.AreEqual (0, destination.Length, "The empty entry should produce an empty output.");
		}

		[Test]
		public void OpenZipRead_OpensReadOnlyArchive ()
		{
			var archivePath = Path.Combine (TempDirectory, "archive.zip");
			using (var archive = ZipArchiveExtensions.CreateZip (archivePath)) {
				archive.AddEntry ("entry.txt", "contents", Encoding.UTF8);
			}

			File.SetAttributes (archivePath, File.GetAttributes (archivePath) | FileAttributes.ReadOnly);
			try {
				using var archive = ZipArchiveExtensions.OpenZipRead (archivePath);
				Assert.AreEqual ("entry.txt", archive.Entries [0].FullName);
			} finally {
				File.SetAttributes (archivePath, FileAttributes.Normal);
			}
		}

		static void CreateMalformedEmptyStoredEntryArchive (string path)
		{
			const string entryName = "R.txt";
			var entryNameBytes = Encoding.ASCII.GetBytes (entryName);

			using var stream = File.Create (path);
			using var writer = new BinaryWriter (stream, Encoding.UTF8, leaveOpen: false);

			writer.Write (0x04034b50);
			writer.Write ((short) 20);
			writer.Write ((short) 0);
			writer.Write ((short) 0);
			writer.Write (0);
			writer.Write (0);
			writer.Write (2);
			writer.Write (0);
			writer.Write ((short) entryNameBytes.Length);
			writer.Write ((short) 0);
			writer.Write (entryNameBytes);
			writer.Write ((byte) 0x03);
			writer.Write ((byte) 0x00);

			var centralDirectoryOffset = stream.Position;
			writer.Write (0x02014b50);
			writer.Write ((short) 20);
			writer.Write ((short) 20);
			writer.Write ((short) 0);
			writer.Write ((short) 0);
			writer.Write (0);
			writer.Write (0);
			writer.Write (2);
			writer.Write (0);
			writer.Write ((short) entryNameBytes.Length);
			writer.Write ((short) 0);
			writer.Write ((short) 0);
			writer.Write ((short) 0);
			writer.Write ((short) 0);
			writer.Write (0);
			writer.Write (0);
			writer.Write (entryNameBytes);

			var centralDirectorySize = stream.Position - centralDirectoryOffset;
			writer.Write (0x06054b50);
			writer.Write ((short) 0);
			writer.Write ((short) 0);
			writer.Write ((short) 1);
			writer.Write ((short) 1);
			writer.Write ((int) centralDirectorySize);
			writer.Write ((int) centralDirectoryOffset);
			writer.Write ((short) 0);
		}
	}
}
