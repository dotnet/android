using System.Linq;
using Xunit;

namespace Microsoft.Android.Sdk.TrimmableTypeMap.Tests;

/// <summary>
/// Tests for covariant return type overrides.
/// When a derived type overrides a base method with a narrower C# return type,
/// the JCW should use the base method's JNI signature (with the original return type).
/// </summary>
public class CovariantReturnTests : FixtureTestBase
{
	[Fact]
	public void CovariantOverride_DetectedWithBaseJniSignatureAndConnector ()
	{
		var peer = FindFixtureByJavaName ("my/app/CovariantDerived");
		var getResult = peer.MarshalMethods.First (m => m.JniName == "getResult");
		Assert.Equal ("()Ljava/lang/Object;", getResult.JniSignature);
		Assert.Equal ("GetGetResultHandler", getResult.Connector);
	}
}
