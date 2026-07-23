// https://github.com/xamarin/xamarin-android/blob/799506a9dfb746b8bdc8a4ab77e19eee875f00e3/src/Xamarin.Android.Build.Tasks/Tests/Xamarin.Android.Build.Tests/MemoryStreamPoolTests.cs

using System;
using System.IO;
using NUnit.Framework;
using Microsoft.Android.Build.Tasks;

namespace Microsoft.Android.Build.BaseTasks.Tests
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
