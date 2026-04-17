using System;
using Android.Runtime;
using Java.Interop;
using Microsoft.Android.Runtime;
using NUnit.Framework;

namespace Java.InteropTests
{
	[TestFixture]
	public class TrimmableTypeMapTypeManagerTests
	{
		// Test subclass that allows instantiation without full TrimmableTypeMap initialization.
		// GetStaticMethodFallbackTypesCore does not use TrimmableTypeMap.Instance, so the test
		// can run without an initialized TrimmableTypeMap singleton.
		sealed class TestableTrimmableTypeMapTypeManager : TrimmableTypeMapTypeManager
		{
		}

		[Test]
		public void GetStaticMethodFallbackTypes_WithPackageName_ReturnsDesugarFallbacks ()
		{
			using var manager = new TestableTrimmableTypeMapTypeManager ();
			var fallbacks = manager.GetStaticMethodFallbackTypes ("android/app/Activity");
			Assert.IsNotNull (fallbacks);
			Assert.AreEqual (2, fallbacks!.Count);
			Assert.AreEqual ("android/app/DesugarActivity$_CC", fallbacks [0]);
			Assert.AreEqual ("android/app/Activity$-CC", fallbacks [1]);
		}

		[Test]
		public void GetStaticMethodFallbackTypes_WithoutPackageName_ReturnsDesugarFallbacks ()
		{
			using var manager = new TestableTrimmableTypeMapTypeManager ();
			var fallbacks = manager.GetStaticMethodFallbackTypes ("Activity");
			Assert.IsNotNull (fallbacks);
			Assert.AreEqual (2, fallbacks!.Count);
			Assert.AreEqual ("DesugarActivity$_CC", fallbacks [0]);
			Assert.AreEqual ("Activity$-CC", fallbacks [1]);
		}

		[Test]
		public void GetStaticMethodFallbackTypes_WithDeepPackageName_ReturnsDesugarFallbacks ()
		{
			using var manager = new TestableTrimmableTypeMapTypeManager ();
			var fallbacks = manager.GetStaticMethodFallbackTypes ("com/example/package/MyInterface");
			Assert.IsNotNull (fallbacks);
			Assert.AreEqual (2, fallbacks!.Count);
			Assert.AreEqual ("com/example/package/DesugarMyInterface$_CC", fallbacks [0]);
			Assert.AreEqual ("com/example/package/MyInterface$-CC", fallbacks [1]);
		}

		// Verifies the generic-type-definition fallback in GetProxyForManagedType:
		// the generator emits one TypeMapAssociation per open generic peer, so a
		// closed instantiation like JavaList<string> must resolve through its GTD.
		[Test]
		public void TryGetJniNameForManagedType_ClosedGeneric_ResolvesViaGenericTypeDefinition ()
		{
			if (!RuntimeFeature.TrimmableTypeMap) {
				Assert.Ignore ("TrimmableTypeMap feature switch is off; test only relevant for the trimmable typemap path.");
			}

			var instance = TrimmableTypeMap.Instance;

			Assert.IsTrue (instance.TryGetJniNameForManagedType (typeof (JavaList<>), out var openJniName),
				"Open generic definition should resolve directly.");
			Assert.IsTrue (instance.TryGetJniNameForManagedType (typeof (JavaList<string>), out var closedStringJniName),
				"Closed instantiation should resolve via GTD fallback.");
			Assert.IsTrue (instance.TryGetJniNameForManagedType (typeof (JavaList<int>), out var closedIntJniName),
				"A second closed instantiation should also resolve via GTD fallback.");

			Assert.AreEqual (openJniName, closedStringJniName,
				"Closed instantiation must share the open GTD's JNI name (Java erases generics).");
			Assert.AreEqual (openJniName, closedIntJniName,
				"Different closed instantiations must map to the same JNI name.");
		}

		[Test]
		public void TryGetJniNameForManagedType_NonGenericType_ResolvesDirectly ()
		{
			if (!RuntimeFeature.TrimmableTypeMap) {
				Assert.Ignore ("TrimmableTypeMap feature switch is off; test only relevant for the trimmable typemap path.");
			}

			// Regression: the GTD fallback must not disturb the non-generic hot path.
			Assert.IsTrue (TrimmableTypeMap.Instance.TryGetJniNameForManagedType (typeof (JavaList), out var jniName));
			Assert.IsFalse (string.IsNullOrEmpty (jniName));
		}

		[Test]
		public void TryGetJniNameForManagedType_UnknownClosedGeneric_ReturnsFalse ()
		{
			if (!RuntimeFeature.TrimmableTypeMap) {
				Assert.Ignore ("TrimmableTypeMap feature switch is off; test only relevant for the trimmable typemap path.");
			}

			// System.Collections.Generic.List<T> has no TypeMapAssociation — both the
			// direct lookup AND the GTD fallback must miss, and the API must return false.
			Assert.IsFalse (TrimmableTypeMap.Instance.TryGetJniNameForManagedType (
				typeof (System.Collections.Generic.List<int>), out var jniName));
			Assert.IsNull (jniName);
		}

		[Test]
		public void TryGetJniNameForManagedType_RepeatedClosedGenericLookup_IsCached ()
		{
			if (!RuntimeFeature.TrimmableTypeMap) {
				Assert.Ignore ("TrimmableTypeMap feature switch is off; test only relevant for the trimmable typemap path.");
			}

			// The cache is keyed by the original closed type, so a second identical
			// lookup returns the same proxy instance without walking the GTD again.
			var instance = TrimmableTypeMap.Instance;

			Assert.IsTrue (instance.TryGetJniNameForManagedType (typeof (JavaList<Guid>), out var first));
			Assert.IsTrue (instance.TryGetJniNameForManagedType (typeof (JavaList<Guid>), out var second));
			Assert.AreEqual (first, second);
		}

		// Pure-function tests for the TargetTypeMatches helper used by
		// TryGetProxyFromHierarchy when the hierarchy lookup finds a proxy whose
		// stored TargetType is an open generic definition.

		class OpenT<T> { }
		class OpenT2<T1, T2> { }
		class ClosedOfIntOpenT : OpenT<int> { }
		class DeepClosedOfOpenT : ClosedOfIntOpenT { }

		[Test]
		public void TargetTypeMatches_DirectAssignable_ReturnsTrue ()
		{
			// Non-generic direct match: proxy target IS-A hint.
			Assert.IsTrue (TrimmableTypeMap.TargetTypeMatches (typeof (object), typeof (string)));
			Assert.IsTrue (TrimmableTypeMap.TargetTypeMatches (typeof (string), typeof (string)));
		}

		[Test]
		public void TargetTypeMatches_ClosedHint_OpenGenericProxy_SelfMatch_ReturnsTrue ()
		{
			// Hint is OpenT<int>; proxy's target is the open GTD OpenT<>.
			// IsAssignableFrom(OpenT<>) against OpenT<int> is false, so this exercises
			// the new GTD base-walk branch (self match on first iteration).
			Assert.IsTrue (TrimmableTypeMap.TargetTypeMatches (typeof (OpenT<int>), typeof (OpenT<>)));
			Assert.IsTrue (TrimmableTypeMap.TargetTypeMatches (typeof (OpenT<string>), typeof (OpenT<>)));
			Assert.IsTrue (TrimmableTypeMap.TargetTypeMatches (typeof (OpenT2<int, string>), typeof (OpenT2<,>)));
		}

		[Test]
		public void TargetTypeMatches_ClosedSubclassHint_OpenGenericProxy_ReturnsTrue ()
		{
			// Hint is a closed subclass of the open generic; the base-walk finds
			// the generic base type whose definition equals the proxy's open target.
			Assert.IsTrue (TrimmableTypeMap.TargetTypeMatches (typeof (ClosedOfIntOpenT), typeof (OpenT<>)));
			Assert.IsTrue (TrimmableTypeMap.TargetTypeMatches (typeof (DeepClosedOfOpenT), typeof (OpenT<>)));
		}

		[Test]
		public void TargetTypeMatches_MismatchedOpenGeneric_ReturnsFalse ()
		{
			// Different open generic definitions must NOT be treated as matching.
			Assert.IsFalse (TrimmableTypeMap.TargetTypeMatches (typeof (OpenT<int>), typeof (OpenT2<,>)));
			Assert.IsFalse (TrimmableTypeMap.TargetTypeMatches (typeof (string), typeof (OpenT<>)));
		}

		[Test]
		public void TargetTypeMatches_UnrelatedNonGeneric_ReturnsFalse ()
		{
			Assert.IsFalse (TrimmableTypeMap.TargetTypeMatches (typeof (string), typeof (int)));
		}
	}
}
