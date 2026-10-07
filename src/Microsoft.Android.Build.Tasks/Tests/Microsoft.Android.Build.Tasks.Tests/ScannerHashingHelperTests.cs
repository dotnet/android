using Microsoft.Android.Sdk.TrimmableTypeMap;
using NUnit.Framework;

namespace Xamarin.Android.Build.Tests;

[TestFixture]
public class ScannerHashingHelperTests
{
	[TestCase ("", "Assembly", "78db731841b75ad2", "eed19f9ba5398d9a")]
	[TestCase ("\u540d\u524d.\u0394", "\u7a0b\u5e8f\u96c6", "b115c97561026b33", "9bef221407e9e4a6")]
	public void HashCompatibility (string ns, string assemblyName, string expected, string legacyExpected)
	{
		Assert.AreEqual (expected, ScannerHashingHelper.ToCrc64 (ns, assemblyName));
		Assert.AreEqual (legacyExpected, ScannerHashingHelper.ToLegacyCrc64 (ns, assemblyName));
	}
}
