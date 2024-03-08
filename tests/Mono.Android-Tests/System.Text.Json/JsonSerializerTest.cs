using System;
using System.Text.Json;

using NUnit.Framework;

namespace System.Text.JsonTests {

	[TestFixture]
	public class JsonSerializerTest {

		[Test]
		public void Serialize ()
		{
			string text = JsonSerializer.Serialize(42);
			Assert.AreEqual("42", text);
		}

		[Test]
		public void Deserialize ()
		{
			object value = JsonSerializer.Deserialize("42", typeof(int));
			Assert.AreEqual(42, value);
		}
	}
}
