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
/// which spawns a detached bridge-processing thread and aborts the process if it runs more
/// than once. <see cref="InitializeIfNeeded"/> is idempotent: the first call performs the
/// initialization and any subsequent call returns immediately, so it is safe to call from
/// every value manager (e.g. the <c>llvm-ir</c> and <c>trimmable-typemap</c> implementations).
/// </para>
/// <para>
/// The GC-bridge registration lives for the entire lifetime of the process and is never torn
/// down: stopping the detached bridge-processing thread is not supported by the runtime.
/// </para>
/// </remarks>
static class JavaMarshalRegisteredPeers
{
	static readonly Dictionary<int, List<ReferenceTrackingHandle>> RegisteredInstances = new ();
	static readonly ConcurrentQueue<IntPtr> CollectedContexts = new ();

	static readonly object initializeLock = new ();
	static bool initialized;

	/// <summary>
	/// Signaled whenever GC bridge processing is *not* in progress.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This is the CoreCLR/NativeAOT equivalent of Mono's
	/// <c>mono_gc_wait_for_bridge_processing()</c>, which blocks on the SGen GC lock held
	/// for the duration of a bridge round. A bridge round flips every registered peer's
	/// global reference strong-&gt;weak, asks ART to collect
	/// (<c>java.lang.Runtime.gc()</c>), and then flips the survivors back. The wait exists
	/// to match Mono's semantics: threads entering managed code from Java do not observe
	/// peers mid-flip.
	/// </para>
	/// <para>
	/// It is *not* a performance optimization. It was measured on a MAUI/SkiaSharp
	/// benchmark (Pixel 5, arm64, 60 Hz, two runs per arm) with the barrier enabled and
	/// disabled, and it made no useful difference: ART "Explicit concurrent copying GC"
	/// wall time was 24.7/27.1 ms enabled vs 24.6/21.6 ms disabled, worst frame gap
	/// 53.6/53.6 ms vs 54.3/57.4 ms. Both arms were far from the Mono baseline on the same
	/// SDK (10.8-11.5 ms GC, 33.5 ms worst gap) even though the JNI global reference count
	/// was identical (~840) on all arms. Whatever makes an ART bridge collection ~2x more
	/// expensive under CoreCLR, it is not mutator activity during the round.
	/// </para>
	/// <para>
	/// <see cref="ManualResetEventSlim"/> is constructed with <c>spinCount: 0</c> on
	/// purpose: the goal is to park the waiting thread rather than burn CPU that ART's
	/// collector could use.
	/// </para>
	/// </remarks>
	static readonly ManualResetEventSlim bridgeProcessingFinished = new (initialState: true, spinCount: 0);

	/// <summary>
	/// <see cref="Environment.CurrentManagedThreadId"/> of the bridge processing thread
	/// while a round is in progress, otherwise 0.
	/// </summary>
	static int bridgeProcessingThreadId;

	/// <summary>
	/// Blocks until any in-progress GC bridge round completes.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Like Mono's implementation, this is inherently racy: a thread that observes "no
	/// bridge processing" can still be preempted by a round that begins immediately
	/// afterwards. The wait narrows the window, it does not close it.
	/// </para>
	/// <para>
	/// In practice exactly one thread parks per round in a typical UI app (measured:
	/// 15-36 ms per round), so this trades one unbounded stall for one bounded one
	/// without reducing total GC cost.
	/// </para>
	/// </remarks>
	internal static void WaitForBridgeProcessing ()
	{
		if (!RuntimeFeature.WaitForGCBridgeProcessing)
			return;

		var finished = bridgeProcessingFinished;
		if (finished.IsSet)
			return;

		// The bridge thread runs BridgeProcessingStarted/Finished itself, so it must
		// never wait on its own completion signal.
		if (Environment.CurrentManagedThreadId == Volatile.Read (ref bridgeProcessingThreadId))
			return;

		finished.Wait ();
	}

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

			unsafe {
				var mark_cross_references_ftn = RuntimeNativeMethods.clr_initialize_gc_bridge (
					&BridgeProcessingStarted, &BridgeProcessingFinished);
				JavaMarshal.Initialize (mark_cross_references_ftn);
			}

			initialized = true;
		}
	}

	public static void CollectPeers ()
	{
		unsafe {
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

	public static void RemovePeer (IJavaPeerable value)
	{
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
				IJavaPeerable? target = peer.Target;
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
		[StructLayout (LayoutKind.Sequential)]
		private struct JniObjectReferenceControlBlock
		{
			public IntPtr handle;
			public int handle_type;
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
// This call site is reachable on all platforms. 'MarkCrossReferencesArgs.ComponentCount' is only supported on: 'android'.
// This call site is reachable on all platforms. 'MarkCrossReferencesArgs.Components' is only supported on: 'android'.
// This call site is reachable on all platforms. 'StronglyConnectedComponent.Count' is only supported on: 'android'.
// This call site is reachable on all platforms. 'StronglyConnectedComponent.Contexts' is only supported on: 'android'.
#pragma warning disable CA1416

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

#pragma warning restore CA1416
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

		// Publish the thread id before clearing the signal so that a reentrant call on
		// this thread can never observe an unsignaled event with a stale id.
		Volatile.Write (ref bridgeProcessingThreadId, Environment.CurrentManagedThreadId);
		bridgeProcessingFinished.Reset ();

		HandleContext.EnsureAllContextsAreOurs (mcr);
	}

	[UnmanagedCallersOnly]
	static unsafe void BridgeProcessingFinished (MarkCrossReferencesArgs* mcr)
	{
		if (mcr == null) {
			throw new ArgumentNullException (nameof (mcr), "MarkCrossReferencesArgs should never be null.");
		}

		try {
			ReadOnlySpan<GCHandle> handlesToFree = ProcessCollectedContexts (mcr);

// This call site is reachable on all platforms. 'JavaMarshal.FinishCrossReferenceProcessing(MarkCrossReferencesArgs*, ReadOnlySpan<GCHandle>)' is only supported on: 'android'.
#pragma warning disable CA1416
			JavaMarshal.FinishCrossReferenceProcessing (mcr, handlesToFree);
#pragma warning restore CA1416
		} finally {
			// Must run even if processing throws, otherwise every thread that entered
			// managed code from Java would block forever.
			Volatile.Write (ref bridgeProcessingThreadId, 0);
			bridgeProcessingFinished.Set ();
		}
	}

	static unsafe ReadOnlySpan<GCHandle> ProcessCollectedContexts (MarkCrossReferencesArgs* mcr)
	{
		List<GCHandle> handlesToFree = [];

// This call site is reachable on all platforms. 'MarkCrossReferencesArgs.ComponentCount' is only supported on: 'android'.
// This call site is reachable on all platforms. 'MarkCrossReferencesArgs.Components' is only supported on: 'android'.
// This call site is reachable on all platforms. 'StronglyConnectedComponent.Count' is only supported on: 'android'.
// This call site is reachable on all platforms. 'StronglyConnectedComponent.Contexts' is only supported on: 'android'.
#pragma warning disable CA1416

		for (int i = 0; (nuint)i < mcr->ComponentCount; i++) {
			StronglyConnectedComponent component = mcr->Components [i];
			for (int j = 0; (nuint)j < component.Count; j++) {
				ProcessContext ((HandleContext*)component.Contexts [j]);
			}
		}

#pragma warning restore CA1416

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

}
