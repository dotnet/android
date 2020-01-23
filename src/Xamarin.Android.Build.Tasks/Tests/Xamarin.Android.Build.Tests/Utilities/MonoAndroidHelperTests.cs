using System.IO;
using System.Text;
using NUnit.Framework;
using Xamarin.Android.Tasks;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	[Category ("Node-2")]
	public class MonoAndroidHelperTests
	{
		string temp;

		[SetUp]
		public void SetUp ()
		{
			temp = Path.Combine (Path.GetTempPath (), TestContext.CurrentContext.Test.Name);
		}

		[TearDown]
		public void TearDown ()
		{
			File.Delete (temp);
		}

		[Test]
		public void CopyIfStringChanged ()
		{
			var foo = "bar";
			Assert.IsTrue (MonoAndroidHelper.CopyIfStringChanged (foo, temp), "Should write on new file.");
			FileAssert.Exists (temp);
			Assert.IsFalse (MonoAndroidHelper.CopyIfStringChanged (foo, temp), "Should *not* write unless changed.");
			foo += "\n";
			Assert.IsTrue (MonoAndroidHelper.CopyIfStringChanged (foo, temp), "Should write when changed.");
		}

		[Test]
		public void CopyIfBytesChanged ()
		{
			var foo = new byte [32];
			Assert.IsTrue (MonoAndroidHelper.CopyIfBytesChanged (foo, temp), "Should write on new file.");
			FileAssert.Exists (temp);
			Assert.IsFalse (MonoAndroidHelper.CopyIfBytesChanged (foo, temp), "Should *not* write unless changed.");
			foo [0] = 0xFF;
			Assert.IsTrue (MonoAndroidHelper.CopyIfBytesChanged (foo, temp), "Should write when changed.");
		}

		[Test]
		public void CopyIfStreamChanged ()
		{
			using (var foo = new MemoryStream ())
			using (var writer = new StreamWriter (foo)) {
				writer.WriteLine ("bar");
				writer.Flush ();

				Assert.IsTrue (MonoAndroidHelper.CopyIfStreamChanged (foo, temp), "Should write on new file.");
				FileAssert.Exists (temp);
				Assert.IsFalse (MonoAndroidHelper.CopyIfStreamChanged (foo, temp), "Should *not* write unless changed.");
				writer.WriteLine ();
				writer.Flush ();
				Assert.IsTrue (MonoAndroidHelper.CopyIfStreamChanged (foo, temp), "Should write when changed.");
			}
		}

		[Test]
		public void CopyIfStringChanged_NewDirectory ()
		{
			temp = Path.Combine (temp, "foo.txt");

			var foo = "bar";
			Assert.IsTrue (MonoAndroidHelper.CopyIfStringChanged (foo, temp), "Should write on new file.");
			FileAssert.Exists (temp);
		}

		[Test]
		public void CopyIfBytesChanged_NewDirectory ()
		{
			temp = Path.Combine (temp, "foo.bin");

			var foo = new byte [32];
			Assert.IsTrue (MonoAndroidHelper.CopyIfBytesChanged (foo, temp), "Should write on new file.");
			FileAssert.Exists (temp);
		}

		[Test]
		public void CopyIfStreamChanged_NewDirectory ()
		{
			temp = Path.Combine (temp, "foo.txt");

			using (var foo = new MemoryStream ())
			using (var writer = new StreamWriter (foo)) {
				writer.WriteLine ("bar");
				writer.Flush ();

				Assert.IsTrue (MonoAndroidHelper.CopyIfStreamChanged (foo, temp), "Should write on new file.");
				FileAssert.Exists (temp);
			}
		}

		[Test]
		public void CopyIfStringChanged_Readonly ()
		{
			if (File.Exists (temp)) {
				File.SetAttributes (temp, FileAttributes.Normal);
			}
			File.WriteAllText (temp, "");
			File.SetAttributes (temp, FileAttributes.ReadOnly);

			var foo = "bar";
			Assert.IsTrue (MonoAndroidHelper.CopyIfStringChanged (foo, temp), "Should write on new file.");
			FileAssert.Exists (temp);
		}

		[Test]
		public void CopyIfBytesChanged_Readonly ()
		{
			if (File.Exists (temp)) {
				File.SetAttributes (temp, FileAttributes.Normal);
			}
			File.WriteAllText (temp, "");
			File.SetAttributes (temp, FileAttributes.ReadOnly);

			var foo = new byte [32];
			Assert.IsTrue (MonoAndroidHelper.CopyIfBytesChanged (foo, temp), "Should write on new file.");
			FileAssert.Exists (temp);
		}

		[Test]
		public void CopyIfStreamChanged_Readonly ()
		{
			if (File.Exists (temp)) {
				File.SetAttributes (temp, FileAttributes.Normal);
			}
			File.WriteAllText (temp, "");
			File.SetAttributes (temp, FileAttributes.ReadOnly);

			using (var foo = new MemoryStream ())
			using (var writer = new StreamWriter (foo)) {
				writer.WriteLine ("bar");
				writer.Flush ();

				Assert.IsTrue (MonoAndroidHelper.CopyIfStreamChanged (foo, temp), "Should write on new file.");
				FileAssert.Exists (temp);
			}
		}

		[Test]
		public void CleanBOM_Readonly ()
		{
			var encoding = Encoding.UTF8;
			Directory.CreateDirectory (temp);
			temp = Path.Combine (temp, "foo.txt");
			if (File.Exists (temp)) {
				File.SetAttributes (temp, FileAttributes.Normal);
			}
			using (var stream = File.Create (temp))
			using (var writer = new StreamWriter (stream, encoding)) {
				writer.Write ("This will have a BOM");
			}
			File.SetAttributes (temp, FileAttributes.ReadOnly);
			var before = File.ReadAllBytes (temp);
			MonoAndroidHelper.CleanBOM (temp);
			var after = File.ReadAllBytes (temp);
			var preamble = encoding.GetPreamble ();
			Assert.AreEqual (before.Length, after.Length + preamble.Length, "BOM should be removed!");
		}

		[Test]
		public void CopyIfStreamChanged_MemoryStreamPool_StreamWriter ()
		{
			var pool = new MemoryStreamPool ();
			var expected = pool.Rent ();
			pool.Return (expected);

			using (var writer = pool.CreateStreamWriter ()) {
				writer.WriteLine ("bar");
				writer.Flush ();

				Assert.IsTrue (MonoAndroidHelper.CopyIfStreamChanged (writer.BaseStream, temp), "Should write on new file.");
				FileAssert.Exists (temp);
			}

			var actual = pool.Rent ();
			Assert.AreSame (expected, actual);
			Assert.AreEqual (0, actual.Length);
		}

		[Test]
		public void CopyIfStreamChanged_MemoryStreamPool_BinaryWriter ()
		{
			var pool = new MemoryStreamPool ();
			var expected = pool.Rent ();
			pool.Return (expected);

			using (var writer = pool.CreateBinaryWriter ()) {
				writer.Write (42);
				writer.Flush ();

				Assert.IsTrue (MonoAndroidHelper.CopyIfStreamChanged (writer.BaseStream, temp), "Should write on new file.");
				FileAssert.Exists (temp);
			}

			var actual = pool.Rent ();
			Assert.AreSame (expected, actual);
			Assert.AreEqual (0, actual.Length);
		}
	}
}
