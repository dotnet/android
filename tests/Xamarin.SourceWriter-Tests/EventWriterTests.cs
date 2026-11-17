using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using NUnit.Framework;

namespace Xamarin.SourceWriter.Tests
{
	[TestFixture]
	public class EventWriterTests
	{
		[Test]
		public void Basics ()
		{
			var ev = new EventWriter { Name = "MyEvent", IsPublic = true, EventType = new TypeReferenceWriter ("EventHandler") };

			var sw = new StringWriter ();
			var writer = new CodeWriter (sw);

			ev.Write (writer);

			var expected = @"public event EventHandler MyEvent;
";

			Assert.AreEqual (expected, sw.ToString ());
		}
	}
}
