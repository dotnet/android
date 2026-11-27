using System.Text.Json;
using NUnit.Framework;

namespace Xamarin.Android.Build.Tests.Tasks {

	[TestFixture]
	public class JsonExtensionsTests {

		[TestCase ("9007199254740993")]
		[TestCase ("9223372036854775807")]
		[TestCase ("18446744073709551615")]
		public void ToNodePreservesLargeNumbers (string number)
		{
			var json = $"{{\"value\":{number}}}";
			using var document = JsonDocument.Parse (json);

			var actual = document.RootElement.ToNode ()?.ToJsonString ();

			Assert.AreEqual (json, actual);
		}
	}
}
