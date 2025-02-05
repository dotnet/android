using System;
using System.Text;

using NUnit.Framework;

namespace Xamarin.Android.RuntimeTests
{
	[TestFixture]
	public class EncodingTests
	{
		EncodingInfo[] EncodingTestData = Encoding.GetEncodings ();

		[Test, TestCaseSource (nameof (EncodingTestData))]
		public void GetAllAvailableEncodings (EncodingInfo info)
		{
			// Requires <MandroidI18n>All</MandroidI18n> or can throw:
			//  System.NotSupportedException : Encoding 37 data could not be found. Make sure you have correct international codeset assembly installed and enabled.
			Encoding enc = info.GetEncoding ();
			Assert.IsNotNull (enc.EncodingName, $"Failed to get Encoding from '{info.DisplayName}'.");
		}
	}
}
