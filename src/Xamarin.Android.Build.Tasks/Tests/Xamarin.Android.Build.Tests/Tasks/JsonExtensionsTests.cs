using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Xamarin.Android.Build.Tests.Tasks {

	[TestClass]
	[DoNotParallelize]
	public class JsonExtensionsTests {

		[TestMethod]
		[DataRow ("9007199254740993")]
		[DataRow ("9223372036854775807")]
		[DataRow ("18446744073709551615")]
		public void ToNodePreservesLargeNumbers (string number)
		{
			var json = $"{{\"value\":{number}}}";
			using var document = JsonDocument.Parse (json);

			var actual = document.RootElement.ToNode ()?.ToJsonString ();

			Assert.AreEqual (json, actual);
		}
	}
}
