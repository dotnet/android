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
	public void LayoutBindingsWorkWithoutWarnings ([TrimmingConfigurations] TrimmingConfiguration configuration)
	{
		var app = BuildAndRun ("Cases/LayoutBindings/LayoutBindings.cs", configuration);
		AssertTrimmingExpectations (app);
	}

	// A user TrimmerRootAssembly item without RootMode metadata builds.
	[Test]
	public void TrimmerRootAssemblyWithoutRootModeBuildsWithoutWarnings ([TrimmingConfigurations] TrimmingConfiguration configuration)
	{
		var app = BuildAndRun ("Cases/TrimmerRootAssemblyWithoutRootMode/TrimmerRootAssemblyWithoutRootMode.cs", configuration);
		AssertTrimmingExpectations (app);
	}
}
