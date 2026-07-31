using System;
using System.Linq;
using Microsoft.Android.Build.Tasks;
using NUnit.Framework;

namespace Microsoft.Android.Build.BaseTasks.Tests
{
	[TestFixture]
	public class HexUtilitiesTests
	{
		[Test]
		public void GetHexValue_UpperCase ()
		{
			const string expected = "0123456789ABCDEF";
			for (int i = 0; i < expected.Length; i++) {
				Assert.AreEqual (expected [i], HexUtilities.GetHexValue (i), $"Mismatch at {i}.");
			}
		}

		[Test]
		public void GetHexValue_LowerCase ()
		{
			const string expected = "0123456789abcdef";
			for (int i = 0; i < expected.Length; i++) {
				Assert.AreEqual (expected [i], HexUtilities.GetHexValue (i, upperCase: false), $"Mismatch at {i}.");
			}
		}

		[Test]
		public void GetHexValue_DefaultsToUpperCase ()
		{
			Assert.AreEqual ('A', HexUtilities.GetHexValue (10));
		}

		[Test]
		public void ToHexString_Empty ()
		{
			Assert.AreEqual ("", HexUtilities.ToHexString (ReadOnlySpan<byte>.Empty));
		}

		[Test]
		public void ToHexString_UpperCase ()
		{
			var bytes = new byte [] { 0x00, 0x01, 0x0f, 0x10, 0x7f, 0x80, 0xab, 0xff };
			Assert.AreEqual ("00010F107F80ABFF", HexUtilities.ToHexString (bytes));
		}

		[Test]
		public void ToHexString_LowerCase ()
		{
			var bytes = new byte [] { 0x00, 0x01, 0x0f, 0x10, 0x7f, 0x80, 0xab, 0xff };
			Assert.AreEqual ("00010f107f80abff", HexUtilities.ToHexString (bytes, upperCase: false));
		}

		[Test]
		public void ToHexString_EveryByteValue ()
		{
			var bytes = Enumerable.Range (0, 256).Select (i => (byte) i).ToArray ();
			var expected = BitConverter.ToString (bytes).Replace ("-", "");
			Assert.AreEqual (expected, HexUtilities.ToHexString (bytes));
			Assert.AreEqual (expected.ToLowerInvariant (), HexUtilities.ToHexString (bytes, upperCase: false));
		}

		// The implementation stackallocs up to 128 chars (64 bytes) and heap allocates beyond that
		[TestCase (63)]
		[TestCase (64)]
		[TestCase (65)]
		[TestCase (1024)]
		public void ToHexString_CrossesStackallocThreshold (int length)
		{
			var bytes = new byte [length];
			new Random (42).NextBytes (bytes);
			var expected = BitConverter.ToString (bytes).Replace ("-", "");
			Assert.AreEqual (expected, HexUtilities.ToHexString (bytes));
		}
	}
}
