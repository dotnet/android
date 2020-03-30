using NUnit.Framework;
using Xamarin.Android.Tasks;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	[Category ("Node-2")]
	public class PackagingUtilsTests
	{
		void AssertIsValid (string path)
		{
			Assert.IsTrue (PackagingUtils.CheckEntryForPackaging (path), $"{path} should be valid");
		}

		void AssertIsNotValid (string path)
		{
			Assert.IsFalse (PackagingUtils.CheckEntryForPackaging (path), $"{path} should *not* be valid");
		}

		[Test]
		public void SigningFiles ()
		{
			AssertIsNotValid ("META-INF/MANIFEST.MF");
			AssertIsNotValid ("META-INF/MANIFEST.SF");
			AssertIsNotValid ("META-INF/MSFTSIG.SF");
			AssertIsNotValid ("META-INF/MSFTSIG.RSA");
			AssertIsNotValid ("META-INF/GOOG.RSA");
			AssertIsNotValid ("META-INF\\MANIFEST.MF");
			AssertIsNotValid ("META-INF\\MANIFEST.SF");
			AssertIsNotValid ("META-INF\\MSFTSIG.SF");
			AssertIsNotValid ("META-INF\\MSFTSIG.RSA");
			AssertIsNotValid ("META-INF\\GOOG.RSA");
		}

		[Test]
		public void SvnDirectories ()
		{
			AssertIsNotValid (".svn/foo");
			AssertIsNotValid (".svn\\foo");
			AssertIsNotValid ("bar/.svn/foo");
			AssertIsNotValid ("bar\\.svn\\foo");
		}

		[Test]
		public void KotlinServices ()
		{
			AssertIsValid ("META-INF/services/kotlinx.coroutines.internal.MainDispatcherFactory");
			AssertIsValid ("META-INF/services/kotlinx.coroutines.CoroutineExceptionHandler");
		}

		[Test]
		public void JavaFiles ()
		{
			AssertIsNotValid ("foo.java");
			AssertIsNotValid ("foo/bar.java");
			AssertIsNotValid ("foo\\bar.java");
			AssertIsNotValid ("foo.class");
			AssertIsNotValid ("foo/bar.class");
			AssertIsNotValid ("foo\\bar.class");
			AssertIsNotValid ("foo.scala");
			AssertIsNotValid ("foo/bar.scala");
			AssertIsNotValid ("foo\\bar.scala");
		}

		[Test]
		public void HiddenFiles ()
		{
			AssertIsNotValid (".foo");
			AssertIsNotValid ("foo/.bar");
			AssertIsNotValid ("foo\\.bar");
			AssertIsNotValid ("foo~");
			AssertIsNotValid ("foo/bar~");
			AssertIsNotValid ("foo\\bar~");
			AssertIsValid ("_foo"); //NOTE: this is allowed
			AssertIsNotValid ("_foo/bar");
		}

		[Test]
		public void Directories ()
		{
			AssertIsNotValid ("foo/");
			AssertIsNotValid ("foo\\");
			AssertIsNotValid ("META-INF/");
			AssertIsNotValid ("META-INF\\");
		}
	}
}
