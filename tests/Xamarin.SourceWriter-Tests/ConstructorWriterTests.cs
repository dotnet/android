using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Xamarin.SourceWriter.Tests
{
	[TestClass]
	public class ConstructorWriterTests
	{
		[TestMethod]
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
