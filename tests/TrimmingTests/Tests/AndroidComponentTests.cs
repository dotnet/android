#nullable enable
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.Combinatorial;

namespace Microsoft.Android.TrimmingTests;

[TestClass]
[TestCategory ("UsesDevice")]
public class AndroidComponentTests : TrimmingTestBase
{
	// Android creates a receiver and a service that the app refers to only by their Java names.
	[TestMethod]
	[CombinatorialData]
	public void ComponentsReferencedOnlyByNameAreKeptWithoutWarnings ([TrimmingConfigurations] TrimmingConfiguration configuration)
	{
		var app = BuildAndRun ("Cases/ManifestComponentsByName.cs", configuration);
		AssertTrimmingExpectations (app);
	}

	// Android creates the custom application, its backup agent and its manage-space activity, and sees its meta-data.
	[TestMethod]
	[CombinatorialData]
	public void CustomApplicationAndItsComponentsAreKeptWithoutWarnings ([TrimmingConfigurations] TrimmingConfiguration configuration)
	{
		var app = BuildAndRun ("Cases/CustomApplication.cs", configuration);
		AssertTrimmingExpectations (app);
	}

	// Android creates an activity that only a hand-written AndroidManifest.xml entry refers to.
	[TestMethod]
	[CombinatorialData]
	[DoNotParallelize] // Launches an activity; see TrimmingTestBase.
	public void ManifestOnlyActivityIsKeptWithoutWarnings ([TrimmingConfigurations] TrimmingConfiguration configuration)
	{
		var app = BuildAndRun ("Cases/ManifestOnlyActivity/ManifestOnlyActivity.cs", configuration);
		AssertTrimmingExpectations (app);
	}

	// A layout XML file inflates a custom view that no managed code refers to.
	[TestMethod]
	[CombinatorialData]
	[DoNotParallelize] // Launches an activity; see TrimmingTestBase.
	public void CustomViewFromLayoutIsKeptWithoutWarnings ([TrimmingConfigurations] TrimmingConfiguration configuration)
	{
		var app = BuildAndRun ("Cases/CustomViewFromLayout/CustomViewFromLayout.cs", configuration);
		AssertTrimmingExpectations (app);
	}
}
