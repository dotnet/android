using System.Text;
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
			Assert.AreEqual ("b9c1bdfc7cd47543", actual);
		}

		[Test]
		public void Collision ()
		{
			Assert.AreNotEqual (ToHash (""), ToHash (new byte [32]));
		}
	}
}
