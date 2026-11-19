#nullable enable
using NUnit.Framework;

namespace Microsoft.Android.TrimmingTests;

/// <summary>Feature switches whose defaults depend on the trimming configuration.</summary>
[TestFixture]
[Category ("UsesDevice")]
[Parallelizable (ParallelScope.Children)]
public class FeatureSwitchTests : TrimmingTestBase
{
	// DefaultValueAttribute (Type, string) keeps working in partial mode, which suppresses the warning that explains it.
	[Test]
	public void DefaultValueAttributeWorksInPartialModeWithoutWarnings ([TrimmingConfigurations (TrimmingConfiguration.CoreClrPartialTrimming)] TrimmingConfiguration configuration)
	{
		var app = BuildAndRun ("Cases/DefaultValueAttributeInPartialMode.cs", configuration);
		AssertTrimmingExpectations (app);
	}
}
