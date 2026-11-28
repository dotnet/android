// https://github.com/dotnet/android/blob/799506a9dfb746b8bdc8a4ab77e19eee875f00e3/src/Xamarin.Android.Build.Tasks/Tests/Xamarin.Android.Build.Tests/MemoryStreamPoolTests.cs

using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Android.Build.Tasks;

namespace Microsoft.Android.Build.BaseTasks.Tests
{
	[TestClass]
	public class MemoryStreamPoolTests
	{
		MemoryStreamPool pool;

		[TestInitialize]
		public void SetUp ()
		{
			pool = new MemoryStreamPool ();
		}

		[TestMethod]
		public void Reuse ()
		{
			var expected = pool.Rent ();
			expected.Write (new byte [] { 1, 2, 3 }, 0, 3);
			pool.Return (expected);
			var actual = pool.Rent ();
			Assert.AreSame (expected, actual);
			Assert.AreEqual (0L, actual.Length);
		}

		[TestMethod]
		public void PutDisposed ()
		{
			var stream = new MemoryStream ();
			stream.Dispose ();
			Assert.ThrowsExactly<NotSupportedException> (() => pool.Return (stream));
		}
	}
}
