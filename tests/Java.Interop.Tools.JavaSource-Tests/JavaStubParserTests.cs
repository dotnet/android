using System;
using System.Linq;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Java.Interop.Tools.JavaSource;
using Xamarin.Android.Tools.ApiXmlAdjuster;

namespace Java.Interop.Tools.JavaSource.Tests
{
	[TestClass]
	public class JavaStubParserTests
	{
		[TestMethod]
		public void TryParse_EmptySource ()
		{
			var parser  = new JavaStubParser ();
			var package = parser.TryParse ("");
			Assert.IsNotNull (package);
			Assert.IsNull (package.Name);
			Assert.AreEqual (0, package.Types.Count);
		}

		[TestMethod]
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
			Assert.IsNull (package.Name);
			Assert.AreEqual (1, package.Types.Count);

			var Example_Type = package.AllTypes.First ();
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

