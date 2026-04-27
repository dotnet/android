using Xunit;

namespace Microsoft.Android.Sdk.TrimmableTypeMap.Tests;

public class ScannerHashingHelperTests
{
	[Theory]
	[InlineData ("MyApp", "TestFixtures", "ec59e927bc71f4d8")]
	[InlineData ("System.Collections.Generic", "My.Assembly", "9ff866e93b19f500")]
	[InlineData ("Hello", "World", "f6bdbfa73a558c54")]
	public void ToLegacyCrc64_KnownInputs_HaveStableOutput (string ns, string assemblyName, string expected)
	{
		Assert.Equal (expected, ScannerHashingHelper.ToLegacyCrc64 (ns, assemblyName));
	}

	[Theory]
	[InlineData ("MyApp", "TestFixtures", "eb3df85c64aa1af6")]
	[InlineData ("System.Collections.Generic", "My.Assembly", "403b37c9b3a5014d")]
	[InlineData ("Hello", "World", "4f2a517f331e7a2c")]
	public void ToCrc64_KnownInputs_HaveStableOutput (string ns, string assemblyName, string expected)
	{
		Assert.Equal (expected, ScannerHashingHelper.ToCrc64 (ns, assemblyName));
	}
}
