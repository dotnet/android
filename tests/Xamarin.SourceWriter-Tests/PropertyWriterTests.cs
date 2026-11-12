using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Xamarin.SourceWriter.Tests
{
	[TestClass]
	public class PropertyWriterTests
	{
		[TestMethod]
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
	}
}
