// https://github.com/xamarin/xamarin-android/blob/34acbbae6795854cc4e9f8eb7167ab011e0266b4/src/Xamarin.Android.Build.Tasks/Tests/Xamarin.Android.Build.Tests/MonoAndroidHelperTests.cs
// https://github.com/xamarin/xamarin-android/blob/799506a9dfb746b8bdc8a4ab77e19eee875f00e3/src/Xamarin.Android.Build.Tasks/Tests/Xamarin.Android.Build.Tests/FilesTests.cs

using NUnit.Framework;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xamarin.Tools.Zip;
using Microsoft.Android.Build.Tasks;

namespace Microsoft.Android.Build.BaseTasks.Tests
{
	[TestFixture]
	public class FilesTests
	{
		bool IsWindows = RuntimeInformation.IsOSPlatform (OSPlatform.Windows);
		const int MaxFileName = 255;

		static readonly Encoding encoding = Encoding.UTF8;
		string tempDir;
		MemoryStream stream;

		[SetUp]
		public void SetUp ()
		{
			tempDir = Path.Combine (Path.GetTempPath (), TestContext.CurrentContext.Test.Name);
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
			using (var zip = ZipArchive.Open (stream)) {
				return Files.ExtractAll (zip, tempDir);
			}
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
			using (var zip = ZipArchive.Create (stream)) {
				zip.AddEntry ("a.txt", "a", encoding);
				zip.AddEntry ("b/b.txt", "b", encoding);
			}

			bool changes = ExtractAll (stream);

			Assert.IsTrue (changes, "ExtractAll should report changes.");
			AssertFile ("a.txt", "a");
			AssertFile (Path.Combine ("b", "b.txt"), "b");
		}

		[Test]
		public void ExtractAll_NoChanges ()
		{
			using (var zip = ZipArchive.Create (stream)) {
				zip.AddEntry ("a.txt", "a", encoding);
				zip.AddEntry ("b/b.txt", "b", encoding);
			}

			bool changes = ExtractAll (stream);
			Assert.IsTrue (changes, "ExtractAll should report changes.");

			stream.SetLength (0);
			using (var zip = ZipArchive.Open (stream)) {
				zip.AddEntry ("a.txt", "a", encoding);
				zip.AddEntry ("b/b.txt", "b", encoding);
			}

			changes = ExtractAll (stream);

			Assert.IsFalse (changes, "ExtractAll should *not* report changes.");
			AssertFile ("a.txt", "a");
			AssertFile (Path.Combine ("b", "b.txt"), "b");
		}

		[Test]
		public void ExtractAll_NewFile ()
		{
			using (var zip = ZipArchive.Create (stream)) {
				zip.AddEntry ("a.txt", "a", encoding);
				zip.AddEntry ("b/b.txt", "b", encoding);
			}

			bool changes = ExtractAll (stream);
			Assert.IsTrue (changes, "ExtractAll should report changes.");

			stream.SetLength (0);
			using (var zip = ZipArchive.Open (stream)) {
				zip.AddEntry ("a.txt", "a", encoding);
				zip.AddEntry ("b/b.txt", "b", encoding);
				zip.AddEntry ("c/c.txt", "c", encoding);
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
			using (var zip = ZipArchive.Create (stream)) {
				zip.AddEntry ("foo.txt", "foo", encoding);
			}

			bool changes = ExtractAll (stream);
			Assert.IsTrue (changes, "ExtractAll should report changes.");

			stream.SetLength (0);
			using (var zip = ZipArchive.Create (stream)) {
				zip.AddEntry ("foo.txt", "bar", encoding);
			}

			changes = ExtractAll (stream);

			Assert.IsTrue (changes, "ExtractAll should report changes.");
			AssertFile ("foo.txt", "bar");
		}

		[Test]
		public void ExtractAll_FileDeleted ()
		{
			using (var zip = ZipArchive.Create (stream)) {
				zip.AddEntry ("a.txt", "a", encoding);
				zip.AddEntry ("b/b.txt", "b", encoding);
			}

			bool changes = ExtractAll (stream);
			Assert.IsTrue (changes, "ExtractAll should report changes.");

			stream.SetLength (0);
			using (var zip = ZipArchive.Open (stream)) {
				zip.AddEntry ("a.txt", "a", encoding);
			}

			changes = ExtractAll (stream);

			Assert.IsTrue (changes, "ExtractAll should report changes.");
			AssertFile ("a.txt", "a");
			FileAssert.DoesNotExist (Path.Combine (tempDir, "b", "b.txt"));
		}

		[Test]
		public void ExtractAll_ModifyCallback ()
		{
			using (var zip = ZipArchive.Create (stream)) {
				zip.AddEntry ("foo/a.txt", "a", encoding);
				zip.AddEntry ("foo/b/b.txt", "b", encoding);
			}

			stream.Position = 0;
			using (var zip = ZipArchive.Open (stream)) {
				bool changes = Files.ExtractAll (zip, tempDir, modifyCallback: e => e.Replace ("foo/", ""));
				Assert.IsTrue (changes, "ExtractAll should report changes.");
			}

			AssertFile ("a.txt", "a");
			AssertFile (Path.Combine ("b", "b.txt"), "b");
		}

		[Test]
		public void ExtractAll_SkipCallback ()
		{
			using (var zip = ZipArchive.Create (stream)) {
				zip.AddEntry ("a.txt", "a", encoding);
				zip.AddEntry ("b/b.txt", "b", encoding);
			}

			stream.Position = 0;
			using (var zip = ZipArchive.Open (stream)) {
				bool changes = Files.ExtractAll (zip, tempDir, skipCallback: e => e == "a.txt");
				Assert.IsTrue (changes, "ExtractAll should report changes.");
			}

			AssertFileDoesNotExist ("a.txt");
			AssertFile (Path.Combine ("b", "b.txt"), "b");
		}

		[Test]
		public void ExtractAll_MacOSFiles ()
		{
			using (var zip = ZipArchive.Create (stream)) {
				zip.AddEntry ("a/.DS_Store", "a", encoding);
				zip.AddEntry ("b/__MACOSX/b.txt", "b", encoding);
				zip.AddEntry ("c/__MACOSX", "c", encoding);
			}

			bool changes = ExtractAll (stream);
			Assert.IsFalse (changes, "ExtractAll should *not* report changes.");
			DirectoryAssert.DoesNotExist (tempDir);
		}

		[Test]
		public void ExtractAll_SkipsPathTraversal ()
		{
			using (var zip = ZipArchive.Create (stream)) {
				zip.AddEntry ("a.txt", "a", encoding);
			}

			var destinationDir = Path.Combine (tempDir, "dest");
			stream.Position = 0;
			using (var zip = ZipArchive.Open (stream)) {
				// modifyCallback introduces a path traversal
				bool changes = Files.ExtractAll (zip, destinationDir, modifyCallback: e => "../" + e);
				Assert.IsFalse (changes, "ExtractAll should not report changes for skipped entries.");
			}
			FileAssert.DoesNotExist (Path.Combine (tempDir, "a.txt"));
		}

		[Test]
		public void ExtractAll_SkipsPathTraversal_ExtractsValidEntries ()
		{
			using (var zip = ZipArchive.Create (stream)) {
				zip.AddEntry ("good.txt", "good", encoding);
				zip.AddEntry ("relative.txt", "relative", encoding);
			}

			var destinationDir = Path.Combine (tempDir, "dest");
			stream.Position = 0;
			using (var zip = ZipArchive.Open (stream)) {
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
			using (var zip = ZipArchive.Create (stream)) {
				zip.AddEntry ("a.txt", "a", encoding);
			}

			var destinationDir = Path.Combine (tempDir, "dest");
			stream.Position = 0;
			using (var zip = ZipArchive.Open (stream)) {
				bool changes = Files.ExtractAll (zip, destinationDir, modifyCallback: e => prefix + e);
				Assert.IsFalse (changes, $"Entry with prefix '{prefix}' should be skipped.");
			}
		}

		[TestCase ("..\\")]
		[TestCase ("..\\..\\")]
		[Platform ("Win")]
		public void ExtractAll_SkipsPathTraversal_BackSlash (string prefix)
		{
			using (var zip = ZipArchive.Create (stream)) {
				zip.AddEntry ("a.txt", "a", encoding);
			}

			var destinationDir = Path.Combine (tempDir, "dest");
			stream.Position = 0;
			using (var zip = ZipArchive.Open (stream)) {
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
	}
}
