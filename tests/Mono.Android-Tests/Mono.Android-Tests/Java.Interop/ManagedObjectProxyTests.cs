using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

using Android.Runtime;
using Java.Interop;

using NUnit.Framework;

namespace Java.InteropTests
{
	[TestFixture]
	[Category ("ManagedObjectProxy")]
	public class ManagedObjectProxyTests
	{
		static IntPtr noPinActionPointer;

		// Managed reference identity is the common round-trip contract. Java-visible equality,
		// hashing, and string conversion are asserted separately because they vary by typemap.
		[Test]
		public void JavaObjectArray_RoundTripPreservesManagedReferences ()
		{
			var value = new ManagedValue (42);
			var equalValue = new ManagedValue (42);
			using var values = new JavaObjectArray<object> (4);

			values [0] = value;
			values [1] = value;
			values [2] = equalValue;
			values [3] = null;

			Assert.AreSame (value, values [0], "The original managed instance should be returned.");
			Assert.AreSame (value, values [1], "Duplicate array entries should return the same managed instance.");
			Assert.AreSame (equalValue, values [2], "A distinct but managed-equal instance should retain its identity.");
			Assert.AreNotSame (values [0], values [2], "Managed equality must not collapse distinct round-trip values.");
			Assert.IsNull (values [3], "A null array entry should round-trip as null.");
			Assert.AreEqual (typeof (ManagedValue), values [0].GetType ());
			Assert.AreEqual (42, ((ManagedValue) values [0]).Value);
		}

		[Test]
		public void JavaObjectArray_RepeatedLookupUsesSameJavaAndManagedIdentity ()
		{
			var value = new ManagedValue (42);
			using var values = new JavaObjectArray<object> (1);
			values [0] = value;

			JniObjectReference firstReference = default;
			JniObjectReference secondReference = default;
			try {
				firstReference = JniEnvironment.Arrays.GetObjectArrayElement (values.PeerReference, 0);
				secondReference = JniEnvironment.Arrays.GetObjectArrayElement (values.PeerReference, 0);

				Assert.IsTrue (JniEnvironment.Types.IsSameObject (firstReference, secondReference),
					"Repeated Java array lookups should refer to the same Java proxy.");

				var first = JniEnvironment.Runtime.ValueManager.GetValue<object> (
					ref firstReference, JniObjectReferenceOptions.CopyAndDispose);
				var second = JniEnvironment.Runtime.ValueManager.GetValue<object> (
					ref secondReference, JniObjectReferenceOptions.CopyAndDispose);

				Assert.AreSame (value, first);
				Assert.AreSame (value, second);
			} finally {
				JniObjectReference.Dispose (ref secondReference);
				JniObjectReference.Dispose (ref firstReference);
			}
		}

		[Test]
		public void JavaLangObjectView_RoundTripUsesManagedProxyAssociation ()
		{
			var value = new ManagedValue (42);
			var reference = JniEnvironment.Runtime.ValueManager.CreateLocalObjectReferenceArgument (typeof (object), value);
			try {
				var viewReference = reference.NewLocalRef ();
				try {
					using var view = new Java.Lang.Object (
						viewReference.Handle,
						JniHandleOwnership.DoNotTransfer | JniHandleOwnership.DoNotRegister);

					Assert.IsTrue (JniEnvironment.Types.IsSameObject (reference, view.PeerReference));

					var roundTripReference = view.PeerReference.NewLocalRef ();
					try {
						var roundTrip = JniEnvironment.Runtime.ValueManager.GetValue<object> (
							ref roundTripReference, JniObjectReferenceOptions.CopyAndDispose);

						Assert.AreSame (value, roundTrip);
					} finally {
						JniObjectReference.Dispose (ref roundTripReference);
					}
				} finally {
					JniObjectReference.Dispose (ref viewReference);
				}
			} finally {
				JniObjectReference.Dispose (ref reference);
			}
		}

		[Test]
		public void JavaList_RoundTripPreservesDuplicateManagedReferencesAndNull ()
		{
			var value = new ManagedValue (42);
			var equalValue = new ManagedValue (42);
			using var values = new JavaList<object> ();

			values.Add (value);
			values.Add (value);
			values.Add (equalValue);
			values.Add (null);

			Assert.AreSame (value, values [0]);
			Assert.AreSame (value, values [1]);
			Assert.AreSame (equalValue, values [2]);
			Assert.AreNotSame (values [0], values [2]);
			Assert.IsNull (values [3]);

			Assert.AreEqual (0, values.IndexOf (value),
				"Java collections should match repeated wrappers for the same managed reference.");
			Assert.AreEqual (2, values.IndexOf (equalValue),
				"Java collection equality should not collapse distinct managed-equal references.");
			Assert.AreEqual (3, values.IndexOf (null));
		}

		[Test]
		public void JavaDictionary_RoundTripPreservesKeysDuplicateValuesAndNull ()
		{
			var firstKey = new ManagedValue (1);
			var secondKey = new ManagedValue (2);
			var value = new ManagedValue (42);
			using var values = new JavaDictionary<object, object> ();

			values.Add (firstKey, value);
			values.Add (secondKey, value);
			values.Add (null, null);

			Assert.AreEqual (3, values.Count);
			Assert.AreSame (value, values [firstKey]);
			Assert.AreSame (value, values [secondKey]);
			Assert.IsNull (values [null]);
		}

		[Test]
		public void JavaDictionary_DistinctManagedEqualKeysRemainDistinct ()
		{
			var firstKey = new ManagedValue (42);
			var equalKey = new ManagedValue (42);
			var firstValue = new ManagedValue (1);
			var secondValue = new ManagedValue (2);
			using var values = new JavaDictionary<object, object> ();

			values.Add (firstKey, firstValue);
			values.Add (equalKey, secondValue);

			Assert.AreEqual (2, values.Count,
				"Java collection wrappers compare the identity of the wrapped managed references.");
			Assert.AreSame (firstValue, values [firstKey]);
			Assert.AreSame (secondValue, values [equalKey]);
		}

		[Test]
		public void NestedJavaCollections_RoundTripPreservesManagedReference ()
		{
			var value = new ManagedValue (42);
			using var inner = new JavaList<object> ();
			using var outer = new JavaList<object> ();
			inner.Add (value);
			outer.Add (inner);

			var roundTripInner = outer [0];

			Assert.AreSame (inner, roundTripInner);
			Assert.AreSame (value, ((JavaList<object>) roundTripInner) [0]);
		}

		// Managed round-trip identity is common to both typemap implementations, but the Java-visible
		// object methods intentionally differ. See https://github.com/dotnet/android/issues/11703.
		[Test]
		public void ProxyObjectMethodsFollowConfiguredJavaSemantics ()
		{
			var value = new ManagedValue (42);
			var equalValue = new ManagedValue (42);
			JniObjectReference reference = default;
			JniObjectReference equalReference = default;

			try {
				reference = JniEnvironment.Runtime.ValueManager.CreateLocalObjectReferenceArgument (typeof (object), value);
				equalReference = JniEnvironment.Runtime.ValueManager.CreateLocalObjectReferenceArgument (typeof (object), equalValue);

				IntPtr proxyClass = JNIEnv.GetObjectClass (reference.Handle);
				try {
					IntPtr equals = JNIEnv.GetMethodID (proxyClass, "equals", "(Ljava/lang/Object;)Z");
					IntPtr hashCode = JNIEnv.GetMethodID (proxyClass, "hashCode", "()I");
					IntPtr toString = JNIEnv.GetMethodID (proxyClass, "toString", "()Ljava/lang/String;");

					Assert.IsFalse (JNIEnv.IsSameObject (reference.Handle, equalReference.Handle),
						"Distinct managed instances should use distinct Java proxies.");
					Assert.IsTrue (JNIEnv.CallBooleanMethod (reference.Handle, equals, new JValue (reference.Handle)));

					int actualHashCode = JNIEnv.CallIntMethod (reference.Handle, hashCode);
					var actualStringReference = new JniObjectReference (
						JNIEnv.CallObjectMethod (reference.Handle, toString),
						JniObjectReferenceType.Local);
					string actualString;
					try {
						actualString = JniEnvironment.Strings.ToString (
							ref actualStringReference, JniObjectReferenceOptions.CopyAndDispose);
					} finally {
						JniObjectReference.Dispose (ref actualStringReference);
					}

					if (Microsoft.Android.Runtime.RuntimeFeature.TrimmableTypeMap) {
						Assert.IsFalse (JNIEnv.CallBooleanMethod (reference.Handle, equals, new JValue (equalReference.Handle)),
							"Trimmable proxies intentionally use Java reference identity instead of managed Equals.");
						Assert.AreEqual (GetJavaIdentityHashCode (reference), actualHashCode,
							"Trimmable proxies intentionally use Java identity hash codes.");
						string runtimeClassName = JNIEnv.GetClassNameFromInstance (reference.Handle).Replace ('/', '.');
						string expectedString = $"{runtimeClassName}@{unchecked ((uint) actualHashCode).ToString ("x", CultureInfo.InvariantCulture)}";
						Assert.AreEqual (expectedString, actualString,
							"Trimmable proxies should use the exact default java.lang.Object.toString format.");
					} else {
						Assert.IsTrue (JNIEnv.CallBooleanMethod (reference.Handle, equals, new JValue (equalReference.Handle)),
							"The llvm-ir proxy forwards Java equals to the managed override.");
						Assert.AreEqual (value.GetHashCode (), actualHashCode);
						Assert.AreEqual (value.ToString (), actualString);
					}
				} finally {
					JNIEnv.DeleteLocalRef (proxyClass);
				}
			} finally {
				JniObjectReference.Dispose (ref equalReference);
				JniObjectReference.Dispose (ref reference);
			}
		}

		[Test]
		public async Task JavaObjectArrayRetainsManagedValueUntilReleased ()
		{
			WeakReference<ManagedValue> weakValue;
			var values = CreateJavaRootedValue (out weakValue);

			try {
				await WaitForGC (() => IsAlive (weakValue),
					"A Java array reference should retain its managed value.");
				AssertJavaRootRetainsValue (values, weakValue);
			} finally {
				try {
					values.Clear ();
				} finally {
					values.Dispose ();
				}
				values = null;
			}

			await WaitForGC (() => !IsAlive (weakValue),
				"The managed value should be collectible after its Java collection reference is cleared and disposed.");
		}

		static int GetJavaIdentityHashCode (JniObjectReference reference)
		{
			var systemClass = JniEnvironment.Types.FindClass ("java/lang/System");
			try {
				IntPtr identityHashCode = JNIEnv.GetStaticMethodID (
					systemClass.Handle, "identityHashCode", "(Ljava/lang/Object;)I");
				return JNIEnv.CallStaticIntMethod (systemClass.Handle, identityHashCode, new JValue (reference.Handle));
			} finally {
				JniObjectReference.Dispose (ref systemClass);
			}
		}

		[MethodImpl (MethodImplOptions.NoInlining)]
		static JavaObjectArray<object> CreateJavaRootedValue (out WeakReference<ManagedValue> weakValue)
		{
			var values = new JavaObjectArray<object> (1);
			bool initialized = false;
			try {
				WeakReference<ManagedValue> createdWeakValue = null;
				// A short-lived stack avoids false pinning when Mono conservatively scans the test thread.
				PerformNoPinAction (() => {
					var value = new ManagedValue (42);
					values [0] = value;
					createdWeakValue = new WeakReference<ManagedValue> (value);
				});
				weakValue = createdWeakValue;
				initialized = true;
				return values;
			} finally {
				if (!initialized) {
					values.Dispose ();
				}
			}
		}

		[MethodImpl (MethodImplOptions.NoInlining)]
		static void AssertJavaRootRetainsValue (
			JavaObjectArray<object> values,
			WeakReference<ManagedValue> weakValue)
		{
			PerformNoPinAction (() => {
				Assert.IsTrue (weakValue.TryGetTarget (out var value),
					"A Java collection reference should retain its managed value.");
				Assert.AreSame (value, values [0]);
				GC.KeepAlive (value);
			});
		}

		static async Task WaitForGC (Func<bool> predicate, string message, int timeoutMilliseconds = 5000)
		{
			bool requireBridgeGeneration = !Microsoft.Android.Runtime.RuntimeFeature.IsMonoRuntime;
			int initialBridgeGeneration = JNIEnv.BridgeProcessingGeneration;
			var timeout = TimeSpan.FromMilliseconds (timeoutMilliseconds);
			var stopwatch = Stopwatch.StartNew ();
			do {
				GC.Collect (generation: 2, mode: GCCollectionMode.Forced, blocking: true);
				GC.WaitForPendingFinalizers ();
				JNIEnv.WaitForBridgeProcessing ();
				JniEnvironment.Runtime.ValueManager.CollectPeers ();
				JNIEnv.WaitForBridgeProcessing ();
				await Task.Yield ();
			} while ((!predicate () ||
					(requireBridgeGeneration && JNIEnv.BridgeProcessingGeneration == initialBridgeGeneration)) &&
				stopwatch.Elapsed < timeout);

			int finalBridgeGeneration = JNIEnv.BridgeProcessingGeneration;
			if (requireBridgeGeneration) {
				Assert.Greater (finalBridgeGeneration, initialBridgeGeneration,
					$"A JNI bridge-processing cycle did not complete within {timeoutMilliseconds}ms. " +
					$"Initial generation: {initialBridgeGeneration}; final generation: {finalBridgeGeneration}.");
			}
			Assert.IsTrue (predicate (), message);
		}

		[MethodImpl (MethodImplOptions.NoInlining)]
		static bool IsAlive (WeakReference<ManagedValue> weakValue)
		{
			bool isAlive = false;
			PerformNoPinAction (() => isAlive = weakValue.TryGetTarget (out _));
			return isAlive;
		}

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
			Exception exception = null;
			var thread = new Thread (() => {
				try {
					NoPinActionHelper (128, action);
				} catch (Exception e) {
					exception = e;
				}
			});
			thread.Start ();
			thread.Join ();

			if (exception != null) {
				ExceptionDispatchInfo.Capture (exception).Throw ();
			}
		}

		sealed class ManagedValue
		{
			public ManagedValue (int value)
			{
				Value = value;
			}

			public int Value { get; }

			public override bool Equals (object obj)
			{
				return obj is ManagedValue other && Value == other.Value;
			}

			public override int GetHashCode ()
			{
				return Value;
			}

			public override string ToString ()
			{
				return $"ManagedValue({Value})";
			}
		}
	}
}
