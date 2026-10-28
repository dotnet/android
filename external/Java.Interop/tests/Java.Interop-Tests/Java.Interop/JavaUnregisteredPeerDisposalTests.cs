#nullable enable

using System;

using Java.Interop;

using NUnit.Framework;

namespace Java.InteropTests {

#if !__ANDROID__
	[NonParallelizable]
#endif
	[TestFixture]
	[Category ("UnregisteredPeerDisposal")]
	public class JavaUnregisteredPeerDisposalTests : JavaVMFixture {

		[TestCase (false)]
		[TestCase (true)]
		public unsafe void DisposeUnlessReferenced_RegisteredPeerIsNotDisposed (bool exception)
		{
			using var type = new JniType (exception ? "java/lang/Throwable" : "java/lang/Object");
			var local = type.NewObject (type.GetConstructor ("()V"), null);
			var observation = new DisposalObservation ();
			var peer = CreatePeer (exception, ref local, observation);
			try {
				var reference = peer.PeerReference;
				var block = peer.JniObjectReferenceControlBlock;
				Assert.AreNotEqual (IntPtr.Zero, block);
				Assert.AreSame (peer, JniEnvironment.Runtime.ValueManager.PeekPeer (local));

				peer.DisposeUnlessReferenced ();
				Assert.IsTrue (peer.PeerReference.IsValid);
				Assert.AreEqual (reference.Handle, peer.PeerReference.Handle);
				Assert.AreEqual (block, peer.JniObjectReferenceControlBlock);
				Assert.AreEqual (0, observation.DisposedCount);
				Assert.AreEqual (0, observation.FinalizedCount);
				Assert.AreSame (peer, JniEnvironment.Runtime.ValueManager.PeekPeer (local));
			} finally {
				Cleanup (peer);
				JniObjectReference.Dispose (ref local);
			}
		}

		[TestCase (false, false)]
		[TestCase (true, false)]
		[TestCase (false, true)]
		[TestCase (true, true)]
		public unsafe void DisposeUnlessReferenced_UnregisteredPeerReleasesControlBlock (bool exception, bool alias)
		{
			using var type = new JniType (exception ? "java/lang/Throwable" : "java/lang/Object");
			var local = type.NewObject (type.GetConstructor ("()V"), null);
			var registeredObservation = new DisposalObservation ();
			var registered = CreatePeer (exception, ref local, registeredObservation);
			var observation = alias ? new DisposalObservation () : registeredObservation;
			var peer = registered;
			try {
				var registeredBlock = registered.JniObjectReferenceControlBlock;
				if (alias)
					peer = CreatePeer (exception, ref local, observation);
				else
					peer.UnregisterFromRuntime ();

				var block = peer.JniObjectReferenceControlBlock;
				Assert.AreNotEqual (IntPtr.Zero, block);
				Assert.IsFalse (ReferenceEquals (peer, JniEnvironment.Runtime.ValueManager.PeekPeer (local)));
				peer.DisposeUnlessReferenced ();
				Assert.AreEqual (1, observation.DisposedCount);
				Assert.AreEqual (0, observation.FinalizedCount);
				Assert.IsTrue (observation.ReferenceWasValid, "Dispose(true) must enter with the unregistered peer's valid reference.");
				Assert.AreEqual (block, observation.ControlBlockDuringCallback);
				Assert.IsFalse (peer.PeerReference.IsValid);
				Assert.AreEqual (IntPtr.Zero, peer.JniObjectReferenceControlBlock);

				peer.DisposeUnlessReferenced ();
				peer.Dispose ();
				peer.SetPeerReference (default);
				Assert.AreEqual (1, observation.DisposedCount);
				Assert.AreEqual (IntPtr.Zero, peer.JniObjectReferenceControlBlock);
				if (alias) {
					Assert.IsTrue (registered.PeerReference.IsValid);
					Assert.AreEqual (registeredBlock, registered.JniObjectReferenceControlBlock);
					Assert.AreEqual (0, registeredObservation.DisposedCount);
					Assert.AreSame (registered, JniEnvironment.Runtime.ValueManager.PeekPeer (local));
				} else {
					Assert.IsNull (JniEnvironment.Runtime.ValueManager.PeekPeer (local));
				}
			} finally {
				Cleanup (peer);
				if (!ReferenceEquals (peer, registered))
					Cleanup (registered);
				JniObjectReference.Dispose (ref local);
			}
		}

		[TestCase (false)]
		[TestCase (true)]
		public unsafe void DisposeUnlessReferenced_ThrowingCallbackCanRetry (bool exception)
		{
			using var type = new JniType (exception ? "java/lang/Throwable" : "java/lang/Object");
			var local = type.NewObject (type.GetConstructor ("()V"), null);
			var observation = new DisposalObservation { ThrowOnDispose = true };
			var peer = CreatePeer (exception, ref local, observation);
			try {
				peer.UnregisterFromRuntime ();
				var reference = peer.PeerReference;
				var block = peer.JniObjectReferenceControlBlock;
				Assert.Throws<InvalidOperationException> (() => peer.DisposeUnlessReferenced ());
				Assert.AreEqual (1, observation.DisposedCount);
				Assert.AreEqual (0, observation.FinalizedCount);
				Assert.IsTrue (observation.ReferenceWasValid);
				Assert.AreEqual (block, observation.ControlBlockDuringCallback);
				Assert.AreEqual (reference.Handle, peer.PeerReference.Handle);
				Assert.IsTrue (JniEnvironment.Types.IsSameObject (local, peer.PeerReference));
				Assert.IsNull (JniEnvironment.Runtime.ValueManager.PeekPeer (local));

				observation.ThrowOnDispose = false;
				peer.DisposeUnlessReferenced ();
				Assert.AreEqual (2, observation.DisposedCount);
				Assert.IsTrue (observation.ReferenceWasValid);
				Assert.IsFalse (peer.PeerReference.IsValid);
				Assert.AreEqual (IntPtr.Zero, peer.JniObjectReferenceControlBlock);
				peer.Dispose ();
				Assert.AreEqual (2, observation.DisposedCount);
			} finally {
				observation.ThrowOnDispose = false;
				Cleanup (peer);
				JniObjectReference.Dispose (ref local);
			}
		}

#if __ANDROID__
		[Test]
		public void DisposeUnlessReferenced_UnregisteredRectReleasesControlBlock ()
		{
			using var rectangle = new Android.Graphics.Rect (1, 2, 3, 4);
			IJavaPeerable peer = rectangle;
			Assert.AreNotEqual (IntPtr.Zero, peer.JniObjectReferenceControlBlock);
			rectangle.UnregisterFromRuntime ();
			rectangle.DisposeUnlessReferenced ();
			Assert.IsFalse (peer.PeerReference.IsValid);
			Assert.AreEqual (IntPtr.Zero, peer.JniObjectReferenceControlBlock);
			rectangle.DisposeUnlessReferenced ();
			rectangle.Dispose ();
			Assert.AreEqual (IntPtr.Zero, peer.JniObjectReferenceControlBlock);
		}
#endif

		[TestCase (false, false)]
		[TestCase (true, false)]
		[TestCase (false, true)]
		[TestCase (true, true)]
		[Category ("JniReferenceLeak")]
		public unsafe void DisposeUnlessReferenced_DeletesGlobalReference (bool exception, bool alias)
		{
			using var type = new JniType (exception ? "java/lang/Throwable" : "java/lang/Object");
			var constructor = type.GetConstructor ("()V");

			GC.Collect ();
			GC.WaitForPendingFinalizers ();
			JniEnvironment.Runtime.ValueManager.CollectPeers ();
			JniEnvironment.Runtime.ValueManager.WaitForGCBridgeProcessing ();
			GC.Collect ();
			GC.WaitForPendingFinalizers ();

			// Warm peer/callback caches after collection before measuring VM-wide counts.
			for (int i = 0; i < 10; i++)
				ReleasePeer (checkReferenceCount: false);
			ReleasePeer (checkReferenceCount: true);

			void ReleasePeer (bool checkReferenceCount)
			{
				var local = type.NewObject (constructor, null);
				var registered = CreatePeer (exception, ref local, new DisposalObservation ());
				var peer = registered;
				try {
					if (alias)
						peer = CreatePeer (exception, ref local, new DisposalObservation ());
					else
						peer.UnregisterFromRuntime ();

					int globals = JniEnvironment.Runtime.GlobalReferenceCount;
					peer.DisposeUnlessReferenced ();
					if (checkReferenceCount)
						Assert.AreEqual (globals - 1, JniEnvironment.Runtime.GlobalReferenceCount,
							"JNI global-reference deletion is independent of native control-block release.");
					Assert.IsFalse (peer.PeerReference.IsValid);
					Assert.AreEqual (IntPtr.Zero, peer.JniObjectReferenceControlBlock);
					if (alias) {
						Assert.IsTrue (registered.PeerReference.IsValid);
						Assert.AreSame (registered, JniEnvironment.Runtime.ValueManager.PeekPeer (local));
					}
				} finally {
					Cleanup (peer);
					if (!ReferenceEquals (peer, registered))
						Cleanup (registered);
					JniObjectReference.Dispose (ref local);
				}
			}
		}

		static IJavaPeerable CreatePeer (bool exception, ref JniObjectReference reference, DisposalObservation observation)
		{
			return exception
				? new ObservedException (ref reference, observation)
				: new ObservedObject (ref reference, observation);
		}

		static void Cleanup (IJavaPeerable peer)
		{
			peer.Dispose ();
			// Also free the retained block when testing the original disposal bypass.
			if (peer.JniObjectReferenceControlBlock != IntPtr.Zero) {
				peer.Disposed ();
				peer.SetPeerReference (default);
			}
			GC.SuppressFinalize (peer);
		}

		sealed class DisposalObservation {
			public IntPtr ControlBlockDuringCallback;
			public int DisposedCount;
			public int FinalizedCount;
			public bool ReferenceWasValid;
			public bool ThrowOnDispose;

			public void Observe (IJavaPeerable peer, bool disposing)
			{
				ReferenceWasValid = peer.PeerReference.IsValid;
				ControlBlockDuringCallback = peer.JniObjectReferenceControlBlock;
				if (disposing) {
					DisposedCount++;
					if (ThrowOnDispose)
						throw new InvalidOperationException ("Disposal callback failed.");
				} else {
					FinalizedCount++;
				}
			}
		}

		[JniTypeSignature ("java/lang/Object", GenerateJavaPeer = false)]
		sealed class ObservedObject : JavaObject {
			readonly DisposalObservation? observation;

			public ObservedObject (ref JniObjectReference reference, DisposalObservation observation)
				: base (ref reference, JniObjectReferenceOptions.Copy)
			{
				this.observation = observation;
			}

			protected override void Dispose (bool disposing)
			{
				base.Dispose (disposing);
				observation?.Observe (this, disposing);
			}
		}

		[JniTypeSignature ("java/lang/Throwable", GenerateJavaPeer = false)]
		sealed class ObservedException : JavaException {
			readonly DisposalObservation? observation;

			public ObservedException (ref JniObjectReference reference, DisposalObservation observation)
				: base (ref reference, JniObjectReferenceOptions.Copy)
			{
				this.observation = observation;
			}

			protected override void Dispose (bool disposing)
			{
				base.Dispose (disposing);
				observation?.Observe (this, disposing);
			}
		}
	}
}
