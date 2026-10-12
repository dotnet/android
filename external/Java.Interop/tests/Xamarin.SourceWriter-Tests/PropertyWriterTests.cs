using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using NUnit.Framework;

namespace Xamarin.SourceWriter.Tests
{
	[TestFixture]
	public class PropertyWriterTests
	{
		[Test]
		public void Basics ()
		{
			var property = new PropertyWriter { Name = "MyProperty", IsPublic = true, PropertyType = TypeReferenceWriter.IntPtr, HasGet = true, HasSet = true };

			property.GetBody.Add ("return IntPtr.Zero;");
			property.SetBody.Add ("this.Handle = value;");

			var sw = new StringWriter ();
			var writer = new CodeWriter (sw);

			property.Write (writer);

			Console.WriteLine (sw.ToString ());

			var expected =
@"public IntPtr MyProperty {
	get { return IntPtr.Zero; }
	set { this.Handle = value; }
}
";

			Assert.AreEqual (expected, sw.ToString ());
		}

		[TestCase (true, false)]
		[TestCase (false, true)]
		[TestCase (true, true)]
		public void UnsafeAccessorBodiesDoNotChangeCallerContract (bool unsafeGetter, bool unsafeSetter)
		{
			var property = new PropertyWriter {
				Name = "Value",
				PropertyType = new TypeReferenceWriter ("int"),
				IsPublic = true,
				HasGet = true,
				HasSet = true,
				HasUnsafeGetterBody = unsafeGetter,
				HasUnsafeSetterBody = unsafeSetter,
			};
			property.GetBody.Add ("return ReadNative ();");
			property.SetBody.Add ("WriteNative (value);");

			var sw = new StringWriter ();
			property.Write (new CodeWriter (sw));

			var getter = unsafeGetter ? "get {\n\t\tunsafe {\n\t\t\treturn ReadNative ();\n\t\t}\n\t}" : "get { return ReadNative (); }";
			var setter = unsafeSetter ? "set {\n\t\tunsafe {\n\t\t\tWriteNative (value);\n\t\t}\n\t}" : "set { WriteNative (value); }";
			Assert.AreEqual ($"public int Value {{\n\t{getter}\n\t{setter}\n}}\n", sw.ToString ().Replace ("\r\n", "\n"));
		}
	}
}
