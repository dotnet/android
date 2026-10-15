using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Java;
using System.Threading;
using Android.Runtime;
using Java.Interop;

namespace Microsoft.Android.Runtime;

// Originally from: https://github.com/dotnet/java-interop/blob/9b1d8781e8e322849d05efac32119c913b21c192/src/Java.Runtime.Environment/Java.Interop/ManagedValueManager.cs
/// <summary>
/// Tracks the JavaMarshal registered peers and integrates them with the CLR's GC bridge.
/// </summary>
static class JavaMarshalRegisteredPeers
{
	static readonly Dictionary<int, RegisteredPeerBucket> RegisteredInstances = new ();
	static readonly ConcurrentQueue<IntPtr> CollectedContexts = new ();
	static readonly Lock s_instancesLock = new ();

	internal static void QueueCollectedContext (IntPtr context)
	{
		CollectedContexts.Enqueue (context);
	}

	public static void CollectPeers ()
	{
		unsafe {
			while (CollectedContexts.TryDequeue (out IntPtr contextPtr)) {
				Debug.Assert (contextPtr != IntPtr.Zero, "CollectedContexts should not contain null pointers.");
				HandleContext* context = (HandleContext*)contextPtr;

				lock (s_instancesLock) {
					Remove (context);
				}

				HandleContext.Free (ref context);
			}

			void Remove (HandleContext* context)
			{
				int key = context->PeerIdentityHashCode;
				if (!RegisteredInstances.TryGetValue (key, out RegisteredPeerBucket peers))
					return;

				for (int i = peers.Count - 1; i >= 0; i--) {
					if (peers [i].BelongsToContext (context)) {
						peers.RemoveAt (i);
					}
				}

				if (peers.Count == 0) {
					RegisteredInstances.Remove (key);
				} else {
					RegisteredInstances [key] = peers;
				}
			}
		}
	}

	public static void AddPeer (IJavaPeerable value)
	{
		// Remove any collected contexts before adding a new peer.
		CollectPeers ();

		var r = value.PeerReference;
		if (!r.IsValid)
			throw new ObjectDisposedException (value.GetType ().FullName);

		if (r.Type != JniObjectReferenceType.Global) {
			value.SetPeerReference (r.NewGlobalRef ());
			JniObjectReference.Dispose (ref r, JniObjectReferenceOptions.CopyAndDispose);
		}
		int key = value.JniIdentityHashCode;
		lock (s_instancesLock) {
			if (!RegisteredInstances.TryGetValue (key, out RegisteredPeerBucket peers)) {
				RegisteredInstances.Add (key, new RegisteredPeerBucket (new ReferenceTrackingHandle (value)));
				return;
			}

			for (int i = peers.Count - 1; i >= 0; i--) {
				var result = ReconcilePeer (peers [i], value, key, out ReferenceTrackingHandle replacement);
				if (result == PeerReconciliationResult.Replace) {
					peers [i] = replacement;
					RegisteredInstances [key] = peers;
					return;
				}
				if (result == PeerReconciliationResult.Keep)
					return;
			}

			peers.Add (new ReferenceTrackingHandle (value));
			RegisteredInstances [key] = peers;
		}
	}

	enum PeerReconciliationResult
	{
		NoMatch,
		Keep,
		Replace,
	}

	static PeerReconciliationResult ReconcilePeer (
			ReferenceTrackingHandle registered,
			IJavaPeerable value,
			int key,
			out ReferenceTrackingHandle replacement)
	{
		replacement = default;
		if (registered.Target is not IJavaPeerable target)
			return PeerReconciliationResult.NoMatch;
		if (!JniEnvironment.Types.IsSameObject (target.PeerReference, value.PeerReference))
			return PeerReconciliationResult.NoMatch;

		// JNIEnv.NewObject/JNIEnv.CreateInstance() compatibility.
		// When two MCW's are created for one Java instance [0],
		// we want the 2nd MCW to replace the 1st, as the 2nd is
		// the one the dev created; the 1st is an implicit intermediary.
		//
		// Meanwhile, a new "replaceable" instance should *not* replace an
		// existing "replaceable" instance; see dotnet/android#9862.
		//
		// [0]: If Java ctor invokes overridden virtual method, we'll
		// transition into managed code w/o a registered instance, and
		// thus will create an "intermediary" via
		// (IntPtr, JniHandleOwnership) .ctor.
		if (target.JniManagedPeerState.HasFlag (JniManagedPeerStates.Replaceable) &&
				!value.JniManagedPeerState.HasFlag (JniManagedPeerStates.Replaceable)) {
			registered.Dispose ();
			replacement = new ReferenceTrackingHandle (value);
			GC.KeepAlive (target);
			return PeerReconciliationResult.Replace;
		}

		if (JniEnvironment.Runtime.ObjectReferenceManager.LogGlobalReferenceMessages)
			WarnNotReplacing (key, value, target);
		GC.KeepAlive (target);
		return PeerReconciliationResult.Keep;
	}

	static void WarnNotReplacing (int key, IJavaPeerable ignoreValue, IJavaPeerable keepValue)
	{
		JniEnvironment.Runtime.ObjectReferenceManager.WriteGlobalReferenceLine (
				"Warning: Not registering PeerReference={0} IdentityHashCode=0x{1} Instance={2} Instance.Type={3} Java.Type={4}; " +
				"keeping previously registered PeerReference={5} Instance={6} Instance.Type={7} Java.Type={8}.",
				ignoreValue.PeerReference.ToString (),
				key.ToString ("x", CultureInfo.InvariantCulture),
				RuntimeHelpers.GetHashCode (ignoreValue).ToString ("x", CultureInfo.InvariantCulture),
				ignoreValue.GetType ().FullName,
				JniEnvironment.Types.GetJniTypeNameFromInstance (ignoreValue.PeerReference),
				keepValue.PeerReference.ToString (),
				RuntimeHelpers.GetHashCode (keepValue).ToString ("x", CultureInfo.InvariantCulture),
				keepValue.GetType ().FullName,
				JniEnvironment.Types.GetJniTypeNameFromInstance (keepValue.PeerReference));
	}

	public static IJavaPeerable? PeekPeer (JniObjectReference reference)
	{
		if (!reference.IsValid)
			return null;

		int key = JniEnvironment.References.GetIdentityHashCode (reference);

		lock (s_instancesLock) {
			if (!RegisteredInstances.TryGetValue (key, out RegisteredPeerBucket peers))
				return null;

			for (int i = peers.Count - 1; i >= 0; i--) {
				if (peers [i].Target is IJavaPeerable peer
					&& JniEnvironment.Types.IsSameObject (reference, peer.PeerReference))
				{
					return peer;
				}
			}

			if (peers.Count == 0)
				RegisteredInstances.Remove (key);
		}
		return null;
	}

	public static void RemovePeer (IJavaPeerable value)
	{
		// Remove any collected contexts before modifying RegisteredInstances
		CollectPeers ();

		if (value == null)
			throw new ArgumentNullException (nameof (value));

		lock (s_instancesLock) {
			int key = value.JniIdentityHashCode;
			if (!RegisteredInstances.TryGetValue (key, out RegisteredPeerBucket peers))
				return;

			for (int i = peers.Count - 1; i >= 0; i--) {
				ReferenceTrackingHandle peer = peers [i];
				IJavaPeerable? target = peer.Target;
				if (ReferenceEquals (value, target)) {
					peers.RemoveAt (i);
					peer.Dispose ();
				}
				GC.KeepAlive (target);
			}
			if (peers.Count == 0)
				RegisteredInstances.Remove (key);
			else
				RegisteredInstances [key] = peers;
		}
	}

	public static void FinalizePeer (IJavaPeerable value)
	{
		var h = value.PeerReference;
		var o = JniEnvironment.Runtime.ObjectReferenceManager;
		// MUST NOT use SafeHandle.ReferenceType: local refs are tied to a JniEnvironment
		// and the JniEnvironment's corresponding thread; it's a thread-local value.
		// Accessing SafeHandle.ReferenceType won't kill anything (so far...), but
		// instead it always returns JniReferenceType.Invalid.
		if (!h.IsValid || h.Type == JniObjectReferenceType.Local) {
			if (o.LogGlobalReferenceMessages) {
				o.WriteGlobalReferenceLine ("Finalizing PeerReference={0} IdentityHashCode=0x{1} Instance=0x{2} Instance.Type={3}",
						h.ToString (),
						value.JniIdentityHashCode.ToString ("x", CultureInfo.InvariantCulture),
						RuntimeHelpers.GetHashCode (value).ToString ("x", CultureInfo.InvariantCulture),
						value.GetType ().ToString ());
			}
			RemovePeer (value);
			value.SetPeerReference (new JniObjectReference ());
			value.Finalized ();
			return;
		}

		RemovePeer (value);
		if (o.LogGlobalReferenceMessages) {
			o.WriteGlobalReferenceLine ("Finalizing PeerReference={0} IdentityHashCode=0x{1} Instance=0x{2} Instance.Type={3}",
					h.ToString (),
					value.JniIdentityHashCode.ToString ("x", CultureInfo.InvariantCulture),
					RuntimeHelpers.GetHashCode (value).ToString ("x", CultureInfo.InvariantCulture),
					value.GetType ().ToString ());
		}
		value.SetPeerReference (new JniObjectReference ());
		JniObjectReference.Dispose (ref h);
		value.Finalized ();
	}

	public static List<JniSurfacedPeerInfo> GetSurfacedPeers ()
	{
		// Remove any collected contexts before iterating over all the registered instances
		CollectPeers ();

		lock (s_instancesLock) {
			var peers = new List<JniSurfacedPeerInfo> (RegisteredInstances.Count);
			foreach (var (identityHashCode, registered) in RegisteredInstances) {
				for (int i = 0; i < registered.Count; i++) {
					var peer = registered [i];
					if (peer.Target is IJavaPeerable target) {
						peers.Add (new JniSurfacedPeerInfo (identityHashCode, new WeakReference<IJavaPeerable> (target)));
					}
				}
			}
			return peers;
		}
	}

	struct RegisteredPeerBucket
	{
		ReferenceTrackingHandle _first;
		List<ReferenceTrackingHandle>? _rest;

		public RegisteredPeerBucket (ReferenceTrackingHandle first)
		{
			_first = first;
			_rest = null;
		}

		public int Count => (_first.IsValid ? 1 : 0) + (_rest?.Count ?? 0);

		public ReferenceTrackingHandle this [int index] {
			get {
				if (index == 0 && _first.IsValid)
					return _first;

				var rest = _rest;
				if (rest == null)
					throw new ArgumentOutOfRangeException (nameof (index));
				return rest [index - 1];
			}
			set {
				if (index == 0 && _first.IsValid) {
					_first = value;
					return;
				}

				var rest = _rest;
				if (rest == null)
					throw new ArgumentOutOfRangeException (nameof (index));
				rest [index - 1] = value;
			}
		}

		public void Add (ReferenceTrackingHandle peer)
		{
			if (!_first.IsValid) {
				_first = peer;
				return;
			}

			_rest ??= [];
			_rest.Add (peer);
		}

		public void RemoveAt (int index)
		{
			if (index == 0) {
				_first = default;
				if (_rest?.Count > 0) {
					_first = _rest [0];
					_rest.RemoveAt (0);
				}
			} else {
				_rest?.RemoveAt (index - 1);
			}

			if (_rest?.Count == 0)
				_rest = null;
		}
	}

	struct ReferenceTrackingHandle : IDisposable
	{
		WeakReference<IJavaPeerable?> _weakReference;
		HandleContext* _context;

		public bool BelongsToContext (HandleContext* context)
		{
			unsafe {
				// SAFETY: this only compares stored context addresses; no context memory is accessed.
				return _context == context;
			}
		}

		public ReferenceTrackingHandle (IJavaPeerable peer)
		{
			unsafe {
				// SAFETY: allocation captures the peer's live control block and owns the context until Dispose/bridge collection.
				_context = HandleContext.Alloc (peer);
			}
			_weakReference = new (peer);
		}

		public IJavaPeerable? Target
			=> _weakReference.TryGetTarget (out var target) ? target : null;

		public bool IsValid {
			get {
				unsafe {
					// SAFETY: this only tests the stored context address; no context memory is accessed.
					return _context != null;
				}
			}
		}

		public void Dispose ()
		{
			unsafe {
				// SAFETY: this only tests the stored context address; no context memory is accessed.
				if (_context == null)
					return;
			}

			IJavaPeerable? target = Target;

			GCHandle handle;
			unsafe {
				// SAFETY: this owner retains the registered context until the matching release below.
				handle = HandleContext.GetAssociatedGCHandle (_context);
			}
			unsafe {
				// SAFETY: this registered handle owns the context; peer/GCHandle retention prevents premature control-block collection.
				HandleContext.Free (ref _context);
			}
			_weakReference.SetTarget (null);
			if (handle.IsAllocated) {
				unsafe {
					// SAFETY: the associated registered context has been removed and this owner releases its GCHandle exactly once.
					handle.Free ();
				}
			}

			// Make sure the target is not collected before we finish disposing
			GC.KeepAlive (target);
		}
	}
}
