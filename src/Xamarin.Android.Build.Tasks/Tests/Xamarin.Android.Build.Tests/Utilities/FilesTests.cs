using NUnit.Framework;
using System.IO;
using System.Text;
using Xamarin.Android.Tools;
using Xamarin.Tools.Zip;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	public class FilesTests
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
			if (Directory.Exists (tempDir))
				Directory.Delete (tempDir, recursive: true);
		}

		void AssertFile (string path, string contents)
		{
			var fullPath = Path.Combine (tempDir, path);
			FileAssert.Exists (fullPath);
			Assert.AreEqual (contents, File.ReadAllText (fullPath), $"Contents did not match at path: {path}");
		}

		bool ExtractAll (MemoryStream stream)
		{
			using (var zip = ZipArchive.Open (stream)) {
				return Files.ExtractAll (zip, tempDir);
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
	}
}
