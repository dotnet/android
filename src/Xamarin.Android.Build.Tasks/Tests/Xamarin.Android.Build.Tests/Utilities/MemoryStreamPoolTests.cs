using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using Xamarin.Android.Tasks;

namespace Xamarin.Android.Build.Tests.Utilities
{
	[TestFixture]
	public class MemoryStreamPoolTests
	{
		MemoryStreamPool pool;

		[SetUp]
		public void SetUp ()
		{
			pool = new MemoryStreamPool ();
		}

		[Test]
		public void Reuse ()
		{
			var expected = pool.Rent ();
			expected.Write (new byte [] { 1, 2, 3 }, 0, 3);
			pool.Return (expected);
			var actual = pool.Rent ();
			Assert.AreSame (expected, actual);
			Assert.AreEqual (0, actual.Length);
		}

		[Test]
		public void PutDisposed ()
		{
			var stream = new MemoryStream ();
			stream.Dispose ();
			Assert.Throws<NotSupportedException> (() => pool.Return (stream));
		}

		[Test]
		public void CreateStreamWriter ()
		{
			var pool = new MemoryStreamPool ();
			var expected = pool.Rent ();
			using (var writer = MemoryStreamPool.Shared.CreateStreamWriter ()) {
				writer.WriteLine ("foobar");
			}
			pool.Return (expected);

			var actual = pool.Rent ();
			Assert.AreSame (expected, actual);
			Assert.AreEqual (0, actual.Length);
		}
	}
}
