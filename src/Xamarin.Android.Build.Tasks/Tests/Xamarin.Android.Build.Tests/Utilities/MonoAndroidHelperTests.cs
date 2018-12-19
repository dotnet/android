using System.IO;
using NUnit.Framework;
using Xamarin.Android.Tasks;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
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
			File.WriteAllText (temp, "");
			File.SetAttributes (temp, FileAttributes.ReadOnly);

			var foo = "bar";
			Assert.IsTrue (MonoAndroidHelper.CopyIfStringChanged (foo, temp), "Should write on new file.");
			FileAssert.Exists (temp);
		}

		[Test]
		public void CopyIfBytesChanged_Readonly ()
		{
			File.WriteAllText (temp, "");
			File.SetAttributes (temp, FileAttributes.ReadOnly);

			var foo = new byte [32];
			Assert.IsTrue (MonoAndroidHelper.CopyIfBytesChanged (foo, temp), "Should write on new file.");
			FileAssert.Exists (temp);
		}

		[Test]
		public void CopyIfStreamChanged_Readonly ()
		{
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
	}
}
