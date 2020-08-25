using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using NUnit.Framework;

namespace Xamarin.SourceWriter.Tests
{
	[TestFixture]
	public class ClassWriterTests
	{
		[Test]
		public void Basics ()
		{
			var klass = new ClassWriter {
				IsPublic = true,
				Inherits = "System.Object",
				IsPartial = true,
				Name = "MyClass",
				UsePriorityOrder = true
			};

			klass.Fields.Add (new FieldWriter { IsPublic = true, Name = "my_field", Type = TypeReferenceWriter.Bool });
			klass.AddInlineComment ("// Test comment");

			klass.Methods.Add (new MethodWriter { Name = "MyMethod", IsPublic = true, ReturnType = TypeReferenceWriter.Void });
			klass.Methods [0].Parameters.Add (new MethodParameterWriter ("test", TypeReferenceWriter.Bool));

			var sw = new StringWriter ();
			var writer = new CodeWriter (sw);

			klass.Write (writer);

			var expected =
@"public partial class MyClass : System.Object {
	public bool my_field;

	// Test comment

	public void MyMethod (bool test)
	{
	}

}
";

			Assert.AreEqual (expected, sw.ToString ());
		}
	}
}
