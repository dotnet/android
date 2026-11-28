#nullable enable
using NUnit.Framework;

namespace Microsoft.Android.TrimmingTests;

[TestFixture]
[Category ("UsesDevice")]
[Parallelizable (ParallelScope.Children)]
public class CallbackTests : TrimmingTestBase
{
	// Java calls managed Handler.Callback (boolean) and CharSequence (char) implementations.
	[Test]
	public void BooleanAndCharCallbacksAreInvokedWithoutWarnings ([TrimmingConfigurations] TrimmingConfiguration configuration)
	{
		var app = BuildAndRun ("Cases/BooleanAndCharCallbacks.cs", configuration);
		AssertTrimmingExpectations (app);
	}

	// A C# event keeps only the Implementor of the listener it uses.
	[Test]
	public void EventKeepsOnlyItsImplementorWithoutWarnings ([TrimmingConfigurations] TrimmingConfiguration configuration)
	{
		var app = BuildAndRun ("Cases/EventImplementors.cs", configuration);
		AssertTrimmingExpectations (app);
	}
}
