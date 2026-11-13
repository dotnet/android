using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using NUnit.Framework;

namespace Xamarin.SourceWriter.Tests
{
	[TestFixture]
	public class FieldWriterTests
	{
		[Test]
		public void ShadowingConstant ()
		{
			var field = new FieldWriter {
				Name = "MyField",
				IsPublic = true,
				IsConst = true,
				IsShadow = true,
				Type = new TypeReferenceWriter ("int"),
				Value = "1",
			};
			var sw = new StringWriter ();
			field.Write (new CodeWriter (sw));

			Assert.AreEqual ($"public new const int MyField = 1;{Environment.NewLine}", sw.ToString ());
		}

		[Test]
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
	}
}
