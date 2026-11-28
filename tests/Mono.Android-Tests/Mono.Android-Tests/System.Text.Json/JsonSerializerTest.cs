using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Json;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace System.Text.JsonTests {

	[TestClass]
	public class JsonSerializerTest {

		[TestMethod]
		[RequiresDynamicCode ("Tests reflection-based JSON serialization.")]
		[RequiresUnreferencedCode ("Tests trimming unsafe features")]
		public void Serialize ()
		{
			if (RuntimeFeature.IsDynamicCodeSupported) {
				string text = JsonSerializer.Serialize (42);
				Assert.AreEqual ("42", text);
			} else {
				Assert.Inconclusive ("Reflection-based JSON serialization requires dynamic code.");
			}
		}

		[TestMethod]
		[RequiresDynamicCode ("Tests reflection-based JSON deserialization.")]
		[RequiresUnreferencedCode ("Tests trimming unsafe features")]
		public void Deserialize ()
		{
			if (RuntimeFeature.IsDynamicCodeSupported) {
				object value = JsonSerializer.Deserialize ("42", typeof (int));
				Assert.AreEqual (42, value);
			} else {
				Assert.Inconclusive ("Reflection-based JSON deserialization requires dynamic code.");
			}
		}
	}
}
