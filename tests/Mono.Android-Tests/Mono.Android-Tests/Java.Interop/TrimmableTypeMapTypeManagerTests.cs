using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
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

		[TestCase ("android/app/Activity", "android/app/DesugarActivity$_CC", "android/app/Activity$-CC")]
		[TestCase ("Activity", "DesugarActivity$_CC", "Activity$-CC")]
		[TestCase ("com/example/package/MyInterface", "com/example/package/DesugarMyInterface$_CC", "com/example/package/MyInterface$-CC")]
		public void GetStaticMethodFallbackTypes_ReturnsDesugarFallbacks (string jniSimpleReference, string expectedDesugar, string expectedFallback)
		{
			using var manager = new TestableTrimmableTypeMapTypeManager ();
			var fallbacks = GetStaticMethodFallbackTypes (manager, jniSimpleReference);

			Assert.AreEqual (2, fallbacks.Count);
			Assert.AreEqual (expectedDesugar, fallbacks [0]);
			Assert.AreEqual (expectedFallback, fallbacks [1]);
		}

		// Verifies the generic-type-definition fallback in GetProxyForManagedType:
		// the generator emits one TypeMapAssociation per open generic peer, so a
		// closed instantiation like JavaList<string> must resolve through its GTD.
		[Test]
		public void TryGetJniNameForManagedType_ClosedGeneric_ResolvesViaGenericTypeDefinition ()
		{
			AssumeTrimmableTypeMapEnabled ();

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
			AssumeTrimmableTypeMapEnabled ();

			// Regression: the GTD fallback must not disturb the non-generic hot path.
			Assert.IsTrue (TrimmableTypeMap.Instance.TryGetJniNameForManagedType (typeof (JavaList), out var jniName));
			Assert.IsFalse (string.IsNullOrEmpty (jniName));
		}

		[Test]
		public void TryGetJniNameForManagedType_UnknownClosedGeneric_ReturnsFalse ()
		{
			AssumeTrimmableTypeMapEnabled ();

			// System.Collections.Generic.List<T> has no TypeMapAssociation — both the
			// direct lookup AND the GTD fallback must miss, and the API must return false.
			Assert.IsFalse (TrimmableTypeMap.Instance.TryGetJniNameForManagedType (
				typeof (System.Collections.Generic.List<int>), out var jniName));
			Assert.IsNull (jniName);
		}

		[Test]
		public void TryGetJniNameForManagedType_RepeatedClosedGenericLookup_IsCached ()
		{
			AssumeTrimmableTypeMapEnabled ();

			// Closed generic peers normalize to their open generic definition, so
			// repeated lookups reuse the same cached proxy.
			var instance = TrimmableTypeMap.Instance;

			Assert.IsTrue (instance.TryGetJniNameForManagedType (typeof (JavaList<Guid>), out var first));
			Assert.IsTrue (instance.TryGetJniNameForManagedType (typeof (JavaList<Guid>), out var second));
			Assert.AreEqual (first, second);
		}

		[Test]
		public void TryGetJniNameForManagedType_DifferentClosedGenerics_UseGenericDefinitionCacheKey ()
		{
			AssumeTrimmableTypeMapEnabled ();

			var instance = TrimmableTypeMap.Instance;
			var cache = GetProxyCache (instance);

			cache.TryRemove (typeof (JavaList<>), out _);
			cache.TryRemove (typeof (JavaList<long>), out _);
			cache.TryRemove (typeof (JavaList<DateTimeOffset>), out _);

			Assert.IsTrue (instance.TryGetJniNameForManagedType (typeof (JavaList<long>), out _));
			Assert.IsTrue (instance.TryGetJniNameForManagedType (typeof (JavaList<DateTimeOffset>), out _));

			Assert.IsTrue (cache.ContainsKey (typeof (JavaList<>)));
			Assert.IsFalse (cache.ContainsKey (typeof (JavaList<long>)));
			Assert.IsFalse (cache.ContainsKey (typeof (JavaList<DateTimeOffset>)));
		}

		[Test]
		public void RegisteredPeer_Dispose_InvokesDisposing ()
		{
			AssumeTrimmableTypeMapEnabled ();

			bool disposed = false;
			bool finalized = false;
			var value = new TrimmableRegisteredDisposedObject {
				OnDisposed = () => disposed = true,
				OnFinalized = () => finalized = true,
			};

			value.Dispose ();

			Assert.IsTrue (disposed);
			Assert.IsFalse (finalized);
		}

		[Test]
		public async Task RegisteredPeer_Dispose_Finalized ()
		{
			AssumeTrimmableTypeMapEnabled ();

			var disposed = new TaskCompletionSource<bool> (TaskCreationOptions.RunContinuationsAsynchronously);
			var finalized = new TaskCompletionSource<bool> (TaskCreationOptions.RunContinuationsAsynchronously);

			PerformNoPinAction (() => {
				PerformNoPinAction (() => {
					var value = new TrimmableRegisteredDisposedObject {
						OnDisposed = () => disposed.TrySetResult (true),
						OnFinalized = () => finalized.TrySetResult (true),
					};
					GC.KeepAlive (value);
				});
				JniEnvironment.Runtime.ValueManager.CollectPeers ();
			});
			JniEnvironment.Runtime.ValueManager.CollectPeers ();

			await WaitForGC (() => disposed.Task.IsCompleted || finalized.Task.IsCompleted,
				"Expected TrimmableRegisteredDisposedObject.Dispose(disposing: false) to run.");

			Assert.IsFalse (disposed.Task.IsCompleted);
			Assert.IsTrue (finalized.Task.IsCompleted);
		}

		[Test]
		public void RegisteredPeer_NestedDisposeInvocations ()
		{
			AssumeTrimmableTypeMapEnabled ();

			var value = new TrimmableRegisteredNestedDisposableObject ();
			value.Dispose ();
			value.Dispose ();
		}

		[Test]
		public void RegisteredPeer_CanCreateGenericHolder ()
		{
			AssumeTrimmableTypeMapEnabled ();

			using var holder = new TrimmableRegisteredGenericHolder<int> ();
			holder.Value = 42;

			Assert.AreEqual (42, holder.Value);
		}

		static ConcurrentDictionary<Type, JavaPeerProxy> GetProxyCache (TrimmableTypeMap instance)
		{
			var field = typeof (TrimmableTypeMap).GetField ("_proxyCache", BindingFlags.Instance | BindingFlags.NonPublic);
			Assert.IsNotNull (field);

			var value = field.GetValue (instance);
			Assert.IsNotNull (value);

			if (value is ConcurrentDictionary<Type, JavaPeerProxy> cache) {
				return cache;
			}

			Assert.Fail ("Unable to access TrimmableTypeMap proxy cache.");
			throw new InvalidOperationException ("Unable to access TrimmableTypeMap proxy cache.");
		}

		static IReadOnlyList<string> GetStaticMethodFallbackTypes (TestableTrimmableTypeMapTypeManager manager, string jniSimpleReference)
		{
			var fallbacks = manager.GetStaticMethodFallbackTypes (jniSimpleReference);
			Assert.IsNotNull (fallbacks);
			return fallbacks ?? throw new InvalidOperationException ("Expected fallback types.");
		}

		static void AssumeTrimmableTypeMapEnabled ()
		{
			if (!RuntimeFeature.TrimmableTypeMap) {
				Assert.Ignore ("TrimmableTypeMap feature switch is off; test only relevant for the trimmable typemap path.");
			}
		}

		static async Task WaitForGC (Func<bool> predicate, string message, int timeoutMilliseconds = 2000)
		{
			var timeout = TimeSpan.FromMilliseconds (timeoutMilliseconds);
			var start = DateTime.UtcNow;
			while (!predicate () && DateTime.UtcNow - start < timeout) {
				GC.Collect (generation: 2, mode: GCCollectionMode.Forced, blocking: true);
				GC.WaitForPendingFinalizers ();
				JniEnvironment.Runtime.ValueManager.CollectPeers ();
				await Task.Yield ();
			}
			Assert.IsTrue (predicate (), message);
		}

		static IntPtr noPinActionPointer;

		static unsafe void NoPinActionHelper (int depth, Action action)
		{
			int* values = stackalloc int [20];
			noPinActionPointer = new IntPtr (values);

			if (depth <= 0) {
				new object ();
				action ();
			} else {
				NoPinActionHelper (depth - 1, action);
			}
		}

		static void PerformNoPinAction (Action action)
		{
			var thread = new Thread (() => NoPinActionHelper (128, action));
			thread.Start ();
			thread.Join ();
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

	[Register ("net/dot/android/test/TrimmableRegisteredDisposedObject")]
	class TrimmableRegisteredDisposedObject : Java.Lang.Object
	{
		public Action OnDisposed = delegate { };
		public Action OnFinalized = delegate { };

		public TrimmableRegisteredDisposedObject ()
		{
		}

		protected override void Dispose (bool disposing)
		{
			if (disposing) {
				OnDisposed ();
			} else {
				OnFinalized ();
			}
			base.Dispose (disposing);
		}
	}

	[Register ("net/dot/android/test/TrimmableRegisteredNestedDisposableObject")]
	class TrimmableRegisteredNestedDisposableObject : Java.Lang.Object
	{
		bool isDisposed;

		public TrimmableRegisteredNestedDisposableObject ()
		{
		}

		protected override void Dispose (bool disposing)
		{
			if (isDisposed) {
				return;
			}
			isDisposed = true;
			if (Handle != IntPtr.Zero) {
				Dispose ();
			}
			base.Dispose (disposing);
		}
	}

	[Register ("net/dot/android/test/TrimmableRegisteredGenericHolder")]
	class TrimmableRegisteredGenericHolder<T> : Java.Lang.Object
	{
		public T Value { get; set; }
	}
}
