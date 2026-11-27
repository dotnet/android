#nullable enable
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.Combinatorial;

namespace Microsoft.Android.TrimmingTests;

[TestClass]
[TestCategory ("UsesDevice")]
public class BuildIntegrationTests : TrimmingTestBase
{
	// The code generated for layout bindings builds without trimming warnings and finds its views.
	[TestMethod]
	[CombinatorialData]
	[DoNotParallelize] // Launches an activity; see TrimmingTestBase.
	public void LayoutBindingsWorkWithoutWarnings ([TrimmingConfigurations] TrimmingConfiguration configuration)
	{
		var app = BuildAndRun ("Cases/LayoutBindings/LayoutBindings.cs", configuration);
		AssertTrimmingExpectations (app);
	}

	// A user TrimmerRootAssembly item without RootMode metadata builds. BuildTest.TrimmerRootAssemblyWithoutRootMode covers
	// CoreCLR Release builds, so this only adds NativeAOT.
	[TestMethod]
	[CombinatorialData]
	public void TrimmerRootAssemblyWithoutRootModeBuildsWithoutWarnings ([TrimmingConfigurations (TrimmingConfiguration.NativeAot)] TrimmingConfiguration configuration)
	{
		var app = BuildAndRun ("Cases/TrimmerRootAssemblyWithoutRootMode/TrimmerRootAssemblyWithoutRootMode.cs", configuration);
		AssertTrimmingExpectations (app);
	}
}
