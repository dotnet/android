using System;
using System.Linq;

using NUnit.Framework;

using Java.Interop.Tools.JavaSource;
using Xamarin.Android.Tools.ApiXmlAdjuster;

namespace Java.Interop.Tools.JavaSource.Tests
{
	[TestFixture]
	public class JavaStubParserTests
	{
		[Test]
		public void TryParse_EmptySource ()
		{
			var parser  = new JavaStubParser ();
			var package = parser.TryParse ("");
			Assert.IsNotNull (package);
			Assert.AreEqual (null, package.Name);
			Assert.AreEqual (0, package.Types.Count);
		}

		[Test]
		public void TryParse_SimpleClass ()
		{
			var parser  = new JavaStubParser ();
			var package = parser.TryParse (@"
class Example {
	public static void m (String text) {
	}
}
");
			Assert.IsNotNull (package);
			Assert.AreEqual (null, package.Name);
			Assert.AreEqual (1, package.Types.Count);

			var Example_Type = package.Types [0];
			Assert.AreEqual ("Example", Example_Type.FullName);

			Assert.AreEqual (1, Example_Type.Members.Count);
			var Example_m = Example_Type.Members [0] as JavaMethod;
			Assert.IsNotNull (Example_m);
			Assert.AreEqual ("m", Example_m.Name);
			Assert.AreEqual ("void", Example_m.Return);
			Assert.AreEqual (1, Example_m.Parameters.Count);
			Assert.AreEqual ("String", Example_m.Parameters [0].Type);
			Assert.AreEqual ("text", Example_m.Parameters [0].Name);
		}
	}
}

