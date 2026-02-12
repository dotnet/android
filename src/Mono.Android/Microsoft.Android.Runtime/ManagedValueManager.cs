// Originally from: https://github.com/dotnet/java-interop/blob/9b1d8781e8e322849d05efac32119c913b21c192/src/Java.Runtime.Environment/Java.Interop/ManagedValueManager.cs
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Java;
using System.Threading;
using System.Threading.Tasks;
using Android.Runtime;
using Java.Interop;

namespace Microsoft.Android.Runtime;

class ManagedValueManager : JniRuntime.JniValueManager
{
	const DynamicallyAccessedMemberTypes Constructors = DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors;

	readonly Lock _registeredInstancesLock = new ();
	readonly Dictionary<int, List<ReferenceTrackingHandle>> _registeredInstances = new ();
	readonly ConcurrentQueue<IntPtr> _collectedContexts = new ();

	bool _disposed;

	static Lazy<ManagedValueManager> s_instance = new (() => new ManagedValueManager ());
	public static ManagedValueManager GetOrCreateInstance () => s_instance.Value;

	unsafe ManagedValueManager ()
	{
		// There can only be one instance of ManagedValueManager because we can call JavaMarshal.Initialize only once.
		var mark_cross_references_ftn = RuntimeNativeMethods.clr_initialize_gc_bridge (&BridgeProcessingStarted, &BridgeProcessingFinished);
		JavaMarshal.Initialize (mark_cross_references_ftn);
	}

	protected override void Dispose (bool disposing)
	{
		_disposed = true;
		base.Dispose (disposing);
	}

	void ThrowIfDisposed ()
	{
		if (_disposed)
			throw new ObjectDisposedException (nameof (ManagedValueManager));
	}

	public override void WaitForGCBridgeProcessing ()
	{
		// Drain any pending contexts that were enqueued during bridge processing.
		// This ensures synchronous cleanup when explicitly requested (e.g., in tests).
		CollectPeers ();
	}

	public unsafe override void CollectPeers ()
	{
		ThrowIfDisposed ();

		while (_collectedContexts.TryDequeue (out IntPtr contextPtr)) {
			Debug.Assert (contextPtr != IntPtr.Zero, "_collectedContexts should not contain null pointers.");
			HandleContext* context = (HandleContext*)contextPtr;
			
			lock (_registeredInstancesLock) {
				Remove (context);
			}

			HandleContext.Free (ref context);
		}

		void Remove (HandleContext* context)
		{
			int key = context->PeerIdentityHashCode;
			if (!_registeredInstances.TryGetValue (key, out List<ReferenceTrackingHandle>? peers))
				return;

			for (int i = peers.Count - 1; i >= 0; i--) {
				var peer = peers [i];
				if (peer.BelongsToContext (context)) {
					peers.RemoveAt (i);
				}
			}

			if (peers.Count == 0) {
				_registeredInstances.Remove (key);
			}
		}
	}

	public override void AddPeer (IJavaPeerable value)
	{
		ThrowIfDisposed ();

		var r = value.PeerReference;
		if (!r.IsValid)
			throw new ObjectDisposedException (value.GetType ().FullName);

		if (r.Type != JniObjectReferenceType.Global) {
			value.SetPeerReference (r.NewGlobalRef ());
			JniObjectReference.Dispose (ref r, JniObjectReferenceOptions.CopyAndDispose);
		}
		int key = value.JniIdentityHashCode;
		lock (_registeredInstancesLock) {
			List<ReferenceTrackingHandle>? peers;
			if (!_registeredInstances.TryGetValue (key, out peers)) {
				peers = [new ReferenceTrackingHandle (value)];
				_registeredInstances.Add (key, peers);
				return;
			}

			for (int i = peers.Count - 1; i >= 0; i--) {
				ReferenceTrackingHandle peer = peers [i];
				if (peer.Target is not IJavaPeerable target)
					continue;
				if (!JniEnvironment.Types.IsSameObject (target.PeerReference, value.PeerReference))
					continue;
				if (target.JniManagedPeerState.HasFlag (JniManagedPeerStates.Replaceable)) {
					peer.Dispose ();
					peers [i] = new ReferenceTrackingHandle (value);
				} else {
					WarnNotReplacing (key, value, target);
				}
				GC.KeepAlive (target);
				return;
			}

			peers.Add (new ReferenceTrackingHandle (value));
		}
	}

	void WarnNotReplacing (int key, IJavaPeerable ignoreValue, IJavaPeerable keepValue)
	{
		Runtime.ObjectReferenceManager.WriteGlobalReferenceLine (
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

	public override IJavaPeerable? PeekPeer (JniObjectReference reference)
	{
		ThrowIfDisposed ();

		if (!reference.IsValid)
			return null;

		int key = GetJniIdentityHashCode (reference);

		lock (_registeredInstancesLock) {
			if (!_registeredInstances.TryGetValue (key, out List<ReferenceTrackingHandle>? peers))
				return null;

			for (int i = peers.Count - 1; i >= 0; i--) {
				if (peers [i].Target is IJavaPeerable peer
					&& JniEnvironment.Types.IsSameObject (reference, peer.PeerReference))
				{
					return peer;
				}
			}

			if (peers.Count == 0)
				_registeredInstances.Remove (key);
		}
		return null;
	}

	public override void RemovePeer (IJavaPeerable value)
	{
		ThrowIfDisposed ();

		// Remove any collected contexts before modifying _registeredInstances
		CollectPeers ();

		if (value == null)
			throw new ArgumentNullException (nameof (value));

		lock (_registeredInstancesLock) {
			int key = value.JniIdentityHashCode;
			if (!_registeredInstances.TryGetValue (key, out List<ReferenceTrackingHandle>? peers))
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
				_registeredInstances.Remove (key);
		}
	}

	public override void FinalizePeer (IJavaPeerable value)
	{
		var h = value.PeerReference;
		var o = Runtime.ObjectReferenceManager;
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

	public override void ActivatePeer (IJavaPeerable? self, JniObjectReference reference, ConstructorInfo cinfo, object?[]? argumentValues)
	{
		try {
			ActivateViaReflection (reference, cinfo, argumentValues);
		} catch (Exception e) {
			var m = string.Format (
					CultureInfo.InvariantCulture,
					"Could not activate {{ PeerReference={0} IdentityHashCode=0x{1} Java.Type={2} }} for managed type '{3}'.",
					reference,
					GetJniIdentityHashCode (reference).ToString ("x", CultureInfo.InvariantCulture),
					JniEnvironment.Types.GetJniTypeNameFromInstance (reference),
					cinfo.DeclaringType?.FullName);
			Debug.WriteLine (m);

			throw new NotSupportedException (m, e);
		}
	}

	void ActivateViaReflection (JniObjectReference reference, ConstructorInfo cinfo, object?[]? argumentValues)
	{
		var declType  = GetDeclaringType (cinfo);

#pragma warning disable IL2072
		var self      = (IJavaPeerable) System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject (declType);
#pragma warning restore IL2072
		self.SetPeerReference (reference);

		cinfo.Invoke (self, argumentValues);

		[UnconditionalSuppressMessage ("Trimming", "IL2073", Justification = "🤷‍♂️")]
		[return: DynamicallyAccessedMembers (Constructors)]
		Type GetDeclaringType (ConstructorInfo cinfo) =>
			cinfo.DeclaringType ?? throw new NotSupportedException ("Do not know the type to create!");
	}

	public override List<JniSurfacedPeerInfo> GetSurfacedPeers ()
	{
		ThrowIfDisposed ();

		// Remove any collected contexts before iterating over all the registered instances
		CollectPeers ();

		lock (_registeredInstancesLock) {
			var peers = new List<JniSurfacedPeerInfo> (_registeredInstances.Count);
			foreach (var (identityHashCode, referenceTrackingHandles) in _registeredInstances) {
				foreach (var peer in referenceTrackingHandles) {
					if (peer.Target is IJavaPeerable target) {
						peers.Add (new JniSurfacedPeerInfo (identityHashCode, new WeakReference<IJavaPeerable> (target)));
					}
				}
			}
			return peers;
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

		public void Dispose ()
		{
			if (_context == null)
				return;

			IJavaPeerable? target = Target;

			GCHandle handle = _context->AssociatedGCHandle;
			HandleContext.Free (ref _context);
			_weakReference.SetTarget (null);
			if (handle.IsAllocated) {
				handle.Free ();
			}

			// Make sure the target is not collected before we finish disposing
			GC.KeepAlive (target);
		}
	}

	[StructLayout (LayoutKind.Sequential)]
	unsafe struct HandleContext
	{
		static readonly nuint Size = (nuint)Marshal.SizeOf<HandleContext> ();

		int _identityHashCode;
		IntPtr _controlBlock;
		IntPtr _gcHandle; // GCHandle stored as IntPtr to keep blittable layout

		public int PeerIdentityHashCode => _identityHashCode;
		public bool IsCollected
		{
			get
			{
				if (_controlBlock == IntPtr.Zero)
					throw new InvalidOperationException ("HandleContext control block is not initialized.");

				return ((JniObjectReferenceControlBlock*) _controlBlock)->handle == IntPtr.Zero;
			}
		}

		// This is an internal mirror of the Java.Interop.JniObjectReferenceControlBlock
		private struct JniObjectReferenceControlBlock
		{
			public IntPtr handle;
			public int handle_type;
			public IntPtr weak_handle;
			public int refs_added;
		}

		public GCHandle AssociatedGCHandle => GCHandle.FromIntPtr (_gcHandle);

#if DEBUG
		static readonly HashSet<IntPtr> s_knownContexts = new ();

		public static unsafe void EnsureAllContextsAreOurs (MarkCrossReferencesArgs* mcr)
		{
			lock (s_knownContexts) {
				for (nuint i = 0; i < mcr->ComponentCount; i++) {
					StronglyConnectedComponent component = mcr->Components [i];
					for (nuint j = 0; j < component.Count; j++) {
						if (!s_knownContexts.Contains ((IntPtr)component.Contexts [j])) {
							throw new InvalidOperationException ("Unknown reference tracking handle.");
						}
					}
				}
			}
		}
#endif

		public static HandleContext* Alloc (IJavaPeerable peer)
		{
			var context = (HandleContext*) NativeMemory.AllocZeroed (1, Size);
			if (context == null) {
				throw new OutOfMemoryException ("Failed to allocate memory for HandleContext.");
			}

			context->_identityHashCode = peer.JniIdentityHashCode;
			context->_controlBlock = peer.JniObjectReferenceControlBlock;

			GCHandle handle = JavaMarshal.CreateReferenceTrackingHandle (peer, context);
			context->_gcHandle = GCHandle.ToIntPtr (handle);

#if DEBUG
			lock (s_knownContexts) {
				s_knownContexts.Add ((IntPtr)context);
			}
#endif

			return context;
		}

		public static void Free (ref HandleContext* context)
		{
			if (context == null) {
				return;
			}

#if DEBUG
			lock (s_knownContexts) {
				s_knownContexts.Remove ((IntPtr)context);
			}
#endif

			NativeMemory.Free (context);
			context = null;
		}
	}

	[UnmanagedCallersOnly]
	static unsafe void BridgeProcessingStarted (MarkCrossReferencesArgs* mcr)
	{
		if (mcr == null) {
			throw new ArgumentNullException (nameof (mcr), "MarkCrossReferencesArgs should never be null.");
		}

#if DEBUG
		HandleContext.EnsureAllContextsAreOurs (mcr);
#endif
	}

	[UnmanagedCallersOnly]
	static unsafe void BridgeProcessingFinished (MarkCrossReferencesArgs* mcr)
	{
		if (mcr == null) {
			throw new ArgumentNullException (nameof (mcr), "MarkCrossReferencesArgs should never be null.");
		}

		ReadOnlySpan<GCHandle> handlesToFree = ProcessCollectedContexts (mcr);
		JavaMarshal.FinishCrossReferenceProcessing (mcr, handlesToFree);

		// Schedule cleanup of _registeredInstances on a thread pool thread.
		// The bridge thread must not take lock(_registeredInstances) — see deadlock notes.
		Task.Run (GetOrCreateInstance ().CollectPeers);
	}

	static unsafe ReadOnlySpan<GCHandle> ProcessCollectedContexts (MarkCrossReferencesArgs* mcr)
	{
		List<GCHandle> handlesToFree = [];
		ManagedValueManager instance = GetOrCreateInstance ();

		for (int i = 0; (nuint)i < mcr->ComponentCount; i++) {
			StronglyConnectedComponent component = mcr->Components [i];
			for (int j = 0; (nuint)j < component.Count; j++) {
				ProcessContext ((HandleContext*)component.Contexts [j]);
			}
		}

		void ProcessContext (HandleContext* context)
		{
			if (context == null) {
				throw new ArgumentNullException (nameof (context), "HandleContext should never be null.");
			}

			// Ignore contexts which were not collected
			if (!context->IsCollected) {
				return;
			}

			GCHandle handle = context->AssociatedGCHandle;

			// Note: modifying the _registeredInstances dictionary while processing the collected contexts
			// is tricky and can lead to deadlocks, so we remember which contexts were collected and we will free
			// them later outside of the bridge processing loop.
			instance._collectedContexts.Enqueue ((IntPtr)context);

			// important: we must not free the handle before passing it to JavaMarshal.FinishCrossReferenceProcessing
			handlesToFree.Add (handle);
		}

		return CollectionsMarshal.AsSpan (handlesToFree);
	}

	const BindingFlags ActivationConstructorBindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

	static  readonly    Type[]  XAConstructorSignature  = new Type [] { typeof (IntPtr), typeof (JniHandleOwnership) };

	protected override bool TryConstructPeer (
			IJavaPeerable self,
			ref JniObjectReference reference,
			JniObjectReferenceOptions options,
			[DynamicallyAccessedMembers (Constructors)]
			Type type)
	{
		var c = type.GetConstructor (ActivationConstructorBindingFlags, null, XAConstructorSignature, null);
		if (c != null) {
			var args = new object[] {
				reference.Handle,
				JniHandleOwnership.DoNotTransfer,
			};
			c.Invoke (self, args);
			JniObjectReference.Dispose (ref reference, options);
			return true;
		}
		return base.TryConstructPeer (self, ref reference, options, type);
	}

	protected override bool TryUnboxPeerObject (IJavaPeerable value, [NotNullWhen (true)] out object? result)
	{
		var proxy = value as JavaProxyThrowable;
		if (proxy != null) {
			result = proxy.InnerException;
			return true;
		}
		return base.TryUnboxPeerObject (value, out result);
	}
}
