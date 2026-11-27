#nullable enable
using NUnit.Framework;

namespace Microsoft.Android.TrimmingTests;

[TestFixture]
[Category ("UsesDevice")]
[Parallelizable (ParallelScope.Children)]
public class PackageTests : TrimmingTestBase
{
	// A binding package whose types are all trimmed doesn't break the type map at startup.
	[Test]
	public void UnusedBindingPackageIsRemovedWithoutWarnings ([TrimmingConfigurations] TrimmingConfiguration configuration)
	{
		var app = BuildAndRun ("Cases/UnusedBindingPackage.cs", configuration);
		AssertTrimmingExpectations (app);
	}

	// Library code that reads its Android resource IDs works.
	[Test]
	[NonParallelizable] // Launches an activity; see TrimmingTestBase.
	public void LibraryResourceDesignerIsAvailableWithoutWarnings ([TrimmingConfigurations] TrimmingConfiguration configuration)
	{
		var app = BuildAndRun ("Cases/LibraryResourceDesigner.cs", configuration);
		AssertTrimmingExpectations (app);
	}

	// A C# listener for a binding interface that registers a compatibility method gets a Java callable wrapper that compiles.
	// The package's dependencies have mismatched version ranges (NU1608).
	[Test]
	public void CompatibilityMethodListenerCompilesWithoutWarnings ([TrimmingConfigurations] TrimmingConfiguration configuration)
	{
		var app = BuildAndRun ("Cases/CompatibilityMethodListener.cs", configuration, expectedWarnings: ["NU1608"]);
		AssertTrimmingExpectations (app);
	}
}
