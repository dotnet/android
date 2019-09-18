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
			var data = Encoding.UTF8.GetBytes (value);
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
			Assert.AreEqual ("a27666cb10ddb0d6", actual);
		}

		[Test]
		public void XmlDocument ()
		{
			var actual = ToHash ("System.Xml.XmlDocument, System.Xml");
			Assert.AreEqual ("2fbc43b3a95193ae", actual);
		}
	}
}
