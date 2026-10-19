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

		[TestCase (false)]
		[TestCase (true)]
		public void UnsafeBodyDoesNotChangeCallerContract (bool requiresUnsafe)
		{
			var method = new MethodWriter {
				Name = "Read",
				ReturnType = new TypeReferenceWriter ("int"),
				IsPublic = true,
				IsUnsafe = requiresUnsafe,
				HasUnsafeBody = true,
			};
			method.Body.Add ("return ReadNative ();");

			var sw = new StringWriter ();
			method.Write (new CodeWriter (sw));

			Assert.AreEqual (
				$"public {(requiresUnsafe ? "unsafe " : "")}int Read ()\n{{\n\tunsafe {{\n\t\treturn ReadNative ();\n\t}}\n}}\n",
				sw.ToString ().Replace ("\r\n", "\n"));
		}
	}
}
