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
	}
}

