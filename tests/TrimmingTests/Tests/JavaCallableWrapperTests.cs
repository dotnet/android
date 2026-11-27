#nullable enable
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.Combinatorial;

namespace Microsoft.Android.TrimmingTests;

[TestClass]
[TestCategory ("UsesDevice")]
public class JavaCallableWrapperTests : TrimmingTestBase
{
	// A peer reachable only through [Export] methods called from Java is kept.
	[TestMethod]
	[CombinatorialData]
	public void ExportOnlyPeerIsCallableFromJavaWithoutWarnings ([TrimmingConfigurations] TrimmingConfiguration configuration)
	{
		var app = BuildAndRun ("Cases/ExportOnlyPeer.cs", configuration);
		AssertTrimmingExpectations (app);
	}

	// [JavascriptInterface] reaches the Java callable wrapper as @JavascriptInterface.
	[TestMethod]
	[CombinatorialData]
	public void JavascriptInterfaceAnnotationIsGeneratedWithoutWarnings ([TrimmingConfigurations] TrimmingConfiguration configuration)
	{
		var app = BuildAndRun ("Cases/JavascriptInterfaceAnnotation.cs", configuration);
		AssertTrimmingExpectations (app);
	}

	// Java callable wrapper signatures use the Java name given by [Service (Name = ...)].
	[TestMethod]
	[CombinatorialData]
	public void NamedComponentIsUsedInSignaturesWithoutWarnings ([TrimmingConfigurations] TrimmingConfiguration configuration)
	{
		var app = BuildAndRun ("Cases/NamedComponentInSignature.cs", configuration);
		AssertTrimmingExpectations (app);
	}

	// [Export (Throws = ...)] reaches the generated Java constructor and method.
	[TestMethod]
	[CombinatorialData]
	public void ExportThrowsIsGeneratedWithoutWarnings ([TrimmingConfigurations] TrimmingConfiguration configuration)
	{
		var app = BuildAndRun ("Cases/ExportThrows/ExportThrows.cs", configuration);
		AssertTrimmingExpectations (app);
	}

	// Java peers with non-ASCII names keep their names and are activated from Java.
	[TestMethod]
	[CombinatorialData]
	public void UnicodeJavaNamesAreActivatedWithoutWarnings ([TrimmingConfigurations] TrimmingConfiguration configuration)
	{
		var app = BuildAndRun ("Cases/UnicodeJavaNames.cs", configuration);
		AssertTrimmingExpectations (app);
	}

	// A Java peer whose constructors and type map proxy were trimmed doesn't keep its Java callable wrapper either. Partial
	// trimming keeps the app assembly whole, so the peer stays activatable there; other cases cover activation from Java.
	[TestMethod]
	[CombinatorialData]
	[Ignore ("https://github.com/dotnet/android/issues/13650: D8 builds and NativeAOT keep every Java callable wrapper.")]
	public void OrphanedJavaCallableWrapperIsRemovedWithoutWarnings ([TrimmingConfigurations (TrimmingConfiguration.CoreClrFullTrimming, TrimmingConfiguration.NativeAot)] TrimmingConfiguration configuration)
	{
		var app = BuildAndRun ("Cases/OrphanedJavaCallableWrapper.cs", configuration);
		AssertTrimmingExpectations (app);
	}
}
