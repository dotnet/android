#nullable enable
using NUnit.Framework;

namespace Microsoft.Android.TrimmingTests;

[TestFixture]
[Category ("UsesDevice")]
[Parallelizable (ParallelScope.Children)]
public class JavaPeerActivationTests : TrimmingTestBase
{
	// The case app converts closed generic peers with JNIEnv.GetArray<T> () and non-generic collection CopyTo ().
	// The [Kept] activation constructor must survive trimming.
	[Test]
	public void ArrayAndCollectionCopyActivateClosedGenericPeerElementsWithoutWarnings ([TrimmingConfigurations] TrimmingConfiguration configuration)
	{
		var app = BuildAndRun ("Cases/GenericPeerArrayElement.cs", configuration);
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
