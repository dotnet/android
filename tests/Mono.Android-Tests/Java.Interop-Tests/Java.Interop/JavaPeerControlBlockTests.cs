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
	public class JavaPeerControlBlockTests {

		[TestCase (false, false)]
		[TestCase (true, false)]
		[TestCase (false, true)]
		[TestCase (true, true)]
		public void FinalizePeer_ReleasesInvalidControlBlock (bool exception, bool noBlock)
		{
			var observation = new FinalizationObservation ();
			var reference = default (JniObjectReference);
			var peer = CreatePeer (exception, ref reference, JniObjectReferenceOptions.None, observation);
			try {
				if (!noBlock)
					peer.SetPeerReference (default);
				Assert.AreEqual (noBlock, peer.JniObjectReferenceControlBlock == IntPtr.Zero);

				JniEnvironment.Runtime.ValueManager.FinalizePeer (peer);

				Assert.IsFalse (peer.PeerReference.IsValid);
				Assert.AreEqual (1, observation.FinalizedCount);
				Assert.IsFalse (observation.ReferenceWasValid, "Dispose(false) must enter with an invalid reference.");
				Assert.AreNotEqual (IntPtr.Zero, observation.ControlBlockDuringCallback);
				Assert.AreEqual (IntPtr.Zero, peer.JniObjectReferenceControlBlock, "Finalization must release the native allocation.");

				peer.SetPeerReference (default);
				peer.Dispose ();
				Assert.AreEqual (IntPtr.Zero, peer.JniObjectReferenceControlBlock);
			} finally {
				Cleanup (peer);
			}
		}

		[TestCase (false)]
		[TestCase (true)]
		[Category ("CoreCLRIgnore")]
		public unsafe void FinalizePeer_PreservesLiveMonoPeer (bool exception)
		{
			using var type = new JniType (exception ? "java/lang/Throwable" : "java/lang/Object");
			var local = type.NewObject (type.GetConstructor ("()V"), null);
			var observation = new FinalizationObservation ();
			var peer = CreatePeer (exception, ref local, JniObjectReferenceOptions.CopyAndDispose, observation);
			try {
				var reference = peer.PeerReference;
				var block = peer.JniObjectReferenceControlBlock;

				JniEnvironment.Runtime.ValueManager.FinalizePeer (peer);

				Assert.AreEqual (0, observation.FinalizedCount, "The Mono bridge must retain a peer whose Java object is still alive.");
				Assert.AreEqual (reference, peer.PeerReference);
				Assert.AreEqual (block, peer.JniObjectReferenceControlBlock);
				Assert.AreSame (peer, JniEnvironment.Runtime.ValueManager.PeekPeer (reference));
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

		[TestCase (false, false)]
		[TestCase (true, false)]
		[TestCase (false, true)]
		[TestCase (true, true)]
		public unsafe void FinalizePeer_RetainsReferenceReattachedByCallback (bool exception, bool throwingCallback)
		{
			using var type = new JniType (exception ? "java/lang/Throwable" : "java/lang/Object");
			var local = type.NewObject (type.GetConstructor ("()V"), null);
			var observation = new FinalizationObservation { Reattach = local, ThrowOnFinalize = throwingCallback };
			var reference = default (JniObjectReference);
			var peer = CreatePeer (exception, ref reference, JniObjectReferenceOptions.None, observation);
			try {
				if (throwingCallback)
					Assert.Throws<InvalidOperationException> (() => JniEnvironment.Runtime.ValueManager.FinalizePeer (peer));
				else
					JniEnvironment.Runtime.ValueManager.FinalizePeer (peer);
				Assert.AreEqual (1, observation.FinalizedCount);
				Assert.IsFalse (observation.ReferenceWasValid, "Dispose(false) must still enter with an invalid reference.");
				Assert.IsTrue (peer.PeerReference.IsValid);
				Assert.AreNotEqual (IntPtr.Zero, peer.JniObjectReferenceControlBlock);
				Assert.IsTrue (JniEnvironment.Types.IsSameObject (local, peer.PeerReference));
				Assert.AreSame (peer, JniEnvironment.Runtime.ValueManager.PeekPeer (local));
				peer.Dispose ();
				Assert.IsFalse (peer.PeerReference.IsValid);
				Assert.AreEqual (IntPtr.Zero, peer.JniObjectReferenceControlBlock);
				Assert.IsNull (JniEnvironment.Runtime.ValueManager.PeekPeer (local));
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
			FinalizerHelpers.PerformNoPinAction (() => CreateCollectiblePeer (exception, invalid, throwingConstructor, observation));
			try {
				var stopwatch = Stopwatch.StartNew ();
				while (observation.Peer == null && stopwatch.Elapsed < TimeSpan.FromSeconds (10)) {
					GC.Collect (generation: 2, mode: GCCollectionMode.Forced, blocking: true);
					GC.WaitForPendingFinalizers ();
					JniEnvironment.Runtime.ValueManager.WaitForGCBridgeProcessing ();
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

		[TestCase (false, false)]
		[TestCase (true, false)]
		[TestCase (false, true)]
		[TestCase (true, true)]
		public unsafe void DisposeUnlessReferenced_PreservesRegisteredPeerAndDisposesUnregisteredPeer (bool exception, bool unregister)
		{
			using var type = new JniType (exception ? "java/lang/Throwable" : "java/lang/Object");
			var local = type.NewObject (type.GetConstructor ("()V"), null);
			var observation = new FinalizationObservation ();
			var peer = CreatePeer (exception, ref local, JniObjectReferenceOptions.CopyAndDispose, observation);
			try {
				var reference = peer.PeerReference;
				if (unregister)
					peer.UnregisterFromRuntime ();
				peer.DisposeUnlessReferenced ();

				Assert.AreEqual (!unregister, peer.PeerReference.IsValid);
				Assert.AreEqual (unregister ? 1 : 0, observation.DisposedCount);
				Assert.AreEqual (0, observation.FinalizedCount);
				Assert.AreEqual (unregister, peer.JniObjectReferenceControlBlock == IntPtr.Zero);
				if (unregister) {
					Assert.IsTrue (observation.ReferenceWasValid, "Dispose(true) must enter with a valid reference.");
					peer.DisposeUnlessReferenced ();
					peer.Dispose ();
					Assert.AreEqual (1, observation.DisposedCount, "Disposal must be idempotent.");
				} else {
					Assert.AreSame (peer, JniEnvironment.Runtime.ValueManager.PeekPeer (reference));
				}
			} finally {
				Cleanup (peer);
				JniObjectReference.Dispose (ref local);
			}
		}

		[TestCase (false)]
		[TestCase (true)]
		public unsafe void DisposeUnlessReferenced_DisposesAliasButPreservesRegisteredPeer (bool exception)
		{
			using var type = new JniType (exception ? "java/lang/Throwable" : "java/lang/Object");
			var local = type.NewObject (type.GetConstructor ("()V"), null);
			var registered = CreatePeer (exception, ref local, JniObjectReferenceOptions.Copy, new FinalizationObservation ());
			var observation = new FinalizationObservation ();
			var alias = CreatePeer (exception, ref local, JniObjectReferenceOptions.Copy, observation);
			try {
				Assert.AreSame (registered, JniEnvironment.Runtime.ValueManager.PeekPeer (local));

				alias.DisposeUnlessReferenced ();

				Assert.AreEqual (1, observation.DisposedCount);
				Assert.IsTrue (observation.ReferenceWasValid);
				Assert.IsFalse (alias.PeerReference.IsValid);
				Assert.AreEqual (IntPtr.Zero, alias.JniObjectReferenceControlBlock);
				Assert.IsTrue (registered.PeerReference.IsValid);
				Assert.AreSame (registered, JniEnvironment.Runtime.ValueManager.PeekPeer (local));
			} finally {
				Cleanup (alias);
				Cleanup (registered);
				JniObjectReference.Dispose (ref local);
			}
		}

		[TestCase (false)]
		[TestCase (true)]
		public unsafe void DisposeUnlessReferenced_ThrowingCallbackPreservesReferenceForRetry (bool exception)
		{
			using var type = new JniType (exception ? "java/lang/Throwable" : "java/lang/Object");
			var local = type.NewObject (type.GetConstructor ("()V"), null);
			var observation = new FinalizationObservation { ThrowOnDispose = true };
			var peer = CreatePeer (exception, ref local, JniObjectReferenceOptions.CopyAndDispose, observation);
			try {
				peer.UnregisterFromRuntime ();
				Assert.Throws<InvalidOperationException> (() => peer.DisposeUnlessReferenced ());
				Assert.IsTrue (observation.ReferenceWasValid);
				Assert.IsTrue (peer.PeerReference.IsValid);
				Assert.AreNotEqual (IntPtr.Zero, peer.JniObjectReferenceControlBlock);

				observation.ThrowOnDispose = false;
				peer.DisposeUnlessReferenced ();
				Assert.AreEqual (2, observation.DisposedCount);
				Assert.IsFalse (peer.PeerReference.IsValid);
				Assert.AreEqual (IntPtr.Zero, peer.JniObjectReferenceControlBlock);
			} finally {
				observation.ThrowOnDispose = false;
				Cleanup (peer);
				JniObjectReference.Dispose (ref local);
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
				peer.Dispose ();
				Assert.IsFalse (peer.PeerReference.IsValid);
				Assert.IsTrue (observation.ReferenceWasValid, "Dispose(true) must still enter with a valid reference.");
				Assert.AreEqual (1, observation.DisposedCount);
				Assert.AreEqual (0, observation.FinalizedCount);
				Assert.AreEqual (IntPtr.Zero, peer.JniObjectReferenceControlBlock);
				peer.Dispose ();
				Assert.AreEqual (1, observation.DisposedCount);
			} finally {
				Cleanup (peer);
				JniObjectReference.Dispose (ref local);
			}
		}

		[TestCase (false, false)]
		[TestCase (true, false)]
		[TestCase (false, true)]
		[TestCase (true, true)]
		[Category ("JniReferenceLeak")]
		public unsafe void DisposePeer_ReleasesGlobalReference (bool exception, bool unlessReferenced)
		{
			using var type = new JniType (exception ? "java/lang/Throwable" : "java/lang/Object");
			var constructor = type.GetConstructor ("()V");

			GC.Collect ();
			GC.WaitForPendingFinalizers ();
			JniEnvironment.Runtime.ValueManager.WaitForGCBridgeProcessing ();

			for (int i = 0; i < 10; i++)
				ReleasePeer (checkReferenceCount: false);
			ReleasePeer (checkReferenceCount: true);

			void ReleasePeer (bool checkReferenceCount)
			{
				var local = type.NewObject (constructor, null);
				var observation = new FinalizationObservation ();
				var peer = CreatePeer (exception, ref local, JniObjectReferenceOptions.CopyAndDispose, observation);
				try {
					if (unlessReferenced)
						peer.UnregisterFromRuntime ();
					int globals = JniEnvironment.Runtime.GlobalReferenceCount;
					if (unlessReferenced)
						peer.DisposeUnlessReferenced ();
					else
						peer.Dispose ();
					if (checkReferenceCount)
						Assert.AreEqual (globals - 1, JniEnvironment.Runtime.GlobalReferenceCount,
							"JNI global-reference deletion is independent of native control-block release.");
					Assert.IsFalse (peer.PeerReference.IsValid);
					Assert.AreEqual (IntPtr.Zero, peer.JniObjectReferenceControlBlock);
				} finally {
					Cleanup (peer);
					JniObjectReference.Dispose (ref local);
				}
			}
		}

		[MethodImpl (MethodImplOptions.NoInlining)]
		static unsafe void CreateCollectiblePeer (bool exception, bool invalid, bool throwingConstructor, FinalizationObservation observation)
		{
			using var type = new JniType (exception ? "java/lang/Throwable" : "java/lang/Object");
			var local = invalid ? default : type.NewObject (type.GetConstructor ("()V"), null);
			try {
				var options = invalid ? JniObjectReferenceOptions.None : JniObjectReferenceOptions.Copy;
				if (throwingConstructor)
					Assert.Throws<InvalidOperationException> (() => CreatePeer (exception, ref local, options, observation, throwingConstructor: true));
				else
					CreatePeer (exception, ref local, options, observation);
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
			public IntPtr ControlBlockDuringCallback;
			public int FinalizedCount;
			public int DisposedCount;
			public bool ReferenceWasValid;
			public bool ThrowOnFinalize;
			public bool ThrowOnDispose;
			public JniObjectReference Reattach;

			public void Observe (IJavaPeerable peer, bool disposing)
			{
				ReferenceWasValid = peer.PeerReference.IsValid;
				if (disposing) {
					DisposedCount++;
					if (ThrowOnDispose)
						throw new InvalidOperationException ("Disposal callback failed.");
					return;
				}
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
