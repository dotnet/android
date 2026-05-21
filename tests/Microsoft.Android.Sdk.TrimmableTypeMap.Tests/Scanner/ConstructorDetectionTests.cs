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

		// The ctor should appear in MarshalMethods as a constructor
		var ctorMethod = Assert.Single (peer.MarshalMethods, m => m.IsConstructor);
		Assert.Equal ("()V", ctorMethod.JniSignature);
		Assert.Equal (".ctor", ctorMethod.JniName);

		// Should produce exactly one JavaConstructor with correct signature
		var javaCtor = Assert.Single (peer.JavaConstructors);
		Assert.Equal ("()V", javaCtor.JniSignature);
	}

	[Fact]
	public void SimpleActivity_ChainsImplicitDefaultCtor ()
	{
		// SimpleActivity has no explicit ctor — the compiler generates a default public one.
		// It should chain from Activity's registered ()V ctor.
		var peer = FindFixtureByJavaName ("my/app/SimpleActivity");
		var javaCtor = Assert.Single (peer.JavaConstructors);
		Assert.Equal ("()V", javaCtor.JniSignature);
	}

	[Fact]
	public void UserActivity_OnlyGetsBaseCtorSeed ()
	{
		// UserActivity only has an activation ctor (IntPtr, JniHandleOwnership).
		// The activation ctor is rejected by the fallback (IntPtr is not a Java type).
		// Only the base ()V seed from Activity remains.
		var peer = FindFixtureByJavaName ("my/app/UserActivity");
		var javaCtor = Assert.Single (peer.JavaConstructors);
		Assert.Equal ("()V", javaCtor.JniSignature);
	}

	[Fact]
	public void FullActivity_OnlyGetsBaseCtorSeed ()
	{
		// Same as UserActivity — activation ctor rejected, only base ()V seed.
		var peer = FindFixtureByJavaName ("my/app/FullActivity");
		var javaCtor = Assert.Single (peer.JavaConstructors);
		Assert.Equal ("()V", javaCtor.JniSignature);
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
		// ctor's params. Activity has a registered ()V ctor, so the parameterless fallback
		// accepts it — Java calls super() and delegates args via nctor_N(p0).
		var peer = FindFixtureByJavaName ("my/app/ActivityWithCustomCtor");
		var ctorSigs = peer.JavaConstructors.Select (c => c.JniSignature).ToList ();
		Assert.Contains ("(Ljava/lang/String;)V", ctorSigs);

		// Verify the fallback ctor uses super() (empty SuperArgumentsString)
		var fallbackCtor = peer.JavaConstructors.First (c => c.JniSignature == "(Ljava/lang/String;)V");
		Assert.Equal ("", fallbackCtor.SuperArgumentsString);
	}

	[Fact]
	public void CustomDialog_SameArityTypeMismatch_UsesParameterlessFallback ()
	{
		// CustomDialog has ctor(string). DialogBase has registered ctor(Context).
		// Same arity (1 param) but different types — must NOT be treated as "already covered".
		// DialogBase also has registered ()V → parameterless fallback accepts ctor(string).
		var peer = FindFixtureByJavaName ("my/app/CustomDialog");
		var ctorSigs = peer.JavaConstructors.Select (c => c.JniSignature).ToList ();

		// Base seeds: ()V and (Landroid/content/Context;)V from DialogBase
		Assert.Contains ("()V", ctorSigs);
		Assert.Contains ("(Landroid/content/Context;)V", ctorSigs);

		// Fallback: ctor(string) accepted via parameterless base ctor
		Assert.Contains ("(Ljava/lang/String;)V", ctorSigs);
	}

	[Fact]
	public void HasMatchingManagedCtor_False_WhenSameArityCtorHasDifferentParameterType ()
	{
		var peer = FindFixtureByJavaName ("my/app/CustomDialog");

		var contextCtor = Assert.Single (peer.JavaConstructors, c => c.JniSignature == "(Landroid/content/Context;)V");
		Assert.False (contextCtor.HasMatchingManagedCtor,
			"CustomDialog has only a string ctor; the inherited Context Java ctor must not match by arity alone.");

		var stringCtor = Assert.Single (peer.JavaConstructors, c => c.JniSignature == "(Ljava/lang/String;)V");
		Assert.True (stringCtor.HasMatchingManagedCtor,
			"The parameterless-fallback string Java ctor should match CustomDialog(string).");
	}

	[Fact]
	public void ActivityWithMultiParamCtor_FallbackComputesFullSignature ()
	{
		// ctor(string, int, bool) should produce "(Ljava/lang/String;IZ)V"
		// via BuildJniCtorSignature mapping each managed type to JNI.
		var peer = FindFixtureByJavaName ("my/app/ActivityWithMultiParamCtor");
		var ctorSigs = peer.JavaConstructors.Select (c => c.JniSignature).ToList ();
		Assert.Contains ("(Ljava/lang/String;IZ)V", ctorSigs);
	}

	[Fact]
	public void ViewArrayCtor_ResolvesObjectArraySignature ()
	{
		// ctor(View[]) should produce "([Landroid/view/View;)V"
		// via TryResolveJniObjectDescriptor looking up View's [Register] JNI name
		var peer = FindFixtureByJavaName ("my/app/ViewArrayActivity");
		var ctorSigs = peer.JavaConstructors.Select (c => c.JniSignature).ToList ();
		Assert.Contains ("([Landroid/view/View;)V", ctorSigs);
	}

	[Fact]
	public void UnsignedPrimitiveCtor_MapsCorrectly ()
	{
		// ctor(ushort, uint, ulong) should produce "(SIJ)V"
		// UInt16→S, UInt32→I, UInt64→J
		var peer = FindFixtureByJavaName ("my/app/UnsignedParamActivity");
		var ctorSigs = peer.JavaConstructors.Select (c => c.JniSignature).ToList ();
		Assert.Contains ("(SIJ)V", ctorSigs);
	}

	// --- Regression: HasMatchingManagedCtor semantics ---
	// These guard the safety net introduced for Java.Lang.Thread+RunnableImplementor:
	// when a Java ctor (e.g. ()V seeded from a [Register]'d base) has no matching
	// user-visible managed ctor, HasMatchingManagedCtor must be false so the UCO
	// model builder can fail instead of emitting an invalid constructor wrapper.
	// If this flips silently to true, the generator emits a member ref to a
	// non-existent managed ctor — manifesting at runtime as
	// `MissingMethodException: Default constructor not found for type ...`.

	[Fact]
	public void HasMatchingManagedCtor_True_WhenExplicitParameterlessExists ()
	{
		// MainActivity defines `public MainActivity () { }` — the scanner must
		// flag the inherited ()V ctor as having a matching user-visible managed
		// ctor so codegen invokes the user ctor rather than falling back.
		var peer = FindFixtureByJavaName ("my/app/MainActivity");
		var voidCtor = Assert.Single (peer.JavaConstructors, c => c.JniSignature == "()V");
		Assert.True (voidCtor.HasMatchingManagedCtor,
			"MainActivity has an explicit public () ctor; the scanner must record HasMatchingManagedCtor = true.");
	}

	[Fact]
	public void HasMatchingManagedCtor_False_WhenOnlyActivationCtorExists ()
	{
		// UserActivity only declares the activation ctor (IntPtr, JniHandleOwnership).
		// There is NO user-visible managed `() : base()`. The Java side gets a ()V
		// ctor seeded from Activity. RunnableImplementor in the SDK has the same
		// shape (only parameterized managed ctors + a JCW-codegen-emitted ()V).
		// HasMatchingManagedCtor MUST be false here, or the generator will emit a
		// metadata reference to a non-existent ..ctor() and the runtime explodes
		// with MissingMethodException once Java tries to activate the peer.
		var peer = FindFixtureByJavaName ("my/app/UserActivity");
		var voidCtor = Assert.Single (peer.JavaConstructors, c => c.JniSignature == "()V");
		Assert.False (voidCtor.HasMatchingManagedCtor,
			"UserActivity has only an activation ctor; HasMatchingManagedCtor must be false so model building fails.");
	}

	[Fact]
	public void HasMatchingManagedCtor_False_WhenOnlyProtectedDefaultCtorExists ()
	{
		var peer = FindFixtureByJavaName ("my/app/ProtectedDefaultCtorActivity");
		var voidCtor = Assert.Single (peer.JavaConstructors, c => c.JniSignature == "()V");
		Assert.False (voidCtor.HasMatchingManagedCtor,
			"The scanner must only match public managed constructors for Java-visible constructor wrappers.");
	}

	[Fact]
	public void HasMatchingManagedCtor_False_WhenOnlyParameterizedManagedCtorExists ()
	{
		// ActivityWithCustomCtor has only an activation ctor + a (string) ctor.
		// The Java ()V ctor is seeded from Activity. There is no managed ()V.
		// HasMatchingManagedCtor MUST be false on the ()V Java ctor.
		// (The (Ljava/lang/String;)V Java ctor uses parameterless-fallback codegen,
		// which is a different code path documented by SuperArgumentsString = "".)
		var peer = FindFixtureByJavaName ("my/app/ActivityWithCustomCtor");
		var voidCtor = Assert.Single (peer.JavaConstructors, c => c.JniSignature == "()V");
		Assert.False (voidCtor.HasMatchingManagedCtor,
			"Only parameterized managed ctors exist; the inherited ()V seed must not claim a managed match.");
	}

}
