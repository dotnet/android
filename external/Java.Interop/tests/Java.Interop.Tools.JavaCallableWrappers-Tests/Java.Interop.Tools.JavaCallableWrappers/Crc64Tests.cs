using System.Text;
using System.Security.Cryptography;
using Java.Interop.Tools.JavaCallableWrappers;
using NUnit.Framework;

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
