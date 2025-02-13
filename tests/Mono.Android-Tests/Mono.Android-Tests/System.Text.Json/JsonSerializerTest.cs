using System;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using NUnit.Framework;

namespace System.Text.JsonTests {

	[TestFixture]
	public class JsonSerializerTest {

		[Test]
		[RequiresUnreferencedCode ("Tests trimming unsafe features")]
		public void Serialize ()
		{
			// FIXME: https://github.com/xamarin/xamarin-android/issues/8724
			#pragma warning disable IL3050
			string text = JsonSerializer.Serialize(42);
			#pragma warning restore IL3050
			Assert.AreEqual("42", text);
		}

		[Test]
		[RequiresUnreferencedCode ("Tests trimming unsafe features")]
		public void Deserialize ()
		{
			// FIXME: https://github.com/xamarin/xamarin-android/issues/8724
			#pragma warning disable IL3050
			object value = JsonSerializer.Deserialize("42", typeof(int));
			#pragma warning restore IL3050
			Assert.AreEqual(42, value);
		}
	}
}
