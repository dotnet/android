using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using Microsoft.Android.Tasks;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	public class ZipArchiveExTests : BaseTest
	{
		string TestDirectory => Path.Combine (Root, "temp", TestName);
		string TestPath => Path.Combine (TestDirectory, "source");
		string Zip => Path.Combine (TestDirectory, "test.zip");

		[SetUp]
		public void SetUp ()
		{
			if (Directory.Exists (TestDirectory))
				Directory.Delete (TestDirectory, recursive: true);
			Directory.CreateDirectory (TestPath);
		}

		void CreateDirectories (params string [] paths)
		{
			foreach (var path in paths) {
				var dest = Path.Combine (TestPath, path);
				Directory.CreateDirectory (Path.GetDirectoryName (dest) ?? throw new InvalidOperationException ("Missing parent directory."));
				//Just put the path in the test file, for testing purposes
				File.WriteAllText (dest, path);
			}
		}

		static DateTime ToDosTime (DateTime t) =>
			new DateTime (t.Year, t.Month, t.Day, t.Hour, t.Minute, t.Second / 2 * 2, t.Kind);

		void AssertZip (string expected)
		{
			FileAssert.Exists (Zip, "Zip file should exist!");

			var builder = new StringBuilder ();
			using (var archive = ZipFile.OpenRead (Zip)) {
				foreach (var entry in archive.Entries) {
					builder.AppendLine (entry.FullName);
				}
			}

			Assert.AreEqual (expected.Trim ().Replace ("\r\n", "\n"), builder.ToString ().Trim ().Replace ("\r\n", "\n"));
		}

		void AssertSkipExisting (string file, string fileInArchive, bool expected, CompressionLevel method = CompressionLevel.Optimal)
		{
			DateTime lastWrite = File.GetLastWriteTimeUtc (file);
			string path = Path.GetFullPath (file);
			using (var archive = new ZipArchiveEx (Zip, FileMode.Open)) {
				var entry = archive.Archive.GetEntry (fileInArchive);
				var modTime = entry?.LastWriteTime.UtcDateTime ?? DateTime.MinValue;
				bool result = archive.SkipExistingFile (file, fileInArchive, method);
				Assert.AreEqual (expected, result,
					$"SkipExistingFile returned unexpected value for {Zip} {path} {fileInArchive}\n" +
					$"Requested compression: {method}; entry lengths: {entry?.Length}/{entry?.CompressedLength}\n" +
					$"{ToDosTime (lastWrite)} (disk {lastWrite:MM/dd/yyyy HH:mm:ss:fff}) <= {ToDosTime (modTime)} (zip {modTime:MM/dd/yyyy HH:mm:ss:fff}) = {result}");
			}
		}

		[Test]
		public void AddDirectory ()
		{
			CreateDirectories ("A.txt", Path.Combine ("B", "B.txt"));

			using (var archive = new ZipArchiveEx (Zip, FileMode.Create)) {
				archive.AddDirectory (TestPath, "temp", CompressionLevel.Optimal);
			}

			AssertZip (
				"""
				temp/A.txt
				temp/B/B.txt
				""");
		}

		[Test]
		public void AddDirectoryOddPaths ()
		{
			CreateDirectories ("A.txt", Path.Combine ("B", "B.txt"));

			using (var archive = new ZipArchiveEx (Zip, FileMode.Create)) {
				archive.AddDirectory (TestPath + @"\../source/", "temp", CompressionLevel.Optimal);
			}

			AssertZip (
				"""
				temp/A.txt
				temp/B/B.txt
				""");
		}

		[Test]
		public void AddDirectoryCurrentDirectory ()
		{
			CreateDirectories ("A.txt", Path.Combine ("B", "B.txt"));

			string cwd = Directory.GetCurrentDirectory ();
			try {
				Directory.SetCurrentDirectory (TestPath);

				using (var archive = new ZipArchiveEx (Zip, FileMode.Create)) {
					archive.AddDirectory (".", "temp", CompressionLevel.Optimal);
				}

				AssertZip (
					"""
					temp/A.txt
					temp/B/B.txt
					""");
			} finally {
				Directory.SetCurrentDirectory (cwd);
			}
		}

		[Test]
		[Repeat (100)]
		public void SkipExistingFile_Exist ()
		{
			CreateDirectories ("A.txt", Path.Combine ("B", "B.txt"));

			using (var archive = new ZipArchiveEx (Zip, FileMode.Create)) {
				archive.AddDirectory (TestPath, folderInArchive: "", CompressionLevel.Optimal);
			}

			AssertZip (
				"""
				A.txt
				B/B.txt
				""");
			AssertSkipExisting (Path.Combine (TestPath, "A.txt"), "A.txt", expected: true);
			AssertSkipExisting (Path.Combine (TestPath, "B", "B.txt"), "B/B.txt", expected: true);
			AssertSkipExisting (Path.Combine (TestPath, "C.txt"), "C.txt", expected: false);
			AssertSkipExisting (Path.Combine (TestPath, "C", "C.txt"), "C/C.txt", expected: false);
		}

		[Test]
		[Repeat (100)]
		public void SkipExistingFile_TimeStamp ()
		{
			CreateDirectories ("A.txt", Path.Combine ("B", "B.txt"));

			using (var archive = new ZipArchiveEx (Zip, FileMode.Create)) {
				archive.AddDirectory (TestPath, folderInArchive: "", CompressionLevel.Optimal);
			}

			AssertZip (
				"""
				A.txt
				B/B.txt
				""");
			var file = Path.Combine (TestPath, "A.txt");
			File.SetLastWriteTimeUtc (file, File.GetLastWriteTimeUtc (file).AddSeconds (2));
			AssertSkipExisting (file, "A.txt", expected: false);
			AssertSkipExisting (Path.Combine (TestPath, "B", "B.txt"), "B/B.txt", expected: true);
		}

		[Test]
		[Repeat (100)]
		public void SkipExistingFile_Compression ()
		{
			var fileName = "A.txt";
			var filePath = Path.Combine (TestPath, fileName);
			CreateDirectories (fileName);

			using (var archive = new ZipArchiveEx (Zip, FileMode.Create)) {
				archive.AddDirectory (TestPath, folderInArchive: "", CompressionLevel.Optimal);
			}

			AssertSkipExisting (filePath, fileName, expected: true, method: CompressionLevel.Optimal);
			AssertSkipExisting (filePath, fileName, expected: true, method: CompressionLevel.Fastest);
			AssertSkipExisting (filePath, fileName, expected: false, method: CompressionLevel.NoCompression);

			File.Delete (Zip);
			using (var archive = new ZipArchiveEx (Zip, FileMode.Create)) {
				archive.AddDirectory (TestPath, folderInArchive: "", method: CompressionLevel.NoCompression);
			}

			AssertSkipExisting (filePath, fileName, expected: true, method: CompressionLevel.NoCompression);
			AssertSkipExisting (filePath, fileName, expected: false, method: CompressionLevel.Optimal);
			AssertSkipExisting (filePath, fileName, expected: false, method: CompressionLevel.Fastest);
		}

		[Test]
		public void ChangedSameSizeContentWithinDosTimestampIsUpdated ()
		{
			var file = Path.Combine (TestPath, "A.txt");
			var timestamp = new DateTime (2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
			File.WriteAllText (file, "before");
			File.SetLastWriteTimeUtc (file, timestamp);
			using (var archive = new ZipArchiveEx (Zip, FileMode.Create)) {
				archive.AddDirectory (TestPath, "");
			}

			File.WriteAllText (file, "after!");
			File.SetLastWriteTimeUtc (file, timestamp.AddSeconds (1));
			AssertSkipExisting (file, "A.txt", expected: false);

			var task = new CreateJavaArchive { BuildEngine = new MockBuildEngine (TestContext.Out) };
			using (var archive = new ZipArchiveEx (Zip, FileMode.Open)) {
				Assert.IsTrue (archive.AddFileIfChanged (task.Log, file, "A.txt", CompressionLevel.Optimal));
			}
			using var zip = ZipFile.OpenRead (Zip);
			var entry = zip.GetEntry ("A.txt") ?? throw new InvalidOperationException ("Missing A.txt.");
			using var reader = new StreamReader (entry.Open ());
			Assert.AreEqual ("after!", reader.ReadToEnd ());
		}

		[Test]
		public void UnchangedFileDoesNotRewriteArchive ()
		{
			CreateDirectories ("A.txt");
			var file = Path.Combine (TestPath, "A.txt");
			using (var archive = new ZipArchiveEx (Zip, FileMode.Create)) {
				archive.AddDirectory (TestPath, "");
			}
			var bytes = File.ReadAllBytes (Zip);
			var timestamp = new DateTime (2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
			File.SetLastWriteTimeUtc (Zip, timestamp);
			var task = new CreateJavaArchive { BuildEngine = new MockBuildEngine (TestContext.Out) };

			using (var archive = new ZipArchiveEx (Zip, FileMode.Open)) {
				Assert.IsFalse (archive.AddFileIfChanged (task.Log, file, "A.txt", CompressionLevel.Optimal));
				archive.AddDirectory (TestPath, "");
			}

			CollectionAssert.AreEqual (bytes, File.ReadAllBytes (Zip));
			Assert.AreEqual (timestamp, File.GetLastWriteTimeUtc (Zip));
		}

		[TestCase (CompressionLevel.NoCompression, false)]
		[TestCase (CompressionLevel.Optimal, true)]
		public void AddDirectoryUsesRequestedCompression (CompressionLevel compression, bool compressed)
		{
			File.WriteAllText (Path.Combine (TestPath, "A.txt"), new string ('A', 4096));
			using (var archive = new ZipArchiveEx (Zip, FileMode.Create)) {
				archive.AddDirectory (TestPath, "", compression);
			}
			using var zip = ZipFile.OpenRead (Zip);
			var entry = zip.GetEntry ("A.txt") ?? throw new InvalidOperationException ("Missing A.txt.");
			AssertCompression (entry, compressed);
			if (compressed)
				Assert.Less (entry.CompressedLength, entry.Length);
		}

		[TestCase (CompressionLevel.NoCompression, CompressionLevel.Optimal, true)]
		[TestCase (CompressionLevel.Optimal, CompressionLevel.NoCompression, false)]
		public void AddDirectoryUpdatesCompression (CompressionLevel before, CompressionLevel after, bool compressed)
		{
			CreateDirectories ("A.txt");
			using (var archive = new ZipArchiveEx (Zip, FileMode.Create)) {
				archive.AddDirectory (TestPath, "", before);
			}
			using (var archive = new ZipArchiveEx (Zip, FileMode.Open)) {
				archive.AddDirectory (TestPath, "", after);
			}
			using var zip = ZipFile.OpenRead (Zip);
			var entry = zip.GetEntry ("A.txt") ?? throw new InvalidOperationException ("Missing A.txt.");
			AssertCompression (entry, compressed);
			Assert.AreEqual (1, zip.Entries.Count);
			using var reader = new StreamReader (entry.Open ());
			Assert.AreEqual ("A.txt", reader.ReadToEnd ());
		}

		[TestCase (CompressionLevel.NoCompression, false)]
		[TestCase (CompressionLevel.Optimal, true)]
		public void MoveEntryPreservesCompressionAndMetadata (CompressionLevel compression, bool compressed)
		{
			var timestamp = new DateTimeOffset (2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
			using (var archive = ZipFile.Open (Zip, ZipArchiveMode.Create)) {
				var entry = archive.CreateEntry ("old.txt", compression);
				entry.LastWriteTime = timestamp;
				entry.ExternalAttributes = 0x12340000;
				using var writer = new StreamWriter (entry.Open (), new UTF8Encoding (false));
				writer.Write (new string ('A', 4096));
			}

			using (var archive = new ZipArchiveEx (Zip, FileMode.Open)) {
				Assert.IsTrue (archive.MoveEntry ("old.txt", "new.txt"));
			}

			using var zip = ZipFile.OpenRead (Zip);
			Assert.IsNull (zip.GetEntry ("old.txt"));
			var moved = zip.GetEntry ("new.txt") ?? throw new InvalidOperationException ("Missing moved entry.");
			AssertCompression (moved, compressed);
			Assert.AreEqual (timestamp.DateTime, moved.LastWriteTime.DateTime);
			Assert.AreEqual (0x12340000, moved.ExternalAttributes);
			using var reader = new StreamReader (moved.Open ());
			Assert.AreEqual (new string ('A', 4096), reader.ReadToEnd ());
		}

		[Test]
		public void MoveEntryHandlesMissingAndUnchangedNames ()
		{
			CreateDirectories ("A.txt");
			using (var archive = new ZipArchiveEx (Zip, FileMode.Create)) {
				archive.AddDirectory (TestPath, "");
			}
			var bytes = File.ReadAllBytes (Zip);
			using (var archive = new ZipArchiveEx (Zip, FileMode.Open)) {
				Assert.IsFalse (archive.MoveEntry ("missing.txt", "new.txt"));
				Assert.IsTrue (archive.MoveEntry ("A.txt", "A.txt"));
			}
			CollectionAssert.AreEqual (bytes, File.ReadAllBytes (Zip));
		}

		[TestCase (CompressionLevel.NoCompression, false)]
		[TestCase (CompressionLevel.Optimal, true)]
		public void FixupWindowsPathSeparatorsPreservesCompression (CompressionLevel compression, bool compressed)
		{
			using (var archive = ZipFile.Open (Zip, ZipArchiveMode.Create)) {
				var entry = archive.CreateEntry (@"assets\dir\A.txt", compression);
				using var writer = new StreamWriter (entry.Open (), new UTF8Encoding (false));
				writer.Write ("contents");
			}
			var renames = new List<(string oldPath, string newPath)> ();
			using (var archive = new ZipArchiveEx (Zip, FileMode.Open)) {
				archive.FixupWindowsPathSeparators ((oldPath, newPath) => renames.Add ((oldPath, newPath)));
			}
			Assert.That (renames, Is.EqualTo (new [] { (@"assets\dir\A.txt", "assets/dir/A.txt") }));
			using var zip = ZipFile.OpenRead (Zip);
			Assert.IsNull (zip.GetEntry (@"assets\dir\A.txt"));
			var normalized = zip.GetEntry ("assets/dir/A.txt") ?? throw new InvalidOperationException ("Missing normalized entry.");
			AssertCompression (normalized, compressed);
			using var reader = new StreamReader (normalized.Open ());
			Assert.AreEqual ("contents", reader.ReadToEnd ());
		}

		static void AssertCompression (ZipArchiveEntry entry, bool compressed)
		{
			Assert.AreEqual (compressed ? ZipCompressionMethod.Deflate : ZipCompressionMethod.Stored, entry.CompressionMethod);
			if (compressed)
				Assert.AreNotEqual (entry.Length, entry.CompressedLength, $"{entry.FullName} should be DEFLATE compressed.");
			else
				Assert.AreEqual (entry.Length, entry.CompressedLength, $"{entry.FullName} should be STORED.");
		}
	}
}
