using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using NUnit.Framework;

namespace Xamarin.SourceWriter.Tests
{
	[TestFixture]
	public class MethodWriterTests
	{
		[Test]
		public void Basics ()
		{
			var method = new MethodWriter { Name = "MyMethod", IsPublic = true, ReturnType = TypeReferenceWriter.Void };
			method.Parameters.Add (new MethodParameterWriter ("test", TypeReferenceWriter.Bool));

			var sw = new StringWriter ();
			var writer = new CodeWriter (sw);

			method.Write (writer);

			var expected =
@"public void MyMethod (bool test)
{
}
";

			Assert.AreEqual (expected, sw.ToString ());
		}
	}
}
