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
}
