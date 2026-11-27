#nullable enable
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.Combinatorial;

namespace Microsoft.Android.TrimmingTests;

/// <summary>Feature switches whose defaults depend on the trimming configuration.</summary>
[TestClass]
[TestCategory ("UsesDevice")]
public class FeatureSwitchTests : TrimmingTestBase
{
	// DefaultValueAttribute (Type, string) keeps working in partial mode, which suppresses the warning that explains it.
	[TestMethod]
	[CombinatorialData]
	public void DefaultValueAttributeWorksInPartialModeWithoutWarnings ([TrimmingConfigurations (TrimmingConfiguration.CoreClrPartialTrimming)] TrimmingConfiguration configuration)
	{
		var app = BuildAndRun ("Cases/DefaultValueAttributeInPartialMode.cs", configuration);
		AssertTrimmingExpectations (app);
	}
}
