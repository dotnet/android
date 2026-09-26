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
/// <remarks>
/// <para>
/// This is a process-wide, static type. <see cref="InitializeIfNeeded"/> performs a
/// process-global, one-shot GC-bridge initialization (<c>clr_initialize_gc_bridge</c>),
/// which starts a native bridge-processing thread and aborts the process if it runs more
/// than once. <see cref="InitializeIfNeeded"/> is idempotent: the first call performs the
/// initialization and any subsequent call returns immediately. The call must occur after the
/// <c>JniRuntime</c> has been created and published, because bridge processing uses its JNI
/// reference manager.
/// </para>
/// <para>
/// The GC-bridge registration lives for the entire lifetime of the process and is never torn
/// down: stopping the detached bridge-processing thread is not supported by the runtime.
/// </para>
/// </remarks>
static class JavaMarshalRegisteredPeers
{
	static readonly Dictionary<int, RegisteredPeerBucket> RegisteredInstances = new ();
	static readonly ConcurrentQueue<IntPtr> CollectedContexts = new ();
	static readonly Lock s_instancesLock = new ();

	static readonly object initializeLock = new ();
	static bool initialized;

	/// <summary>
	/// Performs the one-shot, process-global GC-bridge initialization the first time it is
	/// called; subsequent calls return immediately. See <see cref="JavaMarshalRegisteredPeers"/>
	/// for details on the process-lifetime semantics.
	/// </summary>
	internal static void InitializeIfNeeded ()
	{
		lock (initializeLock) {
			if (initialized)
				return;

			if (RuntimeFeature.EventSourceSupport) {
				RuntimeEventSource.Initialize ();
			}

			unsafe {
				var mark_cross_references_ftn = JavaMarshalGCBridge.Initialize ();
				JavaMarshal.Initialize (mark_cross_references_ftn);
			}

			initialized = true;
		}
	}

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

	unsafe struct ReferenceTrackingHandle : IDisposable
	{
		WeakReference<IJavaPeerable?> _weakReference;
		HandleContext* _context;

		public bool BelongsToContext (HandleContext* context)
			=> _context == context;

		public ReferenceTrackingHandle (IJavaPeerable peer)
		{
			_context = HandleContext.Alloc (peer);
			_weakReference = new (peer);
		}

		public IJavaPeerable? Target
			=> _weakReference.TryGetTarget (out var target) ? target : null;

		public bool IsValid => _context != null;

		public void Dispose ()
		{
			if (_context == null)
				return;

			IJavaPeerable? target = Target;

			GCHandle handle = HandleContext.GetAssociatedGCHandle (_context);
			HandleContext.Free (ref _context);
			_weakReference.SetTarget (null);
			if (handle.IsAllocated) {
				handle.Free ();
			}

			// Make sure the target is not collected before we finish disposing
			GC.KeepAlive (target);
		}
	}
}
