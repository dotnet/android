using NUnit.Framework;
using System;
using System.IO;
using System.Text;
using Xamarin.Android.Tools;
using Xamarin.Tools.Zip;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	[Category ("Node-2")]
	public class FilesTests : BaseTest
	{
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
		public void ToHashString ()
		{
			var bytes = new byte [] { 0x12, 0x34, 0x56, 0x78, 0x90, 0xAB, 0xCD, 0xEF };
			var expected = BitConverter.ToString (bytes).Replace ("-", string.Empty);
			Assert.AreEqual (expected, Files.ToHexString (bytes));
		}
	}
}
