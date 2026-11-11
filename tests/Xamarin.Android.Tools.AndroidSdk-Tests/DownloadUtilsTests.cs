// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Threading;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Xamarin.Android.Tools.Tests
{
	[TestClass]
	public class DownloadUtilsTests
	{
		string tempDir = null!;

		[TestInitialize]
		public void SetUp ()
		{
			tempDir = Path.Combine (Path.GetTempPath (), $"DownloadUtilsTests-{Guid.NewGuid ():N}");
			Directory.CreateDirectory (tempDir);
		}

		[TestCleanup]
		public void TearDown ()
		{
			if (Directory.Exists (tempDir))
				Directory.Delete (tempDir, recursive: true);
		}
		[TestMethod]
		public void ParseChecksumFile_Null_ReturnsNull ()
		{
			Assert.IsNull (DownloadUtils.ParseChecksumFile (null!));
		}

		[TestMethod]
		public void ParseChecksumFile_Empty_ReturnsNull ()
		{
			Assert.IsNull (DownloadUtils.ParseChecksumFile (""));
		}

		[TestMethod]
		public void ParseChecksumFile_WhitespaceOnly_ReturnsNull ()
		{
			Assert.IsNull (DownloadUtils.ParseChecksumFile ("   \n\t  "));
		}

		[TestMethod]
		public void ParseChecksumFile_HashOnly ()
		{
			Assert.AreEqual ("abc123def456", DownloadUtils.ParseChecksumFile ("abc123def456"));
		}

		[TestMethod]
		public void ParseChecksumFile_HashAndFilename ()
		{
			// Standard sha256sum format: "<hash>  <filename>"
			Assert.AreEqual ("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
				DownloadUtils.ParseChecksumFile ("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855  microsoft-jdk-21-linux-x64.tar.gz"));
		}

		[TestMethod]
		public void ParseChecksumFile_HashAndFilename_WithTab ()
		{
			Assert.AreEqual ("abc123", DownloadUtils.ParseChecksumFile ("abc123\tfilename.zip"));
		}

		[TestMethod]
		public void ParseChecksumFile_MultipleLines_ReturnsFirstHash ()
		{
			var content = "abc123  file1.zip\ndef456  file2.zip\n";
			Assert.AreEqual ("abc123", DownloadUtils.ParseChecksumFile (content));
		}

		[TestMethod]
		public void ParseChecksumFile_LeadingAndTrailingWhitespace ()
		{
			Assert.AreEqual ("abc123", DownloadUtils.ParseChecksumFile ("  abc123  filename.zip  \n"));
		}

		[TestMethod]
		[DataRow ("abc123\r\n")]
		[DataRow ("abc123\r")]
		[DataRow ("abc123\n")]
		public void ParseChecksumFile_VariousLineEndings (string content)
		{
			Assert.AreEqual ("abc123", DownloadUtils.ParseChecksumFile (content));
		}

		// --- VerifyChecksum tests ---

		[TestMethod]
		public void VerifyChecksum_MatchingHash_DoesNotThrow ()
		{
			var filePath = Path.Combine (tempDir, "test.bin");
			var content = new byte [] { 0x48, 0x65, 0x6c, 0x6c, 0x6f }; // "Hello"
			File.WriteAllBytes (filePath, content);

			using var sha = SHA256.Create ();
			var expected = BitConverter.ToString (sha.ComputeHash (content)).Replace ("-", "").ToLowerInvariant ();

			DownloadUtils.VerifyChecksum (filePath, expected);
		}

		[TestMethod]
		public void VerifyChecksum_GoogleManifestSha1MatchingHash_DoesNotThrow ()
		{
			var filePath = Path.Combine (tempDir, "test.bin");
			File.WriteAllBytes (filePath, new byte [] { 0x48, 0x65, 0x6c, 0x6c, 0x6f }); // "Hello"

			DownloadUtils.VerifyChecksum (
				filePath,
				"f7ff9e8b7bb2e09b70935a5d785e0cc5d9d0abf0",
				ChecksumType.Sha1);
		}

		[TestMethod]
		public void VerifyChecksum_MismatchedHash_Throws ()
		{
			var filePath = Path.Combine (tempDir, "test.bin");
			File.WriteAllBytes (filePath, new byte [] { 1, 2, 3 });

			var ex = Assert.ThrowsExactly<InvalidOperationException> (() =>
				DownloadUtils.VerifyChecksum (filePath, "0000000000000000000000000000000000000000000000000000000000000000"));
			Assert.Contains ("Checksum verification failed", ex!.Message);
		}

		[TestMethod]
		public void VerifyChecksum_CaseInsensitive ()
		{
			var filePath = Path.Combine (tempDir, "test.bin");
			var content = new byte [] { 0xFF };
			File.WriteAllBytes (filePath, content);

			using var sha = SHA256.Create ();
			var upperHash = BitConverter.ToString (sha.ComputeHash (content)).Replace ("-", "").ToUpperInvariant ();

			DownloadUtils.VerifyChecksum (filePath, upperHash);
		}

		[TestMethod]
		public void VerifyChecksum_NonExistentFile_Throws ()
		{
			var filePath = Path.Combine (tempDir, "nonexistent.bin");
			Assert.ThrowsExactly<FileNotFoundException> (() =>
				DownloadUtils.VerifyChecksum (filePath, "abc123"));
		}

		// --- ExtractZipSafe tests ---

		[TestMethod]
		public void ExtractZipSafe_ValidZip_ExtractsFiles ()
		{
			var zipPath = Path.Combine (tempDir, "test.zip");
			var extractPath = Path.Combine (tempDir, "extracted");
			Directory.CreateDirectory (extractPath);

			using (var archive = ZipFile.Open (zipPath, ZipArchiveMode.Create)) {
				var entry = archive.CreateEntry ("subdir/hello.txt");
				using var writer = new StreamWriter (entry.Open ());
				writer.Write ("hello world");
			}

			DownloadUtils.ExtractZipSafe (zipPath, extractPath, CancellationToken.None);

			var extractedFile = Path.Combine (extractPath, "subdir", "hello.txt");
			Assert.IsTrue (File.Exists (extractedFile), "Extracted file should exist");
			Assert.AreEqual ("hello world", File.ReadAllText (extractedFile));
		}

		[TestMethod]
		[DataRow ("../relative.txt")]
		[DataRow ("../extracted-other/prefix.txt")]
		[DataRow ("../EXTRACTED/case.txt")]
		public void ExtractZipSafe_UnsafeRelativePath_Throws (string entryName)
		{
			var zipPath = Path.Combine (tempDir, "test.zip");
			var extractPath = Path.Combine (tempDir, "extracted");
			Directory.CreateDirectory (extractPath);

			using (var archive = ZipFile.Open (zipPath, ZipArchiveMode.Create)) {
				var entry = archive.CreateEntry (entryName);
				using var writer = new StreamWriter (entry.Open ());
				writer.Write ("foo");
			}

			var ex = Assert.ThrowsExactly<InvalidOperationException> (() =>
				DownloadUtils.ExtractZipSafe (zipPath, extractPath, CancellationToken.None));
			Assert.Contains ("outside target directory", ex!.Message);
		}

		[TestMethod]
		public void ExtractZipSafe_PlatformSeparatorTraversal_Throws ()
		{
			var zipPath = Path.Combine (tempDir, "test.zip");
			var extractPath = Path.Combine (tempDir, "extracted");
			Directory.CreateDirectory (extractPath);

			using (var archive = ZipFile.Open (zipPath, ZipArchiveMode.Create)) {
				var entry = archive.CreateEntry ($"..{Path.DirectorySeparatorChar}relative.txt");
				using var writer = new StreamWriter (entry.Open ());
				writer.Write ("foo");
			}

			Assert.ThrowsExactly<InvalidOperationException> (() =>
				DownloadUtils.ExtractZipSafe (zipPath, extractPath, CancellationToken.None));
		}

		[TestMethod]
		public void ExtractZipSafe_RootedPath_Throws ()
		{
			var zipPath = Path.Combine (tempDir, "test.zip");
			var extractPath = Path.Combine (tempDir, "extracted");
			Directory.CreateDirectory (extractPath);
			var pathRoot = Path.GetPathRoot (extractPath);
			Assert.IsNotNull (pathRoot);

			using (var archive = ZipFile.Open (zipPath, ZipArchiveMode.Create)) {
				var entry = archive.CreateEntry (Path.Combine (pathRoot, "rooted.txt"));
				using var writer = new StreamWriter (entry.Open ());
				writer.Write ("foo");
			}

			Assert.ThrowsExactly<InvalidOperationException> (() =>
				DownloadUtils.ExtractZipSafe (zipPath, extractPath, CancellationToken.None));
		}

		[TestMethod]
		public void ExtractZipSafe_TrailingSeparator_ExtractsFiles ()
		{
			var zipPath = Path.Combine (tempDir, "test.zip");
			var extractPath = Path.Combine (tempDir, "extracted") + Path.DirectorySeparatorChar;
			Directory.CreateDirectory (extractPath);

			using (var archive = ZipFile.Open (zipPath, ZipArchiveMode.Create)) {
				var entry = archive.CreateEntry ("hello.txt");
				using var writer = new StreamWriter (entry.Open ());
				writer.Write ("hello world");
			}

			DownloadUtils.ExtractZipSafe (zipPath, extractPath, CancellationToken.None);

			Assert.AreEqual ("hello world", File.ReadAllText (Path.Combine (extractPath, "hello.txt")));
		}

		[TestMethod]
		public void ExtractZipSafe_SymbolicLinkMetadata_ExtractsRegularFile ()
		{
			var zipPath = Path.Combine (tempDir, "test.zip");
			var extractPath = Path.Combine (tempDir, "extracted");
			Directory.CreateDirectory (extractPath);

			using (var archive = ZipFile.Open (zipPath, ZipArchiveMode.Create)) {
				var entry = archive.CreateEntry ("link");
				entry.ExternalAttributes = unchecked ((0xA000 | 0x1FF) << 16);
				using var writer = new StreamWriter (entry.Open ());
				writer.Write ("../outside.txt");
			}

			DownloadUtils.ExtractZipSafe (zipPath, extractPath, CancellationToken.None);

			var extractedFile = Path.Combine (extractPath, "link");
			Assert.IsTrue (File.Exists (extractedFile));
			Assert.IsFalse ((File.GetAttributes (extractedFile) & FileAttributes.ReparsePoint) != 0);
		}

		[TestMethod]
		public void ExtractZipSafe_EmptyZip_NoFilesExtracted ()
		{
			var zipPath = Path.Combine (tempDir, "empty.zip");
			var extractPath = Path.Combine (tempDir, "extracted");
			Directory.CreateDirectory (extractPath);

			using (ZipFile.Open (zipPath, ZipArchiveMode.Create)) { }

			DownloadUtils.ExtractZipSafe (zipPath, extractPath, CancellationToken.None);

			Assert.AreEqual (0, Directory.GetFiles (extractPath, "*", SearchOption.AllDirectories).Length);
		}

		[TestMethod]
		public void ExtractZipSafe_CancellationToken_Throws ()
		{
			var zipPath = Path.Combine (tempDir, "test.zip");
			var extractPath = Path.Combine (tempDir, "extracted");
			Directory.CreateDirectory (extractPath);

			using (var archive = ZipFile.Open (zipPath, ZipArchiveMode.Create)) {
				for (int i = 0; i < 10; i++) {
					var entry = archive.CreateEntry ($"file{i}.txt");
					using var writer = new StreamWriter (entry.Open ());
					writer.Write ($"content {i}");
				}
			}

			using var cts = new CancellationTokenSource ();
			cts.Cancel ();

			Assert.ThrowsExactly<OperationCanceledException> (() =>
				DownloadUtils.ExtractZipSafe (zipPath, extractPath, cts.Token));
		}
	}
}
