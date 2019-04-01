using System;
using System.IO;
using System.IO.Compression;

using NUnit.Framework;

namespace System.IO.CompressionTests
{
	[TestFixture]
	public class GzipStreamTest
	{
		[Test]
		public void Compression ()
		{
			const string expected = "Hello, compressed world!";
			var o = new MemoryStream ();

			using (var gzip = new StreamWriter (new GZipStream (o, CompressionMode.Compress)))
				gzip.WriteLine (expected);

			o = new MemoryStream (o.ToArray ());
			o.Position = 0;

			string result;
			using (var gzip = new StreamReader (new GZipStream (o, CompressionMode.Decompress)))
				result = gzip.ReadLine ();

			Assert.AreEqual (expected, result);
		}

		// https://bugzilla.xamarin.com/show_bug.cgi?id=34139
		[Test]
		public void GZipStreamFlush_WithNoData_ShouldNotThrow ()
		{
			Assert.DoesNotThrow (() => {
				var data = new byte[1];
				var backing = new MemoryStream ();
				var compressing = new GZipStream (backing, CompressionMode.Compress);
				compressing.Write (data, 0, 0);
				compressing.Flush (); // throws here
				compressing.Close ();
				backing.Close ();
			}, "Regression test for #34139 failed." );
		}
	}
}

