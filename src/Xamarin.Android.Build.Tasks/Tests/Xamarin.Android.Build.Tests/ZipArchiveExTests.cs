using NUnit.Framework;
using System.IO;
using System.Text;
using Xamarin.Android.Tasks;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	public class ZipArchiveExTests
	{
		string temp;
		string zip;

		[SetUp]
		public void SetUp ()
		{
			string root = Path.GetDirectoryName (GetType ().Assembly.Location);
			temp = Path.Combine (root, "temp", TestContext.CurrentContext.Test.Name);
			Directory.CreateDirectory (temp);
			zip = Path.Combine (root, "test.zip");
		}

		[TearDown]
		public void TearDown ()
		{
			Directory.Delete (temp, true);
			File.Delete (zip);
		}

		void CreateDirectories (params string [] paths)
		{
			foreach (var path in paths) {
				var dest = Path.Combine (temp, path);
				Directory.CreateDirectory (Path.GetDirectoryName (dest));
				//Just put the path in the test file, for testing purposes
				File.WriteAllText (dest, path);
			}
		}

		void AssertZip (string expected)
		{
			FileAssert.Exists (zip, "Zip file should exist!");

			var builder = new StringBuilder ();
			using (var archive = new ZipArchiveEx (zip, FileMode.Open)) {
				foreach (var entry in archive.Archive) {
					builder.AppendLine (entry.FullName);
				}
			}

			Assert.AreEqual (expected.Trim (), builder.ToString ().Trim ());
		}

		[Test]
		public void AddDirectory ()
		{
			CreateDirectories ("A.txt", Path.Combine ("B", "B.txt"));

			using (var archive = new ZipArchiveEx (zip, FileMode.Create)) {
				archive.AddDirectory (temp, "temp");
			}

			AssertZip (
@"temp/A.txt
temp/B/
temp/B/B.txt");
		}

		[Test]
		public void AddDirectoryOddPaths ()
		{
			CreateDirectories ("A.txt", Path.Combine ("B", "B.txt"));

			using (var archive = new ZipArchiveEx (zip, FileMode.Create)) {
				archive.AddDirectory (temp + $@"\../{nameof (AddDirectoryOddPaths)}/", "temp");
			}

			AssertZip (
@"temp/A.txt
temp/B/
temp/B/B.txt");
		}

		[Test]
		public void AddDirectoryCurrentDirectory ()
		{
			CreateDirectories ("A.txt", Path.Combine ("B", "B.txt"));

			string cwd = Directory.GetCurrentDirectory ();
			try {
				Directory.SetCurrentDirectory (temp);

				using (var archive = new ZipArchiveEx (zip, FileMode.Create)) {
					archive.AddDirectory (".", "temp");
				}

				AssertZip (
@"temp/A.txt
temp/B/
temp/B/B.txt");
			} finally {
				Directory.SetCurrentDirectory (cwd);
			}
		}
	}
}
