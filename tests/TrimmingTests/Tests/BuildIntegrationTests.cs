#nullable enable
using NUnit.Framework;

namespace Microsoft.Android.TrimmingTests;

[TestFixture]
[Category ("UsesDevice")]
[Parallelizable (ParallelScope.Children)]
public class BuildIntegrationTests : TrimmingTestBase
{
	// The code generated for layout bindings builds without trimming warnings and finds its views.
	[Test]
	[NonParallelizable] // Launches an activity; see TrimmingTestBase.
	public void LayoutBindingsWorkWithoutWarnings ([TrimmingConfigurations] TrimmingConfiguration configuration)
	{
		var app = BuildAndRun ("Cases/LayoutBindings/LayoutBindings.cs", configuration);
		AssertTrimmingExpectations (app);
	}

	// A user TrimmerRootAssembly item without RootMode metadata builds. BuildTest.TrimmerRootAssemblyWithoutRootMode covers
	// CoreCLR Release builds, so this only adds NativeAOT.
	[Test]
	public void TrimmerRootAssemblyWithoutRootModeBuildsWithoutWarnings ([TrimmingConfigurations (TrimmingConfiguration.NativeAot)] TrimmingConfiguration configuration)
	{
		var app = BuildAndRun ("Cases/TrimmerRootAssemblyWithoutRootMode/TrimmerRootAssemblyWithoutRootMode.cs", configuration);
		AssertTrimmingExpectations (app);
	}
}
