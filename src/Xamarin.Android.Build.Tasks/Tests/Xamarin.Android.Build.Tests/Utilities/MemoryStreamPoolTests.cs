using System;
using System.IO;
using NUnit.Framework;
using Xamarin.Android.Tasks;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	[Category ("Node-2")]
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

		[Test]
		public void CreateBinaryWriter ()
		{
			var pool = new MemoryStreamPool ();
			var expected = pool.Rent ();
			using (var writer = MemoryStreamPool.Shared.CreateBinaryWriter ()) {
				writer.Write (42);
			}
			pool.Return (expected);

			var actual = pool.Rent ();
			Assert.AreSame (expected, actual);
			Assert.AreEqual (0, actual.Length);
		}
	}
}
