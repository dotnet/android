using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Xamarin.SourceWriter.Tests
{
	[TestClass]
	public class TypeReferenceWriterTests
	{
		[TestMethod]
		public void Constructor ()
		{
			var t = new TypeReferenceWriter ("String");

			Assert.IsNull (t.Namespace);
			Assert.AreEqual ("String", t.Name);

			t = new TypeReferenceWriter ("System.String");

			Assert.AreEqual ("System", t.Namespace);
			Assert.AreEqual ("String", t.Name);

			t = new TypeReferenceWriter ("System.Internal.String");

			Assert.AreEqual ("System.Internal", t.Namespace);
			Assert.AreEqual ("String", t.Name);
		}

		[TestMethod]
		public void NotNull ()
		{
			var t = new TypeReferenceWriter ("string");

			var sw = new StringWriter ();
			var cw = new CodeWriter (sw);

			t.WriteTypeReference (cw);

			Assert.AreEqual ("string ", sw.ToString ());
		}

		[TestMethod]
		public void Nullable ()
		{
			var t = new TypeReferenceWriter ("string") { Nullable = true };

			var sw = new StringWriter ();
			var cw = new CodeWriter (sw);

			t.WriteTypeReference (cw);

			Assert.AreEqual ("string? ", sw.ToString ());
		}
	}
}
