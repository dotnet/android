using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Threading;

using Android.Runtime;

using Java.Interop;
using Microsoft.Android.Runtime;

using NUnit.Framework;

namespace Java.InteropTests
{
	[TestFixture]
	[NonParallelizable]
	[Category ("TransferredReferences")]
	public class TransferredReferenceTests
	{
		[TestCase (JniHandleOwnership.DoNotTransfer)]
		[TestCase (JniHandleOwnership.TransferLocalRef)]
		[TestCase (JniHandleOwnership.TransferGlobalRef)]
		public void ObjectConstructionConsumesOnlyTransferredInput (JniHandleOwnership ownership)
		{
			WithInput ("java/lang/Object", ownership, (handle, transfer) => {
				using var peer = new Java.Lang.Object (handle, transfer);
				Assert.IsTrue (peer.PeerReference.IsValid);
				if (transfer == JniHandleOwnership.DoNotTransfer) {
					Assert.IsTrue (JNIEnv.IsSameObject (handle, peer.Handle));
				}
			});
		}

		[TestCase (JniHandleOwnership.DoNotTransfer)]
		[TestCase (JniHandleOwnership.TransferLocalRef)]
		[TestCase (JniHandleOwnership.TransferGlobalRef)]
		public void ObjectConstructionFailureReleasesInput (JniHandleOwnership ownership)
		{
			WithInput ("java/lang/Object", ownership, (handle, transfer) => {
				try {
					Assert.Throws<InvalidOperationException> (() => new ConstructionFailurePeer (handle, transfer));
				} finally {
					ConstructionFailurePeer.PartialPeer?.Dispose ();
					ConstructionFailurePeer.PartialPeer = null;
				}
			});
		}

		[TestCase (JniHandleOwnership.DoNotTransfer)]
		[TestCase (JniHandleOwnership.TransferLocalRef)]
		[TestCase (JniHandleOwnership.TransferGlobalRef)]
		public void GetObjectExistingPeerConsumesInput (JniHandleOwnership ownership)
		{
			using var existing = new Java.Lang.String ("existing");
			WithInput (existing.Handle, ownership, (handle, transfer) => {
				Assert.AreSame (existing, Java.Lang.Object.GetObject<Java.Lang.String> (handle, transfer));
			});
		}

		[TestCase (false, JniHandleOwnership.DoNotTransfer)]
		[TestCase (false, JniHandleOwnership.TransferLocalRef)]
		[TestCase (false, JniHandleOwnership.TransferGlobalRef)]
		[TestCase (true, JniHandleOwnership.DoNotTransfer)]
		[TestCase (true, JniHandleOwnership.TransferLocalRef)]
		[TestCase (true, JniHandleOwnership.TransferGlobalRef)]
		public void ThrowingJavaInteropActivationReleasesInput (bool directProxy, JniHandleOwnership ownership)
		{
			WithInput ("net/dot/android/test/TrimmableRuntimeJavaInteropPeer", ownership, (handle, transfer) => {
				TrimmableRuntimeJavaInteropPeer.Reset ();
				TrimmableRuntimeJavaInteropPeer partial = null;
				TrimmableRuntimeJavaInteropPeer.PeerCreated = peer => {
					partial = peer;
					throw new InvalidOperationException ("Transferred input activation failed.");
				};
				try {
					Assert.Throws<InvalidOperationException> (() => {
						if (directProxy) {
							var proxy = TrimmableTypeMap.Instance.GetProxyForJavaObject (handle, typeof (TrimmableRuntimeJavaInteropPeer));
							Assert.IsNotNull (proxy);
							proxy.CreateInstance (handle, transfer);
						} else {
							Java.Lang.Object.GetObject<TrimmableRuntimeJavaInteropPeer> (handle, transfer);
						}
					});
					Assert.IsNotNull (partial);
					Assert.IsTrue (partial.PeerReference.IsValid, "The wrapper's copied reference is separate from the input.");
				} finally {
					partial?.Dispose ();
					TrimmableRuntimeJavaInteropPeer.Reset ();
				}
			});
		}

		[TestCase (JniHandleOwnership.DoNotTransfer)]
		[TestCase (JniHandleOwnership.TransferLocalRef)]
		[TestCase (JniHandleOwnership.TransferGlobalRef)]
		public void LookupFailureReleasesInput (JniHandleOwnership ownership)
		{
			WithInput ("java/lang/Object", ownership, (handle, transfer) => {
				Assert.Throws<ArgumentException> (() => Java.Lang.Object.GetObject<UnmappedPeer> (handle, transfer));
			});
		}

		[TestCase (false, JniHandleOwnership.DoNotTransfer)]
		[TestCase (false, JniHandleOwnership.TransferLocalRef)]
		[TestCase (false, JniHandleOwnership.TransferGlobalRef)]
		[TestCase (true, JniHandleOwnership.DoNotTransfer)]
		[TestCase (true, JniHandleOwnership.TransferLocalRef)]
		[TestCase (true, JniHandleOwnership.TransferGlobalRef)]
		public void ThrowingJavaInteropInvokerReleasesInput (bool inherited, JniHandleOwnership ownership)
		{
			WithInput (inherited ? "java/util/ArrayList" : "java/util/HashSet", ownership, (handle, transfer) => {
				IJavaPeerable partial = null;
				void OnCreated (IJavaPeerable peer)
				{
					partial = peer;
					throw new InvalidOperationException ("Transferred invoker activation failed.");
				}
				if (inherited) {
					InheritedJavaInteropListInvoker.PeerCreated = OnCreated;
				} else {
					JavaInteropCollectionInvoker.PeerCreated = OnCreated;
				}
				try {
					var type = inherited ? typeof (IInheritedJavaInteropList) : typeof (IJavaInteropCollection);
					var proxy = TrimmableTypeMap.Instance.GetProxyForJavaObject (handle, type);
					Assert.IsNotNull (proxy);
					Assert.Throws<InvalidOperationException> (() => proxy.CreateInstance (handle, transfer));
					Assert.IsNotNull (partial);
					Assert.IsTrue (partial.PeerReference.IsValid);
				} finally {
					partial?.Dispose ();
					InheritedJavaInteropListInvoker.PeerCreated = null;
					JavaInteropCollectionInvoker.PeerCreated = null;
				}
			});
		}

		[TestCase (JniHandleOwnership.DoNotTransfer)]
		[TestCase (JniHandleOwnership.TransferLocalRef)]
		[TestCase (JniHandleOwnership.TransferGlobalRef)]
		public void IncompatibleLookupConsumesInput (JniHandleOwnership ownership)
		{
			if (!RuntimeFeature.IsAssignableFromCheck) {
				Assert.Ignore ("This configuration intentionally disables incompatible-cast checking.");
			}
			WithInput ("java/lang/Object", ownership, (handle, transfer) => {
				Assert.IsNull (Java.Lang.Object.GetObject<Java.Lang.String> (handle, transfer));
			});
		}

		[TestCase (JniHandleOwnership.DoNotTransfer)]
		[TestCase (JniHandleOwnership.TransferLocalRef)]
		[TestCase (JniHandleOwnership.TransferGlobalRef)]
		public void JavaInteropProxySuccessCopiesInput (JniHandleOwnership ownership)
		{
			WithInput ("net/dot/android/test/TrimmableRuntimeJavaInteropPeer", ownership, (handle, transfer) => {
				TrimmableRuntimeJavaInteropPeer.Reset ();
				var proxy = TrimmableTypeMap.Instance.GetProxyForJavaObject (handle, typeof (TrimmableRuntimeJavaInteropPeer));
				Assert.IsNotNull (proxy);
				using var peer = proxy.CreateInstance (handle, transfer | JniHandleOwnership.DoNotRegister);
				Assert.IsNotNull (peer);
				Assert.IsTrue (peer.PeerReference.IsValid);
				Assert.AreEqual (JniObjectReferenceOptions.CopyAndDoNotRegister, TrimmableRuntimeJavaInteropPeer.Options);
				Assert.IsNull (JniEnvironment.Runtime.ValueManager.PeekPeer (peer.PeerReference));
			});
		}

		[TestCase (1, JniHandleOwnership.TransferGlobalRef)]
		[TestCase (2, JniHandleOwnership.TransferGlobalRef)]
		[TestCase (3, JniHandleOwnership.TransferGlobalRef)]
		[TestCase (4, JniHandleOwnership.TransferGlobalRef)]
		[TestCase (1, JniHandleOwnership.TransferLocalRef)]
		[TestCase (2, JniHandleOwnership.TransferLocalRef)]
		[TestCase (3, JniHandleOwnership.TransferLocalRef)]
		[TestCase (4, JniHandleOwnership.TransferLocalRef)]
		[TestCase (1, JniHandleOwnership.DoNotTransfer)]
		[TestCase (2, JniHandleOwnership.DoNotTransfer)]
		[TestCase (3, JniHandleOwnership.DoNotTransfer)]
		[TestCase (4, JniHandleOwnership.DoNotTransfer)]
		public void ThrowableExtractionFailureReleasesInput (int stage, JniHandleOwnership ownership)
		{
			var source = JNIEnv.CreateInstance ("net/dot/android/test/TransferFailureThrowable", "(I)V", new JValue (stage));
			try {
				WithInput (source, ownership, (handle, transfer) => {
					var sourceClass = JNIEnv.GetObjectClass (source);
					try {
						var reset = JNIEnv.GetMethodID (sourceClass, "reset", "()V");
						JNIEnv.CallVoidMethod (source, reset);
					} finally {
						JNIEnv.DeleteLocalRef (sourceClass);
					}
					Java.Lang.IllegalStateException failure = null;
					try {
						failure = Assert.Throws<Java.Lang.IllegalStateException> (() => new Java.Lang.Throwable (handle, transfer));
						StringAssert.Contains ("transfer-extraction-" + stage, failure.Message);
					} finally {
						failure?.Dispose ();
						// Stage 4 throws after SetHandle has registered the wrapper. Dispose that
						// copied GREF independently so this assertion measures only the input.
						JniEnvironment.Runtime.ValueManager.PeekPeer (new JniObjectReference (source))?.Dispose ();
					}
				});
			} finally {
				JNIEnv.DeleteLocalRef (source);
			}
		}

		[TestCase (JniHandleOwnership.DoNotTransfer)]
		[TestCase (JniHandleOwnership.TransferLocalRef)]
		[TestCase (JniHandleOwnership.TransferGlobalRef)]
		public void ThrowableSuccessDoesNotDeleteInputPrematurely (JniHandleOwnership ownership)
		{
			WithInput ("java/lang/Throwable", ownership, (handle, transfer) => {
				using var peer = new Java.Lang.Throwable (handle, transfer);
				Assert.IsTrue (peer.PeerReference.IsValid);
				StringAssert.Contains ("java.lang.Throwable", peer.StackTrace);
			});
		}

		[Test]
		public void NullHandleReturnsNull ()
		{
			foreach (var ownership in new [] { JniHandleOwnership.DoNotTransfer, JniHandleOwnership.TransferLocalRef, JniHandleOwnership.TransferGlobalRef }) {
				Assert.IsNull (Java.Lang.Object.GetObject<Java.Lang.Object> (IntPtr.Zero, ownership));
			}
		}

		static void WithInput (string className, JniHandleOwnership ownership, Action<IntPtr, JniHandleOwnership> exercise)
		{
			var source = JNIEnv.CreateInstance (className, "()V");
			try {
				WithInput (source, ownership, exercise);
			} finally {
				JNIEnv.DeleteLocalRef (source);
			}
		}

		static void WithInput (IntPtr source, JniHandleOwnership ownership, Action<IntPtr, JniHandleOwnership> exercise)
		{
			// Warm class, method, proxy and exception caches without transferring the source.
			exercise (source, JniHandleOwnership.DoNotTransfer);
			var locals = Java.Interop.Runtime.LocalReferenceCount;
			var input = ownership == JniHandleOwnership.TransferGlobalRef
				? JNIEnv.NewGlobalRef (source)
				: JNIEnv.NewLocalRef (source);
			var type = ownership == JniHandleOwnership.TransferGlobalRef ? JniObjectReferenceType.Global : JniObjectReferenceType.Local;
			var runtime = JniEnvironment.Runtime;
			var originalManager = runtime.ObjectReferenceManager;
			var observer = new ReferenceObserver (originalManager, source, input, type);
			observer.OnSetRuntime (runtime);
			try {
				SetReferenceManager (runtime, observer);
				exercise (input, ownership);
				Assert.AreEqual (ownership == JniHandleOwnership.DoNotTransfer ? 0 : 1, observer.InputDeletions,
					"Only the original transferred input must be released, exactly once.");
				Assert.IsEmpty (observer.OwnedCopies, "The partially constructed wrapper's own copies must be disposed separately.");
				foreach (var reference in observer.OutstandingGlobals) {
					TestContext.WriteLine ($"Other outstanding GREF {reference.Key:x}: {reference.Value}");
				}
				Assert.AreEqual (locals + (ownership == JniHandleOwnership.DoNotTransfer ? 1 : 0),
					Java.Interop.Runtime.LocalReferenceCount, "Only transferred local references may be deleted.");
				if (ownership == JniHandleOwnership.DoNotTransfer) {
					Assert.IsTrue (JNIEnv.IsSameObject (source, input), "Borrowed input must remain valid.");
				}
			} finally {
				SetReferenceManager (runtime, originalManager);
				if (observer.InputDeletions == 0) {
					JNIEnv.DeleteRef (input, type == JniObjectReferenceType.Global
						? JniHandleOwnership.TransferGlobalRef : JniHandleOwnership.TransferLocalRef);
				}
			}
		}

		[DynamicDependency ("set_ObjectReferenceManager", typeof (JniRuntime))]
		static void SetReferenceManager (JniRuntime runtime, JniRuntime.JniObjectReferenceManager manager)
		{
			var setter = typeof (JniRuntime).GetProperty (nameof (JniRuntime.ObjectReferenceManager))?.GetSetMethod (nonPublic: true)
				?? throw new InvalidOperationException ("Missing reference-manager setter.");
			setter.Invoke (runtime, new object [] { manager });
		}

		sealed class ReferenceObserver : JniRuntime.JniObjectReferenceManager
		{
			readonly JniRuntime.JniObjectReferenceManager inner;
			readonly IntPtr source;
			readonly IntPtr input;
			readonly JniObjectReferenceType inputType;
			readonly int ownerThread = Environment.CurrentManagedThreadId;
			int inputDeletions;

			public ReferenceObserver (JniRuntime.JniObjectReferenceManager inner, IntPtr source, IntPtr input, JniObjectReferenceType inputType)
			{
				this.inner = inner;
				this.source = source;
				this.input = input;
				this.inputType = inputType;
			}

			public int InputDeletions => Volatile.Read (ref inputDeletions);
			public ConcurrentDictionary<IntPtr, string> OwnedCopies { get; } = new ();
			public ConcurrentDictionary<IntPtr, string> OutstandingGlobals { get; } = new ();
			public override int GlobalReferenceCount => inner.GlobalReferenceCount;
			public override int WeakGlobalReferenceCount => inner.WeakGlobalReferenceCount;
			public override bool LogGlobalReferenceMessages => inner.LogGlobalReferenceMessages;
			public override bool LogLocalReferenceMessages => inner.LogLocalReferenceMessages;

			public override void WriteGlobalReferenceLine (string format, params object [] args) => inner.WriteGlobalReferenceLine (format, args);
			public override void WriteLocalReferenceLine (string format, params object [] args) => inner.WriteLocalReferenceLine (format, args);
			public override JniObjectReference CreateLocalReference (JniObjectReference reference, ref int count) => inner.CreateLocalReference (reference, ref count);
			public override void CreatedLocalReference (JniObjectReference reference, ref int count) => inner.CreatedLocalReference (reference, ref count);
			public override IntPtr ReleaseLocalReference (ref JniObjectReference reference, ref int count) => inner.ReleaseLocalReference (ref reference, ref count);
			public override JniObjectReference CreateWeakGlobalReference (JniObjectReference reference) => inner.CreateWeakGlobalReference (reference);
			public override void DeleteWeakGlobalReference (ref JniObjectReference reference) => inner.DeleteWeakGlobalReference (ref reference);

			public override JniObjectReference CreateGlobalReference (JniObjectReference reference)
			{
				var result = inner.CreateGlobalReference (reference);
				if (result.IsValid) {
					var stack = new StackTrace (true).ToString ();
					OutstandingGlobals [result.Handle] = stack;
					if (Environment.CurrentManagedThreadId == ownerThread && JNIEnv.IsSameObject (source, result.Handle)) {
						OwnedCopies [result.Handle] = stack;
					}
				}
				return result;
			}

			public override void DeleteGlobalReference (ref JniObjectReference reference)
			{
				var deleted = reference;
				inner.DeleteGlobalReference (ref reference);
				ObserveDeletion (deleted);
				OutstandingGlobals.TryRemove (deleted.Handle, out _);
				OwnedCopies.TryRemove (deleted.Handle, out _);
			}

			public override void DeleteLocalReference (ref JniObjectReference reference, ref int count)
			{
				var deleted = reference;
				inner.DeleteLocalReference (ref reference, ref count);
				ObserveDeletion (deleted);
			}

			void ObserveDeletion (JniObjectReference reference)
			{
				if (reference.Handle == input && reference.Type == inputType &&
						(inputType != JniObjectReferenceType.Local || Environment.CurrentManagedThreadId == ownerThread)) {
					Interlocked.Increment (ref inputDeletions);
				}
			}
		}
	}

	[Register ("net/dot/android/test/ConstructionFailurePeer", DoNotGenerateAcw = true)]
	sealed class ConstructionFailurePeer : Java.Lang.Object, IJavaPeerable
	{
		public static ConstructionFailurePeer PartialPeer;

		public ConstructionFailurePeer (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer)
		{
		}

		void IJavaPeerable.SetJniIdentityHashCode (int value)
		{
			PartialPeer = this;
			throw new InvalidOperationException ("Construction failed after copying the reference.");
		}
	}

	interface UnmappedPeer : IJavaObject
	{
	}
}
