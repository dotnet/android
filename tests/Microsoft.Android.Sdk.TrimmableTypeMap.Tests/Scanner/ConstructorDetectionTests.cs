using System.Linq;
using Xunit;

namespace Microsoft.Android.Sdk.TrimmableTypeMap.Tests;

/// <summary>
/// Tests for constructor detection: the scanner must find Java constructors for user types
/// that have non-activation constructors, even when those constructors don't have [Register].
/// The legacy JCW generator chains from base registered ctors to derived unregistered ctors.
/// </summary>
public class ConstructorDetectionTests : FixtureTestBase
{
	[Fact]
	public void MainActivity_ChainsFromBaseRegisteredCtor ()
	{
		// MainActivity has an explicit public parameterless ctor without [Register].
		// Activity has [Register(".ctor", "()V", "")] — the scanner should chain from it.
		var peer = FindFixtureByJavaName ("my/app/MainActivity");

		// Should produce exactly one JavaConstructor with correct signature
		Assert.Equal (1, peer.JavaConstructors.Count);
		Assert.Equal ("()V", peer.JavaConstructors [0].JniSignature);

		// The ctor should appear in MarshalMethods as a constructor
		var ctorMethods = peer.MarshalMethods.Where (m => m.IsConstructor).ToList ();
		Assert.Single (ctorMethods);
		Assert.Equal ("()V", ctorMethods [0].JniSignature);
		Assert.Equal (".ctor", ctorMethods [0].JniName);
	}

	[Fact]
	public void SimpleActivity_ChainsImplicitDefaultCtor ()
	{
		// SimpleActivity has no explicit ctor — the compiler generates a default public one.
		// It should chain from Activity's registered ()V ctor.
		var peer = FindFixtureByJavaName ("my/app/SimpleActivity");
		Assert.NotEmpty (peer.JavaConstructors);
		Assert.Equal ("()V", peer.JavaConstructors [0].JniSignature);
	}

	[Fact]
	public void UserActivity_ActivationCtor_AcceptedViaFallback ()
	{
		// UserActivity only has an activation ctor (IntPtr, JniHandleOwnership).
		// Legacy CecilImporter accepts it via the parameterless fallback because
		// Activity has a registered ()V ctor. The ctor's managed parameter types
		// are mapped to JNI Object types.
		var peer = FindFixtureByJavaName ("my/app/UserActivity");
		Assert.NotEmpty (peer.JavaConstructors);
	}

	[Fact]
	public void FullActivity_ActivationCtor_AcceptedViaFallback ()
	{
		// Same as UserActivity — activation ctor accepted via parameterless fallback.
		var peer = FindFixtureByJavaName ("my/app/FullActivity");
		Assert.NotEmpty (peer.JavaConstructors);
	}

	[Fact]
	public void CustomView_DirectRegisterNotAffected ()
	{
		// CustomView has explicit [Register("<init>", ...)] on its ctors.
		// This must continue to work via Pass 1 (direct collection).
		var peer = FindFixtureByJavaName ("my/app/CustomView");
		Assert.Equal (2, peer.JavaConstructors.Count);
		Assert.Equal ("()V", peer.JavaConstructors [0].JniSignature);
		Assert.Equal ("(Landroid/content/Context;)V", peer.JavaConstructors [1].JniSignature);
	}

	[Fact]
	public void JiStyleView_JniConstructorSignatureAttribute ()
	{
		// JiStyleView uses [JniConstructorSignature] instead of [Register].
		// The scanner must recognize this attribute and collect both ctors.
		var peer = FindFixtureByJavaName ("my/app/JiStyleView");
		Assert.Equal (2, peer.JavaConstructors.Count);
		Assert.Equal ("()V", peer.JavaConstructors [0].JniSignature);
		Assert.Equal ("(Landroid/content/Context;)V", peer.JavaConstructors [1].JniSignature);
	}

	[Fact]
	public void ActivityWithCustomCtor_ParameterlessFallback ()
	{
		// ActivityWithCustomCtor has a ctor(string) that doesn't match any base registered
		// ctor's params. But Activity has a registered ()V ctor, so the parameterless
		// fallback path should accept it (matching legacy CecilImporter.cs:394-397).
		var peer = FindFixtureByJavaName ("my/app/ActivityWithCustomCtor");
		var ctorSigs = peer.JavaConstructors.Select (c => c.JniSignature).ToList ();
		Assert.Contains ("(Ljava/lang/String;)V", ctorSigs);
	}
}
