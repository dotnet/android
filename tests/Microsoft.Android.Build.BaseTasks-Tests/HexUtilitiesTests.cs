using System;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Android.Build.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Android.Build.BaseTasks.Tests
{
	[TestClass]
	public class HexUtilitiesTests
	{
		[TestMethod]
		public void GetHexValue_UpperCase ()
		{
			const string expected = "0123456789ABCDEF";
			for (int i = 0; i < expected.Length; i++) {
				Assert.AreEqual (expected [i], HexUtilities.GetHexValue (i), $"Mismatch at {i}.");
			}
		}

		[TestMethod]
		public void GetHexValue_LowerCase ()
		{
			const string expected = "0123456789abcdef";
			for (int i = 0; i < expected.Length; i++) {
				Assert.AreEqual (expected [i], HexUtilities.GetHexValue (i, upperCase: false), $"Mismatch at {i}.");
			}
		}

		[TestMethod]
		public void WriteHex_Span_LowerCase ()
		{
			Span<char> chars = stackalloc char [2];
			HexUtilities.WriteHex (chars, 0xab, upperCase: false);
			Assert.AreEqual ("ab", chars.ToString ());
		}

		[TestMethod]
		public void WriteHex_Span_WritesExactlyTwoChars ()
		{
			Span<char> chars = stackalloc char [4];
			chars.Fill ('_');
			HexUtilities.WriteHex (chars.Slice (1, 2), 0x0f);
			Assert.AreEqual ("_0F_", chars.ToString ());
		}

		[TestMethod]
		public void WriteHex_Span_EveryByteValue ()
		{
			Span<char> chars = stackalloc char [2];
			for (int i = 0; i < 256; i++) {
				HexUtilities.WriteHex (chars, (byte) i);
				Assert.AreEqual (i.ToString ("X2"), chars.ToString (), $"Mismatch at {i}.");
			}
		}

		[TestMethod]
		public void WriteHex_TextWriter_UpperCase ()
		{
			var writer = new StringWriter ();
			HexUtilities.WriteHex (writer, 0x00);
			HexUtilities.WriteHex (writer, 0x0f);
			HexUtilities.WriteHex (writer, 0xff);
			Assert.AreEqual ("000FFF", writer.ToString ());
		}

		[TestMethod]
		public void WriteHex_TextWriter_LowerCase ()
		{
			var writer = new StringWriter ();
			HexUtilities.WriteHex (writer, 0x00, upperCase: false);
			HexUtilities.WriteHex (writer, 0x0f, upperCase: false);
			HexUtilities.WriteHex (writer, 0xff, upperCase: false);
			Assert.AreEqual ("000fff", writer.ToString ());
		}

		[TestMethod]
		public void WriteHex_TextWriter_EveryByteValue ()
		{
			var writer = new StringWriter ();
			for (int i = 0; i < 256; i++) {
				HexUtilities.WriteHex (writer, (byte) i);
			}
			var expected = string.Concat (Enumerable.Range (0, 256).Select (i => i.ToString ("X2")));
			Assert.AreEqual (expected, writer.ToString ());
		}

		[TestMethod]
		public void WriteHex_TextWriter_NullThrows ()
		{
			Assert.ThrowsExactly<ArgumentNullException> (() => HexUtilities.WriteHex (writer: null, value: 0x00));
		}

		[TestMethod]
		public void WriteHex_StringBuilder_UInt16 ()
		{
			var builder = new StringBuilder ("_");
			HexUtilities.WriteHex (builder, 0xabcd);
			HexUtilities.WriteHex (builder, 0x012f, upperCase: false);
			Assert.AreEqual ("_ABCD012f", builder.ToString ());
		}

		[TestMethod]
		public void WriteHex_StringBuilder_NullThrows ()
		{
			Assert.ThrowsExactly<ArgumentNullException> (() => HexUtilities.WriteHex (builder: null, value: 0x0000));
		}

		[TestMethod]
		[DataRow (0)]
		[DataRow (1)]
		public void WriteHex_Span_TooShortThrows (int length)
		{
			var destination = new char [length];
			Assert.ThrowsExactly<ArgumentException> (() => HexUtilities.WriteHex (destination.AsSpan (), 0xAB));
		}

		[TestMethod]
		public void WriteHex_Span_LongerThanTwoOnlyWritesTwo ()
		{
			var destination = new char [] { 'x', 'x', 'x', 'x' };
			HexUtilities.WriteHex (destination.AsSpan (), 0xAB);
			Assert.AreEqual ("ABxx", new string (destination));
		}

		[TestMethod]
		public void ToHexString_Empty ()
		{
			Assert.AreEqual ("", HexUtilities.ToHexString (ReadOnlySpan<byte>.Empty));
		}

		[TestMethod]
		public void ToHexString_UpperCase ()
		{
			var bytes = new byte [] { 0x00, 0x01, 0x0f, 0x10, 0x7f, 0x80, 0xab, 0xff };
			Assert.AreEqual ("00010F107F80ABFF", HexUtilities.ToHexString (bytes));
		}

		[TestMethod]
		public void ToHexString_LowerCase ()
		{
			var bytes = new byte [] { 0x00, 0x01, 0x0f, 0x10, 0x7f, 0x80, 0xab, 0xff };
			Assert.AreEqual ("00010f107f80abff", HexUtilities.ToHexString (bytes, upperCase: false));
		}

		[TestMethod]
		public void ToHexString_EveryByteValue ()
		{
			var bytes = Enumerable.Range (0, 256).Select (i => (byte) i).ToArray ();
			var expected = BitConverter.ToString (bytes).Replace ("-", "");
			Assert.AreEqual (expected, HexUtilities.ToHexString (bytes));
			Assert.AreEqual (expected.ToLowerInvariant (), HexUtilities.ToHexString (bytes, upperCase: false));
		}

		// The implementation stackallocs up to 128 chars (64 bytes) and heap allocates beyond that
		[TestMethod]
		[DataRow (63)]
		[DataRow (64)]
		[DataRow (65)]
		[DataRow (1024)]
		public void ToHexString_CrossesStackallocThreshold (int length)
		{
			var bytes = new byte [length];
			new Random (42).NextBytes (bytes);
			var expected = BitConverter.ToString (bytes).Replace ("-", "");
			Assert.AreEqual (expected, HexUtilities.ToHexString (bytes));
		}
	}
}
