using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Android.Sdk.TrimmableTypeMap;
using NUnit.Framework;

namespace Xamarin.Android.Build.Tests;

[TestFixture]
public class ScannerHashingHelperTests
{
	static IEnumerable<TestCaseData> HashCases ()
	{
		yield return new TestCaseData ("", "", "bcc0865dfe733aa9", "d224ae81d023c9f0").SetName ("HashCompatibility_Empty");
		yield return new TestCaseData ("", "Assembly", "78db731841b75ad2", "eed19f9ba5398d9a").SetName ("HashCompatibility_EmptyNamespace");
		yield return new TestCaseData ("Namespace", "", "59eea696e948b083", "18a7200f8e48fbea").SetName ("HashCompatibility_EmptyAssembly");
		yield return new TestCaseData ("hello", "Assembly", "887d83bb6ec7d6da", "66f7a697cfdc67b1").SetName ("HashCompatibility_Ascii");
		yield return new TestCaseData ("\u540d\u524d.\u0394", "\u7a0b\u5e8f\u96c6", "b115c97561026b33", "9bef221407e9e4a6").SetName ("HashCompatibility_Unicode");
		yield return new TestCaseData ("\ud800", "\udfff", "b6545f34ddfdf03b", "7bd8057a1b0178a5").SetName ("HashCompatibility_InvalidSurrogates");
		yield return new TestCaseData (new string ('N', 254), "A", "e0969f5b4a978c2d", "25b5a5feb1c48b8c").SetName ("HashCompatibility_AsciiStackThreshold");
		yield return new TestCaseData (new string ('N', 255), "A", "75b1a1ccf9ca3e19", "01695f0d05dcaf36").SetName ("HashCompatibility_AsciiHeapThreshold");
		yield return new TestCaseData (new string ('\u00e9', 127), "A", "c267a6d250cd11ac", "f800ff1974d4d72a").SetName ("HashCompatibility_UnicodeStackThreshold");
		yield return new TestCaseData (new string ('\u00e9', 128), "A", "acaa3b013a97a0b6", "68497ba76fde6846").SetName ("HashCompatibility_UnicodeHeapThreshold");
		yield return new TestCaseData (new string ('N', 4096), new string ('A', 4096), "b68153fbcaeb5981", "da09d94407694c3e").SetName ("HashCompatibility_Long");
	}

	[TestCaseSource (nameof (HashCases))]
	public void HashCompatibility (string ns, string assemblyName, string expected, string legacyExpected)
	{
		Assert.AreEqual (expected, ScannerHashingHelper.ToCrc64 (ns, assemblyName));
		if (!BitConverter.IsLittleEndian) {
			var bytes = Encoding.UTF8.GetBytes (ns + ":" + assemblyName);
			ulong crc = ulong.MaxValue;
			for (int i = 0; i < bytes.Length; i++) {
				int index = i < bytes.Length / 8 * 8 ? i / 8 * 8 + 7 - i % 8 : i;
				crc ^= bytes [index];
				for (int bit = 0; bit < 8; bit++)
					crc = (crc >> 1) ^ ((crc & 1) == 0 ? 0 : 0x95ac9329ac4bc9b5UL);
			}
			var hash = BitConverter.GetBytes (crc ^ (ulong) bytes.Length);
			Array.Reverse (hash);
			legacyExpected = Convert.ToHexString (hash).ToLowerInvariant ();
		}
		Assert.AreEqual (legacyExpected, ScannerHashingHelper.ToLegacyCrc64 (ns, assemblyName));
	}
}
