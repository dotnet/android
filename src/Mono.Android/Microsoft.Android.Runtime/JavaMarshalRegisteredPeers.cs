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

	/// <summary>
	/// Diagnostics for dotnet/android#10973. The registry only ever disagrees with a cached
	/// reference such as <c>Application.Context</c> if a *second* managed peer is created for
	/// the same Java instance. Record the two places <see cref="AddPeer"/> observes that -- an
	/// existing entry being replaced, and a second entry being appended because the existing
	/// one's weak reference was cleared -- so a failing test can report which happened, to
	/// whom, and how often.
	///
	/// Published via <see cref="AppContext"/> so tests in other assemblies can read them
	/// without needing <c>InternalsVisibleTo</c>. Recording only: behaviour is unchanged.
	/// </summary>
	internal const string LiveEvictionsKey = "Microsoft.Android.Runtime.LivePeerEvictions";
	internal const string ReplacementsKey = "Microsoft.Android.Runtime.PeerReplacements";
	internal const string DuplicateAppendsKey = "Microsoft.Android.Runtime.PeerDuplicateAppends";

	static int liveEvictions;
	static int replacements;
	static int duplicateAppends;

	static string Id (IJavaPeerable peer)
		=> $"{peer.GetType ().FullName}@managed-0x{RuntimeHelpers.GetHashCode (peer).ToString ("x", CultureInfo.InvariantCulture)}/{peer.PeerReference}";

	static void RecordIfLive (int key, ReferenceTrackingHandle peer)
	{
		if (peer.Target is not IJavaPeerable live)
			return;

		int count = Interlocked.Increment (ref liveEvictions);
		AppContext.SetData (LiveEvictionsKey,
				$"{count} (most recent: key=0x{key.ToString ("x", CultureInfo.InvariantCulture)} {Id (live)})");
		GC.KeepAlive (live);
	}

	static void RecordReplacement (int key, IJavaPeerable replaced, IJavaPeerable replacement)
	{
		int count = Interlocked.Increment (ref replacements);
		AppContext.SetData (ReplacementsKey,
				$"{count} (most recent: key=0x{key.ToString ("x", CultureInfo.InvariantCulture)} " +
				$"replaced {Id (replaced)} with {Id (replacement)})");
	}

	static void RecordDuplicateAppend (int key, IJavaPeerable value, int existing)
	{
		int count = Interlocked.Increment (ref duplicateAppends);
		AppContext.SetData (DuplicateAppendsKey,
				$"{count} (most recent: key=0x{key.ToString ("x", CultureInfo.InvariantCulture)} " +
				$"appended {Id (value)} alongside {existing} existing entr{(existing == 1 ? "y" : "ies")})");
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
						RecordIfLive (key, peer);
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
				// JNIEnv.NewObject/JNIEnv.CreateInstance() compatibility.
				// When two MCWs are created for one Java instance [0], we want the 2nd
				// MCW to replace the 1st, as the 2nd is the one the dev created; the
				// 1st is an implicit intermediary.
				//
				// Meanwhile, a new "replaceable" instance must *not* replace an existing
				// "replaceable" instance; see dotnet/android#9862. Doing so orphans any
				// already-cached reference to the existing peer -- notably
				// `Application.Context`, which caches the peer it first sees and never
				// re-reads it -- so `PeekPeer()` then hands out a different peer for the
				// same Java instance; see dotnet/android#10973.
				//
				// This mirrors `AndroidRuntime.JavaObjectValueManager`; MonoVM has always
				// had the second condition, which is why this only ever fails on CoreCLR.
				//
				// [0]: If a Java ctor invokes an overridden virtual method, we transition
				// into managed code w/o a registered instance, and thus create an
				// "intermediary" via the (IntPtr, JniHandleOwnership) .ctor.
				if (target.JniManagedPeerState.HasFlag (JniManagedPeerStates.Replaceable) &&
						!value.JniManagedPeerState.HasFlag (JniManagedPeerStates.Replaceable)) {
					RecordReplacement (key, target, value);
					peer.Dispose ();
					peers [i] = new ReferenceTrackingHandle (value);
				} else if (JniEnvironment.Runtime.ObjectReferenceManager.LogGlobalReferenceMessages) {
					// Now a normal startup path rather than a rare one, and the arguments
					// below are evaluated eagerly -- including two JNI round-trips for
					// `GetJniTypeNameFromInstance()` -- even though
					// `WriteGlobalReferenceLine()` discards them when logging is off.
					WarnNotReplacing (key, value, target);
				}
				GC.KeepAlive (target);
				return;
			}

			RecordDuplicateAppend (key, value, peers.Count);
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

		HandleContext.EnsureAllContextsAreOurs (mcr);
	}

	[UnmanagedCallersOnly]
	static unsafe void BridgeProcessingFinished (MarkCrossReferencesArgs* mcr)
	{
		if (mcr == null) {
			throw new ArgumentNullException (nameof (mcr), "MarkCrossReferencesArgs should never be null.");
		}

		ReadOnlySpan<GCHandle> handlesToFree = ProcessCollectedContexts (mcr);

// This call site is reachable on all platforms. 'JavaMarshal.FinishCrossReferenceProcessing(MarkCrossReferencesArgs*, ReadOnlySpan<GCHandle>)' is only supported on: 'android'.
#pragma warning disable CA1416
		JavaMarshal.FinishCrossReferenceProcessing (mcr, handlesToFree);
#pragma warning restore CA1416
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
