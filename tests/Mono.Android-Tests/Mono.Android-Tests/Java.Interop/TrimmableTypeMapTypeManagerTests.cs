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
		// Built-in type-signature and static-method fallback lookups do not use
		// TrimmableTypeMap.Instance, so those tests can run without an initialized singleton.
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

		[TestCase (typeof (byte?[][]), "[[Ljava/lang/Byte;")]
		[TestCase (typeof (byte?[][][]), "[[[Ljava/lang/Byte;")]
		[Category ("NativeAOTTrimmable")]
		public void GetTypeSignature_NullableByteJaggedArray_ReturnsJavaLangByte (Type type, string expected)
		{
			using var manager = new TestableTrimmableTypeMapTypeManager ();
			var signature = manager.GetTypeSignature (type);

			Assert.AreEqual (expected, signature.Name);
		}

		[Test]
		public void GetType_RepeatedJavaToManagedLookup_DoesNotAllocate ()
		{
			AssumeTrimmableTypeMapEnabled ();

			const string jniName = "android/view/View";
			const int iterationCount = 1_000;
			var typeMap = TrimmableTypeMap.Instance;
			int targetTypeCount = 0;
			foreach (var targetType in typeMap.GetTargetTypes (jniName)) {
				targetTypeCount++;
			}
			Assert.AreEqual (1, targetTypeCount, "The allocation test requires a non-alias typemap entry.");

			var signature = new JniTypeSignature (jniName);
			var manager = JniEnvironment.Runtime.TypeManager;
			Type result = typeof (void);
			for (int i = 0; i < iterationCount; i++) {
				result = manager.GetType (signature);
			}

			long before = GC.GetAllocatedBytesForCurrentThread ();
			for (int i = 0; i < iterationCount; i++) {
				result = manager.GetType (signature);
			}
			long allocatedBytes = GC.GetAllocatedBytesForCurrentThread () - before;

			Assert.AreEqual (typeof (Android.Views.View), result);
			Assert.AreEqual (0L, allocatedBytes, $"Expected {iterationCount} cached lookups to allocate no managed memory.");
		}

		[Test]
		public void TryGetTargetType_MissingEntry_ReturnsFalse ()
		{
			AssumeTrimmableTypeMapEnabled ();

			Assert.IsFalse (TrimmableTypeMap.Instance.TryGetTargetType ("net/dot/android/test/MissingType", out var targetType));
			Assert.IsNull (targetType);
		}

		[Test]
		public void JniProxyCache_SingleMappingStoresProxyDirectly ()
		{
			AssumeTrimmableTypeMapEnabled ();

			const string jniName = "android/view/View";
			var instance = TrimmableTypeMap.Instance;
			var cache = GetJniProxyCache (instance);
			cache.TryRemove (jniName, out _);

			Assert.IsTrue (instance.TryGetTargetType (jniName, out var targetType));
			Assert.AreEqual (typeof (Android.Views.View), targetType);
			Assert.IsTrue (cache.TryGetValue (jniName, out var cacheEntry));
			Assert.IsInstanceOf<JavaPeerProxy> (cacheEntry);
		}

		[Test]
		public void JniProxyCache_AliasMappingStoresProxyArray ()
		{
			AssumeTrimmableTypeMapEnabled ();

			const string jniName = "java/util/ArrayList";
			var instance = TrimmableTypeMap.Instance;
			var cache = GetJniProxyCache (instance);
			cache.TryRemove (jniName, out _);

			int targetTypeCount = 0;
			foreach (var targetType in instance.GetTargetTypes (jniName)) {
				targetTypeCount++;
			}
			Assert.Greater (targetTypeCount, 1);
			Assert.IsTrue (cache.TryGetValue (jniName, out var cacheEntry));
			Assert.IsInstanceOf<JavaPeerProxy[]> (cacheEntry);
		}

		[Test]
		public void JniProxyCache_MissingMappingStoresEmptyProxyArray ()
		{
			AssumeTrimmableTypeMapEnabled ();

			const string jniName = "net/dot/android/test/MissingProxyCacheEntry";
			var instance = TrimmableTypeMap.Instance;
			var cache = GetJniProxyCache (instance);
			cache.TryRemove (jniName, out _);

			Assert.IsFalse (instance.TryGetTargetType (jniName, out var targetType));
			Assert.IsNull (targetType);
			Assert.IsTrue (cache.TryGetValue (jniName, out var cacheEntry));
			if (cacheEntry is JavaPeerProxy[] proxies) {
				Assert.AreEqual (0, proxies.Length);
				return;
			}

			Assert.Fail ("A missing JNI mapping should be cached as an empty proxy array.");
		}

		[Test]
		public void JniProxyCache_UnexpectedEntryTypeThrows ()
		{
			AssumeTrimmableTypeMapEnabled ();

			const string jniName = "net/dot/android/test/InvalidProxyCacheEntry";
			var instance = TrimmableTypeMap.Instance;
			var cache = GetJniProxyCache (instance);
			cache [jniName] = new object ();
			try {
				var exception = Assert.Throws<InvalidOperationException> (() => instance.TryGetTargetType (jniName, out _));
				StringAssert.Contains ("Unexpected JNI proxy cache entry type", exception?.Message);
			} finally {
				cache.TryRemove (jniName, out _);
			}
		}

		[Test]
		public void RegisterNativeMethods_AliasGroupRegistersEveryCallableWrapper ()
		{
			using var jniType = new JniType ("java/lang/Object");
			var first = new RecordingCallableWrapperProxy ();
			var second = new RecordingCallableWrapperProxy ();

			TrimmableTypeMap.RegisterNativeMethods (new JavaPeerProxy [] { first, second }, jniType);

			Assert.AreEqual (1, first.RegistrationCount);
			Assert.AreEqual (1, second.RegistrationCount);
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
		public void GetProxyForJavaObject_SealedTarget_ReturnsProxy ()
		{
			AssumeTrimmableTypeMapEnabled ();

			using var value = new Java.Lang.String ("value");
			var proxy = TrimmableTypeMap.Instance.GetProxyForJavaObject (value.Handle, typeof (Java.Lang.String));

			if (proxy is null) {
				Assert.Fail ("Expected a proxy for Java.Lang.String.");
				return;
			}
			Assert.AreEqual (typeof (Java.Lang.String), proxy.TargetType);
		}

		[Test]
		public void GetProxyForJavaObject_IncompatibleSealedTarget_ReturnsNull ()
		{
			AssumeTrimmableTypeMapEnabled ();

			using var value = new Java.Lang.Integer (42);
			var proxy = TrimmableTypeMap.Instance.GetProxyForJavaObject (value.Handle, typeof (Java.Lang.String));

			Assert.IsNull (proxy);
		}

		[Test]
		public void CreateInstance_SealedClosedGenericTarget_ReturnsClosedPeer ()
		{
			AssumeTrimmableTypeMapEnabled ();

			using var value = new Java.Util.ArrayList ();
			var peer = TrimmableTypeMap.Instance.CreateInstance (value.Handle, typeof (JavaCollection<int>));
			if (peer is not JavaCollection<int> collection) {
				peer?.Dispose ();
				Assert.Fail ($"Expected {typeof (JavaCollection<int>)}, got {peer?.GetType ()}.");
				return;
			}

			collection.Dispose ();
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

		[Test]
		public void TrimmableJavaProxyObject_CreateLocalObjectReferenceArgumentUsesProxyType ()
		{
			AssumeTrimmableTypeMapEnabled ();

			var value = new object ();
			var reference = JniEnvironment.Runtime.ValueManager.CreateLocalObjectReferenceArgument (typeof (object), value);

			try {
				Assert.AreEqual ("net/dot/jni/internal/TrimmableJavaProxyObject", JNIEnv.GetClassNameFromInstance (reference.Handle));
			} finally {
				JniObjectReference.Dispose (ref reference);
			}
		}

		[Test]
		public void TrimmableJavaProxyObject_CanBeUsedInObjectArray ()
		{
			AssumeTrimmableTypeMapEnabled ();

			using var values = new JavaObjectArray<object> (1);
			values [0] = new object ();

			Assert.AreEqual ("[Ljava/lang/Object;", values.GetJniTypeName ());
		}

		[Test]
		public void TrimmableJavaProxyObject_ObjectMethodsUseJavaIdentitySemantics ()
		{
			AssumeTrimmableTypeMapEnabled ();

			var value = new object ();
			var other = new object ();
			var reference = JniEnvironment.Runtime.ValueManager.CreateLocalObjectReferenceArgument (typeof (object), value);
			var otherReference = JniEnvironment.Runtime.ValueManager.CreateLocalObjectReferenceArgument (typeof (object), other);

			try {
				var localProxy = reference.NewLocalRef ();
				var localOtherProxy = otherReference.NewLocalRef ();

				try {
					IntPtr proxyClass = JNIEnv.GetObjectClass (localProxy.Handle);
					try {
						IntPtr equals = JNIEnv.GetMethodID (proxyClass, "equals", "(Ljava/lang/Object;)Z");
						IntPtr hashCode = JNIEnv.GetMethodID (proxyClass, "hashCode", "()I");
						IntPtr toString = JNIEnv.GetMethodID (proxyClass, "toString", "()Ljava/lang/String;");
						var systemClass = JniEnvironment.Types.FindClass ("java/lang/System");

						try {
							IntPtr identityHashCode = JNIEnv.GetStaticMethodID (systemClass.Handle, "identityHashCode", "(Ljava/lang/Object;)I");

							Assert.IsTrue (JNIEnv.CallBooleanMethod (localProxy.Handle, equals, new JValue (localProxy.Handle)));
							Assert.IsFalse (JNIEnv.CallBooleanMethod (localProxy.Handle, equals, new JValue (localOtherProxy.Handle)));
							Assert.AreEqual (
								JNIEnv.CallStaticIntMethod (systemClass.Handle, identityHashCode, new JValue (localProxy.Handle)),
								JNIEnv.CallIntMethod (localProxy.Handle, hashCode));
							var proxyString = JNIEnv.GetString (JNIEnv.CallObjectMethod (localProxy.Handle, toString), JniHandleOwnership.TransferLocalRef);
							Assert.IsTrue (
								proxyString.StartsWith ("net.dot.jni.internal.TrimmableJavaProxyObject@", StringComparison.Ordinal),
								proxyString);
						} finally {
							JniObjectReference.Dispose (ref systemClass);
						}
					} finally {
						JNIEnv.DeleteLocalRef (proxyClass);
					}
				} finally {
					JniObjectReference.Dispose (ref localProxy);
					JniObjectReference.Dispose (ref localOtherProxy);
				}
			} finally {
				JniObjectReference.Dispose (ref otherReference);
				JniObjectReference.Dispose (ref reference);
			}
		}

		// Regression: the runtime replacement (BuildRuntimeArrayTypes) must return the full set of array
		// and wrapper types, not just T[]. The marshaling round-trip tests only need T[]. This is a pure
		// function, so it runs on every config.
		[Test]
		public void BuildRuntimeArrayTypes_ReferenceLeaf_ReturnsProxyContract ()
		{
			var rank1 = TrimmableTypeMapTypeManager.BuildRuntimeArrayTypes (typeof (Java.Lang.Object), rank: 1);
			CollectionAssert.AreEquivalent (
				new [] {
					typeof (JavaObjectArray<Java.Lang.Object>),
					typeof (Java.Interop.JavaArray<Java.Lang.Object>),
					typeof (Java.Lang.Object[]),
				},
				rank1);

			var rank2 = TrimmableTypeMapTypeManager.BuildRuntimeArrayTypes (typeof (Java.Lang.Object), rank: 2);
			CollectionAssert.AreEquivalent (
				new [] {
					typeof (JavaObjectArray<JavaObjectArray<Java.Lang.Object>>),
					typeof (JavaObjectArray<Java.Lang.Object>[]),
					typeof (JavaObjectArray<Java.Interop.JavaArray<Java.Lang.Object>>),
					typeof (Java.Interop.JavaArray<Java.Lang.Object>[]),
					typeof (JavaObjectArray<Java.Lang.Object[]>),
					typeof (Java.Lang.Object[][]),
				},
				rank2);
		}

		[Test]
		public void BuildRuntimeArrayTypes_PrimitiveLeaf_ReturnsProxyContract ()
		{
			var rank1 = TrimmableTypeMapTypeManager.BuildRuntimeArrayTypes (typeof (sbyte), rank: 1);
			CollectionAssert.AreEquivalent (
				new [] {
					typeof (sbyte[]),
					typeof (Java.Interop.JavaArray<sbyte>),
					typeof (JavaPrimitiveArray<sbyte>),
					typeof (JavaSByteArray),
				},
				rank1);

			var rank2 = TrimmableTypeMapTypeManager.BuildRuntimeArrayTypes (typeof (sbyte), rank: 2);
			CollectionAssert.AreEquivalent (
				new [] {
					typeof (JavaObjectArray<sbyte[]>),
					typeof (sbyte[][]),
					typeof (JavaObjectArray<Java.Interop.JavaArray<sbyte>>),
					typeof (Java.Interop.JavaArray<sbyte>[]),
					typeof (JavaObjectArray<JavaPrimitiveArray<sbyte>>),
					typeof (JavaPrimitiveArray<sbyte>[]),
					typeof (JavaObjectArray<JavaSByteArray>),
					typeof (JavaSByteArray[]),
				},
				rank2);
		}

		[Test]
		public void BuildRuntimeArrayTypes_NullablePrimitiveLeaf_ReturnsVector ()
		{
			// Nullable primitives have no array proxy and no AOT-safe Java.Interop array wrapper, so the
			// fallback returns just the exact rooted vector (int?[]) rather than an unrooted value generic.
			var rank1 = TrimmableTypeMapTypeManager.BuildRuntimeArrayTypes (typeof (int?), rank: 1);
			CollectionAssert.AreEquivalent (new [] { typeof (int?[]) }, rank1);

			var rank2 = TrimmableTypeMapTypeManager.BuildRuntimeArrayTypes (typeof (int?), rank: 2);
			CollectionAssert.AreEquivalent (new [] { typeof (int?[][]) }, rank2);
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

		static ConcurrentDictionary<string, object> GetJniProxyCache (TrimmableTypeMap instance)
		{
			var field = typeof (TrimmableTypeMap).GetField ("_jniProxyCache", BindingFlags.Instance | BindingFlags.NonPublic);
			Assert.IsNotNull (field);

			var value = field.GetValue (instance);
			Assert.IsNotNull (value);

			if (value is ConcurrentDictionary<string, object> cache) {
				return cache;
			}

			Assert.Fail ("Unable to access TrimmableTypeMap JNI proxy cache.");
			throw new InvalidOperationException ("Unable to access TrimmableTypeMap JNI proxy cache.");
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

	sealed class RecordingCallableWrapperProxy : JavaPeerProxy, IAndroidCallableWrapper
	{
		public RecordingCallableWrapperProxy ()
			: base ("java/lang/Object", typeof (Java.Lang.Object))
		{
		}

		public int RegistrationCount { get; private set; }

		public override IJavaPeerable? CreateInstance (IntPtr handle, JniHandleOwnership transfer) => null;

		public void RegisterNatives (JniType nativeClass)
		{
			RegistrationCount++;
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
