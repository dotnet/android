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
	}
}
