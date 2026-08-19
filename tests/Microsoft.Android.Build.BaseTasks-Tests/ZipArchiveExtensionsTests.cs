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

			using (var archive = ZipArchiveExtensions.OpenZip (archivePath, FileMode.Create)) {
				archive.AddEntry ("assets\\foo.txt", "foo", Encoding.UTF8, CompressionLevel.NoCompression);
			}

			var metadata = ZipArchiveMetadataReader.Read (archivePath);
			using (var archive = ZipArchiveExtensions.OpenZip (archivePath, FileMode.Open)) {
				archive.FixupWindowsPathSeparators (entry => metadata [entry.FullName].CompressionMethod.ToCompressionLevel ());
			}

			var updatedMetadata = ZipArchiveMetadataReader.Read (archivePath);
			Assert.IsFalse (updatedMetadata.ContainsKey ("assets\\foo.txt"), "Malformed entry should be removed.");
			Assert.AreEqual (ZipEntryCompressionMethod.Store, updatedMetadata ["assets/foo.txt"].CompressionMethod, "Normalized entry should stay stored.");
		}
	}
}
