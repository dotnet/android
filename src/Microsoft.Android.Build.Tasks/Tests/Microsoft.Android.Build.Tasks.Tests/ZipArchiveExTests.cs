using NUnit.Framework;
using System;
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
	}
}
