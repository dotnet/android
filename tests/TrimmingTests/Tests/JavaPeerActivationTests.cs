#nullable enable
using NUnit.Framework;

namespace Microsoft.Android.TrimmingTests;

[TestFixture]
[Category ("UsesDevice")]
[Parallelizable (ParallelScope.Children)]
public class JavaPeerActivationTests : TrimmingTestBase
{
	// The case app converts a Java array of closed generic peers with JNIEnv.GetArray<T> () and reports PASS only if every
	// element comes back. The [Kept] activation constructor must survive trimming.
	[Test]
	public void GetArrayActivatesClosedGenericPeerElementsWithoutWarnings ([TrimmingConfigurations] TrimmingConfiguration configuration)
	{
		var app = BuildAndRun ("Cases/GenericPeerArrayElement.cs", configuration);
		AssertTrimmingExpectations (app);
	}

	// Non-generic JavaCollection/JavaList wrappers must activate closed generic peers when copying into a typed array.
	[Test]
	public void CollectionCopyToActivatesClosedGenericPeerElementsWithoutWarnings ([TrimmingConfigurations] TrimmingConfiguration configuration)
	{
		var app = BuildAndRun ("Cases/GenericPeerCollectionCopyTo.cs", configuration);
		AssertTrimmingExpectations (app);
	}

	// The case app converts untyped Java collections to IList<string>, ICollection<string>, and IDictionary<string, string>
	// through JavaConvert, without naming the wrappers; their FromJniHandle () methods must survive trimming.
	[Test]
	public void JavaConvertActivatesGenericCollectionWrappersWithoutWarnings ([TrimmingConfigurations] TrimmingConfiguration configuration)
	{
		var app = BuildAndRun ("Cases/GenericCollectionConversion.cs", configuration);
		AssertTrimmingExpectations (app);
	}
}
