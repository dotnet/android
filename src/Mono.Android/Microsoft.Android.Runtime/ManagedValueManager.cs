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
using Android.Runtime;
using Java.Interop;

namespace Microsoft.Android.Runtime;

class ManagedValueManager : JniRuntime.JniValueManager
{
	const DynamicallyAccessedMemberTypes Constructors = DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors;

	readonly ITypeMap typeMap;

	readonly Dictionary<int, List<ReferenceTrackingHandle>> RegisteredInstances = new ();
	readonly ConcurrentQueue<IntPtr> CollectedContexts = new ();

	bool disposed;

	static readonly SemaphoreSlim bridgeProcessingSemaphore = new (1, 1);
	static bool gcBridgeInitialized = false;

	public ManagedValueManager (ITypeMap typeMap)
	{
		ArgumentNullException.ThrowIfNull (typeMap);
		this.typeMap = typeMap;

		InitializeGCBridge ();
	}

	static unsafe void InitializeGCBridge ()
	{
		lock (bridgeProcessingSemaphore) {
			if (gcBridgeInitialized) {
				throw new InvalidOperationException ("GC bridge has already been initialized.");
			}

			gcBridgeInitialized = true;
		}

		var mark_cross_references_ftn = RuntimeNativeMethods.clr_initialize_gc_bridge (&BridgeProcessingStarted, &BridgeProcessingFinished);
		JavaMarshal.Initialize (mark_cross_references_ftn);
	}

	protected override void Dispose (bool disposing)
	{
		disposed = true;
		base.Dispose (disposing);
	}

	void ThrowIfDisposed ()
	{
		if (disposed)
			throw new ObjectDisposedException (nameof (ManagedValueManager));
	}

	public override void WaitForGCBridgeProcessing ()
	{
		bridgeProcessingSemaphore.Wait ();
		bridgeProcessingSemaphore.Release ();
	}

	public unsafe override void CollectPeers ()
	{
		ThrowIfDisposed ();

		while (CollectedContexts.TryDequeue (out IntPtr contextPtr)) {
			Debug.Assert (contextPtr != IntPtr.Zero, "CollectedContexts should not contain null pointers.");
			HandleContext* context = (HandleContext*)contextPtr;
			
			lock (RegisteredInstances) {
				Remove (context);
			}

			HandleContext.Free (ref context);
		}

		void Remove (HandleContext* context)
		{
			int key = context->PeerIdentityHashCode;
			if (!RegisteredInstances.TryGetValue (key, out List<ReferenceTrackingHandle>? peers))
				return;

			for (int i = peers.Count - 1; i >= 0; i--) {
				var peer = peers [i];
				if (peer.BelongsToContext (context)) {
					peers.RemoveAt (i);
				}
			}

			if (peers.Count == 0) {
				RegisteredInstances.Remove (key);
			}
		}
	}

	public override void AddPeer (IJavaPeerable value)
	{
		ThrowIfDisposed ();

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
		lock (RegisteredInstances) {
			List<ReferenceTrackingHandle>? peers;
			if (!RegisteredInstances.TryGetValue (key, out peers)) {
				peers = [new ReferenceTrackingHandle (value)];
				RegisteredInstances.Add (key, peers);
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

		lock (RegisteredInstances) {
			if (!RegisteredInstances.TryGetValue (key, out List<ReferenceTrackingHandle>? peers))
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

	public override void RemovePeer (IJavaPeerable value)
	{
		ThrowIfDisposed ();

		// Remove any collected contexts before modifying RegisteredInstances
		CollectPeers ();

		if (value == null)
			throw new ArgumentNullException (nameof (value));

		lock (RegisteredInstances) {
			int key = value.JniIdentityHashCode;
			if (!RegisteredInstances.TryGetValue (key, out List<ReferenceTrackingHandle>? peers))
				return;

			for (int i = peers.Count - 1; i >= 0; i--) {
				ReferenceTrackingHandle peer = peers [i];
				IJavaPeerable target = peer.Target;
				if (ReferenceEquals (value, target)) {
					peers.RemoveAt (i);
					peer.Dispose ();
				}
				GC.KeepAlive (target);
			}
			if (peers.Count == 0)
				RegisteredInstances.Remove (key);
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
		var declType = cinfo.DeclaringType;
		if (declType == null) {
			throw new NotSupportedException ("ConstructorInfo.DeclaringType is null - cannot determine type to activate.");
		}

		// If the type is already a JavaPeerProxy, use it directly
		if (typeof (JavaPeerProxy).IsAssignableFrom (declType)) {
			ActivateViaProxy (reference, declType);
			return;
		}

		// The type is not a proxy - look up the proxy from TypeMap
		// This happens when the TypeManager returns the original type instead of the proxy
		var proxy = typeMap.GetProxyForManagedType (declType);
		if (proxy != null) {
			var handle = reference.Handle;
			var peer = proxy.CreateInstance (handle, JniHandleOwnership.DoNotTransfer);
			if (peer == null) {
				throw new InvalidOperationException ($"Proxy for {declType.FullName}.CreateInstance returned null.");
			}
			return;
		}

		// No proxy found - this should not happen with the trimmable type map
		// Fall back to reflection-based activation only if dynamic type registration is enabled
		if (!RuntimeFeature.IsDynamicTypeRegistration) {
			throw new NotSupportedException (
				$"Cannot activate type '{declType.FullName}' - no proxy found in TypeMap and dynamic type registration is disabled. " +
				"Ensure the type is included in the TypeMap at build time.");
		}

		ActivateViaReflection (reference, cinfo, argumentValues);
	}

	void ActivateViaProxy (JniObjectReference reference, Type proxyType)
	{
		// Get the proxy instance from the proxy type (which has self-applied attribute)
		var proxy = proxyType.GetCustomAttribute<JavaPeerProxy> (inherit: false);
		if (proxy == null) {
			throw new InvalidOperationException ($"Proxy type {proxyType.FullName} does not have a JavaPeerProxy attribute.");
		}

		// Create the peer instance using the proxy's CreateInstance method
		var handle = reference.Handle;
		var peer = proxy.CreateInstance (handle, JniHandleOwnership.DoNotTransfer);
		if (peer == null) {
			throw new InvalidOperationException ($"Proxy {proxyType.FullName}.CreateInstance returned null.");
		}

		// The instance should already have its peer reference set by CreateInstance
		// (via the activation constructor it calls internally)
	}

	[RequiresUnreferencedCode ("Reflection-based activation cannot statically determine the type from ConstructorInfo.DeclaringType.")]
	void ActivateViaReflection (JniObjectReference reference, ConstructorInfo cinfo, object?[]? argumentValues)
	{
		var declType  = cinfo.DeclaringType ?? throw new NotSupportedException ("Do not know the type to create!");

		var self      = (IJavaPeerable) System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject (declType);
		self.SetPeerReference (reference);

		cinfo.Invoke (self, argumentValues);
	}

	public override List<JniSurfacedPeerInfo> GetSurfacedPeers ()
	{
		ThrowIfDisposed ();

		// Remove any collected contexts before iterating over all the registered instances
		CollectPeers ();

		lock (RegisteredInstances) {
			var peers = new List<JniSurfacedPeerInfo> (RegisteredInstances.Count);
			foreach (var (identityHashCode, referenceTrackingHandles) in RegisteredInstances) {
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
		WeakReference<IJavaPeerable> _weakReference;
		HandleContext* _context;

		public bool BelongsToContext (HandleContext* context)
			=> _context == context;

		public ReferenceTrackingHandle (IJavaPeerable peer)
		{
			_context = HandleContext.Alloc (peer);
			_weakReference = new WeakReference<IJavaPeerable> (peer);
		}

		public IJavaPeerable? Target
			=> _weakReference.TryGetTarget (out var target) ? target : null;

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

	[StructLayout (LayoutKind.Sequential)]
	unsafe struct HandleContext
	{
		static readonly nuint Size = (nuint)Marshal.SizeOf<HandleContext> ();
		static readonly Dictionary<IntPtr, GCHandle> referenceTrackingHandles = new ();

		int identityHashCode;
		IntPtr controlBlock;

		public int PeerIdentityHashCode => identityHashCode;
		public bool IsCollected
		{
			get
			{
				if (controlBlock == IntPtr.Zero)
					throw new InvalidOperationException ("HandleContext control block is not initialized.");

				return ((JniObjectReferenceControlBlock*) controlBlock)->handle == IntPtr.Zero;
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

		public static GCHandle GetAssociatedGCHandle (HandleContext* context)
		{
			lock (referenceTrackingHandles) {
				if (!referenceTrackingHandles.TryGetValue ((IntPtr) context, out GCHandle handle)) {
					throw new InvalidOperationException ("Unknown reference tracking handle.");
				}

				return handle;
			}
		}

		public static unsafe void EnsureAllContextsAreOurs (MarkCrossReferencesArgs* mcr)
		{
			lock (referenceTrackingHandles) {
				for (nuint i = 0; i < mcr->ComponentCount; i++) {
					StronglyConnectedComponent component = mcr->Components [i];
					EnsureAllContextsInComponentAreOurs (component);
				}
			}

			static void EnsureAllContextsInComponentAreOurs (StronglyConnectedComponent component)
			{
				for (nuint i = 0; i < component.Count; i++) {
					EnsureContextIsOurs ((IntPtr)component.Contexts [i]);
				}
			}

			static void EnsureContextIsOurs (IntPtr context)
			{
				if (!referenceTrackingHandles.ContainsKey (context)) {
					throw new InvalidOperationException ("Unknown reference tracking handle.");
				}
			}	
		}

		public static HandleContext* Alloc (IJavaPeerable peer)
		{
			var context = (HandleContext*) NativeMemory.AllocZeroed (1, Size);
			if (context == null) {
				throw new OutOfMemoryException ("Failed to allocate memory for HandleContext.");
			}

			context->identityHashCode = peer.JniIdentityHashCode;
			context->controlBlock = peer.JniObjectReferenceControlBlock;

			GCHandle handle = JavaMarshal.CreateReferenceTrackingHandle (peer, context);
			lock (referenceTrackingHandles) {
				referenceTrackingHandles [(IntPtr) context] = handle;
			}

			return context;
		}

		public static void Free (ref HandleContext* context)
		{
			if (context == null) {
				return;
			}

			lock (referenceTrackingHandles) {
				referenceTrackingHandles.Remove ((IntPtr)context);
			}

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

		HandleContext.EnsureAllContextsAreOurs (mcr);
		bridgeProcessingSemaphore.Wait ();
	}

	[UnmanagedCallersOnly]
	static unsafe void BridgeProcessingFinished (MarkCrossReferencesArgs* mcr)
	{
		if (mcr == null) {
			throw new ArgumentNullException (nameof (mcr), "MarkCrossReferencesArgs should never be null.");
		}

		var instance = JNIEnvInit.androidRuntime?.ValueManager as ManagedValueManager
			?? throw new InvalidOperationException ("The ManagedValueManager instance is not available.");

		ReadOnlySpan<GCHandle> handlesToFree = instance.ProcessCollectedContexts (mcr);
		JavaMarshal.FinishCrossReferenceProcessing (mcr, handlesToFree);

		bridgeProcessingSemaphore.Release ();
	}

	unsafe ReadOnlySpan<GCHandle> ProcessCollectedContexts (MarkCrossReferencesArgs* mcr)
	{
		List<GCHandle> handlesToFree = [];

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

			GCHandle handle = HandleContext.GetAssociatedGCHandle (context);

			// Note: modifying the RegisteredInstances dictionary while processing the collected contexts
			// is tricky and can lead to deadlocks, so we remember which contexts were collected and we will free
			// them later outside of the bridge processing loop.
			CollectedContexts.Enqueue ((IntPtr)context);

			// important: we must not free the handle before passing it to JavaMarshal.FinishCrossReferenceProcessing
			handlesToFree.Add (handle);
		}

		return CollectionsMarshal.AsSpan (handlesToFree);
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

	public override IJavaPeerable? CreatePeer (
		ref JniObjectReference reference,
		JniObjectReferenceOptions options,
		[DynamicallyAccessedMembers (
			DynamicallyAccessedMemberTypes.PublicConstructors |
			DynamicallyAccessedMemberTypes.NonPublicConstructors)]
		Type? targetType)
	{
		if (!reference.IsValid)
			return null;

		var peer = typeMap.CreatePeer (reference.Handle, JniHandleOwnership.DoNotTransfer, targetType) as IJavaPeerable;
		JniObjectReference.Dispose (ref reference, options);
		return peer;
	}
}
