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

	[Fact]
	public void ToXxHash64_KnownInput_HasStableOutput ()
	{
		Assert.Equal ("03e39dfcc696a727", ScannerHashingHelper.ToXxHash64 ("MyApp", "TestFixtures"));
	}
}
