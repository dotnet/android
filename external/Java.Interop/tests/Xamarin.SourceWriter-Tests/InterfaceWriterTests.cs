using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using NUnit.Framework;

namespace Xamarin.SourceWriter.Tests
{
	[TestFixture]
	public class InterfaceWriterTests
	{
		[Test]
		public void Basics ()
		{
			var iface = new InterfaceWriter {
				IsPublic = true,
				Inherits = "IDisposable",
				IsPartial = true,
				Name = "IMyInterface",
				UsePriorityOrder = true
			};

			iface.Methods.Add (new MethodWriter { Name = "MyMethod", IsDeclaration = true, ReturnType = TypeReferenceWriter.Void });
			iface.Methods [0].Parameters.Add (new MethodParameterWriter ("test", TypeReferenceWriter.Bool));

			var sw = new StringWriter ();
			var writer = new CodeWriter (sw);

			iface.Write (writer);

			var expected =
@"public partial interface IMyInterface : IDisposable {
	void MyMethod (bool test);
	
}
";

			Assert.AreEqual (expected, sw.ToString ());
		}
	}
}
