#nullable enable
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.Combinatorial;

namespace Microsoft.Android.TrimmingTests;

[TestClass]
[TestCategory ("UsesDevice")]
public class PackageTests : TrimmingTestBase
{
	// A binding package whose types are all trimmed doesn't break the type map at startup.
	[TestMethod]
	[CombinatorialData]
	public void UnusedBindingPackageIsRemovedWithoutWarnings ([TrimmingConfigurations] TrimmingConfiguration configuration)
	{
		var app = BuildAndRun ("Cases/UnusedBindingPackage.cs", configuration);
		AssertTrimmingExpectations (app);
	}

	// Library code that reads its Android resource IDs works.
	[TestMethod]
	[CombinatorialData]
	[DoNotParallelize] // Launches an activity; see TrimmingTestBase.
	public void LibraryResourceDesignerIsAvailableWithoutWarnings ([TrimmingConfigurations] TrimmingConfiguration configuration)
	{
		var app = BuildAndRun ("Cases/LibraryResourceDesigner.cs", configuration);
		AssertTrimmingExpectations (app);
	}

	// A C# listener for a binding interface that registers a compatibility method gets a Java callable wrapper that compiles.
	// The package's dependencies have mismatched version ranges (NU1608).
	[TestMethod]
	[CombinatorialData]
	public void CompatibilityMethodListenerCompilesWithoutWarnings ([TrimmingConfigurations] TrimmingConfiguration configuration)
	{
		var app = BuildAndRun ("Cases/CompatibilityMethodListener.cs", configuration, expectedWarnings: ["NU1608"]);
		AssertTrimmingExpectations (app);
	}
}
