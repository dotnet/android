#nullable enable
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.Combinatorial;

namespace Microsoft.Android.TrimmingTests;

[TestClass]
[TestCategory ("UsesDevice")]
public class CallbackTests : TrimmingTestBase
{
	// Java calls managed Handler.Callback (boolean) and CharSequence (char) implementations.
	[TestMethod]
	[CombinatorialData]
	public void BooleanAndCharCallbacksAreInvokedWithoutWarnings ([TrimmingConfigurations] TrimmingConfiguration configuration)
	{
		var app = BuildAndRun ("Cases/BooleanAndCharCallbacks.cs", configuration);
		AssertTrimmingExpectations (app);
	}

	// A C# event keeps only the Implementor of the listener it uses.
	[TestMethod]
	[CombinatorialData]
	public void EventKeepsOnlyItsImplementorWithoutWarnings ([TrimmingConfigurations] TrimmingConfiguration configuration)
	{
		var app = BuildAndRun ("Cases/EventImplementors.cs", configuration);
		AssertTrimmingExpectations (app);
	}
}
