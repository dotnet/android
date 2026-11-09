using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Threading;

using Android.Runtime;

using Java.Interop;
using Microsoft.Android.Runtime;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Java.InteropTests
{
	[TestClass]
	[DoNotParallelize]
	[TestCategory ("TransferredReferences")]
	public class TransferredReferenceTests
	{
		public TestContext TestContext { get; set; }

		[TestMethod]
		[DataRow (JniHandleOwnership.DoNotTransfer)]
		[DataRow (JniHandleOwnership.TransferLocalRef)]
		[DataRow (JniHandleOwnership.TransferGlobalRef)]
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

		[TestMethod]
		[DataRow (JniHandleOwnership.DoNotTransfer)]
		[DataRow (JniHandleOwnership.TransferLocalRef)]
		[DataRow (JniHandleOwnership.TransferGlobalRef)]
		public void ObjectConstructionFailureReleasesInput (JniHandleOwnership ownership)
		{
			WithInput ("java/lang/Object", ownership, (handle, transfer) => {
				try {
					Assert.ThrowsExactly<InvalidOperationException> (() => new ConstructionFailurePeer (handle, transfer));
				} finally {
					ConstructionFailurePeer.PartialPeer?.Dispose ();
					ConstructionFailurePeer.PartialPeer = null;
				}
			});
		}

		[TestMethod]
		[DataRow (JniHandleOwnership.DoNotTransfer)]
		[DataRow (JniHandleOwnership.TransferLocalRef)]
		[DataRow (JniHandleOwnership.TransferGlobalRef)]
		public void GetObjectExistingPeerConsumesInput (JniHandleOwnership ownership)
		{
			using var existing = new Java.Lang.String ("existing");
			WithInput (existing.Handle, ownership, (handle, transfer) => {
				Assert.AreSame (existing, Java.Lang.Object.GetObject<Java.Lang.String> (handle, transfer));
			});
		}

		[TestMethod]
		[DataRow (false, JniHandleOwnership.DoNotTransfer)]
		[DataRow (false, JniHandleOwnership.TransferLocalRef)]
		[DataRow (false, JniHandleOwnership.TransferGlobalRef)]
		[DataRow (true, JniHandleOwnership.DoNotTransfer)]
		[DataRow (true, JniHandleOwnership.TransferLocalRef)]
		[DataRow (true, JniHandleOwnership.TransferGlobalRef)]
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

		[TestMethod]
		[DataRow (JniHandleOwnership.DoNotTransfer)]
		[DataRow (JniHandleOwnership.TransferLocalRef)]
		[DataRow (JniHandleOwnership.TransferGlobalRef)]
		public void LookupFailureReleasesInput (JniHandleOwnership ownership)
		{
			WithInput ("java/lang/Object", ownership, (handle, transfer) => {
				Assert.ThrowsExactly<ArgumentException> (() => Java.Lang.Object.GetObject<UnmappedPeer> (handle, transfer));
			});
		}

		[TestMethod]
		[DataRow (false, JniHandleOwnership.DoNotTransfer)]
		[DataRow (false, JniHandleOwnership.TransferLocalRef)]
		[DataRow (false, JniHandleOwnership.TransferGlobalRef)]
		[DataRow (true, JniHandleOwnership.DoNotTransfer)]
		[DataRow (true, JniHandleOwnership.TransferLocalRef)]
		[DataRow (true, JniHandleOwnership.TransferGlobalRef)]
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
					Assert.ThrowsExactly<InvalidOperationException> (() => proxy.CreateInstance (handle, transfer));
					Assert.IsNotNull (partial);
					Assert.IsTrue (partial.PeerReference.IsValid);
				} finally {
					partial?.Dispose ();
					InheritedJavaInteropListInvoker.PeerCreated = null;
					JavaInteropCollectionInvoker.PeerCreated = null;
				}
			});
		}

		[TestMethod]
		[DataRow (JniHandleOwnership.DoNotTransfer)]
		[DataRow (JniHandleOwnership.TransferLocalRef)]
		[DataRow (JniHandleOwnership.TransferGlobalRef)]
		public void IncompatibleLookupConsumesInput (JniHandleOwnership ownership)
		{
			if (!RuntimeFeature.IsAssignableFromCheck) {
				Assert.Inconclusive ("This configuration intentionally disables incompatible-cast checking.");
			}
			WithInput ("java/lang/Object", ownership, (handle, transfer) => {
				Assert.IsNull (Java.Lang.Object.GetObject<Java.Lang.String> (handle, transfer));
			});
		}

		[TestMethod]
		[DataRow (JniHandleOwnership.DoNotTransfer)]
		[DataRow (JniHandleOwnership.TransferLocalRef)]
		[DataRow (JniHandleOwnership.TransferGlobalRef)]
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

		[TestMethod]
		[DataRow (1, JniHandleOwnership.TransferGlobalRef)]
		[DataRow (2, JniHandleOwnership.TransferGlobalRef)]
		[DataRow (3, JniHandleOwnership.TransferGlobalRef)]
		[DataRow (4, JniHandleOwnership.TransferGlobalRef)]
		[DataRow (1, JniHandleOwnership.TransferLocalRef)]
		[DataRow (2, JniHandleOwnership.TransferLocalRef)]
		[DataRow (3, JniHandleOwnership.TransferLocalRef)]
		[DataRow (4, JniHandleOwnership.TransferLocalRef)]
		[DataRow (1, JniHandleOwnership.DoNotTransfer)]
		[DataRow (2, JniHandleOwnership.DoNotTransfer)]
		[DataRow (3, JniHandleOwnership.DoNotTransfer)]
		[DataRow (4, JniHandleOwnership.DoNotTransfer)]
		public void ThrowableExtractionFailureReleasesInput (int stage, JniHandleOwnership ownership)
		{
			AssertThrowableExtractionFailureReleasesInput (stage, ownership, throughRuntime: false);
		}

		[TestMethod]
		[Microsoft.VisualStudio.TestTools.UnitTesting.Combinatorial.CombinatorialData]
		public void RuntimeExceptionLookupFailureReleasesInput (
			[Microsoft.VisualStudio.TestTools.UnitTesting.Combinatorial.CombinatorialValues (1, 2, 3, 4)] int stage,
			[Microsoft.VisualStudio.TestTools.UnitTesting.Combinatorial.CombinatorialValues (JniHandleOwnership.DoNotTransfer, JniHandleOwnership.TransferLocalRef, JniHandleOwnership.TransferGlobalRef)] JniHandleOwnership ownership)
		{
			AssertThrowableExtractionFailureReleasesInput (stage, ownership, throughRuntime: true);
		}

		void AssertThrowableExtractionFailureReleasesInput (int stage, JniHandleOwnership ownership, bool throughRuntime)
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
						if (throughRuntime) {
							var reference = new JniObjectReference (handle, transfer == JniHandleOwnership.TransferGlobalRef
								? JniObjectReferenceType.Global : JniObjectReferenceType.Local);
							var options = transfer == JniHandleOwnership.DoNotTransfer
								? JniObjectReferenceOptions.Copy : JniObjectReferenceOptions.CopyAndDispose;
							failure = Assert.ThrowsExactly<Java.Lang.IllegalStateException> (() =>
								JniEnvironment.Runtime.GetExceptionForThrowable (ref reference, options));
							Assert.AreEqual (transfer == JniHandleOwnership.DoNotTransfer, reference.IsValid,
								"Only the borrowed throwable reference should remain valid after extraction fails.");
						} else {
							failure = Assert.ThrowsExactly<Java.Lang.IllegalStateException> (() => new Java.Lang.Throwable (handle, transfer));
						}
						StringAssert.Contains (failure.Message, "transfer-extraction-" + stage);
						if (stage == 4) {
							Assert.IsNull (JniEnvironment.Runtime.ValueManager.PeekPeer (new JniObjectReference (source)),
								"Failed stack-trace extraction must release the partially constructed wrapper.");
						}
					} finally {
						failure?.Dispose ();
					}
				});
			} finally {
				JNIEnv.DeleteLocalRef (source);
			}
		}

		[TestMethod]
		[DataRow (JniHandleOwnership.DoNotTransfer)]
		[DataRow (JniHandleOwnership.TransferLocalRef)]
		[DataRow (JniHandleOwnership.TransferGlobalRef)]
		public void ThrowableSuccessDoesNotDeleteInputPrematurely (JniHandleOwnership ownership)
		{
			WithInput ("java/lang/Throwable", ownership, (handle, transfer) => {
				using var peer = new Java.Lang.Throwable (handle, transfer);
				Assert.IsTrue (peer.PeerReference.IsValid);
				StringAssert.Contains (peer.StackTrace, "java.lang.Throwable");
			});
		}

		[TestMethod]
		[Microsoft.VisualStudio.TestTools.UnitTesting.Combinatorial.CombinatorialData]
		public void ThrowableExtractionPreservesEscapingPeer (
			[Microsoft.VisualStudio.TestTools.UnitTesting.Combinatorial.CombinatorialValues (5, 6)] int stage,
			[Microsoft.VisualStudio.TestTools.UnitTesting.Combinatorial.CombinatorialValues (JniHandleOwnership.DoNotTransfer, JniHandleOwnership.TransferLocalRef,
				JniHandleOwnership.TransferGlobalRef, JniHandleOwnership.DoNotTransfer | JniHandleOwnership.DoNotRegister)] JniHandleOwnership ownership)
		{
			var source = JNIEnv.CreateInstance ("net/dot/android/test/TransferFailureThrowable", "(I)V", new JValue (stage));
			try {
				WithInput (source, ownership, (handle, transfer) => {
					var sourceClass = JNIEnv.GetObjectClass (source);
					try {
						JNIEnv.CallVoidMethod (source, JNIEnv.GetMethodID (sourceClass, "reset", "()V"));
					} finally {
						JNIEnv.DeleteLocalRef (sourceClass);
					}
					var failure = Assert.Throws<System.Exception> (() => new Java.Lang.Throwable (handle, transfer));
					var escaped = (stage == 5 ? failure : failure?.InnerException) as Java.Lang.Throwable;
					try {
						Assert.IsNotNull (escaped, "The Java failure must surface the source throwable directly or as its cause.");
						if (escaped == null)
							throw new AssertFailedException ("The escaping Throwable peer is missing.");
						Assert.IsTrue (escaped.PeerReference.IsValid, "Constructor cleanup must not dispose an escaping peer.");
						Assert.IsTrue (JNIEnv.IsSameObject (source, escaped.Handle));
						Assert.AreSame (escaped, JniEnvironment.Runtime.ValueManager.PeekPeer (escaped.PeerReference));
						if (JniEnvironment.Runtime.ObjectReferenceManager is ReferenceObserver observer) {
							Assert.AreEqual (1, observer.OwnedCopies.Count,
								"Only the escaping peer may retain a source GREF; an unpublished alias must be released.");
						}
						using var peerClass = escaped.Class;
						Assert.IsNotNull (peerClass, "The escaping peer must still support JNI calls.");

						JniObjectReference pending = default;
						try {
							JniEnvironment.Runtime.RaisePendingException (escaped);
							pending = JniEnvironment.Exceptions.ExceptionOccurred ();
							JniEnvironment.Exceptions.ExceptionClear ();
							JniEnvironment.References.CreatedReference (pending);
							Assert.IsTrue (pending.IsValid);
							Assert.AreSame (escaped, JniEnvironment.Runtime.GetExceptionForThrowable (
								ref pending, JniObjectReferenceOptions.CopyAndDispose));
						} finally {
							JniEnvironment.Exceptions.ExceptionClear ();
							JniObjectReference.Dispose (ref pending);
						}
					} finally {
						(failure as IDisposable)?.Dispose ();
						if (!ReferenceEquals (failure, escaped))
							escaped?.Dispose ();
					}
				});
			} finally {
				JNIEnv.DeleteLocalRef (source);
			}
		}

		[TestMethod]
		[DataRow (JniHandleOwnership.DoNotTransfer)]
		[DataRow (JniHandleOwnership.TransferLocalRef)]
		[DataRow (JniHandleOwnership.TransferGlobalRef)]
		[DataRow (JniHandleOwnership.DoNotTransfer | JniHandleOwnership.DoNotRegister)]
		[DataRow (JniHandleOwnership.TransferLocalRef | JniHandleOwnership.DoNotRegister)]
		[DataRow (JniHandleOwnership.TransferGlobalRef | JniHandleOwnership.DoNotRegister)]
		public void ThrowableConstructionHonorsDoNotRegister (JniHandleOwnership ownership)
		{
			WithInput ("java/lang/Throwable", ownership, (handle, transfer) => {
				using var peer = new Java.Lang.Throwable (handle, transfer);
				Assert.IsTrue (peer.PeerReference.IsValid);
				var registered = JniEnvironment.Runtime.ValueManager.PeekPeer (peer.PeerReference);
				if ((transfer & JniHandleOwnership.DoNotRegister) != 0) {
					Assert.IsNull (registered, "DoNotRegister must suppress registration for a newly constructed Throwable wrapper.");
				} else {
					Assert.AreSame (peer, registered, "Normal Throwable construction must register its wrapper.");
				}
			});
		}

		[TestMethod]
		public void ThrowableActivationPreservesExistingReplaceablePeer ()
		{
			WithInput ("java/lang/Throwable", JniHandleOwnership.DoNotTransfer, (handle, _) => {
				using var existing = new Java.Lang.Throwable (handle, JniHandleOwnership.DoNotTransfer);
				((IJavaPeerable) existing).SetJniManagedPeerState (JniManagedPeerStates.Replaceable);

				Assert.AreSame (existing, JniEnvironment.Runtime.ValueManager.PeekPeer (existing.PeerReference));

				var activated = TrimmableTypeMap.Instance.CreateInstance (handle, typeof (Java.Lang.Throwable));
				Assert.IsNotNull (activated, "The Throwable activation proxy must create a peer.");
				try {
					Assert.AreSame (existing, JniEnvironment.Runtime.ValueManager.PeekPeer (existing.PeerReference),
						"Activation must mark the new peer Replaceable before registering it, preserving the existing winner.");
				} finally {
					activated?.Dispose ();
				}
			});
		}

		[TestMethod]
		[DataRow (JniHandleOwnership.DoNotTransfer)]
		[DataRow (JniHandleOwnership.TransferLocalRef)]
		[DataRow (JniHandleOwnership.TransferGlobalRef)]
		public void ThrowableDoNotRegisterPreservesReentrantAlias (JniHandleOwnership ownership)
		{
			WithInput ("java/lang/Throwable", ownership, (handle, transfer) => {
				Java.Lang.Throwable nested = null;
				ReentrantThrowable.BeforeConstruct = value => {
					nested = Java.Lang.Object.GetObject<Java.Lang.Throwable> (value, JniHandleOwnership.DoNotTransfer);
				};
				ReentrantThrowable outer = null;
				try {
					outer = new ReentrantThrowable (handle, transfer | JniHandleOwnership.DoNotRegister);
					Assert.IsNotNull (nested);
					Assert.AreSame (nested, JniEnvironment.Runtime.ValueManager.PeekPeer (outer.PeerReference),
						"DoNotRegister must preserve the peer created by a reentrant lookup.");
				} finally {
					ReentrantThrowable.BeforeConstruct = null;
					outer?.Dispose ();
					nested?.Dispose ();
				}
			});
		}

		[TestMethod]
		public void NullHandleReturnsNull ()
		{
			foreach (var ownership in new [] { JniHandleOwnership.DoNotTransfer, JniHandleOwnership.TransferLocalRef, JniHandleOwnership.TransferGlobalRef }) {
				Assert.IsNull (Java.Lang.Object.GetObject<Java.Lang.Object> (IntPtr.Zero, ownership));
			}
		}

		void WithInput (string className, JniHandleOwnership ownership, Action<IntPtr, JniHandleOwnership> exercise)
		{
			var source = JNIEnv.CreateInstance (className, "()V");
			try {
				WithInput (source, ownership, exercise);
			} finally {
				JNIEnv.DeleteLocalRef (source);
			}
		}

		void WithInput (IntPtr source, JniHandleOwnership ownership, Action<IntPtr, JniHandleOwnership> exercise)
		{
			// Warm class, method, proxy and exception caches without transferring the source.
			exercise (source, JniHandleOwnership.DoNotTransfer);
			var locals = Java.Interop.Runtime.LocalReferenceCount;
			var transferType = ownership & (JniHandleOwnership.TransferLocalRef | JniHandleOwnership.TransferGlobalRef);
			var transfersInput = transferType != JniHandleOwnership.DoNotTransfer;
			var input = transferType == JniHandleOwnership.TransferGlobalRef
				? JNIEnv.NewGlobalRef (source)
				: JNIEnv.NewLocalRef (source);
			var type = transferType == JniHandleOwnership.TransferGlobalRef ? JniObjectReferenceType.Global : JniObjectReferenceType.Local;
			var runtime = JniEnvironment.Runtime;
			var originalManager = runtime.ObjectReferenceManager;
			var observer = new ReferenceObserver (originalManager, source, input, type);
			observer.OnSetRuntime (runtime);
			try {
				SetReferenceManager (runtime, observer);
				exercise (input, ownership);
				Assert.AreEqual (transfersInput ? 1 : 0, observer.InputDeletions,
					"Only the original transferred input must be released, exactly once.");
				Assert.IsEmpty (observer.OwnedCopies, "The partially constructed wrapper's own copies must be disposed separately.");
				foreach (var reference in observer.OutstandingGlobals) {
					TestContext.WriteLine ($"Other outstanding GREF {reference.Key:x}: {reference.Value}");
				}
				Assert.AreEqual (locals + (transfersInput ? 0 : 1),
					Java.Interop.Runtime.LocalReferenceCount, "Only transferred local references may be deleted.");
				if (!transfersInput) {
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

	sealed class ReentrantThrowable : Java.Lang.Throwable
	{
		public static Action<IntPtr> BeforeConstruct;

		public ReentrantThrowable (IntPtr handle, JniHandleOwnership transfer)
			: base (BeforeConstructHandle (handle), transfer)
		{
		}

		static IntPtr BeforeConstructHandle (IntPtr handle)
		{
			BeforeConstruct?.Invoke (handle);
			return handle;
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
