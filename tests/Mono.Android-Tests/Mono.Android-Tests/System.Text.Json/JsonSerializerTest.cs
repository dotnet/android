using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Json;

using NUnit.Framework;

namespace System.Text.JsonTests {

	[TestFixture]
	public class JsonSerializerTest {

		[Test]
		[RequiresUnreferencedCode ("Tests trimming unsafe features")]
		public void Serialize ()
		{
			if (RuntimeFeature.IsDynamicCodeSupported) {
				string text = JsonSerializer.Serialize (42);
				Assert.AreEqual ("42", text);
			} else {
				Assert.Ignore ("Reflection-based JSON serialization requires dynamic code.");
			}
		}

		[Test]
		[RequiresUnreferencedCode ("Tests trimming unsafe features")]
		public void Deserialize ()
		{
			if (RuntimeFeature.IsDynamicCodeSupported) {
				object value = JsonSerializer.Deserialize ("42", typeof (int));
				Assert.AreEqual (42, value);
			} else {
				Assert.Ignore ("Reflection-based JSON deserialization requires dynamic code.");
			}
		}
	}
}
