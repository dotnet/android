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
	public void MainActivity_HasJavaConstructor ()
	{
		// MainActivity has an explicit public parameterless ctor without [Register].
		// Activity has [Register(".ctor", "()V", "")] — the scanner should chain from it.
		var peer = FindFixtureByJavaName ("my/app/MainActivity");
		Assert.NotEmpty (peer.JavaConstructors);
	}

	[Fact]
	public void MainActivity_JavaConstructor_HasCorrectSignature ()
	{
		var peer = FindFixtureByJavaName ("my/app/MainActivity");
		var ctorSigs = peer.JavaConstructors.Select (c => c.JniSignature).ToList ();
		Assert.Contains ("()V", ctorSigs);
	}

	[Fact]
	public void MainActivity_HasConstructorMarshalMethod ()
	{
		// The ctor should appear in MarshalMethods with IsConstructor=true
		var peer = FindFixtureByJavaName ("my/app/MainActivity");
		var ctorMethods = peer.MarshalMethods.Where (m => m.IsConstructor).ToList ();
		Assert.NotEmpty (ctorMethods);
		Assert.Equal ("()V", ctorMethods [0].JniSignature);
	}

	[Fact]
	public void SimpleActivity_HasJavaConstructor ()
	{
		// SimpleActivity has no explicit ctor — the compiler generates a default public one.
		// It should chain from Activity's registered ()V ctor.
		var peer = FindFixtureByJavaName ("my/app/SimpleActivity");
		Assert.NotEmpty (peer.JavaConstructors);
		Assert.Equal ("()V", peer.JavaConstructors [0].JniSignature);
	}

	[Fact]
	public void UserActivity_HasNoJavaConstructors ()
	{
		// UserActivity only has an activation ctor (IntPtr, JniHandleOwnership).
		// No non-activation ctor exists, so no Java constructor should be generated.
		var peer = FindFixtureByJavaName ("my/app/UserActivity");
		Assert.Empty (peer.JavaConstructors);
	}

	[Fact]
	public void FullActivity_HasNoJavaConstructors ()
	{
		// FullActivity only has an activation ctor — no Java constructors.
		var peer = FindFixtureByJavaName ("my/app/FullActivity");
		Assert.Empty (peer.JavaConstructors);
	}

	[Fact]
	public void CustomView_StillWorksWithDirectRegister ()
	{
		// CustomView has explicit [Register("<init>", ...)] on its ctors.
		// This must continue to work via Pass 1 (direct collection).
		var peer = FindFixtureByJavaName ("my/app/CustomView");
		Assert.Equal (2, peer.JavaConstructors.Count);
		Assert.Equal ("()V", peer.JavaConstructors [0].JniSignature);
		Assert.Equal ("(Landroid/content/Context;)V", peer.JavaConstructors [1].JniSignature);
	}

	[Fact]
	public void ConstructorMarshalMethod_IsMarkedAsConstructor ()
	{
		var peer = FindFixtureByJavaName ("my/app/MainActivity");
		var ctorMethods = peer.MarshalMethods.Where (m => m.IsConstructor).ToList ();
		Assert.Single (ctorMethods);
		Assert.True (ctorMethods [0].IsConstructor);
	}

	[Fact]
	public void ConstructorMarshalMethod_HasCorrectJniName ()
	{
		var peer = FindFixtureByJavaName ("my/app/MainActivity");
		var ctor = peer.MarshalMethods.First (m => m.IsConstructor);
		// The JNI name should be ".ctor" (matching the base's [Register] name)
		Assert.Equal (".ctor", ctor.JniName);
	}

	[Fact]
	public void OnlyNonActivationCtors_BecomeJavaConstructors ()
	{
		// MainActivity has both:
		//   - public MainActivity() → should become JavaConstructor
		//   - implicit activation ctor from base → should NOT become JavaConstructor
		// Verify exactly 1 JavaConstructor
		var peer = FindFixtureByJavaName ("my/app/MainActivity");
		Assert.Equal (1, peer.JavaConstructors.Count);
	}
}
