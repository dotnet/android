using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using NUnit.Framework;

namespace Xamarin.SourceWriter.Tests
{
	[TestFixture]
	public class DelegateWriterTests
	{
		[Test]
		public void Basics ()
		{
			var method = new DelegateWriter { Name = "MyDelegate", IsPublic = true, Type = TypeReferenceWriter.IntPtr };
			method.Parameters.Add (new MethodParameterWriter ("test", TypeReferenceWriter.Bool));

			var sw = new StringWriter ();
			var writer = new CodeWriter (sw);

			method.Write (writer);

			var expected = "public delegate IntPtr MyDelegate (bool test);";

			Assert.AreEqual (expected, sw.ToString ());
		}
	}
}
