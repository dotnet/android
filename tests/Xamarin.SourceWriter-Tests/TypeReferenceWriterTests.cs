using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using NUnit.Framework;

namespace Xamarin.SourceWriter.Tests
{
	[TestFixture]
	public class TypeReferenceWriterTests
	{
		[Test]
		public void Constructor ()
		{
			var t = new TypeReferenceWriter ("String");

			Assert.AreEqual (null, t.Namespace);
			Assert.AreEqual ("String", t.Name);

			t = new TypeReferenceWriter ("System.String");

			Assert.AreEqual ("System", t.Namespace);
			Assert.AreEqual ("String", t.Name);

			t = new TypeReferenceWriter ("System.Internal.String");

			Assert.AreEqual ("System.Internal", t.Namespace);
			Assert.AreEqual ("String", t.Name);
		}

		[Test]
		public void NotNull ()
		{
			var t = new TypeReferenceWriter ("string");

			var sw = new StringWriter ();
			var cw = new CodeWriter (sw);

			t.WriteTypeReference (cw);

			Assert.AreEqual ("string ", sw.ToString ());
		}

		[Test]
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
