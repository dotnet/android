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

		[TestCase (ZipArchiveMode.Read)]
		[TestCase (ZipArchiveMode.Update)]
		[TestCase (ZipArchiveMode.Create)]
		public void OpenZip_DisposesFileWhenArchiveCreationFails (ZipArchiveMode mode)
		{
			var archivePath = Path.Combine (TempDirectory, "archive.zip");
			using (ZipArchiveExtensions.CreateZip (archivePath)) {
			}

			Assert.Throws<ArgumentException> (() => {
				using var archive = mode switch {
					ZipArchiveMode.Read => ZipArchiveExtensions.OpenZipRead (archivePath, Encoding.Unicode),
					ZipArchiveMode.Update => ZipArchiveExtensions.OpenZipUpdate (archivePath, FileMode.Open, Encoding.Unicode),
					ZipArchiveMode.Create => ZipArchiveExtensions.CreateZip (archivePath, FileMode.Create, Encoding.Unicode),
					_ => throw new ArgumentOutOfRangeException (nameof (mode)),
				};
			});

			using var exclusive = new FileStream (archivePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
		}

		[Test]
		public void ReadZip64Metadata ()
		{
			var archivePath = Path.Combine (TempDirectory, "archive.zip");
			CreateZip64Archive (archivePath);

			var metadata = ZipArchiveMetadataReader.ReadEntries (archivePath);

			Assert.AreEqual (1, metadata.Count);
			Assert.AreEqual ("entry.txt", metadata [0].FullName);
			Assert.AreEqual (3, metadata [0].CompressedSize);
			Assert.AreEqual (3, metadata [0].UncompressedSize);
			Assert.AreEqual (ZipEntryCompressionMethod.Store, metadata [0].CompressionMethod);
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

		static void CreateZip64Archive (string path)
		{
			const string entryName = "entry.txt";
			var entryNameBytes = Encoding.ASCII.GetBytes (entryName);
			var contents = Encoding.ASCII.GetBytes ("zip");

			using var stream = File.Create (path);
			using var writer = new BinaryWriter (stream, Encoding.UTF8, leaveOpen: false);

			writer.Write (0x04034b50);
			writer.Write ((short) 45);
			writer.Write ((short) 0);
			writer.Write ((short) 0);
			writer.Write (0);
			writer.Write (0);
			writer.Write (contents.Length);
			writer.Write (contents.Length);
			writer.Write ((short) entryNameBytes.Length);
			writer.Write ((short) 0);
			writer.Write (entryNameBytes);
			writer.Write (contents);

			long centralDirectoryOffset = stream.Position;
			writer.Write (0x02014b50);
			writer.Write ((short) 45);
			writer.Write ((short) 45);
			writer.Write ((short) 0);
			writer.Write ((short) 0);
			writer.Write (0);
			writer.Write (0);
			writer.Write (uint.MaxValue);
			writer.Write (uint.MaxValue);
			writer.Write ((short) entryNameBytes.Length);
			writer.Write ((short) 20);
			writer.Write ((short) 0);
			writer.Write ((short) 0);
			writer.Write ((short) 0);
			writer.Write (0);
			writer.Write (0);
			writer.Write (entryNameBytes);
			writer.Write (Zip64ExtraFieldId);
			writer.Write ((short) 16);
			writer.Write ((long) contents.Length);
			writer.Write ((long) contents.Length);

			long centralDirectorySize = stream.Position - centralDirectoryOffset;
			long zip64DirectoryOffset = stream.Position;
			writer.Write (0x06064b50);
			writer.Write ((long) 44);
			writer.Write ((short) 45);
			writer.Write ((short) 45);
			writer.Write (0);
			writer.Write (0);
			writer.Write ((long) 1);
			writer.Write ((long) 1);
			writer.Write (centralDirectorySize);
			writer.Write (centralDirectoryOffset);

			writer.Write (0x07064b50);
			writer.Write (0);
			writer.Write (zip64DirectoryOffset);
			writer.Write (1);

			writer.Write (0x06054b50);
			writer.Write ((short) 0);
			writer.Write ((short) 0);
			writer.Write (ushort.MaxValue);
			writer.Write (ushort.MaxValue);
			writer.Write (uint.MaxValue);
			writer.Write (uint.MaxValue);
			writer.Write ((short) 0);
		}

		const short Zip64ExtraFieldId = 0x0001;
	}
}
