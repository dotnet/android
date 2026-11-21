#nullable enable
using NUnit.Framework;

namespace Microsoft.Android.TrimmingTests;

[TestFixture]
[Category ("UsesDevice")]
[Parallelizable (ParallelScope.Children)]
public class BindingTrimmingTests : TrimmingTestBase
{
	// The app launches, builds without warnings, and only the bindings it uses survive trimming.
	[Test]
	public void UnusedBindingsAreRemovedWithoutWarnings ([TrimmingConfigurations] TrimmingConfiguration configuration)
	{
		var app = BuildAndRun ("Cases/UnusedBindings.cs", configuration);
		AssertTrimmingExpectations (app);
	}

	// Java calls back into a managed click listener on a TextView; the binding members the app uses are kept, the rest are removed.
	[Test]
	public void UsedBindingMembersAreKeptWithoutWarnings ([TrimmingConfigurations] TrimmingConfiguration configuration)
	{
		var app = BuildAndRun ("Cases/UsedBindings.cs", configuration);
		AssertTrimmingExpectations (app);
	}

	// A Java exception is activated as its bound type; unrelated exception bindings are removed.
	[Test]
	public void JavaExceptionIsActivatedAsBoundTypeWithoutWarnings ([TrimmingConfigurations] TrimmingConfiguration configuration)
	{
		var app = BuildAndRun ("Cases/JavaExceptions.cs", configuration);
		AssertTrimmingExpectations (app);
	}

	// Java-implemented interfaces are activated as their invokers; only the invoker members the app calls are kept.
	[Test]
	public void InterfaceInvokersKeepOnlyUsedMembersWithoutWarnings ([TrimmingConfigurations] TrimmingConfiguration configuration)
	{
		var app = BuildAndRun ("Cases/JavaInterfaceInvokers.cs", configuration);
		AssertTrimmingExpectations (app);
	}

	// A java.lang.Integer returned as java.lang.Object is activated as Java.Lang.Integer; unused members are removed.
	[Test]
	public void JavaObjectIsActivatedAsMostDerivedBindingWithoutWarnings ([TrimmingConfigurations] TrimmingConfiguration configuration)
	{
		var app = BuildAndRun ("Cases/JavaObjectActivation.cs", configuration);
		AssertTrimmingExpectations (app);
	}

	// An app that never converts Java boxed values does not keep JavaConvert or the Java.Lang boxing bindings.
	[Test]
	[Ignore ("https://github.com/dotnet/android/issues/13635: every app keeps JavaConvert and the Java.Lang boxing bindings.")]
	public void UnusedBoxingBindingsAreRemovedWithoutWarnings ([TrimmingConfigurations] TrimmingConfiguration configuration)
	{
		var app = BuildAndRun ("Cases/UnusedBoxingBindings.cs", configuration);
		AssertTrimmingExpectations (app);
	}
}
