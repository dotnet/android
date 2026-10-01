// https://github.com/dotnet/android/blob/34acbbae6795854cc4e9f8eb7167ab011e0266b4/src/Xamarin.Android.Build.Tasks/Tests/Xamarin.Android.Build.Tests/MonoAndroidHelperTests.cs
// https://github.com/dotnet/android/blob/799506a9dfb746b8bdc8a4ab77e19eee875f00e3/src/Xamarin.Android.Build.Tasks/Tests/Xamarin.Android.Build.Tests/FilesTests.cs

using NUnit.Framework;
using System;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Android.Build.Tasks;

namespace Microsoft.Android.Build.BaseTasks.Tests
{
	[TestFixture]
	public class FilesTests
	{
		bool IsWindows = RuntimeInformation.IsOSPlatform (OSPlatform.Windows);
		const int MaxFileName = 255;

		static readonly Encoding encoding = new UTF8Encoding (false);
		string tempDir;
		MemoryStream stream;

		[SetUp]
		public void SetUp ()
		{
			tempDir = Path.Combine (TestContext.CurrentContext.WorkDirectory, "Microsoft.Android.Build.BaseTasks.Tests", TestContext.CurrentContext.Test.ID);
			stream = new MemoryStream ();
		}

		[TearDown]
		public void TearDown ()
		{
			stream.Dispose ();

			var dir = Files.ToLongPath (tempDir);
			if (Directory.Exists (dir))
				Directory.Delete (dir, recursive: true);
		}

		[Test]
		public void ToLongPathIsIdempotent ()
		{
			if (!IsWindows) {
				Assert.Ignore ("Long path prefixes only apply on Windows.");
				return;
			}

			var path = Path.GetFullPath (tempDir);
			var longPath = Files.ToLongPath (path);

			Assert.AreEqual (Files.LongPathPrefix + path, longPath, "Long path prefix should be added.");
			Assert.AreEqual (longPath, Files.ToLongPath (longPath), "Long path prefix should not be added twice.");
		}

		[Test]
		public void CopyIfStringChanged ()
		{
			Directory.CreateDirectory (tempDir);
			var tempFile = Path.Combine (tempDir, "foo.txt");

			var foo = "bar";
			Assert.IsTrue (Files.CopyIfStringChanged (foo, tempFile), "Should write on new file.");
			FileAssert.Exists (tempFile);
			Assert.IsFalse (Files.CopyIfStringChanged (foo, tempFile), "Should *not* write unless changed.");
			foo += "\n";
			Assert.IsTrue (Files.CopyIfStringChanged (foo, tempFile), "Should write when changed.");
		}

		[Test]
		public void CopyIfBytesChanged ()
		{
			Directory.CreateDirectory (tempDir);
			var tempFile = Path.Combine (tempDir, "foo.bin");

			var foo = new byte [32];
			Assert.IsTrue (Files.CopyIfBytesChanged (foo, tempFile), "Should write on new file.");
			FileAssert.Exists (tempFile);
			Assert.IsFalse (Files.CopyIfBytesChanged (foo, tempFile), "Should *not* write unless changed.");
			foo [0] = 0xFF;
			Assert.IsTrue (Files.CopyIfBytesChanged (foo, tempFile), "Should write when changed.");
		}

		[Test]
		public void CopyIfStreamChanged ()
		{
			Directory.CreateDirectory (tempDir);
			var tempFile = Path.Combine (tempDir, "foo.txt");

			using (var foo = new MemoryStream ())
			using (var writer = new StreamWriter (foo)) {
				writer.WriteLine ("bar");
				writer.Flush ();

				Assert.IsTrue (Files.CopyIfStreamChanged (foo, tempFile), "Should write on new file.");
				FileAssert.Exists (tempFile);
				Assert.IsFalse (Files.CopyIfStreamChanged (foo, tempFile), "Should *not* write unless changed.");
				writer.WriteLine ();
				writer.Flush ();
				Assert.IsTrue (Files.CopyIfStreamChanged (foo, tempFile), "Should write when changed.");
			}
		}

		[Test]
		public void CopyIfStringChanged_NewDirectory ()
		{
			Directory.CreateDirectory (tempDir);
			var tempFile = Path.Combine (tempDir, "foo.txt");

			var foo = "bar";
			Assert.IsTrue (Files.CopyIfStringChanged (foo, tempFile), "Should write on new file.");
			FileAssert.Exists (tempFile);
		}

		[Test]
		public void CopyIfBytesChanged_NewDirectory ()
		{
			Directory.CreateDirectory (tempDir);
			var tempFile = Path.Combine (tempDir, "foo.bin");

			var foo = new byte [32];
			Assert.IsTrue (Files.CopyIfBytesChanged (foo, tempFile), "Should write on new file.");
			FileAssert.Exists (tempFile);
		}

		[Test]
		public void CopyIfStreamChanged_NewDirectory ()
		{
			Directory.CreateDirectory (tempDir);
			var tempFile = Path.Combine (tempDir, "foo.txt");

			using (var foo = new MemoryStream ())
			using (var writer = new StreamWriter (foo)) {
				writer.WriteLine ("bar");
				writer.Flush ();

				Assert.IsTrue (Files.CopyIfStreamChanged (foo, tempFile), "Should write on new file.");
				FileAssert.Exists (tempFile);
			}
		}

		[Test]
		public void CopyIfBytesChanged_Readonly ()
		{
			Directory.CreateDirectory (tempDir);
			var tempFile = Path.Combine (tempDir, "foo.bin");

			if (File.Exists (tempFile)) {
				File.SetAttributes (tempFile, FileAttributes.Normal);
			}
			File.WriteAllText (tempFile, "");
			File.SetAttributes (tempFile, FileAttributes.ReadOnly);

			var foo = new byte [32];
			Assert.IsTrue (Files.CopyIfBytesChanged (foo, tempFile), "Should write on new file.");
			FileAssert.Exists (tempFile);
		}

		[Test]
		public void CleanBOM_Readonly ()
		{
			var encoding = Encoding.UTF8;
			Directory.CreateDirectory (tempDir);
			var tempFile = Path.Combine (tempDir, "foo.txt");
			if (File.Exists (tempFile)) {
				File.SetAttributes (tempFile, FileAttributes.Normal);
			}
			using (var stream = File.Create (tempFile))
			using (var writer = new StreamWriter (stream, encoding)) {
				writer.Write ("This will have a BOM");
			}
			File.SetAttributes (tempFile, FileAttributes.ReadOnly);
			var before = File.ReadAllBytes (tempFile);
			Files.CleanBOM (tempFile);
			var after = File.ReadAllBytes (tempFile);
			var preamble = encoding.GetPreamble ();
			Assert.AreEqual (before.Length, after.Length + preamble.Length, "BOM should be removed!");
		}

		[Test]
		public void CopyIfStreamChanged_MemoryStreamPool_StreamWriter ()
		{
			Directory.CreateDirectory (tempDir);
			var tempFile = Path.Combine (tempDir, "foo.txt");

			var pool = new MemoryStreamPool ();
			var expected = pool.Rent ();
			pool.Return (expected);

			using (var writer = pool.CreateStreamWriter ()) {
				writer.WriteLine ("bar");
				writer.Flush ();

				Assert.IsTrue (Files.CopyIfStreamChanged (writer.BaseStream, tempFile), "Should write on new file.");
				FileAssert.Exists (tempFile);
			}

			var actual = pool.Rent ();
			Assert.AreSame (expected, actual);
			Assert.AreEqual (0, actual.Length);
		}

		[Test]
		public void CopyIfStreamChanged_MemoryStreamPool_BinaryWriter ()
		{
			Directory.CreateDirectory (tempDir);
			var tempFile = Path.Combine (tempDir, "foo.bin");

			var pool = new MemoryStreamPool ();
			var expected = pool.Rent ();
			pool.Return (expected);

			using (var writer = pool.CreateBinaryWriter ()) {
				writer.Write (42);
				writer.Flush ();

				Assert.IsTrue (Files.CopyIfStreamChanged (writer.BaseStream, tempFile), "Should write on new file.");
				FileAssert.Exists (tempFile);
			}

			var actual = pool.Rent ();
			Assert.AreSame (expected, actual);
			Assert.AreEqual (0, actual.Length);
		}

		[Test]
		public void SetWriteable ()
		{
			Directory.CreateDirectory (tempDir);
			var tempFile = Path.Combine (tempDir, "foo.txt");

			File.WriteAllText (tempFile, contents: "foo");
			File.SetAttributes (tempFile, FileAttributes.ReadOnly);

			Files.SetWriteable (tempFile);

			var attributes = File.GetAttributes (tempFile);
			Assert.AreEqual (FileAttributes.Normal, attributes);
			File.WriteAllText (tempFile, contents: "bar");
		}

		[Test]
		public void SetDirectoryWriteable ()
		{
			Directory.CreateDirectory (tempDir);
			try {
				var directoryInfo = new DirectoryInfo (tempDir);
				directoryInfo.Attributes |= FileAttributes.ReadOnly;
				Files.SetDirectoryWriteable (tempDir);

				directoryInfo = new DirectoryInfo (tempDir);
				Assert.AreEqual (FileAttributes.Directory, directoryInfo.Attributes);
			} finally {
				Directory.Delete (tempDir);
			}
		}

		void AssertFile (string path, string contents)
		{
			var fullPath = Path.Combine (tempDir, path);
			FileAssert.Exists (fullPath);
			Assert.AreEqual (contents, File.ReadAllText (fullPath), $"Contents did not match at path: {path}");
		}

		void AssertFileDoesNotExist (string path)
		{
			FileAssert.DoesNotExist (Path.Combine (tempDir, path));
		}

		bool ExtractAll (MemoryStream stream)
		{
			stream.Position = 0;
			using (var zip = new ZipArchive (stream, ZipArchiveMode.Read, leaveOpen: true)) {
				return Files.ExtractAll (zip, tempDir);
			}
		}

		static void WriteEntry (ZipArchive zip, string name, string contents, DateTimeOffset? timestamp = null)
		{
			var entry = zip.CreateEntry (name);
			if (timestamp.HasValue)
				entry.LastWriteTime = timestamp.Value;
			using var writer = new StreamWriter (entry.Open (), encoding);
			writer.Write (contents);
		}

		string NewFile (string contents = null, string fileName = "")
		{
			if (string.IsNullOrEmpty (fileName)) {
				fileName = Path.GetRandomFileName ();
			}
			var path = Path.Combine (tempDir, fileName);
			if (!string.IsNullOrEmpty (contents)) {
				Directory.CreateDirectory (Path.GetDirectoryName (path));
				if (IsWindows && path.Length >= Files.MaxPath) {
					File.WriteAllText (Files.ToLongPath (path), contents);
				} else {
					File.WriteAllText (path, contents);
				}
			}
			return path;
		}

		Stream NewStream (string contents) => new MemoryStream (Encoding.Default.GetBytes (contents));

		[Test]
		public void CopyIfChanged_NoChanges ()
		{
			var src = NewFile ("foo");
			var dest = NewFile ("foo");
			Assert.IsFalse (Files.CopyIfChanged (src, dest), "No change should have occurred");
			FileAssert.AreEqual (src, dest);
		}

		[Test]
		public void CopyIfChanged_NoExist ()
		{
			var src = NewFile ("foo");
			var dest = NewFile ();
			Assert.IsTrue (Files.CopyIfChanged (src, dest), "Changes should have occurred");
			FileAssert.AreEqual (src, dest);
		}

		[Test]
		public void CopyIfChanged_LongPath ()
		{
			var src = NewFile (contents: "foo");
			var dest = NewFile (contents: "bar", fileName: "bar".PadRight (MaxFileName, 'N'));
			dest = Files.ToLongPath (dest);
			Assert.IsTrue (Files.CopyIfChanged (src, dest), "Changes should have occurred");
			FileAssert.AreEqual (src, dest);
		}

		[Test]
		public void CopyIfChanged_Changes ()
		{
			var src = NewFile ("foo");
			var dest = NewFile ("bar");
			Assert.IsTrue (Files.CopyIfChanged (src, dest), "Changes should have occurred");
			FileAssert.AreEqual (src, dest);
		}

		[Test]
		public void CopyIfChanged_Readonly ()
		{
			var src = NewFile ("foo");
			var dest = NewFile ("bar");
			File.SetAttributes (dest, FileAttributes.ReadOnly);
			Assert.IsTrue (Files.CopyIfChanged (src, dest), "Changes should have occurred");
			FileAssert.AreEqual (src, dest);
		}

		[Test]
		public void CopyIfChanged_CasingChange ()
		{
			var src = NewFile (contents: "foo");
			var dest = NewFile (contents: "Foo", fileName: "foo");
			dest = dest.Replace ("foo", "Foo");
			Assert.IsTrue (Files.CopyIfChanged (src, dest), "Changes should have occurred");
			FileAssert.AreEqual (src, dest);

			var files = Directory.GetFiles (Path.GetDirectoryName (dest), "Foo");
			Assert.AreEqual ("Foo", Path.GetFileName (files [0]));
		}

		[Test]
		public void CopyIfStringChanged_NoChanges ()
		{
			var dest = NewFile ("foo");
			Assert.IsFalse (Files.CopyIfStringChanged ("foo", dest), "No change should have occurred");
			FileAssert.Exists (dest);
		}

		[Test]
		public void CopyIfStringChanged_NoExist ()
		{
			var dest = NewFile ();
			Assert.IsTrue (Files.CopyIfStringChanged ("foo", dest), "Changes should have occurred");
			FileAssert.Exists (dest);
		}

		[Test]
		public void CopyIfStringChanged_LongPath ()
		{
			var dest = NewFile (fileName: "bar".PadRight (MaxFileName, 'N'));
			dest = Files.ToLongPath (dest);
			Assert.IsTrue (Files.CopyIfStringChanged ("foo", dest), "Changes should have occurred");
			FileAssert.Exists (dest);
		}

		[Test]
		public void CopyIfStringChanged_Changes ()
		{
			var dest = NewFile ("bar");
			Assert.IsTrue (Files.CopyIfStringChanged ("foo", dest), "Changes should have occurred");
			FileAssert.Exists (dest);
		}

		[Test]
		public void CopyIfStringChanged_Readonly ()
		{
			var dest = NewFile ("bar");
			File.SetAttributes (dest, FileAttributes.ReadOnly);
			Assert.IsTrue (Files.CopyIfStringChanged ("foo", dest), "Changes should have occurred");
			FileAssert.Exists (dest);
		}

		[Test]
		public void CopyIfStringChanged_CasingChange ()
		{
			var dest = NewFile (contents: "foo", fileName: "foo");
			dest = dest.Replace ("foo", "Foo");
			Assert.IsTrue (Files.CopyIfStringChanged ("Foo", dest), "Changes should have occurred");
			FileAssert.Exists (dest);
			Assert.AreEqual ("Foo", File.ReadAllText (dest), "File contents should match");

			var files = Directory.GetFiles (Path.GetDirectoryName (dest), "Foo");
			Assert.AreEqual ("Foo", Path.GetFileName (files [0]), "File name should match");
		}

		[Test]
		public void CopyIfStreamChanged_NoChanges ()
		{
			using (var src = NewStream ("foo")) {
				var dest = NewFile ("foo");
				Assert.IsFalse (Files.CopyIfStreamChanged (src, dest), "No change should have occurred");
				FileAssert.Exists (dest);
			}
		}

		[Test]
		public void CopyIfStreamChanged_LongPath ()
		{
			using (var src = NewStream ("foo")) {
				var dest = NewFile (fileName: "bar".PadRight (MaxFileName, 'N'));
				dest = Files.ToLongPath (dest);
				Assert.IsTrue (Files.CopyIfStreamChanged (src, dest), "Changes should have occurred");
				FileAssert.Exists (dest);
			}
		}

		[Test]
		public void CopyIfStreamChanged_NoExist ()
		{
			using (var src = NewStream ("foo")) {
				var dest = NewFile ();
				Assert.IsTrue (Files.CopyIfStreamChanged (src, dest), "Changes should have occurred");
				FileAssert.Exists (dest);
			}
		}

		[Test]
		public void CopyIfStreamChanged_Changes ()
		{
			using (var src = NewStream ("foo")) {
				var dest = NewFile ("bar");
				Assert.IsTrue (Files.CopyIfStreamChanged (src, dest), "Changes should have occurred");
				FileAssert.Exists (dest);
			}
		}

		[Test]
		public void CopyIfStreamChanged_Readonly ()
		{
			using (var src = NewStream ("foo")) {
				var dest = NewFile ("bar");
				File.SetAttributes (dest, FileAttributes.ReadOnly);
				Assert.IsTrue (Files.CopyIfStreamChanged (src, dest), "Changes should have occurred");
				FileAssert.Exists (dest);
			}
		}

		[Test]
		public void CopyIfStreamChanged_CasingChange ()
		{
			using (var src = NewStream ("Foo")) {
				var dest = NewFile (contents: "foo", fileName: "foo");
				dest = dest.Replace ("foo", "Foo");
				Assert.IsTrue (Files.CopyIfStreamChanged (src, dest), "Changes should have occurred");
				FileAssert.Exists (dest);
				Assert.AreEqual ("Foo", File.ReadAllText (dest), "File contents should match");

				var files = Directory.GetFiles (Path.GetDirectoryName (dest), "Foo");
				Assert.AreEqual ("Foo", Path.GetFileName (files [0]), "File name should match");
			}
		}

		[Test]
		public async Task CopyIfChanged_LockedFile ()
		{
			var dest = NewFile (contents: "foo", fileName: "foo_locked");
			var src = NewFile (contents: "foo0", fileName: "foo");
			using (var file = File.OpenWrite (dest)) {
				Assert.Throws<IOException> (() => Files.CopyIfChanged (src, dest));
			}
			src = NewFile (contents: "foo1", fileName: "foo");
			Assert.IsTrue (Files.CopyIfChanged (src, dest));
			src = NewFile (contents: "foo2", fileName: "foo");
			dest = NewFile (contents: "foo", fileName: "foo_locked2");
			var ev = new ManualResetEvent (false);
			var task = Task.Run (async () => {
				var file = File.Open (dest, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read);
				try {
					ev.Set ();
					await Task.Delay (2500);
				} finally {
					file.Close();
					file.Dispose ();
				}
			});
			ev.WaitOne ();
			Assert.IsTrue (Files.CopyIfChanged (src, dest));
			await task;
		}

		[Test]
		public void ExtractAll ()
		{
			using (var zip = new ZipArchive (stream, ZipArchiveMode.Create, leaveOpen: true)) {
				WriteEntry (zip, "a.txt", "a");
				WriteEntry (zip, "b/b.txt", "b");
			}

			bool changes = ExtractAll (stream);

			Assert.IsTrue (changes, "ExtractAll should report changes.");
			AssertFile ("a.txt", "a");
			AssertFile (Path.Combine ("b", "b.txt"), "b");
		}

		[Test]
		public void ExtractAll_NoChanges ()
		{
			using (var zip = new ZipArchive (stream, ZipArchiveMode.Create, leaveOpen: true)) {
				WriteEntry (zip, "a.txt", "a");
				WriteEntry (zip, "b/b.txt", "b");
			}

			bool changes = ExtractAll (stream);
			Assert.IsTrue (changes, "ExtractAll should report changes.");

			stream.SetLength (0);
			stream.Position = 0;
			using (var zip = new ZipArchive (stream, ZipArchiveMode.Create, leaveOpen: true)) {
				WriteEntry (zip, "a.txt", "a");
				WriteEntry (zip, "b/b.txt", "b");
			}

			changes = ExtractAll (stream);

			Assert.IsFalse (changes, "ExtractAll should *not* report changes.");
			AssertFile ("a.txt", "a");
			AssertFile (Path.Combine ("b", "b.txt"), "b");
		}

		[Test]
		public void ExtractAll_NewFile ()
		{
			using (var zip = new ZipArchive (stream, ZipArchiveMode.Create, leaveOpen: true)) {
				WriteEntry (zip, "a.txt", "a");
				WriteEntry (zip, "b/b.txt", "b");
			}

			bool changes = ExtractAll (stream);
			Assert.IsTrue (changes, "ExtractAll should report changes.");

			stream.SetLength (0);
			stream.Position = 0;
			using (var zip = new ZipArchive (stream, ZipArchiveMode.Create, leaveOpen: true)) {
				WriteEntry (zip, "a.txt", "a");
				WriteEntry (zip, "b/b.txt", "b");
				WriteEntry (zip, "c/c.txt", "c");
			}

			changes = ExtractAll (stream);

			Assert.IsTrue (changes, "ExtractAll should report changes.");
			AssertFile ("a.txt", "a");
			AssertFile (Path.Combine ("b", "b.txt"), "b");
			AssertFile (Path.Combine ("c", "c.txt"), "c");
		}

		[Test]
		public void ExtractAll_FileChanged ()
		{
			var timestamp = new DateTimeOffset (2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
			using (var zip = new ZipArchive (stream, ZipArchiveMode.Create, leaveOpen: true)) {
				WriteEntry (zip, "foo.txt", "foo", timestamp);
			}

			bool changes = ExtractAll (stream);
			Assert.IsTrue (changes, "ExtractAll should report changes.");

			stream.SetLength (0);
			stream.Position = 0;
			using (var zip = new ZipArchive (stream, ZipArchiveMode.Create, leaveOpen: true)) {
				WriteEntry (zip, "foo.txt", "bar", timestamp);
			}

			changes = ExtractAll (stream);

			Assert.IsTrue (changes, "ExtractAll should report changes.");
			AssertFile ("foo.txt", "bar");
		}

		[Test]
		public void ExtractAll_ReusesArchiveStreamWithoutRewritingUnchangedFiles ()
		{
			using (var zip = new ZipArchive (stream, ZipArchiveMode.Create, leaveOpen: true)) {
				WriteEntry (zip, "a.txt", "contents");
				zip.CreateEntry ("empty/");
			}
			Assert.IsTrue (ExtractAll (stream));
			Assert.IsTrue (stream.CanRead, "Disposing the archive must leave its reusable stream open.");
			var file = Path.Combine (tempDir, "a.txt");
			var timestamp = new DateTime (2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
			File.SetLastWriteTimeUtc (file, timestamp);

			Assert.IsFalse (ExtractAll (stream));
			Assert.IsTrue (stream.CanRead);
			Assert.AreEqual (timestamp, File.GetLastWriteTimeUtc (file), "Unchanged extracted files should retain their timestamps.");
			AssertFile ("a.txt", "contents");
			DirectoryAssert.DoesNotExist (Path.Combine (tempDir, "empty"));
		}

		[Test]
		public void ExtractAll_FileDeleted ()
		{
			using (var zip = new ZipArchive (stream, ZipArchiveMode.Create, leaveOpen: true)) {
				WriteEntry (zip, "a.txt", "a");
				WriteEntry (zip, "b/b.txt", "b");
			}

			bool changes = ExtractAll (stream);
			Assert.IsTrue (changes, "ExtractAll should report changes.");

			stream.SetLength (0);
			stream.Position = 0;
			using (var zip = new ZipArchive (stream, ZipArchiveMode.Create, leaveOpen: true)) {
				WriteEntry (zip, "a.txt", "a");
			}

			changes = ExtractAll (stream);

			Assert.IsTrue (changes, "ExtractAll should report changes.");
			AssertFile ("a.txt", "a");
			FileAssert.DoesNotExist (Path.Combine (tempDir, "b", "b.txt"));
		}

		[Test]
		public void ExtractAll_ModifyCallback ()
		{
			using (var zip = new ZipArchive (stream, ZipArchiveMode.Create, leaveOpen: true)) {
				WriteEntry (zip, "foo/a.txt", "a");
				WriteEntry (zip, "foo/b/b.txt", "b");
			}

			stream.Position = 0;
			using (var zip = new ZipArchive (stream, ZipArchiveMode.Read, leaveOpen: true)) {
				bool changes = Files.ExtractAll (zip, tempDir, modifyCallback: e => e.Replace ("foo/", ""));
				Assert.IsTrue (changes, "ExtractAll should report changes.");
			}

			AssertFile ("a.txt", "a");
			AssertFile (Path.Combine ("b", "b.txt"), "b");
		}

		[Test]
		public void ExtractAll_SkipCallback ()
		{
			using (var zip = new ZipArchive (stream, ZipArchiveMode.Create, leaveOpen: true)) {
				WriteEntry (zip, "a.txt", "a");
				WriteEntry (zip, "b/b.txt", "b");
			}

			stream.Position = 0;
			using (var zip = new ZipArchive (stream, ZipArchiveMode.Read, leaveOpen: true)) {
				bool changes = Files.ExtractAll (zip, tempDir, skipCallback: e => e == "a.txt");
				Assert.IsTrue (changes, "ExtractAll should report changes.");
			}

			AssertFileDoesNotExist ("a.txt");
			AssertFile (Path.Combine ("b", "b.txt"), "b");
		}

		[Test]
		public void ExtractAll_MacOSFiles ()
		{
			using (var zip = new ZipArchive (stream, ZipArchiveMode.Create, leaveOpen: true)) {
				WriteEntry (zip, "a/.DS_Store", "a");
				WriteEntry (zip, "b/__MACOSX/b.txt", "b");
				WriteEntry (zip, "c/__MACOSX", "c");
			}

			bool changes = ExtractAll (stream);
			Assert.IsFalse (changes, "ExtractAll should *not* report changes.");
			DirectoryAssert.DoesNotExist (tempDir);
		}

		[Test]
		public void ExtractAll_SkipsPathTraversal ()
		{
			using (var zip = new ZipArchive (stream, ZipArchiveMode.Create, leaveOpen: true)) {
				WriteEntry (zip, "a.txt", "a");
			}

			var destinationDir = Path.Combine (tempDir, "dest");
			stream.Position = 0;
			using (var zip = new ZipArchive (stream, ZipArchiveMode.Read, leaveOpen: true)) {
				// modifyCallback introduces a path traversal
				bool changes = Files.ExtractAll (zip, destinationDir, modifyCallback: e => "../" + e);
				Assert.IsFalse (changes, "ExtractAll should not report changes for skipped entries.");
			}
			FileAssert.DoesNotExist (Path.Combine (tempDir, "a.txt"));
		}

		[Test]
		public void ExtractAll_SkipsPathTraversal_ExtractsValidEntries ()
		{
			using (var zip = new ZipArchive (stream, ZipArchiveMode.Create, leaveOpen: true)) {
				WriteEntry (zip, "good.txt", "good");
				WriteEntry (zip, "relative.txt", "relative");
			}

			var destinationDir = Path.Combine (tempDir, "dest");
			stream.Position = 0;
			using (var zip = new ZipArchive (stream, ZipArchiveMode.Read, leaveOpen: true)) {
				// Only relative.txt gets a traversal prefix
				bool changes = Files.ExtractAll (zip, destinationDir, modifyCallback: e =>
					e == "relative.txt" ? "../" + e : e);
				Assert.IsTrue (changes, "ExtractAll should report changes for the valid entry.");
			}
			AssertFile (Path.Combine ("dest", "good.txt"), "good");
			FileAssert.DoesNotExist (Path.Combine (tempDir, "relative.txt"));
		}

		[TestCase ("../../")]
		[TestCase ("foo/../../../")]
		public void ExtractAll_SkipsPathTraversal_ForwardSlash (string prefix)
		{
			using (var zip = new ZipArchive (stream, ZipArchiveMode.Create, leaveOpen: true)) {
				WriteEntry (zip, "a.txt", "a");
			}

			var destinationDir = Path.Combine (tempDir, "dest");
			stream.Position = 0;
			using (var zip = new ZipArchive (stream, ZipArchiveMode.Read, leaveOpen: true)) {
				bool changes = Files.ExtractAll (zip, destinationDir, modifyCallback: e => prefix + e);
				Assert.IsFalse (changes, $"Entry with prefix '{prefix}' should be skipped.");
			}
		}

		[TestCase ("..\\")]
		[TestCase ("..\\..\\")]
		[Platform ("Win")]
		public void ExtractAll_SkipsPathTraversal_BackSlash (string prefix)
		{
			using (var zip = new ZipArchive (stream, ZipArchiveMode.Create, leaveOpen: true)) {
				WriteEntry (zip, "a.txt", "a");
			}

			var destinationDir = Path.Combine (tempDir, "dest");
			stream.Position = 0;
			using (var zip = new ZipArchive (stream, ZipArchiveMode.Read, leaveOpen: true)) {
				bool changes = Files.ExtractAll (zip, destinationDir, modifyCallback: e => prefix + e);
				Assert.IsFalse (changes, $"Entry with prefix '{prefix}' should be skipped.");
			}
		}

		[Test]
		public void ToHashString ()
		{
			var bytes = new byte [] { 0x12, 0x34, 0x56, 0x78, 0x90, 0xAB, 0xCD, 0xEF };
			var expected = BitConverter.ToString (bytes).Replace ("-", string.Empty);
			Assert.AreEqual (expected, Files.ToHexString (bytes));
		}

		[Test]
		public void CopyIfZipChanged_Stream ()
		{
			Directory.CreateDirectory (tempDir);
			var destination = Path.Combine (tempDir, "dest.zip");

			using (var zip = new ZipArchive (stream, ZipArchiveMode.Create, leaveOpen: true)) {
				WriteEntry (zip, "a.txt", "a");
			}
			stream.Position = 0;

			Assert.IsTrue (Files.CopyIfZipChanged (stream, destination), "Should copy on new file.");
			FileAssert.Exists (destination);
			stream.Position = 0;
			Assert.IsFalse (Files.CopyIfZipChanged (stream, destination), "Should *not* copy when unchanged.");
		}

		[Test]
		public void CopyIfZipChanged_ChangedSameSizeEntry ()
		{
			Directory.CreateDirectory (tempDir);
			var destination = Path.Combine (tempDir, "dest.zip");
			var timestamp = new DateTimeOffset (2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
			using (var zip = new ZipArchive (stream, ZipArchiveMode.Create, leaveOpen: true)) {
				WriteEntry (zip, "a.txt", "before", timestamp);
			}
			stream.Position = 0;
			Assert.IsTrue (Files.CopyIfZipChanged (stream, destination));

			stream.Position = 0;
			using (var zip = new ZipArchive (stream, ZipArchiveMode.Update, leaveOpen: true)) {
				var entry = zip.GetEntry ("a.txt") ?? throw new InvalidOperationException ("Missing a.txt.");
				entry.Delete ();
				WriteEntry (zip, "a.txt", "after!", timestamp);
			}
			stream.Position = 0;
			Assert.IsTrue (Files.CopyIfZipChanged (stream, destination), "CRC changes must be detected even when entry length and timestamp match.");
			Assert.IsTrue (stream.CanRead);
			stream.Position = 0;
			Assert.IsFalse (Files.CopyIfZipChanged (stream, destination), "A subsequent unchanged copy should be skipped.");

			using var archive = Files.ReadZipFile (destination);
			var copied = archive.GetEntry ("a.txt") ?? throw new InvalidOperationException ("Missing a.txt.");
			using var reader = new StreamReader (copied.Open ());
			Assert.AreEqual ("after!", reader.ReadToEnd ());
		}

		[Test]
		public void CopyIfZipChanged_AddedAndRemovedEntries ()
		{
			Directory.CreateDirectory (tempDir);
			var destination = Path.Combine (tempDir, "dest.zip");
			using (var zip = new ZipArchive (stream, ZipArchiveMode.Create, leaveOpen: true)) {
				WriteEntry (zip, "a.txt", "a");
			}
			stream.Position = 0;
			Assert.IsTrue (Files.CopyIfZipChanged (stream, destination));

			stream.Position = 0;
			using (var zip = new ZipArchive (stream, ZipArchiveMode.Update, leaveOpen: true)) {
				WriteEntry (zip, "b.txt", "b");
			}
			stream.Position = 0;
			Assert.IsTrue (Files.CopyIfZipChanged (stream, destination), "Adding an entry must be detected.");

			stream.Position = 0;
			using (var zip = new ZipArchive (stream, ZipArchiveMode.Update, leaveOpen: true)) {
				var entry = zip.GetEntry ("a.txt") ?? throw new InvalidOperationException ("Missing a.txt.");
				entry.Delete ();
			}
			stream.Position = 0;
			Assert.IsTrue (Files.CopyIfZipChanged (stream, destination), "Removing an entry must be detected.");
			Assert.IsFalse (Files.ZipAny (destination, entry => entry.FullName == "a.txt"));
			Assert.IsTrue (Files.ZipAny (destination, entry => entry.FullName == "b.txt"));
		}

		[Test]
		public void CopyIfZipChanged_IgnoresCompressionAndTimestampChanges ()
		{
			Directory.CreateDirectory (tempDir);
			var destination = Path.Combine (tempDir, "dest.zip");
			var contents = new string ('A', 4096);
			using (var zip = new ZipArchive (stream, ZipArchiveMode.Create, leaveOpen: true)) {
				WriteEntry (zip, "a.txt", contents, new DateTimeOffset (2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
			}
			stream.Position = 0;
			Assert.IsTrue (Files.CopyIfZipChanged (stream, destination));
			var bytes = File.ReadAllBytes (destination);
			var timestamp = new DateTime (2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
			File.SetLastWriteTimeUtc (destination, timestamp);

			stream.SetLength (0);
			stream.Position = 0;
			using (var zip = new ZipArchive (stream, ZipArchiveMode.Create, leaveOpen: true)) {
				var entry = zip.CreateEntry ("a.txt", CompressionLevel.NoCompression);
				entry.LastWriteTime = new DateTimeOffset (2026, 1, 2, 12, 0, 0, TimeSpan.Zero);
				using var writer = new StreamWriter (entry.Open (), encoding);
				writer.Write (contents);
			}
			stream.Position = 0;
			Assert.IsFalse (Files.CopyIfZipChanged (stream, destination), "Equivalent uncompressed contents should not force a copy.");
			CollectionAssert.AreEqual (bytes, File.ReadAllBytes (destination));
			Assert.AreEqual (timestamp, File.GetLastWriteTimeUtc (destination));
		}

		[TestCase (CompressionLevel.NoCompression)]
		[TestCase (CompressionLevel.Optimal)]
		public void GetZipEntryCrc32 (CompressionLevel compression)
		{
			using (var zip = new ZipArchive (stream, ZipArchiveMode.Create, leaveOpen: true)) {
				var entry = zip.CreateEntry ("crc.txt", compression);
				using var writer = new StreamWriter (entry.Open (), encoding);
				writer.Write ("123456789");
			}
			stream.Position = 0;
			using (var zip = new ZipArchive (stream, ZipArchiveMode.Read, leaveOpen: true)) {
				var entry = zip.GetEntry ("crc.txt") ?? throw new InvalidOperationException ("Missing crc.txt.");
				Assert.AreEqual (0xCBF43926u, Files.GetZipEntryCrc32 (entry));
			}
			Assert.IsTrue (stream.CanRead);
		}

		[TestCase (CompressionLevel.NoCompression)]
		[TestCase (CompressionLevel.Optimal)]
		public void GetZipEntryCrc32_ReadsMetadataWithoutOpeningPayload (CompressionLevel compression)
		{
			using (var zip = new ZipArchive (stream, ZipArchiveMode.Create, leaveOpen: true)) {
				var entry = zip.CreateEntry ("crc.txt", compression);
				using var writer = new StreamWriter (entry.Open (), encoding);
				writer.Write ("123456789");
			}

			using var source = new ReadTrackingStream (stream.ToArray ());
			using var archive = new ZipArchive (source, ZipArchiveMode.Read, leaveOpen: true);
			var archived = archive.GetEntry ("crc.txt") ?? throw new InvalidOperationException ("Missing crc.txt.");
			var bytesRead = source.BytesRead;
			var position = source.Position;
			source.RejectReads = true;

			for (var i = 0; i < 2; i++)
				Assert.AreEqual (0xCBF43926u, Files.GetZipEntryCrc32 (archived));

			Assert.AreEqual (bytesRead, source.BytesRead, "CRC metadata lookup must not read archive contents.");
			Assert.AreEqual (position, source.Position, "CRC metadata lookup must not move the archive stream.");
		}

		[Test]
		public void GetZipEntryCrc32_RejectsNull ()
		{
			Assert.Throws<ArgumentNullException> (() => Files.GetZipEntryCrc32 (null));
		}

		[Test]
		public void CopyIfZipChanged_UnchangedReadsMetadataAndPreservesReusedStream ()
		{
			const int payloadSize = 256 * 1024;
			Directory.CreateDirectory (tempDir);
			var destination = Path.Combine (tempDir, "dest.zip");
			using (var archive = new ZipArchive (stream, ZipArchiveMode.Create, leaveOpen: true)) {
				using var writer = new StreamWriter (archive.CreateEntry ("large.txt", CompressionLevel.NoCompression).Open (), encoding);
				writer.Write (new string ('A', payloadSize));
			}
			stream.Position = 0;
			Assert.IsTrue (Files.CopyIfZipChanged (stream, destination));
			var bytes = File.ReadAllBytes (destination);
			var timestamp = new DateTime (2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
			File.SetLastWriteTimeUtc (destination, timestamp);

			using var source = new ReadTrackingStream (stream.ToArray ());
			source.Position = 7;
			for (var i = 0; i < 2; i++) {
				var bytesRead = source.BytesRead;
				Assert.IsFalse (Files.CopyIfZipChanged (source, destination));
				Assert.IsTrue (source.CanRead, "ZIP hashing must leave reusable streams open.");
				Assert.AreEqual (7, source.Position, "ZIP hashing must restore the caller's stream position.");
				Assert.Less (source.BytesRead - bytesRead, payloadSize / 4, "Unchanged ZIP hashing should read metadata, not the 256 KiB stored payload.");
			}
			CollectionAssert.AreEqual (bytes, File.ReadAllBytes (destination));
			Assert.AreEqual (timestamp, File.GetLastWriteTimeUtc (destination));
		}

		[Test]
		public void CopyIfZipChanged_String ()
		{
			Directory.CreateDirectory (tempDir);
			var source = Path.Combine (tempDir, "source.zip");
			var destination = Path.Combine (tempDir, "dest.zip");

			using (var zip = new ZipArchive (stream, ZipArchiveMode.Create, leaveOpen: true)) {
				WriteEntry (zip, "a.txt", "a");
			}
			stream.Position = 0;
			using (var f = File.Create (source)) {
				stream.CopyTo (f);
			}

			Assert.IsTrue (Files.CopyIfZipChanged (source, destination), "Should copy on new file.");
			FileAssert.Exists (destination);
			Assert.IsFalse (Files.CopyIfZipChanged (source, destination), "Should *not* copy when unchanged.");
		}

		[Test]
		public void CopyIfZipChanged_BareFileName ()
		{
			// Regression test: Path.GetDirectoryName("file.zip") returns "",
			// which previously caused Directory.CreateDirectory("") to throw.
			var cwd = Directory.GetCurrentDirectory ();
			try {
				Directory.CreateDirectory (tempDir);
				Directory.SetCurrentDirectory (tempDir);

				using (var zip = new ZipArchive (stream, ZipArchiveMode.Create, leaveOpen: true)) {
					WriteEntry (zip, "a.txt", "a");
				}
				stream.Position = 0;

				Assert.IsTrue (Files.CopyIfZipChanged (stream, "bare.zip"), "Should copy bare filename.");
				FileAssert.Exists (Path.Combine (tempDir, "bare.zip"));
			} finally {
				Directory.SetCurrentDirectory (cwd);
			}
		}

		[Test]
		public void DeleteFile_NullLog_DoesNotThrow ()
		{
			var path = Path.Combine (tempDir, "directory-instead-of-file");
			Directory.CreateDirectory (path);
			Assert.DoesNotThrow (() => Files.DeleteFile (path, null));
		}

		[Test]
		public void DeleteFile_NonTaskLoggingHelperLog_DoesNotThrow ()
		{
			var path = Path.Combine (tempDir, "directory-instead-of-file-nontasklog");
			Directory.CreateDirectory (path);
			Assert.DoesNotThrow (() => Files.DeleteFile (path, "not a TaskLoggingHelper"));
		}

		[Test]
		public void TryDeleteFile_LogsFailure ()
		{
			var path = Path.Combine (tempDir, "directory-instead-of-file-try-delete");
			Directory.CreateDirectory (path);
			string message = "";

			Assert.DoesNotThrow (() => Files.TryDeleteFile (path, value => message = value));
			Assert.That (message, Does.Contain (path));
		}

		sealed class ReadTrackingStream : Stream
		{
			readonly MemoryStream inner;

			public bool RejectReads { get; set; }
			public long BytesRead { get; private set; }

			public ReadTrackingStream (byte [] contents)
			{
				inner = new MemoryStream (contents, writable: false);
			}

			public override bool CanRead => inner.CanRead;
			public override bool CanSeek => inner.CanSeek;
			public override bool CanWrite => false;
			public override long Length => inner.Length;
			public override long Position {
				get => inner.Position;
				set => inner.Position = value;
			}

			public override int Read (byte [] buffer, int offset, int count)
			{
				CheckRead ();
				var read = inner.Read (buffer, offset, count);
				BytesRead += read;
				return read;
			}

			public override int Read (Span<byte> buffer)
			{
				CheckRead ();
				var read = inner.Read (buffer);
				BytesRead += read;
				return read;
			}

			public override int ReadByte ()
			{
				CheckRead ();
				var value = inner.ReadByte ();
				if (value >= 0)
					BytesRead++;
				return value;
			}

			void CheckRead ()
			{
				if (RejectReads)
					throw new InvalidOperationException ("ZIP payload reads are forbidden after metadata has been loaded.");
			}

			public override long Seek (long offset, SeekOrigin origin) => inner.Seek (offset, origin);
			public override void Flush () => inner.Flush ();
			public override void SetLength (long value) => throw new NotSupportedException ();
			public override void Write (byte [] buffer, int offset, int count) => throw new NotSupportedException ();

			protected override void Dispose (bool disposing)
			{
				if (disposing)
					inner.Dispose ();
				base.Dispose (disposing);
			}
		}
	}
}
