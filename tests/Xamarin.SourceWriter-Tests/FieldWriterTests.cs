using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Xamarin.SourceWriter.Tests
{
	[TestClass]
	public class FieldWriterTests
	{
		[TestMethod]
		public void Basics ()
		{
			var field = new FieldWriter { Name = "MyField", IsPublic = true, Type = TypeReferenceWriter.IntPtr };

			var sw = new StringWriter ();
			var writer = new CodeWriter (sw);

			field.Write (writer);

			var expected = @"public IntPtr MyField;
";

			Assert.AreEqual (expected, sw.ToString ());
		}

		[Test]
		public void ShadowConstField ()
		{
			var field = new FieldWriter {
				Name = "MyConst",
				IsPublic = true,
				IsConst = true,
				IsShadow = true,
				Type = new TypeReferenceWriter ("int"),
				Value = "42",
			};

			var sw = new StringWriter ();
			var writer = new CodeWriter (sw);

			field.Write (writer);

			var expected = "public new const int MyConst = 42;\n";

			Assert.AreEqual (expected, sw.ToString ().Replace ("\r\n", "\n"));
		}
	}
}
