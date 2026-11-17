using System;
using System.Collections.Generic;
using System.Text;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Xamarin.Android.RuntimeTests
{
	[TestClass]
	public class EncodingTests
	{
		public static IEnumerable<object []> EncodingTestData ()
		{
			foreach (var info in Encoding.GetEncodings ())
				yield return [info];
		}

		[TestMethod, DynamicData (nameof (EncodingTestData))]
		public void GetAllAvailableEncodings (EncodingInfo info)
		{
			// Requires <MandroidI18n>All</MandroidI18n> or can throw:
			//  System.NotSupportedException : Encoding 37 data could not be found. Make sure you have correct international codeset assembly installed and enabled.
			Encoding enc = info.GetEncoding ();
			Assert.IsNotNull (enc.EncodingName, $"Failed to get Encoding from '{info.DisplayName}'.");
		}
	}
}
