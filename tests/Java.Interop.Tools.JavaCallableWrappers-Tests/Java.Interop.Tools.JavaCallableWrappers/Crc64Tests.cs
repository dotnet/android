using System;
using System.Text;
using System.Security.Cryptography;
using NUnit.Framework;

#if MICROSOFT_ANDROID_BUILD_BASETASKS_TESTS
using Crc64 = Microsoft.Android.Build.Tasks.Crc64;
#else
using Crc64 = Java.Interop.Tools.JavaCallableWrappers.Crc64;
#endif

namespace Java.Interop.Tools.JavaCallableWrappersTests
{
	[TestFixture]
	public class Crc64Tests
	{
		static string ToHash (string value)
		{
			return ToHash (Encoding.UTF8.GetBytes (value));
		}

		static string ToHash (byte[] data)
		{
			using (var crc = new Crc64 ()) {
				var hash = crc.ComputeHash (data);
				var buf = new StringBuilder (hash.Length * 2);
				foreach (var b in hash)
					buf.AppendFormat ("{0:x2}", b);
				return buf.ToString ();
			}
		}

		[Test]
		public void Hello ()
		{
			var actual = ToHash ("hello");
			Assert.AreEqual ("ad3d04bd697eb3c5", actual);
		}

		[Test]
		public void XmlDocument ()
		{
			var actual = ToHash ("System.Xml.XmlDocument, System.Xml");
			Assert.AreEqual ("348bbd9fecf1b865", actual);
		}

		[Test]
		public void Collision ()
		{
			Assert.AreNotEqual (ToHash (""), ToHash (new byte [32]));
		}

		[TestCase (0, "ffffffffffffffff", "ffffffffffffffff")]
		[TestCase (1, "d6781310b82f4829", "281d59285452080d")]
		[TestCase (7, "a9a501a15dcb9821", "02ce1c8aadeb1672")]
		[TestCase (8, "124da870c21d3cf1", "3879c095076b3224")]
		[TestCase (9, "422bb14202ce6123", "fb5bcb5733a34c8f")]
		[TestCase (15, "b742abfee2c7f613", "2e88fadca7c9ef5b")]
		[TestCase (16, "112c63bf16888eb9", "1db87de7d9640332")]
		[TestCase (17, "44ea8a2640fe617a", "3a9b1dd30122c897")]
		[TestCase (255, "382c476d9844592e", "199e1bbf06ba972b")]
		[TestCase (256, "f28f5786fb64a805", "9e1abf06ba972b00")]
		[TestCase (257, "24100ad42c017a38", "d82787363ed24452")]
		[TestCase (65535, "25eaf1031f734339", "1058243cfd70e86b")]
		[TestCase (65536, "c996e649f8f87f69", "ab531ca50c749bd1")]
		public void ExactOutput (int length, string zeroExpected, string patternedExpected)
		{
			var bytes = new byte [length];
			Assert.AreEqual (NativeEndianHash (zeroExpected), ToHash (bytes));
			for (int i = 0; i < bytes.Length; i++)
				bytes [i] = (byte) (i * 37 + 11);
			var expected = BitConverter.IsLittleEndian ? patternedExpected : ToReferenceHash (bytes);
			Assert.AreEqual (expected, ToHash (bytes));
		}

		[TestCase (0)]
		[TestCase (1)]
		[TestCase (7)]
		[TestCase (8)]
		[TestCase (9)]
		[TestCase (15)]
		[TestCase (16)]
		[TestCase (17)]
		[TestCase (64)]
		public void OffsetAndCount (int offset)
		{
			var bytes = new byte [64];
			for (int i = 0; i < bytes.Length; i++)
				bytes [i] = (byte) (i * 37 + 11);
			for (int count = 0; count <= bytes.Length - offset; count++) {
				using (var crc = new Crc64 ()) {
					var input = new byte [count];
					Array.Copy (bytes, offset, input, 0, count);
					CollectionAssert.AreEqual (Convert.FromHexString (ToReferenceHash (input)),
						crc.ComputeHash (bytes, offset, count), $"offset={offset}, count={count}");
				}
			}
		}

		[TestCase (1)]
		[TestCase (7)]
		[TestCase (8)]
		[TestCase (9)]
		[TestCase (31)]
		public void IncrementalUpdates (int chunkSize)
		{
			var bytes = new byte [257];
			for (int i = 0; i < bytes.Length; i++)
				bytes [i] = (byte) (i * 37 + 11);
			using (var crc = new Crc64 ()) {
				ulong expected = ulong.MaxValue;
				for (int offset = 0; offset < bytes.Length; offset += chunkSize) {
					int count = Math.Min (chunkSize, bytes.Length - offset);
					crc.TransformBlock (bytes, offset, count, null, 0);
					expected = ReferenceUpdate (bytes, offset, count, expected);
				}
				crc.TransformFinalBlock ([], 0, 0);
				CollectionAssert.AreEqual (BitConverter.GetBytes (expected ^ (ulong) bytes.Length), crc.Hash);
				CollectionAssert.AreEqual (BitConverter.GetBytes (ReferenceUpdate (bytes, 0, bytes.Length, ulong.MaxValue) ^ (ulong) bytes.Length),
					crc.ComputeHash (bytes), "ComputeHash must reinitialize the CRC");
			}
		}

		[TestCase (-1, 0, "ibStart")]
		[TestCase (9, 0, "ibStart")]
		[TestCase (int.MaxValue, 0, "ibStart")]
		[TestCase (0, -1, "cbSize")]
		[TestCase (0, 9, "cbSize")]
		[TestCase (1, 8, "cbSize")]
		[TestCase (8, 1, "cbSize")]
		[TestCase (1, int.MaxValue, "cbSize")]
		public void InvalidRangeDoesNotMutateState (int offset, int count, string parameter)
		{
			using (var crc = new Crc64Probe ()) {
				crc.Append ([1, 2, 3], 0, 3);
				var before = crc.Snapshot ();
				var exception = Assert.Throws<ArgumentOutOfRangeException> (() => crc.Append (new byte [8], offset, count));
				Assert.AreEqual (parameter, exception?.ParamName);
				CollectionAssert.AreEqual (before, crc.Snapshot ());
			}
		}

		[Test]
		public void NullArrayDoesNotMutateState ()
		{
			using (var crc = new Crc64Probe ()) {
				crc.Append ([1, 2, 3], 0, 3);
				var before = crc.Snapshot ();
				var exception = Assert.Throws<ArgumentNullException> (() => crc.Append (null, 0, 0));
				Assert.AreEqual ("array", exception?.ParamName);
				CollectionAssert.AreEqual (before, crc.Snapshot ());
			}
		}

		class Crc64Probe : Crc64
		{
			public void Append (byte [] array, int offset, int count) => HashCore (array, offset, count);
			public byte [] Snapshot () => HashFinal ();
		}

		static string NativeEndianHash (string littleEndianHash)
		{
			var bytes = Convert.FromHexString (littleEndianHash);
			if (!BitConverter.IsLittleEndian)
				Array.Reverse (bytes);
			return Convert.ToHexString (bytes).ToLowerInvariant ();
		}

		static string ToReferenceHash (byte [] bytes)
		{
			var crc = ReferenceUpdate (bytes, 0, bytes.Length, ulong.MaxValue);
			return Convert.ToHexString (BitConverter.GetBytes (crc ^ (ulong) bytes.Length)).ToLowerInvariant ();
		}

		static ulong ReferenceUpdate (byte [] bytes, int offset, int count, ulong crc)
		{
			for (int i = 0; i < count; i++) {
				// The legacy slicing loop consumes native-endian words low byte first.
				int index = !BitConverter.IsLittleEndian && i < count / 8 * 8 ? i / 8 * 8 + 7 - i % 8 : i;
				crc ^= bytes [offset + index];
				for (int bit = 0; bit < 8; bit++)
					crc = (crc >> 1) ^ ((crc & 1) == 0 ? 0 : 0x95ac9329ac4bc9b5UL);
			}
			return crc;
		}

		[Test]
		public void AllBytesAreProcessed ()
		{
			// Slicing processes 8 bytes (a 64-bit word) at a time, and if any of the bytes are skipped we will have a
			// collision here.
			string[] inputs = {
				"obj/Debug/lp/10/jl/bin/classes.jar",
				"obj/Debug/lp/11/jl/bin/classes.jar",
				"obj/Debug/lp/12/jl/bin/classes.jar",
			};

			string[] expected = {
				"419a37c9bcfddf3c",
				"6ea5e242b7cc24a7",
				"74770a86f8b97020",
			};

			string[] outputs = new string[inputs.Length];

			for (int i = 0; i < inputs.Length; i++) {
				byte[] bytes = Encoding.UTF8.GetBytes (inputs [i]);
				using (HashAlgorithm hashAlg = new Crc64 ()) {
					byte [] hash = hashAlg.ComputeHash (bytes);
					outputs[i] = ToHash (hash);
					Assert.AreEqual (expected[i], outputs[i], $"hash {i} differs");
				}
			}

			for (int i = 0; i < outputs.Length; i++) {
				for (int j = 0; j < outputs.Length; j++) {
					if (j == i)
						continue;
					Assert.AreNotEqual (outputs[i], outputs[j], $"Outputs {i} and {j} are identical");
				}
			}
		}
	}
}
