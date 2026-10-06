#nullable enable

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

using Java.Interop;

using NUnit.Framework;

namespace Java.InteropTests {

	[TestFixture]
	[Category ("PeerControlBlock")]
	public class JavaPeerControlBlockTests : JavaVMFixture {

		public enum ReferenceState {
			Global,
			Local,
			Invalid,
			NoBlock,
		}

		[TestCase (false, ReferenceState.Global)]
		[TestCase (true, ReferenceState.Global)]
		[TestCase (false, ReferenceState.Local)]
		[TestCase (true, ReferenceState.Local)]
		[TestCase (false, ReferenceState.Invalid)]
		[TestCase (true, ReferenceState.Invalid)]
		[TestCase (false, ReferenceState.NoBlock)]
		[TestCase (true, ReferenceState.NoBlock)]
		public unsafe void FinalizePeer_ReleasesControlBlock (bool exception, ReferenceState state)
		{
			using var type = new JniType (exception ? "java/lang/Throwable" : "java/lang/Object");
			var local = type.NewObject (type.GetConstructor ("()V"), null);
			var observation = new FinalizationObservation ();
			var reference = state == ReferenceState.Global ? local : default;
			var options = state == ReferenceState.Global ? JniObjectReferenceOptions.Copy : JniObjectReferenceOptions.None;
			var peer = CreatePeer (exception, ref reference, options, observation);
			try {
				if (state == ReferenceState.Local)
					peer.SetPeerReference (local);
				else if (state == ReferenceState.Invalid)
					peer.SetPeerReference (default);

				Assert.AreEqual (state == ReferenceState.NoBlock, peer.JniObjectReferenceControlBlock == IntPtr.Zero);
				int globals = JniEnvironment.Runtime.GlobalReferenceCount;
				JniEnvironment.Runtime.ValueManager.FinalizePeer (peer);

				Assert.AreEqual (globals - (state == ReferenceState.Global ? 1 : 0),
					JniEnvironment.Runtime.GlobalReferenceCount, "JNI reference release is independent of control-block release.");
				Assert.AreEqual (1, observation.FinalizedCount);
				Assert.IsFalse (observation.ReferenceWasValid);
				Assert.AreNotEqual (IntPtr.Zero, observation.ControlBlockDuringCallback);
				Assert.AreEqual (IntPtr.Zero, peer.JniObjectReferenceControlBlock, "Finalization must release the native allocation.");

				// Repeated clearing/disposal must not free the same allocation twice.
				peer.SetPeerReference (default);
				peer.Dispose ();
				Assert.AreEqual (IntPtr.Zero, peer.JniObjectReferenceControlBlock);
			} finally {
				Cleanup (peer);
				JniObjectReference.Dispose (ref local);
			}
		}

		[TestCase (false)]
		[TestCase (true)]
		public unsafe void FinalizePeer_AfterExplicitDisposeDoesNotDoubleFree (bool exception)
		{
			using var type = new JniType (exception ? "java/lang/Throwable" : "java/lang/Object");
			var local = type.NewObject (type.GetConstructor ("()V"), null);
			var observation = new FinalizationObservation ();
			var peer = CreatePeer (exception, ref local, JniObjectReferenceOptions.CopyAndDispose, observation);
			try {
				peer.Dispose ();
				JniEnvironment.Runtime.ValueManager.FinalizePeer (peer);
				Assert.AreEqual (1, observation.FinalizedCount);
				Assert.IsFalse (observation.ReferenceWasValid);
				Assert.AreEqual (IntPtr.Zero, peer.JniObjectReferenceControlBlock);
			} finally {
				Cleanup (peer);
				JniObjectReference.Dispose (ref local);
			}
		}

		[TestCase (false)]
		[TestCase (true)]
		public void FinalizePeer_ThrowingCallbackReleasesControlBlock (bool exception)
		{
			var observation = new FinalizationObservation { ThrowOnFinalize = true };
			var reference = default (JniObjectReference);
			var peer = CreatePeer (exception, ref reference, JniObjectReferenceOptions.None, observation);
			peer.SetPeerReference (default);
			try {
				Assert.Throws<InvalidOperationException> (() => JniEnvironment.Runtime.ValueManager.FinalizePeer (peer));
				Assert.AreEqual (1, observation.FinalizedCount);
				Assert.IsFalse (observation.ReferenceWasValid);
				Assert.AreEqual (IntPtr.Zero, peer.JniObjectReferenceControlBlock);
			} finally {
				Cleanup (peer);
			}
		}

		[TestCase (false)]
		[TestCase (true)]
		public unsafe void FinalizePeer_RetainsReferenceReattachedByCallback (bool exception)
		{
			using var type = new JniType (exception ? "java/lang/Throwable" : "java/lang/Object");
			var local = type.NewObject (type.GetConstructor ("()V"), null);
			var observation = new FinalizationObservation { Reattach = local };
			var reference = default (JniObjectReference);
			var peer = CreatePeer (exception, ref reference, JniObjectReferenceOptions.None, observation);
			try {
				JniEnvironment.Runtime.ValueManager.FinalizePeer (peer);
				Assert.IsFalse (observation.ReferenceWasValid, "Dispose(false) must still enter with an invalid reference.");
				Assert.IsTrue (peer.PeerReference.IsValid);
				Assert.AreNotEqual (IntPtr.Zero, peer.JniObjectReferenceControlBlock);
				Assert.IsTrue (JniEnvironment.Types.IsSameObject (local, peer.PeerReference));
				Assert.AreSame (peer, JniEnvironment.Runtime.ValueManager.PeekPeer (local));
				peer.Dispose ();
				Assert.AreEqual (IntPtr.Zero, peer.JniObjectReferenceControlBlock);
			} finally {
				Cleanup (peer);
				JniObjectReference.Dispose (ref local);
			}
		}

		[TestCase (false, false, false)]
		[TestCase (true, false, false)]
		[TestCase (false, true, false)]
		[TestCase (true, true, false)]
		[TestCase (false, false, true)]
		[TestCase (true, false, true)]
		[TestCase (false, true, true)]
		[TestCase (true, true, true)]
		public void ActualFinalizer_ReleasesControlBlock (bool exception, bool invalid, bool throwingConstructor)
		{
			var observation = new FinalizationObservation ();
			CreateCollectiblePeer (exception, invalid, throwingConstructor, observation);
			try {
				var stopwatch = Stopwatch.StartNew ();
				while (observation.Peer == null && stopwatch.Elapsed < TimeSpan.FromSeconds (10)) {
					GC.Collect (generation: 2, mode: GCCollectionMode.Forced, blocking: true);
					GC.WaitForPendingFinalizers ();
					JniEnvironment.Runtime.ValueManager.CollectPeers ();
					Thread.Sleep (10);
				}
				GC.WaitForPendingFinalizers ();

				var peer = observation.Peer;
				Assert.IsNotNull (peer, "The actual JavaObject/JavaException finalizer must run.");
				if (peer == null)
					throw new InvalidOperationException ("Finalizer did not resurrect the observation peer.");
				Assert.AreEqual (1, observation.FinalizedCount);
				Assert.IsFalse (observation.ReferenceWasValid);
				Assert.AreNotEqual (IntPtr.Zero, observation.ControlBlockDuringCallback);
				Assert.AreEqual (IntPtr.Zero, peer.JniObjectReferenceControlBlock,
					"Even a resurrected managed wrapper must not retain its cleared native block.");
			} finally {
				if (observation.Peer is IJavaPeerable peer)
					Cleanup (peer);
			}
		}

		[TestCase (false)]
		[TestCase (true)]
		public unsafe void ExplicitDispose_PreservesCallbackAndReleasesControlBlock (bool exception)
		{
			using var type = new JniType (exception ? "java/lang/Throwable" : "java/lang/Object");
			var local = type.NewObject (type.GetConstructor ("()V"), null);
			var observation = new FinalizationObservation ();
			var peer = CreatePeer (exception, ref local, JniObjectReferenceOptions.CopyAndDispose, observation);
			try {
				int globals = JniEnvironment.Runtime.GlobalReferenceCount;
				peer.Dispose ();
				Assert.AreEqual (globals - 1, JniEnvironment.Runtime.GlobalReferenceCount);
				Assert.IsTrue (observation.ReferenceWasValid, "Dispose(true) must still enter with a valid reference.");
				Assert.AreEqual (0, observation.FinalizedCount);
				Assert.AreEqual (IntPtr.Zero, peer.JniObjectReferenceControlBlock);
				peer.Dispose ();
			} finally {
				Cleanup (peer);
				JniObjectReference.Dispose (ref local);
			}
		}

		[MethodImpl (MethodImplOptions.NoInlining)]
		static unsafe void CreateCollectiblePeer (bool exception, bool invalid, bool throwingConstructor, FinalizationObservation observation)
		{
			using var type = new JniType (exception ? "java/lang/Throwable" : "java/lang/Object");
			var local = invalid ? default : type.NewObject (type.GetConstructor ("()V"), null);
			try {
				var options = invalid ? JniObjectReferenceOptions.None : JniObjectReferenceOptions.Copy;
				try {
					CreatePeer (exception, ref local, options, observation, throwingConstructor);
					Assert.IsFalse (throwingConstructor, "Constructor must throw after acquiring its peer.");
				} catch (InvalidOperationException) when (throwingConstructor) {
				}
				Assert.IsNotNull (observation.WeakPeer, "Construction must reach the derived constructor.");
#if NO_GC_BRIDGE_SUPPORT
				// The host test manager strongly roots registered peers; Android must
				// instead exercise collection through its reference-tracking bridge.
				if (observation.WeakPeer != null && observation.WeakPeer.TryGetTarget (out var peer))
					JniEnvironment.Runtime.ValueManager.RemovePeer (peer);
#endif
			} finally {
				JniObjectReference.Dispose (ref local);
			}
		}

		static IJavaPeerable CreatePeer (bool exception, ref JniObjectReference reference,
			JniObjectReferenceOptions options, FinalizationObservation observation, bool throwingConstructor = false)
		{
			return exception
				? new ObservedException (ref reference, options, observation, throwingConstructor)
				: new ObservedObject (ref reference, options, observation, throwingConstructor);
		}

		static void Cleanup (IJavaPeerable peer)
		{
			GC.SuppressFinalize (peer);
			peer.Dispose ();
			peer.SetPeerReference (default);
		}

		sealed class FinalizationObservation {
			public volatile IJavaPeerable? Peer;
			public WeakReference<IJavaPeerable>? WeakPeer;
			public IntPtr ControlBlockDuringCallback;
			public int FinalizedCount;
			public bool ReferenceWasValid;
			public bool ThrowOnFinalize;
			public JniObjectReference Reattach;

			public void Observe (IJavaPeerable peer, bool disposing)
			{
				ReferenceWasValid = peer.PeerReference.IsValid;
				if (disposing)
					return;
				ControlBlockDuringCallback = peer.JniObjectReferenceControlBlock;
				Interlocked.Increment (ref FinalizedCount);
				Peer = peer;
				if (Reattach.IsValid) {
					var reference = Reattach;
					JniEnvironment.Runtime.ValueManager.ConstructPeer (peer, ref reference, JniObjectReferenceOptions.Copy);
				}
				if (ThrowOnFinalize)
					throw new InvalidOperationException ("Finalization callback failed.");
			}
		}

		[JniTypeSignature ("java/lang/Object", GenerateJavaPeer = false)]
		sealed class ObservedObject : JavaObject {
			readonly FinalizationObservation? observation;

			public ObservedObject (ref JniObjectReference reference, JniObjectReferenceOptions options,
				FinalizationObservation observation, bool throwingConstructor)
				: base (ref reference, options)
			{
				this.observation = observation;
				observation.WeakPeer = new WeakReference<IJavaPeerable> (this, trackResurrection: true);
				if (throwingConstructor)
					throw new InvalidOperationException ("Constructor failed.");
			}

			protected override void Dispose (bool disposing)
			{
				base.Dispose (disposing);
				observation?.Observe (this, disposing);
			}
		}

		[JniTypeSignature ("java/lang/Throwable", GenerateJavaPeer = false)]
		sealed class ObservedException : JavaException {
			readonly FinalizationObservation? observation;

			public ObservedException (ref JniObjectReference reference, JniObjectReferenceOptions options,
				FinalizationObservation observation, bool throwingConstructor)
				: base (ref reference, options)
			{
				this.observation = observation;
				observation.WeakPeer = new WeakReference<IJavaPeerable> (this, trackResurrection: true);
				if (throwingConstructor)
					throw new InvalidOperationException ("Constructor failed.");
			}

			protected override void Dispose (bool disposing)
			{
				base.Dispose (disposing);
				observation?.Observe (this, disposing);
			}
		}
	}
}
