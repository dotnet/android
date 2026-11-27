#nullable enable
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.Combinatorial;

namespace Microsoft.Android.TrimmingTests;

[TestClass]
[TestCategory ("UsesDevice")]
public class JavaPeerActivationTests : TrimmingTestBase
{
	// The case app converts a Java array of closed generic peers with JNIEnv.GetArray<T> () and reports PASS only if every
	// element comes back. The [Kept] activation constructor must survive trimming.
	[TestMethod]
	[CombinatorialData]
	public void GetArrayActivatesClosedGenericPeerElementsWithoutWarnings ([TrimmingConfigurations] TrimmingConfiguration configuration)
	{
		var app = BuildAndRun ("Cases/GenericPeerArrayElement.cs", configuration);
		AssertTrimmingExpectations (app);
	}

	// A bound type that the app uses only in a type check of an Android-created object is still activated.
	[Test]
	public void TypeCheckOnJavaCreatedPeerSucceedsWithoutWarnings ([TrimmingConfigurations] TrimmingConfiguration configuration)
	{
		if (configuration == TrimmingConfiguration.CoreClrPartialTrimming) {
			Assert.Ignore ("https://github.com/dotnet/android/issues/13286: ILLink doesn't keep the type map entry of an isinst target in a copied assembly.");
		}
		var app = BuildAndRun ("Cases/TypeCheckOnJavaCreatedPeer.cs", configuration);
		AssertTrimmingExpectations (app);
	}

	// A bound type that the app uses only in a cast of an Android-created object is still activated.
	[Test]
	public void CastToJavaCreatedPeerSucceedsWithoutWarnings ([TrimmingConfigurations] TrimmingConfiguration configuration)
	{
		if (configuration != TrimmingConfiguration.NativeAot) {
			Assert.Ignore ("https://github.com/dotnet/android/issues/13286: ILLink doesn't keep the type map entry of a castclass target.");
		}
		var app = BuildAndRun ("Cases/CastClassToBoundType.cs", configuration);
		AssertTrimmingExpectations (app);
	}

	// JavaAs<T> () returns the interface of a Java object whose most derived binding was trimmed.
	[Test]
	public void JavaAsReturnsInterfaceOfTrimmedClassWithoutWarnings ([TrimmingConfigurations] TrimmingConfiguration configuration)
	{
		var app = BuildAndRun ("Cases/JavaAsInterfaceOfTrimmedClass.cs", configuration);
		AssertTrimmingExpectations (app);
	}

	// A Java object is activated through its Java interfaces when its Java superclasses' bindings are trimmed.
	[Test]
	public void InterfaceThroughTrimmedSuperclassIsActivatedWithoutWarnings ([TrimmingConfigurations] TrimmingConfiguration configuration)
	{
		var app = BuildAndRun ("Cases/InterfaceThroughTrimmedSuperclass.cs", configuration);
		AssertTrimmingExpectations (app);
	}

	// A Java list is converted to a list of a bound interface type from a runtime Type.
	[Test]
	public void InterfaceElementCollectionsAreConvertedWithoutWarnings ([TrimmingConfigurations] TrimmingConfiguration configuration)
	{
		var app = BuildAndRun ("Cases/InterfaceElementCollections.cs", configuration);
		AssertTrimmingExpectations (app);
	}

	// Java creates managed peers through their user constructors: an exception with a cause, and Handler.Post ()'s runnable.
	[Test]
	public void JavaActivatesUserConstructorsWithoutWarnings ([TrimmingConfigurations] TrimmingConfiguration configuration)
	{
		var app = BuildAndRun ("Cases/JavaActivatedUserConstructors.cs", configuration);
		AssertTrimmingExpectations (app);
	}

	// Non-generic JavaCollection/JavaList wrappers must activate closed generic peers when copying into a typed array.
	[TestMethod]
	[CombinatorialData]
	public void CollectionCopyToActivatesClosedGenericPeerElementsWithoutWarnings ([TrimmingConfigurations] TrimmingConfiguration configuration)
	{
		var app = BuildAndRun ("Cases/GenericPeerCollectionCopyTo.cs", configuration);
		AssertTrimmingExpectations (app);
	}

	// The case app converts untyped Java collections to IList<string>, ICollection<string>, and IDictionary<string, string>
	// through JavaConvert, without naming the wrappers; their FromJniHandle () methods must survive trimming.
	[TestMethod]
	[CombinatorialData]
	public void JavaConvertActivatesGenericCollectionWrappersWithoutWarnings ([TrimmingConfigurations] TrimmingConfiguration configuration)
	{
		var app = BuildAndRun ("Cases/GenericCollectionConversion.cs", configuration);
		AssertTrimmingExpectations (app);
	}

	// The case app converts Java collections to IDictionary<string, double>, IDictionary<long, string>, and IList<int> through
	// JavaConvert, without naming the wrappers; NativeAOT maps the mixed dictionaries' constructors from rooted exemplars.
	[Test]
	public void JavaConvertActivatesValueTypeCollectionWrappersWithoutWarnings ([TrimmingConfigurations] TrimmingConfiguration configuration)
	{
		var app = BuildAndRun ("Cases/ValueTypeCollectionConversion.cs", configuration);
		AssertTrimmingExpectations (app);
	}
}
