// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Threading;

using NUnit.Framework;

namespace Xamarin.Android.Tools.Tests
{
	[TestFixture]
	public class DownloadUtilsTests
	{
		string tempDir = null!;

		[SetUp]
		public void SetUp ()
		{
			tempDir = Path.Combine (Path.GetTempPath (), $"DownloadUtilsTests-{Guid.NewGuid ():N}");
			Directory.CreateDirectory (tempDir);
		}

		[TearDown]
		public void TearDown ()
		{
			if (Directory.Exists (tempDir))
				Directory.Delete (tempDir, recursive: true);
		}
		[Test]
		public void ParseChecksumFile_Null_ReturnsNull ()
		{
			Assert.IsNull (DownloadUtils.ParseChecksumFile (null!));
		}

		[Test]
		public void ParseChecksumFile_Empty_ReturnsNull ()
		{
			Assert.IsNull (DownloadUtils.ParseChecksumFile (""));
		}

		[Test]
		public void ParseChecksumFile_WhitespaceOnly_ReturnsNull ()
		{
			Assert.IsNull (DownloadUtils.ParseChecksumFile ("   \n\t  "));
		}

		[Test]
		public void ParseChecksumFile_HashOnly ()
		{
			Assert.AreEqual ("abc123def456", DownloadUtils.ParseChecksumFile ("abc123def456"));
		}

		[Test]
		public void ParseChecksumFile_HashOnly_WithTrailingNewline ()
		{
			Assert.AreEqual ("abc123def456", DownloadUtils.ParseChecksumFile ("abc123def456\n"));
		}

		[Test]
		public void ParseChecksumFile_HashAndFilename ()
		{
			// Standard sha256sum format: "<hash>  <filename>"
			Assert.AreEqual ("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
				DownloadUtils.ParseChecksumFile ("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855  microsoft-jdk-21-linux-x64.tar.gz"));
		}

		[Test]
		public void ParseChecksumFile_HashAndFilename_WithTab ()
		{
			Assert.AreEqual ("abc123", DownloadUtils.ParseChecksumFile ("abc123\tfilename.zip"));
		}

		[Test]
		public void ParseChecksumFile_MultipleLines_ReturnsFirstHash ()
		{
			var content = "abc123  file1.zip\ndef456  file2.zip\n";
			Assert.AreEqual ("abc123", DownloadUtils.ParseChecksumFile (content));
		}

		[Test]
		public void ParseChecksumFile_LeadingAndTrailingWhitespace ()
		{
			Assert.AreEqual ("abc123", DownloadUtils.ParseChecksumFile ("  abc123  filename.zip  \n"));
		}

		[TestCase ("abc123\r\n")]
		[TestCase ("abc123\r")]
		[TestCase ("abc123\n")]
		public void ParseChecksumFile_VariousLineEndings (string content)
		{
			Assert.AreEqual ("abc123", DownloadUtils.ParseChecksumFile (content));
		}

		// --- VerifyChecksum tests ---

		[Test]
		public void VerifyChecksum_MatchingHash_DoesNotThrow ()
		{
			var filePath = Path.Combine (tempDir, "test.bin");
			var content = new byte [] { 0x48, 0x65, 0x6c, 0x6c, 0x6f }; // "Hello"
			File.WriteAllBytes (filePath, content);

			using var sha = SHA256.Create ();
			var expected = BitConverter.ToString (sha.ComputeHash (content)).Replace ("-", "").ToLowerInvariant ();

			Assert.DoesNotThrow (() => DownloadUtils.VerifyChecksum (filePath, expected));
		}

		[Test]
		public void VerifyChecksum_MismatchedHash_Throws ()
		{
			var filePath = Path.Combine (tempDir, "test.bin");
			File.WriteAllBytes (filePath, new byte [] { 1, 2, 3 });

			var ex = Assert.Throws<InvalidOperationException> (() =>
				DownloadUtils.VerifyChecksum (filePath, "0000000000000000000000000000000000000000000000000000000000000000"));
			Assert.That (ex!.Message, Does.Contain ("Checksum verification failed"));
		}

		[Test]
		public void VerifyChecksum_CaseInsensitive ()
		{
			var filePath = Path.Combine (tempDir, "test.bin");
			var content = new byte [] { 0xFF };
			File.WriteAllBytes (filePath, content);

			using var sha = SHA256.Create ();
			var upperHash = BitConverter.ToString (sha.ComputeHash (content)).Replace ("-", "").ToUpperInvariant ();

			Assert.DoesNotThrow (() => DownloadUtils.VerifyChecksum (filePath, upperHash));
		}

		[Test]
		public void VerifyChecksum_NonExistentFile_Throws ()
		{
			var filePath = Path.Combine (tempDir, "nonexistent.bin");
			Assert.Throws<FileNotFoundException> (() =>
				DownloadUtils.VerifyChecksum (filePath, "abc123"));
		}

		// --- ExtractZipSafe tests ---

		[Test]
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

		[Test]
		public void ExtractZipSafe_ZipSlip_Throws ()
		{
			var zipPath = Path.Combine (tempDir, "evil.zip");
			var extractPath = Path.Combine (tempDir, "extracted");
			Directory.CreateDirectory (extractPath);

			using (var archive = ZipFile.Open (zipPath, ZipArchiveMode.Create)) {
				// Create an entry with a path traversal
				var entry = archive.CreateEntry ("../evil.txt");
				using var writer = new StreamWriter (entry.Open ());
				writer.Write ("malicious");
			}

			var ex = Assert.Throws<InvalidOperationException> (() =>
				DownloadUtils.ExtractZipSafe (zipPath, extractPath, CancellationToken.None));
			Assert.That (ex!.Message, Does.Contain ("outside target directory"));
		}

		[Test]
		public void ExtractZipSafe_EmptyZip_NoFilesExtracted ()
		{
			var zipPath = Path.Combine (tempDir, "empty.zip");
			var extractPath = Path.Combine (tempDir, "extracted");
			Directory.CreateDirectory (extractPath);

			using (ZipFile.Open (zipPath, ZipArchiveMode.Create)) { }

			DownloadUtils.ExtractZipSafe (zipPath, extractPath, CancellationToken.None);

			Assert.AreEqual (0, Directory.GetFiles (extractPath, "*", SearchOption.AllDirectories).Length);
		}

		[Test]
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

			Assert.Throws<OperationCanceledException> (() =>
				DownloadUtils.ExtractZipSafe (zipPath, extractPath, cts.Token));
		}
	}
}
