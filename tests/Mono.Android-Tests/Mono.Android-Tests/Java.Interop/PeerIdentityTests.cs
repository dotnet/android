#nullable enable

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

using Android.Runtime;

using Java.Interop;

using NUnit.Framework;

namespace Java.InteropTests
{
	[TestFixture]
	[NonParallelizable]
	[Category ("JNIObjectArray")]
	public class PeerIdentityTests
	{
		[Test]
		[Category ("PeerIdentityRepro")]
		public void GetObject_ReentrantActivation_PreservesRoundtripIdentity ()
		{
			var manager = JniRuntime.CurrentRuntime.ValueManager;
			var handle = JNIEnv.CreateInstance (ReentrantLookupPeer.JniName, "()V");
			ReentrantLookupPeer? cached = null;
			ReentrantLookupPeer? reentrant = null;
			var createdPeers = new List<ReentrantLookupPeer> ();
			try {
				Assert.IsNull (manager.PeekPeer (new JniObjectReference (handle)));
				ReentrantLookupPeer.PeerCreated = createdPeers.Add;
				ReentrantLookupPeer.BeforeRegistration = value => {
					reentrant = Java.Lang.Object.GetObject<ReentrantLookupPeer> (value, JniHandleOwnership.DoNotTransfer);
				};

				cached = Java.Lang.Object.GetObject<ReentrantLookupPeer> (handle, JniHandleOwnership.DoNotTransfer)
					?? throw new InvalidOperationException ("Could not create the cached peer.");
				var registered = manager.PeekPeer (new JniObjectReference (handle))
					?? throw new InvalidOperationException ("No peer was registered.");
				Assert.AreSame (reentrant, registered, "The reentrant lookup should have registered first.");
				Assert.IsTrue (JniEnvironment.Types.IsSameObject (cached.PeerReference, registered.PeerReference),
					"The wrappers should refer to the same Java object.");

				using var array = new Java.Lang.Object (
					JNIEnv.NewArray (new Java.Lang.Object [] { cached }, typeof (Java.Lang.Object)),
					JniHandleOwnership.TransferLocalRef);
				var values = JNIEnv.GetObjectArray (array.Handle, new [] { typeof (ReentrantLookupPeer) })
					?? throw new InvalidOperationException ("Could not read the Java object array.");
				Assert.AreSame (registered, values [0], "Array marshaling should find the registered peer.");
				Assert.AreSame (cached, values [0],
					$"Lookup returned managed-{RuntimeHelpers.GetHashCode (cached):x} ({cached.JniManagedPeerState}), " +
					$"but the registry retained managed-{RuntimeHelpers.GetHashCode (registered):x} ({registered.JniManagedPeerState}); " +
					$"value manager: {manager.GetType ().FullName}.");
				Assert.AreEqual (2, createdPeers.Count, "Both lookups should construct a peer.");
				Assert.AreNotSame (createdPeers [0], createdPeers [1]);
				foreach (var peer in createdPeers) {
					Assert.AreEqual (ReferenceEquals (peer, registered), peer.PeerReference.IsValid,
						"Only the returned registered peer should retain a JNI reference.");
				}
			} finally {
				ReentrantLookupPeer.BeforeRegistration = null;
				ReentrantLookupPeer.PeerCreated = null;
				foreach (var peer in createdPeers)
					peer.Dispose ();
				cached?.Dispose ();
				if (!ReferenceEquals (cached, reentrant))
					reentrant?.Dispose ();
				JNIEnv.DeleteLocalRef (handle);
			}
		}

		[Test]
		public void GetObject_NewPeer_RemainsRegisteredAndUsable ()
		{
			var handle = JNIEnv.CreateInstance (ReentrantLookupPeer.JniName, "()V");
			try {
				using var peer = Java.Lang.Object.GetObject<ReentrantLookupPeer> (handle, JniHandleOwnership.DoNotTransfer)
					?? throw new InvalidOperationException ("Could not create the peer.");
				Assert.IsTrue (peer.PeerReference.IsValid);
				Assert.AreSame (peer, JniRuntime.CurrentRuntime.ValueManager.PeekPeer (new JniObjectReference (handle)));
				Assert.AreSame (peer, Java.Lang.Object.GetObject<ReentrantLookupPeer> (handle, JniHandleOwnership.DoNotTransfer));
				Assert.IsTrue (JniEnvironment.Types.IsSameObject (new JniObjectReference (handle), peer.PeerReference));
			} finally {
				JNIEnv.DeleteLocalRef (handle);
			}
		}

		[Test]
		public void GetObject_IncompatibleRegisteredPeer_PreservesRequestedType ()
		{
			var handle = JNIEnv.CreateInstance (ReentrantLookupPeer.JniName, "()V");
			try {
				using var registered = new Java.Lang.Object (handle, JniHandleOwnership.DoNotTransfer);
				using var typed = Java.Lang.Object.GetObject<ReentrantLookupPeer> (handle, JniHandleOwnership.DoNotTransfer)
					?? throw new InvalidOperationException ("Could not create the typed peer.");
				Assert.AreNotSame (registered, typed);
				Assert.IsTrue (typed.PeerReference.IsValid, "A returned typed alias must remain usable.");
				Assert.IsTrue (JniEnvironment.Types.IsSameObject (registered.PeerReference, typed.PeerReference));
				Assert.AreSame (registered, JniRuntime.CurrentRuntime.ValueManager.PeekPeer (new JniObjectReference (handle)),
					"Requesting another managed type should not replace the explicitly registered peer.");
			} finally {
				JNIEnv.DeleteLocalRef (handle);
			}
		}
	}

	[Register (JniName, DoNotGenerateAcw = true)]
	public sealed class ReentrantLookupPeer : Java.Lang.Object
	{
		public const string JniName = "net/dot/android/test/ReentrantLookupPeer";
		public static Action<IntPtr>? BeforeRegistration;
		public static Action<ReentrantLookupPeer>? PeerCreated;

		public ReentrantLookupPeer (IntPtr handle, JniHandleOwnership transfer)
			: base (BeforeConstruct (handle), transfer)
		{
			PeerCreated?.Invoke (this);
		}

		static IntPtr BeforeConstruct (IntPtr handle)
		{
			// Complete another lookup after the outer registry miss but before its registration.
			var callback = BeforeRegistration;
			BeforeRegistration = null;
			callback?.Invoke (handle);
			return handle;
		}
	}
}
