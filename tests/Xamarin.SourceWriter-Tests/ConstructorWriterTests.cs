using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using NUnit.Framework;

namespace Xamarin.SourceWriter.Tests
{
	[TestFixture]
	public class ConstructorWriterTests
	{
		[Test]
		public void Basics ()
		{
			var ctor = new ConstructorWriter { Name = "MyClass", IsPublic = true, BaseCall = "base ()" };
			ctor.Parameters.Add (new MethodParameterWriter ("test", TypeReferenceWriter.Bool));

			var sw = new StringWriter ();
			var writer = new CodeWriter (sw);

			ctor.Write (writer);

			var expected =
@"public MyClass (bool test) : base ()
{
}
";

			Assert.AreEqual (expected, sw.ToString ());
		}
	}
}
